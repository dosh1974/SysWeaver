using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// A browser window backed by WebView2, gives access to the WebView2 objects
    /// </summary>
    public interface IWebViewBrowserWindow : IBrowserWindow
    {
        /// <summary>
        /// The WebView2 controller (must only be used on the UI thread, see <see cref="RunOnUiThread(Func{Task})"/>)
        /// </summary>
        CoreWebView2Controller Controller { get; }

        /// <summary>
        /// The WebView2 core (must only be used on the UI thread, see <see cref="RunOnUiThread(Func{Task})"/>)
        /// </summary>
        CoreWebView2 Core { get; }

        /// <summary>
        /// Run a task on the UI thread of the window
        /// </summary>
        /// <param name="task">The function to run</param>
        /// <returns>A task that completes when the function has completed</returns>
        Task RunOnUiThread(Func<Task> task);

        /// <summary>
        /// Run a task on the UI thread of the window
        /// </summary>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="task">The function to run</param>
        /// <returns>A task that completes with the result of the function</returns>
        Task<R> RunOnUiThread<R>(Func<Task<R>> task);
    }





}
