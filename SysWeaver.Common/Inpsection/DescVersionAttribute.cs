using System;

namespace SysWeaver.Inspection
{

    /// <summary>
    /// Put this of a type that implements the IDescribable interface to specify a version (for version handling)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class DescVersionAttribute : Attribute
    {
        public DescVersionAttribute(int version = 1)
        {
            if (version < 1)
                throw new Exception("Invalid version parmeter value (" + version + ") for attribute: DescVersion!");
            Version = version;
        }
        public readonly int Version;
    }

}

