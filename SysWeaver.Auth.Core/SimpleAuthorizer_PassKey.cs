using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.Auth
{

    /// <summary>
    /// Passkey storage of the simple authorizer (the <see cref="AuthorizerBase"/> passkey methods).
    /// The passkeys of all users are stored as one array in <see cref="KeyValueStore.AllApp"/> (few users, so a simple storage is fine).
    /// Enabled by <see cref="SimpleAuthorizerParams.AllowPasskeys"/>, the token based methods (QR code, links, sign up) aren't supported.
    /// </summary>
    public sealed partial class SimpleAuthorizer
    {
        #region Passkeys

        const String PassKeysKey = "SimpleAuthPassKeys";

        /// <summary>
        /// The maximum length of a credential id (standard base64 of the raw id, max 1023 bytes)
        /// </summary>
        const int MaxPassKeyCredentialIdLength = 1368;

        /// <summary>
        /// The maximum number of passkeys per user
        /// </summary>
        const int MaxPassKeysPerUser = 32;

        readonly Object PassKeyLock = new Object();

        /// <summary>
        /// The stored passkeys (loaded on first use), only changed while holding <see cref="PassKeyLock"/>, the array is never modified (copy on write)
        /// </summary>
        PassKeyData[] InternalPassKeys;

        /// <inheritdoc/>
        public override bool SupportPasskeys => Params.AllowPasskeys;

        PassKeyData[] PassKeys
        {
            get
            {
                var p = InternalPassKeys;
                if (p != null)
                    return p;
                lock (PassKeyLock)
                {
                    p = InternalPassKeys;
                    if (p != null)
                        return p;
                    p = KeyValueStore.AllApp.TryGet<PassKeyData[]>(PassKeysKey) ?? [];
                    p = p.Where(x => (x != null) && !String.IsNullOrEmpty(x.CredentialId) && !String.IsNullOrEmpty(x.UserGuid)).ToArray();
                    InternalPassKeys = p;
                    return p;
                }
            }
        }

        /// <summary>
        /// Store a new set of passkeys (call while holding <see cref="PassKeyLock"/>)
        /// </summary>
        void SavePassKeys(PassKeyData[] p)
        {
            KeyValueStore.AllApp.Set(PassKeysKey, p);
            InternalPassKeys = p;
        }

        /// <summary>
        /// Get the current (strong) authorization of a user from it's guid
        /// </summary>
        /// <param name="guid">The user guid</param>
        /// <returns>The authorization or null if the user doesn't exist (anymore)</returns>
        Authorization GetUserAuthFromGuid(String guid)
        {
            if ((guid == null) || !AuthGuids.ContainsKey(guid))
                return null;
            foreach (var x in Auths.Values)
            {
                var a = x.Auth;
                if (String.Equals(a.Guid, guid, StringComparison.Ordinal))
                    return a;
            }
            return null;
        }

        /// <summary>
        /// Get the guid of a user that may use passkeys
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>The guid or null if the user can't use passkeys (not a user of this authorizer, a weak authorization or passkeys are disabled)</returns>
        String GetPassKeyUser(Authorization user)
        {
            if (!Params.AllowPasskeys)
                return null;
            if ((user == null) || (user.Auth != this) || user.WeakMethod)
                return null;
            var g = user.Guid;
            return AuthGuids.ContainsKey(g) ? g : null;
        }

        static readonly Task<IReadOnlyList<PassKeyData>> NullPassKeys = Task.FromResult<IReadOnlyList<PassKeyData>>(null);

        Task<IReadOnlyList<PassKeyData>> GetPassKeysOfGuid(String guid)
            => Task.FromResult<IReadOnlyList<PassKeyData>>(PassKeys.Where(x => String.Equals(x.UserGuid, guid, StringComparison.Ordinal)).Select(x => x.Clone()).ToArray());

        /// <inheritdoc/>
        public override Task<PassKeyData> GetPassKey(String credentialId)
        {
            if ((credentialId == null) || !Params.AllowPasskeys)
                return Task.FromResult<PassKeyData>(null);
            var p = PassKeys.FirstOrDefault(x => String.Equals(x.CredentialId, credentialId, StringComparison.Ordinal));
            //  A passkey of a user that was removed from the configuration is unknown
            if ((p == null) || !AuthGuids.ContainsKey(p.UserGuid))
                return Task.FromResult<PassKeyData>(null);
            return Task.FromResult(p.Clone());
        }

        /// <inheritdoc/>
        public override Task<IReadOnlyList<PassKeyData>> GetPassKeys(Authorization user)
        {
            var g = GetPassKeyUser(user);
            if (g == null)
                return NullPassKeys;
            return GetPassKeysOfGuid(g);
        }

        /// <inheritdoc/>
        public override Task<IReadOnlyList<PassKeyData>> FindPassKeys(String identifier)
        {
            if ((identifier == null) || !Params.AllowPasskeys)
                return NullPassKeys;
            if (!Auths.TryGetValue(identifier.Trim().FastToLower(), out var data))
                return NullPassKeys;
            return GetPassKeysOfGuid(data.Auth.Guid);
        }

        /// <inheritdoc/>
        public override Task<bool> HaveAnyPassKeys(Authorization user)
        {
            var g = GetPassKeyUser(user);
            if (g == null)
                return TaskExt.FalseTask;
            return PassKeys.Any(x => String.Equals(x.UserGuid, g, StringComparison.Ordinal)) ? TaskExt.TrueTask : TaskExt.FalseTask;
        }

        /// <inheritdoc/>
        public override Task<bool> HaveAssignedPassKey(Authorization user, String deviceId)
        {
            var g = GetPassKeyUser(user);
            if ((g == null) || (deviceId == null))
                return TaskExt.FalseTask;
            deviceId = deviceId.LimitLength(64, "");
            return PassKeys.Any(x => String.Equals(x.UserGuid, g, StringComparison.Ordinal) && String.Equals(x.DeviceId, deviceId, StringComparison.Ordinal)) ? TaskExt.TrueTask : TaskExt.FalseTask;
        }

        /// <inheritdoc/>
        public override Task<Authorization> UsePassKey(String credentialId, long signCount, bool backedUp)
        {
            if ((credentialId == null) || !Params.AllowPasskeys)
                return NoAuth;
            lock (PassKeyLock)
            {
                var keys = PassKeys;
                var i = Array.FindIndex(keys, x => String.Equals(x.CredentialId, credentialId, StringComparison.Ordinal));
                if (i < 0)
                    return NoAuth;
                var p = keys[i];
                var auth = GetUserAuthFromGuid(p.UserGuid);
                if (auth == null)
                    return NoAuth;
                p = p.Clone();
                p.LastUsed = DateTime.UtcNow;
                p.SignCount = signCount;
                p.BackedUp = backedUp;
                keys = (PassKeyData[])keys.Clone();
                keys[i] = p;
                SavePassKeys(keys);
                return Task.FromResult(auth);
            }
        }

        /// <inheritdoc/>
        public override Task<bool> AddPassKey(Authorization user, PassKeyData credential)
        {
            var g = GetPassKeyUser(user);
            if ((g == null) || (credential == null))
                return TaskExt.FalseTask;
            var credentialId = credential.CredentialId;
            if (String.IsNullOrEmpty(credentialId) || !credentialId.IsAsciiOnly() || (credentialId.Length > MaxPassKeyCredentialIdLength))
                throw new ArgumentException("Invalid credential id!", nameof(credential));
            var now = DateTime.UtcNow;
            var p = credential.Clone();
            p.UserGuid = g;
            p.Created = now;
            p.LastUsed = now;
            p.DeviceId = p.DeviceId.IsAsciiOnly() ? p.DeviceId.LimitLength(64, "") : null;
            p.RpId = p.RpId.IsAsciiOnly() ? p.RpId.LimitLength(253, "") : null;
            p.Transports = p.Transports.IsAsciiOnly() ? p.Transports.LimitLength(128, "") : null;
            p.AaGuid = p.AaGuid.LimitLength(36, "");
            p.DeviceName = p.DeviceName.LimitLength(64, "");
            lock (PassKeyLock)
            {
                var keys = PassKeys;
                if (keys.Any(x => String.Equals(x.CredentialId, credentialId, StringComparison.Ordinal)))
                    return TaskExt.FalseTask;
                if (keys.Count(x => String.Equals(x.UserGuid, g, StringComparison.Ordinal)) >= MaxPassKeysPerUser)
                    return TaskExt.FalseTask;
                SavePassKeys([.. keys, p]);
            }
            Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}A passkey was added to user {user.Username.ToQuoted()}");
            return TaskExt.TrueTask;
        }

        /// <inheritdoc/>
        public override Task<bool> RenamePassKey(Authorization user, String credentialId, String name)
        {
            var g = GetPassKeyUser(user);
            if ((g == null) || (credentialId == null))
                return TaskExt.FalseTask;
            lock (PassKeyLock)
            {
                var keys = PassKeys;
                var i = Array.FindIndex(keys, x => String.Equals(x.CredentialId, credentialId, StringComparison.Ordinal) && String.Equals(x.UserGuid, g, StringComparison.Ordinal));
                if (i < 0)
                    return TaskExt.FalseTask;
                var p = keys[i].Clone();
                p.DeviceName = name?.Trim().LimitLength(64, "");
                keys = (PassKeyData[])keys.Clone();
                keys[i] = p;
                SavePassKeys(keys);
            }
            return TaskExt.TrueTask;
        }

        static readonly Task<PassKeyDeleteResults> PassKeyDeleted = Task.FromResult(PassKeyDeleteResults.Deleted);
        static readonly Task<PassKeyDeleteResults> PassKeyNotDeleted = Task.FromResult(PassKeyDeleteResults.NotFound);

        /// <inheritdoc/>
        /// <remarks>The users of the simple authorizer always have a password, so any passkey can be removed.</remarks>
        public override Task<PassKeyDeleteResults> DeletePassKey(Authorization user, String credentialId)
        {
            var g = GetPassKeyUser(user);
            if ((g == null) || (credentialId == null))
                return PassKeyNotDeleted;
            lock (PassKeyLock)
            {
                var keys = PassKeys;
                var i = Array.FindIndex(keys, x => String.Equals(x.CredentialId, credentialId, StringComparison.Ordinal) && String.Equals(x.UserGuid, g, StringComparison.Ordinal));
                if (i < 0)
                    return PassKeyNotDeleted;
                SavePassKeys(keys.RemoveAt(i));
            }
            Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}A passkey was removed from user {user.Username.ToQuoted()}");
            return PassKeyDeleted;
        }

        #endregion//Passkeys
    }

}
