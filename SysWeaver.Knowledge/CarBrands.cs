using System;
using System.Collections.Generic;

namespace SysWeaver.Knowledge
{

    public sealed class CarBrandInfo : Info
    {
        internal CarBrandInfo(string name, string desc, string category, int start, int end, String cc, long sold, Info[] parent) : base(name, desc, category, parent, true)
        {
            Start = start;
            End = end;
            Country = cc;
            Count = sold;
        }
        /// <summary>
        /// Production start year (or 0 if unknown)
        /// </summary>
        public readonly int Start;
        /// <summary>
        /// Production end year (or 0 if still in production, as of Q1 2024)
        /// </summary>
        public readonly int End;
        /// <summary>
        /// From what country (as iso code)
        /// </summary>
        public readonly String Country;
        /// <summary>
        /// Count (representing a measure of popularity, currently the number of registered vehicles in Sweden Q1 2024).
        /// </summary>
        public readonly long Count;

    }

    public static class CarBrands
    {
        public const String Group = "Cars";

        public static bool TryGet(String name, out CarBrandInfo info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, CarBrandInfo>> All => Tags;
        
        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, CarBrandInfo> Tags = new Dictionary<string, CarBrandInfo>(StringComparer.Ordinal);


        static void Reg(String name, String startText, String endText, String cc, String soldText, String isCommonText)
        {
            var start = int.Parse(startText);
            var end = int.Parse(endText);
            var sold = long.Parse(soldText);
            var isCommon = isCommonText == "1";
            if (name.Length <= 3)
                isCommon = true;
            if (sold < 500)
                isCommon = true;

            var ci = IsoData.IsoCountry.TryGet(cc);
            var from = (ci != null) ? (" from " + ci.CommonName) : "";
            if (start > 0)
            {
                if (end > 0)
                {
                    from += ", manufactured between " + start + " and " + end;
                }else
                {
                    from += ", manufactured from " + start + " and forward";
                }
            }
            else
            {
                if (end > 0)
                    from += ", manufactured up until " + end;
            }
            if (from.StartsWith(","))
                from = from.Substring(1);

            var prefix = "The car brand ";
            Info[] pa = Car;
            if (end > 0)
            {
                if (end < InfoConsts.VintageYear)
                {
                    prefix = "The vintage car brand ";
                    pa = VintageCar;
                }
                else
                {
                    if (end < InfoConsts.RetroYear)
                    {
                        prefix = "The retro car brand ";
                        pa = RetroCar;
                    }
                }
            }

            var tag = new CarBrandInfo(name, prefix + name + from, Group, start, end, cc, sold, pa);
            var key = name.FastToLower();
            Tags.TryAdd(key, tag);
            AllInfo.TryAdd("car " + key, tag, false, false);
            AllInfo.TryAdd("cars " + key, tag, false, false);
            AllInfo.TryAdd(key + " car", tag, false, false);
            AllInfo.TryAdd(key + " cars", tag, false, false);
            if (!isCommon)
                AllInfo.TryAdd(key, tag, false, false);
        }

        static readonly Info TagVehicle = new Info("Vehicle", "All things that is a vehicles", InfoConsts.GenericGroup, null);
        static readonly Info TagCars = new Info("Car", "Anything that have anything to do with cars", Group, new[] { TagVehicle });
        static readonly Info TagCarBrands = new Info("Car Brand", "Anything related to a car brand", Group, new[] { TagCars , TagVehicle });

        static readonly Info TagCarRetro = new Info("Retro Car", "Anything car related built before " + InfoConsts.RetroYear, Group, new[] { TagCars, TagVehicle, InfoCommon.TagRetro  });
        static readonly Info TagCarVintage = new Info("Vintage Car", "Anything car related built before " + InfoConsts.VintageYear, Group, new[] { TagCarRetro, TagCars, TagVehicle, InfoCommon.TagVintage, InfoCommon.TagRetro });

        static readonly Info[] Car =
        [
            TagCarBrands,
            TagCars,
            TagVehicle,
        ];

        static readonly Info[] RetroCar =
        [
            TagCarBrands,
            TagCarRetro,
            TagCars,
            TagVehicle,
            InfoCommon.TagRetro,
        ];

        static readonly Info[] VintageCar =
        [
            TagCarBrands,
            TagCarVintage,
            TagCarRetro,
            TagCars,
            TagVehicle,
            InfoCommon.TagVintage,
            InfoCommon.TagRetro,
        ];

        static void Reg(Info i)
        {
            AllInfo.TryAdd(i.Name, i, false, true);
        }
        
        #region Data

        static CarBrands()
        {
            Reg(TagVehicle);
            Reg(TagCars);
            Reg(TagCarBrands);
            Reg(TagCarRetro);
            Reg(TagCarVintage);
            var data = DataHelper.GetData<String[]>("Cars");
            var l = data.Length;
            for (int i = 0; i < l; i += 6)
                Reg(data[i], data[i + 1], data[i + 2], data[i + 3], data[i + 4], data[i + 5]);
        }

        #endregion//Data

        #endregion//Setup

    }

}
