using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Request to save a binary file in an editor
    /// </summary>
    public sealed class EditSaveBinFile : EditFile
    {

        /// <summary>
        /// The new content
        /// </summary>
        public Byte[] Content;
    }

}
