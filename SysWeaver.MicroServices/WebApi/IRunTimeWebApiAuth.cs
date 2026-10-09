using System;
using System.Collections.Generic;


namespace SysWeaver.MicroService
{
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
