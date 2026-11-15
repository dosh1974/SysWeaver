using System;

namespace SysWeaver.Net.ExploreModule
{
    /// <summary>
    /// Parameters for <see cref="ExploreHttpServerModule"/>.
    /// </summary>
    public class ExploreHttpServerModuleParams : BaseHttpServerModuleParams
    {

        /// <summary>
        /// Create the parameters, the default <see cref="BaseHttpServerModuleParams.Auth"/> is "Debug,Dev,Ops".
        /// </summary>
        public ExploreHttpServerModuleParams()
        {
            Auth = "Debug,Dev,Ops";
        }
            
        /// <summary>
        /// The performance monitoring setting.
        /// </summary>
        /// <returns>A short text for debugging.</returns>
        public override string ToString() => String.Concat(
            nameof(PerMon), ": ", PerMon);

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;
    }

}
