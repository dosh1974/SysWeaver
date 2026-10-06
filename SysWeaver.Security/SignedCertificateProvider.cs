
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
    /// A certificate provider that provides a certificate signed by a local root (CA) certificate stored in a pfx file.
    /// The generated certificate is (optionally) cached between executions.
    /// Optionally the public part of the root certificate is published on the HTTP server (as an <see cref="IHttpServerModule"/>) so that clients can download and trust it.
    /// </summary>
    /// <remarks>
    /// A cached certificate is reused if it matches the parameters, isn't about to expire and was issued by the current root certificate.
    /// The root file is monitored, if it changes the cached certificate is disposed and <see cref="OnChanged"/> is raised.
    /// Thread safe.
    /// </remarks>
    public sealed class SignedCertificateProvider : ICertificateProvider, IDisposable, IHttpServerModule
    {
        /// <inheritdoc/>
        public override string ToString() => Filename;

        const String Prefix = "[SignedCertificateProvider] ";

        /// <summary>
        /// Create a provider that provides a certificate signed by a local root certificate.
        /// If <see cref="SignedCertificateProviderParams.PublishRoot"/> is true, the root certificate is loaded asynchronously and its public part is exposed as http end points.
        /// </summary>
        /// <param name="msg">Optional message handler, used to report problems with the root certificate.</param>
        /// <param name="p">Parameters, null uses the defaults.</param>
        /// <exception cref="Exception">A subject field contains an invalid character.</exception>
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
                var rootPos = t.FastIndexOf("$(");
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
                        new HttpServerEndPoint(b + name, "[Implicit Folder] from [SignedCertificateProvider]", HttpServerTools.StartedTime, HttpServerTools.StartedETag),
                    ];
                }
            }
        }

        /// <summary>
        /// If the root certificate is published and the uri template contains variables, this is the fixed part before the first variable,
        /// so that the module is only consulted for requests below that prefix. Null if not applicable.
        /// </summary>
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
                var lwt = fi.LastWriteTimeUtc;
                var etag = HttpServerTools.ToEtag(lwt);
                using var c = await CertificateTools.Load(RootFilename, RootPassword).ConfigureAwait(false);
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
                    var ep = new StaticMemoryHttpRequestHandler(name, Prefix + "Extracted public certificate", pem, MimeTypeMap.PlainText, null, 5, 5, lwt, etag);
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

        /// <summary>
        /// Get the certificate, loading it from the cache file or creating (and caching) a new one signed by the root certificate if needed.
        /// </summary>
        /// <returns>The certificate (owned by the provider, do not dispose it).</returns>
        /// <exception cref="Exception">The root certificate file doesn't exist or couldn't be loaded.</exception>
        public async Task<X509Certificate2> GetCert()
        {
            var c = C;
            if (c != null)
                return c;
            var l = Lock;
            await l.WaitAsync().ConfigureAwait(false);
            try
            {
                c = C;
                if (c != null)
                    return c;
                Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
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
                                        ExpireAction = Scheduler.Add(expires.AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, "Self signed cert renewal");
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
                ExpireAction = Scheduler.Add(c.GetExpiration().AddHours(RenewBeforeExpirationHours), InvokeExpireSoon, "Self signed cert renewal");
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
            OnChanged?.Invoke(null);
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
                OnChanged?.Invoke(null);
            }
        }


        /// <summary>
        /// An event that is fired whenever the root certificate file have changed or if the certificate is about to expire.
        /// The argument is always null and the old certificate has already been disposed, call <see cref="GetCert"/> again to get the updated certificate.
        /// </summary>
        public event Action<X509Certificate2> OnChanged;

        IDisposable Fw;

        /// <summary>
        /// Cancel the scheduled renewal, stop monitoring the root file and dispose the current certificate.
        /// </summary>
        public void Dispose()
        {
            var l = Lock;
            l.Wait();
            Interlocked.Exchange(ref ExpireAction, null)?.Dispose();
            Interlocked.Exchange(ref Fw, null)?.Dispose();
            Interlocked.Exchange(ref C, null)?.Dispose();
            l.Release();
        }

        /// <summary>
        /// Get the handler for a published root certificate file.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <returns>The handler serving the PEM encoded root certificate, or null if the local url isn't a published file.</returns>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            Files.TryGetValue(context.LocalUrl, out var h);
            return h;
        }

        /// <summary>
        /// Enumerate the published root certificate end points (and the implicit folders leading to them).
        /// </summary>
        /// <param name="root">Null to enumerate all end points, else only the end points directly in this folder (ex: "certificates/").</param>
        /// <returns>The end points.</returns>
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
