namespace SysWeaver
{
    /// <summary>
    /// Any object registered to the service manager will have the Pause and Continue called when a service is paused
    /// </summary>
    public interface IServicePausable
    {
        /// <summary>
        /// Called whenever a service is paused
        /// </summary>
        void Pause();

        /// <summary>
        /// Called whenever a service is resumed
        /// </summary>
        void Continue();
    }

}
