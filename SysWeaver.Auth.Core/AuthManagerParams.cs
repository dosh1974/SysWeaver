using System;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Parameters for <see cref="AuthManager"/>.
    /// </summary>
    public class AuthManagerParams
    {

        /// <inheritdoc/>
        public override string ToString() =>
            String.Concat(
                nameof(Realm), ": ", Realm.ToQuoted(), ", ",
                nameof(CacheDuration), ": ", CacheDuration, ", ",
                nameof(MaxCachedHeaders), ": ", MaxCachedHeaders);


        /// <summary>
        /// The number of seconds that a successful Authorization header result is cached (minimum 1), also the interval of the periodic cache pruning.
        /// A removed user / API key or a changed password can be used with a cached header for at most this long (unless the authorizer reports the change through its change counter).
        /// </summary>
        public int CacheDuration = 30;

        /// <summary>
        /// The maximum number of cached (successful) Authorization header results, when reached new results are not cached until expired entries are pruned.
        /// 0 or less disables the cache.
        /// </summary>
        public int MaxCachedHeaders = 10000;

        /// <summary>
        /// The realm name, reported in the "WWW-Authenticate" header for basic auth. Null uses the entry assembly name.
        /// </summary>
        public String Realm = "SysWeaver";
    }


}
