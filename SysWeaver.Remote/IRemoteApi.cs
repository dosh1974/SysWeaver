using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Callback function for monitoring remote api calls, called when a remote api call begin
    /// </summary>
    /// <param name="id">A unique id for the request (incremental counter per connection, starting at 1), this is matched in the request end callbacks</param>
    /// <param name="url">The absolute url of the request</param>
    /// <param name="type">The type of request</param>
    /// <param name="timeout">The end point timeout in milli seconds (from <see cref="RemoteTimeoutAttribute"/> on the method), 0 if the connection timeout is used</param>
    /// <param name="requestPayloadSerializer">The name of the serializer used to create the payload (only for POST and PUT request, else null)</param>
    /// <param name="requestPayload">The request payload as sent (after any compression, only for POST and PUT, else empty)</param>
    /// <param name="requestPayloadSize">The size of the request payload, do not use requestPayload.Length (only for POST and PUT, else 0)</param>
    public delegate void RemoteApiCallBegin(long id, String url, HttpEndPointTypes type, int timeout, String requestPayloadSerializer, ReadOnlyMemory<Byte> requestPayload, int requestPayloadSize);

    /// <summary>
    /// Callback function for monitoring remote api calls, called when a remote api call end
    /// </summary>
    /// <param name="id">A unique id for the request (incremental counter), this is matched in the request end callbacks</param>
    /// <param name="ex">The exception if an error occured, else null</param>
    /// <param name="statusCode">The HTTP response code or 0 when an exception happened</param>
    /// <param name="responsePayloadSerizlier">The name of the serializer used to deserialize the response payload (null for errors and methods without a return value)</param>
    /// <param name="responsePayload">The response payload (empty when an exception happened and for successful methods without a return value).
    /// For a successful call with a return value, the callback may replace it (the replacement is deserialized instead).</param>
    public delegate void RemoteApiCallEnd(long id, Exception ex, int statusCode, String responsePayloadSerizlier, ref Memory<Byte> responsePayload);

    /// <summary>
    /// The generated instance of any remote api interface implements this interface.
    /// This functionality adds some debug/monitoring functionality, interfaces can inherit from this or instances can be casted to this.
    /// </summary>
    public interface IRemoteApi : IDisposable
    {
        /// <summary>
        /// Cancels all pending remote api calls on the remote connection (they fail with an exception wrapping an <see cref="OperationCanceledException"/>).
        /// The connection can still be used for new calls.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Invoked before any remote api call (after any payload serilization), not invoked for responses served from the response cache.
        /// </summary>
        event RemoteApiCallBegin OnCallBegin;

        /// <summary>
        /// Invoked after any remote api call (before any response payload deserilization).
        /// Will always get called if OnCallBegin is called, that way calls in progress can be tracked and so on.
        /// </summary>
        event RemoteApiCallEnd OnCallEnd;


    }

}
