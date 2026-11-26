using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// A pending action (a link or code that was sent to a user), the table is cleaned of expired actions at start
    /// </summary>
    [Alias("Actions")]
    [PartitionByKey(nameof(Token))]
    public sealed class DbAction
    {
        /// <summary>
        /// The stored key of the action token (the key): a keyed hash of the token, never the token itself (see UserManagerService.ActionKey),
        /// so a copy of the database has no working links
        /// </summary>
        [PrimaryKey]
        [StringLength(48)]
        [Required]
        [Ascii]
        public string Token { get; set; }

        /// <summary>
        /// The type of action (one of the constants of this class)
        /// </summary>
        [StringLength(8)]
        [Required]
        [Ascii]
        public string Type { get; set; }

        /// <summary>
        /// When the action expires (UTC)
        /// </summary>
        [Index]
        [Required]
        public DateTime Expiration { get; set; }

        /// <summary>
        /// The data of the action (depends on <see cref="Type"/>), encrypted and bound to <see cref="Token"/> and <see cref="Type"/> (see UserManagerService.ProtectActionData)
        /// </summary>
        [Required]
        public byte[] Data { get; set; }



        /// <summary>
        /// Action type: reset the password
        /// </summary>
        public const string ResetPassword = "RstPwd";

        /// <summary>
        /// Action type: delete the user
        /// </summary>
        public const string DeleteUser = "DelUsr";

        /// <summary>
        /// Action type: add a password
        /// </summary>
        public const string AddPassword = "AddPwd";

        /// <summary>
        /// Action type: create a user
        /// </summary>
        public const string CreateUser = "NewUsr";

        /// <summary>
        /// Action type: delete the password
        /// </summary>
        public const string DeletePassword = "DelPwd";


        /// <summary>
        /// Action type: add an e-mail address
        /// </summary>
        public const string AddEmail = "AddEmail";

        /// <summary>
        /// Action type: add a phone number
        /// </summary>
        public const string AddPhone = "AddPhone";
        /// <summary>Undo an e-mail address change (the link in the notice to the old address)</summary>
        public const string UndoEmail = "UndoMail";
    }


}
