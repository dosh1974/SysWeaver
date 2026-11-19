using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Media.Png;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Helpers for AI image generation
    /// </summary>
    public static class AiImageTools
    {
        /// <summary>
        /// Add information chunks (creation time, software, source, description, author and title) to a png image
        /// </summary>
        /// <param name="png">The png image data</param>
        /// <param name="source">The source of the image, typically the model and options used</param>
        /// <param name="prompt">The prompt used to generate the image</param>
        /// <param name="title">Optional title of the image</param>
        /// <param name="author">Optional author of the image (user that created it)</param>
        /// <returns>The new png data</returns>
        public static ReadOnlyMemory<Byte> AddPngInfo(ReadOnlyMemory<Byte> png, String source, String prompt, String title, String author)
        {
            List<PngChunk> chunks;
            using (var s = png.AsStream())
                chunks = PngTools.ReadChunks(s).ToList();
            List<PngChunk> add = new List<PngChunk>(10)
            {
                PngTools.SetCreationTimeInfo(DateTime.UtcNow),
                PngTools.CreateInformationChunk(PngKeywords.Software, EnvInfo.AppDisplayName),
                PngTools.CreateInformationChunk(PngKeywords.Source, (source ?? "").Trim().Replace("  ", " ").Replace("  ", " ")),
                PngTools.CreateInformationChunk(PngKeywords.Description, prompt),
            };
            if (!String.IsNullOrEmpty(author))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Author, author));
            if (!String.IsNullOrEmpty(title))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Title, title));
            chunks.InsertRange(1, add);
            return PngTools.MakePng(chunks);
        }

        /// <summary>
        /// Get the size to use for an image, using a default size if a size isn't specified.
        /// If only the width or height is specified, the other one is computed using the aspect ratio of the default size.
        /// </summary>
        /// <param name="width">The requested width, 0 or less to use the default</param>
        /// <param name="height">The requested height, 0 or less to use the default</param>
        /// <param name="defaultWidth">The default width (1280 if 0 or less)</param>
        /// <param name="defaultHeight">The default height (720 if 0 or less)</param>
        /// <returns>The width and height to use (always positive)</returns>
        public static (int Width, int Height) GetImageSize(int width, int height, int defaultWidth = 1280, int defaultHeight = 720)
        {
            if (defaultWidth <= 0)
                defaultWidth = 1280;
            if (defaultHeight <= 0)
                defaultHeight = 720;
            if (width > 0)
            {
                if (height > 0)
                    return (width, height);
                return (width, Math.Max(1, (int)Math.Round((double)width * defaultHeight / defaultWidth)));
            }
            if (height > 0)
                return (Math.Max(1, (int)Math.Round((double)height * defaultWidth / defaultHeight)), height);
            return (defaultWidth, defaultHeight);
        }

        /// <summary>
        /// Get the mime type of some image data from its signature (png, jpeg, gif, webp and bmp)
        /// </summary>
        /// <param name="data">The image data (or the start of it)</param>
        /// <returns>The mime type, ex: "image/png", or null if unknown</returns>
        public static String GetImageMime(ReadOnlySpan<Byte> data)
        {
            if (data.StartsWith((ReadOnlySpan<Byte>)[0x89, 0x50, 0x4e, 0x47]))
                return "image/png";
            if (data.StartsWith((ReadOnlySpan<Byte>)[0xff, 0xd8, 0xff]))
                return "image/jpeg";
            if (data.StartsWith("GIF8"u8))
                return "image/gif";
            if ((data.Length >= 12) && data.StartsWith("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
                return "image/webp";
            if (data.StartsWith("BM"u8))
                return "image/bmp";
            return null;
        }

        /// <summary>
        /// Get the data of a source image (ex: for image editing), loading it if it's a link.
        /// </summary>
        /// <param name="image">The image, either with data (if Mime is null it's detected from the data) or a link (Data is null) where Name is an "http://", "https://" or "data:" url</param>
        /// <returns>A file with the data, the mime type (never null) and a file name with an extension (never null)</returns>
        /// <exception cref="ArgumentNullException">The image is null</exception>
        /// <exception cref="ArgumentException">The image is empty or not a supported link</exception>
        public static async Task<MemoryFile> GetSourceImage(MemoryFile image)
        {
            ArgumentNullException.ThrowIfNull(image);
            var data = image.Data;
            var name = image.Name;
            var mime = image.Mime;
            if (data == null)
            {
                if (String.IsNullOrEmpty(name))
                    throw new ArgumentException("The source image has no data and no url!", nameof(image));
                if (!(name.FastStartsWith("data:") || name.FastStartsWith("http://") || name.FastStartsWith("https://")))
                    throw new ArgumentException("The source image link must be an \"http://\", \"https://\" or \"data:\" url, not " + name.ToQuoted(), nameof(image));
                var f = await LoadImage(name, null).ConfigureAwait(false);
                data = f.Data;
                name = f.Name;
                mime = f.Mime;
            }
            if ((data?.Length ?? 0) <= 0)
                throw new ArgumentException("The source image is empty!", nameof(image));
            var detected = GetImageMime(data);
            if (detected != null)
                mime = detected;
            else if (String.IsNullOrEmpty(mime) || mime.FastEquals(MimeTypeMap.Data))
                mime = MimeTypeMap.Png;
            var ext = mime switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => MimeTypeMap.TryGetExtensions(mime, out var exts) ? exts.FirstOrDefault() : null,
            };
            if (String.IsNullOrEmpty(ext))
                ext = ".png";
            name = String.IsNullOrEmpty(name) || name.FastStartsWith("data:") ? "image" : System.IO.Path.GetFileNameWithoutExtension(name.SplitLast('/'));
            if (String.IsNullOrEmpty(name))
                name = "image";
            return new MemoryFile(name + ext, mime, data);
        }

        /// <summary>
        /// Combine a prompt and a negative prompt, for models that doesn't support negative prompts
        /// </summary>
        /// <param name="prompt">The prompt</param>
        /// <param name="negativePrompt">The negative prompt (things to avoid), null or blank for none</param>
        /// <returns>The prompt (with the things to avoid appended)</returns>
        public static String CombinePrompt(String prompt, String negativePrompt)
        {
            if (String.IsNullOrWhiteSpace(negativePrompt))
                return prompt;
            return String.Concat(prompt?.TrimEnd(), "\n\nAvoid: ", negativePrompt.Trim());
        }

        /// <summary>
        /// Get a file name for a generated image, ex: "gpt-image-2_20261118_203300.png"
        /// </summary>
        /// <param name="model">The model used</param>
        /// <param name="ext">The extension (including the '.')</param>
        /// <returns>A file name</returns>
        public static String GetImageName(String model, String ext = ".png")
        {
            var m = String.IsNullOrEmpty(model) ? "image" : model;
            var s = String.Create(m.Length, m, (d, src) =>
            {
                for (int i = 0; i < src.Length; ++i)
                {
                    var c = src[i];
                    d[i] = (c < 32) || ("/\\:*?\"<>| ".IndexOf(c) >= 0) ? '_' : c;
                }
            });
            return String.Concat(s, DateTime.UtcNow.ToString("_yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture), ext);
        }

        /// <summary>
        /// Load an image from an url
        /// </summary>
        /// <param name="s">The url to the image, can be:
        /// - A data uri, "data:image/png;base64,xxxxx".
        /// - A fully qualified uri, "http://www.xxx.com/xxx/xxx.png".
        /// - A relative uri (relative to the request), "../data/xxx.png".
        /// </param>
        /// <param name="request">The request (used for relative uri's)</param>
        /// <returns>The image file</returns>
        public static async Task<MemoryFile> LoadImage(String s, HttpServerRequest request)
        {
            if (s.FastStartsWith("data:"))
                return MemoryFile.FromDataUri(s);
            var fns = s.SplitLast('/');
            var ext = fns.SplitLast('.');
            if (s.FastStartsWith("http://") || s.FastStartsWith("https://"))
            {
                var data = await WebTools.HttpClient.GetByteArrayAsync(s).ConfigureAwait(false);
                var mime = MimeTypeMap.TryGetExtensions(ext, out var xx) ? xx.FirstOrDefault() : null;
                return new MemoryFile(fns, mime ?? MimeTypeMap.Data, data);
            }
            s = request.MakeRequestAbsolute(s);
            var rr = await request.Server.InternalRead(s, request.Session).ConfigureAwait(false);
            if (rr == null)
                throw new Exception("Don't know how to read the file " + s.ToQuoted());
            {
                var data = rr.Item1;
                var mime = MimeTypeMap.TryGetMimeType(ext, out var xx) ? xx.Item1 : null;
                return new MemoryFile(fns, mime ?? MimeTypeMap.Data, data.Span);
            }
        }
    }
}
