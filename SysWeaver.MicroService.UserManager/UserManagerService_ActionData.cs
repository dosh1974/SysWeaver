using System;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Db;
using SysWeaver.MicroService.Db;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The action tokens (<see cref="DbAction"/>: invites, password resets, e-mail / phone changes, undo links ...):
    /// only a keyed hash of a token is stored (<see cref="ActionKey"/>), and the data (e-mail addresses, phone numbers and user names) is stored encrypted
    /// with the same secret as the other personal values (<see cref="Pii"/>): a per row random and the encrypted data, bound to the stored key and the type of the row
    /// (a row's data can't be moved to another row).
    /// </summary>
    public sealed partial class UserManagerService
    {
        static String ActionKind(String type, String token) => String.Concat("action\0", type ?? "", "\0", token ?? "");

        /// <summary>
        /// Encrypt the data of an action row
        /// </summary>
        /// <param name="token">The stored key of the row</param>
        /// <param name="type">The type of the row</param>
        /// <param name="plain">The plain data, null returns null</param>
        /// <returns>The data to store: the random followed by the encrypted data</returns>
        Byte[] ProtectActionData(String token, String type, Byte[] plain)
        {
            if (plain == null)
                return null;
            var rnd = DataProtection.NewRandom();
            var enc = Pii.EncryptBytes(ActionKind(type, token), rnd, plain);
            var r = new Byte[rnd.Length + enc.Length];
            rnd.CopyTo(r, 0);
            enc.CopyTo(r, rnd.Length);
            return r;
        }

        /// <summary>
        /// Get the plain data of an action row
        /// </summary>
        /// <param name="row">The row, may be null</param>
        /// <returns>The plain data, null if the row or its data is null</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">The data is invalid, was changed or was moved from another row</exception>
        Byte[] ActionData(DbAction row)
        {
            var d = row?.Data;
            if (d == null)
                return null;
            var rs = DataProtection.RandomSize;
            if (d.Length < rs + DataProtection.Overhead)
                throw new System.Security.Cryptography.CryptographicException("The action data is invalid");
            return Pii.DecryptBytes(ActionKind(row.Type, row.Token), d.AsSpan(0, rs), d.AsSpan(rs));
        }

        #region Stored token keys

        /// <summary>
        /// The length of a stored token key (fits the 48 char column)
        /// </summary>
        const int StoredKeyLength = 48;

        /// <summary>
        /// The prefix of the stored keys of the tokens with a subject tag (<see cref="SubjectTokens.Tag"/>): a keyed hash of the tag (<see cref="SubjectTokens.TagLength"/> hex chars),
        /// so the tokens of a subject are found with a "starts with" query, but the tag (a hash of the subject) can't be computed from a known subject without the secret.
        /// </summary>
        /// <param name="tag">The tag (or the first <see cref="SubjectTokens.TagLength"/> chars of a token)</param>
        /// <returns>The prefix</returns>
        String StoredTagPrefix(String tag)
            => Convert.ToHexString(Pii.Lookup("actiontag", (tag ?? "").ToUpperInvariant()), 0, (SubjectTokens.TagLength + 1) / 2).Substring(0, SubjectTokens.TagLength);

        /// <summary>
        /// The key of an action token as stored in the database (<see cref="DbAction.Token"/>): only a keyed hash of the token is stored, so a copy of the database has no working links.
        /// The prefix keeps the (keyed) subject tag, see <see cref="StoredTagPrefix"/>.
        /// </summary>
        /// <remarks>
        /// Every value is hashed, so a stored key given as a token never matches a row: a copy of the database gives no working links.
        /// </remarks>
        /// <param name="token">The token (from the caller)</param>
        /// <returns>The stored key (48 chars)</returns>
        String ActionKey(String token)
        {
            token = token ?? "";
            var tl = SubjectTokens.TagLength;
            var prefix = StoredTagPrefix(token.Length >= tl ? token.Substring(0, tl) : token);
            var h = Convert.ToBase64String(Pii.Lookup("actiontoken", token)).Replace('+', '-').Replace('/', '_');
            return String.Concat(prefix, h.AsSpan(0, StoredKeyLength - prefix.Length));
        }

        #endregion//Stored token keys

        /// <summary>
        /// The meta value (<see cref="DbUserManagerMeta"/>) that tells that the action table is in the current format
        /// </summary>
        const String ActionFormatMeta = "Actions.Format";

        /// <summary>
        /// The current format of the action table (<see cref="ActionFormatMeta"/>)
        /// </summary>
        const String ActionFormat = "3";

        /// <summary>
        /// One time upgrade of an old database (no <see cref="ActionFormatMeta"/> value, or another format): all action rows are deleted
        /// (pending links must be requested again), then the format is stored.
        /// </summary>
        /// <param name="db">The database</param>
        async Task UpgradeActions(MySqlDbSimpleStack db)
        {
            using var c = await db.GetAsync().ConfigureAwait(false);
            var meta = await c.FirstOrDefaultAsync<DbUserManagerMeta>(x => x.Name == ActionFormatMeta).ConfigureAwait(false);
            if (String.Equals(meta?.Value, ActionFormat, StringComparison.Ordinal))
                return;
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var n = await c.DeleteAllAsync<DbAction>().ConfigureAwait(false);
            if (meta == null)
                await c.InsertAsync(new DbUserManagerMeta { Name = ActionFormatMeta, Value = ActionFormat }).ConfigureAwait(false);
            else
            {
                meta.Value = ActionFormat;
                await c.UpdateAsync(meta).ConfigureAwait(false);
            }
            await tr.CommitAsync().ConfigureAwait(false);
            if (n > 0)
                Manager.AddMessage(MessageLevels.Warning, $"Deleted {n} action token(s) stored in an old format (the links must be requested again)");
        }
    }
}
