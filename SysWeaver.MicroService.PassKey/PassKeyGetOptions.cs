using Fido2NetLib;
using Fido2NetLib.Objects;
using System;
using System.Linq;

namespace SysWeaver.MicroService
{
    public sealed class PassKeyGetOptions
    {
        public PassKeyGetCredential publicKey { get; set; }
    }

    public sealed class PassKeyGetResponse
    {
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
            userVerification = f.UserVerification?.ToString()?.RemoveCamelCase('-')?.FastToLower();
            hints = f.Hints.Select(x =>  x.ToString().RemoveCamelCase('-').FastToLower()).ToArray();
            extensions = f.Extensions == null ? null : new PassKeyAuthenticationExtensionsClientInputs(f.Extensions);
        }

    }

}