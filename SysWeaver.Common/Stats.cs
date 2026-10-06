using System;
using System.Collections.Generic;
using SysWeaver.Data;

namespace SysWeaver
{

    /// <summary>
    /// A single named statistics value of a system (typically displayed in a table by the debug / stats api)
    /// </summary>
    public sealed class Stats
    {
#if DEBUG
        /// <summary>
        /// Returns "System.Name = Value"
        /// </summary>
        /// <returns>A description of the statistics</returns>
        public override string ToString() => String.Concat(System, '.', Name, " = ", Value);
#endif//DEBUG
        /// <summary>
        /// Create a new statistics value with a display format taken from a table data attribute
        /// </summary>
        /// <param name="system">The system that this statistic belong to</param>
        /// <param name="name">The name of the statistics</param>
        /// <param name="value">The value (may be null)</param>
        /// <param name="desc">A description of the statistics</param>
        /// <param name="formatAttribute">The format to use when displaying the value (may be null), <see cref="TF"/> becomes "TypeName|Format"</param>
        public Stats(String system, String name, Object value, String desc, TableDataRawFormatAttribute formatAttribute)
            : this(system, name, value, desc, String.Join('|', (value?.GetType() ?? typeof(String)).CleanTypename(), formatAttribute?.Value))
        {
        }

        /// <summary>
        /// Create a new statistics value
        /// </summary>
        /// <param name="system">The system that this statistic belong to</param>
        /// <param name="name">The name of the statistics</param>
        /// <param name="value">The value (may be null)</param>
        /// <param name="desc">A description of the statistics</param>
        /// <param name="format">The raw "TypeName|Format" string, null to use the clean type name of the value (String if the value is null)</param>
        public Stats(String system, String name, Object value, String desc, String format = null)
        {
            System = system;
            Name = name;
            Value = value;
            TF = format ?? (value?.GetType() ?? typeof(String)).CleanTypename();
            Description = desc;
        }
        /// <summary>
        /// The system that this statistic belong to
        /// </summary>
        public String System;
        /// <summary>
        /// The name of the statistics
        /// </summary>
        public String Name;
        /// <summary>
        /// The statistics value
        /// </summary>
        [TableDataPerRowFormat]
        public Object Value;
        /// <summary>
        /// Must contain the TypeName|Format
        /// </summary>
        [TableDataHide]
        public String TF;

        /// <summary>
        /// Description of the statistics
        /// </summary>
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the description for some statistics with the name \"{0}\"", nameof(Name))]
        [AutoTranslateContext("It is part of a system named \"{0}\"", nameof(System))]
        public String Description;


        /// <summary>
        /// Collect statistics from a function, ignoring any exception
        /// </summary>
        /// <param name="s">The function that returns the statistics (typically a lazy enumerable)</param>
        /// <returns>The statistics enumerated before an exception was thrown (an empty list if the function or the enumeration failed immediately)</returns>
        public static List<Stats> SafeGetStats(Func<IEnumerable<Stats>> s)
        {
            List<Stats> ss = new List<Stats>();
            try
            {
                var stats = s();
                foreach (var x in stats)
                    ss.Add(x);
            }
            catch
            {

            }
            return ss;
        }

    }

    /// <summary>
    /// Instances implementing this interface can be queried for some useful stats
    /// </summary>
    public interface IHaveStats
    {
        /// <summary>
        /// Return some stats
        /// </summary>
        /// <returns>The stats</returns>
        IEnumerable<Stats> GetStats();
    }



}
