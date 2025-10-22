using SysWeaver.Net;
using System;

namespace SysWeaver.MicroService
{
    public sealed class AspHttpServerServiceParams : AspHttpServerParams
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

        /// <summary>
        /// Optional translator instance name 
        /// </summary>
        public String TranslatorInstance;

        /// <summary>
        /// Optional audit instance name 
        /// </summary>
        public String AuditInstance;

    }


}
