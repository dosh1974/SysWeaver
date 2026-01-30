using System;

namespace SysWeaver.MicroService
{
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


        public virtual Object GetParams() => null;
    }



}
