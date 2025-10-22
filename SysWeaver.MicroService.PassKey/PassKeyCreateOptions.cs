using SysWeaver.Auth;
using SysWeaver.Net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

using Fido2NetLib;
using Fido2NetLib.Objects;


namespace SysWeaver.MicroService
{

    public sealed class PassKeyCreateOptions
    {
        public PassKeyCreateCredential publicKey { get; set; }
    }

    public sealed class PassKeyCreateResponse
    {
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

    }

    public sealed class PassKeyRp
    {
        public string id { get; set; }

        public string name { get; set; }

        public string icon { get; set; }

        public PassKeyRp()
        {
        }

        internal PassKeyRp(PublicKeyCredentialRpEntity f)
        {
            id = f.Id;
            name = f.Name;
            icon = f.Icon;
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
            type = f.Type.ToString().RemoveCamelCase('-').FastToLower();
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
            authenticatorAttachment = f.AuthenticatorAttachment?.ToString()?.FastToLower();
            requireResidentKey = f.RequireResidentKey;
            userVerification = f.UserVerification.ToString().FastToLower();
            residentKey = f.ResidentKey.ToString().FastToLower();
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
            type = f.Type.ToString()?.RemoveCamelCase('-')?.FastToLower();
            id = f.Id;
            transports = f.Transports?.Select(x => x.ToString().FastToLower())?.ToArray();
        }
    }

    public sealed class PassKeyAuthenticationExtensionsClientInputs
    {
        public string appid { get; set; }

        public byte[][] authnSel { get; set; }

        public bool? exts { get; set; }

        public bool? uvm { get; set; }

        public PassKeyAuthenticationExtensionsClientInputs()
        {
        }

        internal PassKeyAuthenticationExtensionsClientInputs(AuthenticationExtensionsClientInputs f)
        {
            appid = f.AppID;
            //authnSel = f.;
            exts = f.Extensions;
            uvm = f.UserVerificationMethod;
        }
    }

    public sealed class PassKeyCreateCredential
    {

        //        public long timeout { get; set; }

        public String attestation { get; set; }

        public PassKeyAuthenticatorSelection authenticatorSelection { get; set; }

        public byte[] challenge { get; set; }

        public PassKeyPublicKeyCredentialDescriptor[] excludeCredentials { get; set; }

        public PassKeyAuthenticationExtensionsClientInputs extensions { get; set; }

        public PassKeyCredParam[] pubKeyCredParams { get; set; }

        public PassKeyRp rp { get; set; }

        public PassKeyUser user { get; set; }


        public PassKeyCreateCredential()
        {
        }

        internal PassKeyCreateCredential(CredentialCreateOptions f)
        {
            attestation = f.Attestation.ToString().FastToLower();
            authenticatorSelection = f.AuthenticatorSelection == null ? null : new PassKeyAuthenticatorSelection(f.AuthenticatorSelection);
            challenge = f.Challenge;
            excludeCredentials = f.ExcludeCredentials?.Select(x => new PassKeyPublicKeyCredentialDescriptor(x))?.ToArray();
            extensions = f.Extensions == null ? null : new PassKeyAuthenticationExtensionsClientInputs(f.Extensions);
            pubKeyCredParams = f.PubKeyCredParams?.Select(x => new PassKeyCredParam(x))?.ToArray();
            rp = f.Rp == null ? null : new PassKeyRp(f.Rp);
            //            timeout = f.Timeout;
            user = f.User == null ? null : new PassKeyUser(f.User);

        }

    }

}