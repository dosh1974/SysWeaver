using System;
using SimpleStack.Orm.Attributes;

namespace SysWeaver.MicroService.Db
{
    [Alias("AuthPasskeys")]
    [PartitionByKey(nameof(CredentialId))]
    public sealed class DbAuthPassKey : DbAuthMethod
    {
        /// <summary>
        /// The pass key credential id
        /// </summary>
        [PrimaryKey]
        [Ascii]
        [StringLength(1368)]
        [Required]
        public string CredentialId { get; set; }


        /// <summary>
        /// An optional device id for this credential, used where discoverable credentials isn't available
        /// </summary>
        [StringLength(64)]
        [Ascii]
        [Index]
        public string DeviceId { get; set; }


        /// <summary>
        /// An optional device name for this credential
        /// </summary>
        [StringLength(64)]
        public string DeviceName { get; set; }

        /// <summary>
        /// The public key
        /// </summary>
        [Required]
        public byte[] PublicKey { get; set; }

    }


}
