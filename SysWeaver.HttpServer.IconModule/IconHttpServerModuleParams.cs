using SysWeaver.Net;
using System;

namespace SysWeaver.Net.IconModule
{



    /// <summary>
    /// Parameters for <see cref="IconHttpServerModule"/>: the url root of the icons and the sub folders of the extension, mime type
    /// and flag icons (the auth, compression and client cache options are inherited).
    /// </summary>
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
        /// Sub folder for country flag icons (named by the ISO 3166 alpha-2 code, ex: "us.svg")
        /// </summary>
        public string FlagFolder = "flags";

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;
    }
}
