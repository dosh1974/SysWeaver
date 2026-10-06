using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters for <see cref="RedirectHttpServerModule"/>.
    /// </summary>
    public class RedirectHttpServerModuleParams
    {
        /// <summary>
        /// The redirections to make (ignored if <see cref="Filename"/> is set). Invalid entries are ignored with a warning.
        /// If empty, an http to https redirection is used (<see cref="HttpRedirection.HttpToHttps"/>).
        /// </summary>
        public HttpRedirection[] Redirections;


        /// <summary>
        /// If this is non-empty redirection entries are read from this file.
        /// Filename can contain environment variables.
        /// The file is monitored for any updates and reloaded when changed (if the file contains any invalid row, the previous redirections are kept).
        /// The file must be a UTF8 encoded text file, where each non-empty row not starting with a # is a redirection (text after a # is a comment).
        /// The format of a row is "From To [Code]" (Code defaults to 302, valid codes are 301, 302, 307 and 308), example:
        /// http://*:80/ https://*:443/ 302
        /// Quotes may be used, example:
        /// "http://*:80/" "https://*:443/"
        /// </summary>
        public String Filename;

        /// <summary>
        /// True to match the "from" prefixes case sensitively (default), false to be case insensitive.
        /// </summary>
        public bool CaseSensitive = true;




    }
}
