using System;
using System.IO;
using System.Threading;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    static class FileHelper
    {
        const int retry = 10;

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
                        return File.ReadAllText(name);
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
