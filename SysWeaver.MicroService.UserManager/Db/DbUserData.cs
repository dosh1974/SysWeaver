using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;

namespace SysWeaver.MicroService.Db
{

    public class DbUserDataBase
    {
        /// <summary>
        /// The user that owns this data and the data key name
        /// </summary>
        [Ascii]
        [StringLength(AuhorizationLimits.MaxGuidLength)]
        [PrimaryKey]
        [Index]
        public String UserGuid { get; set; }

        /// <summary>
        /// The user that owns this data and the data key name
        /// </summary>
        [Ascii]
        [StringLength(UserManagerTools.MaxDataKeyLength)]
        [PrimaryKey]
        [Index]
        public String DataKey { get; set; }

    }


    [Alias("UserDataT")]
    [PartitionByKey(nameof(UserGuid))]
    public sealed class DbUserData : DbUserDataBase
    {
        /// <summary>
        /// The data, serialized and compressed
        /// </summary>
        public Byte[] Data { get; set; }

    }


    [Alias("UserDataString")]
    [PartitionByKey(nameof(UserGuid))]
    public sealed class DbUserDataString : DbUserDataBase
    {
        [StringLength(UserManagerTools.MaxDataStringLength)]
        [Index]
        public String Data { get; set; }

    }

}
