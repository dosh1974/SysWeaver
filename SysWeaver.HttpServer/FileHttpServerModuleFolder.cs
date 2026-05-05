using System;

namespace SysWeaver.Net
{

    public sealed class FileHttpServerModuleFolder : FileHttpServerModuleWebFolder
    {
        public override string ToString() => String.Concat(
            nameof(DiscFolder), ": ", DiscFolder.ToQuoted(), ", ", base.ToString());

        /// <summary>
        /// The folder on disc
        /// </summary>
        public String DiscFolder;

        public void CopyTo(FileHttpServerModuleFolder t)
        {
            base.CopyTo(t);
            t.DiscFolder = DiscFolder;
        }


    }


}
