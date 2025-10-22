using QRCoder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Net;



namespace SysWeaver.MicroService
{



    /// <summary>
    /// </summary>
    [IsMicroService]
    public sealed class QrCodeService : IHttpServerModule, IPerfMonitored, IQrCodeService
    {
        public override string ToString()
        {
            var rs = AllEndpoints;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " end point: " : " end points: ", String.Join(", ", rs.Select(x => x.Key.ToQuoted())));
        }

        public QrCodeService(QrCodeParams p = null)
        {
            p = p ?? new QrCodeParams();
            var all = new Dictionary<string, QrEndPoint>(StringComparer.Ordinal);
            var folders = new Dictionary<string, List<IHttpServerEndPoint>>(StringComparer.Ordinal);
            var eps = p.EndPoints;
            if (eps != null)
            {
                foreach (var ep in eps)
                {
                    var url = ep.Url;
                    if (all.ContainsKey(url))
                        throw new Exception("Can't add the url \"" + url + "\" more than once!");
                    var isPng = url.EndsWith(".png");
                    if ((!isPng) && (!url.EndsWith(".svg")))
                        throw new Exception("Url's must end with .png, found \"" + url + "\"!");
                    var w = new QrEndPoint(ep, isPng, PerfMon);
                    all.Add(url, w);
                    var path = url.Split('/');
                    var pl = path.Length;
                    String pkey = "";
                    String nextKey = "";
                    for (int j = 0; j < pl; ++j, pkey = nextKey)
                    {
                        var nextPart = path[j];
                        nextKey = pkey.Length == 0 ? nextPart : String.Join('/', pkey, nextPart);
                        var key = pkey.Length == 0 ? "" : (pkey + "/");
                        bool havePath = folders.TryGetValue(key, out var f);
                        if (!havePath)
                        {
                            f = new List<IHttpServerEndPoint>();
                            folders.Add(key, f);
                        }
                        if ((j + 1) == pl)
                        {
                            f.Add(w);
                            continue;
                        }
                        if (havePath)
                            havePath = f.FirstOrDefault(x => x.GetType() == typeof(HttpServerEndPoint)) != null;
                        if (havePath)
                            continue;
                        f.Add(new HttpServerEndPoint(nextKey, "[Implicit Folder] from " + nameof(QrCodeService), HttpServerTools.StartedTime));
                    }
                }
            }
            OnlyForPrefixes = all.Keys.ToArray();
            AllEndpoints = all.Freeze();
            FolderEndPoints = folders.Freeze();
            ResponseComp = HttpCompressionPriority.GetSupportedEncoders(p.ResponseCompression);
            ResponseAuth = Authorization.GetRequiredTokens(p.ResponseAuth);

        }

        public String[] OnlyForPrefixes { get; init; }

        internal readonly IReadOnlyList<string> ResponseAuth;

        internal readonly HttpCompressionPriority ResponseComp;

        readonly IReadOnlyDictionary<String, QrEndPoint> AllEndpoints;
        readonly IReadOnlyDictionary<String, List<IHttpServerEndPoint>> FolderEndPoints;

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(QrCodeService));

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            if (root == null)
                return AllEndpoints.Values;
            if (!FolderEndPoints.TryGetValue(root, out var eps))
                return HttpServerTools.NoEndPoints;
            return eps;
        }

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (!AllEndpoints.TryGetValue(context.LocalUrl, out var ep))
                return null;
            return ep;
        }


        /// <summary>
        /// Return a QR code as svg data from the given payload data
        /// </summary>
        /// <param name="data">The payload data</param>
        /// <returns>Svg data with the encoded payload data</returns>
        [WebApi("CreateQrCode.svg")]
        [WebApiAuth(Roles.Debug)]
        [WebApiRaw(HttpServerTools.SvgMime)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public ReadOnlyMemory<Byte> CreateQrCodeSvg(String data)
        {
            using var p = PerfMon.Track(nameof(CreateQrCode));
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(String.IsNullOrEmpty(data) ? "https://www.google.com" : data, QRCodeGenerator.ECCLevel.Q);
            return Encoding.UTF8.GetBytes(QrCodeTools.GetSvgPath(qrCodeData));
        }


        /// <summary>
        /// Return a QR code as svg data from the given payload data
        /// </summary>
        /// <param name="data">The payload data</param>
        /// <returns>Svg data with the encoded payload data</returns>
        public String CreateQrCode(String data)
        {
            using var p = PerfMon.Track(nameof(CreateQrCode));
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            return QrCodeTools.GetSvgPath(qrCodeData);
        }

        /// <summary>
        /// Return a QR code as svg data from the given payload data
        /// </summary>
        /// <param name="data">The payload data</param>
        /// <param name="bright">A HTML color to use as the bright color (background)</param>
        /// <param name="dark">A HTML color to use as the dark color ("dots")</param>
        /// <param name="safeArea">If true a safe area of 4 cells using the background is included</param>
        /// <returns>Svg data with the encoded payload data</returns>
        public String CreateQrCode(String data, String bright, String dark = "#000", bool safeArea = true)
        {
            using var p = PerfMon.Track(nameof(CreateQrCode));
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            return QrCodeTools.GetSvgPath(qrCodeData, bright, dark, safeArea);
        }

        /// <summary>
        /// Create a request handler that will return the specified QR code
        /// </summary>
        /// <param name="data">The payload data</param>
        /// <param name="bright">A HTML color to use as the bright color (background)</param>
        /// <param name="dark">A HTML color to use as the dark color ("dots")</param>
        /// <param name="safeArea">If true a safe area of 4 cells using the background is included</param>
        /// <returns>A request handler that could be returned</returns>
        public IHttpRequestHandler CreateResponse(String data, String bright = "#fff", String dark = "#000", bool safeArea = true)
        {
            using var p = PerfMon.Track(nameof(CreateResponse));
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            var svg = QrCodeTools.GetSvgPath(qrCodeData, bright, dark, safeArea);
            return new QrResponseHandler(this, svg);
        }



    }


}
