using System;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.MicroService.Db;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The data of an undo link (<see cref="DbAction.UndoEmail"/>): the account as it was before an e-mail address change
    /// </summary>
    sealed class InternalUndoEmailData
    {
        public long UserId { get; set; }
        /// <summary>The address before the change (the notice with the link was sent to it)</summary>
        public String OldEmail { get; set; }
        /// <summary>The address it was changed to</summary>
        public String NewEmail { get; set; }
        /// <summary>The user name before the change</summary>
        public String OldUserName { get; set; }
        /// <summary>The nick name before the change</summary>
        public String OldNickName { get; set; }
        /// <summary>The phone number at the time of the change, null for none</summary>
        public String OldPhone { get; set; }
        /// <summary>When the address was changed (UTC)</summary>
        public DateTime Changed { get; set; }
    }

    public sealed partial class UserManagerService
    {
        #region Undo an e-mail address change

        /// <summary>
        /// How long the undo link in the changed e-mail notice works
        /// </summary>
        readonly TimeSpan UndoEmailTimeout = TimeSpan.FromDays(7);

        /// <summary>
        /// The passkeys created this long before the change (or later) are removed by an undo (the one who took the account may have
        /// added one just before changing the address)
        /// </summary>
        readonly TimeSpan UndoPassKeyWindow = TimeSpan.FromHours(24);

        /// <summary>
        /// Make the undo link for an e-mail address change (sent to the old address with the notice). Call it after the change is committed:
        /// the change revokes the e-mail links of the account.
        /// </summary>
        async Task<String> AddUndoEmail(InternalUndoEmailData data)
        {
            //  One undo link per changed address: a second change (A -> B -> C) doesn't replace the link sent to A with the one sent to B
            var tokens = await AddAction(data, DbAction.UndoEmail, UserSubject(data.UserId, UndoVariant), data.Changed + UndoEmailTimeout, replace: false).ConfigureAwait(false);
            return tokens.Item1;
        }

        /// <summary>
        /// The link of the notice to the old e-mail address after a change ("This wasn't me"): give the account back to the owner of that address.
        /// <list type="bullet">
        /// <item>The old e-mail address is the account's address again (and the user name / nick name, if they were the address).</item>
        /// <item>The phone number is the one at the time of the change again.</item>
        /// <item>The passkeys created after (or shortly before) the change are removed.</item>
        /// <item>The password stops working: a reset link and code are sent to the old address.</item>
        /// <item>Every other link of the account stops working, and every session of the account is signed out.</item>
        /// </list>
        /// Anyone with the link can use it (no sign in: the account may be taken), once. Only the deletion of the account revokes it.
        /// </summary>
        /// <param name="token">The token of the link</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The address the reset instructions were sent to (the old address)</returns>
        /// <exception cref="HttpResponseException">409: the old address now belongs to another account</exception>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<String> UndoEmailChange(String token, HttpServerRequest context)
        {
            if (String.IsNullOrEmpty(token) || (token.Length <= 32))
                throw Refused(token);
            var dataD = await GetAction<InternalUndoEmailData>(token, DbAction.UndoEmail, null).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
            token = dataD.Item1;
            var data = dataD.Item2;
            var userId = data.UserId;
            var oldEmail = data.OldEmail;
            String currentEmail = null;
            String currentPhone = null;
            bool phoneRestored = false;
            int passKeys = 0;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    throw new UserNoLongerExistException(userId);
                await ThrowIfDisabled(c, userId).ConfigureAwait(false);
                var other = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == oldEmail).ConfigureAwait(false);
                if ((other != null) && (other.UserId != userId))
                    throw HttpResponseException.Refused(String.Concat("The address ", oldEmail.ToQuoted(), " now belongs to another account: contact the support of this site to get your account back"));
                //  The address
                var current = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                currentEmail = current?.Email;
                if (!oldEmail.FastEquals(currentEmail))
                {
                    if (current != null)
                        await c.DeleteAsync(current).ConfigureAwait(false);
                    await c.InsertAsync(new DbEmailAddress
                    {
                        Email = oldEmail,
                        UserId = userId,
                    }).ConfigureAwait(false);
                }
                //  The user name and nick name, when the change made them the new address (no other account may use the old name)
                bool nameChanged = false;
                if ((currentEmail != null) && user.UserName.FastEquals(currentEmail) && !user.UserName.FastEquals(data.OldUserName)
                    && !String.IsNullOrEmpty(data.OldUserName)
                    && (await c.FirstOrDefaultAsync<DbUser>(x => x.UserName == data.OldUserName && x.Id != userId).ConfigureAwait(false) == null))
                {
                    user.UserName = data.OldUserName;
                    nameChanged = true;
                }
                if ((currentEmail != null) && user.NickName.FastEquals(currentEmail) && !String.IsNullOrEmpty(data.OldNickName))
                {
                    user.NickName = data.OldNickName;
                    nameChanged = true;
                }
                if (nameChanged)
                    await c.UpdateAsync(user, x => new { x.UserName, x.NickName }).ConfigureAwait(false);
                //  The phone number at the time of the change
                var phone = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
                currentPhone = phone?.Phone;
                if (!String.Equals(currentPhone, data.OldPhone, StringComparison.Ordinal))
                {
                    if (phone != null)
                        await c.DeleteAsync(phone).ConfigureAwait(false);
                    var oldPhone = data.OldPhone;
                    if ((oldPhone != null) && (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == oldPhone).ConfigureAwait(false) == null))
                    {
                        await c.InsertAsync(new DbPhoneNumber
                        {
                            Phone = oldPhone,
                            UserId = userId,
                        }).ConfigureAwait(false);
                    }
                    phoneRestored = true;
                }
                //  The passkeys made after (or shortly before) the change
                var since = data.Changed - UndoPassKeyWindow;
                passKeys = await c.DeleteAllAsync<DbAuthPassKey>(x => x.UserId == userId && x.Created >= since).ConfigureAwait(false);
                //  The password stops working (the one who changed the address knows it): no hash, a new one must be chosen
                var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                if (pwd != null)
                {
                    pwd.Pwd = "";
                    pwd.MustResetPassword = true;
                    await c.UpdateAsync(pwd, x => new { x.Pwd, x.MustResetPassword }).ConfigureAwait(false);
                }
                //  This link is used, the other undo links and every other link of the account stop working
                await DeleteAction(c, token, DbAction.UndoEmail, null).ConfigureAwait(false);
                await RevokeActions(c, UserSubject(userId, UndoVariant), CauseUsed, null, DbAction.UndoEmail).ConfigureAwait(false);
                await RevokeOnEvent(c, userId, AccountEvents.PasswordForcedReset).ConfigureAwait(false);
                await RevokeOnEvent(c, userId, AccountEvents.EmailChanged).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var signedOut = SignOutUser(context, userId, "The account was given back to the owner of its old e-mail address: sign in again");
            Manager.AddMessage(MessageLevels.Warning, $"User #{userId}: the e-mail address change was undone from the link sent to the old address, {passKeys} passkey(s) removed, {signedOut} session(s) signed out, the password must be chosen again");
            //  Done: the events and the reset message can't undo it
            try
            {
                if (!oldEmail.FastEquals(currentEmail))
                    await RaiseOnEmailChanged(userId, oldEmail, currentEmail).ConfigureAwait(false);
                if (phoneRestored)
                {
                    if (currentPhone != null)
                        await RaiseOnPhoneRemoved(userId, currentPhone).ConfigureAwait(false);
                    if (data.OldPhone != null)
                        await RaiseOnPhoneAdded(userId, data.OldPhone).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Manager.AddMessage(MessageLevels.Warning, $"User #{userId}: an event handler failed after the e-mail address change was undone", ex);
            }
            try
            {
                //  This browser gets the code too (the owner opened the link here)
                await SendResetPassword(oldEmail, false, false, context?.Session, LinkRoot(context), GetMessageParams(context), context?.Session?.Language).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Manager.AddMessage(MessageLevels.Warning, $"User #{userId}: failed to send the password reset after the e-mail address change was undone (the forgot password page sends a new one)", ex);
            }
            return oldEmail;
        }

        #endregion//Undo an e-mail address change
    }
}
