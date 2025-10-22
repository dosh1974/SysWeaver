namespace SysWeaver.MicroService
{
    public sealed class PassKeyParams
    {
        /// <summary>
        /// The life time in seconds of a challenge (a user must validate within this period)
        /// </summary>
        public int ChallengeLifeTime = 3 * 60;


        /// <summary>
        /// A string that specifies the relying party's identifier (for example "login.example.org"). For security purposes:
        /// The calling web app verifies that rpId matches the relying party's origin.
        /// The authenticator verifies that rpId matches the rpId of the credential used for the authentication ceremony.
        /// This value defaults to the current origin's domain.
        /// </summary>
        public string RpId = "www.sysweaver.com";

        /// <summary>
        /// A string representing the name of the relying party (e.g. "Facebook"). 
        /// This is the name the user will be presented with when creating or validating a WebAuthn operation.
        /// </summary>
        public string RpName;

        /// <summary>
        /// A string specifying the relying party's requirements for user verification of the authentication process. 
        /// This verification is initiated by the authenticator, which will request the user to provide an available factor (for example a PIN or a biometric input of some kind).
        ///  The value can be one of the following:
        ///  "required" - The relying party requires user verification, and the operation will fail if it does not occur.
        ///  "preferred" - The relying party prefers user verification if possible, but the operation will not fail if it does not occur.
        ///  "discouraged" - The relying party does not want user verification, in the interests of making user interaction as smooth as possible.
        ///  This value defaults to "preferred".
        /// </summary>
        public string UserVerification;


        /// <summary>
        /// Additional prefixes (origins including schema and port) that is valid
        /// </summary>
        public string[] Prefixes;

    }



}