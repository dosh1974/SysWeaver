using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Abstract Message handler that generates text only output
    /// </summary>
    public abstract class TextMessageHandler : MessageHandler
    {

        public TextMessageHandler(Message.TextStyles style, Modes mode) : base(mode)
        {
            Style = style;
        }

        public readonly Message.TextStyles Style;

        public override string ToString()
        {
            return String.Concat("Text ", Mode, " ", Style);
        }

        String Prev;

        protected sealed override ValueTask Add(Message message)
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

        protected abstract ValueTask WriteText(String text);

        protected override void OnFlush()
        {
        }

    }

}
