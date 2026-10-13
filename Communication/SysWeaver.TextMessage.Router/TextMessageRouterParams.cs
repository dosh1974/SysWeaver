using System;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    public sealed class TextMessageRouterParams
    {
        public override string ToString() => String.Concat(Routes?.Length ?? 0, " route(s)");

        /// <summary>
        /// The providers to use, in priority order (the first matching route is tried first).
        /// </summary>
        public TextMessageRouteParams[] Routes;

        /// <summary>
        /// When a message fails with any of these reasons, the next matching route is tried.
        /// Default: NotOnChannel, OutsideWindow.
        /// </summary>
        public TextMessageFailures[] FallbackOn = [TextMessageFailures.NotOnChannel, TextMessageFailures.OutsideWindow];

        /// <summary>
        /// If true, the next matching route is tried when a provider throws (configuration or connection problems etc).
        /// Default: true.
        /// </summary>
        public bool FallbackOnError = true;

        /// <summary>
        /// When a message to a phone number fails with NotOnChannel, that route is skipped for the number for this number of hours.
        /// Default: 24.
        /// </summary>
        public int NotOnChannelHours = 24;

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

        /// <summary>
        /// The number of seconds to wait before polling a (remote) provider again after a failure.
        /// Default: 10.
        /// </summary>
        public int PollRetryDelay = 10;
    }

    public sealed class TextMessageRouteParams
    {
        public override string ToString() => String.Concat(Name ?? Instance, " (", Channels, ')');

        /// <summary>
        /// The service instance name of the provider (ITextMessageSender and optionally ITextMessageReceiverService, ITextMessageStatusService, ITextMessageEvents).
        /// Required.
        /// </summary>
        public String Instance;

        /// <summary>
        /// The name of the route (used in response tokens and as the provider name in results), default is the instance name.
        /// </summary>
        public String Name;

        /// <summary>
        /// The channel(s) that the provider uses, messages that doesn't allow any of these channels skips this route.
        /// Any (default) matches all messages.
        /// </summary>
        public TextMessageChannels Channels;

        /// <summary>
        /// If non-empty, only use this route for recipients in these countries (two letter ISO-3166a2 codes).
        /// </summary>
        public String[] Countries;

        /// <summary>
        /// Never use this route for recipients in these countries (two letter ISO-3166a2 codes).
        /// </summary>
        public String[] ExcludeCountries;

        /// <summary>
        /// If non-empty, only use this route for messages of these systems.
        /// </summary>
        public String[] Systems;

        /// <summary>
        /// Use this route to send messages.
        /// Default: true.
        /// </summary>
        public bool Send = true;

        /// <summary>
        /// Receive messages and status updates from this provider.
        /// Default: true.
        /// </summary>
        public bool Receive = true;
    }

    /// <summary>
    /// Information about a route
    /// </summary>
    public sealed class TextMessageRouteInfo
    {
        /// <summary>
        /// The route name
        /// </summary>
        public String Name;

        /// <summary>
        /// The service instance name
        /// </summary>
        public String Instance;

        /// <summary>
        /// The channel(s) of the route
        /// </summary>
        public TextMessageChannels Channels;

        /// <summary>
        /// True if the provider instance was found
        /// </summary>
        public bool Found;

        /// <summary>
        /// How messages are received: "events" (in-process), "poll" (remote) or "none"
        /// </summary>
        public String Incoming;

        /// <summary>
        /// How status updates are received: "events" (in-process), "poll" (remote) or "none"
        /// </summary>
        public String Statuses;
    }
}
