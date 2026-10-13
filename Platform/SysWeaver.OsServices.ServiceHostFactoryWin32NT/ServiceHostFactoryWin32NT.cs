namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IServiceHostFactory"/> for Windows (Win32NT), found by name by <see cref="ServiceHost.Run"/>.
    /// </summary>
    public sealed class ServiceHostFactoryWin32NT : IServiceHostFactory
    {
        /// <summary>
        /// Create a host that uses the Windows Service Control Manager.
        /// </summary>
        /// <param name="p">The service parameters.</param>
        /// <returns>The service host.</returns>
        public IServiceHost Create(ServiceParams p) => new ServiceHostWindows(p);
    }
}
