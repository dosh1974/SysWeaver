using System;

namespace SysWeaver.TextMessage
{
    // User = AWS access key id, Password = AWS secret access key.
    // If no credentials are given (no CredFile, User and Password empty), the default AWS credential chain is used
    // (environment variables, ~/.aws/credentials profile, EC2/ECS instance role etc).

    public sealed class AmazonEndUserMessagingParams : CredentialParams
    {
        public override string ToString() => String.Concat(Region, ", ", Senders?.Length ?? 0, " sender(s), ", QueueUrls?.Length ?? 0, " queue(s), Auth: ", base.ToString());

        /// <summary>
        /// The AWS region system name, ex: "eu-north-1", "us-east-1".
        /// Phone numbers, sender ids, configuration sets etc are per region.
        /// Default: "us-east-1".
        /// </summary>
        public String Region = "us-east-1";

        /// <summary>
        /// The senders (origination identities) to use.
        /// If empty, AWS selects the origination identity (the default sender id / phone number of the account).
        /// </summary>
        public AmazonSenderParams[] Senders;

        /// <summary>
        /// Optional configuration set name, required to receive delivery events (the configuration set must have an SNS event destination, see the README).
        /// </summary>
        public String ConfigurationSetName;

        /// <summary>
        /// Urls of SQS queues that are subscribed to the SNS topics for two-way SMS (incoming messages) and delivery events.
        /// One queue can receive both. If empty, no messages or status updates are received.
        /// Ex: "https://sqs.eu-north-1.amazonaws.com/123456789012/sms-events".
        /// </summary>
        public String[] QueueUrls;

        /// <summary>
        /// If true, recipient numbers are looked up (CarrierLookup, a paid API call) before sending, landlines fails with NotOnChannel and invalid numbers with InvalidNumber.
        /// The result is cached for LookupCacheHours.
        /// Default: false.
        /// </summary>
        public bool CarrierLookup;

        /// <summary>
        /// The number of hours to cache carrier lookups and NotOnChannel failures.
        /// Default: 24.
        /// </summary>
        public int LookupCacheHours = 24;

        /// <summary>
        /// Optional India DLT entity id (sent as IN_ENTITY_ID for recipients in India, the template id is OutgoingTextMessage.Template.Name).
        /// </summary>
        public String IndiaEntityId;

        /// <summary>
        /// Optional maximum price per message part, in US dollars, ex: "0.10".
        /// </summary>
        public String MaxPrice;

        /// <summary>
        /// Optional SMS Protect configuration id.
        /// </summary>
        public String ProtectConfigurationId;

        /// <summary>
        /// Number of times to try sending a message (the AWS SDK retries throttling and temporary errors).
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;

        /// <summary>
        /// Optional service url of AWS End User Messaging SMS (ex: a VPC endpoint), if not specified the url of the region is used.
        /// </summary>
        public String ServiceUrl;

        /// <summary>
        /// Optional service url of SQS (ex: a VPC endpoint), if not specified the url of the region is used.
        /// </summary>
        public String SqsServiceUrl;

        /// <summary>
        /// The number of seconds to wait before polling a queue again after a failure.
        /// Default: 10.
        /// </summary>
        public int PollRetryDelay = 10;

        /// <summary>
        /// Maximum size of the message queues
        /// </summary>
        public int Size = 1000;

        /// <summary>
        /// Time out in seconds when waiting for new messages
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of messages to return on a poll
        /// </summary>
        public int MaxPoll = 100;
    }

    public sealed class AmazonSenderParams
    {
        public override string ToString() => String.Concat(OriginationIdentity, String.IsNullOrEmpty(System) ? "" : " System: " + System);

        /// <summary>
        /// The origination identity: a phone number (E.164), a phone number id or ARN, a sender id, or a pool id or ARN.
        /// Required.
        /// </summary>
        public String OriginationIdentity;

        /// <summary>
        /// The phone number of the sender (E.164), only required if the origination identity isn't a phone number (ex: a pool or a phone number id) and two-way messages are received on it.
        /// Used to match incoming messages to the sender, and to select the sender for recipients in the same country.
        /// </summary>
        public String PhoneNumber;

        /// <summary>
        /// Optional system that this sender belongs to.
        /// Incoming messages to this sender gets this system, and outgoing messages for this system are sent from this sender (if any).
        /// </summary>
        public String System;

        /// <summary>
        /// Optional list of two letter ISO-3166a2 country codes that this sender should be used for (in addition to the country of the phone number).
        /// </summary>
        public String[] Countries;
    }
}
