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
        /// Called once with the entire (trimmed) stdout after the process has closed it (if non-empty), and once per non-empty stderr line (from a background thread, exceptions are ignored).
        /// Calls are never concurrent. Never called when <paramref name="useShell"/> is true.</param>
        /// <param name="onExit">Optionally called when the process completed or on error, parameters are: exitCode (-1 on error), exception (null on success), duration and the captured output
        /// (the last 64 outputs, i.e the trimmed stdout and the stderr lines, in the order they were received)</param>
        /// <param name="workingFolder">The folder to use as the current, null to use the current folder of this process</param>
        /// <param name="useShell">Use shell execute (output is then not captured)</param>
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
                    p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => OnOutput(log, e.Data, true, onMessage));
                    p.Start();
                    if (!useShell)
                    {
                        p.BeginErrorReadLine();
                        OnOutput(log, p.StandardOutput.ReadToEnd().Trim(), false, onMessage);
                    }
                    p.WaitForExit();
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
        /// Called once with the entire (trimmed) stdout after the process has closed it (if non-empty), and once per non-empty stderr line (from a background thread, exceptions are ignored).
        /// Calls are never concurrent. Never called when <paramref name="useShell"/> is true.</param>
        /// <param name="onExit">Optionally called when the process completed or on error, parameters are: exitCode (-1 on error), exception (null on success), duration and the captured output
        /// (the last 64 outputs, i.e the trimmed stdout and the stderr lines, in the order they were received)</param>
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
                    p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => OnOutput(log, e.Data, true, onMessage));
                    p.Start();
                    if (!useShell)
                    {
                        p.BeginErrorReadLine();
                        OnOutput(log, p.StandardOutput.ReadToEnd().Trim(), false, onMessage);
                    }
                    if (cancelWait != null)
                        await p.WaitForExitAsync(cancelWait ?? throw new Exception()).ConfigureAwait(false);
                    else
                        await p.WaitForExitAsync().ConfigureAwait(false);
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
        /// Report some output (ignored if empty), keeps the last 64 outputs in the log.
        /// Synchronized on the log, so the callback is never invoked concurrently.
        /// </summary>
        static void OnOutput(LinkedList<String> log, String text, bool isError, Action<String, bool> onMessage)
        {
            if (String.IsNullOrEmpty(text))
                return;
            lock (log)
            {
                log.AddLast(text);
                while (log.Count > 64)
                    log.RemoveFirst();
                if (onMessage == null)
                    return;
                if (!isError)
                {
                    onMessage(text, false);
                    return;
                }
                //  Stderr is reported on a background thread, an exception there would terminate the process
                try
                {
                    onMessage(text, true);
                }
                catch
                {
                }
            }
        }


    }


}
