using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for creating an image from a text prompt, see <see cref="IAiImageGenService"/>.
    /// Options that a service or model doesn't support are ignored.
    /// </summary>
    public class TextToImagePrompt
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(Prompt.ToQuoted(), ", ", Width.ToString(), 'x', Height.ToString(), Transparent ? ", transparent" : "", String.IsNullOrWhiteSpace(Model) ? "" : ", model " + Model.ToQuoted());

        /// <summary>
        /// The model to use, null or blank to use the service default (configured in the service parameters).
        /// For ComfyUI this is the name of the workflow.
        /// </summary>
        public String Model { get; set; }

        /// <summary>
        /// The desired width in pixels, 0 or less to use the default.
        /// If only one of Width and Height is set, the other one is computed using the aspect ratio of the default size.
        /// The image is created using the size (or aspect ratio) closest to this that the model supports, so the actual size may differ.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The desired height in pixels, 0 or less to use the default.
        /// If only one of Width and Height is set, the other one is computed using the aspect ratio of the default size.
        /// The image is created using the size (or aspect ratio) closest to this that the model supports, so the actual size may differ.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The description of the image to create (required).
        /// </summary>
        public String Prompt { get; set; }

        /// <summary>
        /// What to avoid in the image, ex: "blurry, watermark, text".
        /// Null or empty for none (ComfyUI: null uses the default negative prompt).
        /// Services without negative prompt support append it to the prompt as things to avoid.
        /// </summary>
        public String NegativePrompt { get; set; }

        /// <summary>
        /// If true, the image should have a transparent background.
        /// Only respected if the model supports it (if no model is specified, a model that supports transparency may be selected).
        /// </summary>
        public bool Transparent { get; set; }
    }
}
