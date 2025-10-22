namespace SysWeaver.Net
{
    public enum UserStorageScopes
    {
        /// <summary>
        /// Private data, can not be seen by other users
        /// </summary>
        Private = 0,
        /// <summary>
        /// Protected data, can be seen by other user (given that they have a link to the data).
        /// Can require specific auth tokens to view.
        /// </summary>
        Protected,
        /// <summary>
        /// Public data, can be seen by anyone (given that they have a link to the data)
        /// </summary>
        Public
    }


}
