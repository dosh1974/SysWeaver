using System;
using System.Collections.Generic;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on a type or method to specify the auth required
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiAuthAttribute : Attribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify the auth required
        /// </summary>
        /// <param name="auth">The auth required for API's on this type or for this mehtod.\nnull = No auth required.\n"" = Auth required, but no specific access token is required\nComma separated list of required access tokens (one is needed).</param>
        public WebApiAuthAttribute(String auth = "")
        {
            Auth = auth;
        }

        /// <summary>
        /// Auth
        /// </summary>
        public readonly String Auth;
    }



    /// <summary>
    /// Services with API's can implement this API to provide runtime configurable auths
    /// </summary>
    public interface IRunTimeWebApiAuth
    {
        /// <summary>
        /// Runtime auto overrides, key = method name, value = auth for that method
        /// Key "*" means all all methods (that are not otherwise specified)
        /// </summary>
        IReadOnlyDictionary<String, String> MethodAuths { get; }
    }

}
