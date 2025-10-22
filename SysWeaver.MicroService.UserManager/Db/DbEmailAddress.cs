using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;

namespace SysWeaver.MicroService.Db
{
    [Alias("Emails")]
    [PartitionByKey(nameof(Email))]
    public sealed class DbEmailAddress
    {

        [PrimaryKey]
        [StringLength(AuhorizationLimits.MaxUserNameLength)]
        [Required]
        [CaseInsensitive]
        public string Email { get; set; }


        [Index]
        [Required]
        public long UserId { get; set; }

    }


}
