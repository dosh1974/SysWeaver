using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EffectWeaver.Shaders;
using SysWeaver.MicroService.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Serves the browser media player and viewer (mediaView/, mediaPlayer/): lists the GLSL effect libraries, builds Effect Weaver effects
    /// for the player, resolves the media of effects, registers the media viewer for image, video, audio and .glsl files and adds debug
    /// menu entries for the effect libraries and demos.
    /// </summary>
    [WebApiUrl("../mediaView/Api/")]
    [WebMenuPath(null, "Debug/Media", "Media demos", "Media effect libraries and demos", "../icons/display.svg", 30)]
    [WebMenuEmbedded(null, "Debug/Media/BackgroundEffects", "Background effects", "mediaView/BackgroundEffectsLib.html", "Show all background effects available", "../icons/computer.svg", 1, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/ImageEffects", "Image effects", "mediaView/ImageEffectsLib.html", "Show all image effects available", "../icons/image.svg", 2, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/CollageEffects", "Collage effects", "mediaView/CollageEffectsLib.html", "Show all collage effects available", "../icons/brick.svg", 3, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/TextDemo", "Text demo", "mediaView/TextDemo.html", "A demo showcasing the render dynamic text to an image (with added effects)", "../icons/text.svg", 4, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/MediaEditor", "Media editor", "mediaView/MediaEdit.html?link=../logo.png&props=%7B%22Transparent%22%3Atrue%2C%22Effect%22%3A%22imageEffects%2FRain.glsl%22%7D", "The media editor (showing the logo, transparent, with the rain effect): open any media, change its settings and share a link to it", "../icons/edit.svg", 5, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/GoogleMapDemo", "Google map demo", "mediaView/MapDemo.html", "A demo showcase of the Google map support", "../icons/table_country.svg", 8, Roles.Dev)]
    public sealed class MediaService : IDisposable
    {
        /// <summary>
        /// Create the service and register the media viewer for the media file extensions
        /// </summary>
        /// <param name="manager">The service manager (extension viewers, messages and the effect media resolvers)</param>
        /// <param name="p">The parameters, null = defaults</param>
        public MediaService(ServiceManager manager, MediaParams p = null)
        {
            p = p ?? new MediaParams();
            var gmk = p.GoogleMapsKey?.GetApiKey(false);
            GoogleMapsKey = gmk;
            if (gmk == null)
                manager.AddMessage("No Google Maps key supplied, Google map demo won't work!", MessageLevels.Debug);
            Manager = manager;
            var te = MediaEditor;
            foreach (var x in MediaExtensions)
                manager.TryAddExtensionViewer(x, te);
        }
        
        readonly String GoogleMapsKey;
        readonly ServiceManager Manager;

        /// <summary>
        /// Get a google map key (if it's configured and exists)
        /// </summary>
        /// <returns>The Google Maps key, null if none is configured</returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        [WebApiAuth(Roles.Dev)]
        public String GetGoogleMapsKey() => GoogleMapsKey;
        
        /// <summary>
        /// Get a list of all background effects
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(28, WebApiCaches.Globally)]
        public String[] GetBackgroundEffects(HttpServerRequest context)
            => InternalGet("mediaView/backgroundEffects", context);

        /// <summary>
        /// Get a list of all image effects
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(28, WebApiCaches.Globally)]
        public String[] GetImageEffects(HttpServerRequest context)
            => InternalGet("mediaView/imageEffects", context);

        /// <summary>
        /// Get a list of all collage effects
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(28, WebApiCaches.Globally)]
        public String[] GetCollageEffects(HttpServerRequest context)
            => InternalGet("mediaView/collageEffects", context);

        /// <summary>
        /// Get a list of all counter effects
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(28, WebApiCaches.Globally)]
        public String[] GetCounterEffects(HttpServerRequest context)
            => InternalGet("mediaView/counterEffects", context);

        /// <summary>
        /// Get a list of all audio effects (effects that draw a sound, its audio texture)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(28, WebApiCaches.Globally)]
        public String[] GetAudioEffects(HttpServerRequest context)
            => InternalGet("mediaView/audioEffects", context);

        /// <summary>
        /// The most references ResolveEffectMedia takes (a collage)
        /// </summary>
        const int MaxEffectMediaRefs = 64;

        /// <summary>
        /// The media of an effect's media line (<see cref="MediaEffectMediaRequest"/>): its references that aren't urls, or the defaults of its
        /// kind (no references), resolved by the services that know them (<see cref="IEffectMediaResolver"/>, ex: Effect Weaver's editor: its
        /// stock media and the user's own media). Used by the media player when an effect's settings name no Texture.
        /// </summary>
        /// <param name="r">The kind and references of the line</param>
        /// <param name="context">The request (the user's own media are the session's user's)</param>
        /// <returns>The urls (absolute or relative to the mediaView folder), empty if no service knows them</returns>
        [WebApi]
        [WebApiSessionRateLimit(10, 1, 20, 5)]
        public async Task<MediaEffectMediaResponse> ResolveEffectMedia(MediaEffectMediaRequest r, HttpServerRequest context)
        {
            var res = new MediaEffectMediaResponse();
            var kind = r?.Kind ?? "";
            if (kind != "image" && kind != "video" && kind != "audio" && kind != "audio2" && kind != "collage")
                return res;
            var refs = (r.Refs ?? []).Where(x => !String.IsNullOrWhiteSpace(x) && x.Length <= 2048).Take(MaxEffectMediaRefs).ToArray();
            foreach (var x in Manager.GetAll<IEffectMediaResolver>())
            {
                try
                {
                    var urls = await x.ResolveEffectMedia(kind, refs, context);
                    if (urls is { Length: > 0 })
                    {
                        res.Urls = urls;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Manager.AddMessage("Effect media " + kind + " " + String.Join(" ", refs) + ": " + ex.Message, MessageLevels.Warning);
                }
            } 
            return res;
        }


        /// <summary>
        /// The longest effect source BuildEffect accepts (characters)
        /// </summary>
        const int MaxEffectSource = 1 << 20;

        /// <summary>
        /// The most builds BuildEffect keeps (all are dropped when it's reached)
        /// </summary>
        const int MaxBuildCache = 256;

        /// <summary>
        /// Recent builds of BuildEffect, key: SHA256 of the source + target (GLSL version, alpha). A response is never changed after it's cached.
        /// </summary>
        readonly ConcurrentDictionary<String, MediaEffectBuildResponse> BuildCache = new();

        /// <summary>
        /// Build an effect the way Effect Weaver does (SysWeaver.Media.Shader, the same code as Effect Saver, VjWeaver and the editor):
        /// the Ew snippets the effect calls are inserted, ShaderToy style effects get the uniforms and a main(), an effect with a vertex stage
        /// (vertexMain) gets its vertex shader. Used by the media player (media_player_effect.js) for effects in the Effect Weaver format.
        /// </summary>
        /// <param name="r">The effect source and the target</param>
        /// <returns>The program and the effect's control defaults, null if the source is empty, too long or can't be parsed</returns>
        [WebApi]
        public MediaEffectBuildResponse BuildEffect(MediaEffectBuildRequest r)
        {
            var src = r?.Source;
            if (String.IsNullOrEmpty(src) || (src.Length > MaxEffectSource))
                return null;
            //  The same effect is built again and again (every player, page and thumbnail): keep the recent builds (source hash + target)
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(src))) + (r.Gl2 ? "2" : "1") + (r.Alpha ? "a" : "o");
            var cache = BuildCache;
            if (cache.TryGetValue(key, out var cached))
                return cached;
            try
            {
                var fx = ShaderParser.Parse("Effect", src, null);
                var program = ShaderParser.BuildSource(fx, null, r.Gl2, r.Alpha);
                var (fragment, vertex) = ShaderStages.SplitProgram(program);
                var res = new MediaEffectBuildResponse
                {
                    Program = program,
                    Es3 = fragment.StartsWith("#version 300", StringComparison.Ordinal),
                    Vertex = vertex != null,
                    Controls = fx.MouseDefault,
                    ControlNames = fx.Mouse.Select(x => x.Known ? x.Name : "").ToArray(),
                    ControlDefs = fx.Mouse.Select(ControlOf).ToArray(),
                };
                if (cache.Count >= MaxBuildCache)
                    cache.Clear();
                cache[key] = res;
                return res;
            }
            catch (Exception ex)
            {
                Manager.AddMessage("Failed to build the effect " + (r.Name ?? "").ToQuoted() + ": " + ex.Message, MessageLevels.Warning);
                return null;
            }
        }


        /// <summary>
        /// A range in a control's description ("Tempo, 60 .. 200 BPM"), a known unit right after it is taken too (as Effect Weaver's EwShader.MouseMap)
        /// </summary>
        static readonly Regex ControlRangeRx = new(@"(-?\d+(?:\.\d+)?)\s*\.\.\s*(-?\d+(?:\.\d+)?)(?:\s*(BPM|Hz|%|°|deg|ms|s|px)\b)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// A control of an effect for the player (see <see cref="MediaEffectBuildResponse.ControlDefs"/>), null if the effect doesn't describe it.
        /// The value it stands for: its "Min .. Max | Unit" parts, else a range in its description ("0 .. 1" says nothing: none), as Effect Weaver shows it.
        /// </summary>
        static MediaEffectControl ControlOf(MouseChannel c)
        {
            if (!c.Known)
                return null;
            MediaEffectControlMap map = null;
            if (c.Min is double lo && c.Max is double hi && lo != hi)
                map = new MediaEffectControlMap { Min = lo, Max = hi, Unit = String.IsNullOrEmpty(c.Unit) ? null : c.Unit, Format = String.IsNullOrEmpty(c.Format) ? null : c.Format };
            else
            {
                var m = ControlRangeRx.Match(c.Desc ?? "");
                if (m.Success
                    && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                    && a != b && !(a == 0 && b == 1))
                {
                    var u = m.Groups[3].Success ? m.Groups[3].Value : null;
                    if (u != null && (u.Equals("bpm", StringComparison.OrdinalIgnoreCase) || u.Equals("hz", StringComparison.OrdinalIgnoreCase)))
                        u = u.Length == 3 ? "BPM" : "Hz";
                    map = new MediaEffectControlMap
                    {
                        Min = a,
                        Max = b,
                        Unit = String.IsNullOrEmpty(c.Unit) ? u : c.Unit,
                        Format = String.IsNullOrEmpty(c.Format) ? null : c.Format,
                    };
                }
            }
            return new MediaEffectControl { Name = c.Name, Desc = c.Desc ?? "", Default = c.Default, Used = c.Used, Map = map };
        }


        String[] InternalGet(String path, HttpServerRequest context)
        {
            List<String> files = new();
            foreach (var x in context.Server.EnumEndPoints(path))
            {
                var u = x.Uri;
                if (!u.FastEndsWith(".glsl"))
                    continue;
                var l = u.LastIndexOf('/') + 1;
                files.Add(u.Substring(l, u.Length - l - 5));
            }
            files.Sort();
            return files.ToArray();

        }


        /// <summary>
        /// Remove the media viewer registrations
        /// </summary>
        public void Dispose()
        {
            var manager = Manager;
            foreach (var x in MediaExtensions)
                manager.TryRemoveExtensionViewer(x, out var _);
        }


        const string MediaEditor = "mediaView/MediaView.html?link={0}";

        /// <summary>
        /// Should be synched with the image, video and audio extensions in core/format.js (Sw.Format.Ext)
        /// </summary>
        static readonly String[] MediaExtensions = [
            "png",
            "gif",
            "jpg",
            "jpeg",
            "jpe",
            "jfif",
            "avif",
            "webp",
            "apng",
            "bmp",
            "ico",
            "tiff",
            "tif",
            "svg",
            "webm",
            "mp4",
            "m4v",
            "ogg",
            "ogv",
            "mov",
            "mkv",
            "mp3",
            "flac",
            "m4a",
            "m4b",
            "opus",
            "oga",
            "weba",
            "wav",
            "aac",
            "glsl",
        ];

    }

}
