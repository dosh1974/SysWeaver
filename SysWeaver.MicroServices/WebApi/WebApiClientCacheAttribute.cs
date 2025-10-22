using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on a type or method to specify the client cache duration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false)]
    public class WebApiClientCacheAttribute : Attribute
    {

        /// <summary>
        /// Put this attribute on a type or method to specify the client cache duration.
        /// </summary>
        /// <param name="duration">The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response).</param>
        public WebApiClientCacheAttribute(int duration)
        {
            Duration = duration > 0 ? duration : 0;
        }

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        public readonly int Duration;
    }



}
