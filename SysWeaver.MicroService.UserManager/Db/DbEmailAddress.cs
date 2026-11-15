using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// The e-mail address of a user
    /// </summary>
    [Alias("Emails")]
    [PartitionByKey(nameof(Email))]
    public sealed class DbEmailAddress
    {

        /// <summary>
        /// The e-mail address (case insensitive)
        /// </summary>
        [PrimaryKey]
        [StringLength(AuhorizationLimits.MaxUserNameLength)]
        [Required]
        [CaseInsensitive]
        public string Email { get; set; }


        /// <summary>
        /// The id of the user that has the address
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

    }


}
