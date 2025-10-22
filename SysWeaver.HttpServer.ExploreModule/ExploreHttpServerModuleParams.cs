using System;

namespace SysWeaver.Net.ExploreModule
{
    public class ExploreHttpServerModuleParams : BaseHttpServerModuleParams
    {

        public ExploreHttpServerModuleParams()
        {
            Auth = "Debug,Dev,Ops";
        }
            
        public override string ToString() => String.Concat(
            nameof(PerMon), ": ", PerMon);

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;
    }

}
