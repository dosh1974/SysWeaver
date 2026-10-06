using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Message handler that writes messages to the debug output (<see cref="System.Diagnostics.Debug"/>, ex: the Visual Studio output window).
    /// </summary>
    /// <remarks>
    /// Instances are shared singletons, obtained using <see cref="GetSync(Message.TextStyles)"/> or <see cref="GetAsync(Message.TextStyles)"/>.
    /// Since <see cref="System.Diagnostics.Debug"/> calls are removed by the compiler in release builds of this assembly, nothing is output in release builds.
    /// </remarks>
    public sealed class DebugMessageHandler : TextMessageHandler
    {
       
        /// <summary>
        /// Get a debug log handler that isn't blocking the calling thread while outputting (this improved performance but "debugging" using logging is harder)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <returns>A shared message handler instance (the same instance is returned for the same style)</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="style"/> is not a defined <see cref="Message.TextStyles"/> value.</exception>
        public static DebugMessageHandler GetAsync(Message.TextStyles style = Message.TextStyles.Debug)
        {
            var index = (int)style << 1;
            return Handlers[index + 1];
        }

        /// <summary>
        /// Get a debug log handler that is blocking the calling thread while outputting (this makes it better for "debugging" but may slow down)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <returns>A shared message handler instance (the same instance is returned for the same style)</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="style"/> is not a defined <see cref="Message.TextStyles"/> value.</exception>
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
        
        protected override Task WriteText(String text)
        {
            Debug.Write(text);
            return Task.CompletedTask;
        }

        protected override void OnFlush()
        {
            Debug.Flush();
        }
    }

}
