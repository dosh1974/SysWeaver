using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// A user
    /// </summary>
    [Alias("Users")]
    //[PartitionByKey(nameof(Id), nameof(UserName))]
    public sealed class DbUser
    {
        /// <summary>
        /// Unqiue user id
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        /// <summary>
        /// When the user was created
        /// </summary>
        [Index]
        [Required]
        public DateTime Created { get; set; }


        /// <summary>
        /// Icon, this only works if the UserManasger instance is the default "UM"
        /// </summary>
        [Ignore]
        [TableDataUserIcon]
        public String Icon => UserManagerService.MakeGuid(Id, "UM").ToHex();
        
        
        /// <summary>
        /// The user name (not stored in plain text: protected in <see cref="NameKey"/> and <see cref="NameEnc"/>, filled when the row is read by the user manager)
        /// </summary>
        [Ignore]
        public string UserName { get; set; }

        /// <summary>
        /// The domain that the user belongs to (the meaning may be application specific)
        /// </summary>
        [Index]
        [StringLength(AuhorizationLimits.MaxDomainName)]
        public string Domain { get; set; }

        /// <summary>
        /// The language preference for the user
        /// </summary>
        [Index]
        [Ascii]
        [StringLength(8)]
        public string Language { get; set; }


        /// <summary>
        /// Nick name (not stored in plain text: protected in <see cref="NickEnc"/>, filled when the row is read by the user manager)
        /// </summary>
        [Ignore]
        public string NickName { get; set; }

        /// <summary>
        /// The random value of the row, used to encrypt the user name and nick name (replaced on every change), see <see cref="SysWeaver.Auth.DataProtection"/>
        /// </summary>
        [TableDataIgnore]
        [Required]
        public byte[] Rnd { get; set; }

        /// <summary>
        /// The lookup hash of the normalized (trimmed, lower case) user name as lower case hex (unique, used to find a user by name)
        /// </summary>
        [TableDataIgnore]
        [Index(true)]
        [Required]
        [Ascii]
        [StringLength(64)]
        public string NameKey { get; set; }

        /// <summary>
        /// The encrypted user name (original casing)
        /// </summary>
        [TableDataIgnore]
        [Required]
        public byte[] NameEnc { get; set; }

        /// <summary>
        /// The encrypted nick name, null if none
        /// </summary>
        [TableDataIgnore]
        public byte[] NickEnc { get; set; }
    }

}
