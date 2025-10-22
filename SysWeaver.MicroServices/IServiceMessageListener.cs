using System;
using System.Threading.Tasks;



namespace SysWeaver.MicroService
{

    /// <summary>
    /// Any instance registered to the service manager, will get message if this interface is implemented
    /// </summary>
    public interface IServiceMessageListener
    {
        Task OnServiceMessage(String key, Object data);
    }


}
