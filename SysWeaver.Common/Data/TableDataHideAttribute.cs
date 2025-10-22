using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to tell any renderer to hide the column
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataHideAttribute : Attribute
    {
    }


}
