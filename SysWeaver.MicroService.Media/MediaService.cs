using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    [WebApiUrl("../mediaView/Api/")]
    public sealed class MediaService : IDisposable
    {

        public MediaService(ServiceManager manager)
        {
            Manager = manager;
            var te = MediaEditor;
            foreach (var x in MediaExtensions)
                manager.TryAddExtensionViewer(x, te);
        }

        readonly ServiceManager Manager;


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
