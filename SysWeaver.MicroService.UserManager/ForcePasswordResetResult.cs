using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The result of <see cref="UserManagerService.ForcePasswordResetUser"/> and <see cref="UserManagerService.RowForcePasswordReset"/>.
    /// </summary>
    public sealed class ForcePasswordResetResult
    {
        /// <summary>
        /// True if the user was found
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// True if the user has a password (that must now be changed at the next sign in), false if the user has no password (ex: passkey only, nothing was changed)
        /// </summary>
        public bool HadPassword { get; set; }

        /// <summary>
        /// The user name of the user (if found)
        /// </summary>
        public String UserName { get; set; }

        /// <summary>
        /// The number of sessions of the user that were signed out
        /// </summary>
        public int SignedOut { get; set; }

        /// <summary>
        /// The number of e-mail addresses and phone numbers of the user that a message (the link to choose a new password) can be sent to
        /// </summary>
        public int Reachable { get; set; }

        /// <summary>
        /// The number of passkeys of the user (they keep working after a forced reset)
        /// </summary>
        public int PassKeys { get; set; }

        /// <summary>
        /// True if the user must already choose a new password (a forced reset is pending, see <see cref="UserManagerService.UnlockUser"/> to undo it)
        /// </summary>
        public bool MustResetPassword { get; set; }

        /// <summary>
        /// True if the account is disabled (<see cref="UserManagerService.DisableUser"/>)
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// True if the user can't sign in now in any way (disabled, or no passkey and no usable password: a pending forced reset with no address to send the link to).
        /// The administrator can undo the reset (<see cref="UserManagerService.UnlockUser"/>), or delete the account (<see cref="UserManagerService.AdminDeleteUser"/>).
        /// </summary>
        public bool LockedOut { get; set; }

        /// <summary>
        /// Null, or why a forced reset of this user is refused (translated): it would leave the user without any way to sign in
        /// (no e-mail address, phone number or passkey). <see cref="UserManagerService.ForcePasswordResetUser"/> answers 409 with this reason.
        /// </summary>
        public String Refused { get; set; }

        /// <summary>
        /// What a forced reset does to this user (and the user's state), sentences for the confirmation (translated), ex:
        /// "The current password stops working and their open sessions end; ...", "Their passkey keeps working."
        /// </summary>
        public String[] Consequences { get; set; }
    }


    /// <summary>
    /// A pending invitation (or self sign up) that hasn't been accepted yet, a row of <see cref="UserManagerService.InvitationTable"/>.
    /// </summary>
    public sealed class PendingInvitation
    {
        /// <summary>
        /// Who is invited: the e-mail address (or phone number)
        /// </summary>
        [TableDataOrder(0)]
        public String Target { get; set; }

        /// <summary>
        /// The user name the new user gets (when it differs from the e-mail address)
        /// </summary>
        [TableDataOrder(1)]
        public String UserName { get; set; }

        /// <summary>
        /// The security tokens (roles) the new user gets
        /// </summary>
        [TableDataOrder(2)]
        public String Tokens { get; set; }

        /// <summary>
        /// Who sent the invitation, "Self sign-up" for a sign up, empty for an invitation made before this was recorded
        /// </summary>
        [TableDataOrder(3)]
        public String InvitedBy { get; set; }

        /// <summary>
        /// When the invitation link stops working (UTC)
        /// </summary>
        [TableDataOrder(4)]
        public DateTime Expires { get; set; }

        /// <summary>
        /// The cancel token (see <see cref="UserManagerService.CancelInvitation"/>)
        /// </summary>
        [TableDataIgnore]
        public String CancelToken;

        /// <summary>
        /// Actions on the invitation
        /// </summary>
        //  The value is "cancelToken,target,invitation,signUp" ({0}, {2}, {3} and {4} in the action formats), so a plain link or GET request
        //  can't cancel an invitation. One of the two buttons shows per row ({3} / {4}, the "visible" part): an invitation is withdrawn
        //  as an invitation, a self sign up in its own words (S7b N59: "Withdraw the invitation of ... You can invite ... again" for a
        //  sign up nobody invited); both use the same api. The done text (the 7th part, TableDataActionsFull) is the row's result and
        //  its announcement, instead of the generic "Withdraw invitation: done" (Admin N48). Both are final (G47, declared, never guessed
        //  from the words): the web table keeps the row muted without actions until the user refreshes
        [TableDataActionsFull(new[] { TableDataActionFlags.Final, TableDataActionFlags.Final },
            "Withdraw invitation",
            "Withdraw this invitation: the link and the code in the message stop working",
            "../Api/auth/CancelInvitation?\"{0}\"",
            "../icons/close.svg",
            "Withdraw the invitation of {2}?\nThe link and the code in the message stop working. You can invite {2} again later.",
            "{3}",
            "The invitation of {2} is withdrawn: its link and code no longer work. You can invite {2} again.",

            "Withdraw sign-up",
            "Withdraw this sign-up: the code sent to it stops working",
            "../Api/auth/CancelInvitation?\"{0}\"",
            "../icons/close.svg",
            "Withdraw the sign-up of {2}?\nThe code sent to it stops working. {2} can sign up again later.",
            "{4}",
            "The sign-up of {2} is withdrawn: its code no longer works."
            )]
        [TableDataOrder(100)]
        public String Actions
        {
            get
            {
                if (String.IsNullOrEmpty(CancelToken))
                    return "";
                var signUp = InvitedBy == UserManagerService.SelfSignUpLabel;
                return String.Concat(CancelToken, ",", (Target ?? "").Replace(',', ' '), signUp ? ",," : ",1,", signUp ? "1" : "");
            }
        }
    }
}
