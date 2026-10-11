using System;
using System.Diagnostics;
using System.IO;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Parsing and validation of the status file ("/var/run/[Name].service.pid", content: "[ServiceStatus] [pid]") written by the daemon process.
    /// </summary>
    static class StatusFile
    {
        /// <summary>
        /// The argument that the daemon process is started with (the last argument of its command line).
        /// </summary>
        const String DaemonArg = "daemon";

        /// <summary>
        /// Parse the status file text and validate the pid.
        /// </summary>
        /// <param name="text">The text of the status file, ex: "Running 1234".</param>
        /// <param name="isDaemon">Returns true if the process with the given pid is the daemon process (only called for a status that isn't <see cref="ServiceStatus.Stopped"/>).</param>
        /// <param name="status">The status, <see cref="ServiceStatus.Stopped"/> if the recorded process isn't the (running) daemon, undefined if false is returned.</param>
        /// <param name="pid">The pid of the daemon, 0 if the status is <see cref="ServiceStatus.Stopped"/> because the recorded process isn't the daemon (or the pid is 0).</param>
        /// <returns>True if the text could be parsed, false if not (ex: a partially written file, the pid must be a non-negative integer without sign).</returns>
        public static bool TryGetStatus(String text, Func<int, bool> isDaemon, out ServiceStatus status, out int pid)
        {
            pid = 0;
            status = ServiceStatus.Unknown;
            if (text == null)
                return false;
            var kv = text.Trim().Split(' ');
            if (kv.Length != 2)
                return false;
            if (!Enum.TryParse(kv[0], out status))
                return false;
            if (!int.TryParse(kv[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out pid))
                return false;
            if (status == ServiceStatus.Stopped)
                return true;
            //  A pid of 0 or less would signal a process group (kill), never trust it
            if ((pid <= 0) || (!isDaemon(pid)))
            {
                status = ServiceStatus.Stopped;
                pid = 0;
            }
            return true;
        }

        /// <summary>
        /// Check if a process exists and (on Linux, if its command line can be read) is a daemon process of this application.
        /// </summary>
        /// <param name="pid">The process id.</param>
        /// <returns>True if the process exists and its command line (if readable) looks like the daemon of this application, see <see cref="IsDaemonCommandLine"/>.</returns>
        public static bool IsDaemonProcess(int pid)
        {
            if (pid <= 0)
                return false;
            Process p;
            try
            {
                p = Process.GetProcessById(pid);
            }
            catch
            {
                //  No process with that id
                return false;
            }
            using (p)
            {
                try
                {
                    if (p.HasExited)
                        return false;
                }
                catch
                {
                    //  Not allowed to query it: it exists
                }
            }
            String cmdLine;
            try
            {
                cmdLine = File.ReadAllText("/proc/" + pid.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/cmdline");
            }
            catch
            {
                //  Not Linux, or not allowed to read it: trust the pid
                return true;
            }
            return IsDaemonCommandLine(cmdLine, Path.GetFileNameWithoutExtension(EnvInfo.Executable));
        }

        /// <summary>
        /// Check if a process command line (as in "/proc/[pid]/cmdline": the arguments separated and terminated by NUL characters) is a daemon of an application:
        /// the last argument must be "daemon" and one of the other arguments must be a path with the file name (ignoring the extension) of the application,
        /// ex: "/usr/bin/dotnet\0/opt/app/MyService.dll\0daemon\0" or "/opt/app/MyService\0daemon\0" for "MyService".
        /// </summary>
        /// <param name="cmdLine">The NUL separated command line.</param>
        /// <param name="appName">The file name of the executable or entry assembly without the extension (compared case sensitive), ex: "MyService".</param>
        /// <returns>True if the command line is a daemon of the application.</returns>
        public static bool IsDaemonCommandLine(String cmdLine, String appName)
        {
            if (String.IsNullOrEmpty(cmdLine) || String.IsNullOrEmpty(appName))
                return false;
            var args = cmdLine.TrimEnd('\0').Split('\0');
            var l = args.Length - 1;
            if ((l < 1) || (!String.Equals(args[l], DaemonArg, StringComparison.Ordinal)))
                return false;
            for (int i = 0; i < l; ++i)
            {
                var a = args[i];
                if (a.Length <= 0)
                    continue;
                if (String.Equals(Path.GetFileNameWithoutExtension(a), appName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

    }

}
