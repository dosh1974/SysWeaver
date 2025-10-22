
using System;

namespace SysWeaver.Auth
{







    public sealed class UserPasswordDontExistException : Exception
    {
        public UserPasswordDontExistException(long id) : base("No password exist for user " + id)
        {
        }
    }


    public sealed class InvalidEmailException : Exception
    {
        public InvalidEmailException(String email) : base("Email address " + email.ToQuoted() + " is invalid!")
        {
        }
    }

    public sealed class InvalidPhoneException : Exception
    {
        public InvalidPhoneException(String phone) : base("Phone number " + phone.ToQuoted() + " is invalid!")
        {
        }
    }

    public sealed class EmailAlreadyAssignedException : Exception
    {
        public EmailAlreadyAssignedException(String email) : base("Email address " + email.ToQuoted() + " is already asscoiated with this account!")
        {
        }
    }

    public sealed class PhoneAlreadyAssignedException : Exception
    {
        public PhoneAlreadyAssignedException(String phone) : base("Phone number " + phone.ToQuoted() + " is already asscoiated with this account!")
        {
        }
    }

    public sealed class EmailInUseException : Exception
    {
        public EmailInUseException(String email) : base("Email address " + email.ToQuoted() + " is already asscoiated with another account!")
        {
        }
    }

    public sealed class PhoneInUseException : Exception
    {
        public PhoneInUseException(String phone) : base("Phone number " + phone.ToQuoted() + " is already asscoiated with another account!")
        {
        }
    }


    public sealed class UserHaveNoComs : Exception
    {
        public UserHaveNoComs() : base("There are no means of communication! Add one ASAP to prevent lock out!")
        {
        }
    }

    public sealed class UserAlreadyHaveEmail : Exception
    {
        public UserAlreadyHaveEmail() : base("An email address is already associated with this account")
        {
        }
    }

    public sealed class UserDontHaveEmail : Exception
    {
        public UserDontHaveEmail() : base("No email address is associated with this account")
        {
        }
    }


    public sealed class UserAlreadyHavePhone : Exception
    {
        public UserAlreadyHavePhone() : base("An phone number is already associated with this account")
        {
        }
    }

    public sealed class UserDontHavePhone : Exception
    {
        public UserDontHavePhone() : base("No phone number is associated with this account")
        {
        }
    }


    public sealed class CantDeleteLastUserCom : Exception
    {
        public CantDeleteLastUserCom() : base("Can't delete the last communication method, you will be locked out of the account")
        {
        }
    }


    public sealed class TokenExpiredException : Exception
    {
        public TokenExpiredException() : base("The action token has expired!")
        {
        }
    }


    public sealed class UserAlreadyHavePassword : Exception
    {
        public UserAlreadyHavePassword() : base("A password already exist!")
        {
        }
    }

    public sealed class UserDontHavePassword : Exception
    {
        public UserDontHavePassword() : base("No password exist!")
        {
        }
    }


}
