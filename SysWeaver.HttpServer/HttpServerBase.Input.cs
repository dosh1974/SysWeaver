using System;
using System.Collections.Generic;
using System.IO;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {

        /*
        sealed class Input : IDisposable
        {
            public Stream Stream;
            public ReadOnlyMemory<Byte>? Data;
            Stack<IDisposable> Disp;

            public Input(Stream stream)
            {
                Stream = stream;
            }

            public Input(ReadOnlyMemory<byte> data)
            {
                Data = data;
            }

            public void Dispose()
            {
                var s = Stream;
                if (s != null)
                    s.Dispose();
                var d = Disp;
                if (d == null)
                    return;
                while (d.Count > 0)
                    d.Pop().Dispose();
            }

            public void ChangeStream(Stream stream)
            {
                var old = Stream;
                Stream = stream;
                if (old == null)
                    return;
                var d = Disp;
                if (d == null)
                {
                    d = new Stack<IDisposable>();
                    Disp = d;
                }
                d.Push(old);
            }

        }

        */

    }

}
