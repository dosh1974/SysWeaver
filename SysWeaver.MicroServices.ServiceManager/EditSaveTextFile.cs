using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Request to save a text file in an editor
    /// </summary>
    public sealed class EditSaveTextFile : EditFile
    {

        /// <summary>
        /// The new content
        /// </summary>
        public String Content;
    }

}
