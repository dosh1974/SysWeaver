using System;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="UserStorageService"/>
    /// </summary>
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
        /// The compression method to use, default is "br" (can be null to disable compression, can be useful for small data or incompressible data)
        /// </summary>
        public String Comp = "br";

        /// <summary>
        /// The compression level to use (default is balanced)
        /// </summary>
        public CompEncoderLevels Level = CompEncoderLevels.Balanced;

        /// <summary>
        /// Default data retention plan
        /// </summary>
        public UserStorageDataRetention Retention;

        /// <summary>
        /// The serializer to use for links, default is "json"
        /// </summary>
        public String Ser = "json";

        /// <summary>
        /// Use this to enable uploading of public files.
        /// null = disabled uploading of public files (the default).
        /// </summary>
        public UserStorageUpload UploadPublic;

        /// <summary>
        /// Use this to enable uploading of protected files.
        /// null = disabled uploading of protected files (default is enabled with default settings).
        /// </summary>
        public UserStorageUpload UploadProtected = new UserStorageUpload();

        /// <summary>
        /// Use this to enable uploading of private files.
        /// null = disabled uploading of private files (the default).
        /// </summary>
        public UserStorageUpload UploadPrivate;
    }

}
