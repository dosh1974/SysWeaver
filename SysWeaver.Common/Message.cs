using System;
using System.Text;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver
{
    /// <summary>
    /// Represents a log message
    /// </summary>
    [TableDataPrimaryKey(nameof(Id))]
    public sealed class Message
    {
        public override string ToString()
        {
            return Format(Debug);
        }

        /// <summary>
        /// Log format string for debugging (very verbose)
        /// </summary>
        public const String Debug = "#{0,-7} {4:HH:mm::ss} {3,7}: {1}";
        /// <summary>
        /// Log format string for interactive user sessions (not too verbose)
        /// </summary>
        public const String UserConsole = "{3,7}: {1}";
        /// <summary>
        /// Log format string for long running, non-interactive sessions
        /// </summary>
        public const String ServerConsole = "{4:HH:mm::ss} {3,7}: {1}";

        /// <summary>
        /// Returns a formatted log message string, given a formatting string, string formatter.
        /// </summary>
        /// <param name="format">The string formatter, argumets are:
        /// 0: Int64 Id
        /// 1: String Text
        /// 2: Exception Exception (or null)
        /// 3: MessageLevels Level
        /// 4: DateTime Time
        /// 5: In32 ThreadId
        /// </param>
        /// <returns>A formatted log message</returns>
        public String Format(String format)
        {
            return String.Format(format, Id, Text, Exception, Level, Time, ThreadId);
        }

        /// <summary>
        /// Message id (unique number)
        /// </summary>
        public readonly long Id;
        /// <summary>
        /// The time when the message has created
        /// </summary>
        public readonly DateTime Time;
        /// <summary>
        /// Message level
        /// </summary>
        public readonly MessageLevels Level;


        /// <summary>
        /// Prefix to add at every new line before the message text (handles tabs etc)
        /// </summary>
        [TableDataHide]
        public readonly String Prefix;

        /// <summary>
        /// Original prefix without padding
        /// </summary>
        [TableDataName(nameof(Prefix))]
        public readonly String OrgPrefix;

        /// <summary>
        /// Message text
        /// </summary>
        [TableDataText(100)]
        public readonly String Text;
       
        /// <summary>
        /// The thread that created the message
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


        public enum TextStyles
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


        String[] Texts;



        /// <summary>
        /// Get new date strings
        /// </summary>
        /// <param name="prev"></param>
        /// <returns></returns>
        public String GetDate(String prev)
        {
            var localTime = Time.ToLocalTime();
            var s = localTime.ToString("yyyy-MM-dd");
            return s == prev ? null : s;
        }

        /// <summary>
        /// Get the formatted message text
        /// </summary>
        /// <param name="style">The styling of the text</param>
        /// <returns>Formatted message text</returns>
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
