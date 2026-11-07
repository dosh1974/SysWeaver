using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.WebBrowser;


namespace SysWeaver.MicroService
{
    public sealed partial class ThumbnailWebService
    {


        [ClassInterface(ClassInterfaceType.AutoDual)]
        [ComVisible(true)]
        public sealed class AdaptiveSize : IDisposable
        {
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

            public bool Dead;

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
                //  A capture that is running when timing out is allowed to finish
                while (!await CsBlocker.WaitAsync(timeOut + ExtraDelay).ConfigureAwait(false))
                {
                    if (!IsCapturing)
                    {
                        M.AddMessage($"{Prefix}The page didn't take a screen shot in time", MessageLevels.Warning);
                        break;
                    }
                }
                Dead = true;
                return Data;
            }

    
            public double Duration;
            public double Fps;

            public double LoadTime;

            public String Error;

            volatile Byte[] Data;
            internal volatile bool JsIsInControl;

            public void Dispose()
            {
                Dead = true;
                JsBlocker?.Dispose();
                CsBlocker?.Dispose();

            }
        }


    }
}
