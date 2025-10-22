using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Computes and caches file hashes (automatic pruning).
    /// Folders can be overridden using the key "FileHashFolders" in the ApplicationName.Config.json file (shared with DecompressedFileHash).
    /// Default uses the Folders.AllSharedFolders locations.
    /// </summary>
    public static class FileHash
    {


        /// <summary>
        /// Determines if two files have equal content.
        /// If one file doesn't exist, this function returns false.
        /// If none of the files exist, this function returns true.
        /// Else the file content must match (using MD5 hash and length).
        /// </summary>
        /// <param name="a">One file</param>
        /// <param name="b">Another file</param>
        /// <returns>True if the files are identical, else false</returns>
        public static bool FilesAreEqual(String a, String b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (!fa.Exists)
                return !fb.Exists;
            if (!fb.Exists)
                return false;
            if (fa.Length != fb.Length)
                return false;
            var ha = GetHash(a);
            var hb = GetHash(b);
            return ha == hb;
        }


        /// <summary>
        /// Get a hash of the contents of the supplied file
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content</param>
        /// <returns>A hash string (26 chars) or null if there is some error</returns>
        public static String GetHash(String filename)
        {
            bool isWeb = IsWeb(filename);
            if (isWeb)
                return InternalHashAsync(filename, true).RunAsync();
            var keyName = GetCacheKey(filename);
            if (keyName == null)
                return null;
            var d = GetCacheFolder(filename);
            var fn = Path.Combine(d, keyName + ".txt");
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
            Byte[] hash;
            try
            {
                using (var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    hash = MD5.HashData(s);
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
        /// Get a hash of the contents of the supplied file
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content</param>
        /// <returns>A hash string (26 chars) or null if there is some error</returns>
        public static Task<String> GetHashAsync(String filename)
        {
            return InternalHashAsync(filename, IsWeb(filename));
        }

        static async Task<String> InternalHashAsync(String filename, bool isWeb)
        {
            var keyName = await GetCacheKeyAsync(filename, isWeb).ConfigureAwait(false);
            if (keyName == null)
                return null;
            var d = GetCacheFolder(filename);
            var fn = Path.Combine(d, keyName + ".txt");
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
            Byte[] hash;
            try
            {
                if (isWeb)
                {
                    var client = WebTools.HttpClient;
                    using var request = new HttpRequestMessage(HttpMethod.Get, filename);
                    using var response = await client.SendAsync(request);
                    // Ensure we got a successful response
                    if (!response.IsSuccessStatusCode)
                        return null;
                    using (var s = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        hash = await MD5.HashDataAsync(s).ConfigureAwait(false);
                }
                else
                {
                    using (var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                        hash = await MD5.HashDataAsync(s).ConfigureAwait(false);
                }
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




        #region Helpers


        /// <summary>
        /// The functions used to determine if the file is a web file or a local file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if the file will be downloaded</returns>
        public static bool IsWeb(string filename)
        {
            var t = filename.IndexOf("://");
            if (t < 0)
                return false;
            var x = filename.Substring(0, t);
            if (String.Equals(x, "http", StringComparison.Ordinal))
                return true;
            return String.Equals(x, "https", StringComparison.Ordinal);
        }


        static async Task<String> GetWebKey(String url)
        {
            try
            {
                var client = WebTools.HttpClient;
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(request);
                // Ensure we got a successful response
                if (!response.IsSuccessStatusCode)
                    return null;
                var h = response.Headers;

                String lm = null;
                if (h.TryGetValues("Last-Modified", out var lmh))
                    lm = lmh.FirstOrDefault();
                if (h.TryGetValues("ETag", out var eth))
                    lm = lm == null ? eth.FirstOrDefault() : String.Join('|', lm, eth.FirstOrDefault());
                if (lm == null)
                    lm = DateOnly.FromDateTime(DateTime.UtcNow).ToString();
                long length = 0;
                if (h.TryGetValues("Content-Length", out var x2))
                    long.TryParse(x2.FirstOrDefault() ?? "0", out length);
                return String.Join('|', url, lm, length);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Given a file, compute name for meta data
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content</param>
        /// <returns>The key name for this file</returns>
        public static String GetCacheKey(String filename)
        {
            String keyName;
            try
            {
                var fi = new FileInfo(filename);
                if (!fi.Exists)
                    return null;
                keyName = String.Join('|', fi.FullName, fi.LastWriteTimeUtc, fi.Length);
            }
            catch
            {
                return null;
            }
            var keyHash = MD5.HashData(System.Runtime.InteropServices.MemoryMarshal.Cast<Char, Byte>(keyName.AsSpan()));
            keyName = HashTools.GetHashString16(keyHash);
            return keyName;
        }


        /// <summary>
        /// Given a file, compute name for meta data
        /// </summary>
        /// <param name="filename">The existing file to get the hash of the content</param>
        /// <param name="isWeb">Set to true if the file is on the web</param>
        /// <returns>The key name for this file</returns>
        public static async Task<String> GetCacheKeyAsync(String filename, bool isWeb = false)
        {
            String keyName;
            if (isWeb)
            {
                keyName = await GetWebKey(filename).ConfigureAwait(false);
                if (keyName == null)
                    return null;
            }
            else
            {
                try
                {
                    var fi = new FileInfo(filename);
                    if (!fi.Exists)
                        return null;
                    keyName = String.Join('|', fi.FullName, fi.LastWriteTimeUtc, fi.Length);
                }
                catch
                {
                    return null;
                }
            }
            var keyHash = MD5.HashData(System.Runtime.InteropServices.MemoryMarshal.Cast<Char, Byte>(keyName.AsSpan()));
            keyName = HashTools.GetHashString16(keyHash);
            return keyName;
        }

        /// <summary>
        /// Get the location of the cache folder, create one if none exist
        /// </summary>
        public static String GetCacheFolder(String filename)
            => Folders.SelectFolder(C.P, filename);

        #endregion//Helpers

        #region Internal

        sealed class CleanUp
        {
            public readonly String[] P = Folders.FromConfig("FileHashFolders", Folders.AllSharedFolders, "FileHash");

            public CleanUp()
            {
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            }

            void CurrentDomain_ProcessExit(object sender, EventArgs e)
            {
                try
                {
                    var killOlderThan = DateTime.UtcNow.AddDays(-30);
                    foreach (var folder in P)
                    foreach (var x in Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new FileInfo(x);
                            if (fi.LastAccessTimeUtc < killOlderThan)
                                fi.Delete();
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }

        static readonly CleanUp C = new CleanUp();


        #endregion//Internal
    }


}


