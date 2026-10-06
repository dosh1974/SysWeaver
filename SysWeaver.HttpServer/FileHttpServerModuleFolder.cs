using System;

namespace SysWeaver.Net
{

    /// <summary>
    /// Maps a folder on disc to a web folder, used by <see cref="FileHttpServerModule"/>.
    /// </summary>
    public sealed class FileHttpServerModuleFolder : FileHttpServerModuleWebFolder
    {
        public override string ToString() => String.Concat(
            nameof(DiscFolder), ": ", DiscFolder.ToQuoted(), ", ", base.ToString());

        /// <summary>
        /// The folder on disc (absolute or relative to the current directory), null means "web".
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// Copy all settings (including <see cref="DiscFolder"/>) to another instance.
        /// </summary>
        /// <param name="t">The instance to copy to</param>
        public void CopyTo(FileHttpServerModuleFolder t)
        {
            base.CopyTo(t);
            t.DiscFolder = DiscFolder;
        }


    }


}
