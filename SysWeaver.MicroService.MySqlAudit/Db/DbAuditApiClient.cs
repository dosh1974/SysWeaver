using SimpleStack.Orm;
using SimpleStack.Orm.Attributes;
using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService.Db
{

    [TableDataPrimaryKey(nameof(Ip), nameof(DeviceId))] // Additional primary key (only used in Charts)

    [Alias("AuditApiClients")]
    sealed class DbAuditApiClient
    {
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        /// <summary>
        /// Ip address of the client
        /// </summary>
        [Index]
        [Required]
        [Ascii]
        [StringLength(40)]
        [TableDataIp]
        public string Ip { get; set; }

        /// <summary>
        /// Country flag associated with the language
        /// </summary>
        [TableDataIsoLanguageImage]
        [Ignore]
        public string Country => Language;


        /// <summary>
        /// Language code of the client
        /// </summary>
        [Index]
        [Ascii]
        [StringLength(8)]
        public string Language { get; set; }

        /// <summary>
        /// Time zone used by the client
        /// </summary>
        [Index]
        [Ascii]
        [StringLength(32)]
        [TableDataGoogleSearch("{0}", "Information about \"{0}\" time zone")]
        public string TimeZone { get; set; }


        /// <summary>
        /// The device Id
        /// </summary>
        [Index]
        [StringLength(40)]
        [Ascii]
        [Required]
        public string DeviceId { get; set; }


        /// <summary>
        /// Number of times an API was called
        /// </summary>
        [Index]
        [Required]
        [UpdateAggregate(UpdateAggregations.Add)]
        public long Count { get; set; }

        /// <summary>
        /// Id of the last API call made
        /// </summary>
        [Required]
        [UpdateAggregate(UpdateAggregations.Set)]
        [TableDataUrl("{0}", "*table.html?q=../Api/Audit/ApiCallTable&p={1}&n=Api Calls for client %23{1}&s={\"RequestParams\":{\"Order\":[\"-" + nameof(DbAuditApiCall.Id) + "\"]}}", "Click to show the last API calls made by this client")]
        public long LastId { get; set; }

        [TableDataHide]
        [Ignore]
        public long Id3 => Id;

        /// <summary>
        /// Last used an API time stamp
        /// </summary>
        [Index]
        [Required]
        [UpdateAggregate(UpdateAggregations.Max)]
        public DateTime Last { get; set; }


        /// <summary>
        /// Id of the first API call made
        /// </summary>
        [Required]
        [UpdateAggregate(UpdateAggregations.None)]
        [TableDataUrl("{0}", "*table.html?q=../Api/Audit/ApiCallTable&p={1}&n=Api Calls for client %23{1}&s={\"RequestParams\":{\"Order\":[\"" + nameof(DbAuditApiCall.Id) + "\"]}}", "Click to show the first API calls made by this client")]
        public long FirstId { get; set; }


        [TableDataHide]
        [Ignore]
        public long Id2 => Id;



        /// <summary>
        /// First used an API time stamp
        /// </summary>
        [Index]
        [Required]
        [UpdateAggregate(UpdateAggregations.Min)]
        public DateTime First { get; set; }


        /// <summary>
        /// User agent string
        /// </summary>
        [StringLength(4096)]
        [Required]
        [TableDataUserAgent]
        public string UserAgent { get; set; }




    }
}
