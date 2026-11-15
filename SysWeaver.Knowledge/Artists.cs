using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    /// <summary>
    /// Info about an artist
    /// </summary>
    public sealed class Artist : Info, IGoogleInfo
    {
        internal Artist(string name, string desc, DateOnly born, DateOnly died, long pop, Info[] parent) : base(name, desc, Artists.Group, parent, true)
        {
            Born = born;
            Died = died;
            Pop = pop;
        }
        /// <summary>
        /// When the artist was born (or Min if unknown)
        /// </summary>
        public readonly DateOnly Born;

        /// <summary>
        /// When the artist died (or Min if still alive, or unknown)
        /// </summary>
        public readonly DateOnly Died;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }

    }

    /// <summary>
    /// Knowledge about artists
    /// </summary>
    public static class Artists
    {
        /// <summary>
        /// The group (category) name of all artist info
        /// </summary>
        public const String Group = "Artists";

        /// <summary>
        /// Try to get info about an artist
        /// </summary>
        /// <param name="name">The name of the artist, any case</param>
        /// <param name="info">The info if found</param>
        /// <returns>True if info was found</returns>
        public static bool TryGet(String name, out Artist info) => Tags.TryGetValueLower(name, out info);

        /// <summary>
        /// All keyword/info pairs (the keys are lower case)
        /// </summary>
        public static IEnumerable<KeyValuePair<String, Artist>> All => Tags;

        /// <summary>
        /// The number of keywords
        /// </summary>
        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Artist> Tags = new Dictionary<string, Artist>(StringComparer.Ordinal);

        static void Reg(String name, String born, String died, String desc, String pop)
        {
            if (name.Length < 6)
                return;
            var key = name.FastToLower();
            var tag = new Artist(name, desc, DataHelper.ParseDate(born), DataHelper.ParseDate(died), long.Parse(pop), TagArtists);
            Tags.TryAdd(key, tag);
            AllInfo.TryAdd(key, tag, true, false);
        }

        static readonly Info[] TagArtists = [new Info("Artists", "An artist", Group, null)];

        static Artists()
        {
            var data = DataHelper.GetData<String[]>("Artists");
            var l = data.Length;
            for (int i = 0; i < l; i += 5)
            {
                var name = data[i];
                if (name.Length <= 3)
                    continue;
                Reg(name, data[i + 1], data[i + 2], data[i + 3], data[i + 4]);
            }
        }

        #endregion//Setup


    }

}