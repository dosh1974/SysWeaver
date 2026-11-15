using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="TranslatorApiService"/>
    /// </summary>
    public sealed class TranslatorApiParams
    {
        /// <summary>
        /// The auth required to access the API's (default is the Service role)
        /// </summary>
        public String Auth = Roles.Service;
    }


}
