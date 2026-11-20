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

        /// <summary>
        /// Get the models that can create an image from a text prompt, use <see cref="AiImageModel.Id"/> as <see cref="TextToImagePrompt.Model"/>.
        /// For ComfyUI these are the text to image workflows.
        /// </summary>
        /// <returns>The available models (may be empty)</returns>
        /// <exception cref="System.Exception">The models couldn't be fetched (ex: the service is unavailable)</exception>
        Task<AiImageModel[]> GetImageModels();
    }
}
