using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
   

    [WebApiUrl("../mediaView/Api/")]
    [WebMenuPath(null, "Debug/Media", "Media", "Media previews etc", "../icons/display.svg")]
    [WebMenuEmbedded(null, "Debug/Media/BackgroundEffects", "Background effects", "mediaView/BackgroundEffectsLib.html", "Show all background effects available", "../icons/computer.svg", 1, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/ImageEffects", "Image effects", "mediaView/ImageEffectsLib.html", "Show all image effects available", "../icons/image.svg", 2, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/CollageEffects", "Collage effects", "mediaView/CollageEffectsLib.html", "Show all collage effects available", "../icons/brick.svg", 3, Roles.Dev)]
    [WebMenuEmbedded(null, "Debug/Media/TextDemo", "Text demo", "mediaView/TextDemo.html", "A demo showcasing the render dynamic text to an image (with added effects)", "../icons/text.svg", 4, Roles.Dev)]
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
        /// Should be synched with the extensions supported in text.js
        /// </summary>
        static readonly String[] MediaExtensions = [
            "png",
            "gif",
            "jpg",
            "jpeg",
            "avif",
            "webp",
            "tiff",
            "tif",
            "svg",
            "jfif",
            "webm",
            "mp4",
            "ogg",
            "mov",
            "mp3",
            "wav",
            "aac",
            "glsl",
        ];

    }

}
