using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    /// <summary>
    /// Knowledge about sports (names and alternative names)
    /// </summary>
    public static class Sports
    {
        /// <summary>
        /// The group (category) name of all sport info
        /// </summary>
        public const String Group = "Sports";

        /// <summary>
        /// Try to get info about a sport
        /// </summary>
        /// <param name="name">The name (or alternative name) of the sport, any case</param>
        /// <param name="info">The info if found</param>
        /// <returns>True if info was found</returns>
        public static bool TryGet(String name, out Info info) => Tags.TryGetValueLower(name, out info);

        /// <summary>
        /// All keyword/info pairs (the keys are lower case)
        /// </summary>
        public static IEnumerable<KeyValuePair<String, Info>> All => Tags;

        /// <summary>
        /// The number of keywords
        /// </summary>
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