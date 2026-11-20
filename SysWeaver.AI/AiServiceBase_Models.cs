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
            /// <summary>
            /// Create the model lists
            /// </summary>
            /// <param name="llmModels">The large language models, null for none</param>
            /// <param name="imageModels">The image generation models, null for none</param>
            public AiModels(AiLlmModel[] llmModels, AiImageModel[] imageModels)
            {
                LlmModels = llmModels ?? [];
                ImageModels = imageModels ?? [];
            }

            /// <summary>
            /// The large language models
            /// </summary>
            public readonly AiLlmModel[] LlmModels;

            /// <summary>
            /// The image generation models
            /// </summary>
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

        /// <summary>
        /// Get the image edit (image to image) models available from the API (cached, updated at most every 5 minutes).
        /// These are the image models that <see cref="RateImageModel"/> accepts for editing.
        /// </summary>
        /// <returns>The available models</returns>
        public async Task<AiImageEditModel[]> GetImageEditModels()
        {
            var models = await GetImageModels().ConfigureAwait(false);
            var res = new System.Collections.Generic.List<AiImageEditModel>(models.Length);
            foreach (var m in models)
            {
                var id = m?.Id;
                if (String.IsNullOrEmpty(id) || (RateImageModel(id, true, false) < 0))
                    continue;
                res.Add(AiImageEditModel.From(m));
            }
            return res.ToArray();
        }

        /// <summary>
        /// Rate how suitable an image model is, used to select a model when the default image model isn't available (anymore).
        /// </summary>
        /// <param name="model">The model id (as returned by GetImageModels)</param>
        /// <param name="edit">True if the model will be used for image editing (image to image), false for text to image</param>
        /// <param name="transparent">True if a transparent background is requested</param>
        /// <returns>A negative value if the model can't be used, else a higher value for a more suitable model</returns>
        protected virtual int RateImageModel(String model, bool edit, bool transparent) => 0;

        volatile String LastImageModelSubstitution;

        /// <summary>
        /// Get the image model to use.
        /// If a model is specified it's used as is.
        /// Else the default image model (or image edit model) is used, unless it's not available from the API (anymore) or isn't suitable (see RateImageModel),
        /// then the most suitable available model is used instead (a warning is reported when an unavailable default is replaced).
        /// If the available models can't be fetched, the default is used.
        /// </summary>
        /// <param name="model">The requested model, null or blank to use the default</param>
        /// <param name="edit">True to get a model for image editing (image to image), false for text to image</param>
        /// <param name="transparent">True if a transparent background is requested (a default model that doesn't support it may be replaced by one that does)</param>
        /// <returns>The model id to use</returns>
        protected async Task<String> GetImageModel(String model, bool edit, bool transparent = false)
        {
            if (!String.IsNullOrWhiteSpace(model))
                return model.Trim();
            var def = edit ? DefaultImageEditModel : DefaultImageModel;
            AiImageModel[] models;
            try
            {
                models = await GetImageModels().ConfigureAwait(false);
            }
            catch
            {
                //  Some (OpenAI compatible) end points can't list models
                return def;
            }
            if ((models?.Length ?? 0) <= 0)
                return def;
            bool exists = false;
            if (!String.IsNullOrEmpty(def))
            {
                foreach (var m in models)
                {
                    if (String.Equals(m.Id, def, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        def = m.Id;
                        break;
                    }
                }
            }
            if (exists && (RateImageModel(def, edit, transparent) >= 0))
                return def;
            String best = null;
            int bestRate = -1;
            foreach (var m in models)
            {
                var id = m.Id;
                if (String.IsNullOrEmpty(id))
                    continue;
                var r = RateImageModel(id, edit, transparent);
                if (r <= bestRate)
                    continue;
                bestRate = r;
                best = id;
            }
            //  Nothing suitable is available, use the default anyway (the API reports the error)
            if (best == null)
                return def;
            var key = String.Concat(def, "|", best);
            if (!exists && (LastImageModelSubstitution != key))
            {
                LastImageModelSubstitution = key;
                Msg?.AddMessage(MessageLevels.Warning, $"{Name}: The default image {(edit ? "edit " : "")}model {def.ToQuoted()} isn't available, using {best.ToQuoted()} instead");
            }
            return best;
        }

        /// <summary>
        /// Get the size to use for an image, using the default size if a size isn't specified.
        /// If only the width or height is specified, the other one is computed using the aspect ratio of the default size.
        /// </summary>
        /// <param name="width">The requested width, 0 or less to use the default</param>
        /// <param name="height">The requested height, 0 or less to use the default</param>
        /// <returns>The width and height to use (always positive)</returns>
        protected (int Width, int Height) GetImageSize(int width, int height)
            => AiImageTools.GetImageSize(width, height, DefaultImageWidth, DefaultImageHeight);
    }

}
