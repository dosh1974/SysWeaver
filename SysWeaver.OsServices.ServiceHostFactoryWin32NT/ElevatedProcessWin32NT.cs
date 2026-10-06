using System;
using System.Diagnostics;
using System.Security.Principal;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Windows helpers for checking and requesting elevation (administrator rights).
    /// </summary>
    public static class ElevatedProcessWin32NT
    {

        /// <summary>
        /// True if the process is running elevated (the current identity is in the built-in Administrators role)
        /// </summary>
        public static bool IsElevated
        {
            get
            {
                using var i = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(i).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Start a command line elevated using the shell "runas" verb (shows a UAC prompt), optionally waiting for it to exit.
        /// </summary>
        /// <param name="commandLine">The command line to execute</param>
        /// <param name="terminal">If true a new console window is shown, else the window is hidden</param>
        /// <param name="noWait">If true, this method doesn't wait for a result of the started process, it just returns 0</param>
        /// <returns>The exit code of the command or 0 if noWait is true</returns>
        /// <exception cref="System.ComponentModel.Win32Exception">The process couldn't be started, ex: the user declined the UAC prompt.</exception>
        public static int RunElevated(String commandLine, bool terminal, bool noWait)
        {
            var cmd = SystemHelper.GetCommandAndArgs(out var args, commandLine);
            var p = new Process();
            var startInfo = p.StartInfo;
            startInfo.WindowStyle = terminal ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
            startInfo.FileName = cmd;
            startInfo.Arguments = args;
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            p.Start();
            if (noWait)
                return 0;
            p.WaitForExit();
            return p.ExitCode;
        }

    }


}
