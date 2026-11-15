using System;

namespace SimpleStack.Orm.Attributes
{

    /// <summary>
    /// This column is case sensitive
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CaseSensitiveAttribute : Attribute
    {
        /// <summary>
        /// Mark a column as case sensitive
        /// </summary>
        public CaseSensitiveAttribute()
        {
        }
    }

    /// <summary>
    /// This column is case insensitive
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CaseInsensitiveAttribute : Attribute
    {
        /// <summary>
        /// Mark a column as case insensitive
        /// </summary>
        public CaseInsensitiveAttribute()
        {
        }
    }




    /// <summary>
    /// Defines a full text search index on a table
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FullTextSearchIndexAttribute : Attribute
    {
        /// <summary>
        /// Define a full text search index
        /// </summary>
        /// <param name="name">The name of the index, null means "FullText"</param>
        /// <param name="properties">The names of the properties (columns) to include in the index</param>
        public FullTextSearchIndexAttribute(String name, params String[] properties)
        {
            Name = name ?? "FullText";
            Props = properties;
        }

        /// <summary>
        /// The name of the index
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The names of the properties (columns) included in the index
        /// </summary>
        public readonly String[] Props;
    }



    /// <summary>
    /// This column is using the ascii char set
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AsciiAttribute : Attribute
    {
        /// <summary>
        /// Mark a column as using the ascii char set
        /// </summary>
        public AsciiAttribute()
        {
        }
    }
}
