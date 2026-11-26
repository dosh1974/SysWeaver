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
    /// Passkey storage (the <see cref="AuthorizerBase"/> passkey methods), the passkeys are stored in the database (<see cref="DbAuthPassKey"/>).
    /// </summary>
    public sealed partial class UserManagerService
    {
        #region Passkey


        /// <summary>
        /// The action type of tokens that allow adding a passkey to a user (see <see cref="GetPassKeyShareToken(Authorization)"/>)
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

        /// <inheritdoc/>
        public override bool SupportPasskeys => true;

        /// <inheritdoc/>
        public override bool SupportPasskeyTokens => true;

        /// <summary>
        /// Get the internal user id of a user
        /// </summary>
        /// <param name="auth">The user</param>
        /// <returns>The user id or 0 if it isn't a user of this user manager</returns>
        public long GetUid(Authorization auth)
            => (auth?.AuthContext as DbUser)?.Id ?? 0;

        /// <summary>
        /// Get the internal user id of a user of this instance
        /// </summary>
        /// <param name="auth">The user</param>
        /// <returns>The user id or 0 if it isn't a user of this user manager instance</returns>
        long GetOwnUid(Authorization auth)
            => (auth?.Auth == this) ? GetUid(auth) : 0;

        /// <inheritdoc/>
        public override async Task<String> GetPassKeyShareToken(Authorization user)
        {
            var id = GetOwnUid(user);
            if (id == 0)
                return null;
            return (await AddAction(new InternalNewPasswordData
            {
                UserId = id,
            }, ActionTokenGetResetChallenge, UserSubject(id), DateTime.UtcNow + SharePasswordTimeout).ConfigureAwait(false)).Item1;
        }

        /// <inheritdoc/>
        public override TimeSpan PassKeyShareTokenLifeTime => SharePasswordTimeout;

        PassKeyData ToData(DbAuthPassKey p)
            => p == null ? null : new PassKeyData
            {
                CredentialId = p.CredentialId,
                PublicKey = p.PublicKey,
                UserHandle = p.UserHandle,
                UserGuid = MakeGuid(p.UserId),
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

        static DbAuthPassKey ToDb(PassKeyData p, long userId, DateTime now)
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

        /// <inheritdoc/>
        public override async Task<PassKeyData> GetPassKey(String credentialId)
        {
            if (credentialId == null)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            return ToData(await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.CredentialId == credentialId).ConfigureAwait(false));
        }

        async Task<IReadOnlyList<PassKeyData>> GetPassKeys(long userId)
        {
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == userId).ConfigureAwait(false)).Select(ToData).ToList();
        }

        /// <inheritdoc/>
        public override Task<IReadOnlyList<PassKeyData>> GetPassKeys(Authorization user)
        {
            var uid = GetOwnUid(user);
            if (uid == 0)
                return Task.FromResult<IReadOnlyList<PassKeyData>>(null);
            return GetPassKeys(uid);
        }

        /// <inheritdoc/>
        public override async Task<bool> HaveAnyPassKeys(Authorization user)
        {
            var userId = GetOwnUid(user);
            if (userId == 0)
                return false;
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.UserId == userId).ConfigureAwait(false)) != null;
        }

        /// <summary>
        /// Get all passkeys of a user
        /// </summary>
        /// <param name="identifier">User name, email or phone number</param>
        /// <returns>The passkeys of the user, null if the user wasn't found</returns>
        public override async Task<IReadOnlyList<PassKeyData>> FindPassKeys(String identifier)
        {
            if (LoginUnavailable)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            var uid = await FindUser(c, identifier).ConfigureAwait(false);
            if (uid == 0)
                return null;
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false)).Select(ToData).ToList();
        }

        /// <inheritdoc/>
        public override async Task<bool> HaveAssignedPassKey(Authorization user, String deviceId)
        {
            var uid = GetOwnUid(user);
            if (uid == 0)
                return false;
            deviceId = deviceId.LimitLength(64, "");
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.DeviceId == deviceId) && (x.UserId == uid)).ConfigureAwait(false)) != null;
        }

        /// <inheritdoc/>
        public override async Task<Authorization> UsePassKey(String credentialId, long signCount, bool backedUp)
        {
            if (LoginUnavailable)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
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

        /// <inheritdoc/>
        public override async Task<bool> AddPassKey(Authorization user, PassKeyData credential)
        {
            var uid = GetOwnUid(user);
            if (uid == 0)
                throw new NoUserLoggedInException();
            using var c = await DbGetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            await c.InsertAsync(ToDb(credential, uid, DateTime.UtcNow)).ConfigureAwait(false);
            //  A credential was added: the links whose meaning depends on the old state stop working (see ActionTokenGroups)
            await RevokeOnEvent(c, uid, AccountEvents.PassKeyAdded).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Map the exceptions of the token methods to a result
        /// </summary>
        static async Task<PassKeyTokenResult> TokenResult(Func<Task<Authorization>> f)
        {
            try
            {
                return PassKeyTokenResult.Ok(await f().ConfigureAwait(false));
            }
            catch (TokenExpiredException)
            {
                return PassKeyTokenResult.Expired;
            }
            catch (UserNoLongerExistException)
            {
                return PassKeyTokenResult.Fail(PassKeyTokenErrors.UserNoLongerExist);
            }
            catch (UserDisabledException)
            {
                return PassKeyTokenResult.Fail(PassKeyTokenErrors.UserNoLongerExist);
            }
            catch (UserAlreadyExistException)
            {
                return PassKeyTokenResult.Fail(PassKeyTokenErrors.UserAlreadyExist);
            }
        }

        /// <inheritdoc/>
        public override Task<PassKeyTokenResult> GetPassKeyTokenUser(String token, IAuthorizedRequest context)
            => TokenResult(async () =>
            {
                var id = (await GetPassKeyTokenAction(token, (HttpServerRequest)context).ConfigureAwait(false)).Item2.UserId;
                using var c = await DbGetAsync().ConfigureAwait(false);
                return (await InternalAuthUser(c, id).ConfigureAwait(false)) ?? throw await NoAuthException(c, id).ConfigureAwait(false);
            });

        /// <inheritdoc/>
        public override Task<PassKeyTokenResult> AddPassKeyUsingToken(String token, PassKeyData credential, IAuthorizedRequest context)
            => TokenResult(async () =>
            {
                var hc = (HttpServerRequest)context;
                var action = await GetPassKeyTokenAction(token, hc).ConfigureAwait(false);
                var id = action.Item2.UserId;
                using var c = await DbGetAsync().ConfigureAwait(false);
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var auth = (await InternalAuthUser(c, id).ConfigureAwait(false)) ?? throw await NoAuthException(c, id).ConfigureAwait(false);
                await c.InsertAsync(ToDb(credential, id, DateTime.UtcNow)).ConfigureAwait(false);
                await DeleteAction(c, action.Item1, action.Item3, hc).ConfigureAwait(false);
                //  A credential was added with a link: the other links of the user stop working (see ActionTokenGroups)
                await RevokeOnEvent(c, id, AccountEvents.PassKeyAdded).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
                return auth;
            });

        async Task<Tuple<String, InternalNewPasswordData, String>> GetPassKeyTokenAction(String token, HttpServerRequest context)
        {
            foreach (var type in PassKeyTokenTypes)
            {
                var d = await GetAction<InternalNewPasswordData>(token, type, context).ConfigureAwait(false);
                if ((d != null) && ((d.Item2?.UserId ?? 0) != 0))
                    return Tuple.Create(d.Item1, d.Item2, type);
            }
            throw Refused(token);
        }

        /// <inheritdoc/>
        public override async Task<PassKeyNewUserInfo> GetPassKeyNewUserInfo(String token, IAuthorizedRequest context)
        {
            var data = await GetNewUserData(token, (HttpServerRequest)context).ConfigureAwait(false);
            if (data == null)
                return null;
            return new PassKeyNewUserInfo
            {
                UserName = data.UserName,
                Email = data.Email,
                Phone = data.Phone,
                NickName = data.NickName,
            };
        }

        /// <inheritdoc/>
        public override Task<PassKeyTokenResult> CreateUserWithPassKey(String token, PassKeyData credential, IAuthorizedRequest context)
            => TokenResult(() => CreateUser(new AddUserRequest
            {
                Token = token,
            }, (HttpServerRequest)context, false, async (c, id) => await c.InsertAsync(ToDb(credential, id, DateTime.UtcNow)).ConfigureAwait(false)));

        /// <inheritdoc/>
        public override async Task<bool> RenamePassKey(Authorization user, String credentialId, String name)
        {
            var uid = GetOwnUid(user);
            if (uid == 0)
                throw new NoUserLoggedInException();
            using var c = await DbGetAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            if (p == null)
                return false;
            p.DeviceName = name?.Trim().LimitLength(64, "");
            await c.UpdateAsync(p, x => new { x.DeviceName }).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public override async Task<PassKeyDeleteResults> DeletePassKey(Authorization user, String credentialId)
        {
            var uid = GetOwnUid(user);
            if (uid == 0)
                throw new NoUserLoggedInException();
            using var c = await DbGetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            if (p == null)
                return PassKeyDeleteResults.NotFound;
            var keyCount = await c.CountAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false);
            if (keyCount <= 1)
            {
                //  The placeholder row of GetSaltAsync (no hash) is no password
                var hasPwd = IsUsablePassword(await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == uid).ConfigureAwait(false));
                if (!hasPwd)
                    return PassKeyDeleteResults.LastSignInMethod;
            }
            await c.DeleteAllAsync<DbAuthPassKey>(x => (x.CredentialId == credentialId) && (x.UserId == uid)).ConfigureAwait(false);
            //  A credential was removed: the links whose meaning depends on the old state stop working (see ActionTokenGroups)
            await RevokeOnEvent(c, uid, AccountEvents.PassKeyRemoved).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return PassKeyDeleteResults.Deleted;
        }


        #endregion//Passkey


    }
}
