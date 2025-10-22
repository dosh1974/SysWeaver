using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class PathExt
    {

        /// <summary>
        /// Make a path relative to the executables path if it's not already rooted
        /// </summary>
        /// <param name="path">A relative or rooted path</param>
        /// <returns>A rooted path</returns>
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
        /// <returns>A rooted path</returns>
        public static String RootCurrent(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        }



        static readonly IReadOnlySet<Char> InvalidFilenameChars = GetInvalidFilenameChars().Freeze();
        static readonly IReadOnlySet<Char> InvalidFolderPathChars = GetInvalidFolderPathChars().Freeze();
        static readonly Char[] PathSplitChars = [Path.VolumeSeparatorChar, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

        static HashSet<Char> GetInvalidFilenameChars()
        {
            var t = new HashSet<char>(Path.GetInvalidFileNameChars());
            t.Add(Path.VolumeSeparatorChar);
            t.Add(Path.PathSeparator);
            t.Add(Path.DirectorySeparatorChar);
            t.Add(Path.AltDirectorySeparatorChar);
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

        static void SetSafeFilename(Span<Char> d, String s)
        {
            var l = d.Length;
            var inv = InvalidFilenameChars;
            for (int i = 0; i < l; i++)
            {
                var c = s[i];
                if (inv.Contains(c))
                    c = '_';
                d[i] = c;
            }
        }


        /// <summary>
        /// Check if a filename without a path is valid, ex: "Filename.txt".
        /// </summary>
        /// <param name="filename">The filename to test</param>
        /// <returns>True if valid, only way to make sure is to actuallty try to use it though</returns>
        public static bool IsValidFilename(String filename)
        {
            var d = InvalidFilenameChars;
            foreach (var c in filename)
            {
                if (d.Contains(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if a folder with a path is valid, ex: "C:\Windows\System32".
        /// </summary>
        /// <param name="dirPath">The path to the directory to test</param>
        /// <returns>True if valid, only way to make sure is to actuallty try to use it though</returns>
        public static bool IsValidFolderPath(String dirPath)
        {
            var d = InvalidFolderPathChars;
            int cc = 0;
            bool wasSep = false;
            var l = dirPath.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = dirPath[i];
                if (d.Contains(c))
                    return false;
                if (c == ':')
                    ++cc;
                bool isSep = (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
                if (isSep && wasSep)
                    if (i != 1)
                        return false;
                wasSep = isSep;
            }
            return cc <= 1;
        }

        /// <summary>
        /// Check if a file with a path is valid, ex: "C:\Windows\System32\Filename.txt".
        /// </summary>
        /// <param name="fullPath">The path to the file to test</param>
        /// <returns>True if valid, only way to make sure is to actuallty try to use it though</returns>
        public static bool IsValidFilePath(String fullPath)
        {
            var li = fullPath.LastIndexOfAny(PathSplitChars);
            ++li;
            var fnmae = fullPath.Substring(li);
            var path = fullPath.Substring(0, li);
            if (!IsValidFilename(fnmae))
                return false;
            return IsValidFolderPath(path);
        }


        /// <summary>
        /// Replaces all bad filename chars in the input string with '_'
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static String SafeFilename(String s) => String.IsNullOrEmpty(s) ? s : String.Create(s.Length, s, SetSafeFilename);


        /// <summary>
        /// Validate that the path doesn't contain ".", ".." or any invalid chars, including volume separator
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsValidSubPath(String s)
        {
            var p = s.Split(Path.DirectorySeparatorChar);
            var pl = p.Length;
            var inv = InvalidFilenameChars;
            for (int i = 0; i < pl; ++i)
            {
                var pp = p[i];
                if (pp == ".")
                    return false;
                if (pp == "..")
                    return false;
                var l = pp.Length;
                if (l <= 0)
                    return false;
                for (int j = 0; j < l; ++j)
                {
                    if (inv.Contains(pp[j]))
                        return false;
                }
            }
            return true;
        }



        /// <summary>
        /// Get the full directory name (fixes casing)
        /// </summary>
        /// <param name="directoryName">Name of an absolute or relative directory</param>
        /// <returns>A full directory name with correct case</returns>
        public static String GetFullDirectoryName(String directoryName)
        {
            var di = new DirectoryInfo(directoryName);
            return di.FullName;
        }

        /// <summary>
        /// Get the full filer name (fixes casing)
        /// </summary>
        /// <param name="fileName">Name of an absolute or relative file</param>
        /// <returns>A full file name with correct case</returns>
        public static String GetFullFileName(String fileName)
        {
            var di = new FileInfo(fileName);
            return di.FullName;
        }


        /// <summary>
        /// Make sure a folder exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// Make sure that the folder containing thie supplied filename exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="filename">The filename that it's containing folder must exist</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static Exception EnsureCanWriteFile(String filename, int retryCount = 10, int delayInMs = 100) => EnsureFolderExist(Path.GetDirectoryName(filename), retryCount);


        /// <summary>
        /// Make sure a folder exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// Make sure that the folder containing thie supplied filename exists.
        /// If the folder doesn't exist it will be created.
        /// If the create fails it can retry.
        /// </summary>
        /// <param name="filename">The filename that it's containing folder must exist</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <returns>Null if the folder exists, else the exception</returns>
        public static Task<Exception> EnsureCanWriteFileAsync(String filename, int retryCount = 10, int delayInMs = 100) => EnsureFolderExistAsync(Path.GetDirectoryName(filename), retryCount);


        /// <summary>
        /// Delete a file if it exists.
        /// If it fails, retry at least N times.
        /// </summary>
        /// <param name="filename">The file to delete</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
                            throw new Exception("Failed to delete file " + filename.ToQuoted());
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
        /// If it fails, retry at least N times.
        /// </summary>
        /// <param name="filename">The file to delete</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
                            throw new Exception("Failed to delete file " + filename.ToQuoted());
                    }
                }, retryCount, delayInMs).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }



        /// <summary>
        /// Delete a directory if it exists.
        /// If it fails, retry at least N times.
        /// </summary>
        /// <param name="directory">The directory to delete</param>
        /// <param name="onlyEmpty">Only delete the directory if it's empty</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
                            if (onlyEmpty)
                            {
                                if (Directory.GetFiles(directory, "*").Length > 0)
                                    return new DirectoryNotEmptyException(directory);
                                if (Directory.GetDirectories(directory, "*").Length > 0)
                                    return new DirectoryNotEmptyException(directory);
                            }
                        }
                        throw;
                    }
                    if (Directory.Exists(directory))
                        throw new Exception("Failed to delete directory " + directory.ToQuoted());
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
        /// If it fails, retry at least N times.
        /// </summary>
        /// <param name="directory">The directory to delete</param>
        /// <param name="onlyEmpty">Only delete the directory if it's empty</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
                            if (onlyEmpty)
                            {
                                if (Directory.GetFiles(directory, "*").Length > 0)
                                    return new DirectoryNotEmptyException(directory);
                                if (Directory.GetDirectories(directory, "*").Length > 0)
                                    return new DirectoryNotEmptyException(directory);
                            }
                        }
                        throw;
                    }
                    if (Directory.Exists(directory))
                        throw new Exception("Failed to delete directory " + directory.ToQuoted());
                    return null;
                }, retryCount, delayInMs).ConfigureAwait(false);
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <returns>Null if the clean up was successful, else the first exception</returns>
        public static Exception TryRemoveEmptyFolders(String directory, bool deleteDirectoryIfEmpty = true, int retryCount = 10, int delayInMs = 100)
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


        /// <summary>
        /// Delete all empty directories in the supplied folder, or any subfolder.
        /// </summary>
        /// <param name="directory">The directory to clean up</param>
        /// <param name="deleteDirectoryIfEmpty">If true, the directory it self will be remove if it's empty</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        /// <returns>Null if the clean up was successful, else the first exception</returns>
        public static async Task<Exception> TryRemoveEmptyFoldersAsync(String directory, bool deleteDirectoryIfEmpty = true, int retryCount = 10, int delayInMs = 100)
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


        /// <summary>
        /// The functions used to determine if the file is a web file or a local file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if the file is a web file</returns>
        public static bool IsWeb(string filename)
            =>
            filename.IndexOf("://") >= 0;

        /// <summary>
        /// Get the filename part from an url
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static String ExtractWebFilename(string filename)
        {
            var i = filename.IndexOf('?');
            if (i >= 0)
                filename = filename.Substring(0, i);
            var t = filename.LastIndexOf('/');
            if (t < 0)
                return filename;
            filename = filename.Substring(t + 1);
            return filename.Length <= 0 ? "index.html" : filename;
        }


        /// <summary>
        /// Renames / moves a folder
        /// </summary>
        /// <param name="from">The existing folder that should be moved</param>
        /// <param name="to">The desired name of the target folder</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static Exception TryMoveFolder(String from, String to, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                Retry.Op(() => Directory.Move(from, to), retryCount, delayInMs);
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static async Task<Exception> TryMoveFolderAsync(String from, String to, int retryCount = 10, int delayInMs = 100)
        {
            try
            {
                await Retry.OpAsync(() => Directory.Move(from, to), retryCount, delayInMs).ConfigureAwait(false);
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static Exception TryFolderSwap(String targetFolder, String backupFolder, String newFolder, int retryCount = 10, int delayInMs = 100)
        {
            var ex = TryMoveFolder(targetFolder, backupFolder, retryCount, delayInMs);
            if (ex != null)
                return ex;
            ex = TryMoveFolder(newFolder, targetFolder, retryCount, delayInMs);
            if (ex == null)
                return null;
            TryMoveFolder(backupFolder, targetFolder);
            return ex;
        }

        /// <summary>
        /// Renames the target folder to backup folder then renames the new folder to target folder.
        /// </summary>
        /// <param name="targetFolder">The folder that should be replaced</param>
        /// <param name="backupFolder">The backup name of the folder that is replaced</param>
        /// <param name="newFolder">The folder that should replace the target folder</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static async Task<Exception> TryFolderSwapAsync(String targetFolder, String backupFolder, String newFolder, int retryCount = 10, int delayInMs = 100)
        {
            var ex = await TryMoveFolderAsync(targetFolder, backupFolder, retryCount, delayInMs).ConfigureAwait(false);
            if (ex != null)
                return ex;
            ex = await TryMoveFolderAsync(newFolder, targetFolder, retryCount, delayInMs).ConfigureAwait(false);
            if (ex == null)
                return null;
            await TryMoveFolderAsync(backupFolder, targetFolder).ConfigureAwait(false);
            return ex;
        }



        /// <summary>
        /// Allow all users to read / write a folder 
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static bool AllowAllAccess(String folder)
        {
            if (!Directory.Exists(folder))
                return true;
            try
            {
                Directory.CreateDirectory(folder);
                if (EnvInfo.OsPlatform.FastEquals("windows"))
                {
#pragma warning disable CA1416
                    DirectoryInfo dInfo = new DirectoryInfo(folder);
                    DirectorySecurity dSecurity = dInfo.GetAccessControl();
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
    
    
    }

    public sealed class DirectoryNotEmptyException : Exception
    {
        public DirectoryNotEmptyException(String directory) : base("The directory " + directory.ToFolder() + " is not empty")
        {
            Directory = directory;
        }

        public readonly String Directory;
    }

}