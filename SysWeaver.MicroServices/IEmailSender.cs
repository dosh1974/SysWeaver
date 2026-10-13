using System;
using System.Threading;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public interface IEmailSender : IDisposable
    {
        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="message"></param>
        /// <param name="isHtml"></param>
        /// <returns></returns>
        Task Send(String to, String subject, String message, bool isHtml = false);

    }




}
