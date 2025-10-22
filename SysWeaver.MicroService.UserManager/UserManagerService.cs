using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Db;
using SysWeaver.Net;
using SimpleStack.Orm;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SysWeaver.IsoData;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.MicroService
{

    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/LostPassword", "Forgot password", "auth/ForgotPassword.html", "Click to recover your passowrd", "IconRecover", 20, null, true)]

    [WebMenuEmbedded("User", "User/AddPassword", "Add a password", "auth/AddPassword.html", "Click to add a password to your account", "IconAddPassword", 30, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddPassword))]
    [WebMenuEmbedded("User", "User/ChangePassword", "Change password", "auth/SetPassword.html", "Click to change your password", "IconChangePassword", 31, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangePassword))]
    [WebMenuEmbedded("User", "User/DeletePassword", "Delete password", "auth/DeletePassword.html", "Click to delate your account password", "IconDeletePassword", 32, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeletePassword))]

    [WebMenuEmbedded("User", "User/AddEmail", "Add email address", "auth/AddEmail.html", "Click to associate an email address to your account", "IconAddEmail", 40, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddEmail))]
    [WebMenuEmbedded("User", "User/ChangeEmail", "Change email address", "auth/ChangeEmail.html", "Click to change your associated email address", "IconChangeEmail", 41, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangeEmail))]
    [WebMenuEmbedded("User", "User/DeleteEmail", "Remove email address", "auth/DeleteEmail.html", "Click to remove your associated email address", "IconDeleteEmail", 42, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeleteEmail))]

    [WebMenuEmbedded("User", "User/AddPhone", "Add phone number", "auth/AddPhone.html", "Click to associate an phone number to your account", "IconAddPhone", 50, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddPhone))]
    [WebMenuEmbedded("User", "User/ChangePhone", "Change phone number", "auth/ChangePhone.html", "Click to change your associated phone number", "IconChangePhone", 51, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangePhone))]
    [WebMenuEmbedded("User", "User/DeletePhone", "Remove phone number", "auth/DeletePhone.html", "Click to remove your associated phone number", "IconDeletePhone", 52, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeletePhone))]

    [WebMenuEmbedded("User", "User/DeleteAccount", "Delete account", "auth/DeleteAccount.html", "Click to delete this account", "IconDelete", 100, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDelete))]
    [WebMenuPath("User", "User/Management", "Management", "User management tools, manage other users", null, 100000)]
    [WebMenuEmbedded("User", "User/Management/InviteUser", "Invite user", "auth/InviteUser.html", "Click to invite a user to join this site", "IconInvite", 0, UserManagerRoles.InviteUser)]
    [IsMicroService]
    [RequiredDep<IEmailService>]
    public sealed partial class UserManagerService : AuthorizerBase, IDisposable
    {
        #region Dynamic menu

        Task<bool> CanDelete(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return TaskExt.FalseTask;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return TaskExt.FalseTask;
            return TaskExt.TrueTask;
        }

        async Task<bool> CanChangePassword(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyPassword(uid).ConfigureAwait(false);
        }

        async Task<bool> CanAddPassword(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return !(await HaveAnyPassword(uid).ConfigureAwait(false));
        }

        async Task<bool> CanDeletePassword(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyPassword(uid).ConfigureAwait(false);
        }

        async Task<bool> CanRecoverLogin(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyUser().ConfigureAwait(false);
        }

        async Task<bool> CanAddEmail(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Email"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            if (await HaveAnyEmail(uid).ConfigureAwait(false))
                return false;
            return true;
        }

        async Task<bool> CanChangeEmail(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Email"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyEmail(uid).ConfigureAwait(false);
        }

        async Task<bool> CanDeleteEmail(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Email"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyEmail(uid).ConfigureAwait(false);
        }



        async Task<bool> CanAddPhone(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Phone"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            if (await HaveAnyPhone(uid).ConfigureAwait(false))
                return false;
            return true;
        }

        async Task<bool> CanChangePhone(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Phone"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyPhone(uid).ConfigureAwait(false);
        }

        async Task<bool> CanDeletePhone(HttpServerRequest context, WebMenuItem item)
        {
            var s = context.Session;
            if (s == null)
                return false;
            if (!ComsMethods.Contains("Phone"))
                return false;
            var uid = GetUid(s.Auth);
            if (uid == 0)
                return false;
            return await HaveAnyPhone(uid).ConfigureAwait(false);
        }


        #endregion//Dynamic menu
        

        public override string ToString() => String.Concat(
            "Database: ", Params.ToString()
            );

        public UserManagerService(ServiceManager manager, UserManagerParams p)
        {
            p = p ?? new();
            Params = p;
            InternalPasswordPolicy = p.PasswordPolicy ?? new PasswordPolicy();
            DisableLogin = p.DisableLogin;
            //  Add and setup communication methods
            List<IUserManagerComs> coms = new List<IUserManagerComs>(manager.GetAll<IUserManagerComs>(ServiceInstanceTypes.LocalOnly, ServiceInstanceOrders.Oldest));
            if (coms.Count == 0)
                coms.Add(new EmailComs(manager, null));
            Coms = coms.ToArray();
            ComsMethods = new HashSet<string>(coms.Select(x => x.Name), StringComparer.Ordinal).Freeze();
            InternalSignUpMethods = coms.Where(x => x.AllowSignUp).Select(x => x.Name).ToArray();

            var shortCodeDigits = p.ShortCodeDigits;
            if (shortCodeDigits < 3)
                shortCodeDigits = 3;
            if (shortCodeDigits > 16)
                shortCodeDigits = 16;
            ShortCodeDigits = shortCodeDigits;


            ManProps = new UserManagerProps
            {
                Coms = ComsMethods.ToArray(),
                SignUpMethods = InternalSignUpMethods,
                CanSetNickName = p.CanSetNickName,
                CanLogin = !p.DisableLogin,
                ShortCodeDigits = shortCodeDigits,
            };


            AuthMan = manager.TryGet<AuthManagerService>();
            HttpServer = manager.TryGet<HttpServerBase>();
            manager.OnServiceAdded += Manager_OnServiceAdded;

            SystemString = new ManagedString(p.System);
            manager.AddMessage("Message system: " + SystemString.Value.ToQuoted(), MessageLevels.Debug);


            Manager = manager;
            var db = new MySqlDbSimpleStack(p);
            Db = db;
            Data = DataBlob.Get("json", "br", CompEncoderLevels.Best);

            HtmlLogo = SysWeaverLogo.GetHtmlLogo(ConsoleColor.Green, ConsoleColor.DarkGreen, "  ");
            HtmlLogoGradient = SysWeaverLogo.GetHtmlLogoGradient("  ");

            CanSetNickName = p.CanSetNickName;

            //File.WriteAllText("Logo.html", HtmlLogo);
            //File.WriteAllText("LogoGradient.html", HtmlLogoGradient);

            Messages = String.IsNullOrEmpty(p.LocalizedMessages) ? null : new ManagedMessages(p.LocalizedMessages, EnvInfo.AppLanguage ?? "en-US", true,
                "AddPassword",
                "DeletePassword",
                "DeleteUser",
                "Invite",
                "ResetPassword",
                "SignUp", 
                "AddEmail",
                "ChangeEmail",
                "AddEmailNoCode",
                "ChangeEmailNoCode",
                "DeletedEmail",
                "AddPhone",
                "ChangePhone",
                "DeletedPhone",
                "*Header",
                "*Footer"
                );

            Init(db).RunAsync();
            PruneTask = new PeriodicTask(Prune, 15000);
        }

        readonly UserManagerProps ManProps;

        /// <summary>
        /// Use this when sendig text messages as system
        /// </summary>
        public String TextSystemString => SystemString.Value;
        
        ManagedString SystemString;

        void Manager_OnServiceAdded(object inst, ServiceInfo arg2)
        {
            if (inst is AuthManagerService)
                AuthMan = inst as AuthManagerService;
            if (inst is HttpServerBase)
                HttpServer = inst as HttpServerBase;
        }


        AuthManagerService AuthMan;
        HttpServerBase HttpServer;
        
        readonly ManagedMessages Messages;

        readonly String[] InternalSignUpMethods;

        readonly IUserManagerComs[] Coms;

        readonly IReadOnlySet<String> ComsMethods;


        public override PasswordPolicy PasswordPolicy => InternalPasswordPolicy;

        readonly PasswordPolicy InternalPasswordPolicy;

        PeriodicTask PruneTask;

        async ValueTask<bool> Prune()
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var now = DateTime.UtcNow;
                await c.DeleteAllAsync<DbAction>(x => now > x.Expiration).ConfigureAwait(false);
            }
            return true;
        }

        public void Dispose()
        {
            SystemString.Dispose();
            Manager.OnServiceAdded -= Manager_OnServiceAdded;
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
        }

        readonly String HtmlLogo;
        readonly String HtmlLogoGradient;

        static async Task Init(MySqlDbSimpleStack db)
        {
            await db.Init().ConfigureAwait(false);
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbUser>(c).ConfigureAwait(false);
                await db.InitTable<DbEmailAddress>(c).ConfigureAwait(false);
                await db.InitTable<DbPhoneNumber>(c).ConfigureAwait(false);
                await db.InitTable<DbToken>(c).ConfigureAwait(false);
                await db.InitTable<DbAction>(c).ConfigureAwait(false);
                await db.InitTable<DbAuthPassKey>(c).ConfigureAwait(false);
                await db.InitTable<DbAuthPassword>(c).ConfigureAwait(false);
                await db.InitTable<DbUserData>(c).ConfigureAwait(false);
                await db.InitTable<DbUserDataString>(c).ConfigureAwait(false);
            }
        }

  
        public Dictionary<String, String> GetMessageParams(HttpServerRequest context, params String[] add)
        {
            var cols = HashColors.AppColors;
            var d = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                {  "[Site]", EnvInfo.AppDisplayName },
                {  "[Root]", context?.Prefix ?? HttpServer.ExternalRootUri ?? ""},
                {  "[BackgroundDark]", cols.BackgroundDark },
                {  "[Background]", cols.Background },
                {  "[Color]", cols.Acc1 },
                {  "[Acc1]", cols.Acc3 },
                {  "[Acc2]", cols.Acc4},
                {  "[Logo]", HtmlLogo },
                {  "[LogoGradient]", HtmlLogoGradient },
                {  "[ShortCode]", "No code provided, use link" },
            };
            if (context != null)
            {
                var ip = context.GetIpAddress();
                var userAgent = context.Session.UserAgent;
                var s = String.Concat(
                    "<a href=\"https://ip.me/ip/", HttpUtility.HtmlAttributeEncode(ip), "\" style=\"color: ", cols.Acc1, "; text-decoration: none;\">", HttpUtility.HtmlEncode(ip), "</a>",
                    " <a href=\"https://gs.statcounter.com/detect?useragent=", HttpUtility.HtmlAttributeEncode(userAgent), "\" style=\"color: ", cols.Acc1, "; text-decoration: none;\">", HttpUtility.HtmlEncode("🕵️"), "</a>"
                    );
                d["[Origin]"] = s;
            }
            else
            {
                d["[Origin]"] = HttpUtility.HtmlEncode(EnvInfo.AppDisplayName);
            }
            if (add != null)
            {
                var al = add.Length & ~1;
                for (int i = 0; i < al; i += 2)
                    d[add[i]] = add[i + 1];
            }
            return d;

        }



        async Task SendOne(List<String> sentTo, UserManagerComOps message, ManagedLanguageMessages lang, String target, IUserManagerComs coms, Dictionary<String, String> vars)
        {
            await coms.Send(message, lang, target, vars, TextSystemString).ConfigureAwait(false);
            lock (sentTo)
                sentTo.Add(target);
        }


        /// <summary>
        /// Get a communication method for a given target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        IUserManagerComs GetComs(ref String target)
        {
            foreach (var com in Coms)
            {
                if (com.CleanAndValidate(ref target))
                    return com;
            }
            return null;
        }


        /// <summary>
        /// Send a message to all communication targets
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="lang">The message language</param>
        /// <param name="targets">Communication target</param>
        /// <param name="getVars">A function that get the variables</param>
        /// <returns>The targets that the message was successfully sent to</returns>
        async Task<String[]> Send(UserManagerComOps message, ManagedLanguageMessages lang, IReadOnlyList<String> targets, Func<String, IUserManagerComs, Dictionary<String, String>> getVars)
        {
            var l = targets.Count;
            List<String> sentTo = new List<string>(l);
            List<Task> tasks = new List<Task>(l);
            var coms = Coms;
            var cl = coms.Length;
            for (int i = 0; i < l; ++i)
            {
                var target = targets[i];
                for (int j = 0; j < cl; ++j)
                {
                    var com = coms[j];
                    if (!com.CleanAndValidate(ref target))
                        continue;
                    var vars = getVars(target, com);
                    if (vars == null)
                        continue;
                    tasks.Add(SendOne(sentTo, message, lang, target, com, vars));
                    break;
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return sentTo.ToArray();
        }


        async Task<List<String>> GetUserComTargets(OrmConnection c, long userId)
        {
            List<String> targets = new List<string>();
            targets.AddRange((await c.SelectAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false)).Select(x => x.Email));
            targets.AddRange((await c.SelectAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false)).Select(x => x.Phone));
            return targets;
        }




        #region Helpers


        /// <summary>
        /// Get authorization for a user with the specified credentials and update login time
        /// </summary>
        /// <param name="credentialId"></param>
        /// <returns></returns>
        public async Task<Authorization> AuthUser(String credentialId)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var p = await c.FirstOrDefaultAsync<DbAuthPassKey>(x => x.CredentialId == credentialId).ConfigureAwait(false);
            if (p == null)
                return null;
            var id = p.UserId;
            var a = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (a == null)
                return null;
            p.LastUsed = DateTime.UtcNow;
            await c.UpdateAsync(p, x => new { x.LastUsed }).ConfigureAwait(false);
            tr.Commit();
            return a;
        }


        public static String MakeGuid(long userId, String guidPrefix)
            => String.Concat(guidPrefix, ':', HashTools.GetCompactString(userId));

        public String MakeGuid(long userId)
            => String.Concat(GuidPrefix, ':', HashTools.GetCompactString(userId));

        static readonly Tuple<long, Byte[]> InvalidCredentialId = new Tuple<long, Byte[]>(0, null);

        public async Task<Authorization> GetUser<T>(String token, String type, Func<T, long> getIdFromData, HttpServerRequest context)
        {
            var actionData = await GetAction<T>(token, type, context).ConfigureAwait(false);
            var id = getIdFromData(actionData.Item2);
            if (id == 0)
                throw new TokenExpiredException();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var authData = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
            if (authData == null)
                throw new UserNoLongerExistException(id);
            var auth = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (auth == null)
                throw new UserNoLongerExistException(id);
            await DeleteAction(c, token, type, context).ConfigureAwait(false);
            return auth;
        }

        /// <summary>
        /// Return true if there are any users in the db
        /// </summary>
        /// <returns></returns>
        public async Task<bool> HaveAnyUser()
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.CountAsync<DbUser>().ConfigureAwait(false)) > 0;
        }

        /// <summary>
        /// Return true if the user have a password
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public async Task<bool> HaveAnyPassword(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == uid).ConfigureAwait(false)) != null;
        }


        /// <summary>
        /// Return true if the user have a mail
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public async Task<bool> HaveAnyEmail(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == uid).ConfigureAwait(false)) != null;
        }

        /// <summary>
        /// Return true if the user have a mail
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public async Task<bool> HaveAnyPhone(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == uid).ConfigureAwait(false)) != null;
        }


        static String GetNick(String userName, String mail)
        {
            var nickName = (userName.FastEquals(mail) ? mail.Split('@')[0] : userName).LimitLength(AuhorizationLimits.MaxNickNameLength, "");
            return nickName;
        }

        public async Task<Authorization> CreateUser(AddUserRequest r, HttpServerRequest context, bool login)
        {
            var s = context?.Session;
            if (login)
                if (s == null)
                    throw new NoSessionException();
            var token = r.Token;
            var dataD = await GetAction<NewUserData>(token, DbAction.CreateUser, context).ConfigureAwait(false);
            if (dataD == null)
                throw new TokenExpiredException();
            token = dataD.Item1;
            var u = dataD.Item2;
            if ((u == null))
                throw new TokenExpiredException();
            var email = u.Email;
            var phone = u.Phone;
            var userName = u.UserName;
            userName = String.IsNullOrEmpty(userName) ? (email ?? phone) : userName;
            var nickName = GetNick(userName, email);
            var salt = u.Salt;
            var tokens = u.Tokens;
            var tokenSet = tokens == null ? null : new HashSet<String>(tokens.Select(x => x.Trim().FastToLower()), StringComparer.Ordinal);
            long id;
            Authorization auth;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                if (email != null)
                {
                    var f = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == email).ConfigureAwait(false);
                    if (f != null)
                        throw new UserAlreadyExistException(email);
                }
                if (phone != null)
                {
                    var f = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == phone).ConfigureAwait(false);
                    if (f != null)
                        throw new UserAlreadyExistException(phone);
                }
                var now = DateTime.UtcNow;
                var user = new DbUser
                {
                    Created = now,
                    UserName = userName,
                    NickName = nickName,
                    Domain = u.Domain,
                };
                id = await c.InsertAsync<long, DbUser>(user).ConfigureAwait(false);
                user.Id = id;
                if (email != null)
                {
                    await c.InsertAsync(new DbEmailAddress
                    {
                        Email = email,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (phone != null)
                {
                    await c.InsertAsync(new DbPhoneNumber
                    {
                        Phone = phone,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (tokenSet != null)
                {
                    foreach (var t in tokenSet)
                        await c.InsertAsync<DbToken>(new DbToken { UserId = id, Token = t }).ConfigureAwait(false);
                }
                if (!String.IsNullOrEmpty(r.NewHash))
                {
                    await c.InsertAsync(new DbAuthPassword
                    {
                        MustResetPassword = false,
                        Salt = salt,
                        Pwd = r.NewHash,
                        Created = now,
                        LastUsed = now,
                        UserId = id,

                    }).ConfigureAwait(false);
                }
                auth = await InternalAuthUser(c, id).ConfigureAwait(false);
                if (auth == null)
                    throw new UserDoNotExistException(userName);
                await DeleteAction(c, token, DbAction.CreateUser, context).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            try
            {
                await RaiseOnUserCreated(id, userName).ConfigureAwait(false);
            }
            catch
            {
            }
            if (login)
            {
                s.SetAuth(auth);
                await context.Server.RunOnLogin(s).ConfigureAwait(false);
            }
            return auth;
        }

        public async Task<Tuple<bool, long>> TryAddUser(UserManagerIdTypes type, String target, String domain = null, IReadOnlyCollection<String> tokens = null, String language = null)
        {
            String email = null;
            String phone = null;
            long id;
            switch (type)
            {
                case UserManagerIdTypes.Email:
                    email = target;
                    break;
                case UserManagerIdTypes.Phone:
                    phone = target;
                    break;
                default:
                    throw new Exception("Unsupported identification method");
            }
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);

                if (email != null)
                {
                    var f = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == email).ConfigureAwait(false);
                    if (f != null)
                        return Tuple.Create(false, f.UserId);
                }
                if (phone != null)
                {
                    var f = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == phone).ConfigureAwait(false);
                    if (f != null)
                        return Tuple.Create(false, f.UserId);
                }

                var userName = phone ?? email;
                var nickName = GetNick(userName, email);
                var now = DateTime.UtcNow;

                var user = new DbUser
                {
                    Created = now,
                    UserName = userName,
                    NickName = nickName,
                    Domain = domain,
                    Language = language,
                };
                id = await c.InsertAsync<long, DbUser>(user).ConfigureAwait(false);
                user.Id = id;
                if (email != null)
                {
                    await c.InsertAsync(new DbEmailAddress
                    {
                        Email = email,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (phone != null)
                {
                    await c.InsertAsync(new DbPhoneNumber
                    {
                        Phone = phone,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (tokens != null)
                {
                    var ts = new HashSet<String>(tokens.Select(x => x?.Trim()?.FastToLower()).Where(x => !String.IsNullOrEmpty(x)), StringComparer.Ordinal);
                    foreach (var t in ts)
                        await c.InsertAsync<DbToken>(new DbToken { UserId = id, Token = t }).ConfigureAwait(false);
                }
                await tr.CommitAsync().ConfigureAwait(false);
            }
            return Tuple.Create(true, id);
        }


        /// <summary>
        /// Get information about the currently logged in user
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DbUser GetCurrentUser(HttpServerRequest request)
            => (request.Session?.Auth?.AuthContext as DbUser);

        /// <summary>
        /// Get information about the currently logged in user
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public DbUser GetCurrentUser(HttpSession session)
            => (session?.Auth?.AuthContext as DbUser);

        /// <summary>
        /// Get detailed user information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<UserInfo> GetCurrentUserInfo(HttpServerRequest request)
        {
            var user = request.Session?.Auth?.AuthContext as DbUser;
            if (user == null)
                return null;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return await InternalTryGetUserInfo(c, user.Id, user).ConfigureAwait(false);
        }

        public async Task<long> TryGetUser(String identifier)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                return await FindUser(c, identifier).ConfigureAwait(false);
        }


        async Task<UserInfo> InternalTryGetUserInfo(OrmConnection c, long userId, DbUser user = null)
        {
            user = user ?? (await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false));
            if (user == null)
                return null;
            var email = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
            var phone = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
            var guid = MakeGuid(userId);
            var nick = user.NickName;
            bool autoGen = String.IsNullOrEmpty(nick);
            if (autoGen)
                nick = AuthorizationInfo.GetRandomName(guid);
            return new UserInfo
            {
                Id = userId,
                Guid = guid,
                UserName = user.UserName,
                NickName = nick,
                AutoNickName = autoGen,
                Email = email?.Email,
                Phone = phone?.Phone,
                Language = user.Language,
                Created = user.Created,
            };
        }

        public async Task<UserInfo> TryGetUserInfo(String identifier)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var userId = await FindUser(c, identifier).ConfigureAwait(false);
                if (userId == 0)
                    return null;
                return await InternalTryGetUserInfo(c, userId).ConfigureAwait(false);
            }
        }

        public async Task<UserInfo> TryGetUserInfo(long userId)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                return await InternalTryGetUserInfo(c, userId).ConfigureAwait(false);
        }

        public async Task<UserData> TryGetUser(long userId, bool getTokens = false, DbUser user = null)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                user = user ?? (await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false));
                var mail = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                var phone = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
                String[] tokens = null;
                if (getTokens)
                {
                    var t = (await c.SelectAsync<DbToken>(x => x.UserId == userId).ConfigureAwait(false)).ToList();
                    tokens = t.Count == 0 ? null : t.Select(x => x.Token).ToArray();
                }
                return new UserData
                {
                    NickName = user.NickName,
                    UserName = user.UserName,
                    Domain = user.Domain,
                    Language = user.Language,
                    Email = mail?.Email,
                    Phone = phone?.Phone,
                    Tokens = tokens,
                };
            }

        }

        /// <summary>
        /// Update all active session with a new user structure
        /// </summary>
        /// <param name="user"></param>
        /// <param name="newEmail">Optionally se a new email</param>
        /// <param name="clearcache">Default to clearing the session cache</param>
        void ResetSessionAuth(DbUser user, String newEmail = null, bool clearcache = true)
        {
            var userGuid = MakeGuid(user.Id);
            Authorization auth = null;
            HttpServer?.EnumUserSessions(userGuid, session =>
            {
                var oldAuth = session.Auth;
                auth = auth ?? new Authorization(this, user.UserName, oldAuth.Tokens, userGuid, newEmail ?? oldAuth.Email, user.NickName, user, user.Domain, user.Language);
                session.SetAuth(auth);
                if (clearcache)
                    session.InvalidateCache();
            });
        }

        static readonly IReadOnlySet<Char> InvalidChars = ReadOnlyData.Set("|,;:*/\\!\"'".ToCharArray());

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newName"></param>
        /// <returns>true or false, old name, new name</returns>
        /// <exception cref="Exception"></exception>
        public async Task<Tuple<bool, String, String>> SetNickName(long userId, String newName)
        {
            newName = newName?.Trim();
            if (String.IsNullOrEmpty(newName))
                throw new Exception("Name may not be null or empty");
            if (newName.Length > AuhorizationLimits.MaxNickNameLength)
                throw new Exception("Name length must be at most " + AuhorizationLimits.MaxNickNameLength + " chars");
            var inv = InvalidChars;
            foreach (var x in newName)
            {
                if ((x < ' ') || inv.Contains(x))
                    throw new Exception("Name may not have the special characters: '" + x + "' code: " + ((int)x));
            }
            Tuple<bool, String, String> res;
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null, (String)null);
                var current = user.NickName;
                if (current.FastToLower().FastEquals(newName.FastToLower()))
                    return Tuple.Create(false, current, current);
                user.NickName = newName;
                await c.UpdateAsync(user, x => new { x.NickName }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
                res = Tuple.Create(true, current, newName);
            }
            ResetSessionAuth(user);
            await RaiseOnNickChanged(userId, newName).ConfigureAwait(false);
            return res;
        }

        public async Task<Tuple<bool, String>> SetUserName(long userId, String newName)
        {
            newName = newName?.Trim();
            if (String.IsNullOrEmpty(newName))
                throw new Exception("Name may not be null or empty");
            if (newName.Length > AuhorizationLimits.MaxUserNameLength)
                throw new Exception("Name length must be at most " + AuhorizationLimits.MaxUserNameLength + " chars");
            Tuple<bool, String> res;
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null);
                if (user.UserName.FastToLower().FastEquals(newName.FastToLower()))
                    return Tuple.Create(false, user.UserName);
                user.UserName = newName;
                await c.UpdateAsync(user, x => new { x.UserName }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
                res = Tuple.Create(true, user.UserName);
            }
            ResetSessionAuth(user);
            await RaiseOnUserNameChanged(userId, newName).ConfigureAwait(false);
            return res;
        }

        public override async Task<bool> SetLanguage(String userGuid, String languageCode)
        {
            var userId = GetUserId(userGuid);
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    return false;
                if (user.Language.FastEquals(languageCode))
                    return false;
                user.Language = languageCode;
                await c.UpdateAsync(user, x => new { x.Language }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            ResetSessionAuth(user);
            await RaiseOnLanguageChanged(userId, languageCode).ConfigureAwait(false);
            return true;
        }


        // TODO: Send mail and validate

        public async Task<Tuple<bool, String>> ChangeEmail(long userId, String newEmail)
        {
            Tuple<bool, String> res;
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null);

                var email = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                if (email != null)
                {
                    if (email.Email.FastToLower().FastEquals(newEmail.FastToLower()))
                        return Tuple.Create(false, email.Email);
                    await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                }
                res = Tuple.Create(true, email?.Email);
                await c.InsertAsync(new DbEmailAddress
                {
                    Email = newEmail,
                    UserId = userId,
                }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            ResetSessionAuth(user, newEmail);
            return res;
        }


        #endregion // Helpers

        /// <summary>
        /// Validate that an incoming request have a valid one time pad and and that it's the correct user
        /// </summary>
        /// <param name="autheticatedRequest"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        DbUser ValidatePasswordRequestData(AutheticatedRequest autheticatedRequest, HttpServerRequest context)
        {
            var auth = context?.Session?.Auth;
            if (auth == null)
                return null;
            var user = auth.AuthContext as DbUser;
            if (user == null)
                return null;
            if (!Manager.TryConsumeOneTimePad(autheticatedRequest.OneTimePad, out var username))
                return null;
            if (!auth.Username.FastEquals(username))
                return null;
            return user;
        }

        /// <summary>
        /// Validate that the password supplied in an incoming request is correct
        /// </summary>
        /// <param name="autheticatedRequest"></param>
        /// <param name="authData"></param>
        /// <returns></returns>
        bool ValidatePasswordRequestPassword(AutheticatedRequest autheticatedRequest, DbAuthPassword authData)
        {
            if (authData == null)
                return false;
            var hh = AuthTools.HashToString(AuthTools.ComputeHash(authData.Pwd, autheticatedRequest.OneTimePad));
            if (hh != autheticatedRequest.Hash)
                return false;
            return true;
        }

        readonly TimeSpan NewUserTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan DeleteUserTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan ResetPasswordTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan InviteUserTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan SharePasswordTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan AddPasswordTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan DelPasswordTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan AddEmailTimeout = TimeSpan.FromHours(24);
        readonly TimeSpan AddPhoneTimeout = TimeSpan.FromHours(24);


        readonly TimeSpan NewUserShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan DeleteUserShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan ResetPasswordShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan SharePasswordShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan AddPasswordShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan DelPasswordShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan AddEmailShortTimeout = TimeSpan.FromMinutes(15);
        readonly TimeSpan AddPhoneShortTimeout = TimeSpan.FromMinutes(15);



        /// <summary>
        /// Add a pending new user action
        /// </summary>
        /// <param name="username">Email of the new user</param>
        /// <param name="email">Email of the new user</param>
        /// <param name="phone">Phone number of the new user</param>
        /// <param name="salt">Salt to use</param>
        /// <param name="authTokens">The security tokens for the user</param>
        /// <param name="domain">Optional application specific domain</param>
        /// <param name="language">Optional language preference for the user</param>
        /// <param name="expiration">Optional expiration time (UTC), default will bew the NewUserTimeout</param>
        /// <param name="context">Optional context, required to get a short code</param>
        /// <returns>The token that must be supplied together with a new password</returns>
        async Task<Tuple<String, String>> GetNewUserToken(String username, String email, String phone, String salt, String[] authTokens = null, String domain = null, String language = null, DateTime? expiration = null, HttpServerRequest context = null)
        {
            if (email != null)
            {
                email = email.Trim();
                AuthMan.ValidateEmailAddress(email);
            }
            if (phone != null)
            {
                phone = phone.Trim();
                if (String.IsNullOrEmpty(phone))
                    throw new InvalidPhoneException(phone);
            }
            if ((email == null) && (phone == null))
                throw new UserHaveNoComs();


            username = username?.Trim();
            if (String.IsNullOrEmpty(username))
                username = email ?? phone;

            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var fu = await c.FirstOrDefaultAsync<DbUser>(x => x.UserName == username).ConfigureAwait(false);
                if (fu != null)
                    throw new UserAlreadyExistException(username);
                if (email != null)
                {
                    var fm = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == email).ConfigureAwait(false);
                    if (fm != null)
                        throw new UserAlreadyExistException(email);
                }
                if (phone != null)
                {
                    var fp = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == phone).ConfigureAwait(false);
                    if (fp != null)
                        throw new UserAlreadyExistException(phone);
                }
            }
            language = IsoLanguage.Validate(language);
            var now = DateTime.UtcNow;
            var tokens = await AddAction(new NewUserData
            {
                UserName = username,
                Email = email,
                Phone = phone,
                Salt = salt,
                Tokens = authTokens,
                Domain = domain,
                Language = language,
            }, DbAction.CreateUser, expiration ?? (now + NewUserTimeout), context, now + NewUserShortTimeout).ConfigureAwait(false);
            return tokens;
        }



        sealed class ShortData
        {
            public String ShortCode;
            public String Token;
            public DateTime Expiration;
            public String Type;
            public int TriesLeft;

            public ShortData(string shortCode, string token, DateTime expiration, string type, int triesLeft = 10)
            {
                ShortCode = shortCode;
                Token = token;
                Expiration = expiration;
                Type = type;
                TriesLeft = triesLeft;
            }
        }


        const String ShortCodeName = "UserManager.ShortCode";

        static String FormatShortCode(String shortCode)
        {
            if (String.IsNullOrEmpty(shortCode))
                return "";
            return String.Join(' ', shortCode.ToCharArray());
        }

        static String CleanUpShortCode(String shortCode)
        {
            if (String.IsNullOrEmpty(shortCode))
                return null;
            var l = shortCode.Where(x => x >= '0' && x <= '9').ToArray();
            if (l.Length < 3)
                return null;
            return new String(l);
        }


        readonly int ShortCodeDigits = 6;

        /// <summary>
        /// Generate an "action" token, the token should be sent to one or more of a users communication channels.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <param name="expiration"></param>
        /// <param name="context">If non-null a short code (shorter lived) will be generated that can only be used in that session</param>
        /// <param name="shortExpiration">If a context supplied, a short expiration time must also be suplied</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        async Task<Tuple<String, String>> AddAction<T>(T data, String type, DateTime expiration, HttpServerRequest context = null, DateTime? shortExpiration = null)
        {
#if DEBUG
            if (!type.IsAsciiOnly())
                throw new ArgumentException("May only be ascii", nameof(type));
            if (type.Length > 8)
                throw new ArgumentException("Max length of 8", nameof(type));
#endif//DEBUG
            var shortCodeDigits = ShortCodeDigits;
            String token;
            String shortCode;
            using (var rng = SecureRng.Get())
            {
                token = rng.GetGuid48();
                shortCode = rng.GetNumericCode(shortCodeDigits);
            }
            var d = Data.ToData(data);
            using var c = await Db.GetAsync().ConfigureAwait(false);
            await c.InsertAsync<DbAction>(new DbAction
            {
                Token = token,
                Type = type,
                Expiration = expiration,
                Data = d,
            }).ConfigureAwait(false);
            if (context != null)
                context.Session.Set(ShortCodeName, new ShortData(shortCode, token.Replace(' ', '+'), shortExpiration ?? expiration, type));
            return Tuple.Create(token, shortCode);
        }

        async Task<Tuple<String, T>> GetAction<T>(String token, String type, HttpServerRequest context, T onFail = default)
        {
#if DEBUG
            if (!type.IsAsciiOnly())
                throw new ArgumentException("May only be ascii", nameof(type));
            if (type.Length > 8)
                throw new ArgumentException("Max length of 8", nameof(type));
#endif//DEBUG
            bool removeIfFail = false;
            if ((context != null) && (token.Length <= 32))
            {
                var shortCode = CleanUpShortCode(token);
                if (shortCode != null)
                {
                    var s = context.Session;
                    if (s.TryGet<ShortData>(ShortCodeName, out var shortData))
                    {
                        if (DateTime.UtcNow >= shortData.Expiration)
                        {
                            s.TryRemove(ShortCodeName);
                        }
                        else
                        {
                            if (shortData.ShortCode.FastEquals(shortCode) && shortData.Type.FastEquals(type))
                            {
                                removeIfFail = true;
                                token = shortData.Token;
                            }
                            else
                            {
                                --shortData.TriesLeft;
                                if (shortData.TriesLeft < 0)
                                    s.TryRemove(ShortCodeName);
                            }
                        }
                    }
                }
            }
            DbAction d;
            token = token.Replace(' ', '+');
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                d = await c.FirstOrDefaultAsync<DbAction>(x => x.Token == token).ConfigureAwait(false);
            if ((d == null) || (DateTime.UtcNow > d.Expiration) || (!type.FastEquals(type)))
            {
                if (removeIfFail)
                    context.Session.TryRemove(ShortCodeName);
                return default;
            }
            return Tuple.Create(token, Data.FromData<T>(d.Data));
        }


        async Task DeleteAction(OrmConnection c, String token, String type, HttpServerRequest context)
        {
#if DEBUG
            if (!type.IsAsciiOnly())
                throw new ArgumentException("May only be ascii", nameof(type));
            if (type.Length > 8)
                throw new ArgumentException("Max length of 8", nameof(type));
#endif//DEBUG
            token = token.Replace(' ', '+');
            if (context != null)
            {
                var s = context.Session;
                if (s.TryGet<ShortData>(ShortCodeName, out var shortData))
                {
                    if (shortData.Token.FastEquals(token))
                        s.TryRemove(ShortCodeName);
                }
            }
            await c.DeleteAllAsync<DbAction>(x => x.Token == token && x.Type == type).ConfigureAwait(false);
        }


        readonly DataBlob Data;

        readonly ServiceManager Manager;
        readonly UserManagerParams Params;



        /// <summary>
        /// Sign-up is handled in a special service so an application can determine if they want to support it or not
        /// </summary>
        /// <param name="target">Communication target of the new user</param>
        /// <param name="context">Context</param>
        /// <returns>True if a signup request could be sent</returns>
        internal async Task<bool> SignUp(String target, HttpServerRequest context)
        {
            target = target?.Trim();
            var com = GetComs(ref target);
            if (com == null)
                throw new Exception("Don't know how to send messages to " + target.ToQuoted());
            var s = context?.Session;
            if (s == null)
                throw new NoSessionException();
            if (s.Auth != null)
                throw new UserAlreadyLoggedInException();
            var cn = com.Name;
            var salt = AuthTools.GetRandomSalt();
            var tokens = await GetNewUserToken(null, cn == "Email" ? target : null, cn == "Phone" ? target : null, salt, null, null, null, null, context).ConfigureAwait(false);
            String link = String.Concat(context?.Prefix ?? "", "auth/ChoosePassword.html?", HttpUtility.UrlEncode(tokens.Item1));
            /*            String link = String.Concat(context?.Prefix ?? "", "auth/ChoosePassword.html?user=",
                            HttpUtility.UrlEncode(target),
                            "&salt=",
                            HttpUtility.UrlEncode(salt),
                            "&token=",
                            HttpUtility.UrlEncode(token));
            */
            var d = GetMessageParams(context,
                    "[Email]", target,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );

            var lang = await GetLang(context?.Session?.Language).ConfigureAwait(false);
            await com.Send(UserManagerComOps.SignUp, lang, target, d, TextSystemString).ConfigureAwait(false);
            return true;
        }

        static readonly new Task<ManagedLanguageMessages> NoLang = Task.FromResult((ManagedLanguageMessages)null);

        Task<ManagedLanguageMessages> GetLang(String language)
        {
            var m = Messages;
            if (m == null)
                return NoLang;
            return m.GetLang(language, HttpServer?.Translator);
        }



        static readonly UserLoginResponse FailedNoSession = new UserLoginResponse
        {
            Error = UserErrors.NoSession,
        };


        static readonly UserLoginResponse FailedTokenExpired = new UserLoginResponse
        {
            Error = UserErrors.TokenExpired,
        };

        static readonly UserLoginResponse FailedUserAlreadyExist = new UserLoginResponse
        {
            Error = UserErrors.UserAlreadyExist,
        };

        static readonly UserLoginResponse FailedUserDoesntExist = new UserLoginResponse
        {
            Error = UserErrors.UserDoesntExist,
        };

        readonly MySqlDbSimpleStack Db;


        #region Custom Data

        /// <summary>
        /// Get some data associated with a user
        /// </summary>
        /// <typeparam name="T">The type of data to get</typeparam>
        /// <param name="key">The key that identifies this data</param>
        /// <param name="userGuid">The user guid</param>
        /// <returns>null if not found (or null) else the data</returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> GetUserData<T>(String key, String userGuid) where T : class
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key to long!");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            DbUserData dbData;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                dbData = await c.FirstOrDefaultAsync<DbUserData>(s =>
                {
                    s.Where(x => x.UserGuid == userGuid);
                    s.Where(x => x.DataKey == key);
                }).ConfigureAwait(false);
            if (dbData == null)
                return null;
            return Data.FromData<T>(dbData.Data);
        }

        /// <summary>
        /// Associate some data with a user
        /// </summary>
        /// <typeparam name="T">The type of data to set</typeparam>
        /// <param name="key">The key that identifies this data</param>
        /// <param name="userGuid">The user guid</param>
        /// <param name="data">The data to associate to the user (may be null)</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task SetUserData<T>(String key, String userGuid, T data) where T : class
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key to long!");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            var dbData = new DbUserData
            {
                DataKey = key,
                UserGuid = userGuid,
                Data = data == null ? null : Data.ToData(data),
            };
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                await c.UpdateAsync(dbData).ConfigureAwait(false);
        }


        /// <summary>
        /// Get some data associated with a user
        /// </summary>
        /// <param name="key">The key that identifies this data</param>
        /// <param name="userGuid">The user guid</param>
        /// <returns>null if not found (or null) else the data</returns>
        /// <exception cref="Exception"></exception>
        public async Task<String> GetUserDataString(String key, String userGuid)
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key to long!");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            DbUserDataString dbData;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                dbData = await c.FirstOrDefaultAsync<DbUserDataString>(s =>
                {
                    s.Where(x => x.UserGuid == userGuid);
                    s.Where(x => x.DataKey == key);
                }).ConfigureAwait(false);
            return dbData?.Data;
        }

        /// <summary>
        /// Associate a string with a user
        /// </summary>
        /// <param name="key">The key that identifies this data</param>
        /// <param name="userGuid">The user guid</param>
        /// <param name="data">The data to associate to the user (may be null)</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task SetUserDataString(String key, String userGuid, String data)
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key to long!");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            if (data?.Length > UserManagerTools.MaxDataStringLength)
                throw new Exception("String may not be longer than " + UserManagerTools.MaxDataStringLength);
            key = key.FastToLower();
            var dbData = new DbUserDataString
            {
                DataKey = key,
                UserGuid = userGuid,
                Data = data,
            };
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                await c.UpdateAsync(dbData).ConfigureAwait(false);
        }

        #endregion//Custom Data

    }
}
