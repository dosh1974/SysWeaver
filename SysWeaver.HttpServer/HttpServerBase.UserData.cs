using System.Collections.Concurrent;
using SysWeaver.Auth;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        sealed class UserData
        {

            public readonly Authorization Auth;
            public readonly ConcurrentDictionary<HttpSession, bool> Sessions = new ConcurrentDictionary<HttpSession, bool>();

            public UserData(Authorization auth)
            {
                Auth = auth;
            }
        }

    }

}
