using System;
using System.Globalization;

namespace SysWeaver.IpLocation
{
    /// <summary>
    /// Helpers for the ip location sources
    /// </summary>
    public static class IpLocationTools
    {
        /// <summary>
        /// Parse a number using the invariant culture
        /// </summary>
        /// <param name="s">The number as text, ex: "37.42240"</param>
        /// <returns>The number</returns>
        public static double ParseNumber(String s)
            => double.Parse(s, CultureInfo.InvariantCulture);
    }


}
