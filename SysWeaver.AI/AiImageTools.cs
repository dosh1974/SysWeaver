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
        /// <param name="request">Optional request (used to get the author)</param>
        /// <returns>The new png data</returns>
        public static ReadOnlyMemory<Byte> AddPngInfo(ReadOnlyMemory<Byte> png, String source, String prompt, String title, HttpServerRequest request)
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
            var user = request?.Session?.Auth?.NickName;
            if (user != null)
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Author, user));
            if (!String.IsNullOrEmpty(title))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Title, title));
            chunks.InsertRange(1, add);
            return PngTools.MakePng(chunks);
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
