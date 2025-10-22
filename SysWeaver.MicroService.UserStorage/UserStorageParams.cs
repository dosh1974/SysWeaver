using System;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed class UserStorageParams
    {

        /// <summary>
        /// The base url, default = null = "storage"
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// The folders
        /// </summary>
        public String[] Folders;

        /// <summary>
        /// The compression method to use (can be null to disable compression, can be useful for small data or incompressible data)
        /// </summary>
        public String Comp = "br";

        /// <summary>
        /// The compression level to use
        /// </summary>
        public CompEncoderLevels Level = CompEncoderLevels.Balanced;

        /// <summary>
        /// Default data retention plan
        /// </summary>
        public UserStorageDataRetention Retention;

        /// <summary>
        /// The serializer to use for links
        /// </summary>
        public String Ser = "json";

        /// <summary>
        /// Use this to enabled uploading of public files.
        /// null = disabled uplaoding of public files.
        /// </summary>
        public UserStorageUpload UploadPublic;

        /// <summary>
        /// Use this to enabled uploading of protected files.
        /// null = disabled uplaoding of protected files.
        /// </summary>
        public UserStorageUpload UploadProtected = new UserStorageUpload();

        /// <summary>
        /// Use this to enabled uploading of private files.
        /// null = disabled uplaoding of private files.
        /// </summary>
        public UserStorageUpload UploadPrivate;
    }

}
