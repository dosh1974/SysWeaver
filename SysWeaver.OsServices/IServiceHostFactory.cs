namespace SysWeaver.OsServices
{
    public interface IServiceHostFactory
    {
        IServiceHost Create(ServiceParams p);
    }

}
