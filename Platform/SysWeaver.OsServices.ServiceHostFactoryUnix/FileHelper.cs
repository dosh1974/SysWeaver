using System;
using System.IO;
using System.Threading;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// File operations that retry (up to 10 times with a short, increasing sleep) to tolerate concurrent access to the status (pid) files.
    /// </summary>
    static class FileHelper
    {
        const int retry = 10;

        /// <summary>
        /// Delete a file, retrying on failure.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>True if the file doesn't exist afterwards (including if it never existed), false on failure. Never throws.</returns>
        public static bool DeleteFile(String name)
        {
            try
            {
                if (!File.Exists(name))
                    return true;
                for (int i = 0; i < retry; ++i)
                {
                    try
                    {
                        File.Delete(name);
                        if (!File.Exists(name))
                            return true;
                    }
                    catch
                    {
                        var s = i + 1;
                        if (s < retry)
                            Thread.Sleep(s << 1);
                    }
                }
                return !File.Exists(name);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Write (replace) a text file, retrying on failure.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <param name="text">The text to write.</param>
        /// <returns>True if written, false if all attempts failed. Never throws.</returns>
        public static bool WriteText(String name, String text)
        {
            for (int i = 0; i < retry; ++i)
            {
                try
                {
                    File.WriteAllText(name, text);
                    return true;
                }
                catch
                {
                    var s = i + 1;
                    if (s < retry)
                        Thread.Sleep(s << 1);
                }
            }
            return false;
        }


        /// <summary>
        /// Read a text file, retrying on failure.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>The text, or null if the file doesn't exist or couldn't be read. Never throws.</returns>
        public static String ReadText(String name)
        {
            try
            {
                if (!File.Exists(name))
                    return null;
                for (int i = 0; i < retry; ++i)
                {
                    try
                    {
                        return FileExt.ReadText(name);
                    }
                    catch
                    {
                        var s = i + 1;
                        if (s < retry)
                            Thread.Sleep(s << 2);
                    }
                    if (!File.Exists(name))
                        break;
                }
            }
            catch
            {
            }
            return null;
        }

    }

}
