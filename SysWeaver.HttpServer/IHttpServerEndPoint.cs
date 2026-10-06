using System;
using System.Collections.Generic;

namespace SysWeaver.Net
{

    /// <summary>
    /// The kind of an <see cref="IHttpServerEndPoint"/>, used when enumerating end points (diagnostic tables, explorers, menus).
    /// </summary>
    public enum HttpServerEndpointTypes
    {
        /// <summary>
        /// The type is not known.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// A (real or virtual) folder. A folder is not accessible as is, typically the "index.html" in the folder is served in it's place.
        /// </summary>
        Folder,
        /// <summary>
        /// A file, accessable using a GET request
        /// </summary>
        File,
        /// <summary>
        /// A Web API method (see <see cref="ApiHttpServerModule"/>).
        /// </summary>
        Api,
        /// <summary>
        /// A file upload end point.
        /// </summary>
        FileUpload,
    }

    /// <summary>
    /// Implemented by objects that are associated with a server relative url.
    /// </summary>
    public interface IHaveUri
    {
        /// <summary>
        /// The url of the end point, relative to the server prefix (no leading '/'), ex: "Api/Debug/GetInfo".
        /// </summary>
        String Uri { get; }
    }

    /// <summary>
    /// Describes an end point exposed by a module (see <see cref="IHttpServerModule.EnumEndPoints(string)"/>).
    /// This is descriptive information only, used for diagnostics and enumeration; it does not affect how requests are handled.
    /// </summary>
    public interface IHttpServerEndPoint : IHaveUri
    {
        
        /// <summary>
        /// The http method of the end point (ex: "GET"), if null this is not a true end point (typically a disc folder or virtual folder).
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
        /// The duration in seconds that the same request should be cached on the server, i.e the data for the same request from multiple clients within this period will only be produced once (reduces server load).
        /// Zero means no server side caching, a negative value means that the per session cache is used.
        /// </summary>
        int RequestCacheDuration { get; }

        /// <summary>
        /// A textual description of the compression methods that may be applied (in order of preference), or null if no compression is applied.
        /// </summary>
        String CompPreference { get; }
        
        /// <summary>
        /// If the data is stored pre-compressed, the http code of the compression method (ex: "br"), else null.
        /// </summary>
        String PreCompressed { get; }

        /// <summary>
        /// Auth information: null = publicly available, empty = any authenticated user, else the security tokens that are required.
        /// </summary>
        IReadOnlyList<String> Auth { get; }

        /// <summary>
        /// A human readable description of where the content comes from (ex: a file name on disc, an embedded resource or a method).
        /// </summary>
        String Location { get; }

        /// <summary>
        /// The size of the content in bytes (as stored, before any runtime compression), or null if unknown or not applicable.
        /// </summary>
        long? Size { get; }

        /// <summary>
        /// Last modified time stamp (UTC).
        /// </summary>
        DateTime LastModified { get;  }

        /// <summary>
        /// The etag to use, may be null for dynamic end points
        /// </summary>
        String ETag { get; }

        /// <summary>
        /// The mime type of the end point content, may be null if unknown or not applicable.
        /// </summary>
        String Mime { get; }

    }
}
