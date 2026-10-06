using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Used when an <see cref="IUserImageHandler"/> isn't found or doesn't supply an image for a user.
    /// Order is:
    /// <see cref="IUserImageHandler"/>,
    /// <see cref="IDefaultUserImageHandler"/>,
    /// internal default (based on nick).
    /// </summary>
    public interface IDefaultUserImageHandler
    {
        /// <summary>
        /// Get an image of a specific size
        /// </summary>
        /// <param name="userGuid">The hexa decimal user guid</param>
        /// <param name="size">The requested size in pixels (one of the sizes supported by the active <see cref="IUserImageHandler"/>)</param>
        /// <returns>A request handler if it exist, else null</returns>
        Task<IHttpRequestHandler> Get(String userGuid, int size);
    }



    /// <summary>
    /// A service that supplies user images (avatars) in a set of fixed sizes, used by the auth manager to serve user images.
    /// </summary>
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
        Task<IHttpRequestHandler> Get(String userGuid, int size);

        /// <summary>
        /// Delete all images for a specific user
        /// </summary>
        /// <param name="userGuid">The hexa decimal user guid</param>
        /// <returns>True if anything was deleted</returns>
        Task<bool> Delete(String userGuid);


    }


}
