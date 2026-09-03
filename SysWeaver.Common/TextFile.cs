using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver
{

    [TableDataPrimaryKey(nameof(Name))]
    public sealed class TextFile
    {
        /// <summary>
        /// The name of this file
        /// </summary>
        [TableDataUrl("{0}", "*../edit/text.html?{1}n={0}&r=../Api/ReadTextFile?\"{0}\"", "Click to view the log file \"{0}\"")]
        public String Name;

        /// <summary>
        /// Optional text view params.
        /// If set, must end with a &amp;.
        /// Can be used to allow deletion.
        /// Ex: ""d=../MyService/DeleteFile&amp;" 
        /// </summary>
        [TableDataHide]
        public String OpenParams = "";

        /// <summary>
        /// The current size in bytes
        /// </summary>
        [TableDataByteSize]
        public long Size;

        /// <summary>
        /// The time when the file was last updated (UTC)
        /// </summary>
        public DateTime LastUpdate;

        /// <summary>
        /// A description of the contents
        /// </summary>
        [TableDataText(60)]
        [AutoTranslate(false)]
        [AutoTranslateContext("The description of a text file.")]
        [AutoTranslateContext("The name of the text file is \"{0}\"", nameof(Name))]
        public String Description;

        /// <summary>
        /// Required auth
        /// </summary>
        [TableDataTags]
        public String Auth;

        /// <summary>
        /// The full path of the file on disc
        /// </summary>
        [TableDataText]
        public String Filename;

    }

    public interface IHaveTextFiles
    {
        /// <summary>
        /// Get meta data about the available files
        /// </summary>
        /// <returns></returns>
        IEnumerable<TextFile> GetTextFiles();

        /// <summary>
        /// Read the content of a file
        /// </summary>
        /// <returns>null if the file is unknown or if it failed to be read, else the binary data of the file</returns>
        Task<ReadOnlyMemory<Byte>> TryReadTextFile(String name);
    }

}
