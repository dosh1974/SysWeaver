using System;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Auth
{

    /// <summary>
    /// The SCRAM parameters of a user, sent to the client (by GetUserSalt) so it can build a <see cref="ScramProof"/>.
    /// </summary>
    /// <remarks>
    /// The client computes:
    /// <code>
    ///     H              = the password hash computed by the client (ToBase64(SHA256(UTF8(password|salt))), using the salt of the authorizer)
    ///     SaltedPassword = PBKDF2-SHA256(UTF8(H + "|" + Binding), FromBase64(Salt), Iterations, 32 bytes)
    ///     ClientKey      = HMAC-SHA256(SaltedPassword, UTF8("Client Key"))
    ///     StoredKey      = SHA256(ClientKey)
    ///     AuthMessage    = UTF8(user + "," + oneTimePad + "," + clientNonce)
    ///     ClientProof    = ClientKey XOR HMAC-SHA256(StoredKey, AuthMessage)
    /// </code>
    /// See <see cref="ScramTools"/>.
    /// </remarks>
    public sealed class ScramInfo
    {
        /// <summary>
        /// The random (or derived) per user salt, base64
        /// </summary>
        public String Salt;

        /// <summary>
        /// The PBKDF2-SHA256 iteration count
        /// </summary>
        public int Iterations;

        /// <summary>
        /// The application binding string, appended to the password hash before PBKDF2 (so a stored value is bound to an application)
        /// </summary>
        public String Binding;
    }

    /// <summary>
    /// A SCRAM login proof (see <see cref="ScramInfo"/>).
    /// </summary>
    public sealed class ScramProof
    {
        /// <summary>
        /// The user name, as sent when the one time pad was requested
        /// </summary>
        public String UserName;

        /// <summary>
        /// The (already consumed) server one time pad
        /// </summary>
        public String OneTimePad;

        /// <summary>
        /// A random client nonce (may be empty)
        /// </summary>
        public String ClientNonce;

        /// <summary>
        /// The 32 byte client proof
        /// </summary>
        public Byte[] ClientProof;

        /// <summary>
        /// Create a proof object, returns null if the proof isn't valid base64 of 32 bytes
        /// </summary>
        /// <param name="userName">The user name</param>
        /// <param name="oneTimePad">The one time pad</param>
        /// <param name="clientNonce">The client nonce</param>
        /// <param name="clientProofB64">The base64 encoded proof</param>
        /// <returns>The proof or null</returns>
        public static ScramProof TryCreate(String userName, String oneTimePad, String clientNonce, String clientProofB64)
        {
            if (String.IsNullOrEmpty(clientProofB64) || (clientProofB64.Length > 64))
                return null;
            var b = new Byte[ScramTools.KeySize + 2];
            if (!Convert.TryFromBase64String(clientProofB64, b, out var w) || (w != ScramTools.KeySize))
                return null;
            Array.Resize(ref b, ScramTools.KeySize);
            return new ScramProof
            {
                UserName = userName ?? "",
                OneTimePad = oneTimePad ?? "",
                ClientNonce = clientNonce ?? "",
                ClientProof = b,
            };
        }
    }


    /// <summary>
    /// A stored SCRAM verifier: what an authorizer stores per user instead of a password equivalent hash.
    /// A stolen verifier can't be used to sign in (the client key is needed), finding the password costs PBKDF2 per guess.
    /// </summary>
    public sealed class ScramVerifier
    {
        /// <summary>
        /// The salt (16 bytes)
        /// </summary>
        public Byte[] Salt;

        /// <summary>
        /// The PBKDF2 iteration count
        /// </summary>
        public int Iterations;

        /// <summary>
        /// SHA256(ClientKey)
        /// </summary>
        public Byte[] StoredKey;

        /// <summary>
        /// The text format prefix of <see cref="ToString"/>
        /// </summary>
        public const String Prefix = "$s1$";

        /// <summary>
        /// Text format: "$s1$iterations$salt_b64$storedKey_b64" (no ':' or '|' chars, so it can be used in a user line)
        /// </summary>
        /// <returns>The verifier as text</returns>
        public override string ToString() => String.Concat(Prefix, Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture), "$", Convert.ToBase64String(Salt), "$", Convert.ToBase64String(StoredKey));

        /// <summary>
        /// Parse a verifier from it's text format (see <see cref="ToString"/>)
        /// </summary>
        /// <param name="text">The text</param>
        /// <returns>The verifier or null if the text isn't a valid verifier</returns>
        public static ScramVerifier TryParse(String text)
        {
            if ((text == null) || !text.StartsWith(Prefix, StringComparison.Ordinal))
                return null;
            var p = text.Substring(Prefix.Length).Split('$');
            if (p.Length != 3)
                return null;
            if (!int.TryParse(p[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var it))
                return null;
            if ((it < ScramTools.MinIterations) || (it > ScramTools.MaxIterations))
                return null;
            try
            {
                var salt = Convert.FromBase64String(p[1]);
                var sk = Convert.FromBase64String(p[2]);
                if ((salt.Length < 8) || (salt.Length > 64) || (sk.Length != ScramTools.KeySize))
                    return null;
                return new ScramVerifier
                {
                    Iterations = it,
                    Salt = salt,
                    StoredKey = sk,
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Get the info to send to a client
        /// </summary>
        /// <param name="binding">The application binding string</param>
        /// <returns>The info</returns>
        public ScramInfo GetInfo(String binding) => new ScramInfo
        {
            Salt = Convert.ToBase64String(Salt),
            Iterations = Iterations,
            Binding = binding ?? "",
        };
    }


    /// <summary>
    /// SCRAM style (RFC 5802 / RFC 7677) helpers, see <see cref="ScramInfo"/> for the protocol.
    /// </summary>
    public static class ScramTools
    {
        /// <summary>
        /// The size of all keys (SHA256 output)
        /// </summary>
        public const int KeySize = 32;

        /// <summary>
        /// The size of new salts
        /// </summary>
        public const int SaltSize = 16;

        /// <summary>
        /// The default PBKDF2 iteration count
        /// </summary>
        public const int DefaultIterations = 200_000;

        /// <summary>
        /// The minimum accepted PBKDF2 iteration count
        /// </summary>
        public const int MinIterations = 4096;

        /// <summary>
        /// The maximum accepted PBKDF2 iteration count
        /// </summary>
        public const int MaxIterations = 10_000_000;

        /// <summary>
        /// Clamp an iteration count to the valid range, 0 or less gives the default.
        /// </summary>
        /// <param name="iterations">The iteration count</param>
        /// <returns>A valid iteration count</returns>
        public static int ClampIterations(int iterations)
            => iterations <= 0 ? DefaultIterations : Math.Clamp(iterations, MinIterations, MaxIterations);

        static readonly Byte[] ClientKeyText = Encoding.ASCII.GetBytes("Client Key");

        /// <summary>
        /// Compute the client key from a password hash.
        /// </summary>
        /// <param name="passwordHash">The password hash string (H)</param>
        /// <param name="binding">The application binding</param>
        /// <param name="salt">The salt</param>
        /// <param name="iterations">The iteration count</param>
        /// <returns>The client key</returns>
        public static Byte[] ComputeClientKey(String passwordHash, String binding, ReadOnlySpan<Byte> salt, int iterations)
        {
            var pwd = Encoding.UTF8.GetBytes(String.Concat(passwordHash ?? "", "|", binding ?? ""));
            Span<Byte> salted = stackalloc Byte[KeySize];
            try
            {
                Rfc2898DeriveBytes.Pbkdf2(pwd, salt, salted, iterations, HashAlgorithmName.SHA256);
                return HMACSHA256.HashData(salted, ClientKeyText);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salted);
                CryptographicOperations.ZeroMemory(pwd);
            }
        }

        /// <summary>
        /// Create a verifier from a password hash (H).
        /// </summary>
        /// <param name="passwordHash">The password hash string (H)</param>
        /// <param name="binding">The application binding</param>
        /// <param name="iterations">The iteration count, 0 or less for the default</param>
        /// <param name="salt">The salt to use, null for a random salt</param>
        /// <returns>The verifier</returns>
        public static ScramVerifier CreateVerifier(String passwordHash, String binding, int iterations = 0, Byte[] salt = null)
        {
            iterations = ClampIterations(iterations);
            salt = salt ?? RandomNumberGenerator.GetBytes(SaltSize);
            var ck = ComputeClientKey(passwordHash, binding, salt, iterations);
            try
            {
                return new ScramVerifier
                {
                    Iterations = iterations,
                    Salt = salt,
                    StoredKey = SHA256.HashData(ck),
                };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ck);
            }
        }

        /// <summary>
        /// Build the auth message
        /// </summary>
        /// <param name="userName">The user name</param>
        /// <param name="oneTimePad">The one time pad</param>
        /// <param name="clientNonce">The client nonce</param>
        /// <returns>UTF8(user,pad,nonce)</returns>
        public static Byte[] GetAuthMessage(String userName, String oneTimePad, String clientNonce)
            => Encoding.UTF8.GetBytes(String.Join(',', userName ?? "", oneTimePad ?? "", clientNonce ?? ""));

        /// <summary>
        /// Validate a proof against a stored key (constant time).
        /// </summary>
        /// <param name="storedKey">The stored key of the user</param>
        /// <param name="proof">The proof</param>
        /// <returns>True if the proof is valid</returns>
        public static bool Verify(ReadOnlySpan<Byte> storedKey, ScramProof proof)
        {
            if ((proof?.ClientProof == null) || (proof.ClientProof.Length != KeySize) || (storedKey.Length != KeySize))
                return false;
            Span<Byte> sig = stackalloc Byte[KeySize];
            Span<Byte> key = stackalloc Byte[KeySize];
            HMACSHA256.HashData(storedKey, GetAuthMessage(proof.UserName, proof.OneTimePad, proof.ClientNonce), sig);
            var p = proof.ClientProof;
            for (int i = 0; i < KeySize; ++i)
                key[i] = (Byte)(p[i] ^ sig[i]);
            SHA256.HashData(key, sig);
            CryptographicOperations.ZeroMemory(key);
            return CryptographicOperations.FixedTimeEquals(sig, storedKey);
        }

        /// <summary>
        /// Validate a proof against a verifier (constant time).
        /// </summary>
        /// <param name="v">The stored verifier, null always fails</param>
        /// <param name="proof">The proof</param>
        /// <returns>True if the proof is valid</returns>
        public static bool Verify(ScramVerifier v, ScramProof proof) => (v != null) && Verify(v.StoredKey, proof);

        /// <summary>
        /// Validate a password hash (H, ex: from basic auth) against a verifier (costs one PBKDF2).
        /// </summary>
        /// <param name="v">The verifier, null always fails</param>
        /// <param name="passwordHash">The password hash</param>
        /// <param name="binding">The application binding</param>
        /// <returns>True if the hash matches</returns>
        public static bool VerifyLegacyHash(ScramVerifier v, String passwordHash, String binding)
        {
            if ((v == null) || String.IsNullOrEmpty(passwordHash))
                return false;
            var ck = ComputeClientKey(passwordHash, binding, v.Salt, v.Iterations);
            Span<Byte> sk = stackalloc Byte[KeySize];
            SHA256.HashData(ck, sk);
            CryptographicOperations.ZeroMemory(ck);
            return CryptographicOperations.FixedTimeEquals(sk, v.StoredKey);
        }

        /// <summary>
        /// Build a client proof (what a client does), used by C# clients and tests.
        /// </summary>
        /// <param name="info">The SCRAM info from the server</param>
        /// <param name="passwordHash">The password hash (H)</param>
        /// <param name="userName">The user name (as sent to GetUserSalt)</param>
        /// <param name="oneTimePad">The one time pad</param>
        /// <param name="clientNonce">The client nonce</param>
        /// <returns>The base64 encoded client proof</returns>
        public static String CreateClientProof(ScramInfo info, String passwordHash, String userName, String oneTimePad, String clientNonce)
        {
            var ck = ComputeClientKey(passwordHash, info.Binding, Convert.FromBase64String(info.Salt), ClampIterations(info.Iterations));
            Span<Byte> sk = stackalloc Byte[KeySize];
            Span<Byte> sig = stackalloc Byte[KeySize];
            SHA256.HashData(ck, sk);
            HMACSHA256.HashData(sk, GetAuthMessage(userName, oneTimePad, clientNonce), sig);
            for (int i = 0; i < KeySize; ++i)
                sig[i] ^= ck[i];
            CryptographicOperations.ZeroMemory(ck);
            return Convert.ToBase64String(sig);
        }

        /// <summary>
        /// Create a random client nonce
        /// </summary>
        /// <returns>A random base64 string (16 bytes)</returns>
        public static String CreateNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        /// <summary>
        /// The scheme name sent to clients in the GetUserSalt response
        /// </summary>
        public const String SchemeName = "s1";

    }
}
