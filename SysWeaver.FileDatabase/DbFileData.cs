using SimpleStack.Orm.Attributes;
using System;

namespace SysWeaver.FileDatabase
{
    /// <summary>
    /// The base class of a file row in a <see cref="FileDatabase{T}"/>, derive from it to add meta data columns
    /// </summary>
    public abstract class DbFileData
    {
        /// <summary>
        /// The maximum length of the full path, longer file names are ignored
        /// </summary>
        public const int MaxNameLen = 768;
        /// <summary>
        /// The maximum length of the extension (without the '.'), files with longer extensions are ignored
        /// </summary>
        public const int MaxExtLen = 16;

        /// <summary>
        /// The full path of the file (the primary key)
        /// </summary>
        [PrimaryKey]
        [Required]
        [StringLength(MaxNameLen)]
        public String FullPath { get; set; }

        /// <summary>
        /// The file extension in lower case, without the '.' (empty if none)
        /// </summary>
        [Index]
        [Required]
        [StringLength(MaxExtLen)]
        public String Ext { get; set; }

        /// <summary>
        /// When the file was last modified as an UTC tick
        /// </summary>
        [Required]
        [Index]
        public long LastModified { get; set; }

        /// <summary>
        /// Size of the file in bytes
        /// </summary>
        [Required]
        [Index]
        public long Size { get; set; }

        /// <summary>
        /// Time when this file was added to (updated in) the db
        /// </summary>
        [Required]
        [Index]
        public long Changed { get; set; }
    }


}
