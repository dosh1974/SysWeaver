using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    public static class Sports
    {
        public const String Group = "Sports";

        public static bool TryGet(String name, out Info info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Info>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Info> Tags = new Dictionary<string, Info>(StringComparer.Ordinal);

        static void Reg(String name, String desc, String[] keywords)
        {
            var key = name.FastToLower();
            var tag = new Info(name, desc, Group, TagSports, false);
            var t = Tags;
            t.Add(key, tag);
            if (key.Length > 2)
                AllInfo.TryAdd(key, tag, false, false);
            var kl = keywords.Length;
            for (int i = 2; i < kl; ++ i)
            {
                key = keywords[i].FastToLower();
                t.Add(key, tag);
                if (key.Length > 2)
                    AllInfo.TryAdd(key, tag, false, false);
            }
        }

        static readonly Info[] TagSports = [new Info("Sport", "Everything related to a sport", Group, null)];

        static Sports()
        {
            var tag = TagSports[0];
            AllInfo.TryAdd(tag.Name, tag, false, true);

            var data = DataHelper.GetData<String[]>("Sports");
            var l = data.Length;
            for (int i = 0; i < l; ++ i)
            {
                var x = data[i].Split(';');
                var name = x[0];
                var desc = x[1];
                var kl = x.Length;
                if (kl > 2)
                    desc = String.Concat("Also known as: ", String.Join(", ", x, 2, kl - 2), ".\n", desc);
                Reg(name, desc, x);
            }
        }

        #endregion//Setup


    }

}