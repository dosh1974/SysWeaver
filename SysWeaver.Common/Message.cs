using System;
using System.Text;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver
{
    /// <summary>
    /// Represents a single log message, created by <see cref="MessageHost.AddMessage(string, MessageLevels)"/> and dispatched to <see cref="MessageHandler"/> instances.
    /// </summary>
    /// <remarks>
    /// Instances are immutable (except for an internal cache of rendered text) and safe to share between threads.
    /// A leading prefix of the form "[Token]" or "[A] [B]" in the original text is split off into <see cref="Prefix"/> / <see cref="OrgPrefix"/>
    /// and padded so that message texts line up when rendered.
    /// </remarks>
    [TableDataPrimaryKey(nameof(Id))]
    public sealed class Message
    {
        /// <summary>
        /// Format the message using the <see cref="Debug"/> format string.
        /// </summary>
        /// <returns>A formatted message.</returns>
        public override string ToString()
        {
            return Format(Debug);
        }

        /// <summary>
        /// Log format string for debugging (very verbose)
        /// </summary>
        public const String Debug = "#{0,-7} {4:HH:mm:ss} {3,7}: {1}";
        /// <summary>
        /// Log format string for interactive user sessions (not too verbose)
        /// </summary>
        public const String UserConsole = "{3,7}: {1}";
        /// <summary>
        /// Log format string for long running, non-interactive sessions
        /// </summary>
        public const String ServerConsole = "{4:HH:mm:ss} {3,7}: {1}";

        /// <summary>
        /// Returns a formatted log message string, using a composite format string (as used by <see cref="String.Format(string, object[])"/>).
        /// </summary>
        /// <param name="format">The composite format string, arguments are:
        /// 0: Int64 Id
        /// 1: String Text (without the prefix)
        /// 2: Exception Exception (or null)
        /// 3: MessageLevels Level
        /// 4: DateTime Time (UTC)
        /// 5: Int32 ThreadId
        /// </param>
        /// <returns>A formatted log message</returns>
        /// <exception cref="FormatException"><paramref name="format"/> is invalid or references an argument index greater than 5.</exception>
        /// <remarks>Uses the current culture for formatting.</remarks>
        public String Format(String format)
        {
            return String.Format(format, Id, Text, Exception, Level, Time, ThreadId);
        }

        /// <summary>
        /// Message id, a sequence number that is unique (and increasing) per <see cref="MessageHost"/> instance.
        /// </summary>
        public readonly long Id;
        /// <summary>
        /// The time (UTC) when the message was created
        /// </summary>
        public readonly DateTime Time;
        /// <summary>
        /// Message level
        /// </summary>
        public readonly MessageLevels Level;


        /// <summary>
        /// Prefix to add before the message text, the "[..]" prefix padded to a fixed width (27 chars plus a space) followed by any tab indentation.
        /// Never null, if the message had no prefix this is a padding only string.
        /// </summary>
        [TableDataHide]
        public readonly String Prefix;

        /// <summary>
        /// Original "[..]" prefix of the message text without padding or indentation (a padding string if the message had no prefix).
        /// </summary>
        [TableDataName(nameof(Prefix))]
        public readonly String OrgPrefix;

        /// <summary>
        /// Message text (with any leading "[..]" prefix removed)
        /// </summary>
        [TableDataText(100, "{0}", "{0}", true)]
        public readonly String Text;
       
        /// <summary>
        /// The managed thread id of the thread that created the message
        /// </summary>
        public readonly int ThreadId;
        
        /// <summary>
        /// Exception (or null)
        /// </summary>
        public readonly Exception Exception;

        static readonly Char[] Pattern = ". ".ToCharArray();
        const int PatternMask = 1;
        
        static void CreateWidth(Span<Char> w, String s)
        {
            
            var pos = w.Length;
            --pos;
            w[pos] = ' ';
            var l = s.Length;
            while (l > 0)
            {
                --pos;
                --l;
                w[pos] = s[l];
            }
            var p = Pattern;
            while (pos > 0)
            {
                --pos;
                w[pos] = p[pos & PatternMask];
            }
        }

        static String SetWidth(String s, int width)
        {
            var dl = s.Length;
            if (dl < width)
                dl = width;
            ++dl;
            return String.Create(dl, s, CreateWidth);
        }

        /*static void CreateTab(Span<Char> w, String x)
        {
            var pos = w.Length;
            var p = Pattern;
            while (pos > 0)
            {
                --pos;
                w[pos] = p[pos & PatternMask];
            }
        }


        static String SetTab(int width) => String.Create(width, "", CreateTab);
        */
        static String SetTab(int width) => new String(' ', width);


        const int PrefixWidth = 27;
        static readonly String EmptyPrefix = SetWidth("", PrefixWidth);


        internal Message(String message, Exception ex, MessageLevels level, long id, int tab)
        {
            Id = id; ;
            Time = DateTime.UtcNow;
            Exception = ex;
            Level = level;
            ThreadId = Environment.CurrentManagedThreadId;
            int prefixLen = 0;
            int offset = 0;
            int messageStart = 0;
            var ml = message.Length;
            for (; ; )
            {
                if (offset >= ml)
                    break;
                if (message[offset] != '[')
                    break;
                ++offset;
                if (offset >= ml)
                    break;
                offset = message.IndexOf(']', offset);
                if (offset < 0)
                    break;
                ++offset;
                prefixLen = offset;
                messageStart = offset;
                if (offset >= ml)
                    break;
                if (message[offset] != ' ')
                    break;
                ++messageStart;
                ++offset;
            }
            var prefix = prefixLen > 0 ? message.Substring(0, prefixLen) : EmptyPrefix;
            OrgPrefix = prefix;
            if (tab > 0)
            {
                var ts = SetTab(tab);
                if (prefixLen > 0)
                {
                    Prefix = SetWidth(prefix, PrefixWidth) + ts;
                    message = message.Substring(messageStart);

                }else
                {
                    Prefix = prefix + ts;
                }
            }else
            {
                if (prefixLen > 0)
                {
                    Prefix = SetWidth(prefix, PrefixWidth);
                    message = message.Substring(messageStart);
                }
                else
                {
                    Prefix = prefix;
                }
            }
            Text = message;
            //Stack = new StackTrace(depth);
        }


        /// <summary>
        /// The amount of detail to include when rendering a message as text, see <see cref="GetText(TextStyles)"/>.
        /// </summary>
        public enum TextStyles
        {
            /// <summary>
            /// Minimal details, prefix and text only
            /// </summary>
            Normal,
            /// <summary>
            /// Adds the local time (HH:mm:ss) and the message level
            /// </summary>
            Verbose,
            /// <summary>
            /// Same as <see cref="Verbose"/> but also adds the message id and the thread id
            /// </summary>
            Debug,
        }


        String[] Texts;



        /// <summary>
        /// Get the local date of the message (yyyy-MM-dd) if it differs from a previously returned date.
        /// Used by text handlers to emit a date line only when the date changes.
        /// </summary>
        /// <param name="prev">The previously emitted date string (or null).</param>
        /// <returns>The local date as "yyyy-MM-dd", or null if it's equal to <paramref name="prev"/>.</returns>
        public String GetDate(String prev)
        {
            var localTime = Time.ToLocalTime();
            var s = localTime.ToString("yyyy-MM-dd");
            return s == prev ? null : s;
        }

        /// <summary>
        /// Get the formatted message text, including any exception (message and stack trace, for all inner exceptions).
        /// </summary>
        /// <param name="style">The styling of the text</param>
        /// <returns>Formatted message text, always terminated by a new line. Multi-line texts are indented to line up with the first line.</returns>
        /// <remarks>
        /// The result is cached per style, so repeated calls (ex: from multiple handlers) are cheap. Thread safe.
        /// Times are rendered in local time.
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException"><paramref name="style"/> is not a defined <see cref="TextStyles"/> value.</exception>
        public String GetText(TextStyles style)
        {
            var t = Texts;
            if (t == null)
            {
                t = new string[3];
                Interlocked.CompareExchange(ref Texts, t, null);
                t = Texts;
            }
            var si = (int)style;
            var st = t[si];
            if (st != null)
                return st;
            lock (t)
            {
                st = t[si];
                if (st != null)
                    return st;
                Exception e = Exception;
                int headerWidth = 0;
                //  Date / time
                var sb = new StringBuilder();
                if (style >= TextStyles.Verbose)
                {
                    headerWidth += 8;
                    var localTime = Time.ToLocalTime();
                    var timeStamp = localTime.ToString("HH:mm:ss");
                    sb.Append(timeStamp);
                    if (style >= TextStyles.Debug)
                    {
                        sb.Append(String.Format(" #{0,-7} 0x{1:x4}", Id, ThreadId));
                        headerWidth += 16;
                    }
                    sb.Append(' ');
                    sb.Append(Level.ToString().PadRight(8));
                    headerWidth += 9;
                }
                var tab = new String(' ', headerWidth);
                var pre = Prefix;
                var newLine = "\n" + new String(' ', headerWidth + pre.Length);
                sb.Append(pre);
                sb.Append(Text.Trim('\n', '\r').Replace("\n", newLine));
                sb.Append("\n");
                while (e != null)
                {
                    sb.AppendLine(String.Concat(tab, pre, e.Message.Trim('\n', '\r').Replace("\n", newLine)));
                    sb.AppendLine(String.Concat(tab, pre, e.StackTrace?.Trim('\n', '\r')?.Replace("\n", newLine)));
                    e = e.InnerException;
                }
                st = sb.ToString();
                t[si] = st;
                return st;
            }
        }





    }

}
