using Fido2NetLib;
using Fido2NetLib.Objects;
using System;
using System.Linq;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The options to pass to navigator.credentials.get
    /// </summary>
    public sealed class PassKeyGetOptions
    {
        /// <summary>
        /// The id of the challenge, must be sent back with the response
        /// </summary>
        public String challengeId { get; set; }

        /// <summary>
        /// None (0) or the reason why no challenge was created (publicKey is then null)
        /// </summary>
        public PassKeyErrors error { get; set; }

        public PassKeyGetCredential publicKey { get; set; }

        internal static PassKeyGetOptions Fail(PassKeyErrors error) => new PassKeyGetOptions
        {
            error = error,
        };
    }

    /// <summary>
    /// The response of navigator.credentials.get
    /// </summary>
    public sealed class PassKeyGetResponse
    {
        /// <summary>
        /// The id of the challenge (from <see cref="PassKeyGetOptions.challengeId"/>)
        /// </summary>
        public String challengeId { get; set; }

        public string authenticatorAttachment { get; set; }
        public string id { get; set; }
        public Byte[] rawId { get; set; }
        public PassKeyGetAuthenticatorResponse response { get; set; }
        public String type { get; set; }
    }

    public sealed class PassKeyGetAuthenticatorResponse
    {
        public Byte[] authenticatorData { get; set; }
        public Byte[] clientDataJSON { get; set; }
        public Byte[] signature { get; set; }
        public Byte[] userHandle { get; set; }
    }


    public sealed class PassKeyGetCredential
    {
        public PassKeyPublicKeyCredentialDescriptor[] allowCredentials { get; set; }
        public byte[] challenge { get; set; }
        public ulong timeout { get; set; }

        public String rpId { get; set; }


        public String userVerification { get; set; }

        public String[] hints { get; set; }

        public PassKeyAuthenticationExtensionsClientInputs extensions { get; set; }

        public PassKeyGetCredential()
        {
        }

        internal PassKeyGetCredential(AssertionOptions f)
        {
            allowCredentials = f.AllowCredentials?.Select(x => new PassKeyPublicKeyCredentialDescriptor(x))?.ToArray();
            challenge = f.Challenge;
            timeout = f.Timeout;
            rpId = f.RpId;
            userVerification = PassKeyTools.ToWebAuthn(f.UserVerification);
            hints = f.Hints?.Select(x => PassKeyTools.ToWebAuthn(x)).ToArray();
            extensions = f.Extensions == null ? null : new PassKeyAuthenticationExtensionsClientInputs(f.Extensions);
        }

    }

}
