using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Helpers for running an external process, waiting for it to exit and capturing its output.
    /// </summary>
    public sealed class ExternalProcess
    {

        /// <summary>
        /// Run an external command and block until it exits (no timeout).
        /// </summary>
        /// <param name="cmd">The command (executable) to run</param>
        /// <param name="args">Optional command arguments</param>
        /// <param name="onMessage">Optional output callback, second parameter is false for stdout and true for stderr.
        /// Note: currently it's called at most once, with the entire (trimmed) stdout after the process has closed it. Stderr is read but never reported.</param>
        /// <param name="onExit">Optionally called when the process completed or on error, parameters are: exitCode (-1 on error), exception (null on success), duration and the captured output
        /// (currently the trimmed stdout, added twice, and no stderr)</param>
        /// <param name="workingFolder">The folder to use as the current, null to use the current folder of this process</param>
        /// <param name="useShell">Use shell execute. Note: since output is always redirected, true causes the start to fail with an <see cref="InvalidOperationException"/></param>
        /// <returns>The process exit code</returns>
        /// <exception cref="Exception">Any exception thrown while starting or running the process is re-thrown (after <paramref name="onExit"/> has been invoked),
        /// ex: <see cref="System.ComponentModel.Win32Exception"/> if the executable can't be found.</exception>
        public static int Run(String cmd, String args = null, Action<String, bool> onMessage = null, Action<int, Exception, TimeSpan, IEnumerable<String>> onExit = null, String workingFolder = null, bool useShell = false)
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
                    si.WorkingDirectory = workingFolder;
                    si.UseShellExecute = useShell;
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


        /// <summary>
        /// Run an external command and asynchronously wait for it to exit.
        /// </summary>
        /// <param name="cmd">The command (executable or document when using the shell) to run</param>
        /// <param name="args">Optional command arguments</param>
        /// <param name="onMessage">Optional output callback, second parameter is false for stdout and true for stderr.
        /// Note: currently it's called at most once, with the entire (trimmed) stdout after the process has closed it. Stderr is read but never reported. Never called when <paramref name="useShell"/> is true.</param>
        /// <param name="onExit">Optionally called when the process completed or on error, parameters are: exitCode (-1 on error), exception (null on success), duration and the captured output
        /// (currently the trimmed stdout, added twice, and no stderr)</param>
        /// <param name="cancelWait">An optional cancellation token, cancels the wait for the process to exit (the process is NOT killed).
        /// Note: when output is redirected, stdout is read synchronously to the end before the token is observed.</param>
        /// <param name="workingFolder">The folder to use as the current, null to use the current folder of this process</param>
        /// <param name="useShell">Use shell execute (output is then not captured)</param>
        /// <returns>The process exit code</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancelWait"/> was cancelled while waiting for the process to exit.</exception>
        /// <exception cref="Exception">Any exception thrown while starting or running the process is re-thrown (after <paramref name="onExit"/> has been invoked).</exception>
        /// <remarks>Blocks the calling thread while reading stdout (the read is synchronous).</remarks>
        public static async Task<int> RunAsync(String cmd, String args = null, Action<String, bool> onMessage = null, Action<int, Exception, TimeSpan, IEnumerable<String>> onExit = null, CancellationToken? cancelWait = null, String workingFolder = null, bool useShell = false)
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
                    si.WorkingDirectory = workingFolder;
                    si.UseShellExecute = useShell;
                    if (!useShell)
                    {
                        si.RedirectStandardOutput = true;
                        si.RedirectStandardError = true;
                    }
                    si.FileName = cmd;
                    if (!String.IsNullOrEmpty(args))
                        si.Arguments = args;
                    p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => err.Append(Environment.NewLine).Append(e.Data));
                    p.Start();
                    String o = "";
                    if (!useShell)
                    {
                        p.BeginErrorReadLine();
                        o = p.StandardOutput.ReadToEnd().Trim();
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
                    }
                    if (cancelWait != null)
                        await p.WaitForExitAsync(cancelWait ?? throw new Exception()).ConfigureAwait(false);
                    else
                        await p.WaitForExitAsync().ConfigureAwait(false);
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
