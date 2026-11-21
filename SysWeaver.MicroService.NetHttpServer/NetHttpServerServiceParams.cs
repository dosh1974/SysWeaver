using System;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for <see cref="NetHttpServerService"/>: the server parameters plus the service options.
    /// </summary>
    public sealed class NetHttpServerServiceParams : NetHttpServerParams
    {
        public override string ToString() =>
            String.Concat(
                base.ToString(), ", ",
                nameof(Start), ": ", Start, ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// True to start the server when the service is created
        /// </summary>
        public bool Start = true;

        /// <summary>
        /// An optional name of this instance (multiple instances of this service are strongly discouraged)
        /// </summary>
        public String InstanceName;

    }
}
