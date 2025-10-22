using SysWeaver.Remote;

namespace SysWeaver.ExchangeRate.Sources
{
    public sealed class ErsSysWeaverServiceParams : RemoteConnection
    {

        /// <summary>
        /// Number of minutes between each refresh
        /// </summary>
        public int RefreshMinutes = 5;
    }

}
