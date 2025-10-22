using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    [Alias("Tokens")]
    [PartitionByKey(nameof(Token))]
    public sealed class DbToken
    {
        [Index]
        [Required]
        public long UserId { get; set; }

        [PrimaryKey]
        [Required]
        [Ascii]
        [StringLength(48)]
        public string Token { get; set; }

    }


}
