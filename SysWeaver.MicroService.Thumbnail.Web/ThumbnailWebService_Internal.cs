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
            public AdaptiveSize(IMessageHost m, String prefix)
            {
                M = m;
                Prefix = prefix;
            }
            readonly String Prefix;
            readonly IMessageHost M;
            internal IBrowserWindow Win;
            readonly SemaphoreSlim CsBlocker = new SemaphoreSlim(0, 1);
            readonly SemaphoreSlim JsBlocker = new SemaphoreSlim(0, 1);


            public bool Dead;

            public void isSupported()
            {
                JsIsInControl = true;
            }


            internal void AllowJsControl()
            {
                JsBlocker.Release();
            }


            public async Task setSize(int width, int height)
            {
                if (Dead)
                    return;
                var b = JsBlocker;
                await b.WaitAsync().ConfigureAwait(false);
                try
                {
                    M.AddMessage(Prefix + "Page is changing size to " + width + "x" + height, MessageLevels.Debug);
                    await Win.Resize(width, height).ConfigureAwait(false);
                }
                finally
                {
                    b.Release();
                }

            }

            internal bool IsCapturing;

            public async Task doIt(double duration, double fps, String error)
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
                    M.AddMessage(Prefix + "Page is taking a screen shot", MessageLevels.Debug);
                    Data = await Win.CapturePng().ConfigureAwait(false);
                    Duration = duration;
                    Fps = fps;
                }
                catch
                {
                }
                b.Release();
                IsCapturing = false;
            }

            internal async Task<Byte[]> WaitScreenShot(int timeOut = 10000)
            {
                for (; ; )
                {

                    try
                    {
                        await CsBlocker.WaitAsync(timeOut).ConfigureAwait(false);
                    }
                    catch //(Exception ex)
                    {
                        if (IsCapturing)
                            continue;
                    }
                    break;
                }
                Dead = true;
                return Data;
            }

    
            public double Duration;
            public double Fps;


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
