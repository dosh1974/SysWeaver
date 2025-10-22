using System;
using System.Diagnostics;

namespace SysWeaver
{
    public static class SystemHelper
    {
        /// <summary>
        /// Split a command line string to the program name and arguments
        /// </summary>
        /// <param name="args">The parsed arguments, ex "test.txt"</param>
        /// <param name="commandLine">The input command line, ex "notepad test.txt"</param>
        /// <returns>The parsed command, ex: "notepad"</returns>
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
        /// Executes a command line and returns all text from stdout
        /// </summary>
        /// <param name="commandline">The command line, ex "dir *.dll /b"</param>
        /// <returns>The text outputted to stdout from the executed program or null if it failed to start</returns>
        public static String GetStdOutFrom(String commandline) => GetStdOutFrom(out var _, commandline);

        /// <summary>
        /// Executes a command line and returns all text from stdout
        /// </summary>
        /// <param name="exitCode">The exit code of the process or -1 if it failed to start</param>
        /// <param name="commandline">The command line, ex "dir *.dll /b"</param>
        /// <returns>The text outputted to stdout from the executed program or null if it failed to start</returns>
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
                p.Start();
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
        /// Executes a command line and returns the exit code
        /// </summary>
        /// <param name="commandline">The command line, ex "dir *.dll /b"</param>
        /// <returns>The exit code, any exception will return -404 as an exit code</returns>
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
