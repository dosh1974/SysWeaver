using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// Name / value pairs about the user database itself (ex: the fingerprint of the data protection secret used for the protected values).
    /// </summary>
    [Alias("UserManagerMeta")]
    public sealed class DbUserManagerMeta
    {
        /// <summary>
        /// The name of the value
        /// </summary>
        [PrimaryKey]
        [StringLength(64)]
        [Ascii]
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The value
        /// </summary>
        [StringLength(256)]
        public string Value { get; set; }
    }
}
