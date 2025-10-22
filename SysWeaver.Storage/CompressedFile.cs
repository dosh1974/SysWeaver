using SysWeaver.Compression;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;


namespace SysWeaver
{
    /// <summary>
    /// Utility class to open a compressed version of a file.
    /// Files are cached so re-opening will be faster.
    /// </summary>
    public static class CompressedFile
    {

        sealed class CompFile
        {
            public long Len;
            public String Filename;
        }

        /// <summary>
        /// Open a compressed version of a file, with caching etc.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>A compressed stream</returns>
        public static Stream Open(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => new FileStream(GetCompFile(file, compType, level), FileMode.Open, FileAccess.Read, FileShare.Read);


        /// <summary>
        /// Open a compressed version of a file, with caching etc.
        /// </summary>
        /// <param name="file">The name of the file to get the compressed data for</param>
        /// <param name="compType">The compression type</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>A compressed stream</returns>
        public static async ValueTask<Stream> OpenAsync(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => new FileStream(await GetCompFileAsync(file, compType, level).ConfigureAwait(false), FileMode.Open, FileAccess.Read, FileShare.Read);


        /// <summary>
        /// Read all compressed bytes from a file (with caching etc)
        /// </summary>
        /// <param name="file"></param>
        /// <param name="compType"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static Byte[] ReadAllBytes(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => File.ReadAllBytes(GetCompFile(file, compType, level));

        /// <summary>
        /// Read all compressed bytes from a file (with caching etc)
        /// </summary>
        /// <param name="file"></param>
        /// <param name="compType"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static async ValueTask<Byte[]> ReadAllBytesAsync(String file, ICompType compType, CompEncoderLevels level = CompEncoderLevels.Best)
            => File.ReadAllBytes(await GetCompFileAsync(file, compType, level).ConfigureAwait(false));

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

        static async ValueTask<String> GetCompFileAsync(String file, ICompType compType, CompEncoderLevels level)
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
