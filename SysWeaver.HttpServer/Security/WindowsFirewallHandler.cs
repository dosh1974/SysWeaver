using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Security
{
    /// <summary>
    /// A firewall handler for Windows, using "netsh advfirewall" (requires elevated rights).
    /// </summary>
    /// <remarks>
    /// Existing rules are validated by parsing the (English) output of "netsh advfirewall firewall show rule", on localized systems the rule is always updated.
    /// </remarks>
    public sealed class WindowsFirewallHandler : IFirewallHandler
    {
        /// <summary>I still need the Legacy e
        /// The singleton instance.
        /// </summary>
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


        /// <summary>
        /// Make sure that an allow rule with the given name exists for the port (all profiles, any local and remote address).
        /// An existing rule with the same name is validated and updated (or removed and re-added) if needed.
        /// </summary>
        /// <param name="ruleName">The name of the rule</param>
        /// <param name="port">The local port</param>
        /// <param name="msg">Optional message handler</param>
        /// <param name="messagePrefix">The prefix to use for messages, null for the default</param>
        /// <param name="protocol">The protocol(s)</param>
        /// <param name="direction">The direction</param>
        /// <returns>True if a valid rule exists or was added / updated</returns>
        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            var qn = ruleName.ToQuoted();
            var name = qn + " for " + StringTools.RemoveCamelCase(direction.ToString()).FastToLower() + " " + StringTools.RemoveCamelCase(protocol.ToString()).FastToLower() + " traffic on port " + port;
            msg?.AddMessage(MessageLevels.Debug, $"{p}Checking if firewall rule {name} exists");
            var args = "advfirewall firewall show rule name=" + qn;
            List<String> log = new List<string>();
            var r = ExternalProcess.Run("netsh", args, (text, wrn) => log.Add(text));

            var dirs = Dirs[direction];
            var prots = Prots[protocol];
            var ports = port.ToString();
            if (r != 1)
            {
                msg?.AddMessage(MessageLevels.Debug, $"{p}Validating existing firewall rule {name}");
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
                    msg?.AddMessage(MessageLevels.Info, $"{p}{qn} exists and is valid");
                    return true;
                }
                msg?.AddMessage(MessageLevels.Debug, $"{p}Updating existing firewall rule {name}");
                //  Must update
                args = "advfirewall firewall set rule name=" + qn + " new dir=" + dirs + " protocol=" + prots + " localport=" + ports + " profile=any localip=any remoteip=any remoteport=any action=allow enable=yes";
                log.Clear();
                r = ExternalProcess.Run("netsh", args, (text, wrn) =>
                {
                    if (wrn)
                        msg?.AddMessage(MessageLevels.Warning, $"{p}{text}");
                    log.Add(text);
                });
                if (r == 0)
                {
                    msg?.AddMessage(MessageLevels.Info, $"{p}Updated firewall rule {name}");
                    return true;
                }
                //  Update failed
                if (msg != null)
                {
                    msg.AddMessage(MessageLevels.Warning, $"{p}Failed to update firewall rule {qn}, error: {r}");
                    using (msg.Tab())
                    {
                        foreach (var x in log)
                            msg.AddMessage(MessageLevels.Info, $"{p}{x}");
                    }
                }
                // Try to remove rule
                Remove(ruleName, msg, messagePrefix);
            }
            // Add rule
            msg?.AddMessage(MessageLevels.Debug, $"{p}Adding new firewall rule {name}");
            args = "advfirewall firewall add rule name=" + qn + " dir=" + dirs + " protocol=" + prots + " localport=" + ports + " profile=any localip=any remoteip=any remoteport=any action=allow enable=yes";
            log.Clear();
            r = ExternalProcess.Run("netsh", args, (text, wrn) =>
            {
                if (wrn)
                    msg?.AddMessage(MessageLevels.Warning, $"{p}{text}");
                log.Add(text);
            });
            if (r == 0)
            {
                msg?.AddMessage(MessageLevels.Info, $"{p}Added firewall rule {name}");
                return true;
            }
            if (msg != null)
            {
                msg.AddMessage(MessageLevels.Warning, $"{p}Failed to add firewall rule {qn}, error: {r}");
                using (msg.Tab())
                {
                    foreach (var x in log)
                        msg.AddMessage(MessageLevels.Info, $"{p}{x}");
                }
            }
            return false;
        }

        /// <summary>
        /// Delete all rules with the given name.
        /// </summary>
        /// <param name="ruleName">The name of the rule</param>
        /// <param name="msg">Optional message handler</param>
        /// <param name="messagePrefix">The prefix to use for messages, null for the default</param>
        /// <returns>True if the rule was deleted or if no rule with that name exists, false if the delete failed (ex: not elevated)</returns>
        /// <remarks>
        /// If "netsh advfirewall firewall delete rule" fails, "netsh advfirewall firewall show rule" checks if a rule with that name exists
        /// (its exit code is 1 when no rule matches, this doesn't depend on the language of the output and doesn't need elevation).
        /// </remarks>
        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            var qn = ruleName.ToQuoted();
            msg?.AddMessage(MessageLevels.Debug, $"{p}Deleting rule {qn}");
            var args = "advfirewall firewall delete rule name=" + qn;
            List<String> log = new List<string>();
            var r = ExternalProcess.Run("netsh", args, (text, wrn) => log.Add(text));
            if (r == 0)
            {
                msg?.AddMessage(MessageLevels.Info, $"{p}Deleted firewall rule {qn}");
                return true;
            }
            //  Failed, check if a rule with that name exists at all
            if (ExternalProcess.Run("netsh", "advfirewall firewall show rule name=" + qn) == 1)
            {
                msg?.AddMessage(MessageLevels.Debug, $"{p}Firewall rule {qn} doesn't exist");
                return true;
            }
            if (msg != null)
            {
                msg.AddMessage(MessageLevels.Warning, $"{p}Failed to delete firewall rule {qn}, error: {r}");
                using (msg.Tab())
                {
                    foreach (var x in log)
                        msg.AddMessage(MessageLevels.Info, $"{p}{x}");
                }
            }
            return false;
        }
    }


    /// <summary>
    /// A firewall handler for platforms without firewall support (ex: macOS), every operation logs a warning and fails.
    /// </summary>
    public sealed class NoFirewallHandler : IFirewallHandler
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly IFirewallHandler Instance = new NoFirewallHandler();

        /// <inheritdoc/>
        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            msg?.AddMessage(MessageLevels.Warning, $"{p}Can't add firewall rule {ruleName.ToQuoted()}, no firewall handler found!");
            return false;
        }

        /// <inheritdoc/>
        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null)
        {
            var p = messagePrefix ?? FirewallHandler.Prefix;
            msg?.AddMessage(MessageLevels.Warning, $"{p}Can't remove firewall rule {ruleName.ToQuoted()}, no firewall handler found!");
            return false;
        }
    }

    /// <summary>
    /// A firewall handler that forwards to the handler for the current platform (<see cref="WindowsFirewallHandler"/> on Windows,
    /// <see cref="LinuxFirewallHandler"/> on Linux, else <see cref="NoFirewallHandler"/>, ex: on macOS).
    /// </summary>
    public sealed class FirewallHandler : IFirewallHandler
    {
        /// <summary>
        /// The default message prefix.
        /// </summary>
        public const String Prefix = "[Firewall] ";

        /// <inheritdoc/>
        public override string ToString()
        {
            var rs = Handlers;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " supported platform: " : " supported platforms: ", String.Join(", ", rs.Select(x => x.Key.ToQuoted())));
        }

        static readonly IReadOnlyDictionary<String, IFirewallHandler> Handlers = new Dictionary<String, IFirewallHandler>()
        {
            { "Windows",  WindowsFirewallHandler.Instance },
            { "Linux",  LinuxFirewallHandler.Instance },
        }.Freeze();

        static IFirewallHandler Get()
        {
            //  Note: PlatformID.Unix is also used on macOS, so OperatingSystem is used
            if (OperatingSystem.IsWindows())
                return WindowsFirewallHandler.Instance;
            if (OperatingSystem.IsLinux())
                return LinuxFirewallHandler.Instance;
            return NoFirewallHandler.Instance;
        }

        /// <summary>
        /// The handler for the current platform.
        /// </summary>
        public static readonly IFirewallHandler Instance = Get();


        /// <inheritdoc/>
        public bool AddOrSet(string ruleName, int port, IMessageHost msg = null, string messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound)
            => Instance.AddOrSet(ruleName, port, msg, messagePrefix, protocol, direction);

        /// <inheritdoc/>
        public bool Remove(string ruleName, IMessageHost msg = null, string messagePrefix = null) 
            => Instance.Remove(ruleName, msg, messagePrefix);

    }

}
