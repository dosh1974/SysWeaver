using Google.GenAI.Types;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed partial class GoogleAiService
    {

        static readonly String[] AspectRatios =
        [
            null,
            "1:1",
            "3:4",
            "4:3",
            "9:16",
            "16:9",
        ];

        static bool IsImagen(String model) => model.FastStartsWith("imagen");

        static bool SupportsImageSize(String model) => model.FastStartsWith("gemini-3");

        /// <summary>
        /// Make sure that the image is a png
        /// </summary>
        static ReadOnlyMemory<Byte> ToPng(Byte[] data, String mime)
        {
            if (mime.FastEquals("image/png"))
                return data;
            using var bmp = SKBitmap.Decode(data);
            if (bmp == null)
                throw new Exception("Failed to decode the generated image of type " + mime.ToQuoted());
            using var img = SKImage.FromBitmap(bmp);
            using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
            return enc.ToArray();
        }

        static Blob GetImage(GenerateContentResponse r)
        {
            var br = r.PromptFeedback?.BlockReason;
            if (br != null)
                throw new Exception("Model refused to generate the image: " + br.Value);
            var c = r.Candidates?.FirstOrDefault();
            var img = c?.Content?.Parts?.FirstOrDefault(x => (x.InlineData?.MimeType?.FastStartsWith("image/") ?? false) && ((x.InlineData.Data?.Length ?? 0) > 0))?.InlineData;
            if (img != null)
                return img;
            var reason = c?.FinishReason?.Value;
            var text = r.Text;
            throw new Exception("No image was generated" + (reason == null ? "" : " (" + reason + ")") + (String.IsNullOrEmpty(text) ? "" : ": " + text));
        }

        GenerateContentConfig CreateImageConfig(String model, GoogleImagePrompt p)
            => new GenerateContentConfig
            {
                ResponseModalities = [Modality.Image.Value],
                ImageConfig = new ImageConfig
                {
                    AspectRatio = AspectRatios[(int)p.Aspect],
                    ImageSize = (p.Large && SupportsImageSize(model)) ? "4K" : null,
                },
            };

        /// <summary>
        /// Generate an image
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi("debug/" + nameof(ImageGenerate))]
        [WebApiAuth(Roles.Debug)]
        [WebApiRaw("image/png", true)]
        public async Task<ReadOnlyMemory<Byte>> ImageGenerate(GoogleImagePrompt p, HttpServerRequest request)
        {
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(ImageGenerate));
            var model = String.IsNullOrEmpty(p.Model) ? DefaultImageModel : p.Model;
            ReadOnlyMemory<Byte> png;
            if (IsImagen(model))
            {
                var r = await Client.Models.GenerateImagesAsync(model, p.Prompt, new GenerateImagesConfig
                {
                    NumberOfImages = 1,
                    AspectRatio = AspectRatios[(int)p.Aspect],
                    OutputMimeType = "image/png",
                    ImageSize = p.Large ? "2K" : null,
                }).ConfigureAwait(false);
                var gi = r.GeneratedImages?.FirstOrDefault();
                var img = gi?.Image;
                if ((img?.ImageBytes?.Length ?? 0) <= 0)
                    throw new Exception("No image was generated" + (String.IsNullOrEmpty(gi?.RaiFilteredReason) ? "" : ": " + gi.RaiFilteredReason));
                png = ToPng(img.ImageBytes, img.MimeType ?? "image/png");
            }
            else
            {
                var r = await Client.Models.GenerateContentAsync(model, p.Prompt, CreateImageConfig(model, p)).ConfigureAwait(false);
                var img = GetImage(r);
                png = ToPng(img.Data, img.MimeType);
            }
            return AiImageTools.AddPngInfo(png, String.Concat(model, ' ', p.Aspect, p.Large ? " Large" : ""), p.Prompt, p.Title, request);
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
        public async Task<ReadOnlyMemory<Byte>> ImageEdit(GoogleImageEditPrompt p, HttpServerRequest request)
        {
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(ImageEdit));
            var model = String.IsNullOrEmpty(p.Model) ? DefaultImageModel : p.Model;
            if (IsImagen(model))
                throw new Exception("Image editing is not supported by the model " + model.ToQuoted() + ", use a gemini image model");
            var file = await AiImageTools.LoadImage(p.SourceImage, request).ConfigureAwait(false);
            var content = new Content
            {
                Role = GoogleModels.RoleUser,
                Parts =
                [
                    new Part { InlineData = new Blob { Data = file.Data, MimeType = file.Mime } },
                    new Part { Text = p.Prompt },
                ],
            };
            var r = await Client.Models.GenerateContentAsync(model, content, CreateImageConfig(model, p)).ConfigureAwait(false);
            var img = GetImage(r);
            var png = ToPng(img.Data, img.MimeType);
            return AiImageTools.AddPngInfo(png, String.Concat(model, ' ', p.Aspect, p.Large ? " Large" : ""), p.Prompt, p.Title, request);
        }

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
        async Task<String> GenerateImage(GoogleImagePrompt prompt, HttpServerRequest request)
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
        async Task<String> EditImage(GoogleImageEditPrompt prompt, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as AiToolContext;
            if (c == null)
                return null;
            var bin = await ImageEdit(prompt, request).ConfigureAwait(false);
            return await StoreGeneratedImage(c, request, bin, prompt.Title).ConfigureAwait(false);
        }

        static readonly MethodInfo Method_GenerateImage = typeof(GoogleAiService).GetMethod(nameof(GenerateImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_EditImage = typeof(GoogleAiService).GetMethod(nameof(EditImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

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
    }
}
