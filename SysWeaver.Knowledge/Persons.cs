using System;
using System.Collections.Generic;


namespace SysWeaver.Knowledge
{

    public interface IGoogleInfo
    {
        public long Pop { get; }
    }

    public sealed class Person : Info, IGoogleInfo
    {
        internal Person(string name, string desc, DateOnly born, DateOnly died, long pop, Info[] parent) : base(name, desc, Persons.Group, parent, true)
        {
            Born = born;
            Died = died;
            Pop = pop;
        }
        /// <summary>
        /// When the person was born (or Min if unknown)
        /// </summary>
        public readonly DateOnly Born;

        /// <summary>
        /// When the person died (or Min if still alive, or unknown)
        /// </summary>
        public readonly DateOnly Died;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }

    }

    public static class Persons
    {
        public const String Group = "Persons";

        public static bool TryGet(String name, out Person info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, Person>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, Person> Tags = new Dictionary<string, Person>(StringComparer.Ordinal);

        static void Reg(String name, String born, String died, String desc, String pop)
        {
            if (name.Length < 10)
                return;
            if (!name.Contains(' '))
                return;
            var key = name.FastToLower();
            var tag = new Person(name, desc, DataHelper.ParseDate(born), DataHelper.ParseDate(died), long.Parse(pop), TagPersons);
            Tags.TryAdd(key, tag);
            AllInfo.TryAdd(key, tag, true, false);
        }

        static readonly Info[] TagPersons = [new Info("Persons", "A person", Group, null)];

        static Persons()
        {
            var data = DataHelper.GetData<String[]>("Persons");
            var l = data.Length;
            for (int i = 0; i < l; i += 5)
                Reg(data[i], data[i + 1], data[i + 2], data[i + 3], data[i + 4]);
        }

        #endregion//Setup


    }

}