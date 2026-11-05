using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Ask about an action token (a link sent in a message) without using it, see UserManagerService.CheckActionToken.
    /// </summary>
    public sealed class ActionTokenRequest
    {
        /// <summary>
        /// The token, as in the link
        /// </summary>
        public String Token;

        /// <summary>
        /// The kind of link the page expects, null or empty for any. One of <see cref="ActionTokenKinds"/>.
        /// "ResetPassword" also matches an add password link, "AddEmail" / "AddPhone" also match a restore link (the same page and api),
        /// "AddPassKey" also matches a reset / add password link (they can add a passkey).
        /// </summary>
        public String Kind;
    }

    /// <summary>
    /// The kinds of action tokens (links) of the user manager, see <see cref="ActionTokenState.Kind"/>.
    /// </summary>
    public static class ActionTokenKinds
    {
        /// <summary>Reset the password (auth/ResetPassword.html)</summary>
        public const String ResetPassword = "ResetPassword";
        /// <summary>Add a password (auth/ResetPassword.html)</summary>
        public const String AddPassword = "AddPassword";
        /// <summary>Remove the password (auth/DeletePassword.html)</summary>
        public const String DeletePassword = "DeletePassword";
        /// <summary>Delete the account (auth/DeleteAccount.html)</summary>
        public const String DeleteAccount = "DeleteAccount";
        /// <summary>Add or change the e-mail address (auth/AddEmail.html, auth/ChangeEmail.html)</summary>
        public const String AddEmail = "AddEmail";
        /// <summary>Restore a removed e-mail address (auth/AddEmail.html)</summary>
        public const String RestoreEmail = "RestoreEmail";
        /// <summary>Add or change the phone number (auth/AddPhone.html, auth/ChangePhone.html)</summary>
        public const String AddPhone = "AddPhone";
        /// <summary>Restore a removed phone number (auth/AddPhone.html)</summary>
        public const String RestorePhone = "RestorePhone";
        /// <summary>Undo an e-mail address change, from the notice sent to the old address (auth/UndoEmail.html)</summary>
        public const String UndoEmail = "UndoEmail";
        /// <summary>Add a passkey on another device</summary>
        public const String AddPassKey = "AddPassKey";
        /// <summary>An invitation or a sign up (auth/ChoosePassword.html)</summary>
        public const String NewUser = "NewUser";
    }

    /// <summary>
    /// The states of an action token, see <see cref="ActionTokenState.State"/>.
    /// </summary>
    public static class ActionTokenStates
    {
        /// <summary>The link works</summary>
        public const String Live = "Live";
        /// <summary>The link was already used</summary>
        public const String Used = "Used";
        /// <summary>A newer link of the same kind was sent</summary>
        public const String Replaced = "Replaced";
        /// <summary>The account changed after the link was sent (password, forced reset, passkey, e-mail, phone, a newer address, the account was deleted)</summary>
        public const String Revoked = "Revoked";
        /// <summary>The link has expired</summary>
        public const String Expired = "Expired";
        /// <summary>Not known: wrong, incomplete, a typed code, or forgotten after a restart of the server</summary>
        public const String Unknown = "Unknown";
        /// <summary>A live link of another kind than asked (no facts are returned)</summary>
        public const String WrongKind = "WrongKind";
        /// <summary>The link is live, but an administrator disabled the account: it works again when the account is enabled (no facts are returned)</summary>
        public const String Disabled = "Disabled";
    }

    /// <summary>
    /// What an action token (a link sent in a message) is and whether it still works, without using it (UserManagerService.CheckActionToken):
    /// a link page asks on load and shows "This link no longer works" with the real cause before the user acts, or the facts in its confirmation.
    /// </summary>
    public sealed class ActionTokenState
    {
        /// <summary>
        /// One of <see cref="ActionTokenStates"/>: "Live", "Used", "Replaced", "Revoked", "Expired", "Unknown" or "WrongKind"
        /// </summary>
        public String State;

        /// <summary>
        /// The kind of the link (one of <see cref="ActionTokenKinds"/>), null if not known
        /// </summary>
        public String Kind;

        /// <summary>
        /// Why the link doesn't work, one sentence for people (translated), null when it works.
        /// Ex: "This link no longer works: a newer link was sent, use the link in the newest message."
        /// </summary>
        public String Reason;

        /// <summary>
        /// The user name of the account the link is for (only when it works)
        /// </summary>
        public String Account;

        /// <summary>
        /// The e-mail address or phone number that the link adds (add / change / restore e-mail or phone, an invitation; only when it works)
        /// </summary>
        public String Target;

        /// <summary>
        /// When the link expires (UTC, only when it works)
        /// </summary>
        public DateTime Expires;
    }
}
