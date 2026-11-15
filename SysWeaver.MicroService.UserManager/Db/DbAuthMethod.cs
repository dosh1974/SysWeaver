using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// Base class for the ways that a user can sign in (passwords and passkeys)
    /// </summary>
    [PartitionByKey(nameof(UserId))]
    public abstract class DbAuthMethod
    {

        /// <summary>
        /// The user id that this auth method belongs to
        /// </summary>
        [Index]
        [Required]
        public long UserId { get; set; }

        /// <summary>
        /// When the auth method was created
        /// </summary>
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
