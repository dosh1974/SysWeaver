using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Protects stored personal / secret values (e-mail addresses, phone numbers, user names, API keys ...) using a random program secret
    /// that is stored outside of the data (in <see cref="KeyValueStore.AllApp"/>, unique per application and computer).
    /// A leaked database (or data file) doesn't reveal the values without the secret, which needs access to the machine.
    /// </summary>
    /// <remarks>
    /// For every protected value:
    /// <list type="bullet">
    /// <item>A lookup hash (<see cref="Lookup"/>): HMAC-SHA256(secret, kind + "\0" + normalized value), indexable, used to find a row from a value (ex: e-mail => user id).</item>
    /// <item>A per row random value (<see cref="NewRandom"/>), replaced on every change.</item>
    /// <item>The encrypted value (<see cref="Encrypt"/>): AES-256-GCM with the key HMAC-SHA256(secret, "enc\0" + kind + "\0" + random) and a random nonce,
    /// stored as nonce (12 bytes) + cipher text + tag (16 bytes). Used to get the value back (ex: the e-mail address of a user).</item>
    /// </list>
    /// IMPORTANT: if the secret is lost (ex: the key value store folder is deleted, or the data is moved to another computer) the protected values can't be read or found.
    /// Applications (or computers) that share the same data must use the same secret (see <see cref="FromBase64(string)"/>).
    /// Thread safe.
    /// </remarks>
    public sealed class DataProtection
    {
        /// <summary>
        /// The key value store key of the default secret
        /// </summary>
        public const String SecretKey = "SysWeaver.DataProtection.Secret";

        /// <summary>
        /// The size of the secret in bytes
        /// </summary>
        public const int SecretSize = 32;

        /// <summary>
        /// The size of a lookup hash in bytes
        /// </summary>
        public const int LookupSize = 32;

        /// <summary>
        /// The size of the per row random value in bytes
        /// </summary>
        public const int RandomSize = 16;

        const int NonceSize = 12;
        const int TagSize = 16;

        /// <summary>
        /// The extra bytes of an encrypted value (nonce + tag)
        /// </summary>
        public const int Overhead = NonceSize + TagSize;

        /// <summary>
        /// Create using a secret
        /// </summary>
        /// <param name="secret">The secret (at least 16 bytes)</param>
        /// <exception cref="ArgumentException">The secret is too short</exception>
        public DataProtection(ReadOnlySpan<Byte> secret)
        {
            if (secret.Length < 16)
                throw new ArgumentException("The secret must be at least 16 bytes", nameof(secret));
            Secret = secret.ToArray();
        }

        readonly Byte[] Secret;

        /// <summary>
        /// Create using a base64 encoded secret (ex: from a configuration, so that several applications or computers can share data)
        /// </summary>
        /// <param name="secret">The base64 encoded secret (at least 16 bytes)</param>
        /// <returns>The instance</returns>
        public static DataProtection FromBase64(String secret) => new DataProtection(Convert.FromBase64String(secret));

        /// <summary>
        /// Get the secret as base64 (the inverse of <see cref="FromBase64(string)"/>), ex: to configure another application or computer that must share the data.
        /// IMPORTANT: anyone with the secret can read the protected values of a copy of the data.
        /// </summary>
        /// <returns>The base64 encoded secret</returns>
        public String ToBase64() => Convert.ToBase64String(Secret);

        /// <summary>
        /// The default instance, the secret is stored in <see cref="KeyValueStore.AllApp"/> (created on first use).
        /// </summary>
        public static DataProtection Default
        {
            get
            {
                var d = InternalDefault;
                if (d != null)
                    return d;
                lock (DefaultLock)
                {
                    d = InternalDefault;
                    if (d != null)
                        return d;
                    var store = KeyValueStore.AllApp;
                    var s = store.TryGet<String>(SecretKey);
                    Byte[] secret = null;
                    if (!String.IsNullOrEmpty(s))
                    {
                        secret = Convert.FromBase64String(s);
                        if (secret.Length < 16)
                            throw new Exception("The stored data protection secret is invalid (" + SecretKey + ")");
                    }
                    else
                    {
                        secret = RandomNumberGenerator.GetBytes(SecretSize);
                        store.Set(SecretKey, Convert.ToBase64String(secret));
                        //  Read back: never use a secret that wasn't stored (everything protected with it would be lost on restart)
                        if (store.TryGet<String>(SecretKey) != Convert.ToBase64String(secret))
                            throw new Exception("Failed to store the data protection secret (" + SecretKey + ")");
                    }
                    d = new DataProtection(secret);
                    Volatile.Write(ref InternalDefault, d);
                    return d;
                }
            }
        }

        static DataProtection InternalDefault;
        static readonly Object DefaultLock = new Object();

        /// <summary>
        /// A short, non secret fingerprint of the secret (to detect that data was protected with another secret), 8 hex chars
        /// </summary>
        public String Fingerprint => Convert.ToHexString(HMACSHA256.HashData(Secret, "fingerprint"u8), 0, 4);

        /// <summary>
        /// Create a new random value (one per row, replace it on every change)
        /// </summary>
        /// <returns>16 random bytes</returns>
        public static Byte[] NewRandom() => RandomNumberGenerator.GetBytes(RandomSize);

        static Byte[] Join(String a, String b)
            => Encoding.UTF8.GetBytes(String.Concat(a ?? "", "\0", b ?? ""));

        /// <summary>
        /// Compute the lookup hash of a value, the value must be normalized by the caller (ex: lower case e-mail).
        /// </summary>
        /// <param name="kind">The kind of value (ex: "email"), so that equal values of different kinds have different hashes</param>
        /// <param name="normalizedValue">The normalized value</param>
        /// <returns>The lookup hash (32 bytes)</returns>
        public Byte[] Lookup(String kind, String normalizedValue)
            => HMACSHA256.HashData(Secret, Join(kind, normalizedValue));

        Byte[] GetKey(String kind, ReadOnlySpan<Byte> random)
        {
            var k = Encoding.UTF8.GetBytes(String.Concat("enc\0", kind ?? "", "\0"));
            var b = new Byte[k.Length + random.Length];
            k.CopyTo(b, 0);
            random.CopyTo(b.AsSpan(k.Length));
            return HMACSHA256.HashData(Secret, b);
        }

        /// <summary>
        /// Encrypt a value
        /// </summary>
        /// <param name="kind">The kind of value (ex: "email")</param>
        /// <param name="random">The random value of the row (see <see cref="NewRandom"/>)</param>
        /// <param name="value">The value, null returns null</param>
        /// <returns>The encrypted value (nonce + cipher text + tag), null if the value is null</returns>
        public Byte[] Encrypt(String kind, ReadOnlySpan<Byte> random, String value)
        {
            if (value == null)
                return null;
            var plain = Encoding.UTF8.GetBytes(value);
            var key = GetKey(kind, random);
            try
            {
                var r = new Byte[NonceSize + plain.Length + TagSize];
                var nonce = r.AsSpan(0, NonceSize);
                RandomNumberGenerator.Fill(nonce);
                using var aes = new AesGcm(key, TagSize);
                aes.Encrypt(nonce, plain, r.AsSpan(NonceSize, plain.Length), r.AsSpan(NonceSize + plain.Length, TagSize));
                return r;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plain);
            }
        }

        /// <summary>
        /// Decrypt a value
        /// </summary>
        /// <param name="kind">The kind of value (ex: "email"), must be the same as when it was encrypted</param>
        /// <param name="random">The random value of the row, must be the same as when it was encrypted</param>
        /// <param name="data">The encrypted value, null returns null</param>
        /// <returns>The value, null if the data is null</returns>
        /// <exception cref="CryptographicException">The data was changed, or encrypted with another secret, kind or random value</exception>
        public String Decrypt(String kind, ReadOnlySpan<Byte> random, Byte[] data)
        {
            if (data == null)
                return null;
            if (data.Length < Overhead)
                throw new CryptographicException("Invalid protected data");
            var key = GetKey(kind, random);
            var l = data.Length - Overhead;
            var plain = new Byte[l];
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize, l), data.AsSpan(NonceSize + l, TagSize), plain);
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(plain);
            }
        }

        /// <summary>
        /// Encrypt binary data (same scheme as <see cref="Encrypt(string, ReadOnlySpan{byte}, string)"/>)
        /// </summary>
        /// <param name="kind">The kind of value</param>
        /// <param name="random">The random value of the row</param>
        /// <param name="plain">The data</param>
        /// <returns>The encrypted data (nonce + cipher text + tag)</returns>
        public Byte[] EncryptBytes(String kind, ReadOnlySpan<Byte> random, ReadOnlySpan<Byte> plain)
        {
            var key = GetKey(kind, random);
            try
            {
                var r = new Byte[NonceSize + plain.Length + TagSize];
                var nonce = r.AsSpan(0, NonceSize);
                RandomNumberGenerator.Fill(nonce);
                using var aes = new AesGcm(key, TagSize);
                aes.Encrypt(nonce, plain, r.AsSpan(NonceSize, plain.Length), r.AsSpan(NonceSize + plain.Length, TagSize));
                return r;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// Decrypt binary data
        /// </summary>
        /// <param name="kind">The kind of value, must be the same as when it was encrypted</param>
        /// <param name="random">The random value of the row, must be the same as when it was encrypted</param>
        /// <param name="data">The encrypted data</param>
        /// <returns>The data</returns>
        /// <exception cref="CryptographicException">The data was changed, or encrypted with another secret, kind or random value</exception>
        public Byte[] DecryptBytes(String kind, ReadOnlySpan<Byte> random, ReadOnlySpan<Byte> data)
        {
            if (data.Length < Overhead)
                throw new CryptographicException("Invalid protected data");
            var key = GetKey(kind, random);
            try
            {
                var l = data.Length - Overhead;
                var plain = new Byte[l];
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(data.Slice(0, NonceSize), data.Slice(NonceSize, l), data.Slice(NonceSize + l, TagSize), plain);
                return plain;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// Try to decrypt a value
        /// </summary>
        /// <param name="kind">The kind of value</param>
        /// <param name="random">The random value of the row</param>
        /// <param name="data">The encrypted value</param>
        /// <returns>The value, or null if the data is null or can't be decrypted</returns>
        public String TryDecrypt(String kind, ReadOnlySpan<Byte> random, Byte[] data)
        {
            try
            {
                return Decrypt(kind, random, data);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
