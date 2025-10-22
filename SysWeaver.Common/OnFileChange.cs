using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Run a call back every time a file changes, must be Disposed!
    /// </summary>
    public class OnFileChange : OnFileChangeBase
    {

        /// <summary>
        /// Run a call back every time a file changes, must be Disposed!
        /// </summary>
        /// <param name="filename">The file to monitor</param>
        /// <param name="onChange">The callback to execute when changed</param>
        /// <param name="delayMs">The delay in ms before invoking the onChange. 
        /// Some application may write a file using several operations, by ensuring that nothing has changed for a certain period, the odds are greater that the file is fully written</param>
        public OnFileChange(string filename, Action<string> onChange, int delayMs = 5000) : base(filename, delayMs)
        {
            C = onChange;
            Start();
        }

        readonly Action<string> C;

        protected override Task Notify()
        {
            C(Name);
            return Task.CompletedTask;
        }
    }




    /// <summary>
    /// Run an async Task every time a file changes, must be Disposed!
    /// </summary>
    public class OnFileChangeAsync : OnFileChangeBase
    {

        /// <summary>
        /// Run an async Task every time a file changes, must be Disposed!
        /// </summary>
        /// <param name="filename">The file to monitor</param>
        /// <param name="onChange">The task to execute when changed</param>
        /// <param name="delayMs">The delay in ms before invoking the onChange. 
        /// Some application may write a file using several operations, by ensuring that nothing has changed for a certain period, the odds are greater that the file is fully written</param>
        public OnFileChangeAsync(string filename, Func<string, Task> onChange, int delayMs = 5000) : base(filename, delayMs)
        {
            C = onChange;
            Start();
        }

        readonly Func<string, Task> C;

        protected override async Task Notify()
        {
            await C(Name).ConfigureAwait(false);
        }
    }


    public abstract class OnFileChangeBase : IDisposable
    {
        public override string ToString() => Name;

        protected OnFileChangeBase(string filename, int delayMs = 5000)
        {
            var thread = Thread.CurrentThread;
            Name = filename;
            DelayMs = delayMs;
            var f = Path.GetFileName(filename);
            Filter = f;
            var folder = Path.GetDirectoryName(filename);
            if (Directory.Exists(folder))
            {
                var w = new FileSystemWatcher(folder);
                W = w;
                w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                w.InternalBufferSize = 1 << 15;
                w.Filter = f;
                w.Changed += OnChanged;
                w.Created += OnCreated;
                w.Renamed += OnRenamed;
                w.IncludeSubdirectories = false;
            }
        }


        protected void Start()
        {
            var w = W;
            if (w != null)
                w.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            W?.Dispose();
        }

        readonly FileSystemWatcher W;
        readonly String Filter;
        protected readonly String Name;
        readonly int DelayMs;

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;
            OnCreated(sender, e);
        }

        int Running;

        void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (Interlocked.CompareExchange(ref Running, 1, 0) != 0)
                return;
            TaskExt.RunAsync(Wait());
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (String.Equals(e.Name, Filter, StringComparison.OrdinalIgnoreCase))
                OnCreated(sender, e);
        }



        protected abstract Task Notify();


        /// <summary>
        /// The last exception catched
        /// </summary>
        public Tuple<Exception, DateTime> LastExceptiion => Ex;

        /// <summary>
        /// The last exception catched
        /// </summary>
        public long ExceptionCount => Interlocked.Read(ref ExCount);

        volatile Tuple<Exception, DateTime> Ex;
        long ExCount;

        async Task Wait()
        {
            try
            {
                var minAge = DelayMs;
                var d = minAge;
                var n = Name;
                for (int err = 0; err < 5;)
                {
                    await Task.Delay(d + 100).ConfigureAwait(false);
                    try
                    {
                        var now = DateTime.UtcNow;
                        var dt = new FileInfo(n).LastWriteTimeUtc;
                        var age = now - dt;
                        if (age.TotalMilliseconds >= minAge)
                        {
                            try
                            {
                                await Notify().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Ex = Tuple.Create(ex, DateTime.UtcNow);
                                Interlocked.Increment(ref ExCount);
                            }
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Ex = Tuple.Create(ex, DateTime.UtcNow);
                        Interlocked.Increment(ref ExCount);
                        ++err;
                    }
                    if (d > 1000)
                        d = 1000;
                }
            }
            catch (Exception ex)
            {
                Ex = Tuple.Create(ex, DateTime.UtcNow);
                Interlocked.Increment(ref ExCount);
            }
            finally
            {
                Interlocked.Exchange(ref Running, 0);
            }
        }

    }



    /// <summary>
    /// Reads a string from a text file and update it when the file is changed, optionally executes a callback every time a new value is read, must be Disposed!
    /// </summary>
    public class ManagedFileString : OnFileChangeBase
    {

        /// <summary>
        ///  Reads a string from a text file, an executes a callback every time it's changed, must be Disposed!
        /// </summary>
        /// <param name="filename">The file to monitor</param>
        /// <param name="onChange">The callback to execute when changed, the first trimmed line of text is supplied (unless it starts with a '#')</param>
        /// <param name="delayMs">The delay in ms before invoking the onChange.
        /// Some application may write a file using several operations, by ensuring that nothing has changed for a certain period, the odds are greater that the file is fully written</param> 
        /// <param name="invokeFirst">If true, any onChange is invoked in the constructor (if the file exist and have data).</param> 
        public ManagedFileString(string filename, Action<string> onChange = null, int delayMs = 1000, bool invokeFirst = true) : base(filename, delayMs)
        {
            C = onChange;
            if (File.Exists(filename))
            {
                try
                {
                    OnData(File.ReadAllLines(filename), invokeFirst);
                }
                catch (Exception ex)
                {
                    Exceptions.OnException(ex);
                }
            }
            Start();
        }
        /// <summary>
        /// Collects exceptions caught when reading, parsing or in the callback.
        /// </summary>
        public readonly ExceptionTracker Exceptions = new ExceptionTracker();

        /// <summary>
        /// The current value (as read)
        /// </summary>
        public String Data => Current;

        volatile String Current;

        readonly Action<string> C;

        void OnData(String[] data, bool invoke = true)
        {
            foreach (var x in data)
            {
                var l = x?.Trim();
                if (l.Length <= 0)
                    continue;
                if (l[0] == '#')
                    continue;
                if (Current.FastEquals(l))
                    return;
                Current = l;
                if (invoke)
                    C?.Invoke(l);
                return;
            }
        }

        protected override async Task Notify()
        {
            try
            {
                OnData(await File.ReadAllLinesAsync(Name).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Exceptions.OnException(ex);
            }
        }
    }


    /// <summary>
    /// Contains a string value.
    /// Can be a string literal or a name of an existing file.
    /// If it's a file, the first non-empty line (after trimming) that doesn't start with a '#' is used.
    /// If the file content changes, the string value is re-read and the optional onChange callback is invoked.
    /// </summary>
    public sealed class ManagedString : IDisposable
    {
        /// <summary>
        /// Contains a string value.
        /// Can be a string literal or a name of an existing file.
        /// If it's a file, the first non-empty line (after trimming) that doesn't start with a '#' is used.
        /// If the file content changes, the string value is re-read and the optional onChange callback is invoked.
        /// </summary>
        /// <param name="value">The value of the string, or a name of an existing file</param>
        /// <param name="onChange">The callback to execute when changed</param>
        /// <param name="delayMs">The delay in ms before invoking the onChange.
        /// Some application may write a file using several operations, by ensuring that nothing has changed for a certain period, the odds are greater that the file is fully written</param>
        public ManagedString(String value, Action<string> onChange = null, int delayMs = 1000)
        {
            try
            {
                if (!String.IsNullOrEmpty(value))
                {
                    if (File.Exists(value))
                    {
                        Fs = new ManagedFileString(value, onChange, delayMs, false);
                        return;
                    }
                }
            }
            catch
            {
            }
            Fixed = value;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref Fs, null)?.Dispose();
        }

        public String Value => Fs?.Data ?? Fixed;

        ManagedFileString Fs;
        readonly String Fixed;

    }


}
