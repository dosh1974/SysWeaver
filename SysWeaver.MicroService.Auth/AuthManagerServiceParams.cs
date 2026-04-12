using SysWeaver.Auth;
using System;

namespace SysWeaver.MicroService
{
    public sealed class AuthManagerServiceParams : AuthManagerParams
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
        /// Used for basic auth
        /// </summary>
        public String SiteName;

        /// <summary>
        /// If true, allow IP address in email adresses
        /// </summary>
        public bool AllowEmailIps;

        /// <summary>
        /// If true, generate transparent user images
        /// </summary>
        public bool TransparentUserImage;

    }


    public sealed class RecoverMailParams : ManagedMailMessage
    {
        public RecoverMailParams()
        {
            Subject = "Password recovery for [Site]";
            Body = "data.PasswordRecovery.txt";
        }
    }


}
