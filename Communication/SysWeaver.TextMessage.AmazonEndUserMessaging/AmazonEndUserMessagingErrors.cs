using System;
using System.Linq;
using Amazon.PinpointSMSVoiceV2.Model;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    /// <summary>
    /// Maps AWS End User Messaging SMS errors and statuses to normalized values
    /// </summary>
    public static class AmazonEndUserMessagingErrors
    {
        /// <summary>
        /// Map an exception from SendTextMessage to a normalized failure reason
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="code">The reason (error code) of the exception (if any)</param>
        /// <returns>The failure reason, <see cref="TextMessageFailures.Other"/> for errors that isn't related to the recipient (configuration, authentication, temporary errors etc)</returns>
        public static TextMessageFailures GetFailure(Exception ex, out String code)
        {
            switch (ex)
            {
                case ConflictException c:
                    code = c.Reason?.Value;
                    return code switch
                    {
                        "DESTINATION_PHONE_NUMBER_OPTED_OUT" => TextMessageFailures.Blocked,
                        //  Sandbox: the number must be verified
                        "DESTINATION_PHONE_NUMBER_NOT_VERIFIED" => TextMessageFailures.Rejected,
                        "DESTINATION_PHONE_NUMBER_BLOCKED_BY_PROTECT_NUMBER_OVERRIDE" => TextMessageFailures.Rejected,
                        "DESTINATION_COUNTRY_BLOCKED_BY_PROTECT_CONFIGURATION" => TextMessageFailures.Rejected,
                        //  No sender for the destination country
                        "NO_ORIGINATION_IDENTITIES_FOUND" => TextMessageFailures.Rejected,
                        _ => TextMessageFailures.Other,
                    };
                case ValidationException v:
                    code = v.Reason?.Value;
                    switch (code)
                    {
                        case "DESTINATION_COUNTRY_BLOCKED":
                        case "COUNTRY_NOT_ENABLED":
                        case "INVALID_IDENTITY_FOR_DESTINATION_COUNTRY":
                        case "INTERNATIONAL_SENDING_NOT_SUPPORTED":
                        case "SENDER_ID_NOT_REGISTERED":
                        case "SENDER_ID_NOT_SUPPORTED":
                        case "SENDER_ID_REQUIRES_REGISTRATION":
                        case "PRICE_OVER_THRESHOLD":
                            return TextMessageFailures.Rejected;
                    }
                    if (v.Fields?.Any(x => String.Equals(x.Name, "DestinationPhoneNumber", StringComparison.OrdinalIgnoreCase)) ?? false)
                        return TextMessageFailures.InvalidNumber;
                    return TextMessageFailures.Other;
                case ServiceQuotaExceededException q:
                    //  Spend limit etc
                    code = q.Reason?.Value;
                    return TextMessageFailures.Rejected;
            }
            code = null;
            return TextMessageFailures.Other;
        }

        /// <summary>
        /// Map a messageStatus of an event to a normalized status and failure reason
        /// </summary>
        /// <param name="status">The messageStatus, ex: "DELIVERED"</param>
        /// <param name="failure">The failure reason (if failed)</param>
        /// <returns>The status</returns>
        public static TextMessageStatuses GetStatus(String status, out TextMessageFailures failure)
        {
            failure = TextMessageFailures.None;
            switch (status)
            {
                case "ACCEPTED":
                case "QUEUED":
                case "PENDING":
                    return TextMessageStatuses.Accepted;
                case "SENT":
                case "SUCCESSFUL":
                    return TextMessageStatuses.Sent;
                case "DELIVERED":
                    return TextMessageStatuses.Delivered;
                case "INVALID":
                    failure = TextMessageFailures.InvalidNumber;
                    break;
                case "UNREACHABLE":
                case "CARRIER_UNREACHABLE":
                    failure = TextMessageFailures.Unreachable;
                    break;
                case "BLOCKED":
                    failure = TextMessageFailures.Blocked;
                    break;
                case "CARRIER_BLOCKED":
                case "SPAM":
                case "INVALID_MESSAGE":
                case "PROTECT_BLOCKED":
                    failure = TextMessageFailures.Rejected;
                    break;
                case "TTL_EXPIRED":
                    failure = TextMessageFailures.Expired;
                    break;
                case "FAILED":
                case "UNKNOWN":
                case "UNROUTABLE":
                    failure = TextMessageFailures.Other;
                    break;
                default:
                    return TextMessageStatuses.Unknown;
            }
            return TextMessageStatuses.Failed;
        }
    }
}
