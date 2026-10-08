using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Helpers for passkeys (WebAuthn)
    /// </summary>
    public static class PassKeyTools
    {
        #region Origin and relying party

        /// <summary>
        /// Get the origin ("scheme://host[:port]") of a prefix or url
        /// </summary>
        /// <param name="prefix">A prefix or url, ex: "https://www.example.org:443/app/"</param>
        /// <returns>The origin, ex: "https://www.example.org:443", null if it isn't an absolute url</returns>
        public static String GetOrigin(String prefix)
        {
            if (prefix == null)
                return null;
            var p = prefix.IndexOf("://", StringComparison.Ordinal);
            if (p <= 0)
                return null;
            var e = prefix.IndexOfAny(PathChars, p + 3);
            return e < 0 ? prefix : prefix.Substring(0, e);
        }

        static readonly Char[] PathChars = ['/', '?', '#'];

        /// <summary>
        /// Get the (lower case) host of an origin
        /// </summary>
        /// <param name="origin">The origin, ex: "https://www.example.org:443"</param>
        /// <returns>The host, ex: "www.example.org" ("[::1]" for an IPv6 address), null if it isn't an origin</returns>
        public static String GetHost(String origin)
        {
            if (origin == null)
                return null;
            var p = origin.IndexOf("://", StringComparison.Ordinal);
            if (p <= 0)
                return null;
            p += 3;
            int e;
            if ((p < origin.Length) && (origin[p] == '['))
            {
                e = origin.IndexOf(']', p);
                e = e < 0 ? origin.Length : e + 1;
            }
            else
            {
                e = origin.IndexOfAny(PortOrPathChars, p);
                if (e < 0)
                    e = origin.Length;
            }
            var host = origin.Substring(p, e - p).TrimEnd('.').ToLowerInvariant();
            return host.Length <= 0 ? null : host;
        }

        static readonly Char[] PortOrPathChars = [':', '/', '?', '#'];

        /// <summary>
        /// Check if a host is an ip address (passkeys can't be used on ip addresses)
        /// </summary>
        /// <param name="host">The host</param>
        /// <returns>True if the host is an IPv4 or IPv6 address</returns>
        public static bool IsIpHost(String host)
        {
            if (String.IsNullOrEmpty(host))
                return false;
            if (host[0] == '[')
                return true;
            return IPAddress.TryParse(host, out _);
        }

        /// <summary>
        /// Check if a host is localhost (or a sub domain of localhost)
        /// </summary>
        /// <param name="host">The host</param>
        /// <returns>True if the host is localhost</returns>
        public static bool IsLocalHost(String host)
            => (host != null) && (host.Equals("localhost", StringComparison.Ordinal) || host.EndsWith(".localhost", StringComparison.Ordinal));

        /// <summary>
        /// Check if passkeys can be used on an origin (https or http://localhost, and not an ip address)
        /// </summary>
        /// <param name="origin">The origin</param>
        /// <returns>True if passkeys can be used</returns>
        public static bool CanUse(String origin)
        {
            var host = GetHost(origin);
            if (host == null)
                return false;
            if (IsIpHost(host))
                return false;
            if (origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;
            return origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && IsLocalHost(host);
        }

        /// <summary>
        /// Get the relying party id to use for a host
        /// </summary>
        /// <param name="host">The (lower case) host of the request</param>
        /// <param name="configuredRpId">The configured relying party id (a registrable domain), can be null</param>
        /// <returns>The configured relying party id if the host is equal to it or a sub domain of it, else the host</returns>
        public static String GetRpId(String host, String configuredRpId)
        {
            if (String.IsNullOrEmpty(configuredRpId))
                return host;
            var rp = configuredRpId.Trim().TrimEnd('.').ToLowerInvariant();
            if (host.Equals(rp, StringComparison.Ordinal))
                return rp;
            if ((host.Length > rp.Length) && host.EndsWith(rp, StringComparison.Ordinal) && (host[host.Length - rp.Length - 1] == '.'))
                return rp;
            return host;
        }

        #endregion//Origin and relying party

        #region WebAuthn enum values

        static readonly ConcurrentDictionary<Enum, String> EnumToString = new ConcurrentDictionary<Enum, String>();
        static readonly ConcurrentDictionary<Type, Dictionary<String, Enum>> StringToEnum = new ConcurrentDictionary<Type, Dictionary<String, Enum>>();

        static String GetEnumString(Enum value)
        {
            var name = value.ToString();
            var m = value.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static);
            var a = m?.GetCustomAttribute<EnumMemberAttribute>();
            return a?.Value ?? name.ToLowerInvariant();
        }

        /// <summary>
        /// Get the WebAuthn (json) string of a Fido2 enum value, ex: AuthenticatorAttachment.CrossPlatform => "cross-platform"
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="value">The value</param>
        /// <returns>The WebAuthn string</returns>
        public static String ToWebAuthn<T>(T value) where T : struct, Enum
            => EnumToString.GetOrAdd(value, GetEnumString);

        /// <summary>
        /// Get the WebAuthn (json) string of a nullable Fido2 enum value
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="value">The value</param>
        /// <returns>The WebAuthn string or null</returns>
        public static String ToWebAuthn<T>(T? value) where T : struct, Enum
            => value.HasValue ? ToWebAuthn(value.Value) : null;

        /// <summary>
        /// Parse a WebAuthn (json) string of a Fido2 enum
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="value">The WebAuthn string, ex: "cross-platform"</param>
        /// <returns>The value or null if it's unknown</returns>
        public static T? FromWebAuthn<T>(String value) where T : struct, Enum
        {
            if (value == null)
                return null;
            var map = StringToEnum.GetOrAdd(typeof(T), _ => Enum.GetValues<T>().ToDictionary(x => ToWebAuthn(x), x => (Enum)x, StringComparer.OrdinalIgnoreCase));
            return map.TryGetValue(value.Trim(), out var v) ? (T)v : null;
        }

        #endregion//WebAuthn enum values

        #region Conversions

        /// <summary>
        /// Convert a client response to the Fido2 type
        /// </summary>
        /// <param name="response">The response from the client</param>
        /// <returns>The Fido2 response</returns>
        public static AuthenticatorAttestationRawResponse ToRaw(PassKeyCreateResponse response)
        {
            var r = response.response;
            return new AuthenticatorAttestationRawResponse
            {
                Id = response.rawId == null ? response.id : Base64Url.EncodeToString(response.rawId),
                RawId = response.rawId,
                Response = r == null ? null : new AuthenticatorAttestationRawResponse.AttestationResponse
                {
                    AttestationObject = r.attestationObject,
                    ClientDataJson = r.clientDataJSON,
                    Transports = r.transports?.Select(FromWebAuthn<AuthenticatorTransport>).Where(x => x.HasValue).Select(x => x.Value).Distinct().ToArray() ?? [],
                },
                Type = FromWebAuthn<PublicKeyCredentialType>(response.type),
                ClientExtensionResults = new AuthenticationExtensionsClientOutputs(),
            };
        }

        /// <summary>
        /// Convert a client response to the Fido2 type
        /// </summary>
        /// <param name="response">The response from the client</param>
        /// <returns>The Fido2 response</returns>
        public static AuthenticatorAssertionRawResponse ToRaw(PassKeyGetResponse response)
        {
            var r = response.response;
            return new AuthenticatorAssertionRawResponse
            {
                Id = response.rawId == null ? response.id : Base64Url.EncodeToString(response.rawId),
                RawId = response.rawId,
                Response = r == null ? null : new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = r.authenticatorData,
                    ClientDataJson = r.clientDataJSON,
                    Signature = r.signature,
                    UserHandle = r.userHandle,
                },
                Type = FromWebAuthn<PublicKeyCredentialType>(response.type),
                ClientExtensionResults = new AuthenticationExtensionsClientOutputs(),
            };
        }

        /// <summary>
        /// Create a credential descriptor (for allow or exclude lists)
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="transports">Comma separated transports, can be null</param>
        /// <returns>The descriptor</returns>
        public static PublicKeyCredentialDescriptor ToDescriptor(String credentialId, String transports)
        {
            var t = transports?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(FromWebAuthn<AuthenticatorTransport>).Where(x => x.HasValue).Select(x => x.Value).Distinct().ToArray();
            return new PublicKeyCredentialDescriptor(PublicKeyCredentialType.PublicKey, Convert.FromBase64String(credentialId), (t == null) || (t.Length <= 0) ? null : t);
        }

        /// <summary>
        /// Get the transports as a comma separated string
        /// </summary>
        /// <param name="transports">The transports, can be null</param>
        /// <returns>The transports as a comma separated string, null if none</returns>
        public static String ToString(IEnumerable<AuthenticatorTransport> transports)
        {
            if (transports == null)
                return null;
            var s = String.Join(',', transports.Distinct().Select(x => ToWebAuthn(x)));
            return s.Length <= 0 ? null : s;
        }

        #endregion//Conversions

        #region Names

        /// <summary>
        /// Names of some well known passkey providers (by AAGUID)
        /// </summary>
        static readonly Dictionary<Guid, String> Providers = new Dictionary<Guid, String>
        {
            { Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4"), "Google Password Manager" },
            { Guid.Parse("fbfc3007-154e-4ecc-8c0b-6e020557d7bd"), "iCloud Keychain" },
            { Guid.Parse("08987058-cadc-4b81-b6e1-30de50dcbe96"), "Windows Hello" },
            { Guid.Parse("9ddd1817-af5a-4672-a2b9-3e3dd95000a9"), "Windows Hello" },
            { Guid.Parse("6028b017-b1d4-4c02-b4b3-afcdafc96bb2"), "Windows Hello" },
            { Guid.Parse("bada5566-a7aa-401f-bd96-45619a55120d"), "1Password" },
            { Guid.Parse("d548826e-79b4-db40-a3d8-11116f7e8349"), "Bitwarden" },
        };

        /// <summary>
        /// Get a user friendly name of a new passkey
        /// </summary>
        /// <param name="aaGuid">The authenticator attestation guid</param>
        /// <param name="userAgent">The user agent of the browser that created it, can be null</param>
        /// <returns>A name, ex: "Windows Hello (Edge on Windows)"</returns>
        public static String GetName(Guid aaGuid, String userAgent)
        {
            var device = GetDeviceName(userAgent);
            if (!Providers.TryGetValue(aaGuid, out var provider))
                return device ?? "Passkey";
            return device == null ? provider : String.Concat(provider, " (", device, ')');
        }

        /// <summary>
        /// Get a short device description from a user agent string, ex: "Chrome on Android"
        /// </summary>
        /// <param name="userAgent">The user agent</param>
        /// <returns>The description or null if it's unknown</returns>
        public static String GetDeviceName(String userAgent)
        {
            if (String.IsNullOrEmpty(userAgent))
                return null;
            String os =
                userAgent.Contains("Windows", StringComparison.Ordinal) ? "Windows" :
                userAgent.Contains("Android", StringComparison.Ordinal) ? "Android" :
                userAgent.Contains("iPhone", StringComparison.Ordinal) ? "iPhone" :
                userAgent.Contains("iPad", StringComparison.Ordinal) ? "iPad" :
                userAgent.Contains("CrOS", StringComparison.Ordinal) ? "ChromeOS" :
                userAgent.Contains("Mac OS X", StringComparison.Ordinal) ? "macOS" :
                userAgent.Contains("Linux", StringComparison.Ordinal) ? "Linux" :
                null;
            String browser =
                userAgent.Contains("Edg", StringComparison.Ordinal) ? "Edge" :
                userAgent.Contains("OPR/", StringComparison.Ordinal) ? "Opera" :
                userAgent.Contains("SamsungBrowser", StringComparison.Ordinal) ? "Samsung Internet" :
                userAgent.Contains("Firefox/", StringComparison.Ordinal) || userAgent.Contains("FxiOS", StringComparison.Ordinal) ? "Firefox" :
                userAgent.Contains("Chrome/", StringComparison.Ordinal) || userAgent.Contains("CriOS", StringComparison.Ordinal) ? "Chrome" :
                userAgent.Contains("Safari/", StringComparison.Ordinal) ? "Safari" :
                null;
            if (os == null)
                return browser;
            if (browser == null)
                return os;
            return String.Concat(browser, " on ", os);
        }

        #endregion//Names

    }
}
