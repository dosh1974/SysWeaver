using System;
using System.Collections.Generic;
using SysWeaver.Data;

namespace SysWeaver
{

    /// <summary>
    /// Represents some statistics
    /// </summary>
    public sealed class Stats
    {
#if DEBUG
        public override string ToString() => String.Concat(System, '.', Name, " = ", Value);
#endif//DEBUG
        public Stats(String system, String name, Object value, String desc, TableDataRawFormatAttribute formatAttribute)
            : this(system, name, value, desc, String.Join('|', (value?.GetType() ?? typeof(String)).FullName, formatAttribute?.Value))
        {
        }

        public Stats(String system, String name, Object value, String desc, String format = null)
        {
            System = system;
            Name = name;
            Value = value;
            TF = format ?? (value?.GetType() ?? typeof(String)).FullName;
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
