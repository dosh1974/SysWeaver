using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.AI
{

    public sealed partial class OpenAiService
    {
        /// <summary>
        /// Model id prefixes of image generation models
        /// </summary>
        static readonly String[] ImageModelPrefixes = ["dall-e", "gpt-image", "chatgpt-image"];

        /// <summary>
        /// Model id prefixes of large language models
        /// </summary>
        static readonly String[] LlmModelPrefixes = ["gpt-", "chatgpt-", "o1", "o3", "o4", "codex-"];

        /// <summary>
        /// If a model id contains any of these, it's not a chat / text generation model
        /// </summary>
        static readonly String[] NonLlmModelParts = ["image", "audio", "realtime", "tts", "transcribe", "embedding", "moderation", "instruct", "search", "computer-use"];

        static bool StartsWithAny(String id, String[] prefixes)
        {
            foreach (var x in prefixes)
                if (id.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static bool ContainsAny(String id, String[] parts)
        {
            foreach (var x in parts)
                if (id.Contains(x, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        protected override async Task<AiModels> FetchModels()
        {
            var r = await new OpenAIClient(ApiKey, Options).GetOpenAIModelClient().GetModelsAsync().ConfigureAwait(false);
            List<AiLlmModel> llms = new();
            List<AiImageModel> images = new();
            foreach (var m in r.Value)
            {
                var id = m.Id;
                if (String.IsNullOrEmpty(id))
                    continue;
                DateTime? created = m.CreatedAt == default ? null : m.CreatedAt.UtcDateTime;
                if (StartsWithAny(id, ImageModelPrefixes))
                {
                    images.Add(new AiImageModel
                    {
                        Id = id,
                        Owner = m.OwnedBy,
                        Created = created,
                    });
                    continue;
                }
                if (!StartsWithAny(id, LlmModelPrefixes))
                    continue;
                if (ContainsAny(id, NonLlmModelParts))
                    continue;
                llms.Add(new AiLlmModel
                {
                    Id = id,
                    Owner = m.OwnedBy,
                    Created = created,
                });
            }
            llms.Sort((a, b) => String.CompareOrdinal(a.Id, b.Id));
            images.Sort((a, b) => String.CompareOrdinal(a.Id, b.Id));
            return new AiModels(llms.ToArray(), images.ToArray());
        }
    }

}
