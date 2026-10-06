using System;
using System.Collections.Generic;

namespace SysWeaver.Net
{
    /// <summary>
    /// A listener prefix (the scheme, host and port a server listens on), with optional certificate binding and firewall rule.
    /// </summary>
    public sealed class HttpServerPrefix
    {
        /// <inheritdoc/>
        public override string ToString() => Prefix;


        /// <summary>
        /// https on port 443 for all host names, with any certificate provider and a firewall rule to make it accessible outside the executing computer, requires elevated execution.
        /// A new instance is returned on every call.
        /// </summary>
        public static HttpServerPrefix DefaultExternalHttps => new HttpServerPrefix
        {
            Prefix = "https://*:443",
            AddToFirewall = true,
            Certificate = "*",
        };

        /// <summary>
        /// https on port 443 with any certificate provider, intended to be accessible only by this computer.
        /// Note: the host name is misspelled as "locahost" so this prefix doesn't match "localhost" requests.
        /// </summary>
        public static HttpServerPrefix DefaultLocalHttps => new HttpServerPrefix
        {
            Prefix = "https://locahost:443",
            Certificate = "*",
        };

        /// <summary>
        /// http on port 80 for all host names, with a firewall to make it accessible outside the executing computer, requires elevated execution, not recommended, should use https!
        /// </summary>
        public static HttpServerPrefix DefaultExternalHttp => new HttpServerPrefix
        {
            Prefix = "http://*:80",
            AddToFirewall = true,
        };

        /// <summary>
        /// http on port 80, intended to be accessible only by this computer.
        /// Note: the host name is misspelled as "locahost" so this prefix doesn't match "localhost" requests.
        /// </summary>
        public static HttpServerPrefix DefaultLocalHttp => new HttpServerPrefix
        {
            Prefix = "http://locahost:80",
        };

        /// <summary>
        /// The default firewall rule name, "$(AppName)" and "$(Port)" are replaced (EnvInfo variables and the port of the prefix).
        /// </summary>
        public const String DefaultFirewallName = "SysWeaver $(AppName) $(Port)";


        /// <summary>
        /// The prefix to listen on, syntax: "protocol://hostname:port/route".
        /// Where:
        /// "protocol" = "http" or "https" (defaults to "http").
        /// "hostname" = Examples: "*" (any host), "192.168.1.10", "localhost", "www.mydomain.com".
        /// "port" = Defaults to "80" if protocol is "http" and "443" if protocol is "https".
        /// "route" = Optional route, not available for Kestrel.
        /// The server normalizes the prefix (see <see cref="FixPrefix"/>) and removes default ports.
        /// </summary>
        public String Prefix;

        /// <summary>
        /// Optionally bind a certificate to the prefix.
        /// Only works for https prefixes.
        /// The value is the name of a registered certificate provider, or "*" to take any (first) provider.
        /// </summary>
        public String Certificate;

        /// <summary>
        /// If true, open inbound TCP traffic on the port found in the prefix.
        /// </summary>
        public bool AddToFirewall;

        /// <summary>
        /// Name of the firewall rule (should be unique per application).
        /// EnvInfo rules can be used.
        /// </summary>
        public String FirewallName = DefaultFirewallName;


        const String DefaultProtocol = "http";

        /// <summary>
        /// Normalize a prefix to "scheme://host:port/path/" (lower cased host, explicit port, trailing slash).
        /// A missing scheme defaults to "http", "*" is kept as the host wildcard.
        /// </summary>
        /// <param name="f">The prefix as configured</param>
        /// <returns>The normalized prefix, or null if <paramref name="f"/> is null or white space</returns>
        /// <exception cref="UriFormatException">Thrown if the prefix isn't a valid uri</exception>
        public static String FixPrefix(String f)
        {
            f = f?.Trim();
            if (String.IsNullOrEmpty(f))
                return null;
            if (!f.Contains("://"))
                f = DefaultProtocol + "://" + f;
            var th = "sys_weaver_temp_hostname";
            var t = f.Replace("*", th);
            var uri = new Uri(t);
            f = String.Concat(uri.Scheme, "://", uri.Host.Replace(th, "*").FastToLower(), ':', uri.Port, uri.LocalPath);
            if (!f.EndsWith('/'))
                f += '/';
            return f;
        }

        /// <summary>
        /// Create a shallow copy.
        /// </summary>
        /// <returns>A new instance with the same values</returns>
        public HttpServerPrefix Clone()
        {
            return new HttpServerPrefix
            {
                Prefix = Prefix,
                AddToFirewall = AddToFirewall,
                Certificate = Certificate,
                FirewallName = FirewallName,
            };
        }

    }
}
