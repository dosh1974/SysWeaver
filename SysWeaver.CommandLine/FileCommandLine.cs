using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SysWeaver
{

    /// <summary>
    /// Option arguments (and parsers) that accept existing file names, with wildcard support.
    /// </summary>
    public static class FileCommandLineOptionArgument
    {
        /// <summary>
        /// Parses a file name or mask that must match exactly one existing file.
        /// </summary>
        /// <param name="value">A file name or mask (see <see cref="ParseMultipleExisting(string, bool)"/>), relative names are relative to the current directory.</param>
        /// <returns>The full path of the matching file.</returns>
        /// <exception cref="ArgumentException">No file, or more than one file, matches.</exception>
        public static String ParseSingleExisting(String value)
        {
            var files = ParseMultipleExisting(value).ToList();
            var fl = files.Count;
            if (fl <= 0)
                throw new ArgumentException(String.Concat("There is no existing file matching ", value.ToQuoted(), '!'), nameof(value));
            if (fl > 1)
                throw new ArgumentException(String.Concat("More than one file is matching ", value.ToQuoted(), '!', Environment.NewLine, String.Join(Environment.NewLine, files.Select(x => x.Key.ToQuoted()))), nameof(value));
            return files[0].Key;
        }

        static readonly Char[] SplitChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar];

        /// <summary>
        /// Parses one or more file names or masks into the list of existing files they match.
        /// </summary>
        /// <param name="values">
        /// File names or masks separated by ';'. The file name part may use the wildcards * and ?.
        /// A mask ending with '+' (ex: "src/*.cs+") searches sub directories recursively.
        /// Relative paths are resolved against the current directory.
        /// </param>
        /// <param name="allowSequences">If true, each found file is treated as the first in a numbered sequence and following files (ex: "Frame_1.png", "Frame_2.png") are added while they exist (see <c>StringTools.CountUp</c>).</param>
        /// <returns>
        /// The matching files sorted by full path (ordinal, case insensitive).
        /// The key is the full path and the value is the path relative to the searched folder (includes sub folders for recursive searches).
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">The folder of a mask doesn't exist.</exception>
        public static IList<KeyValuePair<String, String>> ParseMultipleExisting(String values, bool allowSequences = false)
        {
            var fileMasks = values.Split(';');
            Dictionary<String, String> allFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fileMask in fileMasks)
            {
                var value = fileMask.Trim();
                if (!String.IsNullOrEmpty(value))
                {
                    var l = value.LastIndexOfAny(SplitChars) + 1;
                    var folder = l <= 0 ? Environment.CurrentDirectory : Path.GetFullPath(value.Substring(0, l));
                    var mask = value.Substring(l);
                    bool rec = false;
                    if (mask.EndsWith('+'))
                    {
                        rec = true;
                        mask = mask.Substring(0, mask.Length - 1);
                    }
                    var fl = folder.Length;
                    if (!folder.EndsWith(Path.DirectorySeparatorChar))
                        ++fl;
                    foreach (var f in Directory.GetFiles(folder, mask, rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        if (!allFiles.TryAdd(f, f.Substring(fl)))
                            continue;
                        if (allowSequences)
                        {
                            var test = StringTools.CountUp(f);
                            while (test != f)
                            {
                                if (!File.Exists(test))
                                    break;
                                if (!allFiles.TryAdd(test, test.Substring(fl)))
                                    break;
                                test = StringTools.CountUp(test);
                            }
                        }
                    }

                }
            }
            var ao = allFiles.ToList();
            ao.Sort((a, b) => String.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return ao;
        }

        /// <summary>
        /// Make an option argument that must be a single existing file (parsed with <see cref="ParseSingleExisting(string)"/> into a full path).
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text</param>
        /// <returns>An option argument</returns>
        public static CommandLineOptionArgument SingleExisting(String name, String defaultValue = null, String helpText = null) => CommandLineOptionArgument.Make(name, typeof(String), x => (Object)ParseSingleExisting(x), defaultValue, helpText);
        /// <summary>
        /// Make an option argument that accepts existing files (parsed with <see cref="ParseMultipleExisting(string, bool)"/> into an <see cref="IList{T}"/> of full path/relative path pairs).
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text</param>
        /// <returns>An option argument</returns>
        /// <remarks>The declared <see cref="CommandLineOptionArgument.Type"/> is <see cref="String"/> although the parsed value is a list.</remarks>
        public static CommandLineOptionArgument MultipleExisting(String name, String defaultValue = null, String helpText = null) => CommandLineOptionArgument.Make(name, typeof(String), x => (Object)ParseMultipleExisting(x), defaultValue, helpText);
    }

    /// <summary>
    /// Positional arguments that accept existing file names, with wildcard support.
    /// </summary>
    public static class FileCommandLineArgument
    {

        /// <summary>
        /// Make an argument that must be a single existing file (parsed with <see cref="FileCommandLineOptionArgument.ParseSingleExisting(string)"/> into a full path).
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="optional">Set to true if the argument is optional</param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text, null for a default text</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument SingleExisting(String name, bool optional = false, String defaultValue = null, String helpText = null) => CommandLineArgument.Make(name, typeof(String), x => (Object)FileCommandLineOptionArgument.ParseSingleExisting(x), optional, defaultValue, helpText ?? "A single existing file");
        /// <summary>
        /// Make an argument that accepts existing files (parsed with <see cref="FileCommandLineOptionArgument.ParseMultipleExisting(string, bool)"/> into an <see cref="IList{T}"/> of full path/relative path pairs).
        /// </summary>
        /// <param name="name">Name of the argument</param>
        /// <param name="optional">Set to true if the argument is optional</param>
        /// <param name="defaultValue">Text shown as the default value tag, or null</param>
        /// <param name="helpText">Optional help text, null for a default text describing the mask syntax</param>
        /// <param name="allowSequences">If true, found files are treated as the start of numbered file sequences</param>
        /// <returns>An argument</returns>
        public static CommandLineArgument MultipleExisting(String name, bool optional = false, String defaultValue = null, String helpText = null, bool allowSequences = false) => CommandLineArgument.Make(name, typeof(String), x => (Object)FileCommandLineOptionArgument.ParseMultipleExisting(x, allowSequences), optional, defaultValue, helpText ?? "Existing files, can use wildcards (*,?), if a file ends in +, a recursive search will be done. Multiple files/masks can be supplied, separated by a ;");

    }
}
