using System;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public abstract partial class AiServiceBase
    {
        /// <summary>
        /// The models available from the API (as returned by FetchModels)
        /// </summary>
        protected sealed class AiModels
        {
            public AiModels(AiLlmModel[] llmModels, AiImageModel[] imageModels)
            {
                LlmModels = llmModels ?? [];
                ImageModels = imageModels ?? [];
            }

            public readonly AiLlmModel[] LlmModels;
            public readonly AiImageModel[] ImageModels;
        }

        /// <summary>
        /// Fetch all available models from the API (API specific).
        /// The result is cached, so this is called at most once every 5 minutes.
        /// </summary>
        /// <returns>The available models</returns>
        protected abstract Task<AiModels> FetchModels();

        readonly CachedValue<AiModels> ModelCache = new CachedValue<AiModels>(TimeSpan.FromMinutes(5));

        /// <summary>
        /// Get the large language models (chat / text generation) available from the API (cached, updated at most every 5 minutes)
        /// </summary>
        /// <returns>The available models</returns>
        public async Task<AiLlmModel[]> GetLlmModels()
            => (await ModelCache.GetOrUpdate(FetchModels).ConfigureAwait(false)).LlmModels;

        /// <summary>
        /// Get the image generation models available from the API (cached, updated at most every 5 minutes)
        /// </summary>
        /// <returns>The available models</returns>
        public async Task<AiImageModel[]> GetImageModels()
            => (await ModelCache.GetOrUpdate(FetchModels).ConfigureAwait(false)).ImageModels;
    }

}
