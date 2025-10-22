
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using SysWeaver.Net;
using System.Collections.Concurrent;
using System.Text;

namespace SysWeaver.Security
{


    /// <summary>
    /// A certificate provider that provides a signed certificate.
    /// The generated certificate is (optionally) cached between executions.
    /// </summary>
    public sealed class SignedCertificateProvider : ICertificateProvider, IDisposable, IHttpServerModule
    {
        public override string ToString() => Filename;

        const String Prefix = "[SignedCertificateProvider] ";

        /// <summary>
        /// A certificate provider that provides a signed certificate.
        /// The generated certificate is (optionally) cached between executions.
        /// </summary>
        /// <param name="msg">Message handler</param>
        /// <param name="p">Paramaters</param>
        public SignedCertificateProvider(IMessageHost msg = null, SignedCertificateProviderParams p = null)
        {
            p = p ?? new SignedCertificateProviderParams();
            P = new SignedCertificateCreator(p);
            Msg = msg;
            Filename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename));
            Password = EnvInfo.ResolveText(p.Password);
            RootFilename = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.RootFilename));
            RootPassword = PathTemplate.Resolve(p.RootPassword);
            MinValidHours = Math.Max(p.MinValidHours, 2);
            RenewBeforeExpirationHours = -Math.Max(p.RenewBeforeExpirationHours, (MinValidHours + 1) >> 1);
            var u = p.RootCertUri?.Trim(' ', '\t', '\n', '\\', '/');
            bool publish = p.PublishRoot && (!String.IsNullOrEmpty(u));
            if (publish)
            {
                var t = EnvInfo.ResolveText(u.Replace('\\', '/'));
                FileTemplate = t;
                var rootPos = t.IndexOf("$(");
                if (rootPos > 0)
                    OnlyForPrefixes = [t.Substring(0, rootPos)];

                var li = t.LastIndexOf('/');
                var root = li < 0 ? "" : t.Substring(0, li);
                FileRoot = root.Length > 0 ? (root + '/') : root;
                TaskExt.RunAsync(SetFiles());
                var eps = EndPoints;
                while (root.Length > 0)
                {
                    li = root.LastIndexOf('/');
                    var name = root.Substring(li + 1);
                    root = li < 0 ? "" : root.Substring(0, li);
                    var b = root.Length > 0 ? (root + '/') : root;
                    eps[b] =
                    [
                        new HttpServerEndPoint(b + name, "[Implicit Folder] from [SignedCertificateProvider]", HttpServerTools.StartedTime),
                    ];
                }
            }
        }

        public String[] OnlyForPrefixes { get; init; }


        readonly IMessageHost Msg;
        readonly String FileRoot;
        readonly String FileTemplate;
        
        async Task<bool> SetFiles()
        {
            try
            {
                //  Load root cert
                var fi = new FileInfo(RootFilename);
                if (!fi.Exists)
                {
                    Msg?.AddMessage(Prefix + "Can't get public certificate from " + RootFilename.ToFilename() + " since the file doesn't exist!", MessageLevels.Warning);
                    return false;
                }
                var lw = HttpServerTools.ToEtag(fi.LastAccessTimeUtc);
                var c = await CertificateTools.Load(RootFilename, RootPassword).ConfigureAwait(false);
                var pem = Encoding.UTF8.GetBytes(c.ExportCertificatePem());
                var fileTemp = FileTemplate;
                Dictionary<String, String> extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "filename", Path.GetFileNameWithoutExtension(RootFilename) },
                };
                var exts = Extensions;
                var el = exts.Length;
                IHttpServerEndPoint[] eps = GC.AllocateUninitializedArray<IHttpServerEndPoint>(el);
                var files = Files;
                for (int i = 0; i < el; ++ i)
                {
                    extra["ext"] = exts[i];
                    var name = EnvInfo.ResolveText(fileTemp, true, extra);
                    var ep = new StaticMemoryHttpRequestHandler(name, Prefix + "Extracted public certificate", pem, MimeTypeMap.PlainText, null, 5, 5, lw);
                    files[name] = ep;
                    eps[i] = ep;
                }
                EndPoints[FileRoot] = eps;
                return true;
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(Prefix + "Failed to get public certificate from " + RootFilename.ToFilename(), ex, MessageLevels.Warning);
                return false;
            }
        }


        readonly ConcurrentDictionary<String, IHttpRequestHandler> Files = new ConcurrentDictionary<string, IHttpRequestHandler>(StringComparer.OrdinalIgnoreCase);

        readonly ConcurrentDictionary<String, IHttpServerEndPoint[]> EndPoints = new ConcurrentDictionary<string, IHttpServerEndPoint[]>(StringComparer.OrdinalIgnoreCase);


        static readonly String[] Extensions =
        [
            "pem", "crt",
        ];

        readonly int MinValidHours;
        readonly int RenewBeforeExpirationHours;
        readonly string Filename;
        readonly string Password;
        readonly string RootFilename;
        readonly string RootPassword;

        readonly SignedCertificateCreator P;

        X509Certificate2 C;

        readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        IDisposable ExpireAction;

        public async Task<X509Certificate2> GetCert()
        {
            var c = C;
            if (c != null)
                return c;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            try
            {
                c = C;
                if (c != null)
                    return c;
                var p = P;
                var f = Filename;
                var rootFile = RootFilename;
                var pw = Password;
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
                                //  Must create a new
                                using (var root = await CertificateTools.Load(rootFile, RootPassword).ConfigureAwait(false))
                                {
                                    if (root.Subject == c.Issuer)
                                    {
                                        Interlocked.Exchange(ref C, c)?.Dispose();
                                        ExpireAction = Scheduler.Add(expires.AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, true);
                                        return c;
                                    }
                                }
                            }
                        }
                        c.Dispose();
                    }
                    catch
                    {
                    }
                }
                //  Must create a new
                using (var root = await CertificateTools.Load(rootFile, RootPassword).ConfigureAwait(false))
                {
                    if (Fw == null)
                        Fw = new OnFileChangeAsync(rootFile, InvokeRootChanged);
                    c = p.Create(root);
                }
                if (haveFile)
                {
                    var dir = Path.GetDirectoryName(f);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(f, c.Export(X509ContentType.Pfx, pw)).ConfigureAwait(false);
                    await File.WriteAllTextAsync(Path.ChangeExtension(f, "crt"), c.ExportCertificatePem()).ConfigureAwait(false);
                }
                Interlocked.Exchange(ref C, c)?.Dispose();
                ExpireAction = Scheduler.Add(c.GetExpiration().AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, true);
                return c;
            }
            finally
            {
                l.Release();
            }
        }

        void InvokeExpireSoon()
        {
            Interlocked.Exchange(ref C, null)?.Dispose();
            OnChanged?.Invoke();
        }

        async Task InvokeRootChanged(String name)
        {
            bool ok = false;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var c = await CertificateTools.Load(RootFilename, RootPassword).ConfigureAwait(false))
                    ok = true;
                await SetFiles().ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                l.Release();
            }
            if (ok)
            {
                Interlocked.Exchange(ref C, null)?.Dispose();
                OnChanged?.Invoke();
            }
        }


        /// <summary>
        /// An event that is fired whenever the certificate file have changed or if the certificate is about to expire.
        /// An application should restart (or re-init) to get the updated cert (calling GetCert again will return an updated cert).
        /// </summary>
        public event Action OnChanged;

        IDisposable Fw;

        public void Dispose()
        {
            var l = Lock;
            l.Wait();
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            Interlocked.Exchange(ref Fw, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
            l.Release();
        }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            Files.TryGetValue(context.LocalUrl, out var h);
            return h;
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            if (root == null)
            {
                foreach (var x in EndPoints)
                {
                    foreach (var y in x.Value)
                        yield return y;
                }
            }else
            {
                if (EndPoints.TryGetValue(root, out var x))
                {
                    foreach (var y in x)
                        yield return y;
                }
            }
        }
    }


}
