using System;

namespace SysWeaver.AI
{
    public class GoogleImagePrompt
    {
        /// <summary>
        /// The image description, make sure to use "Prompt" and not "prompt".
        /// </summary>
        public String Prompt;

        /// <summary>
        /// Create a larger image, think 4K vs 1080p.
        /// </summary>
        [AiOptional]
        public bool Large = false;

        /// <summary>
        /// The desired aspect ratio
        /// </summary>
        [AiOptional]
        public AiImageAspectRatios Aspect = AiImageAspectRatios.Square;

        /// <summary>
        /// The title of this image, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

        /// <summary>
        /// The image model to use, only set if the user required a specfifc model.
        /// Valid models are:
        /// * "gemini-2.5-flash-image" (default)
        /// * "gemini-3-pro-image-preview" (highest quality, supports large images)
        /// * "imagen-4.0-generate-001" (generation only)
        /// * "imagen-4.0-ultra-generate-001" (generation only)
        /// </summary>
        [AiOptional]
        public String Model;
    }

    public class GoogleImageEditPrompt : GoogleImagePrompt
    {
        /// <summary>
        /// The URL to an image to give to the image generation model.
        /// This could be:
        /// - A data uri, "data:image/png;base64,xxxxx".
        /// - A fully qualified uri, "http://www.xxx.com/xxx/xxx.png".
        /// - A relative uri (as supplied), "../data/xxx.png".
        /// </summary>
        public String SourceImage;
    }

}
