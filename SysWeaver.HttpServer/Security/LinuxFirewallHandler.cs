using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SysWeaver.Security
{
    /// <summary>
    /// A firewall handler for Linux, supporting the three most common firewalls: ufw (Ubuntu / Debian), firewalld (RHEL, Fedora, CentOS, SUSE) and plain nftables.
    /// The firewall that is active is detected (see <see cref="Detect"/>), rules are only added to that firewall.
    /// </summary>
    /// <remarks>
    /// <para>All changes (and most status queries) require root, a process that isn't root gets a warning and false (like <see cref="WindowsFirewallHandler"/> without elevation), "sudo" is never used.
    /// If no firewall is active, nothing blocks the port: <see cref="AddOrSet"/> and <see cref="Remove"/> return true (with an info / debug message).</para>
    /// <para>All programs are started directly (no shell) with every value passed as a separate argument.
    /// Rule names are sanitized (see <see cref="SanitizeRuleName"/>) by both <see cref="AddOrSet"/> and <see cref="Remove"/>, only names that are empty after sanitizing are refused.</para>
    /// <para>Rules that weren't added by this handler (other names / comments) are never changed or removed.</para>
    /// <para>How rules are identified and stored:</para>
    /// <list type="bullet">
    /// <item><b>ufw</b>: a rule with the rule name as comment, ex: "ufw allow in proto tcp from any to any port 443 comment 'name'" (IPv4 and IPv6 if enabled in ufw), permanent.
    /// Outbound rules allow traffic from the local port: "ufw allow out proto tcp from any port 443 to any".
    /// Existing rules are found by parsing the "### tuple ###" lines of "/etc/ufw/user.rules" and "/etc/ufw/user6.rules" and deleted by their exact rule spec.
    /// If an identical rule without (or with another) comment exists, it satisfies the rule: nothing is added for that address family (ufw would otherwise take over that rule and change its comment,
    /// so that a later <see cref="Remove"/> would delete it), an info message is logged.</item>
    /// <item><b>firewalld</b>: a SysWeaver owned service (see <see cref="GetFirewalldServiceName"/>, its short description is the rule name) with the port(s), added to the default zone, permanent and runtime.
    /// Creating or changing the service runs "firewall-cmd --reload" (that also discards runtime only changes made by others), validating an existing valid service doesn't.
    /// Ports opened by others (zone ports or other services) are left alone, the own service is added anyway.
    /// firewalld zones don't filter outgoing traffic, so an outbound rule isn't needed (true is returned without any change).</item>
    /// <item><b>nftables</b>: a rule like "tcp dport 443 accept comment \"name\"" is inserted first in every native filter base chain hooked to input (or output for outbound, "tcp sport 443"),
    /// in the families inet, ip and ip6 (tables named "firewalld" and the iptables-nft tables, with chains named INPUT / OUTPUT etc, are skipped).
    /// The rules are added to the existing chains (an accept in a separate table wouldn't override a drop in another table, since every base chain must accept a packet).
    /// The rules are runtime only (not saved to "/etc/nftables.conf"), they are lost on reboot or when the ruleset is reloaded (the http servers add them on every start).
    /// If no such chain exists, nothing filters the traffic and nothing is added.</item>
    /// </list>
    /// <para>Not supported: iptables only systems (iptables-legacy, or iptables rules without ufw / firewalld), those are reported as "no firewall".</para>
    /// </remarks>
    public sealed class LinuxFirewallHandler : IFirewallHandler
    {
        /// <summary>
        /// The singleton instance, using the real system (see <see cref="LinuxFirewallSystem"/>).
        /// </summary>
        public static readonly LinuxFirewallHandler Instance = new LinuxFirewallHandler(LinuxFirewallSystem.Instance);

        /// <summary>
        /// Create a firewall handler.
        /// </summary>
        /// <param name="system">The system to use, <see cref="LinuxFirewallSystem.Instance"/> for the real system</param>
        /// <exception cref="ArgumentNullException"><paramref name="system"/> is null</exception>
        public LinuxFirewallHandler(ILinuxFirewallSystem system)
        {
            ArgumentNullException.ThrowIfNull(system);
            Sys = system;
        }

        readonly ILinuxFirewallSystem Sys;

        /// <inheritdoc/>
        public override string ToString() => "Linux firewall handler (ufw, firewalld, nftables)";

        #region Constants

        /// <summary>
        /// The ufw config file, contains "ENABLED=yes" if ufw is enabled.
        /// </summary>
        public const String UfwConfigFile = "/etc/ufw/ufw.conf";

        /// <summary>
        /// The ufw defaults file, contains "IPV6=no" if ufw doesn't handle IPv6.
        /// </summary>
        public const String UfwDefaultsFile = "/etc/default/ufw";

        /// <summary>
        /// The ufw IPv4 user rules file (readable by root only).
        /// </summary>
        public const String UfwRulesFile = "/etc/ufw/user.rules";

        /// <summary>
        /// The ufw IPv6 user rules file (readable by root only).
        /// </summary>
        public const String UfwRules6File = "/etc/ufw/user6.rules";

        /// <summary>
        /// The prefix of the firewalld services created by this handler.
        /// </summary>
        public const String FirewalldServicePrefix = "sysweaver-";

        /// <summary>
        /// The maximum length of a rule name (nftables comments are limited to 128 bytes).
        /// </summary>
        public const int MaxRuleNameLength = 120;

        /// <summary>
        /// The folders that are searched for the programs after the PATH (a service may run with a PATH without the sbin folders).
        /// </summary>
        static readonly String[] KnownFolders = ["/usr/local/sbin", "/usr/local/bin", "/usr/sbin", "/usr/bin", "/sbin", "/bin"];

        /// <summary>
        /// The iptables chain names, iptables-nft creates base chains with these names (such tables are managed by iptables / ufw, not plain nftables).
        /// </summary>
        static readonly HashSet<String> IptablesChains = new HashSet<string>(StringComparer.Ordinal) { "INPUT", "OUTPUT", "FORWARD", "PREROUTING", "POSTROUTING" };

        #endregion//Constants

        #region Rule names

        /// <summary>
        /// Check if a string can be used as a rule name.
        /// Valid names have 1 to <see cref="MaxRuleNameLength"/> chars, don't start or end with a space and only contain ASCII letters, digits, spaces and any of "-_.:,()[]+=@/".
        /// No quotes, backslashes or shell meta characters (the names are used in ufw comments and quoted nftables comments).
        /// </summary>
        /// <param name="ruleName">The rule name</param>
        /// <returns>True if valid</returns>
        public static bool IsValidRuleName(String ruleName)
        {
            if (String.IsNullOrEmpty(ruleName))
                return false;
            var l = ruleName.Length;
            if (l > MaxRuleNameLength)
                return false;
            if ((ruleName[0] == ' ') || (ruleName[l - 1] == ' '))
                return false;
            foreach (var c in ruleName)
            {
                if (!IsAllowedChar(c))
                    return false;
            }
            return true;
        }

        static bool IsAllowedChar(Char c)
            => ((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z')) || ((c >= '0') && (c <= '9')) || (" -_.:,()[]+=@/".IndexOf(c) >= 0);

        /// <summary>
        /// Map some letters without a Unicode decomposition to ASCII.
        /// </summary>
        static readonly IReadOnlyDictionary<Char, String> Transliterations = new Dictionary<Char, String>
        {
            { 'ß', "ss" }, { 'æ', "ae" }, { 'Æ', "AE" }, { 'ø', "o" }, { 'Ø', "O" }, { 'œ', "oe" }, { 'Œ', "OE" },
            { 'đ', "d" }, { 'Đ', "D" }, { 'ð', "d" }, { 'Ð', "D" }, { 'ł', "l" }, { 'Ł', "L" }, { 'þ', "th" }, { 'Þ', "TH" }, { 'ı', "i" },
        };

        /// <summary>
        /// Make a valid rule name (see <see cref="IsValidRuleName"/>) from any string, deterministically: the same input always gives the same name,
        /// so <see cref="Remove"/> finds a rule added by <see cref="AddOrSet"/> (both use this method). A valid name is returned unchanged.
        /// <list type="number">
        /// <item>Diacritics are removed (Unicode decomposition, ex: "ä" gives "a") and some letters are transliterated (ex: "ß" gives "ss", "ø" gives "o").</item>
        /// <item>Any other char that isn't allowed is replaced by '_', a run of replaced chars gives a single '_' (ex: "a\"'b" gives "a_b").</item>
        /// <item>Leading and trailing spaces are removed.</item>
        /// <item>If the result is longer than <see cref="MaxRuleNameLength"/>, it's truncated and '_' + an 8 hex digit FNV-1a hash (of the UTF-8 input) is appended.</item>
        /// </list>
        /// </summary>
        /// <param name="ruleName">The rule name, may be null</param>
        /// <returns>The sanitized name, or null if nothing is left (null, empty or only spaces)</returns>
        public static String SanitizeRuleName(String ruleName)
        {
            if (ruleName == null)
                return null;
            if (IsValidRuleName(ruleName))
                return ruleName;
            StringBuilder sb = new StringBuilder(ruleName.Length);
            bool replaced = false;
            foreach (var c in ruleName.Normalize(NormalizationForm.FormD))
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if ((cat == UnicodeCategory.NonSpacingMark) || (cat == UnicodeCategory.SpacingCombiningMark) || (cat == UnicodeCategory.EnclosingMark))
                    continue;
                if (IsAllowedChar(c))
                {
                    sb.Append(c);
                    replaced = false;
                    continue;
                }
                if (Transliterations.TryGetValue(c, out var t))
                {
                    sb.Append(t);
                    replaced = false;
                    continue;
                }
                if (!replaced)
                    sb.Append('_');
                replaced = true;
            }
            var r = sb.ToString().Trim(' ');
            if (r.Length <= 0)
                return null;
            if (r.Length > MaxRuleNameLength)
                r = r.Substring(0, MaxRuleNameLength - 9).TrimEnd(' ') + "_" + Fnv1a(ruleName).ToString("x8", CultureInfo.InvariantCulture);
            return r;
        }

        static uint Fnv1a(String s)
        {
            uint h = 2166136261;
            foreach (var b in Encoding.UTF8.GetBytes(s))
            {
                h ^= b;
                h *= 16777619;
            }
            return h;
        }

        /// <summary>
        /// Get the name of the firewalld service that is used for a rule name:
        /// <see cref="FirewalldServicePrefix"/> + the lower case name (other chars than a-z and 0-9 replaced by '-', at most 32 chars) + '-' + a 32 bit FNV-1a hash (8 hex digits) of the exact rule name (UTF-8).
        /// Ex: "SysWeaver MyApp 443" gives "sysweaver-sysweaver-myapp-443-xxxxxxxx".
        /// </summary>
        /// <param name="ruleName">The rule name</param>
        /// <returns>The service name</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ruleName"/> is null</exception>
        public static String GetFirewalldServiceName(String ruleName)
        {
            ArgumentNullException.ThrowIfNull(ruleName);
            StringBuilder sb = new StringBuilder(FirewalldServicePrefix.Length + 42);
            sb.Append(FirewalldServicePrefix);
            var start = sb.Length;
            bool dash = true;
            foreach (var cc in ruleName)
            {
                var c = cc;
                if ((c >= 'A') && (c <= 'Z'))
                    c = (Char)(c + 32);
                bool ok = ((c >= 'a') && (c <= 'z')) || ((c >= '0') && (c <= '9'));
                if (!ok)
                {
                    if (dash)
                        continue;
                    c = '-';
                }
                dash = !ok;
                if ((sb.Length - start) >= 32)
                    break;
                sb.Append(c);
            }
            while ((sb.Length > start) && (sb[sb.Length - 1] == '-'))
                --sb.Length;
            if (sb.Length > start)
                sb.Append('-');
            sb.Append(Fnv1a(ruleName).ToString("x8", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        #endregion//Rule names

        #region Detection

        readonly Object DetectLock = new Object();
        LinuxFirewallDetection Cached;

        /// <summary>
        /// How long a detection result is cached by <see cref="Detect"/> (default 5 minutes), <see cref="TimeSpan.Zero"/> to always detect.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Forget the cached detection result, the next call to <see cref="Detect"/> (or any rule operation) detects again.
        /// </summary>
        public void ClearCache()
        {
            lock (DetectLock)
                Cached = null;
        }

        /// <summary>
        /// Detect what firewalls are installed, enabled and active, and which one is used.
        /// The result is cached for <see cref="CacheDuration"/>.
        /// </summary>
        /// <param name="refresh">True to ignore any cached result (and cache the new one)</param>
        /// <returns>The detection result, never null</returns>
        /// <remarks>
        /// Detection (in priority order, the first active firewall is used):
        /// <list type="number">
        /// <item>firewalld: "firewall-cmd" found, active if "firewall-cmd --state" prints "running" ("not running" or no system D-Bus: inactive,
        /// if not authorized: "systemctl is-active firewalld", unknown without systemd). Enabled: "systemctl is-enabled firewalld".</item>
        /// <item>ufw: "ufw" found, active if "ufw status" prints "Status: active" (root only, else unknown). Enabled: "ENABLED=yes" in <see cref="UfwConfigFile"/>.</item>
        /// <item>nftables: "nft" found, as root: active if the ruleset ("nft -j list ruleset") has a native filter base chain hooked to input or output (not firewalld's or iptables-nft's),
        /// not root: "systemctl is-active nftables" (unknown without systemd). Enabled: "systemctl is-enabled nftables".</item>
        /// </list>
        /// Programs are searched in the PATH and then in the usual sbin / bin folders.
        /// </remarks>
        public LinuxFirewallDetection Detect(bool refresh = false)
        {
            lock (DetectLock)
            {
                var c = Cached;
                if ((!refresh) && (c != null) && ((DateTime.UtcNow - c.Time) < CacheDuration))
                    return c;
                c = DetectNow();
                Cached = c;
                return c;
            }
        }

        String FindProgram(String name)
        {
            HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
            var sp = Sys.SearchPath;
            IEnumerable<String> dirs = String.IsNullOrEmpty(sp) ? KnownFolders : sp.Split(':').Concat(KnownFolders);
            foreach (var d in dirs)
            {
                if (String.IsNullOrEmpty(d))
                    continue;
                //  Only absolute folders (a relative folder in the PATH would depend on the current folder)
                if (d[0] != '/')
                    continue;
                if (!seen.Add(d))
                    continue;
                var f = d.EndsWith('/') ? (d + name) : (d + "/" + name);
                if (Sys.FileExists(f))
                    return f;
            }
            return null;
        }

        LinuxCommandResult Exec(String program, params String[] args) => Sys.Run(program, args);

        static String FirstLine(String text)
        {
            text = text.Trim();
            var i = text.IndexOf('\n');
            return (i < 0 ? text : text.Substring(0, i)).Trim();
        }

        static LinuxFirewallStates SystemctlIsActive(LinuxCommandResult r)
        {
            switch (FirstLine(r.Output))
            {
                case "active":
                case "reloading":
                case "activating":
                    return LinuxFirewallStates.Yes;
                case "inactive":
                case "failed":
                case "deactivating":
                    return LinuxFirewallStates.No;
            }
            return LinuxFirewallStates.Unknown;
        }

        static LinuxFirewallStates SystemctlIsEnabled(LinuxCommandResult r)
        {
            switch (FirstLine(r.Output))
            {
                case "enabled":
                case "enabled-runtime":
                    return LinuxFirewallStates.Yes;
                case "disabled":
                case "masked":
                case "masked-runtime":
                case "not-found":
                    return LinuxFirewallStates.No;
            }
            //  systemd < 250 prints this for a unit that doesn't exist
            if (r.Error.Contains("No such file or directory", StringComparison.Ordinal))
                return LinuxFirewallStates.No;
            return LinuxFirewallStates.Unknown;
        }

        static String GetConfigValue(String text, String key)
        {
            if (text == null)
                return null;
            String value = null;
            foreach (var lx in text.Split('\n'))
            {
                var l = lx.Trim();
                if (l.StartsWith('#'))
                    continue;
                var i = l.IndexOf('=');
                if (i <= 0)
                    continue;
                if (!String.Equals(l.Substring(0, i).Trim(), key, StringComparison.Ordinal))
                    continue;
                value = l.Substring(i + 1).Trim().Trim('"', '\'');
            }
            return value;
        }

        LinuxFirewallDetection DetectNow()
        {
            var isRoot = Sys.IsRoot;
            var systemctl = FindProgram("systemctl");
            var hasSystemd = (systemctl != null) && Sys.DirectoryExists("/run/systemd/system");
            List<LinuxFirewallInfo> installed = new List<LinuxFirewallInfo>(3);
            //  firewalld
            var fw = FindProgram("firewall-cmd");
            if (fw != null)
            {
                var i = new LinuxFirewallInfo
                {
                    Kind = LinuxFirewallKinds.Firewalld,
                    Program = fw,
                };
                var v = FirstLine(Exec(fw, "--version").Output);
                if ((v.Length > 0) && Char.IsAsciiDigit(v[0]))
                    i.Version = v;
                i.Enabled = hasSystemd ? SystemctlIsEnabled(Exec(systemctl, "is-enabled", "firewalld")) : LinuxFirewallStates.Unknown;
                var r = Exec(fw, "--state");
                var all = r.AllOutput;
                if (FirstLine(r.Output) == "running")
                    i.Active = LinuxFirewallStates.Yes;
                else if (all.Contains("not running", StringComparison.Ordinal))
                    i.Active = LinuxFirewallStates.No;
                else if (all.Contains("DBUS_ERROR", StringComparison.Ordinal))
                {
                    //  No system bus => firewalld can't run
                    i.Active = LinuxFirewallStates.No;
                    i.Note = "(no D-Bus)";
                }
                else if (hasSystemd)
                {
                    i.Active = SystemctlIsActive(Exec(systemctl, "is-active", "firewalld"));
                    i.Note = "(state from systemd)";
                }
                else
                {
                    i.Note = "(firewall-cmd --state failed: " + FirstLine(all) + ")";
                }
                installed.Add(i);
            }
            //  ufw
            var ufw = FindProgram("ufw");
            if (ufw != null)
            {
                var i = new LinuxFirewallInfo
                {
                    Kind = LinuxFirewallKinds.Ufw,
                    Program = ufw,
                };
                var v = FirstLine(Exec(ufw, "version").Output);
                if (v.StartsWith("ufw ", StringComparison.Ordinal))
                    i.Version = v.Substring(4).Trim();
                var en = GetConfigValue(Sys.TryReadAllText(UfwConfigFile), "ENABLED");
                if (en != null)
                    i.Enabled = String.Equals(en, "yes", StringComparison.OrdinalIgnoreCase) ? LinuxFirewallStates.Yes : LinuxFirewallStates.No;
                if (isRoot)
                {
                    var r = Exec(ufw, "status");
                    var l = FirstLine(r.Output);
                    if (l == "Status: active")
                        i.Active = LinuxFirewallStates.Yes;
                    else if (l == "Status: inactive")
                        i.Active = LinuxFirewallStates.No;
                    else
                        i.Note = "(ufw status failed: " + FirstLine(r.AllOutput) + ")";
                }
                else
                {
                    i.Note = "(ufw status requires root)";
                }
                installed.Add(i);
            }
            //  nftables
            var nft = FindProgram("nft");
            if (nft != null)
            {
                var i = new LinuxFirewallInfo
                {
                    Kind = LinuxFirewallKinds.Nftables,
                    Program = nft,
                };
                var v = FirstLine(Exec(nft, "--version").Output);
                //  "nftables v1.0.9 (Old Doc Yak #3)"
                if (v.StartsWith("nftables v", StringComparison.Ordinal))
                {
                    v = v.Substring(10);
                    var e = v.IndexOf(' ');
                    i.Version = e < 0 ? v : v.Substring(0, e);
                }
                i.Enabled = hasSystemd ? SystemctlIsEnabled(Exec(systemctl, "is-enabled", "nftables")) : LinuxFirewallStates.Unknown;
                bool known = false;
                if (isRoot)
                {
                    var r = Exec(nft, "-j", "list", "ruleset");
                    if ((r.ExitCode == 0) && TryParseNft(r.Output, out var chains, out var _))
                    {
                        var native = chains.Where(x => x.IsNativeFilter && ((x.Hook == "input") || (x.Hook == "output"))).ToList();
                        i.Active = native.Count > 0 ? LinuxFirewallStates.Yes : LinuxFirewallStates.No;
                        if (native.Count > 0)
                            i.Note = "(chains: " + String.Join(", ", native.Select(x => x.ToString())) + ")";
                        known = true;
                    }
                    else
                    {
                        i.Note = "(nft list ruleset failed: " + FirstLine(r.AllOutput) + ")";
                    }
                }
                if (!known)
                {
                    if (hasSystemd)
                    {
                        i.Active = SystemctlIsActive(Exec(systemctl, "is-active", "nftables"));
                        i.Note = "(state from systemd)";
                    }
                    else if (i.Note == null)
                    {
                        i.Note = "(nft list ruleset requires root)";
                    }
                }
                installed.Add(i);
            }
            //  Select
            var used = installed.FirstOrDefault(x => x.Active == LinuxFirewallStates.Yes);
            bool certain;
            if (used != null)
            {
                //  Certain if no firewall with a higher priority might be active
                certain = !installed.TakeWhile(x => x != used).Any(x => x.Active == LinuxFirewallStates.Unknown);
            }
            else
            {
                used = installed.FirstOrDefault(x => x.LikelyActive);
                certain = installed.All(x => x.Active != LinuxFirewallStates.Unknown);
            }
            return new LinuxFirewallDetection
            {
                Time = DateTime.UtcNow,
                IsRoot = isRoot,
                HasSystemd = hasSystemd,
                Installed = installed.ToArray(),
                Used = used,
                IsCertain = certain,
            };
        }

        #endregion//Detection

        #region IFirewallHandler

        static readonly String[] ProtsTcp = ["tcp"];
        static readonly String[] ProtsUdp = ["udp"];
        static readonly String[] ProtsBoth = ["tcp", "udp"];

        static String[] GetProtocols(FirewallProtcols protocol) => protocol switch
        {
            FirewallProtcols.Tcp => ProtsTcp,
            FirewallProtcols.Udp => ProtsUdp,
            FirewallProtcols.TcpAndUdp => ProtsBoth,
            _ => null,
        };

        static bool PrepareName(ref String ruleName, IMessageHost msg, String p, String what, out String qn)
        {
            var s = SanitizeRuleName(ruleName);
            if (s == null)
            {
                qn = (ruleName ?? "null").ToQuoted();
                msg?.AddMessage(p + "Can't " + what + " firewall rule " + qn + ", the name is empty!", MessageLevels.Warning);
                return false;
            }
            qn = s.ToQuoted();
            if (!String.Equals(s, ruleName, StringComparison.Ordinal))
                msg?.AddMessage(p + "Using firewall rule name " + qn + " for " + ruleName.ToQuoted() + " (sanitized)");
            ruleName = s;
            return true;
        }

        static void LogFailure(IMessageHost msg, String p, String text, LinuxCommandResult r)
        {
            if (msg == null)
                return;
            msg.AddMessage(p + text + ", error: " + r.ExitCode, MessageLevels.Warning);
            using (msg.Tab())
            {
                foreach (var x in r.AllOutput.Split('\n'))
                {
                    var t = x.Trim();
                    if (t.Length > 0)
                        msg.AddMessage(p + t, MessageLevels.Info);
                }
            }
        }

        /// <summary>
        /// Make sure that an allow rule with the given name exists for the port (any local and remote address) in the active firewall (see <see cref="Detect"/>).
        /// An existing rule with the same name is validated and updated (deleted and re-added) if needed.
        /// </summary>
        /// <param name="ruleName">The name of the rule, must be valid (see <see cref="IsValidRuleName"/>)</param>
        /// <param name="port">The local port (1 to 65535)</param>
        /// <param name="msg">Optional message handler</param>
        /// <param name="messagePrefix">The prefix to use for messages, null for the default</param>
        /// <param name="protocol">The protocol(s)</param>
        /// <param name="direction">The direction, outbound rules allow traffic from the local port (no change is needed for firewalld)</param>
        /// <returns>True if a valid rule exists or was added / updated, or if no firewall is active (nothing blocks the port).
        /// False if the rule name is empty, the port is invalid, if the process isn't root (and a firewall is or might be active) or if a firewall command failed (a warning is logged).</returns>
        /// <remarks>The rule name is sanitized with <see cref="SanitizeRuleName"/> (an info message is logged if it's changed).</remarks>
        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            if (!PrepareName(ref ruleName, msg, p, "add", out var qn))
                return false;
            var prots = GetProtocols(protocol);
            if ((port <= 0) || (port > 65535) || (prots == null) || ((direction != FirewallDirections.Inbound) && (direction != FirewallDirections.Outbound)))
            {
                msg?.AddMessage(p + "Can't add firewall rule " + qn + ", invalid port (" + port + "), protocol (" + protocol + ") or direction (" + direction + ")!", MessageLevels.Warning);
                return false;
            }
            var name = qn + " for " + StringTools.RemoveCamelCase(direction.ToString()).FastToLower() + " " + StringTools.RemoveCamelCase(protocol.ToString()).FastToLower() + " traffic on port " + port;
            var d = Detect();
            var used = d.Used;
            if (used == null)
            {
                msg?.AddMessage(p + "No active firewall found" + (d.IsCertain ? "" : " (probably)") + ", traffic on port " + port + " isn't blocked, firewall rule " + qn + " isn't needed (" + d + ")");
                return true;
            }
            if (!d.IsRoot)
            {
                msg?.AddMessage(p + "Can't add firewall rule " + name + " to " + used.Kind + ", root is required!", MessageLevels.Warning);
                return false;
            }
            msg?.AddMessage(p + "Checking if " + used.Kind + " firewall rule " + name + " exists", MessageLevels.Debug);
            switch (used.Kind)
            {
                case LinuxFirewallKinds.Ufw:
                    return UfwAddOrSet(used.Program, ruleName, port, prots.Length > 1 ? "any" : prots[0], direction == FirewallDirections.Outbound, msg, p, qn, name);
                case LinuxFirewallKinds.Firewalld:
                    if (direction == FirewallDirections.Outbound)
                    {
                        msg?.AddMessage(p + "firewalld doesn't filter outgoing traffic, firewall rule " + name + " isn't needed");
                        return true;
                    }
                    return FirewalldAddOrSet(used.Program, ruleName, port, prots, msg, p, qn, name);
                case LinuxFirewallKinds.Nftables:
                    return NftAddOrSet(used.Program, ruleName, port, prots, direction == FirewallDirections.Outbound, msg, p, qn, name);
            }
            return false;
        }

        /// <summary>
        /// Delete all rules with the given name from the active firewall (see <see cref="Detect"/>).
        /// </summary>
        /// <param name="ruleName">The name of the rule</param>
        /// <param name="msg">Optional message handler</param>
        /// <param name="messagePrefix">The prefix to use for messages, null for the default</param>
        /// <returns>True if the rules were deleted, if no rule with that name exists or if no firewall is active.
        /// False if the rule name is empty, if the process isn't root (and a firewall is or might be active) or if a firewall command failed (a warning is logged).</returns>
        /// <remarks>The rule name is sanitized with <see cref="SanitizeRuleName"/> (like <see cref="AddOrSet"/> does).</remarks>
        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            if (!PrepareName(ref ruleName, msg, p, "remove", out var qn))
                return false;
            var d = Detect();
            var used = d.Used;
            if (used == null)
            {
                msg?.AddMessage(p + "No active firewall found" + (d.IsCertain ? "" : " (probably)") + ", firewall rule " + qn + " doesn't need to be removed", MessageLevels.Debug);
                return true;
            }
            if (!d.IsRoot)
            {
                msg?.AddMessage(p + "Can't remove firewall rule " + qn + " from " + used.Kind + ", root is required!", MessageLevels.Warning);
                return false;
            }
            msg?.AddMessage(p + "Deleting " + used.Kind + " rule " + qn, MessageLevels.Debug);
            switch (used.Kind)
            {
                case LinuxFirewallKinds.Ufw:
                    return UfwRemove(used.Program, ruleName, msg, p, qn);
                case LinuxFirewallKinds.Firewalld:
                    return FirewalldRemove(used.Program, ruleName, msg, p, qn);
                case LinuxFirewallKinds.Nftables:
                    return NftRemove(used.Program, ruleName, msg, p, qn);
            }
            return false;
        }

        #endregion//IFirewallHandler

        #region ufw

        /// <summary>
        /// A parsed "### tuple ###" line of the ufw user rules:
        /// "### tuple ### action proto dport dst sport src [dapp sapp] direction[_iface] [comment=hex]"
        /// </summary>
        sealed class UfwTuple
        {
            public String Action;
            public String Proto;
            public String DPort;
            public String Dst;
            public String SPort;
            public String Src;
            public String DApp;
            public String SApp;
            public String Dir;
            public String Iface;
            public String Comment;

            public String Key => String.Join(' ', Action, Proto, DPort, Dst, SPort, Src, DApp ?? "-", SApp ?? "-", Dir, Iface ?? "-");

            public override string ToString() => String.Join(' ', Action, Dir, Proto, "from", Src, SApp ?? SPort, "to", Dst, DApp ?? DPort);
        }

        static String DecodeHex(String hex)
        {
            if ((hex.Length & 1) != 0)
                return null;
            var b = new byte[hex.Length >> 1];
            for (int i = 0; i < b.Length; ++i)
            {
                if (!Byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out b[i]))
                    return null;
            }
            return Encoding.UTF8.GetString(b);
        }

        static List<UfwTuple> ParseUfwTuples(String text)
        {
            List<UfwTuple> res = new List<UfwTuple>();
            if (text == null)
                return res;
            const String marker = "### tuple ###";
            foreach (var lx in text.Split('\n'))
            {
                var l = lx.Trim();
                if (!l.StartsWith(marker, StringComparison.Ordinal))
                    continue;
                var t = l.Substring(marker.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                String comment = null;
                if ((t.Count > 0) && t[t.Count - 1].StartsWith("comment=", StringComparison.Ordinal))
                {
                    comment = DecodeHex(t[t.Count - 1].Substring(8));
                    t.RemoveAt(t.Count - 1);
                }
                if ((t.Count != 7) && (t.Count != 9))
                    continue;
                var dir = t[t.Count - 1];
                String iface = null;
                var di = dir.IndexOf('_');
                if (di > 0)
                {
                    iface = dir.Substring(di + 1);
                    dir = dir.Substring(0, di);
                }
                var u = new UfwTuple
                {
                    Action = t[0],
                    Proto = t[1],
                    DPort = t[2],
                    Dst = t[3],
                    SPort = t[4],
                    Src = t[5],
                    Dir = dir,
                    Iface = iface,
                    Comment = comment,
                };
                if (t.Count == 9)
                {
                    if (t[6] != "-")
                        u.DApp = t[6].Replace("%20", " ");
                    if (t[7] != "-")
                        u.SApp = t[7].Replace("%20", " ");
                }
                res.Add(u);
            }
            return res;
        }

        static UfwTuple UfwExpected(int port, String proto, bool outbound, bool v6)
        {
            var any = v6 ? "::/0" : "0.0.0.0/0";
            var ps = port.ToString(CultureInfo.InvariantCulture);
            return new UfwTuple
            {
                Action = "allow",
                Proto = proto,
                DPort = outbound ? "any" : ps,
                Dst = any,
                SPort = outbound ? ps : "any",
                Src = any,
                Dir = outbound ? "out" : "in",
            };
        }

        /// <summary>
        /// Get the "ufw delete" arguments that delete exactly the rule of a tuple.
        /// </summary>
        static String[] UfwDeleteArgs(UfwTuple t)
        {
            List<String> a = new List<string> { "delete" };
            a.AddRange(t.Action.Split('_', StringSplitOptions.RemoveEmptyEntries));
            a.Add(t.Dir);
            if (t.Iface != null)
            {
                a.Add("on");
                a.Add(t.Iface);
            }
            if ((t.Proto != "any") && (t.DApp == null) && (t.SApp == null))
            {
                a.Add("proto");
                a.Add(t.Proto);
            }
            a.Add("from");
            a.Add(t.Src);
            if (t.SApp != null)
            {
                a.Add("app");
                a.Add(t.SApp);
            }
            else if (t.SPort != "any")
            {
                a.Add("port");
                a.Add(t.SPort);
            }
            a.Add("to");
            a.Add(t.Dst);
            if (t.DApp != null)
            {
                a.Add("app");
                a.Add(t.DApp);
            }
            else if (t.DPort != "any")
            {
                a.Add("port");
                a.Add(t.DPort);
            }
            return a.ToArray();
        }

        /// <summary>
        /// Read the ufw rules, split into the ones with the given comment (ours) and all others (foreign).
        /// </summary>
        bool UfwGetRules(String ruleName, IMessageHost msg, String p, String qn, out List<UfwTuple> v4, out List<UfwTuple> v6, out List<UfwTuple> f4, out List<UfwTuple> f6)
        {
            var t4 = Sys.TryReadAllText(UfwRulesFile);
            v4 = null;
            v6 = null;
            f4 = null;
            f6 = null;
            if (t4 == null)
            {
                msg?.AddMessage(p + "Can't read " + UfwRulesFile.ToQuoted() + ", failed to check firewall rule " + qn, MessageLevels.Warning);
                return false;
            }
            bool mine(UfwTuple x) => String.Equals(x.Comment, ruleName, StringComparison.Ordinal);
            var a4 = ParseUfwTuples(t4);
            var a6 = ParseUfwTuples(Sys.TryReadAllText(UfwRules6File));
            v4 = a4.Where(mine).ToList();
            v6 = a6.Where(mine).ToList();
            f4 = a4.Where(x => !mine(x)).ToList();
            f6 = a6.Where(x => !mine(x)).ToList();
            return true;
        }

        bool UfwDelete(String ufw, IEnumerable<UfwTuple> rules, IMessageHost msg, String p, String qn)
        {
            bool ok = true;
            foreach (var t in rules)
            {
                var r = Exec(ufw, UfwDeleteArgs(t));
                if (r.ExitCode == 0)
                {
                    msg?.AddMessage(p + "Deleted ufw rule " + qn + " (" + t + ")", MessageLevels.Debug);
                    continue;
                }
                LogFailure(msg, p, "Failed to delete ufw rule " + qn + " (" + t + ")", r);
                ok = false;
            }
            return ok;
        }

        bool UfwAddOrSet(String ufw, String ruleName, int port, String proto, bool outbound, IMessageHost msg, String p, String qn, String name)
        {
            if (!UfwGetRules(ruleName, msg, p, qn, out var v4, out var v6, out var f4, out var f6))
                return false;
            var ipv6 = !String.Equals(GetConfigValue(Sys.TryReadAllText(UfwDefaultsFile), "IPV6"), "no", StringComparison.OrdinalIgnoreCase);
            var e4 = UfwExpected(port, proto, outbound, false).Key;
            var e6 = UfwExpected(port, proto, outbound, true).Key;
            //  An identical rule without (or with another) comment satisfies the rule: it's never taken over (ufw would change its comment) or duplicated
            var covered4 = f4.Any(x => x.Key == e4);
            var covered6 = (!ipv6) || f6.Any(x => x.Key == e6);
            var ok4 = ((v4.Count == 1) && (v4[0].Key == e4)) || ((v4.Count == 0) && covered4);
            var ok6 = ipv6 ? (((v6.Count == 1) && (v6[0].Key == e6)) || ((v6.Count == 0) && covered6)) : (v6.Count == 0);
            var foreignMsg = "an identical rule without the comment " + qn + " exists, it's not changed or removed";
            if (ok4 && ok6)
            {
                if ((v4.Count + v6.Count) <= 0)
                    msg?.AddMessage(p + "Firewall rule " + name + " isn't needed, " + foreignMsg);
                else if ((v4.Count == 0) || (ipv6 && (v6.Count == 0)))
                    msg?.AddMessage(p + qn + " exists and is valid (partly as a foreign rule, " + foreignMsg + ")");
                else
                    msg?.AddMessage(p + qn + " exists and is valid");
                return true;
            }
            var update = (v4.Count + v6.Count) > 0;
            if (update)
            {
                msg?.AddMessage(p + "Updating existing firewall rule " + name, MessageLevels.Debug);
                UfwDelete(ufw, v4.Concat(v6), msg, p, qn);
            }
            var need4 = !covered4;
            var need6 = ipv6 && !covered6;
            if (!(need4 || need6))
            {
                msg?.AddMessage(p + (update ? "Removed" : "Didn't add") + " firewall rule " + name + ", " + foreignMsg);
                return true;
            }
            if (!update)
                msg?.AddMessage(p + "Adding new firewall rule " + name, MessageLevels.Debug);
            //  Only add the address family that isn't covered by a foreign rule (adding "any" would take over the foreign rule)
            var addr = (need4 && (need6 || !ipv6)) ? "any" : (need4 ? "0.0.0.0/0" : "::/0");
            if ((addr != "any") && (msg != null))
                msg.AddMessage(p + "Only adding the " + (need4 ? "IPv4" : "IPv6") + " part of firewall rule " + qn + ", " + foreignMsg);
            List<String> a = new List<string> { "allow", outbound ? "out" : "in" };
            if (proto != "any")
            {
                a.Add("proto");
                a.Add(proto);
            }
            var ps = port.ToString(CultureInfo.InvariantCulture);
            if (outbound)
                a.AddRange(["from", addr, "port", ps, "to", addr]);
            else
                a.AddRange(["from", addr, "to", addr, "port", ps]);
            a.Add("comment");
            a.Add(ruleName);
            var r = Exec(ufw, a.ToArray());
            if (r.ExitCode == 0)
            {
                msg?.AddMessage(p + (update ? "Updated" : "Added") + " firewall rule " + name);
                return true;
            }
            LogFailure(msg, p, "Failed to " + (update ? "update" : "add") + " firewall rule " + qn, r);
            return false;
        }

        bool UfwRemove(String ufw, String ruleName, IMessageHost msg, String p, String qn)
        {
            if (!UfwGetRules(ruleName, msg, p, qn, out var v4, out var v6, out var _, out var _))
                return false;
            if ((v4.Count + v6.Count) <= 0)
            {
                msg?.AddMessage(p + "Firewall rule " + qn + " doesn't exist", MessageLevels.Debug);
                return true;
            }
            //  Delete the IPv6 rules first: deleting an IPv4 spec never touches IPv6 rules (and vice versa), the order doesn't matter
            if (!UfwDelete(ufw, v6.Concat(v4), msg, p, qn))
                return false;
            msg?.AddMessage(p + "Deleted firewall rule " + qn);
            return true;
        }

        #endregion//ufw

        #region firewalld

        static HashSet<String> FirewalldZonesWithService(String listAllZones, String service)
        {
            HashSet<String> zones = new HashSet<string>(StringComparer.Ordinal);
            String zone = null;
            foreach (var lx in listAllZones.Split('\n'))
            {
                var l = lx.TrimEnd('\r');
                if (l.Length <= 0)
                    continue;
                if (!Char.IsWhiteSpace(l[0]))
                {
                    var e = l.IndexOf(' ');
                    zone = e < 0 ? l : l.Substring(0, e);
                    continue;
                }
                l = l.Trim();
                if (zone == null)
                    continue;
                if (!l.StartsWith("services:", StringComparison.Ordinal))
                    continue;
                if (l.Substring(9).Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(service, StringComparer.Ordinal))
                    zones.Add(zone);
            }
            return zones;
        }

        bool FirewalldAddOrSet(String fw, String ruleName, int port, String[] prots, IMessageHost msg, String p, String qn, String name)
        {
            var svc = GetFirewalldServiceName(ruleName);
            var r = Exec(fw, "--get-default-zone");
            var zone = FirstLine(r.Output);
            if ((r.ExitCode != 0) || (zone.Length <= 0))
            {
                LogFailure(msg, p, "Failed to get the default firewalld zone, can't add firewall rule " + qn, r);
                return false;
            }
            var ps = port.ToString(CultureInfo.InvariantCulture);
            var expected = prots.Select(x => ps + "/" + x).ToHashSet(StringComparer.Ordinal);
            r = Exec(fw, "--permanent", "--service=" + svc, "--get-ports");
            var exists = r.ExitCode == 0;
            bool changed = false;
            bool inZone = false;
            bool update = false;
            if (exists)
            {
                var ports = r.Output.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                inZone = FirstLine(Exec(fw, "--permanent", "--zone=" + zone, "--query-service=" + svc).Output) == "yes";
                var inRuntime = FirstLine(Exec(fw, "--zone=" + zone, "--query-service=" + svc).Output) == "yes";
                if (inZone && inRuntime && ports.SetEquals(expected))
                {
                    msg?.AddMessage(p + qn + " exists and is valid");
                    return true;
                }
                update = true;
                msg?.AddMessage(p + "Updating existing firewall rule " + name, MessageLevels.Debug);
                foreach (var x in ports)
                {
                    if (expected.Contains(x))
                        continue;
                    r = Exec(fw, "--permanent", "--service=" + svc, "--remove-port=" + x);
                    if (r.ExitCode != 0)
                    {
                        LogFailure(msg, p, "Failed to update firewall rule " + qn, r);
                        return false;
                    }
                    changed = true;
                }
                foreach (var x in expected)
                {
                    if (ports.Contains(x))
                        continue;
                    r = Exec(fw, "--permanent", "--service=" + svc, "--add-port=" + x);
                    if (r.ExitCode != 0)
                    {
                        LogFailure(msg, p, "Failed to update firewall rule " + qn, r);
                        return false;
                    }
                    changed = true;
                }
            }
            else
            {
                msg?.AddMessage(p + "Adding new firewall rule " + name + " (service " + svc.ToQuoted() + ")", MessageLevels.Debug);
                List<String[]> cmds =
                [
                    ["--permanent", "--new-service=" + svc],
                    ["--permanent", "--service=" + svc, "--set-short=" + ruleName],
                    ["--permanent", "--service=" + svc, "--set-description=Added by SysWeaver, firewall rule: " + ruleName],
                ];
                foreach (var x in expected)
                    cmds.Add(["--permanent", "--service=" + svc, "--add-port=" + x]);
                foreach (var c in cmds)
                {
                    r = Exec(fw, c);
                    if (r.ExitCode != 0)
                    {
                        LogFailure(msg, p, "Failed to add firewall rule " + qn, r);
                        return false;
                    }
                }
                changed = true;
            }
            var verb = update ? "update" : "add";
            if (!inZone)
            {
                r = Exec(fw, "--permanent", "--zone=" + zone, "--add-service=" + svc);
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to " + verb + " firewall rule " + qn, r);
                    return false;
                }
                changed = true;
            }
            if (!changed)
            {
                //  Only missing in the runtime config
                r = Exec(fw, "--zone=" + zone, "--add-service=" + svc);
                changed = r.ExitCode != 0;
            }
            if (changed)
            {
                r = Exec(fw, "--reload");
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to " + verb + " firewall rule " + qn + " (reload failed)", r);
                    return false;
                }
            }
            msg?.AddMessage(p + (update ? "Updated" : "Added") + " firewall rule " + name);
            return true;
        }

        bool FirewalldRemove(String fw, String ruleName, IMessageHost msg, String p, String qn)
        {
            var svc = GetFirewalldServiceName(ruleName);
            var exists = Exec(fw, "--permanent", "--service=" + svc, "--get-ports").ExitCode == 0;
            var r = Exec(fw, "--permanent", "--list-all-zones");
            if (r.ExitCode != 0)
            {
                LogFailure(msg, p, "Failed to delete firewall rule " + qn + " (can't list the zones)", r);
                return false;
            }
            var permZones = FirewalldZonesWithService(r.Output, svc);
            r = Exec(fw, "--list-all-zones");
            var runZones = r.ExitCode == 0 ? FirewalldZonesWithService(r.Output, svc) : new HashSet<String>();
            if ((!exists) && (permZones.Count <= 0) && (runZones.Count <= 0))
            {
                msg?.AddMessage(p + "Firewall rule " + qn + " doesn't exist", MessageLevels.Debug);
                return true;
            }
            bool ok = true;
            foreach (var z in permZones)
            {
                r = Exec(fw, "--permanent", "--zone=" + z, "--remove-service=" + svc);
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to delete firewall rule " + qn + " from zone " + z.ToQuoted(), r);
                    ok = false;
                }
            }
            foreach (var z in runZones)
            {
                r = Exec(fw, "--zone=" + z, "--remove-service=" + svc);
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to delete firewall rule " + qn + " from runtime zone " + z.ToQuoted(), r);
                    ok = false;
                }
            }
            if (ok && exists)
            {
                r = Exec(fw, "--permanent", "--delete-service=" + svc);
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to delete firewall rule " + qn + " (service " + svc.ToQuoted() + ")", r);
                    ok = false;
                }
            }
            if (ok)
                msg?.AddMessage(p + "Deleted firewall rule " + qn);
            return ok;
        }

        #endregion//firewalld

        #region nftables

        sealed class NftChain
        {
            public String Family;
            public String Table;
            public String Name;
            public String Type;
            public String Hook;

            public bool IsNativeFilter;

            public override string ToString() => String.Join(' ', Family, Table, Name);
        }

        sealed class NftRule
        {
            public String Family;
            public String Table;
            public String Chain;
            public long Handle;
            public String Comment;
            /// <summary>
            /// "proto field port", ex: "tcp dport 443" if the rule is exactly "[proto] [field] [port] accept", else null
            /// </summary>
            public String Simple;
        }

        static bool IsNftName(String s)
        {
            if (String.IsNullOrEmpty(s) || (s.Length > 64))
                return false;
            var c = s[0];
            if (!(((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z')) || (c == '_')))
                return false;
            foreach (var x in s)
            {
                if (((x >= 'a') && (x <= 'z')) || ((x >= 'A') && (x <= 'Z')) || ((x >= '0') && (x <= '9')) || (x == '_') || (x == '-') || (x == '.'))
                    continue;
                return false;
            }
            return true;
        }

        static String GetString(JsonElement e, String name) => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.String) ? v.GetString() : null;

        static String GetSimple(JsonElement rule)
        {
            if (!rule.TryGetProperty("expr", out var ex) || (ex.ValueKind != JsonValueKind.Array) || (ex.GetArrayLength() != 2))
                return null;
            var m = ex[0];
            if ((m.ValueKind != JsonValueKind.Object) || !m.TryGetProperty("match", out m))
                return null;
            if (GetString(m, "op") != "==")
                return null;
            if (!m.TryGetProperty("left", out var left) || (left.ValueKind != JsonValueKind.Object) || !left.TryGetProperty("payload", out var pl) || (pl.ValueKind != JsonValueKind.Object))
                return null;
            var proto = GetString(pl, "protocol");
            var field = GetString(pl, "field");
            if ((proto == null) || (field == null))
                return null;
            if (!m.TryGetProperty("right", out var right) || (right.ValueKind != JsonValueKind.Number) || !right.TryGetInt32(out var port))
                return null;
            var a = ex[1];
            if ((a.ValueKind != JsonValueKind.Object) || !a.TryGetProperty("accept", out var _))
                return null;
            return String.Join(' ', proto, field, port.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Parse the output of "nft -j list ruleset".
        /// </summary>
        static bool TryParseNft(String json, out List<NftChain> chains, out List<NftRule> rules)
        {
            chains = new List<NftChain>();
            rules = new List<NftRule>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("nftables", out var items) || (items.ValueKind != JsonValueKind.Array))
                    return false;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    if (item.TryGetProperty("chain", out var c) && (c.ValueKind == JsonValueKind.Object))
                    {
                        var ch = new NftChain
                        {
                            Family = GetString(c, "family"),
                            Table = GetString(c, "table"),
                            Name = GetString(c, "name"),
                            Type = GetString(c, "type"),
                            Hook = GetString(c, "hook"),
                        };
                        ch.IsNativeFilter =
                            (ch.Hook != null)
                            && (ch.Type == "filter")
                            && ((ch.Family == "inet") || (ch.Family == "ip") || (ch.Family == "ip6"))
                            && !String.Equals(ch.Table, "firewalld", StringComparison.OrdinalIgnoreCase)
                            && !((ch.Family != "inet") && IptablesChains.Contains(ch.Name ?? ""));
                        chains.Add(ch);
                        continue;
                    }
                    if (item.TryGetProperty("rule", out var r) && (r.ValueKind == JsonValueKind.Object))
                    {
                        var comment = GetString(r, "comment");
                        if (comment == null)
                            continue;
                        long handle = 0;
                        if (r.TryGetProperty("handle", out var h) && (h.ValueKind == JsonValueKind.Number))
                            h.TryGetInt64(out handle);
                        rules.Add(new NftRule
                        {
                            Family = GetString(r, "family"),
                            Table = GetString(r, "table"),
                            Chain = GetString(r, "chain"),
                            Handle = handle,
                            Comment = comment,
                            Simple = GetSimple(r),
                        });
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        bool NftGetRuleset(String nft, IMessageHost msg, String p, String what, out List<NftChain> chains, out List<NftRule> rules)
        {
            var r = Exec(nft, "-j", "list", "ruleset");
            if ((r.ExitCode == 0) && TryParseNft(r.Output, out chains, out rules))
                return true;
            chains = null;
            rules = null;
            LogFailure(msg, p, "Failed to " + what + " (can't list the nftables ruleset)", r);
            return false;
        }

        bool NftDelete(String nft, IEnumerable<NftRule> rules, IMessageHost msg, String p, String qn)
        {
            bool ok = true;
            foreach (var x in rules)
            {
                if (!(IsNftName(x.Family) && IsNftName(x.Table) && IsNftName(x.Chain) && (x.Handle > 0)))
                {
                    msg?.AddMessage(p + "Can't delete nftables rule " + qn + " from chain " + String.Join(' ', x.Family, x.Table, x.Chain).ToQuoted() + ", unsupported name", MessageLevels.Warning);
                    ok = false;
                    continue;
                }
                var r = Exec(nft, "delete", "rule", x.Family, x.Table, x.Chain, "handle", x.Handle.ToString(CultureInfo.InvariantCulture));
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to delete nftables rule " + qn + " from chain " + String.Join(' ', x.Family, x.Table, x.Chain).ToQuoted(), r);
                    ok = false;
                }
            }
            return ok;
        }

        bool NftAddOrSet(String nft, String ruleName, int port, String[] prots, bool outbound, IMessageHost msg, String p, String qn, String name)
        {
            if (!NftGetRuleset(nft, msg, p, "add firewall rule " + qn, out var chains, out var rules))
                return false;
            var hook = outbound ? "output" : "input";
            var field = outbound ? "sport" : "dport";
            List<NftChain> targets = new List<NftChain>();
            foreach (var c in chains)
            {
                if (!(c.IsNativeFilter && (c.Hook == hook)))
                    continue;
                if (!(IsNftName(c.Table) && IsNftName(c.Name)))
                {
                    msg?.AddMessage(p + "Can't add firewall rule " + qn + " to nftables chain " + c.ToString().ToQuoted() + ", unsupported name", MessageLevels.Warning);
                    continue;
                }
                targets.Add(c);
            }
            var mine = rules.Where(x => String.Equals(x.Comment, ruleName, StringComparison.Ordinal)).ToList();
            var ps = port.ToString(CultureInfo.InvariantCulture);
            //  Expected: one rule per target chain and protocol
            List<(NftChain Chain, String Proto, String Simple)> expected = new();
            foreach (var c in targets)
                foreach (var x in prots)
                    expected.Add((c, x, String.Join(' ', x, field, ps)));
            if (expected.Count <= 0)
            {
                if (mine.Count > 0)
                    NftDelete(nft, mine, msg, p, qn);
                msg?.AddMessage(p + "No nftables chain filters " + (outbound ? "outgoing" : "incoming") + " traffic, firewall rule " + qn + " isn't needed");
                return true;
            }
            bool valid = mine.Count == expected.Count;
            if (valid)
            {
                foreach (var e in expected)
                {
                    if (mine.Count(x => (x.Family == e.Chain.Family) && (x.Table == e.Chain.Table) && (x.Chain == e.Chain.Name) && (x.Simple == e.Simple)) == 1)
                        continue;
                    valid = false;
                    break;
                }
            }
            if (valid)
            {
                msg?.AddMessage(p + qn + " exists and is valid");
                return true;
            }
            var update = mine.Count > 0;
            if (update)
            {
                msg?.AddMessage(p + "Updating existing firewall rule " + name, MessageLevels.Debug);
                if (!NftDelete(nft, mine, msg, p, qn))
                    return false;
            }
            else
            {
                msg?.AddMessage(p + "Adding new firewall rule " + name, MessageLevels.Debug);
            }
            var comment = "\"" + ruleName + "\"";
            foreach (var e in expected)
            {
                var c = e.Chain;
                var r = Exec(nft, "insert", "rule", c.Family, c.Table, c.Name, e.Proto, field, ps, "accept", "comment", comment);
                if (r.ExitCode != 0)
                {
                    LogFailure(msg, p, "Failed to " + (update ? "update" : "add") + " firewall rule " + qn + " in chain " + c.ToString().ToQuoted(), r);
                    return false;
                }
            }
            msg?.AddMessage(p + (update ? "Updated" : "Added") + " firewall rule " + name + " (runtime only)");
            return true;
        }

        bool NftRemove(String nft, String ruleName, IMessageHost msg, String p, String qn)
        {
            if (!NftGetRuleset(nft, msg, p, "delete firewall rule " + qn, out var _, out var rules))
                return false;
            var mine = rules.Where(x => String.Equals(x.Comment, ruleName, StringComparison.Ordinal)).ToList();
            if (mine.Count <= 0)
            {
                msg?.AddMessage(p + "Firewall rule " + qn + " doesn't exist", MessageLevels.Debug);
                return true;
            }
            if (!NftDelete(nft, mine, msg, p, qn))
                return false;
            msg?.AddMessage(p + "Deleted firewall rule " + qn);
            return true;
        }

        #endregion//nftables

    }
}
