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

    sealed class ServiceHostSysVinit : IServiceHost
    {
        public String Name => "Linux SysV init";

        public ServiceHostSysVinit(ServiceParams p)
        {
            P = p;
            SName = p.Name;
            IsElevated = UnixHelpers.IsElevated;
        }

        readonly ServiceParams P;

        readonly String SName;
        
        String ScriptName => "/etc/init.d/" + SName;
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

        bool IsInstalled()
        {
            var fn = ScriptName;
            if (!File.Exists(fn))
                return false;
            var p = P;
            var name = SName;
            foreach (var x in Directory.GetDirectories("/etc", "rc*.d", SearchOption.TopDirectoryOnly))
            {
                if (Directory.GetFiles(x, "???" + name, SearchOption.TopDirectoryOnly).Length > 0)
                    return true;
            }
            return false;
        }

        public ServiceResponse Install()
        {
            var fn = ScriptName;
            var p = P;
            var name = SName;
            if (IsInstalled())
                return ServiceResponse.AlreadyInstalled;
            var temp = new TextTemplate(Template);
            var cmd = ServiceHost.GetCommand();
            var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);


            var disp = p.DisplayName ?? p.Name;
            var desc = p.Description;
            if (!String.IsNullOrEmpty(desc))
                disp += "\n# Description:       " + desc.Replace("\r", "\r#                    ");

            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Name",           name },
                { "DisplayName",    disp },
                { "Command",        cmd },
                { "Exe",            exe},
                { "Args",           args },
            };
            var f = temp.Get(vars).Replace("\r", "");
            File.WriteAllText(fn, f);
            File.SetUnixFileMode(fn,
                UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupExecute | UnixFileMode.GroupRead |
                UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
            var ret = SystemHelper.Run("update-rc.d -f \"" + name + "\" defaults");
            if (ret != 0)
                return ServiceResponse.InstallFailed;
            if (!IsInstalled())
                return ServiceResponse.InstallFailed;
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
                SystemHelper.GetStdOutFrom(out var ret, "service \"" + SName + "\" start");
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
                SystemHelper.GetStdOutFrom(out var ret, "service \"" + SName + "\" stop");
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

            if (!FileHelper.DeleteFile(ScriptName))
                return ServiceResponse.UninstallFailed;
            if (!FileHelper.DeleteFile(PidName))
                return ServiceResponse.UninstallFailed;
            var ret = SystemHelper.Run("update-rc.d -f \"" + SName + "\" remove");
            if (ret != 0)
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
### BEGIN INIT INFO
# Provides:          $(Name)
# Required-Start:    $remote_fs $syslog $time
# Required-Stop:     $remote_fs $syslog $time
# Should-Start:      $network $named slapd autofs ypbind nscd nslcd winbind sssd
# Should-Stop:       $network $named slapd autofs ypbind nscd nslcd winbind sssd
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: $(DisplayName)
### END INIT INFO

NAME=$(Name)
DAEMON=$(Exe)
DAEMON_ARGS=$(Args)
PIDFILE=/var/run/$(Name).pid

test -f $DAEMON || exit 0


. /lib/lsb/init-functions

# Start the $(Name) service 
start() {
        log_daemon_msg ""Starting service: $(DisplayName)"" ""$(Name)""
        start-stop-daemon --start --quiet --pidfile ""$PIDFILE"" --exec ""$DAEMON"" --test > /dev/null || return 1
        start-stop-daemon --start --background --make-pidfile --pidfile ""$PIDFILE"" --exec ""$DAEMON"" -- $DAEMON_ARGS daemon
        log_end_msg ""$?""
}

# Stop the $(Name) service  
stop() {
        log_daemon_msg ""Stopping service: $(DisplayName)"" ""$(Name)""
        start-stop-daemon --stop --retry=TERM/30/KILL/5 --pidfile ""$PIDFILE"" --exec ""$DAEMON""
    	RETVAL=""$?""
        rm -f ""$PIDFILE""
        log_end_msg $RETVAL
}

# Logic #
case ""$1"" in
  start)
        start
        ;;
  stop)
        stop
        ;;
  status)
        status_of_proc -p ""$PIDFILE"" ""$DAEMON"" && exit 0 || exit $?
        ;;
  restart|reload|condrestart)
        log_daemon_msg ""Restarting service: $(DisplayName)"" ""$Name)""
        stop
        start
        ;;
  *)
        log_action_msg ""Usage: /etc/init.d/${Name} {start|stop|status|restart|reload|force-reload}""
        exit 1
esac

exit 0
";

    }

}
