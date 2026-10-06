using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace SysWeaver.Remote.Connection
{
    /// <summary>
    /// Delegating handler that applies a per request timeout (set using the internal SetTimeout request extension), or <see cref="DefaultTimeout"/>.
    /// A timeout is reported as a <see cref="TimeoutException"/> instead of a <see cref="TaskCanceledException"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="HttpClient.Timeout"/> of the owning client still applies, so a per request timeout longer than that has no effect.
    /// </remarks>
    public sealed class HttpClientTimeoutHandler : DelegatingHandler
    {
        /// <summary>
        /// The timeout used for requests that don't specify one, <see cref="Timeout.InfiniteTimeSpan"/> for no timeout. Defaults to 100 seconds.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Sends the request, cancelling it if the timeout elapses.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>The response.</returns>
        /// <exception cref="TimeoutException">The timeout elapsed before a response was received.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var cts = GetCancellationTokenSource(request, cancellationToken);
            try
            {
                return await base.SendAsync(request, cts?.Token ?? cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }

        CancellationTokenSource GetCancellationTokenSource(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var timeout = request.GetTimeout() ?? DefaultTimeout;
            // No need to create a CTS if there's no timeout
            if (timeout == Timeout.InfiniteTimeSpan)
                return null;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return cts;
        }
    }

}
