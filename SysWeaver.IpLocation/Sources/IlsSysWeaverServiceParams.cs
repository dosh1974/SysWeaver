using SysWeaver.Remote;

namespace SysWeaver.IpLocation.Sources
{
    /// <summary>
    /// Parameters for the <see cref="IlsSysWeaverService"/> ip location source (the connection to the other service)
    /// </summary>
    public sealed class IlsSysWeaverServiceParams : RemoteConnection
    {
        /// <summary>
        /// Create the parameters
        /// </summary>
        public IlsSysWeaverServiceParams()
        {
            //AuthMethod = RemoteAuthMethod.SysWeaverLogin;
        }

    }

}
