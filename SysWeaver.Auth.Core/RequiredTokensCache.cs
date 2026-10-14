using System;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver.Auth
{
    /// <summary>
    /// A token requirement string and its pre-processed tokens (<see cref="Authorization.GetRequiredTokens(string)"/>), for call sites that check a requirement that is stored as a string (ex: a configuration value).
    /// Use <see cref="Get(ref RequiredTokensCache, string)"/> with a field of the owner to parse the requirement once and call <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/> (no parsing per check).
    /// </summary>
    /// <remarks>
    /// Instances are immutable, the requirement and its tokens are published together (one reference), so a cache field can be read and updated concurrently.
    /// The cache is keyed by the reference of the requirement string: assigning another string (even an equal one) parses it again, a string that is built per call should not be cached.
    /// </remarks>
    public sealed class RequiredTokensCache
    {
        RequiredTokensCache(String requiredTokens, IReadOnlyList<String> tokens)
        {
            RequiredTokens = requiredTokens;
            Tokens = tokens;
        }

        /// <summary>
        /// The requirement string (the cache key, compared by reference)
        /// </summary>
        public readonly String RequiredTokens;

        /// <summary>
        /// The pre-processed tokens of <see cref="RequiredTokens"/>, as returned by <see cref="Authorization.GetRequiredTokens(string)"/>
        /// </summary>
        public readonly IReadOnlyList<String> Tokens;

        /// <summary>
        /// Get the pre-processed tokens of a requirement, parsed once per requirement string (reference) and stored in a cache field.
        /// <c>auth.IsValid(RequiredTokensCache.Get(ref cache, requiredTokens))</c> gives the same result as <c>auth.IsValid(requiredTokens)</c>.
        /// </summary>
        /// <param name="cache">The cache field (null initially), updated if it doesn't hold the tokens of <paramref name="requiredTokens"/></param>
        /// <param name="requiredTokens">A list of comma separated tokens (case insensitive).
        /// Null means no auth required, empty means that any logged in user is valid and "-" means that no one is valid</param>
        /// <returns>The same as <see cref="Authorization.GetRequiredTokens(string)"/>: null if <paramref name="requiredTokens"/> is null (the cache isn't changed)</returns>
        public static IReadOnlyList<String> Get(ref RequiredTokensCache cache, String requiredTokens)
        {
            if (requiredTokens == null)
                return null;
            var c = Volatile.Read(ref cache);
            if ((c != null) && ReferenceEquals(c.RequiredTokens, requiredTokens))
                return c.Tokens;
            c = new RequiredTokensCache(requiredTokens, Authorization.GetRequiredTokens(requiredTokens));
            Volatile.Write(ref cache, c);
            return c.Tokens;
        }
    }
}
