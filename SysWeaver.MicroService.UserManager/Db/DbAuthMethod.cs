using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    [PartitionByKey(nameof(UserId))]
    public abstract class DbAuthMethod
    {

        /// <summary>
        /// The user id that this auth method belongs to
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

        [Index]
        [Required]
        public DateTime Created { get; set; }

        /// <summary>
        /// The time when the auth method was last sucessfully used to login a user
        /// </summary>
        [Index]
        [Required]
        public DateTime LastUsed { get; set; }

    }


}
