using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// The phone number of a user
    /// </summary>
    [Alias("PhoneNumbers")]
    [PartitionByKey(nameof(Phone))]
    public sealed class DbPhoneNumber
    {
        /// <summary>
        /// The phone number (normalized, prefix and number separated by a space)
        /// </summary>
        [PrimaryKey]
        [StringLength(64)]
        [Required]
        [Ascii]
        public string Phone { get; set; }

        /// <summary>
        /// The id of the user that has the phone number
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

    }


}
