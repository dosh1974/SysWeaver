using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// The e-mail address of a user.
    /// The address isn't stored in plain text: <see cref="Key"/> is the lookup hash of the normalized (trimmed, lower case) address and
    /// <see cref="IDbProtectedValue.Enc"/> the encrypted address (see <see cref="SysWeaver.Auth.DataProtection"/>).
    /// </summary>
    [Alias("Emails")]
    [PartitionByKey(nameof(Key))]
    public sealed class DbEmailAddress : IDbProtectedValue
    {
        /// <summary>
        /// The lookup hash of the normalized e-mail address, lower case hex (unique)
        /// </summary>
        [PrimaryKey]
        [StringLength(64)]
        [Required]
        [Ascii]
        public string Key { get; set; }

        /// <summary>
        /// The id of the user that has the address
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

        /// <summary>
        /// The random value of the row (replaced on every change)
        /// </summary>
        [Required]
        public byte[] Rnd { get; set; }

        /// <summary>
        /// The encrypted e-mail address (original casing)
        /// </summary>
        [Required]
        public byte[] Enc { get; set; }

        /// <summary>
        /// The e-mail address (not stored, filled when the row is read by the user manager)
        /// </summary>
        [Ignore]
        public string Email { get; set; }

        String IDbProtectedValue.Value { get => Email; set => Email = value; }
    }
}
