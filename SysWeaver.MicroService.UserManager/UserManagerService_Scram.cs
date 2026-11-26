using System;
using System.Threading.Tasks;
using SimpleStack.Orm;
using SysWeaver.Auth;
using SysWeaver.Db;
using SysWeaver.MicroService.Db;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Password storage: passwords are stored as SCRAM verifiers (see <see cref="ScramInfo"/>), a stolen database can't be used to sign in.
    /// The PBKDF2 input is the hash H = ToBase64(SHA256(UTF8(password|Salt))) (what the clients send when a password is set),
    /// followed by "|" and the application binding (<see cref="UserManagerParams.PasswordHashString"/>). H is never stored.
    /// </summary>
    public sealed partial class UserManagerService
    {
        /// <summary>
        /// The application binding of the password verifiers (<see cref="UserManagerParams.PasswordHashString"/>, defaults to the entry assembly name)
        /// </summary>
        public String PasswordBinding => String.IsNullOrEmpty(Params.PasswordHashString) ? EnvInfo.AppAssemblyName : Params.PasswordHashString;

        /// <summary>
        /// The iteration count of new verifiers
        /// </summary>
        int NewIterations => ScramTools.ClampIterations(Params.ScramIterations);

        /// <inheritdoc/>
        public override ScramInfo ScramDefaults => DisableLogin ? null : new ScramInfo
        {
            Iterations = NewIterations,
            Binding = PasswordBinding,
        };

        /// <summary>
        /// Get the verifier of a stored password
        /// </summary>
        /// <param name="a">The stored password, may be null</param>
        /// <returns>The verifier or null if the password isn't stored as a verifier</returns>
        static ScramVerifier GetVerifier(DbAuthPassword a)
        {
            if ((a == null) || String.IsNullOrEmpty(a.StoredKey) || String.IsNullOrEmpty(a.ScramSalt))
                return null;
            try
            {
                var sk = Convert.FromBase64String(a.StoredKey);
                if (sk.Length != ScramTools.KeySize)
                    return null;
                return new ScramVerifier
                {
                    Salt = Convert.FromBase64String(a.ScramSalt),
                    Iterations = ScramTools.ClampIterations(a.ScramIterations),
                    StoredKey = sk,
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Set the password of a row (doesn't update the database), H is never stored.
        /// </summary>
        /// <param name="a">The row</param>
        /// <param name="hash">The hash H as sent by the client (null or empty for "no password")</param>
        void SetPasswordHash(DbAuthPassword a, String hash)
        {
            if (String.IsNullOrEmpty(hash))
            {
                a.ScramSalt = null;
                a.StoredKey = null;
                a.ScramIterations = 0;
                return;
            }
            var v = ScramTools.CreateVerifier(hash, PasswordBinding, NewIterations);
            a.ScramSalt = Convert.ToBase64String(v.Salt);
            a.ScramIterations = v.Iterations;
            a.StoredKey = Convert.ToBase64String(v.StoredKey);
        }

        /// <summary>
        /// The SCRAM salt shown for a user without a password: stable and keyed with the data protection secret, so it looks like a real one
        /// (services sharing the database share the secret, so they show the same salt).
        /// </summary>
        /// <param name="passwordSalt">The salt of the placeholder password row</param>
        /// <param name="binding">The password binding</param>
        /// <returns>A salt of <see cref="ScramTools.SaltSize"/> bytes</returns>
        Byte[] NoPasswordScramSalt(String passwordSalt, String binding)
            => Pii.Lookup("scramsalt", String.Concat("np|", passwordSalt, "|", binding)).AsSpan(0, ScramTools.SaltSize).ToArray();

        /// <summary>
        /// Init the database: the tables, the protected values and the one time upgrades of an old database (see <see cref="UpgradePasswords"/> and <see cref="UpgradeActions"/>)
        /// </summary>
        /// <param name="db">The database</param>
        async Task InitAndMigrate(MySqlDbSimpleStack db)
        {
            try
            {
                await db.Init().ConfigureAwait(false);
                //  Several services may share the user database (ex: Hypnos client, system and management servers):
                //  only one at a time validates the tables and migrates (the others wait, then see the migrated database)
                using var migrationLock = await AcquireMigrationLock(db).ConfigureAwait(false);
                if (!Params.ReadOnly)
                    await UpgradePasswords(db).ConfigureAwait(false);
                await InitTables(db).ConfigureAwait(false);
                //  The protected values (user names, e-mail addresses, phone numbers): checks the secret and migrates an old database, opens the gate (DbGetAsync)
                await PrepareProtectedData(db).ConfigureAwait(false);
                if (!Params.ReadOnly)
                    await UpgradeActions(db).ConfigureAwait(false);
                await migrationLock.ReleaseAsync().ConfigureAwait(false);
                PiiReady.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Manager.AddMessage("The user database can't be used (nothing is read or written until this is fixed and the service is restarted)", ex, MessageLevels.Error);
                PiiReady.TrySetException(new Exception("The user database is unavailable: " + ex.Message, ex));
            }
        }

        /// <summary>
        /// The column of an old database that holds password equivalent hashes
        /// </summary>
        const String OldPasswordColumn = "Pwd";

        /// <summary>
        /// One time upgrade of an old database (the password table has the <see cref="OldPasswordColumn"/> column): the passwords stored as password equivalent hashes
        /// are deleted (those users choose a new password with "forgot password"), then the column is dropped.
        /// </summary>
        /// <param name="db">The database</param>
        async Task UpgradePasswords(MySqlDbSimpleStack db)
        {
            using var c = await db.GetAsync().ConfigureAwait(false);
            var t = TableName<DbAuthPassword>(c);
            var cols = await GetColumns(c, t).ConfigureAwait(false);
            if ((cols == null) || !cols.Contains(OldPasswordColumn))
                return;
            var pwd = QC(c, OldPasswordColumn);
            var n = Convert.ToInt64(await c.ScaleCommandAsync(String.Concat("SELECT COUNT(*) FROM ", Q(c, t), " WHERE ", pwd, " <> ''")).ConfigureAwait(false));
            if (n > 0)
                await c.CommandAsync(String.Concat("DELETE FROM ", Q(c, t), " WHERE ", pwd, " <> ''")).ConfigureAwait(false);
            await c.CommandAsync(String.Concat("ALTER TABLE ", Q(c, t), " DROP COLUMN ", pwd)).ConfigureAwait(false);
            Manager.AddMessage(MessageLevels.Warning, $"Deleted {n} password(s) stored in an old format: those users must choose a new password (\"forgot password\")");
        }

        /// <summary>
        /// True if a hash H (ex: the hash of a new password made with the current salt) is the stored password (costs one PBKDF2).
        /// </summary>
        /// <param name="a">The stored password, may be null</param>
        /// <param name="hash">The hash H (base64)</param>
        /// <returns>True if it matches</returns>
        bool IsSamePassword(DbAuthPassword a, String hash)
        {
            if (String.IsNullOrEmpty(hash))
                return false;
            var v = GetVerifier(a);
            return (v != null) && ScramTools.VerifyLegacyHash(v, hash, PasswordBinding);
        }

        /// <summary>
        /// Validate a SCRAM password proof, the one time pad must already be validated and consumed.
        /// </summary>
        /// <param name="a">The stored password, may be null</param>
        /// <param name="userName">The user name of the one time pad</param>
        /// <param name="oneTimePad">The one time pad</param>
        /// <param name="clientNonce">The SCRAM client nonce</param>
        /// <param name="clientProof">The SCRAM client proof (base64)</param>
        /// <returns>True if the proof is valid</returns>
        static bool IsValidPasswordProof(DbAuthPassword a, String userName, String oneTimePad, String clientNonce, String clientProof)
        {
            var v = GetVerifier(a);
            if ((v == null) || (clientProof == null))
                return false;
            return ScramTools.Verify(v, ScramProof.TryCreate(userName, oneTimePad, clientNonce, clientProof));
        }

        /// <summary>
        /// Get the SCRAM parameters of a user.
        /// A user without a password gets parameters that look like real ones (stable, derived from the salt of the placeholder row).
        /// </summary>
        /// <param name="userName">The user name (email, phone)</param>
        /// <returns>The parameters, null if the user is unknown</returns>
        public override async Task<ScramInfo> GetScramInfoAsync(string userName)
        {
            if (LoginUnavailable)
                return null;
            if (String.IsNullOrEmpty(userName))
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            var id = await FindUser(c, userName).ConfigureAwait(false);
            if (id == 0)
                return null;
            var auth = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            if (auth == null)
                return null;
            var binding = PasswordBinding;
            var v = GetVerifier(auth);
            if (v != null)
                return v.GetInfo(binding);
            //  No password: stable parameters of the same shape
            return new ScramInfo
            {
                Salt = Convert.ToBase64String(NoPasswordScramSalt(auth.Salt, binding)),
                Iterations = NewIterations,
                Binding = binding,
            };
        }

        /// <summary>
        /// Authorize a user using a SCRAM proof
        /// </summary>
        /// <param name="proof">The proof</param>
        /// <returns>The auth or null</returns>
        /// <exception cref="AccountDisabledException">The proof is valid but the account is disabled</exception>
        /// <exception cref="PasswordChangeRequiredException">The proof is valid but the password must be changed</exception>
        public override Task<Authorization> ScramAuth(ScramProof proof)
        {
            if (proof == null)
                return NoAuth;
            return PasswordAuth(proof.UserName, a => ScramTools.Verify(GetVerifier(a), proof));
        }
    }
}
