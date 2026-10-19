using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class AnthropicAiService
    {
        protected override async Task<AiModels> FetchModels()
        {
            List<AiLlmModel> llms = new();
            var page = await Client.Models.List().ConfigureAwait(false);
            for (; ; )
            {
                foreach (var m in page.Items)
                {
                    var id = m.ID;
                    if (String.IsNullOrEmpty(id))
                        continue;
                    llms.Add(new AiLlmModel
                    {
                        Id = id,
                        Name = m.DisplayName,
                        Owner = "anthropic",
                        Created = m.CreatedAt.UtcDateTime,
                        InputTokenLimit = ToInt(m.MaxInputTokens),
                        OutputTokenLimit = ToInt(m.MaxTokens),
                        CanReason = AnthropicModels.GetOptions(id).Thinking != AnthropicModels.ThinkingTypes.None,
                    });
                }
                if (!page.HasNext())
                    break;
                page = await page.Next().ConfigureAwait(false);
            }
            llms.Sort((a, b) => String.CompareOrdinal(a.Id, b.Id));
            //  The API have no image generation models
            return new AiModels(llms.ToArray(), null);
        }

        static int? ToInt(long? v)
            => v.HasValue ? (int)Math.Min(v.Value, int.MaxValue) : null;
    }
}
