using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// A row of the users table of the Users page (<see cref="UserManagerService.UserAdminTable"/>): the user (the same table and columns as
    /// <see cref="DbUser"/>, read only, the table is made by <see cref="DbUser"/>) and its row actions.
    /// The plain users table (<see cref="UserManagerService.UserDataTable"/>) stays <see cref="DbUser"/>, without the actions, as before.
    /// </summary>
    [Alias("Users")]
    public sealed class DbUserAdminRow
    {
        /// <summary>
        /// Unqiue user id
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        /// <summary>
        /// When the user was created
        /// </summary>
        [Index]
        [Required]
        public DateTime Created { get; set; }

        /// <summary>
        /// Icon, this only works if the UserManasger instance is the default "UM"
        /// </summary>
        [Ignore]
        [TableDataUserIcon]
        public String Icon => UserManagerService.MakeGuid(Id, "UM").ToHex();

        /// <summary>
        /// The user name
        /// </summary>
        [Index(true)]
        [Required]
        [StringLength(AuhorizationLimits.MaxUserNameLength)]
        [CaseInsensitive]
        public string UserName { get; set; }

        /// <summary>
        /// The domain that the user belongs to (the meaning may be application specific)
        /// </summary>
        [Index]
        [StringLength(AuhorizationLimits.MaxDomainName)]
        public string Domain { get; set; }

        /// <summary>
        /// The language preference for the user
        /// </summary>
        [Index]
        [Ascii]
        [StringLength(8)]
        public string Language { get; set; }

        /// <summary>
        /// Nick name
        /// </summary>
        [StringLength(AuhorizationLimits.MaxNickNameLength)]
        public string NickName { get; set; }

        /// <summary>
        /// Actions on the user
        /// </summary>
        /// <remarks>The row actions of the users table, not stored. The value is "rowToken,userName,reset,unlock,disable,enable" ({0}, {2} and the visibility
        /// flags {3} to {6} in the action formats); the row token can't be guessed, so a plain link or GET request can't use an action.
        /// <see cref="UserManagerService.UserAdminTable"/> blanks it (no action) for users that may not manage users, and sets the flags by the state of each user:
        /// Force password reset only when it can be done (a password, no reset pending, and a way back in: an address or a passkey), Undo forced reset
        /// when one is pending, Disable / Enable by state. Without the table's flags (this default) only Force password reset is shown.
        /// Each action has a done text (the 7th part, <see cref="TableDataActionsFullAttribute"/>): the web table shows it as the row's result and
        /// announces it, ex: "ann@example.com can sign in again." instead of the generic "Enable (ann@example.com): done" (Admin N8). They are the
        /// sentences of the matching admin APIs (<see cref="AdminUserResult.Text"/>), plain like the other parts of the action formats.
        /// None of the four is final (<see cref="TableDataActionFlags.None"/>, declared, G47 / N53): the user stays, so after each action the row is re-read and
        /// offers the actions of its new state (ex: Undo forced reset right after Force password reset). The tool tips' words ("…their open sessions end")
        /// never decide that.</remarks>
        [Ignore]
        [TableDataActionsFull(new[] { TableDataActionFlags.None, TableDataActionFlags.None, TableDataActionFlags.None, TableDataActionFlags.None },
            "Force password reset",
            "Make the user choose a new password at the next sign in: the current password stops working and their open sessions end",
            "../Api/auth/RowForcePasswordReset?\"{0}\"",
            "../icons/user_password.svg",
            "Force a password reset for {2}?\nThey must choose a new password at the next sign in: the current password stops working and their open sessions end. Passkeys keep working.",
            "{3}",
            "{2} must choose a new password at the next sign in. Their open sessions ended.",

            "Undo forced reset",
            "The user's current password works again (ex: a user who can't get the link to choose a new password)",
            "../Api/auth/RowUnlockUser?\"{0}\"",
            "../icons/unlock.svg",
            "Undo the forced password reset of {2}?\nTheir current password works again.",
            "{4}",
            "{2} can sign in with the current password again.",

            "Disable",
            "The user can't sign in in any way until the account is enabled again, nothing is deleted",
            "../Api/auth/RowDisableUser?\"{0}\"",
            "../icons/lock.svg",
            "Disable the account {2}?\nThey can't sign in in any way and their open sessions end, until the account is enabled again. Nothing is deleted.",
            "{5}",
            "{2} is disabled and can't sign in until the account is enabled again.",

            "Enable",
            "The user can sign in again",
            "../Api/auth/RowEnableUser?\"{0}\"",
            "../icons/user_check.svg",
            "Enable the account {2}?\nThey can sign in again.",
            "{6}",
            "{2} can sign in again."
            )]
        [TableDataOrder(100)]
        public String Actions => String.Concat(UserManagerService.UserRowToken(Id), ",", (UserName ?? "").Replace(',', ' '), ",1,0,0,0");
    }

}
