using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// A security token that is given to a user
    /// </summary>
    [Alias("Tokens")]
    [PartitionByKey(nameof(Token))]
    public sealed class DbToken
    {
        /// <summary>
        /// The id of the user that has the token
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

        /// <summary>
        /// The security token
        /// </summary>
        [PrimaryKey]
        [Required]
        [Ascii]
        [StringLength(48)]
        public string Token { get; set; }

    }


}
