using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver
{

    /// <summary>
    /// Meta data about a text file (ex: a log file) that can be listed and viewed using the web UI (see <see cref="IHaveTextFiles"/>).
    /// The attributes controls how it's presented in a table.
    /// </summary>
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

    /// <summary>
    /// Implemented by services that exposes text files (ex: log files) that can be listed and read (ex: by the server manager).
    /// </summary>
    public interface IHaveTextFiles
    {
        /// <summary>
        /// Get meta data about the available files
        /// </summary>
        /// <returns>The meta data of all available files</returns>
        IEnumerable<TextFile> GetTextFiles();

        /// <summary>
        /// Read the content of a file
        /// </summary>
        /// <param name="name">The name of the file (<see cref="TextFile.Name"/>)</param>
        /// <returns>Empty memory (default) if the file is unknown or if it failed to be read, else the binary data of the file</returns>
        Task<ReadOnlyMemory<Byte>> TryReadTextFile(String name);
    }

}
