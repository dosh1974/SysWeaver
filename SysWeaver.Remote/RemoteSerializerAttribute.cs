using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Override the serializer to use for a specific API call (or calls if placed on an interface)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class RemoteSerializerAttribute : Attribute
    {
        /// <summary>
        /// The serializer to use for encoding and decoding, encoding can be different by using the postSerizlier
        /// </summary>
        public readonly String Ser;

        /// <summary>
        /// The serializer to use for encoding, if null the same serializer as for decoding will be used
        /// </summary>
        public readonly String PostSer;

        /// <summary>
        /// Override the serializer to use for a specific API call (or calls if placed on an interface), default is to use the serializer specified in the RemoteConnection
        /// </summary>
        /// <param name="serializer">The name of the serializer to use for this API call (or calls if placed on an interface)</param>
        /// <param name="postSerializer">The name of the serializer to use for encoding data, if null the same serializer will be used for encoding and decoding</param>
        public RemoteSerializerAttribute(String serializer, String postSerializer = null)
        {
            Ser = serializer;
            PostSer = postSerializer;
        }

    }

}
