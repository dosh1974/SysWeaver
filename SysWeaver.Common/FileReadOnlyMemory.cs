
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
    /// Functions for getting the content of a file as memory, using memory mapped io when possible (no copy, the OS pages the file in on demand).
    /// </summary>
    /// <remarks>
    /// All functions reads from the current position of the file stream to the end of the file.
    /// The returned <see cref="IUnmanagedReadOnlyMemory{T}"/> must be disposed when no longer used (unmaps the file, or returns a pooled buffer).
    /// Files opened by name are opened with <see cref="FileShare.Read"/>, so the file can't be modified while it's mapped.
    /// Used by <see cref="StreamExt"/> for all <see cref="FileStream"/>'s, by <see cref="FileHash"/> and by the http file handler.
    /// </remarks>
    public static class FileReadOnlyMemory
    {

        /// <summary>
        /// Read the content of a file into a new array (the file is memory mapped and copied, if memory mapped IO doesn't work the file is read normally).
        /// </summary>
        /// <param name="filename">The name of the file to read</param>
        /// <returns>The content of the file (an empty array for an empty file)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Byte[]> ReadAllBytesAsync(string filename)
            => ReadAllBytesAsync(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Read the content of a file into a new array (the file is memory mapped and copied, if memory mapped IO doesn't work the file is read normally).
        /// </summary>
        /// <param name="filename">The name of the file to read</param>
        /// <returns>The content of the file (an empty array for an empty file)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] ReadAllBytes(string filename)
            => ReadAllBytes(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Read the remaining content of a file stream (from the current position) into a new array.
        /// The file is memory mapped and copied, if memory mapped IO doesn't work the file is read normally.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed before this method returns</param>
        /// <returns>The remaining content of the file (an empty array if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is larger than the max array size</exception>
        public static async Task<Byte[]> ReadAllBytesAsync(FileStream fileStream, bool leaveOpen = false)
        {
            using var p = await ReadAsync(fileStream, leaveOpen).ConfigureAwait(false);
            var s = p.Memory.Span;
            var len = s.Length;
            var dest = GC.AllocateUninitializedArray<Byte>(len);
            s.CopyTo(dest);
            return dest;
        }


        /// <summary>
        /// Read the remaining content of a file stream (from the current position) into a new array.
        /// The file is memory mapped and copied, if memory mapped IO doesn't work the file is read normally.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed before this method returns</param>
        /// <returns>The remaining content of the file (an empty array if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is larger than the max array size</exception>
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
        /// If memory mapped IO doesn't work the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(string filename)
            => ReadAsync(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work (or the remaining data is larger than int.MaxValue bytes) the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed (when the returned memory is disposed if the file is mapped)</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is too large to be read into memory</exception>
        public static async Task<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(FileStream fileStream, bool leaveOpen = false)
        {
            try
            {
                var pos = fileStream.Position;
                var l = fileStream.Length - pos;
                if (l <= 0)
                {
                    if (!leaveOpen)
                        fileStream.Dispose();
                    return UnmanagedMemory<Byte>.EmptyReadOnlyMemory;
                }
                if (l <= int.MaxValue)
                    return new MappedFileMemoryHandler<Byte>(fileStream, (int)l, pos, leaveOpen);
            }
            catch
            {
            }
            using var x = leaveOpen ? null : fileStream;
            using var ms = new ArrayPoolStream();
            await fileStream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetMemory();
        }



        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Read(string filename)
            => Read(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work (or the remaining data is larger than int.MaxValue bytes) the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed (when the returned memory is disposed if the file is mapped)</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is too large to be read into memory</exception>
        public static IUnmanagedReadOnlyMemory<Byte> Read(FileStream fileStream, bool leaveOpen = false)
        {
            try
            {
                var pos = fileStream.Position;
                var l = fileStream.Length - pos;
                if (l <= 0)
                {
                    if (!leaveOpen)
                        fileStream.Dispose();
                    return UnmanagedMemory<Byte>.EmptyReadOnlyMemory;
                }
                if (l <= int.MaxValue)
                    return new MappedFileMemoryHandler<Byte>(fileStream, (int)l, pos, leaveOpen);
            }
            catch
            {
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
        /// <returns>The content of the file, dispose it when done (the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="Exception">The remaining data is larger than int.MaxValue bytes</exception>
        /// <exception cref="IOException">The file can't be mapped</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Map(string filename)
            => Map<Byte>(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));

        /// <summary>
        /// Get the content of a file as memory, the file is not read, just mapped into the process.
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="Exception">The remaining data is larger than int.MaxValue bytes</exception>
        /// <exception cref="IOException">The file can't be mapped</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Map<T>(string filename) where T : unmanaged
            => Map<T>(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file in bytes, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped (its length is at most <paramref name="maxLength"/>) else false (the file is closed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, string filename, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Try to get the content of a file as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file in bytes, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped (its length is at most <paramref name="maxLength"/>) else false (the file is closed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap<T>(out IUnmanagedReadOnlyMemory<T> mem, string filename, int maxLength = int.MaxValue) where T : unmanaged
            => TryMap<T>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// The remaining length may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (and positioned at the end of the mapped data on success), the caller must dispose it.
        /// If false, the stream is owned by the returned memory (disposed with it), or disposed immediately if the data is empty or the mapping fails</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="Exception">The remaining data is larger than int.MaxValue bytes</exception>
        /// <exception cref="IOException">The file can't be mapped</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Map(FileStream fileStream, bool leaveOpen = false)
            => Map<Byte>(fileStream, leaveOpen);

        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// The remaining length may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (and positioned at the end of the mapped data on success), the caller must dispose it.
        /// If false, the stream is owned by the returned memory (disposed with it), or disposed immediately if the data is empty or the mapping fails</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="Exception">The remaining data is larger than int.MaxValue bytes</exception>
        /// <exception cref="IOException">The file can't be mapped</exception>
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
        /// Try to get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">Only affects the case when the function returns false: if true the caller must dispose the stream, else it's disposed.
        /// If the function returns true, the stream is always owned by the returned memory (disposed with it, or immediately if the data is empty)</param>
        /// <param name="maxLength">The maximum remaining length in bytes, if the remaining data is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped (the remaining length is at most <paramref name="maxLength"/>) else false</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, FileStream fileStream, bool leaveOpen = false, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, fileStream, leaveOpen, maxLength);

        /// <summary>
        /// Try to get the remaining content of a file stream (from the current position) as memory, the file is not read, just mapped into the process.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">Only affects the case when the function returns false: if true the caller must dispose the stream, else it's disposed.
        /// If the function returns true, the stream is always owned by the returned memory (disposed with it, or immediately if the data is empty)</param>
        /// <param name="maxLength">The maximum remaining length in bytes, if the remaining data is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped (the remaining length is at most <paramref name="maxLength"/>) else false</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
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
                    // On success the stream is always disposed (see leaveOpen)
                    fileStream.Dispose();
                    fileStream = null;
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
        /// A MemoryManager over a read only memory mapped view of a file, disposing it unmaps the view and closes the mapping (and the file stream unless leaveOpen was used).
        /// </summary>
        /// <remarks>The mapped memory is never moved, so no pinning is required</remarks>
        sealed unsafe class MappedFileMemoryHandler<T> : MemoryManager<T>, IUnmanagedReadOnlyMemory<T>
            where T : unmanaged
        {
            readonly T* _pointer;
            readonly int _length;
            
      

            /// <summary>
            /// Map a region of a file
            /// </summary>
            /// <param name="fs">The file stream to map</param>
            /// <param name="byteSize">The number of bytes to map</param>
            /// <param name="pos">The file offset of the first byte to map</param>
            /// <param name="leaveOpen">If true, the stream isn't disposed by the mapping and is positioned at the end of the mapped region, else the mapping owns the stream (if the constructor throws, the stream is never disposed, so the caller can still use it)</param>
            public MappedFileMemoryHandler(FileStream fs, int byteSize, long pos = 0, bool leaveOpen = false)
            {
                // Always leave the stream open in the mapping, so that a failure here doesn't dispose the stream (the callers falls back to reading it), the stream is disposed in Dispose instead (unless leaveOpen)
                var file = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                try
                {
                    var view = file.CreateViewAccessor(pos, byteSize, MemoryMappedFileAccess.Read);
                    try
                    {
                        byte* ptr = (byte*)0;
                        var h = view.SafeMemoryMappedViewHandle;
                        h.AcquirePointer(ref ptr);
                        // The view starts at an allocation granularity boundary, the requested position is at PointerOffset
                        ptr += view.PointerOffset;
                        F = file;
                        V = view;
                        H = h;
                        _pointer = (T*)ptr;
                        _length = (int)byteSize / sizeof(T);
                        ReadOnlyMemory = Memory;
                        if (leaveOpen)
                            fs.Position = pos + byteSize;
                        else
                            Fs = fs;
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
            /// Unmap the view and close the mapping (thread safe, only the first call has an effect)
            /// </summary>
            /// <param name="disposing">Ignored</param>
            protected override void Dispose(bool disposing) {

                var h = Interlocked.Exchange(ref H, null);
                if (h != null)
                {
                    h.ReleasePointer();
                    h.Dispose();
                }
                Interlocked.Exchange(ref V, null)?.Dispose();
                Interlocked.Exchange(ref F, null)?.Dispose();
                Interlocked.Exchange(ref Fs, null)?.Dispose();
            }


            FileStream Fs;
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
                if (elementIndex < 0 || elementIndex > _length)
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
            /// The mapped region as readonly memory
            /// </summary>
            public readonly ReadOnlyMemory<T> ReadOnlyMemory;


            ReadOnlyMemory<T> IUnmanagedReadOnlyMemory<T>.Memory => ReadOnlyMemory;
        }



    }
}
