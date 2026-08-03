using System;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Use this to specify a default instance name for a service object
    /// </summary>
    public interface IHaveDefaultInstanceName
    {
        /// <summary>
        /// If no instance name is supplied when creating the service object, the name is collected from the instance
        /// </summary>
        String DefaultInstanceName { get; }
    }

}
