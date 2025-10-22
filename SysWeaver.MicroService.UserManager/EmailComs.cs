using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public sealed class EmailComs : IUserManagerComs, IDisposable
    {
        public String Name => "Email";
        
        public bool AllowSignUp { get; init; }

        public EmailComs(ServiceManager manager, EmailComsParams p)
        {
            p = p ?? new EmailComsParams();
            Manager = manager;
            AuthMan = manager.TryGet<AuthManagerService>();
            manager.OnServiceAdded += Manager_OnServiceAdded;
            AllowSignUp = p.AllowSignUp;
            var wl = p.WhiteListPrefixes;
            var wll = wl?.Length ?? 0;
            if (wll > 0)
            {
                for (int i = 0; i < wll; ++i)
                    wl[i] = wl[i].Trim().FastToLower();
                WhiteListPrefixes = wl;
            }

            Email = manager.Get<IEmailService>(p.Instance);
            var m = Messages;
            m[(int)UserManagerComOps.Invite] = p.Invite ?? new();
            m[(int)UserManagerComOps.SignUp] = p.SignUp ?? new();
            m[(int)UserManagerComOps.DeleteUser] = p.DeleteUser ?? new();
            m[(int)UserManagerComOps.ResetPassword] = p.ResetPassword ?? new();
            m[(int)UserManagerComOps.AddPassword] = p.AddPassword ?? new();
            m[(int)UserManagerComOps.DeletePassword] = p.DeletePassword ?? new();

            m[(int)UserManagerComOps.AddEmail] = p.AddEmail ?? new();
            m[(int)UserManagerComOps.ChangeEmail] = p.ChangeEmail ?? new();

            m[(int)UserManagerComOps.AddEmailNoCode] = p.AddEmailNoCode ?? new();
            m[(int)UserManagerComOps.ChangeEmailNoCode] = p.ChangeEmailNoCode ?? new();

            m[(int)UserManagerComOps.DeletedEmail] = p.DeletedEmail ?? new();

            m[(int)UserManagerComOps.AddPhone] = p.AddPhone ?? new();
            m[(int)UserManagerComOps.ChangePhone] = p.ChangePhone ?? new();
            m[(int)UserManagerComOps.DeletedPhone] = p.DeletedPhone ?? new();

            Footer = p.Footer ?? new();
            Header = p.Header ?? new();
        }

        public void Dispose()
        {
            Manager.OnServiceAdded -= Manager_OnServiceAdded;

        }

        AuthManagerService AuthMan;
        
        void Manager_OnServiceAdded(object inst, ServiceInfo arg2)
        {
            if (inst is AuthManagerService)
                AuthMan = inst as AuthManagerService;
        }

        readonly ServiceManager Manager;
        readonly String[] WhiteListPrefixes;

        public bool CleanAndValidate(ref String target)
        {
            var am = AuthMan;
            if (am == null)
                return false;
            return am.ValidateEmailAddress(target, false);
        }

        public Task Send(UserManagerComOps ops, ManagedLanguageMessages lang, String target, Dictionary<String, String> vars, String system)
        {
            bool haveLang = lang != null;
            var m = haveLang ? lang.GetMail(ops) : Messages[(int)ops];
            if (m == null)
                throw new Exception("Undefined mail message " + ops);
            var wl = WhiteListPrefixes;
            if (wl != null)
            {
                bool ok = false;
                var wll = wl.Length;
                var tl = target.FastToLower();
                for (int i = 0; i < wll; ++ i)
                {
                    ok = tl.FastEndsWith(wl[i]);
                    if (ok)
                        break;
                }
                if (!ok)
                    throw new Exception("Email address is not part of the whitselist!");
            }
            var isHtml = m.IsHtml;
            var mHeader = (haveLang ? lang.GetMail("*Header") : Header);
            var mFooter = (haveLang ? lang.GetMail("*Footer") : Footer);
            String header = "";
            if ((mHeader != null) && (isHtml == mHeader.IsHtml))
                mHeader.GetMessage(out var _, out header, vars);
            String footer = "";
            if ((mFooter != null) && (isHtml == mFooter.IsHtml))
                mFooter.GetMessage(out var _, out footer, vars);
            vars["[Header]"] = header;
            vars["[Footer]"] = footer;
            m.GetMessage(out var subject, out var body, vars);
            return Email.Send(target, subject, body, m.IsHtml);
        }

        readonly IEmailService Email;
        readonly ManagedMailMessage[] Messages = new ManagedMailMessage[(int)UserManagerComOps.Count];
        readonly ManagedMailMessage Header;
        readonly ManagedMailMessage Footer;




    }

}
