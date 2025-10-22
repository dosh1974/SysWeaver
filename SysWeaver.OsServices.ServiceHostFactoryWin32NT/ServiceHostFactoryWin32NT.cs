namespace SysWeaver.OsServices
{
    public sealed class ServiceHostFactoryWin32NT : IServiceHostFactory
    {
        public IServiceHost Create(ServiceParams p) => new ServiceHostWindows(p);
    }
}
