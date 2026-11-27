using System;
using System.Globalization;
using System.Linq;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The location of an external ip address (expanded into columns of the connection tables), empty if unknown
    /// </summary>
    [TableDataExpand("{0}", "")]
    public sealed class SmIpInfo
    {
        /// <summary>
        /// The two letter ISO 3166 country code, empty if unknown
        /// </summary>
        [TableDataOrder(50)]
        [TableDataIsoCountry]
        public String Country = "";

        /// <summary>
        /// The name of the country (in English), empty if unknown
        /// </summary>
        [TableDataOrder(51)]
        public String CountryName = "";

        /// <summary>
        /// The city, empty if unknown
        /// </summary>
        [TableDataOrder(52)]
        public String City = "";

        /// <summary>
        /// The region (state) code, empty if unknown
        /// </summary>
        [TableDataOrder(53)]
        [TableDataHide]
        public String Region = "";

        /// <summary>
        /// The autonomous system (network or organization) of the address, empty if unknown
        /// </summary>
        [TableDataOrder(54)]
        [TableDataText(32)]
        public String Network = "";

        /// <summary>
        /// A Google Maps link to the location, empty if the coordinates are unknown
        /// </summary>
        [TableDataOrder(55)]
        [TableDataUrl("Map", "+{0}", "Show the location on Google Maps")]
        public String Map = "";

        /// <summary>
        /// The service that provided the location, empty if unknown
        /// </summary>
        [TableDataOrder(56)]
        [TableDataHide]
        public String Source = "";

        /// <summary>
        /// The latitude in degrees (0 if unknown)
        /// </summary>
        [TableDataIgnore]
        public double Latitude;

        /// <summary>
        /// The longitude in degrees (0 if unknown)
        /// </summary>
        [TableDataIgnore]
        public double Longitude;

        /// <summary>
        /// An empty instance (unknown location)
        /// </summary>
        public static readonly SmIpInfo Unknown = new SmIpInfo();

        /// <summary>
        /// A short description, ex: "Stockholm, Sweden (SE), AS1257 Tele2", empty if unknown
        /// </summary>
        public override string ToString()
        {
            var c = Country.Length == 0 ? "" : (CountryName.Length > 0 ? String.Concat(CountryName, " (", Country, ")") : Country);
            return String.Join(", ", new[] { City, c, Network }.Where(x => !String.IsNullOrEmpty(x)));
        }

        /// <summary>
        /// Create from a location
        /// </summary>
        /// <param name="l">The location, null if unknown</param>
        /// <returns>The info, <see cref="Unknown"/> if the location is null</returns>
        public static SmIpInfo From(Location l)
        {
            if (l == null)
                return Unknown;
            var cc = (l.IsoCountry ?? "").Trim().ToUpperInvariant();
            var r = new SmIpInfo
            {
                Country = cc,
                CountryName = CountryNameOf(cc),
                City = l.City ?? "",
                Region = l.RegionCode ?? "",
                Network = l.AutonomousSystem ?? "",
                Source = l.Source ?? "",
                Latitude = l.Latitude,
                Longitude = l.Longitude,
            };
            if (((l.Latitude != 0) || (l.Longitude != 0)) && Double.IsFinite(l.Latitude) && Double.IsFinite(l.Longitude))
                r.Map = String.Concat("https://www.google.com/maps/search/?api=1&query=",
                    l.Latitude.ToString("0.#####", CultureInfo.InvariantCulture), "%2C", l.Longitude.ToString("0.#####", CultureInfo.InvariantCulture));
            return r;
        }

        /// <summary>
        /// Get the English name of a country
        /// </summary>
        /// <param name="isoCountry">The two letter ISO 3166 country code</param>
        /// <returns>The name, empty if unknown</returns>
        public static String CountryNameOf(String isoCountry)
        {
            if (String.IsNullOrEmpty(isoCountry) || (isoCountry.Length != 2))
                return "";
            try
            {
                return new RegionInfo(isoCountry).EnglishName;
            }
            catch
            {
                return "";
            }
        }
    }
}
