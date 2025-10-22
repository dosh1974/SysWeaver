using System;
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
        /// <param name="provider">The certificate provider</param>
        /// <param name="logPrefix">The prefix to use for logging</param>
        /// <param name="onCertChanged"></param>
        /// <returns></returns>
        public static async Task<bool> BindHttps(String listenerPrefix, ICertificateProvider provider, Action<ICertificateProvider, String> onCertChanged = null, IMessageHost msg = null, String logPrefix = "")
        {
            try
            {
                var cert = await provider.GetCert().ConfigureAwait(false);
                var hash = cert.GetCertHashString().FastToLower();
                var uri = new Uri(listenerPrefix.Replace("*", "localhost"));
                if (uri.Scheme.FastToLower() != "https")
                {
                    msg?.AddMessage(logPrefix + "Can't bind certificate to non-https prefix " + listenerPrefix.ToQuoted(), MessageLevels.Warning);
                    return true;
                }
                String bind = 
                    uri.Host == "localhost" 
                    ? 
                        ("ipport=0.0.0.0:" + uri.Port) 
                    : 
                        ("hostnameport=" + uri.Host + ":" + uri.Port + " certstorename=MY");
                var pid = Environment.OSVersion.Platform;
                void onChange()
                {
                    provider.OnChanged -= onChange;
                    onCertChanged?.Invoke(provider, listenerPrefix);
                };
                switch (pid)
                {
                    case PlatformID.Win32NT:

                        try
                        {
                            cert.Install();
                        }
                        catch (Exception ex)
                        {
                            msg?.AddMessage(logPrefix + "Failed to install certificate " + hash.ToQuoted(), ex, MessageLevels.Warning);
                        }
                        string updateArgs = "http update sslcert " + bind + " certhash=" + hash + " appid=" + EnvInfo.AppGuid;
                        msg?.AddMessage(logPrefix + "Running command: \"netsh " + updateArgs + "\"", MessageLevels.Debug);
                        var r = ExternalProcess.Run("netsh", updateArgs, (text, wrn) =>
                        {
                            msg?.AddMessage(logPrefix + "[UpdateCert] " + text, wrn ? MessageLevels.Warning : MessageLevels.Debug);
                        });
                        if (r != 0)
                        {
                            string addArgs = "http add sslcert " + bind + " certhash=" + hash + " appid=" + EnvInfo.AppGuid;
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
                        provider.OnChanged += onChange;
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
