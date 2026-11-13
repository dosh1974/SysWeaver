using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// Checks if a SysWeaver server answers: a GET of "serverTime" (the cheapest end point of every SysWeaver web server, the shell syncs its clock with it).
    /// Used to wait for a server that is starting and to keep a server active between a warm up and a test.
    /// </summary>
    public sealed class BenchServerProbe : IDisposable
    {
        /// <summary>
        /// The path of the probe, relative to the target.
        /// </summary>
        public const String Path = "serverTime";

        /// <summary>
        /// Create a probe.
        /// </summary>
        /// <param name="target">The root url of the server</param>
        /// <param name="ignoreCertificateErrors">Accept any https certificate</param>
        /// <param name="timeout">The time out of one request</param>
        public BenchServerProbe(Uri target, bool ignoreCertificateErrors, TimeSpan timeout)
        {
            Uri = new Uri(target, Path);
            var h = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                ConnectTimeout = timeout,
                AutomaticDecompression = DecompressionMethods.None,
            };
            if (ignoreCertificateErrors)
                h.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            Client = new HttpClient(h, true)
            {
                Timeout = timeout,
            };
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SysWeaver.HttpBench/1.0");
        }

        /// <summary>
        /// The url that is requested.
        /// </summary>
        public readonly Uri Uri;

        readonly HttpClient Client;

        public void Dispose() => Client.Dispose();

        /// <summary>
        /// Request the probe once.
        /// </summary>
        /// <param name="cancel">Cancels the request</param>
        /// <returns>Null and the time in ms if the server answered with a 2xx status, else the reason it didn't</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancel"/> was cancelled</exception>
        public async Task<(String Error, double Ms)> PingAsync(CancellationToken cancel = default)
        {
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                using var res = await Client.GetAsync(Uri, cancel).ConfigureAwait(false);
                await res.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
                var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                var status = (int)res.StatusCode;
                if ((status >= 200) && (status < 300))
                    return (null, ms);
                return (String.Concat("HTTP ", status, " ", res.ReasonPhrase), ms);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var ie = ex.InnerException;
                //  The message of a connection error often repeats the one of the inner exception
                var msg = (ie == null) || ex.Message.Contains(ie.Message, StringComparison.Ordinal) ? ex.Message : String.Concat(ex.Message, " (", ie.Message, ")");
                return (msg, Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
        }

        /// <summary>
        /// Request the probe until the server answers (one request per second at most).
        /// </summary>
        /// <param name="maxWait">The longest time to wait</param>
        /// <param name="cancel">Cancels the wait</param>
        /// <param name="onFail">Called after a failed request with the reason and the seconds waited so far, may be null</param>
        /// <returns>Ok: the server answered. Failures: the number of failed requests before that (0 = it answered at once). Error: the last reason, null if ok</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancel"/> was cancelled</exception>
        public async Task<(bool Ok, int Failures, String Error)> WaitAsync(TimeSpan maxWait, CancellationToken cancel = default, Action<String, double> onFail = null)
        {
            var t0 = Stopwatch.GetTimestamp();
            int failures = 0;
            for (; ; )
            {
                var r0 = Stopwatch.GetTimestamp();
                var (err, _) = await PingAsync(cancel).ConfigureAwait(false);
                if (err == null)
                    return (true, failures, null);
                ++failures;
                var waited = Stopwatch.GetElapsedTime(t0);
                onFail?.Invoke(err, waited.TotalSeconds);
                if (waited >= maxWait)
                    return (false, failures, err);
                //  At most one request per second (a refused connection fails at once)
                var left = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(r0);
                if (left > TimeSpan.Zero)
                    await Task.Delay(left, cancel).ConfigureAwait(false);
            }
        }
    }
}
