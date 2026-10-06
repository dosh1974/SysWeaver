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
                nameof(CacheDuration), ": ", CacheDuration);


        /// <summary>
        /// The interval in seconds of the periodic cache pruning (minimum 1).
        /// </summary>
        public int CacheDuration = 30;

        /// <summary>
        /// The realm name, reported in the "WWW-Authenticate" header for basic auth. Null uses the entry assembly name.
        /// </summary>
        public String Realm = "SysWeaver";
    }


}
