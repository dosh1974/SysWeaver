using System;
using System.Globalization;

namespace SysWeaver.IpLocation
{
    public sealed class IpLocation
    {
        public override string ToString() => String.Concat(IsoCountry, ' ', Latitude.ToString(CultureInfo.InvariantCulture), ", ", Longitude.ToString(CultureInfo.InvariantCulture), "\n", Address);

        public String IsoCountry;
        public Double Latitude;
        public Double Longitude;
        public String Address;
        public String RegionCode;
        public String AutonomousSystem;
        public String Source;
        public DateTime Sourced;

        public IpLocation()
        {
        }

        public IpLocation(string isoCountry, double latitude, double longitude, string address, string regionCode, string autonomousSystem, string source, DateTime sourced)
        {
            IsoCountry = isoCountry;
            Latitude = latitude;
            Longitude = longitude;
            Address = address;
            RegionCode = regionCode;
            AutonomousSystem = autonomousSystem;
            Source = source;
            Sourced = sourced;
        }

        public IpLocation(IpLocation c)
        {
            IsoCountry = c.IsoCountry;
            Latitude = c.Latitude;
            Longitude = c.Longitude;
            Address = c.Address;
            RegionCode = c.RegionCode;
            AutonomousSystem = c.AutonomousSystem;
            Source = c.Source;
            Sourced = c.Sourced;
        }
    }


}
