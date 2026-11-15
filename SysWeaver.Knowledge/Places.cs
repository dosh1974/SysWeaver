using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{


    /// <summary>
    /// What kind of location a place is in
    /// </summary>
    public enum PlaceLocations
    {
        /// <summary>
        /// The location is unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// The place is in a capital city
        /// </summary>
        Capital,
        /// <summary>
        /// The place is in a city (that is not a capital)
        /// </summary>
        City,
        /// <summary>
        /// The place is in a country (no specific city)
        /// </summary>
        Country
    }

    /// <summary>
    /// Info about a place
    /// </summary>
    public sealed class Place : Info, IGoogleInfo
    {
        /// <summary>
        /// Create place info
        /// </summary>
        /// <param name="loc">The kind of location of the place</param>
        /// <param name="location">The name of the location (city or country)</param>
        /// <param name="name">The name of the place</param>
        /// <param name="desc">The description</param>
        /// <param name="category">The category of the info</param>
        /// <param name="pop">Popularity (measured as number of hits when searching)</param>
        /// <param name="parent">Optional parent info</param>
        public Place(PlaceLocations loc, String location, string name, string desc, string category, long pop, Info[] parent = null)
            : base(name, desc, category, parent, true)
        {
            LocationType = loc;
            Location = location;
            Pop = pop;
        }

        /// <summary>
        /// What kind of location the place is in
        /// </summary>
        public readonly PlaceLocations LocationType;

        /// <summary>
        /// The name of the location (city or country) the place is in
        /// </summary>
        public readonly String Location;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }

    }

    /// <summary>
    /// Knowledge about places (landmarks and other places of interest)
    /// </summary>
    public static class Places
    {
        /// <summary>
        /// The group (category) name of all place info
        /// </summary>
        public const String Group = "Places";

        /// <summary>
        /// Try to get info about a place
        /// </summary>
        /// <param name="name">The name of the place, any case</param>
        /// <param name="info">The info if found</param>
        /// <returns>True if info was found</returns>
        public static bool TryGet(String name, out Place info) => Tags.TryGetValueLower(name, out info);

        /// <summary>
        /// All keyword/info pairs (the keys are lower case)
        /// </summary>
        public static IEnumerable<KeyValuePair<String, Place>> All => Tags;

        /// <summary>
        /// The number of keywords
        /// </summary>
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