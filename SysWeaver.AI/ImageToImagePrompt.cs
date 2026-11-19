namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for creating an image from a source image and a text prompt, see <see cref="IAiImageEditService"/>.
    /// The prompt describes what to do with the source image, ex: "Make it a watercolor painting".
    /// The model defaults to the service default image edit model (not the text to image model).
    /// </summary>
    public class ImageToImagePrompt : TextToImagePrompt
    {
        /// <inheritdoc/>
        public override string ToString() => string.Concat(base.ToString(), ", source ", SourceImage?.ToString() ?? "null");

        /// <summary>
        /// The source image (required).
        /// Either the image data (if <see cref="MemoryFile.Mime"/> is null, it's detected from the data),
        /// or a link (<see cref="MemoryFile.Data"/> is null) where <see cref="MemoryFile.Name"/> is an "http://", "https://" or "data:" url.
        /// </summary>
        public MemoryFile SourceImage { get; set; }
    }
}
