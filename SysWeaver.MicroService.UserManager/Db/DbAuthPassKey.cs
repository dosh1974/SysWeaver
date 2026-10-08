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

        /// <summary>
        /// The user handle (user.id) that the credential was created with, null for credentials created before this was stored (the handle is then the user guid)
        /// </summary>
        public byte[] UserHandle { get; set; }

        /// <summary>
        /// The relying party id that the credential was created for, null if unknown
        /// </summary>
        [StringLength(253)]
        [Ascii]
        public string RpId { get; set; }

        /// <summary>
        /// The last seen signature counter (0 if the authenticator doesn't use a counter)
        /// </summary>
        public long SignCount { get; set; }

        /// <summary>
        /// Comma separated list of transports reported by the authenticator (ex: "internal,hybrid"), null if unknown
        /// </summary>
        [StringLength(128)]
        [Ascii]
        public string Transports { get; set; }

        /// <summary>
        /// True if the credential may be backed up (synced passkey)
        /// </summary>
        public bool BackupEligible { get; set; }

        /// <summary>
        /// True if the credential is backed up (synced passkey)
        /// </summary>
        public bool BackedUp { get; set; }

        /// <summary>
        /// The authenticator attestation guid (identifies the passkey provider), null if unknown
        /// </summary>
        [StringLength(36)]
        [Ascii]
        public string AaGuid { get; set; }

    }


}
