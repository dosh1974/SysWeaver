using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A service entry in a configuration (a service type, an optional instance name and optional parameters)
    /// </summary>
    public class ConfigEntry
    {

        /// <summary>
        /// Fully qualified type name of the service type
        /// </summary>
        public String Type;

        /// <summary>
        /// Optional name of the instance
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String Name;


        /// <summary>
        /// Get the parameters object for the service
        /// </summary>
        /// <returns>The parameters, or null if the service has none</returns>
        public virtual Object GetParams() => null;
    }



}
