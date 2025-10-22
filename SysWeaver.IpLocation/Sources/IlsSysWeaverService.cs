using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Sources
{

    public sealed class IlsSysWeaverService : IIpLocationSource, IDisposable
    {

        public IlsSysWeaverService(IlsSysWeaverServiceParams p)
        {
            p = p ?? new IlsSysWeaverServiceParams();
            Source = p.BaseUrl;
            Remote = p.Create<IRemote>();
            var s = Source;
            var i = s.IndexOf("://");
            if (i >= 0)
                s = s.Substring(i + 3);
            i = s.IndexOf('/');
            if (i >= 0)
                s = s.Substring(0, i);
            i = s.IndexOf(':');
            if (i >= 0)
                s = s.Substring(0, i);
            Server = s;
        }

        readonly String Server;

        public override string ToString() => Source;

        public void Dispose()
        {
            Remote.Dispose();
        }

        public async Task<IpLocation> LookUp(string ip)
        {
            var r = await Remote.GetLocation(ip).ConfigureAwait(false);
            if (r == null)
                return r;
            r.Source = String.Concat(Server, " => ", r.Source);
            return r;
        }

        readonly IRemote Remote;

        public string Source { get; init; }

        public string Name => Source;
    }

}
