using System;

namespace SysWeaver.MicroService
{
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
