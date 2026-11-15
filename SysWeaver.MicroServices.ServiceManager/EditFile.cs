using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Identifies a file in an editor (base for the edit requests)
    /// </summary>
    public class EditFile
    {
        /// <summary>
        /// The url used to read the file
        /// </summary>
        public String Url;

        /// <summary>
        /// The name of the file (filename of url)
        /// </summary>
        public String Name;

    }

}
