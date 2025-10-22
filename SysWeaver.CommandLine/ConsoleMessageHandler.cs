using System;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Message handler that output's messages to the console
    /// </summary>
    public sealed class ConsoleMessageHandler : MessageHandler
    {
        
        /// <summary>
        /// Get a console log handler that isn't blocking the calling thread while outputting (this improved performance but "debugging" using logging is harder)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <param name="monoChrome">True for monochrome output</param>
        /// <returns>A message handler</returns>
        public static ConsoleMessageHandler GetAsync(Styles style = Styles.Debug, bool monoChrome = false)
        {
            int index = 1 + (monoChrome ? 2 : 0);
            index |= (int)style << 2;
            return Handlers[index];
        }

        /// <summary>
        /// Get a console log handler that is blocking the calling thread while outputting (this makes it better for "debugging" but may slow down)
        /// </summary>
        /// <param name="style">The display style to use</param>
        /// <param name="monoChrome">True for monochrome output</param>
        /// <returns>A message handler</returns>
        public static ConsoleMessageHandler GetSync(Styles style = Styles.Debug, bool monoChrome = false)
        {
            int index = 0 + (monoChrome ? 2 : 0);
            index |= (int)style << 2;
            return Handlers[index];
        }


        public enum Styles
        {
            /// <summary>
            /// Minimal details
            /// </summary>
            Normal,
            /// <summary>
            /// Plenty of details
            /// </summary>
            Verbose,
            /// <summary>
            /// Even more details
            /// </summary>
            Debug,
        }

        public readonly bool Monochrome;
        public readonly Styles Style;

        readonly ConsoleColor DefaultForeground;

        ConsoleMessageHandler(Styles style, bool monoChrome, Modes mode) : base(mode)
        {
            Style = style;
            Monochrome = monoChrome;
            DefaultForeground = Console.ForegroundColor;
        }

        public override void Dispose()
        {
            base.Dispose();
            Console.ResetColor();
        }


        static readonly ConsoleMessageHandler[] Handlers =
        [
            new ConsoleMessageHandler(Styles.Normal, false, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Normal, false, Modes.Async),
            new ConsoleMessageHandler(Styles.Normal, true, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Normal, true, Modes.Async),

            new ConsoleMessageHandler(Styles.Verbose, false, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Verbose, false, Modes.Async),
            new ConsoleMessageHandler(Styles.Verbose, true, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Verbose, true, Modes.Async),

            new ConsoleMessageHandler(Styles.Debug, false, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Debug, false, Modes.Async),
            new ConsoleMessageHandler(Styles.Debug, true, Modes.NativeSync),
            new ConsoleMessageHandler(Styles.Debug, true, Modes.Async),
        ];

        public override string ToString()
        {
            return String.Concat("Console ", Mode, " ", Style, Monochrome ? " Monochrome" : "");
        }

        static ConsoleColor[] DarkColors =
        [
            ConsoleColor.Black,
            ConsoleColor.DarkGreen,
            ConsoleColor.Gray,
            ConsoleColor.DarkYellow,
            ConsoleColor.DarkRed,
        ];
        
        static ConsoleColor[] BrightColors =
        [
            ConsoleColor.Black,
            ConsoleColor.Green,
            ConsoleColor.White,
            ConsoleColor.Yellow,
            ConsoleColor.Red,
        ];
        
        void SetColor(ConsoleColor color)
        {
            if (Monochrome)
                return;
            Console.ForegroundColor = color;
        }

        String Prev;

        static readonly object Lock = new object();

        protected override ValueTask Add(Message message)
        {
            Exception e = message.Exception;
            var li = (int)message.Level;
            ConsoleColor normal = DarkColors[li];
            ConsoleColor bright = BrightColors[li];
            ConsoleColor debug = ConsoleColor.DarkGray;
            int headerWidth = 0;
            //  Date / time
            lock (Lock)
            {
                var prevColor = Console.ForegroundColor;
                if ((message.Level == MessageLevels.Info) && (prevColor != DefaultForeground))
                    normal = prevColor;
                if (Style >= Styles.Verbose)
                {
                    var localTime = message.Time.ToLocalTime();
                    var timeStamp = localTime.ToString("HH:mm:ss");
                    var s = localTime.ToString("yyyyMMdd");
                    SetColor(debug);
                    if (s != Prev)
                    {
                        Prev = s;
                        Console.WriteLine(localTime.ToString("yyyy-MM-dd"));
                    }
                    headerWidth += 8;
                    Console.Write(timeStamp);
                    if (Style >= Styles.Debug)
                    {
                        Console.Write(String.Format(" #{0,-7} 0x{1:x4}", message.Id, message.ThreadId));
                        headerWidth += 16;
                    }
                    if (Monochrome)
                    {
                        Console.Write(" " + message.Level.ToString().PadRight(8));
                        headerWidth += 9;
                    }
                    else
                    {
                        Console.Write(" ");
                        ++headerWidth;
                    }
                }
                var tab = new String(' ', headerWidth);
                SetColor(e != null ? bright : normal);
                var pre = message.Prefix;
                var newLine = "\n" + new String(' ', headerWidth + pre.Length);
                Console.WriteLine(pre + message.Text.Trim('\n', '\r').Replace("\n", newLine));
                while (e != null)
                {
                    SetColor(bright);
                    Console.WriteLine(String.Concat(tab, pre, e.Message.Trim('\n', '\r').Replace("\n", newLine)));
                    SetColor(normal);
                    Console.WriteLine(String.Concat(tab, pre, e.StackTrace?.Trim('\n', '\r')?.Replace("\n", newLine)));
                    e = e.InnerException;
                }
                SetColor(prevColor);
            }
            return default;
        }

        protected override void OnFlush()
        {
            Console.Out.Flush();
        }
    }

}
