using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleStack.Orm;
using SysWeaver.Auth;
using SysWeaver.Db;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Support for a user database that is shared by several services (ex: the Hypnos client, system and management servers):
    /// a database lock around the start up migration, and a data protection secret that can be read from a (shared) key file.
    /// </summary>
    public sealed partial class UserManagerService
    {
        /// <summary>
        /// A MySQL named lock (held by its own connection, released on dispose or when the connection closes)
        /// </summary>
        sealed class MigrationLock : IDisposable
        {
            public MigrationLock(OrmConnection c, String name)
            {
                C = c;
                Name = name;
            }
            OrmConnection C;
            readonly String Name;

            public async Task ReleaseAsync()
            {
                var c = C;
                if (c == null)
                    return;
                C = null;
                try
                {
                    using var r = await c.ReaderCommandAsync("SELECT RELEASE_LOCK('" + Name + "')").ConfigureAwait(false);
                }
                catch
                {
                }
                c.Dispose();
            }

            public void Dispose()
            {
                var c = C;
                C = null;
                //  Closing the connection releases the lock
                c?.Dispose();
            }
        }

        /// <summary>
        /// Wait for the migration lock of the user database (at most 15 minutes, a running migration of a large database may take a while)
        /// </summary>
        /// <param name="db">The database</param>
        /// <returns>The lock, dispose (or release) it when done</returns>
        /// <exception cref="Exception">The lock couldn't be acquired</exception>
        async Task<MigrationLock> AcquireMigrationLock(MySqlDbSimpleStack db)
        {
            var schema = db.P?.Schema ?? "";
            var name = "SwUM." + schema;
            //  MySQL lock names are at most 64 chars, and only safe chars are used in the statement
            if ((name.Length > 64) || !IsSafeLockName(name))
                name = "SwUM." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema)), 0, 16);
            var c = await db.GetAsync().ConfigureAwait(false);
            try
            {
                for (int i = 0; ; ++i)
                {
                    using (var r = await c.ReaderCommandAsync("SELECT GET_LOCK('" + name + "', 60)").ConfigureAwait(false))
                    {
                        if (await r.ReadAsync().ConfigureAwait(false) && !r.IsDBNull(0) && (Convert.ToInt64(r.GetValue(0)) == 1))
                            return new MigrationLock(c, name);
                    }
                    if (i >= 14)
                        throw new Exception("Timed out waiting for the migration lock of the user database (another service is migrating it?)");
                    if (i == 0)
                        Manager.AddMessage("Waiting for another service to finish the start up / migration of the shared user database", MessageLevels.Info);
                }
            }
            catch
            {
                c.Dispose();
                throw;
            }
        }

        static bool IsSafeLockName(String s)
        {
            foreach (var ch in s)
                if (!(Char.IsAsciiLetterOrDigit(ch) || (ch == '.') || (ch == '_') || (ch == '-')))
                    return false;
            return true;
        }

        /// <summary>
        /// Get the data protection secret that this service uses for the user database, ex: to set <see cref="UserManagerParams.DataProtectionSecret"/>
        /// in another service that shares the database, or to keep a backup (without it the user names, e-mail addresses and phone numbers can't be read).
        /// IMPORTANT: anyone with the secret and a copy of the database can read the personal values, every call is audited.
        /// </summary>
        /// <returns>The secret, its fingerprint and where it's from</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public DataProtectionSecretInfo GetDataProtectionSecret()
        {
            var dp = Volatile.Read(ref Pii) ?? throw new HttpResponseException(503, "The user database isn't ready (or can't be used), the data protection secret isn't loaded");
            var p = Params;
            String source;
            if (!String.IsNullOrEmpty(p.DataProtectionSecret))
                source = nameof(UserManagerParams.DataProtectionSecret) + " (configuration)";
            else if (!String.IsNullOrEmpty(p.DataProtectionSecretFile))
                source = String.Concat(nameof(UserManagerParams.DataProtectionSecretFile), " ", PathTemplate.Resolve(p.DataProtectionSecretFile).ToQuoted());
            else
                source = String.Concat("The key value store of the application on this computer (", DataProtection.SecretKey, ")");
            Manager.AddMessage(MessageLevels.Warning, $"The data protection secret (fingerprint {dp.Fingerprint}) was read using the API");
            return new DataProtectionSecretInfo
            {
                Secret = dp.ToBase64(),
                Fingerprint = dp.Fingerprint,
                Source = source,
            };
        }

        /// <summary>
        /// Get the data protection secret: <see cref="UserManagerParams.DataProtectionSecret"/>, else <see cref="UserManagerParams.DataProtectionSecretFile"/>
        /// (created with a new random secret if it doesn't exist), else <see cref="DataProtection.Default"/>.
        /// </summary>
        /// <returns>The data protection</returns>
        DataProtection GetDataProtection()
        {
            var p = Params;
            if (!String.IsNullOrEmpty(p.DataProtectionSecret))
                return DataProtection.FromBase64(p.DataProtectionSecret);
            if (String.IsNullOrEmpty(p.DataProtectionSecretFile))
                return DataProtection.Default;
            var file = PathTemplate.Resolve(p.DataProtectionSecretFile);
            if (!File.Exists(file))
            {
                if (p.ReadOnly)
                    throw new Exception("The data protection secret file " + file.ToQuoted() + " doesn't exist");
                var dir = Path.GetDirectoryName(file);
                if (!String.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                //  Written to a temp file and moved in place (no overwrite): another service never reads a partial file, and never overwrites a secret that another service just created
                var temp = String.Concat(file, ".", Guid.NewGuid().ToString("N"), ".tmp");
                try
                {
                    File.WriteAllText(temp, Convert.ToBase64String(RandomNumberGenerator.GetBytes(DataProtection.SecretSize)), Encoding.ASCII);
                    File.Move(temp, file, false);
                    Manager.AddMessage(MessageLevels.Warning, $"Created a new data protection secret in {file.ToQuoted()}: back it up, without it the user names, e-mail addresses and phone numbers can't be read. Every service that shares the user database must use this file (or the same secret)");
                }
                catch (IOException) when (File.Exists(file))
                {
                    //  Another service created it first
                }
                finally
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                    }
                }
            }
            return DataProtection.FromBase64(File.ReadAllText(file).Trim());
        }
    }

    /// <summary>
    /// The data protection secret of the user database (see <see cref="UserManagerService.GetDataProtectionSecret"/>)
    /// </summary>
    public sealed class DataProtectionSecretInfo
    {
        /// <summary>
        /// The secret (base64), use it as <see cref="UserManagerParams.DataProtectionSecret"/> (or as the content of a <see cref="UserManagerParams.DataProtectionSecretFile"/>)
        /// </summary>
        public String Secret;

        /// <summary>
        /// The fingerprint of the secret (not secret), the same as the one stored in the user database
        /// </summary>
        public String Fingerprint;

        /// <summary>
        /// Where the secret is from
        /// </summary>
        public String Source;
    }
}
