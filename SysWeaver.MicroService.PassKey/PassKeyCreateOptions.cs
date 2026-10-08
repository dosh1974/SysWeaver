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

        public string authenticatorAttachment { get; set; }
        public string id { get; set; }
        public Byte[] rawId { get; set; }
        public PassKeyCreateAuthenticatorResponse response { get; set; }

        public String type { get; set; }
    }

    public sealed class PassKeyCreateAuthenticatorResponse
    {
        public Byte[] attestationObject { get; set; }
        public Byte[] clientDataJSON { get; set; }

        /// <summary>
        /// The result of response.getTransports() (if supported by the browser)
        /// </summary>
        public String[] transports { get; set; }
    }

    public sealed class PassKeyRp
    {
        public string id { get; set; }

        public string name { get; set; }

        public PassKeyRp()
        {
        }

        internal PassKeyRp(PublicKeyCredentialRpEntity f)
        {
            id = f.Id;
            name = f.Name;
        }
    }

    public sealed class PassKeyUser
    {
        public string name { get; set; }

        public byte[] id { get; set; }

        public string displayName { get; set; }


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

    public sealed class PassKeyCredParam
    {

        public int alg { get; set; }

        public String type { get; set; }

        public PassKeyCredParam()
        {
        }
        internal PassKeyCredParam(PubKeyCredParam f)
        {
            alg = (int)f.Alg;
            type = PassKeyTools.ToWebAuthn(f.Type);
        }
    }

    public sealed class PassKeyAuthenticatorSelection
    {
        public String authenticatorAttachment { get; set; }

        public bool requireResidentKey { get; set; }

        public String residentKey { get; set; }

        public String userVerification { get; set; }

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

    public sealed class PassKeyPublicKeyCredentialDescriptor
    {

        public byte[] id { get; set; }

        public String[] transports { get; set; }

        public String type { get; set; }


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

    public sealed class PassKeyAuthenticationExtensionsClientInputs
    {
        public string appid { get; set; }

        public bool? exts { get; set; }

        public bool? credProps { get; set; }

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

    public sealed class PassKeyCreateCredential
    {

        public ulong timeout { get; set; }

        public String attestation { get; set; }

        public PassKeyAuthenticatorSelection authenticatorSelection { get; set; }

        public byte[] challenge { get; set; }

        public PassKeyPublicKeyCredentialDescriptor[] excludeCredentials { get; set; }

        public PassKeyAuthenticationExtensionsClientInputs extensions { get; set; }

        public PassKeyCredParam[] pubKeyCredParams { get; set; }

        public PassKeyRp rp { get; set; }

        public PassKeyUser user { get; set; }

        public String[] hints { get; set; }


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
