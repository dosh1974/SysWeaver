using System;
using System.Collections.Generic;
using System.IO;

namespace SysWeaver.Net
{
    public sealed class HttpRequestData : IDisposable
    {

        public static readonly HttpRequestData Empty = new ();

        public HttpRequestData()
        {
        }

        public bool IsEmpty => Stream == null && Mem.IsEmpty;


        public HttpRequestData(Stream stream)
        {
            Stream = stream;
            Mem = null;
            var d = new Stack<IDisposable>();
            d.Push(stream);
            Disp = d;
        }

        public HttpRequestData(ReadOnlyMemory<Byte> mem, IDisposable disposable = null)
        {
            Stream = null;
            Mem = mem;
            if (disposable == null)
                return;
            var d = new Stack<IDisposable>();
            d.Push(disposable);
            Disp = d;
        }

        public HttpRequestData(ReadOnlyMemory<Byte> mem, bool doNotCache, IDisposable disposable = null)
        {
            Stream = null;
            Mem = mem;
            IsMapped = doNotCache;
            if (disposable == null)
                return;
            var d = new Stack<IDisposable>();
            d.Push(disposable);
            Disp = d;
        }

        public HttpRequestData(IUnmanagedReadOnlyMemory<Byte> mem)
        {
            Stream = null;
            Mem = mem.Memory;
            var d = new Stack<IDisposable>();
            d.Push(mem);
            Disp = d;
            IsMapped = true;
        }

        public ReadOnlyMemory<Byte> GetMemory() => Mem;


        internal Stream Stream;
        internal ReadOnlyMemory<Byte> Mem;
        internal bool IsMapped;

        Stack<IDisposable> Disp;

        internal void ChangeStream(Stream stream)
        {
            var old = Stream;
            Stream = stream;
            Mem = null;
            IsMapped = false;
            if (stream == null)
                return;
            var d = Disp;
            if (d == null)
            {
                d = new Stack<IDisposable>();
                Disp = d;
            }
            d.Push(stream);
        }

        internal void ChangeMem(ReadOnlyMemory<Byte> mem)
        {
            Mem = mem;
            Stream = null;
            IsMapped = false;
        }

        public void Dispose()
        {
            var d = Disp;
            if (d == null)
                return;
            while (d.Count > 0)
                d.Pop().Dispose();
        }
    }
}
