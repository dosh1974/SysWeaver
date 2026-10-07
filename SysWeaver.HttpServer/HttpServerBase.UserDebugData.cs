using System;
using System.Text;
using SysWeaver.Data;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// A row in the active users debug table (<see cref="ActiveUsers"/>).
        /// </summary>
        /// <remarks>
        /// <see cref="Sessions"/> contains redacted session tokens (only the first 6 characters, same as <see cref="ActiveSessions"/>).
        /// </remarks>
        [TableDataPrimaryKey(nameof(Name))]

        sealed class UserDebugData
        {
            /// <summary>
            /// Create a snapshot of a user, sessions that can expire are skipped.
            /// </summary>
            /// <param name="d">The user data</param>
            /// <param name="nowTick">The current UTC time in ticks</param>
            public UserDebugData(UserData d, long nowTick)
            {
                var a = d.Auth;
                Name = a.Username;
                Email = a.Email;
                NickName = a.NickName;
                Gen = a.AutoNickName;
                var t = a.Tokens;
                Auth = (t == null) || (t.Count <= 0) ? null : String.Join(',', a.Tokens);
                var sb = new StringBuilder();
                int c = 0;
                foreach (var sk in d.Sessions)
                {
                    var s = sk.Key;
                    if (s.CanExpire(nowTick))
                        continue;
                    ++c;
                    if (sb.Length > 0)
                        sb.Append(',');
                    //  Redact the token (same as ActiveSessions), the full token would allow anyone seeing this table to hijack the session
                    var token = s.Token;
                    if ((token != null) && (token.Length > 6))
                        sb.Append(token, 0, 6).Append("****");
                    else
                        sb.Append("****");
                    sb.Append(':');
                    var dur = (nowTick - s.Start) / TimeSpan.TicksPerSecond;
                    sb.Append("Duration: ").Append(dur).AppendLine(" seconds.");
                    sb.Append("Address: ").Append(s.Address).AppendLine(".");
                    sb.Append("User agent: ").Append(s.UserAgent?.Replace(',', '¤'));
                }
                SessionCount = c;
                Sessions = sb.ToString();
            }
            /// <summary>
            /// User name
            /// </summary>
            public String Name;
            /// <summary>
            /// User email
            /// </summary>
            [TableDataKey]
            public String Email;
            
            /// <summary>
            /// User selectable nick name, displayed on public pages etc
            /// </summary>
            public String NickName;

            /// <summary>
            /// True if the nick name is auto generated (not selected by the user)
            /// </summary>
            public bool Gen;

            /// <summary>
            /// Auth information, null = open, empty = auth required or comma separted tokens that are required
            /// </summary>
            [TableDataTags("{^0}", null, "{0}", true)]
            public String Auth;

            /// <summary>
            /// Number active sessions that the user is logged in to.
            /// </summary>
            public int SessionCount;

            /// <summary>
            /// Information about each session that the user is logged in to (token, duration, address and user agent).
            /// </summary>
            [TableDataTags("{1}", "{2}\n", "Token: {1}.\n{2}", true)]
            public String Sessions;
        }



    }

}
