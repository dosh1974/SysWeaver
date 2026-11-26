using System;
using System.Runtime.CompilerServices;

namespace SysWeaver.AI
{
    /// <summary>
    /// AI chat extensions of <see cref="AiTool"/>
    /// </summary>
    public static class AiToolExt
    {
        //  One command per tool, only created for the tools that are used as chat commands
        static readonly ConditionalWeakTable<AiTool, AiCommand> Commands = new();

        static readonly ConditionalWeakTable<AiTool, AiCommand>.CreateValueCallback CreateFn = Create;

        extension(AiTool tool)
        {
            /// <summary>
            /// The tool as a chat command (runs the tool with the supplied json argument), null if the tool is external (can't be invoked)
            /// </summary>
            public AiCommand AsCommand => tool.IsExternal ? null : Commands.GetValue(tool, CreateFn);
        }

        static AiCommand Create(AiTool tool)
            => new AiCommand(tool.Name, tool.Auth, async (args, s, r) =>
            {
                var m = await tool.Invoke(String.IsNullOrEmpty(args) ? null : BinaryData.FromString(String.Join(args, "x:", ';')), r).ConfigureAwait(false);
                m = AiTools.BeautifyJson(m);
                return new Chat.ChatMessage
                {
                    Text = String.Join(m, "```json\n", "\n```"),
                    Format = Chat.ChatMessageFormats.MarkDown
                };
            }, tool.Arg == null ? null : "<json data>", tool.Desc);
    }

}
