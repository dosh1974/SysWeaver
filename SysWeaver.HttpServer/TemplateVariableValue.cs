using System;
using SysWeaver.Data;



namespace SysWeaver.MicroService
{
    /// <summary>
    /// A snapshot of a template variable (name and value), used by the template variables debug table.
    /// </summary>
    public sealed class TemplateVariableValue
    {
        /// <summary>
        /// True if the variable is dynamic (depends on the request / session), templates that use dynamic variables are not cached (or only per session).
        /// </summary>
        public bool Dynamic;
        /// <summary>
        /// Name of the variable. In a text template use ${Name}.
        /// </summary>
        [TableDataFormat(null, null, "${{2}}")]
        public String Name;
        /// <summary>
        /// Value of the variable (for the request that created the snapshot).
        /// </summary>
        public String Value;

        /// <summary>
        /// Create an empty instance (for serialization).
        /// </summary>
        public TemplateVariableValue()
        {
        }

        /// <summary>
        /// Create a snapshot of a variable.
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="value">Value of the variable</param>
        /// <param name="dynamic">True if the variable is dynamic</param>
        public TemplateVariableValue(string name, string value, bool dynamic = false)
        {
            Name = name;
            Value = value;
            Dynamic = dynamic;
        }
    }




}
