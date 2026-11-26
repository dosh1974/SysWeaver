using System;
using System.Globalization;

namespace SysWeaver.Net
{
    /// <summary>
    /// The estimated location of an ip address
    /// </summary>
    public sealed class Location
    {
        /// <inheritdoc />
        public override string ToString() => String.Create(CultureInfo.InvariantCulture, $"{IsoCountry} {Latitude}, {Longitude}\n{Address}");

        /// <summary>
        /// The two letter ISO 3166 country code
        /// </summary>
        public String IsoCountry;
        /// <summary>
        /// The latitude in degrees
        /// </summary>
        public Double Latitude;
        /// <summary>
        /// The longitude in degrees
        /// </summary>
        public Double Longitude;
        /// <summary>
        /// A multi line address (zip code, city, region, country...), as far as it is known
        /// </summary>
        public String Address;
        /// <summary>
        /// The region (state) code, null if unknown
        /// </summary>
        public String RegionCode;
        /// <summary>
        /// The autonomous system (or organization) of the ip address, null if unknown
        /// </summary>
        public String AutonomousSystem;
        /// <summary>
        /// The name of the source that provided this information
        /// </summary>
        public String Source;
        /// <summary>
        /// When the information was collected from the source (UTC)
        /// </summary>
        public DateTime Sourced;
        /// <summary>
        /// The city, null if unknown
        /// </summary>
        public String City;


        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public Location()
        {
        }

        /// <summary>
        /// Create an instance with all values
        /// </summary>
        /// <param name="isoCountry">The two letter ISO 3166 country code</param>
        /// <param name="latitude">The latitude in degrees</param>
        /// <param name="longitude">The longitude in degrees</param>
        /// <param name="address">A multi line address</param>
        /// <param name="regionCode">The region (state) code</param>
        /// <param name="autonomousSystem">The autonomous system (or organization)</param>
        /// <param name="source">The name of the source of the information</param>
        /// <param name="sourced">When the information was collected (UTC)</param>
        /// <param name="city">The city</param>
        public Location(string isoCountry, double latitude, double longitude, string address, string regionCode, string autonomousSystem, string source, DateTime sourced, String city)
        {
            IsoCountry = isoCountry;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            RegionCode = regionCode;
            AutonomousSystem = autonomousSystem;
            Source = source;
            Sourced = sourced;
            City = city;
        }

        /// <summary>
        /// Create a copy of another instance
        /// </summary>
        /// <param name="c">The instance to copy</param>
        public Location(Location c)
        {
            IsoCountry = c.IsoCountry;
            Latitude = c.Latitude;
            Longitude = c.Longitude;
            Address = c.Address;
            RegionCode = c.RegionCode;
            AutonomousSystem = c.AutonomousSystem;
            Source = c.Source;
            Sourced = c.Sourced;
            City = c.City;
        }
    }


}
