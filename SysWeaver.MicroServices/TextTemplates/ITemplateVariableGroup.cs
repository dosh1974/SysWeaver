using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// A named group of text template variables
    /// </summary>
    public interface ITemplateVariableGroup
    {

        /// <summary>
        /// True if the values can change over time (so they should not be cached)
        /// </summary>
        bool IsDynamic { get; }

        /// <summary>
        /// Get the value of a variable
        /// </summary>
        /// <param name="key">The variable key (without the group name)</param>
        /// <returns>The value, or null if there is no such variable</returns>
        String GetTemplateVariableValue(String key);

        /// <summary>
        /// Enumerate all variables in the group
        /// </summary>
        /// <returns>Key (without the group name) and value pairs</returns>
        IEnumerable<KeyValuePair<String, String>> EnumTemplateVariableValues();

    }



}
