using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// The result of <see cref="ExternalProcess.RunWithTimeoutAsync"/>.
    /// </summary>
    public sealed class ExternalProcessResult
    {
        public override string ToString() => Exception != null ? ("Failed: " + Exception.Message) : (TimedOut ? "Timed out" : ("Exit code " + ExitCode));

        /// <summary>
        /// The exit code of the process, -1 if it couldn't be started or timed out.
        /// </summary>
        public int ExitCode;

        /// <summary>
        /// True if the process didn't exit in time and was killed (with all it's child processes).
        /// </summary>
        public bool TimedOut;

        /// <summary>
        /// The exception if the process couldn't be started (ex: the executable doesn't exist), else null.
        /// </summary>
        public Exception Exception;

        /// <summary>
        /// The output (stdout and stderr lines in the order they were received, trimmed).
        /// </summary>
        public String Output;

        /// <summary>
        /// The time it took.
        /// </summary>
        public TimeSpan Duration;

        /// <summary>
        /// True if the process ran and exited by itself (it may still have failed, see <see cref="ExitCode"/>).
        /// </summary>
        public bool Exited => (Exception == null) && !TimedOut;
    }

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
        /// Run an external command with a time limit, the process (and all it's child processes) is killed if it doesn't exit in time.
        /// Never throws (a failure to start is returned in <see cref="ExternalProcessResult.Exception"/>).
        /// </summary>
        /// <param name="cmd">The executable to run</param>
        /// <param name="args">The arguments (each one is quoted as needed), may be null</param>
        /// <param name="timeout">The maximum time to wait for the process to exit</param>
        /// <param name="workingFolder">The folder to use as the current, null to use the current folder of this process</param>
        /// <param name="onMessage">Optional output callback, called once per non-empty line, second parameter is false for stdout and true for stderr (from a background thread, never concurrent, exceptions are ignored)</param>
        /// <returns>The result</returns>
        /// <remarks>
        /// When the process has exited, the output is read for at most 2 more seconds: a child process that inherited the output (ex: a daemon started by the process) keeps it open.
        /// </remarks>
        public static async Task<ExternalProcessResult> RunWithTimeoutAsync(String cmd, IEnumerable<String> args, TimeSpan timeout, String workingFolder = null, Action<String, bool> onMessage = null)
        {
            var start = DateTime.UtcNow;
            var output = new StringBuilder();
            var res = new ExternalProcessResult
            {
                ExitCode = -1,
            };
            var outDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var errDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLine(String line, bool isError, TaskCompletionSource done)
            {
                if (line == null)
                {
                    done.TrySetResult();
                    return;
                }
                lock (output)
                {
                    output.AppendLine(line);
                    if (onMessage == null || line.Length <= 0)
                        return;
                    try
                    {
                        onMessage(line, isError);
                    }
                    catch
                    {
                    }
                }
            }
            try
            {
                using var p = new Process();
                var si = p.StartInfo;
                si.FileName = cmd;
                if (args != null)
                    foreach (var a in args)
                        si.ArgumentList.Add(a);
                si.WorkingDirectory = workingFolder;
                si.UseShellExecute = false;
                si.CreateNoWindow = true;
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
                si.RedirectStandardInput = true;
                p.EnableRaisingEvents = true;
                p.OutputDataReceived += (s, e) => OnLine(e.Data, false, outDone);
                p.ErrorDataReceived += (s, e) => OnLine(e.Data, true, errDone);
                p.Exited += (s, e) => exited.TrySetResult();
                p.Start();
                try
                {
                    //  No input (a prompt would wait forever)
                    p.StandardInput.Close();
                }
                catch
                {
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                var ms = (long)timeout.TotalMilliseconds;
                var wait = ms <= 0 ? Task.CompletedTask : Task.Delay(ms > int.MaxValue ? Timeout.InfiniteTimeSpan : timeout);
                if (p.HasExited)
                    exited.TrySetResult();
                if (await Task.WhenAny(exited.Task, wait).ConfigureAwait(false) != exited.Task)
                {
                    res.TimedOut = true;
                    try
                    {
                        p.Kill(true);
                    }
                    catch
                    {
                    }
                    await Task.WhenAny(exited.Task, Task.Delay(5000)).ConfigureAwait(false);
                }
                else
                {
                    await Task.WhenAny(Task.WhenAll(outDone.Task, errDone.Task), Task.Delay(2000)).ConfigureAwait(false);
                    res.ExitCode = p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                res.Exception = ex;
            }
            lock (output)
                res.Output = output.ToString().Trim();
            res.Duration = DateTime.UtcNow - start;
            return res;
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
