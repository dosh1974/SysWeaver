using Google.GenAI.Types;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class GoogleAiService : IAiImageGenService, IAiImageEditService
    {
        /// <summary>
        /// Create an image from a text prompt.
        /// If no model is specified, the default image model is used (or the most suitable available model if it's not available).
        /// The aspect ratio closest to the requested size is used, and the resolution (1K, 2K or 4K) closest to it if the model supports it.
        /// Transparency isn't supported and the negative prompt is appended to the prompt (as things to avoid).
        /// </summary>
        /// <param name="prompt">The prompt</param>
        /// <returns>The png image</returns>
        public async Task<MemoryFile> TextToImage(TextToImagePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var text = GetPromptText(prompt);
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(TextToImage));
            var model = await GetImageModel(prompt.Model, false).ConfigureAwait(false);
            var (w, h) = GetImageSize(prompt.Width, prompt.Height);
            ReadOnlyMemory<Byte> png;
            String aspect, size;
            if (IsImagen(model))
            {
                aspect = ClosestAspect(ImagenAspects, w, h);
                size = Math.Max(w, h) > 1536 ? "2K" : "1K";
                var r = await Client.Models.GenerateImagesAsync(model, text, new GenerateImagesConfig
                {
                    NumberOfImages = 1,
                    AspectRatio = aspect,
                    OutputMimeType = "image/png",
                    ImageSize = size,
                }).ConfigureAwait(false);
                var gi = r.GeneratedImages?.FirstOrDefault();
                var img = gi?.Image;
                if ((img?.ImageBytes?.Length ?? 0) <= 0)
                    throw new Exception("No image was generated" + (String.IsNullOrEmpty(gi?.RaiFilteredReason) ? "" : ": " + gi.RaiFilteredReason));
                png = ToPng(img.ImageBytes, img.MimeType ?? "image/png");
            }
            else
            {
                var r = await Client.Models.GenerateContentAsync(model, text, CreateImageConfig(model, w, h, out aspect, out size)).ConfigureAwait(false);
                var img = GetImage(r);
                png = ToPng(img.Data, img.MimeType);
            }
            return ToImageFile(png, model, aspect, size, prompt);
        }

        /// <summary>
        /// Create an image from a source image and a text prompt (image edit), using a Gemini image model (Imagen models can't edit images).
        /// If no model is specified, the default image edit model is used (or the most suitable available model if it's not available or can't edit images).
        /// The aspect ratio closest to the requested size is used, and the resolution (1K, 2K or 4K) closest to it if the model supports it.
        /// Transparency isn't supported and the negative prompt is appended to the prompt (as things to avoid).
        /// </summary>
        /// <param name="prompt">The prompt and the source image</param>
        /// <returns>The png image</returns>
        public async Task<MemoryFile> ImageToImage(ImageToImagePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var text = GetPromptText(prompt);
            var source = await AiImageTools.GetSourceImage(prompt.SourceImage).ConfigureAwait(false);
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(ImageToImage));
            var model = await GetImageModel(prompt.Model, true).ConfigureAwait(false);
            if (IsImagen(model))
                throw new Exception("Image editing is not supported by the model " + model.ToQuoted() + ", use a gemini image model");
            var (w, h) = GetImageSize(prompt.Width, prompt.Height);
            var content = new Content
            {
                Role = GoogleModels.RoleUser,
                Parts =
                [
                    new Part { InlineData = new Blob { Data = source.Data, MimeType = source.Mime } },
                    new Part { Text = text },
                ],
            };
            var r = await Client.Models.GenerateContentAsync(model, content, CreateImageConfig(model, w, h, out var aspect, out var size)).ConfigureAwait(false);
            var img = GetImage(r);
            return ToImageFile(ToPng(img.Data, img.MimeType), model, aspect, size, prompt);
        }

        static String GetPromptText(TextToImagePrompt prompt)
        {
            if (String.IsNullOrWhiteSpace(prompt.Prompt))
                throw new ArgumentException("A prompt is required!", nameof(prompt));
            return AiImageTools.CombinePrompt(prompt.Prompt, prompt.NegativePrompt);
        }

        static MemoryFile ToImageFile(ReadOnlyMemory<Byte> png, String model, String aspect, String size, TextToImagePrompt prompt)
        {
            png = AiImageTools.AddPngInfo(png, String.Concat(model, ' ', aspect, size == null ? "" : " " + size), prompt.Prompt, null, null);
            return new MemoryFile(AiImageTools.GetImageName(model), MimeTypeMap.Png, png.ToArray());
        }

        /// <summary>
        /// The aspect ratios supported by the Gemini image models
        /// </summary>
        static readonly String[] GeminiAspects = ["1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9"];

        /// <summary>
        /// The aspect ratios supported by the Imagen models
        /// </summary>
        static readonly String[] ImagenAspects = ["1:1", "3:4", "4:3", "9:16", "16:9"];

        /// <summary>
        /// Get the aspect ratio (ex: "16:9") closest to a size
        /// </summary>
        static String ClosestAspect(String[] aspects, int w, int h)
        {
            var a = Math.Log((double)w / h);
            String best = aspects[0];
            var bestDiff = double.MaxValue;
            foreach (var x in aspects)
            {
                var i = x.IndexOf(':');
                var d = Math.Abs(Math.Log(double.Parse(x.AsSpan(0, i), CultureInfo.InvariantCulture) / double.Parse(x.AsSpan(i + 1), CultureInfo.InvariantCulture)) - a);
                if (d >= bestDiff)
                    continue;
                bestDiff = d;
                best = x;
            }
            return best;
        }

        /// <summary>
        /// Create the config for a Gemini image model, using the aspect ratio and resolution closest to the size
        /// </summary>
        static GenerateContentConfig CreateImageConfig(String model, int w, int h, out String aspect, out String size)
        {
            aspect = ClosestAspect(GeminiAspects, w, h);
            size = null;
            if (SupportsImageSize(model))
            {
                var m = Math.Max(w, h);
                size = m > 3072 ? "4K" : (m > 1536 ? "2K" : "1K");
            }
            return new GenerateContentConfig
            {
                ResponseModalities = [Modality.Image.Value],
                ImageConfig = new ImageConfig
                {
                    AspectRatio = aspect,
                    ImageSize = size,
                },
            };
        }

        /// <summary>
        /// Get the version of a model, ex: "gemini-3.1-flash-image" => 3.1, "imagen-4.0-generate-001" => 4
        /// </summary>
        /// <returns>The version, 0 if unknown</returns>
        static double GetModelVersion(String model)
        {
            var i = model.IndexOf('-');
            if (i < 0)
                return 0;
            var s = model.AsSpan(i + 1);
            int l = 0;
            while ((l < s.Length) && (Char.IsAsciiDigit(s[l]) || (s[l] == '.')))
                ++l;
            return double.TryParse(s.Slice(0, l).TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <inheritdoc />
        protected override int RateImageModel(String model, bool edit, bool transparent)
        {
            int rate;
            if (IsImagen(model))
            {
                //  Imagen can't edit images
                if (edit)
                    return -1;
                rate = 200 + (int)(GetModelVersion(model) * 10);
                if (model.Contains("ultra", StringComparison.OrdinalIgnoreCase))
                    rate += 5;
                if (model.Contains("fast", StringComparison.OrdinalIgnoreCase))
                    rate -= 5;
                return rate;
            }
            if (!model.FastStartsWith("gemini"))
                return -1;
            rate = 1000 + (int)(GetModelVersion(model) * 100);
            if (model.Contains("-pro", StringComparison.OrdinalIgnoreCase))
                rate += 30;
            if (model.Contains("-lite", StringComparison.OrdinalIgnoreCase))
                rate -= 30;
            if (model.Contains("preview", StringComparison.OrdinalIgnoreCase) || model.Contains("exp", StringComparison.OrdinalIgnoreCase))
                rate -= 5;
            return rate;
        }
    }
}
