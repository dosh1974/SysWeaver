using System;
using System.Collections.Generic;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Services with API's can implement this interface to provide runtime configurable auths
    /// </summary>
    public interface IRunTimeWebApiAuth
    {
        /// <summary>
        /// Runtime auth overrides, key = method name, value = auth for that method
        /// Key "*" means all methods (that are not otherwise specified)
        /// </summary>
        IReadOnlyDictionary<String, String> MethodAuths { get; }
    }

}
