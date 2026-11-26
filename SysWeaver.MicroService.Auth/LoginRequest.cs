using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The parameters of a login (see <see cref="LoginService.Login"/>)
    /// </summary>
    public sealed class LoginRequest
    {
        /// <summary>
        /// The legacy proof, only used if <see cref="ClientProof"/> is null (and only accepted by authorizers that still allow it). Process:
        ///     temp = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, userSalt))))
        ///     Hash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', temp, OneTimePad))))
        /// </summary>
        public String Hash { get; set; }

        /// <summary>
        /// The one time pad used (from GetUserSalt)
        /// </summary>
        public String OneTimePad { get; set; }

        /// <summary>
        /// The SCRAM client proof (base64), used when GetUserSalt returned SCRAM parameters, see <see cref="SysWeaver.Auth.ScramInfo"/>.
        /// </summary>
        public String ClientProof { get; set; }

        /// <summary>
        /// The random client nonce used in the SCRAM auth message (user + "," + OneTimePad + "," + ClientNonce)
        /// </summary>
        public String ClientNonce { get; set; }
    }


  




}
