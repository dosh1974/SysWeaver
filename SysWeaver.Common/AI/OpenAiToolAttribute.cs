using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that prints out an emoji when an AI tools is called
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OpenAiToolAttribute : Attribute
    {
        public OpenAiToolAttribute(String icon)
        {
            Icon = icon;
        }
        public readonly String Icon;
    }

    /// <summary>
    /// Put this on a method on an instance in the service registry and the method will be available as an Open AI tool.
    /// No need to add a dependency to open AI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OpenAiUseAttribute : Attribute
    {
        public OpenAiUseAttribute(bool use = true)
        {
            Use = use;
        }
        public readonly bool Use;
    }

    /// <summary>
    /// An interface used to indicate that the type contains open AI tools (having methods with the OpenAiUseAttribute or OpenAiToolAttribute)
    /// </summary>
    public interface IHaveOpenAiTools
    {
    }


    /// <summary>
    /// For AI functions that return a table data reference, use this attribute to specify the type of the row.
    /// Column information will then be added to the AI tool declareation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OpenAiTableRowTypeAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rowType">The type of data in the table</param>
        /// <param name="canEdit">Set to true if this method can modify the table data before creating and returning a reference</param>
        public OpenAiTableRowTypeAttribute(Type rowType, bool canEdit)
        {
            RowType = rowType;
            CanEdit = canEdit;
        }
        public readonly Type RowType;
        public readonly bool CanEdit;
    }


}
