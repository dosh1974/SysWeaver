using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.MicroService
{


    [WebApiUrl("Lcm")]
    public sealed partial class LanCertificateManagerService : Security.ILanCertificateManager, IPerfMonitored
    {

        public LanCertificateManagerService(ServiceManager m, LanCertificateManagerParams p)
        {
            Manager = m;
            var ds = new ConcurrentDictionary<String, Domain>(StringComparer.Ordinal);
            Domains = ds;
            foreach (var x in p.Domains)
            {
                var d = new Domain(x, p, m, this);
                ds.TryAdd(x.FastToLower(), d);
            }
            InitDomains().RunAsync();
            RetryTask = new PeriodicTask(Retry, 60000, true, true, true);
        }


        readonly PeriodicTask RetryTask;


        async Task<bool> Retry()
        {
            foreach (var d in Domains.Values)
            {
                var cert = d.Data;
                if (cert != null)
                    continue;
                try
                {
                    using var _ = PerfMon.Track(nameof(Domain.UpdateCert) + "." + d.DomainName);
                    await d.UpdateCert().ConfigureAwait(false);
                }
                catch
                {
                }
            }
            return true;
        }

        /// <summary>
        /// Get the domains
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Domains", "Domains", null, "icons/network.svg")]
        public TableData DomainTable(TableDataRequest r)
            => TableDataTools.Get(r, 1000, Domains.Values.Select(x => new Data(x)));

        [WebApi]
        [WebApiAuth(Roles.OpsDev)]
        public async Task<bool> UpdateCert(String domainName)
        {
            if (!Domains.TryGetValue(domainName.FastToLower(), out var d))
                throw new Exception("Unknown domain!");
            using var _ = PerfMon.Track(nameof(Domain.UpdateCert) + "." + d.DomainName);
            await d.UpdateCert().ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Get the certificate for a domain
        /// </summary>
        /// <param name="r">Parameters for the cert</param>
        /// <returns>null if the supplied cc matches the internal change counter, else the cert data</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth(Roles.OpsDev + ",Service")]
        public async Task<GetLanCertResponse> GetCert(GetLanCertRequest r)
        {
            if (!Domains.TryGetValue(r.DomainName.FastToLower(), out var d))
                throw new Exception("Unknown domain!");
            var data = d.Data;
            var cc = data?.Cc ?? -1;
            if (r.Cc == cc)
                return null;
            var c = data?.Cert;
            if (c == null)
            {
                return new GetLanCertResponse
                {
                    Cc = cc,
                };
            }
            var certData = c.Export(X509ContentType.Pfx, r.Password);
            return new GetLanCertResponse
            {
                Cc = cc,
                CertPfx = certData,
            };
        }


        public void Dispose()
        {
            RetryTask.Dispose();
            var ds = Domains;
            foreach (var x in ds.Values)
                x.Dispose();
            ds.Clear();
        }

        readonly ServiceManager Manager;

        void OnChange(Domain d)
        {
            Manager.AddMessage("Certificate for " + d.DomainName + " was updated!");
        }

        async Task InitDomains()
        {
            foreach (var d in Domains.Values)
                await d.UpdateCert().ConfigureAwait(false);
        }

        readonly ExceptionTracker CertErrors = new();

        readonly ConcurrentDictionary<String, Domain> Domains;

        public PerfMonitor PerfMon { get; } = new(nameof(LanCertificateManagerService));
    
    
    }

}
