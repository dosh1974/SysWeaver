using System;

namespace SysWeaver
{
    /// <summary>
    /// Replace embedded files (if they exist)
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ReplaceEmbeddedFilesAttribute : Attribute
    {
        public ReplaceEmbeddedFilesAttribute(bool replace = true)
        {
            Replace = replace;
        }

        public readonly bool Replace;
    }
}