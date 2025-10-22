using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace SysWeaver.Remote.Connection
{
    sealed class HttpClientTimeoutHandler : DelegatingHandler
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

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
