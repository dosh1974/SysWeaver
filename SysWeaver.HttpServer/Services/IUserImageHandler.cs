using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Used when an IUsageIMageHandler isn't found or doesn't supply an image for a user.
    /// Order is:
    /// IUserImageHandler
    /// IDefaultUserImageHandler
    /// Internal default (based on nick).
    /// </summary>
    public interface IDefaultUserImageHandler
    {
        /// <summary>
        /// Get an image of a specific size
        /// </summary>
        /// <param name="userGuid">The hexa decimal user guid</param>
        /// <param name="size">The size (must be one from the Sizes property)</param>
        /// <returns>A request handler if it exist, else null</returns>
        ValueTask<IHttpRequestHandler> Get(String userGuid, int size);
    }



    public interface IUserImageHandler
    {
        /// <summary>
        /// The supported sizes in pixels (always square)
        /// </summary>
        int[] Sizes { get; }


        /// <summary>
        /// Get an image of a specific size
        /// </summary>
        /// <param name="userGuid">The hexa decimal user guid</param>
        /// <param name="size">The size (must be one from the Sizes property)</param>
        /// <returns>A request handler if it exist, else null</returns>
        ValueTask<IHttpRequestHandler> Get(String userGuid, int size);

        /// <summary>
        /// Delete all images for a specific user
        /// </summary>
        /// <param name="userGuid">The hexa decimal user guid</param>
        /// <returns>True if anything was deleted</returns>
        ValueTask<bool> Delete(String userGuid);


    }


}
