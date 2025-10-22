
using System;
using System.Collections.Generic;
using System.IO;
using SysWeaver.Serialization;
using SysWeaver.MicroService;
using System.Runtime.InteropServices;
using System.Threading;
using SysWeaver.Auth;
using System.Diagnostics;

namespace SysWeaver.OsServices
{



    public static class ServiceHost
    {

        public static String GetCommand(String args = null)
        {
            var cmd = EnvInfo.ExecCommand;
            if (!String.IsNullOrEmpty(args))
                cmd = String.Join(' ', cmd, args);
            return cmd;
        }



        static readonly IReadOnlySet<ServiceVerbs> RequireServiceHost = ReadOnlyData.Set(
            ServiceVerbs.Status,
            ServiceVerbs.Install,
            ServiceVerbs.Uninstall,
            ServiceVerbs.Reinstall,
            ServiceVerbs.Start,
            ServiceVerbs.Stop,
            ServiceVerbs.Pause,
            ServiceVerbs.Continue,
            ServiceVerbs.Restart,
            ServiceVerbs.Daemon,
            ServiceVerbs.Hash
        );

        static readonly IReadOnlyDictionary<ServiceVerbs, int> ArgCounts = new Dictionary<ServiceVerbs, int>()
        {
            { ServiceVerbs.Hash, 2 },
        }.Freeze();

        static readonly IReadOnlyDictionary<String, ServiceVerbs> Verbs = new Dictionary<string, ServiceVerbs>(StringComparer.Ordinal)
        {
            { "-h", ServiceVerbs.Help },
            { "/h", ServiceVerbs.Help },
            { "-help", ServiceVerbs.Help },
            { "/help", ServiceVerbs.Help },
            { "-?", ServiceVerbs.Help },
            { "/?", ServiceVerbs.Help },
            { "?", ServiceVerbs.Help },
            { "help", ServiceVerbs.Help },
            { "status", ServiceVerbs.Status },
            { "install", ServiceVerbs.Install },
            { "uninstall", ServiceVerbs.Uninstall },
            { "reinstall", ServiceVerbs.Reinstall },
            { "start", ServiceVerbs.Start },
            { "stop", ServiceVerbs.Stop },
            { "pause", ServiceVerbs.Pause },
            { "continue", ServiceVerbs.Continue },
            { "restart", ServiceVerbs.Restart },
            { "debug", ServiceVerbs.Debug },
            { "execute", ServiceVerbs.Execute },
            { "daemon", ServiceVerbs.Daemon },
            { "hash", ServiceVerbs.Hash },
        }.Freeze();




        static void Header(ServiceParams p, IServiceHost serviceHost)
        {
            Console.WriteLine();
            var logo = p.AsciiLogo;
            if (logo == null)
                SysWeaverLogo.Draw();
            else
                SysWeaverLogo.Draw(ConsoleColor.Blue, ConsoleColor.DarkBlue, ConsoleColor.Green);
            Console.WriteLine();
            if (p.Name == p.DisplayName)
            {
                Console.Write("This is the ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(p.Name);
                Console.ResetColor();
                Console.Write(" service");
            }else
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(p.DisplayName);
                Console.ResetColor();
                Console.Write(" [");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(p.Name);
                Console.ResetColor();
                Console.Write(" service]");
            }
            var desc = p.Description;
            if (!String.IsNullOrEmpty(desc))
            {
                Console.WriteLine(":");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(desc);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(".");
                Console.ResetColor();
            }
            if (serviceHost != null)
            {
                Console.WriteLine();
                Console.WriteLine("Service System: " + serviceHost.Name);
            }
            if (logo != null)
            {
                Console.WriteLine();
                AsciiTools.RenderColor(logo, AsciiTools.ConsolePalette);
            }
        }

        static bool WriteAction(ServiceParams p, IServiceHost serviceHost, ServiceVerbs verb)
        {
            var action = verb.Action();
            if (action == null)
                return false;
            Header(p, serviceHost);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(action);
            Console.ResetColor();
            return true;
        }

        static int Usage(ServiceParams p, ServiceResponse i, IServiceHost sm)
        {
            Header(p, sm);
            Console.WriteLine();
            if (sm != null)
            {
                if (sm.IsElevated || (!sm.NeedElevation(ServiceVerbs.Status)))
                {
                    Console.Write("Status: ");
                    var c = (int)sm.Status();
                    WriteStatus(c);
                }
            }
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Use: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(Path.GetFileName(EnvInfo.Executable).ToQuoted());
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" [Command]");
            Console.Write("[Command]");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" is one of the following:");
            void item(string cmd, string text)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("  " + cmd);
                var space = new String(' ', Math.Max(0, 12 - cmd.Length));
                Console.Write(space);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(" = ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(text);
            }
            item("help", "this text.");
            item("status", "display current status.");
            item("install", "install the service (not start).");
            item("uninstall", "stop (if running) and uninstall the service.");
            item("reinstall", "uninstall and install or start if the service is running.");
            item("start", "install (if not installed) and start the service.");
            item("stop", "stop the service.");
            item("pause", "pause the service.");
            item("continue", "resumes the service when paused.");
            item("restart", "restart the service process.");
            item("execute", "run as a command line program.");
            item("debug", "run as a command line program with more verbose output.");
            item("hash [user] [password]", "compute and display a simple password hash.");
            Console.ResetColor();
            var ci = (int)i;
            Environment.Exit(ci);
            return ci;
        }

        static int Hash(ServiceParams p, IServiceHost sm, String user, String password)
        {
            Header(p, sm);
            Console.WriteLine();
            user = user?.Trim();
            password = password?.Trim();
            if (String.IsNullOrEmpty(user))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid user name!");
                Console.WriteLine("Must be atleast one character!");
                Console.ResetColor();
                Environment.Exit(0);
                return 0;
            }
            var pe = AuthTools.ValidatePassword(password);
            if (pe != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid password!");
                Console.WriteLine(pe);
                Console.WriteLine(AuthTools.PasswordRules);
                Console.ResetColor();
                Environment.Exit(0);
                return 0;
            }
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Hash: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(user);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(AuthTools.ComputeSimplePasswordHash(user, password));
            Console.ResetColor();
            Environment.Exit(1);
            return 1;
        }

        static void WriteCode(int code)
        {
            ServiceResponse r = (ServiceResponse)code;
            if (r <= 0)
                Console.ForegroundColor = ConsoleColor.Red;
            else
                Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(r.Text());
            Console.ResetColor();
        }

        static void WriteStatus(int code)
        {
            ServiceStatus r = (ServiceStatus)code;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(r.Text());
            Console.ResetColor();
        }

        static int Reinstall(IServiceHost serviceManager)
        {
            var status = serviceManager.Status();
            var ret = serviceManager.Uninstall();
            switch (ret)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.NotInstalled:
                    break;
                default:
                    return (int)ret;
            }
            switch (status)
            {
                case ServiceStatus.Running:
                case ServiceStatus.StartPending:
                case ServiceStatus.PausePending:
                case ServiceStatus.ContinuePending:
                case ServiceStatus.Paused:
                    return (int)serviceManager.Start();
            }
            return (int)serviceManager.Install();
        }

        static int Restart(IServiceHost serviceManager)
        {
            var ret = serviceManager.Stop();
            switch (ret)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.AlreadyStopping:
                    break;
                default:
                    return (int)ret;
            }
            return (int)serviceManager.Start();
        }

        /// <summary>
        /// Never call directly, used internally by OS specific hosts
        /// </summary>
        /// <param name="man"></param>
        public static void RestartService(ServiceManager man)
        {
            var cmd = EnvInfo.HostExecutable;
            var args = ServiceVerbs.Restart.ToString().FastToLower();
            var msg = "Restarting service using command: " + cmd.ToQuoted() + " " + args;
            if (man == null)
                Console.WriteLine(msg);
            else
                man.AddMessage(msg);
            ProcessStartInfo p = new ProcessStartInfo();
            p.Arguments = args;
            p.WindowStyle = ProcessWindowStyle.Hidden;
            p.CreateNoWindow = true;
            p.FileName = cmd;
            Process.Start(p);
        }

        static void StartService()
        {
            var cmd = EnvInfo.HostExecutable;
            var args = ServiceVerbs.Start.ToString().FastToLower();
            var msg = "Starting service using command: " + cmd.ToQuoted() + " " + args;
            Console.WriteLine(msg);
            ProcessStartInfo p = new ProcessStartInfo();
            p.Arguments = args;
            p.WindowStyle = ProcessWindowStyle.Hidden;
            p.CreateNoWindow = true;
            p.FileName = cmd;
            Process.Start(p);
        }


        static void DoRestart(ServiceVerbs verb)
        {
            var cmd = EnvInfo.HostExecutable;
            var args = verb.ToString().FastToLower();
            Console.WriteLine("Restarting service using command: " + cmd.ToQuoted() + " " + args);
            ProcessStartInfo p = new ProcessStartInfo();
            p.Arguments = args;
            p.FileName = cmd;
            Process.Start(p);
        }

        

        static bool Backup(String filename, ServiceManager man)
        {
            var t = new FileInfo(filename);
            if (!t.Exists)
                return true;
            var fi = t.LastWriteTimeUtc.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            fi = fi.Replace(':', '_');
            fi = fi.Replace('T', '_');
            var fn = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + "." + fi + Path.GetExtension(filename));
            try
            {
                File.Copy(filename, fn, true);
                if (man == null)
                    Console.WriteLine("Backed up file " + filename.ToQuoted() + " to " + fn.ToQuoted());
                else
                    man.AddMessage("Backed up file " + filename.ToQuoted() + " to " + fn.ToQuoted());
                return true;
            }
            catch (Exception ex)
            {
                if (man == null)
                {
                    Console.WriteLine("Failed to back up file " + filename.ToQuoted() + " to " + fn.ToQuoted());
                    Console.WriteLine("Exception: " + ex);
                }
                else
                    man.AddMessage("Failed to back up file " + filename.ToQuoted() + " to " + fn.ToQuoted(), ex, MessageLevels.Warning);
            }
            return false;
        }

        static void SaveWorkingManifest(ServiceManager man)
        {
            AppDomain.CurrentDomain.UnhandledException -= ReplaceFaultyManifestAndRestart;
            var current = man.ManifestFileName;
            var baseName = Path.Combine(Path.GetDirectoryName(current), Path.GetFileNameWithoutExtension(current));
            var lastGood = baseName + ".LastGood" + Path.GetExtension(current);
            if (FileHash.FilesAreEqual(current, lastGood))
                return;
            Backup(lastGood, man);
            try
            {
                File.Copy(current, lastGood, true);
                man.AddMessage("Saved the current configuration, as the last good configuration: " + lastGood.ToQuoted());
            }
            catch (Exception ex)
            {
                man.AddMessage("Failed to save the current configuration, as the last good configuration: " + lastGood.ToQuoted(), ex, MessageLevels.Warning);
            }
        }

        static void WatchFaultyManifest(Action doRestart)
        {
            RestartAfterFault = doRestart;
            AppDomain.CurrentDomain.UnhandledException += ReplaceFaultyManifestAndRestart;
        }
        static Action RestartAfterFault;

        static void ReplaceFaultyManifestAndRestart(object sender, UnhandledExceptionEventArgs e)
        {
            var current = ServiceManager.GetManifestFileName();
            var baseName = Path.Combine(Path.GetDirectoryName(current), Path.GetFileNameWithoutExtension(current));
            var baseExt = Path.GetExtension(current);
            var lastGood = baseName + ".LastGood" + baseExt;
            if (!File.Exists(lastGood))
            {
                Console.WriteLine("No last good configuration file " + lastGood.ToQuoted() + " found, won't restart the service.");
                return;
            }
            if (FileHash.FilesAreEqual(current, lastGood))
            {
                Console.WriteLine("The last good configuration file " + lastGood.ToQuoted() + " is identical to the current config, won't restart the service.");
                return;
            }
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Will not restore last known good configuration when debugging");
                return;
            }
            var replaced = baseName + ".Replace" + baseExt;
            try
            {
                File.Copy(current, replaced, true);
                Console.WriteLine("Saved the current configuration, as the replaced configuration: " + replaced.ToQuoted());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save the current configuration, as the replaced configuration: " + replaced.ToQuoted());
                Console.WriteLine("Exception: " + ex);
                Console.WriteLine("Won't replace the current configuration since we couldn't back it up hence won't restart the service.");
                return;
            }
            Backup(current, null);
            try
            {
                File.Copy(lastGood, current, true);
                Console.WriteLine("Saved the last good configuration, as the current configuration: " + current.ToQuoted());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save the last good configuration, as the current configuration: " + current.ToQuoted());
                Console.WriteLine("Exception: " + ex);
                try
                {
                    File.Copy(replaced, current, true);
                    Console.WriteLine("Restored the current configuration from the replaced configuration: " + current.ToQuoted());
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Failed to restore the current configuration from the replaced configuration: " + current.ToQuoted());
                    Console.WriteLine("Exception: " + ex2);
                }
                Console.WriteLine("Won't restart the service.");
                return;
            }
            var f = RestartAfterFault;
            if (f == null)
            {
                Console.WriteLine("Can't restart, no restart function have been registered");
                return;
            }
            Console.WriteLine("Restarting process");
            f();
        }

        /// <summary>
        /// Run this from the main entrypoint of a command line program to run as a serivce.
        /// Services will be loaded and created from service manifest file named: "ExecutableFile.Services.json".
        /// You can have different manifest files depending on the OS, using the syntax "ExecutableFile.Services.[OS].json", where OS is one of the following:
        /// * Win32NT = The operating system is Windows NT or later.
        /// * Unix = The operating system is Unix.
        /// </summary>
        /// <param name="p">Optional params</param>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static int Run(ServiceParams p = null, Action<ServiceManager> onStart = null)
        {
            try
            {
                p = p ?? new ServiceParams();
                var name = p.Name;
                if (String.IsNullOrEmpty(name))
                    name = EnvInfo.AppName;
                p.Name = name;
                var fname = p.DisplayName ?? name;
                p.DisplayName = fname;
                if (p.AutoRecover)
                {
                    var ss = onStart;
                    onStart = man =>
                    {
                        ss?.Invoke(man);
                        SaveWorkingManifest(man);
                    };
                }
                IServiceHost serviceManager = null;
                String dname = String.Concat(typeof(ServiceHost).Namespace, '.', nameof(ServiceHost), "Factory", Environment.OSVersion.Platform);
                try
                {
                    var dtype = TypeFinder.Get(dname);
                    if (dtype == null)
                        dtype = TypeFinder.Get(String.Join(", ", dname, dname));
                    if (typeof(IServiceHostFactory).IsAssignableFrom(dtype))
                    {
                        serviceManager = (Activator.CreateInstance(dtype) as IServiceHostFactory).Create(p);
                    }
                }
                catch
                {
                }

                ServiceVerbs verb = ServiceVerbs.None;
                var args = Environment.GetCommandLineArgs();
                var al = args.Length;
                if (al <= 1)
                {
                    if (EnvInfo.HaveConsole)
                        verb = ServiceVerbs.Help;
                    else
                        verb = ServiceVerbs.Start;
                }
                else
                {
                    if (!Verbs.TryGetValue(args[1].FastToLower(), out verb))
                    {
                        Console.WriteLine("Invalid command: " + Environment.CommandLine.ToQuoted());
                        return Usage(p, ServiceResponse.InvalidCommad, serviceManager);
                    }
                    ArgCounts.TryGetValue(verb, out var expectedCount);
                    expectedCount += 2;
                    if (al != expectedCount)
                    {
                        Console.WriteLine("Invalid command: " + Environment.CommandLine.ToQuoted());
                        return Usage(p, ServiceResponse.ToManyArgs, serviceManager);
                    }
                }
                if (serviceManager == null)
                {
                    if (RequireServiceHost.Contains(verb))
                    {
                        Header(p, serviceManager);
                        Console.WriteLine();
                        Console.WriteLine("Couldn't find a service system for this OS (" + Environment.OSVersion.Platform + ").");
                        Console.WriteLine("Expected a " + nameof(IServiceHostFactory).ToQuoted() + " type implementation named " + dname.ToQuoted());
                        return (int)ServiceResponse.UnhandledOs;
                    }
                }

                Action header = () => WriteAction(p, serviceManager, verb);
                Func<int> doIt = null;
                Action<int> onResponse = WriteCode;
                bool terminal = false;
                bool forceElevation = false;
                switch (verb)
                {
                    case ServiceVerbs.Daemon:
                        header = null;
                        onResponse = null;
                        doIt = () =>
                        {
                            if (p.AutoRecover)
                                WatchFaultyManifest(StartService);
                            return serviceManager.Run(onStart);
                        };
                        forceElevation = p.NeedToRunElevated;
                        terminal = true;
                        break;
                    case ServiceVerbs.Hash:
                        Hash(p, serviceManager, args[2], args[3]);
                        return (int)ServiceResponse.Ok;

                    case ServiceVerbs.Help:
                        Usage(p, 0, serviceManager);
                        return (int)ServiceResponse.Ok;
                    case ServiceVerbs.Execute:
                    case ServiceVerbs.Debug:
                        var procName = Process.GetCurrentProcess().ProcessName;
                        const int timeOutSeconds = 15;
                        var giveUpAt = DateTime.UtcNow.AddSeconds(timeOutSeconds);
                        for (int i = 0; ; ++ i)
                        {
                            var procs = Process.GetProcessesByName(procName);
                            var l = procs.Length;
                            if (l <= 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("No process named " + procName.ToQuoted() + " found?!");
                                Console.ResetColor();
                                break;
                            }
                            if (l == 1)
                            {
                                if (i > 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine(".done!");
                                    Console.ResetColor();
                                }
                                break;
                            }
                            if (i == 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write("Multiple processes named " + procName.ToQuoted() + " found, waiting for other process to close..");
                                Console.ResetColor();
                            }
                            Thread.Sleep(10);
                            if (DateTime.UtcNow > giveUpAt)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(".gave up after " + timeOutSeconds + " seconds!");
                                Console.ResetColor();
                                break;
                            }
                            if (Console.KeyAvailable && (Console.ReadKey(true).Key == ConsoleKey.Escape))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("..cancelled!");
                                Console.WriteLine("'Esc' pressed, shutting down.");
                                Console.ResetColor();
                                return 0;
                            }

                        }

                        header = null;
                        onResponse = null;
                        forceElevation = p.NeedToRunElevated;
                        terminal = true;
                        doIt = () =>
                        {
                            Header(p, null);
                            Console.WriteLine();
                            Console.WriteLine("Running as a console program.");
                            bool restart = false;
                            void Shutdown(ServiceManager manager)
                            {
                                manager.Dispose();
                                Console.ResetColor();
                                Console.WriteLine("All services disposed.");
                                Console.WriteLine();
                                SysWeaverLogo.RenderAvGradient();
                                if (restart)
                                    DoRestart(verb);
                                Environment.Exit((int)ServiceResponse.Ok);
                            }


                            int didAbort = 0;
                            void Restart(ServiceManager sm)
                            {
                                if (Interlocked.CompareExchange(ref didAbort, 1, 0) != 0)
                                    return;
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                if (sm == null)
                                    Console.WriteLine("Restart process requested.");
                                else
                                    sm.AddMessage("Restart process requested.");
                                Console.ResetColor();
                                restart = true;
                                Shutdown(sm);
                            }
                            if (p.AutoRecover)
                                WatchFaultyManifest(() => DoRestart(verb));
                            using (var manager = new ServiceManager(true, sm =>
                            {
                                sm.AcceptMessageAbove = verb == ServiceVerbs.Debug ? MessageLevels.All : MessageLevels.Debug;
                            }, Restart))
                            {
                                onStart?.Invoke(manager);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                manager.AddMessage("Press 'Esc' to exit.");
                                Console.ResetColor();
                                bool consoleAlive = true;

                                Action<PosixSignalContext> onAbort = c =>
                                {
                                    if (Interlocked.CompareExchange(ref didAbort, 1, 0) != 0)
                                        return;
                                    if (consoleAlive)
                                    {
                                        using (var s = Console.OpenStandardInput())
                                            s.Write([27, 10, 13]);
                                    }
                                    c.Cancel = true;
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    manager.AddMessage("Got " + c.Signal + ", shutting down.");
                                    Console.ResetColor();
                                    Shutdown(manager);
                                };


                                void onPauseKey()
                                {
                                    if (manager.IsPaused)
                                    {
                                        manager.AddMessage("'Space' pressed, resuming");
                                        manager.Resume();
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        manager.AddMessage("'Space' pressed, pausing");
                                        Console.ResetColor();
                                        manager.Pause();
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        manager.AddMessage("Press 'Space' to resume");
                                        Console.ResetColor();
                                    }
                                }

                                Console.CancelKeyPress += (o, s) =>
                                {
                                    consoleAlive = false;
                                    onAbort(new PosixSignalContext(PosixSignal.SIGINT));
                                };

                                AppDomain.CurrentDomain.ProcessExit += (o, s) =>
                                {
                                    onAbort(new PosixSignalContext(PosixSignal.SIGQUIT));
                                };

                                

                                using (PosixSignalRegistration.Create(PosixSignal.SIGINT, onAbort))
                                using (PosixSignalRegistration.Create(PosixSignal.SIGHUP, onAbort))
                                using (PosixSignalRegistration.Create(PosixSignal.SIGQUIT, onAbort))
                                using (PosixSignalRegistration.Create(PosixSignal.SIGTERM, onAbort))
                                {
                                    if (Console.IsInputRedirected)
                                    {
                                        for (; ; )
                                        {
                                            var c = Console.Read();
                                            if ((c == 27) || (c < 0))
                                                break;
                                            if (c == 32)
                                                onPauseKey();
                                        }
                                    }
                                    else
                                    {
                                        for (; ; )
                                        {
                                            var c = Console.ReadKey(true).Key;
                                            if (c == ConsoleKey.Escape)
                                                break;
                                            if (c == ConsoleKey.Spacebar)
                                                onPauseKey();
                                        }
                                    }
                                }
                                if (Interlocked.CompareExchange(ref didAbort, 1, 0) != 0)
                                    Thread.Sleep(60000);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                manager.AddMessage("'Esc' pressed, shutting down.");
                                Console.ResetColor();
                            }
                            Console.ResetColor();
                            Console.WriteLine("All services disposed.");
                            Console.WriteLine();
                            SysWeaverLogo.RenderAvGradient();
                            if (restart)
                                DoRestart(verb);
                            return (int)ServiceResponse.Ok;
                        };
                        break;
                    case ServiceVerbs.Status:
                        doIt = () => (int)serviceManager.Status();
                        onResponse = WriteStatus;
                        break;
                    case ServiceVerbs.Install:
                        doIt = () => (int)serviceManager.Install();
                        break;
                    case ServiceVerbs.Uninstall:
                        doIt = () => (int)serviceManager.Uninstall();
                        break;
                    case ServiceVerbs.Reinstall:
                        doIt = () => Reinstall(serviceManager);
                        break;
                    case ServiceVerbs.Start:
                        doIt = () => (int)serviceManager.Start();
                        break;
                    case ServiceVerbs.Stop:
                        doIt = () => (int)serviceManager.Stop();
                        break;
                    case ServiceVerbs.Pause:
                        doIt = () => (int)serviceManager.Pause();
                        break;
                    case ServiceVerbs.Continue:
                        doIt = () => (int)serviceManager.Continue();
                        break;
                    case ServiceVerbs.Restart:
                        doIt = () => Restart(serviceManager);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                header?.Invoke();
                int c;
                if (serviceManager == null)
                {
                    c = doIt();
                }
                else
                {
                    if (serviceManager.IsElevated)
                    {
                        c = doIt();
                    }
                    else
                    {
                        if (forceElevation || serviceManager.NeedElevation(verb))
                        {
                            /*Console.WriteLine();
                            Console.WriteLine("Running elevated >>>" + EnvInfo.CommandLine + "<<<");
                            Console.WriteLine();*/
                            c = serviceManager.RunElevated(EnvInfo.CommandLine, terminal, terminal);
                        }
                        else
                        {
                            c = doIt();
                        }
                    }
                }
                onResponse?.Invoke(c);
                return c;


            }
            finally
            {
                Console.ResetColor();
            }
        }

    }
}
