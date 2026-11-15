using Nito.AsyncEx;
using System;
using System.Threading;
using System.Windows.Forms;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// A <see cref="TaskProxy"/> that runs all queued work on its own dedicated STA thread (with a message pump), as required by UI components like WebView2
    /// </summary>
    public class DedicatedThreadTaskProxy : TaskProxy
    {
        /// <inheritdoc />
        public override string ToString() => Name;

        /// <summary>
        /// Create the proxy and start its thread
        /// </summary>
        /// <param name="name">The name of the thread, null to use "Proxy #" followed by the thread id</param>
        public DedicatedThreadTaskProxy(String name = null)
        {
            Name = name;
            var t = new Thread(ThreadMain);
            T = t;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }


        String Name;
        readonly Thread T;

        void ThreadMain()
        {
            var ct = T;
            var n = Name ?? ("Proxy #" + ct.ManagedThreadId);
            Name = n;
            try
            {
                ct.Name = n;
            }
            catch
            {
            }
            using var ctx = new AsyncContext();
            SynchronizationContext.SetSynchronizationContext(ctx.SynchronizationContext);
            Action a = Application.DoEvents;
            for (; ;)
            {

                var d = SpinWait(a);
                for (; ; )
                {
                    var t = NextTask();
                    if (t == null)
                        break;
                    a();
                    if (t.IsCompleted)
                        continue;
                    ctx.Factory.Run(() => t);
                    var sw = new SpinWait();
                    while (!t.IsCompleted)
                    {
                        sw.SpinOnce();
                        a();
                    }
                }
                if (d)
                    break;
            }
        }

        /// <summary>
        /// Stop accepting work and wait for the thread to exit
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            T.Join();
        }


    }





}
