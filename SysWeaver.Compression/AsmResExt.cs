using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SysWeaver.Compression;

namespace SysWeaver
{
    /// <summary>
    /// Extension methods for reading (optionally pre-compressed) embedded resources.
    /// </summary>
    /// <remarks>
    /// A resource is considered compressed if its name ends with a "." followed by an extension registered in the <see cref="CompManager"/> (ex: "Site.index.html.br").
    /// Uncompressed data of resources that are memory mapped (the normal case) is returned without copying, as memory that is valid for the lifetime of the (non-collectible) assembly.
    /// </remarks>
    public static class AsmResExt
    {
        /// <summary>
        /// The resource names of an assembly (GetManifestResourceNames returns a new array every time)
        /// </summary>
        static readonly ConditionalWeakTable<Assembly, String[]> ResourceNames = new();

        static String[] GetResourceNames(Assembly asm) => ResourceNames.GetValue(asm, static a => a.GetManifestResourceNames());

        /// <summary>
        /// Read all data of a stream into an array of the exact size
        /// </summary>
        static Byte[] ReadAllBytes(Stream s)
        {
            var len = checked((int)(s.Length - s.Position));
            if (len <= 0)
                return Array.Empty<Byte>();
            var ret = GC.AllocateUninitializedArray<Byte>(len);
            s.ReadExactly(ret);
            return ret;
        }

        /// <summary>
        /// Open a resource stream, throws a <see cref="FileNotFoundException"/> naming the resource if it doesn't exist
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The full name of the resource, null if it wasn't found</param>
        /// <param name="requestedName">The name to report if the resource doesn't exist</param>
        static Stream OpenResource(Assembly asm, String name, String requestedName)
        {
            var s = name == null ? null : asm.GetManifestResourceStream(name);
            if (s == null)
                throw new FileNotFoundException(String.Concat("Embedded resource \"", requestedName, "\" not found in ", asm.FullName), requestedName);
            return s;
        }

        /// <summary>
        /// Given an uncompressed resource name, find the resource that has that name or that name + "." + a compression extension, and modify to the true resource name.
        /// If no resource starts with the name, the name is retried with the prefix of the first resource that contains ".data." (the convention used for embedded web data).
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource.
        /// On return it's the full name of the resource that was found (the compressed or uncompressed one, whichever is listed first), or null if no resource was found</param>
        /// <returns>The compression type of the found resource, or null if it's uncompressed or not found</returns>
        public static ICompType FindResource(this Assembly asm, ref String uncompressedName)
        {
            var allRes = GetResourceNames(asm);
            foreach (var t in allRes)
            {
                if (!t.StartsWith(uncompressedName, StringComparison.Ordinal))
                    continue;
                if (t.FastEquals(uncompressedName))
                    return null;
                var comp = CompManager.GetFromExt(t.AsSpan(uncompressedName.Length + 1));
                if (comp == null)
                    continue;
                uncompressedName = t;
                return comp;
            }
            String prefixed = null;
            foreach (var t in allRes)
            {
                var k = t.FastIndexOf(".data.");
                if (k >= 0)
                {
                    prefixed = String.Concat(t.AsSpan(0, k + 6), uncompressedName);
                    break;
                }
            }
            if (prefixed != null)
            {
                uncompressedName = prefixed;
                foreach (var t in allRes)
                {
                    if (!t.StartsWith(uncompressedName, StringComparison.Ordinal))
                        continue;
                    if (t.FastEquals(uncompressedName))
                        return null;
                    var comp = CompManager.GetFromExt(t.AsSpan(uncompressedName.Length + 1));
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
        /// Determine the compression method used in a resource based on its extension (only the name is inspected, the resource doesn't need to exist)
        /// </summary>
        /// <param name="asm">The assembly that contain the resource (not used)</param>
        /// <param name="compressedName">The name of the resource, if the resource is compressed the compression extension is removed</param>
        /// <returns>The compression type or null</returns>
        public static ICompType GetResourceCompression(this Assembly asm, ref String compressedName)
        {
            var f = compressedName.LastIndexOf('.');
            if (f < 0)
                return null;
            var comp = CompManager.GetFromExt(compressedName.AsSpan(f + 1));
            if (comp != null)
                compressedName = compressedName.Substring(0, f);
            return comp;
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="compressedName">The full name of the resource (including any compression extension), if the resource is compressed the compression extension is removed</param>
        /// <returns>The uncompressed data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        /// <exception cref="InvalidDataException">The compressed data is invalid.</exception>
        public static unsafe ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, ref String compressedName)
        {
            var o = compressedName;
            var comp = GetResourceCompression(asm, ref compressedName);
            using var s = OpenResource(asm, o, o);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            return comp == null ? ReadAllBytes(s) : comp.GetDecompressed(s);
        }


        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="uncompressedName">The name of the resource without any compression extension, a compressed version is located using <see cref="FindResource(Assembly, ref string)"/></param>
        /// <returns>The uncompressed data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        /// <exception cref="InvalidDataException">The compressed data is invalid.</exception>
        public static unsafe ReadOnlyMemory<Byte> GetUncompressedResourceData(this Assembly asm, String uncompressedName)
        {
            var o = uncompressedName;
            var comp = FindResource(asm, ref uncompressedName);
            using var s = OpenResource(asm, uncompressedName, o);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            return comp == null ? ReadAllBytes(s) : comp.GetDecompressed(s);
        }

        /// <summary>
        /// Get the data of an embedded resource (no decompression), without copying if the resource is memory mapped
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The full name of the resource</param>
        /// <returns>The data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        public static unsafe ReadOnlyMemory<Byte> GetResourceData(this Assembly asm, String name)
        {
            using var s = OpenResource(asm, name, name);
            if (s is UnmanagedMemoryStream x)
                return new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory;
            return ReadAllBytes(s);
        }

        /// <summary>
        /// Get the data of an embedded resource as a new byte array (no decompression)
        /// </summary>
        /// <param name="asm">The assembly that contain the resource</param>
        /// <param name="name">The full name of the resource</param>
        /// <returns>The data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        public static unsafe Byte[] GetResourceDataBytes(this Assembly asm, String name)
        {
            using var s = OpenResource(asm, name, name);
            if (s is UnmanagedMemoryStream x)
            {
                var sl = checked((int)x.Length);
                var ret = GC.AllocateUninitializedArray<Byte>(sl);
                new ReadOnlySpan<byte>(x.PositionPointer, sl).CopyTo(ret.AsSpan());
                return ret;
            }
            return ReadAllBytes(s);
        }

        /// <summary>
        /// Get the data of an embedded resource, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource, if the resource isn't found by name it's retried prefixed with the namespace of this type</param>
        /// <param name="uncompressedName">The name of the resource without any compression extension, a compressed version is located using <see cref="FindResource(Assembly, ref string)"/></param>
        /// <returns>The uncompressed data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        /// <exception cref="InvalidDataException">The compressed data is invalid.</exception>
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
            using var s = OpenResource(asm, t, uncompressedName);
            if (s is UnmanagedMemoryStream x)
                return comp == null
                    ?
                    new UnmanagedMemoryManager<Byte>(x.PositionPointer, checked((int)x.Length)).ReadOnlyMemory
                    : comp.GetDecompressed(new ReadOnlySpan<byte>(x.PositionPointer, checked((int)x.Length)));
            return comp == null ? ReadAllBytes(s) : comp.GetDecompressed(s);
        }

        /// <summary>
        /// Get the data of an embedded resource as a new byte array, if it's compressed it will be decompressed
        /// </summary>
        /// <param name="asmType">A type in the assembly that contain the resource, if the resource isn't found by name it's retried prefixed with the namespace of this type</param>
        /// <param name="uncompressedName">The name of the resource without any compression extension, a compressed version is located using <see cref="FindResource(Assembly, ref string)"/></param>
        /// <returns>The uncompressed data of the resource</returns>
        /// <exception cref="FileNotFoundException">The resource doesn't exist.</exception>
        /// <exception cref="InvalidDataException">The compressed data is invalid.</exception>
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
            using var s = OpenResource(asm, t, uncompressedName);
            if (s is UnmanagedMemoryStream x)
            {
                var sl = checked((int)x.Length);
                if (comp == null)
                {
                    var ret = GC.AllocateUninitializedArray<Byte>(sl);
                    new ReadOnlySpan<byte>(x.PositionPointer, sl).CopyTo(ret.AsSpan());
                    return ret;
                }
                return comp.GetDecompressedArray(new ReadOnlySpan<byte>(x.PositionPointer, sl));
            }
            return comp == null ? ReadAllBytes(s) : comp.GetDecompressedArray(s);
        }



        /// <summary>
        /// A <see cref="MemoryManager{T}"/> over a raw pointer, used to expose memory mapped resource data as <see cref="Memory{T}"/> without copying
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
