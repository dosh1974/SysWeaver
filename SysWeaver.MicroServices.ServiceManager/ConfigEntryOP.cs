using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A service entry in a configuration with optional (nullable) typed parameters
    /// </summary>
    /// <typeparam name="T">The type of the parameters</typeparam>
    public class ConfigEntryOP<T> : ConfigEntry
    {
        /// <summary>
        /// Optional parameters
        /// </summary>
        [EditAllowNull]
        public T Params;

        /// <summary>
        /// Create an entry without parameters
        /// </summary>
        public ConfigEntryOP()
        {
        }

        /// <summary>
        /// Create an entry with parameters
        /// </summary>
        /// <param name="p">The parameters</param>
        public ConfigEntryOP(T p)
        {
            Params = p;
        }

        /// <inheritdoc />
        public override Object GetParams() => Params;
    }



}
