using System;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    public interface IBrowserWindow : IDisposable
    {

        String Url { get; }

        int Width { get; }
        int Height { get; }


        Task<bool> Resize(int width, int height, double scale = 1.0);

        Task LoadUrl(String url, bool throwOnError = true);

        Task<bool> WaitLoaded(bool throwOnError = true);
        
        Task<BrowserJsResponse> RunJs(String js);
        
        Task<Byte[]> CapturePng(bool optimizeForSpeed = false);

        Task<Byte[]> CaptureJpeg(int quality = 70, bool optimizeForSpeed = false);

        Task<bool> AddJsObject(String name, Object obj);

        Task<bool> RemoveJsObject(String name);

    }


}
