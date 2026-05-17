using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SysWeaver.Compression;

namespace SysWeaver
{
    public static class AsmResExt
    {

        /// <summary>
        /// Given an uncompressed resource name, find the compressed version (if any) and modify to the true resource name
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if a compressed version is found it's modified to that resource</param>
        /// <returns>The compression type or null</returns>
        public static ICompType FindResource(this Assembly asm, ref String uncompressedName)
        {
            var allRes = asm.GetManifestResourceNames();
            foreach (var t in allRes)
            {
                if (!t.StartsWith(uncompressedName, StringComparison.Ordinal))
                    continue;
                if (t.FastEquals(uncompressedName))
                    return null;
                var comp = CompManager.GetFromExt(t.Substring(uncompressedName.Length + 1));
                if (comp == null)
                    continue;
                uncompressedName = t;
                return comp;
            }
            String prefix = null;
            foreach (var t in allRes)
            {
                var k = t.FastIndexOf(".data.");
                if (k >= 0)
                {
                    prefix = t.Substring(0, k + 6);
                    break;
                }
            }
            if (prefix != null)
            {
                uncompressedName = prefix + uncompressedName;
                foreach (var t in allRes)
                {
                    if (!t.StartsWith(uncompressedName, StringComparison.Ordinal))
                        continue;
                    if (t.FastEquals(uncompressedName))
                        return null;
                    var comp = CompManager.GetFromExt(t.Substring(uncompressedName.Length + 1));
                    if (comp == null)
                        continue;
                    uncompressedName = t;
                    return comp;
                }
            }
            uncompressedName = null;
            return null;
        }


        /// <summary>
        /// Determine the compression method used in a resource based on it's extension
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="compressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The compression type or null</returns>
        public static ICompType GetResourceCompression(this Assembly asm, ref String compressedName)
        {
            var f = compressedName.LastIndexOf('.');
            if (f < 0)
                return null;
            var comp = CompManager.GetFromExt(compressedName.Substring(f + 1));
            if (comp != null)
                compressedName = compressedName.Substring(0, f);
            return comp;
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="compressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static unsafe ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, ref String compressedName)
        {
            var o = compressedName;
            var comp = GetResourceCompression(asm, ref compressedName);
            using var s = asm.GetManifestResourceStream(o);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            if (comp == null)
            {
                using var ms = new MemoryStream((int)s.Length);
                s.CopyTo(ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }else
            {
                using var ms = new MemoryStream((int)s.Length * 4);
                comp.Decompress(s, ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }


        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static unsafe ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, String uncompressedName)
        {
            var comp = FindResource(asm, ref uncompressedName);
            using var s = asm.GetManifestResourceStream(uncompressedName);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            if (comp == null)
            {
                using var ms = new MemoryStream((int)s.Length);
                s.CopyTo(ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
            else
            {
                using var ms = new MemoryStream((int)s.Length * 4);
                comp.Decompress(s, ms);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get the data of an embedded resource
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The name of the resource</param>
        /// <returns>The data of the resource</returns>
        public static unsafe ReadOnlyMemory<Byte> GetResourceData(this Assembly asm, String name)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s is UnmanagedMemoryStream x)
                return new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory;
            using var ms = new MemoryStream((int)s.Length);
            s.CopyTo(ms);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Get the data of an embedded resource as a byte array
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The name of the resource</param>
        /// <returns>The data of the resource</returns>
        public static unsafe Byte[] GetResourceDataBytes(this Assembly asm, String name)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s is UnmanagedMemoryStream x)
            {
                var sl = checked((int)x.Length);
                var ret = GC.AllocateUninitializedArray<Byte>(sl);
                new ReadOnlySpan<byte>(x.PositionPointer, sl).CopyTo(ret.AsSpan());
                return ret;
            }
            using var ms = new MemoryStream((int)s.Length);
            s.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static unsafe ReadOnlyMemory<Byte> GetUncompressedResourceData(this Type asmType, String uncompressedName)
        {
            var asm = asmType.Assembly;
            var t = uncompressedName;
            var comp = FindResource(asm, ref t);
            if (t == null)
            {
                t = String.Concat(asmType.Namespace, '.', uncompressedName);
                comp = FindResource(asm, ref t);
            }
            using var s = asm.GetManifestResourceStream(t);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            using var ms = new MemoryStream((int)s.Length);
            if (comp == null)
                s.CopyTo(ms);
            else
                comp.Decompress(s, ms);
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        public static unsafe Byte[] GetUncompressedResourceDataBytes(this Type asmType, String uncompressedName)
        {
            var asm = asmType.Assembly;
            var t = uncompressedName;
            var comp = FindResource(asm, ref t);
            if (t == null)
            {
                t = String.Concat(asmType.Namespace, '.', uncompressedName);
                comp = FindResource(asm, ref t);
            }
            using var s = asm.GetManifestResourceStream(t);
            if (s is UnmanagedMemoryStream x)
            {
                var sl = checked((int)x.Length);
                if (comp == null)
                {
                    var ret = GC.AllocateUninitializedArray<Byte>(sl);
                    new ReadOnlySpan<byte>(x.PositionPointer, sl).CopyTo(ret.AsSpan());
                    return ret;
                }
                using (var cms = new MemoryStream(sl))
                {
                    comp.Decompress(new ReadOnlySpan<byte>(x.PositionPointer, sl), cms);
                    return cms.ToArray();
                }
            }
            using var ms = new MemoryStream((int)s.Length);
            if (comp == null)
                s.CopyTo(ms);
            else
                comp.Decompress(s, ms);
            return ms.ToArray();
        }



        /// <summary>
        /// A MemoryManager over a raw pointer
        /// </summary>
        /// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
        sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
            where T : unmanaged
        {
            readonly T* _pointer;
            readonly int _length;

            /// <summary>
            /// Create a new UnmanagedMemoryManager instance at the given pointer and size
            /// </summary>
            /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
            public UnmanagedMemoryManager(Span<T> span)
            {
                fixed (T* ptr = &MemoryMarshal.GetReference(span))
                {
                    _pointer = ptr;
                    _length = span.Length;
                }
            }

            /// <summary>
            /// Create a new UnmanagedMemoryManager instance at the given pointer and size
            /// </summary>
            /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
            public UnmanagedMemoryManager(ReadOnlySpan<T> span)
            {
                fixed (T* ptr = &MemoryMarshal.GetReference(span))
                {
                    _pointer = ptr;
                    _length = span.Length;
                }
            }

            /// <summary>
            /// Create a new UnmanagedMemoryManager instance at the given pointer and size
            /// </summary>
            public UnmanagedMemoryManager(T* pointer, int length)
            {
                if (length < 0)
                    throw new ArgumentOutOfRangeException(nameof(length));
                _pointer = pointer;
                _length = length;
            }

            /// <summary>
            /// Create a new UnmanagedMemoryManager instance at the given pointer and size
            /// </summary>
            public UnmanagedMemoryManager(IntPtr pointer, int length) : this((T*)pointer.ToPointer(), length) { }

            /// <summary>
            /// Obtains a span that represents the region
            /// </summary>
            public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

            /// <summary>
            /// Provides access to a pointer that represents the data (note: no actual pin occurs)
            /// </summary>
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                if (elementIndex < 0 || elementIndex >= _length)
                    throw new ArgumentOutOfRangeException(nameof(elementIndex));
                return new MemoryHandle(_pointer + elementIndex);
            }

            /// <summary>
            /// Has no effect
            /// </summary>
            public override void Unpin() { }

            /// <summary>
            /// Releases all resources associated with this object
            /// </summary>
            protected override void Dispose(bool disposing) { }

            /// <summary>
            /// Get some readonly memory
            /// </summary>
            public ReadOnlyMemory<T> ReadOnlyMemory => Memory;

        }


    }


}
