using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    [Alias("PhoneNumbers")]
    [PartitionByKey(nameof(Phone))]
    public sealed class DbPhoneNumber
    {
        [PrimaryKey]
        [StringLength(64)]
        [Required]
        [Ascii]
        public string Phone { get; set; }

        [Index]
        [Required]
        public long UserId { get; set; }

    }


}
