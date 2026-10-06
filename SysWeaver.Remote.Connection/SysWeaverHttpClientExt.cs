using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Extensions to HttpClient to perform some actions against SysWeaver services: login / logout (session cookie based) and file upload.
    /// </summary>
    /// <remarks>
    /// The client must keep cookies (the default <see cref="HttpClientHandler"/> does) for the login session to be used by later requests.
    /// </remarks>
    public static class SysWeaverHttpClientExt
    {
        #region Login

        /// <summary>
        /// SysWeaver auth information
        /// </summary>
        public sealed class AuthInfo
        {
            /// <summary>
            /// True if a user is logged in to the session
            /// </summary>
            public bool Succeeded { get; set; }

            /// <summary>
            /// The language code of the user / session
            /// </summary>
            public String Language { get; set; }

            /// <summary>
            /// Guid (can be used for user image etc)
            /// </summary>
            public String Guid { get; set; }

            /// <summary>
            /// The unique account name of the user
            /// </summary>
            public String Username { get; set; }


            /// <summary>
            /// The non-unique customizable nick name of the user (defaults to username)
            /// </summary>
            public String NickName { get; set; }

            /// <summary>
            /// The domain of the user (the domain meaning is application specific)
            /// </summary>
            public String Domain { get; set; }

            /// <summary>
            /// The security tokens that this user have
            /// </summary>
            public String[] Tokens { get; set; }
        }

        /// <summary>
        /// The login request payload sent to "Api/auth/Login".
        /// </summary>
        sealed class LoginReq
        {
            /// <summary>
            /// Process: 
            ///     temp = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, userSalt))))
            ///     Hash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', temp, OneTimePad))))
            /// </summary>
            public String Hash { get; set; }

            /// <summary>
            /// The one time pad used (from GetUserSalt)
            /// </summary>
            public String OneTimePad { get; set; }
        }


        /// <summary>
        /// Login to a SysWeaver service
        /// </summary>
        /// <param name="client">The http client to login</param>
        /// <param name="server">The base address to a SysWeaver service</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password (never sent in plaintext, nor sent using the same hash twice) or password hash.
        /// A 44 character base64 string that decodes to 32 bytes is treated as the already salted password hash.</param>
        /// <returns>User information, check <see cref="AuthInfo.Succeeded"/></returns>
        /// <remarks>
        /// Requests a salt and one time pad from "Api/auth/GetUserSalt" and then posts the double SHA256 hash to "Api/auth/Login".
        /// </remarks>
        /// <exception cref="Exception">A request failed (non 200 response).</exception>
        public async static Task<AuthInfo> SysWeaverLogin(this HttpClient client, String server, String username, String password)
        {
            String hash = null;
            if (password.Length == 44)
            {
                Span<Byte> d = stackalloc byte[32];
                if (Convert.TryFromBase64String(password, d, out var w))
                {
                    if (w == 32)
                        hash = password;
                }
            }
            var urlbase = server.TrimEnd('/') + '/';
            var data = await client.PostJsonRequest<String, String[]>(urlbase + "Api/auth/GetUserSalt", username).ConfigureAwait(false);
            var salt = data[0];
            var oneTimePad = data[1];
            hash = hash ?? Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, salt))));
            hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', hash, oneTimePad))));
            var r = new LoginReq
            {
                Hash = hash,
                OneTimePad = oneTimePad,
            };
            return await client.PostJsonRequest<LoginReq, AuthInfo>(urlbase + "Api/auth/Login", r).ConfigureAwait(false);
        }

        /// <summary>
        /// Log out the currently logged in user (GET "logout").
        /// </summary>
        /// <param name="client">The http client to log out</param>
        /// <param name="server">The base address to a SysWeaver service</param>
        /// <returns>Always true (the response status is ignored).</returns>
        public async static Task<bool> SysWeaverLogout(this HttpClient client, String server)
        {
            var urlbase = server.TrimEnd('/') + '/';
            await client.GetAsync(urlbase + "logout").ConfigureAwait(false);
            return true;
        }


        #endregion//Login

        #region File upload


        /// <summary>
        /// Information about a file 
        /// </summary>
        public sealed class FileInfo
        {
            /// <summary>
            /// Returns the file name and the status.
            /// </summary>
            /// <returns>Ex: "file.txt [Upload]".</returns>
            public override string ToString() => String.Concat(Name.ToFilename(), " [", Status, ']');

            /// <summary>
            /// Name of the file (only filename and extension, no path)
            /// </summary>
            public String Name;
            /// <summary>
            /// Length in bytes of the file data
            /// </summary>
            public long Length;
            /// <summary>
            /// The hash of the file content (as computed by <see cref="FileHash.GetHashAsync(string)"/>), encoded using lower case hex values, "abcd...", where a = is the high nibble of the first byte, b is the low nibble of the first byte and so on...
            /// </summary>
            public String Hash;
            /// <summary>
            /// Number of milliseconds since the Unix epoch (UTC) of the last modification
            /// </summary>
            public double LastModified;

            /// <summary>
            /// Set the LastModified field from a an UTC DateTime time.
            /// </summary>
            /// <param name="utcLastModified">The last modified time in UTC</param>
            public void SetLastModified(DateTime utcLastModified)
            {
                LastModified = new DateTimeOffset(utcLastModified).ToUnixTimeMilliseconds();
            }

            /// <summary>
            /// Get the status reported by the server (from <see cref="SysWeaverFileUploadPrepare"/>).
            /// </summary>
            /// <returns>The status.</returns>
            public FileStatus GetStatus() => Status;

            internal FileStatus Status;

            internal String FullFileName;

        }

        /// <summary>
        /// The status for a file that is about to be uploaded to a server
        /// </summary>
        public enum FileStatus
        {
            /// <summary>
            /// Can't upload this file since the disc quota will be exceeded
            /// </summary>
            DiscQuotaExceeded = -7,
            /// <summary>
            /// Not authorized to upload this file
            /// </summary>
            NotAuthorized = -6,
            /// <summary>
            /// Some parameters are wrong
            /// </summary>
            InvalidParams = -5,
            /// <summary>
            /// The file extension is not accepted
            /// </summary>
            RefuseExtension = -4,
            /// <summary>
            /// The file is refused due to it's size (typically too big)
            /// </summary>
            RefuseSize = -3,
            /// <summary>
            /// File is refused for some other reason
            /// </summary>
            Refuse = -2,
            /// <summary>
            /// The upload failed (might retry)
            /// </summary>
            UploadFailed = -1,
            /// <summary>
            /// No status found
            /// </summary>
            None = 0,
            /// <summary>
            /// The file already exist and the checksum etc matches so no need to upload again
            /// </summary>
            AlreadyUploaded = 1,
            /// <summary>
            /// The file is ok to be uploaded
            /// </summary>
            Upload = 2,
        }



        /// <summary>
        /// Check upload status for one or more files
        /// </summary>
        sealed class FileUploadRequest
        {
            /// <summary>
            /// The name of the repository to upload to
            /// </summary>
            public String Repo;
            /// <summary>
            /// File information
            /// </summary>
            public FileInfo[] Files;
        }


        /// <summary>
        /// Prepare file(s) for upload to a SysWeaver service (get information from the server about the files)
        /// </summary>
        /// <param name="client">The http client to use</param>
        /// <param name="server">The base address to a SysWeaver service</param>
        /// <param name="repo">The repository</param>
        /// <param name="filenames">File(s) to prepare for upload</param>
        /// <returns>Information about the files (with the server status), or null if no files were specified or the server response was invalid</returns>
        /// <exception cref="Exception">A file doesn't exist, or the request failed.</exception>
        public async static Task<FileInfo[]> SysWeaverFileUploadPrepare(this HttpClient client, String server, String repo, params String[] filenames)
        {
            var urlbase = server.TrimEnd('/') + '/';
            var count = filenames.Length;
            if (count <= 0)
                return null;
            var fi = new FileInfo[count];
            for (int i = 0; i < count; ++i)
            {
                var di = new System.IO.FileInfo(filenames[i]);
                var fn = di.FullName;
                if (!di.Exists)
                    throw new Exception("File " + fn.ToFilename() + " doesn't exist!");
                var hashStr = await FileHash.GetHashAsync(fn).ConfigureAwait(false);
                var hash = HashTools.GetHashFromString26(hashStr);
                var hexhHash = hash.ToHex();
                var f = new FileInfo
                {
                    FullFileName = fn,
                    Name = Path.GetFileName(fn),
                    Hash = hexhHash,
                    Length = di.Length,
                };
                f.SetLastModified(di.LastWriteTimeUtc);
                fi[i] = f;
            }
            var r = new FileUploadRequest
            {
                Repo = repo,
                Files = fi,
            };
            var res = await client.PostJsonRequest<FileUploadRequest, FileStatus[]>(urlbase + "upload/CheckStatus", r).ConfigureAwait(false);
            if (res == null)
                return null;
            if (res.Length != count)
                return null;
            for (int i = 0; i < count; ++i)
                fi[i].Status = res[i];
            return fi;
        }

        /// <summary>
        /// Upload the files previously prepared
        /// </summary>
        /// <param name="client">The http client to use</param>
        /// <param name="server">The base address to a SysWeaver service</param>
        /// <param name="repo">The repository</param>
        /// <param name="info">The files as returned by <see cref="SysWeaverFileUploadPrepare"/>, only files with the status <see cref="FileStatus.Upload"/> are uploaded (concurrently)</param>
        /// <returns>True if the upload request(s) were performed, false if <paramref name="info"/> is empty. True does NOT mean that the file(s) were uploaded successfully,
        /// the per file upload result is written back to the file info(s) (see <see cref="FileInfo.GetStatus"/>).</returns>
        public async static Task<bool> SysWeaverFileUploadPrepared(this HttpClient client, String server, String repo, FileInfo[] info)
        {
            var urlbase = server.TrimEnd('/') + '/';
            var count = info.Length;
            if (count <= 0)
                return false;
            urlbase = urlbase + "upload/Upload?repo=" + repo;
            await info.Where(f => f.Status == FileStatus.Upload).ProcessAsync(async f => f.Status = await UploadOne(client, urlbase, f).ConfigureAwait(false)).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Upload one or more files to a SysWeaver service
        /// </summary>
        /// <param name="client">The http client to use</param>
        /// <param name="server">The base address to a SysWeaver service</param>
        /// <param name="repo">The repository</param>
        /// <param name="filenames">File(s) to upload</param>
        /// <returns>An array of information about the files (and their status), or null if some fatal error happened</returns>
        public async static Task<FileInfo[]> SysWeaverFileUpload(this HttpClient client, String server, String repo, params String[] filenames)
        {
            var info = await SysWeaverFileUploadPrepare(client, server, repo, filenames).ConfigureAwait(false);
            if (info == null)
                return null;
            bool needUpload = false;
            foreach (var x in info)
            {
                needUpload = x.Status == FileStatus.Upload;
                if (needUpload)
                    break;
            }
            if (!needUpload)
                return info;
            if (!await SysWeaverFileUploadPrepared(client, server, repo, info).ConfigureAwait(false))
                return null;
            return info;
        }

        static readonly MediaTypeHeaderValue Mt = new MediaTypeHeaderValue("application/octet-stream");

        static async Task<FileStatus> UploadOne(HttpClient client, String baseUrl, FileInfo fi)
        {
            try
            {
                using var fs = new FileStream(fi.FullFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 32768, true);
                var s = new StreamContent(fs);
                s.Headers.ContentLength = fi.Length;
                s.Headers.ContentType = Mt;
                baseUrl = String.Concat(baseUrl, "&name=", fi.Name, "&length=" + fi.Length + "&hash=" + fi.Hash + "&time=" + fi.LastModified);
                using var res = await client.PostAsync(baseUrl, s).ConfigureAwait(false);
                if (res.StatusCode != System.Net.HttpStatusCode.OK)
                    return FileStatus.UploadFailed;
                var val = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Enum.TryParse<FileStatus>(val, out var r) ? r : FileStatus.UploadFailed;
            }
            catch
            {
                return FileStatus.UploadFailed;
            }
        }


        #endregion//File upload


    }


}


