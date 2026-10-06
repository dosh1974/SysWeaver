using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Data retention policy, can optionally be controlled per user by supplying an implementation of <see cref="IUserStoragePerUserHandler"/>.
    /// </summary>
    public sealed class UserStorageDataRetention
    {
        /// <summary>
        /// Number of days to keep private files alive (after last view).
        /// 0 or less to use defaults. Values above 100000 are clamped.
        /// </summary>
        public int PrivateDays = 90;

        /// <summary>
        /// Number of days to keep protected files alive (after last view).
        /// 0 or less to use defaults. Values above 100000 are clamped.
        /// </summary>
        public int ProtectedDays = 60;

        /// <summary>
        /// Number of days to keep public files alive (after last view).
        /// 0 or less to use defaults. Values above 100000 are clamped.
        /// </summary>
        public int PublicDays = 30;

        /// <summary>
        /// Maximum number of Mb of space that a user can have.
        /// If exceeded, the files that would expire in the nearest future are deleted.
        /// 0 or less to use defaults.
        /// </summary>
        public int DiscQuotaMb = 100;

        /// <summary>
        /// Get the validated max number of bytes that a user may store.
        /// </summary>
        /// <param name="defaultDiscQuotaMb">The quota in Mb to use if <see cref="DiscQuotaMb"/> is 0 or less; if this is also 0 or less, 100 Mb is used.</param>
        /// <returns>The quota in bytes.</returns>
        public long GetMaxDiscBytes(int defaultDiscQuotaMb) => 
            ((long)(DiscQuotaMb <= 0 ?
                (defaultDiscQuotaMb <= 0 ? 100 : defaultDiscQuotaMb)
                :
                DiscQuotaMb)) << 20;

        /// <summary>
        /// Get the validated time spans, in the order of UserStorageScopes:
        /// 0 = Private.
        /// 1 = Protected.
        /// 2 = Public.
        /// </summary>
        /// <param name="to">An array of at least length 3</param>
        /// <param name="defaults">The defaults to use in case defaults are required, null to use the built in defaults (90, 60 and 30 days).
        /// Note that the values of <paramref name="defaults"/> are used as is (not validated).</param>
        public void Get(TimeSpan[] to, UserStorageDataRetention defaults)
        {
            var pri = PrivateDays;
            var pro = ProtectedDays;
            var pub = PublicDays;
            to[0] = TimeSpan.FromDays(Math.Min(100000, pri <= 0 ? (defaults?.PrivateDays ?? 90) : pri));
            to[1] = TimeSpan.FromDays(Math.Min(100000, pro <= 0 ? (defaults?.ProtectedDays ?? 60) : pro));
            to[2] = TimeSpan.FromDays(Math.Min(100000, pub <= 0 ? (defaults?.PublicDays ?? 30) : pub));
        }

        /// <summary>
        /// Get the validated time spans, in the order of UserStorageScopes:
        /// 0 = Private.
        /// 1 = Protected.
        /// 2 = Public.
        /// </summary>
        /// <param name="defaults">The defaults to use in case defaults are required</param>
        /// <returns>An array of length 3 with the retention durations</returns>
        public TimeSpan[] Get(UserStorageDataRetention defaults)
        {
            var t = new TimeSpan[3];
            Get(t, defaults);
            return t;
        }
    }


}
