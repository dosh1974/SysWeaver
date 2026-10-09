using System;
using SysWeaver.Compression;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    public interface IBlob
    {
        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">The object to blobbify</param>
        /// <param name="compLevel">The compression level to use.
        /// Null means use the default level based on the size of the data:
        /// 4KB or less: Best.
        /// Otherwise: Balanced.
        /// </param>
        /// <param name="levelSize">The size threshold for determining the compression level (only applies if compLevel is null)</param>
        /// <returns>A binary blob</returns>
        Byte[] ToBlob<T>(T data, CompEncoderLevels? compLevel = null, int levelSize = 1024);

        /// <summary>
        /// Convert a database blob to an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        T FromBlob<T>(ReadOnlySpan<Byte> data);

        /// <summary>
        /// Convert a blob to an object, or a default instance if the blob is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        T NewOrBlob<T>(Byte[] data) where T : new();

    }


}
