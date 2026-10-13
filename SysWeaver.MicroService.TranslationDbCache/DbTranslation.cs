using SimpleStack.Orm.Attributes;
using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using SysWeaver.Compression;

namespace SysWeaver.MicroService
{





    [Alias("Translations")]
    [PartitionByKey(nameof(Hash))]
    public sealed class DbTranslation
    {
        /// <summary>
        /// Time stamp when translation was performed
        /// </summary>
        [Required]
        [Index]
        public long Time { get; set; }

        /// <summary>
        /// The hash key used
        /// </summary>
        [StringLength(128)]
        [Ascii]
        [PrimaryKey]
        public String Hash { get; set; }

        /// <summary>
        /// The compressed translated text
        /// </summary>
        [Required]
        public Byte[] Translated { get; set; }

        /// <summary>
        /// Time stamp when translation was performed
        /// </summary>
        [Required]
        [Index]
        [Default(638984160000000000L)] 
        public long Expiration { get; set; }

        public static String ComputeHash(String from, String to, String text, String context)
        {
            //  Same as the SHA512 (lower case hex) of the UTF-8 bytes of String.Join('|', from, to, text, context) (null is empty).
            //  The parts are encoded one by one (a '|' is never a part of a surrogate pair, so the bytes are the same) into a stack or pooled buffer,
            //  and hashed into a stack buffer, only the hex string is allocated
            from ??= String.Empty;
            to ??= String.Empty;
            text ??= String.Empty;
            context ??= String.Empty;
            var enc = Encoding.UTF8;
            //  At most 3 bytes per char
            var maxLen = ((long)from.Length + to.Length + text.Length + context.Length) * 3 + 3;
            Span<Byte> hash = stackalloc Byte[SHA512.HashSizeInBytes];
            Byte[] rented = null;
            Span<Byte> data = maxLen <= MaxStackHashBytes
                ? stackalloc Byte[MaxStackHashBytes]
                : (rented = ArrayPool<Byte>.Shared.Rent(enc.GetByteCount(from) + enc.GetByteCount(to) + enc.GetByteCount(text) + enc.GetByteCount(context) + 3));
            try
            {
                var o = enc.GetBytes(from, data);
                data[o] = (Byte)'|';
                ++o;
                o += enc.GetBytes(to, data.Slice(o));
                data[o] = (Byte)'|';
                ++o;
                o += enc.GetBytes(text, data.Slice(o));
                data[o] = (Byte)'|';
                ++o;
                o += enc.GetBytes(context, data.Slice(o));
                SHA512.HashData(data.Slice(0, o), hash);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Byte>.Shared.Return(rented);
            }
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// The max number of bytes to hash that <see cref="ComputeHash(string, string, string, string)"/> encodes on the stack
        /// </summary>
        const int MaxStackHashBytes = 1024;


        static readonly Encoding Enc = Encoding.UTF8;
        static readonly ICompType BlobComp = CompManager.GetFromHttp("br");

        public static Byte[] ToBlob(String text)
        {
            var temp = GC.AllocateUninitializedArray<Byte>(text.Length * 5);
            var len = Enc.GetBytes(text, temp);
            var v = BlobComp.GetCompressed(temp.AsSpan(0, len), CompEncoderLevels.Best);
            return v.ToArray();
        }

        public static String FromBlob(Byte[] data)
        {
            using var d = BlobComp.GetUnmanagedDecompressed(data);
            var s = Enc.GetString(d.Memory.Span);
            return s;
        }

    }




}
