using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SysWeaver.MicroService;

namespace SysWeaver.Email
{

    public class AnyEmail : IncomingEmail
    {
        /// <summary>
        /// True if this email was received (faked), false if it was sent by the system
        /// </summary>
        public bool Incoming;

        /// <summary>
        /// The error (if sending failed)
        /// </summary>
        public String Error;

        /// <summary>
        /// A short plain text preview of the body
        /// </summary>
        internal readonly String Preview;

        public override string ToString() => String.Concat('#', Id, Incoming ? " from " : " to ", Incoming ? From : To, Error == null ? "" : " [" + Error + "]", ": ", Subject);

        public AnyEmail(bool incoming, String from, String to, String subject, String body, bool isHtml, String system)
        {
            Incoming = incoming;
            Id = NewId();
            Time = DateTime.UtcNow;
            From = from;
            To = to;
            Subject = subject;
            Body = body;
            IsHtml = isHtml;
            System = system;
            Preview = GetPreview(body, isHtml);
        }

        public static String NewId() => "FakeMail" + Interlocked.Increment(ref Counter);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;

        const int PreviewLength = 200;

        static readonly Regex RemoveBlocks = new Regex(@"<(head|style|script|title)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        static readonly Regex RemoveTags = new Regex(@"<[^>]*>", RegexOptions.Singleline | RegexOptions.Compiled);

        static String GetPreview(String body, bool isHtml)
        {
            if (String.IsNullOrEmpty(body))
                return "";
            if (isHtml)
            {
                body = RemoveBlocks.Replace(body, " ");
                body = RemoveTags.Replace(body, " ");
                body = WebUtility.HtmlDecode(body);
            }
            var sb = new StringBuilder(Math.Min(body.Length, PreviewLength));
            bool ws = false;
            foreach (var c in body)
            {
                if (Char.IsWhiteSpace(c))
                {
                    ws = sb.Length > 0;
                    continue;
                }
                if (ws)
                    sb.Append(' ');
                ws = false;
                sb.Append(c);
                if (sb.Length >= PreviewLength)
                    break;
            }
            return sb.ToString();
        }

    }

}
