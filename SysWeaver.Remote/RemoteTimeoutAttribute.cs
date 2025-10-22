using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Override the time-out for a specific API call (or calls if placed on an interface)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class RemoteTimeoutAttribute : Attribute
    {
        /// <summary>
        /// The time-out to use in millie seconds, if less or equal than zero, the time-out in the RemoteConnection settings is used
        /// </summary>
        public readonly int TimeOutInMilliSeconds;

        /// <summary>
        /// Override the time-out for this API call (or calls if placed on an interface)
        /// </summary>
        /// <param name="timeOutInMilliSeconds">The time-out to use in millie seconds, if less or equal than zero, the time-out in the RemoteConnection is used</param>
        public RemoteTimeoutAttribute(int timeOutInMilliSeconds)
        {
            TimeOutInMilliSeconds = timeOutInMilliSeconds;
        }
    }




    /// <summary>
    /// Override the cache behaviour for a specific API call (or calls if placed on an interface)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class RemoteCacheAttribute : Attribute
    {
        /// <summary>
        /// Value to use to disable cache behaviour
        /// </summary>
        public const int Disabled = 0;

        /// <summary>
        /// Value to use to if the values passed in the remote connection should be used
        /// </summary>
        public const int UseConnection = -1;


        /// <summary>
        /// Number of seconds that the response can be resued, if 0 or less no time based caching is performed
        /// </summary>
        public readonly int Duration;

        /// <summary>
        /// Maximum number of cached responses, if 0 or less there is no maximum
        /// </summary>
        public readonly int MaxItems;


        /// <summary>
        /// Override the cache behaviour for this specific API call (or calls if placed on an interface)
        /// </summary>
        /// <param name="duration">Number of seconds that the response can be resued, if 0 or less no time based caching is performed</param>
        /// <param name="maxItems">Maximum number of cached responses, if 0 or less there is no maximum</param>
        public RemoteCacheAttribute(int duration = 0, int maxItems = 0)
        {
            Duration = Math.Max(UseConnection, duration);
            MaxItems = Math.Max(UseConnection, maxItems);
        }
    }


    /// <summary>
    /// Disables any caching for a specific API call (or calls if placed on an interface)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class NoRemoteCacheAttribute : RemoteCacheAttribute
    {
        /// <summary>
        /// Disables any caching for this specific API call (or calls if placed on an interface)
        /// </summary>
        public NoRemoteCacheAttribute() : base(0,0)
        {
        }
   
    }



    /// <summary>
    /// Use an object instead of an array for methods with more than one parameter.
    /// Params names are mapped to object fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class ParamAsObjectAttribute : Attribute
    {
        /// <summary>
        /// Use an object instead of an array for methods with more than one parameter.
        /// Params names are mapped to object fields.
        /// </summary>
        public ParamAsObjectAttribute()
        {
        }

    }



}
