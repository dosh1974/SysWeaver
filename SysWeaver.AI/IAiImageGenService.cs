using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// A service that can create an image from a text prompt (API agnostic).
    /// Implemented by the OpenAI, Google and ComfyUI services.
    /// </summary>
    public interface IAiImageGenService
    {
        /// <summary>
        /// Create an image from a text prompt.
        /// </summary>
        /// <param name="prompt">What to create, the model (null or blank for the service default), the size etc</param>
        /// <returns>The created image (typically a png image, see <see cref="MemoryFile.Mime"/>)</returns>
        /// <exception cref="System.ArgumentNullException">The prompt is null</exception>
        /// <exception cref="System.Exception">The image couldn't be created (ex: the model refused or the service failed)</exception>
        Task<MemoryFile> TextToImage(TextToImagePrompt prompt);
    }
}
