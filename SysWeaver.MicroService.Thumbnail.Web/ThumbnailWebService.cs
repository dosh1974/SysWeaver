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
    /// Creates images (screenshots of web pages, media, effects and Google Maps) by rendering them in a browser window (<see cref="IBrowserService"/>)
    /// </summary>
    [IsMicroService]
    [RequiredDep(typeof(IBrowserService))]
    [WebApiUrl("Thumbnail")]
    public sealed partial class ThumbnailWebService :  IHttpServerModule, IPerfMonitored, IRunTimeWebApiAuth, IThumbnailWebService, IHaveStats
    {

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="manager">The service manager (an <see cref="IBrowserService"/> is required)</param>
        /// <param name="p">Parameters, null to use the defaults</param>
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
            var ae = String.IsNullOrWhiteSpace(p.AudioEffect) ? "Led_Spectrum" : p.AudioEffect.Trim();
            //  A name: an effect of mediaView/audioEffects, else an url / a path relative to the mediaView folder
            AudioEffect = ae.IndexOf('/') < 0 && !ae.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase) ? "audioEffects/" + ae + ".glsl" : ae;
        }

        /// <summary>The effect that draws a sound (relative to the mediaView folder, or an url), see <see cref="ThumbnailWebParams.AudioEffect"/></summary>
        readonly String AudioEffect;

        /// <summary>
        /// Get the http prefix to use when the browser should load a page from this server
        /// </summary>
        /// <param name="context">The request, null to use the first prefix of the http server (with "*" replaced by "127.0.0.1")</param>
        /// <param name="throwOnFail">True to throw if no prefix is found</param>
        /// <returns>The prefix, null if none was found (and throwOnFail is false)</returns>
        /// <exception cref="Exception">If no prefix was found and throwOnFail is true</exception>
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
        
        /// <summary>
        /// Get the file extension (including the '.') of the path of an url, ignoring the query and fragment
        /// </summary>
        /// <param name="url">The url</param>
        /// <returns>The extension, ex: ".png", or an empty string if the path has no extension</returns>
        static String GetUrlExtension(String url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var u))
                return Path.GetExtension(u.AbsolutePath);
            var e = url.AsSpan();
            var i = e.IndexOfAny('?', '#');
            if (i >= 0)
                e = e.Slice(0, i);
            return Path.GetExtension(e).ToString();
        }

        /// <summary>
        /// Download an url and make the data available (for 60 seconds) from a temporary url of this server (a CORS proxy)
        /// </summary>
        /// <param name="url">The url to download</param>
        /// <returns>The handler that serves the data (under "thumbnailWebProxy/"), null if the download failed</returns>
        public async Task<StaticMemoryHttpRequestHandler> GetProxiedUrl(String url)
        {
            var ext = GetUrlExtension(url);
            //  (hex: a base64 name has "/" and "+", a "+" in the path can come back as a space and the proxied file isn't found)
            var newUrl = Guid.NewGuid().ToString("N");
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


        /// <inheritdoc />
        public void Dispose()
        {
        }

        readonly ServiceManager M;

        /// <inheritdoc />
        public IReadOnlyDictionary<String, String> MethodAuths { get; init; }

        readonly AsyncLock MaxScreenShotLock;

        readonly RequestOptions Options;
        readonly Tuple<string, bool> PngMime;
        readonly IBrowserService Browser;

        /// <inheritdoc />
        public String[] OnlyForPrefixes { get; } = ["thumbnailWeb/", "thumbnailWebProxy/"];

        /// <inheritdoc />
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
            //  The query string is the source url (Url is already decoded), QueryStringStart is 0 if there is no query
            var qs = context.QueryStringStart;
            if (qs <= 0)
                return null;
            var src = context.Url.Substring(qs);
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
                return ThrowOnPageError(d).Item1.AsMemory();
            }, Options);
        }

        /// <summary>
        /// Take a screenshot of an url
        /// </summary>
        /// <param name="url">The url to load</param>
        /// <param name="width">The width of the browser window in pixels</param>
        /// <param name="height">The height of the browser window in pixels</param>
        /// <param name="scale">The scale (dpi scaling) of the window</param>
        /// <param name="format">The image format</param>
        /// <param name="quality">The jpeg quality (only used for jpeg)</param>
        /// <param name="optimizeForSpeed">True to optimize for speed rather than quality</param>
        /// <param name="extraDelayMs">An extra time to wait (in ms) after the page was loaded, before the screenshot is taken</param>
        /// <returns>The image data</returns>
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

        /// <inheritdoc />
        public PerfMonitor PerfMon { get; init; } = new PerfMonitor(ServiceName);

        static long PC;


        long JsInControlCount;
        long AdaptCount;


        /// <summary>
        /// Exposed to the page as "ErrorText": lets a page report an error text to the service while a screenshot is taken (a non empty text fails the screenshot)
        /// </summary>
        [ClassInterface(ClassInterfaceType.AutoDual)]
        [ComVisible(true)]

        public sealed class TextResult
        {

            /// <summary>
            /// Called by the page to set the text
            /// </summary>
            /// <param name="text">The error text (null or empty for no error)</param>
            /// <returns>A completed task</returns>
            public Task SetText(string text)
            {
                Text = text;
                return Task.CompletedTask;
            }

            internal String Text;

        }

        /// <summary>
        /// Take a screenshot of an url, if the page supports it (it is "aware") it controls the size of the window and when the screenshot is taken
        /// </summary>
        /// <param name="url">The url to load</param>
        /// <param name="initWidth">The initial width of the browser window in pixels</param>
        /// <param name="initHeight">The initial height of the browser window in pixels</param>
        /// <param name="scale">The scale (dpi scaling) of the window</param>
        /// <param name="format">The image format</param>
        /// <param name="quality">The jpeg quality (only used for jpeg)</param>
        /// <param name="optimizeForSpeed">True to optimize for speed rather than quality</param>
        /// <param name="extraDelayMs">An extra time to wait (in ms) before the screenshot is taken</param>
        /// <returns>The image data and information about the image (size, and for pages that are aware: duration, fps and load time)</returns>
        /// <exception cref="Exception">If the page reports an error text</exception>
        public async Task<Tuple<Byte[], MediaInfo>> GetAdaptiveImageAsync(String url, int initWidth = 1920, int initHeight = 1080, double scale = 1, ScreenshotImageFormats format = ScreenshotImageFormats.Png, int quality = 70, bool optimizeForSpeed = false, int extraDelayMs = 0)
        {
            String prefix = "[ThumbnailWeb " + Interlocked.Increment(ref PC) + "] ";
            using var _ = await MaxScreenShotLock.Lock().ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(GetAdaptiveImageAsync));
            M.AddMessage(MessageLevels.Debug, $"{prefix}Creating browser");
            using var b = await Browser.OpenWindow().ConfigureAwait(false);
            using var a = new AdaptiveSize(M, prefix, format, quality, optimizeForSpeed, extraDelayMs, scale);
            await b.AddJsObject("ScreenShotHost", a).ConfigureAwait(false);
            var error = new TextResult();
            await b.AddJsObject("ErrorText", error).ConfigureAwait(false);
            M.AddMessage(MessageLevels.Debug, $"{prefix}Resizing window to {initWidth}x{initHeight}");
            await b.Resize(initWidth, initHeight, scale).ConfigureAwait(false);
            //M.AddMessage(prefix + "Wait for resize to take effect");
            //await Task.Delay(100).ConfigureAwait(false);
            M.AddMessage(MessageLevels.Debug, $"{prefix}Loading url {url.ToQuoted()}");
            a.Win = b;
            await b.LoadUrl(url).ConfigureAwait(false);
            await Task.WhenAny(b.WaitLoaded(false), a.WhenSupported).ConfigureAwait(false);
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
                M.AddMessage(MessageLevels.Debug, $"{prefix}Page is aware, wait for page to take the screen shot");
                a.AllowJsControl();
                data = await a.WaitScreenShot(10000).ConfigureAwait(false);
                if (data != null)
                {
                    n.Width = b.Width;
                    n.Height = b.Height;
                    n.Duration = a.Duration;
                    n.Fps = a.Fps;
                    n.LoadTime = a.LoadTime;
                }
                if (a.Error != null)
                {
                    n.Desc = "Error: " + a.Error; 
                    M.AddMessage(MessageLevels.Debug, $"{prefix}Got an error: {a.Error}");
                    return Tuple.Create(data, n);
                }
            }
            if (data == null)
            {
                M.AddMessage(MessageLevels.Debug, $"{prefix}Taking screen shot");
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
                M.AddMessage(MessageLevels.Warning, $"{prefix}Failed: {et}");
                throw new Exception(et);
            }
            M.AddMessage(MessageLevels.Debug, $"{prefix}All done");
            return Tuple.Create(data, n);
        }


        /// <summary>
        /// A page reporting an error doesn't have any data
        /// </summary>
        static Tuple<Byte[], MediaInfo> ThrowOnPageError(Tuple<Byte[], MediaInfo> r)
        {
            var desc = r.Item2?.Desc ?? "";
            if (desc.FastStartsWith("Error:"))
                throw new Exception(desc);
            return r;
        }

        static readonly ITextSerializerType JsonSer = SerManager.GetText("json");


        String InternalGetMediaUrl(GetMediaRequest r, HttpServerRequest context)
        {
            var d = r.Params;
            String e = "";
            if (d != null)
                e = String.Concat("&props=", Uri.EscapeDataString(JsonSer.ToString(d)));
            //  A position as a part of the duration (a sound: the middle)
            if (r.RelPos is double rp)
                e = String.Concat(e, "&rpos=", rp.ToString(CultureInfo.InvariantCulture));
            return String.Concat(GetPrefix(context), "mediaView/MediaPreview.html?type=", r.Type, "&link=", Uri.EscapeDataString(r.Url), "&pos=", r.Pos.ToString(CultureInfo.InvariantCulture), e);
        }

        /// <summary>
        /// An url of another origin than the page (an absolute http(s) url to another host / port than the prefix)
        /// </summary>
        static bool IsCrossOrigin(String url, String prefix)
        {
            if (String.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
                return false;
            if (!Uri.TryCreate(prefix, UriKind.Absolute, out var p))
                return true;
            return !String.Equals(u.Scheme, p.Scheme, StringComparison.OrdinalIgnoreCase) || !String.Equals(u.Host, p.Host, StringComparison.OrdinalIgnoreCase) || u.Port != p.Port;
        }

        /// <summary>
        /// Serve an url from this server (the CORS proxy, <see cref="GetProxiedUrl"/>): the url the page uses instead, null if it can't be downloaded
        /// </summary>
        async Task<String> Proxy(String url)
        {
            var h = await GetProxiedUrl(url).ConfigureAwait(false);
            return h == null ? null : "../" + h.Uri;
        }

        /// <summary>
        /// The urls the page uses as WebGL textures or fetches (an effect's picture, an image under an effect, a sound for the offline analysis):
        /// those of another origin are served by this server (the CORS proxy), a browser refuses them otherwise (a tainted texture, a blocked fetch)
        /// </summary>
        async Task ProxyTextures(GetMediaRequest r, HttpServerRequest context)
        {
            var prefix = GetPrefix(context, false);
            foreach (var (get, set) in r.TextureUrls)
            {
                var url = get();
                if (!IsCrossOrigin(url, prefix))
                    continue;
                var p = await Proxy(url).ConfigureAwait(false);
                if (p != null)
                    set(p);
            }
        }

        /// <summary>A page error caused by a cross origin media (a refused texture / fetch)</summary>
        static bool IsCorsError(String desc)
        {
            var d = desc.FastToLower();
            return d.FastIndexOf("cors") >= 0 || d.FastIndexOf("cross-origin") >= 0 || d.FastIndexOf("cross origin") >= 0;
        }

        /// <summary>
        /// The screenshot of a media (MediaPreview.html): cross origin textures are proxied first; a page error about a cross origin media
        /// proxies the media itself and tries again
        /// </summary>
        async Task<Tuple<Byte[], MediaInfo>> InternalShot(GetMediaRequest r, HttpServerRequest context, ScreenshotImageFormats format, int quality)
        {
            await ProxyTextures(r, context).ConfigureAwait(false);
            var x = await GetAdaptiveImageAsync(InternalGetMediaUrl(r, context), r.Width, r.Height, 1, format, quality, false, r.ExtraDelayMs).ConfigureAwait(false);
            var desc = x.Item2?.Desc ?? "";
            if (!desc.FastStartsWith("Error:"))
                return x;
            if (!IsCorsError(desc))
                throw new Exception(desc);
            var p = await Proxy(r.Url).ConfigureAwait(false);
            if (p == null)
                throw new Exception(desc);
            r.Url = p;
            x = await GetAdaptiveImageAsync(InternalGetMediaUrl(r, context), r.Width, r.Height, 1, format, quality, false, r.ExtraDelayMs).ConfigureAwait(false);
            desc = x.Item2?.Desc ?? "";
            if (desc.FastStartsWith("Error:"))
                throw new Exception(desc);
            return x;
        }

        async Task<ScreenshotImageResponse> InternalGetMedia(GetMediaRequest r, HttpServerRequest context, ScreenshotImageFormats format = ScreenshotImageFormats.Png, int quality = 70)
        {
            var x = await InternalShot(r, context, format, quality).ConfigureAwait(false);
            return new ScreenshotImageResponse
            {
                Data = x.Item1,
                Info = x.Item2,
            };
        }

        async Task<ReadOnlyMemory<Byte>> InternalGetMediaPng(GetMediaRequest r, HttpServerRequest context)
            => (await InternalShot(r, context, ScreenshotImageFormats.Png, 70).ConfigureAwait(false)).Item1;


        #region Media Image

        /// <summary>
        /// Get data from an image url: renders the image with the media viewer in a browser window and takes a screenshot
        /// </summary>
        /// <param name="r">The image url, image options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot (png) and information about it (the size and the url)</returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaImage(GetMediaImageRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an image url (see <see cref="GetMediaImage"/>, this returns only the image)
        /// </summary>
        /// <param name="r">The image url, image options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot as png image data</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaImage) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaImage(GetMediaImageRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Image

        #region Media Video

        /// <summary>
        /// Get data from a video url: renders a frame of the video with the media viewer in a browser window and takes a screenshot
        /// </summary>
        /// <param name="r">The video url, video options, the size of the screenshot, when to take it (position in seconds into the video) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot (png) and information about it (the size and the url)</returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaVideo(GetMediaVideoRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a video url (see <see cref="GetMediaVideo"/>, this returns only the image)
        /// </summary>
        /// <param name="r">The video url, video options, the size of the screenshot, when to take it (position in seconds into the video) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot as png image data</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaVideo) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaVideo(GetMediaVideoRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Video

        #region Media Effect

        /// <summary>
        /// Get data from an effect url: renders the (shader) effect with the media viewer in a browser window and takes a screenshot
        /// </summary>
        /// <param name="r">The effect url, effect options, the size of the screenshot, when to take it (position in seconds into the effect) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot (png) and information about it (the size and the url)</returns>
        [WebApi]
        //[WebApiClientCache(30)]
        //[WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaEffect(GetMediaEffectRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from an effect url (see <see cref="GetMediaEffect"/>, this returns only the image)
        /// </summary>
        /// <param name="r">The effect url, effect options, the size of the screenshot, when to take it (position in seconds into the effect) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot as png image data</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaEffect) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaEffect(GetMediaEffectRequest r, HttpServerRequest context)
            => InternalGetMediaPng(r, context);

        #endregion//Media Effect

        #region Media Audio

        /// <summary>The audio request with its effect: the request's, else the service's AudioEffect (Led_Spectrum)</summary>
        GetMediaAudioRequest WithAudioEffect(GetMediaAudioRequest r)
        {
            r.Options ??= new SysWeaver.MicroService.Media.MediaAudio();
            if (String.IsNullOrWhiteSpace(r.Options.Effect))
                r.Options.Effect = AudioEffect;
            return r;
        }

        /// <summary>
        /// Get data from a sound url: an audio effect draws the sound (the request's Options.Effect, else the service's AudioEffect, by default
        /// Led_Spectrum) with the media viewer in a browser window, the screenshot shows it in the middle of the sound (its duration × 0.5: the
        /// audio texture is computed from the decoded samples at that position)
        /// </summary>
        /// <param name="r">The sound url, audio options (the effect and its settings), the size of the screenshot and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot (png) and information about it (the size, the duration and the url)</returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaAudio(GetMediaAudioRequest r, HttpServerRequest context)
            => InternalGetMedia(WithAudioEffect(r), context);

        /// <summary>
        /// Get a png from a sound url (see <see cref="GetMediaAudio"/>, this returns only the image)
        /// </summary>
        /// <param name="r">The sound url, audio options (the effect and its settings), the size of the screenshot and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot as png image data</returns>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi(nameof(MediaAudio) + ".png")]
        public Task<ReadOnlyMemory<Byte>> MediaAudio(GetMediaAudioRequest r, HttpServerRequest context)
            => InternalGetMediaPng(WithAudioEffect(r), context);

        #endregion//Media Audio

        #region Media YouTube

        /// <summary>
        /// Get data from a YouTube code: renders a frame of the YouTube video with the media viewer in a browser window and takes a screenshot
        /// </summary>
        /// <param name="r">The YouTube video code, the size of the screenshot, when to take it (position in seconds into the video) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot (png) and information about it (the size and the url)</returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiCompression("")]
        public Task<ScreenshotImageResponse> GetMediaYouTube(GetMediaYouTubeRequest r, HttpServerRequest context)
            => InternalGetMedia(r, context);

        /// <summary>
        /// Get a png from a YouTube code (see <see cref="GetMediaYouTube"/>, this returns only the image)
        /// </summary>
        /// <param name="r">The YouTube video code, the size of the screenshot, when to take it (position in seconds into the video) and an optional extra delay</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The screenshot as png image data</returns>
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
        /// Get a png from a google map position (requires a google maps key in the service parameters)
        /// </summary>
        /// <param name="r">The map: center (latitude, longitude), zoom, map type, language, size, dpi and an optional pin</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The map as png image data</returns>
        /// <exception cref="Exception">No google maps key is configured, or the center is invalid</exception>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi("GetGoogleMap.png")]
        public Task<ReadOnlyMemory<Byte>> GetGoogleMapPng(GetGoogleMapRequest r, HttpServerRequest context)
            => InternalGetGoogleMap(r, ScreenshotImageFormats.Png, 100, context);


        /// <summary>
        /// Get a jpg from a google map position (requires a google maps key in the service parameters)
        /// </summary>
        /// <param name="r">The map: center (latitude, longitude), zoom, map type, language, size, dpi, an optional pin and the jpeg quality</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The map as jpeg image data</returns>
        /// <exception cref="Exception">No google maps key is configured, or the center is invalid</exception>
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
            return ThrowOnPageError(res).Item1;
        }

        #endregion//Media Map

        /// <summary>
        /// Get an image and meta data for a given url (a screenshot of the page).
        /// If the request has Control set, a page that is "aware" decides the size of the window and when the screenshot is taken, and the response includes information about the result.
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale (in percent, 0 is 100), the image format and quality, and optional delay and speed settings</param>
        /// <returns>The image data, and information about the image if control was requested (size, and for pages that are aware: duration, fps and load time)</returns>
        /// <exception cref="ArgumentException">No url was given</exception>
        [WebApi]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4, WebApiCaches.Globally)]
        [WebApiCompression("")]
        [WebApiAuth(Roles.Service)]
        public async Task<ScreenshotImageResponse> GetImage(ScreenshotImageRequest r)
        {
            if (String.IsNullOrEmpty(r?.Url))
                throw new ArgumentException("An url is required!");
            //  The request is in percent, the browser wants a factor
            var scale = r.Scale > 0 ? (r.Scale / 100.0) : 1.0;
            if (r.Control)
            {
                var x = await GetAdaptiveImageAsync(r.Url, r.Width, r.Height, scale, r.Format, r.Quality, r.OptimizeForSpeed, r.ExtraDelayMs).ConfigureAwait(false);
                ThrowOnPageError(x);
                return new ScreenshotImageResponse
                {
                    Data = x.Item1,
                    Info = x.Item2,
                };
            }
            var y = await GetImageAsync(r.Url, r.Width, r.Height, scale, r.Format, r.Quality, r.OptimizeForSpeed, r.ExtraDelayMs).ConfigureAwait(false);
            return new ScreenshotImageResponse
            {
                Data = y,
            };
        }

        /// <summary>
        /// Get a png image (screenshot) from an url
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale and optional delay and speed settings</param>
        /// <returns>The screenshot as png image data</returns>
        /// <exception cref="ArgumentException">No request or no url was given</exception>
        [WebApiClientCache(30)] 
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/png", true)]
        [WebApi("WebScreenshot.png")]
        public async Task<ReadOnlyMemory<Byte>> WebScreenshotPng(ScreenshotPngRequest r)
            => (await GetImage(ScreenshotImageRequest.From(r ?? throw new ArgumentException("A request is required!"))).ConfigureAwait(false)).Data;

        /// <summary>
        /// Get a jpeg image (screenshot) from an url
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale, the jpeg quality and optional delay and speed settings</param>
        /// <returns>The screenshot as jpeg image data</returns>
        /// <exception cref="ArgumentException">No request or no url was given</exception>
        [WebApiClientCache(30)]
        [WebApiRequestCache(25, WebApiCaches.Globally)]
        [WebApiRaw("image/jpeg", true)]
        [WebApi("WebScreenshot.jpg")]
        public async Task<ReadOnlyMemory<Byte>> WebScreenshotJpg(ScreenshotJpegRequest r)
            => (await GetImage(ScreenshotImageRequest.From(r ?? throw new ArgumentException("A request is required!"))).ConfigureAwait(false)).Data;


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

        /// <summary>
        /// The effects of the media service, used for stress testing
        /// </summary>
        static readonly String[] Effects = [
            "2D-clouds", "Blobs", "Bubbles", "Bubbles2", "CarbonFabric", "Caustics", "ChaseRGB", "ChromaticBlob",
            "ChromeTiles", "CloudFlight", "Clouds", "ColorBubbles", "CubistApollonianCircles", "CyberCube", "DancingLights", "DiatomArrangement",
            "Fire", "Fireworks", "Flow", "GlowingLines", "Grass", "Grid", "HexPulse", "HexWave",
            "LargeBubbles", "LightRays", "Lines", "LiquidMetal", "MacPaint", "Mario", "Matrix", "MetaBalls",
            "MetallicVoronotRoses", "MetalPlates", "Meters", "MorningSilhouettes", "Pulse", "RaceBanner", "RainbowCells", "Silk",
            "SimpleTiles", "SlowCaustics", "SmoothBands", "SoftBokeh", "SoftWaves", "SphereMandala", "Spin", "Terrain",
            "TwinklingTunnel", "VerticalMeters",
            ];

        String EffectUrl(String name) => InternalGetMediaUrl(new GetMediaEffectRequest { Url = "backgroundEffects/" + name + ".glsl" }, null);

        /// <summary>
        /// Stress test: take screenshots of the background effects one at a time (the images are also saved in a temp folder)
        /// </summary>
        /// <param name="count">The number of screenshots to take</param>
        /// <returns>The last screenshot as a "data:image/png;base64," url</returns>
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
                    Url = EffectUrl(flag),
                    Control = true,
                    Width = 320,
                    Height = 180,
                };
                res = await GetImage(p).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(f, String.Join(i.ToString().PadLeft(4, '0'), nameof(StressTestSerial), ".png")), res.Data).ConfigureAwait(false);
                tot += res.Data.Length;
            }

            M.AddMessage(MessageLevels.Info, $"{Prefix}Took: {DateTime.UtcNow - start}, data len: {tot}");
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Data);
            return d;
        }

        /// <summary>
        /// Stress test: take screenshots of the background effects all at the same time (the images are also saved in a temp folder)
        /// </summary>
        /// <param name="count">The number of screenshots to take</param>
        /// <returns>The last screenshot (the one with the highest index) as a "data:image/png;base64," url</returns>
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
                    Url = EffectUrl(flag),
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
            M.AddMessage(MessageLevels.Info, $"{Prefix}Took: {DateTime.UtcNow - start}, data len: {tot}");
            var d = "data:image/png;base64," + Convert.ToBase64String(res.Data);
            return d;
        }




        /// <inheritdoc />
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        /// <inheritdoc />
        public IEnumerable<Stats> GetStats()
        {
            var j = Interlocked.Read(ref JsInControlCount);
            var a = Interlocked.Read(ref AdaptCount);
            yield return new Stats(ServiceName, "Js in control", j, "The number of times the js was in control of taking the screenshot");
            yield return new Stats(ServiceName, "Js not in control", a - j, "The number of times the js was NOT in control of taking the screenshot");
        }
    }



}
