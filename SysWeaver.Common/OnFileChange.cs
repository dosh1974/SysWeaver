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

        /// <summary>
        /// Invokes the callback with the name of the file
        /// </summary>
        /// <returns>A completed task</returns>
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

        /// <summary>
        /// Invokes the callback with the name of the file and waits for it to complete
        /// </summary>
        /// <returns>The task of the callback</returns>
        protected override async Task Notify()
        {
            await C(Name).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Base class for monitoring a single file for changes (using a <see cref="FileSystemWatcher"/>), must be Disposed!
    /// When the file is created, changed or renamed to the monitored name, <see cref="Notify"/> is called once the file hasn't been written to for the specified delay.
    /// </summary>
    /// <remarks>
    /// Only one notification is pending / running at a time, changes made while waiting or while <see cref="Notify"/> is executing causes another wait and notification afterwards.
    /// The folder of the file must exist when the instance is created, else the file is never monitored (a <see cref="DirectoryNotFoundException"/> is recorded in <see cref="LastExceptiion"/>).
    /// Exceptions are never thrown from the background notification, they are recorded in <see cref="LastExceptiion"/> and <see cref="ExceptionCount"/>.
    /// Derived classes must call <see cref="Start"/> at the end of their constructor.
    /// </remarks>
    public abstract class OnFileChangeBase : IDisposable
    {
        /// <summary>
        /// The name of the monitored file
        /// </summary>
        /// <returns>The file name as supplied to the constructor</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Set up the monitoring (call <see cref="Start"/> to begin raising events)
        /// </summary>
        /// <param name="filename">The file to monitor, a relative path is relative to the current folder (the folder of the file must exist)</param>
        /// <param name="delayMs">The time in ms that the file must be unchanged (based on its last write time) before <see cref="Notify"/> is called</param>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is null, empty or not a valid path (from <see cref="Path.GetFullPath(string)"/>)</exception>
        protected OnFileChangeBase(string filename, int delayMs = 5000)
        {
            var thread = Thread.CurrentThread;
            Name = filename;
            DelayMs = delayMs;
            //  Use the full path, so that a file name without a folder is watched in the current folder
            var fullName = Path.GetFullPath(filename);
            FullName = fullName;
            var f = Path.GetFileName(fullName);
            Filter = f;
            var folder = Path.GetDirectoryName(fullName);
            if (!Directory.Exists(folder))
            {
                Ex = Tuple.Create<Exception, DateTime>(new DirectoryNotFoundException("The folder " + folder.ToQuoted() + " doesn't exist, the file " + fullName.ToQuoted() + " will not be monitored!"), DateTime.UtcNow);
                Interlocked.Increment(ref ExCount);
            }
            else
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


        /// <summary>
        /// Start raising change events (does nothing if the folder didn't exist)
        /// </summary>
        protected void Start()
        {
            var w = W;
            if (w != null)
                w.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Stop monitoring the file.
        /// A notification that is already pending may still be executed after this.
        /// </summary>
        public void Dispose()
        {
            W?.Dispose();
        }

        readonly FileSystemWatcher W;
        readonly String Filter;
        /// <summary>
        /// The name of the monitored file (as supplied to the constructor)
        /// </summary>
        protected readonly String Name;
        readonly String FullName;
        readonly int DelayMs;

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;
            OnCreated(sender, e);
        }

        int Running;
        int Dirty;

        void OnCreated(object sender, FileSystemEventArgs e)
        {
            Interlocked.Exchange(ref Dirty, 1);
            if (Interlocked.CompareExchange(ref Running, 1, 0) != 0)
                return;
            TaskExt.RunAsync(Wait());
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (String.Equals(e.Name, Filter, StringComparison.OrdinalIgnoreCase))
                OnCreated(sender, e);
        }



        /// <summary>
        /// Called (on a thread pool thread) when the file has changed and then been unchanged for the delay.
        /// Any exception is caught and recorded.
        /// </summary>
        /// <returns>A task that completes when the notification is handled</returns>
        protected abstract Task Notify();


        /// <summary>
        /// The last exception caught (and when it was caught, UTC), null if no exception has been caught
        /// </summary>
        public Tuple<Exception, DateTime> LastExceptiion => Ex;

        /// <summary>
        /// The number of exceptions caught
        /// </summary>
        public long ExceptionCount => Interlocked.Read(ref ExCount);

        volatile Tuple<Exception, DateTime> Ex;
        long ExCount;

        /// <summary>
        /// Wait until the file hasn't been written to for the delay, then notify (gives up after 5 failures to read the file time).
        /// Repeats if a change event was received after the wait started or while notifying.
        /// </summary>
        async Task Wait()
        {
            for (; ; )
            {
                try
                {
                    Interlocked.Exchange(ref Dirty, 0);
                    var minAge = DelayMs;
                    var d = minAge;
                    var n = FullName;
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
                                //  Any change event after this point will cause another notification
                                Interlocked.Exchange(ref Dirty, 0);
                                try
                                {
                                    await Notify().ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Ex = Tuple.Create(ex, DateTime.UtcNow);
                                    Interlocked.Increment(ref ExCount);
                                }
                                break;
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
                Interlocked.Exchange(ref Running, 0);
                //  A change event received while running (that didn't start a new wait) must be handled
                if (Volatile.Read(ref Dirty) == 0)
                    return;
                if (Interlocked.CompareExchange(ref Running, 1, 0) != 0)
                    return;
            }
        }

    }



    /// <summary>
    /// Reads a string from a text file and update it when the file is changed, optionally executes a callback every time a new value is read, must be Disposed!
    /// </summary>
    public class ManagedFileString : OnFileChangeBase
    {

        /// <summary>
        ///  Reads a string from a text file, and executes a callback every time it's changed, must be Disposed!
        /// </summary>
        /// <param name="filename">The file to monitor</param>
        /// <param name="onChange">The callback to execute when changed, the first non-empty trimmed line of text that doesn't start with a '#' is supplied.
        /// It's only invoked if the value differs from the current value (if the file has no such line, the current value is kept and nothing is invoked)</param>
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
                    OnData(FileExt.ReadLines(filename), invokeFirst);
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
        /// The current value (as read), null if no value has been read yet
        /// </summary>
        public String Data => Current;

        volatile String Current;

        readonly Action<string> C;

        /// <summary>
        /// Update the current value from the lines of the file (the first non-empty, non-comment line), invoke the callback if it changed
        /// </summary>
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

        /// <summary>
        /// Re-read the file and update the value (exceptions are recorded in <see cref="Exceptions"/>)
        /// </summary>
        /// <returns>A task that completes when the file is read</returns>
        protected override async Task Notify()
        {
            try
            {
                OnData(await FileExt.ReadLinesAsync(Name).ConfigureAwait(false));
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
        /// <param name="value">The value of the string, or a name of an existing file (the file must exist when this is called to be treated as a file)</param>
        /// <param name="onChange">The callback to execute when changed (only used if the value is a file, it's not invoked for the initial value)</param>
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

        /// <summary>
        /// Stop monitoring the file (if any), the value of a file is lost (becomes null) after this
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref Fs, null)?.Dispose();
        }

        /// <summary>
        /// The current value, the literal value or the current value read from the file
        /// </summary>
        public String Value => Fs?.Data ?? Fixed;

        ManagedFileString Fs;
        readonly String Fixed;

    }


}
