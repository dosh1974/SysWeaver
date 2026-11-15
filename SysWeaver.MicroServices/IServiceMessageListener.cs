using System;
using System.Threading.Tasks;



namespace SysWeaver.MicroService
{

    /// <summary>
    /// Any instance registered to the service manager, will get message if this interface is implemented
    /// </summary>
    public interface IServiceMessageListener
    {
        /// <summary>
        /// Called when a message is posted to the service manager
        /// </summary>
        /// <param name="key">Message key</param>
        /// <param name="data">Message data</param>
        /// <returns>A task that completes when the message has been handled</returns>
        Task OnServiceMessage(String key, Object data);
    }

}
