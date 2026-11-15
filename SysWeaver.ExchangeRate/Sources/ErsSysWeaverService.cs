using System;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.ExchangeRate.Sources
{
    /// <summary>
    /// The remote api of another exchange rate service
    /// </summary>
    public interface IRemote : IDisposable
    {
        /// <summary>
        /// Get the rates of the remote service if they are newer than a time
        /// </summary>
        /// <param name="newerThan">The time of the rates that the caller already has (UTC)</param>
        /// <returns>The rates, null if there are no newer rates</returns>
        [RemoteEndPoint("Api/ExchangeRates/" + nameof(SyncRates), HttpEndPointTypes.Post)]
        Task<Rates> SyncRates(DateTime newerThan);
    }

    /// <summary>
    /// A rate source that gets the rates from another SysWeaver exchange rate service
    /// </summary>
    public sealed class ErsSysWeaverService : IRateSource, IDisposable
    {

        /// <summary>
        /// Create the source
        /// </summary>
        /// <param name="p">The connection to the other service</param>
        public ErsSysWeaverService(ErsSysWeaverServiceParams p)
        {
            Source = p.BaseUrl;
            Remote = p.Create<IRemote>();
            RefreshMinutes = Math.Max(p.RefreshMinutes, 1);
        }

        /// <inheritdoc />
        public override string ToString() => Source;

        /// <inheritdoc />
        public void Dispose()
        {
            Remote.Dispose();
        }

        readonly IRemote Remote;

    
        /// <inheritdoc />
        public string Source { get; init; }

        /// <inheritdoc />
        public int RefreshMinutes { get; init; }

        volatile Rates Current;

        /// <inheritdoc />
        public async Task<Rates> GetRates()
        {
            var r = Current;
            DateTime last = r == null ? DateTime.MinValue : r.LastUpdated;
            var n = await Remote.SyncRates(last).ConfigureAwait(false);
            if (n == null)
                return r;
            n.Source = String.Join(" => ", Source, n.Source);
            Interlocked.Exchange(ref Current, n);
            return n;
        }

    }

}
