using SysWeaver.Net;
using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for <see cref="StaticDataHttpServerService"/>: the module parameters plus the instance name.
    /// </summary>
    public sealed class StaticDataHttpServerServiceParams : StaticDataHttpServerModuleParams
    {

        public override string ToString() =>
            String.Concat(base.ToString(), ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// An optional name of this instance (multiple instances of this service are strongly discouraged)
        /// </summary>
        public String InstanceName;
    }

}
