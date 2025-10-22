using SimpleStack.Orm.Attributes;
using System;
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
            var data = Encoding.UTF8.GetBytes(String.Join('|', from, to, text, context));
            var hash = SHA512.HashData(data).ToHex();
            return hash;
        }


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
            var d = BlobComp.GetDecompressed(data);
            var s = Enc.GetString(d.Span);
            return s;
        }

    }




}
