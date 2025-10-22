using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SysWeaver
{

    public static class FileCommandLineOptionArgument
    {
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
                        allFiles.Add(f, f.Substring(fl));
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

        public static CommandLineOptionArgument SingleExisting(String name, String defaultValue = null, String helpText = null) => CommandLineOptionArgument.Make(name, typeof(String), x => (Object)ParseSingleExisting(x), defaultValue, helpText);
        public static CommandLineOptionArgument MultipleExisting(String name, String defaultValue = null, String helpText = null) => CommandLineOptionArgument.Make(name, typeof(String), x => (Object)ParseMultipleExisting(x), defaultValue, helpText);
    }

    public static class FileCommandLineArgument
    {

        public static CommandLineArgument SingleExisting(String name, bool optional = false, String defaultValue = null, String helpText = null) => CommandLineArgument.Make(name, typeof(String), x => (Object)FileCommandLineOptionArgument.ParseSingleExisting(x), optional, defaultValue, helpText ?? "A single existing file");
        public static CommandLineArgument MultipleExisting(String name, bool optional = false, String defaultValue = null, String helpText = null, bool allowSequences = false) => CommandLineArgument.Make(name, typeof(String), x => (Object)FileCommandLineOptionArgument.ParseMultipleExisting(x, allowSequences), optional, defaultValue, helpText ?? "Existing files, can use wildcards (*,?), if a file ends in +, a recursive search will be done. Multiple files/masks can be supplied, separated by a ;");

    }
}
