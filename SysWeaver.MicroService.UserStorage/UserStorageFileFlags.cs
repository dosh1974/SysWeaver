using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A maximum of 4 bits can be used!
    /// </summary>
    [Flags]
    enum UserStorageFileFlags
    {
        /// <summary>
        /// If true, the user can delete this file manually
        /// </summary>
        UserDeletable = 1,
    }


}
