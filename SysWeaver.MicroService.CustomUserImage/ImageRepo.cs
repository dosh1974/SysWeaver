using ImageMagick;
using System;
using System.IO;
using System.Threading.Tasks;
using SysWeaver.Media;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public sealed class ImageRepo : StaticFileRepo
    {
                





        /// <summary>
        /// The sizes to save the file as
        /// </summary>
        public ImageSize[] Sizes;

        protected override Task<Tuple<FileUploadStatus, object>> OnRequest(HttpServerRequest r, bool isUpload)
        {
            Extensions = ImageRepoService.SupportedSourceExts;
            SavePreCompressed = false;
            Compression = "";
            return base.OnRequest(r, isUpload);
        }

        protected override async Task<FileUploadStatus> OnUploaded(FileInfo file, long replacedSize, FileUploadInfo info, HttpServerRequest r, object context)
        {
            var data = await File.ReadAllBytesAsync(file.FullName).ConfigureAwait(false);
            var imgInfo = new MagickImageInfo(data);
            switch (imgInfo.Format)
            {
                case MagickFormat.Png:
                case MagickFormat.Jpeg:
                case MagickFormat.Tif:
                case MagickFormat.WebP:
                case MagickFormat.Avif:
                    break;
                default:
                    return FileUploadStatus.InvalidFile;
            }
            var fn = Path.GetFileNameWithoutExtension(file.Name);
            var sizes = Sizes;
            var ss = sizes.Length;
            ReadOnlyMemory<Byte>[] imgs = new ReadOnlyMemory<byte>[ss];
            String[] names = new string[ss];

            using var img = new MagickImage(data);
            for (int i = 0; i < ss; ++i)
            {
                var size = sizes[i];
                using var resized = img.Clone() as MagickImage;

                var bgCol = new MagickColor(size.Background ?? "#000");
                if ((!size.AllowTransparent) && (img.HasAlpha))
                {
                    img.BackgroundColor = bgCol;
                    img.Alpha(AlphaOption.Remove);
                }

                int w = (int)resized.Width;
                int h = (int)resized.Height;
                int tw = size.Width;
                int th = size.Height;
                if (tw > 0)
                {
                    if (th <= 0)
                        th = (h * tw + (w >> 1)) / w;
                }else
                {
                    if (th > 0)
                    {
                        tw = (w * th + (h >> 1)) / h;
                    }else
                    {
                        tw = w;
                        th = h;
                    }
                }
                if ((tw != w) || (th != h))
                {
                    var es = size.ExactSize;
                    if (size.Fit)
                        ImageTools.FitInto(resized, tw, th, es, es ? bgCol : null);
                    else
                        ImageTools.FillInto(resized, tw, th, es);
                }
                ImageRepoService.SaveFormats.TryGetValue(size.Ext, out var save);
                resized.Format = save.SaveFormat;
                resized.Quality = (uint)Math.Max(10, Math.Min(100, size.Quality));
                imgs[i] = ImageTools.ToData(resized);
                names[i] = String.Format(size.Name, fn) + save.SaveExt;
            }
            var folder = file.DirectoryName;
            for (int i = 0; i < ss; ++i)
                await FileExt.WriteMemoryAsync(Path.Combine(folder, names[i]), imgs[i]).ConfigureAwait(false);
            return FileUploadStatus.None;
        }

    }
}
