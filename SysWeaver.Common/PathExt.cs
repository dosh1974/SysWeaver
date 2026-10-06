using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Path validation and manipulation, and file system operations that retries on failure (the Try* and Ensure* methods never throws, they return the exception instead)
    /// </summary>
    public static class PathExt
    {

        /// <summary>
        /// Remove the extension from a path.
        /// Example: "C:\Windows\Temp\Data.dll" to "C:\Windows\Temp\Data"
        /// Example: "C:\Windows\Temp\NoExt" to "C:\Windows\Temp\NoExt"
        /// </summary>
        /// <param name="path">A path with an extension.
        /// Example: "C:\Windows\Temp\Data.dll" to "C:\Windows\Temp\Data"
        /// Example: "C:\Windows\Temp\NoExt" to "C:\Windows\Temp\NoExt"
        /// </param>
        /// <returns>The path without an extension or the original path (same instance) if non exist.
        /// Example: "C:\Windows\Temp\Data.dll" to "C:\Windows\Temp\Data"
        /// Example: "C:\Windows\Temp\NoExt" to "C:\Windows\Temp\NoExt"
        /// </returns>
        /// <remarks>
        /// Only the last '.' is removed (along with everything after it), so "a.tar.gz" becomes "a.tar".
        /// A '.' in a directory name (followed by a '/' or '\') is not considered to be an extension, both '/' and '\' are treated as separators on all platforms.
        /// A file name starting with a '.' (ex: ".gitignore") is treated as an extension, so the file name part becomes empty.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        public static String StripExtension(String path)
        {
            ArgumentNullException.ThrowIfNull(path);
            var e = path.LastIndexOf('.');
            if (e < 0)
                return path;
            // A '.' in a directory name is not an extension
            if (path.AsSpan(e + 1).IndexOfAny('/', '\\') >= 0)
                return path;
            return path.Substring(0, e);
        }

        /// <summary>
        /// Make a path relative to the executables path if it's not already rooted
        /// </summary>
        /// <param name="path">A relative or rooted path</param>
        /// <returns>A rooted path (a rooted input is returned as is, without any normalization)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> contains invalid characters (ex: '\0').</exception>
        public static String RootExecutable(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.GetFullPath(Path.Combine(EnvInfo.ExecutableDir, path));
        }

        /// <summary>
        /// Make a path relative to the current path if it's not already rooted
        /// </summary>
        /// <param name="path">A relative or rooted path</param>
        /// <returns>A rooted path (a rooted input is returned as is, without any normalization)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> contains invalid characters (ex: '\0').</exception>
        public static String RootCurrent(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        }



        static readonly SearchValues<Char> InvalidFilenameChars = SearchValues.Create(GetInvalidFilenameChars().ToArray());
        static readonly SearchValues<Char> InvalidFolderPathChars = SearchValues.Create(GetInvalidFolderPathChars().ToArray());

        static HashSet<Char> GetInvalidFilenameChars()
        {
            var t = new HashSet<char>(Path.GetInvalidFileNameChars())
            {
                Path.VolumeSeparatorChar,
                Path.PathSeparator,
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            };
            foreach (var x in Path.GetInvalidPathChars())
                t.Add(x);
            return t;
        }

        static HashSet<Char> GetInvalidFolderPathChars()
        {
            var t = new HashSet<char>(Path.GetInvalidPathChars());
            t.Add(Path.PathSeparator);
            return t;
        }

        static void SetSafeFilename(Span<Char> d, (String Text, int First) state)
        {
            state.Text.AsSpan().CopyTo(d);
            // The first invalid char is known, replace it and search for the next one
            var t = d.Slice(state.First);
            var inv = InvalidFilenameChars;
            for (; ; )
            {
                t[0] = '_';
                t = t.Slice(1);
                var i = t.IndexOfAny(inv);
                if (i < 0)
                    return;
                t = t.Slice(i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool InternalIsValidFilename(ReadOnlySpan<Char> filename)
            => filename.IndexOfAny(InvalidFilenameChars) < 0;

        static bool InternalIsValidFolderPath(ReadOnlySpan<Char> dirPath)
        {
            if (dirPath.IndexOfAny(InvalidFolderPathChars) >= 0)
                return false;
            // At most one volume separator
            var ci = dirPath.IndexOf(':');
            if (ci >= 0 && dirPath.Slice(ci + 1).IndexOf(':') >= 0)
                return false;
            // No consecutive separators (except at the start, ex: "\\server\share")
            var ds = Path.DirectorySeparatorChar;
            var ads = Path.AltDirectorySeparatorChar;
            var l = dirPath.Length;
            int o = 0;
            for (; ; )
            {
                var i = dirPath.Slice(o).IndexOfAny(ds, ads);
                if (i < 0)
                    return true;
                var n = o + i + 1;
                if (n >= l)
                    return true;
                var c = dirPath[n];
                if ((c == ds || c == ads) && (n != 1))
                    return false;
                o = n;
            }
        }


        /// <summary>
        /// Check if a filename without a path is valid, ex: "Filename.txt".
        /// </summary>
        /// <param name="filename">The filename to test</param>
        /// <returns>True if valid (no invalid file name or path chars, and no volume, path or directory separators), only way to make sure is to actuallty try to use it though.
        /// An empty string is considered valid.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        public static bool IsValidFilename(String filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            return InternalIsValidFilename(filename);
        }

        /// <summary>
        /// Check if a folder with a path is valid, ex: "C:\Windows\System32".
        /// </summary>
        /// <param name="dirPath">The path to the directory to test</param>
        /// <returns>True if valid, only way to make sure is to actuallty try to use it though.
        /// Invalid paths contains invalid path chars, a path separator (';' on Windows), more than one ':' or two consecutive directory separators (except at the start, to allow UNC paths).
        /// An empty string is considered valid.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dirPath"/> is null.</exception>
        public static bool IsValidFolderPath(String dirPath)
        {
            ArgumentNullException.ThrowIfNull(dirPath);
            return InternalIsValidFolderPath(dirPath);
        }

        /// <summary>
        /// Check if a file with a path is valid, ex: "C:\Windows\System32\Filename.txt".
        /// </summary>
        /// <param name="fullPath">The path to the file to test</param>
        /// <returns>True if valid, only way to make sure is to actuallty try to use it though.
        /// The part after the last volume or directory separator must be a valid file name (see <see cref="IsValidFilename(string)"/>) and the part before it must be a valid folder path (see <see cref="IsValidFolderPath(string)"/>).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fullPath"/> is null.</exception>
        public static bool IsValidFilePath(String fullPath)
        {
            ArgumentNullException.ThrowIfNull(fullPath);
            var s = fullPath.AsSpan();
            var li = s.LastIndexOfAny(Path.VolumeSeparatorChar, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + 1;
            if (!InternalIsValidFilename(s.Slice(li)))
                return false;
            return InternalIsValidFolderPath(s.Slice(0, li));
        }


        /// <summary>
        /// Replaces all bad filename chars in the input string with '_'
        /// </summary>
        /// <param name="s">The string to make into a safe filename, can be null or empty</param>
        /// <returns>A string of the same length as the input where every char that is invalid in a file name (see <see cref="IsValidFilename(string)"/>) is replaced with a '_'.
        /// If no chars needs to be replaced (or if the input is null or empty) the input instance is returned</returns>
        public static String SafeFilename(String s)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var i = s.AsSpan().IndexOfAny(InvalidFilenameChars);
            if (i < 0)
                return s;
            return String.Create(s.Length, (s, i), SetSafeFilename);
        }


        /// <summary>
        /// Validate that the path doesn't contain ".", ".." or any invalid chars, including volume separator
        /// </summary>
        /// <param name="s">A relative path, using <see cref="Path.DirectorySeparatorChar"/> as the separator, ex: "Sub\Folder\File.txt"</param>
        /// <returns>True if every part (separated by <see cref="Path.DirectorySeparatorChar"/>) is a non-empty valid file name (see <see cref="IsValidFilename(string)"/>) that isn't "." or "..".
        /// An empty string, a leading or trailing separator or two consecutive separators makes the path invalid.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
        public static bool IsValidSubPath(String s)
        {
            ArgumentNullException.ThrowIfNull(s);
            var p = s.AsSpan();
            var sep = Path.DirectorySeparatorChar;
            for (; ; )
            {
                var i = p.IndexOf(sep);
                var pp = i < 0 ? p : p.Slice(0, i);
                var l = pp.Length;
                if (l <= 0)
                    return false;
                if ((l <= 2) && (pp[0] == '.') && ((l == 1) || (pp[1] == '.')))
                    return false;
                if (!InternalIsValidFilename(pp))
                    return false;
                if (i < 0)
                    return true;
                p = p.Slice(i + 1);
            }
        }



        /// <summary>
        /// Get the full directory name (fixes casing)
        /// </summary>
        /// <param name="directoryName">Name of an absolute or relative directory</param>
        /// <returns>A full directory name with correct case (the casing of the parts that doesn't exist is kept as is)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="directoryName"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="directoryName"/> is empty or contains invalid characters (ex: '\0').</exception>
        /// <exception cref="PathTooLongException">The path is too long.</exception>
        public static String GetFullDirectoryName(String directoryName)
        {
            var di = new DirectoryInfo(directoryName);
            return FixCase(di.FullName);
        }

        /// <summary>
        /// Get the full file name (fixes casing)
        /// </summary>
        /// <param name="fileName">Name of an absolute or relative file</param>
        /// <returns>A full file name with correct case (the casing of the parts that doesn't exist is kept as is)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> is empty or contains invalid characters (ex: '\0').</exception>
        /// <exception cref="PathTooLongException">The path is too long.</exception>
        public static String GetFullFileName(String fileName)
        {
            var di = new FileInfo(fileName);
            return FixCase(di.FullName);
        }

        /// <summary>
        /// Use the casing of the file system for all existing parts of a full path (the root is kept as is).
        /// Note that each part is used as a search pattern, so a name containing wild cards ('*' or '?', only valid on non-Windows file systems) may match another entry.
        /// </summary>
        /// <param name="full">A full path</param>
        /// <returns>The path with the casing of the existing parts fixed</returns>
        static String FixCase(String full)
        {
            try
            {
                var t = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parent = Path.GetDirectoryName(t);
                if (String.IsNullOrEmpty(parent))
                    return full;
                var p = FixCase(parent);
                var name = Path.GetFileName(t);
                var di = new DirectoryInfo(p);
                var m = di.Exists ? di.EnumerateFileSystemInfos(name).FirstOrDefault() : null;
                return Path.Combine(p, m?.Name ?? name) + full.Substring(t.Length);
            }
            catch
            {
                return full;
            }
        }


        /// <summary>
        /// Make sure a folder exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static Exception EnsureFolderExist(String folder, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                for (; ; )
                {
                    if (Directory.Exists(folder))
                        return null;
                    try
                    {
                        Directory.CreateDirectory(folder);
                        if (Directory.Exists(folder))
                            return null;
                        // Created but doesn't exist (deleted by someone else?), treat as a failure (instead of looping forever)
                        throw new IOException("Failed to create directory " + folder.ToQuoted());
                    }
                    catch (ArgumentException ex)
                    {
                        // Invalid path (null, empty, invalid chars), no point in retrying
                        return ex;
                    }
                    catch (Exception ex)
                    {
                        --retryCount;
                        if (retryCount <= 0)
                            return ex;
                        Thread.Sleep(delayInMs);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Make sure that the folder containing the supplied filename exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="filename">The filename that it's containing folder must exist</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static Exception EnsureCanWriteFile(String filename, int retryCount = 10, int delayInMs = 100)
        {
            var dir = GetContainingFolder(filename, out var ex);
            return ex ?? EnsureFolderExist(dir, retryCount, delayInMs);
        }


        /// <summary>
        /// Make sure a folder exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static async Task<Exception> EnsureFolderExistAsync(String folder, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                for (; ; )
                {
                    if (Directory.Exists(folder))
                        return null;
                    try
                    {
                        Directory.CreateDirectory(folder);
                        if (Directory.Exists(folder))
                            return null;
                        // Created but doesn't exist (deleted by someone else?), treat as a failure (instead of looping forever)
                        throw new IOException("Failed to create directory " + folder.ToQuoted());
                    }
                    catch (ArgumentException ex)
                    {
                        // Invalid path (null, empty, invalid chars), no point in retrying
                        return ex;
                    }
                    catch (Exception ex)
                    {
                        --retryCount;
                        if (retryCount <= 0)
                            return ex;
                        await Task.Delay(delayInMs).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Make sure that the folder containing the supplied filename exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="filename">The filename that it's containing folder must exist</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static Task<Exception> EnsureCanWriteFileAsync(String filename, int retryCount = 10, int delayInMs = 100)
        {
            var dir = GetContainingFolder(filename, out var ex);
            return ex != null ? Task.FromResult(ex) : EnsureFolderExistAsync(dir, retryCount, delayInMs);
        }

        /// <summary>
        /// Get the folder that contains a file (for relative file names without a folder the current directory is used, for a root, the root is used)
        /// </summary>
        /// <param name="filename">The filename</param>
        /// <param name="ex">Set to the exception on error</param>
        /// <returns>The folder</returns>
        static String GetContainingFolder(String filename, out Exception ex)
        {
            ex = null;
            if (filename == null)
            {
                ex = new ArgumentNullException(nameof(filename));
                return null;
            }
            if (filename.Length == 0)
            {
                ex = new ArgumentException("The filename can't be empty", nameof(filename));
                return null;
            }
            try
            {
                var dir = Path.GetDirectoryName(filename);
                // null => filename is a root, "" => a file name without a folder
                if (dir == null)
                    return filename;
                return dir.Length == 0 ? "." : dir;
            }
            catch (Exception e)
            {
                ex = e;
                return null;
            }
        }


        /// <summary>
        /// Delete a file if it exists.
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="filename">The file to delete</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file doesn't exist anymore, else the exception</returns>
        public static Exception TryDeleteFile(String filename, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                Retry.Op(() =>
                {
                    if (File.Exists(filename))
                    {
                        File.Delete(filename);
                        if (File.Exists(filename))
                            throw new IOException("Failed to delete file " + filename.ToQuoted());
                    }
                }, retryCount, delayInMs);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Delete a file if it exists.
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="filename">The file to delete</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file doesn't exist anymore, else the exception</returns>
        public static async Task<Exception> TryDeleteFileAsync(String filename, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                await Retry.OpAsync(() =>
                {
                    if (File.Exists(filename))
                    {
                        File.Delete(filename);
                        if (File.Exists(filename))
                            throw new IOException("Failed to delete file " + filename.ToQuoted());
                    }
                }, retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        static void InternalCopy(String source, String dest)
        {
            var fi = new FileInfo(source);
            var ct = fi.CreationTimeUtc;
            var lwt = fi.LastWriteTimeUtc;
            var la = fi.LastAccessTimeUtc;
            var di = new FileInfo(dest);
            if (di.Exists && di.IsReadOnly)
                di.IsReadOnly = false;
            var dir = di.Directory;
            if (!dir.Exists)
                dir.Create();
            File.Copy(source, dest, true);
            //di = new FileInfo(dest);
            di.CreationTimeUtc = ct;
            di.LastWriteTimeUtc = lwt;
            di.LastAccessTimeUtc = la;
        }

        static void InternalMove(String source, String dest)
        {
            var fi = new FileInfo(source);
            var ct = fi.CreationTimeUtc;
            var lwt = fi.LastWriteTimeUtc;
            var la = fi.LastAccessTimeUtc;
            var di = new FileInfo(dest);
            if (di.Exists && di.IsReadOnly)
                di.IsReadOnly = false;
            var dir = di.Directory;
            if (!dir.Exists)
                dir.Create();
            File.Move(source, dest, true);
            //di = new FileInfo(dest);
            di.CreationTimeUtc = ct;
            di.LastWriteTimeUtc = lwt;
            di.LastAccessTimeUtc = la;
        }


        static void InternalCompress(String source, String dest, CompressionLevel level)
        {
            var fi = new FileInfo(source);
            var ct = fi.CreationTimeUtc;
            var lwt = fi.LastWriteTimeUtc;
            var la = fi.LastAccessTimeUtc;
            var di = new FileInfo(dest);
            if (di.Exists && di.IsReadOnly)
                di.IsReadOnly = false;
            var dir = di.Directory;
            if (!dir.Exists)
                dir.Create();
            using (var src = fi.OpenRead())
            {
                using var dst = di.Create();
                using var x = new GZipStream(dst, level);
                src.CopyTo(x);
            }
            //di = new FileInfo(dest);
            di.CreationTimeUtc = ct;
            di.LastWriteTimeUtc = lwt;
            di.LastAccessTimeUtc = la;
        }

        static async Task InternalCompressAsync(String source, String dest, CompressionLevel level)
        {
            var fi = new FileInfo(source);
            var ct = fi.CreationTimeUtc;
            var lwt = fi.LastWriteTimeUtc;
            var la = fi.LastAccessTimeUtc;
            var di = new FileInfo(dest);
            if (di.Exists && di.IsReadOnly)
                di.IsReadOnly = false;
            var dir = di.Directory;
            if (!dir.Exists)
                dir.Create();
            using (var src = fi.OpenRead())
            {
                using var dst = di.Create();
                using var x = new GZipStream(dst, level);
                await src.CopyToAsync(x).ConfigureAwait(false);
            }
            //di = new FileInfo(dest);
            di.CreationTimeUtc = ct;
            di.LastWriteTimeUtc = lwt;
            di.LastAccessTimeUtc = la;
        }

        /// <summary>
        /// Try to copy a file (the destination folder is created if needed, a read only destination is overwritten and the file times of the source are applied to the destination).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to copy</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was copied successfully, else the exception</returns>
        public static Exception TryCopyFile(String source, String dest, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                Retry.Op(() => InternalCopy(source, dest), retryCount, delayInMs);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Try to copy a file (the destination folder is created if needed, a read only destination is overwritten and the file times of the source are applied to the destination).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to copy</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was copied successfully, else the exception</returns>
        public static async Task<Exception> TryCopyFileAsync(String source, String dest, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                await Retry.OpAsync(() => InternalCopy(source, dest), retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Try to move / rename a file (the destination folder is created if needed, an existing destination is overwritten and the file times of the source are kept).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to move</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was moved successfully, else the exception</returns>
        public static Exception TryMoveFile(String source, String dest, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                Retry.Op(() => InternalMove(source, dest), retryCount, delayInMs);
/*                Retry.Op(() => 
                {
                    using (new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.None)) ;
                }, retryCount, delayInMs);
*/                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Try to move / rename a file (the destination folder is created if needed, an existing destination is overwritten and the file times of the source are kept).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to move</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was moved successfully, else the exception</returns>
        public static async Task<Exception> TryMoveFileAsync(String source, String dest, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                await Retry.OpAsync(() => InternalMove(source, dest), retryCount, delayInMs).ConfigureAwait(false);
/*                await Retry.OpAsync(() =>
                {
                    using (new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                }, retryCount, delayInMs).ConfigureAwait(false);
*/                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        /// <summary>
        /// Try to Gzip-compress a file (the creation, last write and last access times of the source are applied to the destination).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to compress</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was compressed  successfully, else the exception</returns>
        public static Exception TryGZipFile(String source, String dest, CompressionLevel compressionLevel = CompressionLevel.Optimal, int retryCount = 3, int delayInMs = 100)
        {
            try
            {
                Retry.Op(() => InternalCompress(source, dest, compressionLevel), retryCount, delayInMs);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Try to Gzip-compress a file (the creation, last write and last access times of the source are applied to the destination).
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="source">The file to compress</param>
        /// <param name="dest">The file to overwrite or create</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the file was compressed successfully, else the exception</returns>
        public static async Task<Exception> TryGZipFileAsync(String source, String dest, CompressionLevel compressionLevel = CompressionLevel.Optimal, int retryCount = 3, int delayInMs = 100)
        {
            try
            {
                await Retry.OpAsync(() => InternalCompressAsync(source, dest, compressionLevel), retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }



        /// <summary>
        /// Delete a directory if it exists.
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="directory">The directory to delete</param>
        /// <param name="onlyEmpty">Only delete the directory if it's empty</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the directory doesn't exists, else the exception</returns>
        public static Exception TryDeleteDirectory(String directory, bool onlyEmpty = true, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                return Retry.Op<Exception>(() =>
                {
                    if (!Directory.Exists(directory))
                        return null;
                    try
                    {
                        Directory.Delete(directory, !onlyEmpty);
                    }
                    catch (IOException)
                    {
                        if (onlyEmpty)
                        {
                            if (Directory.EnumerateFileSystemEntries(directory).Any())
                                return new DirectoryNotEmptyException(directory);
                        }
                        throw;
                    }
                    if (Directory.Exists(directory))
                        throw new IOException("Failed to delete directory " + directory.ToQuoted());
                    return null;
                }, retryCount, delayInMs);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }



        /// <summary>
        /// Delete a directory if it exists.
        /// If it fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="directory">The directory to delete</param>
        /// <param name="onlyEmpty">Only delete the directory if it's empty</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the directory doesn't exists, else the exception</returns>
        public static async Task<Exception> TryDeleteDirectoryAsync(String directory, bool onlyEmpty = true, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                return await Retry.OpAsync<Exception>(() =>
                {
                    if (!Directory.Exists(directory))
                        return null;
                    try
                    {
                        Directory.Delete(directory, !onlyEmpty);
                    }
                    catch (IOException)
                    {
                        if (onlyEmpty)
                        {
                            if (Directory.EnumerateFileSystemEntries(directory).Any())
                                return new DirectoryNotEmptyException(directory);
                        }
                        throw;
                    }
                    if (Directory.Exists(directory))
                        throw new IOException("Failed to delete directory " + directory.ToQuoted());
                    return null;
                }, retryCount, delayInMs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        static readonly Task<Exception> NullExpectionValueTask = Task.FromResult((Exception)null);

        /// <summary>
        /// Clean a directory, decide on a per file and per folder basis if it should be deleted
        /// If a delete operation fails, it's retried (up to retryCount attempts in total).
        /// </summary>
        /// <param name="directory">The directory to delete</param>
        /// <param name="doDelete">Callback used to determine if it should be deleted or not, first argument is the file or folder name, if the second argument is true, it's a folder.
        /// The callback will be executed in paralell so it must be thread safe</param>
        /// <param name="continueOnError">Continue to delete as much as possible on delete errors, will return the first exception as usual</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <returns>Null if successful or if the directory doesn't exist, else the (first) exception</returns>
        /// <remarks>Folders are processed bottom up, so a folder is only deleted (if the callback says so) after all of it's content have been processed, a folder that isn't empty is not deleted (a <see cref="DirectoryNotEmptyException"/> is returned).
        /// This method never throws, any exception is returned instead.</remarks>
        public static async Task<Exception> TryCleanDirectoryAsync(String directory, Func<String, bool, bool> doDelete, bool continueOnError = true, int retryCount = 10, int delayInMs = 100)
        {
            Exception rex = null;
            try
            {
                HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
                Stack<String> folders = new Stack<string>();
                folders.Push(directory);
                while (folders.TryPop(out directory))
                {
                    if (!Directory.Exists(directory))
                        continue;
                    if (seen.Add(directory))
                    {
                        var dirs = Directory.GetDirectories(directory, "*");
                        var dl = dirs.Length;
                        if (dl > 0)
                        {
                            folders.Push(directory);
                            for (int i = 0; i < dl; ++i)
                                folders.Push(dirs[i]);
                            continue;
                        }
                    }
                    var files = Directory.GetFiles(directory, "*");
                    var exs = await files.ConvertAsync<String, Exception>(f => doDelete(f, false) ? TryDeleteFileAsync(f, retryCount, delayInMs) : NullExpectionValueTask).ConfigureAwait(false);
                    if (continueOnError)
                    {
                        foreach (var x in exs)
                            rex = rex ?? x;
                    }
                    else
                    {
                        foreach (var x in exs)
                            if (x != null)
                                return x;
                    }
                    if (doDelete(directory, true))
                    {
                        var ex = await TryDeleteDirectoryAsync(directory, true, retryCount, delayInMs).ConfigureAwait(false);
                        if (!continueOnError)
                            if (ex != null)
                                return ex;
                        rex = rex ?? ex;
                    }
                }
                return rex;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        /// <summary>
        /// Delete all empty directories in the supplied folder, or any subfolder.
        /// </summary>
        /// <param name="directory">The directory to clean up</param>
        /// <param name="deleteDirectoryIfEmpty">If true, the directory it self will be remove if it's empty</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the clean up was successful, else the first exception (an exception is returned if the directory doesn't exist)</returns>
        public static Exception TryRemoveEmptyFolders(String directory, bool deleteDirectoryIfEmpty = true, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                var dirs = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
                Array.Sort(dirs, (a, b) => b.Length - a.Length);
                if (deleteDirectoryIfEmpty)
                    dirs = dirs.Push(directory);
                Exception ex = null;
                foreach (var dir in dirs)
                {
                    if (Directory.GetFiles(dir, "*").Length > 0)
                        continue;
                    if (Directory.GetDirectories(dir, "*").Length > 0)
                        continue;
                    var e = TryDeleteDirectory(dir, true, retryCount, delayInMs);
                    ex = ex ?? e;
                }
                return ex;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        /// <summary>
        /// Delete all empty directories in the supplied folder, or any subfolder.
        /// </summary>
        /// <param name="directory">The directory to clean up</param>
        /// <param name="deleteDirectoryIfEmpty">If true, the directory it self will be remove if it's empty</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <returns>Null if the clean up was successful, else the first exception (an exception is returned if the directory doesn't exist)</returns>
        public static async Task<Exception> TryRemoveEmptyFoldersAsync(String directory, bool deleteDirectoryIfEmpty = true, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                var dirs = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
                Array.Sort(dirs, (a, b) => b.Length - a.Length);
                if (deleteDirectoryIfEmpty)
                    dirs = dirs.Push(directory);
                Exception ex = null;
                foreach (var dir in dirs)
                {
                    if (Directory.GetFiles(dir, "*").Length > 0)
                        continue;
                    if (Directory.GetDirectories(dir, "*").Length > 0)
                        continue;
                    var e = await TryDeleteDirectoryAsync(dir, true, retryCount, delayInMs).ConfigureAwait(false);
                    ex = ex ?? e;
                }
                return ex;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        /// <summary>
        /// The functions used to determine if the file is a web file or a local file
        /// </summary>
        /// <param name="filename">The filename or url to test</param>
        /// <returns>True if the file is a web file (contains "://" anywhere, ex: "http://", "https://" or "file://"), false if null</returns>
        public static bool IsWeb(string filename)
            =>
            filename.FastIndexOf("://") >= 0;

        /// <summary>
        /// Get the filename part from an url.
        /// Example: "http://example.com/a/b.png?x=1" => "b.png"
        /// </summary>
        /// <param name="filename">An url (or a path using '/' as the separator)</param>
        /// <returns>The part after the last '/' (any query string, starting at the first '?', is removed first).
        /// If the url ends with a '/' (ex: "http://example.com/"), "index.html" is returned.
        /// If there is no '/' the input (without any query string) is returned</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        public static String ExtractWebFilename(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            var s = filename.AsSpan();
            var end = s.IndexOf('?');
            if (end < 0)
                end = s.Length;
            var t = s.Slice(0, end).LastIndexOf('/') + 1;
            if (t <= 0)
                return end == s.Length ? filename : filename.Substring(0, end);
            var l = end - t;
            return l <= 0 ? "index.html" : filename.Substring(t, l);
        }


        /*
        static void IsFolderReady(String folder)
        {
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                using (new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }
            }
        }
        */
        /// <summary>
        /// Renames / moves a folder
        /// </summary>
        /// <param name="from">The existing folder that should be moved</param>
        /// <param name="to">The desired name of the target folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <param name="replaceTo">If replace to is true and a folder named to exists, it will be removed before moving</param>
        /// <returns>An exception if there was an error, null if everything went well</returns>
        public static Exception TryMoveFolder(String from, String to, int retryCount = 10, int delayInMs = 100, bool replaceTo = false)
        {
            try
            {
                if (replaceTo)
                    TryDeleteDirectory(to, false, retryCount, delayInMs);
                Retry.Op(() => Directory.Move(from, to), retryCount, delayInMs);
                //Retry.Op(() => IsFolderReady(to), retryCount, delayInMs);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Renames / moves a folder
        /// </summary>
        /// <param name="from">The existing folder that should be moved</param>
        /// <param name="to">The desired name of the target folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <param name="replaceTo">If replace to is true and a folder named to exists, it will be removed before moving</param>
        /// <returns>An exception if there was an error, null if everything went well</returns>
        public static async Task<Exception> TryMoveFolderAsync(String from, String to, int retryCount = 10, int delayInMs = 100, bool replaceTo = false)
        {
            try
            {
                if (replaceTo)
                    await TryDeleteDirectoryAsync(to, false, retryCount, delayInMs).ConfigureAwait(false);
                await Retry.OpAsync(() => Directory.Move(from, to), retryCount, delayInMs).ConfigureAwait(false);
                //await Retry.OpAsync(() => IsFolderReady(to), retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        static void DirectoryCopy(String from, String to)
        {
            var t = Path.GetFullPath(from).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var tl = t.Length + 1;
            var files = Directory.GetFiles(t, "*", SearchOption.AllDirectories);
            // Make sure that the destination exists (even if the source folder is empty)
            Directory.CreateDirectory(to);
            foreach (var f in files)
            {
                var dest = Path.Combine(to, f.Substring(tl));
                var dir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(f, dest, true);
            }
            foreach (var f in Directory.GetDirectories(t, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
            {
                var dest = Path.Combine(to, f.Substring(tl));
                if (!Directory.Exists(dest))
                    Directory.CreateDirectory(dest);
                var di = new DirectoryInfo(dest);
                var fi = new DirectoryInfo(f);
                di.Attributes = fi.Attributes;
                di.LastWriteTimeUtc = fi.LastWriteTimeUtc;
                di.CreationTimeUtc = fi.CreationTimeUtc;
            }
        }


        /// <summary>
        /// Make a copy of a folder (recursively, existing files in the destination are overwritten).
        /// File times are kept for files (by <see cref="File.Copy(string, string, bool)"/>), attributes and times are copied for sub folders.
        /// </summary>
        /// <param name="from">The existing folder that should be copied</param>
        /// <param name="to">The desired name of the target folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <param name="replaceTo">If replace to is true and a folder named to exists, it will be removed before copying</param>
        /// <returns>An exception if there was an error, null if everything went well</returns>
        public static async Task<Exception> TryCopyFolderAsync(String from, String to, int retryCount = 10, int delayInMs = 100, bool replaceTo = false)
        {
            try
            {
                if (replaceTo)
                    await TryDeleteDirectoryAsync(to, false, retryCount, delayInMs).ConfigureAwait(false);
                await Retry.OpAsync(() => DirectoryCopy(from, to), retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


        /// <summary>
        /// Renames the target folder to backup folder then renames the new folder to target folder.
        /// </summary>
        /// <param name="targetFolder">The folder that should be replaced</param>
        /// <param name="backupFolder">The backup name of the folder that is replaced</param>
        /// <param name="newFolder">The folder that should replace the target folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <param name="replaceBak">If true and the backup folder exist, it will de deleted before swapping</param>
        /// <returns>Null if the swap was successful, else the exception (if the new folder couldn't be moved, an attempt is made to restore the target folder from the backup folder)</returns>
        public static Exception TryFolderSwap(String targetFolder, String backupFolder, String newFolder, int retryCount = 10, int delayInMs = 100, bool replaceBak = true)
        {
            Exception ex;
            bool didBak = Directory.Exists(targetFolder);
            if (didBak)
            {
                ex = TryMoveFolder(targetFolder, backupFolder, retryCount, delayInMs, replaceBak);
                if (ex != null)
                    return ex;
            }
            ex = TryMoveFolder(newFolder, targetFolder, retryCount, delayInMs);
            if (ex == null)
                return null;
            if (didBak)
                TryMoveFolder(backupFolder, targetFolder);
            return ex;
        }

        /// <summary>
        /// Renames the target folder to backup folder then renames the new folder to target folder.
        /// </summary>
        /// <param name="targetFolder">The folder that should be replaced</param>
        /// <param name="backupFolder">The backup name of the folder that is replaced</param>
        /// <param name="newFolder">The folder that should replace the target folder</param>
        /// <param name="retryCount">The maximum number of times to try the operation (a value less than 2 means that it's only tried once)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <remarks>This method never throws, any exception is returned instead.</remarks>
        /// <param name="replaceBak">If true and the backup folder exist, it will de deleted before swapping</param>
        /// <param name="onBackupCopy">if non null, the existing folder is copied to the backup folder, then this function is executed.
        /// If it returns an exception (or throws), the backup folder is deleted and the exception is returned (nothing is swapped)</param>
        /// <returns>Null if the swap was successful, else the exception (if the new folder couldn't be moved, an attempt is made to restore the target folder from the backup folder)</returns>
        public static async Task<Exception> TryFolderSwapAsync(String targetFolder, String backupFolder, String newFolder, int retryCount = 10, int delayInMs = 100, bool replaceBak = true, Func<String, Task<Exception>> onBackupCopy = null)
        {
            Exception ex;
            bool didBak = Directory.Exists(targetFolder);
            if (didBak)
            {
                if (onBackupCopy != null)
                {
                    ex = await TryCopyFolderAsync(targetFolder, backupFolder, retryCount, delayInMs, replaceBak).ConfigureAwait(false);
                    if (ex != null)
                        return ex;
                    try
                    {
                        ex = await onBackupCopy(backupFolder).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                    if (ex != null)
                    {
                        await TryDeleteDirectoryAsync(backupFolder, false, retryCount, delayInMs).ConfigureAwait(false);
                        return ex;
                    }
                }
                else
                {
                    ex = await TryMoveFolderAsync(targetFolder, backupFolder, retryCount, delayInMs, replaceBak).ConfigureAwait(false);
                    if (ex != null)
                        return ex;
                }
            }
            ex = await TryMoveFolderAsync(newFolder, targetFolder, retryCount, delayInMs, didBak | replaceBak).ConfigureAwait(false);
            if (ex == null)
                return null;
            if (didBak)
                await TryMoveFolderAsync(backupFolder, targetFolder).ConfigureAwait(false);
            return ex;
        }



        /// <summary>
        /// Allow all users to read / write a folder (adds a full control rule for "Everyone").
        /// Only does anything on Windows.
        /// </summary>
        /// <remarks>
        /// The rule is inherited using <see cref="PropagationFlags.NoPropagateInherit"/>, so it only applies to the folder and its direct files and sub folders, not to deeper levels.
        /// Granting everyone full control makes the folder writable by any local user, only use it for data that isn't security sensitive.
        /// </remarks>
        /// <param name="folder">The folder</param>
        /// <returns>True if successful (or if the folder doesn't exist or the OS isn't Windows), false on error (never throws)</returns>
        public static bool AllowAllAccess(String folder)
        {
            try
            {
                if (EnvInfo.OsPlatform.FastEquals("windows"))
                {
#pragma warning disable CA1416
                    var dInfo = new DirectoryInfo(folder);
                    if (!dInfo.Exists)
                        return true;
                    var dSecurity = dInfo.GetAccessControl();
                    var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                    var rule = new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow);
                    bool found = false;
                    foreach (FileSystemAccessRule x in dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                    {
                        if (x.IdentityReference.Value != rule.IdentityReference.Value)
                            continue;
                        if (x.AccessControlType != rule.AccessControlType)
                            continue;
                        if (x.FileSystemRights != rule.FileSystemRights)
                            continue;
                        if (x.InheritanceFlags != rule.InheritanceFlags)
                            continue;
                        if (x.PropagationFlags != rule.PropagationFlags)
                            continue;
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        dSecurity.AddAccessRule(rule);
                        dInfo.SetAccessControl(dSecurity);
                    }
#pragma warning restore CA1416
                }
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Disable content indexing of a folder
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <returns>True if successful (or if the folder doesn't exist), false on error (never throws)</returns>
        public static bool DisableIndexing(String folder)
        {
            try
            {
                var dInfo = new DirectoryInfo(folder);
                if (!dInfo.Exists)
                    return true;
                if ((dInfo.Attributes & FileAttributes.NotContentIndexed) != 0)
                    return true;
                dInfo.Attributes |= FileAttributes.NotContentIndexed;
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Create a folder if it doesn't exist and set it up for data usage (enable all access and disable indexing, see <see cref="SetupDataFolder(string)"/>).
        /// If the folder exist and <paramref name="forceSetup"/> is false nothing will be done.
        /// </summary>
        /// <param name="path">The folder to setup</param>
        /// <param name="forceSetup">If true the enabling of all access and disabling indexing will be enforced, if false it will only be applied if the folder was created (failures to set up the folder are ignored)</param>
        /// <returns>The full path, or null if the folder couldn't be created (never throws)</returns>
        public static String CreateDataFolder(String path, bool forceSetup = true)
        {
            bool isNew = forceSetup || (!Path.Exists(path));
            var ex = PathExt.EnsureFolderExist(path);
            if (ex != null)
                return null;
            path = new DirectoryInfo(path).FullName;
            if (isNew)
                SetupDataFolder(path);
            return path;

        }


        /// <summary>
        /// Set up a folder for data: disable content indexing and allow it to be accessed by everyone (see <see cref="AllowAllAccess(string)"/>)
        /// </summary>
        /// <param name="path">The folder</param>
        /// <returns>True if successful, false on error (never throws)</returns>
        public static bool SetupDataFolder(String path)
        {
            bool r = PathExt.AllowAllAccess(path);
            r &= PathExt.DisableIndexing(path);
            return r;
        }

        /// <summary>
        /// Get the directory name and file mask from a path (typically containing wild cards).
        /// Example: "D:\Temp\*.png" => "D:\Temp" and fileMask = "*.png".
        /// Handles cases like having no directory (uses Environment.CurrentDirectory), ex: "*.png", "..\*.txt", ".\*.exe", "Sub\*.jpg" and so on.
        /// </summary>
        /// <param name="pathWithMask">Path (directory and file mask), ex: "D:\Temp\*.png"</param>
        /// <param name="fileMask">Will be filled in with the file mask</param>
        /// <returns>Will return the rooted (full) directory part of the path, the directory doesn't have to exist (it may end with a directory separator)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pathWithMask"/> is null.</exception>
        /// <exception cref="ArgumentException">The directory part is invalid (ex: contains a '\0').</exception>
        /// <exception cref="PathTooLongException">The directory part is too long.</exception>
        public static String GetDirectoryAndMask(String pathWithMask, out String fileMask)
        {
            ArgumentNullException.ThrowIfNull(pathWithMask);
            var x = pathWithMask.AsSpan().LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar) + 1;
            var dir = pathWithMask.Substring(0, x);
            if (!Path.IsPathRooted(dir))
            {
                var e = Environment.CurrentDirectory;
                dir = dir.Length == 0 ? e : Path.Combine(e, dir);
            }
            dir = new DirectoryInfo(dir).FullName;
            fileMask = pathWithMask.Substring(x);
            return dir;
        }




        /// <summary>
        /// Validate that a string is a valid path to a filename and optionally that it exist
        /// </summary>
        /// <param name="filenameAndPath">The (relative or absolute) path to a file</param>
        /// <param name="mustExist">If true, the file must also exist (a directory is not a file)</param>
        /// <returns>False if null, empty, an url (contains "://"), an invalid path or if the file must exist and doesn't, else true (never throws)</returns>
        public static bool IsValidPathToFile(String filenameAndPath, bool mustExist = false)
        {
            if (String.IsNullOrEmpty(filenameAndPath))
                return false;
            if (filenameAndPath.FastIndexOf("://") >= 0)
                return false;
            //  TODO: Make smarter
            try
            {
                var fi = new FileInfo(filenameAndPath);
                return mustExist ? fi.Exists : true;
            }
            catch
            {
                return false;
            }
        }

    }

    /// <summary>
    /// Returned (not thrown) by <see cref="PathExt.TryDeleteDirectory(string, bool, int, int)"/> and <see cref="PathExt.TryDeleteDirectoryAsync(string, bool, int, int)"/> when only empty directories should be deleted and the directory isn't empty
    /// </summary>
    public sealed class DirectoryNotEmptyException : Exception
    {
        /// <summary>
        /// Create a new exception
        /// </summary>
        /// <param name="directory">The directory that isn't empty</param>
        public DirectoryNotEmptyException(String directory) : base("The directory " + directory.ToFolder() + " is not empty")
        {
            Directory = directory;
        }

        /// <summary>
        /// The directory that isn't empty
        /// </summary>
        public readonly String Directory;
    }

}