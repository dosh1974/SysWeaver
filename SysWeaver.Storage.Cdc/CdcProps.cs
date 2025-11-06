using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SysWeaver.Compression;

namespace SysWeaver
{
    
    public sealed class CdcProps
    {
        /// <summary>
        /// Signature of a function that is used for computing the hash of a chunk
        /// </summary>
        /// <param name="source">The data to compute the hash for</param>
        /// <param name="hash">The resulting hash</param>
        /// <param name="bytesWritten">Number of bytes written (must be consistent)</param>
        /// <returns>True if successful</returns>
        public delegate bool Hasher(ReadOnlySpan<byte> source, Span<byte> hash, out int bytesWritten);


        /// <summary>
        /// Init some new properties, properties must be the same everywhere for things to work properly.
        /// Supply null to function or use (CdcProps.Default).
        /// Only create a new property set for testing.
        /// </summary>
        /// <param name="averageSize">The target chunk size, must be a power of 2</param>
        /// <param name="minSize">Computed from averageSize, but can be overriden</param>
        /// <param name="maxSize">Computed from averageSize, but can be overriden</param>
        /// <param name="hash">The hash function to use</param>
        /// <param name="hashName">Name of this hash function, musn't be the same as any other used hash function, defaults to SHA256</param>
        /// <param name="compression">The compression method to use for chunk storage (and transmission etc)</param>
        /// <param name="folders">Optionally store chunk data in these folders (advanced)</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public CdcProps(int averageSize = 1 << 15, int minSize = 0, int maxSize = 0, Hasher hash = null, String hashName = null, String compression = "br", IReadOnlyList<String> folders = null)
        {
            if (averageSize < 1024)
                throw new ArgumentException("Average size must be at least 1024!", nameof(averageSize));
            if (!averageSize.IsPow2())
                throw new ArgumentException("Average size must be a power of two!", nameof(averageSize));
            if ((minSize <= 0) || (minSize >= averageSize))
                minSize = averageSize >> 2;
            if (maxSize <= averageSize)
                maxSize = averageSize << 2;
            if (hash == null)
            {
                hash = SHA256.TryHashData;
                if (hashName == null)
                    hashName = "SHA256";
            }
            var t = Temp.AsSpan();
            if (!hash(t, t, out var hashSize))
                throw new Exception("Hasher failed!");
            Comp = CompManager.GetFromHttp(compression);
            HashSize = hashSize;
            Hash = hash;
            HashName = hashName;
            AverageSize = averageSize;
            MinSize = minSize;
            MaxSize = maxSize;
            var mask = (uint)averageSize;
            mask -= 1;
            Mask1 = (mask << 1) | mask;
            Mask2 = mask >> 1;
            var key = String.Concat(hashName, ' ', AverageSize, " in [", MinSize, ", ", MaxSize, ']');
            Key = key;
            ChunkFolders = (folders?.Count ?? 0) > 0 ? folders : Folders.AllSharedFolders.Convert(folder => Path.Combine(folder, "ContentChunks", key));
        }
   
        static readonly Byte[] Temp = new byte[1024];

        public override string ToString() => Key;


        public String CompFileExt => Comp.FileExtensions.FirstOrDefault() ?? "comp";

        /// <summary>
        /// The compression used for chunks
        /// </summary>
        public readonly ICompType Comp;

        /// <summary>
        /// The function used to compute a hash
        /// </summary>
        public readonly Hasher Hash;

        /// <summary>
        /// Name of this hash function, musn't be the same as any other used hash function
        /// </summary>
        public readonly String HashName;

        /// <summary>
        /// The number of bytes for a hash
        /// </summary>
        public readonly int HashSize;

        /// <summary>
        /// The minimum size of a chunk (before compression)
        /// </summary>
        public readonly int MinSize;

        /// <summary>
        /// The maximum size of a chunk (before compression)
        /// </summary>
        public readonly int MaxSize;

        /// <summary>
        /// The target size of chunk (before compression)
        /// </summary>
        public readonly int AverageSize;
        
        /// <summary>
        /// The "Key" for these properties, used for folder names etc
        /// </summary>
        public readonly String Key;

        /// <summary>
        /// A computed mask, used internally
        /// </summary>
        internal readonly uint Mask1;

        /// <summary>
        /// A computed mask, used internally
        /// </summary>
        internal readonly uint Mask2;

        /// <summary>
        /// Chunk data is stored in these folders, somewhat evenly distributed
        /// </summary>
        public readonly IReadOnlyList<String> ChunkFolders;

        /// <summary>
        /// The default properties (used when null is supplied)
        /// </summary>
        public static readonly CdcProps Default = new CdcProps();

    }
}
