using SimpleStack.Orm.Attributes;
using System;

namespace SysWeaver.FileDatabase
{
    public abstract class DbFileData
    {
        public const int MaxNameLen = 768;
        public const int MaxExtLen = 16;

        /// <summary>
        /// Name of the file 
        /// </summary>
        [PrimaryKey]
        [Required]
        [StringLength(MaxNameLen)]
        public String FullPath { get; set; }

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
