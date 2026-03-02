
using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Buffers;
using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SysWeaver
{



    /// <summary>
    /// Function for getting the file content as memory (using memory mapped io).
    /// </summary>
    public static class FileReadOnlyMemory
    {

        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<Byte[]> ReadAllBytesAsync(string filename)
            => ReadAllBytesAsync(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] ReadAllBytes(string filename)
            => ReadAllBytes(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the content of a file using memory mapped io if possible
        /// If memory mapped IO doesn't work the file is read to memory in a traditional fashion
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        public static async ValueTask<Byte[]> ReadAllBytesAsync(FileStream fileStream, bool leaveOpen = false)
        {
            using var p = await ReadAsync(fileStream, leaveOpen).ConfigureAwait(false);
            var s = p.Memory.Span;
            var len = s.Length;
            var dest = GC.AllocateUninitializedArray<Byte>(len);
            s.CopyTo(dest);
            return dest;
        }


        /// <summary>
        /// Get the content of a file using memory mapped io if possible
        /// If memory mapped IO doesn't work the file is read to memory in a traditional fashion
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        public static Byte[] ReadAllBytes(FileStream fileStream, bool leaveOpen = false)
        {
            using var p = Read(fileStream, leaveOpen);
            var s = p.Memory.Span;
            var len = s.Length;
            var dest = GC.AllocateUninitializedArray<Byte>(len);
            s.CopyTo(dest);
            return dest;
        }


        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(string filename)
            => ReadAsync(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        public static async ValueTask<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(FileStream fileStream, bool leaveOpen = false)
        {
            try
            {
                if (TryMap<Byte>(out var mem, fileStream, leaveOpen))
                {
                    fileStream = null;
                    return mem;
                }
            }
            catch
            {
            }
            finally
            {
                if (!leaveOpen)
                    fileStream?.Dispose();
            }
            using var x = leaveOpen ? null : fileStream;
            using var ms = new ArrayPoolStream();
            await fileStream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetMemory();
        }



        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Read(string filename)
            => Read(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        public static IUnmanagedReadOnlyMemory<Byte> Read(FileStream fileStream, bool leaveOpen = false)
        {
            try
            {
                if (TryMap<Byte>(out var mem, fileStream, leaveOpen))
                {
                    fileStream = null;
                    return mem;
                }
            }
            catch
            {
            }
            finally
            {
                if (!leaveOpen)
                    fileStream?.Dispose();
            }
            using var x = leaveOpen ? null : fileStream;
            using var ms = new ArrayPoolStream();
            fileStream.CopyTo(ms);
            return ms.GetMemory();
        }






        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Map(string filename)
            => Map<Byte>(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));

        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Map<T>(string filename) where T : unmanaged
            => Map<T>(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file could be mapped (is smaller than the maximum) else false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, string filename, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file could be mapped (is smaller than the maximum) else false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap<T>(out IUnmanagedReadOnlyMemory<T> mem, string filename, int maxLength = int.MaxValue) where T : unmanaged
            => TryMap<T>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Map(FileStream fileStream, bool leaveOpen = false)
            => Map<Byte>(fileStream, leaveOpen);

        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <returns>The content of the file</returns>
        public static IUnmanagedReadOnlyMemory<T> Map<T>(FileStream fileStream, bool leaveOpen = false) where T : unmanaged
        {
            try
            {
                var pos = fileStream.Position;
                var l = fileStream.Length - pos;
                if (l > int.MaxValue)
                    throw new Exception("File to large for mapping!");
                if (l <= 0)
                    return UnmanagedMemory<T>.EmptyReadOnlyMemory;
                var ret = new MappedFileMemoryHandler<T>(fileStream, (int)l, pos, leaveOpen);
                fileStream = null;
                return ret;
            }
            finally
            {
                if (!leaveOpen)
                    fileStream?.Dispose();
            }
        }

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <param name="maxLength">The maximum length of the file, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file could be mapped (is smaller than the maximum) else false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, FileStream fileStream, bool leaveOpen = false, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, fileStream, leaveOpen, maxLength);

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true and the function returns false, the callee must Dispose the stream, if the function returns true, the stream is Disposed automatically</param>
        /// <param name="maxLength">The maximum length of the file, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file could be mapped (is smaller than the maximum) else false</returns>
        public static bool TryMap<T>(out IUnmanagedReadOnlyMemory<T> mem, FileStream fileStream, bool leaveOpen = false, int maxLength = int.MaxValue) where T : unmanaged
        {
            try
            {
                var pos = fileStream.Position;
                var l = fileStream.Length - pos;
                if (l > maxLength)
                {
                    mem = null;
                    return false;
                }
                if (l <= 0)
                {
                    mem = UnmanagedMemory<T>.EmptyReadOnlyMemory;
                    return true;
                }
                mem = new MappedFileMemoryHandler<T>(fileStream, (int)l, pos);
                fileStream = null;
                return true;
            }
            finally
            {
                if (!leaveOpen)
                    fileStream?.Dispose();
            }
        }






        /// <summary>
        /// A MemoryManager over a raw pointer
        /// </summary>
        /// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
        sealed unsafe class MappedFileMemoryHandler<T> : MemoryManager<T>, IUnmanagedReadOnlyMemory<T>
            where T : unmanaged
        {
            readonly T* _pointer;
            readonly int _length;
            
      

            /// <summary>
            /// Create a new UnmanagedMemoryManager instance at the given pointer and size
            /// </summary>
            /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
            public MappedFileMemoryHandler(FileStream fs, int byteSize, long pos = 0, bool leaveOpen = false)
            {
                var file = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen);
                try
                {
                    var view = file.CreateViewAccessor(pos, byteSize, MemoryMappedFileAccess.Read);
                    try
                    {
                        byte* ptr = (byte*)0;
                        var h = view.SafeMemoryMappedViewHandle;
                        h.AcquirePointer(ref ptr);
                        F = file;
                        V = view;
                        H = h;
                        _pointer = (T*)ptr;
                        _length = (int)byteSize / Marshal.SizeOf<T>();
                        ReadOnlyMemory = Memory;
                        if (leaveOpen)
                            fs.Position = pos + byteSize;
                    }
                    catch
                    {
                        view.Dispose();
                        throw;
                    }

                }
                catch
                {
                    file.Dispose();
                    throw;
                }

            }

            /// <summary>
            /// Releases all resources associated with this object
            /// </summary>
            protected override void Dispose(bool disposing) {

                var h = Interlocked.Exchange(ref H, null);
                if (h != null)
                {
                    h.ReleasePointer();
                    h.Dispose();
                }
                Interlocked.Exchange(ref V, null)?.Dispose();
                Interlocked.Exchange(ref F, null)?.Dispose();
            }


            MemoryMappedFile F;
            MemoryMappedViewAccessor V;
            SafeMemoryMappedViewHandle H;


            /// <summary>
            /// Obtains a span that represents the region
            /// </summary>
            public override Span<T> GetSpan() => new (_pointer, _length);

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
            public override void Unpin() 
            { 
                
            }


            /// <summary>
            /// Get some readonly memory
            /// </summary>
            public readonly ReadOnlyMemory<T> ReadOnlyMemory;


            ReadOnlyMemory<T> IUnmanagedReadOnlyMemory<T>.Memory => ReadOnlyMemory;
        }



    }
}
