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

        public String SiteName;

        public bool AllowEmailIps;
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
