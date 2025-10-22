using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Message handler that output's messages to the console
    /// </summary>
    public sealed class DebugMessageHandler : TextMessageHandler
    {
       
        /// <summary>
        /// Get a debug log handler that isn't blocking the calling thread while outputting (this improved performance but "debugging" using logging is harder)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <returns>A message handler</returns>
        public static DebugMessageHandler GetAsync(Message.TextStyles style = Message.TextStyles.Debug)
        {
            var index = (int)style << 1;
            return Handlers[index + 1];
        }

        /// <summary>
        /// Get a debug log handler that is blocking the calling thread while outputting (this makes it better for "debugging" but may slow down)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <returns>A message handler</returns>
        public static DebugMessageHandler GetSync(Message.TextStyles style = Message.TextStyles.Debug)
        {
            var index = (int)style << 1;
            return Handlers[index];
        }

        DebugMessageHandler(Message.TextStyles style, Modes mode) : base(style, mode)
        {
        }

        static readonly DebugMessageHandler[] Handlers =
        [
            new DebugMessageHandler(Message.TextStyles.Normal, Modes.NativeSync),
            new DebugMessageHandler(Message.TextStyles.Normal, Modes.Async),

            new DebugMessageHandler(Message.TextStyles.Verbose, Modes.NativeSync),
            new DebugMessageHandler(Message.TextStyles.Verbose, Modes.Async),

            new DebugMessageHandler(Message.TextStyles.Debug, Modes.NativeSync),
            new DebugMessageHandler(Message.TextStyles.Debug, Modes.Async),
        ];

        public override string ToString()
        {
            return String.Concat("Debug ", Mode, " ", Style);
        }
        
        protected override ValueTask WriteText(String text)
        {
            Debug.Write(text);
            return default;
        }

        protected override void OnFlush()
        {
            Debug.Flush();
        }
    }

}
