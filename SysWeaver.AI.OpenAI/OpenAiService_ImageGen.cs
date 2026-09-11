using CommunityToolkit.HighPerformance;
using ExCSS;
using OpenAI.Images;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Media.Png;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    public sealed partial class OpenAiService
    {


        /// <summary>
        /// The model used when supplying an empty model (can be configured)
        /// </summary>
        public readonly String DefaultImageModel;


        readonly AsyncLock ImageGenLock;


        /// <summary>
        /// Create a simple Image client
        /// </summary>
        /// <param name="model">The gpt model to use, ex:
        /// "dall-e-3"
        /// </param>
        /// <returns></returns>
        public ImageClient CreateImageClient(String model = null)
        {
            if (String.IsNullOrEmpty(model))
                model = DefaultImageModel;
            return new ImageClient(model, ApiKey, Options);
        }

#pragma warning disable OPENAI001


        /// <summary>
        /// Generate an image
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi("debug/" + nameof(ImageGenerate))]
        [WebApiAuth(Roles.Debug)]
        [WebApiRaw("image/png", true)]
        public async Task<ReadOnlyMemory<Byte>> ImageGenerate(OpenAiImagePrompt p, HttpServerRequest request)
        {
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(ImageGenerate));
            var model = p.Model ?? DefaultImageModel;
            var client = CreateImageClient(model);
            ImageGenerationOptions options = null;
            if (ModelGenOptions.TryGetValue(model, out var fn))
                options = fn(p);
            else
                options = new()
                {
                    Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                    Size = ImageSizes[(int)p.Size],
                    Style = p.Vivid ? GeneratedImageStyle.Vivid : GeneratedImageStyle.Natural,
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                    //ResponseFormat = GeneratedImageFormat.Bytes,
                };
           
            GeneratedImage image;
            image = await client.GenerateImageAsync(p.Prompt, options).ConfigureAwait(false);

            BinaryData bytes = image.ImageBytes;
            var pngMem = bytes.ToMemory();
            List<PngChunk> chunks;
            using (var s = pngMem.AsStream())
                chunks = PngTools.ReadChunks(s).ToList();
            List<PngChunk> add = new List<PngChunk>(10)
            {
                PngTools.SetCreationTimeInfo(DateTime.UtcNow),
                PngTools.CreateInformationChunk(PngKeywords.Software, EnvInfo.AppDisplayName),
                PngTools.CreateInformationChunk(PngKeywords.Source, String.Concat(model, ' ', options.Quality, ' ', options.Style).Trim().Replace("  ", " ").Replace("  ", " ")),
                PngTools.CreateInformationChunk(PngKeywords.Description, p.Prompt),
            };
            var user = request?.Session?.Auth?.NickName;
            if (user != null)
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Author, user));
            if (!String.IsNullOrEmpty(p.Title))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Title, p.Title));
            chunks.InsertRange(1, add);
            pngMem = PngTools.MakePng(chunks);
            return pngMem;
        }



        /// <summary>
        /// Edit an existing image
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi("debug/" + nameof(ImageEdit))]
        [WebApiAuth(Roles.Debug)]
        [WebApiRaw("image/png", true)]
        public async Task<ReadOnlyMemory<Byte>> ImageEdit(OpenAiImageEditPrompt p, HttpServerRequest request)
        {
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(ImageEdit));
            var model = p.Model ?? DefaultImageModel;
            var client = CreateImageClient(model);
            ImageEditOptions options = null;
            if (ModelEditOptions.TryGetValue(model, out var fn))
                options = fn(p);
            else
                options = new()
                {
                    Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                    Size = ImageSizes[(int)p.Size],
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                };
            MemoryFile file = null;
            var s = p.SourceImage;
            if (s.FastStartsWith("data:"))
            {
                file = MemoryFile.FromDataUri(s);
            }else
            {
                var fns = s.SplitLast('/');
                var ext = fns.SplitLast('.');
                if (s.FastStartsWith("http://") || s.FastStartsWith("https://"))
                {
                    var data = await WebTools.HttpClient.GetByteArrayAsync(s).ConfigureAwait(false);
                    var mime = MimeTypeMap.TryGetExtensions(ext, out var xx) ? xx.FirstOrDefault() : null;
                    file = new MemoryFile(fns, mime ?? MimeTypeMap.Data, data);
                }
                else
                {
                    s = request.MakeRequestAbsolute(s);
                    var rr = await request.Server.InternalRead(s, request.Session).ConfigureAwait(false);
                    if (rr == null)
                        throw new Exception("Don't know how to read the file " + s.ToQuoted());
                    var data = rr.Item1;
                    var mime = MimeTypeMap.TryGetExtensions(ext, out var xx) ? xx.FirstOrDefault() : null;
                    file = new MemoryFile(fns, mime ?? MimeTypeMap.Data, data.Span);
                }
            }
            GeneratedImage image;
            {
                using var ms = new MemoryStream(file.Data, false);
                image = await client.GenerateImageEditAsync(ms, file.Name, p.Prompt, options).ConfigureAwait(false);
            }

            BinaryData bytes = image.ImageBytes;
            var pngMem = bytes.ToMemory();
            List<PngChunk> chunks;
            using (var pms = pngMem.AsStream())
                chunks = PngTools.ReadChunks(pms).ToList();
            List<PngChunk> add = new List<PngChunk>(10)
            {
                PngTools.SetCreationTimeInfo(DateTime.UtcNow),
                PngTools.CreateInformationChunk(PngKeywords.Software, EnvInfo.AppDisplayName),
                PngTools.CreateInformationChunk(PngKeywords.Source, String.Concat(model, ' ', options.Quality).Trim().Replace("  ", " ").Replace("  ", " ")),
                PngTools.CreateInformationChunk(PngKeywords.Description, p.Prompt),
            };
            var user = request?.Session?.Auth?.NickName;
            if (user != null)
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Author, user));
            if (!String.IsNullOrEmpty(p.Title))
                add.Add(PngTools.CreateInformationChunk(PngKeywords.Title, p.Title));
            chunks.InsertRange(1, add);
            pngMem = PngTools.MakePng(chunks);
            return pngMem;
        }

        static readonly GeneratedImageBackground[] Backgrounds = 
        [
            GeneratedImageBackground.Auto,
            GeneratedImageBackground.Opaque,
            GeneratedImageBackground.Transparent,
        ];

        static readonly IReadOnlyDictionary<String, Func<OpenAiImagePrompt, ImageGenerationOptions>> ModelGenOptions = new Dictionary<String, Func<OpenAiImagePrompt, ImageGenerationOptions>>(StringComparer.Ordinal)
        {
            { 
                "gpt-image-1", 
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                    }
            },
            { 
                "gpt-image-1.5",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {   
                "gpt-image-2",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2[(int)p.Size],
                    }
                },
                {
                "gpt-image-2.5-sunburst",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2_5[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2.5-flare",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2_5[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "dall-e-2",
                    p => new ImageGenerationOptions
                    {
                        Size = GeneratedImageSize.W1024xH1024,
                        ResponseFormat = GeneratedImageFormat.Bytes,
                    }
            },
            {
                "dall-e-3",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                        Size = ImageSizes[(int)p.Size],
                    }
            },
        }.Freeze();



        static readonly IReadOnlyDictionary<String, Func<OpenAiImageEditPrompt, ImageEditOptions>> ModelEditOptions = new Dictionary<String, Func<OpenAiImageEditPrompt, ImageEditOptions>>(StringComparer.Ordinal)
        {
            {
                "gpt-image-1",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                    }
            },
            {
                "gpt-image-1.5",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Small ? ImageSizes2s : ImageSizes2)[(int)p.Size],
                    }
                },
                {
                "gpt-image-2.5-sunburst",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Small ? ImageSizes2_5s : ImageSizes2_5)[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2.5-flare",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Small ? ImageSizes2_5s : ImageSizes2_5)[(int)p.Size],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "dall-e-2",
                    p => new ImageEditOptions
                    {
                        Size = GeneratedImageSize.W1024xH1024,
                        ResponseFormat = GeneratedImageFormat.Bytes,
                    }
            },
            {
                "dall-e-3",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                        Size = ImageSizes[(int)p.Size],
                    }
            },
        }.Freeze();

        /*
        Auto,
        Square, 1:1 aspect ratio
        Portrait, 3:4 aspect ratio
        Landscape, 4:3 aspect ratio
        TallPortrait, 9:16 aspect ratio
        WideLandscape, 16:9 aspect ratio
        */

        static readonly GeneratedImageSize[] ImageSizes = [
            GeneratedImageSize.Auto,
            GeneratedImageSize.W1024xH1024,
            GeneratedImageSize.W1024xH1792,
            GeneratedImageSize.W1792xH1024,
            GeneratedImageSize.W1024xH1792,
            GeneratedImageSize.W1792xH1024,
            ];


        static readonly GeneratedImageSize[] ImageSizes1 = [
            GeneratedImageSize.Auto,
            GeneratedImageSize.W1024xH1024,
            GeneratedImageSize.W1024xH1536,
            GeneratedImageSize.W1536xH1024,
            GeneratedImageSize.W1024xH1536,
            GeneratedImageSize.W1536xH1024,
            ];


        static readonly GeneratedImageSize[] ImageSizes2 = [
            GeneratedImageSize.Auto,
            new GeneratedImageSize(2048, 2048),
            new GeneratedImageSize(1536, 2048),
            new GeneratedImageSize(2048, 1536),
            new GeneratedImageSize(2160, 3840),
            new GeneratedImageSize(3840, 2160),
            ];

        static readonly GeneratedImageSize[] ImageSizes2_5 = [
            GeneratedImageSize.Auto,
            new GeneratedImageSize(2048, 2048),
            new GeneratedImageSize(2480, 3508),
            new GeneratedImageSize(3508, 2480),
            new GeneratedImageSize(2160, 3840),
            new GeneratedImageSize(3840, 2160),
            ];

        static int Fix16(int v)
            => (v + 15) & ~15;

        static readonly GeneratedImageSize[] ImageSizes2s = [
            GeneratedImageSize.Auto,
            new GeneratedImageSize(Fix16(2048 / 2), Fix16(2048 / 2)),
            new GeneratedImageSize(Fix16(1536 / 2), Fix16(2048 / 2)),
            new GeneratedImageSize(Fix16(2048 / 2), Fix16(1536 / 2)),
            new GeneratedImageSize(Fix16(2160 / 2), Fix16(3840 / 2)),
            new GeneratedImageSize(Fix16(3840 / 2), Fix16(2160 / 2)),
            ];

        static readonly GeneratedImageSize[] ImageSizes2_5s = [
            GeneratedImageSize.Auto,
            new GeneratedImageSize(Fix16(2048 / 2), Fix16(2048 / 2)),
            new GeneratedImageSize(Fix16(2480 / 2), Fix16(3508 / 2)),
            new GeneratedImageSize(Fix16(3508 / 2), Fix16(2480 / 2)),
            new GeneratedImageSize(Fix16(2160 / 2), Fix16(3840 / 2)),
            new GeneratedImageSize(Fix16(3840 / 2), Fix16(2160 / 2)),
            ];


#pragma warning restore OPENAI001

    }
}
