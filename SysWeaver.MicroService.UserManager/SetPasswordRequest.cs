using System;

namespace SysWeaver.MicroService
{
    public sealed class SetPasswordRequest : AutheticatedRequest
    {

        /// <summary>
        ///  NewHash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', newPassword, userSalt))))
        /// </summary>
        public String NewHash { get; set; }

    }


    /// <summary>
    /// Used when adding or changing a password (must be validated)
    /// </summary>
    public sealed class SetEmailRequest : AutheticatedRequest
    {
        /// <summary>
        /// New phone address
        /// </summary>
        public String Email { get; set; }
    }

    /// <summary>
    /// Used when adding or changing a password (must be validated)
    /// </summary>
    public sealed class SetPhoneRequest : AutheticatedRequest
    {
        /// <summary>
        /// New phone number
        /// </summary>
        public String Phone { get; set; }
    }

}
