using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Hashing, salt and token helpers used by the authorization system.
    /// </summary>
    /// <remarks>
    /// Password hashes are a single SHA256 of "password|salt" (fast hashes, matching what the web clients compute).
    /// </remarks>
    public static class AuthTools
    {
        /// <summary>
        /// The audit group used for auth related API audits.
        /// </summary>
        public const String AuditGroup = "auth";


        /// <summary>
        /// Compute a hash for some given text: SHA256(UTF8(text))
        /// </summary>  
        /// <param name="text">The text to hash, may not be null</param>
        /// <returns>Computed hash as a byte array: SHA256(UTF8(text))</returns>
        public static Byte[] ComputeHash(String text)
        {
            Byte[] rented = null;
            var l = text.Length << 2;
            Span<Byte> dest = l <= 4096 ? stackalloc Byte[l] : (rented = ArrayPoolStream.Rent(l));
            try
            {
                if (!Encoding.UTF8.TryGetBytes(text.AsSpan(), dest, out var size))
                    throw new Exception("Internal error!");
                return SHA256.HashData(dest.Slice(0, size));
            }
            finally
            {
                if (rented != null)
                    ArrayPoolStream.Return(rented);
            }
        }


        /// <summary>
        /// Compute a hash for some given text: SHA256(UTF8(text))
        /// </summary>
        /// <param name="text">The text to hash, may not be null</param>
        /// <returns>The hash as a string: ToBase64(SHA256(UTF8(text)))</returns>
        public static String ComputeHashString(String text)
        {
            Byte[] rented = null;
            var l = text.Length << 2;
            Span<Byte> dest = l <= 4096 ? stackalloc Byte[l] : (rented = ArrayPoolStream.Rent(l));
            try
            {
                if (!Encoding.UTF8.TryGetBytes(text.AsSpan(), dest, out var size))
                    throw new Exception("Internal error!");
                Span<Byte> hash = stackalloc Byte[SHA256.HashSizeInBytes];
                if (SHA256.HashData(dest.Slice(0, size), hash) != SHA256.HashSizeInBytes)
                    throw new Exception("Internal error!");
                return Convert.ToBase64String(hash);
            }
            finally
            {
                if (rented != null)
                    ArrayPoolStream.Return(rented);

            }
        }

        /// <summary>
        /// Compute a hash for the given password and salt: SHA256(UTF8(password|salt))
        /// </summary>
        /// <param name="password">Password</param>
        /// <param name="userSalt">Salt</param>
        /// <returns>Computed hash as a byte array: SHA256(UTF8(password|salt))</returns>
        public static Byte[] ComputeHash(String password, String userSalt) => ComputeHash(String.Join('|', password, userSalt));


        /// <summary>
        /// Compute a hash for the given password and salt: SHA256(UTF8(password|salt))
        /// </summary>
        /// <param name="password">Password</param>
        /// <param name="userSalt">Salt</param>
        /// <returns>The hash as a string: ToBase64(SHA256(UTF8(password|salt)))</returns>
        /// 
        public static String ComputeHashString(String password, String userSalt) => ComputeHashString(String.Join('|', password, userSalt));

        /// <summary>
        /// Convert a byte array hash to a hash string: ToBase64(hash)
        /// </summary>
        /// <param name="hash">The hash as a byte array</param>
        /// <returns>The hash as a string: ToBase64(hash)</returns>
        public static String HashToString(ReadOnlySpan<Byte> hash) => Convert.ToBase64String(hash);

        /// <summary>
        /// Get a random salt (from a cryptographically secure RNG), 24 chars using 144 random bits
        /// </summary>
        /// <returns>A random salt, 24 chars using 144 random bits</returns>
        public static String GetRandomSalt()
        {
            using var rng = SecureRng.Get();
            return rng.GetGuid24();
        }

        /// <summary>
        /// Get a deterministic salt from some data, 24 chars using 144 hashed bits
        /// </summary>
        /// <param name="data">The data to hash</param>
        /// <returns>A salt, 24 chars using 144 hash bits</returns>
        public static String GetHashSalt(ReadOnlySpan<Byte> data) => SecureRng.GetHashGuid24(data);

        /// <summary>
        /// Get a deterministic salt from some string (UTF8 encoded), 24 chars using 144 hashed bits
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>A salt, 24 chars using 144 hash bits</returns>
        public static String GetHashSalt(String text) => SecureRng.GetHashGuid24(Encoding.UTF8.GetBytes(text));


        /// <summary>
        /// A description of the password rules enforced by <see cref="ValidatePassword(string)"/>, suitable to show when validation fails.
        /// </summary>
        public const String PasswordRules = "Password must be at least 8 characters.";

        /// <summary>
        /// Validate a password using a fixed minimal rule (at least 8 characters), return error string.
        /// Use <see cref="PasswordPolicyExt.Check(PasswordPolicy, string)"/> for policy based validation.
        /// </summary>
        /// <param name="password">The password to test</param>
        /// <returns>Error string or null if password is valid</returns>
        public static String ValidatePassword(String password)
        {
            if (String.IsNullOrEmpty(password))
                return "Password may not be empty!";
            if (password.Length < 8)
                return "Password must be atleast 8 characters!";
            return null;
        }


        /// <summary>
        /// Compute a simple deterministic salt for given user (unique per user per application), used by <see cref="SimpleAuthorizer"/>.
        /// Salt = ToBase64(SHA256(UTF8(lowercase(username + AppAssemblyName) + magic))).
        /// </summary>
        /// <param name="user">A valid user name</param>
        /// <returns>Plain text salt (44 chars)</returns>
        public static String ComputeSimpleSalt(String user)
        { 
            var hash = ComputeHashString((user + EnvInfo.AppAssemblyName).FastToLower() + "XfETwfDcxBJGvHmgd");
            return hash;
        }

        /// <summary>
        /// Compute a simple password hash using the salt from <see cref="ComputeSimpleSalt(string)"/>: ToBase64(SHA256(UTF8(password|salt))).
        /// This is the hash format that can be used instead of a clear text password in <see cref="SimpleAuthorizerParams.Users"/>.
        /// </summary>
        /// <param name="user">A valid user name</param>
        /// <param name="password">A valid password</param>
        /// <returns>Hash as a base64 string (44 chars)</returns>
        public static String ComputeSimplePasswordHash(String user, String password)
        {
            var salt = ComputeSimpleSalt(user);
            var h = ComputeHashString(password, salt);
            return h;
        }

        /// <summary>
        /// A token that no one should have
        /// </summary>
        public const String NoAuthToken = "  No auth ";

        /// <summary>
        /// An empty token list, as a requirement it means "any logged in user".
        /// </summary>
        public static readonly IReadOnlyList<String> Empty = [];

        /// <summary>
        /// An empty (frozen) token set.
        /// </summary>
        public static readonly IReadOnlySet<String> EmptyTokens = new HashSet<String>(StringComparer.Ordinal).Freeze();

        /// <summary>
        /// A token list that no one can fulfill (the result of the "-" requirement), compared by reference in <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> NoAuth = [NoAuthToken];

        /// <summary>
        /// A token set containing only <see cref="NoAuthToken"/> (the result of the "-" requirement).
        /// </summary>
        public static readonly IReadOnlySet<String> NoAuthSet = new HashSet<String>(StringComparer.Ordinal)
        {
            NoAuthToken
        }.Freeze();



        /// <summary>
        /// Split a comma separated token string into a list of unique, trimmed, lower case tokens.
        /// Unlike <see cref="Authorization.GetRequiredTokens(string)"/>, empty entries and "-" are not handled specially.
        /// </summary>
        /// <param name="tokens">The comma separated tokens</param>
        /// <returns>The tokens, or null if <paramref name="tokens"/> is null</returns>
        public static IReadOnlyList<String> GetList(String tokens)
        {
            if (tokens == null)
                return null;
            var t = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in tokens.Split(','))
                t.Add(x.FastTrimToLower());
            return t.ToArray();
        }

        /// <summary>
        /// The tokens of <see cref="Roles.Debug"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> DebugAuth = GetList(Roles.Debug);
        /// <summary>
        /// The tokens of <see cref="Roles.Admin"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> AdminAuth = GetList(Roles.Admin);
        /// <summary>
        /// The tokens of <see cref="Roles.Dev"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> DevAuth = GetList(Roles.Dev);

        /// <summary>
        /// Validate that a user guid is at most <see cref="AuhorizationLimits.MaxGuidLength"/> chars and only contains ASCII chars.
        /// </summary>
        /// <param name="guid">The guid to validate, may not be null</param>
        /// <exception cref="Exception">The guid is too long or contains non ASCII chars.</exception>
        public static void ValidateUserGuid(String guid)
        {
            if (guid.Length > AuhorizationLimits.MaxGuidLength)
                throw new Exception("Guid to long!");
            if (!StringTools.IsAsciiOnly(guid))
                throw new Exception("Guid may only contain ASCII chars!");
        }

    }

    /// <summary>
    /// Limits for user related values, enforced by <see cref="AuthorizationInfo"/>.
    /// </summary>
    public static class AuhorizationLimits
    {
        /// <summary>
        /// Maximum number of chars in a user name.
        /// </summary>
        public const int MaxUserNameLength = 128;
        /// <summary>
        /// Maximum number of chars in an email.
        /// </summary>
        public const int MaxEmailLength = 128;
        /// <summary>
        /// Maximum number of chars in a user guid.
        /// </summary>
        public const int MaxGuidLength = 48;
        /// <summary>
        /// Maximum number of chars in a domain.
        /// </summary>
        public const int MaxDomainName = 256;
        /// <summary>
        /// Maximum number of chars in a generated nick name.
        /// </summary>
        public const int MaxNickNameLength = 24;

    }


}
