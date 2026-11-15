using System;
using System.Linq;

using Fido2NetLib;
using Fido2NetLib.Objects;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// The options to pass to navigator.credentials.create
    /// </summary>
    public sealed class PassKeyCreateOptions
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
        /// The publicKey member to pass to navigator.credentials.create, null if the challenge could not be created (see <see cref="error"/>)
        /// </summary>
        public PassKeyCreateCredential publicKey { get; set; }

        internal static PassKeyCreateOptions Fail(PassKeyErrors error) => new PassKeyCreateOptions
        {
            error = error,
        };
    }

    /// <summary>
    /// The response of navigator.credentials.create
    /// </summary>
    public sealed class PassKeyCreateResponse
    {
        /// <summary>
        /// The id of the challenge (from <see cref="PassKeyCreateOptions.challengeId"/>)
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
        /// The authenticator attestation response
        /// </summary>
        public PassKeyCreateAuthenticatorResponse response { get; set; }

        /// <summary>
        /// The credential type, "public-key"
        /// </summary>
        public String type { get; set; }
    }

    /// <summary>
    /// The authenticator attestation response of navigator.credentials.create
    /// </summary>
    public sealed class PassKeyCreateAuthenticatorResponse
    {
        /// <summary>
        /// The attestation object (contains the new public key)
        /// </summary>
        public Byte[] attestationObject { get; set; }

        /// <summary>
        /// The client data (json)
        /// </summary>
        public Byte[] clientDataJSON { get; set; }

        /// <summary>
        /// The result of response.getTransports() (if supported by the browser)
        /// </summary>
        public String[] transports { get; set; }
    }

    /// <summary>
    /// The relying party (this site)
    /// </summary>
    public sealed class PassKeyRp
    {
        /// <summary>
        /// The relying party id (a domain)
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The name of the relying party, shown to the user
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyRp()
        {
        }

        internal PassKeyRp(PublicKeyCredentialRpEntity f)
        {
            id = f.Id;
            name = f.Name;
        }
    }

    /// <summary>
    /// The user a passkey is created for
    /// </summary>
    public sealed class PassKeyUser
    {
        /// <summary>
        /// The account name of the user
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// The user handle (an opaque id)
        /// </summary>
        public byte[] id { get; set; }

        /// <summary>
        /// The name shown to the user
        /// </summary>
        public string displayName { get; set; }


        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyUser()
        {
        }

        internal PassKeyUser(Fido2User f)
        {
            name = f.Name;
            id = f.Id;
            displayName = f.DisplayName;
        }
    }

    /// <summary>
    /// An accepted public key credential type and signature algorithm
    /// </summary>
    public sealed class PassKeyCredParam
    {

        /// <summary>
        /// The COSE algorithm identifier (ex: -7 for ES256)
        /// </summary>
        public int alg { get; set; }

        /// <summary>
        /// The credential type, "public-key"
        /// </summary>
        public String type { get; set; }

        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyCredParam()
        {
        }
        internal PassKeyCredParam(PubKeyCredParam f)
        {
            alg = (int)f.Alg;
            type = PassKeyTools.ToWebAuthn(f.Type);
        }
    }

    /// <summary>
    /// Requirements for the authenticator used when creating a passkey
    /// </summary>
    public sealed class PassKeyAuthenticatorSelection
    {
        /// <summary>
        /// Restrict to "platform" or "cross-platform" authenticators, null for any
        /// </summary>
        public String authenticatorAttachment { get; set; }

        /// <summary>
        /// True if a discoverable credential is required (legacy flag, true when <see cref="residentKey"/> is "required")
        /// </summary>
        public bool requireResidentKey { get; set; }

        /// <summary>
        /// The discoverable credential (resident key) requirement ("required", "preferred" or "discouraged")
        /// </summary>
        public String residentKey { get; set; }

        /// <summary>
        /// The user verification requirement ("required", "preferred" or "discouraged")
        /// </summary>
        public String userVerification { get; set; }

        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyAuthenticatorSelection()
        {
        }

        internal PassKeyAuthenticatorSelection(AuthenticatorSelection f)
        {
            authenticatorAttachment = PassKeyTools.ToWebAuthn(f.AuthenticatorAttachment);
            requireResidentKey = f.ResidentKey == ResidentKeyRequirement.Required;
            userVerification = PassKeyTools.ToWebAuthn(f.UserVerification);
            residentKey = PassKeyTools.ToWebAuthn(f.ResidentKey);
        }

    }

    /// <summary>
    /// Describes a credential (used in allow and exclude lists)
    /// </summary>
    public sealed class PassKeyPublicKeyCredentialDescriptor
    {

        /// <summary>
        /// The raw credential id
        /// </summary>
        public byte[] id { get; set; }

        /// <summary>
        /// The transports the credential can be reached by (ex: "usb", "internal", "hybrid"), can be null
        /// </summary>
        public String[] transports { get; set; }

        /// <summary>
        /// The credential type, "public-key"
        /// </summary>
        public String type { get; set; }


        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyPublicKeyCredentialDescriptor()
        {
        }

        internal PassKeyPublicKeyCredentialDescriptor(PublicKeyCredentialDescriptor f)
        {
            type = PassKeyTools.ToWebAuthn(f.Type);
            id = f.Id;
            transports = f.Transports?.Select(x => PassKeyTools.ToWebAuthn(x))?.ToArray();
        }
    }

    /// <summary>
    /// WebAuthn client extension inputs
    /// </summary>
    public sealed class PassKeyAuthenticationExtensionsClientInputs
    {
        /// <summary>
        /// The appid extension (legacy FIDO U2F application id), can be null
        /// </summary>
        public string appid { get; set; }

        /// <summary>
        /// The exts extension (request the list of supported extensions), can be null
        /// </summary>
        public bool? exts { get; set; }

        /// <summary>
        /// The credProps extension (request properties of the created credential), can be null
        /// </summary>
        public bool? credProps { get; set; }

        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyAuthenticationExtensionsClientInputs()
        {
        }

        internal PassKeyAuthenticationExtensionsClientInputs(AuthenticationExtensionsClientInputs f)
        {
            appid = f.AppID;
            exts = f.Extensions;
            credProps = f.CredProps;
        }
    }

    /// <summary>
    /// The publicKey options to pass to navigator.credentials.create
    /// </summary>
    public sealed class PassKeyCreateCredential
    {

        /// <summary>
        /// The time in milliseconds that the browser should wait for the user
        /// </summary>
        public ulong timeout { get; set; }

        /// <summary>
        /// The attestation conveyance preference ("none", "indirect", "direct" or "enterprise")
        /// </summary>
        public String attestation { get; set; }

        /// <summary>
        /// Requirements for the authenticator, can be null
        /// </summary>
        public PassKeyAuthenticatorSelection authenticatorSelection { get; set; }

        /// <summary>
        /// The random challenge
        /// </summary>
        public byte[] challenge { get; set; }

        /// <summary>
        /// Credentials that already exist for the user, the authenticator will not create a new passkey if it holds one of these
        /// </summary>
        public PassKeyPublicKeyCredentialDescriptor[] excludeCredentials { get; set; }

        /// <summary>
        /// Client extension inputs, can be null
        /// </summary>
        public PassKeyAuthenticationExtensionsClientInputs extensions { get; set; }

        /// <summary>
        /// The accepted credential types and signature algorithms, in order of preference
        /// </summary>
        public PassKeyCredParam[] pubKeyCredParams { get; set; }

        /// <summary>
        /// The relying party (this site)
        /// </summary>
        public PassKeyRp rp { get; set; }

        /// <summary>
        /// The user the passkey is created for
        /// </summary>
        public PassKeyUser user { get; set; }

        /// <summary>
        /// Hints to the browser about which kind of authenticator to use, can be null
        /// </summary>
        public String[] hints { get; set; }


        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public PassKeyCreateCredential()
        {
        }

        internal PassKeyCreateCredential(CredentialCreateOptions f)
        {
            timeout = f.Timeout;
            attestation = PassKeyTools.ToWebAuthn(f.Attestation);
            authenticatorSelection = f.AuthenticatorSelection == null ? null : new PassKeyAuthenticatorSelection(f.AuthenticatorSelection);
            challenge = f.Challenge;
            excludeCredentials = f.ExcludeCredentials?.Select(x => new PassKeyPublicKeyCredentialDescriptor(x))?.ToArray();
            extensions = f.Extensions == null ? null : new PassKeyAuthenticationExtensionsClientInputs(f.Extensions);
            pubKeyCredParams = f.PubKeyCredParams?.Select(x => new PassKeyCredParam(x))?.ToArray();
            rp = f.Rp == null ? null : new PassKeyRp(f.Rp);
            user = f.User == null ? null : new PassKeyUser(f.User);
            hints = f.Hints?.Select(x => PassKeyTools.ToWebAuthn(x)).ToArray();
        }

    }

}
