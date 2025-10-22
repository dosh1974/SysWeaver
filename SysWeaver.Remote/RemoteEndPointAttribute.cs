using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Defines the end point formatting for an api call and/or the http end point type (verb)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class RemoteEndPointAttribute : Attribute
    {
        /// <summary>
        /// The path formatting to use
        /// </summary>
        public readonly String Path;

        /// <summary>
        /// The verb to use
        /// </summary>
        public readonly HttpEndPointTypes Type;

        /// <summary>
        /// Defines the end point formatting for an api call and optionally the rest type (verb)
        /// </summary>
        /// <param name="path">The path formatting to use, for GET and PUT calls - method arguments can be inserted using the {arg} syntax, if there is a single argument and that argument is an object, the syntax is {field} or {property}</param>
        /// <param name="type">The type of verb to use</param>
        public RemoteEndPointAttribute(String path, HttpEndPointTypes type = HttpEndPointTypes.Get)
        {
            Type = type;
            Path = path;
        }

        /// <summary>
        /// Defines the rest type (verb) of an api and optionally the end point formatting 
        /// </summary>
        /// <param name="type">The type of verb to use</param>
        /// <param name="path">The path formatting to use, for GET and PUT calls - method arguments can be inserted using the {arg} syntax, if there is a single argument and that argument is an object, the syntax is {field} or {property}</param>
        public RemoteEndPointAttribute(HttpEndPointTypes type, String path = null)
        {
            Type = type;
            Path = path;
        }

    }




    /// <summary>
    /// Defines a path prefix to use for all methods in this interface
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class RemotePathPrefixAttribute : Attribute
    {
        /// <summary>
        /// The path prefix to use on all methods in this interface
        /// </summary>
        public readonly String PathPrefix;


        /// <summary>
        /// Defines a path prefix to use for all methods in this interface
        /// </summary>
        /// <param name="pathPrefix">The path prefix to use on all methods in this interface</param>
        public RemotePathPrefixAttribute(String pathPrefix)
        {
            PathPrefix = pathPrefix;
        }
    }

}
