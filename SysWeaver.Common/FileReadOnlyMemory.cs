
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
    /// Functions for getting the content of a file as memory, using memory mapped io on Windows (no copy, the OS pages the file in on demand).
    /// On other platforms (Linux, macOS etc) the data is read into pooled memory instead (see <see cref="UsesMemoryMapping"/>).
    /// </summary>
    /// <remarks>
    /// All functions reads from the current position of the file stream to the end of the file.
    /// The returned <see cref="IUnmanagedReadOnlyMemory{T}"/> must be disposed when no longer used (unmaps the file, or returns a pooled buffer), the memory must not be used after that.
    /// Files opened by name are opened with <see cref="FileShare.Read"/> (on Windows others can't open the file for writing while it's open, on other platforms this is only advisory).
    /// Windows refuses to truncate a file while a view of it is mapped (ERROR_USER_MAPPED_FILE), so a mapped file can't shrink under the memory (the content may still change if the file was opened with <see cref="FileShare.Write"/>).
    /// On Linux / macOS nothing prevents another thread / process from truncating a mapped file, touching a mapped page past the new end raises SIGBUS, which kills the process (can't be caught),
    /// that's why files are never mapped there: the memory is a copy of the data taken when the function is called (if the file shrinks during the read, the memory is shorter).
    /// Used by <see cref="StreamExt"/> for all <see cref="FileStream"/>'s, by <see cref="FileHash"/> and by the http file handler.
    /// </remarks>
    public static class FileReadOnlyMemory
    {
        /// <summary>
        /// True if files are memory mapped (Windows only), false if the data is read into pooled memory (all other platforms).
        /// </summary>
        /// <remarks>
        /// On non-Windows platforms a mapped file can be truncated by anyone that can write to it, the next access to a page past the new end of file kills the process (SIGBUS).
        /// </remarks>
        public static bool UsesMemoryMapping
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => OperatingSystem.IsWindows();
        }

        /// <summary>
        /// Read the content of a file into a new array (on Windows the file is memory mapped and copied, if memory mapped IO doesn't work, or on other platforms, the file is read normally).
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
        /// Read the content of a file into a new array (on Windows the file is memory mapped and copied, if memory mapped IO doesn't work, or on other platforms, the file is read normally).
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
        /// On Windows the file is memory mapped and copied (if memory mapped IO doesn't work the file is read normally), on other platforms it's read directly into the array.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed before this method returns</param>
        /// <returns>The remaining content of the file (an empty array if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is larger than the max array size</exception>
        public static async Task<Byte[]> ReadAllBytesAsync(FileStream fileStream, bool leaveOpen = false)
        {
            if (!UsesMemoryMapping)
            {
                var l = RemainingLength(fileStream);
                if ((l > 0) && (l <= Array.MaxLength))
                {
                    using var x = leaveOpen ? null : fileStream;
                    //  A sync handle is read synchronously (async reads of a sync handle are queued to the thread pool, slower, a mapped file blocks on page faults too)
                    return fileStream.IsAsync ? await ReadArrayAsync(fileStream, (int)l).ConfigureAwait(false) : ReadArray(fileStream, (int)l);
                }
            }
            using var p = await ReadAsync(fileStream, leaveOpen).ConfigureAwait(false);
            var s = p.Memory.Span;
            var len = s.Length;
            var dest = GC.AllocateUninitializedArray<Byte>(len);
            s.CopyTo(dest);
            return dest;
        }


        /// <summary>
        /// Read the remaining content of a file stream (from the current position) into a new array.
        /// On Windows the file is memory mapped and copied (if memory mapped IO doesn't work the file is read normally), on other platforms it's read directly into the array.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed before this method returns</param>
        /// <returns>The remaining content of the file (an empty array if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is larger than the max array size</exception>
        public static Byte[] ReadAllBytes(FileStream fileStream, bool leaveOpen = false)
        {
            if (!UsesMemoryMapping)
            {
                var l = RemainingLength(fileStream);
                if ((l > 0) && (l <= Array.MaxLength))
                {
                    using var x = leaveOpen ? null : fileStream;
                    return ReadArray(fileStream, (int)l);
                }
            }
            using var p = Read(fileStream, leaveOpen);
            var s = p.Memory.Span;
            var len = s.Length;
            var dest = GC.AllocateUninitializedArray<Byte>(len);
            s.CopyTo(dest);
            return dest;
        }


        /// <summary>
        /// Get the content of a file as memory, on Windows the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work, or on other platforms (see <see cref="UsesMemoryMapping"/>), the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (if mapped, the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(string filename)
            => ReadAsync(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work (or the remaining data is larger than int.MaxValue bytes), or on other platforms (see <see cref="UsesMemoryMapping"/>), the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed (when the returned memory is disposed if the file is mapped, else before this method returns)</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is too large to be read into memory</exception>
        public static async Task<IUnmanagedReadOnlyMemory<Byte>> ReadAsync(FileStream fileStream, bool leaveOpen = false)
        {
            if (!UsesMemoryMapping)
            {
                var l = RemainingLength(fileStream);
                //  Unknown length (ex: /proc files reports 0, non seekable files) or too large, read until the end below (as for a failed mapping on Windows)
                if ((l > 0) && (l <= Array.MaxLength))
                {
                    using var x2 = leaveOpen ? null : fileStream;
                    //  A sync handle is read synchronously (async reads of a sync handle are queued to the thread pool, slower, a mapped file blocks on page faults too)
                    return fileStream.IsAsync ? await ReadPooledAsync(fileStream, (int)l).ConfigureAwait(false) : ReadPooled<Byte>(fileStream, (int)l);
                }
            }
            else
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
            }
            using var x = leaveOpen ? null : fileStream;
            using var ms = new ArrayPoolStream();
            await fileStream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetMemory();
        }



        /// <summary>
        /// Get the content of a file as memory, on Windows the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work, or on other platforms (see <see cref="UsesMemoryMapping"/>), the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (if mapped, the file is kept open until then)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Read(string filename)
            => Read(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));


        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process.
        /// If memory mapped IO doesn't work (or the remaining data is larger than int.MaxValue bytes), or on other platforms (see <see cref="UsesMemoryMapping"/>), the file is read to (pooled) memory.
        /// This is the safest method to use.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (positioned at the end of the read data), else it's disposed (when the returned memory is disposed if the file is mapped, else before this method returns)</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">An I/O error occurred, or the remaining data is too large to be read into memory</exception>
        public static IUnmanagedReadOnlyMemory<Byte> Read(FileStream fileStream, bool leaveOpen = false)
        {
            if (!UsesMemoryMapping)
            {
                var l = RemainingLength(fileStream);
                //  Unknown length (ex: /proc files reports 0, non seekable files) or too large, read until the end below (as for a failed mapping on Windows)
                if ((l > 0) && (l <= Array.MaxLength))
                {
                    using var x2 = leaveOpen ? null : fileStream;
                    return ReadPooled<Byte>(fileStream, (int)l);
                }
            }
            else
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
            }
            using var x = leaveOpen ? null : fileStream;
            using var ms = new ArrayPoolStream();
            fileStream.CopyTo(ms);
            return ms.GetMemory();
        }






        /// <summary>
        /// Get the content of a file as memory, on Windows the file is not read, just mapped into the process (on other platforms the file is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (if mapped, the file is kept open until then)</returns>
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
        /// Get the content of a file as memory, on Windows the file is not read, just mapped into the process (on other platforms the file is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// The length of the file may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="filename">The name of the file to map</param>
        /// <returns>The content of the file, dispose it when done (if mapped, the file is kept open until then)</returns>
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
        /// Try to get the content of a file as memory, on Windows the file is not read, just mapped into the process (on other platforms the file is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// </summary>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file in bytes, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped or read (its length is at most <paramref name="maxLength"/>) else false (the file is closed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, string filename, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Try to get the content of a file as memory, on Windows the file is not read, just mapped into the process (on other platforms the file is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="filename">The name of the file to map</param>
        /// <param name="maxLength">The maximum length of the file in bytes, if the file is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped or read (its length is at most <paramref name="maxLength"/>) else false (the file is closed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist</exception>
        /// <exception cref="IOException">The file can't be opened or read (ex: it's opened for writing by someone else)</exception>
        /// <exception cref="UnauthorizedAccessException">Access to the file is denied</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap<T>(out IUnmanagedReadOnlyMemory<T> mem, string filename, int maxLength = int.MaxValue) where T : unmanaged
            => TryMap<T>(out mem, new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), false, maxLength);

        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process (on other platforms the data is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// The remaining length may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (and positioned at the end of the mapped data on success), the caller must dispose it.
        /// If false, the stream is owned by the returned memory (disposed with it), or disposed immediately if the data is empty, the mapping fails or the data is read (non-Windows platforms)</param>
        /// <returns>The remaining content of the file, dispose it when done (a shared empty instance if there is no remaining data)</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="Exception">The remaining data is larger than int.MaxValue bytes</exception>
        /// <exception cref="IOException">The file can't be mapped</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<Byte> Map(FileStream fileStream, bool leaveOpen = false)
            => Map<Byte>(fileStream, leaveOpen);

        /// <summary>
        /// Get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process (on other platforms the data is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// The remaining length may not be larger than int.MaxValue (2GB), in that case an exception is thrown.
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">If true, the stream is left open (and positioned at the end of the mapped data on success), the caller must dispose it.
        /// If false, the stream is owned by the returned memory (disposed with it), or disposed immediately if the data is empty, the mapping fails or the data is read (non-Windows platforms)</param>
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
                if (!UsesMemoryMapping)
                    return ReadPooled<T>(fileStream, (int)l);
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
        /// Try to get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process (on other platforms the data is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// </summary>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">Only affects the case when the function returns false: if true the caller must dispose the stream, else it's disposed.
        /// If the function returns true, the stream is always owned by the returned memory (disposed with it, or immediately if the data is empty or read (non-Windows platforms))</param>
        /// <param name="maxLength">The maximum remaining length in bytes, if the remaining data is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped or read (the remaining length is at most <paramref name="maxLength"/>) else false</returns>
        /// <exception cref="NullReferenceException"><paramref name="fileStream"/> is null</exception>
        /// <exception cref="IOException">The file can't be mapped (failures to map throws, they don't return false)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMap(out IUnmanagedReadOnlyMemory<Byte> mem, FileStream fileStream, bool leaveOpen = false, int maxLength = int.MaxValue)
            => TryMap<Byte>(out mem, fileStream, leaveOpen, maxLength);

        /// <summary>
        /// Try to get the remaining content of a file stream (from the current position) as memory, on Windows the file is not read, just mapped into the process (on other platforms the data is read to pooled memory, see <see cref="UsesMemoryMapping"/>).
        /// </summary>
        /// <typeparam name="T">The element type, the file is reinterpreted as an array of T (a trailing partial element is ignored)</typeparam>
        /// <param name="mem">The content of the file if successful (dispose it when done), else null</param>
        /// <param name="fileStream">The file stream, must be open for reading</param>
        /// <param name="leaveOpen">Only affects the case when the function returns false: if true the caller must dispose the stream, else it's disposed.
        /// If the function returns true, the stream is always owned by the returned memory (disposed with it, or immediately if the data is empty or read (non-Windows platforms))</param>
        /// <param name="maxLength">The maximum remaining length in bytes, if the remaining data is larger than this, the function returns false</param>
        /// <returns>True if the file was mapped or read (the remaining length is at most <paramref name="maxLength"/>) else false</returns>
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
                if (UsesMemoryMapping)
                {
                    mem = new MappedFileMemoryHandler<T>(fileStream, (int)l, pos);
                }
                else
                {
                    mem = ReadPooled<T>(fileStream, (int)l);
                    // On success the stream is always disposed (see leaveOpen)
                    fileStream.Dispose();
                }
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
        /// The remaining length of a file stream (0 if it's unknown, ex: not seekable)
        /// </summary>
        static long RemainingLength(FileStream fileStream)
        {
            try
            {
                return fileStream.CanSeek ? fileStream.Length - fileStream.Position : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Read from the current position of a stream until the destination is full or the end of the stream is reached
        /// </summary>
        /// <returns>The number of bytes read</returns>
        static int ReadFully(FileStream fs, Span<Byte> dest)
        {
            var size = dest.Length;
            var read = 0;
            while (read < size)
            {
                var r = fs.Read(dest.Slice(read));
                if (r <= 0)
                    break;
                read += r;
            }
            return read;
        }

        /// <summary>
        /// Read from the current position of a stream until the destination is full or the end of the stream is reached
        /// </summary>
        /// <returns>The number of bytes read</returns>
        static async ValueTask<int> ReadFullyAsync(FileStream fs, Memory<Byte> dest)
        {
            var size = dest.Length;
            var read = 0;
            while (read < size)
            {
                var r = await fs.ReadAsync(dest.Slice(read)).ConfigureAwait(false);
                if (r <= 0)
                    break;
                read += r;
            }
            return read;
        }

        /// <summary>
        /// Read the remaining data of a stream into a new array (used instead of mapping on non-Windows platforms).
        /// Stops early at the end of the file (if the file shrunk).
        /// The stream isn't disposed.
        /// </summary>
        static Byte[] ReadArray(FileStream fs, int byteSize)
        {
            var d = GC.AllocateUninitializedArray<Byte>(byteSize);
            var read = ReadFully(fs, d);
            return read == byteSize ? d : d.AsSpan(0, read).ToArray();
        }

        /// <summary>
        /// Read the remaining data of a stream into a new array (used instead of mapping on non-Windows platforms).
        /// Stops early at the end of the file (if the file shrunk).
        /// The stream isn't disposed.
        /// </summary>
        static async Task<Byte[]> ReadArrayAsync(FileStream fs, int byteSize)
        {
            var d = GC.AllocateUninitializedArray<Byte>(byteSize);
            var read = await ReadFullyAsync(fs, d).ConfigureAwait(false);
            return read == byteSize ? d : d.AsSpan(0, read).ToArray();
        }

        /// <summary>
        /// Read up to byteSize bytes from the current position of a stream into pooled memory (used instead of mapping on non-Windows platforms).
        /// Stops early at the end of the file (if the file shrunk), a trailing partial element is ignored.
        /// The stream isn't disposed.
        /// </summary>
        static IUnmanagedReadOnlyMemory<T> ReadPooled<T>(FileStream fs, int byteSize) where T : unmanaged
        {
            var es = Unsafe.SizeOf<T>();
            var count = byteSize / es;
            if (count <= 0)
                return UnmanagedMemory<T>.EmptyReadOnlyMemory;
            var buf = ArrayPool<T>.Shared.Rent(count);
            try
            {
                count = ReadFully(fs, MemoryMarshal.AsBytes(buf.AsSpan(0, count))) / es;
                if (count > 0)
                {
                    var ret = new PooledMemory<T>(buf, count);
                    buf = null;
                    return ret;
                }
                return UnmanagedMemory<T>.EmptyReadOnlyMemory;
            }
            finally
            {
                if (buf != null)
                    ArrayPool<T>.Shared.Return(buf);
            }
        }

        /// <summary>
        /// Read up to byteSize bytes from the current position of a stream into pooled memory (used instead of mapping on non-Windows platforms).
        /// Stops early at the end of the file (if the file shrunk).
        /// The stream isn't disposed.
        /// </summary>
        static async Task<IUnmanagedReadOnlyMemory<Byte>> ReadPooledAsync(FileStream fs, int byteSize)
        {
            var buf = ArrayPool<Byte>.Shared.Rent(byteSize);
            try
            {
                var read = await ReadFullyAsync(fs, buf.AsMemory(0, byteSize)).ConfigureAwait(false);
                if (read > 0)
                {
                    var ret = new PooledMemory<Byte>(buf, read);
                    buf = null;
                    return ret;
                }
                return UnmanagedMemory<Byte>.EmptyReadOnlyMemory;
            }
            finally
            {
                if (buf != null)
                    ArrayPool<Byte>.Shared.Return(buf);
            }
        }

        /// <summary>
        /// A copy of (a part of) a file in a buffer rented from the shared ArrayPool, used instead of a mapping on non-Windows platforms.
        /// Disposing it returns the buffer to the pool (thread safe, only the first call has an effect).
        /// </summary>
        sealed class PooledMemory<T> : IUnmanagedReadOnlyMemory<T> where T : unmanaged
        {
            public PooledMemory(T[] buffer, int length)
            {
                Buffer = buffer;
                Memory = new ReadOnlyMemory<T>(buffer, 0, length);
            }

            T[] Buffer;

            /// <summary>
            /// The data (must not be used after the object is disposed)
            /// </summary>
            public ReadOnlyMemory<T> Memory { get; }

            /// <summary>
            /// Return the buffer to the pool (only the first call has an effect)
            /// </summary>
            public void Dispose()
            {
                var b = Interlocked.Exchange(ref Buffer, null);
                if (b != null)
                    ArrayPool<T>.Shared.Return(b);
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
