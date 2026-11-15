using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="NetworkService"/>
    /// </summary>
    public sealed class NetworkServiceParams
    {
        /// <summary>
        /// Number of seconds to wait for a LAN ip (at least 5 seconds is used)
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

        /// <summary>
        /// True to wait for internet connection
        /// </summary>
        public bool WaitForInternet = false;

        /// <summary>
        /// Number of seconds to wait for an internet connection (only used if <see cref="WaitForInternet"/> is true, 0 or less skips the wait)
        /// </summary>
        public int InternetTimeOutSeconds = 60;

        /// <summary>
        /// True to throw an exception when no internet is found
        /// </summary>
        public bool FailIfNoInternetFound = true;

    }




}
