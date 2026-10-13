using System;


namespace SysWeaver.Email
{
    public sealed class MicrosoftAcsEmailParams : ApiKeyParams
    {

        public override string ToString() => String.Concat(String.IsNullOrEmpty(Endpoint) ? "" : Endpoint + ", ", "From: ", From.ToQuoted(), ", Auth: ", base.ToString());

        // ApiKey = either the full ACS connection string ("endpoint=https://...;accesskey=..."),
        // or just the access key (then the Endpoint must be specified).

        /// <summary>
        /// The ACS resource endpoint, ex: "https://my-acs.europe.communication.azure.com".
        /// Only required if the ApiKey (or the content of the CredFile) is an access key and not a full connection string.
        /// </summary>
        public String Endpoint { get; set; }

        /// <summary>
        /// The email address to send from, must be a MailFrom address of an email domain connected to the ACS resource.
        /// Ex: "DoNotReply@xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.azurecomm.net" (Azure managed domain) or "noreply@example.com" (custom domain).
        /// The sender display name is configured on the MailFrom address in Azure.
        /// </summary>
        public String From { get; set; }

        /// <summary>
        /// If true, wait until ACS reports the email as delivered to the outbound mail server (or failed), this polls the operation status and can take seconds.
        /// If false, return as soon as ACS has accepted the email for sending.
        /// Default: false.
        /// </summary>
        public bool WaitForCompletion { get; set; }

        /// <summary>
        /// Number of times to retry sending an email (before returning a failure).
        /// Requests rejected by ACS (invalid address, unknown sender etc) are not retried.
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;
    }

}
