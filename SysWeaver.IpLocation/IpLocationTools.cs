using System;
using System.Globalization;

namespace SysWeaver.IpLocation
{
    public static class IpLocationTools
    {
        public static double ParseNumber(String s) 
            => double.Parse(s, CultureInfo.InvariantCulture);
    }


}
