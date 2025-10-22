using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Interface for a user storage service.
    /// Can be used to store user owned files with support for:
    /// - Access scope (private, protected, public etc).
    /// - Old data is automatically pruned (files are deleted if the time since last accees exceeds the policy).
    /// - Disc quota is maintained (the files that would expire in the neareast future is deleted when exceeded).
    /// </summary>
    public interface IUserStorageService
    {

        /// <summary>
        /// Store some private data, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="filename">The desired filename and extension (no path)</param>
        /// <param name="data">The data to store</param>
        /// <param name="userDeletable">If true, the user can delete this file</param>
        /// <returns>The url to the file</returns>
        Task<string> StorePrivateFile(HttpServerRequest context, string filename, ReadOnlyMemory<byte> data, bool userDeletable = true);

        /// <summary>
        /// Store some public or protected data, accessible to anyone that have the specified auth
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="filename">The desired filename and extension (no path)</param>
        /// <param name="data">The data to store</param>
        /// <param name="requireAuthTokens">
        /// null or "-" = Anyone can access the data, even if they are not logged in.
        /// "" = Any logged in user can access the data.
        /// One or more tokens that is accepted to read the data.
        /// </param>
        /// <param name="userDeletable">If true, the user can delete this file</param>
        /// <returns>The url to the file</returns>
        Task<string> StorePublicFile(HttpServerRequest context, string filename, ReadOnlyMemory<byte> data, string requireAuthTokens = null, bool userDeletable = true);

        /// <summary>
        /// Store a link to something as private, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="url">The link (url) to store</param>
        /// <param name="storedFiles">The url's to any stored files that are required to display the link correctly, they should not be user deletable</param>
        /// <returns>The url that can be used to view the stored link</returns>
        Task<string> StorePrivateLink(HttpServerRequest context, string url, params String[] storedFiles);

        /// <summary>
        /// Store a link to something as public or protected, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="url">The link (url) to store</param>
        /// <param name="requireAuthTokens">
        /// null or "-" = Anyone can access the data, even if they are not logged in.
        /// "" = Any logged in user can access the data.
        /// One or more tokens that is accepted to read the data.
        /// </param>
        /// <param name="storedFiles">The url's to any stored files that are required to display the link correctly (must be stored with the same auth), they should not be user deletable</param>
        /// <returns>The url that can be used to view the stored link</returns>
        Task<string> StorePublicLink(HttpServerRequest context, string url, string requireAuthTokens, params String[] storedFiles);

        /// <summary>
        /// Read a file from the store
        /// </summary>
        /// <param name="context">The request context that triggered this read</param>
        /// <param name="url">Url to the file</param>
        /// <param name="markAsAccessed">If true the file is marked as accessed and expiration time is moved forward</param>
        /// <returns>The uncompressed content of the file</returns>
        Task<ReadOnlyMemory<Byte>?> ReadFile(HttpServerRequest context, string url, bool markAsAccessed = true);

        /// <summary>
        /// Get the scope of a link or a file
        /// </summary>
        /// <param name="context"></param>
        /// <param name="url">Url to the file or link</param>
        /// <returns>The scope</returns>
        Task<UserStorageScopes?> GetScope(HttpServerRequest context, string url);

        /// <summary>
        /// Get the URL prefix used by this storage
        /// </summary>
        /// <returns></returns>
        Task<String> GetBaseUrlPrefix();

        /// <summary>
        /// Get the user part of the path
        /// </summary>
        /// <param name="userGuid">The user guid</param>
        /// <returns></returns>
        String GetUserPath(String userGuid);


    }


    /// <summary>
    /// Interface that service can use to specially handle chat sotring of links.
    /// </summary>
    public interface IChatStoreLinkHandler
    {
        /// <summary>
        /// Called when the chat wants to store a link to something.
        /// Can override default behavior.
        /// </summary>
        /// <param name="us">The storeage to store to</param>
        /// <param name="url">The url to store (override based on this)</param>
        /// <param name="scope">The scope to store data under</param>
        /// <param name="context">The current request context</param>
        /// <returns>The store linked or null (if no special handling was performed)</returns>
        Task<String> HandleLink(IUserStorageService us, String url, UserStorageScopes scope, HttpServerRequest context);
    }

}
