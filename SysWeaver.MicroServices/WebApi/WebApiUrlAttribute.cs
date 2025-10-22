using System;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Put this attribute on a type to specify it's base url, this is the prefix to the method name
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class WebApiUrlAttribute : Attribute
    {

        /// <summary>
        /// Put this attribute on a type to specify it's base url, this is the prefix to the method name
        /// </summary>
        /// <param name="url">Base url for this type, {0} is replaced with the type name</param>
        public WebApiUrlAttribute(String url)
        {
            Url = url;
        }

        /// <summary>
        /// Base url for this type
        /// </summary>
        public readonly String Url;
    }


}
