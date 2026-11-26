using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleStack.Orm;
using SysWeaver.Auth;
using SysWeaver.Db;
using SysWeaver.MicroService.Db;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Protection of the personal values (user names, nick names, e-mail addresses, phone numbers), see <see cref="DataProtection"/>:
    /// the database only holds a lookup hash (to find a user from a value, and for uniqueness), a per row random value (replaced on every change)
    /// and the encrypted value. A leaked database doesn't reveal them without the secret (<see cref="UserManagerParams.DataProtectionSecret"/>, or
    /// <see cref="DataProtection.Default"/> stored in the key value store of the application on this computer).
    /// The fingerprint of the secret is stored in the database (<see cref="DbUserManagerMeta"/>): another secret makes the service refuse to use the database.
    /// A database in the old (plain text) format is migrated on start.
    /// </summary>
    public sealed partial class UserManagerService
    {
        const String KindEmail = "email";
        const String KindPhone = "phone";
        const String KindUserName = "username";
        const String KindNickName = "nickname";

        /// <summary>
        /// The name of the meta value with the fingerprint of the data protection secret
        /// </summary>
        const String FingerprintMeta = "DataProtection.Fingerprint";

        /// <summary>
        /// The protection (null until <see cref="PrepareProtectedData"/> ran)
        /// </summary>
        DataProtection Pii;

        /// <summary>
        /// Completed when the database can be used (the secret is checked and an old database is migrated), faulted if it can't be used
        /// </summary>
        readonly TaskCompletionSource<bool> PiiReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// True if sign in (and every other use of the database) is unavailable: <see cref="UserManagerParams.DisableLogin"/>, or the database can't be used
        /// (wrong data protection secret, failed migration)
        /// </summary>
        bool LoginUnavailable => DisableLogin || PiiReady.Task.IsFaulted;

        /// <summary>
        /// Open a database connection, waits until the database is ready (secret checked, migrated)
        /// </summary>
        /// <returns>An open connection, the caller must dispose it</returns>
        /// <exception cref="Exception">The database can't be used (see the log)</exception>
        async Task<OrmConnection> DbGetAsync()
        {
            await PiiReady.Task.ConfigureAwait(false);
            return await Db.GetAsync().ConfigureAwait(false);
        }

        #region Values

        /// <summary>
        /// Normalize a user name or e-mail address for the lookup hash (the columns used to be case insensitive): trimmed, invariant lower case
        /// </summary>
        static String NormalizeText(String value) => (value ?? "").Trim().ToLowerInvariant();

        /// <summary>
        /// Normalize a phone number for the lookup hash (it is already normalized by the validation): trimmed
        /// </summary>
        static String NormalizePhone(String value) => (value ?? "").Trim();

        /// <summary>
        /// Who performed an action, for log messages: the user guid (never a user name, they are often e-mail addresses)
        /// </summary>
        /// <param name="context">The request, may be null</param>
        /// <returns>The guid of the signed in user, or "?"</returns>
        static String LogActor(SysWeaver.Net.HttpServerRequest context) => context?.Session?.Auth?.Guid ?? "?";

        String LookupKey(String kind, String normalized) => Convert.ToHexStringLower(Pii.Lookup(kind, normalized));

        /// <summary>
        /// The lookup key of a user name
        /// </summary>
        String UserNameKey(String userName) => LookupKey(KindUserName, NormalizeText(userName));

        /// <summary>
        /// The lookup key of an e-mail address
        /// </summary>
        String EmailKey(String email) => LookupKey(KindEmail, NormalizeText(email));

        /// <summary>
        /// The lookup key of a phone number
        /// </summary>
        String PhoneKey(String phone) => LookupKey(KindPhone, NormalizePhone(phone));

        /// <summary>
        /// Protect a value row: a new random value, the lookup key and the encrypted value (from <see cref="IDbProtectedValue.Value"/>)
        /// </summary>
        T ProtectValue<T>(T row, String kind, String key) where T : IDbProtectedValue
        {
            var rnd = DataProtection.NewRandom();
            row.Rnd = rnd;
            row.Key = key;
            row.Enc = Pii.Encrypt(kind, rnd, row.Value ?? "");
            return row;
        }

        /// <summary>
        /// Decrypt a value row (sets <see cref="IDbProtectedValue.Value"/>), null returns null
        /// </summary>
        T UnprotectValue<T>(T row, String kind) where T : class, IDbProtectedValue
        {
            if (row == null)
                return null;
            row.Value = Pii.Decrypt(kind, row.Rnd, row.Enc);
            return row;
        }

        DbEmailAddress Protect(DbEmailAddress row) => ProtectValue(row, KindEmail, EmailKey(row.Email));
        DbPhoneNumber Protect(DbPhoneNumber row) => ProtectValue(row, KindPhone, PhoneKey(row.Phone));
        DbEmailAddress Unprotect(DbEmailAddress row) => UnprotectValue(row, KindEmail);
        DbPhoneNumber Unprotect(DbPhoneNumber row) => UnprotectValue(row, KindPhone);

        /// <summary>
        /// Protect the user name and nick name of a user (a new random value)
        /// </summary>
        DbUser Protect(DbUser user)
        {
            var rnd = DataProtection.NewRandom();
            user.Rnd = rnd;
            user.NameKey = UserNameKey(user.UserName);
            user.NameEnc = Pii.Encrypt(KindUserName, rnd, user.UserName ?? "");
            user.NickEnc = Pii.Encrypt(KindNickName, rnd, user.NickName);
            return user;
        }

        /// <summary>
        /// Decrypt the user name and nick name of a user, null returns null
        /// </summary>
        DbUser Unprotect(DbUser user)
        {
            if (user == null)
                return null;
            user.UserName = Pii.Decrypt(KindUserName, user.Rnd, user.NameEnc);
            user.NickName = Pii.Decrypt(KindNickName, user.Rnd, user.NickEnc);
            return user;
        }

        #endregion//Values

        #region Database helpers

        async Task<DbUser> GetDbUser(OrmConnection c, long id)
            => Unprotect(await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false));

        async Task<DbUser> FindDbUserByName(OrmConnection c, String userName)
        {
            if (String.IsNullOrEmpty(userName))
                return null;
            var k = UserNameKey(userName);
            return Unprotect(await c.FirstOrDefaultAsync<DbUser>(x => x.NameKey == k).ConfigureAwait(false));
        }

        async Task<DbEmailAddress> GetDbEmail(OrmConnection c, long userId)
            => Unprotect(await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false));

        async Task<List<DbEmailAddress>> GetDbEmails(OrmConnection c, long userId)
            => (await c.SelectAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false)).Select(Unprotect).ToList();

        async Task<DbEmailAddress> FindDbEmail(OrmConnection c, String email)
        {
            if (String.IsNullOrEmpty(email))
                return null;
            var k = EmailKey(email);
            return Unprotect(await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Key == k).ConfigureAwait(false));
        }

        async Task<DbPhoneNumber> GetDbPhone(OrmConnection c, long userId)
            => Unprotect(await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false));

        async Task<List<DbPhoneNumber>> GetDbPhones(OrmConnection c, long userId)
            => (await c.SelectAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false)).Select(Unprotect).ToList();

        async Task<DbPhoneNumber> FindDbPhone(OrmConnection c, String phone)
        {
            if (String.IsNullOrEmpty(phone))
                return null;
            var k = PhoneKey(phone);
            return Unprotect(await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Key == k).ConfigureAwait(false));
        }

        Task<int> InsertEmail(OrmConnection c, DbEmailAddress row) => c.InsertAsync(Protect(row));

        Task<int> InsertPhone(OrmConnection c, DbPhoneNumber row) => c.InsertAsync(Protect(row));

        Task<long> InsertUser(OrmConnection c, DbUser user)
        {
            InvalidateUserList();
            return c.InsertAsync<long, DbUser>(Protect(user));
        }

        /// <summary>
        /// Store a changed user name and / or nick name of a user (both are encrypted again with a new random value)
        /// </summary>
        Task<int> UpdateUserNames(OrmConnection c, DbUser user)
        {
            Protect(user);
            InvalidateUserList();
            return c.UpdateAsync(user, x => new { x.Rnd, x.NameKey, x.NameEnc, x.NickEnc });
        }

        #endregion//Database helpers

        #region User list (admin tables)

        /// <summary>
        /// How long the decrypted user list of the admin tables is kept, in ms
        /// </summary>
        const long UserListCacheMs = 3000;

        Tuple<long, List<DbUser>> UserList;
        readonly AsyncLock UserListLock = new AsyncLock();

        void InvalidateUserList() => Interlocked.Exchange(ref UserList, null);

        /// <summary>
        /// All users, decrypted (the admin tables sort, filter and search them in memory: the database can't, the values are encrypted).
        /// Cached for <see cref="UserListCacheMs"/> ms.
        /// </summary>
        async Task<List<DbUser>> GetAllUsers(bool noCache = false)
        {
            var now = Environment.TickCount64;
            var l = UserList;
            if (!noCache && (l != null) && (l.Item1 > now))
                return l.Item2;
            using var _ = await UserListLock.Lock().ConfigureAwait(false);
            l = UserList;
            if (!noCache && (l != null) && (l.Item1 > now))
                return l.Item2;
            List<DbUser> users;
            using (var c = await DbGetAsync().ConfigureAwait(false))
                users = (await c.SelectAsync<DbUser>().ConfigureAwait(false)).Select(Unprotect).ToList();
            Interlocked.Exchange(ref UserList, Tuple.Create(Environment.TickCount64 + UserListCacheMs, users));
            return users;
        }

        #endregion//User list (admin tables)

        #region Start up (secret check, migration)

        /// <summary>
        /// Rows per migration batch
        /// </summary>
        const int MigrateBatch = 500;

        /// <summary>
        /// The suffix of an old (plain text) table while it is migrated
        /// </summary>
        const String OldTableSuffix = "_PlainOld";

        /// <summary>
        /// Check the data protection secret against the database and migrate an old (plain text) database, then init the protected tables.
        /// Restartable: an interrupted migration continues on the next start, the plain text data is only removed after it has been converted (and checked).
        /// </summary>
        /// <param name="db">The database</param>
        /// <exception cref="Exception">The database can't be used (another secret, a failed migration, an old database in read only mode)</exception>
        async Task PrepareProtectedData(MySqlDbSimpleStack db)
        {
            var p = Params;
            var dp = GetDataProtection();
            Pii = dp;
            var fingerprint = dp.Fingerprint;
            using var c = await db.GetAsync().ConfigureAwait(false);
            var stored = (await c.FirstOrDefaultAsync<DbUserManagerMeta>(x => x.Name == FingerprintMeta).ConfigureAwait(false))?.Value;
            if ((stored != null) && !String.Equals(stored, fingerprint, StringComparison.Ordinal))
                throw new Exception($"The data protection secret (fingerprint {fingerprint}) isn't the one used for this user database (fingerprint {stored}): set {nameof(UserManagerParams)}.{nameof(UserManagerParams.DataProtectionSecret)} to the secret that was used, or restore the key value store of this application");
            var usersT = TableName<DbUser>(c);
            var emailsT = TableName<DbEmailAddress>(c);
            var phonesT = TableName<DbPhoneNumber>(c);
            var oldUsers = await IsOldFormat(c, usersT, nameof(DbUser.UserName)).ConfigureAwait(false);
            var oldEmails = await IsOldFormat(c, emailsT, nameof(DbEmailAddress.Email)).ConfigureAwait(false) || await TableExists(c, emailsT + OldTableSuffix).ConfigureAwait(false);
            var oldPhones = await IsOldFormat(c, phonesT, nameof(DbPhoneNumber.Phone)).ConfigureAwait(false) || await TableExists(c, phonesT + OldTableSuffix).ConfigureAwait(false);
            var migrate = oldUsers || oldEmails || oldPhones;
            if (migrate && p.ReadOnly)
                throw new Exception("The user database is in the old (plain text) format and the service is read only, it can't be migrated");
            if (stored == null)
            {
                //  No fingerprint: a new database, an old (plain text) one or a protected one without a stored fingerprint, any protected data must decrypt with this secret
                if (!migrate && (await TableExists(c, usersT).ConfigureAwait(false)))
                {
                    var any = await c.FirstOrDefaultAsync<DbUser>(x => x.Id > 0).ConfigureAwait(false);
                    if ((any != null) && (dp.TryDecrypt(KindUserName, any.Rnd, any.NameEnc) == null))
                        throw new Exception($"The user database is protected with another data protection secret than this one (fingerprint {fingerprint}): set {nameof(UserManagerParams)}.{nameof(UserManagerParams.DataProtectionSecret)} to the secret that was used");
                }
                if (!p.ReadOnly)
                    await c.InsertAsync(new DbUserManagerMeta { Name = FingerprintMeta, Value = fingerprint }).ConfigureAwait(false);
            }
            if (migrate)
            {
                Manager.AddMessage($"The user database is in the old format (plain text user names, e-mail addresses and phone numbers), migrating it (protected with the data protection secret {fingerprint}, keep a backup of the key value store of this application: without the secret the data can't be read)", MessageLevels.Warning);
                if (oldUsers)
                    await MigrateUsers(db, c, usersT).ConfigureAwait(false);
                if (oldEmails)
                    await MigrateValues<DbEmailAddress>(db, c, emailsT, nameof(DbEmailAddress.Email), (v, uid) => Protect(new DbEmailAddress { Email = v, UserId = uid })).ConfigureAwait(false);
                if (oldPhones)
                    await MigrateValues<DbPhoneNumber>(db, c, phonesT, nameof(DbPhoneNumber.Phone), (v, uid) => Protect(new DbPhoneNumber { Phone = v, UserId = uid })).ConfigureAwait(false);
            }
            await InitProtectedTables(db, c).ConfigureAwait(false);
            if (migrate)
                Manager.AddMessage("The user database was migrated, the plain text user names, e-mail addresses and phone numbers are removed", MessageLevels.Info);
            else
            {
                //  A sanity check: the first user decrypts (a wrong secret is found before anything is written)
                var any = await c.FirstOrDefaultAsync<DbUser>(x => x.Id > 0).ConfigureAwait(false);
                if ((any != null) && (dp.TryDecrypt(KindUserName, any.Rnd, any.NameEnc) == null))
                    throw new Exception($"The user names can't be decrypted with the data protection secret (fingerprint {fingerprint})");
            }
        }

        /// <summary>
        /// Init (create or validate) the tables with protected values
        /// </summary>
        static async Task InitProtectedTables(MySqlDbSimpleStack db, OrmConnection c)
        {
            await db.InitTable<DbUser>(c).ConfigureAwait(false);
            await db.InitTable<DbEmailAddress>(c).ConfigureAwait(false);
            await db.InitTable<DbPhoneNumber>(c).ConfigureAwait(false);
        }

        static String TableName<T>(OrmConnection c)
            => c.DialectProvider.NamingStrategy.GetTableName(ModelDefinition<T>.Definition.ModelName);

        static String Q(OrmConnection c, String table) => c.DialectProvider.GetQuotedTableName(table);

        static String QC(OrmConnection c, String column) => c.DialectProvider.GetQuotedColumnName(column);

        /// <summary>
        /// Get the column names of a table, null if the table doesn't exist
        /// </summary>
        static async Task<HashSet<String>> GetColumns(OrmConnection c, String table)
        {
            try
            {
                var cols = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                using var r = await c.ReaderCommandAsync("SHOW COLUMNS FROM " + Q(c, table)).ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                    cols.Add(r.GetString(0));
                return cols;
            }
            catch
            {
                return null;
            }
        }

        static async Task<bool> TableExists(OrmConnection c, String table) => (await GetColumns(c, table).ConfigureAwait(false)) != null;

        static async Task<bool> IsOldFormat(OrmConnection c, String table, String plainColumn)
            => (await GetColumns(c, table).ConfigureAwait(false))?.Contains(plainColumn) ?? false;

        /// <summary>
        /// Migrate the users table in place (the ids are kept, other tables refer to them): add the protected columns (nullable), fill them in batches,
        /// check that every row is filled, then <see cref="InitProtectedTables"/> drops the plain text columns and adds the indexes / constraints of the model.
        /// </summary>
        async Task MigrateUsers(MySqlDbSimpleStack db, OrmConnection c, String table)
        {
            var t = Q(c, table);
            var cols = await GetColumns(c, table).ConfigureAwait(false);
            var keyCol = QC(c, nameof(DbUser.NameKey));
            async Task Add(String name, String type)
            {
                if (!cols.Contains(name))
                    await c.CommandAsync(String.Concat("ALTER TABLE ", t, " ADD COLUMN ", QC(c, name), " ", type)).ConfigureAwait(false);
            }
            await Add(nameof(DbUser.Rnd), "BLOB NULL").ConfigureAwait(false);
            await Add(nameof(DbUser.NameKey), "VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL").ConfigureAwait(false);
            await Add(nameof(DbUser.NameEnc), "BLOB NULL").ConfigureAwait(false);
            await Add(nameof(DbUser.NickEnc), "BLOB NULL").ConfigureAwait(false);
            var idCol = QC(c, nameof(DbUser.Id));
            var select = String.Concat("SELECT ", idCol, ", ", QC(c, nameof(DbUser.UserName)), ", ", QC(c, nameof(DbUser.NickName)), " FROM ", t, " WHERE ", keyCol, " IS NULL ORDER BY ", idCol, " LIMIT ", MigrateBatch.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var update = String.Concat("UPDATE ", t, " SET ", QC(c, nameof(DbUser.Rnd)), "=@p0, ", keyCol, "=@p1, ", QC(c, nameof(DbUser.NameEnc)), "=@p2, ", QC(c, nameof(DbUser.NickEnc)), "=@p3 WHERE ", idCol, "=@p4");
            long count = 0;
            for (; ; )
            {
                var rows = new List<DbUser>();
                using (var r = await c.ReaderCommandAsync(select).ConfigureAwait(false))
                {
                    while (await r.ReadAsync().ConfigureAwait(false))
                        rows.Add(new DbUser
                        {
                            Id = r.GetInt64(0),
                            UserName = r.IsDBNull(1) ? "" : r.GetString(1),
                            NickName = r.IsDBNull(2) ? null : r.GetString(2),
                        });
                }
                if (rows.Count == 0)
                    break;
                foreach (var u in rows)
                {
                    Protect(u);
                    await c.CommandAsync(update, u.Rnd, u.NameKey, u.NameEnc, (Object)u.NickEnc ?? DBNull.Value, u.Id).ConfigureAwait(false);
                    ++count;
                }
            }
            //  Every row must be converted before the plain text columns are dropped
            var left = Convert.ToInt64(await c.ScaleCommandAsync(String.Concat("SELECT COUNT(*) FROM ", t, " WHERE ", keyCol, " IS NULL")).ConfigureAwait(false));
            if (left != 0)
                throw new Exception($"{left} user(s) could not be migrated");
            //  Verify a sample: decrypts to the stored plain text
            using (var r = await c.ReaderCommandAsync(String.Concat("SELECT ", QC(c, nameof(DbUser.UserName)), ", ", QC(c, nameof(DbUser.Rnd)), ", ", QC(c, nameof(DbUser.NameEnc)), " FROM ", t, " LIMIT 10")).ConfigureAwait(false))
            {
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var plain = r.IsDBNull(0) ? "" : r.GetString(0);
                    if (Pii.Decrypt(KindUserName, (Byte[])r.GetValue(1), (Byte[])r.GetValue(2)) != plain)
                        throw new Exception("The migrated user names don't match");
                }
            }
            //  The plain text columns are dropped by the table validation (InitProtectedTables), done explicitly here so they are gone even if the validation is off
            await c.CommandAsync(String.Concat("ALTER TABLE ", t, " DROP COLUMN ", QC(c, nameof(DbUser.UserName)), ", DROP COLUMN ", QC(c, nameof(DbUser.NickName)))).ConfigureAwait(false);
            Manager.AddMessage(MessageLevels.Info, $"Migrated {count} user(s)");
        }

        /// <summary>
        /// Migrate a value table (e-mail addresses, phone numbers): the old table is renamed (the primary and partition keys change), the new table is
        /// made, the rows are converted and inserted (rows that are already there are skipped, so it can continue after an interruption), every old row
        /// is checked to be in the new table, then the old table is dropped.
        /// </summary>
        async Task MigrateValues<T>(MySqlDbSimpleStack db, OrmConnection c, String table, String plainColumn, Func<String, long, T> make) where T : class, IDbProtectedValue, new()
        {
            var old = table + OldTableSuffix;
            if (!await TableExists(c, old).ConfigureAwait(false))
                await c.CommandAsync(String.Concat("RENAME TABLE ", Q(c, table), " TO ", Q(c, old))).ConfigureAwait(false);
            await db.InitTable<T>(c).ConfigureAwait(false);
            var newT = Q(c, table);
            var keyCol = QC(c, nameof(IDbProtectedValue.Key));
            var plain = QC(c, plainColumn);
            var uidCol = QC(c, nameof(IDbProtectedValue.UserId));
            long offset = 0;
            long count = 0;
            long total = 0;
            for (; ; )
            {
                var rows = new List<Tuple<String, long>>();
                using (var r = await c.ReaderCommandAsync(String.Concat("SELECT ", plain, ", ", uidCol, " FROM ", Q(c, old), " ORDER BY ", plain, " LIMIT ", MigrateBatch.ToString(System.Globalization.CultureInfo.InvariantCulture), " OFFSET ", offset.ToString(System.Globalization.CultureInfo.InvariantCulture))).ConfigureAwait(false))
                {
                    while (await r.ReadAsync().ConfigureAwait(false))
                        rows.Add(Tuple.Create(r.GetString(0), r.GetInt64(1)));
                }
                if (rows.Count == 0)
                    break;
                offset += rows.Count;
                foreach (var x in rows)
                {
                    ++total;
                    var row = make(x.Item1, x.Item2);
                    var exists = await c.ScaleCommandAsync(String.Concat("SELECT COUNT(*) FROM ", newT, " WHERE ", keyCol, "=@p0"), row.Key).ConfigureAwait(false);
                    if (Convert.ToInt64(exists) != 0)
                        continue;
                    await c.InsertAsync(row).ConfigureAwait(false);
                    ++count;
                }
            }
            //  Every old value must be in the new table before the old table is dropped
            offset = 0;
            for (; ; )
            {
                var keys = new List<String>();
                using (var r = await c.ReaderCommandAsync(String.Concat("SELECT ", plain, ", ", uidCol, " FROM ", Q(c, old), " ORDER BY ", plain, " LIMIT ", MigrateBatch.ToString(System.Globalization.CultureInfo.InvariantCulture), " OFFSET ", offset.ToString(System.Globalization.CultureInfo.InvariantCulture))).ConfigureAwait(false))
                {
                    while (await r.ReadAsync().ConfigureAwait(false))
                        keys.Add(make(r.GetString(0), r.GetInt64(1)).Key);
                }
                if (keys.Count == 0)
                    break;
                offset += keys.Count;
                foreach (var k in keys)
                {
                    var n = Convert.ToInt64(await c.ScaleCommandAsync(String.Concat("SELECT COUNT(*) FROM ", newT, " WHERE ", keyCol, "=@p0"), k).ConfigureAwait(false));
                    if (n != 1)
                        throw new Exception($"The migration of {table} failed, a value is missing in the new table (the old table {old} is kept)");
                }
            }
            await c.CommandAsync("DROP TABLE " + Q(c, old)).ConfigureAwait(false);
            Manager.AddMessage(MessageLevels.Info, $"Migrated {table}: {count} row(s) converted ({total} in the old table)");
        }

        #endregion//Start up (secret check, migration)
    }
}
