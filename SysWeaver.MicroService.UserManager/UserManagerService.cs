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
using SysWeaver.Security;
using SimpleStack.Orm;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SysWeaver.IsoData;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Manages users (stored in a MySql database) and authenticates them with passwords, passkeys or action tokens sent by e-mail or text message.
    /// Also handles sign up, invitations, password resets and the user administration.
    /// </summary>
    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/LostPassword", "Forgot password", "auth/ForgotPassword.html", "Click to recover your password", "IconRecover", 20, null, true)]

    [WebMenuEmbedded("User", "User/AddPassword", "Add a password", "auth/AddPassword.html", "Click to add a password to your account", "IconAddPassword", 30, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddPassword))]
    [WebMenuEmbedded("User", "User/ChangePassword", "Change password", "auth/SetPassword.html", "Click to change your password", "IconChangePassword", 31, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangePassword))]
    [WebMenuEmbedded("User", "User/DeletePassword", "Delete password", "auth/DeletePassword.html", "Click to delete your account password", "IconDeletePassword", 32, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeletePassword))]

    [WebMenuEmbedded("User", "User/AddEmail", "Add email address", "auth/AddEmail.html", "Click to associate an email address to your account", "IconAddEmail", 40, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddEmail))]
    [WebMenuEmbedded("User", "User/ChangeEmail", "Change email address", "auth/ChangeEmail.html", "Click to change your associated email address", "IconChangeEmail", 41, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangeEmail))]
    [WebMenuEmbedded("User", "User/DeleteEmail", "Remove email address", "auth/DeleteEmail.html", "Click to remove your associated email address", "IconDeleteEmail", 42, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeleteEmail))]

    [WebMenuEmbedded("User", "User/AddPhone", "Add phone number", "auth/AddPhone.html", "Click to associate a phone number to your account", "IconAddPhone", 50, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanAddPhone))]
    [WebMenuEmbedded("User", "User/ChangePhone", "Change phone number", "auth/ChangePhone.html", "Click to change your associated phone number", "IconChangePhone", 51, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanChangePhone))]
    [WebMenuEmbedded("User", "User/DeletePhone", "Remove phone number", "auth/DeletePhone.html", "Click to remove your associated phone number", "IconDeletePhone", 52, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDeletePhone))]

    [WebMenuEmbedded("User", "User/DeleteAccount", "Delete account", "auth/DeleteAccount.html", "Click to delete this account", "IconDelete", 100, "", false, nameof(SysWeaver) + "." + nameof(MicroService) + "." + nameof(UserManagerService) + "." + nameof(UserManagerService.CanDelete))]
    //  One place for the user accounts, the original's: User menu > Management > Invite user / Users (Users replaces the original "View users"
    //  table item, same roles). S7a O3: the rewrite's copies in the main menu (Administration > Users > Invite user) are gone, the original had none.
    [WebMenuPath("User", "User/Management", "Management", "User management tools, manage other users", null, 100000)]
    [WebMenuEmbedded("User", "User/Management/InviteUser", "Invite user", "auth/InviteUser.html", "Click to invite a user to join this site", "IconInvite", 0, UserManagerRoles.InviteUser)]
    [WebMenuEmbedded("User", "User/Management/Users", "Users", "auth/Users.html", "The user accounts of this site: find a user, invite a user or force a password reset", "IconUsers", 1, UserManagerRoles.ViewUser)]
    [IsMicroService]
    [RequiredDep<IEmailSender>]
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
            if (!ComsMethods.ContainsKey("Email"))
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
            if (!ComsMethods.ContainsKey("Email"))
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
            if (!ComsMethods.ContainsKey("Email"))
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
            if (!(ComsMethods.ContainsKey("Sms") || ComsMethods.ContainsKey("WhatsApp")))
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
            if (!(ComsMethods.ContainsKey("Sms") || ComsMethods.ContainsKey("WhatsApp")))
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
            if (!(ComsMethods.ContainsKey("Sms") || ComsMethods.ContainsKey("WhatsApp")))
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

        readonly bool SendAllComs;

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="manager">The service manager (used to find the communication methods, e-mail sender and other services)</param>
        /// <param name="p">Parameters, null to use the defaults</param>
        public UserManagerService(ServiceManager manager, UserManagerParams p)
        {
            p = p ?? new();
            Params = p;
            InternalPasswordPolicy = p.PasswordPolicy ?? new PasswordPolicy();
            DisableLogin = p.DisableLogin;
            //  Add and setup communication methods
            var allComs = manager.GetAllInfo<IUserManagerComs>(ServiceInstanceTypes.LocalOnly, ServiceInstanceOrders.Oldest).ToList();
            //  No coms services: e-mail through an e-mail sender, if there is one (without a sender nothing can be sent: no e-mail coms,
            //  the pages then don't offer e-mail). EmailInstance may name the sender (an e-mail coms named so doesn't exist then): it is
            //  used, and the default coms is found by that name too.
            EmailComs defaultEmail = null;
            if ((allComs.Count == 0) && (!p.DisableLogin))
            {
                var sender = DefaultEmailSender(manager, p.EmailInstance);
                if (sender == null)
                    manager.AddMessage("There is no e-mail sender (IEmailSender) and no other communication service: invitations, sign ups and password resets can't be sent", MessageLevels.Warning);
                else
                {
                    defaultEmail = new EmailComs(manager, new EmailComsParams { Instance = sender });
                    allComs.Add(new KeyValuePair<IUserManagerComs, ServiceInfo>(defaultEmail, null));
                }
            }
            Dictionary<String, IUserManagerComs> valid = new Dictionary<string, IUserManagerComs>(StringComparer.Ordinal);
            foreach (var x in allComs)
            {
                var i = x.Key;
                var name = i.Name;
                var inst = x.Value?.Name;
                if (!String.IsNullOrEmpty(inst))
                    valid[$"{name}\n{inst}"] = i;
                valid[name] = i;
            }
            if ((defaultEmail != null) && !String.IsNullOrEmpty(p.EmailInstance))
                valid[$"Email\n{p.EmailInstance}"] = defaultEmail;

            //  A coms by its name and the configured instance. An instance that doesn't exist: the coms of that name (with a warning), before
            //  this a wrong instance name left the coms out and nothing could be sent ("Enter a valid e-mail address or phone number" for
            //  every invitation)
            IUserManagerComs find(String comName, String instance)
            {
                if (String.IsNullOrEmpty(instance))
                    return valid.TryGetValue(comName, out var c) ? c : null;
                if (valid.TryGetValue($"{comName}\n{instance}", out var ci))
                    return ci;
                if (valid.TryGetValue(comName, out var cn))
                {
                    manager.AddMessage(MessageLevels.Warning, $"There is no {comName} communication service named {instance.ToQuoted()}, using the {comName} service there is");
                    return cn;
                }
                return null;
            }

            List<IUserManagerComs> orderedComs = new(allComs.Count);
            var comsOrder = p.ComsOrder?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            SendAllComs = (comsOrder?.Length ?? 0) <= 0;
            void addCom(IUserManagerComs com)
            {
                if ((com != null) && !orderedComs.Contains(com))
                    orderedComs.Add(com);
            }
            if (SendAllComs)
            {
                addCom(find("Email", p.EmailInstance));
                addCom(find("WhatsApp", p.WhatsAppInstance));
                addCom(find("Sms", p.SmsInstance));
            }
            else
            {
                foreach (var x in comsOrder)
                {
                    if (x.FastEquals("Email"))
                        addCom(find("Email", p.EmailInstance));
                    if (x.FastEquals("WhatsApp"))
                        addCom(find("WhatsApp", p.WhatsAppInstance));
                    if (x.FastEquals("Sms"))
                        addCom(find("Sms", p.SmsInstance));
                }
            }
            if ((orderedComs.Count == 0) && !p.DisableLogin)
                manager.AddMessage("The user manager has no way to send messages (no e-mail, WhatsApp or text message service): invitations, sign ups and password resets can't be sent", MessageLevels.Warning);

            Coms = orderedComs.ToArray();
            ComsMethods = orderedComs.ToDictionary(x => x.Name, StringComparer.Ordinal).Freeze();

            InternalSignUpMethods = orderedComs.Where(x => x.AllowSignUp).Select(x => x.Name).ToArray();

            var shortCodeDigits = p.ShortCodeDigits;
            if (shortCodeDigits < 3)
                shortCodeDigits = 3;
            if (shortCodeDigits > 16)
                shortCodeDigits = 16;
            ShortCodeDigits = shortCodeDigits;
            CodeTryLimits = GetCodeTryLimits(p.CodeTriesPerUser);
            ResetCodeTryLimits = CodeTryLimits.Length > 0 ? CodeTryLimits : [new RateLimiterParams { Count = 10, Duration = 15 * 60 }];


            ManProps = new UserManagerProps
            {
                Coms = ComsMethods.Keys.ToArray(),
                //  Empty until a SignUpService registers (EnableSignUp): without it there is no sign up api, so no method may be offered
                SignUpMethods = [],
                CanSetNickName = p.CanSetNickName,
                CanLogin = !p.DisableLogin,
                ShortCodeDigits = shortCodeDigits,
            };


            AuthMan = manager.TryGet<AuthManagerService>();
            HttpServer = manager.TryGet<HttpServerBase>();
            manager.OnServiceAdded += Manager_OnServiceAdded;

            SystemString = new ManagedString(PathTemplate.Resolve(p.System));
            manager.AddMessage(MessageLevels.Debug, $"Message system: {SystemString.Value.ToQuoted()}");


            Manager = manager;
            var db = new MySqlDbSimpleStack(p, manager);
            Db = db;
            Data = DataBlob.Get("json", "br", CompEncoderLevels.Best);

            HtmlLogo = SysWeaverLogo.GetHtmlLogo(ConsoleColor.Green, ConsoleColor.DarkGreen, "  ");
            HtmlLogoGradient = SysWeaverLogo.GetHtmlLogoGradient("  ");

            CanSetNickName = p.CanSetNickName;

            //File.WriteAllText("Logo.html", HtmlLogo);
            //File.WriteAllText("LogoGradient.html", HtmlLogoGradient);

            Messages = String.IsNullOrEmpty(p.LocalizedMessages) ? null : new ManagedMessages(p.LocalizedMessages, EnvInfo.AppLanguage ?? "en-US", true,
                ManagedMessageTypes.Text | ManagedMessageTypes.Email,
                ManagedMessageTypes.All,
                //  The variables besides the standard ones: "[UndoLink]" of the changed e-mail notice
                ChangedEmailMail.UserManagerVars,
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
                //  Optional (newer than the other templates): folders without it use the built in template
                "*ChangedEmail",
                "*Header",
                "*Footer"
                );

            InitAndMigrate(db).RunAsync();
            PruneTask = new PeriodicTask(Prune, 15000);
        }

        readonly UserManagerProps ManProps;

        /// <summary>
        /// The e-mail sender of the default e-mail coms (used when no coms service is configured)
        /// </summary>
        /// <param name="manager">The service manager</param>
        /// <param name="preferred">The configured instance (UserManagerParams.EmailInstance): used when an e-mail sender has that name</param>
        /// <returns>The instance name of the sender: the preferred one, else "useradmin" (the default of EmailComsParams), else "" (any
        /// sender); null if there is no e-mail sender</returns>
        static String DefaultEmailSender(ServiceManager manager, String preferred)
        {
            preferred = preferred?.Trim();
            if (!String.IsNullOrEmpty(preferred) && (manager.TryGet<IEmailSender>(preferred) != null))
                return preferred;
            const String usual = "useradmin";
            if (manager.TryGet<IEmailSender>(usual) != null)
                return usual;
            return manager.TryGet<IEmailSender>() != null ? "" : null;
        }

        /// <summary>
        /// The text of a target that no coms can send to: names what can be used (only e-mail, only phone numbers, or both)
        /// </summary>
        /// <param name="target">The given target</param>
        /// <param name="signUp">True for a sign up: only the coms that allow it</param>
        /// <returns>The error to throw</returns>
        HttpResponseException InvalidTargetError(String target, bool signUp = false)
        {
            var coms = signUp ? Coms.Where(x => x.AllowSignUp).ToArray() : Coms;
            if (coms.Length == 0)
                return signUp && (Coms.Length > 0)
                    ? new HttpResponseException(403, "Signing up is not available on this site")
                    : new HttpResponseException(503, "Messages can't be sent: there is no e-mail or text message service set up");
            var email = coms.Any(x => x.Name == "Email");
            var phone = coms.Any(x => x.Name != "Email");
            var what = email && phone ? "a valid e-mail address or phone number"
                : (email ? "a valid e-mail address" : "a valid phone number (with the country code, ex: +46 70 123 45 67)");
            return new HttpResponseException(400, "Enter " + what + " (" + (target ?? "").ToQuoted() + " is not)");
        }

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

        /// <summary>
        /// The root of the links in the messages (see <see cref="HttpServerBase.GetLinkRoot"/>): with a wildcard listener prefix the
        /// configured external root, never the client's Host header (it could make a reset link point to someone else's site)
        /// </summary>
        /// <param name="context">The request, null for none</param>
        String LinkRoot(HttpServerRequest context)
            => context?.Server?.GetLinkRoot(context) ?? HttpServer?.GetLinkRoot(null) ?? "";
        
        readonly ManagedMessages Messages;

        readonly String[] InternalSignUpMethods;

        /// <summary>
        /// The sign up methods offered to clients: empty until a <see cref="SignUpService"/> calls <see cref="EnableSignUp"/>
        /// </summary>
        String[] ActiveSignUpMethods = [];

        /// <summary>
        /// Offer the configured sign up methods to clients (<see cref="SignUpMethods"/>, <see cref="UserManagerProps"/>).
        /// Called by the <see cref="SignUpService"/> (the web api of the sign up) when it is created.
        /// </summary>
        internal void EnableSignUp()
        {
            ActiveSignUpMethods = InternalSignUpMethods;
            ManProps.SignUpMethods = InternalSignUpMethods;
        }

        readonly IUserManagerComs[] Coms;

        readonly IReadOnlyDictionary<String, IUserManagerComs> ComsMethods;


        public override PasswordPolicy PasswordPolicy => InternalPasswordPolicy;

        readonly PasswordPolicy InternalPasswordPolicy;

        PeriodicTask PruneTask;

        async Task<bool> Prune()
        {
            //  First: no database needed (a database error doesn't stop it)
            PruneResetPasswordSent();
            PruneCodes();
            //  The database can't be used (or isn't ready yet: the migration runs)
            if (!PiiReady.Task.IsCompletedSuccessfully)
                return true;
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                var now = DateTime.UtcNow;
                await c.DeleteAllAsync<DbAction>(x => now > x.Expiration).ConfigureAwait(false);
            }
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            SystemString.Dispose();
            Manager.OnServiceAdded -= Manager_OnServiceAdded;
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            //  The password reset work that waits ends (the running sends finish)
            ResetPasswordStop.Cancel();
        }

        readonly String HtmlLogo;
        readonly String HtmlLogoGradient;

        /// <summary>
        /// Init the tables that don't hold protected values (the user, e-mail and phone tables are made by <see cref="InitProtectedTables"/>, after the migration)
        /// </summary>
        /// <remarks>db.Init must have been called (and the migration lock should be held, see <see cref="InitAndMigrate"/>)</remarks>
        static async Task InitTables(MySqlDbSimpleStack db)
        {
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbToken>(c).ConfigureAwait(false);
                await db.InitTable<DbAction>(c).ConfigureAwait(false);
                await db.InitTable<DbAuthPassKey>(c).ConfigureAwait(false);
                await db.InitTable<DbAuthPassword>(c).ConfigureAwait(false);
                await db.InitTable<DbUserData>(c).ConfigureAwait(false);
                await db.InitTable<DbUserDataString>(c).ConfigureAwait(false);
                await db.InitTable<DbUserManagerMeta>(c).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Get the variables used by the message templates
        /// </summary>
        /// <param name="context">The request (used for the root and the origin), null to use the external root</param>
        /// <param name="add">Additional variables, as name, value pairs (an odd last name is ignored), null for none</param>
        /// <returns>A new dictionary with the variables</returns>
        public Dictionary<String, String> GetMessageParams(HttpServerRequest context, params String[] add)
            //  A null array is the same as no additional parameters
            => GetMessageParams(context, (ReadOnlySpan<String>)add);

        /// <summary>
        /// Get the variables used by the message templates
        /// </summary>
        /// <param name="context">The request (used for the root and the origin), null to use the external root</param>
        /// <param name="add">Additional variables, as name, value pairs (an odd last name is ignored)</param>
        /// <returns>A new dictionary with the variables</returns>
        public Dictionary<String, String> GetMessageParams(HttpServerRequest context, params ReadOnlySpan<String> add)
        {
            var cols = HashColors.AppColors;
            var d = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                {  "[Site]", EnvInfo.AppDisplayName },
                {  "[Root]", LinkRoot(context) },
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
                    "<a href=\"https://www.abuseipdb.com/check/", HttpUtility.HtmlAttributeEncode(ip), "\" style=\"color: ", cols.Acc1, "; text-decoration: none;\">", HttpUtility.HtmlEncode(ip), "</a>",
                    " <a href=\"https://gs.statcounter.com/detect?useragent=", HttpUtility.HtmlAttributeEncode(userAgent), "\" style=\"color: ", cols.Acc1, "; text-decoration: none;\">", HttpUtility.HtmlEncode("🕵️"), "</a>"
                    );
                d["[Origin]"] = s;
            }
            else
            {
                d["[Origin]"] = HttpUtility.HtmlEncode(EnvInfo.AppDisplayName);
            }
            var al = add.Length & ~1;
            for (int i = 0; i < al; i += 2)
                d[add[i]] = add[i + 1];
            return d;
        }


        async Task SendOne(List<String> sentTo, UserManagerComOps message, ManagedLanguageMessages lang, String target, IUserManagerComs coms, Dictionary<String, String> vars)
        {
            await coms.Send(message, lang, target, vars, TextSystemString).ConfigureAwait(false);
            lock (sentTo)
                sentTo.Add(target);
        }

        async Task SendOne(List<String> sentTo, String message, ManagedLanguageMessages messages, String target, IUserManagerComs coms, Dictionary<String, String> vars, params ManagedLanguageMessages[] fallbacks)
        {
            await coms.Send(message, messages, target, vars, TextSystemString, fallbacks).ConfigureAwait(false);
            lock (sentTo)
                sentTo.Add(target);
        }

        /// <summary>
        /// Send a custom message to a user's registered emails and phone numbers
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <param name="message">The message key to send (key into the messages returned by getMessageData)</param>
        /// <param name="getMessageData">A function that given the communication type (Email, WhatsApp, Sms) and language, returns the localized messages and variables to use</param>
        /// <param name="fallBackLanguage">The language to use if the member haven't got a preferred language</param>
        /// <returns>The email and phone numbers that the message was sent to</returns>
        /// <exception cref="UserDoNotExistException"></exception>
        /// <exception cref="UserHaveNoComs"></exception>
        public async Task<String[]> SendCustomUserMessage(long userId, String message, Func<String, IUserManagerComs, String, ValueTask<ValueTuple<ManagedLanguageMessages, Dictionary<String, String>>>> getMessageData, String fallBackLanguage = null)
        {
            var user = await TryGetUser(userId).ConfigureAwait(false);
            if (user == null)
                throw new UserDoNotExistException(userId.ToString());
            var userName = user.NickName ?? user.UserName;
            List<String> targets;
            using (var c = await DbGetAsync().ConfigureAwait(false))
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
            if (targets.Count <= 0)
                throw new UserHaveNoComs();
            var commonVars = GetMessageParams(null, "[UserName]", userName);
            var lang = user.Language ?? fallBackLanguage ?? "en";
            var l = targets.Count;
            var coms = Coms;
            var cl = coms.Length;
            if (SendAllComs)
            {
                List<String> sentTo = new List<string>(l);
                List<Task> tasks = new List<Task>(l);
                for (int i = 0; i < l; ++i)
                {
                    var target = targets[i];
                    for (int j = 0; j < cl; ++j)
                    {
                        var com = coms[j];
                        if (!com.CleanAndValidate(ref target))
                            continue;
                        var data = await getMessageData(target, com, lang).ConfigureAwait(false);
                        var vars = data.Item2;
                        if (vars == null)
                            vars = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var x in commonVars)
                            vars.TryAdd(x.Key, x.Value);
                        tasks.Add(SendOne(sentTo, message, data.Item1, target, com, vars));
                        break;
                    }
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                return sentTo.ToArray();
            }
            else
            {
                List<String> sentTo = new List<string>(1);
                for (int j = 0; j < cl; ++j)
                {
                    var com = coms[j];
                    for (int i = 0; i < l; ++i)
                    {
                        var target = targets[i];
                        if (!com.CleanAndValidate(ref target))
                            continue;

                        var data = await getMessageData(target, com, lang).ConfigureAwait(false);
                        var vars = data.Item2;
                        if (vars == null)
                            vars = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var x in commonVars)
                            vars.TryAdd(x.Key, x.Value);
                        await SendOne(sentTo, message, data.Item1, target, com, vars).ConfigureAwait(false);
                        if (sentTo.Count > 0)
                            return sentTo.ToArray();
                    }
                }
                return Array.Empty<String>();
            }
        }



        /// <summary>
        /// Get a communication method for a given target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <param name="signUp">True for a sign up: only the coms that allow it (<see cref="IUserManagerComs.AllowSignUp"/>, the sign up
        /// methods the sign up page offers)</param>
        IUserManagerComs GetComs(ref String target, bool signUp = false)
        {
            foreach (var com in Coms)
            {
                if (signUp && !com.AllowSignUp)
                    continue;
                if (com.CleanAndValidate(ref target))
                    return com;
            }
            return null;
        }


        /// <summary>
        /// Send a message to a user's registered emails and phone numbers
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <param name="message">The message key to send (key into the messages)</param>
        /// <param name="messages">The localized messages</param>
        /// <param name="getVars">A function that should return any message vars</param>
        /// <returns>The email and phone numbers that the message was sent to</returns>
        /// <exception cref="UserDoNotExistException"></exception>
        /// <exception cref="UserHaveNoComs"></exception>
        public async Task<String[]> SendUserMessage(long userId, String message, ManagedLanguageMessages messages, Func<String, IUserManagerComs, Dictionary<String, String>> getVars)
        {
            var user = await TryGetUser(userId).ConfigureAwait(false);
            if (user == null)
                throw new UserDoNotExistException(userId.ToString());
            var userName = user.NickName ?? user.UserName;
            List<String> targets;
            using (var c = await DbGetAsync().ConfigureAwait(false))
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
            if (targets.Count <= 0)
                throw new UserHaveNoComs();
            var commonVars = GetMessageParams(null, "[UserName]", userName);
            var lang = await GetLang(messages.Language).ConfigureAwait(false);
            var l = targets.Count;
            var coms = Coms;
            var cl = coms.Length;
            if (SendAllComs)
            {
                List<String> sentTo = new List<string>(l);
                List<Task> tasks = new List<Task>(l);
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
                            vars = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var x in commonVars)
                            vars.TryAdd(x.Key, x.Value);
                        tasks.Add(SendOne(sentTo, message, messages, target, com, vars, lang));
                        break;
                    }
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                return sentTo.ToArray();
            }
            else
            {
                List<String> sentTo = new List<string>(1);
                for (int j = 0; j < cl; ++j)
                {
                    var com = coms[j];
                    for (int i = 0; i < l; ++ i)
                    {
                        var target = targets[i];
                        if (!com.CleanAndValidate(ref target))
                            continue;
                        var vars = getVars(target, com);
                        if (vars == null)
                            vars = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var x in commonVars)
                            vars.TryAdd(x.Key, x.Value);
                        await SendOne(sentTo, message, messages, target, com, vars, lang).ConfigureAwait(false);
                        if (sentTo.Count > 0)
                            return sentTo.ToArray();
                    }
                }
                return Array.Empty<String>();
            }
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
            var coms = Coms;
            var cl = coms.Length;
            if (SendAllComs)
            {
                List<Task> tasks = new List<Task>(l);
                List<String> sentTo = new List<string>(l);
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
            }else
            {
                List<String> sentTo = new List<string>(1);
                for (int j = 0; j < cl; ++j)
                {
                    var com = coms[j];
                    for (int i = 0; i < l; ++i)
                    {
                        var target = targets[i];
                        if (!com.CleanAndValidate(ref target))
                            continue;
                        var vars = getVars(target, com);
                        if (vars == null)
                            continue;
                        await SendOne(sentTo, message, lang, target, com, vars).ConfigureAwait(false);
                        if (sentTo.Count > 0)
                            return sentTo.ToArray();
                    }
                }
                return Array.Empty<String>();


            }
        }


        async Task<List<String>> GetUserComTargets(OrmConnection c, long userId)
        {
            List<String> targets = new List<string>();
            targets.AddRange((await GetDbEmails(c, userId).ConfigureAwait(false)).Select(x => x.Email));
            targets.AddRange((await GetDbPhones(c, userId).ConfigureAwait(false)).Select(x => x.Phone));
            return targets;
        }




        #region Helpers


        /// <summary>
        /// Get the guid (the unique id used by sessions and authorizations) of a user
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="guidPrefix">The guid prefix of the service (<see cref="GuidPrefix"/>)</param>
        /// <returns>The guid, "prefix:id"</returns>
        public static String MakeGuid(long userId, String guidPrefix)
            => String.Concat(guidPrefix, ':', HashTools.GetCompactString(userId));

        /// <summary>
        /// Get the guid (the unique id used by sessions and authorizations) of a user
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <returns>The guid, "prefix:id"</returns>
        public String MakeGuid(long userId)
            => String.Concat(GuidPrefix, ':', HashTools.GetCompactString(userId));

        static readonly Tuple<long, Byte[]> InvalidCredentialId = new Tuple<long, Byte[]>(0, null);

        /// <summary>
        /// Consume an action token and get the authorization of the user that it belongs to
        /// </summary>
        /// <typeparam name="T">The type of the data stored with the action</typeparam>
        /// <param name="token">The action token (or short code)</param>
        /// <param name="type">The action type, one of the <see cref="Db.DbAction"/> types</param>
        /// <param name="getIdFromData">Gets the user id from the data stored with the action</param>
        /// <param name="context">The request context</param>
        /// <returns>The authorization of the user</returns>
        /// <exception cref="TokenExpiredException">If the token can't be used, or has no user</exception>
        public async Task<Authorization> GetUser<T>(String token, String type, Func<T, long> getIdFromData, HttpServerRequest context)
        {
            var actionData = await GetAction<T>(token, type, context).ConfigureAwait(false);
            if (actionData == null)
                throw Refused(token);
            token = actionData.Item1;
            var id = getIdFromData(actionData.Item2);
            if (id == 0)
                throw new TokenExpiredException();
            using var c = await DbGetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var authData = await GetDbUser(c, id).ConfigureAwait(false);
            if (authData == null)
                throw new UserNoLongerExistException(id);
            var auth = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (auth == null)
                throw await NoAuthException(c, id).ConfigureAwait(false);
            await DeleteAction(c, token, type, context).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return auth;
        }




        /// <summary>
        /// The number of users
        /// </summary>
        public sealed class UserStats
        {
            /// <summary>
            /// Time when this data was collected
            /// </summary>
            public readonly DateTime Time;

            /// <summary>
            /// Current number of users
            /// </summary>
            public readonly long Current;
            
            /// <summary>
            /// Total number of users ever registered
            /// </summary>
            public readonly long Total;

            /// <summary>
            /// Null if eveything is fine
            /// </summary>
            public readonly String ErrorMessage;

            /// <summary>
            /// Create the statistics
            /// </summary>
            /// <param name="time">Time when the data was collected</param>
            /// <param name="current">Current number of users</param>
            /// <param name="total">Total number of users ever registered</param>
            /// <param name="errorMessage">Null if everything is fine</param>
            public UserStats(DateTime time, long current, long total, String errorMessage)
            {
                Time = time;
                Current = current;
                Total = total;
                ErrorMessage = errorMessage;
            }
        }



        volatile UserStats Stats;
        readonly AsyncLock GetStatsLock = new AsyncLock();

        /// <summary>
        /// Get the number of users (cached for 30 seconds)
        /// </summary>
        /// <returns>The statistics</returns>
        public async Task<UserStats> GetUserStats()
        {
            var now = DateTime.UtcNow;
            var renewDate = now.AddSeconds(-30);
            var stats = Stats;
            if ((stats != null) && (stats.Time > renewDate))
                return stats;
            using var _ = await GetStatsLock.Lock().ConfigureAwait(false);
            stats = Stats;
            if ((stats != null) && (stats.Time > renewDate))
                return stats;
            Exception err = null;
            try
            {

                using var c = await DbGetAsync().ConfigureAwait(false);
                var dp = c.DialectProvider;
                var colName = dp.GetQuotedColumnName(nameof(DbUser.Id));
                var tableName = dp.GetQuotedTableName(ModelDefinition<DbUser>.Definition);
                var cmd = String.Concat("SELECT COUNT(", colName, "), MAX(", colName, ") FROM ", tableName);
                using var reader = await c.ReaderCommandAsync(cmd).ConfigureAwait(false);
                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    stats = new UserStats(now, reader.GetInt64(0), Math.Max(0, reader.GetInt64(1)), null);
                    Interlocked.Exchange(ref Stats, stats);
                    return stats;
                }
            }
            catch (Exception ex)
            {
                err = ex;
            }
            if (stats != null)
            {
                stats = new UserStats(now, stats.Current, stats.Total, err?.Message ?? "No new stats, using oldstats");
            }
            else
            {
                stats = new UserStats(now, 0, 0, err?.Message ?? "No stats");
            }
            Interlocked.Exchange(ref Stats, stats);
            return stats;
        }


        /// <summary>
        /// Return true if there are any users in the db
        /// </summary>
        /// <returns>True if at least one user account exists</returns>
        public async Task<bool> HaveAnyUser()
        {
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await c.CountAsync<DbUser>().ConfigureAwait(false)) > 0;
        }

        /// <summary>
        /// Return true if the user have a password
        /// </summary>
        /// <param name="uid">The user id (0 is no user)</param>
        /// <returns>True if the user has a password</returns>
        public async Task<bool> HaveAnyPassword(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await DbGetAsync().ConfigureAwait(false);
            //  A row without a hash is no password (GetSaltAsync adds one for a user without a password when anyone asks for the salt)
            return IsUsablePassword(await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == uid).ConfigureAwait(false));
        }


        /// <summary>
        /// Return true if the user have an e-mail address
        /// </summary>
        /// <param name="uid">The user id (0 is no user)</param>
        /// <returns>True if the user has at least one e-mail address</returns>
        public async Task<bool> HaveAnyEmail(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await GetDbEmail(c, uid).ConfigureAwait(false)) != null;
        }

        /// <summary>
        /// Return true if the user have a phone number
        /// </summary>
        /// <param name="uid">The user id (0 is no user)</param>
        /// <returns>True if the user has at least one phone number</returns>
        public async Task<bool> HaveAnyPhone(long uid)
        {
            if (uid == 0)
                return false;
            using var c = await DbGetAsync().ConfigureAwait(false);
            return (await GetDbPhone(c, uid).ConfigureAwait(false)) != null;
        }


        static String GetNick(String userName, String mail)
        {
            var nickName = (userName.FastEquals(mail) ? mail.Split('@')[0] : null).LimitLength(AuhorizationLimits.MaxNickNameLength, "");
            return nickName;
        }

        /// <summary>
        /// Create a user using a create user action token
        /// </summary>
        /// <param name="r">The request</param>
        /// <param name="context">The request context</param>
        /// <param name="login">True to sign in the new user</param>
        /// <returns>The new user</returns>
        public Task<Authorization> CreateUser(AddUserRequest r, HttpServerRequest context, bool login)
            => CreateUser(r, context, login, null);

        /// <summary>
        /// Create a user using a create user action token
        /// </summary>
        /// <param name="r">The request</param>
        /// <param name="context">The request context</param>
        /// <param name="login">True to sign in the new user</param>
        /// <param name="beforeCommit">Optional function that is called with the connection and the new user id before the transaction is committed (to add auth methods atomically)</param>
        /// <returns>The new user</returns>
        async Task<Authorization> CreateUser(AddUserRequest r, HttpServerRequest context, bool login, Func<OrmConnection, long, Task> beforeCommit)
        {
            var s = context?.Session;
            if (login)
                if (s == null)
                    throw new NoSessionException();
            var token = r.Token;
            var dataD = await GetAction<NewUserData>(token, DbAction.CreateUser, context).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
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
            var tokenSet = tokens == null ? null : new HashSet<String>(tokens.Select(x => x.FastTrimToLower()), StringComparer.Ordinal);
            long id;
            Authorization auth;
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                if (email != null)
                {
                    var f = await FindDbEmail(c, email).ConfigureAwait(false);
                    if (f != null)
                        throw new UserAlreadyExistException(email);
                }
                if (phone != null)
                {
                    var f = await FindDbPhone(c, phone).ConfigureAwait(false);
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
                id = await InsertUser(c, user).ConfigureAwait(false);
                user.Id = id;
                if (email != null)
                {
                    await InsertEmail(c, new DbEmailAddress
                    {
                        Email = email,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (phone != null)
                {
                    await InsertPhone(c, new DbPhoneNumber
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
                    var pwdRow = new DbAuthPassword
                    {
                        MustResetPassword = false,
                        Salt = salt,
                        Created = now,
                        LastUsed = now,
                        UserId = id,
                    };
                    SetPasswordHash(pwdRow, r.NewHash);
                    await c.InsertAsync(pwdRow).ConfigureAwait(false);
                }
                if (beforeCommit != null)
                    await beforeCommit(c, id).ConfigureAwait(false);
                auth = await InternalAuthUser(c, id).ConfigureAwait(false);
                if (auth == null)
                    throw new UserDoNotExistException(userName);
                await DeleteAction(c, token, DbAction.CreateUser, context).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            //  Audit: the new account, how it came to be (invitation or sign up) and its sign in method and tokens (no secrets)
            {
                var how = String.IsNullOrEmpty(u.InvitedBy) ? "signed up" : $"accepted the invitation by \"{u.InvitedBy}\"";
                var method = !String.IsNullOrEmpty(r.NewHash) ? "a password" : (beforeCommit != null ? "a passkey" : "no sign in method yet");
                var tokenText = (tokenSet == null) || (tokenSet.Count == 0) ? "no tokens" : String.Concat("the tokens ", String.Join(", ", tokenSet));
                Manager.AddMessage(MessageLevels.Info, $"User #{id} was created: {how}, with {method} and {tokenText}");
            }
            await RaiseOnUserCreated(id, userName).ConfigureAwait(false);
            if (login)
            {
                s.SetAuth(auth);
                await context.Server.RunOnLogin(s, context).ConfigureAwait(false);
            }
            return auth;
        }

        /// <summary>
        /// Try to add a new user, the user name is the target (and the nick name is the part before the "@" of an e-mail address)
        /// </summary>
        /// <param name="type">The type of the target</param>
        /// <param name="target">The e-mail address or phone number of the new user (must be validated by the caller)</param>
        /// <param name="domain">Optional application specific domain of the user</param>
        /// <param name="tokens">Optional security tokens to give the user</param>
        /// <param name="language">Optional preferred language of the user</param>
        /// <returns>True and the id of the new user, or false and the id of the existing user that already has the e-mail address or phone number</returns>
        /// <exception cref="Exception">If the type isn't supported</exception>
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
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);

                if (email != null)
                {
                    var f = await FindDbEmail(c, email).ConfigureAwait(false);
                    if (f != null)
                        return Tuple.Create(false, f.UserId);
                }
                if (phone != null)
                {
                    var f = await FindDbPhone(c, phone).ConfigureAwait(false);
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
                id = await InsertUser(c, user).ConfigureAwait(false);
                user.Id = id;
                if (email != null)
                {
                    await InsertEmail(c, new DbEmailAddress
                    {
                        Email = email,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (phone != null)
                {
                    await InsertPhone(c, new DbPhoneNumber
                    {
                        Phone = phone,
                        UserId = id,
                    }).ConfigureAwait(false);
                }
                if (tokens != null)
                {
                    var ts = new HashSet<String>(tokens.Select(x => x?.FastTrimToLower()).Where(x => !String.IsNullOrEmpty(x)), StringComparer.Ordinal);
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
        /// <param name="request">The request (its session is used)</param>
        /// <returns>The user of the session, or null if nobody is signed in or the user isn't managed by this service</returns>
        public DbUser GetCurrentUser(HttpServerRequest request)
            => (request.Session?.Auth?.AuthContext as DbUser);

        /// <summary>
        /// Get information about the currently logged in user
        /// </summary>
        /// <param name="session">The session</param>
        /// <returns>The user of the session, or null if nobody is signed in or the user isn't managed by this service</returns>
        public DbUser GetCurrentUser(HttpSession session)
            => (session?.Auth?.AuthContext as DbUser);

        /// <summary>
        /// Get detailed user information
        /// </summary>
        /// <param name="request">The request (its session is used)</param>
        /// <returns>Detailed information about the signed in user, or null if nobody is signed in or the user isn't managed by this service</returns>
        public async Task<UserInfo> GetCurrentUserInfo(HttpServerRequest request)
        {
            var user = request.Session?.Auth?.AuthContext as DbUser;
            if (user == null)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            return await InternalTryGetUserInfo(c, user.Id, user).ConfigureAwait(false);
        }

        /// <summary>
        /// Find a user by user name, e-mail address or phone number
        /// </summary>
        /// <param name="identifier">The user name, e-mail address or phone number</param>
        /// <returns>The id of the user, 0 if no user was found</returns>
        public async Task<long> TryGetUser(String identifier)
        {
            using (var c = await DbGetAsync().ConfigureAwait(false))
                return await FindUser(c, identifier).ConfigureAwait(false);
        }


        async Task<UserInfo> InternalTryGetUserInfo(OrmConnection c, long userId, DbUser user = null)
        {
            user = user ?? (await GetDbUser(c, userId).ConfigureAwait(false));
            if (user == null)
                return null;
            var email = await GetDbEmail(c, userId).ConfigureAwait(false);
            var phone = await GetDbPhone(c, userId).ConfigureAwait(false);
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

        /// <summary>
        /// Get information about a user, found by user name, e-mail address or phone number
        /// </summary>
        /// <param name="identifier">The user name, e-mail address or phone number</param>
        /// <returns>The user, null if no user was found</returns>
        public async Task<UserInfo> TryGetUserInfo(String identifier)
        {
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                var userId = await FindUser(c, identifier).ConfigureAwait(false);
                if (userId == 0)
                    return null;
                return await InternalTryGetUserInfo(c, userId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <returns>The user, null if it doesn't exist</returns>
        public async Task<UserInfo> TryGetUserInfo(long userId)
        {
            using (var c = await DbGetAsync().ConfigureAwait(false))
                return await InternalTryGetUserInfo(c, userId).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the data of a user
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="getTokens">True to also get the security tokens of the user</param>
        /// <param name="user">The user record if the caller already has it, null to look it up</param>
        /// <returns>The user data, null if the user doesn't exist</returns>
        public async Task<UserData> TryGetUser(long userId, bool getTokens = false, DbUser user = null)
        {
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                user = user ?? (await GetDbUser(c, userId).ConfigureAwait(false));
                if (user == null)
                    return null;
                var mail = await GetDbEmail(c, userId).ConfigureAwait(false);
                var phone = await GetDbPhone(c, userId).ConfigureAwait(false);
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
                auth = auth ?? new Authorization(this, user.UserName, oldAuth.Tokens, false, userGuid, newEmail ?? oldAuth.Email, user.NickName, user, user.Domain, user.Language);
                session.SetAuth(auth);
                if (clearcache)
                    session.InvalidateCache();
            });
        }

        static readonly IReadOnlySet<Char> InvalidChars = ReadOnlyData.Set("|,;:*/\\!\"'".ToCharArray());

        /// <summary>
        /// Set the nick name of a user (trimmed, at most AuhorizationLimits.MaxNickNameLength chars, no special characters)
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <param name="newName">The new nick name</param>
        /// <returns>true if changed (false if the user doesn't exist or already has exactly this name), old name, new name</returns>
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
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await GetDbUser(c, userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null, (String)null);
                var current = user.NickName;
                //  Unchanged only when exactly the same: a change of the letter case ("anna" to "Anna") is saved
                if (current.FastEquals(newName))
                    return Tuple.Create(false, current, current);
                user.NickName = newName;
                await UpdateUserNames(c, user).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
                res = Tuple.Create(true, current, newName);
            }
            ResetSessionAuth(user);
            await RaiseOnNickChanged(userId, newName).ConfigureAwait(false);
            return res;
        }

        /// <summary>
        /// Change the user name of a user
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="newName">The new name (trimmed)</param>
        /// <returns>True and the previous name if it was changed, false and the current name if it was the same, false and null if the user doesn't exist</returns>
        /// <exception cref="Exception">If the name is empty or too long</exception>
        public async Task<Tuple<bool, String>> SetUserName(long userId, String newName)
        {
            newName = newName?.Trim();
            if (String.IsNullOrEmpty(newName))
                throw new Exception("Name may not be null or empty");
            if (newName.Length > AuhorizationLimits.MaxUserNameLength)
                throw new Exception("Name length must be at most " + AuhorizationLimits.MaxUserNameLength + " chars");
            Tuple<bool, String> res;
            DbUser user;
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await GetDbUser(c, userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null);
                if (user.UserName.FastToLower().FastEquals(newName.FastToLower()))
                    return Tuple.Create(false, user.UserName);
                user.UserName = newName;
                await UpdateUserNames(c, user).ConfigureAwait(false);
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
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await GetDbUser(c, userId).ConfigureAwait(false);
                if (user == null)
                    return false;
                if (user.Language.FastEquals(languageCode))
                    return false;
                user.Language = languageCode;
                await c.UpdateAsync(user, x => new { x.Language }).ConfigureAwait(false);
                InvalidateUserList();
                await tr.CommitAsync().ConfigureAwait(false);
            }
            ResetSessionAuth(user);
            await RaiseOnLanguageChanged(userId, languageCode).ConfigureAwait(false);
            return true;
        }


        // TODO: Send mail and validate

        /// <summary>
        /// Set the e-mail address of a user directly (no validation and no message is sent), links that depend on the old address stop working
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="newEmail">The new e-mail address</param>
        /// <returns>True and the previous address (null if none) if it was changed, false and the current address if it was the same, false and null if the user doesn't exist</returns>
        public async Task<Tuple<bool, String>> ChangeEmail(long userId, String newEmail)
        {
            Tuple<bool, String> res;
            DbUser user;
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                user = await GetDbUser(c, userId).ConfigureAwait(false);
                if (user == null)
                    return Tuple.Create(false, (String)null);

                var email = await GetDbEmail(c, userId).ConfigureAwait(false);
                if (email != null)
                {
                    if (email.Email.FastToLower().FastEquals(newEmail.FastToLower()))
                        return Tuple.Create(false, email.Email);
                    await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                }
                res = Tuple.Create(true, email?.Email);
                await InsertEmail(c, new DbEmailAddress
                {
                    Email = newEmail,
                    UserId = userId,
                }).ConfigureAwait(false);
                //  The links that depend on the old address stop working
                await RevokeOnEvent(c, userId, AccountEvents.EmailChanged).ConfigureAwait(false);
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
            autheticatedRequest.PadUserName = username;
            return user;
        }

        /// <summary>
        /// Validate that the password proof supplied in an incoming request is correct (the SCRAM proof).
        /// <see cref="ValidatePasswordRequestData"/> must have been called first (it consumes the one time pad).
        /// </summary>
        /// <param name="autheticatedRequest">The request</param>
        /// <param name="authData">The stored password of the user</param>
        /// <returns>True if the proof is valid</returns>
        bool ValidatePasswordRequestPassword(AutheticatedRequest autheticatedRequest, DbAuthPassword authData)
            => IsValidPasswordProof(authData, autheticatedRequest.PadUserName, autheticatedRequest.OneTimePad, autheticatedRequest.ClientNonce, autheticatedRequest.ClientProof);

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
        /// <param name="subjectKind">"invite" or "signup": only one live token per kind and address (a newer invitation to the same address revokes the older one, see <see cref="TargetSubject"/>)</param>
        /// <param name="username">Email of the new user</param>
        /// <param name="email">Email of the new user</param>
        /// <param name="phone">Phone number of the new user</param>
        /// <param name="salt">Salt to use</param>
        /// <param name="authTokens">The security tokens for the user</param>
        /// <param name="domain">Optional application specific domain</param>
        /// <param name="language">Optional language preference for the user</param>
        /// <param name="expiration">Optional expiration time (UTC), default will bew the NewUserTimeout</param>
        /// <param name="context">Optional context, required to get a short code</param>
        /// <param name="invitedBy">Optional user name of the inviter (shown in the pending invitations)</param>
        /// <returns>The token that must be supplied together with a new password</returns>
        async Task<Tuple<String, String>> GetNewUserToken(String subjectKind, String username, String email, String phone, String salt, String[] authTokens = null, String domain = null, String language = null, DateTime? expiration = null, HttpServerRequest context = null, String invitedBy = null)
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

            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                var fu = await FindDbUserByName(c, username).ConfigureAwait(false);
                if (fu != null)
                    throw new UserAlreadyExistException(username);
                if (email != null)
                {
                    var fm = await FindDbEmail(c, email).ConfigureAwait(false);
                    if (fm != null)
                        throw new UserAlreadyExistException(email);
                }
                if (phone != null)
                {
                    var fp = await FindDbPhone(c, phone).ConfigureAwait(false);
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
                InvitedBy = invitedBy,
            }, DbAction.CreateUser, TargetSubject(subjectKind, email ?? phone), expiration ?? (now + NewUserTimeout), context, now + NewUserShortTimeout).ConfigureAwait(false);
            return tokens;
        }



        sealed class ShortData
        {
            public String ShortCode;
            public String Token;
            public DateTime Expiration;
            public String Type;
            public int TriesLeft;
            /// <summary>The account of the code, 0 for none (a sign up): its wrong tries also count per account, see <see cref="TrySessionCode"/></summary>
            public readonly long UserId;

            public ShortData(string shortCode, string token, DateTime expiration, string type, long userId, int triesLeft = 10)
            {
                ShortCode = shortCode;
                Token = token;
                Expiration = expiration;
                Type = type;
                UserId = userId;
                TriesLeft = triesLeft;
            }
        }


        const String ShortCodeName = "UserManager.ShortCode";

        static String FormatShortCode(String shortCode)
        {
            return shortCode ?? "";
/*            if (String.IsNullOrEmpty(shortCode))
                return "";
            return String.Join(' ', shortCode.ToCharArray());*/
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

        #region One live token per subject and purpose

        /// <summary>
        /// The subject of the action tokens that belong to a user (see <see cref="AddAction"/>): only one live token per user and type.
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <param name="variant">Optional variant, ex: "restore" for the recovery link of a removed e-mail address (so a new change request doesn't revoke it)</param>
        /// <returns>The subject</returns>
        public static String UserSubject(long userId, String variant = null)
            => variant == null
                ? String.Concat("user:", userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : String.Concat("user:", userId.ToString(System.Globalization.CultureInfo.InvariantCulture), "|", variant);

        /// <summary>
        /// The subject of the action tokens for an address that has no user yet (an invitation or a sign up): only one live token per address and kind.
        /// </summary>
        /// <param name="kind">"invite" or "signup" (separate: a sign up never revokes an invitation)</param>
        /// <param name="target">The e-mail address or phone number</param>
        /// <returns>The subject</returns>
        public static String TargetSubject(String kind, String target)
            => String.Concat(kind, ":", target?.Trim().ToLowerInvariant() ?? "");

        /// <summary>
        /// The subject variant of the recovery link sent to a removed e-mail address or phone number (see <see cref="UserSubject"/>): a new change request doesn't revoke it,
        /// but any later change of the account does (see <see cref="ActionTokenGroups"/>), and it never replaces a newer address.
        /// </summary>
        public const String RestoreVariant = "restore";

        /// <summary>
        /// The subject variant of the undo link sent to the old e-mail address after a change (see <see cref="UndoEmailChange"/>): only the deletion of
        /// the account revokes it (the one who changed the address can't make it stop working), a newer undo link doesn't replace it.
        /// </summary>
        public const String UndoVariant = "undo";

        /// <summary>
        /// The events that change an account, and so revoke the outstanding links whose meaning depends on the old state (see <see cref="ActionTokenGroups"/>).
        /// </summary>
        public static class AccountEvents
        {
            /// <summary>The password was changed, set (reset link) or added</summary>
            public const String PasswordChanged = "PasswordChanged";
            /// <summary>An administrator required a new password</summary>
            public const String PasswordForcedReset = "PasswordForcedReset";
            /// <summary>The password was removed</summary>
            public const String PasswordRemoved = "PasswordRemoved";
            /// <summary>A passkey was added</summary>
            public const String PassKeyAdded = "PassKeyAdded";
            /// <summary>A passkey was removed</summary>
            public const String PassKeyRemoved = "PassKeyRemoved";
            /// <summary>An e-mail address was added or changed (confirmed)</summary>
            public const String EmailChanged = "EmailChanged";
            /// <summary>The e-mail address was removed</summary>
            public const String EmailRemoved = "EmailRemoved";
            /// <summary>A phone number was added or changed (confirmed)</summary>
            public const String PhoneChanged = "PhoneChanged";
            /// <summary>The phone number was removed</summary>
            public const String PhoneRemoved = "PhoneRemoved";
            /// <summary>The account was deleted</summary>
            public const String AccountDeleted = "AccountDeleted";
        }

        /// <summary>
        /// The token kind (purpose) of an action type and subject variant, as declared in <see cref="ActionTokenGroups"/>: the type, or "type|variant".
        /// </summary>
        /// <param name="type">The action type, ex: <see cref="DbAction.AddEmail"/></param>
        /// <param name="variant">The subject variant, ex: <see cref="RestoreVariant"/>, null for none</param>
        /// <returns>The kind</returns>
        public static String TokenKind(String type, String variant = null)
            => variant == null ? type : String.Concat(type, "|", variant);

        /// <summary>
        /// <b>Every action token kind of a user and the account events that revoke it, by group</b> (one place, so a new kind can't be forgotten):
        /// <list type="bullet">
        /// <item>"Account" links (reset / add / delete password, add a passkey, delete the account; sent to every address of the account): revoked by every account event,
        /// so a link sent to an old address or before a password change never works afterwards.</item>
        /// <item>"Email" links (add / change e-mail, restore a removed e-mail): revoked by a credential change, an e-mail change or removal, and the deletion.</item>
        /// <item>"Phone" links (add / change phone, restore a removed phone): revoked by a credential change, a phone change or removal, and the deletion.</item>
        /// <item>"Undo" links (undo an e-mail change, sent to the old address): only revoked by the deletion, so the one who changed the address can't make them stop working.</item>
        /// <item>Invitations and sign ups (<see cref="DbAction.CreateUser"/>) belong to no account yet: no event revokes them.</item>
        /// </list>
        /// A new kind of action token MUST be declared here (<see cref="AddAction"/> refuses an undeclared kind for a user subject); the unit tests generate
        /// the matrix of every kind and every event from this declaration.
        /// </summary>
        public static readonly TokenGroups ActionTokenGroups = new TokenGroups()
            .Group("Account",
                AccountEvents.PasswordChanged, AccountEvents.PasswordForcedReset, AccountEvents.PasswordRemoved, AccountEvents.PassKeyAdded, AccountEvents.PassKeyRemoved,
                AccountEvents.EmailChanged, AccountEvents.EmailRemoved, AccountEvents.PhoneChanged, AccountEvents.PhoneRemoved, AccountEvents.AccountDeleted)
            .Group("Email",
                AccountEvents.PasswordChanged, AccountEvents.PasswordForcedReset, AccountEvents.PasswordRemoved, AccountEvents.PassKeyAdded, AccountEvents.PassKeyRemoved,
                AccountEvents.EmailChanged, AccountEvents.EmailRemoved, AccountEvents.AccountDeleted)
            .Group("Undo",
                AccountEvents.AccountDeleted)
            .Group("Phone",
                AccountEvents.PasswordChanged, AccountEvents.PasswordForcedReset, AccountEvents.PasswordRemoved, AccountEvents.PassKeyAdded, AccountEvents.PassKeyRemoved,
                AccountEvents.PhoneChanged, AccountEvents.PhoneRemoved, AccountEvents.AccountDeleted)
            .Purpose(DbAction.ResetPassword, "Account")
            .Purpose(DbAction.AddPassword, "Account")
            .Purpose(DbAction.DeletePassword, "Account")
            .Purpose(ActionTokenGetResetChallenge, "Account")
            .Purpose(DbAction.DeleteUser, "Account")
            .Purpose(DbAction.AddEmail, "Email")
            .Purpose(TokenKind(DbAction.AddEmail, RestoreVariant), "Email")
            .Purpose(DbAction.AddPhone, "Phone")
            .Purpose(TokenKind(DbAction.AddPhone, RestoreVariant), "Phone")
            .Purpose(DbAction.UndoEmail, "Undo")
            .Purpose(TokenKind(DbAction.UndoEmail, UndoVariant), "Undo")
            .Purpose(DbAction.CreateUser);

        #region Dead tokens (why a link no longer works)

        /// <summary>
        /// Why a token is no longer live (in memory, for the "this link no longer works" page, see <see cref="CheckActionToken"/>): "Used", "Replaced" or an <see cref="AccountEvents"/> name.
        /// Forgotten at a restart (the page then says the generic sentence).
        /// </summary>
        readonly Dictionary<String, Tuple<String, long>> DeadTokens = new(StringComparer.Ordinal);

        /// <summary>
        /// The most dead tokens remembered (the older half is forgotten when full)
        /// </summary>
        const int MaxDeadTokens = 20000;

        /// <summary>
        /// The cause of a token that was used
        /// </summary>
        public const String CauseUsed = "Used";

        /// <summary>
        /// The cause of a token that was replaced by a newer one
        /// </summary>
        public const String CauseReplaced = "Replaced";

        /// <summary>
        /// The cause of a token that expired
        /// </summary>
        public const String CauseExpired = "Expired";

        /// <summary>
        /// The cause of a restore link when the account has a newer address
        /// </summary>
        public const String CauseNewerContact = "NewerContact";

        /// <summary>
        /// Remember why a token (as sent by the client) is no longer live
        /// </summary>
        void MarkDead(String token, String cause)
        {
            if (String.IsNullOrEmpty(token))
                return;
            MarkDeadKey(ActionKey(token), cause);
        }

        /// <summary>
        /// Remember why a token is no longer live, by its stored key (<see cref="ActionKey"/>)
        /// </summary>
        void MarkDeadKey(String key, String cause)
        {
            var token = key;
            var d = DeadTokens;
            lock (d)
            {
                if (d.Count >= MaxDeadTokens)
                {
                    var keep = d.OrderByDescending(x => x.Value.Item2).Take(MaxDeadTokens / 2).ToList();
                    d.Clear();
                    foreach (var x in keep)
                        d[x.Key] = x.Value;
                }
                d[token] = Tuple.Create(cause, DateTime.UtcNow.Ticks);
            }
        }

        String DeadCause(String token)
        {
            if (String.IsNullOrEmpty(token))
                return null;
            token = ActionKey(token);
            var d = DeadTokens;
            lock (d)
                return d.TryGetValue(token, out var c) ? c.Item1 : null;
        }

        /// <summary>
        /// The sentence for people that says why a link no longer works.
        /// </summary>
        /// <param name="cause">The cause ("Used", "Replaced", "Expired", "NewerContact", an <see cref="AccountEvents"/> name), null if unknown</param>
        /// <param name="isCode">True for a short code that the user typed (not a link)</param>
        /// <param name="kindType">The action type (for the "newer address" sentence), null if unknown</param>
        /// <returns>The sentence (in english, translated by the server)</returns>
        public static String TokenRefusedReason(String cause, bool isCode = false, String kindType = null)
        {
            if (isCode && (cause == null))
                return "The code is wrong or no longer works: check it, or ask for a new message.";
            const String p = "This link no longer works: ";
            switch (cause)
            {
                case CauseUsed: return p + "it was already used.";
                case CauseReplaced: return p + "a newer link was sent, use the link in the newest message.";
                case CauseExpired: return p + "it has expired.";
                case CauseNewerContact: return p + (kindType == DbAction.AddPhone ? "a newer phone number was confirmed for the account after it was sent." : "a newer e-mail address was confirmed for the account after it was sent.");
                case AccountEvents.PasswordChanged: return p + "the password of the account was changed after it was sent.";
                case AccountEvents.PasswordForcedReset: return p + "an administrator required a new password after it was sent.";
                case AccountEvents.PasswordRemoved: return p + "the password of the account was removed after it was sent.";
                case AccountEvents.PassKeyAdded: return p + "a passkey was added to the account after it was sent.";
                case AccountEvents.PassKeyRemoved: return p + "a passkey of the account was removed after it was sent.";
                case AccountEvents.EmailChanged: return p + "the e-mail address of the account was changed after it was sent.";
                case AccountEvents.EmailRemoved: return p + "the e-mail address of the account was removed after it was sent.";
                case AccountEvents.PhoneChanged: return p + "the phone number of the account was changed after it was sent.";
                case AccountEvents.PhoneRemoved: return p + "the phone number of the account was removed after it was sent.";
                case AccountEvents.AccountDeleted: return p + "the account was deleted.";
            }
            return p + "it is incomplete, or it was already used, replaced by a newer link or has expired.";
        }

        /// <summary>
        /// The exception for a token that can't be used (410 with the reason, see <see cref="TokenExpiredException"/>).
        /// </summary>
        /// <param name="token">The token or short code as sent by the client</param>
        /// <returns>The exception to throw</returns>
        TokenExpiredException Refused(String token)
        {
            token = token?.Replace(' ', '+');
            var isCode = (token != null) && (token.Length <= 32);
            var cause = isCode ? null : DeadCause(token);
            return new TokenExpiredException(TokenRefusedReason(cause, isCode), isCode ? 400 : 410);
        }

        #endregion//Dead tokens

        /// <summary>
        /// Revoke the live action tokens of a subject for some types (tokens made by <see cref="AddAction"/> with that subject).
        /// </summary>
        /// <param name="c">An open connection (may be in a transaction)</param>
        /// <param name="subject">The subject</param>
        /// <param name="cause">Why ("Replaced" or an <see cref="AccountEvents"/> name), remembered for the "no longer works" page</param>
        /// <param name="keep">A token to keep (ex: the one just issued), null for none</param>
        /// <param name="types">The action types</param>
        /// <returns>The number of tokens revoked</returns>
        async Task<int> RevokeActions(OrmConnection c, String subject, String cause, String keep, params String[] types)
        {
            int n = 0;
            foreach (var type in types)
            {
                //  The stored keys keep a (keyed) tag prefix, so the tokens of a subject are still found with a "starts with" query
                var tag = StoredTagPrefix(SubjectTokens.Tag(type, subject));
                var keepKey = keep == null ? null : ActionKey(keep);
                var rows = await c.SelectAsync<DbAction>(x => x.Type == type && x.Token.StartsWith(tag)).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    var t = row.Token;
                    if (String.Equals(t, keepKey, StringComparison.Ordinal))
                        continue;
                    n += await c.DeleteAllAsync<DbAction>(x => x.Token == t).ConfigureAwait(false);
                    MarkDeadKey(t, cause);
                }
            }
            return n;
        }

        /// <summary>
        /// Something changed on an account: revoke every outstanding link of the user whose kind is revoked by the event (see <see cref="ActionTokenGroups"/>).
        /// Call it in the transaction that makes the change.
        /// </summary>
        /// <param name="c">An open connection (may be in a transaction)</param>
        /// <param name="userId">The user</param>
        /// <param name="evt">The event, one of <see cref="AccountEvents"/></param>
        /// <param name="keep">A token to keep (ex: the restore link just sent because of the event), null for none</param>
        /// <returns>The number of tokens revoked</returns>
        async Task<int> RevokeOnEvent(OrmConnection c, long userId, String evt, String keep = null)
        {
            int n = 0;
            var kinds = ActionTokenGroups.RevokedBy(evt);
            //  The reset codes use the reset token
            if (kinds.Contains(DbAction.ResetPassword))
                RemoveResetCodes(userId);
            foreach (var kind in kinds)
            {
                var bar = kind.IndexOf('|');
                var type = bar < 0 ? kind : kind.Substring(0, bar);
                var variant = bar < 0 ? null : kind.Substring(bar + 1);
                n += await RevokeActions(c, UserSubject(userId, variant), evt, keep, type).ConfigureAwait(false);
            }
            if (n > 0)
                Manager.AddMessage(MessageLevels.Debug, $"Revoked {n} outstanding links of user #{userId} ({evt})");
            return n;
        }

        /// <summary>
        /// Generate an "action" token, the token should be sent to one or more of a users communication channels.
        /// <para>Only ONE token per <paramref name="subject"/> and <paramref name="type"/> is live: the older live tokens of the same subject and type are revoked
        /// (an older, unused link stops working when a newer one is sent). Pass null as the subject only for a token that may exist many times.</para>
        /// </summary>
        /// <typeparam name="T">The type of the data</typeparam>
        /// <param name="data">The data kept with the token</param>
        /// <param name="type">The action type (ex: <see cref="DbAction.ResetPassword"/>)</param>
        /// <param name="subject">Who the token is for (ex: <see cref="UserSubject"/>, <see cref="TargetSubject"/>), null = no "one live token" rule</param>
        /// <param name="expiration">When the token expires (UTC)</param>
        /// <param name="context">If non-null a short code (shorter lived) will be generated that can only be used in that session</param>
        /// <param name="shortExpiration">If a context supplied, a short expiration time must also be suplied</param>
        /// <param name="replace">False to keep the older live tokens of the subject (ex: one undo link per changed address, see <see cref="UndoVariant"/>)</param>
        /// <returns>The token and the short code</returns>
        /// <exception cref="ArgumentException"></exception>
        async Task<Tuple<String, String>> AddAction<T>(T data, String type, String subject, DateTime expiration, HttpServerRequest context = null, DateTime? shortExpiration = null, bool replace = true)
        {
#if DEBUG
            if (!type.IsAsciiOnly())
                throw new ArgumentException("May only be ascii", nameof(type));
            if (type.Length > 8)
                throw new ArgumentException("Max length of 8", nameof(type));
#endif//DEBUG
            //  Every kind of token must be declared in the revocation groups (so no event forgets it)
            if (subject != null)
            {
                var bar = subject.IndexOf('|');
                var kind = TokenKind(type, bar < 0 ? null : subject.Substring(bar + 1));
                if (!ActionTokenGroups.IsDeclared(kind))
                    throw new ArgumentException("The action token kind " + kind + " is not declared in ActionTokenGroups", nameof(type));
            }
            var shortCodeDigits = ShortCodeDigits;
            String token;
            String shortCode;
            using (var rng = SecureRng.Get())
            {
                token = subject == null ? rng.GetGuid48() : SubjectTokens.Create(type, subject, 48);
                shortCode = rng.GetNumericCode(shortCodeDigits);
            }
            var d = Data.ToData(data);
            using var c = await DbGetAsync().ConfigureAwait(false);
            using (var tr = await c.BeginTransactionAsync().ConfigureAwait(false))
            {
                if ((subject != null) && replace)
                    await RevokeActions(c, subject, CauseReplaced, null, type).ConfigureAwait(false);
                await c.InsertAsync<DbAction>(new DbAction
                {
                    Token = ActionKey(token),
                    Type = type,
                    Expiration = expiration,
                    Data = ProtectActionData(ActionKey(token), type, d),
                }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            if (context != null)
                context.Session.Set(ShortCodeName, new ShortData(shortCode, token.Replace(' ', '+'), shortExpiration ?? expiration, type, SubjectUserId(subject)));
            return Tuple.Create(token, shortCode);
        }

        #endregion//One live token per subject and purpose

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
                    //  A right code for another type uses up no try either (the pages probe more than one type with the same code)
                    if (s.TryGet<ShortData>(ShortCodeName, out var shortData) && (TrySessionCode(s, shortData, shortCode) > 0) && shortData.Type.FastEquals(type))
                    {
                        removeIfFail = true;
                        token = shortData.Token;
                    }
                }
            }
            DbAction d;
            token = token.Replace(' ', '+');
            using (var c = await DbGetAsync().ConfigureAwait(false))
                {
                    //  Only a keyed hash of the token is stored (a copy of the database has no working links)
                    var key = ActionKey(token);
                    d = await c.FirstOrDefaultAsync<DbAction>(x => x.Token == key).ConfigureAwait(false);
                }
            if ((d == null) || (DateTime.UtcNow > d.Expiration) || (!type.FastEquals(d.Type)))
            {
                if ((d != null) && type.FastEquals(d.Type))
                    MarkDead(token, CauseExpired);
                if (removeIfFail)
                    context.Session.TryRemove(ShortCodeName);
                return default;
            }
            return Tuple.Create(token, Data.FromData<T>(ActionData(d)));
        }


        /// <summary>
        /// Delete (use) an action token.
        /// Call it inside the transaction that performs the action: the token is consumed by deleting its row, so of two concurrent uses only one deletes the row,
        /// the other gets a <see cref="TokenExpiredException"/> and its transaction is rolled back (a link can't be used twice).
        /// </summary>
        /// <param name="c">The connection (with an open transaction)</param>
        /// <param name="token">The token</param>
        /// <param name="type">The action type</param>
        /// <param name="context">The request (to remove a short code from the session), may be null</param>
        /// <param name="mustExist">If true (the default) a <see cref="TokenExpiredException"/> is thrown if no row was deleted</param>
        /// <exception cref="TokenExpiredException">The token row didn't exist (already used) and <paramref name="mustExist"/> is true</exception>
        async Task DeleteAction(OrmConnection c, String token, String type, HttpServerRequest context, bool mustExist = true)
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
            var key = ActionKey(token);
            var n = await c.DeleteAllAsync<DbAction>(x => x.Token == key && x.Type == type).ConfigureAwait(false);
            if (mustExist && (n <= 0))
                throw new TokenExpiredException();
            MarkDead(token, CauseUsed);
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
            //  Only the sign up methods (the coms that allow it: what the sign up page offers), not every coms
            var com = GetComs(ref target, true);
            if (com == null)
                throw InvalidTargetError(target, true);
            var s = context?.Session;
            if (s == null)
                throw new NoSessionException();
            if (s.Auth != null)
                throw new UserAlreadyLoggedInException();
            var cn = com.Name;
            var salt = AuthTools.GetRandomSalt();
            //  Every com but Email sends to a phone number (Sms and WhatsApp): the phone is kept for any of them (AS7-11: with WhatsApp first in the
            //  coms order the sign up lost the phone, only "Sms" was checked)
            var isEmail = cn == "Email";
            var tokens = await GetNewUserToken("signup", null, isEmail ? target : null, isEmail ? null : target, salt, null, null, null, null, context).ConfigureAwait(false);
            String link = String.Concat(LinkRoot(context), "auth/ChoosePassword.html?", HttpUtility.UrlEncode(tokens.Item1));
            /*            String link = String.Concat(LinkRoot(context), "auth/ChoosePassword.html?user=",
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
                throw new Exception("Key too long");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            DbUserData dbData;
            using (var c = await DbGetAsync().ConfigureAwait(false))
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
        /// <returns>A task that completes when the data is stored</returns>
        /// <exception cref="Exception"></exception>
        public async Task SetUserData<T>(String key, String userGuid, T data) where T : class
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key too long");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            var dbData = new DbUserData
            {
                DataKey = key,
                UserGuid = userGuid,
                Data = data == null ? null : Data.ToData(data),
            };
            using (var c = await DbGetAsync().ConfigureAwait(false))
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
                throw new Exception("Key too long");
            if (!StringTools.IsIdentifier(key))
                throw new Exception("Guid must be an \"identifier\", only 'a'-'z' and numbers is accepeted (no number at the first position)");
            key = key.FastToLower();
            DbUserDataString dbData;
            using (var c = await DbGetAsync().ConfigureAwait(false))
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
        /// <returns>A task that completes when the data is stored</returns>
        /// <exception cref="Exception"></exception>
        public async Task SetUserDataString(String key, String userGuid, String data)
        {
            if (key.Length > UserManagerTools.MaxDataKeyLength)
                throw new Exception("Key too long");
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
            using (var c = await DbGetAsync().ConfigureAwait(false))
                await c.UpdateAsync(dbData).ConfigureAwait(false);
        }

        #endregion//Custom Data

    }
}
