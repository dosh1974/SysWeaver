using System;
using SysWeaver.Remote;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyClientParams : RemoteConnection
    {
        public ReverseProxyClientParams()
        {
            // Use strong auth method by default (no real reason to not use)
            AuthMethod = RemoteAuthMethod.SysWeaverLogin;
        }

        /// <summary>
        /// The id of this client.
        /// This is part of the url to reach this client.
        /// For example, if the base url for the reverse proxy server is https://www.example.com/ReverseProxyFiles/ and ClientId is "TestClient", the (base) url to use to reach this client is:
        /// https://www.example.com/ReverseProxyFiles/TestClient/
        /// </summary>
        public String ClientId = "$(MachineName)";


        /// <summary>
        /// Array of endpoints in the format : "name:destination".
        /// "name" is part of the url to reach the end point.
        /// For example, if the base url for the reverse proxy server is https://www.example.com/ReverseProxyFiles/ and ClientId is "TestClient" and name = "Data", the (base) url to use to reach this end point is:
        /// https://www.example.com/ReverseProxyFiles/TestClient/Data/
        /// "destination" is the local end point that will be accessed.
        /// If it starts with "http://" or "https://" a request is made to that location, ex: http://localhost/publicData/
        /// "https://www.example.com/ReverseProxyFiles/TestClient/Data/Logo.png" will read from "http://localhost/publicData/Logo.png"
        /// If not the data will be requested from the service that hosts this proxy, ex: "publicData"
        /// "https://www.example.com/ReverseProxyFiles/TestClient/Data/Logo.png" will read from this service at "publicData/Logo.png"
        /// If no end points are specified, "name" will be empty and data will be empty, mapping the root of to the root of this service.
        /// "name" must be unique among the end points.
        /// "name" can't be empty for multiple end points.
        /// </summary>
        public String[] EndPoints;


        /// <summary>
        /// Number of connections (long polls) that wait for requests from the server, dont make this too low.
        /// A request that one of them gets is handled on its own task (see <see cref="MaxRequestsInProgress"/>), so it never blocks the polls.
        /// </summary>
        public int MaxConcurrentRequests = 8;

        /// <summary>
        /// Max number of requests that are handled on their own task while the connection that got them polls for the next one at once
        /// (the task sends the response with a poll of its own). A slow request (ex: a long poll such as Api/application/GetMessages,
        /// held up to 90 s) then never holds up the requests behind it, even with few connections.
        /// When this many are in progress, a connection handles its request itself (one at a time, as with 0).
        /// The remote connection's MaxConcurrency is raised to at least MaxConcurrentRequests + MaxRequestsInProgress.
        /// </summary>
        public int MaxRequestsInProgress = 32;

        /// <summary>
        /// The local server path to the revers proxy, this should match the BaseUrl of the ReverseProxyServerParams.
        /// Only used in debug tables.
        /// </summary>
        public String ServerBaseUrl;
    }

}
