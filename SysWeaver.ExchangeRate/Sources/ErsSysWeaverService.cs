using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.ExchangeRate.Sources
{
    public interface IRemote : IDisposable
    {
        Task<Rates> SyncRates(DateTime newerThan);
    }

    public sealed class ErsSysWeaverService : IRateSource, IDisposable
    {

        public ErsSysWeaverService(ErsSysWeaverServiceParams p)
        {
            Source = p.BaseUrl;
            Remote = p.Create<IRemote>();
            RefreshMinutes = Math.Max(p.RefreshMinutes, 1);
        }

        public override string ToString() => Source;

        public void Dispose()
        {
            Remote.Dispose();
        }

        readonly IRemote Remote;

    
        public string Source { get; init; }

        public int RefreshMinutes { get; init; }

        volatile Rates Current;

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
