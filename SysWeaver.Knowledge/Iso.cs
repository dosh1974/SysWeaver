using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.IsoData;

namespace SysWeaver.Knowledge
{
    public sealed class Country : Info
    {
        internal Country(string desc, IsoCountry c, Info[] parent) : base(c.CommonName, desc, Countries.Group, parent, false)
        {
            Iso = c.Iso3166a2;
            Currency = c.Currency;
            Population = c.Population;
            LandArea = c.LandArea;
        }
        /// <summary>
        /// The ISO 3166 Alpha 2 country code of the country.
        /// </summary>
        public readonly String Iso;

        /// <summary>
        /// ISO 4217 currency code of the most common currency used in the country
        /// </summary>
        public readonly String Currency;

        /// <summary>
        /// The population estimate (2020) of the country, a zero means no information
        /// </summary>
        public readonly long Population;

        /// <summary>
        /// The land area estimate in km² (2020) of the country, a zero means no information
        /// </summary>
        public readonly int LandArea;

    }

    public static class Countries
    {
        public const String Group = "Countries";

        public static bool TryGet(String name, out Country info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Country>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Country> Tags = new Dictionary<string, Country>(StringComparer.Ordinal);


        static Countries()
        {
            AllInfo.TryAdd(TagCountries.Name, TagCountries, false, true);
            var cp = C;
            var tags = Tags;
            Dictionary<String, Country> cs = new Dictionary<string, Country>();
            foreach (var ck in IsoCountry.Aliases)
            {
                var name = ck.Key;
                var c = ck.Value;
                var ckey = c.Iso3166a2;
                if (cs.TryGetValue(ckey, out var tag))
                {
                    tags.Add(name, tag);
                    continue;
                }
                String desc = "The country " + c.CommonName + ".";
                if (c.OfficialName != c.CommonName)
                    desc += "\nFormal name: " + c.OfficialName + ".";
                var cc = IsoCurrency.TryGet(c.Currency);
                if (cc != null)
                    desc += "\nCommon currency: " + cc.Name + " (" + cc.Iso4217 + ").";
                if (c.Population > 0)
                {
                    desc += "\nEstimated population: " + c.Population + " (as of 2020).";
                    if (c.LandArea > 0)
                        desc += "\nPopulation density: " + Math.Round(c.PopDense) + " per km² (as of 2020).";
                }
                if (c.LandArea > 0)
                    desc += "\nLand area: " + c.LandArea+ " km² (as of 2020).";
                tag = new Country(desc, c, cp);
                tags.Add(name, tag);
                cs.Add(ckey, tag);
            }
            var data = DataHelper.GetData<String[]>("LangAlts");
            var l = data.Length;
            for (int i = 0; i < l; ++i)
            {
                var aliases = data[i].Split(',');
                var ckey = aliases[0];
                if (!cs.TryGetValue(ckey, out var tag))
                    continue;
                var al = aliases.Length;
                for (int j = 1; j < al; ++ j)
                {
                    var name = aliases[j].FastToLower();
                    tags[name] = tag;
                }
            }
            foreach (var x in tags)
            {
                var k = x.Key;
                if (k.Length > 2)
                    AllInfo.TryAdd(k, x.Value, true, false);
            }
        }

        public static readonly Info TagCountries = new Info("Country", "Any country", Group, null);
        static readonly Info[] C =
        [
            TagCountries
        ];


        #endregion//Setup

    }

}
