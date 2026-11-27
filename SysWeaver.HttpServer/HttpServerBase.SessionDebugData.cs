using System;
using SysWeaver.Data;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// A row in the active sessions debug table (<see cref="ActiveSessions"/>), also returned by <see cref="SessionDetails"/>.
        /// </summary>
        /// <remarks>By default the table shows who (User, Address, UserAgent, Auth), when (Started, LastActivity), the state (Expired, Banned) and the
        /// actions; the other columns are [TableDataHide] (still sent, the user can show them with the column picker).
        /// The ban columns (probe trap) and intrusion columns are set by <see cref="SetSecurityInfo"/>.</remarks>
        [SysWeaver.Data.TableDataCard("+session@monitoring/cards")]
        [TableDataPrimaryKey(nameof(TokenHash))]
        [TableDataDefaultOrder("-" + nameof(LastActivity))]
        public sealed class SessionDebugData
        {
            /// <summary>
            /// Create a snapshot of a session.
            /// </summary>
            /// <param name="s">The session</param>
            /// <param name="utcNow">The current UTC time</param>
            /// <param name="ended">True if the session is no longer active (it expired or was ended), it is then shown as expired</param>
            /// <param name="endedUser">The user that was signed in to an ended session (its auth is cleared when it ends), null if none or unknown</param>
            /// <param name="flag">True if the server serves the language flags (<see cref="HaveIsoFlags"/>), else <see cref="Flag"/> is null (no image that can't load)</param>
            internal SessionDebugData(HttpSession s, DateTime utcNow, bool ended = false, String endedUser = null, bool flag = true)
            {
                User = endedUser;
                var nowTick = utcNow.Ticks;
                TokenHash = s.TokenHash;
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
            /// The session id: the hash of the session token (<see cref="HttpSession.TokenHash"/>), the same id as in the audits and logs.
            /// The token itself is never shown (anyone knowing it could use the session).
            /// </summary>
            [TableDataHide]
            public readonly String TokenHash;

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
            [TableDataHide]
            [TableDataIsoLanguage]
            public readonly String Flag;

            /// <summary>
            /// The language to use
            /// </summary>
            [TableDataHide]
            [TableDataIsoLanguage]
            public readonly String Language;

            /// <summary>
            /// The client language
            /// </summary>
            [TableDataHide]
            [TableDataIsoLanguage]
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
            /// True if the session or its client address is banned by the probe trap (it asked for a url that only probes ask for, ex: ".env"):
            /// every request gets a trap answer (a decoy secret file or a decompression bomb)
            /// </summary>
            public bool Banned;

            /// <summary>
            /// When the ban ends (the later of the session and address bans), null if not banned
            /// </summary>
            [TableDataHide]
            public DateTime? BannedUntil;

            /// <summary>
            /// When the ban of the session ends, null if the session isn't banned
            /// </summary>
            [TableDataHide]
            public DateTime? SessionBannedUntil;

            /// <summary>
            /// When the ban of the client address ends (see the ip bans table), null if the address isn't banned
            /// </summary>
            [TableDataHide]
            public DateTime? AddressBannedUntil;

            /// <summary>
            /// True if the client address is never banned (HttpProbeTrapParams.NeverBan, ex: a local reverse proxy), only the session is banned
            /// </summary>
            [TableDataHide]
            public bool NeverBanned;

            /// <summary>
            /// The probe url that banned the session or the address, null if not banned
            /// </summary>
            [TableDataHide]
            public String BanUrl;

            /// <summary>
            /// The number of trap answers to the client address while banned, null if the address isn't banned
            /// </summary>
            [TableDataHide]
            public long? TrapAnswers;

            /// <summary>
            /// The latest trap answer to the client address, ex: "Bomb (br)", "Decoy (EnvFile)", null if none
            /// </summary>
            [TableDataHide]
            public String TrapLastAnswer;

            /// <summary>
            /// Intrusion detection: the number of different urls with an error response (400, 404 etc) from the client address within the window, null if the address isn't tracked
            /// </summary>
            [TableDataHide]
            public int? IntrusionErrorUrls;

            /// <summary>
            /// Intrusion detection: the number of calls to tracked API's (sign in etc) from the client address within the window, null if the address isn't tracked
            /// </summary>
            [TableDataHide]
            public long? IntrusionApiCalls;

            /// <summary>
            /// Intrusion detection: when an intrusion was last reported for the client address (its error responses and tracked API calls are then delayed), null if never (within the window)
            /// </summary>
            [TableDataHide]
            public DateTime? IntrusionReported;

            /// <summary>
            /// The token needed to lift the ban of the session and its address (see <see cref="SessionLiftBanToken"/>), null if not banned
            /// </summary>
            [TableDataIgnore]
            public String LiftToken;

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
            //  The value is "endToken,who,liftToken" ({0}, {2} and {3} in the action formats), empty when there is nothing to do.
            //  The end token is empty when the session can't be ended here (expired, not signed in, or your own session), the lift token when nothing is banned:
            //  the action is then hidden, and the request must include the token (so a plain link or GET request can't end a session or lift a ban).
            //  End session is final (G47, declared, never guessed from the words): the web table keeps the row muted without actions until the user refreshes.
            //  Lift ban is not final (the session stays, the row is re-read).
            [TableDataActionsFull(new[] { TableDataActionFlags.Final, TableDataActionFlags.None },
    "End session",
    "End this session: the user is signed out on that device and must sign in again",
    "../Api/debug/EndSession?\"{0}\"",
    "../icons/user_logout.svg",
    "End the session of {2}?",
    "{0}",
    "",
    "Lift ban",
    "Lift the probe trap ban of this session and its address: they can use the site again (the intrusion detection state of the address is cleared too)",
    "../Api/debug/LiftSessionBan?\"{3}\"",
    "../icons/unlock.svg",
    "Lift the ban of {2}?",
    "{3}",
    "The ban of {2} was lifted"
    )]
            [TableDataOrder(100)]
            public String Actions => (EndToken == null) && (LiftToken == null) ? "" : String.Concat(EndToken ?? "", ",", User == null ? "a visitor who is not signed in" : User.Replace(',', ' '), ",", LiftToken ?? "");

        }



    }

}
