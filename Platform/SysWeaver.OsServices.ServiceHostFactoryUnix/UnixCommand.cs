using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Runs programs with an argument list (no shell, so arguments are never interpreted) and captures stdout and stderr.
    /// </summary>
    static class UnixCommand
    {
        /// <summary>
        /// Max time to wait for a command (ms)
        /// </summary>
        const int Timeout = 60 * 1000;

        /// <summary>
        /// Find a program in the PATH (and the common sbin folders, they are not always in the PATH of a service).
        /// </summary>
        /// <param name="program">The program name, ex: "ufw"</param>
        /// <returns>The full name or null if not found</returns>
        public static String Find(String program)
        {
            var path = (Environment.GetEnvironmentVariable("PATH") ?? "") + ":/usr/local/sbin:/usr/sbin:/sbin:/usr/local/bin:/usr/bin:/bin";
            foreach (var d in path.Split(':'))
            {
                if (d.Length <= 0)
                    continue;
                try
                {
                    var f = Path.Combine(d, program);
                    if (File.Exists(f))
                        return f;
                }
                catch
                {
                }
            }
            return null;
        }

        /// <summary>
        /// Run a program and wait for it to exit.
        /// </summary>
        /// <param name="stdOut">The text written to stdout</param>
        /// <param name="stdErr">The text written to stderr</param>
        /// <param name="program">The program to run</param>
        /// <param name="args">The arguments</param>
        /// <returns>The exit code, -1 if the program couldn't be started (stdErr contains the reason)</returns>
        public static int Run(out String stdOut, out String stdErr, String program, IEnumerable<String> args)
            => Run(out stdOut, out stdErr, program, args, null);

        /// <summary>
        /// Run a program and wait for it to exit.
        /// </summary>
        /// <param name="stdOut">The text written to stdout</param>
        /// <param name="stdErr">The text written to stderr</param>
        /// <param name="program">The program to run</param>
        /// <param name="args">The arguments</param>
        /// <param name="input">Text written to stdin (null for none)</param>
        /// <returns>The exit code, -1 if the program couldn't be started (stdErr contains the reason)</returns>
        public static int Run(out String stdOut, out String stdErr, String program, IEnumerable<String> args, String input)
        {
            try
            {
                using var p = new Process();
                var s = p.StartInfo;
                s.FileName = program;
                foreach (var a in args)
                    s.ArgumentList.Add(a);
                s.UseShellExecute = false;
                s.RedirectStandardOutput = true;
                s.RedirectStandardError = true;
                s.RedirectStandardInput = true;
                s.Environment["LC_ALL"] = "C";
                s.Environment["SYSTEMD_COLORS"] = "0";
                var err = new StringBuilder();
                p.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        lock (err)
                            err.AppendLine(e.Data);
                };
                p.Start();
                if (input != null)
                    p.StandardInput.Write(input);
                p.StandardInput.Close();
                p.BeginErrorReadLine();
                var o = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(Timeout))
                {
                    try
                    {
                        p.Kill(true);
                    }
                    catch
                    {
                    }
                    stdOut = "";
                    stdErr = "Timed out";
                    return -1;
                }
                p.WaitForExit();
                stdOut = o.Result;
                lock (err)
                    stdErr = err.ToString();
                return p.ExitCode;
            }
            catch (Exception ex)
            {
                stdOut = "";
                stdErr = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// Run a program, returns stdout or throws an exception (with stderr) if the exit code isn't zero.
        /// </summary>
        /// <param name="program">The program to run</param>
        /// <param name="args">The arguments</param>
        /// <returns>The text written to stdout</returns>
        public static String Get(String program, params String[] args)
        {
            var r = Run(out var o, out var e, program, args);
            if (r != 0)
                throw new Exception(Error(program, args, r, o, e));
            return o;
        }

        /// <summary>
        /// Run a program that changes something, if the process isn't running as root it's run using "sudo -n" (fails if a password is required).
        /// Throws an exception (with stderr) if the exit code isn't zero.
        /// </summary>
        /// <param name="program">The program to run</param>
        /// <param name="args">The arguments</param>
        /// <returns>The text written to stdout</returns>
        public static String Change(String program, params String[] args)
        {
            if (UnixHelpers.IsElevated)
                return Get(program, args);
            var sudo = Find("sudo") ?? throw new Exception("Root access is required (run the server as root or install sudo)!");
            var a = new List<String> { "-n", program };
            a.AddRange(args);
            var r = Run(out var o, out var e, sudo, a);
            if (r != 0)
                throw new Exception(Error(program, args, r, o, e));
            return o;
        }

        /// <summary>
        /// Like <see cref="Change(String, String[])"/> but with text written to stdin.
        /// </summary>
        /// <param name="input">Text written to stdin</param>
        /// <param name="program">The program to run</param>
        /// <param name="args">The arguments</param>
        /// <returns>The text written to stdout</returns>
        public static String ChangeWithInput(String input, String program, params String[] args)
        {
            int r;
            String o, e;
            if (UnixHelpers.IsElevated)
            {
                r = Run(out o, out e, program, args, input);
            }
            else
            {
                var sudo = Find("sudo") ?? throw new Exception("Root access is required (run the server as root or install sudo)!");
                var a = new List<String> { "-n", program };
                a.AddRange(args);
                r = Run(out o, out e, sudo, a, input);
            }
            if (r != 0)
                throw new Exception(Error(program, args, r, o, e));
            return o;
        }

        /// <summary>
        /// Write a text file that may be owned by root (ex: /etc/environment).
        /// If the process isn't root (and can't write the file) the text is written to a temp file and copied using "sudo -n cp" (keeps the owner and mode of an existing file).
        /// </summary>
        /// <param name="file">The full path of the file</param>
        /// <param name="text">The new content</param>
        public static void WriteFile(String file, String text)
        {
            try
            {
                File.WriteAllText(file, text);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (UnixHelpers.IsElevated)
                    throw;
            }
            var tmp = Path.Combine(Path.GetTempPath(), "sysweaver_" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(tmp, text);
                Change(Find("cp") ?? "/bin/cp", "--", tmp, file);
            }
            finally
            {
                try
                {
                    File.Delete(tmp);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Delete a file that may be owned by root (uses "sudo -n rm" if needed).
        /// </summary>
        /// <param name="file">The full path of the file</param>
        public static void DeleteFile(String file)
        {
            try
            {
                File.Delete(file);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (UnixHelpers.IsElevated)
                    throw;
            }
            Change(Find("rm") ?? "/bin/rm", "--", file);
        }

        static String Error(String program, String[] args, int code, String o, String e)
        {
            var msg = (e ?? "").Trim();
            if (msg.Length <= 0)
                msg = (o ?? "").Trim();
            if (msg.Length <= 0)
                msg = "Exit code " + code;
            return String.Concat("\"", Path.GetFileName(program), " ", String.Join(' ', args), "\" failed: ", msg);
        }

        /// <summary>
        /// Split a line into words, like a shell (single and double quotes, backslash escapes).
        /// </summary>
        /// <param name="line">The line</param>
        /// <returns>The words</returns>
        public static List<String> Split(String line)
        {
            var res = new List<String>();
            var sb = new StringBuilder();
            bool inWord = false;
            char quote = '\0';
            for (int i = 0; i < line.Length; ++i)
            {
                var c = line[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                        continue;
                    }
                    if ((c == '\\') && (quote == '"') && (i + 1 < line.Length))
                    {
                        sb.Append(line[++i]);
                        continue;
                    }
                    sb.Append(c);
                    continue;
                }
                if (Char.IsWhiteSpace(c))
                {
                    if (inWord)
                    {
                        res.Add(sb.ToString());
                        sb.Clear();
                        inWord = false;
                    }
                    continue;
                }
                inWord = true;
                if ((c == '"') || (c == '\''))
                {
                    quote = c;
                    continue;
                }
                if ((c == '\\') && (i + 1 < line.Length))
                {
                    sb.Append(line[++i]);
                    continue;
                }
                sb.Append(c);
            }
            if (inWord)
                res.Add(sb.ToString());
            return res;
        }
    }
}
