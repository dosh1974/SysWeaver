using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Callback function for monitoring remote api calls, called when a remote api call begin
    /// </summary>
    /// <param name="id">A unique id for the request (incremental counter), this is matched in the request end callbacks</param>
    /// <param name="url">The url of the request</param>
    /// <param name="type">The type of request</param>
    /// <param name="timeout">The timeout for this request in milli seconds</param>
    /// <param name="requestPayloadSerializer">The serializer used to create the payload (only for POST and PUT request)</param>
    /// <param name="requestPayload">The request payload (only for POST and PUT)</param>
    /// <param name="requestPayloadSize">The size of the request payload, do not use requestPayload.Length (only for POST and PUT)</param>
    public delegate void RemoteApiCallBegin(long id, String url, HttpEndPointTypes type, int timeout, String requestPayloadSerializer, ReadOnlyMemory<Byte> requestPayload, int requestPayloadSize);

    /// <summary>
    /// Callback function for monitoring remote api calls, called when a remote api call end
    /// </summary>
    /// <param name="id">A unique id for the request (incremental counter), this is matched in the request end callbacks</param>
    /// <param name="ex">The exception if an error occured, else null</param>
    /// <param name="statusCode">The HTTP response code or 0 when an exception happened</param>
    /// <param name="responsePayloadSerizlier">The serializer used to deserialize the response payload</param>
    /// <param name="responsePayload">The response payload</param>
    public delegate void RemoteApiCallEnd(long id, Exception ex, int statusCode, String responsePayloadSerizlier, ref Memory<Byte> responsePayload);

    /// <summary>
    /// The generated instance of any remote api interface implements this interface.
    /// This functionality adds some debug/monitoring functionality, interfaces can inherit from this or instances can be casted to this.
    /// </summary>
    public interface IRemoteApi : IDisposable
    {
        /// <summary>
        /// Cancels all pending remote api calls on the remote connection.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Invoked before any remote api call (after any payload serilization).
        /// </summary>
        event RemoteApiCallBegin OnCallBegin;

        /// <summary>
        /// Invoked after any remote api call (before any response payload deserilization).
        /// Will always get called if OnCallBegin is called, that way calls in progress can be tracked and so on.
        /// </summary>
        event RemoteApiCallEnd OnCallEnd;


    }

}
