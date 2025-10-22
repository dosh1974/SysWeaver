
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CefSharp;
using CefSharp.OffScreen;


namespace SysWeaver.WebBrowser
{


    static class CefSharp
    {
#if !SINGLE_THREAD

        static readonly SingleThreadTaskScheduler Scheduler = new SingleThreadTaskScheduler(default);
        public static Task RunTask(Func<Task> task) => Scheduler.RunTask(task);
        public static Task<R> RunTask<R>(Func<Task<R>> task) => Scheduler.RunTask(task);
        public static Task Run(Action task) => Scheduler.Run(task);
        public static Task<R> Run<R>(Func<R> task) => Scheduler.Run(task);


#else//SINGLE_THREAD

        public static Task RunTask(Func<Task> task) => task();
        public static Task<R> RunTask<R>(Func<Task<R>> task) => task();
        public static Task Run(Action task)
        {
            task();
            return Task.CompletedTask;
        }
        
        public static Task<R> Run<R>(Func<R> task) => Task.FromResult(task());

#endif//SINGLE_THREAD

        static CefSharp()
        {
            CefSharpSettings.ConcurrentTaskExecution = true;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("CefSharp"))
            {
                string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                string architectureSpecificPath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                    Environment.Is64BitProcess ? "x64" : "x86",
                    assemblyName);

                return File.Exists(architectureSpecificPath)
                    ? Assembly.LoadFile(architectureSpecificPath)
                    : null;
            }

            return null;
        }

        public static void OnTypeInit()
        {
        }

        static long InstanceCount;
        static readonly AsyncLock Lock = new AsyncLock();

        public static async Task Init(IMessageHost m, CefSettings settings)
        {
            if (Interlocked.Increment(ref InstanceCount) != 1)
                return;
            using (await Lock.Lock().ConfigureAwait(false))
            {
                if (Interlocked.Read(ref InstanceCount) != 1)
                    return;
                var success = await RunTask(() => InternalInit(m, settings)).ConfigureAwait(false);
                if (!success)
                {
                    throw new Exception("Unable to initialize CEF, check the log file.");
                }
            }
        }

        internal static String Prefix => "[CefBrowser] ";


        static async Task<bool> InternalInit(IMessageHost m, CefSettings settings)
        {
            var path = Path.Combine(EnvInfo.RuntimeFolderNative, "CefSharp.BrowserSubprocess.exe");
            m?.AddMessage(Prefix + "Initialize from folder " + path.ToQuoted());
            //settings.BrowserSubprocessPath = path;
            return await Cef.InitializeAsync(settings, performDependencyCheck: true, browserProcessHandler: null);
        }

        static Task InternalShutDown(IMessageHost m)
        {
            m?.AddMessage(Prefix + "Shutdown");
            try
            {
                Cef.Shutdown();
            }
            catch (Exception ex)
            {
                m?.AddMessage(Prefix + "Shutdown failed", ex, MessageLevels.Warning);
            }
            return Task.CompletedTask;
        }

        public static void OnDispose(IMessageHost m)
        {
            if (Interlocked.Decrement(ref InstanceCount) == 0)
            {
                try
                {
                    Run(() => InternalShutDown(m)).RunAsync();
                }
                catch
                {
                }
            }

        }

    }

}
