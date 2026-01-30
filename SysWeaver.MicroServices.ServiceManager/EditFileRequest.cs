using System;

namespace SysWeaver.MicroService
{
    public class EditFileRequest
    {
        /// <summary>
        /// The url to use for reading (use "../" to get to site root, editors should always be one folder in from site root)
        /// </summary>
        [EditMin(1)]
        public String Read;

        /// <summary>
        /// Optional, the url to use for saving the file, argument should be one of the EditSave*File objects. Ex: EditSaveTextFile
        /// </summary>
        [EditAllowNull]
        public String Save;

        /// <summary>
        /// Optional, the url to use for deleting the file, argument should be EditFile
        /// </summary>
        [EditAllowNull]
        public String Delete;

    }

}
