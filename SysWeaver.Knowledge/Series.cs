using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    public sealed class Serie : Info
    {
        internal Serie(string name, string desc, int yearStart, int yearEnd, String rating, String plot, String length, Info[] parent) : base(name, desc, Series.Group, parent, true)
        {
            YearStart = yearStart;
            YearEnd = yearEnd;
            Rating = rating;
            Plot = plot;
        }

        /// <summary>
        /// What year the serie was released
        /// </summary>
        public readonly int YearStart;

        /// <summary>
        /// What year the serie was released
        /// </summary>
        public readonly int YearEnd;

        /// <summary>
        /// The rating of the serie
        /// </summary>
        public readonly String Rating;

        /// <summary>
        /// The plot of the serie
        /// </summary>
        public readonly String Plot;

        /// <summary>
        /// The length of the serie
        /// </summary>
        public readonly String Length;
    }


    public static class Series
    {
        public const String Group = "Series";

        public static bool TryGet(String name, out Serie info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Serie>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Serie> Tags = new Dictionary<string, Serie>(StringComparer.Ordinal);

        static void Reg(Dictionary<String, Info> parts, String name, int yearStart, int yearEnd, String rating, String length, String cat, String plot)
        {
            HashSet<Info> seen = new HashSet<Info>();
            List<Info> tags = new List<Info>();
            void Add(Info x)
            {
                if (seen.Add(x))
                    tags.Add(x);
            }
            foreach (var x in MediaCat.AllMedia)
                Add(x);
            Add(TagSerie);
            var cats = cat.Split(',');
            foreach (var x in cats)
            {
                var n = x.Trim();
                if (n.Length <= 0)
                    continue;
                Add(MediaCat.Get(n));
            }
            /*
            if (Years.TryGet(yearStart.ToString(), out var yi))
            {
                Add(yi);
                foreach (var x in yi.Parents?.Nullable())
                    Add(x);
            }
            if (yearEnd > yearStart)
            {
                if (Years.TryGet(yearEnd.ToString(), out yi))
                {
                    Add(yi);
                    foreach (var x in yi.Parents?.Nullable())
                        Add(x);
                }
            }
            if (!String.IsNullOrEmpty(rating))
            {
                var x = Info.GetInfo(out var isNew, "Rated " + rating, "The media rating " + rating, MediaCat.Group, MediaCat.AllMedia);
                if (isNew)
                    AllInfo.TryAdd(x.Name, x, false, false);
                Add(x);
            }*/
            var np = name.Split(':');
            var xl = np.Length;
            List<String> extraKeys = new List<String>();
            if (xl > 1)
            {
                foreach (var t in np)
                {
                    var tt = t.Trim();
                    if (tt.Length <= 0)
                        continue;
                    var k = tt.FastToLower();
                    if (parts.TryGetValue(k, out var x))
                        Add(x);
                    else
                        extraKeys.Add(k);
                }
                if (np.Length == extraKeys.Count)
                    extraKeys.Clear();
            }
            String desc = String.Concat("The ", cats[0], ' ', name);
            if (yearStart == yearEnd)
            {
                desc = String.Concat(desc, "\nReleased: ", yearStart, "\n");
            }
            else
            {
                if (yearEnd > yearStart)
                    desc = String.Concat(desc, "\nRunning: ", yearStart, " - ", yearEnd, "\n");
                else
                    desc = String.Concat(desc, "\nRunning since: ", yearStart, "\n");
            }
            if (!String.IsNullOrEmpty(length))
                desc = String.Concat(desc, "Length: ", length, "\n");
            if (!String.IsNullOrEmpty(cat))
                desc = String.Concat(desc, "Cathegories: ", String.Join(", ", cat.Split(',')), "\n");
            if (!String.IsNullOrEmpty(rating))
                desc = String.Concat(desc, "Rating: ", rating, "\n");
            if (!String.IsNullOrEmpty(plot))
                desc = String.Concat(desc, "Plot:\n", plot);
            var m = new Serie(name, desc, yearStart, yearEnd, rating, plot, length, tags.ToArray());
            var tagName = name.FastToLower();
            Tags.TryAdd(tagName, m);
            AllInfo.TryAdd(tagName, m, true, false);
            foreach (var x in extraKeys)
            {
                Tags.TryAdd(x, m);
                AllInfo.TryAdd(x, m, true, false);
            }
        }

        static readonly Info TagSerie = Info.GetInfo(out var _, "Series", "A series", Group, MediaCat.AllMedia);

        static Series()
        {
            AllInfo.TryAdd(TagSerie.Name, TagSerie, false, true);
            var data = DataHelper.GetData<String[]>("Series");
            var l = data.Length;
            Dictionary<String, Tuple<int, String>> counts = new Dictionary<string, Tuple<int, string>>(StringComparer.Ordinal);
            for (int i = 0; i < l; i += 6)
            {
                var name = data[i];
                var x = name.Split(':');
                var xl = x.Length;
                if (xl <= 1)
                    continue;
                foreach (var t in x)
                {
                    var tt = t.Trim();
                    if (tt.Length <= 0)
                        continue;
                    var k = tt.FastToLower();
                    counts.TryGetValue(k, out var cc);
                    cc = Tuple.Create((cc?.Item1 ?? 0) + 1, tt);
                    counts[k] = cc;
                }
            }
            Dictionary<String, Info> parts = new Dictionary<string, Info>(StringComparer.Ordinal);
            foreach (var x in counts)
            {
                if (x.Value.Item1 < 2)
                    continue;
                if (x.Key.StartsWith("part"))
                    continue;
                if (x.Key.StartsWith("episode"))
                    continue;
                var cat = Info.GetInfo(out var wasNew, x.Value.Item2, "The " + x.Value.Item2 + " series", Group, MediaCat.AllMedia, true);
                if (wasNew)
                    AllInfo.TryAdd(cat.Name, cat, false, false);
                parts.Add(x.Key, cat);
            }
            Char[] split = ['-', '–'];
            for (int i = 0; i < l; i += 6)
            {
                var name = data[i];
                var x = data[i + 1].Split(split);
                var yearStart = int.Parse(x[0].Trim());
                int.TryParse(x[x.Length - 1].Trim(), out var yearEnd);
                var rating = data[i + 2];
                var length = data[i + 3];
                var cat = data[i + 4];
                var plot = data[i + 5];
                Reg(parts, name, yearStart, yearEnd, rating, length, cat, plot);
            }
        }

        #endregion//Setup


    }

}