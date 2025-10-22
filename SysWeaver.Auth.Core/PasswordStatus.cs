namespace SysWeaver.Auth
{
    /// <summary>
    /// Represents the status of a password
    /// </summary>
    public enum PasswordStatus
    {
        /// <summary>
        /// Password is in some unkown state (error)
        /// </summary>
        UnknownError,
        /// <summary>
        /// Password is ok, fulfiiling the policy
        /// </summary>
        Ok,
        /// <summary>
        /// Password is too short
        /// </summary>
        TooShort,
        /// <summary>
        /// Password is too long
        /// </summary>
        TooLong,
        /// <summary>
        /// Password need a letter
        /// </summary>
        NeedLetter,
        /// <summary>
        /// Password need an uppercase letter
        /// </summary>
        NeedUpperCase,
        /// <summary>
        /// Password need a lowercase letter
        /// </summary>
        NeedLowerCase,
        /// <summary>
        /// Password need a number
        /// </summary>
        NeedNumber,
        /// <summary>
        /// Password need a special character (non numeric and non letter)
        /// </summary>
        NeedSpecial,
    }

}
