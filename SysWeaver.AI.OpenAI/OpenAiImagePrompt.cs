using OpenAI.Images;
using System;

namespace SysWeaver.AI
{

    public class OpenAiImagePrompt
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
        /// Create a smaller image (half size), think 4K vs 1080p.
        /// </summary>
        [OpenAiOptional]
        public bool Small = false;


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
        /// The image model to use, only set if the user required a specfifc model or if transparency is requested.
        /// Valid models are: 
        /// * "gpt-image-1" 
        /// * "gpt-image-1.5" 
        /// * "gpt-image-2 (default)"
        /// </summary>
        [OpenAiOptional]
        public String Model;

        /// <summary>
        /// Optionally specify the background type, transparency is only supported by:
        /// * "gpt-image-1.5"
        /// * "gpt-image-2.5-sunburst" (n/a)
        /// * "gpt-image-2.5-flare"  (n/a)
        /// </summary>
        [OpenAiOptional]
        public OpenAiImageBackgrounds Background;
    }

}
