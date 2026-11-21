using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Exposes the sign up of the <see cref="UserManagerService"/> as a web api
    /// </summary>
    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/SignUp", "Sign up", "auth/SignUp.html", "Click to sign up to the service", "IconSignUp", 10, null, true)]
    [IsMicroService]
    [RequiredDep<UserManagerService>]
    public sealed class SignUpService
    {

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="manager">The service manager (the <see cref="UserManagerService"/> is required)</param>
        public SignUpService(ServiceManager manager)
        {
            Am = manager.Get<UserManagerService>();
            //  The sign up api exists now: the user manager may offer its sign up methods (SignUp.html, the sign in page)
            Am.EnableSignUp();
        }

        readonly UserManagerService Am;


        /// <summary>
        /// Request a sign up to the service.
        /// </summary>
        /// <param name="emailOrPhone">Depending on user managers settings this can be an email or a phone number</param>
        /// <param name="context">The request context</param>
        /// <returns>True if the sign up request was accepted (a message is sent to the address)</returns>
        [WebApi]
        public Task<bool> SignUp(String emailOrPhone, HttpServerRequest context) => Am.SignUp(emailOrPhone, context);

    }

}
