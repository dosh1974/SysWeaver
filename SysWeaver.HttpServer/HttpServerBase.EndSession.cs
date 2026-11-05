using System;
using System.Security.Cryptography;
using System.Text;
using SysWeaver.Auth;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// Key of the session end tokens (see <see cref="SessionEndToken"/>), random per process so tokens never survive a restart.
        /// </summary>
        static readonly byte[] SessionEndKey = RandomNumberGenerator.GetBytes(32);

        /// <summary>
        /// The token needed to end a session with <see cref="EndSession(string, HttpServerRequest)"/>.
        /// It is sent with the (authenticated, same origin) <see cref="ActiveSessions"/> table only, it can't be guessed and it doesn't reveal
        /// the session token, so a crafted link, a deep link scan or an image (a plain GET, CSRF style) can't end a session.
        /// </summary>
        /// <param name="sessionToken">The (full) session token</param>
        /// <returns>The end token, the first 128 bits (base64url) of a HMAC-SHA256 of the session token</returns>
        static String SessionEndToken(String sessionToken)
        {
            var h = HMACSHA256.HashData(SessionEndKey, Encoding.UTF8.GetBytes(sessionToken ?? ""));
            return Convert.ToBase64String(h, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// End (close) an active session: the user of that session is signed out and the browser gets a new, anonymous session on its next request.
        /// Use this to end a suspicious session.
        /// The token comes from the "End session" action of the active sessions table (it can't be guessed), your own session can't be ended here (sign out instead).
        /// </summary>
        /// <param name="token">The end token of the session (from the active sessions table)</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>True when the session was ended (it never returns false: a session that is not found is a 409 with the reason, so the client can show why nothing was done)</returns>
        /// <exception cref="HttpResponseException">400: the token is missing; 409 with the reason: it is your own session, or the session has already ended (expired, ended or signed out)</exception>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.AdminOps)]
        public bool EndSession(String token, HttpServerRequest context)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > 64))
                throw new HttpResponseException(400, "A session end token is required");
            var a = Encoding.ASCII.GetBytes(token);
            HttpSession found = null;
            foreach (var s in Sessions.Values)
            {
                var b = Encoding.ASCII.GetBytes(SessionEndToken(s.Token));
                if (CryptographicOperations.FixedTimeEquals(a, b))
                {
                    found = s;
                    break;
                }
            }
            if (found == null)
                throw HttpResponseException.AlreadyDone("The session has already ended (it expired, was ended or signed out)");
            if (found == context.Session)
                throw HttpResponseException.Refused("This is your own session: sign out instead");
            var by = context.Session?.Auth?.Username ?? "?";
            var who = found.Auth?.Username;
            var closed = CloseSession(found, "The session was ended by an administrator");
            if (!closed)
                throw HttpResponseException.AlreadyDone("The session has already ended (it expired, was ended or signed out)");
            Msg?.AddMessage(who == null
                ? $"{Prefix}A session from {found.Address} was ended by \"{by}\""
                : $"{Prefix}The session of \"{who}\" from {found.Address} was ended by \"{by}\"", MessageLevels.Info);
            return true;
        }
    }
}
