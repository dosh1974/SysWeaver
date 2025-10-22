using System;


namespace SysWeaver.MicroService
{
    public sealed class NetworkServiceParams
    {
        /// <summary>
        /// Number of seconds to wait for a network connerction
        /// </summary>
        public int TimeOutSeconds = 30;

        /// <summary>
        /// True to throw an exception when no ip is found
        /// </summary>
        public bool FailIfNoIpFound = true;

        /// <summary>
        /// If non-null, the first number in the LAN ip must match this, valid values are: "192", "172", "10"
        /// </summary>
        public String MustStartWith = null;

        /*
        /// <summary>
        /// True to wait for internet connection
        /// </summary>
        public bool WaitForInternet = false;
        */
    }




}
