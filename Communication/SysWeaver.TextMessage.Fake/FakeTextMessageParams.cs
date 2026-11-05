using System;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    public sealed class FakeTextMessageParams
    {
        /// <summary>
        /// Maximum size of the message queues (will try to keep them around this number)
        /// </summary>
        public int Size = 250;

        /// <summary>
        /// Time out in seconds when waiting for new messages
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of messages to return on a poll
        /// </summary>
        public int MaxPoll = 100;

        /// <summary>
        /// The channel that this fake provider simulates.
        /// Messages that doesn't allow this channel fails with NotOnChannel.
        /// Default: Sms.
        /// </summary>
        public TextMessageChannels Channel = TextMessageChannels.Sms;

        /// <summary>
        /// The phone number that messages are sent from.
        /// Default: "+1 555 0100".
        /// </summary>
        [EditType(EditTypes.Phone)]
        public String From = "+1 555 0100";

        /// <summary>
        /// Messages sent to phone numbers starting with any of these (digits only, with country code, ex: "4670") fails with NotOnChannel (simulates a number that isn't on the channel).
        /// </summary>
        public String[] NotOnChannelPrefixes;

        /// <summary>
        /// If true, the NotOnChannel failure is reported asynchronously (as a status update, like WhatsApp), else the send result is failed.
        /// Default: false.
        /// </summary>
        public bool FailAsync;

        /// <summary>
        /// If true, sent messages gets the status sent and then delivered (as status updates).
        /// Default: true.
        /// </summary>
        public bool AutoDeliver = true;

        /// <summary>
        /// The delay in milli seconds between status updates.
        /// Default: 200.
        /// </summary>
        public int StatusDelay = 200;

        /// <summary>
        /// The auth required to use the API's (and see the menu items) of this service, the fake messages can contain login codes etc.
        /// null = no auth (anyone, as the original: the services that send through it and the testers' pages call it without signing in), "" = any signed in user, else a comma separated list of tokens (one is needed), ex: Roles.OpsDev.
        /// Default: null.
        /// </summary>
        public String Auth;
    }

    /// <summary>
    /// Simulate a status update of a sent message
    /// </summary>
    public sealed class FakeStatusRequest
    {
        /// <summary>
        /// The id of a sent message
        /// </summary>
        public String MessageId;

        /// <summary>
        /// The new status
        /// </summary>
        public TextMessageStatuses Status = TextMessageStatuses.Delivered;

        /// <summary>
        /// The failure reason (if failed)
        /// </summary>
        public TextMessageFailures Failure;
    }

}
