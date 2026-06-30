using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public sealed class PhoneComs : IUserManagerComs
    {
        public String Name => "Phone";
        public bool AllowSignUp { get; init; }

        public PhoneComs(ServiceManager manager, PhoneComsParams p)
        {
            p = p ?? new PhoneComsParams();
            AllowSignUp = p.AllowSignUp;
            TextSender = manager.Get<ITextMessageSender>(p.Instance);

            var m = Messages;
            m[(int)UserManagerComOps.Invite] = new(p.Invite ?? "data.InviteText.txt");
            m[(int)UserManagerComOps.SignUp] = new(p.SignUp ?? "data.SignUpText.txt");
            m[(int)UserManagerComOps.DeleteUser] = new(p.DeleteUser ?? "data.DeleteUserText.txt");
            m[(int)UserManagerComOps.ResetPassword] = new(p.ResetPassword ?? "data.ResetPasswordText.txt");
            m[(int)UserManagerComOps.AddPassword] = new(p.AddPassword ?? "data.AddPasswordText.txt");
            m[(int)UserManagerComOps.DeletePassword] = new(p.DeletePassword ?? "data.DeletePasswordText.txt");

            m[(int)UserManagerComOps.AddEmail] = new(p.AddEmail ?? "data.AddEmailText.txt");
            m[(int)UserManagerComOps.ChangeEmail] = new(p.ChangeEmail ?? "data.ChangeEmailText.txt");

            m[(int)UserManagerComOps.AddEmailNoCode] = new(p.AddEmailNoCode ?? "data.AddEmailNoCodeText.txt");
            m[(int)UserManagerComOps.ChangeEmailNoCode] = new(p.ChangeEmailNoCode ?? "data.ChangeEmailNoCodeText.txt");

            m[(int)UserManagerComOps.DeletedEmail] = new(p.DeletedEmail ?? "data.DeletedEmailText.txt");

            m[(int)UserManagerComOps.AddPhone] = new(p.AddPhone ?? "data.AddPhoneText.txt");
            m[(int)UserManagerComOps.ChangePhone] = new(p.ChangePhone ?? "data.ChangePhoneText.txt");
            m[(int)UserManagerComOps.DeletedPhone] = new(p.DeletedPhone ?? "data.DeletedPhoneText.txt");


            Footer = new(p.Footer ?? "data.FooterText.txt");
            Header = new(p.Header ?? "data.HeaderText.txt");

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
            var header = (haveLang ? messages.GetText("*Header") : Header)?.GetMessage(vars) ?? "";
            var footer = (haveLang ? messages.GetText("*Footer") : Footer)?.GetMessage(vars) ?? "";
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
            var header = (get("*Header") ?? Header)?.GetMessage(vars) ?? "";
            var footer = (get("*Footer") ?? Footer)?.GetMessage(vars) ?? "";
            vars["[Header]"] = header;
            vars["[Footer]"] = footer;
            var body = m.GetMessage(vars);
            return TextSender.Send(target, body, system);
        }

        static readonly Char[] TrimChars = "\n\t\r ".ToCharArray();

        readonly UmManagedTextMessage[] Messages = new UmManagedTextMessage[(int)UserManagerComOps.Count];
        readonly UmManagedTextMessage Header;
        readonly UmManagedTextMessage Footer;

    }

    /// <summary>
    /// Need the type to be located in this assembly in order to get the embedded resources
    /// </summary>
    sealed class UmManagedTextMessage : ManagedTextMessage
    {
        public UmManagedTextMessage(String body) : base(body)
        {
        }
    }

}
