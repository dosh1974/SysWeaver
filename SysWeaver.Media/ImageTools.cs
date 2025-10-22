using ImageMagick;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysWeaver.Media
{
    public static class ImageTools
    {


        public static async Task<bool> FillInto(String source, String dest, int width, int height, bool scaleUp = false, Gravity gravity = Gravity.Center, MagickFormat format = MagickFormat.Png, int bitDepth = 8)
        {
            using var image = await ReadImage(source).ConfigureAwait(false);
            if (!FillInto(image, width, height, scaleUp, gravity))
                return false;
            if (bitDepth > 0)
                image.SetBitDepth((uint)bitDepth, Channels.All);
            await image.WriteAsync(dest, format).ConfigureAwait(false);
            return true;
        }


        public static async Task<bool> FitInto(String source, String dest, int width, int height, bool scaleUp = false, MagickColor extendUsing = null, Gravity gravity = Gravity.Center, MagickFormat format = MagickFormat.Png, int bitDepth = 8)
        {
            using var image = await ReadImage(source).ConfigureAwait(false);
            if (!FitInto(image, width, height, scaleUp, extendUsing, gravity))
                return false;
            if (bitDepth > 0)
                image.SetBitDepth((uint)bitDepth, Channels.All);
            await image.WriteAsync(dest, format).ConfigureAwait(false);
            return true;
        }


        public static bool FillInto(MagickImage image, int width, int height, bool scaleUp = false, Gravity gravity = Gravity.Center)
        {
            var w = image.Width;
            var h = image.Height;
            if ((w <= 0) || (h <= 0))
                return false;
            var scaleX = (double)width / (double)w;
            var scaleY = (double)height / (double)h;
            var scale = scaleX > scaleY ? scaleX : scaleY;
            if ((scale > 1) && (!scaleUp))
                scale = 1;
            if (scale != 1)
            {
                var nw = (uint)(scale * w + 0.5);
                var nh = (uint)(scale * h + 0.5);
                if ((width - nw) >= -1)
                    nw = (uint)width;
                if ((height - nh) >= -1)
                    nh = (uint)height;
                image.Resize(nw, nh);
                w = nw;
                h = nh;
            }
            if ((w > width) || (h > height))
                image.Crop((uint)(w < width ? w : (uint)width), (uint)(h < height ? h : (uint)height), gravity);
            return true;
        }

        public static bool FitInto(MagickImage image, int width, int height, bool scaleUp = false, MagickColor extendUsing = null, Gravity gravity = Gravity.Center)
        {
            var w = image.Width;
            var h = image.Height;
            if ((w <= 0) || (h <= 0))
                return false;
            var scaleX = (double)width / (double)w;
            var scaleY = (double)height / (double)h;
            var scale = scaleX < scaleY ? scaleX : scaleY;
            if ((scale > 1) && (!scaleUp))
                scale = 1;
            if (scale != 1)
            {
                var nw = (uint)(scale * w + 0.5);
                var nh = (uint)(scale * h + 0.5);
                if ((nw - width) >= -1)
                    nw = (uint)width;
                if ((nw - height) >= -1)
                    nh = (uint)height;
                image.Resize(nw, nh);
                w = nw;
                h = nh;
            }
            if (extendUsing != null)
            {
                if ((w < width) || (h < height))
                    image.Extent((uint)width, (uint)height, gravity, extendUsing);
            }
            return true;
        }


        public static Task<MagickImage> ReadImage(String filename)
        {
            bool isWeb = FileHash.IsWeb(filename);
            return isWeb ? ReadImageWeb(filename) : ReadImageLocal(filename);
        }

        /*
        public static MagickImage ReadImage(Byte[] data)
        {
            return new MagickImage(data);
        }*/

        public static MagickImage ReadImage(ReadOnlyMemory<Byte> data)
        {
            return new MagickImage(data.Span);
        }

        public static Task<MagickImage> ReadImageLocal(String filename)
        {
            return Task.FromResult(new MagickImage(filename));
        }

        public static async Task<MagickImage> ReadImageWeb(String url)
        {
            var client = WebTools.GetHttpClient(15);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;
            return new MagickImage(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        }



        public static ReadOnlyMemory<Byte> ToData(MagickImage image)
        {
            var size = (int)(image.Width * image.Height + 32);
            using var ms = new MemoryStream(size);
            image.Write(ms);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

    }
}
