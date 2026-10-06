using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// Optional service that lets the user storage use a data retention policy per user (e.g. depending on subscription level).
    /// </summary>
    public interface IUserStoragePerUserHandler
    {
        /// <summary>
        /// Get the user storage retention for the given user.
        /// </summary>
        /// <param name="userGuid">The guid (identifier) of the user.</param>
        /// <returns>null to use default retention, else the settings for the specific user</returns>
        Task<UserStorageDataRetention> GetUserDataRetention(String userGuid);


    }


}
