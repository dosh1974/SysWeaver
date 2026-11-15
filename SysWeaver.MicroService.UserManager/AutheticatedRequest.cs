using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request that is authenticated by proving that the caller knows the current password (without sending it)
    /// </summary>
    public class AutheticatedRequest
    {
        /// <summary>
        /// The hash of the current password. Process:
        ///     temp = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, userSalt))))
        ///     Hash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', temp, OneTimePad))))
        /// </summary>
        public String Hash { get; set; }

        /// <summary>
        /// The one time pad used (from GetUserSalt)
        /// </summary>
        public String OneTimePad { get; set; }

    }


}
