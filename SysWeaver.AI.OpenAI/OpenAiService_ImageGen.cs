using OpenAI.Images;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    public sealed partial class OpenAiService
    {




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

        #region Image tools

        /// <summary>
        /// Generate an image using generative AI.
        /// Generating images are expensive, try to solve problems without generating an image.
        /// Don't generate large images unless specified.
        /// </summary>
        /// <param name="prompt">Paramaters for the generation</param>
        /// <param name="request"></param>
        /// <returns>An url to the generated png image</returns>
        [AiTool("🖼️✨")]
        async Task<String> GenerateImage(OpenAiImagePrompt prompt, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as AiToolContext;
            if (c == null)
                return null;
            var bin = await ImageGenerate(prompt, request).ConfigureAwait(false);
            return await StoreGeneratedImage(c, request, bin, prompt.Title).ConfigureAwait(false);
        }

        /// <summary>
        /// Edit an image using generative AI.
        /// Generating images are expensive, try to solve problems without generating an image.
        /// Don't generate large images unless specified.
        /// </summary>
        /// <param name="prompt">Source image and paramaters for the generation</param>
        /// <param name="request"></param>
        /// <returns>An url to the generated png image</returns>
        [AiTool("🖼️✂️")]
        async Task<String> EditImage(OpenAiImageEditPrompt prompt, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as AiToolContext;
            if (c == null)
                return null;
            var bin = await ImageEdit(prompt, request).ConfigureAwait(false);
            return await StoreGeneratedImage(c, request, bin, prompt.Title).ConfigureAwait(false);
        }

        static readonly MethodInfo Method_GenerateImage = typeof(OpenAiService).GetMethod(nameof(GenerateImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_EditImage = typeof(OpenAiService).GetMethod(nameof(EditImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GenerateImage(IAiChatSession s) =>
            s.AddTool(this, Method_GenerateImage, null, PerfMon, "Debug,Content");

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_EditImage(IAiChatSession s) =>
            s.AddTool(this, Method_EditImage, null, PerfMon, "Debug,Content");

        protected override void AddDefaultTools(IAiChatSession s)
        {
            base.AddDefaultTools(s);
            AddTool_GenerateImage(s);
            AddTool_EditImage(s);
        }

        #endregion//Image tools



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
                    Size = ImageSizes[(int)p.Aspect],
                    Style = p.Vivid ? GeneratedImageStyle.Vivid : GeneratedImageStyle.Natural,
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                    //ResponseFormat = GeneratedImageFormat.Bytes,
                };
           
            GeneratedImage image;
            image = await client.GenerateImageAsync(p.Prompt, options).ConfigureAwait(false);

            return AiImageTools.AddPngInfo(image.ImageBytes.ToMemory(), String.Concat(model, ' ', options.Quality, ' ', options.Style), p.Prompt, p.Title, request);
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
                    Size = ImageSizes[(int)p.Aspect],
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                };
            var file = await AiImageTools.LoadImage(p.SourceImage, request).ConfigureAwait(false);
            GeneratedImage image;
            {
                using var ms = new MemoryStream(file.Data, false);
                image = await client.GenerateImageEditAsync(ms, file.Name, p.Prompt, options).ConfigureAwait(false);
            }

            return AiImageTools.AddPngInfo(image.ImageBytes.ToMemory(), String.Concat(model, ' ', options.Quality), p.Prompt, p.Title, request);
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
                        Size = ImageSizes1[(int)p.Aspect],
                    }
            },
            { 
                "gpt-image-1.5",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Aspect],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {   
                "gpt-image-2",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2[(int)p.Aspect],
                    }
                },
                {
                "gpt-image-2.5-sunburst",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2_5[(int)p.Aspect],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2.5-flare",
                    p => new ImageGenerationOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes2_5[(int)p.Aspect],
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
                        Size = ImageSizes[(int)p.Aspect],
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
                        Size = ImageSizes1[(int)p.Aspect],
                    }
            },
            {
                "gpt-image-1.5",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = ImageSizes1[(int)p.Aspect],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Large ? ImageSizes2 : ImageSizes2s)[(int)p.Aspect],
                    }
                },
                {
                "gpt-image-2.5-sunburst",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Large ? ImageSizes2_5 : ImageSizes2_5s)[(int)p.Aspect],
                        Background = Backgrounds[(int)p.Background],
                    }
            },
            {
                "gpt-image-2.5-flare",
                    p => new ImageEditOptions
                    {
                        Quality = p.HighQuality ? "high" : "medium",
                        Size = (p.Large ? ImageSizes2_5 : ImageSizes2_5s)[(int)p.Aspect],
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
                        Size = ImageSizes[(int)p.Aspect],
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
