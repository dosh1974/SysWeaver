using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Db;
using SysWeaver.Net;
using SimpleStack.Orm;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SimpleStack.Orm.Expressions.Statements.Typed;
using SysWeaver.IsoData;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {
        #region Passkey


        public const String ActionTokenGetResetChallenge = "AddKey";

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
        /// Get a list of passkey id's for the current user
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<List<String>> GetPublicKeyIdsForUser(HttpServerRequest context)
        {
            var auth = context.Session.Auth?.AuthContext as DbUser;
            if (auth == null)
                return null;
            var uid = auth.Id;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false)).Select(x => x.CredentialId).ToList();
        }


        /// <summary>
        /// Get a list of passkey id's for a specific user
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<List<String>> GetPublicKeyIdsForUser(String identifier)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var uid = await FindUser(c, identifier).ConfigureAwait(false);
            if (uid == 0)
                return null;
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false)).Select(x => x.CredentialId).ToList();
        }


        /// <summary>
        /// Get a list of passkey id's for a specific user
        /// </summary>
        /// <param name="auth"></param>
        /// <returns></returns>
        public async Task<List<String>> GetPublicKeyIdsForUser(Authorization auth)
        {
            var uid = (auth?.AuthContext as DbUser)?.Id ?? 0;
            if (uid == 0)
                return null;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.SelectAsync<DbAuthPassKey>(x => x.UserId == uid).ConfigureAwait(false)).Select(x => x.CredentialId).ToList();
        }


        /// <summary>
        /// Get a list of passkey id's for a given device
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task<bool> HaveAnyPasskeysForDevice(String deviceId)
        {
            if (deviceId == null)
                return false;
            deviceId = deviceId.LimitLength(64, null);
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.DeviceId == deviceId).ConfigureAwait(false)) != null;
        }

        /// <summary>
        /// Get a list of passkey id's for a given device
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task<List<String>> GetPublicKeyIdsForDevice(String deviceId)
        {
            if (deviceId == null)
                return null;
            deviceId = deviceId.LimitLength(64, null);
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.SelectAsync<DbAuthPassKey>(x => x.DeviceId == deviceId).ConfigureAwait(false)).Select(x => x.CredentialId).ToList();
        }

        /// <summary>
        /// Get the Public key, too validate a user request using passkey or similar
        /// </summary>
        /// <param name="credentialId"></param>
        /// <returns></returns>
        public async Task<Tuple<Byte[], long>> GetPassKeyPublicKey(String credentialId)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.CredentialId == credentialId).ConfigureAwait(false);
            if (p == null)
                return null;
            return Tuple.Create(p.PublicKey, p.UserId);
        }


        public long GetUid(Authorization auth)
            => (auth?.AuthContext as DbUser)?.Id ?? 0;

        public async Task<Boolean> HaveAssignedPassKey(String deviceId, long uid)
        {
            if (uid == 0)
                return false;
            deviceId = deviceId.LimitLength(64, null);
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassKey>(x => (x.DeviceId == deviceId) && (x.UserId == uid)).ConfigureAwait(false)) != null;
        }


        /// <summary>
        /// Attach a new public key auth (such as passkey), to the current users account
        /// </summary>
        /// <param name="credentialId"></param>
        /// <param name="deviceName"></param>
        /// <param name="deviceId"></param>
        /// <param name="publicKey"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> AttachPublicKeyAuth(String credentialId, String deviceName, String deviceId, Byte[] publicKey, HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser;
            if (user == null)
                throw new NoUserLoggedInException();
            if (!credentialId.IsAsciiOnly())
                throw new ArgumentException("Credential id may only contain ascii chars!", nameof(credentialId));
            if (credentialId.Length > 1368)
                throw new ArgumentException("Credential id may not be lopnger 1368 chars!", nameof(credentialId));
            if (!deviceId.IsAsciiOnly())
                deviceId = null;
            var now = DateTime.UtcNow;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            await c.InsertAsync(new DbAuthPassKey
            {
                CredentialId = credentialId,
                DeviceName = deviceName.LimitLength(64, null),
                DeviceId = deviceId.LimitLength(64, null),
                PublicKey = publicKey,
                UserId = user.Id,
                Created = now,
                LastUsed = now,
            }).ConfigureAwait(false);
            return true;
        }


        #endregion//Passkey


    }
}
