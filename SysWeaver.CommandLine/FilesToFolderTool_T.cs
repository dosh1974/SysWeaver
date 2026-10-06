

using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    
    /// <summary>
    /// Static helpers for building "files in, folder out" command line tools, using <see cref="FilesToFolderTool{T}.Instance"/>.
    /// </summary>
    /// <remarks>
    /// The command line is "[options] SourceFiles [DestFolder]", where options are generated from the public members of the options type
    /// (see <see cref="CommandLine.GetOptions(object, CommandLine.OptionMembers)"/>).
    /// </remarks>
    public static class FilesToFolderTool
    {
        /// <summary>
        /// Process some input file(s), with an optional separate output folder
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>
        /// 0 if successful, 1 if help was requested, -1 if the command line was invalid, -2 if processing threw an exception,
        /// otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static int OnFiles<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, int> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed sequentially by an async function (the calling thread blocks until done)
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>
        /// 0 if successful, 1 if help was requested, -1 if the command line was invalid, -2 if processing threw an exception,
        /// otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static int OnFiles<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Intended to process some input file(s) in parallel, with an optional separate output folder.
        /// NOTE: currently forwards to the sequential <see cref="OnFiles{T}(string[], Func{IMessageHost, T, string, string, string, Task{int}}, Func{T, int})"/> overload, so files are processed one at a time.
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of concurrent files (currently ignored)</param>
        /// <returns>
        /// 0 if successful, 1 if help was requested, -1 if the command line was invalid, -2 if processing threw an exception,
        /// otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static int OnFilesParallel<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);


        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed sequentially and asynchronously
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>
        /// 0 if successful, 1 if help was requested, -1 if the command line was invalid, -2 if processing threw an exception,
        /// otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static Task<int> OnFilesAsync<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesAsync(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed async and in parallel
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of concurrent files, if 0 or less it's the number of CPU threads plus the thread count (so -1 gives one less than the CPU count), at least 1</param>
        /// <returns>
        /// 0 if successful, 1 if help was requested, -1 if the command line was invalid, -2 if processing threw an exception,
        /// otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static Task<int> OnFilesParallelAsync<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesParallelAsync(commandLineArgs, doOnFile, validateParams, threadCount);


    }

}


