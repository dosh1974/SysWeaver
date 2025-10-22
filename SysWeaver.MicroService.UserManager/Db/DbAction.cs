using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    [Alias("Actions")]
    [PartitionByKey(nameof(Token))]
    public sealed class DbAction
    {
        [PrimaryKey]
        [StringLength(48)]
        [Required]
        [Ascii]
        public string Token { get; set; }

        [StringLength(8)]
        [Required]
        [Ascii]
        public string Type { get; set; }

        [Index]
        [Required]
        public DateTime Expiration { get; set; }

        [Required]
        public byte[] Data { get; set; }



        public const string ResetPassword = "RstPwd";
        public const string DeleteUser = "DelUsr";
        public const string AddPassword = "AddPwd";
        public const string CreateUser = "NewUsr";
        public const string DeletePassword = "DelPwd";

        public const string AddEmail = "AddEmail";
        public const string AddPhone = "AddPhone";
    }


}
