using ImageMagick;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysWeaver.Media
{
    /// <summary>
    /// Image helpers built on ImageMagick (Magick.NET)
    /// </summary>
    public static class ImageTools
    {


        /// <summary>
        /// Read an image, scale it so that it covers the target size (see <see cref="FillInto(MagickImage, int, int, bool, Gravity)"/>) and write it.
        /// </summary>
        /// <param name="source">The source file name or http(s) url</param>
        /// <param name="dest">The destination file name</param>
        /// <param name="width">The target width</param>
        /// <param name="height">The target height</param>
        /// <param name="scaleUp">If true, smaller images are scaled up</param>
        /// <param name="gravity">The part of the image to keep when cropping</param>
        /// <param name="format">The format to write</param>
        /// <param name="bitDepth">If greater than 0, the bit depth of all channels is set to this before writing</param>
        /// <returns>False if the image has no pixels, else true</returns>
        /// <exception cref="MagickException">The source can't be read or the destination can't be written</exception>
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


        /// <summary>
        /// Read an image, scale it so that it fits inside the target size (see <see cref="FitInto(MagickImage, int, int, bool, MagickColor, Gravity)"/>) and write it.
        /// </summary>
        /// <param name="source">The source file name or http(s) url</param>
        /// <param name="dest">The destination file name</param>
        /// <param name="width">The target width</param>
        /// <param name="height">The target height</param>
        /// <param name="scaleUp">If true, smaller images are scaled up</param>
        /// <param name="extendUsing">If not null, the image is extended to the target size using this color</param>
        /// <param name="gravity">The placement of the image when extending</param>
        /// <param name="format">The format to write</param>
        /// <param name="bitDepth">If greater than 0, the bit depth of all channels is set to this before writing</param>
        /// <returns>False if the image has no pixels, else true</returns>
        /// <exception cref="MagickException">The source can't be read or the destination can't be written</exception>
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


        /// <summary>
        /// Scale an image (keeping the aspect ratio) so that it covers the target size, and crop the part outside of the target size.
        /// Images smaller than the target size are only scaled up if <paramref name="scaleUp"/> is true (else they are only cropped in the dimension that is too large).
        /// </summary>
        /// <param name="image">The image to modify</param>
        /// <param name="width">The target width</param>
        /// <param name="height">The target height</param>
        /// <param name="scaleUp">If true, smaller images are scaled up</param>
        /// <param name="gravity">The part of the image to keep when cropping</param>
        /// <returns>False if the image has no pixels, else true</returns>
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

        /// <summary>
        /// Scale an image (keeping the aspect ratio) so that it fits inside the target size, optionally extending it to the target size.
        /// Images smaller than the target size are only scaled up if <paramref name="scaleUp"/> is true.
        /// </summary>
        /// <param name="image">The image to modify</param>
        /// <param name="width">The target width</param>
        /// <param name="height">The target height</param>
        /// <param name="scaleUp">If true, smaller images are scaled up</param>
        /// <param name="extendUsing">If not null, the image is extended to the target size using this color (also when it's not scaled)</param>
        /// <param name="gravity">The placement of the image when extending</param>
        /// <returns>False if the image has no pixels, else true</returns>
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
                if ((nh - height) >= -1)
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


        /// <summary>
        /// Read an image from a local file, or download it if the name is an http:// or https:// url (see <see cref="ReadImageWeb"/>)
        /// </summary>
        /// <param name="filename">The file name or url</param>
        /// <returns>The image (dispose it), null if the download failed with an unsuccessful status code</returns>
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

        /// <summary>
        /// Read an image from memory (any format ImageMagick can detect)
        /// </summary>
        /// <param name="data">The encoded image</param>
        /// <returns>The image (dispose it)</returns>
        /// <exception cref="MagickException">The data can't be decoded</exception>
        public static MagickImage ReadImage(ReadOnlyMemory<Byte> data)
        {
            return new MagickImage(data.Span);
        }

        /// <summary>
        /// Read an image from a local file (synchronously, the returned task is completed)
        /// </summary>
        /// <param name="filename">The file name</param>
        /// <returns>The image (dispose it)</returns>
        /// <exception cref="MagickException">The file can't be read or decoded</exception>
        public static Task<MagickImage> ReadImageLocal(String filename)
        {
            return Task.FromResult(new MagickImage(filename));
        }

        /// <summary>
        /// Download an image (using a shared http client with a 15 second timeout)
        /// </summary>
        /// <param name="url">The url</param>
        /// <returns>The image (dispose it), null if the response status code isn't successful</returns>
        public static async Task<MagickImage> ReadImageWeb(String url)
        {
            var client = WebTools.GetHttpClient(15);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;
            return new MagickImage(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        }



        /// <summary>
        /// Encode an image using its current <see cref="MagickImage.Format"/>
        /// </summary>
        /// <param name="image">The image</param>
        /// <returns>The encoded image</returns>
        public static ReadOnlyMemory<Byte> ToData(MagickImage image)
        {
            var size = (int)(image.Width * image.Height + 32);
            using var ms = new MemoryStream(size);
            image.Write(ms);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }



        /// <summary>
        /// Convert the image data to a MagickImage
        /// </summary>
        /// <param name="data">The RGBA pixels</param>
        /// <returns>A new image (dispose it)</returns>
        /// <exception cref="Exception">The data is too short for the size (thrown by ImageMagick)</exception>
        public static MagickImage ToImage(this ImageData32 data)
        {
            var w = (uint)data.Width;
            var h = (uint)data.Height;
            var img = new MagickImage(MagickColors.Black, w, h);
            try
            {
                img.ImportPixels(data.Data.Span, new PixelImportSettings(w, h, StorageType.Char, PixelMapping.RGBA));
                return img;
            }
            catch
            {
                img.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Save the image data to disc (the format is determined by the file extension)
        /// </summary>
        /// <param name="data">The RGBA pixels</param>
        /// <param name="filename">The destination file name</param>
        public static void Save(this ImageData32 data, String filename)
        {
            using var img = ToImage(data);
            img.Write(filename);
        }

        /// <summary>
        /// Save the image data to disc (the format is determined by the file extension)
        /// </summary>
        /// <param name="data">The RGBA pixels</param>
        /// <param name="filename">The destination file name</param>
        /// <returns>A task that completes when the file is written</returns>
        public static async Task SaveAsync(this ImageData32 data, String filename)
        {
            using var img = ToImage(data);
            await img.WriteAsync(filename).ConfigureAwait(false);
        }

        /// <summary>
        /// Save the image data to a stream
        /// </summary>
        /// <param name="data">The RGBA pixels</param>
        /// <param name="stream">The destination stream</param>
        /// <param name="format">The image format, defaults to png</param>
        /// <param name="keepOpen">Keep the stream open after the write (by default the stream is disposed)</param>
        public static void Save(this ImageData32 data, Stream stream, MagickFormat format = MagickFormat.Png, bool keepOpen = false)
        {
            using var ss = keepOpen ? null : stream;
            using var img = ToImage(data);
            img.Write(stream, format);
        }


    }
}
