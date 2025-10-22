using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            Browser = manager.Get<IBrowserService>();
            PngMime = MimeTypeMap.GetMimeType("png");
            Options = new RequestOptions(p.ClientCacheDuration, p.RequestCacheDuration, 0, null, p.Auth);
            MethodAuths = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                { "*", p.Auth },
            }.Freeze();
            MaxScreenShotLock = new AsyncLock(Math.Max(1, p.MaxConcurrency));
        }
        
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
                var d = await GetAdaptivePngAsync(src, width, height).ConfigureAwait(false);
                return d.Item1.AsMemory();
            }, Options);
        }

        public async Task<Byte[]> GetPngAsync(String url, int width = 1920, int height = 1080)
        {
            using var _ = await MaxScreenShotLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(GetPngAsync));
            using var b = await Browser.OpenWindow().ConfigureAwait(false);
            await b.Resize(width, height).ConfigureAwait(false);
            await b.LoadUrl(url).ConfigureAwait(false);
            await b.WaitLoaded().ConfigureAwait(false);
            return await b.CapturePng().ConfigureAwait(false);
        }

        const String ServiceName = "ThumbnailWeb";

        const String Prefix = "[" + ServiceName + "] ";

        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(ServiceName);

        static long PC;


        long JsInControlCount;
        long AdaptCount;

        public async Task<Tuple<Byte[], MediaInfo>> GetAdaptivePngAsync(String url, int initWidth = 1920, int initHeight = 1080)
        {
            String prefix = "[ThumbnailWeb " + Interlocked.Increment(ref PC) + "] ";
            using var _ = await MaxScreenShotLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(GetAdaptivePngAsync));
            M.AddMessage(prefix + "Creating browser", MessageLevels.Debug);
            using var b = await Browser.OpenWindow().ConfigureAwait(false);
            using var a = new AdaptiveSize(M, prefix);
            await b.AddJsObject("ScreenShotHost", a).ConfigureAwait(false);
            M.AddMessage(prefix + "Resizing window to " + initWidth + "x" + initHeight, MessageLevels.Debug);
            await b.Resize(initWidth, initHeight).ConfigureAwait(false);
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
                data = await b.CapturePng().ConfigureAwait(false);
            }
            M.AddMessage(prefix + "All done", MessageLevels.Debug);
            return Tuple.Create(data, n);
        }


        static readonly ITextSerializerType JsonSer = SerManager.GetText("json");


        static String InternalGetMediaUrl(GetMediaRequest r, HttpServerRequest context)
        {
            var d = r.Params;
            String e = "";
            if (d != null)
                e = String.Concat("&props=", Uri.EscapeDataString(JsonSer.ToString(d)));
            return String.Concat(context.Prefix, "../mediaView/MediaPreview.html?type=", r.Type, "&link=", Uri.EscapeDataString(r.Url), "&pos=", r.Pos.ToString(CultureInfo.InvariantCulture), e);
        }

        async Task<GetPngResponse> InternalGetMedia(GetMediaRequest r, HttpServerRequest context)
        {
            var url = InternalGetMediaUrl(r, context);
            var x = await GetAdaptivePngAsync(url, r.Width, r.Height).ConfigureAwait(false);
            if (x.Item2?.Desc.FastEquals("CORS") ?? false)
            {
                var h = await GetProxiedUrl(r.Url).ConfigureAwait(false);
                if (h != null)
                {
                    r.Url = "../" + h.Uri;
                    url = InternalGetMediaUrl(r, context);
                    x = await GetAdaptivePngAsync(url, r.Width, r.Height).ConfigureAwait(false);
                }
            }
            return new GetPngResponse
            {
                Png = x.Item1,
                Info = x.Item2,
            };
        }

        async Task<ReadOnlyMemory<Byte>> InternalGetMediaPng(GetMediaRequest r, HttpServerRequest context)
        {
            var url = InternalGetMediaUrl(r, context);
            var x = await GetAdaptivePngAsync(url, r.Width, r.Height).ConfigureAwait(false);
            if (x.Item2?.Desc.FastEquals("CORS") ?? false)
            {
                var h = await GetProxiedUrl(r.Url).ConfigureAwait(false);
                if (h != null)
                {
                    r.Url = "../" + h.Uri;
                    url = InternalGetMediaUrl(r, context);
                    x = await GetAdaptivePngAsync(url, r.Width, r.Height).ConfigureAwait(false);
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
        [WebApiRequestCache(25)]
        public Task<GetPngResponse> GetMediaImage(GetMediaImageRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an image url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
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
        [WebApiRequestCache(25)]
        public Task<GetPngResponse> GetMediaVideo(GetMediaVideoRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a video url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>        
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
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
        [WebApiRequestCache(25)]
        public Task<GetPngResponse> GetMediaEffect(GetMediaEffectRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an effect url
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>        
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
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
        [WebApiRequestCache(25)]
        public Task<GetPngResponse> GetMediaYouTube(GetMediaYouTubeRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a YouTube code
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>            
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaYouTube) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaYouTube(GetMediaYouTubeRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media YouTube

        /// <summary>
        /// Get png and data for a given url
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(5)]
        [WebApiRequestCache(3)]
        public async Task<GetPngResponse> GetPng(GetPngRequest r)
        {
            if (r.Control)
            {
                var x = await GetAdaptivePngAsync(r.Url, r.Width, r.Height).ConfigureAwait(false);
                return new GetPngResponse
                {
                    Png = x.Item1,
                    Info = x.Item2,
                };
            }
            var y = await GetPngAsync(r.Url, r.Width, r.Height).ConfigureAwait(false);
            return new GetPngResponse
            {
                Png = y,
            };
        }

        /// <summary>
        /// Get a png (screenshot) from an url
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>Response</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(WebScreenshot) + ".png")]
        public async Task<ReadOnlyMemory<Byte>> WebScreenshot(GetPngRequest r)
            => (await GetPng(r).ConfigureAwait(false)).Png;

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
            GetPngResponse res = null;
            long tot = 0;
            for (int i = 0; i < count; ++i)
            {
                String flag = flags[i % fl];
                var p = new GetPngRequest
                {
                    Control = true,
                    Width = 320,
                    Height = 180,
                    //Url = "https://www.quizzweaver.com/quizz/MediaPreview.html?type=3&link=https://www.quizzweaver.com/icons/flags/" + flag + ".svg",
                    Url = "https://www.quizzweaver.com/quizz/MediaPreview.html?type=9&link=https://www.quizzweaver.com/Media/Stock/StockMedia/Effects/" + flag + ".glsl",
                };
                res = await GetPng(p).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(f, String.Join(i.ToString().PadLeft(4, '0'), nameof(StressTestSerial), ".png")), res.Png).ConfigureAwait(false);
                tot += res.Png.Length;
            }

            M.AddMessage(Prefix + "Took: " + (DateTime.UtcNow - start) + ", data len: " + tot);
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Png);
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
            GetPngResponse res = null;
            Task[] tasks = new Task[count];
            long tot = 0;
            for (int i = 0; i < count; ++i)
            {
                String flag = flags[i % fl];
                var p = new GetPngRequest
                {
                    Control = true,
                    Width = 320,
                    Height = 180,
                    //Url = "https://www.quizzweaver.com/quizz/MediaPreview.html?type=3&link=https://www.quizzweaver.com/icons/flags/" + flag + ".svg",
                    Url = "https://www.quizzweaver.com/quizz/MediaPreview.html?type=9&link=https://www.quizzweaver.com/Media/Stock/StockMedia/Effects/" + flag + ".glsl",
                };
                var ii = i + 1;
                async Task DoOne()
                {
                    var r = await GetPng(p).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(Path.Combine(f, String.Join((ii - 1).ToString().PadLeft(4, '0'), nameof(StressTestParalell), ".png")), r.Png).ConfigureAwait(false);
                    Interlocked.Add(ref tot, r.Png.Length);
                    if (ii == count)
                        res = r;
                }
                tasks[i] = DoOne();
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            M.AddMessage(Prefix + "Took: " + (DateTime.UtcNow - start) + ", data len: " + tot);
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Png);
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
