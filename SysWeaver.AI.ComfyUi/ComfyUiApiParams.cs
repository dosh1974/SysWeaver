using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the <see cref="ComfyUiApiService"/>
    /// </summary>
    public sealed class ComfyUiApiParams
    {
        /// <summary>
        /// The auth required to access the API's (all methods).
        /// Null = open (no auth), empty = any logged in user, else comma separated tokens where at least one is required (ex: "Admin,Ops").
        /// </summary>
        public String Auth = "";
    }
}
