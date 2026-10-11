using System;

namespace SysWeaver.Data
{

    /// <summary>
    /// Put on a member to ignore it when using the type in a data table (no column is created for it).
    /// An ignored member isn't text searched either, unless it also has a <see cref="TableDataSearchAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIgnoreAttribute : Attribute
    {
    }

}
