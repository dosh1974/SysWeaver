using System;

namespace SysWeaver.Auth
{
    public class AuthManagerParams
    {

        public override string ToString() =>
            String.Concat(
                nameof(Realm), ": ", Realm.ToQuoted(), ", ",
                nameof(CacheDuration), ": ", CacheDuration);


        /// <summary>
        /// Number of seconds to cache results (only invalid results are pruned)
        /// </summary>
        public int CacheDuration = 30;

        /// <summary>
        /// The hash suffix to use, should be unique for each system so that the hash is different for the same user/pwd pair
        /// </summary>
        public String Realm = "SysWeaver";
    }


}
