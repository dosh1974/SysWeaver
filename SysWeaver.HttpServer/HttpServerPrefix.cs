using System;
using System.Collections.Generic;

namespace SysWeaver.Net
{
    public sealed class HttpServerPrefix
    {
        public override string ToString() => Prefix;


        /// <summary>
        /// https with certificate and firewall to make it accessible outside the executing computer, requires elevated execution
        /// </summary>
        public static HttpServerPrefix DefaultExternalHttps => new HttpServerPrefix
        {
            Prefix = "https://*:443",
            AddToFirewall = true,
            Certificate = "*",
        };

        /// <summary>
        /// https with certificate accessible only on by this computer
        /// </summary>
        public static HttpServerPrefix DefaultLocalHttps => new HttpServerPrefix
        {
            Prefix = "https://locahost:443",
            Certificate = "*",
        };

        /// <summary>
        /// http with firewall to make it accessible outside the executing computer, requires elevated execution, not recommended, should use https!
        /// </summary>
        public static HttpServerPrefix DefaultExternalHttp => new HttpServerPrefix
        {
            Prefix = "http://*:80",
            AddToFirewall = true,
        };

        /// <summary>
        /// http accessible only on by this computer
        /// </summary>
        public static HttpServerPrefix DefaultLocalHttp => new HttpServerPrefix
        {
            Prefix = "http://locahost:80",
        };

        public const String DefaultFirewallName = "SysWeaver $(AppName) $(Port)";


        /// <summary>
        /// The prefix to listen on, syntax: "protocol://hostname:port/route".
        /// Where:
        /// "protcol" = "http" or "https" (defaults to "http").
        /// "hostname" = Examples: "192.168.1.10", "localhost", "www.mydomain.com".
        /// "port" = Default's to "80" if protocol is "http" and "443" is protocol is "https".
        /// "route" = Optional route, not available for kestral.
        /// </summary>
        public String Prefix;

        /// <summary>
        /// Optionally bind a certificate to the prefix.
        /// Only works for https prefix'es.
        /// The value is the name of a registered ICertificate provider, or "*" to take any (first) provider.
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
