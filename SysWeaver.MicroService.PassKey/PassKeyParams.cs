namespace SysWeaver.MicroService
{
    public sealed class PassKeyParams
    {
        /// <summary>
        /// The life time in seconds of a challenge (a user must validate within this period).
        /// Sent to the browser as the timeout and enforced by the server.
        /// </summary>
        public int ChallengeLifeTime = 3 * 60;


        /// <summary>
        /// Optional relying party id, a registrable domain (for example "example.org") that passkeys are bound to.
        /// It is used when the request host is equal to it or is a sub domain of it (so that a passkey created on "www.example.org" works on "login.example.org").
        /// For any other host (or if null) the request host is used.
        /// </summary>
        public string RpId;

        /// <summary>
        /// A string representing the name of the relying party (e.g. "Facebook").
        /// This is the name the user will be presented with when creating or validating a WebAuthn operation.
        /// Defaults to the application name.
        /// </summary>
        public string RpName;

        /// <summary>
        /// A string specifying the relying party's requirements for user verification (when creating and using a passkey).
        /// This verification is initiated by the authenticator, which will request the user to provide an available factor (for example a PIN or a biometric input of some kind).
        ///  The value can be one of the following:
        ///  "required" - The relying party requires user verification, and the operation will fail if it does not occur.
        ///  "preferred" - The relying party prefers user verification if possible, but the operation will not fail if it does not occur.
        ///  "discouraged" - The relying party does not want user verification, in the interests of making user interaction as smooth as possible.
        ///  This value defaults to "required" (a passkey is then possession and verification).
        /// </summary>
        public string UserVerification = "required";

        /// <summary>
        /// A string specifying the relying party's requirements for discoverable credentials (resident keys) when creating a passkey.
        ///  The value can be one of the following:
        ///  "required" - The credential must be discoverable (sign in without entering a user id), creation fails on authenticators that can't store it.
        ///  "preferred" - A discoverable credential is created if possible.
        ///  "discouraged" - A server-side (non-discoverable) credential is preferred, such credentials can only be used after entering a user id.
        ///  This value defaults to "required".
        /// </summary>
        public string ResidentKey = "required";


        /// <summary>
        /// Additional origins (scheme, host and optionally port, ex: "https://login.example.org") that are accepted besides the origin of the request.
        /// Required when the server is reached through a proxy that changes the host or scheme (ex: a TLS terminating reverse proxy).
        /// </summary>
        public string[] Prefixes;

    }



}
