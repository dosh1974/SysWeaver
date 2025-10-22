using System;
using System.Collections.Generic;

namespace SysWeaver.Net
{

    public enum HttpServerEndpointTypes
    {
        Unknown = 0,
        /// <summary>
        /// A folder is not accessable as is, typically the index.htm in the folder is accessed in it's place
        /// </summary>
        Folder,
        /// <summary>
        /// A file, accessable using a GET request
        /// </summary>
        File,
        /// <summary>
        /// A api call
        /// </summary>
        Api,
        /// <summary>
        /// File upload
        /// </summary>
        FileUpload,
    }

    public interface IHaveUri
    {
        /// <summary>
        /// The Uri of the end point
        /// </summary>
        String Uri { get; }
    }

    public interface IHttpServerEndPoint : IHaveUri
    {
        
        /// <summary>
        /// The http method of the end point, if null this is not a true end point (typically a disc folder or virtual folder)
        /// </summary>
        String Method { get; }


        /// <summary>
        /// The type of end point
        /// </summary>
        HttpServerEndpointTypes Type { get; }

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        int ClientCacheDuration { get; }
        
        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// </summary>
        int RequestCacheDuration { get; }

        /// <summary>
        /// True if the input is from a stream
        /// </summary>
        bool UseStream { get; }

        /// <summary>
        /// The compression method to use (in order of preferens) or null if no compression should be applied
        /// </summary>
        String CompPreference { get; }
        
        /// <summary>
        /// If the data is pre-compressed, the compression method is shown here
        /// </summary>
        String PreCompressed { get; }

        /// <summary>
        /// Auth information, null = open, empty = auth required or comma separted tokens that are required
        /// </summary>
        IReadOnlyList<String> Auth { get; }

        /// <summary>
        /// The location information
        /// </summary>
        String Location { get; }

        /// <summary>
        /// Size
        /// </summary>
        long? Size { get; }

        /// <summary>
        /// Last modified time stamp
        /// </summary>
        DateTime LastModified { get;  }
        
        /// <summary>
        /// The mime of the end point
        /// </summary>
        String Mime { get; }

    }
}
