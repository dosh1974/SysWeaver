using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Net;
using SimpleStack.Orm;
using SysWeaver.MicroService.Db;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A passkey (public key credential) attached to a user
    /// </summary>
    public sealed class PassKeyCredential
    {
        /// <summary>
        /// The credential id (standard base64 of the raw id)
        /// </summary>
        public String CredentialId;

        /// <summary>
        /// The COSE encoded public key
        /// </summary>
        public Byte[] PublicKey;

        /// <summary>
        /// The user handle (user.id) the credential was created with, null if unknown (created before this was stored, the handle is then the user guid)
        /// </summary>
        public Byte[] UserHandle;

        /// <summary>
        /// The id of the user that owns this credential
        /// </summary>
        public long UserId;

        /// <summary>
        /// The relying party id the credential was created for, null if unknown
        /// </summary>
        public String RpId;

        /// <summary>
        /// The last seen signature counter
        /// </summary>
        public long SignCount;

        /// <summary>
        /// Comma separated transports (ex: "internal,hybrid"), null if unknown
        /// </summary>
        public String Transports;

        /// <summary>
        /// True if the credential may be backed up (synced)
        /// </summary>
        public bool BackupEligible;

        /// <summary>
        /// True if the credential is backed up (synced)
        /// </summary>
        public bool BackedUp;

        /// <summary>
        /// The authenticator attestation guid, null if unknown
        /// </summary>
        public String AaGuid;

        /// <summary>
        /// The device id (cookie) of the device that created the credential
        /// </summary>
        public String DeviceId;

        /// <summary>
        /// A user friendly name of the credential
        /// </summary>
        public String DeviceName;

        /// <summary>
        /// When the credential was created (UTC)
        /// </summary>
        public DateTime Created;

        /// <summary>
        /// When the credential was last used to sign in (UTC)
        /// </summary>
        public DateTime LastUsed;
    }

    /// <summary>
    /// Thrown when trying to remove the last way a user can sign in
    /// </summary>
    public sealed class LastSignInMethodException : Exception
    {
        public LastSignInMethodException() : base("The last sign in method of a user can't be removed!")
        {
        }
    }


    public sealed partial class UserManagerService
    {
        #region Passkey


        /// <summary>
        /// The action type of tokens that allow adding a passkey to a user (see <see cref="GetShareDeviceToken(HttpServerRequest)"/>)
        /// </summary>
        public const String ActionTokenGetResetChallenge = "AddKey";

        /// <summary>
        /// The action types of tokens that can be used to add a passkey to an existing user (and sign in)
        /// </summary>
        static readonly String[] PassKeyTokenTypes = [ActionTokenGetResetChallenge, DbAction.ResetPassword, DbAction.AddPassword];

        /// <summary>
        /// The maximum length of a credential id (standard base64 of the raw id, max 1023 bytes)
        /// </summary>
        public const int MaxPassKeyCredentialIdLength = 1368;

        /// <summary>
        /// Create a token that allows adding a passkey to the signed in user (on some other device)
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The token or null if no user is signed in</returns>
        public async Task<String> GetShareDeviceToken(HttpServerRequest context)
        {
            var auth = context.Session.Auth?.AuthContext as DbUser;
            if (auth == null)
                return null;
            var id = auth.Id;
            return (await AddAction(new InternalNewPasswordData
            {
                UserId = id,
            }, ActionTokenGetResetChallenge, DateTime.UtcNow + SharePasswordTimeout).ConfigureAwait(false)).Item1;
        }

        /// <summary>
        /// The time a share device token (see <see cref="GetShareDeviceToken(HttpServerRequest)"/>) is valid
        /// </summary>
        public TimeSpan ShareDeviceTokenLifeTime => SharePasswordTimeout;

        static PassKeyCredential ToCredential(DbAuthPassKey p)
            => p == null ? null : new PassKeyCredential
            {
                CredentialId = p.CredentialId,
                PublicKey = p.PublicKey,
                UserHandle = p.UserHandle,
                UserId = p.UserId,
                RpId = p.RpId,
                SignCount = p.SignCount,
                Transports = p.Transports,
                BackupEligible = p.BackupEligible,
                BackedUp = p.BackedUp,
                AaGuid = p.AaGuid,
                DeviceId = p.DeviceId,
                DeviceName = p.DeviceName,
                Created = p.Created,
                LastUsed = p.LastUsed,
            };

        static DbAuthPassKey ToDb(PassKeyCredential p, long userId, DateTime now)
        {
            var credentialId = p.CredentialId;
            if (!credentialId.IsAsciiOnly())
                throw new ArgumentException("Credential id may only contain ascii chars!", nameof(p));
            if (credentialId.Length > MaxPassKeyCredentialIdLength)
                throw new ArgumentException("Credential id may not be longer than " + MaxPassKeyCredentialIdLength + " chars!", nameof(p));
            var deviceId = p.DeviceId;
            if (!deviceId.IsAsciiOnly())
                deviceId = null;
            var rpId = p.RpId;
            if (!rpId.IsAsciiOnly())
                rpId = null;
            var transports = p.Transports;
            if (!transports.IsAsciiOnly())
                transports = null;
            return new DbAuthPassKey
            {
                CredentialId = credentialId,
                PublicKey = p.PublicKey,
                UserHandle = p.UserHandle,
                RpId = rpId.LimitLength(253, ""),
                SignCount = p.SignCount,
                Transports = transports.LimitLength(128, ""),
                BackupEligible = p.BackupEligible,
                BackedUp = p.BackedUp,
                AaGuid = p.AaGuid.LimitLength(36, ""),
                DeviceName = p.DeviceName.LimitLength(64, ""),
                DeviceId = deviceId.LimitLength(64, ""),
                UserId = userId,
                Created = now,
                LastUsed = now,
            };
        }

        /// <summary>
        /// Get a passkey
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <returns>The passkey or null if it doesn't exist</returns>
        public async Task<PassKeyCredential> GetPassKey(String credentialId)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return ToCredential(await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.CredentialId == credentialId).ConfigureAwait(false));
        }

        /// <summary>
        /// Get all passkeys of a user
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <returns>The passkeys of the user (can be empty)</returns>
        public async Task<List<PassKeyCredential>> GetPassKeys(long userId)
        {
            if (userId == 0)
                return [];
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == userId).ConfigureAwait(false)).Select(ToCredential).ToList();
        }

        /// <summary>
        /// Get all passkeys of a user
        /// </summary>
        /// <param name="auth">The user</param>
        /// <returns>The passkeys of the user (can be empty)</returns>
        public Task<List<PassKeyCredential>> GetPassKeys(Authorization auth)
            => GetPassKeys(GetUid(auth));

        /// <summary>
        /// Get all passkeys of a user
        /// </summary>
        /// <param name="identifier">User name, email or phone number</param>
        /// <returns>The passkeys of the user, null if the user wasn't found</returns>
        public async Task<List<PassKeyCredential>> GetPassKeys(String identifier)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var uid = await FindUser(c, identifier).ConfigureAwait(false);
            if (uid == 0)
                return null;
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false)).Select(ToCredential).ToList();
        }

        /// <summary>
        /// Get the internal user id of a user
        /// </summary>
        /// <param name="auth">The user</param>
        /// <returns>The user id or 0 if it isn't a user of this user manager</returns>
        public long GetUid(Authorization auth)
            => (auth?.AuthContext as DbUser)?.Id ?? 0;

        /// <summary>
        /// Check if a passkey of a user was created on a device
        /// </summary>
        /// <param name="deviceId">The device id (cookie)</param>
        /// <param name="uid">The user id</param>
        /// <returns>True if the user have a passkey that was created on the device</returns>
        public async Task<Boolean> HaveAssignedPassKey(String deviceId, long uid)
        {
            if (uid == 0)
                return false;
            deviceId = deviceId.LimitLength(64, "");
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.DeviceId == deviceId) && (x.UserId == uid)).ConfigureAwait(false)) != null;
        }

        /// <summary>
        /// Sign in a user using a verified passkey, updates the signature counter, backup state and last use time
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="signCount">The new signature counter</param>
        /// <param name="backedUp">The current backup state</param>
        /// <returns>The user or null if the passkey or user doesn't exist</returns>
        public async Task<Authorization> UsePassKey(String credentialId, long signCount, bool backedUp)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.CredentialId == credentialId).ConfigureAwait(false);
            if (p == null)
                return null;
            var a = await InternalAuthUser(c, p.UserId).ConfigureAwait(false);
            if (a == null)
                return null;
            p.LastUsed = DateTime.UtcNow;
            p.SignCount = signCount;
            p.BackedUp = backedUp;
            await c.UpdateAsync(p, x => new { x.LastUsed, x.SignCount, x.BackedUp }).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return a;
        }

        /// <summary>
        /// Attach a new passkey to the signed in user
        /// </summary>
        /// <param name="credential">The verified passkey</param>
        /// <param name="context">The request context</param>
        /// <returns>True if the passkey was added</returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        public async Task<bool> AttachPassKey(PassKeyCredential credential, HttpServerRequest context)
        {
            var user = context.Session.Auth?.AuthContext as DbUser;
            if (user == null)
                throw new NoUserLoggedInException();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            await c.InsertAsync(ToDb(credential, user.Id, DateTime.UtcNow)).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Get the user that a passkey token (from <see cref="GetShareDeviceToken(HttpServerRequest)"/>, a reset password or an add password token) belongs to, the token is not consumed.
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="context">The request context</param>
        /// <returns>The user</returns>
        /// <exception cref="TokenExpiredException">Thrown if the token is invalid or has expired</exception>
        public async Task<Authorization> GetPassKeyTokenUser(String token, HttpServerRequest context)
        {
            var id = (await GetPassKeyTokenAction(token, context).ConfigureAwait(false)).Item2.UserId;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await InternalAuthUser(c, id).ConfigureAwait(false)) ?? throw new UserNoLongerExistException(id);
        }

        /// <summary>
        /// Attach a verified passkey to the user that a passkey token (from <see cref="GetShareDeviceToken(HttpServerRequest)"/>, a reset password or an add password token) belongs to and consume the token.
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="credential">The verified passkey</param>
        /// <param name="context">The request context</param>
        /// <returns>The user (not signed in)</returns>
        /// <exception cref="TokenExpiredException">Thrown if the token is invalid or has expired</exception>
        public async Task<Authorization> AttachPassKeyUsingToken(String token, PassKeyCredential credential, HttpServerRequest context)
        {
            var action = await GetPassKeyTokenAction(token, context).ConfigureAwait(false);
            var id = action.Item2.UserId;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var auth = (await InternalAuthUser(c, id).ConfigureAwait(false)) ?? throw new UserNoLongerExistException(id);
            await c.InsertAsync(ToDb(credential, id, DateTime.UtcNow)).ConfigureAwait(false);
            await DeleteAction(c, action.Item1, action.Item3, context).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return auth;
        }

        async Task<Tuple<String, InternalNewPasswordData, String>> GetPassKeyTokenAction(String token, HttpServerRequest context)
        {
            foreach (var type in PassKeyTokenTypes)
            {
                var d = await GetAction<InternalNewPasswordData>(token, type, context).ConfigureAwait(false);
                if ((d != null) && ((d.Item2?.UserId ?? 0) != 0))
                    return Tuple.Create(d.Item1, d.Item2, type);
            }
            throw new TokenExpiredException();
        }

        /// <summary>
        /// Create a new user using a create user token, with a verified passkey as the sign in method (the token is consumed)
        /// </summary>
        /// <param name="token">The create user token</param>
        /// <param name="credential">The verified passkey, the user handle should be set</param>
        /// <param name="context">The request context</param>
        /// <returns>The new user (not signed in)</returns>
        public Task<Authorization> CreateUserWithPassKey(String token, PassKeyCredential credential, HttpServerRequest context)
            => CreateUser(new AddUserRequest
            {
                Token = token,
            }, context, false, async (c, id) => await c.InsertAsync(ToDb(credential, id, DateTime.UtcNow)).ConfigureAwait(false));

        /// <summary>
        /// Rename a passkey of the signed in user
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="name">The new name</param>
        /// <param name="context">The request context</param>
        /// <returns>True if the passkey was renamed, false if it doesn't exist (or belongs to some other user)</returns>
        public async Task<bool> RenamePassKey(String credentialId, String name, HttpServerRequest context)
        {
            var uid = GetUid(context.Session.Auth);
            if (uid == 0)
                throw new NoUserLoggedInException();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            if (p == null)
                return false;
            p.DeviceName = name?.Trim().LimitLength(64, "");
            await c.UpdateAsync(p, x => new { x.DeviceName }).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Remove a passkey from the signed in user, the last sign in method (passkey or password) of a user can't be removed
        /// </summary>
        /// <param name="credentialId">The credential id (standard base64 of the raw id)</param>
        /// <param name="context">The request context</param>
        /// <returns>True if the passkey was removed, false if it doesn't exist (or belongs to some other user)</returns>
        /// <exception cref="LastSignInMethodException">Thrown if this is the last way the user can sign in</exception>
        public async Task<bool> DeletePassKey(String credentialId, HttpServerRequest context)
        {
            var uid = GetUid(context.Session.Auth);
            if (uid == 0)
                throw new NoUserLoggedInException();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            if (p == null)
                return false;
            var keyCount = await c.CountAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false);
            if (keyCount <= 1)
            {
                var pwdCount = await c.CountAsync<DbAuthPassword>(x => x.UserId == uid).ConfigureAwait(false);
                if (pwdCount <= 0)
                    throw new LastSignInMethodException();
            }
            await c.DeleteAllAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return true;
        }


        #endregion//Passkey


    }
}
