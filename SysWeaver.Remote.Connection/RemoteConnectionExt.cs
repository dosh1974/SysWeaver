using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver
{
    public static class RemoteConnectionExt
    {
        #region Login


        /// <summary>
        /// Login to a SysWeaver service
        /// </summary>
        /// <param name="connection">A connection to a SysWeaver service</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password (never sent in plaintext, nor sent using the same hash twice)</param>
        /// <param name="urlFix">An optional url part added to the remote connections base url (to get to the server root url)</param>
        /// <returns>User information if successful else null</returns>
        public static Task<SysWeaverHttpClientExt.AuthInfo> SysWeaverLogin(this IRemoteApi connection, String username, String password, String urlFix = null)
        {
            var b = connection as RemoteConnectionBase;
            if (b == null)
                return Task.FromResult<SysWeaverHttpClientExt.AuthInfo>(null);
            var client = b.Client;
            var urlbase = b.UrlBase;
            if (!String.IsNullOrEmpty(urlFix))
                urlbase += urlFix;
            return client.SysWeaverLogin(urlbase, username, password);
        }

        /// <summary>
        /// Log out the currentgly logged in user
        /// </summary>
        /// <param name="connection">A connection to a SysWeaver service</param>
        /// <param name="urlFix">An optional url part added to the remote connections base url (to get to the server root url)</param>
        /// <returns></returns>
        public static Task<bool> SysWeaverLogOut(this IRemoteApi connection, String urlFix = null)
        {
            var b = connection as RemoteConnectionBase;
            if (b == null)
                return TaskExt.FalseTask;
            var client = b.Client;
            var urlbase = b.UrlBase;
            if (!String.IsNullOrEmpty(urlFix))
                urlbase += urlFix;
            return client.SysWeaverLogout(urlbase);
        }


        #endregion//Login

        #region File upload



        /// <summary>
        /// Prepare file(s) for upload to a SysWeaver service (get information from the server about the files)
        /// </summary>
        /// <param name="connection">The remote connection</param>
        /// <param name="repo">The repository</param>
        /// <param name="urlFix">An optional url part added to the remote connections base url (to get to the server root url)</param>
        /// <param name="filenames">File(s) to prepare for upload</param>
        /// <returns>Information about the files</returns>
        public static Task<SysWeaverHttpClientExt.FileInfo[]> SysWeaverFileUploadPrepare(this IRemoteApi connection, String repo, String urlFix, params String[] filenames)
        {
            var b = connection as RemoteConnectionBase;
            if (b == null)
                return Task.FromResult<SysWeaverHttpClientExt.FileInfo[]>(null);
            var client = b.Client;
            var urlbase = b.UrlBase;
            if (!String.IsNullOrEmpty(urlFix))
                urlbase += urlFix;
            return client.SysWeaverFileUploadPrepare(urlbase, repo, filenames);
        }

        /// <summary>
        /// Upload the files previously prepared
        /// </summary>
        /// <param name="connection">The remote connection</param>
        /// <param name="repo">The repository</param>
        /// <param name="urlFix">An optional url part added to the remote connections base url (to get to the server root url)</param>
        /// <param name="info">The files as returned byFileUploadPrepare</param>
        /// <returns>True if the upload request(s) was performed and status was updated in the file info(s). True does NOT mean that the file(s) was uploaded succesfully, need to check status</returns>
        public static Task<bool> SysWeaverFileUploadPrepared(this IRemoteApi connection, String repo, String urlFix, SysWeaverHttpClientExt.FileInfo[] info)
        {
            var b = connection as RemoteConnectionBase;
            if (b == null)
                return TaskExt.FalseTask;
            var client = b.Client;
            var urlbase = b.UrlBase;
            if (!String.IsNullOrEmpty(urlFix))
                urlbase += urlFix;
            return client.SysWeaverFileUploadPrepared(urlbase, repo, info);
        }

        /// <summary>
        /// Upload one or more files to a SysWeaver service
        /// </summary>
        /// <param name="connection">The remote connection</param>
        /// <param name="repo">The repository</param>
        /// <param name="urlFix">An optional url part added to the remote connections base url (to get to the server root url)</param>
        /// <param name="filenames">File(s) to upload</param>
        /// <returns>An array of information about the files (and their status), or null if some fatal error happened</returns>
        public static Task<SysWeaverHttpClientExt.FileInfo[]> SysWeaverFileUpload(this IRemoteApi connection, String repo, String urlFix, params String[] filenames)
        {
            var b = connection as RemoteConnectionBase;
            if (b == null)
                return Task.FromResult<SysWeaverHttpClientExt.FileInfo[]>(null);
            var client = b.Client;
            var urlbase = b.UrlBase;
            if (!String.IsNullOrEmpty(urlFix))
                urlbase += urlFix;
            return client.SysWeaverFileUpload(urlbase, repo, filenames);
        }

        #endregion//File upload

    }


}


