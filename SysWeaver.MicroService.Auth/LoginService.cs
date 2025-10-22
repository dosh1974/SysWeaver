using SysWeaver.Auth;
using SysWeaver.Net;
using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/Login", "Sign in", "auth/Login.html?parent=1&loc=.", "Click to enter credentials", "IconLogin", 1, null, true)]
    [IsMicroService]
    [RequiredDep<AuthManagerService>]
    public sealed class LoginService
    {

        public LoginService(ServiceManager manager)
        {
            Manager = manager;
            Am = manager.Get<AuthManagerService>();
        }


        public override string ToString() => "Provides an API for logging in securely";
        readonly ServiceManager Manager;
        readonly AuthManagerService Am;

        /// <summary>
        /// Request the salt used for password hashing for a given user
        /// </summary>
        /// <param name="username">The user to get some salt for</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The salt and one time pad to use for password hashing.
        /// [0] = Salt.
        /// [1] = One time pad.
        /// </returns>
        [WebApi]
        public async Task<String[]> GetUserSalt(String username, HttpServerRequest context)
        {
            var a = context.Session.Auth;
            String data;
            if (a == null)
            {
                var am = Am;
                data = await am.Auth.GetSalt(username).ConfigureAwait(false);
            }
            else
            {
                data = await a.Auth.GetSaltAsync(username).ConfigureAwait(false);
            }
            return [data, Manager.CreateOneTimePad(username)];
        }


        /// <summary>
        /// Try to login a user
        /// </summary>
        /// <param name="login">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>User information</returns>
        [WebApi]
        [WebApiAudit(AuthTools.AuditGroup)]
        [WebApiAuditFilterParams(nameof(AuditInputFilter_Login))]
        public async Task<AuthInfo> Login(LoginRequest login, HttpServerRequest context)
        {
            var s = context?.Session;
            if (s == null)
                return AuthInfo.Failed;
            if (s.Auth != null)
                return AuthInfo.Failed;
            if (!Manager.TryConsumeOneTimePad(login.OneTimePad, out var username))
                return AuthInfo.Failed;
            var am = Am.Auth;
            var auth = await am.UserHash(username, login.Hash ?? "", login.OneTimePad).ConfigureAwait(false);
            await TaskExt.RandomDelay().ConfigureAwait(false);
            if (auth == null)
                return AuthInfo.Failed;
            s.SetAuth(auth);
            await context.Server.RunOnLogin(s).ConfigureAwait(false);
            s.InvalidateCache();
            try
            {
                OnLogin?.Invoke(context);
                await OnLoginAsync.RaiseEvents(context).ConfigureAwait(false);
            }
            catch
            {
            }
            return Am.GetUser(context);
        }
        
        Object AuditInputFilter_Login(long id, HttpServerRequest request, Object obj)
        {
            var data = obj as LoginRequest;
            if (data == null)
                return obj;
            if (Manager.InspectOneTimePad(data.OneTimePad, out var username))
                return username;
            return "<< Invalid OneTimePad >>";
        }

        /// <summary>
        /// Called whenever a login is executed
        /// </summary>
        public event Action<HttpServerRequest> OnLogin;

        /// <summary>
        /// Called whenever a login is executed
        /// </summary>
        public event Func<HttpServerRequest, Task> OnLoginAsync;


        /// <summary>
        /// Get the common password policy (minimum requirement for all authorizers)
        /// </summary>
        /// <returns>The password policy</returns>
        [WebApi]
        public PasswordPolicy GetCommonPasswordPolicy() => Am.Auth.CommonPasswordPolicy;

        /// <summary>
        /// Get the password policy for a user (return the policy for the authorizer of the user, or the common policy if the user is unknown).
        /// </summary>
        /// <param name="username">The user name to get password policy for</param>
        /// <returns>The password policy</returns>
        [WebApi]
        public Task<PasswordPolicy> GetPasswordPolicy(String username)
            => Am.Auth.GetPasswordPolicy(username);


    }


  




}
