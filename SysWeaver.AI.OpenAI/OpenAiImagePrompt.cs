using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiImagePrompt
    {
        /// <summary>
        /// The image description, make sure to use "Prompt" and not "prompt".
        /// </summary>
        public String Prompt;

        /// <summary>
        /// High quality (false = standard quality).
        /// </summary>
        [OpenAiOptional]
        public bool HighQuality = true;

        /// <summary>
        /// Vivid colors (false = natural colors).
        /// </summary>
        [OpenAiOptional]
        public bool Vivid;

        /// <summary>
        /// The desired size / aspect ratio
        /// </summary>
        [OpenAiOptional]
        public OpenAiImageSizes Size = OpenAiImageSizes.Square;

        /// <summary>
        /// The title of this image, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

        /// <summary>
        /// The image model to use, only set if the user required a specfifc model.
        /// Valid models are: "dall-e-2", "dall-e-3", "gpt-image-1", "gpt-image-1.5" and "gpt-image-2".
        /// </summary>
        [OpenAiOptional]
        public String Model;

    }
}
