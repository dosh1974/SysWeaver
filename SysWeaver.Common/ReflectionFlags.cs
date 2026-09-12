using System;

namespace SysWeaver
{
    /// <summary>
    /// A collection of flags that describe some reflection properties
    /// </summary>
    [Flags]
    public enum ReflectionFlags
    {
        /// <summary>
        /// Nothing
        /// </summary>
        None = 0,
        /// <summary>
        /// The reflected item is static, else instance
        /// </summary>
        IsStatic = 1,
        /// <summary>
        /// The reflected item is public, else private / protected / internal etc
        /// </summary>
        IsPublic = 2,
        /// <summary>
        /// The reflected item is declared in the specified type, else it's declared in one of the inherited types
        /// </summary>
        IsDeclared = 4,
        /// <summary>
        /// All reflection types combined
        /// </summary>
        All = IsStatic | IsPublic | IsDeclared
    }

}
