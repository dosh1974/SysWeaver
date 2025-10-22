using SysWeaver.Net.ExploreModule;
using System;

namespace SysWeaver.MicroService
{
    public sealed class ExploreHttpServerServiceParams : ExploreHttpServerModuleParams
    {
        public override string ToString() =>
            String.Concat(
                base.ToString(), ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// An optional name of this isntance (multiple instances of this service is strongly discouraged)
        /// </summary>
        public String InstanceName;
    }


}
