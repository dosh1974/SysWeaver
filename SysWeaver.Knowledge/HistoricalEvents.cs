using System;
using System.Collections.Generic;
using System.Text;


namespace SysWeaver.Knowledge
{

    /// <summary>
    /// Info about a historical event
    /// </summary>
    public sealed class HistoricalEvent : Info, IGoogleInfo
    {
        internal HistoricalEvent(string name, string desc, DateOnly born, DateOnly died, long pop, Info[] parent) : base(name, desc, HistoricalEvents.Group, parent, false)
        {
            Born = born;
            Died = died;
            Pop = pop;
        }
        /// <summary>
        /// When the event started (or Min if unknown)
        /// </summary>
        public readonly DateOnly Born;

        /// <summary>
        /// When the event ended (or Min if unknown)
        /// </summary>
        public readonly DateOnly Died;

        /// <summary>
        /// Popularity (measured as number of hits when searching)
        /// </summary>
        public long Pop { get; private set; }
    }

    /// <summary>
    /// Knowledge about historical events
    /// </summary>
    public static class HistoricalEvents
    {
        /// <summary>
        /// The group (category) name of all historical event info
        /// </summary>
        public const String Group = "Historical events";

        /// <summary>
        /// Try to get info about a historical event
        /// </summary>
        /// <param name="name">The name of the event, any case</param>
        /// <param name="info">The info if found</param>
        /// <returns>True if info was found</returns>
        public static bool TryGet(String name, out HistoricalEvent info) => Tags.TryGetValueLower(name, out info);

        /// <summary>
        /// All keyword/info pairs (the keys are lower case)
        /// </summary>
        public static IEnumerable<KeyValuePair<String, HistoricalEvent>> All => Tags;

        /// <summary>
        /// The number of keywords
        /// </summary>
        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, HistoricalEvent> Tags = new Dictionary<string, HistoricalEvent>(StringComparer.Ordinal);

        static void Reg(String name, String born, String died, String desc, String pop)
        {
            if (name.Length < 10)
                return;
            var key = name.FastToLower();
            var tag = new HistoricalEvent(name, desc, DataHelper.ParseDate(born), DataHelper.ParseDate(died), long.Parse(pop), TagEvents);
            Tags.Add(key, tag);
            AllInfo.TryAdd(key, tag, false, false);
        }

        static readonly Info[] TagEvents = [new Info("Historical events", "All historical events", Group, null)];

        static HistoricalEvents()
        {
            var data = DataHelper.GetData<String[]>("Events");
            var l = data.Length;
            for (int i = 0; i < l; i += 5)
                Reg(data[i], data[i + 1], data[i + 2], data[i + 3], data[i + 4]);
        }

        #endregion//Setup


    }

}