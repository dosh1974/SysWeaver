using System;
using SysWeaver.AI;

namespace SysWeaver.Map
{

    /// <summary>
    /// Specifies a map resource to use
    /// </summary>
    public class MapSelect
    {
        /// <summary>
        /// The map to use.
        /// Country maps need to set the MapCountry to the ISO code to use 
        /// </summary>
        public Maps Map;

        /// <summary>
        /// Only used when Map is a single country.
        /// The ISO 3166 country code.
        /// </summary>
        [OpenAiOptional]
        public String MapCountry;

    }
}
