using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Knowledge
{
    public static class Years
    {
        public const String Group = "Years";

        public static bool TryGet(String name, out Info info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Info>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Info> Tags = new Dictionary<string, Info>(StringComparer.Ordinal);



        static readonly Info[] TagYears = [new Info("Years", "Anything related to a specific year", Group, null)];
        static readonly Info[] TagDecades = [new Info("Decacdes", "Anything related to a specific decade", Group, null)];
        static readonly Info[] TagCentury = [new Info("Centuries", "Anything related to a specific century", Group, null)];

        static void RegData(Dictionary<int, List<Info>> to, int year, Info add)
        {
            if (!to.TryGetValue(year, out var l))
            {
                l = new List<Info>();
                to.Add(year, l);
            }
            l.Add(add);
        }

        static void RegCentury(Dictionary<int, List<Info>> to, String name, int start, int end)
        {
            var info = new Info(name, "Anything related to the " + name + " centrury (" + start + "-" + end + ")", Group, TagCentury);
            for (int i = start; i <= end; ++i)
                RegData(to, i, info);
        }

        static void RegDecade(Dictionary<int, List<Info>> to, String name, int start, int end)
        {
            var info = new Info(name, "Anything related to the " + name + " decade (" + start + "-" + end + ")", Group, TagDecades);
            for (int i = start; i <= end; ++i)
                RegData(to, i, info);
        }

        static void RegYear(Dictionary<int, List<Info>> to, int year)
        {
            var info = new Info(year.ToString(), "Anything related to the year " + year, Group, TagYears);
            RegData(to, year, info);
        }

        static void AddRec(List<Info> to, HashSet<Info> seen, Info from)
        {
            var p = from.Parents;
            if (p == null)
                return;
            foreach (var i in p)
            {
                if (!seen.Add(i))
                    continue;
                to.Add(i);
                AddRec(to, seen, i);
            }
        }

        static Years()
        {
            Dictionary<int, List<Info>> to = new Dictionary<int, List<Info>>();
            for (int i = 1920; i <= 2050; ++i)
                RegYear(to, i);
            for (int i = 10; i <= 90; i += 10)
                RegDecade(to, i.ToString() + "s", 1900 + i, 1909 + i);
            for (int i = 1; i <= 21; ++i)
            {
                var end = i * 100;
                var start = end - 99;
                switch (i)
                {
                    case 1:
                        RegCentury(to, i.ToString() + "st century", start, end);
                        break;
                    case 2:
                        RegCentury(to, i.ToString() + "nd century", start, end);
                        break;
                    case 3:
                        RegCentury(to, i.ToString() + "rd century", start, end);
                        break;
                    default:
                        RegCentury(to, i.ToString() + "th century", start, end);
                        break;
                }
            }
            var tags = Tags;
            void AddOne(String name, Info i)
            {
                var k = name.FastToLower();
                if (tags.ContainsKey(k))
                    return;
                tags.Add(k, i);
                AllInfo.TryAdd(k, i, false, false);
            }
            foreach (var year in to)
            {
                var p = year.Value;
                var pl = p.Count;
                bool didAddYear = false;
                for (int i = 0; i < pl;  ++i)
                {
                    var info = p[i];
                    var aa = info.Parents;
                    var l = new List<Info>(20);
                    var copyS = i + 1;
                    while (copyS < pl)
                    {
                        l.Add(p[copyS]);
                        ++copyS;
                    }
                    l.AddRange(aa);
                    var seen = new HashSet<Info>(l);
                    foreach (var x in l.ToList())
                        AddRec(l, seen, x);

                    info = new Info(info.Name, info.Desc, info.Category, l.ToArray(), info.IsName);


                    AddOne(info.Name, info);
                    didAddYear |= (aa == TagYears);
                    if (aa == TagDecades)
                    {
                        var xx = info.Name.Substring(0, 2) + "'s";
                        AddOne(xx, info);
                        if (!didAddYear)
                        {
                            AddOne("19" + info.Name, info);
                            AddOne("19" + xx, info);
                        }
                    }
                }

                if (!didAddYear)
                {
                    if (year.Key >= 1000)
                        AddOne(year.Key.ToString(), p[0]);
                }

            }
        }


        #endregion//Setup

    }


}



