using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IFirewall"/> for Windows, uses the Windows Defender Firewall COM API (HNetCfg.FwPolicy2, late bound).
    /// Rule names are not unique on Windows, so the id of a rule is a hash of all its properties (except enabled),
    /// identical rules gets a "-2", "-3".. suffix.
    /// </summary>
    sealed class Win32Firewall : IFirewall
    {
        public String Name => "Windows Defender Firewall";

        public bool CanDisableRules => true;

        static readonly Type PolicyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true);
        static readonly Type RuleType = Type.GetTypeFromProgID("HNetCfg.FWRule", true);

        static dynamic Policy() => Activator.CreateInstance(PolicyType);

        static Object GetIndexed(Object o, String name, Object index)
            => o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, [index]);

        const int ProfileDomain = 1;
        const int ProfilePrivate = 2;
        const int ProfilePublic = 4;
        const int ProfileAll = 0x7FFFFFFF;

        public IReadOnlyList<FirewallProfile> GetProfiles()
        {
            Object p = Policy();
            int current = (int)((dynamic)p).CurrentProfileTypes;
            var res = new List<FirewallProfile>();
            foreach (var (bit, name) in new[] { (ProfileDomain, "Domain"), (ProfilePrivate, "Private"), (ProfilePublic, "Public") })
            {
                res.Add(new FirewallProfile
                {
                    Name = name,
                    Active = (current & bit) != 0,
                    Enabled = (bool)GetIndexed(p, "FirewallEnabled", bit),
                    DefaultInbound = ((int)GetIndexed(p, "DefaultInboundAction", bit)) == 1 ? FirewallActions.Allow : FirewallActions.Block,
                    DefaultOutbound = ((int)GetIndexed(p, "DefaultOutboundAction", bit)) == 1 ? FirewallActions.Allow : FirewallActions.Block,
                });
            }
            return res;
        }

        public IReadOnlyList<FirewallRule> GetRules() => Enumerate().Select(x => x.Rule).ToList();

        public FirewallRule GetRule(String id)
        {
            if (String.IsNullOrEmpty(id))
                return null;
            foreach (var (rule, _) in Enumerate())
                if (rule.Id == id)
                    return rule;
            return null;
        }

        public String AddRule(FirewallRule rule)
        {
            Validate(rule);
            dynamic r = Activator.CreateInstance(RuleType);
            Apply(r, rule, null);
            dynamic p = Policy();
            try
            {
                p.Rules.Add(r);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to add the rule: " + Message(ex));
            }
            return IdAfterChange(r);
        }

        public String UpdateRule(String id, FirewallRule rule)
        {
            Validate(rule);
            var (cur, com) = Find(id);
            //  The program and service can't be cleared on an existing rule, replace the rule instead (keeping the raw indirect strings if not changed)
            var cleared = ((cur.Program.Length > 0) && String.IsNullOrWhiteSpace(rule.Program)) || ((cur.Service.Length > 0) && String.IsNullOrWhiteSpace(rule.Service));
            if (cleared)
            {
                var x = rule.Clone();
                if (x.Name.Trim() == cur.DisplayName)
                    x.Name = cur.Name;
                dynamic r = Activator.CreateInstance(RuleType);
                try
                {
                    Apply(r, x, null);
                    if (x.Description == cur.Description)
                        r.Description = (String)com.Description;
                    if (x.Group == cur.Group)
                        r.Grouping = (String)com.Grouping;
                    dynamic p = Policy();
                    p.Rules.Add(r);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to update the rule: " + Message(ex));
                }
                Remove(com);
                return IdAfterChange(r);
            }
            try
            {
                Apply(com, rule, cur);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to update the rule: " + Message(ex));
            }
            return IdAfterChange(com);
        }

        public void RemoveRule(String id)
        {
            var (_, com) = Find(id);
            Remove(com);
        }

        static void Remove(dynamic com)
        {
            //  Rule names are not unique and Remove takes a name, so rename it to something unique first
            var temp = "SysWeaver.Remove." + Guid.NewGuid().ToString("N");
            try
            {
                com.Name = temp;
                dynamic p = Policy();
                p.Rules.Remove(temp);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to remove the rule: " + Message(ex));
            }
        }

        public String SetRuleEnabled(String id, bool enabled)
        {
            var (_, com) = Find(id);
            try
            {
                com.Enabled = enabled;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to " + (enabled ? "enable" : "disable") + " the rule: " + Message(ex));
            }
            return IdAfterChange(com);
        }

        #region Helpers

        static String Message(Exception ex)
        {
            while ((ex is TargetInvocationException) && (ex.InnerException != null))
                ex = ex.InnerException;
            var m = ex.Message;
            if (ex is UnauthorizedAccessException)
                m += " (the server must run elevated to change firewall rules)";
            return m;
        }

        /// <summary>
        /// The id of a rule after it was changed (the id depends on the values, the occurrence suffix requires a new enumeration)
        /// </summary>
        String IdAfterChange(dynamic com)
        {
            FirewallRule now = ToRule(com, null);
            var key = Hash(now);
            var match = Enumerate().Select(x => x.Rule.Id).FirstOrDefault(x => (x == key) || x.StartsWith(key + "-", StringComparison.Ordinal));
            return match ?? key;
        }

        (FirewallRule Rule, dynamic Com) Find(String id)
        {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentException("No rule id!");
            foreach (var x in Enumerate())
                if (x.Rule.Id == id)
                    return x;
            throw new Exception("The rule doesn't exist (it may have been changed or removed)!");
        }

        List<(FirewallRule Rule, dynamic Com)> Enumerate()
        {
            dynamic p = Policy();
            var res = new List<(FirewallRule, dynamic)>();
            var seen = new Dictionary<String, int>(StringComparer.Ordinal);
            foreach (dynamic r in (IEnumerable)p.Rules)
            {
                FirewallRule rule = ToRule(r, null);
                var id = rule.Id;
                if (seen.TryGetValue(id, out var count))
                {
                    seen[id] = ++count;
                    rule.Id = String.Concat(id, "-", count);
                }
                else
                {
                    seen[id] = 1;
                }
                res.Add((rule, r));
            }
            return res;
        }

        static String S(Object o) => o as String ?? (o?.ToString() ?? "");

        static T Try<T>(Func<T> f, T def = default)
        {
            try
            {
                return f();
            }
            catch
            {
                return def;
            }
        }

        static readonly ConcurrentDictionary<String, String> Resolved = new(StringComparer.Ordinal);

        static String Resolve(String s)
        {
            if (String.IsNullOrEmpty(s) || (s[0] != '@'))
                return s ?? "";
            return Resolved.GetOrAdd(s, Win32SystemServices.ResolveIndirect);
        }

        static String ProtocolName(int p) => p switch
        {
            256 => "Any",
            6 => "TCP",
            17 => "UDP",
            1 => "ICMPv4",
            58 => "ICMPv6",
            _ => p.ToString(),
        };

        static int ProtocolNumber(String s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0 || s == "*" || s.Equals("Any", StringComparison.OrdinalIgnoreCase))
                return 256;
            if (s.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                return 6;
            if (s.Equals("UDP", StringComparison.OrdinalIgnoreCase))
                return 17;
            if (s.Equals("ICMPv4", StringComparison.OrdinalIgnoreCase) || s.Equals("ICMP", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (s.Equals("ICMPv6", StringComparison.OrdinalIgnoreCase))
                return 58;
            if (int.TryParse(s, out var v) && (v >= 0) && (v <= 256))
                return v;
            throw new ArgumentException("Invalid protocol \"" + s + "\", use Any, TCP, UDP, ICMPv4, ICMPv6 or a number (0-255)!");
        }

        static String ProfilesText(int p)
        {
            if ((p & 7) == 7)
                return "All";
            var l = new List<String>();
            if ((p & ProfileDomain) != 0) l.Add("Domain");
            if ((p & ProfilePrivate) != 0) l.Add("Private");
            if ((p & ProfilePublic) != 0) l.Add("Public");
            return String.Join(",", l);
        }

        static int ProfilesMask(String s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0 || s == "*" || s.Equals("All", StringComparison.OrdinalIgnoreCase) || s.Equals("Any", StringComparison.OrdinalIgnoreCase))
                return ProfileAll;
            int m = 0;
            foreach (var x in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                m |= x.ToLowerInvariant() switch
                {
                    "domain" => ProfileDomain,
                    "private" => ProfilePrivate,
                    "public" => ProfilePublic,
                    "all" => ProfileAll,
                    _ => throw new ArgumentException("Invalid profile \"" + x + "\", use Domain, Private, Public or All!"),
                };
            }
            return m == 0 ? ProfileAll : m;
        }

        static bool IsAny(String s) => String.IsNullOrWhiteSpace(s) || (s.Trim() == "*");

        static void Validate(FirewallRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (String.IsNullOrWhiteSpace(rule.Name))
                throw new ArgumentException("A rule must have a name!");
            if (rule.Direction == FirewallDirections.Forward)
                throw new ArgumentException("Windows firewall rules can't be for forwarded traffic!");
            if (rule.Action is FirewallActions.Reject or FirewallActions.Limit)
                throw new ArgumentException("Windows firewall rules can only allow or block!");
            var p = ProtocolNumber(rule.Protocol);
            if ((p != 6) && (p != 17) && ((!IsAny(rule.LocalPorts)) || (!IsAny(rule.RemotePorts))))
                throw new ArgumentException("Ports can only be specified for TCP and UDP!");
            ProfilesMask(rule.Profiles);
        }

        static FirewallRule ToRule(dynamic r, FirewallRule into)
        {
            var x = into ?? new FirewallRule();
            x.Name = S(r.Name);
            x.DisplayName = Resolve(x.Name);
            x.Description = Resolve(S(r.Description));
            var group = S(r.Grouping);
            x.Group = Resolve(group);
            x.Enabled = (bool)r.Enabled;
            x.Direction = ((int)r.Direction) == 2 ? FirewallDirections.Out : FirewallDirections.In;
            x.Action = ((int)r.Action) == 1 ? FirewallActions.Allow : FirewallActions.Block;
            x.Protocol = ProtocolName((int)r.Protocol);
            x.LocalPorts = S(r.LocalPorts);
            x.RemotePorts = S(r.RemotePorts);
            x.LocalAddresses = S(r.LocalAddresses);
            x.RemoteAddresses = S(r.RemoteAddresses);
            x.Program = S(r.ApplicationName);
            x.Service = S(r.ServiceName);
            x.Interface = S(r.InterfaceTypes);
            x.Profiles = ProfilesText((int)r.Profiles);
            x.IcmpTypes = S(r.IcmpTypesAndCodes);
            x.EdgeTraversal = (bool)r.EdgeTraversal;
            var props = new List<KeyValuePair<String, String>>();
            if (x.Name != x.DisplayName)
                props.Add(new("Raw name", x.Name));
            if (group != x.Group)
                props.Add(new("Raw group", group));
            Object rr = r;
            var eto = Try(() => (int)(dynamic)rr.GetType().InvokeMember("EdgeTraversalOptions", BindingFlags.GetProperty, null, rr, null), -1);
            if (eto >= 0)
                props.Add(new("Edge traversal options", eto switch
                {
                    0 => "Block",
                    1 => "Allow",
                    2 => "Defer to app",
                    3 => "Defer to user",
                    _ => eto.ToString(),
                }));
            var pkg = Try(() => S(rr.GetType().InvokeMember("LocalAppPackageId", BindingFlags.GetProperty, null, rr, null)), "");
            if (pkg.Length > 0)
                props.Add(new("App package id", pkg));
            var owner = Try(() => S(rr.GetType().InvokeMember("LocalUserOwner", BindingFlags.GetProperty, null, rr, null)), "");
            if (owner.Length > 0)
                props.Add(new("Local user owner", owner));
            x.Properties = props.ToArray();
            x.Id = Hash(x);
            return x;
        }

        static String Hash(FirewallRule x)
        {
            var s = String.Join('\u0001', x.Name, x.Description, x.Group, x.Direction, x.Action, x.Protocol, x.LocalPorts, x.RemotePorts,
                x.LocalAddresses, x.RemoteAddresses, x.Program, x.Service, x.Interface, x.Profiles, x.IcmpTypes, x.EdgeTraversal);
            var h = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToBase64String(h, 0, 12).Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Set the values of a COM rule, if cur is not null (an update), only changed values are set
        /// </summary>
        static void Apply(dynamic r, FirewallRule x, FirewallRule cur)
        {
            bool changed(String a, String b) => (cur == null) || (!String.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.Ordinal));
            //  Names / descriptions / groups are shown resolved, keep the raw (indirect) string if not changed
            var name = x.Name.Trim();
            if ((cur == null) || ((name != cur.DisplayName) && (name != cur.Name)))
                r.Name = name;
            if (changed(x.Description, cur?.Description))
                r.Description = String.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim();
            if (changed(x.Group, cur?.Group))
                r.Grouping = String.IsNullOrWhiteSpace(x.Group) ? null : x.Group.Trim();
            if ((cur == null) || (x.Direction != cur.Direction))
                r.Direction = x.Direction == FirewallDirections.Out ? 2 : 1;
            if ((cur == null) || (x.Action != cur.Action))
                r.Action = x.Action == FirewallActions.Allow ? 1 : 0;
            //  Setting an empty program / service on a new rule fails (E_INVALIDARG)
            if (changed(x.Program, cur?.Program) && ((cur != null) || (!String.IsNullOrWhiteSpace(x.Program))))
                r.ApplicationName = String.IsNullOrWhiteSpace(x.Program) ? null : x.Program.Trim();
            if (changed(x.Service, cur?.Service) && ((cur != null) || (!String.IsNullOrWhiteSpace(x.Service))))
                r.ServiceName = String.IsNullOrWhiteSpace(x.Service) ? null : x.Service.Trim();
            var proto = ProtocolNumber(x.Protocol);
            var hasPorts = (proto == 6) || (proto == 17);
            if ((cur == null) || (proto != ProtocolNumber(cur.Protocol)))
            {
                //  Ports must be cleared before changing to a protocol without ports
                var curProto = (int)r.Protocol;
                if ((!hasPorts) && ((curProto == 6) || (curProto == 17)))
                {
                    r.LocalPorts = "*";
                    r.RemotePorts = "*";
                }
                r.Protocol = proto;
            }
            if (hasPorts)
            {
                if (changed(x.LocalPorts, cur?.LocalPorts))
                    r.LocalPorts = IsAny(x.LocalPorts) ? "*" : x.LocalPorts.Trim();
                if (changed(x.RemotePorts, cur?.RemotePorts))
                    r.RemotePorts = IsAny(x.RemotePorts) ? "*" : x.RemotePorts.Trim();
            }
            if (((proto == 1) || (proto == 58)) && changed(x.IcmpTypes, cur?.IcmpTypes))
                r.IcmpTypesAndCodes = IsAny(x.IcmpTypes) ? "*" : x.IcmpTypes.Trim();
            if (changed(x.LocalAddresses, cur?.LocalAddresses))
                r.LocalAddresses = IsAny(x.LocalAddresses) ? "*" : x.LocalAddresses.Trim();
            if (changed(x.RemoteAddresses, cur?.RemoteAddresses))
                r.RemoteAddresses = IsAny(x.RemoteAddresses) ? "*" : x.RemoteAddresses.Trim();
            if (changed(x.Interface, cur?.Interface))
                r.InterfaceTypes = IsAny(x.Interface) ? "All" : x.Interface.Trim();
            var profiles = ProfilesMask(x.Profiles);
            if ((cur == null) || (profiles != ProfilesMask(cur.Profiles)))
                r.Profiles = profiles;
            if ((cur == null) || (x.EdgeTraversal != cur.EdgeTraversal))
                r.EdgeTraversal = x.EdgeTraversal;
            if ((cur == null) || (x.Enabled != cur.Enabled))
                r.Enabled = x.Enabled;
        }

        #endregion//Helpers
    }
}
