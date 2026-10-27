using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
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
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.Security
{


    /// <summary>
    /// Provides a certificate for a public domain from an ACME certificate authority (Let's Encrypt by default, using Certes), validated using HTTP-01 challenges.
    /// </summary>
    /// <remarks>
    /// The certificate (pfx, protected by <see cref="CertificateBaseParams.Password"/>, and a PEM ".crt") and the account key are cached on disc.
    /// <see cref="GetCert"/> uses the cached certificate if it's valid for at least the minimum valid hours, else it logs in to (or creates) the account, orders a new certificate,
    /// and serves the challenges either as files in the challenge folder or using this module (a temporary HTTP server on port 80 is started if no challenge folder is configured).
    /// Terms of service changes are accepted automatically.
    /// When the certificate is about to expire, it's dropped and <see cref="OnChanged"/> is raised (the next <see cref="GetCert"/> orders a new certificate).
    /// </remarks>
    public sealed class AcmeCertificateProvider : ICertificateProvider, IHttpServerModule, IHaveDefaultInstanceName
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
                            msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Can't import certificate for {bn.ToQuoted()}", ex);
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
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Can't import certificate {f.ToQuoted()} since it doesn't exist!");
                        continue;
                    }
                    var pem = await FileExt.ReadTextAsync(f).ConfigureAwait(false);
                    Byte[] certBytes;
                    try
                    {
                        certBytes = CertificateTools.GetCertBytes(pem);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Can't import certificate {f.ToQuoted()}", ex);
                        continue;
                    }
                    certs.Add(certBytes);
                }
            }
            return certs;
        }

        /// <summary>
        /// Create an ACME certificate provider, nothing is requested until <see cref="GetCert"/> is called.
        /// Resolves the file names and creates the folders of the certificate, account key and challenge files (failures are only reported to <paramref name="msg"/>).
        /// </summary>
        /// <param name="msg">Optional message host for progress and failures.</param>
        /// <param name="p">Parameters, null uses the defaults (which throws, since an email and a domain name are required).</param>
        /// <exception cref="ArgumentException">The email or domain name is missing, or a subject field is invalid (see <see cref="SignedCertificateCreator.ValidateString(string, string)"/>).</exception>
        /// <exception cref="UriFormatException"><see cref="AcmeCertificateParams.AuthApi"/> isn't a valid absolute uri.</exception>
        public AcmeCertificateProvider(IMessageHost msg = null, AcmeCertificateParams p = null)
        {
            Msg = msg;
            p = p ?? new AcmeCertificateParams();
            var email = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.Email.Trim()), nameof(p.Email));
            if (String.IsNullOrEmpty(email))
                throw new ArgumentException("Must provide a valid email to create an account!", nameof(p.Email));
            var domainName = SignedCertificateCreator.ValidateString(EnvInfo.ResolveText(p.DomainName), nameof(p.DomainName));
            if (String.IsNullOrEmpty(domainName))
                throw new ArgumentException("Must provide a valid domain name!", nameof(p.DomainName));
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
            var certFilename = String.IsNullOrEmpty(p.Filename) ? null : EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename, extra));
            var cex = certFilename == null ? null : PathExt.EnsureCanWriteFile(certFilename);
            if (cex != null)
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create the folder required to store {certFilename.ToFilename()}", cex);
            Filename = certFilename;
            Password = EnvInfo.ResolveText(p.Password);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
            //  A loaded certificate that is due for renewal must be replaced, else the renewal is re-scheduled in the past (immediately, in a loop) (security-cov-2)
            MinValidHours = Math.Max(MinValidHours, -RenewBeforeExpirationHours);
            ImportCertFiles = p.ImportCertFiles;

            //  Setup challenge folder
            var challengeFolder = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.ChallengeFolder, extra));
            if (String.IsNullOrEmpty(challengeFolder))
                challengeFolder = null;
            if (challengeFolder != null)
            {
                var ex = PathExt.EnsureFolderExist(challengeFolder);
                if (ex != null)
                    msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create the challenge folder {challengeFolder.ToFolder()}", ex);
            }
            ChallengeFolder = challengeFolder;
            var accountFile = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.AccountFilename, extra));
            KeyFilename = accountFile;
            var aex = PathExt.EnsureCanWriteFile(accountFile);
            if (aex != null)
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create the folder required to store {accountFile.ToFilename()}", aex);
            var authUrl = new Uri(authApi);
            AuthApi = authUrl;
            Mail = email;
            msg?.AddMessage(MessageLevels.Info, $"{Prefix}Using API at {authUrl.ToString().ToQuoted()}");
        }

        /// <summary>
        /// The domain name.
        /// </summary>
        public string DefaultInstanceName => Names[0];


        readonly String[] ImportCertFiles;
        readonly String Mail;
        readonly Uri AuthApi;
        readonly String KeyFilename;
        readonly String[] Names;
        readonly CsrInfo Info;
        readonly String ChallengeFolder;
        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        /// <summary>
        /// The resolved full name of the cached certificate (pfx) file, null if caching is disabled (no file name).
        /// </summary>
        public readonly string Filename;
        readonly string Password;
        readonly IMessageHost Msg;

        readonly ConcurrentDictionary<String, Byte[]> Tokens = new ConcurrentDictionary<string, Byte[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get the name of the file that a HTTP-01 challenge key authorization is written to when a challenge folder is used.
        /// The token comes from the CA, every char that is invalid in a file name (including directory and volume separators) is replaced with a '_' (see <see cref="PathExt.SafeFilename(string)"/>),
        /// so a malicious token (ex: "..\..\x.aspx") can't write outside of the challenge folder.
        /// Valid RFC 8555 tokens (base64url chars) are never changed; for a changed token the file has the changed name, so the CA can't fetch it (that challenge fails validation).
        /// </summary>
        /// <param name="challengeFolder">The challenge folder.</param>
        /// <param name="token">The challenge token from the CA.</param>
        /// <returns>The full name of the challenge file (inside the challenge folder).</returns>
        public static String GetChallengeFilename(String challengeFolder, String token)
            => Path.Combine(challengeFolder, PathExt.SafeFilename(token));

        IDisposable StartHttpServer()
        {
            var msg = Msg;
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Starting a http server at port 80");
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
                msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Stopping the http server at port 80");
                server.Dispose();
                msg?.AddMessage(Prefix + "Stopped the http server at port 80");
            });
        }

        async Task<X509Certificate2> RequestNewCertificate(AcmeContext context, IAccountContext account)
        {
            var msg = Msg;
            //  Create an order
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Creating a new order for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}");
            IOrderContext orderContext;
            try
            {
                orderContext = await context.NewOrder(Names).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create new certificate order context for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}", ex);
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
                    msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create a HTTP challenge for new certificate order context for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}", ex);
                    throw;
                }
                var key = challangeContext.Token;
                var value = challangeContext.KeyAuthz;
                String cfile = null;
                if (cfolder != null)
                {
                    cfile = GetChallengeFilename(cfolder, key);
                    if (!String.Equals(Path.GetFileName(cfile), key, StringComparison.Ordinal))
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}The challenge token {key.ToQuoted()} contains invalid file name chars, using the file {cfile.ToFilename()} (the challenge validation will probably fail)");
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllTextAsync(cfile, value)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to save challenge file to {cfile.ToFilename()}", ex);
                        throw;
                    }
                }
                else
                {
                    tokens[key] = Encoding.UTF8.GetBytes(value);
                }
                challenges.Add(Tuple.Create(challangeContext, key, cfile));
                msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Created a http challenge using: {key.ToQuoted()} = {value.ToQuoted()}");
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
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to validate challenge {c.Item2.ToQuoted()}", ex);
                        throw;
                    }
                    var file = c.Item3;
                    if (file != null)
                    {
                        var ex = PathExt.TryDeleteFile(file);
                        if (ex != null)
                            msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to delete challenge file {file.ToQuoted()}, ignoring!", ex);
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
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to finalize new certificate order for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}", ex);
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
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Download the certificate");
            CertificateChain certChain;
            try
            {
                certChain = await orderContext.Download().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to download new certificate chain for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}", ex);
                throw;
            }
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Building certificate from downloaded data");
            try
            {
                var pfxBuilder = certChain.ToPfx(certKey);
                pfxBuilder.FullChain = false;
                foreach (var importCert in await GetImportCerts().ConfigureAwait(false))
                    pfxBuilder.AddIssuer(importCert);
                var pwd = Password;
                var cert = pfxBuilder.Build("SysWeaver ACME " + Info.CommonName, pwd);
                var fs = X509CertificateLoader.LoadPkcs12(cert, pwd, CertificateTools.InMemoryKeyStorageFlags);
                //var fs = new X509Certificate2(cert, pwd, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Certificate is built successfully");
                return fs;
            }
            catch (Exception ex)
            {
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create certificate from data for: {String.Join(", ", Names.Select(x => x.ToQuoted()))}", ex);
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
                    accountKey = KeyFactory.FromPem(await FileExt.ReadTextAsync(accountFile).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to load account key from {accountFile.ToFilename()}, creating a new account!", ex);
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
                                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Account status is {info.Status}, creating a new account!");
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
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Creating a new account using mail {mail.ToMail()}");
            context = new AcmeContext(baseUrl);
            try
            {
                account = await context.NewAccount(mail, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create a new account using {mail.ToMail()}", ex);
                throw;
            }
            //  Make sure destination folder exists
            msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Saving account key to {accountFile.ToFilename()}");
            accountKey = context.AccountKey;
            var pex = PathExt.EnsureCanWriteFile(accountFile);
            if (pex != null)
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create the folder required to store {accountFile.ToFilename()}", pex);
        //  Save account key
            try
            {
                await Retry.OpAsync(() => File.WriteAllTextAsync(accountFile, accountKey.ToPem())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to save account key to {accountFile.ToFilename()}", ex);
                throw;
            }
            msg?.AddMessage(MessageLevels.Info, $"{Prefix}Created new account using mail {mail.ToMail()}");
            return Tuple.Create(context, account);
        }

        X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        IDisposable ExpireAction;

        void InvokeExpireSoon()
        {
            Interlocked.Exchange(ref C, null)?.Dispose();
            OnChanged?.Invoke(null);
        }

        /// <summary>
        /// Get the certificate: the cached one if it's valid long enough, else a new certificate is ordered from the certificate authority (this can take a while).
        /// </summary>
        /// <returns>The certificate (owned by the provider, do not dispose it).</returns>
        /// <exception cref="Exception">The account couldn't be created, or ordering / validating / downloading the certificate failed.</exception>
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
                            ExpireAction = Scheduler.Add(renewAtX, InvokeExpireSoon, "ACME cert renewal");
//                            if (TestCount == 1)
//                                ExpireAction = TaskScheduler.Add(DateTime.UtcNow.AddSeconds(30), InvokeExpireSoon, true); C = null; PathExt.TryDeleteFile(f); // HACK: Used to test renewal
                            msg?.AddMessage(MessageLevels.Info, $"{Prefix}Loaded certificate, expires at {expiresX.ToString("o")}, renewal is scheduled at {renewAtX.ToString("o")}");
                            return c;
                        }
                        c.Dispose();
                        msg?.AddMessage(Prefix + "Loaded certificate has expired, will request a new certificate");
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to load certificate from {f.ToFilename()}, will request a new certificate", ex);
                    }
                }
            //  Create a new certificate
                var contexts = await GetContext().ConfigureAwait(false);
                c = await RequestNewCertificate(contexts.Item1, contexts.Item2).ConfigureAwait(false);

                if (haveFile)
                {
                    msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Saving certificate file {f.ToFilename()}");
                    var pex = PathExt.EnsureCanWriteFile(f);
                    if (pex != null)
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to create folder required to save {f.ToFilename()}", pex);
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllBytesAsync(f, c.Export(X509ContentType.Pfx, Password))).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to save certificate file {f.ToFilename()}", ex);
                    }
                    var crtFile = Path.ChangeExtension(f, "crt");
                    msg?.AddMessage(MessageLevels.Debug, $"{Prefix}Saving public certificate file {crtFile.ToFilename()}");
                    try
                    {
                        await Retry.OpAsync(() => File.WriteAllTextAsync(crtFile, c.ExportCertificatePem())).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        msg?.AddMessage(MessageLevels.Warning, $"{Prefix}Failed to save public certificate file {crtFile.ToFilename()}", ex);
                    }
                }
                Interlocked.Exchange(ref C, c)?.Dispose();
                var expires = c.GetExpiration();
                var renewAt = expires.AddHours(RenewBeforeExpirationHours);
                ExpireAction = Scheduler.Add(renewAt, InvokeExpireSoon, "ACME cert renewal");
                msg?.AddMessage(MessageLevels.Info, $"{Prefix}Created certificate, expires at {expires.ToString("o")}, renewal is scheduled at {renewAt.ToString("o")}");
                return c;
            }
            finally
            {
                l.Release();
            }
        }

        const String ChallengeDir = ".well-known/acme-challenge/";

        static readonly int CdLength = ChallengeDir.Length;

        /// <summary>
        /// The module only handles requests below ".well-known/acme-challenge/".
        /// </summary>
        public String[] OnlyForPrefixes { get; } = [ChallengeDir];


        /// <summary>
        /// Serve the key authorization of a pending HTTP-01 challenge (only http requests, and only while challenges are served by this module, i.e. no challenge folder is used).
        /// </summary>
        /// <param name="context">The request.</param>
        /// <returns><see cref="HttpServerTools.AlreadyHandled"/> if the token is known (the response is set), else null.</returns>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var l = context.LocalUrl;
            if (!context.Url.FastStartsWith("http://"))
                return null;
            var key = l.Substring(CdLength);
            if (!Tokens.TryGetValue(key, out var token))
                return null;
            context.SetResMime(HttpServerTools.TextMime);
            context.SetResStatusCode(200);
            context.SetResBody(token, 0, token.Length);
            return HttpServerTools.AlreadyHandled;
        }

        /// <summary>
        /// No end points are listed (challenges are temporary).
        /// </summary>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        /// <summary>
        /// An event that is fired when the certificate is about to expire.
        /// The argument is always null and the old certificate has already been disposed, call <see cref="GetCert"/> again to get a renewed certificate.
        /// </summary>
        public event Action<X509Certificate2> OnChanged;

    }
}