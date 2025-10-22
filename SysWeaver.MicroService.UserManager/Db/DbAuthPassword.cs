using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{


    [Alias("AuthPasswords")]
    [PartitionByKey(nameof(Id))]
    public sealed class DbAuthPassword : DbAuthMethod
    {
        /// <summary>
        /// Unique ID for this password
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }


        /// <summary>
        /// The password salt
        /// </summary>
        [Required]
        [StringLength(48)]
        [Ascii]
        public string Salt { get; set; }

        /// <summary>
        /// The password hash
        /// </summary>
        [Required]
        [StringLength(48)]
        [Ascii]
        public string Pwd { get; set; }

        /// <summary>
        /// True if the password may only be used once
        /// </summary>
        [Required]
        public bool MustResetPassword { get; set; }
    }


}
