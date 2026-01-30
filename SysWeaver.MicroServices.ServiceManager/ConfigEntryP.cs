using System;

namespace SysWeaver.MicroService
{
    public class ConfigEntryP<T> : ConfigEntry
    {
        /// <summary>
        /// Parameters
        /// </summary>
        public T Params;


        public ConfigEntryP()
        {
        }

        public ConfigEntryP(T p)
        {
            Params = p;
        }

        public override Object GetParams() => Params;
    }



}
