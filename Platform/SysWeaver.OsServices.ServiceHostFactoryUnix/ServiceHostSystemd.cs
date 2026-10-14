using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SysWeaver.MicroService;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{

    /// <summary>
    /// <see cref="IServiceHost"/> for Linux systemd.
    /// Installs a unit file "/etc/systemd/system/[Name].service" (Restart=always, RestartSec=60, User = the user running the install, ExecStart = "[command] daemon") and enables it using systemctl.
    /// The values are escaped for systemd (specifiers, quoting, variable substitution), see <see cref="CreateUnit"/>.
    /// </summary>
    /// <remarks>
    /// The runtime status and pid of the daemon is tracked in "/var/run/[Name].service.pid" ("Status Pid"), written by the daemon process.
    /// Pause and continue are implemented by sending SIGINT and SIGCONT to the daemon; SIGTERM, SIGHUP and SIGQUIT stop it.
    /// All verbs that change the service require root.
    /// </remarks>
    sealed class ServiceHostSystemD : IServiceHost
    {
        /// <inheritdoc/>
        public String Name => "Linux systemd";

        /// <summary>
        /// Creates the host.
        /// </summary>
        /// <param name="p">The service parameters.</param>
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


        /// <summary>
        /// Get the status of the service: the status file written by the daemon, combined with the state of the unit in systemd (see <see cref="ApplyUnitState"/>),
        /// ex: a crashed daemon that systemd will restart (Restart=always, "activating (auto-restart)") is <see cref="ServiceStatus.StartPending"/>, not <see cref="ServiceStatus.Stopped"/>.
        /// </summary>
        /// <returns>The status.</returns>
        public ServiceStatus Status()
        {
            if (!IsInstalled())
                return ServiceStatus.NotInstalled;
            var s = GetRuntimeStatus();
            if (s != ServiceStatus.Stopped)
                return s;
            return ApplyUnitState(s, GetUnitActiveState());
        }

        bool IsInstalled() => File.Exists(ScriptName);

        /// <summary>
        /// Create a systemctl command line for this unit.
        /// </summary>
        /// <param name="args">The systemctl arguments before the unit name, ex: "stop" or "--no-block restart".</param>
        /// <param name="name">The service (unit) name, written double quoted.</param>
        /// <returns>The command line, ex: systemctl --no-block restart "MyService".</returns>
        static String Systemctl(String args, String name) => String.Concat("systemctl ", args, " ", name.ToQuoted());

        /// <summary>
        /// Get the ActiveState of the unit from systemd ("active", "reloading", "inactive", "failed", "activating", "deactivating").
        /// </summary>
        /// <returns>The state, or null if systemctl failed.</returns>
        String GetUnitActiveState()
        {
            var s = SystemHelper.GetStdOutFrom(out var ret, Systemctl("show -p ActiveState --value", SName));
            if (ret != 0)
                return null;
            return s?.Trim();
        }

        /// <summary>
        /// Check if a unit ActiveState means that systemd isn't running and won't (re)start the service by itself.
        /// </summary>
        /// <param name="activeState">The ActiveState of the unit, null if unknown.</param>
        /// <returns>True for "inactive", "failed" and an unknown (null or empty) state.</returns>
        static bool IsUnitInactive(String activeState) => String.IsNullOrEmpty(activeState) || (activeState == "inactive") || (activeState == "failed");

        /// <summary>
        /// Combine the status from the status file with the ActiveState of the unit.
        /// The status file only tells if the daemon process is alive; systemd may be about to (re)start it (ex: Restart=always after a crash is "activating (auto-restart)")
        /// or still be stopping it.
        /// </summary>
        /// <param name="fileStatus">The status from the status file.</param>
        /// <param name="activeState">The ActiveState of the unit, null if unknown.</param>
        /// <returns>The <paramref name="fileStatus"/>, except for a <see cref="ServiceStatus.Stopped"/> daemon: <see cref="ServiceStatus.StartPending"/> if the unit is
        /// "activating", "active" or "reloading", <see cref="ServiceStatus.StopPending"/> if it's "deactivating".</returns>
        static ServiceStatus ApplyUnitState(ServiceStatus fileStatus, String activeState)
        {
            if (fileStatus != ServiceStatus.Stopped)
                return fileStatus;
            switch (activeState)
            {
                case "activating":
                case "active":
                case "reloading":
                    return ServiceStatus.StartPending;
                case "deactivating":
                    return ServiceStatus.StopPending;
            }
            return fileStatus;
        }

        /// <inheritdoc/>
        public ServiceResponse Install()
        {
            var fn = ScriptName;
            var p = P;
            var name = SName;
            if (IsInstalled())
                return ServiceResponse.AlreadyInstalled;
            String f;
            try
            {
                f = CreateUnit(p.DisplayName ?? p.Name, Environment.UserName, ServiceHost.GetCommand());
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine(ex.Message);
                return ServiceResponse.InstallFailed;
            }
            FileHelper.DeleteFile(PidName);
            File.WriteAllText(fn, f);
            /*            File.SetUnixFileMode(fn,
                            UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                            UnixFileMode.GroupExecute | UnixFileMode.GroupRead |
                            UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
            */
            //  Load the new unit file (systemd may still have a removed unit with this name loaded)
            SystemHelper.GetStdOutFrom(out _, "systemctl daemon-reload");
            if (SystemHelper.Run(Systemctl("enable", name)) != 0)
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
                SystemHelper.GetStdOutFrom(out var ret, Systemctl("start", SName));
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

        /// <summary>
        /// Stop the service.
        /// If the daemon isn't running, the unit is still stopped (using systemctl) unless systemd reports it as inactive or failed, ex: after a crash systemd
        /// restarts the service (Restart=always, "activating (auto-restart)"), that pending restart is cancelled.
        /// </summary>
        /// <returns>The result, <see cref="ServiceResponse.NotRunning"/> if neither the daemon nor the unit is running.</returns>
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
                        //  The status file only tells that the daemon isn't alive, systemd may be about to (re)start it
                        if (IsUnitInactive(GetUnitActiveState()))
                            return ServiceResponse.NotRunning;
                        break;
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
                SystemHelper.GetStdOutFrom(out var ret, Systemctl("stop", SName));
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

        /// <summary>
        /// Stop (see <see cref="Stop"/>), disable and remove the unit, then unload it from systemd (daemon-reload, reset-failed).
        /// </summary>
        /// <returns>The result.</returns>
        public ServiceResponse Uninstall()
        {
            if (!IsInstalled())
                return ServiceResponse.NotInstalled;
            var c = Stop();
            switch (c)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.NotRunning:
                case ServiceResponse.AlreadyStopping:
                    break;
                default:
                    return c;
            }
            foreach (var cmd in GetUninstallCommands(SName, false))
                SystemHelper.GetStdOutFrom(out _, cmd);
            if (!FileHelper.DeleteFile(ScriptName))
                return ServiceResponse.UninstallFailed;
            if (!FileHelper.DeleteFile(PidName))
                return ServiceResponse.UninstallFailed;
            //  Unload the removed unit, else systemd still has it in memory (and could restart it)
            foreach (var cmd in GetUninstallCommands(SName, true))
                SystemHelper.GetStdOutFrom(out _, cmd);
            return ServiceResponse.Ok;
        }

        /// <summary>
        /// Get the systemctl commands that <see cref="Uninstall"/> runs (the results are ignored).
        /// </summary>
        /// <param name="name">The service (unit) name.</param>
        /// <param name="afterRemove">False for the commands to run before the unit file is removed ("disable --now", that also stops a unit that was started meanwhile),
        /// true for the commands to run after it's removed ("daemon-reload", "reset-failed").</param>
        /// <returns>The command lines.</returns>
        static String[] GetUninstallCommands(String name, bool afterRemove) => afterRemove ?
            [
                "systemctl daemon-reload",
                Systemctl("reset-failed", name),
            ]
            :
            [
                Systemctl("disable --now", name),
            ];


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
        /// Have systemd restart the service, called from the daemon process (ex: <see cref="ServiceManager.RestartProcess"/>, or the auto recovery after a faulty manifest).
        /// Runs "systemctl --no-block restart [Name]": the restart is a job of systemd itself, so it isn't killed when systemd stops the processes of the unit
        /// (a "[command] restart" child process of the daemon is in the cgroup of the unit and would be killed by the stop, leaving the service stopped).
        /// Requires the daemon to run as root (User=root, the default when installed with sudo).
        /// </summary>
        /// <param name="man">The service manager used to log the restart, or null to write to the console.</param>
        /// <param name="startOnly">Ignored, systemd always restarts (a restart of a stopping / failed unit starts it).</param>
        public void RequestRestart(ServiceManager man, bool startOnly)
        {
            var cmd = GetRestartCommand(SName);
            var msg = "Restarting service using command: " + cmd;
            if (man == null)
                Console.WriteLine(msg);
            else
                man.AddMessage(msg);
            SystemHelper.GetStdOutFrom(out var ret, cmd);
            if (ret == 0)
                return;
            msg = "Failed to restart the service, " + cmd.ToQuoted() + " exited with code " + ret;
            if (man == null)
                Console.WriteLine(msg);
            else
                man.AddMessage(msg, MessageLevels.Warning);
        }

        /// <summary>
        /// Get the command used by <see cref="RequestRestart"/>.
        /// </summary>
        /// <param name="name">The service (unit) name.</param>
        /// <returns>The command line, ex: systemctl --no-block restart "MyService".</returns>
        static String GetRestartCommand(String name) => Systemctl("--no-block restart", name);

        /// <summary>
        /// The daemon main loop: creates the <see cref="ServiceManager"/>, writes the status file and handles signals until SIGTERM, SIGHUP or SIGQUIT is received.
        /// A restart requested by the service manager is done by systemd, see <see cref="RequestRestart"/>.
        /// </summary>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created.</param>
        /// <returns>Always 0.</returns>
        public int Run(Action<ServiceManager> onStart)
        {
            WriteStatus(ServiceStatus.StartPending);
            using (var manager = new ServiceManager(true, null, man => RequestRestart(man, false)))
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
                    manager.AddMessage(MessageLevels.Info, $"Got signal: {c.Signal}");
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
        /// Make a free text value safe for a unit file setting that resolves specifiers (ex: Description=, User=), see systemd.syntax(7) and systemd.unit(5).
        /// Control characters (including line breaks) are replaced by spaces, '%' becomes "%%".
        /// A value ending with an odd number of backslashes gets a trailing space (systemd strips it), else the line would be continued with the next one.
        /// Note: systemd strips leading and trailing white space of a value, these values are not unescaped.
        /// </summary>
        /// <param name="value">The value, null is treated as an empty string.</param>
        /// <returns>The value to write after the "=".</returns>
        static String UnitValue(String value)
        {
            if (String.IsNullOrEmpty(value))
                return "";
            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (Char.IsControl(c))
                    sb.Append(' ');
                else if (c == '%')
                    sb.Append("%%");
                else
                    sb.Append(c);
            }
            int bs = 0;
            for (int i = sb.Length - 1; (i >= 0) && (sb[i] == '\\'); --i)
                ++bs;
            if ((bs & 1) != 0)
                sb.Append(' ');
            return sb.ToString();
        }

        /// <summary>
        /// Quote / escape a word of an ExecStart= command line (see "Command lines" in systemd.service(5) and "Quoting" in systemd.syntax(7)):
        /// '%' becomes "%%" (specifiers), '$' becomes "$$" if <paramref name="envExpanded"/> (environment variable substitution).
        /// A word that is empty or contains white space, quotes, backslashes or control characters is double quoted, with '\' and '"' backslash escaped
        /// and control characters written as C escapes. A lone ";" (the command separator) is written as "\;".
        /// </summary>
        /// <param name="word">The word (the argument value as the process should get it).</param>
        /// <param name="envExpanded">True if systemd substitutes environment variables in this word (all arguments, including argv[0], but not the executable path).</param>
        /// <returns>The text to write.</returns>
        /// <exception cref="NotSupportedException">The word contains a NUL character (can't be passed to a process).</exception>
        static String ExecWord(String word, bool envExpanded)
        {
            if (word == ";")
                return "\\;";
            bool quote = word.Length == 0;
            foreach (var c in word)
            {
                if (Char.IsWhiteSpace(c) || Char.IsControl(c) || (c == '"') || (c == '\'') || (c == '\\'))
                {
                    quote = true;
                    break;
                }
            }
            var sb = new StringBuilder(word.Length + 8);
            if (quote)
                sb.Append('"');
            foreach (var c in word)
            {
                switch (c)
                {
                    case '%':
                        sb.Append("%%");
                        break;
                    case '$':
                        sb.Append(envExpanded ? "$$" : "$");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\0':
                        throw new NotSupportedException("A command line argument for systemd can't contain a NUL character");
                    default:
                        if (Char.IsControl(c))
                        {
                            //  \xNN is a raw byte (must be ASCII to keep the argument valid UTF-8), \uNNNN is UTF-8 encoded
                            if (c < 0x80)
                                sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                            else
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                            sb.Append(c);
                        break;
                }
            }
            if (quote)
                sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Create the value of the ExecStart= setting (see "Command lines" in systemd.service(5)), every word is quoted / escaped as needed (see <see cref="ExecWord"/>).
        /// The executable path isn't subject to environment variable substitution (but argv[0] is), so if the path contains a '$', the "@" prefix is used
        /// to pass argv[0] (with "$$") separately: "@/opt/a$b/app" "/opt/a$$b/app" ...
        /// </summary>
        /// <param name="exe">The absolute path of the executable.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The ExecStart= value, ex: /usr/bin/dotnet "/opt/my app/MyService.dll" daemon</returns>
        /// <exception cref="NotSupportedException">The executable path isn't absolute or contains characters that systemd refuses in an executable path
        /// (control characters, quotes, backslashes, '*', '?', '['), or an argument contains a NUL character.</exception>
        static String CreateExecStart(String exe, IEnumerable<String> args)
        {
            exe ??= "";
            if ((exe.Length < 2) || (exe[0] != '/') || (exe[exe.Length - 1] == '/'))
                throw new NotSupportedException("The executable path " + exe.ToQuoted() + " must be an absolute file path to be used in a systemd unit");
            foreach (var c in exe)
                if (Char.IsControl(c) || (c == '"') || (c == '\'') || (c == '\\') || (c == '*') || (c == '?') || (c == '['))
                    throw new NotSupportedException("The executable path " + exe.ToQuoted() + " contains characters that systemd doesn't allow in an executable path (control characters, quotes, backslashes, '*', '?', '[')");
            var words = new List<String>();
            if (exe.IndexOf('$') >= 0)
            {
                words.Add(ExecWord("@" + exe, false));
                words.Add(ExecWord(exe, true));
            }
            else
                words.Add(ExecWord(exe, false));
            foreach (var a in args)
                words.Add(ExecWord(a ?? "", true));
            return String.Join(' ', words);
        }

        /// <summary>
        /// Create the systemd unit text (LF line endings).
        /// All values are escaped (see <see cref="UnitValue"/> and <see cref="CreateExecStart"/>), so names and paths may contain spaces, quotes, '%', '$' and line breaks
        /// (in the description they are replaced by spaces).
        /// </summary>
        /// <param name="displayName">The display name (Description=).</param>
        /// <param name="user">The user to run the service as (User=), an empty value means root.</param>
        /// <param name="cmd">The command that starts the application (as <see cref="ServiceHost.GetCommand(string)"/>), ex: "/usr/bin/dotnet "/opt/my app/MyService.dll"".
        /// The executable is started with the (single, unquoted) argument if any, followed by "daemon".</param>
        /// <returns>The unit text.</returns>
        /// <exception cref="NotSupportedException">The executable can't be used in a systemd unit, see <see cref="CreateExecStart"/>.</exception>
        static String CreateUnit(String displayName, String user, String cmd)
        {
            var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
            //  The argument is a single (quoted if it contains spaces) path, see EnvInfo.ExecCommand
            if ((args.Length >= 2) && (args[0] == '"') && (args[args.Length - 1] == '"'))
                args = args.Substring(1, args.Length - 2);
            var argList = args.Length > 0 ? new[] { args, "daemon" } : new[] { "daemon" };
            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Description",    UnitValue(displayName) },
                { "User",           UnitValue(user) },
                { "ExecStart",      CreateExecStart(exe, argList) },
            };
            return new TextTemplate(Template).Get(vars).Replace("\r", "");
        }

        /// <summary>
        /// The unit file, see <see cref="CreateUnit"/>.
        /// </summary>
        static readonly String Template = @"
[Unit]
Description=$(Description)
After=network.target
StartLimitIntervalSec=0

[Service]
Type=simple
Restart=always
RestartSec=60
User=$(User)
ExecStart=$(ExecStart)

[Install]
WantedBy=multi-user.target


";

    }

}
