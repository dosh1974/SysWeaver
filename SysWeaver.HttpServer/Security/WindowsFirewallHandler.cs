using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Security
{
    public sealed class WindowsFirewallHandler : IFirewallHandler
    {
        public static readonly IFirewallHandler Instance = new WindowsFirewallHandler();
        static readonly IReadOnlyDictionary<FirewallDirections, String> Dirs = new Dictionary<FirewallDirections, string>
        {
            {  FirewallDirections.Inbound, "in" },
            {  FirewallDirections.Outbound, "out" },
        }.Freeze();

        static readonly IReadOnlyDictionary<FirewallProtcols, String> Prots = new Dictionary<FirewallProtcols, string>
        {
            {  FirewallProtcols.Tcp, "tcp" },
            {  FirewallProtcols.Udp, "udp" },
            {  FirewallProtcols.TcpAndUdp, "any" },
        }.Freeze();


        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            var qn = ruleName.ToQuoted();
            var name = qn + " for " + StringTools.RemoveCamelCase(direction.ToString()).FastToLower() + " " + StringTools.RemoveCamelCase(protocol.ToString()).FastToLower() + " traffic on port " + port;
            msg?.AddMessage(p + "Checking if firewall rule " + name + " exists", MessageLevels.Debug);
            var args = "advfirewall firewall show rule name=" + qn;
            List<String> log = new List<string>();
            var r = ExternalProcess.Run("netsh", args, (text, wrn) => log.Add(text));

            var dirs = Dirs[direction];
            var prots = Prots[protocol];
            var ports = port.ToString();
            if (r != 1)
            {
                msg?.AddMessage(p + "Validating existing firewall rule " + name, MessageLevels.Debug);
                //  Check existing
                Dictionary<String, String> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in log)
                {
                    foreach (var rx in t.Split('\n'))
                    {
                        var row = rx.Trim();
                        var ri = row.IndexOf(':');
                        if (ri < 0)
                            continue;
                        var key = row.Substring(0, ri).TrimEnd();
                        var value = row.Substring(ri + 1).TrimStart();
                        values[key] = value;
                    }
                }
                /*
    Rule Name:                            SysWeaver Test 10443
    ----------------------------------------------------------------------
    Enabled:                              Yes
    Direction:                            In
    Profiles:                             Domain,Private,Public
    Grouping:                             
    LocalIP:                              Any
    RemoteIP:                             Any
    Protocol:                             TCP
    LocalPort:                            10443
    RemotePort:                           Any
    Edge traversal:                       No
    Action:                               Allow
    Ok.                */
                bool isOk(String key, String value) => values.TryGetValue(key, out var val) ? String.Equals(value, val, StringComparison.OrdinalIgnoreCase) : false;
                bool ok = true;
                ok &= isOk("Enabled", "Yes");
                ok &= isOk("Direction", dirs);
                ok &= isOk("LocalIP", "Any");
                ok &= isOk("RemoteIP", "Any");
                ok &= isOk("Protocol", prots);
                ok &= isOk("LocalPort", ports);
                ok &= isOk("RemotePort", "Any");
                ok &= isOk("Action", "Allow");
                if (ok)
                {
                    msg.AddMessage(p + qn + " exists and is valid");
                    return true;
                }
                msg?.AddMessage(p + "Updating existing firewall rule " + name, MessageLevels.Debug);
                //  Must update
                args = "advfirewall firewall set rule name=" + qn + " new dir=" + dirs + " protocol=" + prots + " localport=" + ports + " profile=any localip=any remoteip=any remoteport=any action=allow enable=yes";
                log.Clear();
                r = ExternalProcess.Run("netsh", args, (text, wrn) =>
                {
                    if (wrn)
                        msg?.AddMessage(p + text, MessageLevels.Warning);
                    log.Add(text);
                });
                if (r == 0)
                {
                    msg?.AddMessage(p + "Updated firewall rule " + name);
                    return true;
                }
                //  Update failed
                if (msg != null)
                {
                    msg.AddMessage(p + "Failed to update firewall rule " + qn + ", error: " + r, MessageLevels.Warning);
                    using (msg.Tab())
                    {
                        foreach (var x in log)
                            msg.AddMessage(p + x, MessageLevels.Info);
                    }
                }
                // Try to remove rule
                Remove(ruleName, msg, messagePrefix);
            }
            // Add rule
            msg?.AddMessage(p + "Adding new firewall rule " + name, MessageLevels.Debug);
            args = "advfirewall firewall add rule name=" + qn + " dir=" + dirs + " protocol=" + prots + " localport=" + ports + " profile=any localip=any remoteip=any remoteport=any action=allow enable=yes";
            log.Clear();
            r = ExternalProcess.Run("netsh", args, (text, wrn) =>
            {
                if (wrn)
                    msg?.AddMessage(p + text, MessageLevels.Warning);
                log.Add(text);
            });
            if (r == 0)
            {
                msg?.AddMessage(p + "Added firewall rule " + name);
                return true;
            }
            if (msg != null)
            {
                msg.AddMessage(p + "Failed to add firewall rule " + qn + ", error: " + r, MessageLevels.Warning);
                using (msg.Tab())
                {
                    foreach (var x in log)
                        msg.AddMessage(p + x, MessageLevels.Info);
                }
            }
            return false;
        }

        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            var qn = ruleName.ToQuoted();
            msg?.AddMessage(p + "Deleting rule " + qn, MessageLevels.Debug);
            var args = "advfirewall firewall delete rule name=" + qn;
            List<String> log = new List<string>();
            var r = ExternalProcess.Run("netsh", args, (text, wrn) => log.Add(text));
            if (r == 0)
            {
                msg?.AddMessage(p + "Deleted firewall rule " + qn);
                return true;
            }
            if (msg != null)
            {
                msg.AddMessage(p + "Failed to delete firewall rule " + qn + ", error: " + r, MessageLevels.Warning);
                using (msg.Tab())
                {
                    foreach (var x in log)
                        msg.AddMessage(p + x, MessageLevels.Info);
                }
            }
            return false;
        }
    }


    public sealed class NoFirewallHandler : IFirewallHandler
    {
        public static readonly IFirewallHandler Instance = new NoFirewallHandler();

        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            msg?.AddMessage(p + "Can't add firewall rule " + ruleName.ToQuoted() + ", no firewall handler found!", MessageLevels.Warning);
            return false;
        }

        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            msg?.AddMessage(p + "Can't remove firewall rule " + ruleName.ToQuoted() + ", no firewall handler found!", MessageLevels.Warning);
            return false;
        }
    }

    public sealed class FirewallHandler : IFirewallHandler
    {
        public const String Prefix = "[Firewall] ";

        public override string ToString()
        {
            var rs = Handlers;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " supported platform: " : " supported platforms: ", String.Join(", ", rs.Select(x => x.Key.ToString().ToQuoted())));
        }

        static readonly IReadOnlyDictionary<PlatformID, IFirewallHandler> Handlers = new Dictionary<PlatformID, IFirewallHandler>()
        {
            { PlatformID.Win32NT,  WindowsFirewallHandler.Instance },
        }.Freeze();

        static IFirewallHandler Get()
        {
            var os = Environment.OSVersion.Platform;
            Handlers.TryGetValue(os, out var h);
            return h ?? NoFirewallHandler.Instance;
        }

        public static readonly IFirewallHandler Instance = Get();


        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
            => Instance.AddOrSet(ruleName, port, msg, messagePrefix, protocol, direction);

        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null) 
            => Instance.Remove(ruleName, msg, messagePrefix);

    }

}
