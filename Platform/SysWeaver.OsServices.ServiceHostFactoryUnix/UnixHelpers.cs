using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Unix (libc) helpers for elevation and signals.
    /// </summary>
    public static class UnixHelpers
    {

        [DllImport("libc", SetLastError = true)]
        static extern uint geteuid();


        [DllImport("libc", SetLastError = true)]
        static extern int kill(int pid, int sig);


        /// <summary>
        /// True if the process is running elevated (effective user id is 0, root)
        /// </summary>
        public static bool IsElevated
        {
            get
            {
                var uid = geteuid();
                return uid == 0;
            }
        }

        /// <summary>
        /// Run a command line elevated by prefixing it with "sudo" (may prompt for a password), and wait for it to exit.
        /// </summary>
        /// <param name="commandLine">The command line to execute</param>
        /// <param name="terminal">If true the command shares this console, else its stdout is captured (and discarded)</param>
        /// <param name="noWait">Ignored, this method always waits for the process to exit</param>
        /// <returns>The exit code of the command, or 1 (<see cref="ServiceResponseHelper.ExitGenericError"/>) if "sudo" couldn't be started (a message is written to the console,
        /// ex: "sudo" isn't installed)</returns>
        public static int RunElevated(String commandLine, bool terminal, bool noWait)
        {
            commandLine = "sudo " + commandLine;
            int r;
            if (terminal)
                r = SystemHelper.Run(commandLine);
            else
                SystemHelper.GetStdOutFrom(out r, commandLine);
            if (r >= 0)
                return r;
            //  The process couldn't be started (Run returns -404, GetStdOutFrom -1)
            Console.WriteLine(GetElevationFailedMessage(FindInPath("sudo", Environment.GetEnvironmentVariable("PATH")) != null));
            return ServiceResponseHelper.ExitGenericError;
        }

        /// <summary>
        /// Get the message written when "sudo" couldn't be started.
        /// </summary>
        /// <param name="sudoFound">True if "sudo" was found in the PATH.</param>
        /// <returns>The message.</returns>
        static String GetElevationFailedMessage(bool sudoFound) => sudoFound ?
            "Failed to run the command elevated: \"sudo\" couldn't be started, run the command as root instead."
            :
            "Failed to run the command elevated: \"sudo\" not found (not installed or not in the PATH), install it or run the command as root.";

        /// <summary>
        /// Find a program in a search path.
        /// </summary>
        /// <param name="program">The program file name, ex: "sudo".</param>
        /// <param name="searchPath">The folders, separated by <see cref="System.IO.Path.PathSeparator"/> (':' on Linux), ex: the PATH environment variable. Null or empty folders are skipped.</param>
        /// <returns>The full name of the first existing file, or null if not found.</returns>
        static String FindInPath(String program, String searchPath)
        {
            if (String.IsNullOrEmpty(searchPath))
                return null;
            foreach (var d in searchPath.Split(System.IO.Path.PathSeparator))
            {
                if (d.Length <= 0)
                    continue;
                try
                {
                    var f = System.IO.Path.Combine(d, program);
                    if (System.IO.File.Exists(f))
                        return f;
                }
                catch
                {
                    //  Invalid folder name, skip
                }
            }
            return null;
        }


        /// <summary>
        /// Linux signal numbers (x86/ARM numbering).
        /// </summary>
        public enum Signals : int
        { 
            /// <summary>
            /// Hangup (POSIX).
            /// </summary>
            SIGHUP = 1,
            /// <summary>
            /// Interrupt (ANSI).
            /// </summary>
            SIGINT = 2,
            /// <summary>
            /// Quit (POSIX).
            /// </summary>
            SIGQUIT = 3,
            /// <summary>
            /// Illegal instruction (ANSI).
            /// </summary>
            SIGILL = 4,
            /// <summary>
            /// Trace trap (POSIX).
            /// </summary>
            SIGTRAP = 5,
            /// <summary>
            /// Abort (ANSI).
            /// </summary>
            SIGABRT = 6,
            /// <summary>
            /// IOT trap (4.2 BSD).
            /// </summary>
            SIGIOT = 6,
            /// <summary>
            /// BUS error (4.2 BSD).
            /// </summary>
            SIGBUS = 7,
            /// <summary>
            /// Floating-point exception (ANSI).
            /// </summary>
            SIGFPE = 8,
            /// <summary>
            /// Kill, unblockable (POSIX).
            /// </summary>
            SIGKILL = 9,
            /// <summary>
            /// User-defined signal 1 (POSIX).
            /// </summary>
            SIGUSR1 = 10,
            /// <summary>
            /// Segmentation violation (ANSI).
            /// </summary>
            SIGSEGV = 11,
            /// <summary>
            /// User-defined signal 2 (POSIX).
            /// </summary>
            SIGUSR2 = 12,
            /// <summary>
            /// Broken pipe (POSIX).
            /// </summary>
            SIGPIPE = 13,
            /// <summary>
            /// Alarm clock (POSIX).
            /// </summary>
            SIGALRM = 14,
            /// <summary>
            /// Termination (ANSI).
            /// </summary>
            SIGTERM = 15,
            /// <summary>
            /// Stack fault.
            /// </summary>
            SIGSTKFLT = 16,
            /// <summary>
            /// Same as SIGCHLD (System V).
            /// </summary>
            SIGCLD = SIGCHLD,
            /// <summary>
            /// Child status has changed (POSIX).
            /// </summary>
            SIGCHLD = 17,
            /// <summary>
            /// Continue (POSIX).
            /// </summary>
            SIGCONT = 18,
            /// <summary>
            /// Stop, unblockable (POSIX).
            /// </summary>
            SIGSTOP = 19,
            /// <summary>
            /// Keyboard stop (POSIX).
            /// </summary>
            SIGTSTP = 20,
            /// <summary>
            /// Background read from tty (POSIX).
            /// </summary>
            SIGTTIN = 21,
            /// <summary>
            /// Background write to tty (POSIX).
            /// </summary>
            SIGTTOU = 22,
            /// <summary>
            /// Urgent condition on socket (4.2 BSD).
            /// </summary>
            SIGURG = 23,
            /// <summary>
            /// CPU limit exceeded (4.2 BSD).
            /// </summary>
            SIGXCPU = 24,
            /// <summary>
            /// File size limit exceeded (4.2 BSD).
            /// </summary>
            SIGXFSZ = 25,
            /// <summary>
            /// Virtual alarm clock (4.2 BSD).
            /// </summary>
            SIGVTALRM = 26,
            /// <summary>
            /// Profiling alarm clock (4.2 BSD).
            /// </summary>
            SIGPROF = 27,
            /// <summary>
            /// Window size change (4.3 BSD, Sun).
            /// </summary>
            SIGWINCH = 28,
            /// <summary>
            /// Pollable event occurred (System V).
            /// </summary>
            SIGPOLL = SIGIO,
            /// <summary>
            /// I/O now possible (4.2 BSD).
            /// </summary>
            SIGIO = 29,
            /// <summary>
            /// Power failure restart (System V).
            /// </summary>
            SIGPWR = 30,
            /// <summary>
            /// Bad system call.
            /// </summary>
            SIGSYS = 31, 
            /// <summary>
            /// Unused signal, same as SIGSYS.
            /// </summary>
            SIGUNUSED = 31
        }

        /// <summary>
        /// Send a posix/unix signal to a process (libc kill)
        /// </summary>
        /// <param name="processId">The process id</param>
        /// <param name="signal">The signal to send</param>
        /// <returns>The return code, 0 = success, -1 = failure</returns>
        public static int SendSignal(int processId, Signals signal) => kill(processId, (int)signal);


    }
}
