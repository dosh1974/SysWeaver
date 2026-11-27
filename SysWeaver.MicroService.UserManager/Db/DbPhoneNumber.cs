using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// The phone number of a user.
    /// The number isn't stored in plain text: <see cref="Key"/> is the lookup hash of the normalized number and
    /// <see cref="Enc"/> the encrypted number (see <see cref="SysWeaver.Auth.DataProtection"/>).
    /// </summary>
    [Alias("PhoneNumbers")]
    [PartitionByKey(nameof(Key))]
    public sealed class DbPhoneNumber : IDbProtectedValue
    {
        /// <summary>
        /// The lookup hash of the normalized phone number, lower case hex (unique)
        /// </summary>
        [PrimaryKey]
        [StringLength(64)]
        [Required]
        [Ascii]
        public string Key { get; set; }

        /// <summary>
        /// The id of the user that has the phone number
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
        /// The encrypted phone number
        /// </summary>
        [Required]
        public byte[] Enc { get; set; }

        /// <summary>
        /// The phone number (normalized, prefix and number separated by a space; not stored, filled when the row is read by the user manager)
        /// </summary>
        [Ignore]
        [SysWeaver.Data.TableDataPhone]
        public string Phone { get; set; }

        String IDbProtectedValue.Value { get => Phone; set => Phone = value; }
    }
}
