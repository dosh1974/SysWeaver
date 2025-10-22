using System;

namespace SysWeaver.MicroService
{
    public class AutheticatedRequest
    {
        /// <summary>
        /// Process: 
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
