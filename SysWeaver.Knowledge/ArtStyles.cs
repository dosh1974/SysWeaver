using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    public sealed class ArtStyle : Info, IGoogleInfo
    {
        internal ArtStyle(string name, string desc, DateOnly from, DateOnly to, long pop, Info[] parent) : base(name, desc, ArtStyles.Group, parent, false)
        {
            From = from;
            To = to;
            Pop = pop;
        }
        /// <summary>
        /// When the art style was introduced
        /// </summary>
        public readonly DateOnly From;

        /// <summary>
        /// When the art style was considered over
        /// </summary>
        public readonly DateOnly To;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }

    }

    public static class ArtStyles
    {
        public const String Group = "Art styles";

        public static bool TryGet(String name, out ArtStyle info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, ArtStyle>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, ArtStyle> Tags = new Dictionary<string, ArtStyle>(StringComparer.Ordinal);

        static void Reg(String name, String born, String died, String desc, String pop)
        {
            if (name.Length < 6)
                return;
            var key = name.FastToLower();
            var tag = new ArtStyle(name, desc, DataHelper.ParseDate(born), DataHelper.ParseDate(died), long.Parse(pop), TagArts);
            Tags.Add(key, tag);
            AllInfo.TryAdd(key, tag, false, false);
        }

        static readonly Info[] TagArts = [new Info("Art style", "An artistic style", Group, null)];

        static ArtStyles()
        {
            var data = DataHelper.GetData<String[]>("Arts");
            var l = data.Length;
            for (int i = 0; i < l; i += 5)
                Reg(data[i], data[i + 1], data[i + 2], data[i + 3], data[i + 4]);
        }

        #endregion//Setup


    }

}