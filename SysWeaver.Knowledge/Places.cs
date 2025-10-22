using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{


    public enum PlaceLocations
    {
        Unknown,
        Capital,
        City,
        Country
    }

    public sealed class Place : Info, IGoogleInfo
    {
        public Place(PlaceLocations loc, String location, string name, string desc, string category, long pop, Info[] parent = null)
            : base(name, desc, category, parent, true)
        {
            LocationType = loc;
            Location = location;
            Pop = pop;
        }
        
        public readonly PlaceLocations LocationType;
        public readonly String Location;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }

    }

    public static class Places
    {
        public const String Group = "Places";

        public static bool TryGet(String name, out Place info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Place>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Place> Tags = new Dictionary<string, Place>(StringComparer.Ordinal);

        static void Reg(String name, String desc, String pop, String loc)
        {
            if (name.Length < 8)
                return;
            var key = name.FastToLower();
            PlaceLocations type = PlaceLocations.Unknown;
            String l = null;
            Info info = null;
            if (loc != null)
            {
                switch (loc[0])
                {
                    case 'C':
                        type = PlaceLocations.Capital;
                        l = loc.Substring(1);
                        if (Cities.TryGet(l, out var x))
                            info = x;
                        break;
                    case 'I':
                        type = PlaceLocations.City;
                        l = loc.Substring(1);
                        if (Cities.TryGet(l, out var xx))
                            info = xx;
                        break;
                    case 'O':
                        type = PlaceLocations.Country;
                        l = IsoData.IsoCountry.TryGet(loc.Substring(1)).CommonName;
                        if (Countries.TryGet(l, out var xy))
                            info = xy;
                        break;
                }
            }

            List<Info> infos = new List<Info>();
            if (info != null)
            {
                infos.Add(info);
                var p = info.Parents;
                if (p != null)
                {
                    foreach (var i in p)
                        infos.Add(i);
                }
            }
            foreach (var i in TagPlaces)
                infos.Add(i);
            var tag = new Place(type, l, name, desc, Group, long.Parse(pop), infos.ToArray());
            Tags.Add(key, tag);
            AllInfo.TryAdd(key, tag, true, false);
        }

        static readonly Info[] TagPlaces = [new Info("Places", "A place", Group, null)];

        static Places()
        {
            var data = DataHelper.GetData<String[]>("Places");
            var l = data.Length;
            for (int i = 0; i < l; i += 4)
            {
                var name = data[i];
                if (name.Length <= 3)
                    continue;
                Reg(name, data[i + 1], data[i + 2], data[i + 3]);
            }
        }

        #endregion//Setup


    }

}