using System;

namespace SysWeaver
{
    /// <summary>
    /// Apply to an assembly to control whether its embedded resources replace already registered static files with the same path
    /// when the HTTP server serves embedded resources as files (read by the static data HTTP module).
    /// Without this attribute, embedded files do not replace existing ones.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ReplaceEmbeddedFilesAttribute : Attribute
    {
        /// <summary>
        /// Control whether embedded files in this assembly replace existing files.
        /// </summary>
        /// <param name="replace">True to replace existing files with the same path, false to keep the existing ones.</param>
        public ReplaceEmbeddedFilesAttribute(bool replace = true)
        {
            Replace = replace;
        }

        /// <summary>
        /// True if embedded files in this assembly should replace existing files with the same path.
        /// </summary>
        public readonly bool Replace;
    }
}