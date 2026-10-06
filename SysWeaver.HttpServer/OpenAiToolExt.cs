using SysWeaver.AI;
using System;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Extensions that let API methods invoked as AI tools interact with the AI chat (add links and files to the response message).
    /// </summary>
    /// <remarks>
    /// The AI service stores an <see cref="IAiToolContext"/> in <see cref="HttpServerRequest.Properties"/> under <see cref="RequestAiToolContext"/>
    /// before invoking a tool.
    /// Calling these outside of an AI tool invocation is a no-op.
    /// </remarks>
    public static class OpenAiToolExt
    {

        /// <summary>
        /// The key used to store the tool context in <see cref="HttpServerRequest.Properties"/>.
        /// </summary>
        public const String RequestAiToolContext = "AiToolContext";


        /// <summary>
        /// Add a link to something (displayed in chat).
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="url">The local or absolute url to the file.</param>
        public static void OpenAiAddLink(this HttpServerRequest request, String url)
        {
            request.Properties.TryGetValue(RequestAiToolContext, out var o);
            var c = o as IAiToolContext;
            if (c == null)
                return;
            c.AddLink(url);
        }

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file, as text</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file, null if the request isn't an AI tool invocation (or the context is of another type)</returns>
        public static String OpenAiAddMessageFile(this HttpServerRequest request, String mime, String data, String filename)
        {
            request.Properties.TryGetValue(RequestAiToolContext, out var o);
            var c = o as IAiToolContext;
            if (c == null)
                return null;
            return c.AddMessageFile(mime, data, filename);
        }

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The binary data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file, null if the request isn't an AI tool invocation (or the context is of another type)</returns>
        public static String OpenAiAddMessageFile(this HttpServerRequest request, String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            request.Properties.TryGetValue(RequestAiToolContext, out var o);
            var c = o as IAiToolContext;
            if (c == null)
                return null;
            return c.AddMessageFile(mime, data, filename);
        }


    }


}
