using SysWeaver.Compression;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace SysWeaver
{


    /// <summary>
    /// Computes and caches file hashes of the decompressed content (automatic pruning).
    /// Folders can be overridden using the key "FileHashFolders" in the ApplicationName.Config.json file (shared with FileHash).
    /// Default uses the Folders.AllSharedFolders locations.
    /// </summary>
    public static class DecompressedFileHash
    {
        /// <summary>
        /// Get a hash of the decompressed contents of the supplied file
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content, must be in one of the known compressed formats</param>
        /// <returns>A hash string (26 chars) or null if there is some error</returns>
        public static String GetHash(String filename)
        {
            var keyName = FileHash.GetCacheKey(filename);
            if (keyName == null)
                return null;
            var d = FileHash.GetCacheFolder(filename);
            var fn = Path.Combine(d, keyName + "_d.txt");
            try
            {
                var ci = new FileInfo(fn);
                if (ci.Exists)
                {
                    var s = File.ReadAllText(fn, Encoding.ASCII);
                    if (s.Length == 26)
                    {
                        ci.LastAccessTimeUtc = DateTime.UtcNow;
                        return s;
                    }
                }
            }
            catch
            {
            }
            var ext = Path.GetExtension(filename);
            var comp = CompManager.GetFromExt(ext);
            if (comp == null)
                return null;

            Byte[] hash;
            try
            {
                ReadOnlyMemory<Byte> data;
                using (var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    data = comp.GetDecompressed(s);
                hash = MD5.HashData(data.Span);
            }
            catch
            {
                return null;
            }
            var text = HashTools.GetHashString16(hash);
            try
            {
                File.WriteAllText(fn, text, Encoding.ASCII);
            }
            catch
            {
                try
                {
                    File.Delete(fn);
                }
                catch
                {

                }
            }
            return text;
        }

        /// <summary>
        /// Get a hash of the decompressed contents of the supplied file
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content, must be in one of the known compressed formats</param>
        /// <returns>A hash string (26 chars) or null if there is some error</returns>
        public static async Task<String> GetHashAsync(String filename)
        {
            var keyName = await FileHash.GetCacheKeyAsync(filename).ConfigureAwait(false);
            if (keyName == null)
                return null;
            var d = FileHash.GetCacheFolder(filename);
            var fn = Path.Combine(d, keyName + "_d.txt");
            try
            {
                var ci = new FileInfo(fn);
                if (ci.Exists)
                {
                    var s = await File.ReadAllTextAsync(fn, Encoding.ASCII).ConfigureAwait(false);
                    if (s.Length == 26)
                    {
                        ci.LastAccessTimeUtc = DateTime.UtcNow;
                        return s;
                    }
                }
            }
            catch
            {
            }
            var ext = Path.GetExtension(filename);
            var comp = CompManager.GetFromExt(ext);
            if (comp == null)
                return null;

            Byte[] hash;
            try
            {
                ReadOnlyMemory<Byte> data;
                using (var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    data = await comp.GetDecompressedAsync(s).ConfigureAwait(false);
                hash = MD5.HashData(data.Span);
            }
            catch
            {
                return null;
            }
            var text = HashTools.GetHashString16(hash);
            try
            {
                await File.WriteAllTextAsync(fn, text, Encoding.ASCII).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    File.Delete(fn);
                }
                catch
                {

                }
            }
            return text;
        }

    }

}
