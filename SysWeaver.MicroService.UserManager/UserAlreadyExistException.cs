using System;
using SysWeaver.Net;

namespace SysWeaver.Auth
{
    //  The account exceptions are refusals (a mistake of the user or a state that changed), not server failures: they implement IHttpRefusal,
    //  so the http server answers their status (4xx) with the message as the reason for people, instead of a 500.


    /// <summary>
    /// The user has no password (404).
    /// </summary>
    public sealed class UserPasswordDontExistException : Exception, IHttpRefusal
    {
        public UserPasswordDontExistException(long id) : base("No password exist for user " + id)
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 404;
    }


    /// <summary>
    /// The e-mail address is not valid (400).
    /// </summary>
    public sealed class InvalidEmailException : Exception, IHttpRefusal
    {
        public InvalidEmailException(String email) : base("The e-mail address " + email.ToQuoted() + " is not valid")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 400;
    }

    /// <summary>
    /// The phone number is not valid (400).
    /// </summary>
    public sealed class InvalidPhoneException : Exception, IHttpRefusal
    {
        public InvalidPhoneException(String phone) : base("The phone number " + phone.ToQuoted() + " is not valid")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 400;
    }

    /// <summary>
    /// The account already has this e-mail address (409).
    /// </summary>
    public sealed class EmailAlreadyAssignedException : Exception, IHttpRefusal
    {
        public EmailAlreadyAssignedException(String email) : base("The e-mail address " + email.ToQuoted() + " is already the address of this account")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// The account already has this phone number (409).
    /// </summary>
    public sealed class PhoneAlreadyAssignedException : Exception, IHttpRefusal
    {
        public PhoneAlreadyAssignedException(String phone) : base("The phone number " + phone.ToQuoted() + " is already the number of this account")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// Another account has this e-mail address (409).
    /// </summary>
    public sealed class EmailInUseException : Exception, IHttpRefusal
    {
        public EmailInUseException(String email) : base("The e-mail address " + email.ToQuoted() + " belongs to another account")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// Another account has this phone number (409).
    /// </summary>
    public sealed class PhoneInUseException : Exception, IHttpRefusal
    {
        public PhoneInUseException(String phone) : base("The phone number " + phone.ToQuoted() + " belongs to another account")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }


    /// <summary>
    /// An administrator disabled the account (UserManagerService.DisableUser): no reset message is sent and its links don't work until it is enabled
    /// again (423 Locked, so a page can tell it from a wrong entry; the reason says what to do).
    /// </summary>
    public sealed class UserDisabledException : Exception, IHttpRefusal
    {
        /// <summary>
        /// The text of the exception (and of the 423 answer), translated by the server.
        /// </summary>
        public const String Reason = "This account is disabled. Contact the administrator of this site.";

        public UserDisabledException() : base(Reason)
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 423;
    }


    /// <summary>
    /// The account has no e-mail address and no phone number to send a message to (409).
    /// </summary>
    public sealed class UserHaveNoComs : Exception, IHttpRefusal
    {
        public UserHaveNoComs() : base("The account has no e-mail address or phone number to send the message to: add one first, so you can't be locked out")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// The account already has an e-mail address (409).
    /// </summary>
    public sealed class UserAlreadyHaveEmail : Exception, IHttpRefusal
    {
        public UserAlreadyHaveEmail() : base("The account already has an e-mail address: change it instead")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// The account has no e-mail address (409).
    /// </summary>
    public sealed class UserDontHaveEmail : Exception, IHttpRefusal
    {
        public UserDontHaveEmail() : base("The account has no e-mail address")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }


    /// <summary>
    /// The account already has a phone number (409).
    /// </summary>
    public sealed class UserAlreadyHavePhone : Exception, IHttpRefusal
    {
        public UserAlreadyHavePhone() : base("The account already has a phone number: change it instead")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// The account has no phone number (409).
    /// </summary>
    public sealed class UserDontHavePhone : Exception, IHttpRefusal
    {
        public UserDontHavePhone() : base("The account has no phone number")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }


    /// <summary>
    /// The last e-mail address / phone number can't be removed (409).
    /// </summary>
    public sealed class CantDeleteLastUserCom : Exception, IHttpRefusal
    {
        public CantDeleteLastUserCom() : base("The last e-mail address or phone number can't be removed: you would be locked out of the account")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }


    /// <summary>
    /// An action token (a link or code sent in a message) can't be used: it was used, replaced by a newer one, revoked by a change of the account, has expired,
    /// or is wrong. Answered as 410 (a short code typed by the user: 400) with the reason for people (see UserManagerService.TokenRefusedReason).
    /// </summary>
    public sealed class TokenExpiredException : Exception, IHttpRefusal
    {
        /// <summary>
        /// The generic reason (when the cause isn't known).
        /// </summary>
        public const String DefaultReason = "This link no longer works: it is incomplete, or it was already used, replaced by a newer link or has expired.";

        /// <summary>
        /// A token that can't be used, with the generic reason (410).
        /// </summary>
        public TokenExpiredException() : this(DefaultReason)
        {
        }

        /// <summary>
        /// A token that can't be used.
        /// </summary>
        /// <param name="reason">Why, for people (ex: "This link no longer works: a newer link was sent, use the link in the newest message.")</param>
        /// <param name="responseCode">The http status: 410 for a link, 400 for a wrong short code</param>
        public TokenExpiredException(String reason, int responseCode = 410) : base(reason ?? DefaultReason)
        {
            ResponseCode = responseCode;
        }

        /// <inheritdoc/>
        public int ResponseCode { get; }
    }


    /// <summary>
    /// The account already has a password (409).
    /// </summary>
    public sealed class UserAlreadyHavePassword : Exception, IHttpRefusal
    {
        public UserAlreadyHavePassword() : base("The account already has a password: change it instead")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }

    /// <summary>
    /// The account has no password (409).
    /// </summary>
    public sealed class UserDontHavePassword : Exception, IHttpRefusal
    {
        public UserDontHavePassword() : base("The account has no password")
        {
        }

        /// <inheritdoc/>
        public int ResponseCode => 409;
    }


}
