using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public sealed class WhatsAppComs : IUserManagerComs
    {
        public String Name => "WhatsApp";
        public bool AllowSignUp { get; init; }

        public WhatsAppComs(ServiceManager manager, WhatsAppComsParams p)
        {
            p = p ?? new WhatsAppComsParams();
            AllowSignUp = p.AllowSignUp;
            TextSender = manager.Get<ITextMessageSender>(p.Instance);

            var m = Messages;
            m[(int)UserManagerComOps.Invite] = new(p.Invite ?? "data.InviteNotification.md");
            m[(int)UserManagerComOps.SignUp] = new(p.SignUp ?? "data.SignUpNotification.md");
            m[(int)UserManagerComOps.DeleteUser] = new(p.DeleteUser ?? "data.DeleteUserNotification.md");
            m[(int)UserManagerComOps.ResetPassword] = new(p.ResetPassword ?? "data.ResetPasswordNotification.md");
            m[(int)UserManagerComOps.AddPassword] = new(p.AddPassword ?? "data.AddPasswordNotification.md");
            m[(int)UserManagerComOps.DeletePassword] = new(p.DeletePassword ?? "data.DeletePasswordNotification.md");

            m[(int)UserManagerComOps.AddEmail] = new(p.AddEmail ?? "data.AddEmailNotification.md");
            m[(int)UserManagerComOps.ChangeEmail] = new(p.ChangeEmail ?? "data.ChangeEmailNotification.md");

            m[(int)UserManagerComOps.AddEmailNoCode] = new(p.AddEmailNoCode ?? "data.AddEmailNoCodeNotification.md");
            m[(int)UserManagerComOps.ChangeEmailNoCode] = new(p.ChangeEmailNoCode ?? "data.ChangeEmailNoCodeNotification.md");

            m[(int)UserManagerComOps.DeletedEmail] = new(p.DeletedEmail ?? "data.DeletedEmailNotification.md");

            m[(int)UserManagerComOps.AddPhone] = new(p.AddWhatsApp ?? "data.AddWhatsAppNotification.md");
            m[(int)UserManagerComOps.ChangePhone] = new(p.ChangeWhatsApp ?? "data.ChangeWhatsAppNotification.md");
            m[(int)UserManagerComOps.DeletedPhone] = new(p.DeletedWhatsApp ?? "data.DeletedWhatsAppNotification.md");


            Footer = new(p.Footer ?? "data.FooterNotification.md");
            Header = new(p.Header ?? "data.HeaderNotification.md");

        }


        readonly ITextMessageSender TextSender;

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


        public Task Send(UserManagerComOps ops, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system)
        {
            bool haveLang = messages != null;
            var m = haveLang ? messages.GetText(ops) : Messages[(int)ops];
            if (m == null)
                throw new Exception("Undefined text message " + ops);
            PhonePrefix.GetValidatedPhoneNumber(out var name, out var isoCountry, out var pre, out var num, target);
            target = String.Join(' ', pre, num);
            var header = (haveLang ? messages.GetText("Header") : Header)?.GetMessage(vars) ?? "";
            var footer = (haveLang ? messages.GetText("Footer") : Footer)?.GetMessage(vars) ?? "";
            vars["[Header]"] = header;
            vars["[Footer]"] = footer;
            var body = m.GetMessage(vars);
            return TextSender.Send(target, body, system);
        }

        public Task Send(String message, ManagedLanguageMessages messages, String target, Dictionary<String, String> vars, String system, params ManagedLanguageMessages[] fallbacks)
        {
            ManagedTextMessage get(String mm)
            {
                var m = messages?.GetText(mm);
                if (m != null)
                    return m;
                foreach (var x in fallbacks)
                {
                    m = x.GetText(mm);
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
        readonly UmManagedTextMessage[] Messages = new UmManagedTextMessage[(int)UserManagerComOps.Count];
        readonly UmManagedTextMessage Header;
        readonly UmManagedTextMessage Footer;

    }


}
