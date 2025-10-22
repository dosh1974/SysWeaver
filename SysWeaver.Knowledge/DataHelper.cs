using System;
using System.Reflection;
using SysWeaver.Compression;
using SysWeaver.Serialization;

namespace SysWeaver.Knowledge
{
    internal static class DataHelper
    {
        static readonly ICompType Comp = CompManager.GetFromHttp("br");
        static readonly ISerializerType Ser = SerManager.Get("json");
        static readonly Assembly Asm = typeof(DataHelper).Assembly;
        static readonly String Base = typeof(DataHelper).Namespace + ".data.";

        public static T GetData<T>(String name)
        {
            using var s = Asm.GetManifestResourceStream(String.Join(name, Base, ".db"));
            var data = Comp.GetDecompressed(s);
            return Ser.Create<T>(data);
        }


        public static DateOnly ParseDate(String s)
        {
            if (String.IsNullOrEmpty(s))
                return DateOnly.MinValue;
            DateOnly.TryParse(s, out var x);
            return x;
        }

    }

}

