using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Auth
{
    public static class AuthTools
    {
        public const String AuditGroup = "auth";


        /// <summary>
        /// Compute a hash for some given text: SHA256(UTF8(text))
        /// </summary>
        /// <param name="text">Password</param>
        /// <returns>Computed hash as a byte array: SHA256(UTF8(text))</returns>
        public static Byte[] ComputeHash(String text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

        /// <summary>
        /// Compute a hash for the given password and salt: SHA256(UTF8(password|salt))
        /// </summary>
        /// <param name="password">Password</param>
        /// <param name="userSalt">Salt</param>
        /// <returns>Computed hash as a byte array: SHA256(UTF8(password|salt))</returns>
        public static Byte[] ComputeHash(String password, String userSalt) => ComputeHash(String.Join('|', password, userSalt));

        /// <summary>
        /// Convert a byte array hash to a hash string: ToBase64(hash)
        /// </summary>
        /// <param name="hash">The hash as a byte array</param>
        /// <returns>The hash as a string: ToBase64(hash)</returns>
        public static String HashToString(Byte[] hash) => Convert.ToBase64String(hash);

            /// <summary>
        /// Get a random salt, 24 chars using 144 random bits
        /// </summary>
        /// <returns>A random salt, 24 chars using 144 random bits</returns>
        public static String GetRandomSalt()
        {
            using var rng = SecureRng.Get();
            return rng.GetGuid24();
        }

        /// <summary>
        /// Get a salt from some data, 24 chars using 144 random hashed bits
        /// </summary>
        /// <returns>A salt, 24 chars using 144 hash bits</returns>
        public static String GetHashSalt(ReadOnlySpan<Byte> data) => SecureRng.GetHashGuid24(data);

        /// <summary>
        /// Get a salt from some string, 24 chars using 144 random hashed bits
        /// </summary>
        /// <returns>A salt, 24 chars using 144 hash bits</returns>
        public static String GetHashSalt(String text) => SecureRng.GetHashGuid24(Encoding.UTF8.GetBytes(text));


        /// <summary>
        /// When password validating fails, this could be 
        /// </summary>
        public const String PasswordRules = "Password must be at least 8 characters.";

        /// <summary>
        /// Validate a password, return error string
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
        /// Compute a simple salt for given user (unique per user per application).
        /// Salt = Hash(username + AppAssemblyName all lowercased).
        /// </summary>
        /// <param name="user">A valid user name</param>
        /// <returns>PLain text salt</returns>
        public static String ComputeSimpleSalt(String user)
        { 
            var hash = ComputeHash((user + EnvInfo.AppAssemblyName).FastToLower() + "XfETwfDcxBJGvHmgd");
            return HashToString(hash);
        }

        /// <summary>
        /// Compute a simple hash (salt is username + AppAssemblyName all lowercased).
        /// </summary>
        /// <param name="user">A valid user name</param>
        /// <param name="password">A valid password</param>
        /// <returns>Hash as a string</returns>
        public static String ComputeSimplePasswordHash(String user, String password)
        {
            var salt = ComputeSimpleSalt(user);
            var h = ComputeHash(password, salt);
            return HashToString(h);
        }

        /// <summary>
        /// A token that no one should have
        /// </summary>
        public const String NoAuthToken = "  No auth ";

        public static readonly IReadOnlyList<String> Empty = [];

        public static readonly IReadOnlySet<String> EmptyTokens = new HashSet<String>(StringComparer.Ordinal).Freeze();

        public static readonly IReadOnlyList<String> NoAuth = [NoAuthToken];

        public static readonly IReadOnlySet<String> NoAuthSet = new HashSet<String>(StringComparer.Ordinal)
        {
            NoAuthToken
        }.Freeze();



        static IReadOnlyList<String> GetList(String tokens)
        {
            var t = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in tokens.Split(','))
                t.Add(x.Trim().FastToLower());
            return t.ToArray();
        }

        public static readonly IReadOnlyList<String> DebugAuth = GetList(Roles.Debug);
        public static readonly IReadOnlyList<String> DevAuth = GetList(Roles.Dev);

        public static void ValidateUserGuid(String guid)
        {
            if (guid.Length > AuhorizationLimits.MaxGuidLength)
                throw new Exception("Guid to long!");
            if (!StringTools.IsAsciiOnly(guid))
                throw new Exception("Guid may only contain ASCII chars!");
        }

    }

    public static class AuhorizationLimits
    {
        public const int MaxUserNameLength = 128;
        public const int MaxEmailLength = 128;
        public const int MaxGuidLength = 48;
        public const int MaxDomainName = 256;
        public const int MaxNickNameLength = 24;

    }


}
