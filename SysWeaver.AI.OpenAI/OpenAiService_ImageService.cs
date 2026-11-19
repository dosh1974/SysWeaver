using OpenAI.Images;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.AI
{

    public sealed partial class OpenAiService : IAiImageGenService, IAiImageEditService
    {
#pragma warning disable OPENAI001

        /// <summary>
        /// Create an image from a text prompt.
        /// If no model is specified, the default image model is used (or the most suitable available model if it's not available, or doesn't support a requested transparent background).
        /// The size closest to the requested size that the model supports is used (gpt-image-2 and later supports almost any size, older models only a few sizes).
        /// The negative prompt is appended to the prompt (as things to avoid).
        /// </summary>
        /// <param name="prompt">The prompt</param>
        /// <returns>The png image</returns>
        public async Task<MemoryFile> TextToImage(TextToImagePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var text = GetPromptText(prompt);
            using var _ = await (ImageGenLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false);
            using var __ = PerfMon.Track(nameof(TextToImage));
            var model = await GetImageModel(prompt.Model, false, prompt.Transparent).ConfigureAwait(false);
            var (w, h) = GetImageSize(prompt.Width, prompt.Height);
            var (sw, sh) = GetOpenAiImageSize(model, w, h);
            var size = new GeneratedImageSize(sw, sh);
            ImageGenerationOptions options;
            if (IsDallE(model))
                options = new ImageGenerationOptions
                {
                    Size = size,
                    Quality = IsDallE2(model) ? (GeneratedImageQuality?)null : GeneratedImageQuality.High,
                    ResponseFormat = GeneratedImageFormat.Bytes,
                };
            else
                options = new ImageGenerationOptions
                {
                    Size = size,
                    Quality = "high",
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                    Background = prompt.Transparent && SupportsTransparency(model) ? GeneratedImageBackground.Transparent : (GeneratedImageBackground?)null,
                };
            var image = await CreateImageClient(model).GenerateImageAsync(text, options).ConfigureAwait(false);
            return ToImageFile(image, model, sw, sh, prompt);
        }

        /// <summary>
        /// Create an image from a source image and a text prompt (image edit).
        /// If no model is specified, the default image edit model is used (or the most suitable available model if it's not available, can't edit images or doesn't support a requested transparent background).
        /// The size closest to the requested size that the model supports is used (gpt-image-2 and later supports almost any size, older models only a few sizes).
        /// The negative prompt is appended to the prompt (as things to avoid).
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
            var model = await GetImageModel(prompt.Model, true, prompt.Transparent).ConfigureAwait(false);
            var (w, h) = GetImageSize(prompt.Width, prompt.Height);
            var (sw, sh) = GetOpenAiImageSize(model, w, h);
            var size = new GeneratedImageSize(sw, sh);
            ImageEditOptions options;
            if (IsDallE(model))
                options = new ImageEditOptions
                {
                    Size = size,
                    ResponseFormat = GeneratedImageFormat.Bytes,
                };
            else
                options = new ImageEditOptions
                {
                    Size = size,
                    Quality = "high",
                    OutputFileFormat = GeneratedImageFileFormat.Png,
                    Background = prompt.Transparent && SupportsTransparency(model) ? GeneratedImageBackground.Transparent : (GeneratedImageBackground?)null,
                };
            GeneratedImage image;
            using (var ms = new MemoryStream(source.Data, false))
                image = await CreateImageClient(model).GenerateImageEditAsync(ms, source.Name, text, options).ConfigureAwait(false);
            return ToImageFile(image, model, sw, sh, prompt);
        }

        static String GetPromptText(TextToImagePrompt prompt)
        {
            if (String.IsNullOrWhiteSpace(prompt.Prompt))
                throw new ArgumentException("A prompt is required!", nameof(prompt));
            return AiImageTools.CombinePrompt(prompt.Prompt, prompt.NegativePrompt);
        }

        static MemoryFile ToImageFile(GeneratedImage image, String model, int width, int height, TextToImagePrompt prompt)
        {
            var bytes = image?.ImageBytes;
            if (bytes == null)
                throw new Exception("No image was generated by " + model.ToQuoted());
            var png = AiImageTools.AddPngInfo(bytes.ToMemory(), String.Concat(model, ' ', width.ToString(CultureInfo.InvariantCulture), 'x', height.ToString(CultureInfo.InvariantCulture), prompt.Transparent ? " transparent" : ""), prompt.Prompt, null, null);
            return new MemoryFile(AiImageTools.GetImageName(model), MimeTypeMap.Png, png.ToArray());
        }

        #region Model selection

        static bool IsDallE(String model) => model.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase);

        static bool IsDallE2(String model) => model.StartsWith("dall-e-2", StringComparison.OrdinalIgnoreCase);

        static bool IsDallE3(String model) => model.StartsWith("dall-e-3", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Get the version of a gpt image model, ex: "gpt-image-1.5" => 1.5, "gpt-image-2.5-flare" => 2.5.
        /// </summary>
        /// <returns>The version, 0 if not a gpt image model (or the version is unknown)</returns>
        static double GetGptImageVersion(String model)
        {
            const String prefix = "gpt-image-";
            if (!model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return 0;
            var s = model.AsSpan(prefix.Length);
            int l = 0;
            while ((l < s.Length) && (Char.IsAsciiDigit(s[l]) || (s[l] == '.')))
                ++l;
            //  Don't treat a date snapshot ("gpt-image-1-2025-04-23") as part of the version
            return double.TryParse(s.Slice(0, l).TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>
        /// True if the model can create images with a transparent background
        /// </summary>
        static bool SupportsTransparency(String model)
            => model.StartsWith("gpt-image-1.5", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("gpt-image-2.5", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        protected override int RateImageModel(String model, bool edit, bool transparent)
        {
            if (transparent && !SupportsTransparency(model))
                return -1;
            if (IsDallE3(model))
                return edit ? -1 : 20;
            if (IsDallE2(model))
                return 10;
            var v = GetGptImageVersion(model);
            int rate;
            if (v > 0)
                rate = 1000 + (int)(v * 100);
            else if (model.StartsWith("chatgpt-image", StringComparison.OrdinalIgnoreCase))
                rate = 500;
            else
                return -1;
            //  Prefer the full models, and the model alias over a dated snapshot
            if (model.Contains("mini", StringComparison.OrdinalIgnoreCase))
                rate -= 60;
            if (System.Text.RegularExpressions.Regex.IsMatch(model, @"-\d{4}-\d{2}-\d{2}$"))
                rate -= 5;
            return rate;
        }

        #endregion//Model selection

        #region Size selection

        static readonly (int W, int H)[] DallE3Sizes = [(1024, 1024), (1792, 1024), (1024, 1792)];

        static readonly (int W, int H)[] GptImage1Sizes = [(1024, 1024), (1536, 1024), (1024, 1536)];

        /// <summary>
        /// Get the size from a list of sizes with the aspect ratio closest to the desired size
        /// </summary>
        static (int W, int H) Closest((int W, int H)[] sizes, int w, int h)
        {
            var a = Math.Log((double)w / h);
            var best = sizes[0];
            var bestDiff = double.MaxValue;
            foreach (var s in sizes)
            {
                var d = Math.Abs(Math.Log((double)s.W / s.H) - a);
                if (d >= bestDiff)
                    continue;
                bestDiff = d;
                best = s;
            }
            return best;
        }

        /// <summary>
        /// Get the image size to request, the closest size to the desired size that the model supports.
        /// gpt-image-2 and later: both sides a multiple of 16, at most 3840, an aspect ratio of at most 3:1 and between 655,360 and 8,294,400 pixels.
        /// </summary>
        static (int W, int H) GetOpenAiImageSize(String model, int w, int h)
        {
            if (IsDallE2(model))
                return (1024, 1024);
            if (IsDallE3(model))
                return Closest(DallE3Sizes, w, h);
            if (GetGptImageVersion(model) < 2)
                return Closest(GptImage1Sizes, w, h);
            const double maxSide = 3840;
            const double minPixels = 655_360;
            const double maxPixels = 8_294_400;
            var aspect = Math.Clamp((double)w / h, 1.0 / 3.0, 3.0);
            var pixels = Math.Clamp((double)w * h, minPixels, maxPixels);
            var fw = Math.Sqrt(pixels * aspect);
            var fh = fw / aspect;
            var scale = Math.Min(1.0, maxSide / Math.Max(fw, fh));
            fw *= scale;
            fh *= scale;
            int iw = Math.Max(16, (int)Math.Round(fw / 16) * 16);
            int ih = Math.Max(16, (int)Math.Round(fh / 16) * 16);
            //  Rounding may get us out of range, adjust in steps of 16
            while ((Math.Max(iw, ih) > maxSide) || ((double)iw * ih > maxPixels))
            {
                if (iw >= ih)
                    iw -= 16;
                else
                    ih -= 16;
            }
            while ((double)iw * ih < minPixels)
            {
                if ((iw <= ih) && (iw + 16 <= maxSide))
                    iw += 16;
                else
                    ih += 16;
            }
            return (iw, ih);
        }

        #endregion//Size selection

#pragma warning restore OPENAI001
    }
}
