using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="TranslatorDebugService"/>
    /// </summary>
    public sealed class TranslatorDebugParams
    {
        /// <summary>
        /// The auth required to access the API's (default is the Debug role)
        /// </summary>
        public String Auth = Roles.Debug;
    }




}
