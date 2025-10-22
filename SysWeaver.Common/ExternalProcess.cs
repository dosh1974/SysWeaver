using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SysWeaver
{
    public sealed class ExternalProcess
    {

        /// <summary>
        /// Run an external command
        /// </summary>
        /// <param name="cmd">The command to run</param>
        /// <param name="args">Optional command arguments</param>
        /// <param name="onMessage">Optionally called on every line outputted, second parameter is false for stdout and true for stderr</param>
        /// <param name="onExit">Optionally called when the process completed or on error, paramaters are: exitCode, exception, duration and the last X stdout/stderr lines of output</param>
        /// <returns>The process exit code</returns>
        public static int Run(String cmd, String args = null, Action<String, bool> onMessage = null, Action<int, Exception, TimeSpan, IEnumerable<String>> onExit = null)
        {
            var start = DateTime.UtcNow;
            LinkedList<String> log = new LinkedList<string>();
            try
            {
                using (var p = new Process())
                {
                    int logLen = 0;
                    StringBuilder err = new StringBuilder();
                    var si = p.StartInfo;
                    si.UseShellExecute = false;
                    si.RedirectStandardOutput = true;
                    si.RedirectStandardError = true;
                    si.FileName = cmd;
                    if (!String.IsNullOrEmpty(args))
                        si.Arguments = args;
                    p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => err.Append(Environment.NewLine).Append(e.Data));
                    p.Start();
                    p.BeginErrorReadLine();
                    var o = p.StandardOutput.ReadToEnd().Trim();
                    if (o.Length > 0)
                    {
                        onMessage?.Invoke(o, false);
                        lock (log)
                        {
                            log.AddLast(o);
                            ++logLen;
                            while (logLen > 64)
                            {
                                --logLen;
                                log.RemoveFirst();
                            }
                        }
                    }
                    p.WaitForExit();
                    if (o.Length > 0)
                    {
                        //onMessage?.Invoke(o, true);
                        lock (log)
                        {
                            log.AddLast(o);
                            ++logLen;
                            while (logLen > 64)
                            {
                                --logLen;
                                log.RemoveFirst();
                            }
                        }
                    }
                    var ec = p.ExitCode;
                    onExit?.Invoke(ec, null, DateTime.UtcNow - start, log);
                    return ec;
                }
            }
            catch (Exception ex)
            {
                onExit?.Invoke(-1, ex, DateTime.UtcNow - start, log);
                throw;
            }
        }


    }


}
