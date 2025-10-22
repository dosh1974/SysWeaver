using System;

namespace SysWeaver.MicroService
{
    public sealed class TypeParams 
    {
        public override string ToString() =>
            String.Concat(
                base.ToString(), ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// An optional name of this isntance (multiple instances of this service is strongly discouraged)
        /// </summary>
        public String InstanceName;

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;
    }


}
