using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a type (or on a member) to expand it when used in a data table, i.e. the public instance members of the value becomes columns of the table.
    /// </summary>
    /// <remarks>
    /// Only applies to member types that isn't a supported column type, members of such a type without this attribute are ignored.
    /// For reference types, a null value is replaced by a default instance (so the type must be constructable).
    /// The prefixes are applied recursively when an expanded type contains other expanded members.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataExpandAttribute : Attribute
    {
        /// <summary>
        /// Make the object expand into columns
        /// </summary>
        /// <param name="memberNamePrefix">The name prefix to use.
        /// {0} = Member name.
        /// </param>
        /// <param name="titlePrefix">The title prefix to use.
        /// {0} = Member name.
        /// {1} = Decamel cased member name.
        /// </param>
        public TableDataExpandAttribute(String memberNamePrefix = "{0}_", String titlePrefix = "{1} ")
        {
            MemberNamePrefix = memberNamePrefix;
            TitlePrefix = titlePrefix;
        }
        /// <summary>
        /// The name prefix format, {0} = Member (column) name.
        /// </summary>
        public readonly String MemberNamePrefix;
        /// <summary>
        /// The title prefix format, {0} = Member (column) name, {1} = Column title.
        /// </summary>
        public readonly String TitlePrefix;

    }

}
