using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    public static class UnixHelpers
    {

        [DllImport("libc", SetLastError = true)]
        static extern uint geteuid();


        [DllImport("libc", SetLastError = true)]
        static extern int kill(int pid, int sig);


        /// <summary>
        /// True if the process is running elevated
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
        /// Ensure that the process runs elevated (a new process that is elevated will be spawned if required)
        /// </summary>
        /// <param name="commandLine">The command line to execute</param>
        /// <param name="terminal">If true a new console windows is shown</param>
        /// <param name="noWait">If true, this method doesn't wait for a result of the started process, it just returns 0</param>
        /// <returns>The exit code of the command or 0 if noWait is true</returns>
        public static int RunElevated(String commandLine, bool terminal, bool noWait)
        {
            commandLine = "sudo " + commandLine;
            if (terminal)
                return SystemHelper.Run(commandLine);
            SystemHelper.GetStdOutFrom(out var r, commandLine);
            return r;
        }


        public enum Signals : int
        { 
            /// Hangup (POSIX).
            SIGHUP = 1,
            /// Interrupt (ANSI).
            SIGINT = 2,
            /// Quit (POSIX).
            SIGQUIT = 3,
            /// Illegal instruction (ANSI).
            SIGILL = 4,
            /// Trace trap (POSIX).
            SIGTRAP = 5,
            /// Abort (ANSI).
            SIGABRT = 6,
            /// IOT trap (4.2 BSD).
            SIGIOT = 6,
            /// BUS error (4.2 BSD).
            SIGBUS = 7,
            /// Floating-point exception (ANSI).
            SIGFPE = 8,
            /// Kill, unblockable (POSIX).
            SIGKILL = 9,
            /// User-defined signal 1 (POSIX).
            SIGUSR1 = 10,
            /// Segmentation violation (ANSI).
            SIGSEGV = 11,
            /// User-defined signal 2 (POSIX).
            SIGUSR2 = 12,
            /// Broken pipe (POSIX).
            SIGPIPE = 13,
            /// Alarm clock (POSIX).
            SIGALRM = 14,
            /// Termination (ANSI).
            SIGTERM = 15,
            /// Stack fault.
            SIGSTKFLT = 16,
            /// Same as SIGCHLD (System V).
            SIGCLD = SIGCHLD,
            /// Child status has changed (POSIX).
            SIGCHLD = 17,
            /// Continue (POSIX).
            SIGCONT = 18,
            /// Stop, unblockable (POSIX).
            SIGSTOP = 19,
            /// Keyboard stop (POSIX).
            SIGTSTP = 20,
            /// Background read from tty (POSIX).
            SIGTTIN = 21,
            /// Background write to tty (POSIX).
            SIGTTOU = 22,
            /// Urgent condition on socket (4.2 BSD).
            SIGURG = 23,
            /// CPU limit exceeded (4.2 BSD).
            SIGXCPU = 24,
            /// File size limit exceeded (4.2 BSD).
            SIGXFSZ = 25,
            /// Virtual alarm clock (4.2 BSD).
            SIGVTALRM = 26,
            /// Profiling alarm clock (4.2 BSD).
            SIGPROF = 27,
            /// Window size change (4.3 BSD, Sun).
            SIGWINCH = 28,
            /// Pollable event occurred (System V).
            SIGPOLL = SIGIO,
            /// I/O now possible (4.2 BSD).
            SIGIO = 29,
            /// Power failure restart (System V).
            SIGPWR = 30,
            /// Bad system call.
            SIGSYS = 31, 
            SIGUNUSED = 31
        }

        /// <summary>
        /// Send a posix/unix signal to a process
        /// </summary>
        /// <param name="processId">The process id</param>
        /// <param name="signal">The signal to send</param>
        /// <returns>The return code, 0 = success</returns>
        public static int SendSignal(int processId, Signals signal) => kill(processId, (int)signal);


    }
}
