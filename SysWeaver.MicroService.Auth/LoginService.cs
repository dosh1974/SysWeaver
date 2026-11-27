using SysWeaver.Auth;
using SysWeaver.Net;
using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// The sign in web api of the <see cref="AuthManagerService"/>: hands out the salt (and the SCRAM parameters) and a one-time pad for a user name,
    /// signs in with a SCRAM client proof (or the legacy salted password hash) bound to that pad (so it can't be replayed) and reports the password policies.
    /// Adds "Sign in" to the user menu.
    /// </summary>
    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/Login", "Sign in", "auth/Login.html?parent=1&loc=.", "Click to enter credentials", "IconLogin", 1, null, true)]
    [IsMicroService]
    [RequiredDep<AuthManagerService>]
    public sealed class LoginService
    {

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="manager">The service manager, must contain an <see cref="AuthManagerService"/></param>
        public LoginService(ServiceManager manager)
        {
            Manager = manager;
            Am = manager.Get<AuthManagerService>();
        }


        /// <inheritdoc />
        public override string ToString() => "Provides an API for logging in securely";
        readonly ServiceManager Manager;
        readonly AuthManagerService Am;

        /// <summary>
        /// Request the salt used for password hashing for a given user
        /// </summary>
        /// <param name="username">The user to get some salt for</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The salt and one time pad to use for password hashing.
        /// [0] = Salt (the legacy salt, used to compute the legacy hash H).
        /// [1] = One time pad.
        /// If the user's authorizer supports SCRAM (see <see cref="ScramInfo"/>, unknown users get fake values of the same shape):
        /// [2] = "s1" (<see cref="ScramTools.SchemeName"/>).
        /// [3] = The SCRAM salt (base64).
        /// [4] = The PBKDF2 iteration count.
        /// [5] = The application binding string.
        /// </returns>
        [WebApi]
        //  Process wide: permissive (many users, many sites), per address: conservative (password guessing from one client)
        [WebApiServiceRateLimit(200, 1, 400, 5)]
        [WebApiIpRateLimit(20, 60, 10, 10)]
        public async Task<String[]> GetUserSalt(String username, HttpServerRequest context)
        {
            var a = context.Session.Auth;
            var p = await Am.Auth.GetLoginParams(username, a?.Auth).ConfigureAwait(false);
            var pad = Manager.CreateOneTimePad(username);
            var scram = p.Item2;
            if (scram == null)
                return [p.Item1, pad];
            return [p.Item1, pad, ScramTools.SchemeName, scram.Salt, scram.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture), scram.Binding ?? ""];
        }


        /// <summary>
        /// Try to login a user, using the SCRAM proof if <see cref="LoginRequest.ClientProof"/> is set, else the legacy one time hash
        /// </summary>
        /// <param name="login">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>User information</returns>
        [WebApi]
        [WebApiAudit(AuthTools.AuditGroup)]
        [WebApiAuditFilterParams(nameof(AuditInputFilter_Login))]
        [WebApiServiceRateLimit(100, 1, 200, 5)]
        [WebApiIpRateLimit(20, 60, 10, 10)]
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
            Authorization auth;
            try
            {
                if (login.ClientProof != null)
                    auth = await am.ScramAuth(username, login.OneTimePad, login.ClientNonce, login.ClientProof).ConfigureAwait(false);
                else
                    auth = await am.UserHash(username, login.Hash ?? "", login.OneTimePad).ConfigureAwait(false);
            }
            catch (PasswordChangeRequiredException ex)
            {
                //  the password was right but must be changed first (ex: a forced password reset): no session,
                //  send the instructions to choose a new password (the short code works in this session)
                String[] sentTo = null;
                try
                {
                    var sc = ex.StartChange;
                    if (sc != null)
                        sentTo = await sc(context).ConfigureAwait(false);
                }
                catch
                {
                }
                await TaskExt.RandomDelay().ConfigureAwait(false);
                return new AuthInfo
                {
                    PasswordChangeRequired = true,
                    SentTo = sentTo,
                };
            }
            catch (AccountDisabledException)
            {
                //  the password was right but an administrator disabled the account: no session, the page says so
                await TaskExt.RandomDelay().ConfigureAwait(false);
                return new AuthInfo
                {
                    AccountDisabled = true,
                };
            }
            await TaskExt.RandomDelay().ConfigureAwait(false);
            if (auth == null)
                return AuthInfo.Failed;
            s.SetAuth(auth);
            await context.Server.RunOnLogin(s, context).ConfigureAwait(false);
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
        /// Called whenever a login is executed (synchronous handlers)
        /// </summary>
        public event Action<HttpServerRequest> OnLogin;

        /// <summary>
        /// Called whenever a login is executed (asynchronous handlers)
        /// </summary>
        public event Func<HttpServerRequest, Task> OnLoginAsync;


        /// <summary>
        /// Get the common password policy (minimum requirement for all authorizers)
        /// </summary>
        /// <returns>The password policy</returns>
        [WebApi]
        public PasswordPolicy GetCommonPasswordPolicy() => Am.Auth.CommonPasswordPolicy;

        /// <summary>
        /// Get the password policy for a user: the policy of the authorizer of the user.
        /// An unknown user gets the policy of an authorizer chosen from a keyed hash of the name (the same every time), so the answer doesn't tell if the user exists.
        /// An empty user name gets the common policy.
        /// </summary>
        /// <param name="username">The user name to get password policy for</param>
        /// <returns>The password policy</returns>
        [WebApi]
        //  Like the sign in API's, a bit more permissive per address: the sign in pages ask whenever the user name changes
        [WebApiServiceRateLimit(200, 1, 400, 5)]
        [WebApiIpRateLimit(60, 60, 20, 10)]
        public Task<PasswordPolicy> GetPasswordPolicy(String username)
            => Am.Auth.GetPasswordPolicy(username);


    }


  




}
