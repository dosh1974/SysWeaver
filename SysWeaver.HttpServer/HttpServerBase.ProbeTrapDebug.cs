using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SysWeaver.Auth;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        #region Ban info of the sessions

        /// <summary>
        /// The token needed to lift the ban of a session with <see cref="LiftSessionBan(string, HttpServerRequest)"/> (like <see cref="SessionEndToken"/>, a different purpose).
        /// </summary>
        static String SessionLiftBanToken(HttpSession session)
        {
            var h = HMACSHA256.HashData(SessionEndKey, Encoding.UTF8.GetBytes(String.Concat("liftban:", session.TokenHash ?? "")));
            return Convert.ToBase64String(h, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Fill in the ban (probe trap) and intrusion detection columns of a session row
        /// </summary>
        /// <param name="d">The row</param>
        /// <param name="s">The session</param>
        /// <param name="nowTicks">The current UTC time in ticks</param>
        SessionDebugData SetSecurityInfo(SessionDebugData d, HttpSession s, long nowTicks)
        {
            var sb = Interlocked.Read(ref s.BannedUntilTicks);
            if (sb > nowTicks)
            {
                d.SessionBannedUntil = new DateTime(sb, DateTimeKind.Utc);
                d.BanUrl = s.BannedUrl;
            }
            var key = HttpIpRateLimiters.GetKey(s.Address);
            var t = ProbeTrap;
            if (t != null)
            {
                var ban = t.Find(key);
                if (ban != null)
                {
                    d.AddressBannedUntil = new DateTime(ban.UntilTicks, DateTimeKind.Utc);
                    d.BanUrl ??= ban.TriggerUrl;
                    d.TrapAnswers = Interlocked.Read(ref ban.Requests);
                    d.TrapLastAnswer = ban.LastAnswer;
                }
                d.NeverBanned = t.IsNeverBanned(s.Address);
            }
            var bu = d.SessionBannedUntil > d.AddressBannedUntil ? d.SessionBannedUntil : (d.AddressBannedUntil ?? d.SessionBannedUntil);
            d.BannedUntil = bu;
            d.Banned = bu != null;
            if (d.Banned)
                d.LiftToken = SessionLiftBanToken(s);
            var i = Intrusions;
            if ((i != null) && i.TryGetAddressInfo(key, out var ii))
            {
                d.IntrusionErrorUrls = ii.ErrorUrls;
                d.IntrusionApiCalls = ii.ApiRequests;
                d.IntrusionReported = ii.LastReport;
            }
            return d;
        }

        #endregion//Ban info of the sessions

        #region Lift bans

        /// <summary>
        /// Lift the ban of an address key: the address ban, the bans of all sessions from the address and its intrusion detection state
        /// </summary>
        /// <param name="key">The address key (<see cref="HttpIpRateLimiters.GetKey(string)"/>)</param>
        /// <param name="session">A session to unban (it may be from another address, ex: a NeverBan address), null for none</param>
        /// <param name="by">The user that lifted the ban (for the log)</param>
        /// <returns>The number of lifted bans (the address and sessions)</returns>
        int LiftBans(String key, HttpSession session, String by)
        {
            int c = 0;
            var t = ProbeTrap;
            var ban = t?.Lift(key);
            if (ban != null)
                ++c;
            void Unban(HttpSession s)
            {
                if (Interlocked.Exchange(ref s.BannedUntilTicks, 0) != 0)
                    ++c;
            }
            if (session != null)
                Unban(session);
            foreach (var s in Sessions.Values)
                if (String.Equals(HttpIpRateLimiters.GetKey(s.Address), key, StringComparison.Ordinal))
                    Unban(s);
            Intrusions?.Forget(key);
            if (c > 0)
                Msg?.AddMessage(String.Concat(Prefix, "Probe trap: the ban of ", key, ban == null ? "" : String.Concat(" (it asked for \"", ban.TriggerUrl, "\")"), " was lifted by \"", by, "\""), MessageLevels.Info);
            return c;
        }

        /// <summary>
        /// Lift the ban of a client address (from the "Lift ban" action of the ip bans table): the address can use the site again, the sessions
        /// from the address are unbanned and its intrusion detection state is cleared.
        /// The client may still hold the large cookies of the trap answers (they expire with the ban), clearing the site cookies of the browser removes them.
        /// </summary>
        /// <param name="token">The lift token of the ban (from the ip bans table)</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>True when the ban was lifted (it never returns false: a ban that is not found is a 409 with the reason)</returns>
        /// <exception cref="HttpResponseException">400: the token is missing; 409: the ban has already ended (it expired or was lifted)</exception>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.AdminOps)]
        public bool LiftIpBan(String token, HttpServerRequest context)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > 64))
                throw new HttpResponseException(400, "A ban lift token is required");
            var ban = ProbeTrap?.FindByLiftToken(token);
            if (ban == null)
                throw HttpResponseException.AlreadyDone("The ban has already ended (it expired or was lifted)");
            LiftBans(ban.Key, null, context?.Session?.Auth?.Username ?? "?");
            return true;
        }

        /// <summary>
        /// Lift the ban of a session (from the "Lift ban" action of the active sessions table): the session and its client address can use the site again,
        /// the other sessions from the address are unbanned and the intrusion detection state of the address is cleared.
        /// The client may still hold the large cookies of the trap answers (they expire with the ban), clearing the site cookies of the browser removes them.
        /// </summary>
        /// <param name="token">The lift token of the session (from the active sessions table)</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>True when the ban was lifted (it never returns false: a session that is not banned is a 409 with the reason)</returns>
        /// <exception cref="HttpResponseException">400: the token is missing; 409: the session is gone or not banned (the ban expired or was lifted)</exception>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.AdminOps)]
        public bool LiftSessionBan(String token, HttpServerRequest context)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > 64))
                throw new HttpResponseException(400, "A ban lift token is required");
            var a = Encoding.ASCII.GetBytes(token);
            HttpSession found = null;
            foreach (var s in Sessions.Values.Concat(ExpiredSessions))
            {
                if (CryptographicOperations.FixedTimeEquals(a, Encoding.ASCII.GetBytes(SessionLiftBanToken(s))))
                {
                    found = s;
                    break;
                }
            }
            if ((found == null) || (LiftBans(HttpIpRateLimiters.GetKey(found.Address), found, context?.Session?.Auth?.Username ?? "?") <= 0))
                throw HttpResponseException.AlreadyDone("The session isn't banned (the ban expired or was lifted)");
            return true;
        }

        #endregion//Lift bans

        #region Ip bans table

        /// <summary>
        /// A row in the ip bans table (<see cref="IpBans"/>): a client address that is banned by the probe trap.
        /// </summary>
        [TableDataPrimaryKey(nameof(Address))]
        [TableDataDefaultOrder("-" + nameof(Started))]
        sealed class IpBanDebugData
        {
            internal IpBanDebugData(BanEntry b, long nowTicks, IntrusionDetection intrusions)
            {
                Address = b.Key;
                LastAddress = b.Address;
                FirstBanned = new DateTime(b.FirstTicks, DateTimeKind.Utc);
                Started = new DateTime(Interlocked.Read(ref b.StartTicks), DateTimeKind.Utc);
                var u = Interlocked.Read(ref b.UntilTicks);
                Until = new DateTime(u, DateTimeKind.Utc);
                Remaining = TimeSpan.FromTicks(Math.Max(0, u - nowTicks));
                LastSeen = new DateTime(Interlocked.Read(ref b.LastSeenTicks), DateTimeKind.Utc);
                TriggerUrl = b.TriggerUrl;
                LastUrl = b.LastUrl;
                var urls = b.GetUrls(out var uc);
                UrlCount = uc;
                Urls = String.Join('\n', urls) + (uc > urls.Length ? String.Concat("\n… ", (uc - urls.Length).ToString(System.Globalization.CultureInfo.InvariantCulture), " more") : "");
                UserAgent = b.UserAgent;
                Session = b.Session;
                Probes = Interlocked.Read(ref b.Probes);
                Requests = Interlocked.Read(ref b.Requests);
                Decoys = Interlocked.Read(ref b.Decoys);
                Bombs = Interlocked.Read(ref b.Bombs);
                Plain = Interlocked.Read(ref b.Plain);
                Empty = Interlocked.Read(ref b.Empty);
                LastAnswer = b.LastAnswer;
                if (b.DecoyIdentity >= 0)
                {
                    DecoyIdentity = b.DecoyIdentity;
                    DecoyAwsKey = b.DecoyAwsKey;
                }
                if ((intrusions != null) && intrusions.TryGetAddressInfo(b.Key, out var ii))
                {
                    IntrusionErrorUrls = ii.ErrorUrls;
                    IntrusionApiCalls = ii.ApiRequests;
                    IntrusionReported = ii.LastReport;
                }
                Actions = String.Concat(b.LiftToken, ",", b.Key);
            }

            /// <summary>
            /// The banned address (an IPv6 address is banned by its /64 network)
            /// </summary>
            public readonly String Address;

            /// <summary>
            /// The latest exact client address of the ban
            /// </summary>
            [TableDataIp]
            public readonly String LastAddress;

            /// <summary>
            /// When the current ban started
            /// </summary>
            public readonly DateTime Started;

            /// <summary>
            /// When the ban ends (unless it is extended by another probe, or lifted)
            /// </summary>
            public readonly DateTime Until;

            /// <summary>
            /// The time left of the ban
            /// </summary>
            public readonly TimeSpan Remaining;

            /// <summary>
            /// When the address was first banned (a ban that expired and was renewed by a new probe keeps it)
            /// </summary>
            [TableDataHide]
            public readonly DateTime FirstBanned;

            /// <summary>
            /// The latest request of the address
            /// </summary>
            public readonly DateTime LastSeen;

            /// <summary>
            /// The probe url that banned the address
            /// </summary>
            public readonly String TriggerUrl;

            /// <summary>
            /// The latest url requested by the address
            /// </summary>
            [TableDataHide]
            public readonly String LastUrl;

            /// <summary>
            /// The number of different urls requested by the address since it was banned
            /// </summary>
            public readonly int UrlCount;

            /// <summary>
            /// The urls requested by the address (the first ones, with the number of requests when more than one)
            /// </summary>
            [TableDataText(60)]
            public readonly String Urls;

            /// <summary>
            /// The number of probe urls that started or extended the ban
            /// </summary>
            [TableDataHide]
            public readonly long Probes;

            /// <summary>
            /// The number of requests while banned (all get a trap answer)
            /// </summary>
            public readonly long Requests;

            /// <summary>
            /// The number of decoy secret files served (a believable fake for a known secret file, ex: ".env")
            /// </summary>
            public readonly long Decoys;

            /// <summary>
            /// The number of decompression bombs served (the hostile answer to a client that accepts compression)
            /// </summary>
            public readonly long Bombs;

            /// <summary>
            /// The number of plain text trap answers served (the hostile answer to a client that accepts no compression)
            /// </summary>
            [TableDataHide]
            public readonly long Plain;

            /// <summary>
            /// The number of trap answers without a body (HEAD requests, or too many trap answers in progress)
            /// </summary>
            [TableDataHide]
            public readonly long Empty;

            /// <summary>
            /// The latest trap answer, ex: "Bomb (br)", "Decoy (EnvFile)"
            /// </summary>
            public readonly String LastAnswer;

            /// <summary>
            /// The decoy identity of the address (the set of fake credentials it got, see ProbeDecoy.FindIdentity), null if it got no decoy
            /// </summary>
            [TableDataHide]
            public readonly int? DecoyIdentity;

            /// <summary>
            /// The AWS access key id in the decoys of the address (a use of it shows up in CloudTrail when it is a canary key), null if it got no decoy
            /// </summary>
            [TableDataHide]
            public readonly String DecoyAwsKey;

            /// <summary>
            /// The user agent of the request that banned the address
            /// </summary>
            [TableDataUserAgent]
            public readonly String UserAgent;

            /// <summary>
            /// The session id (hash of the session token) of the latest probe, empty if the probe had no session
            /// </summary>
            [TableDataHide]
            [TableDataUrl("{0}", "*table.html?q=../Api/debug/ActiveSessions&f={\"Invert\":false,\"CaseSensitive\":false,\"Op\":6,\"Value\":\"{0}\",\"ColName\":\"TokenHash\",\"ColumnIndex\":0}", "Click to show the session if it is still active (or recently ended)")]
            public readonly String Session;

            /// <summary>
            /// Intrusion detection: the number of different urls with an error response (400, 404 etc) within the window, null if the address isn't tracked
            /// </summary>
            [TableDataHide]
            public readonly int? IntrusionErrorUrls;

            /// <summary>
            /// Intrusion detection: the number of calls to tracked API's (sign in etc) within the window, null if the address isn't tracked
            /// </summary>
            [TableDataHide]
            public readonly long? IntrusionApiCalls;

            /// <summary>
            /// Intrusion detection: when an intrusion was last reported for the address, null if never (within the window)
            /// </summary>
            [TableDataHide]
            public readonly DateTime? IntrusionReported;

            /// <summary>
            /// Actions on the ban
            /// </summary>
            //  The value is "liftToken,address" ({0} and {2} in the action formats), the request must include the token (a plain link can't lift a ban).
            //  Lift is final: the ban is gone, the web table keeps the row muted without actions until the user refreshes.
            [TableDataActionsFull(new[] { TableDataActionFlags.Final },
                "Lift ban",
                "Lift the ban: the address can use the site again (its sessions are unbanned, and its intrusion detection state is cleared)",
                "../Api/debug/LiftIpBan?\"{0}\"",
                "../icons/unlock.svg",
                "Lift the ban of {2}?",
                "{0}",
                "The ban of {2} was lifted"
            )]
            [TableDataOrder(100)]
            public readonly String Actions;
        }

        /// <summary>
        /// Get the client addresses that are banned by the probe trap (they asked for a url that only probes ask for, ex: ".env").
        /// </summary>
        /// <param name="r">Table parameters</param>
        /// <returns>The table data</returns>
        //  Developer notes:
        //  - The bans are kept in memory only (a restart lifts all bans), expired bans are not listed.
        //  - A ban can be lifted with its "Lift ban" action (LiftIpBan, the token is random per ban).
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuMonitoring + "/{0}", "IP bans", null, "IconTable", 22)]
        public TableData IpBans(TableDataRequest r)
        {
            var now = DateTime.UtcNow.Ticks;
            var t = ProbeTrap;
            var i = Intrusions;
            IEnumerable<IpBanDebugData> rows = t == null ? [] : t.GetBans().Where(x => Interlocked.Read(ref x.UntilTicks) > now).Select(x => new IpBanDebugData(x, now, i));
            return TableDataTools.Get(r, 5000, rows);
        }

        #endregion//Ip bans table

        #region Probe trap rules table

        /// <summary>
        /// A row in the probe trap rules table (<see cref="ProbeTrapRules"/>): what urls are trapped, and what the answer is.
        /// </summary>
        [TableDataPrimaryKey(nameof(Pattern))]
        sealed class ProbeTrapRuleDebugData
        {
            /// <summary>
            /// The url or the url pattern ({a,b} = one of the alternatives, * = any text)
            /// </summary>
            [TableDataText(80, "{0}", "{0}", true)]
            public String Pattern;

            /// <summary>
            /// Where the pattern matches
            /// </summary>
            public String Match;

            /// <summary>
            /// The answer: "Decoy" (a believable fake secret file), "Bomb" (the hostile answer: a decompression bomb, or plain text when the client accepts no compression)
            /// </summary>
            [TableDataTags]
            public String Answer;

            /// <summary>
            /// The kind of decoy file, empty for a bomb
            /// </summary>
            public String Decoy;

            /// <summary>
            /// True if the client address is banned (BanMinutes) when it asks for the url, false if only the url is answered by the trap
            /// </summary>
            public bool Bans;

            /// <summary>
            /// Where the rule comes from: "Built-in list" (srcData/ProbeUrls.txt), "ExtraUrls" (the configuration), "Decoy rules" (the known secret files), "Ban"
            /// </summary>
            public String Source;

            /// <summary>
            /// More about the rule and its answer
            /// </summary>
            [TableDataText(80)]
            public String Details;
        }

        /// <summary>
        /// Get the rules of the probe trap: what urls get a decoy secret file (a believable fake, ex: a ".env" with credentials that belong to nothing),
        /// what urls get a "bomb" (the hostile answer) and what a banned client gets.
        /// The trap only answers a request that hits no end point (a 404): a site that really serves one of the urls is never affected.
        /// </summary>
        /// <param name="r">Table parameters</param>
        /// <returns>The table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuMonitoring + "/{0}", "Probe trap rules", null, "IconTable", 23)]
        public TableData ProbeTrapRules(TableDataRequest r) => TableDataTools.Get(r, 5000, GetProbeTrapRules());

        IEnumerable<ProbeTrapRuleDebugData> GetProbeTrapRules()
        {
            var t = ProbeTrap;
            if (t == null)
                yield break;
            var p = t.P;
            var bans = t.BanTicks > 0;
            var useDecoys = t.Decoy != null;
            var bomb = String.Concat("A delay of ", p.MinDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture), "-", p.MaxDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " ms, status 200, hostile headers and cookies, and a decompression bomb in the best compression the client accepts (br: 10 GB, zstd: 1 GB, gzip / deflate: 50 MB unpacked), or ",
                p.UncompressedSize.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), " bytes of plain text. Above ",
                p.MaxConcurrent.ToString(System.Globalization.CultureInfo.InvariantCulture), " trap answers in progress: no delay and no body.");
            var decoy = String.Concat("A believable decoy file (", p.DecoyMinDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture), "-",
                p.DecoyMaxDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture), " ms, normal headers) with fake credentials of the identity of the client address.");
            var ban = bans ? String.Concat(" The address is banned for ", p.BanMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture), " minutes.") : "";
            //  The rules of the decoy files: also trap urls (a known secret file is a probe even when it isn't in the url list)
            foreach (var x in ProbeDecoy.GetRules())
            {
                yield return new ProbeTrapRuleDebugData
                {
                    Pattern = x.Pattern,
                    Match = x.Scope switch
                    {
                        ProbeDecoy.RuleScopes.UrlEnd => "Url ends with",
                        ProbeDecoy.RuleScopes.AnySegment => "Any folder or file name (not .well-known, .github*)",
                        _ => "Whole url",
                    },
                    Answer = useDecoys ? "Decoy" : "Bomb",
                    Decoy = useDecoys ? x.Type.ToString() : "",
                    Bans = bans,
                    Source = "Decoy rules",
                    Details = (useDecoys ? decoy : bomb) + ban,
                };
            }
            //  The url list: the decoy rules win for a known secret file
            foreach (var u in t.Urls.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var type = useDecoys ? ProbeDecoy.Detect(u) : ProbePayloadTypes.None;
                var below = useDecoys ? ProbeDecoy.Detect("folder/" + u) : ProbePayloadTypes.None;
                String answer;
                String details;
                if (type == ProbePayloadTypes.None)
                {
                    answer = below == ProbePayloadTypes.None ? "Bomb" : "Bomb,Decoy";
                    details = bomb;
                }
                else
                {
                    answer = below == ProbePayloadTypes.None ? "Decoy,Bomb" : "Decoy";
                    details = below == ProbePayloadTypes.None ? decoy + " Below a folder (ex: \"folder/" + u + "\") it gets a bomb instead (the decoy rule only matches the whole url)." : decoy;
                }
                yield return new ProbeTrapRuleDebugData
                {
                    Pattern = u,
                    Match = "Whole url, or below any folder",
                    Answer = answer,
                    Decoy = type != ProbePayloadTypes.None ? type.ToString() : "",
                    Bans = bans,
                    Source = t.ExtraUrls.Contains(u) ? "ExtraUrls" : "Built-in list",
                    Details = details + ban,
                };
            }
            if (bans)
            {
                yield return new ProbeTrapRuleDebugData
                {
                    Pattern = "*",
                    Match = "Any url of a banned address or session",
                    Answer = useDecoys ? "Decoy,Bomb" : "Bomb",
                    Decoy = "",
                    Bans = false,
                    Source = "Ban",
                    Details = String.Concat("While banned every request gets a trap answer (no session is created for a banned address): ", useDecoys ? "a decoy for a known secret file (the decoy rules), else " : "", bomb),
                };
            }
        }

        #endregion//Probe trap rules table
    }
}
