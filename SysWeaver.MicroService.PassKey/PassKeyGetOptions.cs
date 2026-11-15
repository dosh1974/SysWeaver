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

        /// <summary>
        /// The publicKey member to pass to navigator.credentials.get, null if the challenge could not be created (see <see cref="error"/>)
        /// </summary>
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

        /// <summary>
        /// The authenticator attachment of the credential ("platform" or "cross-platform"), if reported by the browser
        /// </summary>
        public string authenticatorAttachment { get; set; }

        /// <summary>
        /// The credential id (base64url encoded)
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The raw credential id
        /// </summary>
        public Byte[] rawId { get; set; }

        /// <summary>
        /// The authenticator assertion response
        /// </summary>
        public PassKeyGetAuthenticatorResponse response { get; set; }

        /// <summary>
        /// The credential type, "public-key"
        /// </summary>
        public String type { get; set; }
    }

    /// <summary>
    /// The authenticator assertion response of navigator.credentials.get
    /// </summary>
    public sealed class PassKeyGetAuthenticatorResponse
    {
        /// <summary>
        /// The authenticator data
        /// </summary>
        public Byte[] authenticatorData { get; set; }

        /// <summary>
        /// The client data (json)
        /// </summary>
        public Byte[] clientDataJSON { get; set; }

        /// <summary>
        /// The assertion signature
        /// </summary>
        public Byte[] signature { get; set; }

        /// <summary>
        /// The user handle (the user id given when the credential was created), can be null
        /// </summary>
        public Byte[] userHandle { get; set; }
    }


    /// <summary>
    /// The publicKey options to pass to navigator.credentials.get
    /// </summary>
    public sealed class PassKeyGetCredential
    {
        /// <summary>
        /// The credentials that are allowed to be used, empty or null to allow any (discoverable) credential
        /// </summary>
        public PassKeyPublicKeyCredentialDescriptor[] allowCredentials { get; set; }

        /// <summary>
        /// The random challenge that must be signed
        /// </summary>
        public byte[] challenge { get; set; }

        /// <summary>
        /// The time in milliseconds that the browser should wait for the user
        /// </summary>
        public ulong timeout { get; set; }

        /// <summary>
        /// The relying party id
        /// </summary>
        public String rpId { get; set; }


        /// <summary>
        /// The user verification requirement ("required", "preferred" or "discouraged")
        /// </summary>
        public String userVerification { get; set; }

        /// <summary>
        /// Hints to the browser about which kind of authenticator to use, can be null
        /// </summary>
        public String[] hints { get; set; }

        /// <summary>
        /// Client extension inputs, can be null
        /// </summary>
        public PassKeyAuthenticationExtensionsClientInputs extensions { get; set; }

        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
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
