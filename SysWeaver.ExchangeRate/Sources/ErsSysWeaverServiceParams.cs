using SysWeaver.Remote;

namespace SysWeaver.ExchangeRate.Sources
{
    /// <summary>
    /// Parameters for the <see cref="ErsSysWeaverService"/> rate source (the connection to the other service)
    /// </summary>
    public sealed class ErsSysWeaverServiceParams : RemoteConnection
    {

        /// <summary>
        /// Create the parameters
        /// </summary>
        public ErsSysWeaverServiceParams()
        {
            //AuthMethod = RemoteAuthMethod.SysWeaverLogin;
        }

        /// <summary>
        /// Number of minutes between each refresh (at least 1).
        /// Defaults to 5.
        /// </summary>
        public int RefreshMinutes = 5;
    }

}
