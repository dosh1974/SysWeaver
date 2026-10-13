using System;
using SysWeaver.MicroService;
using SysWeaver.TextMessage.WhatsAppApi;

namespace SysWeaver.TextMessage
{
    /// <summary>
    /// A request to the WhatsApp Cloud API failed with an error
    /// </summary>
    public sealed class WhatsAppApiException : Exception
    {
        /// <summary>
        /// Message undeliverable (typically the recipient isn't a WhatsApp user)
        /// </summary>
        public const int Undeliverable = 131026;

        /// <summary>
        /// Re-engagement message, more than 24 hours have passed since the recipient last replied
        /// </summary>
        public const int ReEngagement = 131047;

        public WhatsAppApiException(WaError error, int statusCode, Exception inner) : base("WhatsApp error " + error, inner)
        {
            Error = error;
            StatusCode = statusCode;
        }

        /// <summary>
        /// The error returned by the API
        /// </summary>
        public readonly WaError Error;

        /// <summary>
        /// The http status code (0 if unknown)
        /// </summary>
        public readonly int StatusCode;

        /// <summary>
        /// The WhatsApp error code
        /// </summary>
        public int Code => Error.code;

        /// <summary>
        /// The normalized failure reason, <see cref="TextMessageFailures.Other"/> for errors that isn't related to the recipient (configuration, authentication, temporary errors etc)
        /// </summary>
        public TextMessageFailures Failure => GetFailure(Code);

        /// <summary>
        /// Map a WhatsApp error code to a normalized failure reason
        /// </summary>
        /// <param name="code">The WhatsApp error code</param>
        /// <returns>The failure reason</returns>
        public static TextMessageFailures GetFailure(int code) => code switch
        {
            Undeliverable => TextMessageFailures.NotOnChannel,
            ReEngagement => TextMessageFailures.OutsideWindow,
            131021 => TextMessageFailures.InvalidNumber,    //  Recipient cannot be sender
            131030 => TextMessageFailures.Rejected,         //  Recipient not in the allowed list (test numbers)
            131049 => TextMessageFailures.Rejected,         //  Not delivered to maintain ecosystem engagement
            130472 => TextMessageFailures.Rejected,         //  Part of an experiment
            131050 => TextMessageFailures.Blocked,          //  User stopped marketing messages
            131048 => TextMessageFailures.RateLimited,      //  Spam rate limit hit
            131056 => TextMessageFailures.RateLimited,      //  Pair rate limit hit
            131008 or 131009 or 132000 or 132001 or 132005 or 132007 or 132012 => TextMessageFailures.Rejected,  //  Missing / invalid parameters, template issues
            _ => TextMessageFailures.Other,
        };
    }
}
