using System;
using SysWeaver.Data;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents a snapshot a template variable
    /// </summary>
    public sealed class TemplateVariableValue
    {
        /// <summary>
        /// True if the variable is dynamic (changes dynamically), using this will prevent any caching
        /// </summary>
        public bool Dynamic;
        /// <summary>
        /// Name of the variable. In the text template use $(Name).
        /// </summary>
        [TableDataFormat(null, null, "${{2}}")]
        public String Name;
        /// <summary>
        /// Value of the variable
        /// </summary>
        public String Value;

        public TemplateVariableValue()
        {
        }

        public TemplateVariableValue(string name, string value, bool dynamic = false)
        {
            Name = name;
            Value = value;
            Dynamic = dynamic;
        }
    }




}
