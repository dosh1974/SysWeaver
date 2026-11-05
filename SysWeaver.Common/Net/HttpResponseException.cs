using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Throw this from a http request handler to respond with a specific http status code and message (instead of a 500 error).
    /// Also thrown by http clients when the server responds with an unexpected status code.
    /// <para>Write the message for the person that asked (what happened and, when it helps, what to do), without a status prefix ("Forbidden - ") or suffix (" [403]"):
    /// the client shows it as the reason (see <see cref="IsReason"/>). Ex: 409 "The invitation could not be withdrawn: it was already used, has expired or was withdrawn".</para>
    /// </summary>
    /// <remarks>
    /// The http server translates the message from <see cref="Translate"/> to the session language (if a translator is available).
    /// </remarks>
    public sealed class HttpResponseException : Exception
    {

        static String DefMsg(int code)
        {
            String text = "Http request error [";
            switch (code)
            {
                case 404:
                    text = "Not Found - The server cannot find the requested resource [";
                    break;
                case 429:
                    text = "Too Many Requests - The client has sent too many requests in a given amount of time [";
                    break;
            }
            return String.Concat(text, code, ']');
        }


        /// <summary>
        /// Create an exception that results in a specific http response.
        /// </summary>
        /// <param name="responseCode">The http status code, ex: 404</param>
        /// <param name="message">The message (response text), null to use a default message ending with the status code in brackets</param>
        /// <param name="translateFrom">The language of the message (used for translation), null to never translate the message</param>
        public HttpResponseException(int responseCode, String message = null, String translateFrom = "en") : base(message ?? DefMsg(responseCode))
        {
            ResponseCode = responseCode;
            Translate = translateFrom;
            IsReason = !String.IsNullOrEmpty(message);
        }
        /// <summary>
        /// The http status code
        /// </summary>
        public readonly int ResponseCode;
        /// <summary>
        /// The language of the message (used for translation), null if the message shouldn't be translated
        /// </summary>
        public readonly String Translate;

        /// <summary>
        /// True if the message is a reason written for people (the default when a message is given): the http server then marks the response
        /// with the "X-Sw-Reason: 1" header, so the client shows the text as the reason (the "why") instead of a generic sentence for the status.
        /// False for the default message (no message given); set it to false for texts that aren't meant for people (ex: a status relayed from another server).
        /// </summary>
        public bool IsReason { get; init; }

        /// <summary>
        /// What the refusal means for the person that asked, sent as the "X-Sw-Outcome" response header (one of the <see cref="HttpOutcomes"/>), so a client
        /// never guesses it from the status code. Null (the default) lets the http server pick it from the status (<see cref="HttpOutcomes.Default"/>):
        /// 401 / a missing role <see cref="HttpOutcomes.Denied"/>, 404 / 410 <see cref="HttpOutcomes.Gone"/>, any other 4xx <see cref="HttpOutcomes.Refused"/>, a 5xx none.
        /// <para>Set it when the status alone would mislead, most of all for <see cref="HttpOutcomes.AlreadyDone"/> ("the goal is already met, nothing was needed"),
        /// ex: <c>throw HttpResponseException.AlreadyDone("Process #12 has already ended")</c>.</para>
        /// </summary>
        public String Outcome { get; init; }

        /// <summary>
        /// A 409 that says the goal of the request is already met (ex: the process to kill has already ended, the session to end has already ended),
        /// so nothing was needed: the client shows "not needed" (<see cref="HttpOutcomes.AlreadyDone"/>).
        /// </summary>
        /// <param name="message">The reason for people, ex: "Process #12 has already ended"</param>
        /// <param name="responseCode">The status, default 409</param>
        /// <returns>The exception to throw</returns>
        public static HttpResponseException AlreadyDone(String message, int responseCode = 409) => new HttpResponseException(responseCode, message) { Outcome = HttpOutcomes.AlreadyDone };

        /// <summary>
        /// A refusal: the goal of the request is NOT met and the reason says why (ex: "This is your own session: sign out instead"),
        /// the client shows "not done" (<see cref="HttpOutcomes.Refused"/>).
        /// </summary>
        /// <param name="message">The reason for people</param>
        /// <param name="responseCode">The status, default 409</param>
        /// <returns>The exception to throw</returns>
        public static HttpResponseException Refused(String message, int responseCode = 409) => new HttpResponseException(responseCode, message) { Outcome = HttpOutcomes.Refused };

        /// <summary>
        /// The target of the request is gone (it no longer exists, or never did), so nothing was done (<see cref="HttpOutcomes.Gone"/>):
        /// the client may mark the row as no longer listed.
        /// </summary>
        /// <param name="message">The reason for people, ex: "There is no performance monitor named "x" (any more): refresh the table"</param>
        /// <param name="responseCode">The status, default 409 (404 / 410 when they fit)</param>
        /// <returns>The exception to throw</returns>
        public static HttpResponseException Gone(String message, int responseCode = 409) => new HttpResponseException(responseCode, message) { Outcome = HttpOutcomes.Gone };
    }


    /// <summary>
    /// The outcome kinds of a refused request, sent by the http server as the "X-Sw-Outcome" response header (HttpServerBase.OutcomeHeader) on every 4xx
    /// answer of a refusal (an <see cref="HttpResponseException"/>, an <see cref="IHttpRefusal"/>, a missing role, not signed in), next to "X-Sw-Reason"
    /// (the body is the reason) and "X-Sw-Denied" (role / signin).
    /// The server says the kind, the client never guesses it from the status code: only <see cref="AlreadyDone"/> means "not needed".
    /// </summary>
    public static class HttpOutcomes
    {
        /// <summary>
        /// The goal is already met (ex: the process has already ended, the session has already ended): nothing was needed. Shown as "not needed", calm.
        /// Only sent when the code that refused said so (<see cref="HttpResponseException.AlreadyDone"/>), never picked from a status.
        /// </summary>
        public const String AlreadyDone = "AlreadyDone";

        /// <summary>
        /// Refused: the goal is NOT met, the reason says why (ex: the process is protected, it is your own session, the input is wrong). Shown as "not done".
        /// The default for a 4xx other than 401 / 404 / 410 / a missing role.
        /// </summary>
        public const String Refused = "Refused";

        /// <summary>
        /// The target no longer exists (or never did): nothing was done, a row may be shown as "no longer listed". The default for 404 and 410.
        /// </summary>
        public const String Gone = "Gone";

        /// <summary>
        /// The user (or visitor) may not do this at all: a missing role (with "X-Sw-Denied: role") or not signed in (401, "X-Sw-Denied: signin").
        /// </summary>
        public const String Denied = "Denied";

        /// <summary>
        /// The outcome kind of a 4xx answer when the code that refused didn't say it: <see cref="Denied"/> for 401 or a "X-Sw-Denied" signal,
        /// <see cref="Gone"/> for 404 / 410, <see cref="Refused"/> for any other 4xx, null for any other status (a 5xx is a failure, not a refusal).
        /// </summary>
        /// <param name="status">The http status</param>
        /// <param name="denied">The "X-Sw-Denied" value, null if none</param>
        /// <returns>The outcome kind, null if none</returns>
        public static String Default(int status, String denied = null)
        {
            if ((status < 400) || (status >= 500))
                return null;
            if ((denied != null) || (status == 401))
                return Denied;
            if ((status == 404) || (status == 410))
                return Gone;
            return Refused;
        }
    }


    /// <summary>
    /// Implement this on an exception type that means "the request was refused" (a mistake of the user or a state that changed: a link that no longer works,
    /// an address in use, a wrong password ...) rather than "the server failed": the http server answers <see cref="ResponseCode"/> (a 4xx) with the exception's
    /// message as the reason for people (marked "X-Sw-Reason: 1", translated from english), like a <see cref="HttpResponseException"/>, instead of a 500,
    /// and it is not counted as a server problem.
    /// <para>Use it for the exceptions of a library that code also catches by type (ex: <c>catch (TokenExpiredException)</c>), so the type can stay
    /// and still answer the right status. Write the message for the person that asked (what happened and, when it helps, what to do).</para>
    /// </summary>
    public interface IHttpRefusal
    {
        /// <summary>
        /// The http status code to answer, ex: 400, 404, 409 or 410
        /// </summary>
        int ResponseCode { get; }

        /// <summary>
        /// The outcome kind (one of the <see cref="HttpOutcomes"/>, sent as "X-Sw-Outcome"), null (the default) to pick it from <see cref="ResponseCode"/>
        /// (<see cref="HttpOutcomes.Default"/>). Return <see cref="HttpOutcomes.AlreadyDone"/> only when the goal of the request is already met.
        /// </summary>
        String Outcome => null;
    }

}