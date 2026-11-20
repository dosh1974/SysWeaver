using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// A service that can create an image from a source image and a text prompt, ex: edit an image (API agnostic).
    /// Implemented by the OpenAI, Google and ComfyUI services.
    /// </summary>
    public interface IAiImageEditService
    {
        /// <summary>
        /// Create an image from a source image and a text prompt, ex: "Make it a watercolor painting".
        /// </summary>
        /// <param name="prompt">The source image, what to do with it, the model (null or blank for the service default), the size etc</param>
        /// <returns>The created image (typically a png image, see <see cref="MemoryFile.Mime"/>)</returns>
        /// <exception cref="System.ArgumentNullException">The prompt or the source image is null</exception>
        /// <exception cref="System.Exception">The image couldn't be created (ex: the model refused or the service failed)</exception>
        Task<MemoryFile> ImageToImage(ImageToImagePrompt prompt);

        /// <summary>
        /// Get the models that can create an image from a source image and a text prompt, use <see cref="AiImageEditModel.Id"/> as the model of an <see cref="ImageToImagePrompt"/>.
        /// For ComfyUI these are the images to image workflows.
        /// </summary>
        /// <returns>The available models (may be empty)</returns>
        /// <exception cref="System.Exception">The models couldn't be fetched (ex: the service is unavailable)</exception>
        Task<AiImageEditModel[]> GetImageEditModels();
    }
}
