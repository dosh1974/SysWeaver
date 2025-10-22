using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public interface IFileRepo
    {
        /// <summary>
        /// The unique id for this instance, uploads are typically done to "upload/{Key}/{Filename}"
        /// </summary>
        String Key { get; }

        /// <summary>
        /// Check if files can/should be uploaded
        /// </summary>
        /// <param name="info">Information about some files</param>
        /// <param name="r">The request context (check for auth etc)</param>
        /// <returns>An array of matching size with the result for each file</returns>
        ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r);

        /// <summary>
        /// Upload a single file
        /// </summary>
        /// <param name="s">The stream to the file data</param>
        /// <param name="file">Information about the file</param>
        /// <param name="r">The request context (check for auth etc)</param>
        /// <param name="decoder">The data may be compressed, if so, this is the decoder to use to decompress it</param>
        /// <returns>The result of the operation, typically negative for failures</returns>
        ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder);

        /// <summary>
        /// If non-null, the files in these folders (typically where the uploaded files end up) will be accessable 
        /// </summary>
        IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders { get; }

        /// <summary>
        /// The required auth for uploading files (null = no auth required, "" = no special auth token is required, but user must be authenticated)
        /// </summary>
        String UploadAuth { get; }

    }

}
