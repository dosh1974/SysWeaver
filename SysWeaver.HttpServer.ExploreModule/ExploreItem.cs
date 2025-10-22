using System;

namespace SysWeaver.Net.ExploreModule
{
    sealed class ExploreItem
    {
        /// <summary>
        /// The name of the end point
        /// </summary>
        public String Name;

        /// <summary>
        /// The http method of the end point, if null this is not a true end point (typically a disc folder or virtual folder)
        /// </summary>
        public String Method;


        /// <summary>
        /// The type of end point
        /// </summary>
        public HttpServerEndpointTypes Type;

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        public int ClientCacheDuration;

        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// </summary>
        public int RequestCacheDuration;

        /// <summary>
        /// If true, the request is cached per session else it's cached globally
        /// </summary>
        public bool PerSession;

        /// <summary>
        /// The compression method to use (in order of preferens) or null if no compression should be applied
        /// </summary>
        public String CompPreference;

        /// <summary>
        /// If the data is pre-compressed, the compression method is shown here
        /// </summary>
        public String PreCompressed;
        
        /// <summary>
        /// Auth information, null = open, empty = auth required or comma separted tokens that are required
        /// </summary>
        public String Auth;
        
        /// <summary>
        /// The location information
        /// </summary>
        public String Location;

        /// <summary>
        /// Size
        /// </summary>
        public long? Size;

        /// <summary>
        /// Last modified time stamp
        /// </summary>
        public DateTime LastModified;

        /// <summary>
        /// The mime of the end point
        /// </summary>
        public String Mime;
    }

}
