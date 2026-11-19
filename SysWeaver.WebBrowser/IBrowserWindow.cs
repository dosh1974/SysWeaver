using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// A browser window (a page) that can load an url, run scripts and capture screenshots
    /// </summary>
    public interface IBrowserWindow : IDisposable
    {

        /// <summary>
        /// The url that was last loaded
        /// </summary>
        String Url { get; }

        /// <summary>
        /// The width of the window (viewport) in pixels
        /// </summary>
        int Width { get; }
        /// <summary>
        /// The height of the window (viewport) in pixels
        /// </summary>
        int Height { get; }


        /// <summary>
        /// Resize the window
        /// </summary>
        /// <param name="width">The new width in pixels</param>
        /// <param name="height">The new height in pixels</param>
        /// <param name="scale">The scale (dpi scaling), some browsers can only change it before a page is loaded</param>
        /// <returns>True if the window was resized</returns>
        Task<bool> Resize(int width, int height, double scale = 1.0);

        /// <summary>
        /// Load an url
        /// </summary>
        /// <param name="url">The url to load</param>
        /// <param name="throwOnError">True to throw if the url can't be loaded</param>
        /// <returns>A task that completes when the page has loaded</returns>
        Task LoadUrl(String url, bool throwOnError = true);

        /// <summary>
        /// Wait until the page is fully loaded (typically until there is no more network activity)
        /// </summary>
        /// <param name="throwOnError">True to throw if waiting fails</param>
        /// <returns>True if the page is loaded, false if waiting failed (and throwOnError is false)</returns>
        Task<bool> WaitLoaded(bool throwOnError = true);

        /// <summary>
        /// Wait until no network requests (from any frame, including cross origin frames such as an embedded map) have been
        /// pending for <paramref name="idleMs"/> ms. Unlike <see cref="WaitLoaded"/> this can be called at any time, ex: after the
        /// page has started loading content dynamically (map tiles, images etc).
        /// Requests that stay open for a long time (streaming, long polling) are ignored after a while.
        /// The default implementation (a browser that can't track requests) just waits for <paramref name="idleMs"/> ms and returns false.
        /// </summary>
        /// <param name="idleMs">The time in ms without any network activity that is required</param>
        /// <param name="timeoutMs">The maximum time in ms to wait</param>
        /// <returns>True if the network went idle, false on a time out (or if the browser can't track requests)</returns>
        async Task<bool> WaitNetworkIdle(int idleMs = 500, int timeoutMs = 10000)
        {
            await Task.Delay(Math.Clamp(idleMs, 0, Math.Max(0, timeoutMs))).ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Run some javascript in the page
        /// </summary>
        /// <param name="js">The javascript</param>
        /// <returns>The result of the script (or the error)</returns>
        Task<BrowserJsResponse> RunJs(String js);
        
        /// <summary>
        /// Capture the window as a png image
        /// </summary>
        /// <param name="optimizeForSpeed">True to optimize for speed rather than quality (ignored by some browsers)</param>
        /// <returns>The png data</returns>
        Task<Byte[]> CapturePng(bool optimizeForSpeed = false);

        /// <summary>
        /// Capture the window as a jpeg image
        /// </summary>
        /// <param name="quality">The jpeg quality, 0 to 100</param>
        /// <param name="optimizeForSpeed">True to optimize for speed rather than quality (ignored by some browsers)</param>
        /// <returns>The jpeg data</returns>
        Task<Byte[]> CaptureJpeg(int quality = 70, bool optimizeForSpeed = false);

        /// <summary>
        /// Make an object available to the javascript of the page (its methods can be called from the page)
        /// </summary>
        /// <param name="name">The name that the object gets in the page</param>
        /// <param name="obj">The object</param>
        /// <returns>True if the object was added, false if an object with that name already exists</returns>
        Task<bool> AddJsObject(String name, Object obj);

        /// <summary>
        /// Remove an object that was added using <see cref="AddJsObject"/>
        /// </summary>
        /// <param name="name">The name of the object</param>
        /// <returns>True if the object was removed, false if there is no such object</returns>
        Task<bool> RemoveJsObject(String name);

    }


}
