using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SysWeaver.MicroService;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{

    sealed class ServiceHostSystemD : IServiceHost
    {
        public String Name => "Linux systemd";

        public ServiceHostSystemD(ServiceParams p)
        {
            P = p;
            SName = p.Name;
            IsElevated = UnixHelpers.IsElevated;
        }

        readonly ServiceParams P;

        readonly String SName;

        String ScriptName => "/etc/systemd/system/" + SName + ".service";
        String PidName => "/var/run/" + SName + ".service.pid";

        #region Elevation

        public bool IsElevated { get; private set; }

        static readonly IReadOnlySet<ServiceVerbs> IntNeedElevation = ReadOnlyData.Set(
            ServiceVerbs.Install,
            ServiceVerbs.Uninstall,
            ServiceVerbs.Reinstall,
            ServiceVerbs.Restart,
            ServiceVerbs.Start,
            ServiceVerbs.Stop,
            ServiceVerbs.Pause,
            ServiceVerbs.Continue
        );

        public bool NeedElevation(ServiceVerbs verb) => IntNeedElevation.Contains(verb);

        public int RunElevated(String commandLine, bool terminal, bool noWait) => UnixHelpers.RunElevated(commandLine, terminal, noWait);

        #endregion//Elevation


        public ServiceStatus Status()
        {
            if (!IsInstalled())
                return ServiceStatus.NotInstalled;
            return GetRuntimeStatus();
        }

        bool IsInstalled() => File.Exists(ScriptName);

        public ServiceResponse Install()
        {
            var fn = ScriptName;
            var p = P;
            var name = SName;
            if (IsInstalled())
                return ServiceResponse.AlreadyInstalled;
            FileHelper.DeleteFile(PidName);

            var temp = new TextTemplate(Template);
            var cmd = ServiceHost.GetCommand();

            var disp = p.DisplayName ?? p.Name;
            var desc = p.Description;

            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Name",           name },
                { "DisplayName",    disp },
                { "Command",        cmd },
                { "Description",    desc ?? ""},
                { "User",    Environment.UserName },
            };
            var f = temp.Get(vars).Replace("\r", "");
            File.WriteAllText(fn, f);
            /*            File.SetUnixFileMode(fn,
                            UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                            UnixFileMode.GroupExecute | UnixFileMode.GroupRead |
                            UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
            */
            if (SystemHelper.Run("systemctl enable " + name.ToQuoted()) != 0)
            {
                FileHelper.DeleteFile(fn);
                return ServiceResponse.InstallFailed;
            }
            if (!IsInstalled())
            {
                FileHelper.DeleteFile(fn);
                return ServiceResponse.InstallFailed;
            }
            WriteStatus(ServiceStatus.Stopped);
            return ServiceResponse.Ok;
        }

        ServiceStatus WaitForWhile(ServiceStatus whileStateIs, params ServiceStatus[] states)
        {
            var s = new HashSet<ServiceStatus>(states);
            var checkWhile = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            var end = DateTime.UtcNow + TimeSpan.FromSeconds(60 * 3);
            for (; ; )
            {
                var t = GetRuntimeStatus();
                if (t == ServiceStatus.NotInstalled)
                    return t;
                if (t == ServiceStatus.Unknown)
                    return t;
                if (s.Contains(t))
                    return t;

                var now = DateTime.UtcNow;
                if (now > checkWhile)
                {
                    if (t != whileStateIs)
                        return t;
                }
                if (DateTime.UtcNow > end)
                    return ServiceStatus.Unknown;
                Thread.Sleep(100);
            }
        }

        ServiceStatus WaitFor(params ServiceStatus[] states)
        {
            var s = new HashSet<ServiceStatus>(states);
            var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            for (; ; )
            {
                var t = GetRuntimeStatus();
                if (t == ServiceStatus.NotInstalled)
                    return t;
                if (t == ServiceStatus.Unknown)
                    return t;
                if (s.Contains(t))
                    return t;
                if (DateTime.UtcNow > end)
                    return ServiceStatus.Unknown;
                Thread.Sleep(100);
            }
        }

        public ServiceResponse Start()
        {
            var i = Install();
            switch (i)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.AlreadyInstalled:
                    break;
                default:
                    return i;
            }
            var rt = GetRuntimeStatus();
            switch (rt)
            {
                case ServiceStatus.NotInstalled:
                    return ServiceResponse.NotInstalled;
                case ServiceStatus.Running:
                    return ServiceResponse.AlreadyRunning;
                case ServiceStatus.StopPending:
                    WaitFor(ServiceStatus.Stopped);
                    break;
                case ServiceStatus.PausePending:
                case ServiceStatus.Paused:
                    return Continue();
                case ServiceStatus.ContinuePending:
                    if (WaitForWhile(ServiceStatus.ContinuePending, ServiceStatus.Running) == ServiceStatus.Running)
                        return ServiceResponse.AlreadyRunning;
                    break;
                case ServiceStatus.StartPending:

                    if (WaitForWhile(ServiceStatus.StartPending, ServiceStatus.Running) == ServiceStatus.Running)
                        return ServiceResponse.AlreadyStarting;
                    break;
            }
            try
            {
                SystemHelper.GetStdOutFrom(out var ret, "systemctl start " + SName.ToQuoted());
                if (ret != 0)
                    return ServiceResponse.StartFailed;
                if (WaitForWhile(ServiceStatus.StartPending, ServiceStatus.Running) == ServiceStatus.Running)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.StartFailed;
        }

        public ServiceResponse Stop()
        {
            for (; ; )
            {
                var rt = GetRuntimeStatus(out _);
                switch (rt)
                {
                    case ServiceStatus.NotInstalled:
                        return ServiceResponse.NotInstalled;
                    case ServiceStatus.Stopped:
                        return ServiceResponse.NotRunning;
                    case ServiceStatus.StopPending:
                        if (WaitForWhile(ServiceStatus.StopPending, ServiceStatus.Stopped) == ServiceStatus.Stopped)
                            return ServiceResponse.AlreadyStopping;
                        continue;
                    case ServiceStatus.StartPending:
                        if (WaitForWhile(ServiceStatus.StartPending, ServiceStatus.Running) == ServiceStatus.Running)
                            break;
                        continue;
                }
                break;
            }
            try
            {
                SystemHelper.GetStdOutFrom(out var ret, "systemctl stop " + SName.ToQuoted());
                if (ret != 0)
                    return ServiceResponse.StopFailed;
                if (WaitForWhile(ServiceStatus.StopPending, ServiceStatus.Stopped) == ServiceStatus.Stopped)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.StopFailed;
        }

        public ServiceResponse Uninstall()
        {
            if (!IsInstalled())
                return ServiceResponse.NotInstalled;
            var c = Stop();
            switch (c)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.NotRunning:
                    break;
                default:
                    return c;
            }

            SystemHelper.GetStdOutFrom(out var ret, "systemctl disable " + SName.ToQuoted());
            if (!FileHelper.DeleteFile(ScriptName))
                return ServiceResponse.UninstallFailed;
            if (!FileHelper.DeleteFile(PidName))
                return ServiceResponse.UninstallFailed;
            return ServiceResponse.Ok;
        }


        public ServiceResponse Pause()
        {
            var status = GetRuntimeStatus(out var pid);
            switch (status)
            {
                case ServiceStatus.Paused:
                    return ServiceResponse.AlreadyPaused;
                case ServiceStatus.PausePending:
                    if (WaitForWhile(ServiceStatus.PausePending, ServiceStatus.Paused) == ServiceStatus.Paused)
                        return ServiceResponse.AlreadyPaused;
                    return ServiceResponse.PauseFailed;
                case ServiceStatus.ContinuePending:
                    if (WaitForWhile(ServiceStatus.ContinuePending, ServiceStatus.Running) != ServiceStatus.Running)
                        return ServiceResponse.PauseFailed;
                    break;
                case ServiceStatus.Running:
                    break;
                default:
                    return ServiceResponse.PauseFailed;
            }
            if (UnixHelpers.SendSignal(pid, UnixHelpers.Signals.SIGINT) != 0)
                return ServiceResponse.PauseFailed;
            return ServiceResponse.Ok;
        }

        public ServiceResponse Continue()
        {
            var status = GetRuntimeStatus(out var pid);
            switch (status)
            {
                case ServiceStatus.Running:
                    return ServiceResponse.AlreadyRunning;
                case ServiceStatus.ContinuePending:
                    if (WaitForWhile(ServiceStatus.ContinuePending, ServiceStatus.Running) == ServiceStatus.Running)
                        return ServiceResponse.AlreadyRunning;
                    return ServiceResponse.ContinueFailed;
                case ServiceStatus.PausePending:
                    if (WaitForWhile(ServiceStatus.PausePending, ServiceStatus.Paused) != ServiceStatus.Paused)
                        return ServiceResponse.ContinueFailed;
                    break;
                case ServiceStatus.Paused:
                    break;
                default:
                    return ServiceResponse.ContinueFailed;
            }
            if (UnixHelpers.SendSignal(pid, UnixHelpers.Signals.SIGCONT) != 0)
                return ServiceResponse.ContinueFailed;
            return ServiceResponse.Ok;
        }


        void WriteStatus(ServiceStatus status) => FileHelper.WriteText(PidName, String.Join(' ', status, Process.GetCurrentProcess().Id));

        ServiceStatus GetRuntimeStatus() => GetRuntimeStatus(out var _);

        ServiceStatus GetRuntimeStatus(out int pid)
        {
            const int retry = 10;
            var n = PidName;
            pid = 0;
            for (int i = 0; i < retry; ++i)
            {
                var t = FileHelper.ReadText(n);
                if (t == null)
                    return IsInstalled() ? ServiceStatus.Stopped : ServiceStatus.NotInstalled;
                try
                {
                    var kv = t.Split(' ');
                    if (Enum.TryParse<ServiceStatus>(kv[0], out ServiceStatus status))
                    {
                        pid = int.Parse(kv[1]);
                        if (status == ServiceStatus.Stopped)
                            return status;
                        try
                        {
                            var p = Process.GetProcessById(pid);
                            if (p == null)
                            {
                                status = ServiceStatus.Stopped;
                                pid = 0;
                            }
                            return status;
                        }
                        catch
                        {
                            status = ServiceStatus.Stopped;
                            return status;
                        }
                    }
                }
                catch
                {
                    var s = i + 1;
                    if (s < retry)
                        Thread.Sleep(s << 2);
                }
            }
            return ServiceStatus.Unknown;
        }


        public int Run(Action<ServiceManager> onStart)
        {
            WriteStatus(ServiceStatus.StartPending);
            using (var manager = new ServiceManager(true, null, ServiceHost.RestartService))
            {
                onStart?.Invoke(manager);
                WriteStatus(ServiceStatus.Running);
                manager.AddMessage("Service started");
                int quit = 0;
                int paused = 0;
                Action<PosixSignalContext> onAbort = c =>
                {
                    c.Cancel = true;
                    if (Interlocked.CompareExchange(ref quit, 1, 0) != 0)
                        return;
                    manager.AddMessage("Got signal: " + c.Signal);
                };
                Action<PosixSignalContext> onPause = c =>
                {
                    c.Cancel = true;
                    if (Interlocked.CompareExchange(ref paused, 1, 0) != 0)
                        return;
                    WriteStatus(ServiceStatus.PausePending);
                    manager.Pause();
                    WriteStatus(ServiceStatus.Paused);
                };
                Action<PosixSignalContext> onContinue = c =>
                {
                    c.Cancel = true;
                    if (Interlocked.CompareExchange(ref paused, 0, 1) != 1)
                        return;
                    WriteStatus(ServiceStatus.ContinuePending);
                    manager.Resume();
                    WriteStatus(ServiceStatus.Running);
                };
                using (PosixSignalRegistration.Create(PosixSignal.SIGINT, onPause))
                using (PosixSignalRegistration.Create(PosixSignal.SIGCONT, onContinue))
                using (PosixSignalRegistration.Create(PosixSignal.SIGHUP, onAbort))
                using (PosixSignalRegistration.Create(PosixSignal.SIGQUIT, onAbort))
                using (PosixSignalRegistration.Create(PosixSignal.SIGTERM, onAbort))
                {
                    while (quit == 0)
                        Thread.Sleep(100);
                }
                WriteStatus(ServiceStatus.StopPending);
                manager.AddMessage("Closing service");
            }
            WriteStatus(ServiceStatus.Stopped);
            return 0;
        }


        static readonly String Template = @"
[Unit]
Description=$(DisplayName)
After=network.target
StartLimitIntervalSec=0

[Service]
Type=simple
Restart=always
RestartSec=60
User=$(User)
ExecStart=$(Command) daemon

[Install]
WantedBy=multi-user.target


";

    }

}
