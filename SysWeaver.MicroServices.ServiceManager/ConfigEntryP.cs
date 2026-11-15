using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A service entry in a configuration with typed parameters
    /// </summary>
    /// <typeparam name="T">The type of the parameters</typeparam>
    public class ConfigEntryP<T> : ConfigEntry
    {
        /// <summary>
        /// Parameters
        /// </summary>
        public T Params;


        /// <summary>
        /// Create an entry with default parameters
        /// </summary>
        public ConfigEntryP()
        {
        }

        /// <summary>
        /// Create an entry with parameters
        /// </summary>
        /// <param name="p">The parameters</param>
        public ConfigEntryP(T p)
        {
            Params = p;
        }

        /// <inheritdoc />
        public override Object GetParams() => Params;
    }



}
