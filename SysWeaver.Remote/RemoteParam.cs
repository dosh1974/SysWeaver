namespace SysWeaver.Remote
{
    /// <summary>
    /// Some constants to make API definition easier
    /// </summary>
    public static class RemoteParam
    {
        /// <summary>
        /// Json serializer constant
        /// </summary>
        public const string JsonSerializer = "json";
        
        /// <summary>
        /// Serializer used for POST and PUT that encodes using the x-www-form-urlencoded format, this can only be used for POST serialization
        /// </summary>
        public const string FormUrlSerializer = "formUrl";

        /// <summary>
        /// Serializer used for POST and PUT that encodes using the x-www-form-urlencoded format, all members that has the default value (of their type) is excluded, this can only be used for POST serialization
        /// </summary>
        public const string FormUrlIgnoreDefaultsSerializer = "formUrlIgnoreDefaults";
        

    }

}
