using System;
using System.Collections.Generic;
using System.IO;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Parameters for the e-mail communication method of the user manager (<see cref="EmailComs"/>)
    /// </summary>
    public sealed class EmailComsParams : ComsParams
    {

        /// <summary>
        /// If non-empty, only email addresses that ends in one of the supplied suffixes (case insensitive, ex: "@example.org") can be sent to.
        /// </summary>
        public String[] WhiteListPrefixes;

        /// <summary>
        /// An optional instance name of the IEmailService to use.
        /// If null, the first email instance will be used.
        /// Default: "useradmin".
        /// </summary>
        public String Instance = "useradmin";

        /// <summary>
        /// An optional mail template to use for invite emails.
        /// </summary>
        public InviteMail Invite;

        /// <summary>
        /// An optional mail template to use for sign up emails.
        /// </summary>
        public SignUpMail SignUp;

        /// <summary>
        /// An optional mail template to use for delete user emails.
        /// </summary>
        public DeleteUserMail DeleteUser;

        /// <summary>
        /// An optional mail template to use for reset password emails.
        /// </summary>
        public ResetPasswordMail ResetPassword;

        /// <summary>
        /// An optional mail template to use for the reset instructions when an administrator required a new password (forced password reset).
        /// </summary>
        public ForcedResetPasswordMail ForcedResetPassword;

        /// <summary>
        /// An optional mail template to use for add password emails.
        /// </summary>
        public AddPasswordMail AddPassword;

        /// <summary>
        /// An optional mail template to use for delete password emails.
        /// </summary>
        public DeletePasswordMail DeletePassword;

        /// <summary>
        /// An optional mail template to use for add email emails.
        /// </summary>
        public AddEmailMail AddEmail;

        /// <summary>
        /// An optional mail template to use for change email emails.
        /// </summary>
        public ChangeEmailMail ChangeEmail;

        /// <summary>
        /// An optional mail template to use for add email emails (without code).
        /// </summary>
        public AddEmailMail AddEmailNoCode;

        /// <summary>
        /// An optional mail template to use for change email emails (without code).
        /// </summary>
        public ChangeEmailMail ChangeEmailNoCode;


        /// <summary>
        /// An optional mail template to use for delete email emails.
        /// </summary>
        public DeletedEmailMail DeletedEmail;

        /// <summary>
        /// An optional mail template to use for the notice sent to the old email address after the email address of an account was changed.
        /// </summary>
        public ChangedEmailMail ChangedEmail;


        /// <summary>
        /// An optional mail template to use for add phone number emails.
        /// </summary>
        public AddPhoneMail AddPhone;

        /// <summary>
        /// An optional mail template to use for change phone number emails.
        /// </summary>
        public ChangePhoneMail ChangePhone;

        /// <summary>
        /// An optional mail template to use for delete phone number emails.
        /// </summary>
        public DeletedPhoneMail DeletedPhone;


        /// <summary>
        /// An optional template to include as mail header in emails.
        /// Subject and IsHtml is not used.
        /// </summary>
        public MailHeader Header;

        /// <summary>
        /// An optional template to include as mail footer in emails.
        /// Subject and IsHtml is not used.
        /// </summary>
        public MailFooter Footer;
    }


    /// <summary>
    /// The default mail header template (embedded resource data.HeaderMail.html)
    /// </summary>
    public sealed class MailHeader : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded body
        /// </summary>
        public MailHeader()
        {
            Subject = "";
            Body = "data.HeaderMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default mail footer template (embedded resource data.FooterMail.html)
    /// </summary>
    public sealed class MailFooter : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded body
        /// </summary>
        public MailFooter()
        {
            Subject = "";
            Body = "data.FooterMail.html";
            IsHtml = true;
        }
    }



    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.Invite"/> e-mail
    /// </summary>
    public sealed class InviteMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public InviteMail()
        {
            Subject = "data.InviteMail.txt";
            Body = "data.InviteMail.html";
            IsHtml = true;
        }
    }


    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.SignUp"/> e-mail
    /// </summary>
    public sealed class SignUpMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public SignUpMail()
        {
            Subject = "data.SignUpMail.txt";
            Body = "data.SignUpMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.DeleteUser"/> e-mail
    /// </summary>
    public sealed class DeleteUserMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public DeleteUserMail()
        {
            Subject = "data.DeleteUserMail.txt";
            Body = "data.DeleteUserMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ResetPassword"/> e-mail
    /// </summary>
    public sealed class ResetPasswordMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ResetPasswordMail()
        {
            Subject = "data.ResetPasswordMail.txt";
            Body = "data.ResetPasswordMail.html";
            IsHtml = true;
        }
    }


    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ForcedResetPassword"/> e-mail
    /// </summary>
    public sealed class ForcedResetPasswordMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ForcedResetPasswordMail()
        {
            Subject = "data.ForcedResetPasswordMail.txt";
            Body = "data.ForcedResetPasswordMail.html";
            IsHtml = true;
        }
    }


    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.AddPassword"/> e-mail
    /// </summary>
    public sealed class AddPasswordMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public AddPasswordMail()
        {
            Subject = "data.AddPasswordMail.txt";
            Body = "data.AddPasswordMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.DeletePassword"/> e-mail
    /// </summary>
    public sealed class DeletePasswordMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public DeletePasswordMail()
        {
            Subject = "data.DeletePasswordMail.txt";
            Body = "data.DeletePasswordMail.html";
            IsHtml = true;
        }
    }



    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.AddEmail"/> e-mail
    /// </summary>
    public sealed class AddEmailMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public AddEmailMail()
        {
            Subject = "data.AddEmailMail.txt";
            Body = "data.AddEmailMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ChangeEmail"/> e-mail
    /// </summary>
    public sealed class ChangeEmailMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ChangeEmailMail()
        {
            Subject = "data.ChangeEmailMail.txt";
            Body = "data.ChangeEmailMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.AddEmailNoCode"/> e-mail
    /// </summary>
    public sealed class AddEmailNoCodeMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public AddEmailNoCodeMail()
        {
            Subject = "data.AddEmailNoCodeMail.txt";
            Body = "data.AddEmailNoCodeMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ChangeEmailNoCode"/> e-mail
    /// </summary>
    public sealed class ChangeEmailNoCodeMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ChangeEmailNoCodeMail()
        {
            Subject = "data.ChangeEmailNoCodeMail.txt";
            Body = "data.ChangeEmailNoCodeMail.html";
            IsHtml = true;
        }
    }



    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.DeletedEmail"/> e-mail
    /// </summary>
    public sealed class DeletedEmailMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public DeletedEmailMail()
        {
            Subject = "data.DeletedEmailMail.txt";
            Body = "data.DeletedEmailMail.html";
            IsHtml = true;
        }
    }


    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ChangedEmail"/> e-mail
    /// </summary>
    public sealed class ChangedEmailMail : ManagedMailMessage
    {
        /// <summary>
        /// The variables of the user manager templates besides the standard ones (<see cref="ManagedVars"/>): "[UndoLink]" (the undo link of the changed e-mail notice)
        /// </summary>
        public static readonly IReadOnlySet<String> UserManagerVars = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase, "[UndoLink]");

        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ChangedEmailMail() : base(UserManagerVars)
        {
            Subject = "data.ChangedEmailMail.txt";
            Body = "data.ChangedEmailMail.html";
            IsHtml = true;
        }
    }



    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.AddPhone"/> e-mail
    /// </summary>
    public sealed class AddPhoneMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public AddPhoneMail()
        {
            Subject = "data.AddPhoneMail.txt";
            Body = "data.AddPhoneMail.html";
            IsHtml = true;
        }
    }

    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.ChangePhone"/> e-mail
    /// </summary>
    public sealed class ChangePhoneMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public ChangePhoneMail()
        {
            Subject = "data.ChangePhoneMail.txt";
            Body = "data.ChangePhoneMail.html";
            IsHtml = true;
        }
    }
    /// <summary>
    /// The default template (embedded resources) of the <see cref="UserManagerComOps.DeletedPhone"/> e-mail
    /// </summary>
    public sealed class DeletedPhoneMail : ManagedMailMessage
    {
        /// <summary>
        /// Create the template with the default embedded subject and body
        /// </summary>
        public DeletedPhoneMail()
        {
            Subject = "data.DeletedPhoneMail.txt";
            Body = "data.DeletedPhoneMail.html";
            IsHtml = true;
        }
    }


}
