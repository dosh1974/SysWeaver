using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request that is authenticated by proving that the caller knows the current password (without sending it)
    /// </summary>
    public class AutheticatedRequest
    {
        /// <summary>
        /// The one time pad used (from GetUserSalt)
        /// </summary>
        public String OneTimePad { get; set; }

        /// <summary>
        /// The SCRAM client proof (base64), see <see cref="SysWeaver.Auth.ScramInfo"/>.
        /// </summary>
        public String ClientProof { get; set; }

        /// <summary>
        /// The random client nonce used in the SCRAM auth message
        /// </summary>
        public String ClientNonce { get; set; }

        /// <summary>
        /// The user name of the (consumed) one time pad, set by the server when the pad is validated
        /// </summary>
        internal String PadUserName;

    }


}
