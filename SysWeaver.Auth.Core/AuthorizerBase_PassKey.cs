using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Auth
{

    /// <summary>
    /// A passkey (WebAuthn public key credential) attached to a user of an <see cref="AuthorizerBase"/>.
    /// A plain data class (no FIDO2 types), the passkey service verifies the credentials, the authorizer only stores them.
    /// </summary>
    public sealed class PassKeyData
    {
        /// <summary>
        /// The credential id (standard base64 of the raw id), unique
        /// </summary>
        public String CredentialId { get; set; }

        /// <summary>
        /// The COSE encoded public key
        /// </summary>
        public Byte[] PublicKey { get; set; }

        /// <summary>
        /// The user handle (user.id) the credential was created with, null if unknown (the handle is then the UTF8 of <see cref="UserGuid"/>)
        /// </summary>
        public Byte[] UserHandle { get; set; }

        /// <summary>
        /// The guid of the user that owns this credential (see <see cref="AuthorizationInfo.Guid"/>), set by the authorizer when read
        /// </summary>
        public String UserGuid { get; set; }

        /// <summary>
        /// The relying party id the credential was created for, null if unknown
        /// </summary>
        public String RpId { get; set; }

        /// <summary>
        /// The last seen signature counter
        /// </summary>
        public long SignCount { get; set; }

        /// <summary>
        /// Comma separated transports (ex: "internal,hybrid"), null if unknown
        /// </summary>
        public String Transports { get; set; }

        /// <summary>
        /// True if the credential may be backed up (synced)
        /// </summary>
        public bool BackupEligible { get; set; }

        /// <summary>
        /// True if the credential is backed up (synced)
        /// </summary>
        public bool BackedUp { get; set; }

        /// <summary>
        /// The authenticator attestation guid, null if unknown
        /// </summary>
        public String AaGuid { get; set; }

        /// <summary>
        /// The device id (cookie) of the device that created the credential
        /// </summary>
        public String DeviceId { get; set; }

        /// <summary>
        /// A user friendly name of the credential
        /// </summary>
        public String DeviceName { get; set; }

        /// <summary>
        /// When the credential was created (UTC)
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// When the credential was last used to sign in (UTC)
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Create a copy
        /// </summary>
        /// <returns>A shallow copy (the byte arrays are shared, they are never modified)</returns>
        public PassKeyData Clone() => (PassKeyData)MemberwiseClone();
    }


    /// <summary>
    /// The result of <see cref="AuthorizerBase.DeletePassKey(Authorization, string)"/>
    /// </summary>
    public enum PassKeyDeleteResults
    {
        /// <summary>
        /// The passkey was removed
        /// </summary>
        Deleted,
        /// <summary>
        /// The passkey doesn't exist (or belongs to some other user)
        /// </summary>
        NotFound,
        /// <summary>
        /// The passkey is the last way the user can sign in, it can't be removed
        /// </summary>
        LastSignInMethod,
    }

    /// <summary>
    /// Errors of the token based passkey methods (<see cref="AuthorizerBase.GetPassKeyTokenUser"/> etc)
    /// </summary>
    public enum PassKeyTokenErrors
    {
        /// <summary>
        /// No error
        /// </summary>
        None,
        /// <summary>
        /// The token is unknown, invalid or has expired (or the authorizer doesn't support tokens)
        /// </summary>
        TokenExpired,
        /// <summary>
        /// The user of the token no longer exist (or is disabled)
        /// </summary>
        UserNoLongerExist,
        /// <summary>
        /// A user with the same identity already exist (new users only)
        /// </summary>
        UserAlreadyExist,
    }

    /// <summary>
    /// The result of the token based passkey methods
    /// </summary>
    public sealed class PassKeyTokenResult
    {
        /// <summary>
        /// The user (not signed in), null on errors
        /// </summary>
        public Authorization Auth;

        /// <summary>
        /// The error, <see cref="PassKeyTokenErrors.None"/> on success
        /// </summary>
        public PassKeyTokenErrors Error;

        /// <summary>
        /// A successful result
        /// </summary>
        /// <param name="auth">The user</param>
        /// <returns>The result</returns>
        public static PassKeyTokenResult Ok(Authorization auth) => new PassKeyTokenResult { Auth = auth };

        /// <summary>
        /// A failed result
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>The result</returns>
        public static PassKeyTokenResult Fail(PassKeyTokenErrors error) => new PassKeyTokenResult { Error = error };

        /// <summary>
        /// The token is unknown, invalid or has expired
        /// </summary>
        public static readonly PassKeyTokenResult Expired = Fail(PassKeyTokenErrors.TokenExpired);
    }

    /// <summary>
    /// Information about a user that will be created from a sign up token (see <see cref="AuthorizerBase.GetPassKeyNewUserInfo"/>)
    /// </summary>
    public sealed class PassKeyNewUserInfo
    {
        /// <summary>
        /// The user name, may be null
        /// </summary>
        public String UserName;
        /// <summary>
        /// The email, may be null
        /// </summary>
        public String Email;
        /// <summary>
        /// The phone number, may be null
        /// </summary>
        public String Phone;
        /// <summary>
        /// The nick name, may be null
        /// </summary>
        public String NickName;
    }


    public abstract partial class AuthorizerBase
    {

        #region Passkeys

        /// <summary>
        /// True if this authorizer can store passkeys for it's users (the passkey methods below are implemented).
        /// The passkey service uses every authorizer that supports passkeys, the default is false.
        /// </summary>
        public virtual bool SupportPasskeys => false;

        /// <summary>
        /// True if this authorizer supports the token based passkey methods (adding a passkey on another device using a QR code,
        /// using a reset / add password link or creating a new user with a passkey), see <see cref="GetPassKeyShareToken"/>.
        /// The default is false.
        /// </summary>
        public virtual bool SupportPasskeyTokens => false;

        static readonly Task<PassKeyData> NoPassKey = Task.FromResult<PassKeyData>(null);
        static readonly Task<IReadOnlyList<PassKeyData>> NoPassKeys = Task.FromResult<IReadOnlyList<PassKeyData>>(null);
        static readonly Task<PassKeyDeleteResults> PassKeyNotFound = Task.FromResult(PassKeyDeleteResults.NotFound);
        static readonly Task<PassKeyTokenResult> PassKeyTokenExpired = Task.FromResult(PassKeyTokenResult.Expired);

        /// <summary>
        /// Get a passkey of any user of this authorizer
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <returns>The passkey (with <see cref="PassKeyData.UserGuid"/> set), or null if it doesn't exist</returns>
        public virtual Task<PassKeyData> GetPassKey(String credentialId) => NoPassKey;

        /// <summary>
        /// Get all passkeys of a user of this authorizer
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <returns>The passkeys of the user (can be empty), null if the user isn't a user of this authorizer (or passkeys aren't supported)</returns>
        public virtual Task<IReadOnlyList<PassKeyData>> GetPassKeys(Authorization user) => NoPassKeys;

        /// <summary>
        /// Get all passkeys of a user (before sign in)
        /// </summary>
        /// <param name="identifier">The user name (or any other identifier that can be used to sign in, ex: email or phone number)</param>
        /// <returns>The passkeys of the user (can be empty), null if the user is unknown to this authorizer (or passkeys aren't supported)</returns>
        public virtual Task<IReadOnlyList<PassKeyData>> FindPassKeys(String identifier) => NoPassKeys;

        /// <summary>
        /// Check if a user have any passkeys
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <returns>True if the user have at least one passkey</returns>
        public virtual Task<bool> HaveAnyPassKeys(Authorization user) => TaskExt.FalseTask;

        /// <summary>
        /// Check if a passkey of a user was created on a device
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <param name="deviceId">The device id (cookie)</param>
        /// <returns>True if the user have a passkey that was created on the device</returns>
        public virtual Task<bool> HaveAssignedPassKey(Authorization user, String deviceId) => TaskExt.FalseTask;

        /// <summary>
        /// Sign in a user using a verified passkey, updates the signature counter, backup state and last use time
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="signCount">The new signature counter</param>
        /// <param name="backedUp">The current backup state</param>
        /// <returns>The user or null if the passkey or user doesn't exist (or the user can't sign in)</returns>
        public virtual Task<Authorization> UsePassKey(String credentialId, long signCount, bool backedUp) => NoAuth;

        /// <summary>
        /// Attach a new (verified) passkey to a signed in user
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <param name="credential">The verified passkey</param>
        /// <returns>True if the passkey was added</returns>
        public virtual Task<bool> AddPassKey(Authorization user, PassKeyData credential) => TaskExt.FalseTask;

        /// <summary>
        /// Rename a passkey of a user
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="name">The new name</param>
        /// <returns>True if the passkey was renamed, false if it doesn't exist (or belongs to some other user)</returns>
        public virtual Task<bool> RenamePassKey(Authorization user, String credentialId, String name) => TaskExt.FalseTask;

        /// <summary>
        /// Remove a passkey of a user, the last sign in method (passkey or password) of a user must not be removed
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <returns>The result</returns>
        public virtual Task<PassKeyDeleteResults> DeletePassKey(Authorization user, String credentialId) => PassKeyNotFound;


        /// <summary>
        /// Create a token that allows adding a passkey to a signed in user on some other device (requires <see cref="SupportPasskeyTokens"/>)
        /// </summary>
        /// <param name="user">The user (an authorization from this authorizer)</param>
        /// <returns>The token, or null if not supported</returns>
        public virtual Task<String> GetPassKeyShareToken(Authorization user) => TaskExt.NullStringTask;

        /// <summary>
        /// The time a token from <see cref="GetPassKeyShareToken"/> is valid
        /// </summary>
        public virtual TimeSpan PassKeyShareTokenLifeTime => TimeSpan.Zero;

        /// <summary>
        /// Get the user that a passkey token (from <see cref="GetPassKeyShareToken"/>, a reset password or an add password token) belongs to, the token is not consumed (requires <see cref="SupportPasskeyTokens"/>).
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="context">The request (the http server request)</param>
        /// <returns>The result</returns>
        public virtual Task<PassKeyTokenResult> GetPassKeyTokenUser(String token, IAuthorizedRequest context) => PassKeyTokenExpired;

        /// <summary>
        /// Attach a verified passkey to the user that a passkey token belongs to and consume the token (requires <see cref="SupportPasskeyTokens"/>).
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="credential">The verified passkey</param>
        /// <param name="context">The request (the http server request)</param>
        /// <returns>The result, with the user (not signed in) on success</returns>
        public virtual Task<PassKeyTokenResult> AddPassKeyUsingToken(String token, PassKeyData credential, IAuthorizedRequest context) => PassKeyTokenExpired;

        /// <summary>
        /// Get information about the user that a sign up token will create (requires <see cref="SupportPasskeyTokens"/>)
        /// </summary>
        /// <param name="token">The sign up token</param>
        /// <param name="context">The request (the http server request)</param>
        /// <returns>The information, null if the token is invalid (or not supported)</returns>
        public virtual Task<PassKeyNewUserInfo> GetPassKeyNewUserInfo(String token, IAuthorizedRequest context) => Task.FromResult<PassKeyNewUserInfo>(null);

        /// <summary>
        /// Create a new user using a sign up token, with a verified passkey as the sign in method, the token is consumed (requires <see cref="SupportPasskeyTokens"/>).
        /// </summary>
        /// <param name="token">The sign up token</param>
        /// <param name="credential">The verified passkey, the user handle should be set</param>
        /// <param name="context">The request (the http server request)</param>
        /// <returns>The result, with the new user (not signed in) on success</returns>
        public virtual Task<PassKeyTokenResult> CreateUserWithPassKey(String token, PassKeyData credential, IAuthorizedRequest context) => PassKeyTokenExpired;

        #endregion//Passkeys

    }

}
