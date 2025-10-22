using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Net;



namespace SysWeaver.MicroService
{
    internal sealed class QrEndPoint : IHttpServerEndPoint, IHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public QrEndPoint(QrCodeEndPoint ep, bool isPng, PerfMonitor perf)
        {
            Uri = ep.Url;
            Auth = Authorization.GetRequiredTokens(ep.Auth);
            IsPng = isPng;
            DrawQuite = ep.DrawQuite;
            Mime = isPng ? "image/png" : "image/svg+xml; charset=UTF-8";
            CompPreference = isPng ? null : ep.Compression;
            Compression = isPng ? null : HttpCompressionPriority.GetSupportedEncoders(ep.Compression);
            QrSize = new System.Drawing.Size(ep.Size, ep.Size);
            QrDark = System.Drawing.Color.FromArgb(ep.DarkColor | unchecked((int)0xff000000U));
            QrBright = System.Drawing.Color.FromArgb(ep.BrightColor | unchecked((int)0xff000000U));
            WebQrDark = QrCodeTools.GetWebColor(QrDark);
            WebQrBright = QrCodeTools.GetWebColor(QrBright);
            Perf = perf;
        }
        readonly PerfMonitor Perf;
        readonly bool IsPng;
        readonly bool DrawQuite;
        readonly System.Drawing.Size QrSize;
        readonly System.Drawing.Color QrDark;
        readonly System.Drawing.Color QrBright;
        readonly String WebQrDark;
        readonly String WebQrBright;

        public string Uri { get; init; }

        public string Method => "GET";

        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;

        public int ClientCacheDuration => WebApiTools.CacheClientStatic;

        public int RequestCacheDuration => 30 * 60;

        public bool UseStream => false;

        public string CompPreference { get; init; }

        public string PreCompressed => null;

        public IReadOnlyList<string> Auth { get; init; }

        public string Location => nameof(QrCodeService);

        public long? Size => null;

        public DateTime LastModified => HttpServerTools.StartedTime;

        public string Mime { get; init; }

        public HttpCompressionPriority Compression { get; init; }

        public ICompDecoder Decoder => null;


        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return HttpServerTools.StartedText;
        }

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            using var p = Perf.Track(Uri);
            var data = HttpUtility.UrlDecode(request.Url.Substring(request.QueryStringStart));
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            var height = qrCodeData.ModuleMatrix.Count;
            var width = qrCodeData.ModuleMatrix[0].Count;
            if (height > width)
                width = height;
            var drawQuite = DrawQuite;
            if (!drawQuite)
                width -= 8;
            var dim = QrSize.Width / width;
            Byte[] resData;
            if (IsPng)
            {
                using var qrCode = new PngByteQRCode(qrCodeData);
                if (dim < 1)
                    dim = 1;
                resData = qrCode.GetGraphic(dim, QrDark, QrBright, drawQuite);
            }
            else
            {
                var qrCodeAsSvg = QrCodeTools.GetSvgPath(qrCodeData, WebQrBright, WebQrDark, drawQuite);
                //using SvgQRCode qrCode = new SvgQRCode(qrCodeData);
                //var qrCodeAsSvg = qrCode.GetGraphic(1, QrDark, QrBright, drawQuite, SvgQRCode.SizingMode.ViewBoxAttribute);
                resData = Encoding.UTF8.GetBytes(qrCodeAsSvg);
            }
            request.SetResMime(Mime);
            return resData;
        }



        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
    }

}
