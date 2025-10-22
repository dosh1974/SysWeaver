using SimpleStack.Orm.Attributes;
using System;
using System.Globalization;
using SysWeaver.Data;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Caches
{
    [Alias("IpLocations")]
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
        /// The data as json
        /// </summary>
        [StringLength(16384)]
        [TableDataOrder(100)]
        [TableDataJson]
        public String Data { get; set; }

        /// <summary>
        /// Link to map
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
                return String.Concat(l.Latitude.ToString(CultureInfo.InvariantCulture), ',', l.Longitude.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Country
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
        /// What Autonomous System provided this information
        /// </summary>
        [Ignore]
        public String AS => Loc?.AutonomousSystem;

        /// <summary>
        /// What source provided this information
        /// </summary>
        [Ignore]
        public String Source => Loc?.Source;


        IpLocation Loc
        {
            get
            {
                var l = _Loc;
                if (l != null)
                    return l;
                var d = Data;
                if (d == null)
                    return null;
                l = SerManager.GetText("json").FromString<IpLocation>(d);
                _Loc = l;
                return l;
            }
        }

        IpLocation _Loc;

    }
}
