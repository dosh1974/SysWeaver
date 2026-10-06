using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// Thrown when an operation requires an <see cref="HttpSession"/> but the request has none.
    /// </summary>
    public sealed class NoSessionException : Exception
    {
        /// <summary>
        /// Create the exception with a fixed message.
        /// </summary>
        public NoSessionException() : base("No session found!?")
        {
        }
    }

    /// <summary>
    /// Thrown when an operation requires a logged in user but the session has no <see cref="SysWeaver.Auth.Authorization"/>.
    /// </summary>
    /// <remarks>
    /// If this is thrown by a module while <see cref="HttpServerBase"/> resolves the request handler, the request is routed through the
    /// normal authentication flow (login redirect, Authorization header / API key, 401) instead of failing.
    /// </remarks>
    public sealed class NoUserLoggedInException : Exception
    {
        /// <summary>
        /// Create the exception with a fixed message.
        /// </summary>
        public NoUserLoggedInException() : base("No user is logged into this session!")
        {
        }
    }

    /// <summary>
    /// Thrown when an operation (such as a login or sign up) requires that no user is logged into the session.
    /// </summary>
    public sealed class UserAlreadyLoggedInException : Exception
    {
        /// <summary>
        /// Create the exception with a fixed message.
        /// </summary>
        public UserAlreadyLoggedInException() : base("A user is already logged into this session!")
        {
        }
    }

    /// <summary>
    /// Thrown when the current user (or anonymous session) lacks the tokens required to access a resource.
    /// </summary>
    public sealed class UserNotAllowedException : Exception
    {
        /// <summary>
        /// Create the exception with a fixed message.
        /// </summary>
        public UserNotAllowedException() : base("The user is not allowed to access this resource!")
        {
        }
    }

    /// <summary>
    /// Thrown when trying to create a user whose name is already taken.
    /// </summary>
    public sealed class UserAlreadyExistException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="userName">The user name that already exists (quoted in the message)</param>
        public UserAlreadyExistException(String userName) : base("User " + userName.ToQuoted() + " already exist!")
        {
        }
    }

    /// <summary>
    /// Thrown when a referenced user can't be found.
    /// </summary>
    public sealed class UserDoNotExistException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="userName">The user name (or id as text) that wasn't found (quoted in the message)</param>
        public UserDoNotExistException(String userName) : base("User " + userName.ToQuoted() + " do not exist!")
        {
        }
    }


    /// <summary>
    /// Thrown when a user that was previously known (for example the one logged into a session) has been removed.
    /// </summary>
    public sealed class UserNoLongerExistException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="id">The id of the removed user</param>
        public UserNoLongerExistException(long id) : base("User #" + id + "do not exist anymore!")
        {
        }
    }


    /// <summary>
    /// Thrown when authentication (credentials, token etc) fails.
    /// </summary>
    public sealed class AuthenticationFailedException : Exception
    {
        /// <summary>
        /// Create the exception with a fixed message.
        /// </summary>
        public AuthenticationFailedException() : base("Authentication failed")
        {
        }
    }


}
