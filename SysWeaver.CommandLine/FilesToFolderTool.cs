

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Quick and easy shell for creating tools that requires a input file and some optional destination folder
    /// </summary>
    /// <typeparam name="T">A type with optional options</typeparam>
    public sealed class FilesToFolderTool<T> where T : class, new()
    {
        /// <summary>
        /// Name of source files
        /// </summary>
        public String SourceFilesName = "SourceFiles";
        /// <summary>
        /// Optional help text
        /// </summary>
        public String SourceFilesHelp = null;
        /// <summary>
        /// If true, input files will be treated as the first in a sequence, i.e "Frame_0.png" will result in "Frame_0.png", "Frame_1.png" etc will be processed
        /// </summary>
        public bool AllowSourceSequence;

        /// <summary>
        /// Name of the destination folder argument
        /// </summary>
        public String DestFolderName = "DestFolder";
        /// <summary>
        /// Help for the destination folder argument
        /// </summary>
        public String DestFolderHelp;


        public static FilesToFolderTool<T> Instance = new FilesToFolderTool<T>();

        static String MakeDir(String dest, KeyValuePair<String, String> source)
        {
            String d = dest;
            if (d == null)
            {
                d = Path.GetDirectoryName(source.Key);
            }else
            {
                var val = Path.GetDirectoryName(source.Value);
                if (!String.IsNullOrEmpty(val))
                    d = Path.Combine(d, val);
            }
            if (!Directory.Exists(d))
            {
                try
                {
                    Directory.CreateDirectory(d);
                }
                catch
                {
                    if (!Directory.Exists(d))
                        throw;
                } 
            }
            return d;
        }



        /// <summary>
        /// Process the files specified on the command line with the specified options and destination folder
        /// </summary>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if sucessfull or the error code</returns>
        public int OnFiles(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, int> doOnFile, Func<T, int> validateParams = null)
        {
            return InternalOnFiles(commandLineArgs, (msg, opt, files, dest) =>
            {
                foreach (var src in files)
                {
                    var d = MakeDir(dest, src);
                    var r = doOnFile(msg, opt, src.Key, src.Value, d);
                    if (r != 0)
                        return r;
                }
                return 0;
            }, validateParams);
        }

        /// <summary>
        /// Process the files specified on the command line with the specified options and destination folder, files are processed async
        /// </summary>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if sucessfull or the error code</returns>
        public int OnFiles(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null)
        {
            return InternalOnFiles(commandLineArgs, (msg, opt, files, dest) =>
            {
                var t = Task.Run(async () =>
                {
                    foreach (var src in files)
                    {
                        var d = MakeDir(dest, src);
                        var r = await doOnFile(msg, opt, src.Key, src.Value, d).ConfigureAwait(false);
                        if (r != 0)
                            return r;
                    }
                    return 0;
                });
                t.ConfigureAwait(false);
                return t.Result;
            }, validateParams);
        }

        /// <summary>
        /// Process the files specified on the command line with the specified options and destination folder, files are processed async and in parallel
        /// </summary>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of threads to execute, if 0 or less it's the number of CPU threads + the thread count</param>
        /// <returns>0 if sucessfull or the error code</returns>
        public int OnFilesParallel(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1)
        {
            var count = threadCount > 0 ? threadCount : Environment.ProcessorCount - threadCount;
            if (count < 1)
                count = 1;
            var popt = new ParallelOptions
            {
                MaxDegreeOfParallelism = count,
            };
            return InternalOnFiles(commandLineArgs, (msg, opt, files, dest) =>
            {
                int ret = 0;
                Parallel.ForEachAsync(files, popt, async (src, ct) =>
                {
                    if (ret != 0)
                        return;
                    var d = MakeDir(dest, src);
                    var r = await doOnFile(msg, opt, src.Key, src.Value, d).ConfigureAwait(false);
                    if (r != 0)
                        InterlockedEx.Min(ref ret, r);
                }); 
                return ret;
            }, validateParams);
        }




        /// <summary>
        /// Process the files specified on the command line with the specified options and destination folder, files are processed async
        /// </summary>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <returns>0 if sucessfull or the error code</returns>
        public Task<int> OnFilesAsync(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null)
        {
            return InternalOnFilesAsync(commandLineArgs, async (msg, opt, files, dest) =>
            {
                foreach (var src in files)
                {
                    var d = MakeDir(dest, src);
                    var r = await doOnFile(msg, opt, src.Key, src.Value, d).ConfigureAwait(false);
                    if (r != 0)
                        return r;
                }
                return 0;
            }, validateParams);
        }

        /// <summary>
        /// Process the files specified on the command line with the specified options and destination folder, files are processed async and in parallel
        /// </summary>
        /// <param name="commandLineArgs">The command line args (as passed to main)</param>
        /// <param name="doOnFile">The custom function to execute once for every frame, if the return value isn't zero, processing of files is aborted and an error is displayed</param>
        /// <param name="validateParams">Optionally validate (and do precomputations) the params after they have been read, return non-zero to signal an error or throw an exception</param>
        /// <param name="threadCount">Maximum number of threads to execute, if 0 or less it's the number of CPU threads + the thread count</param>
        /// <returns>0 if sucessfull or the error code</returns>
        public Task<int> OnFilesParallelAsync(String[] commandLineArgs, Func<IMessageHost, T, String, String, String, Task<int>> doOnFile, Func<T, int> validateParams = null, int threadCount = -1)
        {
            var count = threadCount > 0 ? threadCount : Environment.ProcessorCount + threadCount;
            if (count < 1)
                count = 1;
            var popt = new ParallelOptions
            {
                MaxDegreeOfParallelism = count,
            };
            return InternalOnFilesAsync(commandLineArgs, async (msg, opt, files, dest) =>
            {
                int ret = 0;
                await Parallel.ForEachAsync(files, popt, async (src, ct) =>
                {
                    if (ret != 0)
                        return;
                    var d = MakeDir(dest, src);
                    var r = await doOnFile(msg, opt, src.Key, src.Value, d).ConfigureAwait(false);
                    if (r != 0)
                        InterlockedEx.Min(ref ret, r);
                }).ConfigureAwait(false);
                return ret;
            }, validateParams);
        }



        int InternalOnFiles(String[] commandLineArgs, Func<IMessageHost, T, IEnumerable<KeyValuePair<String, String>>, String, int> doOnFile, Func<T, int> validateParams)
        {
            var msg = new MessageHost(ConsoleMessageHandler.GetSync());
            CommandLineArgument[] validArgs =
            [
                FileCommandLineArgument.MultipleExisting(SourceFilesName, false, null, SourceFilesHelp, AllowSourceSequence),
                CommandLineArgument.Make<String>(DestFolderName, true, DestFolderHelp ?? "<Same as source file>"),
            ];
            CommandLine cmd;
            T opt;
            try
            {
                cmd = CommandLine.ParseObject(out opt, commandLineArgs, validArgs, CommandLine.OptionMembers.All);
                if (cmd == null)
                {
                    foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                        msg.AddMessage(t);
                    return 1;
                }
                if (validateParams != null)
                {
                    var r = validateParams(opt);
                    if (r != 0)
                    {
                        foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                            msg.AddMessage(t);
                        return r;
                    }
                }
            }
            catch (Exception ex)
            {
                msg.AddMessage(ex.Message, MessageLevels.Error);
                foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                    msg.AddMessage(t);
                return -1;
            }

            var files = cmd.Arguments[0].Item2 as IEnumerable<KeyValuePair<String, String>>;
            var dest = cmd.Arguments.FirstOrDefault(x => x.Item1.Name == DestFolderName)?.Item2 as String;
            try
            {
                return doOnFile(msg, opt, files, dest);
            }
            catch (Exception ex)
            {
                msg.AddMessage(ex.Message, MessageLevels.Error);
                return -2;
            }
        }

        async Task<int> InternalOnFilesAsync(String[] commandLineArgs, Func<IMessageHost, T, IEnumerable<KeyValuePair<String, String>>, String, Task<int>> doOnFile, Func<T, int> validateParams)
        {
            var msg = new MessageHost(ConsoleMessageHandler.GetSync());
            CommandLineArgument[] validArgs =
            [
                FileCommandLineArgument.MultipleExisting(SourceFilesName, false, null, SourceFilesHelp, AllowSourceSequence),
                CommandLineArgument.Make<String>(DestFolderName, true, DestFolderHelp ?? "<Same as source file>"),
            ];
            CommandLine cmd;
            T opt;
            try
            {
                cmd = CommandLine.ParseObject(out opt, commandLineArgs, validArgs, CommandLine.OptionMembers.All);
                if (cmd == null)
                {
                    foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                        msg.AddMessage(t);
                    return 1;
                }
                if (validateParams != null)
                {
                    var r = validateParams(opt);
                    if (r != 0)
                    {
                        foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                            msg.AddMessage(t);
                        return r;
                    }
                }
            }
            catch (Exception ex)
            {
                msg.AddMessage(ex.Message, MessageLevels.Error);
                foreach (var t in CommandLine.SyntaxObject<T>(validArgs, CommandLine.OptionMembers.All))
                    msg.AddMessage(t);
                return -1;
            }
            var files = cmd.Arguments[0].Item2 as IEnumerable<KeyValuePair<String, String>>;
            var dest = cmd.Arguments.FirstOrDefault(x => x.Item1.Name == DestFolderName)?.Item2 as String;
            try
            {
                return await doOnFile(msg, opt, files, dest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                msg.AddMessage(ex.Message, MessageLevels.Error);
                return -2;
            }
        }

    }

}


