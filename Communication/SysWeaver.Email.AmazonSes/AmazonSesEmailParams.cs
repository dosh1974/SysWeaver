using System;


namespace SysWeaver.Email
{
    public sealed class AmazonSesEmailParams : CredentialParams
    {

        public override string ToString() => String.Concat(Region, ", From: ", From.ToQuoted(), String.IsNullOrEmpty(ConfigurationSet) ? ", Auth: " : ", Config set: " + ConfigurationSet.ToQuoted() + ", Auth: ", base.ToString());

        // User = AWS access key id, Password = AWS secret access key.
        // If no credentials are given (no CredFile, User and Password empty), the default AWS credential chain is used
        // (environment variables, ~/.aws/credentials profile, EC2/ECS instance role etc).

        /// <summary>
        /// The AWS region system name of the SES endpoint to use, ex: "eu-north-1", "us-east-1".
        /// The sender identity (From address or it's domain) must be verified in this region.
        /// Default: "us-east-1".
        /// </summary>
        public String Region { get; set; } = "us-east-1";

        /// <summary>
        /// The email address to send from, must be a verified identity (or belong to a verified domain) in SES.
        /// </summary>
        public String From { get; set; }

        /// <summary>
        /// Optional display name for the sender, ex: "My Service".
        /// </summary>
        public String FromName { get; set; }

        /// <summary>
        /// Optional SES configuration set name to use when sending (for event publishing, dedicated IP pools etc).
        /// </summary>
        public String ConfigurationSet { get; set; }

        /// <summary>
        /// Number of times to retry sending an email (before returning a failure).
        /// Requests rejected by SES (invalid address, unverified identity etc) are not retried.
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;
    }

}
