
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using System.Threading;
using SysWeaver.Remote;

namespace SysWeaver.Security
{

    /// <summary>
    /// Managed Lan certificates that is issued by the Lan Certificate Manager service
    /// </summary>
    public sealed class LanCertificateProvider : ICertificateProvider, IDisposable, IPerfMonitored
    {
        public override string ToString() => String.Concat(DomainName, " from ", Server, " cached in \"", Filename, '"');


        /// <summary>
        /// Creates a self sigend certificate
        /// </summary>
        /// <param name="msg">Optional message host</param>
        /// <param name="p">Paramaters</param>
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

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(LanCertificateProvider));

        async ValueTask<ValueTuple<X509Certificate2, int>> InternalGetCert(IMessageHost msg = null)
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
            ExpireAction = Scheduler.AddValueTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon);
            Interlocked.Exchange(ref C, c)?.Dispose();
            return c;
        }

        async ValueTask InvokeExpireSoon()
        {
            using var _ = await Lock.Lock().ConfigureAwait(false);
            var nn = await InternalGetCert().ConfigureAwait(false);
            var newCert = nn.Item1;
            var oldCert = C;
            //  Same cert (or self signed being 
            bool isWorse = CertificateTools.IsSelfSigned(newCert) && (!CertificateTools.IsSelfSigned(oldCert));
            if (oldCert.Thumbprint.FastEquals(newCert.Thumbprint) || isWorse)
            {
                ExpireAction = Scheduler.AddValueTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon);
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
            ExpireAction = Scheduler.AddValueTask(DateTime.UtcNow.AddMinutes(nn.Item2), InvokeExpireSoon);
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
        /// An event that is fired if the certificate is about to expire.
        /// An application should restart (or re-init) to get the updated cert (calling GetCert again will return an updated cert).
        /// </summary>
        public event Action<X509Certificate2> OnChanged;

        public void Dispose()
        {
            using var _ = Lock.LockSync();
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
        }

    }


}
