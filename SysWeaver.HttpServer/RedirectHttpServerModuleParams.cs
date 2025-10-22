using System;

namespace SysWeaver.Net
{
    public class RedirectHttpServerModuleParams
    {
        /// <summary>
        /// The redirections to make
        /// </summary>
        public HttpRedirection[] Redirections;


        /// <summary>
        /// If this is non-empty redirection entries are read from this file.
        /// Filename can contain environment variables.
        /// If the file is monitored for any updates and reloaded when changed.
        /// The file must be a UTF8 encoded text file, where each non-empty row not starting with a # is a redirection.
        /// The format of a row is "From To (Code)", example:
        /// http://*:80/ https://*:443/ 302
        /// Quotes may be used, example:
        /// "http://*:80/" "https://*:443/"
        /// </summary>
        public String Filename;

        /// <summary>
        /// Set to false to be case insensitive
        /// </summary>
        public bool CaseSensitive = true;




    }
}
