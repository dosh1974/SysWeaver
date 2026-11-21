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
        /// The base url (relative to the site root) that stored files and links are served below, default = null = "storage" ("" serves them at the root)
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// The storage folders, files are spread over them (the index of the folder is part of the url, so don't remove or reorder folders that hold data).
        /// Can use path variables, ex: "$(CommonApplicationData)".
        /// Null or empty uses a "UserStorage" sub folder in each of the application data folders (shared by all OS users).
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
        /// Default data retention plan (days to keep per scope after the last access and the disc quota), null uses the <see cref="UserStorageDataRetention"/> defaults.
        /// An <see cref="IUserStoragePerUserHandler"/> service can return another plan per user.
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
