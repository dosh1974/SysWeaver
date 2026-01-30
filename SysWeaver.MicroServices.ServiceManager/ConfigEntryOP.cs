using System;

namespace SysWeaver.MicroService
{
    public class ConfigEntryOP<T> : ConfigEntry
    {
        /// <summary>
        /// Optional parameters
        /// </summary>
        [EditAllowNull]
        public T Params;

        public ConfigEntryOP()
        {
        }

        public ConfigEntryOP(T p)
        {
            Params = p;
        }

        public override Object GetParams() => Params;
    }



}
