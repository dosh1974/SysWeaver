using System;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    /// <summary>
    /// Maps Twilio error codes to normalized failure reasons.
    /// See: https://www.twilio.com/docs/api/errors
    /// </summary>
    public static class TwilioErrors
    {
        /// <summary>
        /// Map a Twilio error code to a normalized failure reason
        /// </summary>
        /// <param name="code">The Twilio error code</param>
        /// <returns>The failure reason, <see cref="TextMessageFailures.Other"/> for errors that isn't related to the recipient (configuration, authentication, temporary errors etc)</returns>
        public static TextMessageFailures GetFailure(int code) => code switch
        {
            21211 => TextMessageFailures.InvalidNumber,     //  Invalid 'To' phone number
            21214 => TextMessageFailures.Unreachable,       //  'To' phone number cannot be reached
            21265 => TextMessageFailures.InvalidNumber,     //  'To' number cannot be a short code
            21408 => TextMessageFailures.Rejected,          //  Permission to send to this region not enabled
            21610 => TextMessageFailures.Blocked,           //  Attempt to send to unsubscribed recipient (replied STOP)
            21612 => TextMessageFailures.NotOnChannel,      //  The 'To' number is not reachable via this channel / sender
            21614 => TextMessageFailures.NotOnChannel,      //  'To' number is not a valid mobile number (landline)
            30001 => TextMessageFailures.RateLimited,       //  Queue overflow
            30002 => TextMessageFailures.Rejected,          //  Account suspended
            30003 => TextMessageFailures.Unreachable,       //  Unreachable destination handset
            30004 => TextMessageFailures.Blocked,           //  Message blocked
            30005 => TextMessageFailures.InvalidNumber,     //  Unknown destination handset
            30006 => TextMessageFailures.NotOnChannel,      //  Landline or unreachable carrier
            30007 => TextMessageFailures.Rejected,          //  Message filtered (carrier)
            30032 or 30034 => TextMessageFailures.Rejected,            //  Toll free not verified / 10DLC unregistered
            30036 => TextMessageFailures.Expired,           //  Validity period expired
            30038 => TextMessageFailures.Rejected,          //  OTP message body filtered
            63003 => TextMessageFailures.NotOnChannel,      //  Channel could not find To address (not a WhatsApp user)
            63016 => TextMessageFailures.OutsideWindow,     //  Outside the WhatsApp 24 hour window, use a template
            63024 => TextMessageFailures.InvalidNumber,     //  Invalid message recipient
            63032 or 63049 => TextMessageFailures.Rejected, //  Meta chose not to deliver
            63018 or 20429 or 14107 => TextMessageFailures.RateLimited,
            _ => TextMessageFailures.Other,
        };

        /// <summary>
        /// Error codes that are temporary (the request can be retried)
        /// </summary>
        public static bool IsTransient(int code, int httpStatus)
            => (httpStatus >= 500) || (httpStatus == 429) || (code == 20429) || (code == 20500) || (code == 20503);

        /// <summary>
        /// Map a Twilio message status to a normalized status
        /// </summary>
        public static TextMessageStatuses GetStatus(String status) => status switch
        {
            "accepted" or "scheduled" or "queued" or "sending" => TextMessageStatuses.Accepted,
            "sent" => TextMessageStatuses.Sent,
            "delivered" => TextMessageStatuses.Delivered,
            "read" => TextMessageStatuses.Read,
            "undelivered" or "failed" or "canceled" => TextMessageStatuses.Failed,
            _ => TextMessageStatuses.Unknown,
        };
    }
}
