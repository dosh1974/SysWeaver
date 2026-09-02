using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Media;
using SysWeaver.Net;
using SysWeaver.Serialization;
using SysWeaver.WebBrowser;

namespace SysWeaver.MicroService
{



    /// <summary>
    /// </summary>
    [IsMicroService]
    [RequiredDep(typeof(IBrowserService))]
    [WebApiUrl("Thumbnail")]
    public sealed partial class ThumbnailWebService :  IHttpServerModule, IPerfMonitored, IRunTimeWebApiAuth, IThumbnailWebService, IHaveStats
    {

        public ThumbnailWebService(ServiceManager manager, ThumbnailWebParams p = null)
        {
            M = manager;
            p = p ?? new ThumbnailWebParams();
            var gmk = p.GoogleMapsKey?.GetApiKey(false);
            GoogleMapsKey = gmk;
            if (gmk == null)
                manager.AddMessage("No Google Maps key supplied, Google maps image creation will not work!", MessageLevels.Debug);
            Browser = manager.Get<IBrowserService>();
            PngMime = MimeTypeMap.GetMimeType("png");
            Options = new RequestOptions(p.ClientCacheDuration, p.RequestCacheDuration, 0, null, p.Auth);
            MethodAuths = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                { "*", p.Auth },
            }.Freeze();
            MaxScreenShotLock = new AsyncLock(Math.Max(1, p.MaxConcurrency));
        }

        public String GetPrefix(HttpServerRequest context, bool throwOnFail = true)
        {
            if (context != null)
                return context.Prefix;
            var s = HttpServer;
            if (s == null)
            {
                s = M.TryGet<HttpServerBase>();
                HttpServer = s;
            }
            var prefix = s?.AllPrefixes?.FirstOrDefault()?.Replace("*", "127.0.0.1");
            if ((prefix == null) && throwOnFail)
                throw new Exception("No http prefix found!");
            return prefix;
        }

        HttpServerBase HttpServer;


        readonly String GoogleMapsKey;
        
        public async Task<StaticMemoryHttpRequestHandler> GetProxiedUrl(String url)
        {
            var ext = url.Substring(url.LastIndexOf('.'));
            String newUrl;
            using (var rng = SecureRng.Get())
                newUrl = rng.GetGuid24();
            newUrl = newUrl + ext;

            var c = CorsProxy;
            var data = await c.GetOrUpdateAsync(newUrl, async x =>
            {
                var c = WebTools.HttpClient;
                try
                {
                    var d = await c.GetByteArrayAsync(url).ConfigureAwait(false);
                    var m = MimeTypeMap.GetMimeType(ext);
                    return new StaticMemoryHttpRequestHandler(
                        String.Concat("thumbnailWebProxy/", newUrl),
                        "Proxy",
                        d,
                        m.Item1,
                        null);
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(false);
            return data == null ? null : data;
        }



        readonly FastMemCache<String, StaticMemoryHttpRequestHandler> CorsProxy = new (TimeSpan.FromSeconds(60), StringComparer.Ordinal);


        public void Dispose()
        {
        }

        readonly ServiceManager M;

        public IReadOnlyDictionary<String, String> MethodAuths { get; init; }

        readonly AsyncLock MaxScreenShotLock;

        readonly RequestOptions Options;
        readonly Tuple<string, bool> PngMime;
        readonly IBrowserService Browser;

        public String[] OnlyForPrefixes { get; } = ["thumbnailWeb/", "thumbnailWebProxy/"];

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var url = context.LocalUrl;
            /*            if (!url.FastStartsWith("thumbnailWeb/"))
                        {
                            if (!url.FastStartsWith("thumbnailWebProxy/"))
                               return null;
                            CorsProxy.TryGet(url.Substring(18), out var data);
                            return data;
                        }
            */
            if (url[12] != '/')
            {
                CorsProxy.TryGet(url.Substring(18), out var data);
                return data;
            }
            var name = url.Substring(13);
            var ext = name.Split('.');
            if (ext.Length != 2)
                return null;
            if (ext[1] != "png")
                return null;
            var rname = ext[0];
            var src = HttpUtility.UrlDecode(context.Url.Substring(context.QueryStringStart));
            var sl = src.Length;
            if (sl <= 0)
                return null;
            var size = rname.Split('x');
            if (size.Length != 2)
                return null;
            if (!int.TryParse(size[0], out var width))
                return null;
            if (!int.TryParse(size[1], out var height))
                return null;
            if (width <= 0)
                return null;
            if (height <= 0)
                return null;
            return new DynamicDataHttpRequestHandler(PngMime, async r =>
            {
                var d = await GetAdaptiveImageAsync(src, width, height).ConfigureAwait(false);
                return d.Item1.AsMemory();
            }, Options);
        }

        public async Task<Byte[]> GetImageAsync(String url, int width = 1920, int height = 1080, double scale = 1, ScreenshotImageFormats format = ScreenshotImageFormats.Png, int quality = 70, bool optimizeForSpeed = false, int extraDelayMs = 0)
        {
            using var _ = await MaxScreenShotLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(GetImageAsync));
            using var b = await Browser.OpenWindow().ConfigureAwait(false);
            await b.Resize(width, height, scale).ConfigureAwait(false);
            await b.LoadUrl(url).ConfigureAwait(false);
            await b.WaitLoaded().ConfigureAwait(false);
            if (extraDelayMs > 0)
                await Task.Delay(extraDelayMs).ConfigureAwait(false);
            switch (format)
            {
                default:
                    return await b.CapturePng(optimizeForSpeed).ConfigureAwait(false);
                case ScreenshotImageFormats.Jpg:
                    return await b.CaptureJpeg(quality, optimizeForSpeed).ConfigureAwait(false);
            }
        }

        const String ServiceName = "ThumbnailWeb";

        const String Prefix = "[" + ServiceName + "] ";

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(ServiceName);

        static long PC;


        long JsInControlCount;
        long AdaptCount;


        [ClassInterface(ClassInterfaceType.AutoDual)]
        [ComVisible(true)]

        public sealed class TextResult
        {

            public Task SetText(string text)
            {
                Text = text;
                return Task.CompletedTask;
            }

            internal String Text;

        }

        public async Task<Tuple<Byte[], MediaInfo>> GetAdaptiveImageAsync(String url, int initWidth = 1920, int initHeight = 1080, double scale = 1, ScreenshotImageFormats format = ScreenshotImageFormats.Png, int quality = 70, bool optimizeForSpeed = false, int extraDelayMs = 0)
        {
            String prefix = "[ThumbnailWeb " + Interlocked.Increment(ref PC) + "] ";
            using var _ = await MaxScreenShotLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(GetAdaptiveImageAsync));
            M.AddMessage(prefix + "Creating browser", MessageLevels.Debug);
            using var b = await Browser.OpenWindow().ConfigureAwait(false);
            using var a = new AdaptiveSize(M, prefix, format, quality, optimizeForSpeed, extraDelayMs);
            await b.AddJsObject("ScreenShotHost", a).ConfigureAwait(false);
            var error = new TextResult();
            await b.AddJsObject("ErrorText", error).ConfigureAwait(false);
            M.AddMessage(prefix + "Resizing window to " + initWidth + "x" + initHeight, MessageLevels.Debug);
            await b.Resize(initWidth, initHeight, scale).ConfigureAwait(false);
            //M.AddMessage(prefix + "Wait for resize to take effect");
            //await Task.Delay(100).ConfigureAwait(false);
            M.AddMessage(prefix + "Loading url " + url.ToQuoted(), MessageLevels.Debug);
            a.Win = b;
            await b.LoadUrl(url).ConfigureAwait(false);
            await b.WaitLoaded().ConfigureAwait(false);
            Byte[] data = null;
            MediaInfo n = new MediaInfo
            {
                Width = initWidth,
                Height = initHeight,
                Desc = url,
            };
            Interlocked.Increment(ref AdaptCount);
            if (a.JsIsInControl)
            {
                Interlocked.Increment(ref JsInControlCount);
                M.AddMessage(prefix + "Page is aware, wait for page to take the screen shot", MessageLevels.Debug);
                a.AllowJsControl();
                data = await a.WaitScreenShot(10000).ConfigureAwait(false);
                if (data != null)
                {
                    n.Width = b.Width;
                    n.Height = b.Height;
                    n.Duration = a.Duration;
                    n.Fps = a.Fps;
                }
                if (a.Error != null)
                {
                    n.Desc = "CORS";
                    M.AddMessage(prefix + "Got an error: " + a.Error, MessageLevels.Debug);
                    return Tuple.Create(data, n);
                }
            }
            if (data == null)
            {
                M.AddMessage(prefix + "Taking screen shot", MessageLevels.Debug);
                switch (format)
                {
                    default:
                        data = await b.CapturePng(optimizeForSpeed).ConfigureAwait(false);
                        break;
                    case ScreenshotImageFormats.Jpg:
                        data = await b.CaptureJpeg(quality, optimizeForSpeed).ConfigureAwait(false);
                        break;
                }
            }
            var et = error.Text;
            if (!String.IsNullOrEmpty(et))
            {
                M.AddMessage(prefix + "Failed: " + et, MessageLevels.Warning);
                throw new Exception(et);
            }
            M.AddMessage(prefix + "All done", MessageLevels.Debug);
            return Tuple.Create(data, n);
        }


        static readonly ITextSerializerType JsonSer = SerManager.GetText("json");


        String InternalGetMediaUrl(GetMediaRequest r, HttpServerRequest context)
        {
            var d = r.Params;
            String e = "";
            if (d != null)
                e = String.Concat("&props=", Uri.EscapeDataString(JsonSer.ToString(d)));
            return String.Concat(GetPrefix(context), "mediaView/MediaPreview.html?type=", r.Type, "&link=", Uri.EscapeDataString(r.Url), "&pos=", r.Pos.ToString(CultureInfo.InvariantCulture), e);
        }

        async Task<ScreenshotImageResponse> InternalGetMedia(GetMediaRequest r, HttpServerRequest context, ScreenshotImageFormats format = ScreenshotImageFormats.Png, int quality = 70)
        {
            var url = InternalGetMediaUrl(r, context);
            var x = await GetAdaptiveImageAsync(url, r.Width, r.Height, 1, format, quality, false, r.ExtraDelayMs).ConfigureAwait(false);
            if (x.Item2?.Desc.FastEquals("CORS") ?? false)
            {
                var h = await GetProxiedUrl(r.Url).ConfigureAwait(false);
                if (h != null)
                {
                    r.Url = "../" + h.Uri;
                    url = InternalGetMediaUrl(r, context);
                    x = await GetAdaptiveImageAsync(url, r.Width, r.Height, 1, format, quality, false, r.ExtraDelayMs).ConfigureAwait(false);
                }
            }
            return new ScreenshotImageResponse
            {
                Data = x.Item1,
                Info = x.Item2,
            };
        }

        async Task<ReadOnlyMemory<Byte>> InternalGetMediaPng(GetMediaRequest r, HttpServerRequest context)
        {
            var url = InternalGetMediaUrl(r, context);
            var x = await GetAdaptiveImageAsync(url, r.Width, r.Height, 1, ScreenshotImageFormats.Png, 70, false, r.ExtraDelayMs).ConfigureAwait(false);
            if (x.Item2?.Desc.FastEquals("CORS") ?? false)
            {
                var h = await GetProxiedUrl(r.Url).ConfigureAwait(false);
                if (h != null)
                {
                    r.Url = "../" + h.Uri;
                    url = InternalGetMediaUrl(r, context);
                    x = await GetAdaptiveImageAsync(url, r.Width, r.Height, 1, ScreenshotImageFormats.Png, 70, false, r.ExtraDelayMs).ConfigureAwait(false);
                }
            }
            return x.Item1;
        }


        #region Media Image

        /// <summary>
        /// Get data from an image url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaImage(GetMediaImageRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an image url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaImage) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaImage(GetMediaImageRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Image

        #region Media Video

        /// <summary>
        /// Get data from a video url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaVideo(GetMediaVideoRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a video url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>        
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaVideo) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaVideo(GetMediaVideoRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Video

        #region Media Effect

        /// <summary>
        /// Get data from an effect url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaEffect(GetMediaEffectRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an effect url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>        
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaEffect) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaEffect(GetMediaEffectRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Effect

        #region Media YouTube

        /// <summary>
        /// Get data from a YouTube code
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaYouTube(GetMediaYouTubeRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a YouTube code
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>            
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaYouTube) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaYouTube(GetMediaYouTubeRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media YouTube


        #region Media Map
          
        static readonly String[] GoogleMapTypeNames =
            [
                "satellite",
                "roadmap",
                "dark",
            ];

        /// <summary>
        /// Get a png from a google map position
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>            
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi("GetGoogleMap.png")]
        public Task<ReadOnlyMemory<Byte>> GetGoogleMapPng(GetGoogleMapRequest r, HttpServerRequest context)
            => InternalGetGoogleMap(r, ScreenshotImageFormats.Png, 100, context);


        /// <summary>
        /// Get a jpg from a google map position
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>            
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/jpeg", true)]
        [WebApi("GetGoogleMap.jpg")]
        public Task<ReadOnlyMemory<Byte>> GetGoogleMapJpg(GetGoogleMapJpegRequest r, HttpServerRequest context)
            => InternalGetGoogleMap(r, ScreenshotImageFormats.Jpg, r.Quality, context);


        async Task<ReadOnlyMemory<Byte>> InternalGetGoogleMap(GetGoogleMapRequest r, ScreenshotImageFormats format, int quality, HttpServerRequest context)
        {
            var key = GoogleMapsKey;
            if (String.IsNullOrEmpty(key))
                throw new Exception("Must supply a google maps key for embedded maps in the parameters!");
            var center = r.Center.Replace(" ", "");
            if (center.IndexOf(',') < 0)
                throw new Exception("Invalid center, expecting lattitude, longitude");
            var zoom = r.Zoom;
            if ((zoom < 0) || (zoom > 20))
                zoom = 15;
            var lang = IsoData.IsoLanguage.TryGetName(r.Language)?.Iso639_1 ?? "en";
            var dpi = r.Dpi;
            if (dpi < 25)
                dpi = 25;
            if (dpi > 400)
                dpi = 400;
            int width = r.Width;
            if (width < 64)
                width = 64;
            if (width > 16384)
                width = 16384;

            int height = r.Height;
            if (height < 64)
                height = 64;
            if (height > 16384)
                height = 16384;

            String url = String.Concat(GetPrefix(context),
                "mediaView/MapView.html?key=", key,
                "&center=", Uri.EscapeDataString(center),
                "&zoom=", zoom,
                "&language=", lang,
                "&maptype=", GoogleMapTypeNames[(int)r.Type],
                "&dpi=", dpi.ToString(CultureInfo.InvariantCulture),
                "");

            var p = r.Pin?.Trim();
            if (!String.IsNullOrEmpty(p))
            {
                url = String.Concat(url, "&pin=", p);
                p = r.PinX?.Trim();
                if (!String.IsNullOrEmpty(p))
                    url = String.Concat(url, "&pinX=", p);
                p = r.PinY?.Trim();
                if (!String.IsNullOrEmpty(p))
                    url = String.Concat(url, "&pinY=", p);
                var h = r.PinHeight;
                if (h > 0)
                {
                    if (h > 512)
                        h = 512;
                    url = String.Concat(url, "&pinHeight=", h.ToString(CultureInfo.InvariantCulture));
                }
            }
            var res = await GetAdaptiveImageAsync(url, width, height, 1, format, quality, r.OptimizeForSpeed, r.ExtraDelayMs).ConfigureAwait(false);
            //            var res = await GetAdaptivePngAsync(url, width, height, dpi / 100.0).ConfigureAwait(false);
            return res.Item1;
        }

        #endregion//Media Map

        /// <summary>
        /// Get an image and meta data for a given url
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4, WebApiCaches.Globally)]
        [WebApiCompression("")]
        [WebApiAuth(Roles.Service)]
        public async Task<ScreenshotImageResponse> GetImage(ScreenshotImageRequest r)
        {
            if (r.Control)
            {
                var x = await GetAdaptiveImageAsync(r.Url, r.Width, r.Height, r.Scale, r.Format, r.Quality, r.OptimizeForSpeed, r.ExtraDelayMs).ConfigureAwait(false);
                return new ScreenshotImageResponse
                {
                    Data = x.Item1,
                    Info = x.Item2,
                };
            }
            var y = await GetImageAsync(r.Url, r.Width, r.Height, r.Scale, r.Format, r.Quality, r.OptimizeForSpeed, r.ExtraDelayMs).ConfigureAwait(false);
            return new ScreenshotImageResponse
            {
                Data = y,
            };
        }

        /// <summary>
        /// Get an image (screenshot) from an url
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>Response</returns>
        [WebApiClientCache(30)] 
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi("WebScreenshot.png")]
        public async Task<ReadOnlyMemory<Byte>> WebScreenshotPng(ScreenshotPngRequest r)
            => (await GetImage(ScreenshotImageRequest.From(r)).ConfigureAwait(false)).Data;

        /// <summary>
        /// Get a jpeg (screenshot) from an url
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>Response</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/jpeg", true)]
        [WebApi("WebScreenshot.jpg")]
        public async Task<ReadOnlyMemory<Byte>> WebScreenshotJpg(ScreenshotJpegRequest r)
            => (await GetImage(ScreenshotImageRequest.From(r)).ConfigureAwait(false)).Data;


        static readonly String[] Flags = [
            "ad", "ae", "af", "ag", "ai", "al", "am", "ao", "aq",
            "ar", "arab", "as", "at", "au", "aw", "ax", "az", "ba",
            "bb", "bd", "be", "bf", "bg", "bh", "bi", "bj", "bl",
            "bm", "bn", "bo", "bq", "br", "bs", "bt", "bv", "bw",
            "by", "bz", "ca", "cc", "cd", "cefta", "cf", "cg", "ch",
            "ci", "ck", "cl", "cm", "cn", "co", "cp", "cr", "cu",
            "cv", "cw", "cx", "cy", "cz", "de", "dg", "dj", "dk",
            "dm", "do", "dz", "eac", "ec", "ee", "eg", "eh", "er",
            "es", "es-ct", "es-ga", "es-pv", "et", "eu", "fi", "fj", "fk",
            "fm", "fo", "fr", "ga", "gb", "gb-eng", "gb-nir", "gb-sct", "gb-wls",
            "gd", "ge", "gf", "gg", "gh", "gi", "gl", "gm", "gn",
            "gp", "gq", "gr", "gs", "gt", "gu", "gw", "gy", "hk",
            "hm", "hn", "hr", "ht", "hu", "ic", "id", "ie", "il",
            "im", "in", "io", "iq", "ir", "is", "it", "je", "jm",
            "jo", "jp", "ke", "kg", "kh", "ki", "km", "kn", "kp",
            "kr", "kw", "ky", "kz", "la", "lb", "lc", "li", "lk",
            "lr", "ls", "lt", "lu", "lv", "ly", "ma", "mc", "md",
            "me", "mf", "mg", "mh", "mk", "ml", "mm", "mn", "mo",
            "mp", "mq", "mr", "ms", "mt", "mu", "mv", "mw", "mx",
            "my", "mz", "na", "nc", "ne", "nf", "ng", "ni", "nl",
            "no", "np", "nr", "nu", "nz", "om", "pa", "pc", "pe",
            "pf", "pg", "ph", "pk", "pl", "pm", "pn", "pr", "ps",
            "pt", "pw", "py", "qa", "re", "ro", "rs", "ru", "rw",
            "sa", "sb", "sc", "sd", "se", "sg", "sh", "sh-ac", "sh-hl",
            "sh-ta", "si", "sj", "sk", "sl", "sm", "sn", "so", "sr",
            "ss", "st", "sv", "sx", "sy", "sz", "tc", "td", "tf",
            "tg", "th", "tj", "tk", "tl", "tm", "tn", "to", "tr",
            "tt", "tv", "tw", "tz", "ua", "ug", "um", "un", "us",
            "uy", "uz", "va", "vc", "ve", "vg", "vi", "vn", "vu",
            "wf", "ws", "xk", "xx", "ye", "yt", "za", "zm", "zw",
        ];

        static readonly String[] Effects = [
            "_SpeedTest", "2D clouds", "3D dot zoom", "3D flag", "3D maze lattice", "3D tile map", "3D Truchet pattern zoom", "60s",
            "Abstract box scape", "Alien nursery", "Alien underwater base", "Analog clock", "Animated cube scape", "Animated grafitti", "Anti gravity", "Aurora",
            "Blobby fractal", "Blue blobs", "Blue lines", "Blue waves", "Bokeh", "Bouncing light balls", "BPM machine", "Bubbles",
            "Bubbly cloth", "Cable tunnel", "Cartoon factory", "Cartoon mandela", "Cell", "Chase RGB", "Chase", "Chromatic blob",
            "Chrome fractal", "Chrome tiles", "Clock icon", "Closing doors", "Cloud fly through", "Cloud tunnel", "Clouds", "Color ring",
            "Color zoom", "Colorful octupus fractal", "Colorful pencils", "Coral worms", "Crazy squares", "Cube cave", "Cubes", "Dancing lights",
            "Deep space", "Digital clock", "Disco room", "Disco stars", "Disco", "Endless 3d grid", "Fractal grid", "Funky blobs",
            "Gears", "Gel pearls", "Ghost aurora", "Glass cave", "Glitter", "Glowing lines", "Gold dust", "Golden lines",
            "Green lines", "Green spiral", "Heart", "Hex core", "Hexgrid", "Hologram marble", "Hologram projection", "Knots",
            "Light rays", "Liquid cubes", "Mandela", "Mario", "Matrix", "Maze", "Menger sponge", "Metal plates",
            "Misty mountain", "Muscle", "Neon Caleidoscope", "Neon grid", "Neon hart", "Neon parallax", "Neural", "Night sky",
            "Noise contours", "Ocean", "Papercut landscape", "Planet surface", "Polka torus", "Red sea", "Retro clipbook", "Rotating color tiles",
            "Rotating tiles", "Rotating transparent discs", "Science", "Scientific UI", "Scrolling discs", "Sea of balls", "Shifting patterns", "Simple gears",
            "Simple line", "Sine lines", "Slow caustics", "Smiley", "Smooth bands", "Snow flakes", "Snow", "Soft bokeh",
            "Soft waves", "Space fractal", "Space", "Spiral balls", "Spiral circles", "Star tunnel", "Steel plasma", "Subdivision",
            "Sun flare", "Toon cloud", "Triangle landscape", "Truchet pattern", "Underwater", "Voroni gems", "Voroni rgb", "Voroni",
            "Voxel Pacman", "Wavy blobs", "White blob",
            ];

        [WebApi]
        [WebApiAuth(Roles.Debug)]
        public async Task<String> StressTestSerial(int count)
        {
            var f = TempFolder.Get("WebViewStressTest");
            var start = DateTime.UtcNow;
            //var flags = Flags;
            var flags = Effects;
            var fl = flags.Length;
            ScreenshotImageResponse res = null;
            long tot = 0;
            for (int i = 0; i < count; ++i)
            {
                String flag = flags[i % fl];
                var p = new ScreenshotImageRequest
                {
                    Control = true,
                    Width = 320,
                    Height = 180,
                };
                res = await GetImage(p).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(f, String.Join(i.ToString().PadLeft(4, '0'), nameof(StressTestSerial), ".png")), res.Data).ConfigureAwait(false);
                tot += res.Data.Length;
            }

            M.AddMessage(Prefix + "Took: " + (DateTime.UtcNow - start) + ", data len: " + tot);
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Data);
            return d;
        }

        [WebApi]
        [WebApiAuth(Roles.Debug)]
        public async Task<String> StressTestParalell(int count)
        {
            var f = TempFolder.Get("WebViewStressTest");
            var start = DateTime.UtcNow;
            //var flags = Flags;
            var flags = Effects;
            var fl = flags.Length;
            ScreenshotImageResponse res = null;
            Task[] tasks = new Task[count];
            long tot = 0;
            for (int i = 0; i < count; ++i)
            {
                String flag = flags[i % fl];
                var p = new ScreenshotImageRequest
                {
                    Control = true,
                    Width = 320,
                    Height = 180,
                };
                var ii = i + 1;
                async Task DoOne()
                {
                    var r = await GetImage(p).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(Path.Combine(f, String.Join((ii - 1).ToString().PadLeft(4, '0'), nameof(StressTestParalell), ".png")), r.Data).ConfigureAwait(false);
                    Interlocked.Add(ref tot, r.Data.Length);
                    if (ii == count)
                        res = r;
                }
                tasks[i] = DoOne();
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            M.AddMessage(Prefix + "Took: " + (DateTime.UtcNow - start) + ", data len: " + tot);
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Data);
            return d;
        }




        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        public IEnumerable<Stats> GetStats()
        {
            var j = Interlocked.Read(ref JsInControlCount);
            var a = Interlocked.Read(ref AdaptCount);
            yield return new Stats(ServiceName, "Js in control", j, "The number of times the js was in control of taking the screenshot");
            yield return new Stats(ServiceName, "Js not in control", a - j, "The number of times the js was NOT in control of taking the screenshot");
        }
    }



}
