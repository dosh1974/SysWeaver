using System;

namespace SysWeaver.Inspection
{

    /// <summary>
    /// Put this on a type that implements the <see cref="IDescribable"/> interface to specify the current version (for version handling).
    /// Types without the attribute have version 1.
    /// </summary>
    /// <remarks>
    /// When data written by an older version is read, <see cref="IDescribable.Describe(IInspector, int)"/> is called with the old version.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class DescVersionAttribute : Attribute
    {
        /// <summary>
        /// Specify the current version of the type.
        /// </summary>
        /// <param name="version">The current version, must be 1 or greater</param>
        /// <exception cref="Exception">The version is less than 1 (thrown when the attribute is instantiated, i.e. when it's read using reflection)</exception>
        public DescVersionAttribute(int version = 1)
        {
            if (version < 1)
                throw new Exception("Invalid version parmeter value (" + version + ") for attribute: DescVersion!");
            Version = version;
        }
        /// <summary>
        /// The current version of the type (1 or greater)
        /// </summary>
        public readonly int Version;
    }

}

