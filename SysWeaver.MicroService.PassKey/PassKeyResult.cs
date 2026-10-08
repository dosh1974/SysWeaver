using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Passkey error codes
    /// </summary>
    public enum PassKeyErrors
    {
        /// <summary>
        /// No error
        /// </summary>
        None = 0,
        /// <summary>
        /// Passkeys can't be used on this host (an ip address or a http origin that isn't localhost)
        /// </summary>
        NotSupported,
        /// <summary>
        /// A user is already signed in
        /// </summary>
        AlreadySignedIn,
        /// <summary>
        /// No user is signed in
        /// </summary>
        NotSignedIn,
        /// <summary>
        /// The challenge has expired or was already used (or is unknown)
        /// </summary>
        ChallengeExpired,
        /// <summary>
        /// The passkey isn't known by the server (removed or created for some other site)
        /// </summary>
        UnknownCredential,
        /// <summary>
        /// The user don't have any passkeys (or the user doesn't exist)
        /// </summary>
        NoPassKeys,
        /// <summary>
        /// The response from the authenticator failed verification
        /// </summary>
        VerificationFailed,
        /// <summary>
        /// The passkey is already registered
        /// </summary>
        AlreadyRegistered,
        /// <summary>
        /// The token (link or QR code) has expired or was already used
        /// </summary>
        TokenExpired,
        /// <summary>
        /// The account already exist (when creating a new account)
        /// </summary>
        AccountExists,
        /// <summary>
        /// The last sign in method of a user can't be removed
        /// </summary>
        LastSignInMethod,
    }

    /// <summary>
    /// The result of a passkey operation
    /// </summary>
    public sealed class PassKeyResult
    {
        /// <summary>
        /// The error code, None (0) on success
        /// </summary>
        public PassKeyErrors Error { get; set; }

        /// <summary>
        /// An optional technical message (when the verification fails)
        /// </summary>
        public String Message { get; set; }

        /// <summary>
        /// The signed in user (when a user was signed in by the operation)
        /// </summary>
        public AuthInfo User { get; set; }

        internal static readonly PassKeyResult Ok = new PassKeyResult();

        internal static PassKeyResult Fail(PassKeyErrors error, String message = null) => new PassKeyResult
        {
            Error = error,
            Message = message,
        };
    }

    /// <summary>
    /// Information about a passkey of the signed in user
    /// </summary>
    public sealed class PassKeyInfo
    {
        /// <summary>
        /// The credential id (used to rename or remove it)
        /// </summary>
        public String Id { get; set; }

        /// <summary>
        /// The name of the passkey
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// When the passkey was created (UTC)
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// When the passkey was last used to sign in (UTC)
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// True if the passkey is synced (backed up), so it's available on the users other devices
        /// </summary>
        public bool Synced { get; set; }

        /// <summary>
        /// True if the passkey was created on this device (browser)
        /// </summary>
        public bool ThisDevice { get; set; }
    }

    /// <summary>
    /// Rename a passkey
    /// </summary>
    public sealed class PassKeyRenameRequest
    {
        /// <summary>
        /// The credential id (from <see cref="PassKeyInfo.Id"/>)
        /// </summary>
        public String Id { get; set; }

        /// <summary>
        /// The new name
        /// </summary>
        public String Name { get; set; }
    }

}
