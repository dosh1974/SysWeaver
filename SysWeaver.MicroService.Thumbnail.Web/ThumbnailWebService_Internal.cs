using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.WebBrowser;


namespace SysWeaver.MicroService
{
    public sealed partial class ThumbnailWebService
    {


        /// <summary>
        /// Exposed to the page as "ScreenShotHost": lets a page that is "aware" take control of a screenshot of itself.
        /// The page tells that it's supported (<see cref="isSupported"/>), may change the size of the window (<see cref="setSize"/>) and tells when it's ready to be captured (<see cref="doIt"/>).
        /// </summary>
        [ClassInterface(ClassInterfaceType.AutoDual)]
        [ComVisible(true)]
        public sealed class AdaptiveSize : IDisposable
        {
            /// <summary>
            /// Create the screenshot controller for one browser window
            /// </summary>
            /// <param name="m">Where messages are logged</param>
            /// <param name="prefix">The prefix of the log messages</param>
            /// <param name="format">The image format of the screenshot</param>
            /// <param name="quality">The jpeg quality (only used for jpeg)</param>
            /// <param name="optimizeForSpeed">True to optimize for speed rather than quality</param>
            /// <param name="extraDelay">An extra time to wait (in ms) before the screenshot is taken</param>
            /// <param name="scale">The scale (dpi scaling) of the window, used when the page doesn't supply one</param>
            public AdaptiveSize(IMessageHost m, String prefix, ScreenshotImageFormats format, int quality, bool optimizeForSpeed, int extraDelay, double scale = 1)
            {
                M = m;
                Prefix = prefix;
                Format = format;
                Quality = quality;
                OptimizeForSpeed = optimizeForSpeed;
                ExtraDelay = extraDelay;
                Scale = scale;
            }
            readonly double Scale;
            readonly ScreenshotImageFormats Format;
            readonly int Quality;
            readonly bool OptimizeForSpeed;

            readonly String Prefix;
            readonly IMessageHost M;
            internal IBrowserWindow Win;
            readonly SemaphoreSlim CsBlocker = new SemaphoreSlim(0, 1);
            readonly SemaphoreSlim JsBlocker = new SemaphoreSlim(0, 1);
            readonly int ExtraDelay;

            /// <summary>
            /// True when the screenshot is done (or failed, or this object was disposed), calls from the page are ignored after that
            /// </summary>
            public bool Dead;

            /// <summary>
            /// Called by the page to tell that it is "aware": it takes control of the size of the window and of when the screenshot is taken
            /// </summary>
            public void isSupported()
            {
                JsIsInControl = true;
                Supported.TrySetResult();
            }


            /// <summary>
            /// Completed when the page has told that it will take the screen shot, there is no need to wait for it to load after that
            /// </summary>
            internal Task WhenSupported => Supported.Task;

            readonly TaskCompletionSource Supported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            internal void AllowJsControl()
            {
                JsBlocker.Release();
            }


            /// <summary>
            /// Called by the page to change the size of the window
            /// </summary>
            /// <param name="width">The width in pixels</param>
            /// <param name="height">The height in pixels</param>
            /// <param name="scale">The scale, 0 (pages usually only supply the size) keeps the scale of the request</param>
            public async Task setSize(int width, int height, double scale = 0)
            {
                if (Dead)
                    return;
                var b = JsBlocker;
                await b.WaitAsync().ConfigureAwait(false);
                try
                {
                    M.AddMessage(MessageLevels.Debug, $"{Prefix}Page is changing size to {width}x{height}");
                    await Win.Resize(width, height, scale > 0 ? scale : Scale).ConfigureAwait(false);
                }
                finally
                {
                    b.Release();
                }

            }

            internal bool IsCapturing;

            /// <summary>
            /// The number of <see cref="waitNetworkIdle"/> calls in progress (the screenshot time out is extended while waiting)
            /// </summary>
            int NetWaits;

            /// <summary>
            /// Called by the page to wait until the network has been idle (no pending requests in any frame, including cross origin
            /// frames such as a Google map) for some time, ex: before calling <see cref="doIt"/> when the content is loaded dynamically.
            /// </summary>
            /// <param name="idleMs">The time in ms without any network activity that is required (50 to 5000)</param>
            /// <param name="timeoutMs">The maximum time in ms to wait (at most 20000)</param>
            /// <returns>True if the network went idle, false on a time out (or if the browser can't track requests)</returns>
            public async Task<bool> waitNetworkIdle(int idleMs = 500, int timeoutMs = 8000)
            {
                if (Dead)
                    return false;
                var w = Win;
                if (w == null)
                    return false;
                idleMs = Math.Clamp(idleMs, 50, 5000);
                timeoutMs = Math.Clamp(timeoutMs, idleMs, 20000);
                Interlocked.Increment(ref NetWaits);
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    var ok = await w.WaitNetworkIdle(idleMs, timeoutMs).ConfigureAwait(false);
                    M.AddMessage(MessageLevels.Debug, $"{Prefix}Waited {System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds:0} ms for the network to be idle: {(ok ? "idle" : "not idle")}");
                    return ok;
                }
                catch (Exception ex)
                {
                    M.AddMessage($"{Prefix}Failed to wait for the network to be idle", ex, MessageLevels.Warning);
                    return false;
                }
                finally
                {
                    Interlocked.Decrement(ref NetWaits);
                }
            }

            /// <summary>
            /// Called by the page when it's ready to be captured (or has failed): takes the screenshot (after the extra delay).
            /// </summary>
            /// <param name="duration">The duration of the media (or effect) in seconds</param>
            /// <param name="fps">The frame rate of the media</param>
            /// <param name="error">An error text if the page failed (no screenshot is taken then), else null</param>
            /// <param name="loadTime">The time the page spent loading, in seconds</param>
            /// <returns>A task that completes when the screenshot is taken</returns>
            public async Task doIt(double duration, double fps, String error, double loadTime)
            {
                if (Dead)
                    return;
                if (error != null)
                {
                    Error = error;
                    CsBlocker.Release();
                    return;
                }
                var b = JsBlocker;
                IsCapturing = true;
                await b.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (Dead)
                        return;
                    Dead = true;
                    if (ExtraDelay > 0)
                    {
                        M.AddMessage(MessageLevels.Debug, $"{Prefix}Waiting for {ExtraDelay} ms");
                        await Task.Delay(ExtraDelay).ConfigureAwait(false);
                    }
                    M.AddMessage(MessageLevels.Debug, $"{Prefix}Page is taking a screen shot");
                    switch (Format)
                    {
                        default:
                            Data = await Win.CapturePng(OptimizeForSpeed).ConfigureAwait(false);
                            break;
                        case ScreenshotImageFormats.Jpg:
                            Data = await Win.CaptureJpeg(Quality, OptimizeForSpeed).ConfigureAwait(false);
                            break;
                    }
                    Duration = duration;
                    Fps = fps;
                    LoadTime = loadTime;
                }
                catch (Exception ex)
                {
                    M.AddMessage($"{Prefix}Failed to take the screen shot requested by the page", ex, MessageLevels.Warning);
                }
                IsCapturing = false;
                b.Release();
                CsBlocker.Release();
            }

            internal async Task<Byte[]> WaitScreenShot(int timeOut = 10000)
            {
                //  A capture (or a wait for the network to be idle, it has a time out of it's own) that is running when timing out is allowed to finish
                while (!await CsBlocker.WaitAsync(timeOut + ExtraDelay).ConfigureAwait(false))
                {
                    if (!(IsCapturing || (Volatile.Read(ref NetWaits) > 0)))
                    {
                        M.AddMessage($"{Prefix}The page didn't take a screen shot in time", MessageLevels.Warning);
                        break;
                    }
                }
                Dead = true;
                return Data;
            }

    
            /// <summary>
            /// The duration of the media (or effect) in seconds, as reported by the page
            /// </summary>
            public double Duration;

            /// <summary>
            /// The frame rate of the media, as reported by the page
            /// </summary>
            public double Fps;

            /// <summary>
            /// The time the page spent loading, as reported by the page
            /// </summary>
            public double LoadTime;

            /// <summary>
            /// The error text reported by the page (null if none)
            /// </summary>
            public String Error;

            volatile Byte[] Data;
            internal volatile bool JsIsInControl;

            /// <inheritdoc />
            public void Dispose()
            {
                Dead = true;
                JsBlocker?.Dispose();
                CsBlocker?.Dispose();

            }
        }


    }
}
