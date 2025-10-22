using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using SysWeaver.Net;

namespace SysWeaver.Security
{


    public sealed class AcmeCertificateProvider : ICertificateProvider, IHttpServerModule
    {

        const String Prefix = "[ACME] ";

        async Task<List<Byte[]>> GetImportCerts()
        {
            var msg = Msg;
            List<Byte[]> certs = new List<byte[]>();
            var importCerts = ImportCertFiles;
            if ((importCerts == null) || (importCerts.Length <= 0))
            {
                var bn = AuthApi.OriginalString;
                var niceName = PathExt.SafeFilename(bn);
                var type = typeof(AcmeCertificateProvider);
                using (var s = type.Assembly.GetManifestResourceStream(type.Namespace + ".Certs." + niceName + ".pem"))
                {
                    if (s != null)
                    {
                        var l = (int)s.Length;
                        var b = GC.AllocateUninitializedArray<Byte>(l);
                        await s.ReadAsync(b, 0, l).ConfigureAwait(false);
                        Byte[] certBytes;
                        try
                        {
                            certBytes = CertificateTools.GetCertBytes(b);
                            certs.Add(certBytes);
                        }
                        catch (Exception ex)
                        {
                            msg?.AddMessage(Prefix + "Can't import certificate for " + bn.ToQuoted(), ex, MessageLevels.Warning);
                        }
                    }
                }
            }
            else
            {
                foreach (var cn in importCerts)
                {
                    if (String.IsNullOrEmpty(cn))
                        continue;
                    var f = PathTemplate.Resolve(cn);
                    if (!File.Exists(f))
                    {
                        msg?.AddMessage(Prefix + "Can't import certificate " + f.ToQuoted() + " since it doesn't exist!", MessageLevels.Warning);
                        continue;
                    }
                    var pem = await File.ReadAllTextAsync(f).ConfigureAwait(false);
                    Byte[] certBytes;
                    try
                    {
                        certBytes = CertificateTools.GetCertBytes(pem);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Can't import certificate " + f.ToQuoted(), ex, MessageLevels.Warning);
                        continue;
                    }
                    certs.Add(certBytes);
                }
            }
            return certs;
        }

        public AcmeCertificateProvider(IMessageHost msg = null, AcmeCertificateParams p = null)
        {
            Msg = msg;
            p = p ?? new AcmeCertificateParams();
            var email = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.Email), nameof(p.Email));
            if (String.IsNullOrEmpty(email))
                throw new ArgumentException("Must provide a valid email to create an account!", nameof(p.Email));
            var domainName = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.DomainName), nameof(p.DomainName));
            if (String.IsNullOrEmpty(domainName))
                throw new ArgumentException("Must provide a valid domain name!", nameof(p.Email));
            Names = [domainName];
            Info = new CsrInfo
            {
                CountryName = SignedCertificateCreator.GetValidatedCountry(p.Country, nameof(p.Country)),
                State = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.State), nameof(p.State)),
                Locality = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.Locality), nameof(p.Locality)),
                Organization = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.Organization), nameof(p.Organization)),
                OrganizationUnit = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.Unit), nameof(p.Unit)),
                CommonName = domainName,
            };
            var authApi = p.AuthApi;
            var hash = MD5.HashData(Encoding.Unicode.GetBytes(String.Join('|', authApi, domainName, email))).ToHex();
            var extra = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                { "authapi", PathExt.SafeFilename(authApi ) },
                { "domainname", PathExt.SafeFilename(domainName) },
                { "email", PathExt.SafeFilename(email) },
                { "hash", hash },
            };
            var certFilename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename, extra));
            var cex = PathExt.EnsureCanWriteFile(certFilename);
            if (cex != null)
                msg?.AddMessage(Prefix + "Failed to create the folder required to store " + certFilename.ToFilename(), cex, MessageLevels.Warning);
            Filename = certFilename;
            Password = EnvInfo.ResolveText(p.Password);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
            ImportCertFiles = p.ImportCertFiles;

            //  Setup challenge folder
            var challengeFolder = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.ChallengeFolder, extra));
            if (String.IsNullOrEmpty(challengeFolder))
                challengeFolder = null;
            if (challengeFolder != null)
            {
                var ex = PathExt.EnsureFolderExist(challengeFolder);
                if (ex != null)
                    msg?.AddMessage(Prefix + "Failed to create the challenge folder " + challengeFolder.ToFolder(), ex, MessageLevels.Warning);
            }
            ChallengeFolder = challengeFolder;
            var accountFile = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.AccountFilename, extra));
            KeyFilename = accountFile;
            var aex = PathExt.EnsureCanWriteFile(accountFile);
            if (aex != null)
                msg?.AddMessage(Prefix + "Failed to create the folder required to store " + accountFile.ToFilename(), aex, MessageLevels.Warning);
            var authUrl = new Uri(authApi);
            AuthApi = authUrl;
            Mail = email;
            msg?.AddMessage(Prefix + "Using API at " + authUrl.ToString().ToQuoted());
        }

        readonly String[] ImportCertFiles;
        readonly String Mail;
        readonly Uri AuthApi;
        readonly String KeyFilename;
        readonly String[] Names;
        readonly CsrInfo Info;
        readonly String ChallengeFolder;
        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        public readonly string Filename;
        readonly string Password;
        readonly IMessageHost Msg;

        readonly ConcurrentDictionary<String, Byte[]> Tokens = new ConcurrentDictionary<string, Byte[]>(StringComparer.OrdinalIgnoreCase);

        IDisposable StartHttpServer()
        {
            var msg = Msg;
            msg?.AddMessage(Prefix + "Starting a http server at port 80", MessageLevels.Debug);
            NetHttpServer server;
            try
            {
                server = new NetHttpServer(Msg, null, null, null, null, FirewallHandler.Instance, new NetHttpServerParams
                {
                    ListenOn =
                    [
                        new HttpServerPrefix
                        {
                            Prefix = "http://*:80/",
                            AddToFirewall = true,
                            FirewallName = HttpServerPrefix.DefaultFirewallName + " ACME",
                        }
                    ],
                });
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to start a http server at port 80", ex, MessageLevels.Warning);
                msg?.AddMessage(Prefix + "Assuming that this process already have a http server at port 80 (or if some other process have it, this is a mis-configuration and challanges will fail)");
                return null;
            }
            server.AddModule(this);
            server.Start();
            msg?.AddMessage(Prefix + "Started a http server at port 80");
            return new AsDisposable(() =>
            {
                msg?.AddMessage(Prefix + "Stopping the http server at port 80", MessageLevels.Debug);
                server.Dispose();
                msg?.AddMessage(Prefix + "Stopped the http server at port 80");
            });
        }

        async Task<X509Certificate2> RequestNewCertificate(AcmeContext context, IAccountContext account)
        {
            var msg = Msg;
            //  Create an order
            msg?.AddMessage(Prefix + "Creating a new order for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), MessageLevels.Debug);
            IOrderContext orderContext;
            try
            {
                orderContext = await context.NewOrder(Names).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to create new certificate order context for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), ex, MessageLevels.Warning);
                throw;
            }
            //  Authorize
            msg?.AddMessage(Prefix + "Getting auhorizations");
            var authContext = await orderContext.Authorizations().ConfigureAwait(false);
            var cfolder = ChallengeFolder;
            var tokens = Tokens;
            var challenges = new List<Tuple<IChallengeContext, string, string>>();
            foreach (var a in authContext)
            {
                IChallengeContext challangeContext;
                try
                {
                    challangeContext = await a.Http().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    msg?.AddMessage(Prefix + "Failed to create a HTTP challenge for new certificate order context for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), ex, MessageLevels.Warning);
                    throw;
                }
                var key = challangeContext.Token;
                var value = challangeContext.KeyAuthz;
                String cfile = null;
                if (cfolder != null)
                {
                    cfile = Path.Combine(cfolder, key);
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllTextAsync(cfile, value)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to save challenge file to " + cfile.ToFilename(), ex, MessageLevels.Warning);
                        throw;
                    }
                }
                else
                {
                    tokens[key] = Encoding.UTF8.GetBytes(value);
                }
                challenges.Add(Tuple.Create(challangeContext, key, cfile));
                msg?.AddMessage(Prefix + "Created a http challenge using: " + key.ToQuoted() + " = " + value.ToQuoted(), MessageLevels.Debug);
            }
            //  Start http server if none is running
            IDisposable server = null;
            if (cfolder == null)
                server = StartHttpServer();
            using (server)
            {
                msg?.AddMessage(Prefix + "Waiting for http challanges to be completed");
                foreach (var c in challenges)
                {
                    var challangeContext = c.Item1;
                    try
                    {
                        for (bool first = true; ; first = false)
                        {
                            Challenge res;
                            try
                            {
                                res = first ? (await challangeContext.Validate().ConfigureAwait(false)) : (await challangeContext.Resource().ConfigureAwait(false));
                            }
                            catch
                            {
                                if (!first)
                                    throw;
                                res = await challangeContext.Resource().ConfigureAwait(false);
                            }
                            if ((res.Status == ChallengeStatus.Pending) || (res.Status == ChallengeStatus.Processing))
                            {
                                await Task.Delay(1000).ConfigureAwait(false);
                                continue;
                            }
                            if (res.Status == ChallengeStatus.Valid)
                                break;
                            throw new Exception("Failed to validate challange " + c.Item2.ToQuoted() + ", error: " + res.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to validate challenge " + c.Item2.ToQuoted(), ex, MessageLevels.Warning);
                        throw;
                    }
                    var file = c.Item3;
                    if (file != null)
                    {
                        var ex = PathExt.TryDeleteFile(file);
                        if (ex != null)
                            msg?.AddMessage(Prefix + "Failed to delete challenge file " + file.ToQuoted() + ", ignoring!", ex, MessageLevels.Warning);
                    }
                    else
                    {
                        tokens.TryRemove(c.Item2, out var _);
                    }
                }
            }
            msg?.AddMessage(Prefix + "Waiting for the order finalizing to complete");
            //  Finalize order
            var certKey = KeyFactory.NewKey(KeyAlgorithm.RS256);
            Order inst;
            try
            {
                inst = await orderContext.Finalize(Info, certKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to finalize new certificate order for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), ex, MessageLevels.Warning);
                throw;
            }
            try
            {
                for (; ; )
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    var res = await orderContext.Resource().ConfigureAwait(false);
                    if ((res.Status == OrderStatus.Pending) || (res.Status == OrderStatus.Processing))
                    {
                        continue;
                    }
                    if (res.Status == OrderStatus.Valid)
                        break;
                    throw new Exception("Failed to finalize order, error: " + res.Error);
                }
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to finalize order", ex, MessageLevels.Warning);
                throw;
            }
            msg?.AddMessage(Prefix + "Download the certificate", MessageLevels.Debug);
            CertificateChain certChain;
            try
            {
                certChain = await orderContext.Download().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to download new certificate chain for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), ex, MessageLevels.Warning);
                throw;
            }
            msg?.AddMessage(Prefix + "Building certificate from downloaded data", MessageLevels.Debug);
            try
            {
                var pfxBuilder = certChain.ToPfx(certKey);
                pfxBuilder.FullChain = false;
                foreach (var importCert in await GetImportCerts().ConfigureAwait(false))
                    pfxBuilder.AddIssuer(importCert);
                var pwd = Password;
                var cert = pfxBuilder.Build("SysWeaver ACME " + Info.CommonName, pwd);
                var fs = new X509Certificate2(cert, pwd, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                msg?.AddMessage(Prefix + "Certificate is built successfully", MessageLevels.Debug);
                return fs;
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to create certificate from data for: " + String.Join(", ", Names.Select(x => x.ToQuoted())), ex, MessageLevels.Warning);
                throw;
            }
        }

        async Task<Tuple<AcmeContext, IAccountContext>> GetContext()
        {
            var msg = Msg;
            var baseUrl = AuthApi;
            var accountFile = KeyFilename;
            var mail = Mail;
            AcmeContext context = null;
            IAccountContext account = null;
            IKey accountKey = null;
            if (File.Exists(accountFile))
            {
                //  Load exisitng key
                try
                {
                    accountKey = KeyFactory.FromPem(await File.ReadAllTextAsync(accountFile).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    msg?.AddMessage(Prefix + "Failed to load account key from " + accountFile.ToFilename() + ", creating a new account!", ex, MessageLevels.Warning);
                }
                //  Log in to account
                if (accountKey != null)
                {
                    msg?.AddMessage(Prefix + "Logging in to account");
                    context = new AcmeContext(baseUrl, accountKey);
                    try
                    {
                        account = await context.Account().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to get account information, creating a new account!", ex, MessageLevels.Warning);
                    }
                }
                //  Get account resources
                if (account != null)
                {
                    try
                    {
                        var info = await account.Resource().ConfigureAwait(false);
                        switch (info.Status)
                        {
                            case AccountStatus.Valid:
                                //  Check for any update requirements
                                bool mustAgree = !(info.TermsOfServiceAgreed ?? true);
                                if (mustAgree)
                                    msg?.AddMessage(Prefix + "Terms of service changed, accepting new terms automatically!", MessageLevels.Warning);
                                bool newMail;
                                if ((info.Contact == null) || (info.Contact.Count != 1))
                                {
                                    newMail = true;
                                }
                                else
                                {
                                    newMail = !String.Equals(mail, info.Contact[0].Replace("mailto:", "", StringComparison.OrdinalIgnoreCase).Trim(), StringComparison.OrdinalIgnoreCase);
                                }
                                if (newMail)
                                    msg?.AddMessage(Prefix + "Mail address changed, updating!", MessageLevels.Warning);
                            //  Update resources
                                if (newMail || mustAgree)
                                {
                                    try
                                    {
                                        await account.Update(
                                            [
                                                "mailto:" + mail
                                            ],
                                            true).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        msg?.AddMessage(Prefix + "Failed to update account information, trying to continue anyway!", ex, MessageLevels.Warning);
                                    }
                                }
                                break;
                            default:
                                msg?.AddMessage(Prefix + "Account status is " + info.Status + ", creating a new account!", MessageLevels.Warning);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to get account resources, trying to continue anyway!", ex, MessageLevels.Warning);
                    }
                    return Tuple.Create(context, account);
                }
            }
        //  Create new account
            msg?.AddMessage(Prefix + "Creating a new account using mail " + mail.ToMail(), MessageLevels.Debug);
            context = new AcmeContext(baseUrl);
            try
            {
                account = await context.NewAccount(mail, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to create a new account using " + mail.ToMail(), ex, MessageLevels.Warning);
                throw;
            }
            //  Make sure destination folder exists
            msg?.AddMessage(Prefix + "Saving account key to " + accountFile.ToFilename(), MessageLevels.Debug);
            accountKey = context.AccountKey;
            var pex = PathExt.EnsureCanWriteFile(accountFile);
            if (pex != null)
                msg?.AddMessage(Prefix + "Failed to create the folder required to store " + accountFile.ToFilename(), pex, MessageLevels.Warning);
        //  Save account key
            try
            {
                await Retry.OpAsync(() => File.WriteAllTextAsync(accountFile, accountKey.ToPem())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to save account key to " + accountFile.ToFilename(), ex, MessageLevels.Warning);
                throw;
            }
            msg?.AddMessage(Prefix + "Created new account using mail " + mail.ToMail());
            return Tuple.Create(context, account);
        }

        X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        IDisposable ExpireAction;

        void InvokeExpireSoon()
        {
            Interlocked.Exchange(ref C, null)?.Dispose();
            OnChanged?.Invoke();
        }

        public async Task<X509Certificate2> GetCert()
        {
            var c = C;
            if (c != null)
                return c;
            var msg = Msg;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            try
            {
                c = C;
                if (c != null)
                    return c;
                Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            //  Use existing file if present and valid
                var f = Filename;
                var pw = Password;
                var haveFile = !String.IsNullOrEmpty(f);
                if (haveFile && File.Exists(f))
                {
                    try
                    {
                        c = await CertificateTools.Load(f, pw, false).ConfigureAwait(false);
                        if (!CertificateTools.IsSoonExpired(c, out var expiresX, MinValidHours))
                        {
                            Interlocked.Exchange(ref C, c)?.Dispose();
                            var renewAtX = expiresX.AddHours(RenewBeforeExpirationHours);
                            ExpireAction = Scheduler.Add(renewAtX, InvokeExpireSoon, true);
//                            if (TestCount == 1)
//                                ExpireAction = TaskScheduler.Add(DateTime.UtcNow.AddSeconds(30), InvokeExpireSoon, true); C = null; PathExt.TryDeleteFile(f); // HACK: Used to test renewal
                            msg?.AddMessage(Prefix + "Loaded certificate, expires at " + expiresX.ToString("o") + ", renewal is scheduled at " + renewAtX.ToString("o"));
                            return c;
                        }
                        c.Dispose();
                        msg?.AddMessage(Prefix + "Loaded certificate has expired, will request a new certificate");
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to load certificate from " + f.ToFilename() + ", will request a new certificate", ex, MessageLevels.Warning);
                    }
                }
            //  Create a new certificate
                var contexts = await GetContext().ConfigureAwait(false);
                c = await RequestNewCertificate(contexts.Item1, contexts.Item2).ConfigureAwait(false);

                if (haveFile)
                {
                    msg?.AddMessage(Prefix + "Saving certificate file " + f.ToFilename(), MessageLevels.Debug);
                    var pex = PathExt.EnsureCanWriteFile(f);
                    if (pex != null)
                        msg?.AddMessage(Prefix + "Failed to create folder required to save " + f.ToFilename(), pex, MessageLevels.Warning);
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllBytesAsync(f, c.Export(X509ContentType.Pfx, Password))).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to save certificate file " + f.ToFilename(), ex, MessageLevels.Warning);
                    }
                    var crtFile = Path.ChangeExtension(f, "crt");
                    msg?.AddMessage(Prefix + "Saving public certificate file " + crtFile.ToFilename(), MessageLevels.Debug);
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllTextAsync(crtFile, c.ExportCertificatePem())).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(Prefix + "Failed to save public certificate file " + crtFile.ToFilename(), ex, MessageLevels.Warning);
                    }
                }
                Interlocked.Exchange(ref C, c)?.Dispose();
                var expires = c.GetExpiration();
                var renewAt = expires.AddHours(RenewBeforeExpirationHours);
                ExpireAction = Scheduler.Add(renewAt, InvokeExpireSoon, true);
                msg?.AddMessage(Prefix + "Created certificate, expires at " + expires.ToString("o") + ", renewal is scheduled at " + renewAt.ToString("o"));
                return c;
            }
            finally
            {
                l.Release();
            }
        }

        const String ChallengeDir = ".well-known/acme-challenge/";

        static readonly int CdLength = ChallengeDir.Length;

        public String[] OnlyForPrefixes { get; } = [ChallengeDir];

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var l = context.LocalUrl;
            //if (!l.FastStartsWith(ChallengeDir))
                //return null;
            if (!context.Uri.Scheme.FastEquals("http"))
                return null;
            var key = l.Substring(CdLength);
            if (!Tokens.TryGetValue(key, out var token))
                return null;
            context.SetResMime(HttpServerTools.TextMime);
            context.SetResStatusCode(200);
            context.SetResBody(token);
            return HttpServerTools.AlreadyHandled;
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        public event Action OnChanged;

    }
}