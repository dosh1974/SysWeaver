using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Shared helpers for the Linux firewalls.
    /// </summary>
    static class UnixFirewallHelper
    {
        public static String Hash(String s)
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToBase64String(h, 0, 12).Replace('+', '-').Replace('/', '_');
        }

        public static bool IsAny(String s) => String.IsNullOrWhiteSpace(s) || (s.Trim() == "*") || s.Trim().Equals("any", StringComparison.OrdinalIgnoreCase) || s.Trim().Equals("Anywhere", StringComparison.OrdinalIgnoreCase);

        public static String Proto(String s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s switch
            {
                "" or "*" or "any" or "all" => "",
                "icmpv4" => "icmp",
                "icmpv6" => "ipv6-icmp",
                _ => s,
            };
        }

        public static String ProtoName(String s) => (s ?? "").ToLowerInvariant() switch
        {
            "" or "all" or "any" => "Any",
            "tcp" => "TCP",
            "udp" => "UDP",
            "icmp" => "ICMPv4",
            "ipv6-icmp" or "icmpv6" => "ICMPv6",
            var x => x,
        };

        public static void Validate(FirewallRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            foreach (var v in new[] { rule.Name, rule.LocalPorts, rule.RemotePorts, rule.LocalAddresses, rule.RemoteAddresses, rule.Interface, rule.Protocol })
                if ((v != null) && v.Any(c => c < ' '))
                    throw new ArgumentException("Control characters are not allowed!");
            //  Values are passed as arguments, they must not look like options
            foreach (var v in new[] { rule.Name, rule.Service, rule.LocalPorts, rule.RemotePorts, rule.LocalAddresses, rule.RemoteAddresses, rule.Interface, rule.Protocol })
                if ((v != null) && v.Trim().StartsWith('-'))
                    throw new ArgumentException("Values can't start with a '-'!");
        }
    }


    /// <summary>
    /// <see cref="IFirewall"/> for ufw (Uncomplicated Firewall), the rules are read using "ufw show added" (works even if ufw is inactive).
    /// The id of a rule is a hash of the rule command, ufw rules can't be disabled (only removed).
    /// </summary>
    sealed class UfwFirewall : IFirewall
    {
        public String Name => "ufw";

        public bool CanDisableRules => false;

        readonly String Ufw;

        public UfwFirewall(String ufw)
        {
            Ufw = ufw;
        }

        public IReadOnlyList<FirewallProfile> GetProfiles()
        {
            var text = UnixCommand.Change(Ufw, "status", "verbose");
            var p = new FirewallProfile
            {
                Name = "ufw",
                Active = true,
                Enabled = text.Contains("Status: active", StringComparison.Ordinal),
                DefaultInbound = FirewallActions.Block,
                DefaultOutbound = FirewallActions.Allow,
            };
            foreach (var line in SystemdSystemServices.Lines(text))
            {
                if (!line.StartsWith("Default:", StringComparison.Ordinal))
                    continue;
                foreach (var part in line.Substring(8).Split(','))
                {
                    var t = part.Trim();
                    var a = t.StartsWith("allow") ? FirewallActions.Allow : t.StartsWith("reject") ? FirewallActions.Reject : FirewallActions.Block;
                    if (t.Contains("(incoming)"))
                        p.DefaultInbound = a;
                    else if (t.Contains("(outgoing)"))
                        p.DefaultOutbound = a;
                }
            }
            return [p];
        }

        /// <summary>
        /// The rule commands (without the leading "ufw")
        /// </summary>
        List<String> Commands()
        {
            var res = new List<String>();
            foreach (var line in SystemdSystemServices.Lines(UnixCommand.Change(Ufw, "show", "added")))
            {
                var l = line.Trim();
                if (l.StartsWith("ufw ", StringComparison.Ordinal))
                    res.Add(l.Substring(4).Trim());
            }
            return res;
        }

        public IReadOnlyList<FirewallRule> GetRules() => Commands().Select(Parse).ToList();

        public FirewallRule GetRule(String id) => GetRules().FirstOrDefault(r => r.Id == id);

        static FirewallRule Parse(String cmd)
        {
            var w = UnixCommand.Split(cmd);
            var r = new FirewallRule
            {
                Id = UnixFirewallHelper.Hash(cmd),
                Profiles = "All",
                Protocol = "Any",
                LocalPorts = "",
                RemotePorts = "",
                LocalAddresses = "",
                RemoteAddresses = "",
                Program = "",
                Service = "",
                Interface = "",
                Description = "",
                Group = "",
                IcmpTypes = "",
                Enabled = true,
                Properties = [new("Rule", "ufw " + cmd)],
            };
            int i = 0;
            String next() => i < w.Count ? w[i++] : "";
            String peek() => i < w.Count ? w[i] : "";
            var dir = FirewallDirections.In;
            if (peek() == "route")
            {
                ++i;
                dir = FirewallDirections.Forward;
            }
            r.Action = next() switch
            {
                "deny" => FirewallActions.Block,
                "reject" => FirewallActions.Reject,
                "limit" => FirewallActions.Limit,
                _ => FirewallActions.Allow,
            };
            if (peek() is "in" or "out")
            {
                if (next() == "out" && (dir != FirewallDirections.Forward))
                    dir = FirewallDirections.Out;
            }
            r.Direction = dir;
            if (peek() == "on")
            {
                ++i;
                r.Interface = next();
            }
            if (peek() is "log" or "log-all")
                ++i;
            String from = "", to = "", fromPort = "", toPort = "";
            bool full = false;
            while (i < w.Count)
            {
                var t = next();
                switch (t)
                {
                    case "proto":
                        r.Protocol = UnixFirewallHelper.ProtoName(next());
                        full = true;
                        break;
                    case "from":
                        from = next();
                        full = true;
                        if (peek() == "port")
                        {
                            ++i;
                            fromPort = next();
                        }
                        else if (peek() == "app")
                        {
                            ++i;
                            r.Service = next();
                        }
                        break;
                    case "to":
                        to = next();
                        full = true;
                        if (peek() == "port")
                        {
                            ++i;
                            toPort = next();
                        }
                        else if (peek() == "app")
                        {
                            ++i;
                            r.Service = next();
                        }
                        break;
                    case "comment":
                        r.Name = next();
                        break;
                    case "in":
                    case "out":
                        //  A routed rule with both an in and out interface, can't be edited
                        if (peek() == "on")
                        {
                            ++i;
                            r.Properties = [.. r.Properties, new(t == "in" ? "In interface" : "Out interface", next())];
                        }
                        r.ReadOnly = true;
                        break;
                    default:
                        if (!full)
                        {
                            //  Simple syntax: "22", "22/tcp", "8000:8100/tcp", "ssh" or an app profile name
                            var s = t.Split('/');
                            if (s.Length > 1)
                                r.Protocol = UnixFirewallHelper.ProtoName(s[1]);
                            if (s[0].Length > 0 && (Char.IsDigit(s[0][0])))
                                toPort = s[0].Replace(':', '-');
                            else
                                r.Service = s[0];
                        }
                        break;
                }
            }
            toPort = toPort.Replace(':', '-');
            fromPort = fromPort.Replace(':', '-');
            static String addr(String a) => UnixFirewallHelper.IsAny(a) ? "" : a;
            if (dir == FirewallDirections.Out)
            {
                r.LocalAddresses = addr(from);
                r.LocalPorts = fromPort;
                r.RemoteAddresses = addr(to);
                r.RemotePorts = toPort;
            }
            else
            {
                r.RemoteAddresses = addr(from);
                r.RemotePorts = fromPort;
                r.LocalAddresses = addr(to);
                r.LocalPorts = toPort;
            }
            if (String.IsNullOrEmpty(r.Name))
                r.Name = cmd;
            r.DisplayName = r.Name;
            return r;
        }

        /// <summary>
        /// Build the ufw arguments for a rule (without "ufw")
        /// </summary>
        static List<String> Build(FirewallRule r)
        {
            UnixFirewallHelper.Validate(r);
            var a = new List<String>();
            if (r.Direction == FirewallDirections.Forward)
                a.Add("route");
            a.Add(r.Action switch
            {
                FirewallActions.Block => "deny",
                FirewallActions.Reject => "reject",
                FirewallActions.Limit => "limit",
                _ => "allow",
            });
            a.Add(r.Direction == FirewallDirections.Out ? "out" : "in");
            var name = (r.Name ?? "").Trim();
            var comment = (name.Length > 0) && (!name.StartsWith("allow ") && !name.StartsWith("deny ") && !name.StartsWith("reject ") && !name.StartsWith("limit ") && !name.StartsWith("route "));
            //  Only a service name (from /etc/services) or app profile: use the simple syntax, ex: "allow in ssh"
            if ((!String.IsNullOrWhiteSpace(r.Service)) && UnixFirewallHelper.IsAny(r.LocalPorts) && UnixFirewallHelper.IsAny(r.RemotePorts) && UnixFirewallHelper.IsAny(r.LocalAddresses)
                && UnixFirewallHelper.IsAny(r.RemoteAddresses) && UnixFirewallHelper.IsAny(r.Interface) && (r.Direction != FirewallDirections.Forward))
            {
                var p = UnixFirewallHelper.Proto(r.Protocol);
                a.Add(((p == "tcp") || (p == "udp")) ? (r.Service.Trim() + "/" + p) : r.Service.Trim());
                if (comment)
                {
                    a.Add("comment");
                    a.Add(name);
                }
                return a;
            }
            if (!UnixFirewallHelper.IsAny(r.Interface))
            {
                a.Add("on");
                a.Add(r.Interface.Trim());
            }
            var proto = UnixFirewallHelper.Proto(r.Protocol);
            if (proto == "ipv6-icmp")
                proto = "ipv6";
            String ports(String p) => UnixFirewallHelper.IsAny(p) ? null : p.Trim().Replace('-', ':').Replace(" ", "");
            String addrs(String p) => UnixFirewallHelper.IsAny(p) ? "any" : p.Trim();
            var lp = ports(r.LocalPorts);
            var rp = ports(r.RemotePorts);
            if (((lp != null) || (rp != null)) && (proto != "tcp") && (proto != "udp") && (proto.Length > 0))
                throw new ArgumentException("Ports can only be specified for TCP and UDP!");
            //  Multiple ports requires a protocol
            if ((((lp?.Contains(',') ?? false) || (lp?.Contains(':') ?? false)) || ((rp?.Contains(',') ?? false) || (rp?.Contains(':') ?? false))) && (proto.Length <= 0))
                throw new ArgumentException("A protocol (TCP or UDP) must be specified for port ranges and lists!");
            if (proto.Length > 0)
            {
                a.Add("proto");
                a.Add(proto);
            }
            var outDir = r.Direction == FirewallDirections.Out;
            a.Add("from");
            a.Add(addrs(outDir ? r.LocalAddresses : r.RemoteAddresses));
            var fp = outDir ? lp : rp;
            if (fp != null)
            {
                a.Add("port");
                a.Add(fp);
            }
            a.Add("to");
            a.Add(addrs(outDir ? r.RemoteAddresses : r.LocalAddresses));
            var tp = outDir ? rp : lp;
            if (tp != null)
            {
                a.Add("port");
                a.Add(tp);
            }
            else if (!String.IsNullOrWhiteSpace(r.Service))
            {
                a.Add("app");
                a.Add(r.Service.Trim());
            }
            if (comment)
            {
                a.Add("comment");
                a.Add(name);
            }
            return a;
        }

        String FindCommand(String id)
            => Commands().FirstOrDefault(c => UnixFirewallHelper.Hash(c) == id) ?? throw new Exception("The rule doesn't exist (it may have been changed or removed)!");

        String IdOf(List<String> args)
        {
            //  ufw normalizes the rule, find the new one by comparing the parsed values
            var want = Parse(String.Join(' ', args.Select(Quote)));
            var match = GetRules().LastOrDefault(r => Same(r, want));
            return match?.Id ?? UnixFirewallHelper.Hash(String.Join(' ', args));
        }

        static bool Same(FirewallRule a, FirewallRule b)
            => (a.Action == b.Action) && (a.Direction == b.Direction) && (a.Protocol == b.Protocol) && (a.LocalPorts == b.LocalPorts) && (a.RemotePorts == b.RemotePorts)
            && (a.LocalAddresses == b.LocalAddresses) && (a.RemoteAddresses == b.RemoteAddresses) && (a.Interface == b.Interface);

        static String Quote(String s) => s.Any(Char.IsWhiteSpace) || s.Contains('\'') || s.Contains('"') ? ("'" + s.Replace("'", "") + "'") : s;

        public String AddRule(FirewallRule rule)
        {
            var args = Build(rule);
            UnixCommand.Change(Ufw, args.ToArray());
            return IdOf(args);
        }

        public String UpdateRule(String id, FirewallRule rule)
        {
            var args = Build(rule);
            RemoveRule(id);
            UnixCommand.Change(Ufw, args.ToArray());
            return IdOf(args);
        }

        public void RemoveRule(String id)
        {
            var cmd = FindCommand(id);
            var args = new List<String> { "--force", "delete" };
            args.AddRange(UnixCommand.Split(cmd));
            UnixCommand.Change(Ufw, args.ToArray());
        }

        public String SetRuleEnabled(String id, bool enabled)
        {
            if (enabled)
                return id;
            throw new NotSupportedException("ufw rules can't be disabled, remove it instead!");
        }
    }


    /// <summary>
    /// <see cref="IFirewall"/> for iptables / ip6tables (the filter table), the rules are read using "iptables -S".
    /// The id of a rule is a hash of the rule specification, rules can't be disabled (only removed).
    /// NOTE: Changes are not persisted (use iptables-save / netfilter-persistent to keep them after a reboot).
    /// </summary>
    sealed class IptablesFirewall : IFirewall
    {
        public String Name => "iptables";

        public bool CanDisableRules => false;

        readonly String V4;
        readonly String V6;

        public IptablesFirewall(String v4, String v6)
        {
            V4 = v4;
            V6 = v6;
        }

        IEnumerable<(String Tool, String Family, String Line)> Lines()
        {
            foreach (var (tool, fam) in new[] { (V4, "IPv4"), (V6, "IPv6") })
            {
                if (tool == null)
                    continue;
                String text;
                try
                {
                    text = UnixCommand.Change(tool, "-S");
                }
                catch when (fam == "IPv6")
                {
                    continue;
                }
                foreach (var l in SystemdSystemServices.Lines(text))
                    yield return (tool, fam, l.Trim());
            }
        }

        public IReadOnlyList<FirewallProfile> GetProfiles()
        {
            var p = new FirewallProfile
            {
                Name = "iptables",
                Enabled = true,
                Active = true,
                DefaultInbound = FirewallActions.Allow,
                DefaultOutbound = FirewallActions.Allow,
            };
            foreach (var (_, fam, line) in Lines())
            {
                if (fam != "IPv4")
                    continue;
                var w = UnixCommand.Split(line);
                if ((w.Count < 3) || (w[0] != "-P"))
                    continue;
                var a = w[2] == "ACCEPT" ? FirewallActions.Allow : FirewallActions.Block;
                if (w[1] == "INPUT")
                    p.DefaultInbound = a;
                else if (w[1] == "OUTPUT")
                    p.DefaultOutbound = a;
            }
            return [p];
        }

        public IReadOnlyList<FirewallRule> GetRules()
            => Lines().Where(x => x.Line.StartsWith("-A ", StringComparison.Ordinal)).Select(x => Parse(x.Family, x.Line)).ToList();

        public FirewallRule GetRule(String id) => GetRules().FirstOrDefault(r => r.Id == id);

        static FirewallRule Parse(String family, String line)
        {
            var w = UnixCommand.Split(line);
            var chain = w.Count > 1 ? w[1] : "";
            var r = new FirewallRule
            {
                Id = UnixFirewallHelper.Hash(family + " " + line),
                Protocol = "Any",
                LocalPorts = "",
                RemotePorts = "",
                LocalAddresses = "",
                RemoteAddresses = "",
                Program = "",
                Service = "",
                Interface = "",
                Description = "",
                Group = chain,
                IcmpTypes = "",
                Profiles = family,
                Enabled = true,
                Direction = chain switch
                {
                    "OUTPUT" => FirewallDirections.Out,
                    "FORWARD" => FirewallDirections.Forward,
                    _ => FirewallDirections.In,
                },
            };
            var outDir = r.Direction == FirewallDirections.Out;
            String target = "";
            var unknown = new List<String>();
            for (int i = 2; i < w.Count; ++i)
            {
                var t = w[i];
                String arg() => (i + 1) < w.Count ? w[++i] : "";
                switch (t)
                {
                    case "-p":
                        r.Protocol = UnixFirewallHelper.ProtoName(arg());
                        break;
                    case "-s":
                        if (outDir) r.LocalAddresses = arg(); else r.RemoteAddresses = arg();
                        break;
                    case "-d":
                        if (outDir) r.RemoteAddresses = arg(); else r.LocalAddresses = arg();
                        break;
                    case "-i":
                    case "-o":
                        r.Interface = arg();
                        break;
                    case "--dport":
                    case "--dports":
                        if (outDir) r.RemotePorts = arg().Replace(':', '-'); else r.LocalPorts = arg().Replace(':', '-');
                        break;
                    case "--sport":
                    case "--sports":
                        if (outDir) r.LocalPorts = arg().Replace(':', '-'); else r.RemotePorts = arg().Replace(':', '-');
                        break;
                    case "--comment":
                        r.Name = arg();
                        break;
                    case "--icmp-type":
                    case "--icmpv6-type":
                        r.IcmpTypes = arg();
                        break;
                    case "-j":
                        target = arg();
                        break;
                    case "-m":
                        var m = arg();
                        if ((m != "tcp") && (m != "udp") && (m != "comment") && (m != "multiport") && (m != "icmp") && (m != "icmp6"))
                            unknown.Add("-m " + m);
                        break;
                    default:
                        unknown.Add(t);
                        break;
                }
            }
            r.Action = target switch
            {
                "ACCEPT" => FirewallActions.Allow,
                "REJECT" => FirewallActions.Reject,
                _ => FirewallActions.Block,
            };
            //  Rules that can't be represented (other chains, targets or matches) are read only
            r.ReadOnly = (chain is not ("INPUT" or "OUTPUT" or "FORWARD")) || (target is not ("ACCEPT" or "DROP" or "REJECT")) || (unknown.Count > 0);
            if (String.IsNullOrEmpty(r.Name))
                r.Name = line;
            r.DisplayName = r.Name;
            r.Properties =
            [
                new("Rule", line),
                new("Family", family),
                new("Chain", chain),
                new("Target", target),
            ];
            return r;
        }

        List<String> Build(FirewallRule r, out String tool)
        {
            //  Rules without a comment are named by their specification (not a comment)
            if (r.Name?.StartsWith("-A ", StringComparison.Ordinal) ?? false)
            {
                r = r.Clone();
                r.Name = "";
            }
            UnixFirewallHelper.Validate(r);
            var v6 = (r.Profiles ?? "").Contains("6") || (r.LocalAddresses ?? "").Contains(':') || (r.RemoteAddresses ?? "").Contains(':') || (UnixFirewallHelper.Proto(r.Protocol) == "ipv6-icmp");
            tool = v6 ? (V6 ?? throw new Exception("ip6tables wasn't found!")) : V4;
            if (r.Action == FirewallActions.Limit)
                throw new ArgumentException("iptables rules can't limit (use allow, block or reject)!");
            var a = new List<String>
            {
                r.Direction switch
                {
                    FirewallDirections.Out => "OUTPUT",
                    FirewallDirections.Forward => "FORWARD",
                    _ => "INPUT",
                },
            };
            var outDir = r.Direction == FirewallDirections.Out;
            var proto = UnixFirewallHelper.Proto(r.Protocol);
            if (proto.Length > 0)
            {
                a.Add("-p");
                a.Add(proto);
            }
            void addr(String flag, String v)
            {
                if (UnixFirewallHelper.IsAny(v))
                    return;
                a.Add(flag);
                a.Add(v.Trim().Replace(" ", ""));
            }
            addr("-s", outDir ? r.LocalAddresses : r.RemoteAddresses);
            addr("-d", outDir ? r.RemoteAddresses : r.LocalAddresses);
            if (!UnixFirewallHelper.IsAny(r.Interface))
            {
                a.Add(outDir ? "-o" : "-i");
                a.Add(r.Interface.Trim());
            }
            var dport = outDir ? r.RemotePorts : r.LocalPorts;
            var sport = outDir ? r.LocalPorts : r.RemotePorts;
            if ((!UnixFirewallHelper.IsAny(dport)) || (!UnixFirewallHelper.IsAny(sport)))
            {
                if ((proto != "tcp") && (proto != "udp"))
                    throw new ArgumentException("Ports can only be specified for TCP and UDP!");
                String norm(String p) => p.Trim().Replace(" ", "").Replace('-', ':');
                var multi = ((dport ?? "").Contains(',')) || ((sport ?? "").Contains(','));
                a.Add("-m");
                a.Add(multi ? "multiport" : proto);
                if (!UnixFirewallHelper.IsAny(dport))
                {
                    a.Add(multi ? "--dports" : "--dport");
                    a.Add(norm(dport));
                }
                if (!UnixFirewallHelper.IsAny(sport))
                {
                    a.Add(multi ? "--sports" : "--sport");
                    a.Add(norm(sport));
                }
            }
            var name = (r.Name ?? "").Trim();
            if ((name.Length > 0) && (!name.StartsWith("-A ", StringComparison.Ordinal)))
            {
                a.Add("-m");
                a.Add("comment");
                a.Add("--comment");
                a.Add(name.Length > 256 ? name.Substring(0, 256) : name);
            }
            a.Add("-j");
            a.Add(r.Action switch
            {
                FirewallActions.Allow => "ACCEPT",
                FirewallActions.Reject => "REJECT",
                _ => "DROP",
            });
            return a;
        }

        (String Tool, String Line) Find(String id)
        {
            foreach (var (tool, fam, line) in Lines())
                if (line.StartsWith("-A ", StringComparison.Ordinal) && (UnixFirewallHelper.Hash(fam + " " + line) == id))
                    return (tool, line);
            throw new Exception("The rule doesn't exist (it may have been changed or removed)!");
        }

        String IdOf(String tool, List<String> args)
        {
            var fam = tool == V6 ? "IPv6" : "IPv4";
            var want = Parse(fam, "-A " + String.Join(' ', args.Select(x => x.Any(Char.IsWhiteSpace) ? ("\"" + x + "\"") : x)));
            var match = GetRules().LastOrDefault(r => (r.Profiles == fam) && (r.Direction == want.Direction) && (r.Action == want.Action) && (r.Protocol == want.Protocol)
                && (r.LocalPorts == want.LocalPorts) && (r.RemotePorts == want.RemotePorts) && (r.LocalAddresses.StartsWith(want.LocalAddresses)) && (r.RemoteAddresses.StartsWith(want.RemoteAddresses)));
            return match?.Id ?? want.Id;
        }

        public String AddRule(FirewallRule rule)
        {
            var args = Build(rule, out var tool);
            var a = new List<String> { "-A" };
            a.AddRange(args);
            UnixCommand.Change(tool, a.ToArray());
            return IdOf(tool, args);
        }

        public String UpdateRule(String id, FirewallRule rule)
        {
            var (oldTool, line) = Find(id);
            var args = Build(rule, out var tool);
            //  Replace in place (keeps the order) if it's the same chain and tool
            var w = UnixCommand.Split(line);
            if ((tool == oldTool) && (w.Count > 1) && (w[1] == args[0]))
            {
                var index = 0;
                foreach (var (t, _, l) in Lines())
                {
                    if ((t != tool) || (!l.StartsWith("-A " + w[1] + " ", StringComparison.Ordinal)))
                        continue;
                    ++index;
                    if (l == line)
                        break;
                }
                var a = new List<String> { "-R", args[0], index.ToString() };
                a.AddRange(args.Skip(1));
                UnixCommand.Change(tool, a.ToArray());
                return IdOf(tool, args);
            }
            RemoveRule(id);
            return AddRule(rule);
        }

        public void RemoveRule(String id)
        {
            var (tool, line) = Find(id);
            var w = UnixCommand.Split(line);
            w[0] = "-D";
            UnixCommand.Change(tool, w.ToArray());
        }

        public String SetRuleEnabled(String id, bool enabled)
        {
            if (enabled)
                return id;
            throw new NotSupportedException("iptables rules can't be disabled, remove it instead!");
        }
    }
}
