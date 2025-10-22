using System;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed class NetHttpServerServiceParams : NetHttpServerParams
    {
        public override string ToString() =>
            String.Concat(
                base.ToString(), ", ",
                nameof(Start), ": ", Start, ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// Tru to start the service
        /// </summary>
        public bool Start = true;

        /// <summary>
        /// An optional name of this isntance (multiple instances of this service is strongly discouraged)
        /// </summary>
        public String InstanceName;

    }
}
