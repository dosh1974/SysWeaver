using SysWeaver.Net;
using System;

namespace SysWeaver.Net.IconModule
{



    public class IconHttpServerModuleParams : BaseHttpServerModuleParams
    {
        public override string ToString() => String.Concat(
            nameof(UriRoot), ": ", UriRoot.ToQuoted(), ", ",
            nameof(ExtensionFolder), ": ", ExtensionFolder.ToQuoted(), ", ",
            nameof(MimeFolder), ": ", MimeFolder.ToQuoted(), ", ",
            nameof(FlagFolder), ": ", FlagFolder.ToQuoted());

        /// <summary>
        /// The uri root for the assets
        /// </summary>
        public string UriRoot = "icons";

        /// <summary>
        /// Sub folder for file extension icons
        /// </summary>
        public string ExtensionFolder = "ext";

        /// <summary>
        /// Sub folder for mime type icons
        /// </summary>
        public string MimeFolder = "mime";

        /// <summary>
        /// Sub folder for mime type icons
        /// </summary>
        public string FlagFolder = "flags";

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;
    }
}
