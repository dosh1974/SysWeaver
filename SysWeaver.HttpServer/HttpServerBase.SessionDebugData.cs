using System;
using SysWeaver.Data;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// A row in the active sessions debug table (<see cref="ActiveSessions"/>).
        /// </summary>
        /// <remarks>By default the table shows who (User, Address, UserAgent, Auth), when (Started, LastActivity), the state (Expired) and the
        /// actions; the other columns are [TableDataHide] (still sent, the user can show them with the column picker).</remarks>
        [TableDataPrimaryKey(nameof(Token))]
        [TableDataDefaultOrder("-" + nameof(LastActivity))]
        sealed class SessionDebugData
        {
            /// <summary>
            /// Create a snapshot of a session.
            /// </summary>
            /// <param name="s">The session</param>
            /// <param name="utcNow">The current UTC time</param>
            /// <param name="ended">True if the session is no longer active (it expired or was ended), it is then shown as expired</param>
            /// <param name="endedUser">The user that was signed in to an ended session (its auth is cleared when it ends), null if none or unknown</param>
            /// <param name="flag">True if the server serves the language flags (<see cref="HaveIsoFlags"/>), else <see cref="Flag"/> is null (no image that can't load)</param>
            public SessionDebugData(HttpSession s, DateTime utcNow, bool ended = false, String endedUser = null, bool flag = true)
            {
                User = endedUser;
                var nowTick = utcNow.Ticks;
                Token = s.Token;
                var st = new DateTime(s.Start, DateTimeKind.Utc);
                LastActivity = new DateTime(s.LastActivity, DateTimeKind.Utc);
                var count = s.RequestInProgress;
                if (count == 0)
                    Last = utcNow - LastActivity;
                Started = st;
                Expiration = new DateTime(s.ExpirationTime, DateTimeKind.Utc);
                Timeout = TimeSpan.FromTicks(s.KeepAliveDurationTicks);
                Expired = ended || s.CanExpire(nowTick);
                Duration = utcNow - st;
                var a = s.Auth;
                if (a != null)
                {
                    User = a.Username ?? endedUser;
                    var t = a.Tokens;
                    if (t != null)
                        Auth = String.Join(',', t);
                    else
                        Auth = "-";
                    Weak = a.WeakMethod;
                }
                Address = s.Address;
                UserAgent = s.UserAgent;
                Cache = s.Cache?.Count ?? 0;
                Protocol = s.HttpProtocol;
                Count = s.RequestCount;
                Active = count;
                DeviceId = s.DeviceId;
                Flag = flag ? s.Language : null;
                Language = s.Language;
                ClientTimeZone = s.ClientTimeZone;
                ClientLanguage = s.ClientLanguage;
            }

            /// <summary>
            /// Session token, this is the full token, <see cref="ActiveSessions"/> redacts it after the table has been built.
            /// </summary>
            [TableDataHide]
            public readonly String Token;

            /// <summary>
            /// True if the session has expired or was ended (it is no longer active)
            /// </summary>
            public readonly bool Expired;

            /// <summary>
            /// How long the session has been active
            /// </summary>
            [TableDataHide]
            public readonly TimeSpan Duration;

            /// <summary>
            /// How long ago the last activity was made (zero while a request is in progress)
            /// </summary>
            [TableDataHide]
            public readonly TimeSpan Last;

            /// <summary>
            /// The address of the connected client
            /// </summary>
            [TableDataIp]
            public readonly String Address;

            /// <summary>
            /// User if logged in
            /// </summary>
            public readonly String User;

            /// <summary>
            /// Auth tokens
            /// </summary>
            [TableDataTags]
            public readonly String Auth;

            /// <summary>
            /// If true, a weak auth method was used (Basic auth, Bearer token etc).
            /// If false, a proper login request was used
            /// </summary>
            [TableDataHide]
            public readonly bool Weak;

            /// <summary>
            /// Id of the device
            /// </summary>
            [TableDataHide]
            public readonly String DeviceId;

            /// <summary>
            /// Number of requests made in this session
            /// </summary>
            [TableDataHide]
            public readonly long Count;

            /// <summary>
            /// Number of active requests
            /// </summary>
            [TableDataHide]
            public readonly long Active;

            /// <summary>
            /// When the session started
            /// </summary>
            public readonly DateTime Started;

            /// <summary>
            /// The time when the last activity was made
            /// </summary>
            public readonly DateTime LastActivity;

            /// <summary>
            /// Session expiration, this is extended when session is in use
            /// </summary>
            [TableDataHide]
            public readonly DateTime Expiration;

            /// <summary>
            /// Session timeout (when a session haven't interacted for this long, it will die)
            /// </summary>
            [TableDataHide]
            public readonly TimeSpan Timeout;

            /// <summary>
            /// Number of cached entries
            /// </summary>
            [TableDataHide]
            public readonly long Cache;

            /// <summary>
            /// The http protocol used
            /// </summary>
            [TableDataHide]
            public readonly String Protocol;

            /// <summary>
            /// The flag of the language
            /// </summary>
            [TableDataIsoLanguageImage]
            [TableDataHide]
            public readonly String Flag;

            /// <summary>
            /// The language to use
            /// </summary>
            [TableDataHide]
            public readonly String Language;

            /// <summary>
            /// The client language
            /// </summary>
            [TableDataHide]
            public readonly String ClientLanguage;

            /// <summary>
            /// The client time zone
            /// </summary>
            [TableDataHide]
            public readonly String ClientTimeZone;


            /// <summary>
            /// User agent
            /// </summary>
            [TableDataUserAgent]
            public readonly String UserAgent;

            /// <summary>
            /// The token needed to end the session (see <see cref="SessionEndToken"/>), null for expired sessions, the caller's own session and sessions without a signed in user
            /// (ending an anonymous session only makes the browser start a new one with its next request, so it is not offered)
            /// </summary>
            [TableDataIgnore]
            public String EndToken;

            /// <summary>
            /// True on the row of the caller's own session (the session that asked for the table), so a page can mark it ("you").
            /// Hidden by default (still sent, the user can show it with the column picker): the column name is "IsCurrent".
            /// </summary>
            [TableDataHide]
            public bool IsCurrent;

            /// <summary>
            /// Actions on the session
            /// </summary>
            //  The value is "endToken,who" ({0} and {2} in the action formats), empty when the session can't be ended here (expired, not signed in, or your own session):
            //  the action is then hidden, and the request must include the token (so a plain link or GET request can't end a session).
            //  End session is final (G47, declared, never guessed from the words): the web table keeps the row muted without actions until the user refreshes.
            [TableDataActionsFull(new[] { TableDataActionFlags.Final },
    "End session",
    "End this session: the user is signed out on that device and must sign in again",
    "../Api/debug/EndSession?\"{0}\"",
    "../icons/user_logout.svg",
    "End the session of {2}?",
    "{0}",
    ""
    )]
            [TableDataOrder(100)]
            public String Actions => EndToken == null ? "" : String.Concat(EndToken, ",", User == null ? "a visitor who is not signed in" : User.Replace(',', ' '));

        }



    }

}
