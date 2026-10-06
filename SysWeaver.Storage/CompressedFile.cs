using SysWeaver.Compression;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;


namespace SysWeaver
{
    /// <summary>
    /// Utility class to open a compressed version of a file.
    /// The compressed data is cached on disc (using <see cref="FileMetaData"/>, keyed by the content hash of the source file, compression type and level),
    /// so re-opening the same content is fast. Cached files not used for 30 days are pruned at process exit.
    /// </summary>
    /// <remarks>
    /// Used by the HTTP server to serve pre-compressed static files.
    /// Concurrent requests for the same content are serialized using a <see cref="SystemLock"/>.
    /// </remarks>
    public static class CompressedFile
    {

        sealed class CompFile
        {
            public long Len;
            public String Filename;
        }

        /// <summary>
        /// Open a compressed version of a file, compressing it first if no cached copy exists.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>A read only stream of the cached compressed file, the caller must dispose it.</returns>
        /// <exception cref="Exception"><paramref name="file"/> doesn't exist.</exception>
        public static Stream Open(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => new FileStream(GetCompFile(file, compType, level), FileMode.Open, FileAccess.Read, FileShare.Read);


        /// <summary>
        /// Open a compressed version of a file, compressing it first if no cached copy exists.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>A read only stream of the cached compressed file, the caller must dispose it.</returns>
        /// <exception cref="Exception"><paramref name="file"/> doesn't exist.</exception>
        public static async Task<Stream> OpenAsync(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => new FileStream(await GetCompFileAsync(file, compType, level).ConfigureAwait(false), FileMode.Open, FileAccess.Read, FileShare.Read);


        /// <summary>
        /// Read all compressed bytes of a file, compressing it first if no cached copy exists.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>The compressed data, the caller must dispose it.</returns>
        /// <exception cref="Exception"><paramref name="file"/> doesn't exist.</exception>
        public static IUnmanagedReadOnlyMemory<Byte> ReadAllBytes(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => FileReadOnlyMemory.Read(GetCompFile(file, compType, level));

        /// <summary>
        /// Read all compressed bytes of a file, compressing it first if no cached copy exists.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>The compressed data, the caller must dispose it.</returns>
        /// <exception cref="Exception"><paramref name="file"/> doesn't exist.</exception>
        public static async Task<IUnmanagedReadOnlyMemory<Byte>> ReadAllBytesAsync(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => FileReadOnlyMemory.Read(await GetCompFileAsync(file, compType, level).ConfigureAwait(false));

        static String GetCompFile(String file, ICompType compType, CompEncoderLevels level)
        {
            var fn = new FileInfo(file);
            if (!fn.Exists)
                throw new Exception("File does not exist!");
            var ext = compType.FileExtensions?.FirstOrDefault() ?? "cmp";
            var suffix = String.Concat('_', compType.HttpCode, '_', level, '.', ext);
            return FileMetaData.Process<CompFile>("CompressedFile", file, (srcFile, compName, existing) =>
            {
                var fi = new FileInfo(compName);
                if (existing != null)
                {
                    if (fi.Exists && (fi.Length == existing.Len) && (existing.Filename.FastEquals(compName)))
                        return null;
                }
                using (var s = fn.OpenRead())
                using (var d = fi.OpenWrite())
                    compType.Compress(s, d, level);
                return new CompFile
                {
                    Filename = compName,
                    Len = fi.Length,
                };
            }, 30, suffix).Filename;
        }

        static async Task<String> GetCompFileAsync(String file, ICompType compType, CompEncoderLevels level)
        {
            var fn = new FileInfo(file);
            if (!fn.Exists)
                throw new Exception("File does not exist!");
            var ext = compType.FileExtensions?.FirstOrDefault() ?? "cmp";
            var suffix = String.Concat('_', compType.HttpCode, '_', level, '.', ext);
            return (await FileMetaData.ProcessAsync<CompFile>("CompressedFile", file, async (srcFile, compName, existing) =>
            {
                var fi = new FileInfo(compName);
                if (existing != null)
                {
                    if (fi.Exists && (fi.Length == existing.Len) && (existing.Filename.FastEquals(compName)))
                        return null;
                }
                using (var s = fn.OpenRead())
                using (var d = fi.OpenWrite())
                    await compType.CompressAsync(s, d, level).ConfigureAwait(false);
                return new CompFile
                {
                    Filename = compName,
                    Len = fi.Length,
                };
            }, 30, suffix).ConfigureAwait(false)).Filename;
        }

    }

}
