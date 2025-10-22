using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class NoSessionException : Exception
    {
        public NoSessionException() : base("No session found!?")
        {
        }
    }

    public sealed class NoUserLoggedInException : Exception
    {
        public NoUserLoggedInException() : base("No user is logged into this session!")
        {
        }
    }

    public sealed class UserAlreadyLoggedInException : Exception
    {
        public UserAlreadyLoggedInException() : base("A user is already logged into this session!")
        {
        }
    }

    public sealed class UserNotAllowedException : Exception
    {
        public UserNotAllowedException() : base("The user is not allowed to access this reasource!")
        {
        }
    }

    public sealed class UserAlreadyExistException : Exception
    {
        public UserAlreadyExistException(String userName) : base("User " + userName.ToQuoted() + " already exist!")
        {
        }
    }

    public sealed class UserDoNotExistException : Exception
    {
        public UserDoNotExistException(String userName) : base("User " + userName.ToQuoted() + " do not exist!")
        {
        }
    }


    public sealed class UserNoLongerExistException : Exception
    {
        public UserNoLongerExistException(long id) : base("User #" + id + "do not exist anymore!")
        {
        }
    }


    public sealed class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException() : base("Authentication failed")
        {
        }
    }


}
