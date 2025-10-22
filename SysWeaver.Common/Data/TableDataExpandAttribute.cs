using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a type to expand it when used in a data table
    /// </summary>
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
        public readonly String MemberNamePrefix;
        public readonly String TitlePrefix;

    }

}
