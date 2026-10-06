namespace SysWeaver.OsServices
{
    /// <summary>
    /// Creates the OS specific <see cref="IServiceHost"/>.
    /// <see cref="ServiceHost.Run"/> looks for an implementation named "SysWeaver.OsServices.ServiceHostFactory[Platform]" (where Platform is <see cref="System.Environment.OSVersion"/>.Platform, ex: Win32NT or Unix),
    /// in loaded assemblies or in an assembly file with the same name next to the executable. Implementations must have a public parameterless constructor.
    /// </summary>
    public interface IServiceHostFactory
    {
        /// <summary>
        /// Create the service host for the current OS.
        /// </summary>
        /// <param name="p">The service parameters (name etc is already resolved).</param>
        /// <returns>The service host for this OS.</returns>
        IServiceHost Create(ServiceParams p);
    }

}
