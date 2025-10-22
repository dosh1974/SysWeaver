

using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    
    /// <summary>
    /// Process files, with options
    /// </summary>
    public static class FilesToFolderTool
    {
        /// <summary>
        /// Process some input file(s), with an optional separate output folder
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if successful, positive for wrong args and negative for errors</returns>
        public static int OnFiles<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, int> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed async
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if successful, positive for wrong args and negative for errors</returns>
        public static int OnFiles<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed async and in parallel
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of threads to execute, if 0 or less it's the number of CPU threads + the thread count</param>
        /// <returns>0 if successful, positive for wrong args and negative for errors</returns>
        public static int OnFilesParallel<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFiles(commandLineArgs, doOnFile, validateParams);


        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed async
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if successful, positive for wrong args and negative for errors</returns>
        public static Task<int> OnFilesAsync<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesAsync(commandLineArgs, doOnFile, validateParams);

        /// <summary>
        /// Process some input file(s), with an optional separate output folder, files are processed async and in parallel
        /// </summary>
        /// <typeparam name="T">The type containing optional options</typeparam>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of threads to execute, if 0 or less it's the number of CPU threads + the thread count</param>
        /// <returns>0 if successful, positive for wrong args and negative for errors</returns>
        public static Task<int> OnFilesParallelAsync<T>(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1) where T : class, new() => FilesToFolderTool<T>.Instance.OnFilesParallelAsync(commandLineArgs, doOnFile, validateParams, threadCount);


    }

}


