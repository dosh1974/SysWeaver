using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Sources
{

    /// <summary>
    /// An ip location source that asks another SysWeaver ip location service
    /// </summary>
    public sealed class IlsSysWeaverService : IIpLocationSource, IDisposable
    {

        /// <summary>
        /// Create the source
        /// </summary>
        /// <param name="p">The connection to the other service, null to use the defaults</param>
        public IlsSysWeaverService(IlsSysWeaverServiceParams p)
        {
            p = p ?? new IlsSysWeaverServiceParams();
            Source = p.BaseUrl;
            Remote = p.Create<IRemote>();
            var s = Source;
            var i = s.FastIndexOf("://");
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

        /// <inheritdoc />
        public override string ToString() => Source;

        /// <inheritdoc />
        public void Dispose()
        {
            Remote.Dispose();
        }

        /// <inheritdoc />
        public async Task<Net.Location> LookUp(string ip)
        {
            var r = await Remote.GetLocation(ip).ConfigureAwait(false);
            if (r == null)
                return r;
            r.Source = String.Concat(Server, " => ", r.Source);
            return r;
        }

        readonly IRemote Remote;

        /// <summary>
        /// The base url of the other service
        /// </summary>
        public string Source { get; init; }

        /// <inheritdoc />
        public string Name => Source;
    }

}
