using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    [WebApiUrl("auth")]
    [WebMenuEmbedded("User", "User/SignUp", "Sign up", "auth/SignUp.html", "Click to sign up to the service", "IconSignUp", 10, null, true)]
    [IsMicroService]
    [RequiredDep<UserManagerService>]
    public sealed class SignUpService
    {
            
        public SignUpService(ServiceManager manager)
        {
            Am = manager.Get<UserManagerService>();
        }

        readonly UserManagerService Am;


        /// <summary>
        /// Request a sign up to the service.
        /// </summary>
        /// <param name="emailOrPhone">Depending on user managers settings this can be an email or a phone number</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public Task<bool> SignUp(String emailOrPhone, HttpServerRequest context) => Am.SignUp(emailOrPhone, context);

    }

}
