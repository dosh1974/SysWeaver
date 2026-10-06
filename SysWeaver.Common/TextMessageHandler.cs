using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Abstract Message handler that generates text only output.
    /// Messages are rendered using <see cref="Message.GetText(Message.TextStyles)"/> and passed to <see cref="WriteText(string)"/>.
    /// </summary>
    /// <remarks>
    /// For <see cref="Message.TextStyles.Verbose"/> and higher, a "yyyy-MM-dd" date line is prepended whenever the (local) date differs from the previous message.
    /// </remarks>
    public abstract class TextMessageHandler : MessageHandler
    {

        /// <summary>
        /// Create a text message handler.
        /// </summary>
        /// <param name="style">The amount of detail to include in the text.</param>
        /// <param name="mode">How messages are delivered, see <see cref="MessageHandler.Modes"/>.</param>
        public TextMessageHandler(Message.TextStyles style, Modes mode) : base(mode)
        {
            Style = style;
        }

        /// <summary>
        /// The amount of detail included in the text output.
        /// </summary>
        public readonly Message.TextStyles Style;

        public override string ToString()
        {
            return String.Concat("Text ", Mode, " ", Style);
        }

        String Prev;

        protected sealed override Task Add(Message message)
        {
            var style = Style;
            var text = message.GetText(style);
            if (style >= Message.TextStyles.Verbose)
            {
                var t = message.GetDate(Prev);
                if (t != null)
                {
                    Prev = t;
                    text = String.Join('\n', t, text);
                }
            }
            return WriteText(text);
        }

        /// <summary>
        /// Write rendered text to the output.
        /// </summary>
        /// <param name="text">The text to write, already terminated by a new line (may contain multiple lines).</param>
        /// <returns>A task that completes when the text is written (must be completed on return in <see cref="MessageHandler.Modes.NativeSync"/> mode).</returns>
        protected abstract Task WriteText(String text);

        protected override void OnFlush()
        {
        }

    }

}
