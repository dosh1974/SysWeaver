
using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{

    /// <summary>
    /// A service that can open browser windows
    /// </summary>
    public interface IBrowserService
    {
        /// <summary>
        /// Open a new browser window (dispose it when done)
        /// </summary>
        /// <returns>The window</returns>
        Task<IBrowserWindow> OpenWindow();
    }


}
