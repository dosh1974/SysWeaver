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

    /// <summary>
    /// <see cref="IServiceHost"/> for Linux SysV init.
    /// Installs an LSB init script "/etc/init.d/[Name]" (using start-stop-daemon to run "[command] daemon") and registers it using update-rc.d.
    /// </summary>
    /// <remarks>
    /// The runtime status and pid of the daemon is tracked in "/var/run/[Name].service.pid" ("Status Pid"), written by the daemon process.
    /// Pause and continue are implemented by sending SIGINT and SIGCONT to the daemon; SIGTERM, SIGHUP and SIGQUIT stop it.
    /// All verbs that change the service require root.
    /// </remarks>
    sealed class ServiceHostSysVinit : IServiceHost
    {
        /// <inheritdoc/>
        public String Name => "Linux SysV init";

        /// <summary>
        /// Creates the host.
        /// </summary>
        /// <param name="p">The service parameters.</param>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool NeedElevation(ServiceVerbs verb) => IntNeedElevation.Contains(verb);

        /// <inheritdoc cref="UnixHelpers.RunElevated(string, bool, bool)"/>
        public int RunElevated(String commandLine, bool terminal, bool noWait) => UnixHelpers.RunElevated(commandLine, terminal, noWait);

        #endregion//Elevation


        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public ServiceResponse Install()
        {
            var fn = ScriptName;
            var p = P;
            var name = SName;
            if (IsInstalled())
                return ServiceResponse.AlreadyInstalled;
            var cmd = ServiceHost.GetCommand();
            var f = CreateScript(name, p.DisplayName ?? p.Name, p.Description, cmd, GetAppDll(cmd, EnvInfo.Executable));
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

        /// <summary>
        /// Install (if needed) and start the service, a paused service is continued.
        /// </summary>
        /// <returns>The result.</returns>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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


        /// <summary>
        /// Pause the running service by sending SIGINT to the daemon (returns before the pause has completed).
        /// </summary>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Resume the paused service by sending SIGCONT to the daemon (returns before the resume has completed).
        /// </summary>
        /// <returns>The result.</returns>
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
        void WriteStatus(ServiceStatus status) => FileHelper.WriteText(PidName, String.Join(' ', status, Environment.ProcessId));
        
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
                //  The recorded process must be the daemon (the pid may have been reused by another process after the daemon was killed)
                if (StatusFile.TryGetStatus(t, StatusFile.IsDaemonProcess, out var status, out pid))
                    return status;
                //  Probably a partially written file, retry
                var s = i + 1;
                if (s < retry)
                    Thread.Sleep(s << 2);
            }
            pid = 0;
            return ServiceStatus.Unknown;
        }


        /// <summary>
        /// The daemon main loop: creates the <see cref="ServiceManager"/>, writes the status file and handles signals until SIGTERM, SIGHUP or SIGQUIT is received.
        /// </summary>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created.</param>
        /// <returns>Always 0.</returns>
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

        /// <summary>
        /// Quote a value for a POSIX shell (single quotes, an embedded single quote becomes '\''), so that it's never expanded or split.
        /// </summary>
        /// <param name="value">The value, null is treated as an empty string.</param>
        /// <returns>The quoted value, ex: "it's" => "'it'\''s'".</returns>
        static String ShellQuote(String value) => String.Concat("'", (value ?? "").Replace("'", "'\\''"), "'");

        /// <summary>
        /// Make a value safe for a single line shell comment (control characters, including line breaks, are replaced by spaces).
        /// </summary>
        /// <param name="value">The value, null is treated as an empty string.</param>
        /// <returns>The single line value.</returns>
        static String CommentLine(String value)
        {
            if (String.IsNullOrEmpty(value))
                return "";
            var c = value.ToCharArray();
            for (int i = 0; i < c.Length; ++i)
                if (Char.IsControl(c[i]))
                    c[i] = ' ';
            return new String(c);
        }

        /// <summary>
        /// Create the LSB init script text (LF line endings).
        /// All values are shell quoted (or put in comments as single lines), so names and paths may contain spaces and shell characters.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="displayName">The display name (Short-Description and the start / stop log lines).</param>
        /// <param name="description">The optional description (multiple lines are allowed), put in the LSB header only.</param>
        /// <param name="cmd">The command that starts the application (as <see cref="ServiceHost.GetCommand(string)"/>), ex: "/usr/bin/dotnet "/opt/my app/MyService.dll"".
        /// The executable is run by start-stop-daemon, followed by the (single, unquoted) argument if any, followed by "daemon".
        /// The argument is the application file run by the executable (ex: the .dll run by dotnet), the script checks that it exists like the executable.</param>
        /// <param name="appDll">The managed entry .dll that an apphost (native executable without argument) loads, the script checks that it exists like the executable
        /// (APP_DLL); null or empty if there is none (ex: single-file or native AOT publish, or the command already has the .dll as argument), see <see cref="GetAppDll"/>.</param>
        /// <returns>The script text.</returns>
        static String CreateScript(String name, String displayName, String description, String cmd, String appDll)
        {
            var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
            //  The argument is a single (quoted if it contains spaces) path, see EnvInfo.ExecCommand
            if ((args.Length >= 2) && (args[0] == '"') && (args[args.Length - 1] == '"'))
                args = args.Substring(1, args.Length - 2);
            var desc = "";
            if (!String.IsNullOrEmpty(description))
            {
                var lines = description.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                for (int i = 0; i < lines.Length; ++i)
                    lines[i] = CommentLine(lines[i]);
                desc = "\n# Description:       " + String.Join("\n#                    ", lines);
            }
            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "HeaderName",         CommentLine(name) },
                { "HeaderDisplayName",  CommentLine(displayName) },
                { "HeaderDescription",  desc },
                { "Name",               ShellQuote(name) },
                { "DisplayName",        ShellQuote(CommentLine(displayName)) },
                { "Exe",                ShellQuote(exe) },
                { "Args",               ShellQuote(args) },
                { "AppDll",             ShellQuote(appDll) },
            };
            return new TextTemplate(Template).Get(vars).Replace("\r", "");
        }

        /// <summary>
        /// Get the managed entry .dll that the script must check for an apphost install (the apphost loads it, but it isn't on the command line).
        /// </summary>
        /// <param name="cmd">The command that starts the application (as <see cref="ServiceHost.GetCommand(string)"/>).</param>
        /// <param name="entryAssembly">The entry assembly path (<see cref="EnvInfo.Executable"/>, it's the host executable when there is no separate assembly file).</param>
        /// <returns>The entry assembly path if the command is a single executable (an apphost, no argument) and the entry assembly is another, existing file;
        /// else null (ex: "dotnet MyService.dll", where the .dll is the argument, or a single-file / native AOT publish without a separate .dll).</returns>
        static String GetAppDll(String cmd, String entryAssembly)
        {
            var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
            if (args.Length > 0)
                return null;
            if (String.IsNullOrEmpty(entryAssembly) || String.Equals(entryAssembly, exe, StringComparison.Ordinal))
                return null;
            return File.Exists(entryAssembly) ? entryAssembly : null;
        }

        /// <summary>
        /// The LSB init script, see <see cref="CreateScript"/>.
        /// Exit codes (LSB): 0 = success (also start of a running and stop of a stopped service), 1 = start / stop / restart failed, 2 = unknown action,
        /// 5 = the executable, the application file it runs (DAEMON_ARGS, ex: the .dll run by dotnet) or the entry .dll of an apphost (APP_DLL) is missing (stop then exits with 0, status with 4), status = the LSB pidofproc result (0 = running, 1 = dead but the pid file exists,
        /// 3 = not running, 4 = unknown).
        /// </summary>
        static readonly String Template = @"#!/bin/sh
### BEGIN INIT INFO
# Provides:          $(HeaderName)
# Required-Start:    $remote_fs $syslog $time
# Required-Stop:     $remote_fs $syslog $time
# Should-Start:      $network $named slapd autofs ypbind nscd nslcd winbind sssd
# Should-Stop:       $network $named slapd autofs ypbind nscd nslcd winbind sssd
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: $(HeaderDisplayName)$(HeaderDescription)
### END INIT INFO

NAME=$(Name)
DESC=$(DisplayName)
DAEMON=$(Exe)
DAEMON_ARGS=$(Args)
APP_DLL=$(AppDll)
PIDFILE=""/var/run/${NAME}.pid""

# The executable, the application file it runs if any (ex: the .dll run by dotnet) and the managed entry .dll of an apphost if any, must exist
if [ ! -f ""$DAEMON"" ] || { [ -n ""$DAEMON_ARGS"" ] && [ ! -f ""$DAEMON_ARGS"" ]; } || { [ -n ""$APP_DLL"" ] && [ ! -f ""$APP_DLL"" ]; }; then
        case ""$1"" in
          stop) exit 0 ;;
          status) exit 4 ;;
          *) exit 5 ;;
        esac
fi


. /lib/lsb/init-functions

# Start the service, returns 0 = started, 1 = already running, 2 = failed to start
do_start() {
        start-stop-daemon --start --quiet --pidfile ""$PIDFILE"" --exec ""$DAEMON"" --test > /dev/null || return 1
        start-stop-daemon --start --background --make-pidfile --pidfile ""$PIDFILE"" --exec ""$DAEMON"" -- ${DAEMON_ARGS:+""$DAEMON_ARGS""} daemon || return 2
        return 0
}

# Stop the service, returns 0 = stopped, 1 = wasn't running, 2 = failed to stop
do_stop() {
        start-stop-daemon --stop --retry=TERM/30/KILL/5 --pidfile ""$PIDFILE"" --exec ""$DAEMON""
        RETVAL=""$?""
        [ ""$RETVAL"" = 2 ] && return 2
        rm -f ""$PIDFILE""
        [ ""$RETVAL"" = 0 ] || return 1
        return 0
}

# Logic #
case ""$1"" in
  start)
        log_daemon_msg ""Starting service: $DESC"" ""$NAME""
        do_start
        case ""$?"" in
                0|1) log_end_msg 0 ;;
                *) log_end_msg 1; exit 1 ;;
        esac
        ;;
  stop)
        log_daemon_msg ""Stopping service: $DESC"" ""$NAME""
        do_stop
        case ""$?"" in
                0|1) log_end_msg 0 ;;
                *) log_end_msg 1; exit 1 ;;
        esac
        ;;
  status)
        # LSB pidofproc with quoted arguments (Debian's status_of_proc passes them unquoted: a path with spaces fails with ""invalid arguments"")
        pidofproc -p ""$PIDFILE"" ""$DAEMON"" > /dev/null
        RETVAL=""$?""
        case ""$RETVAL"" in
                0) log_success_msg ""$NAME is running"" ;;
                4) log_failure_msg ""could not access PID file for $NAME"" ;;
                *) log_failure_msg ""$NAME is not running"" ;;
        esac
        exit ""$RETVAL""
        ;;
  restart|reload|force-reload|condrestart)
        log_daemon_msg ""Restarting service: $DESC"" ""$NAME""
        do_stop
        if [ ""$?"" = 2 ]; then
                log_end_msg 1
                exit 1
        fi
        do_start
        if [ ""$?"" = 0 ]; then
                log_end_msg 0
        else
                log_end_msg 1
                exit 1
        fi
        ;;
  *)
        log_action_msg ""Usage: /etc/init.d/${NAME} {start|stop|status|restart|reload|force-reload}""
        exit 2
        ;;
esac

exit 0
";

    }

}
