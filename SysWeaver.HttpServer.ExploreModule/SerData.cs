using SysWeaver.Data;
using SysWeaver.Serialization;
using System;

namespace SysWeaver.Net.ExploreModule
{
    [TableDataPrimaryKey(nameof(Name))]
    sealed class SerData
    {
        public SerData(ISerializerInfo i)
        {
            Name = i.Name;
            Extension = i.Extension;
            Icon = i.Extension;
            Mime = i.Mime;
            Encoding = i.Encoding?.EncodingName;
            Prio = i.Prio;
        }

        /// <summary>
        /// The name of the compression implementation
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The serialized formats file extension
        /// </summary>
        [TableDataFileExtension]
        public readonly String Extension;

        /// <summary>
        /// File extension icon
        /// </summary>
        [TableDataFileExtensionImage]
        public readonly String Icon;

        /// <summary>
        /// The mime code for the data created by this serializer
        /// </summary>
        [TableDataMime]
        public readonly String Mime;

        /// <summary>
        /// If the serializer is text-based, this is the econding to use
        /// </summary>
        [TableDataEncoding]
        public readonly String Encoding;

        /// <summary>
        /// The priority (quality) of the compressor, if multiple compressors are available the one with the highest priority is returned by the compression manager
        /// </summary>
        public readonly int Prio;
    }

}
