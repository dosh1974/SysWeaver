using System;

namespace SysWeaver.AI
{
#pragma warning disable CS0649

    /// <summary>
    /// Paramaters for some data to be serverd from an url
    /// </summary>
    sealed class OpenAiData
    {
        /// <summary>
        /// The mime of the data.
        /// </summary>
        public String MimeType;

        /// <summary>
        /// The data as an UTF-8 encoded string.
        /// Binary data can be suplied using a base64 data uri, for instance:
        /// "data:text/plain;base64,SGVsbG8h".
        /// The mime encoded in the data uri is ignored, the mime used will be the one in MimeType
        /// </summary>
        public String Data;

        /// <summary>
        /// The title of this data, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

    }

#pragma warning restore CS0649

}
