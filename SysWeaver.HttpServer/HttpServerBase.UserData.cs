using System.Collections.Concurrent;
using SysWeaver.Auth;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// A logged in user (keyed by <see cref="AuthorizationInfo.Guid"/>) and the sessions that the user is logged into.
        /// </summary>
        sealed class UserData
        {

            /// <summary>
            /// The authorization of the first session that the user logged into.
            /// </summary>
            public readonly Authorization Auth;
            /// <summary>
            /// The sessions the user is logged into (the value is always true).
            /// </summary>
            public readonly ConcurrentDictionary<HttpSession, bool> Sessions = new ConcurrentDictionary<HttpSession, bool>();

            public UserData(Authorization auth)
            {
                Auth = auth;
            }
        }

    }

}
