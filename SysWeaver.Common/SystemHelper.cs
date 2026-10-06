using System;
using System.Diagnostics;

namespace SysWeaver
{
    /// <summary>
    /// Helpers for running external programs from a single command line string (no shell is used).
    /// </summary>
    public static class SystemHelper
    {
        /// <summary>
        /// Split a command line string to the program name and arguments.
        /// The program name ends at the first space, or if it starts with a single or double quote, at the matching closing quote.
        /// </summary>
        /// <param name="args">The parsed arguments, ex "test.txt" (leading white space trimmed, empty if there are no arguments)</param>
        /// <param name="commandLine">The input command line, ex "notepad test.txt" (surrounding white space is ignored)</param>
        /// <returns>The parsed command, ex: "notepad" (without quotes). If a quoted command has no closing quote, the whole command line (including the opening quote) is returned.</returns>
        /// <exception cref="NullReferenceException"><paramref name="commandLine"/> is null.</exception>
        public static String GetCommandAndArgs(out String args, String commandLine)
        {
            commandLine = commandLine.Trim();
            args = "";
            int start = 0;
            Char end = ' ';
            var cl = commandLine.Length;
            if (cl <= 0)
                return "";
            var first = commandLine[0];
            if ((first == 39) || (first == '"'))
            {
                ++start;
                end = first;
            }
            var i = commandLine.IndexOf(end, start);
            if (i < 0)
                return commandLine;
            args = commandLine.Substring(i + 1).TrimStart();
            return commandLine.Substring(start, i - start);
        }


        /// <summary>
        /// Executes a command line and returns all text from stdout, see <see cref="GetStdOutFrom(out int, string)"/>
        /// </summary>
        /// <param name="commandline">The command line, ex "ls -l". The program is started directly (not through a shell).</param>
        /// <returns>The text outputted to stdout from the executed program or null if it failed to start</returns>
        public static String GetStdOutFrom(String commandline) => GetStdOutFrom(out var _, commandline);

        /// <summary>
        /// Executes a command line and returns all text from stdout
        /// </summary>
        /// <param name="exitCode">The exit code of the process or -1 if it failed to start</param>
        /// <param name="commandline">The command line, ex "ls -l". The program is started directly (not through a shell), so shell built-ins and redirections don't work.</param>
        /// <returns>The text outputted to stdout from the executed program or null if it failed to start</returns>
        /// <remarks>
        /// Blocks until the process exits, no timeout. Stderr is redirected and discarded (not returned and not written to the console).
        /// </remarks>
        public static String GetStdOutFrom(out int exitCode, String commandline)
        {
            var cmd = GetCommandAndArgs(out var args, commandline);
            try
            {
                using Process p = new();
                var s = p.StartInfo;
                s.UseShellExecute = false;
                s.RedirectStandardOutput = true;
                s.RedirectStandardError = true;
                s.FileName = cmd;
                s.Arguments = args;
                //  Drain (and discard) stderr asynchronously, so that the process can't block on a full stderr pipe
                p.ErrorDataReceived += (sender, e) => { };
                p.Start();
                p.BeginErrorReadLine();
                var o = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                exitCode = p.ExitCode;
                return o;
            }
            catch
            {
                exitCode = -1;
                return null;
            }
        }


        /// <summary>
        /// Executes a command line, waits (no timeout) for it to exit and returns the exit code.
        /// The output is not redirected (inherits the console of the current process).
        /// </summary>
        /// <param name="commandline">The command line, ex "systemctl daemon-reload". The program is started directly (not through a shell).</param>
        /// <returns>The exit code, any exception (ex: program not found) will return -404 as an exit code</returns>
        public static int Run(String commandline)
        {
            var cmd = GetCommandAndArgs(out var args, commandline);
            try
            {
                using Process p = new();
                var s = p.StartInfo;
                s.UseShellExecute = false;
                s.FileName = cmd;
                s.Arguments = args;
                p.Start();
                p.WaitForExit();
                return p.ExitCode;
            }
            catch
            {
                return -404;
            }
        }
    }
}
