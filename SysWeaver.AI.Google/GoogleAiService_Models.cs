using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class GoogleAiService
    {
        /// <summary>
        /// Model id prefixes of large language models
        /// </summary>
        static readonly String[] LlmModelPrefixes = ["gemini", "gemma"];

        /// <summary>
        /// If a model id contains any of these, it's not a chat / text generation model
        /// </summary>
        static readonly String[] NonLlmModelParts = ["image", "tts", "embedding", "audio", "live"];

        static bool IsImageModel(String id) => id.StartsWith("imagen", StringComparison.OrdinalIgnoreCase) || id.Contains("-image", StringComparison.OrdinalIgnoreCase);

        static bool IsLlmModel(String id)
        {
            var ok = false;
            foreach (var x in LlmModelPrefixes)
                if (id.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                {
                    ok = true;
                    break;
                }
            if (!ok)
                return false;
            foreach (var x in NonLlmModelParts)
                if (id.Contains(x, StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        static bool Supports(List<String> actions, String action)
        {
            //  Vertex AI doesn't report supported actions, assume supported
            if (actions == null)
                return true;
            foreach (var x in actions)
                if (String.Equals(x, action, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        protected override async Task<AiModels> FetchModels()
        {
            List<AiLlmModel> llms = new();
            List<AiImageModel> images = new();
            await foreach (var m in (await Client.Models.ListAsync().ConfigureAwait(false)).ConfigureAwait(false))
            {
                //  Names are "models/gemini-2.5-flash" (Gemini API) or "publishers/google/models/gemini-2.5-flash" (Vertex AI)
                var id = m.Name;
                if (String.IsNullOrEmpty(id))
                    continue;
                id = id.Substring(id.LastIndexOf('/') + 1);
                var actions = m.SupportedActions;
                if (IsImageModel(id))
                {
                    if (!(Supports(actions, "generateContent") || Supports(actions, "predict")))
                        continue;
                    images.Add(new AiImageModel
                    {
                        Id = id,
                        Name = m.DisplayName,
                        Description = m.Description,
                        Owner = "google",
                    });
                    continue;
                }
                if (!IsLlmModel(id))
                    continue;
                if (!Supports(actions, "generateContent"))
                    continue;
                llms.Add(new AiLlmModel
                {
                    Id = id,
                    Name = m.DisplayName,
                    Description = m.Description,
                    Owner = "google",
                    InputTokenLimit = m.InputTokenLimit,
                    OutputTokenLimit = m.OutputTokenLimit,
                    CanReason = m.Thinking,
                });
            }
            llms.Sort((a, b) => String.CompareOrdinal(a.Id, b.Id));
            images.Sort((a, b) => String.CompareOrdinal(a.Id, b.Id));
            return new AiModels(llms.ToArray(), images.ToArray());
        }
    }
}
