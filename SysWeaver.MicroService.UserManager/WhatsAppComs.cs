using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Communicates with users using WhatsApp messages (through an <see cref="ITextMessageSender"/>)
    /// </summary>
    public sealed class WhatsAppComs : IUserManagerComs
    {
        /// <inheritdoc />
        public String Name => "WhatsApp";

        /// <inheritdoc />
        public bool AllowSignUp { get; init; }

        /// <summary>
        /// Create the WhatsApp communication method
        /// </summary>
        /// <param name="manager">The service manager, used to get the <see cref="ITextMessageSender"/></param>
        /// <param name="p">Parameters, null to use the defaults</param>
        public WhatsAppComs(ServiceManager manager, WhatsAppComsParams p)
        {
            p = p ?? new WhatsAppComsParams();
            AllowSignUp = p.AllowSignUp;
            TextSender = manager.Get<ITextMessageSender>(p.Instance);

            //  The defaults are the WhatsApp texts of srcData (*WhatsApp.md); the messages without one (the e-mail ones, header and footer)
            //  use the text message (SMS) texts. (A name that is no file / resource is used as the template text itself, so a wrong
            //  default name was sent as the message.)
            var m = Messages;
            m[(int)UserManagerComOps.Invite] = new (p.Invite ?? "data.InviteWhatsApp.md");
            m[(int)UserManagerComOps.SignUp] = new(p.SignUp ?? "data.SignUpWhatsApp.md");
            m[(int)UserManagerComOps.DeleteUser] = new(p.DeleteUser ?? "data.DeleteUserWhatsApp.md");
            m[(int)UserManagerComOps.ResetPassword] = new(p.ResetPassword ?? "data.ResetPasswordWhatsApp.md");
            m[(int)UserManagerComOps.AddPassword] = new(p.AddPassword ?? "data.AddPasswordWhatsApp.md");
            m[(int)UserManagerComOps.DeletePassword] = new(p.DeletePassword ?? "data.DeletePasswordWhatsApp.md");

            m[(int)UserManagerComOps.AddEmail] = new(p.AddEmail ?? "data.AddEmailText.txt");
            m[(int)UserManagerComOps.ChangeEmail] = new(p.ChangeEmail ?? "data.ChangeEmailText.txt");

            m[(int)UserManagerComOps.AddEmailNoCode] = new(p.AddEmailNoCode ?? "data.AddEmailNoCodeText.txt");
            m[(int)UserManagerComOps.ChangeEmailNoCode] = new(p.ChangeEmailNoCode ?? "data.ChangeEmailNoCodeText.txt");

            m[(int)UserManagerComOps.DeletedEmail] = new(p.DeletedEmail ?? "data.DeletedEmailText.txt");

            m[(int)UserManagerComOps.AddPhone] = new(p.AddWhatsApp ?? "data.AddPhoneWhatsApp.md");
            m[(int)UserManagerComOps.ChangePhone] = new(p.ChangeWhatsApp ?? "data.ChangePhoneWhatsApp.md");
            m[(int)UserManagerComOps.DeletedPhone] = new(p.DeletedWhatsApp ?? "data.DeletedPhoneWhatsApp.md");
            m[(int)UserManagerComOps.ForcedResetPassword] = new(p.ForcedResetPassword ?? "data.ForcedResetPasswordWhatsApp.md");


            Footer = new(p.Footer ?? "data.FooterText.txt");
            Header = new(p.Header ?? "data.HeaderText.txt");

        }


        readonly ITextMessageSender TextSender;

        /// <inheritdoc />
        public bool CleanAndValidate(ref String target)
        {
            try
            {
                var ccs = PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var prefix, out var number, target);
                target = String.Join(' ', prefix, number);
                return true;
            }
            catch
            {
            }
            return false;
        }


        /// <inheritdoc />
        public Task Send(UserManagerComOps ops, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system)
        {
            bool haveLang = messages != null;
            var m = haveLang ? messages.GetWhatsApp(ops.ToString()) : Messages[(int)ops];
            //  Localized message folders made before the forced reset template existed: use the reset template (as SmsComs)
            if ((m == null) && haveLang && (ops == UserManagerComOps.ForcedResetPassword))
                m = messages.GetWhatsApp(nameof(UserManagerComOps.ResetPassword));
            if (m == null)
                throw new Exception("Undefined text message " + ops);
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, target);
            target = String.Join(' ', pre, num);
            var header = (haveLang ? messages.GetWhatsApp("Header") : Header)?.GetMessage(vars) ?? "";
            var footer = (haveLang ? messages.GetWhatsApp("Footer") : Footer)?.GetMessage(vars) ?? "";
            vars["[Header]"] = header;
            vars["[Footer]"] = footer;
            var body = m.GetMessage(vars);
            return TextSender.Send(target, body, system);
        }

        /// <inheritdoc />
        public Task Send(String message, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system, params ManagedLanguageMessages[] fallbacks)
        {
            ManagedWhatsAppMessage get(String mm)
            {
                var m = messages?.GetWhatsApp(mm);
                if (m != null)
                    return m;
                foreach (var x in fallbacks)
                {
                    m = x.GetWhatsApp(mm);
                    if (m != null)
                        return m;
                }
                return null;
            }
            var m = get(message);
            if (m == null)
                throw new Exception("Undefined text message " + message);
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, target);
            target = String.Join(' ', pre, num);
            var header = (get("Header") ?? Header)?.GetMessage(vars) ?? "";
            var footer = (get("Footer") ?? Footer)?.GetMessage(vars) ?? "";
            vars["[Header]"] = header;
            vars["[Footer]"] = footer;
            var body = m.GetMessage(vars);
            return TextSender.Send(target, body, system);
        }
        readonly ManagedWhatsAppMessage[] Messages = new ManagedWhatsAppMessage[(int)UserManagerComOps.Count];
        readonly ManagedWhatsAppMessage Header;
        readonly ManagedWhatsAppMessage Footer;

    }


}
