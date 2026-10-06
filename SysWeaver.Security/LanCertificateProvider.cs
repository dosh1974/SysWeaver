
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using System.Threading;
using SysWeaver.Remote;

namespace SysWeaver.Security
{

    /// <summary>
    /// Provides a LAN certificate issued by a central Lan Certificate Manager service (see <see cref="ILanCertificateManager"/>).
    /// If the manager can't be reached (and no certificate has been received yet), a self-signed certificate is used instead (cached in the configured file).
    /// </summary>
    /// <remarks>
    /// Configuration is typically read from files in the key folder: the server base url (<see cref="LanCertificateProviderParams.ServerConfigFile"/>),
    /// the credentials (<see cref="LanCertificateProviderParams.ServerCreds"/>) and the domain name (<see cref="LanCertificateProviderParams.DomainName"/>).
    /// The manager is polled every 60 minutes while a server certificate is used, and every 15 minutes while a self-signed fallback is used.
    /// A server certificate is never replaced by a self-signed one during renewal.
    /// When the certificate changes, <see cref="OnChanged"/> is raised with the new certificate and the old certificate is disposed.
    /// Thread safe.
    /// </remarks>
    public sealed class LanCertificateProvider : ICertificateProvider, IDisposable, IPerfMonitored
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(DomainName, " from ", Server, " cached in \"", Filename, '"');


        /// <summary>
        /// Create a LAN certificate provider.
        /// Reads the domain name (if it's a file) and the server base url from disc.
        /// </summary>
        /// <param name="msg">Optional message host, used to report failures to get a certificate from the manager.</param>
        /// <param name="p">Parameters, null uses the defaults.</param>
        /// <exception cref="Exception">The domain name file couldn't be read, the domain name isn't a valid DNS name or a subject field is invalid.</exception>
        public LanCertificateProvider(IMessageHost msg = null, LanCertificateProviderParams p = null)
        {
            p = p ?? new LanCertificateProviderParams();
            Filename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename));
            Password = EnvInfo.ResolveText(p.Password);

            String domainName = EnvInfo.ResolveText(p.DomainName ?? "$(KeyFolder)/LanCertificateProvider_DomainName.txt");
            var tname = PathTemplate.Resolve(domainName);
            if (PathExt.IsValidPathToFile(tname, true))
            {
                var d = FileExt.ReadNonCommentString(tname);
                if (d == null)
                    throw new Exception("Couldn't read a domain name from \"" + tname + "\"");
                domainName = d;
            }
            StringValidate.DnsName(domainName);
            DomainName = domainName;

            ServerCreds = p.ServerCreds ?? new CredentialParams
            {
                CredFile = "$(KeyFolder)/LanCertificateProvider_SwLanCertManager.txt",
            };
            var serverFilename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.ServerConfigFile ?? "$(KeyFolder)/LanCertificateProvider_Server.txt"));
            String server = FileExt.ReadNonCommentString(serverFilename);
            Server = server.TrimEnd('/') + '/';
            Msg = msg;
            P = new SignedCertificateCreator(p);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
        }

        readonly String DomainName;
        readonly CredentialParams ServerCreds;
        readonly String Server;


        readonly IMessageHost Msg;

        long Cc;

        readonly string Filename;
        readonly string Password;


        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        readonly SignedCertificateCreator P;

        X509Certificate2 C;

        readonly AsyncLock Lock = new AsyncLock();

        IDisposable ExpireAction;


        Byte[] LastGoodCert;

        const int CheckServerCertEveryMinutes = 60;
        const int CheckSelfSignedCertEveryMinutes = 15;

        /// <inheritdoc/>
        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(LanCertificateProvider));

        async Task<ValueTuple<X509Certificate2, int>> InternalGetCert(IMessageHost msg = null)
        {
            using var _ = PerfMon.Track(nameof(InternalGetCert));
            X509Certificate2 c;
            var f = Filename;
            var pw = Password;
            try
            {
                var cr = ServerCreds;
                GetLanCertResponse res;
                using (var __ = PerfMon.Track(nameof(InternalGetCert) + ".Request"))
                {
                    using var remoteManager = new RemoteConnection
                    {
                        BaseUrl = Server,
                        User = cr.User,
                        Password = cr.Password,
                        CredFile = cr.CredFile,
                        AuthMethod = RemoteAuthMethod.SysWeaverLogin,
                    }.Create<ILanCertificateManager>();
                    res = await remoteManager.GetCert(new GetLanCertRequest
                    {
                        DomainName = DomainName,
                        Password = pw,
                        Cc = Cc,
                    }).ConfigureAwait(false);
                }
                if (res != null)
                {
                    Cc = res.Cc;
                    LastGoodCert = res.CertPfx;
                }
                var cert = LastGoodCert;
                if (cert != null)
                {
                    c = await CertificateTools.Create(cert, pw, false).ConfigureAwait(false);
                    return ValueTuple.Create(c, CheckServerCertEveryMinutes);
                }
            }
            catch (Exception ex)
            {
                msg?.AddMessage("Failed to get Lan cert, reverting to self signed cert", ex, MessageLevels.Warning);
            }
            var p = P;
            var haveFile = !String.IsNullOrEmpty(f);
            //  Try to load from file
            if (haveFile && File.Exists(f))
            {
                try
                {
                    c = await CertificateTools.Load(f, pw, false).ConfigureAwait(false);
                    if (!CertificateTools.IsSoonExpired(c, out var expires, MinValidHours))
                    {
                        if (p.IsSame(c))
                            return ValueTuple.Create(c, CheckSelfSignedCertEveryMinutes);
                    }
                    c.Dispose();
                }
                catch
                {
                }
            }
            //  Must create a new
            c = p.CreateSelfSigned();
            if (haveFile)
            {
                var dir = Path.GetDirectoryName(f);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(f, c.Export(X509ContentType.Pfx, pw)).ConfigureAwait(false);
                //await File.WriteAllBytesAsync(Path.ChangeExtension(f, "crt"), c.Export(X509ContentType.Cert, (String)null)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.ChangeExtension(f, "crt"), c.ExportCertificatePem()).ConfigureAwait(false);
            }
            return ValueTuple.Create(c, CheckSelfSignedCertEveryMinutes);
        }

        /// <summary>
        /// Get the current certificate.
        /// The first call contacts the manager (falling back to a self-signed certificate) and schedules periodic renewal checks.
        /// </summary>
        /// <returns>The certificate (owned by the provider, do not dispose it).</returns>
        public async Task<X509Certificate2> GetCert()
        {
            var c = C;
            if (c != null)
                return c;
            using var _ = await Lock.Lock().ConfigureAwait(false);
            c = C;
            if (c != null)
                return c;
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            var nn = await InternalGetCert(Msg).ConfigureAwait(false);
            c = nn.Item1;
            ExpireAction = Scheduler.AddTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon, "Renew LAN cert");
            Interlocked.Exchange(ref C, c)?.Dispose();
            return c;
        }

        async Task InvokeExpireSoon()
        {
            using var _ = await Lock.Lock().ConfigureAwait(false);
            var nn = await InternalGetCert().ConfigureAwait(false);
            var newCert = nn.Item1;
            var oldCert = C;
            //  Same cert (or self signed being 
            bool isWorse = CertificateTools.IsSelfSigned(newCert) && (!CertificateTools.IsSelfSigned(oldCert));
            if (oldCert.Thumbprint.FastEquals(newCert.Thumbprint) || isWorse)
            {
                ExpireAction = Scheduler.AddTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon, "Renew LAN cert");
                try
                {
                    newCert.Dispose();
                }
                catch (Exception ex)
                {
                    Msg?.AddMessage("Failed to Dispose new certificate " + newCert.Thumbprint, ex, MessageLevels.Warning);
                }
                return;
            }
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            oldCert = Interlocked.Exchange(ref C, newCert);
            OnChanged?.Invoke(newCert);
            ExpireAction = Scheduler.AddTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon, "Renew LAN cert");
            try
            {
                oldCert?.Dispose();
            }
            catch (Exception ex)
            {
                Msg?.AddMessage("Failed to Dispose old certificate " + oldCert.Thumbprint, ex, MessageLevels.Warning);
            }
        }

        /// <summary>
        /// An event that is fired when a renewal check found a different (and not worse) certificate.
        /// The argument is the new certificate (the same instance that <see cref="GetCert"/> will return), the previous certificate is disposed after the event handlers returns.
        /// </summary>
        public event Action<X509Certificate2> OnChanged;

        /// <summary>
        /// Cancel renewal checks and dispose the current certificate.
        /// </summary>
        public void Dispose()
        {
            using var _ = Lock.LockSync();
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
        }

    }


}
