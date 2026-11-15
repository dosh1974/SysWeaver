using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="SmtpEmailService"/>, the user name is also used as the from address
    /// </summary>
    public sealed class SmtpEmailParams : CredentialParams
    {

        /// <inheritdoc />
        public override string ToString() => String.Concat(Server, ':', Port, EnableSSL ? " [SSL], Auth: " : ", Auth: ", base.ToString());

        /// <summary>
        /// The smtp server address (DNS name or IP).
        /// </summary>
        public String Server { get; set; }

        /// <summary>
        /// The smtp server port number (typically 587).
        /// Default: 587.
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// True to enable SSL encryption (recommended).
        /// Default: True.
        /// </summary>
        public bool EnableSSL { get; set; } = true;

        /// <summary>
        /// Maximum number of attempts to send an email (including the first, minimum 1) before returning a failure.
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;
    }

}
