using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Marks a method as an AI tool and specifies the icon (typically one or more emojis) that is displayed when the tool is called.
    /// </summary>
    /// <remarks>
    /// A method is an AI tool if it has this attribute or an <see cref="AiUseAttribute"/> with <see cref="AiUseAttribute.Use"/> set to true.
    /// Tool methods on service instances implementing <see cref="IHaveAiTools"/> are added to AI chat services, and tool methods on any service instance can be exposed over MCP.
    /// The tool name is computed from <see cref="AiToolPrefixAttribute"/> and <see cref="AiToolNameAttribute"/>, the description from the XML documentation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiToolAttribute : Attribute
    {
        /// <summary>
        /// Marks a method as an AI tool.
        /// </summary>
        /// <param name="icon">The icon (typically emojis) displayed when the tool is called.</param>
        public AiToolAttribute(String icon)
        {
            Icon = icon;
        }
        /// <summary>
        /// The icon (typically emojis) displayed when the tool is called.
        /// </summary>
        public readonly String Icon;
    }

    /// <summary>
    /// Put this on a method on an instance in the service registry and the method will be available as an AI tool (without an icon).
    /// No need to add a dependency to any AI provider.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="AiToolAttribute"/> which also specifies an icon.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiUseAttribute : Attribute
    {
        /// <summary>
        /// Marks (or explicitly unmarks) a method as an AI tool.
        /// </summary>
        /// <param name="use">True to use the method as an AI tool.</param>
        public AiUseAttribute(bool use = true)
        {
            Use = use;
        }
        /// <summary>
        /// True if the method should be used as an AI tool.
        /// </summary>
        public readonly bool Use;
    }

    /// <summary>
    /// A marker interface used to indicate that the type contains AI tools (methods with an <see cref="AiToolAttribute"/> or an <see cref="AiUseAttribute"/>).
    /// </summary>
    /// <remarks>
    /// AI chat services add the tools of all service instances implementing this interface (and remove them when the instance is removed).
    /// The MCP service doesn't require this interface.
    /// </remarks>
    public interface IHaveAiTools
    {
    }


    /// <summary>
    /// For AI functions that return a table data reference, use this attribute to specify the type of the row.
    /// Column information will then be added to the AI tool declaration.
    /// </summary>
    /// <remarks>
    /// Note: Currently not read by any code in the framework.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiTableRowTypeAttribute : Attribute
    {
        /// <summary>
        /// Specify the row type of the table data returned by an AI tool.
        /// </summary>
        /// <param name="rowType">The type of data in the table</param>
        /// <param name="canEdit">Set to true if this method can modify the table data before creating and returning a reference</param>
        public AiTableRowTypeAttribute(Type rowType, bool canEdit)
        {
            RowType = rowType;
            CanEdit = canEdit;
        }
        /// <summary>
        /// The type of data in the table.
        /// </summary>
        public readonly Type RowType;
        /// <summary>
        /// True if the method can modify the table data before creating and returning a reference.
        /// </summary>
        public readonly bool CanEdit;
    }


}
