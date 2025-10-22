using SimpleStack.Orm.Attributes;
using System;
using SysWeaver.Auth;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{

    [TableDataPrimaryKey(nameof(Api))] // Additional primary key (only used in Charts)
    [Alias("AuditApiCalls")]
    sealed class DbAuditApiCall
    {
        /// <summary>
        /// Unique id, one for each API call
        /// </summary>
        [PrimaryKey]
        public long Id { get; set; }

        /// <summary>
        /// Name of the API
        /// </summary>
        [Required]
        [Index]
        [Ascii]
        [StringLength(128)]
        public string Api { get; set; }


        /// <summary>
        /// Api Group
        /// </summary>
        [Required]
        [Index]
        [Ascii]
        [StringLength(32)]
        public string Group { get; set; }


        /// <summary>
        /// State, 0 = In progress, 1 = Completed, 2 = Exception
        /// </summary>
        [Required]
        [Index]
        public byte State { get; set; }

        /// <summary>
        /// Time when the execution began
        /// </summary>
        [Required]
        [Index]
        public DateTime Begin { get; set; }

        /// <summary>
        /// Time when the execution completed
        /// </summary>
        [Index]
        public DateTime End { get; set; }

        /// <summary>
        /// The (capped) input data as json, null if the API is a void method
        /// </summary>
        [StringLength(768)]
        [Index]
        [TableDataJson]
        public string Input { get; set; }


        /// <summary>
        /// The (capped) output data as json, null if the API is a void method, the plain text Exception message if State equals 2
        /// </summary>
        [StringLength(768)]
        [Index]
        [TableDataJson]
        public string Output { get; set; }

        /// <summary>
        /// User icon
        /// </summary>
        [TableDataUserIcon]
        [Ignore]
        public String Icon => UserGuid.ToHex();

        /// <summary>
        /// The name of the user that performed this action (can be null if the API was called without any user logged in)
        /// </summary>
        [StringLength(AuhorizationLimits.MaxUserNameLength)]
        [Index]
        public string UserName { get; set; }


        /// <summary>
        /// The guid of the user that performed this action (can be null if the API was called without any user logged in)
        /// </summary>
        [StringLength(AuhorizationLimits.MaxGuidLength)]
        [Index]
        [Ascii]
        public string UserGuid { get; set; }

        /// <summary>
        /// The session that invoked this API
        /// </summary>
        [Index]
        [Ascii]
        [StringLength(32)]
        [TableDataUrl("{0}", "*table.html?q=../Api/debug/ActiveSessions&f={\"Invert\":false,\"CaseSensitive\":false,\"Op\":6,\"Value\":\"{0}\",\"ColName\":\"Token\",\"ColumnIndex\":0}", "Click to show session information if it's still active")]
        public String Session { get; set; }

        /// <summary>
        /// The client used for the API call
        /// </summary>
        [Index]
        [Required]
        [TableDataUrl("{0}", "*table.html?q=../Api/Audit/ClientTable&n=Client %23{0}&p={0}", "Click to show the first API call")]
        public long ClientId { get; set; }




    }
}
