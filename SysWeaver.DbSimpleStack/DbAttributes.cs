using System;

namespace SimpleStack.Orm.Attributes
{

    /// <summary>
    /// This column is case sensitive
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CaseSensitiveAttribute : Attribute
    {
        public CaseSensitiveAttribute()
        {
        }
    }

    /// <summary>
    /// This column is case sensitive
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CaseInsensitiveAttribute : Attribute
    {
        public CaseInsensitiveAttribute()
        {
        }
    }




    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FullTextSearchIndexAttribute : Attribute
    {
        public FullTextSearchIndexAttribute(String name, params String[] properties)
        {
            Name = name ?? "FullText";
            Props = properties;
        }
        public readonly String Name;
        public readonly String[] Props;
    }



    /// <summary>
    /// This column is using the ascii char set
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AsciiAttribute : Attribute
    {
        public AsciiAttribute()
        {
        }
    }
}
