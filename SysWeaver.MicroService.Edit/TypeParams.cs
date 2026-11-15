using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="TypeService"/>
    /// </summary>
    public sealed class TypeParams
    {
        /// <inheritdoc />
        public override string ToString() =>
            String.Concat(
                base.ToString(), ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// An optional name of this instance (multiple instances of this service is strongly discouraged)
        /// </summary>
        public String InstanceName;

        /// <summary>
        /// Enable performance monitoring.
        /// Defaults to true.
        /// </summary>
        public bool PerMon = true;
    }


}
