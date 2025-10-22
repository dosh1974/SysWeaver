using System;

namespace SysWeaver.MicroService
{
    public sealed class PhonePrefixInfo
    {
        /// <summary>
        /// Name of phone prefix.
        /// </summary>
        public String Name;

        /// <summary>
        /// International dialing prefix.
        /// </summary>
        public String CountryCode;

        /// <summary>
        /// Any region prefixes (can be multiple separated by a comma)
        /// </summary>
        public String RegionPrefixes;

        /// <summary>
        /// Two letter ISO-3166a2 country code, null means that this is not a country.
        /// </summary>
        public String IsoCountry;
    }

}
