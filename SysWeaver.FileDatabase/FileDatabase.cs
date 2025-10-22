using SimpleStack.Orm;
using SimpleStack.Orm.Expressions.Statements.Typed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Db;

namespace SysWeaver.FileDatabase
{

    public sealed class FileDatabase<T> : IDisposable, IHaveStats, IPerfMonitored where T : DbFileData, new()
    {

        public override string ToString()
        {
            var f = Folders;
            if (f == null)
                return SysName;
            var fl = f.Length;
            return String.Concat(SysName, " monitoring ", fl, fl == 1 ? " folder:" : " folders: ", String.Join(", ", f.Select(x => x.ToQuoted())));
        }

        /// <summary>
        /// Return false to ignore this file (will no be present)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public delegate Task<bool> UpdateEntry(T data);

        public FileDatabase(IMessageHost msg, DbSimpleStack db, UpdateEntry entryFn, Func<String, String> isMetaData = null, String instanceName = null)
        {
            Db = db;
            Msg = msg;
            EntryFn = entryFn;
            IsMetaData = isMetaData ?? (x => null);
            var name = "FileDatabase." + (instanceName ?? typeof(T).Name);
            SysName = name;
            PerfMon = new PerfMonitor(SysName);
            Prefix = "[" + name + "] ";
        }
        public PerfMonitor PerfMon { get; init; }

        readonly Func<String, String> IsMetaData;
        readonly IMessageHost Msg;
        readonly String Prefix;
        readonly String SysName;
        String[] Folders;
        StringTree ValidFolders;
        readonly UpdateEntry EntryFn;
        readonly DbSimpleStack Db;


        readonly ExceptionTracker GenericFails = new ExceptionTracker();
        readonly ExceptionTracker UpdateFails = new ExceptionTracker();
        readonly ExceptionTracker InsertFails = new ExceptionTracker();
        readonly ExceptionTracker DeleteFails = new ExceptionTracker();
        readonly ExceptionTracker LengthFails = new ExceptionTracker();




        /// <summary>
        /// Must be called once after construction, can be slow since it will bring everything up to date
        /// </summary>
        /// <returns></returns>
        public async Task<List<T>> Init(params String[] folders)
        {
            using var _ = PerfMon.Track(nameof(Init));
            var msg = Msg;
            List<T> allList = new List<T>();
            msg?.AddMessage(Prefix + "Syncing file system with database");
            using (msg?.Tab())
            {
                var db = Db;
                using var cons = ObjectPool.CreateAsync(() => db.GetAsync());
                using (var c = await cons.Alloc().ConfigureAwait(false))
                    await db.InitTable<T>(c).ConfigureAwait(false);
                if (folders != null)
                {
                    StringTree validFolders = null;
                    var fc = folders.Length;
                    var ws = new List<FileSystemWatcher>(fc);
                    var newFolders = new List<String>(fc);
                    var entryFn = EntryFn;
                    ConcurrentDictionary<String, T> all = new ConcurrentDictionary<string, T>(StringComparer.Ordinal);
                    using (var c = await cons.Alloc().ConfigureAwait(false))
                        foreach (var x in await ((OrmConnection)c).SelectAsync<T>().ConfigureAwait(false))
                        {
                            all[x.FullPath] = x;
                            allList.Add(x);
                        }
                    List<Task> tasks = new List<Task>();
                    using var maxConc = new SemaphoreSlim(10, 10);

                    async Task addNew(FileInfo fi, string ext)
                    {
                        await maxConc.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            var data = new T
                            {
                                FullPath = fi.FullName,
                                Ext = ext.FastToLower(),
                                LastModified = fi.LastWriteTimeUtc.Ticks,
                                Size = fi.Length,
                                Changed = DateTime.UtcNow.Ticks,
                            };
                            if (!await entryFn(data).ConfigureAwait(false))
                                return;
                            using (var c = await cons.Alloc().ConfigureAwait(false))
                                await ((OrmConnection)c).InsertAsync(data).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            InsertFails.OnException(ex);
                        }
                        finally
                        {
                            maxConc.Release();
                        }
                        all.TryRemove(fi.FullName, out var _);
                    }

                    async Task update(T data, FileInfo fi)
                    {
                        await maxConc.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            data.LastModified = fi.LastWriteTimeUtc.Ticks;
                            data.Size = fi.Length;
                            data.Changed = DateTime.UtcNow.Ticks;
                            if (!await entryFn(data).ConfigureAwait(false))
                                return;
                            using (var c = await cons.Alloc().ConfigureAwait(false))
                                await ((OrmConnection)c).UpdateAsync(data).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            UpdateFails.OnException(ex);
                        }
                        finally
                        {
                            maxConc.Release();
                        }
                        all.TryRemove(fi.FullName, out var _);
                    }

                    async Task delete(T data)
                    {
                        await maxConc.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            using (var c = await cons.Alloc().ConfigureAwait(false))
                                await ((OrmConnection)c).DeleteAsync(data).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            DeleteFails.OnException(ex);
                        }
                        finally
                        {
                            maxConc.Release();
                        }
                    }

                    foreach (var xx in folders.OrderBy(x => x.Length))
                    {
                        var folderName = xx;
                        var di = new DirectoryInfo(folderName.TrimEnd(Path.DirectorySeparatorChar));
                        if (!di.Exists)
                        {
                            msg?.AddMessage(Prefix + "Folder " + folderName.ToQuoted() + " doesn't exist, ignoring!", MessageLevels.Warning);
                            continue;
                        }
                        var folder = di.FullName;
                        folderName = folder + Path.DirectorySeparatorChar;
                        if (validFolders != null)
                        {
                            var pex = validFolders.StartsWithAny(folderName);
                            if (pex != null)
                            {
                                msg?.AddMessage(Prefix + "Folder " + folderName.ToQuoted() + " is already covered by folder " + pex.ToQuoted() + ", ignoring!", MessageLevels.Warning);
                                continue;
                            }
                        }
#if DEBUG
                        msg?.AddMessage(Prefix + "Syncing folder " + folderName.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG

                        validFolders = StringTree.Add(folderName, false, validFolders);
                        newFolders.Add(folderName);
                        //  Start listening for changes
                        var w = new FileSystemWatcher(folder);
                        w.IncludeSubdirectories = true;
                        w.Changed += W_Changed;
                        w.Created += W_Created;
                        w.Deleted += W_Deleted;
                        w.Renamed += W_Renamed;
                        w.Error += W_Error;
                        w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName | NotifyFilters.CreationTime;
                        //w.Filter = "*.*";
                        w.EnableRaisingEvents = true;
                        ws.Add(w);
                        //  Update current state
                        foreach (var x in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            var fi = new FileInfo(x);
                            var fname = fi.FullName;
                            if (fname.Length > DbFileData.MaxNameLen)
                            {
                                LengthFails.OnException(new Exception("Length of filename " + fname.ToQuoted() + " exceeds the maxium, ignored"));
                                continue;
                            }
                            var ext = fi.Extension;
                            var el = ext.Length;
                            if (el > (DbFileData.MaxExtLen + 1))
                            {
                                LengthFails.OnException(new Exception("Length of file extension for " + fname.ToQuoted() + " exceeds the maxium, ignored"));
                                continue;
                            }
                            if (el > 0)
                                ext = ext.Substring(1);

                            if (!all.TryGetValue(fname, out var data))
                            {
                                tasks.Add(addNew(fi, ext));
                                continue;
                            }
                            if ((data.LastModified == fi.LastWriteTimeUtc.Ticks) && (data.Size == fi.Length))
                            {
                                all.TryRemove(fname, out var _);
                                continue;
                            }
                            tasks.Add(update(data, fi));
                        }
                    }
                    if (tasks.Count > 0)
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    tasks = all.Select(x => delete(x.Value)).ToList();
                    if (tasks.Count > 0)
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    ValidFolders = validFolders;
                    Watchers = ws.ToArray();
                    Folders = newFolders.ToArray();
                }
                UpdateTask = new PeriodicTask(ProcessUpdates, 100, true, true);
            }
            return allList;
        }


        private void W_Error(object sender, ErrorEventArgs e)
        {
#if DEBUG
            Msg?.AddMessage(Prefix + "File watch error: " + e.GetException(), MessageLevels.Debug);
#endif//DEBUG
            GenericFails.OnException(e.GetException());
        }

        PeriodicTask UpdateTask;

        void W_Renamed(object sender, RenamedEventArgs e)
        {
            try
            {
                var name = e.FullPath;
                var oname = e.OldFullPath;
                var valid = ValidFolders;
                if ((File.GetAttributes(name) & FileAttributes.Directory) != 0)
                {
                    bool doCreate = valid.StartsWithAny(name + Path.DirectorySeparatorChar) != null;
                    bool doDelete = valid.StartsWithAny(oname + Path.DirectorySeparatorChar) != null;
#if DEBUG
                    Msg?.AddMessage(Prefix + "Renamed directory " + name.ToQuoted() + " from " + oname.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                    var dl = name.Length + 1;
                    foreach (var file in Directory.GetFiles(name, "*", SearchOption.AllDirectories))
                    {
                        if (doDelete)
                            Updates.Enqueue(new Update(Path.Combine(oname, file.Substring(dl)) , true));
                        if (doCreate)
                            Updates.Enqueue(new Update(file, false));
                    }
                    return;
                }
#if DEBUG
                Msg?.AddMessage(Prefix + "Renamed file " + name.ToQuoted() + " from " + oname.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                if (valid.StartsWithAny(oname) != null)
                    Updates.Enqueue(new Update(oname, true));
                if (valid.StartsWithAny(name) != null)
                    Updates.Enqueue(new Update(name, false));
            }
            catch (Exception ex)
            {
                GenericFails.OnException(ex);
            }
        }

        void W_Deleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                var name = e.FullPath;
#if DEBUG
                Msg?.AddMessage(Prefix + "Deleted file (or folder) " + name.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                Updates.Enqueue(new Update(e.FullPath, true));
            }
            catch (Exception ex)
            {
                GenericFails.OnException(ex);
            }
        }

        void W_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                var name = e.FullPath;
                if ((File.GetAttributes(name) & FileAttributes.Directory) != 0)
                {
#if DEBUG
                    Msg?.AddMessage(Prefix + "Created folder " + name.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                    foreach (var x in Directory.GetFiles(name, "*", SearchOption.AllDirectories))
                        Updates.Enqueue(new Update(x, false));
                    return;
                }
#if DEBUG
                Msg?.AddMessage(Prefix + "Created file " + name.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                Updates.Enqueue(new Update(name, false));
            }
            catch (Exception ex)
            {
                GenericFails.OnException(ex);
            }
        }

        void W_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                var name = e.FullPath;
                if ((File.GetAttributes(name) & FileAttributes.Directory) != 0)
                    return;
#if DEBUG
                Msg?.AddMessage(Prefix + "Changed file " + name.ToQuoted(), MessageLevels.Debug);
#endif//DEBUG
                Updates.Enqueue(new Update(name, false));
            }
            catch (Exception ex)
            {
                GenericFails.OnException(ex);
            }
        }

        FileSystemWatcher[] Watchers;

        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
            var ws = Interlocked.Exchange(ref Watchers, null);
            if (ws != null)
            {
                var l = ws.Length;
                while (l > 0)
                {
                    --l;
                    ws[l]?.Dispose();
                }
            }
        }

        long DeleteCount;
        long UpdateCount;
        long InsertCount;

        public void MarkAsCompleted(String file)
            => CompletedFiles.TryAdd(file, true);

        ConcurrentDictionary<String, bool> CompletedFiles = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        async ValueTask<bool> ProcessUpdates()
        {
            using var _ = PerfMon.Track(nameof(ProcessUpdates));
            List<Update> toEnque = new List<Update>();

            var completedFiles = Interlocked.Exchange(ref CompletedFiles, new ConcurrentDictionary<string, bool>(StringComparer.Ordinal));
            var updates = Updates;
            try
            {
                var entryFn = EntryFn;
                using var c = await Db.GetAsync().ConfigureAwait(false);
                var minTime = DateTime.UtcNow - TimeSpan.FromSeconds(15);
                while (updates.TryDequeue(out var d))
                {
                    var fname = d.Filename;
                    var metaName = IsMetaData(fname);
                    if (d.Deleted)
                    {
                        try
                        {
                            bool isDir = String.IsNullOrEmpty(Path.GetExtension(fname)) || ((await c.FirstOrDefaultAsync<T>(x => x.FullPath == fname).ConfigureAwait(false)) == null);
                            if (isDir)
                            {
                                fname += Path.DirectorySeparatorChar;
                                fname = fname.Replace("\\", "\\\\");
                                await c.DeleteAllAsync<T>(x => x.FullPath.StartsWith(fname)).ConfigureAwait(false);
                            }
                            else
                            {
                                await c.DeleteAllAsync<T>(x => x.FullPath == fname).ConfigureAwait(false);
                            }
                            Interlocked.Increment(ref DeleteCount);
                            Interlocked.Increment(ref Cc);
                        }
                        catch (Exception ex)
                        {
                            DeleteFails.OnException(ex);
                        }
                        if (metaName == null)
                            continue;
                        fname = metaName;
                        metaName = null;
                        d = new Update(fname, false);
                    }
                    try
                    {
                        var fi = new FileInfo(fname);
                        if (!fi.Exists)
                            continue;
                        bool wasCompleted = false;
                        if (fi.LastWriteTimeUtc > minTime)
                        {
                            if (!completedFiles.TryRemove(fname, out wasCompleted))
                            {
                                toEnque.Add(d);
                                continue;
                            }
                        }
                        if (metaName != null)
                        {
                            fname = metaName;
                            metaName = null;
                            d = new Update(fname, false);
                            fi = new FileInfo(fname);
                            if (!fi.Exists)
                                continue;
                            if (fi.LastWriteTimeUtc > minTime)
                            {
                                if (!wasCompleted)
                                {
                                    toEnque.Add(d);
                                    continue;
                                }
                            }
                        }
                        using (var tr = await c.BeginTransactionAsync().ConfigureAwait(false))
                        {
                            var data = await c.FirstOrDefaultAsync<T>(x => x.FullPath == fname).ConfigureAwait(false);
                            if (data != null)
                            {
                                data.LastModified = fi.LastAccessTimeUtc.Ticks;
                                data.Size = fi.Length;
                                data.Changed = DateTime.UtcNow.Ticks;
                                try
                                {
                                    if (!await entryFn(data).ConfigureAwait(false))
                                        continue;
                                    await c.UpdateAsync(data).ConfigureAwait(false);
                                    Interlocked.Increment(ref UpdateCount);
                                }
                                catch (Exception ex)
                                {
                                    UpdateFails.OnException(ex);
                                    toEnque.Add(d);
                                }
                            }
                            else
                            {
                                if (fname.Length > DbFileData.MaxNameLen)
                                {
                                    LengthFails.OnException(new Exception("Length of filename " + fname.ToQuoted() + " exceeds the maxium, ignored"));
                                    continue;
                                }
                                var ext = fi.Extension;
                                var el = ext.Length;
                                if (el > (DbFileData.MaxExtLen + 1))
                                {
                                    LengthFails.OnException(new Exception("Length of file extension for " + fname.ToQuoted() + " exceeds the maxium, ignored"));
                                    continue;
                                }
                                if (el > 0)
                                    ext = ext.Substring(1);
                                data = new T
                                {
                                    FullPath = fi.FullName,
                                    Ext = ext.FastToLower(),
                                    LastModified = fi.LastWriteTimeUtc.Ticks,
                                    Size = fi.Length,
                                    Changed = DateTime.UtcNow.Ticks,
                                };
                                try
                                {
                                    if (!await entryFn(data).ConfigureAwait(false))
                                        continue;
                                    await c.InsertAsync(data).ConfigureAwait(false);
                                    Interlocked.Increment(ref InsertCount);
                                }
                                catch (Exception ex)
                                {
                                    InsertFails.OnException(ex);
                                    toEnque.Add(d);
                                }
                            }
                            await tr.CommitAsync().ConfigureAwait(false);
                        }

                        Interlocked.Increment(ref Cc);
                    }
                    catch (Exception ex)
                    {
                        GenericFails.OnException(ex);
                        toEnque.Add(d);
                    }
                }
            }
            catch
            {
            }
            foreach (var x in toEnque)
                updates.Enqueue(x);
            return true;
        }

        public IEnumerable<Stats> GetStats()
        {
            var sys = SysName;
            yield return new Stats(SysName, "Insert.Count", Interlocked.Read(ref InsertCount), "The number of times a new file have been inserted into the database after initial sync");
            foreach (var s in InsertFails.GetStats(sys, "Insert.Fails."))
                yield return s;
            yield return new Stats(SysName, "Update.Count", Interlocked.Read(ref UpdateCount), "The number of times a file have been update in the database after initial sync");
            foreach (var s in UpdateFails.GetStats(sys, "Update.Fails."))
                yield return s;
            yield return new Stats(SysName, "Delete.Count", Interlocked.Read(ref DeleteCount), "The number of times a file have been deleted from the database after initial sync");
            foreach (var s in DeleteFails.GetStats(sys, "Delete.Fails."))
                yield return s;

            foreach (var s in LengthFails.GetStats(sys, "Length.Fails."))
                yield return s;
            foreach (var s in GenericFails.GetStats(sys, "Generic.Fails."))
                yield return s;
        }

        sealed class Update
        {
            public readonly String Filename;
            public readonly bool Deleted;

            public Update(string filename, bool deleted)
            {
                Filename = filename;
                Deleted = deleted;
            }
        }

        long Cc;

        readonly ConcurrentQueue<Update> Updates = new ConcurrentQueue<Update>();


        public Task<TableData> GetAsTable(TableDataRequest r, int refreshRate) 
            => Db.GetAsTableData<T>(r, refreshRate);

        public async Task<List<T>> GetFiltered(TableDataRequest r)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.GetFiltered<T>(r).ConfigureAwait(false)).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="r"></param>
        /// <returns>Result List, skip, limit, lookAhead</returns>
        public async Task<Tuple<List<T>, long, long, long>> GetFilteredWithParams(TableDataRequest r)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var res = (await c.GetFiltered<T>(out var skip, out var limit, out var lookAhead, r).ConfigureAwait(false)).ToList();
            return Tuple.Create(res, skip, limit, lookAhead);
        }

    }
}
