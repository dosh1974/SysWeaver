using System;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class ComfyUiService : IAiImageGenService, IAiImageEditService
    {
        /// <summary>
        /// Create an image from a text prompt using a text to image workflow.
        /// The model is the name of the workflow, if not specified the default text to image workflow is used (see <see cref="DefaultTextToImage"/>).
        /// The size is rounded to a multiple that the model accepts, the negative prompt and transparency are only respected if the workflow supports them.
        /// </summary>
        /// <param name="prompt">The prompt</param>
        /// <returns>The image (typically image/png)</returns>
        /// <exception cref="Exception">The workflow failed or wasn't found</exception>
        public async Task<MemoryFile> TextToImage(TextToImagePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var (w, h) = AiImageTools.GetImageSize(prompt.Width, prompt.Height, DefaultImageWidth, DefaultImageHeight);
            var p = new TextToImageParams
            {
                Workflow = String.IsNullOrWhiteSpace(prompt.Model) ? null : prompt.Model.Trim(),
                Prompt = prompt.Prompt,
                Width = w,
                Height = h,
                Transparent = prompt.Transparent,
            };
            if (prompt.NegativePrompt != null)
                p.NegativePrompt = prompt.NegativePrompt;
            return ToImageFile(await TextToImage(p).ConfigureAwait(false), p);
        }

        /// <summary>
        /// Create an image from a source image and a text prompt using an images to image (image edit) workflow.
        /// The model is the name of the workflow, if not specified the default images to image workflow is used (see <see cref="DefaultImagesToImage"/>).
        /// The size is rounded to a multiple that the model accepts, the negative prompt and transparency are only respected if the workflow supports them.
        /// </summary>
        /// <param name="prompt">The prompt and the source image</param>
        /// <returns>The image (typically image/png)</returns>
        /// <exception cref="Exception">The workflow failed or wasn't found</exception>
        public async Task<MemoryFile> ImageToImage(ImageToImagePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            var source = prompt.SourceImage;
            ArgumentNullException.ThrowIfNull(source, nameof(prompt.SourceImage));
            //  A data uri can't be downloaded by the server, decode it
            if ((source.Data == null) && (source.Name?.FastStartsWith("data:") ?? false))
                source = MemoryFile.FromDataUri(source.Name);
            var (w, h) = AiImageTools.GetImageSize(prompt.Width, prompt.Height, DefaultImageWidth, DefaultImageHeight);
            var p = new ImagesToImageParams
            {
                Workflow = String.IsNullOrWhiteSpace(prompt.Model) ? null : prompt.Model.Trim(),
                Prompt = prompt.Prompt,
                Width = w,
                Height = h,
                Transparent = prompt.Transparent,
                Files = [source],
            };
            if (prompt.NegativePrompt != null)
                p.NegativePrompt = prompt.NegativePrompt;
            return ToImageFile(await ImagesToImage(p).ConfigureAwait(false), p);
        }

        /// <summary>
        /// Return the file of a result or throw the error, a png image gets information chunks (the prompt, the workflow and seed)
        /// </summary>
        static MemoryFile ToImageFile(ComfyResult r, ComfyParams p)
        {
            if (r.Error != null)
                throw new Exception(r.Error);
            if (String.Equals(r.Mime, MimeTypeMap.Png, StringComparison.OrdinalIgnoreCase))
                r.Data = AiImageTools.AddPngInfo(r.Data, String.Concat("ComfyUI ", r.Workflow, " seed ", r.Seed.ToString()), p.Prompt, null, null).ToArray();
            return r;
        }
    }
}
