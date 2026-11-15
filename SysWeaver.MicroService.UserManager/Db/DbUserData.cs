using System;
using SimpleStack.Orm.Attributes;
using SysWeaver.Auth;

namespace SysWeaver.MicroService.Db
{

    /// <summary>
    /// Base class for the (key, value) data that is stored per user
    /// </summary>
    public class DbUserDataBase
    {
        /// <summary>
        /// The guid of the user that owns this data
        /// </summary>
        [Ascii]
        [StringLength(AuhorizationLimits.MaxGuidLength)]
        [PrimaryKey]
        [Index]
        public String UserGuid { get; set; }

        /// <summary>
        /// The name of the data key
        /// </summary>
        [Ascii]
        [StringLength(UserManagerTools.MaxDataKeyLength)]
        [PrimaryKey]
        [Index]
        public String DataKey { get; set; }

    }


    /// <summary>
    /// Binary user data (serialized and compressed)
    /// </summary>
    [Alias("UserDataT")]
    [PartitionByKey(nameof(UserGuid))]
    public sealed class DbUserData : DbUserDataBase
    {
        /// <summary>
        /// The data, serialized and compressed
        /// </summary>
        public Byte[] Data { get; set; }

    }


    /// <summary>
    /// String user data (searchable)
    /// </summary>
    [Alias("UserDataString")]
    [PartitionByKey(nameof(UserGuid))]
    public sealed class DbUserDataString : DbUserDataBase
    {
        /// <summary>
        /// The string data (at most <see cref="UserManagerTools.MaxDataStringLength"/> characters)
        /// </summary>
        [StringLength(UserManagerTools.MaxDataStringLength)]
        [Index]
        public String Data { get; set; }

    }

}
