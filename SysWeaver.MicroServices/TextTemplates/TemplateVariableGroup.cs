using System;
using System.Collections.Generic;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// A template variable group that is implemented using delegates
    /// </summary>
    public sealed class TemplateVariableGroup : ITemplateVariableGroup
    {

        /// <summary>
        /// Create a template variable group
        /// </summary>
        /// <param name="get">Function to get the value of a variable by key</param>
        /// <param name="enumValues">Function to enumerate all variables</param>
        /// <param name="isDynamic">True if the values can change over time</param>
        public TemplateVariableGroup(Func<String, String> get, Func<IEnumerable<KeyValuePair<String, String>>> enumValues, bool isDynamic = false)
        {
            Get = get;
            EnumValues = enumValues;
            IsDynamic = isDynamic;
        }

        readonly Func<String, String> Get;
        readonly Func<IEnumerable<KeyValuePair<String, String>>> EnumValues;


        /// <inheritdoc />
        public bool IsDynamic { get; private set; }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<String, String>> EnumTemplateVariableValues() => EnumValues();

        /// <inheritdoc />
        public string GetTemplateVariableValue(string key) => Get(key);
    }



}
