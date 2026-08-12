using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SysWeaver.Security;

namespace SysWeaver.Net
{
    public static class CertificateBinder
    {
        /// <summary>
        /// Bind a certificate to a https port.
        /// This is platform/OS specific, currently these platforms are supported:
        /// * WinNT
        /// </summary>
        /// <param name="msg">Message handler</param>
        /// <param name="listenerPrefix">The listener prefix, ex: "https://*:443"</param>
        /// <param name="cert">The certificate to bind</param>
        /// <param name="logPrefix">The prefix to use for logging</param>
        /// <returns></returns>
        public static async Task<bool> BindHttps(String listenerPrefix, X509Certificate2 cert, IMessageHost msg = null, String logPrefix = "")
        {
            try
            {
                var hash = cert.GetCertHashString().FastToLower();
                var uri = new Uri(listenerPrefix.Replace("*", "localhost"));
                if (uri.Scheme.FastToLower() != "https")
                {
                    msg?.AddMessage(logPrefix + "Can't bind certificate to non-https prefix " + listenerPrefix.ToQuoted(), MessageLevels.Warning);
                    return true;
                }
                String bind;
                if (uri.Host.FastEquals("localhost"))
                {
                    bind = "ipport=0.0.0.0:" + uri.Port;
                }else
                {
                    bind = "hostnameport=" + uri.Host + ":" + uri.Port;
                }
                const string store = " certstorename=MY";
                var pid = Environment.OSVersion.Platform;
                switch (pid)
                {
                    case PlatformID.Win32NT:

                        bool isNew = false;
                        try
                        {
                            isNew = cert.Install();
                        }
                        catch (Exception ex)
                        {
                            msg?.AddMessage(logPrefix + "Failed to install certificate " + hash.ToQuoted(), ex, MessageLevels.Warning);
                        }
                        if (isNew)
                        {
                            string removeArgs = "http delete sslcert " + bind;
                            msg?.AddMessage(logPrefix + "Running command: \"netsh " + removeArgs + "\"", MessageLevels.Debug);
                            ExternalProcess.Run("netsh", removeArgs, (text, wrn) =>
                            {
                                msg?.AddMessage(logPrefix + "[RemoveCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                            });
                        }
                        string updateArgs = "http update sslcert " + bind + store + " certhash=" + hash + " appid=" + EnvInfo.AppGuid;
                        msg?.AddMessage(logPrefix + "Running command: \"netsh " + updateArgs + "\"", MessageLevels.Debug);
                        var r = ExternalProcess.Run("netsh", updateArgs, (text, wrn) =>
                        {
                            msg?.AddMessage(logPrefix + "[UpdateCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                        });
                        if (r != 0)
                        {
                            string addArgs = "http add sslcert " + bind + store + " certhash=" + hash + " appid=" + EnvInfo.AppGuid;
                            msg?.AddMessage(logPrefix + "Running command: \"netsh " + addArgs + "\"", MessageLevels.Debug);
                            r = ExternalProcess.Run("netsh", addArgs, (text, wrn) =>
                            {
                                msg?.AddMessage(logPrefix + "[AddCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                            });
                            if (r != 0)
                            {
                                string removeArgs = "http delete sslcert " + bind;
                                msg?.AddMessage(logPrefix + "Running command: \"netsh " + removeArgs + "\"", MessageLevels.Debug);
                                r = ExternalProcess.Run("netsh", removeArgs, (text, wrn) =>
                                {
                                    msg?.AddMessage(logPrefix + "[RemoveCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                                });
                                msg?.AddMessage(logPrefix + "Running command: \"netsh " + addArgs + "\"", MessageLevels.Debug);
                                r = ExternalProcess.Run("netsh", addArgs, (text, wrn) =>
                                {
                                    msg?.AddMessage(logPrefix + "[AddCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                                });
                            }
                        }
                        break;
                    default:
                        msg?.AddMessage(logPrefix + "Bind certificate's on platform " + pid + " is not supported!", MessageLevels.Warning);
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                msg?.AddMessage(logPrefix + "Failed to get certificate for prefix " + listenerPrefix.ToQuoted(), ex, MessageLevels.Warning);
                return false;
            }
        }

    }



}
