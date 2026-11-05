using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Check a password reset code in any browser, see UserManagerService.ValidateResetCode.
    /// </summary>
    public sealed class ResetCodeRequest
    {
        /// <summary>
        /// The user id (e-mail address, phone number or user name), null or empty for the one this session asked a reset for
        /// </summary>
        public String UserId;

        /// <summary>
        /// The code, as in the message
        /// </summary>
        public String Code;
    }
}
