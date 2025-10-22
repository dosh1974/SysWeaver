using System;

namespace SysWeaver.MicroService
{
    public sealed class PhoneNumberInfo
    {
        /// <summary>
        /// Regional prefix (country code + any regional)
        /// </summary>
        public String Prefix;

        /// <summary>
        /// Local number
        /// </summary>
        public String Local;

        /// <summary>
        /// Name of the phone prefix (typically the country)
        /// </summary>
        public String Name;

        /// <summary>
        /// Two letter ISO-3166a2 country code of the highest ranked country
        /// </summary>
        public String IsoCountry;

        /// <summary>
        /// A list of all matched prefixes
        /// </summary>
        public PhonePrefixInfo[] Prefixes;
    }

}
