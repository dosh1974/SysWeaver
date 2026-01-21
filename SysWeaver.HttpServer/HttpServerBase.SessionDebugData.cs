using System;
using SysWeaver.Data;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        [TableDataPrimaryKey(nameof(Token))]
        sealed class SessionDebugData
        {
            public SessionDebugData(HttpSession s, DateTime utcNow)
            {
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
                Expired = s.CanExpire(nowTick);
                Duration = utcNow - st;
                var a = s.Auth;
                if (a != null)
                {
                    User = a.Username;
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
                Flag = s.Language;
                Language = s.Language;
                ClientTimeZone = s.ClientTimeZone;
                ClientLanguage = s.ClientLanguage;
            }

            /// <summary>
            /// Session token (redacted)
            /// </summary>
            public readonly String Token;

            /// <summary>
            /// True if the session has expired
            /// </summary>
            public readonly bool Expired;

            /// <summary>
            /// How long the session has been active
            /// </summary>
            public readonly TimeSpan Duration;

            /// <summary>
            /// How long ago the last activity was made
            /// </summary>
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
            public readonly bool Weak;

            /// <summary>
            /// Id of the device
            /// </summary>
            public readonly String DeviceId;

            /// <summary>
            /// Number of requests made in this session
            /// </summary>
            public readonly long Count;

            /// <summary>
            /// Number of active requests
            /// </summary>
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
            public readonly DateTime Expiration;

            /// <summary>
            /// Session timeout (when a session haven't interacted for this long, it will die)
            /// </summary>
            public readonly TimeSpan Timeout;

            /// <summary>
            /// Number of cached entries
            /// </summary>
            public readonly long Cache;

            /// <summary>
            /// The http protocol used
            /// </summary>
            public readonly String Protocol;

            /// <summary>
            /// The flag of the language
            /// </summary>
            [TableDataIsoLanguageImage]
            public readonly String Flag;

            /// <summary>
            /// The language to use
            /// </summary>
            public readonly String Language;

            /// <summary>
            /// The client language
            /// </summary>
            public readonly String ClientLanguage;

            /// <summary>
            /// The client time zone
            /// </summary>
            public readonly String ClientTimeZone;


            /// <summary>
            /// User agent
            /// </summary>
            [TableDataUserAgent]
            public readonly String UserAgent;

        }



    }

}
