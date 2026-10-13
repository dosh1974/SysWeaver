using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    public interface IWebViewBrowserWindow : IBrowserWindow
    {
        CoreWebView2Controller Controller { get; }
        CoreWebView2 Core { get; }
        Task RunOnUiThread(Func<Task> task);
        Task<R> RunOnUiThread<R>(Func<Task<R>> task);
    }





}
