using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EffectWeaver.Shaders;
using SysWeaver.MicroService.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
   

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
        /// <returns></returns>
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
