using System;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    public interface IBlob
    {
        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        Byte[] ToBlob<T>(T data);

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
