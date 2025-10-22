using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{



    public sealed class MediaThumbnailParams
    {
        public int MaxConcurrency = 16;

        public String MediaUrlRoot;
    }

    /// <summary>
    /// Service that enables media thumbnails using web client rendering
    /// </summary>
    [IsMicroService]
    [RequiredDep(typeof(IThumbnailWebService))]
    [RequiredDep(typeof(FileHttpServerModule))]
    [RequiredDep(typeof(HttpServerBase))]
    public sealed class MediaThumbnailService : IDisposable
    {
        public override string ToString()
        {
            var rs = MediaInfo.ExternalMediaTypes.ToList();
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " media handlers: " : " media handlers: ", String.Join(", ", rs));
        }



        public MediaThumbnailService(ServiceManager manager, MediaThumbnailParams p = null)
        {
            p = p ?? new MediaThumbnailParams();
            Thumbnail = manager.Get<IThumbnailWebService>();
            var m = MediaHandlers;
            m.Push(MediaInfo.AddMediaInfoCreator("glsl", GetEffect));
            m.Push(MediaInfo.AddMediaInfoCreator("svg", GetImage));
            MediaUrlRoot = p.MediaUrlRoot;
            FileServer = manager.TryGet<FileHttpServerModule>();
            if (FileServer == null)
            {
                Action<Object, ServiceInfo> get = null;
                get = (service, info) =>
                {
                    var ns = service as FileHttpServerModule;
                    if (ns == null)
                        return;
                    FileServer = ns;
                    manager.OnServiceAdded -= get;
                };
                manager.OnServiceAdded += get;
            }

            HttpServer = manager.TryGet<HttpServerBase>();
            if (HttpServer == null)
            {
                Action<Object, ServiceInfo> get = null;
                get = (service, info) =>
                {
                    var ns = service as HttpServerBase;
                    if (ns == null)
                        return;
                    HttpServer = ns;
                    manager.OnServiceAdded -= get;
                };
                manager.OnServiceAdded += get;
            }
            M = manager;

            MaxThumbLock = new AsyncLock(Math.Max(1, p.MaxConcurrency));
        }
        readonly IThumbnailWebService Thumbnail;
        readonly ServiceManager M;
        readonly String MediaUrlRoot;
        HttpServerBase HttpServer;
        FileHttpServerModule FileServer;

        readonly AsyncLock MaxThumbLock;

        Task<MediaInfo> GetEffect(String filename, int width, int height, bool fill, String baseName)
            => GetMedia(filename, width, height, fill, baseName, 5.0, 4);


        Task<MediaInfo> GetImage(String filename, int width, int height, bool fill, String baseName)
            => GetMedia(filename, width, height, fill, baseName, 5.0, 0);

        async Task<MediaInfo> GetMedia(String filename, int width, int height, bool fill, String baseName, double pos, int type)
        {
            var s = HttpServer;
            if (s == null)
                return null;
            var pre = MediaUrlRoot ?? s.LocalUri;
            if (pre == null)
                return null;
            if (!FileHash.IsWeb(filename))
            {
                var u = FileServer;
                if (u == null)
                    return null;
                if (!File.Exists(filename))
                    return null;
                filename = FileServer.LocalToWeb(filename);
                if (filename == null)
                    return null;
                filename = pre + filename;
            }
            var url = pre + "mediaView/MediaPreview.html?pos=" + pos.ToString(CultureInfo.InvariantCulture) + "&type=" + type + "&link=" + filename;
            if (fill)
                url += "&fill";
            GetPngResponse data;
            int rw = 640;
            int rh = 360;
            if ((width > 0) && (height > 0))
            {
                rw = width;
                rh = height;
                while ((rw < 640) || (rh < 360))
                {
                    rw += rw;
                    rh += rh;
                    if (rw >= 2048)
                        break;
                    if (rh >= 2048)
                        break;
                }
            }
            using (await MaxThumbLock.Lock().ConfigureAwait(false))
                data = await Thumbnail.GetPng(new GetPngRequest
                {
                    Control = true,
                    Url = url,
                    Width = rw,
                    Height = rh,
                }).ConfigureAwait(false);
            var mi = data.Info;
            double scale = rw;
            scale *= rh;
            scale /= 1920;
            scale /= 1080;
            mi.Fps *= scale;
            if ((width <= 0) || (height <= 0))
            {
                mi.Width = 1920;
                mi.Height = 1080;
                return mi;
            }
            var png = data.Png;
            if ((rw != width) || (rh != height))
            {
                using var image = ImageTools.ReadImage(png);
                if (fill)
                    ImageTools.FillInto(image, width, height);
                else
                    ImageTools.FitInto(image, width, height);
                using var ms = new MemoryStream(width * height * 3);
                image.Write(ms, ImageMagick.MagickFormat.Png);
                png = ms.ToArray();
            }
            var fn = baseName + ".png";
            await File.WriteAllBytesAsync(fn, png).ConfigureAwait(false);
            mi.IconFile = fn;
            return mi;

        }

        readonly Stack<IDisposable> MediaHandlers = new Stack<IDisposable>();

        public void Dispose()
        {
            var m = MediaHandlers;
            while (m.Count > 0)
                m.Pop()?.Dispose();
        }


    }



}
