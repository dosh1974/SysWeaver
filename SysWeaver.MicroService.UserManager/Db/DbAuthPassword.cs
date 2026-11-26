using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{


    /// <summary>
    /// The password of a user
    /// </summary>
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
        /// The SCRAM salt (base64), null if the password isn't stored as a verifier
        /// </summary>
        [StringLength(48)]
        [Ascii]
        public string ScramSalt { get; set; }

        /// <summary>
        /// The SCRAM (PBKDF2) iteration count
        /// </summary>
        public int ScramIterations { get; set; }

        /// <summary>
        /// The SCRAM stored key: ToBase64(SHA256(ClientKey)), null if the password isn't stored as a verifier.
        /// Can't be used to sign in.
        /// </summary>
        [StringLength(48)]
        [Ascii]
        public string StoredKey { get; set; }

        /// <summary>
        /// True if the password may only be used once
        /// </summary>
        [Required]
        public bool MustResetPassword { get; set; }
    }


}
