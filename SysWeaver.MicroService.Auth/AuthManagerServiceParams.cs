using SysWeaver.Auth;
using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="AuthManagerService"/>
    /// </summary>
    public sealed class AuthManagerServiceParams : AuthManagerParams
    {
        /// <inheritdoc />
        public override string ToString() => 
            String.Concat(
                base.ToString(), ", ",
                nameof(InstanceName), ": ", InstanceName.ToQuoted());

        /// <summary>
        /// An optional name of this isntance (multiple instances of this service is strongly discouraged)
        /// </summary>
        public String InstanceName;

        /// <summary>
        /// Name of the site, used for basic auth and generated user images, defaults to the name of the executable
        /// </summary>
        public String SiteName;

        /// <summary>
        /// If true, allow IP address in email adresses
        /// </summary>
        public bool AllowEmailIps;

        /// <summary>
        /// If true, generated default user images are transparent, otherwise (default) they are opaque
        /// </summary>
        public bool TransparentUserImage;

    }


    /// <summary>
    /// The password recovery mail
    /// </summary>
    public sealed class RecoverMailParams : ManagedMailMessage
    {
        /// <summary>
        /// Sets the default subject and body (the "data.PasswordRecovery.txt" resource)
        /// </summary>
        public RecoverMailParams()
        {
            Subject = "Password recovery for [Site]";
            Body = "data.PasswordRecovery.txt";
        }
    }


}
