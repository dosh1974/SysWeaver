using Nito.AsyncEx;
using System;
using System.Threading;
using System.Windows.Forms;

namespace SysWeaver.WebBrowser
{
    public class DedicatedThreadTaskProxy : TaskProxy
    {
        public override string ToString() => Name;

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

        public override void Dispose()
        {
            base.Dispose();
            T.Join();
        }


    }





}
