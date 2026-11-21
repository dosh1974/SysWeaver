using SysWeaver.Net;
using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for <see cref="AspHttpServerService"/>: the server parameters plus the service options.
    /// </summary>
    public sealed class AspHttpServerServiceParams : AspHttpServerParams
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

        /// <summary>
        /// Optional instance name of the translator (ITranslator) to use, null for the default instance
        /// </summary>
        public String TranslatorInstance;

        /// <summary>
        /// Optional instance name of the API audit service (IApiAuditService) to use, null for the default instance
        /// </summary>
        public String AuditInstance;

    }


}
