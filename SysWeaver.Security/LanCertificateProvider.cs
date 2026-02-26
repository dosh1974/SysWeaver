
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
    public sealed class LanCertificateProvider : ICertificateProvider, IDisposable
    {
        public override string ToString() => Filename;


        static String GetFirstLine(String filename)
        {
            foreach (var l in File.ReadAllLines(filename))
            {
                var t = l.Trim();
                var i = t.IndexOf('#');
                if (i >= 0)
                    t = t.Substring(0, i).TrimEnd();
                if (t.Length > 0)
                    return t;
            }
            return null;
        }

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
            String domainName = EnvInfo.ResolveText(p.DomainName);
            bool fail = false;
            try
            {
                var tname = PathTemplate.Resolve(p.DomainName);
                if (tname.IndexOfAny(":/\\".ToCharArray()) > 0)
                {
                    fail = true;
                    tname = EnvInfo.MakeAbsoulte(tname);
                    if (File.Exists(tname))
                    {
                        var d = GetFirstLine(tname);
                        if (d == null)
                            throw new Exception("Couldn't read a domain name from \"" + tname + "\"");
                        domainName = d;
                    }else
                    {
                        throw new Exception("Domain name file \"" + tname + "\" does not exist!");
                    }
                }
            }
            catch
            {
                if (fail)
                    throw;
            }
            StringValidate.DnsName(domainName);
            DomainName = domainName;
            Msg = msg;
            var serverFilename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.ServerConfigFile));
            String server = GetFirstLine(serverFilename);
            server = server.TrimEnd('/') + '/';
            LanMan = new RemoteConnection
            {
                BaseUrl = server,
                User = p.ServerCreds.User,
                Password = p.ServerCreds.Password,
                CredFile = p.ServerCreds.CredFile,
                AuthMethod = RemoteAuthMethod.SysWeaverLogin,
            }.Create<ILanCertificateManager>();
            P = new SignedCertificateCreator(p);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
        }

        readonly IMessageHost Msg;

        readonly ILanCertificateManager LanMan;
        readonly String DomainName;
        long Cc;

        readonly string Filename;
        readonly string Password;


        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        readonly SignedCertificateCreator P;

        X509Certificate2 C;

        readonly AsyncLock Lock = new AsyncLock();

        IDisposable ExpireAction;

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
            var f = Filename;
            var pw = Password;
            try
            {
                var res = await LanMan.GetCert(new GetLanCertRequest
                {
                    DomainName = DomainName,
                    Password = pw,
                    Cc = Cc,
                }).ConfigureAwait(false);
                if (res != null)
                {
                    Cc = res.Cc;
                    var cert = res.CertPfx;
                    if (cert != null)
                    {
                        c = await CertificateTools.Create(cert, pw, false).ConfigureAwait(false);
                        Interlocked.Exchange(ref C, c)?.Dispose();
                        CertificateTools.IsSoonExpired(c, out var expires, MinValidHours);
                        ExpireAction = Scheduler.Add(expires.AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, true);
                        return c;
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.AddMessage("Failed to get Lan cert, reverting to self signed cert", ex, MessageLevels.Warning);
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
                        {
                            Interlocked.Exchange(ref C, c)?.Dispose();
                            ExpireAction = Scheduler.Add(DateTime.UtcNow.AddMinutes(5), InvokeExpireSoon, true);
                            return c;
                        }
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
            Interlocked.Exchange(ref C, c)?.Dispose();
            ExpireAction = Scheduler.Add(DateTime.UtcNow.AddMinutes(5), InvokeExpireSoon, true);
            return c;
        }

        void InvokeExpireSoon()
        {
            Interlocked.Exchange(ref C, null)?.Dispose();
            OnChanged?.Invoke(null);
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
            LanMan.Dispose();
        }

    }


}
