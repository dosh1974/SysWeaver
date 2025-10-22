using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on a method to invoke a member to check if the api is available or not
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebApiOptionalAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a method to invoke a member to check if the api is available or not
        /// </summary>
        /// <param name="memberName">An instance field, property or method with a boolean, if returning true, the API is exposed, else it's hidden</param>
        public WebApiOptionalAttribute(String memberName)
        {
            MemberName = memberName;
        }

        public readonly String MemberName;

    }

}
