using SimpleStack.Orm.Attributes;
using System;
using System.Globalization;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Caches
{
    /// <summary>
    /// A cached ip location (a row in the database)
    /// </summary>
    [Alias("IpLocations")]
    [PartitionByKey(nameof(Ip))]
    public sealed class DbIpLocation
    {
        /// <summary>
        /// Geo located IP address
        /// </summary>
        [PrimaryKey]
        [Required]
        [Ascii]
        [StringLength(40)]
        [TableDataIp]
        public String Ip { get; set; }

        /// <summary>
        /// When it was added to the cache
        /// </summary>
        [Required]
        [Index]
        public DateTime Added { get; set; }

        /// <summary>
        /// The location as json, null if no location was found for the ip address
        /// </summary>
        [StringLength(16384)]
        [TableDataOrder(100)]
        [TableDataJson]
        public String Data { get; set; }

        /// <summary>
        /// The position as "latitude,longitude" (used to show a map image)
        /// </summary>
        [TableDataGoogleMapsPlaceImage]
        [Ignore]
        public String Map
        {
            get
            {
                var l = Loc;
                if (l == null)
                    return null;
                return String.Create(CultureInfo.InvariantCulture, $"{l.Latitude},{l.Longitude}");
            }
        }

        /// <summary>
        /// The ISO country code (shown as a flag)
        /// </summary>
        [TableDataIsoCountryImage]
        [Ignore]
        public String Flag => Loc?.IsoCountry;

        /// <summary>
        /// Address
        /// </summary>
        [Ignore]
        [TableDataText(60)]
        public String Address => Loc?.Address;


        /// <summary>
        /// City
        /// </summary>
        [Ignore]
        [TableDataText(60)]
        public String City => Loc?.City;


        /// <summary>
        /// What Autonomous System provided this information
        /// </summary>
        [Ignore]
        public String AS => Loc?.AutonomousSystem;

        /// <summary>
        /// What source provided this information
        /// </summary>
        [Ignore]
        public String Source => Loc?.Source;


        Location Loc
        {
            get
            {
                var l = _Loc;
                if (l != null)
                    return l;
                var d = Data;
                if (d == null)
                    return null;
                l = SerManager.GetText("json").FromString<Location>(d);
                _Loc = l;
                return l;
            }
        }

        Location _Loc;

    }
}
