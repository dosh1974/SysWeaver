using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiImagePrompt
    {
        /// <summary>
        /// The prompt to use
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
        /// The size of the generated image
        /// </summary>
        [OpenAiOptional]
        public OpenAiImageSizes Size = OpenAiImageSizes._1024x1024;

        /// <summary>
        /// The title of this image, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

    }
}
