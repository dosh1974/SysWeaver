using System;

namespace SysWeaver.AI
{
    public class OpenAiImageEditPrompt : OpenAiImagePrompt
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
