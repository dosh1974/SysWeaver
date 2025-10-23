using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{
    [Alias("Users")]
    //[PartitionByKey(nameof(Id))]
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
        /// The user name
        /// </summary>
        [Index(true)]
        [Required]
        [StringLength(AuhorizationLimits.MaxUserNameLength)]
        [CaseInsensitive]
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
        /// Nick name
        /// </summary>
        [StringLength(AuhorizationLimits.MaxNickNameLength)]
        public string NickName { get; set; }
    }

}
