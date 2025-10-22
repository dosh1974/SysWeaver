using System;
using System.Collections.Generic;
using System.IO;

namespace SysWeaver.MicroService
{


    public sealed class EmailComsParams : ComsParams
    {

        /// <summary>
        /// If non-empty, only email addresses that ends in one of the supplied prefixes are valid.
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


    public sealed class MailHeader : ManagedMailMessage
    {
        public MailHeader()
        {
            Subject = "";
            Body = "data.HeaderMail.html";
            IsHtml = true;
        }
    }

    public sealed class MailFooter : ManagedMailMessage
    {
        public MailFooter()
        {
            Subject = "";
            Body = "data.FooterMail.html";
            IsHtml = true;
        }
    }



    public sealed class InviteMail : ManagedMailMessage
    {
        public InviteMail()
        {
            Subject = "data.InviteMail.txt";
            Body = "data.InviteMail.html";
            IsHtml = true;
        }
    }


    public sealed class SignUpMail : ManagedMailMessage
    {
        public SignUpMail()
        {
            Subject = "data.SignUpMail.txt";
            Body = "data.SignUpMail.html";
            IsHtml = true;
        }
    }

    public sealed class DeleteUserMail : ManagedMailMessage
    {
        public DeleteUserMail()
        {
            Subject = "data.DeleteUserMail.txt";
            Body = "data.DeleteUserMail.html";
            IsHtml = true;
        }
    }

    public sealed class ResetPasswordMail : ManagedMailMessage
    {
        public ResetPasswordMail()
        {
            Subject = "data.ResetPasswordMail.txt";
            Body = "data.ResetPasswordMail.html";
            IsHtml = true;
        }
    }


    public sealed class AddPasswordMail : ManagedMailMessage
    {
        public AddPasswordMail()
        {
            Subject = "data.AddPasswordMail.txt";
            Body = "data.AddPasswordMail.html";
            IsHtml = true;
        }
    }

    public sealed class DeletePasswordMail : ManagedMailMessage
    {
        public DeletePasswordMail()
        {
            Subject = "data.DeletePasswordMail.txt";
            Body = "data.DeletePasswordMail.html";
            IsHtml = true;
        }
    }



    public sealed class AddEmailMail : ManagedMailMessage
    {
        public AddEmailMail()
        {
            Subject = "data.AddEmailMail.txt";
            Body = "data.AddEmailMail.html";
            IsHtml = true;
        }
    }

    public sealed class ChangeEmailMail : ManagedMailMessage
    {
        public ChangeEmailMail()
        {
            Subject = "data.ChangeEmailMail.txt";
            Body = "data.ChangeEmailMail.html";
            IsHtml = true;
        }
    }

    public sealed class AddEmailNoCodeMail : ManagedMailMessage
    {
        public AddEmailNoCodeMail()
        {
            Subject = "data.AddEmailNoCodeMail.txt";
            Body = "data.AddEmailNoCodeMail.html";
            IsHtml = true;
        }
    }

    public sealed class ChangeEmailNoCodeMail : ManagedMailMessage
    {
        public ChangeEmailNoCodeMail()
        {
            Subject = "data.ChangeEmailNoCodeMail.txt";
            Body = "data.ChangeEmailNoCodeMail.html";
            IsHtml = true;
        }
    }



    public sealed class DeletedEmailMail : ManagedMailMessage
    {
        public DeletedEmailMail()
        {
            Subject = "data.DeletedEmailMail.txt";
            Body = "data.DeletedEmailMail.html";
            IsHtml = true;
        }
    }



    public sealed class AddPhoneMail : ManagedMailMessage
    {
        public AddPhoneMail()
        {
            Subject = "data.AddPhoneMail.txt";
            Body = "data.AddPhoneMail.html";
            IsHtml = true;
        }
    }

    public sealed class ChangePhoneMail : ManagedMailMessage
    {
        public ChangePhoneMail()
        {
            Subject = "data.ChangePhoneMail.txt";
            Body = "data.ChangePhoneMail.html";
            IsHtml = true;
        }
    }
    public sealed class DeletedPhoneMail : ManagedMailMessage
    {
        public DeletedPhoneMail()
        {
            Subject = "data.DeletedPhoneMail.txt";
            Body = "data.DeletedPhoneMail.html";
            IsHtml = true;
        }
    }


}
