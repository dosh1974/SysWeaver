using System;
using SysWeaver.Compression;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    /// <summary>
    /// Converts objects to and from database blobs
    /// </summary>
    public interface IBlob
    {
        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The object to blobbify</param>
        /// <param name="compLevel">The compression level to use.
        /// Null means use the default level based on the size of the data:
        /// levelSize bytes or less: Best.
        /// Otherwise: Balanced.
        /// </param>
        /// <param name="levelSize">The size threshold for determining the compression level (only applies if compLevel is null)</param>
        /// <returns>A binary blob</returns>
        Byte[] ToBlob<T>(T data, CompEncoderLevels? compLevel = null, int levelSize = 1024);

        /// <summary>
        /// Convert a database blob to an object
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The blob data</param>
        /// <returns>The object</returns>
        T FromBlob<T>(ReadOnlySpan<Byte> data);

        /// <summary>
        /// Convert a blob to an object, or a default instance if the blob is null
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The blob data, may be null</param>
        /// <returns>The object, or a new instance if data is null</returns>
        T NewOrBlob<T>(Byte[] data) where T : new();

    }


}
