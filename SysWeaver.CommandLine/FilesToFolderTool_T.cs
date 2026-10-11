

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
    /// <para>The processing methods return a process exit code (to return from Main) following the common convention 0 = success, non-zero (1-255) = failure:
    /// 0 = success (also when help was requested with "-?" or "-help"), 1 = processing threw an exception, 2 = invalid command line (or the validation threw),
    /// else the first non-zero value returned by the validation or file callback (passed through unchanged, so return values in 1-255 from them: Linux truncates exit codes to 8 bits and Windows "if errorlevel 1" ignores negative codes).</para>
    /// </remarks>
    public static class FilesToFolderTool
    {
        /// <summary>
        /// Exit code: success (also returned when help was requested).
        /// </summary>
        public const int ExitSuccess = 0;
        /// <summary>
        /// Exit code: generic error, processing (a file callback) threw an exception.
        /// </summary>
        public const int ExitGenericError = 1;
        /// <summary>
        /// Exit code: invalid command line arguments (unknown option, no source files argument, a source folder that doesn't exist etc), or the validation callback threw an exception.
        /// A source name / mask that matches no file in an existing folder is not an error (nothing is processed, the result is <see cref="ExitSuccess"/>).
        /// </summary>
        public const int ExitInvalidArguments = 2;


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
        /// The process exit code: 0 (<see cref="ExitSuccess"/>) if successful or if help was requested, 2 (<see cref="ExitInvalidArguments"/>) if the command line was invalid,
        /// 1 (<see cref="ExitGenericError"/>) if processing threw an exception, otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
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
        /// The process exit code: 0 (<see cref="ExitSuccess"/>) if successful or if help was requested, 2 (<see cref="ExitInvalidArguments"/>) if the command line was invalid,
        /// 1 (<see cref="ExitGenericError"/>) if processing threw an exception, otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static int OnFiles<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s) in parallel, with an optional separate output folder (see <see cref="FilesToFolderTool{T}.OnFilesParallel(string[], Func{IMessageHost, T, string, string, string, Task{int}}, Func{T, int}, int)"/>).
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">
        /// Called once per input file with (message host, options, full source path, source path relative to its search folder, destination folder).
        /// The destination folder is created if needed; it is the source file's folder when no destination argument is given, else the destination plus the relative sub folder.
        /// A non-zero return value stops the processing and becomes the return value.
        /// </param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of concurrent files, if 0 or less it's computed from the number of CPU threads (see <see cref="FilesToFolderTool{T}.OnFilesParallel(string[], Func{IMessageHost, T, string, string, string, Task{int}}, Func{T, int}, int)"/>)</param>
        /// <returns>
        /// The process exit code: 0 (<see cref="ExitSuccess"/>) if successful or if help was requested, 2 (<see cref="ExitInvalidArguments"/>) if the command line was invalid,
        /// 1 (<see cref="ExitGenericError"/>) if processing threw an exception, otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static int OnFilesParallel<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesParallel(commandLineArgs, doOnFile, validateParams, threadCount);


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
        /// The process exit code: 0 (<see cref="ExitSuccess"/>) if successful or if help was requested, 2 (<see cref="ExitInvalidArguments"/>) if the command line was invalid,
        /// 1 (<see cref="ExitGenericError"/>) if processing threw an exception, otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
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
        /// The process exit code: 0 (<see cref="ExitSuccess"/>) if successful or if help was requested, 2 (<see cref="ExitInvalidArguments"/>) if the command line was invalid,
        /// 1 (<see cref="ExitGenericError"/>) if processing threw an exception, otherwise the non-zero value returned by <paramref name="validateParams"/> or <paramref name="doOnFile"/>.
        /// </returns>
        public static Task<int> OnFilesParallelAsync<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesParallelAsync(commandLineArgs, doOnFile, validateParams, threadCount);


    }

}


