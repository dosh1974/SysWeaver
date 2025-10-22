using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public interface IUserStoragePerUserHandler
    {
        /// <summary>
        /// Get the user storage retention for the given user
        /// </summary>
        /// <param name="userGuid">The hash of this user</param>
        /// <returns>null to use default retention, else the settings for the specific user</returns>
        Task<UserStorageDataRetention> GetUserDataRetention(String userGuid);


    }


}
