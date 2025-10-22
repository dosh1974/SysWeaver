
namespace SysWeaver.MicroService
{
    /// <summary>
    /// What service to get
    /// </summary>
    public enum ServiceInstanceTypes
    {
        /// <summary>
        /// Any instance
        /// </summary>
        Any = 0,
        /// <summary>
        /// Local services only
        /// </summary>
        LocalOnly,
        /// <summary>
        /// Remote services only
        /// </summary>
        RemoteOnly,
        /// <summary>
        /// Local service first, then remote
        /// </summary>
        LocalOrRemote,
        /// <summary>
        /// Remote service first, then local
        /// </summary>
        RemoteAllLocal,
    }

}
