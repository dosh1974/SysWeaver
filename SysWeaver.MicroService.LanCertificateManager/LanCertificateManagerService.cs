using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Manages ACME certificates for a set of domains and hands them out to other (LAN) services
    /// </summary>
    [WebApiUrl("Lcm")]
    public sealed partial class LanCertificateManagerService : Security.ILanCertificateManager, IPerfMonitored
    {

        /// <summary>
        /// Create the service, starts fetching the certificates of all domains in the background and retries failed ones every minute
        /// </summary>
        /// <param name="m">The service manager</param>
        /// <param name="p">Parameters, <see cref="LanCertificateManagerParams.Domains"/> must be set</param>
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
                //  No certificate yet, or the renewal is due (the provider dropped its certificate shortly before it expires)
                var cert = d.Data;
                if ((cert != null) && !d.RenewalDue)
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
        /// <param name="r">Table request parameters</param>
        /// <returns>A table with the state of all managed domains</returns>
        [WebApi]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Domains", "Domains", null, "icons/network.svg")]
        public TableData DomainTable(TableDataRequest r)
            => TableDataTools.Get(r, 1000, Domains.Values.Select(x => new Data(x)));

        /// <summary>
        /// Manually check (and if needed update) the certificate of a domain
        /// </summary>
        /// <param name="domainName">The domain name (case insensitive)</param>
        /// <returns>Always true</returns>
        /// <exception cref="Exception">If the domain isn't managed by this service</exception>
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
        /// <returns>null if the supplied cc matches the internal change counter, else the cert data (the pfx is null if no certificate is available yet)</returns>
        /// <exception cref="Exception">If the domain isn't managed by this service</exception>
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


        /// <summary>
        /// Stops the retry task and disposes all domains
        /// </summary>
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
            Manager.AddMessage(MessageLevels.Info, $"Certificate for {d.DomainName} was updated!");
        }

        void OnRenewalDue(Domain d)
        {
            Manager.AddMessage(MessageLevels.Info, $"Certificate for {d.DomainName} expires soon, ordering a new one");
        }

        async Task InitDomains()
        {
            foreach (var d in Domains.Values)
                await d.UpdateCert().ConfigureAwait(false);
        }

        readonly ExceptionTracker CertErrors = new();

        readonly ConcurrentDictionary<String, Domain> Domains;

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; } = new(nameof(LanCertificateManagerService));
    
    
    }

}
