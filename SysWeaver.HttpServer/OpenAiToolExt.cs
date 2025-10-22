using SysWeaver.AI;
using System;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public static class OpenAiToolExt
    {

        /// <summary>
        /// The key to used to store the tool context into a request
        /// </summary>
        public const String RequestAiToolContext = "AiToolContext";


        /// <summary>
        /// Add a link to something (displayed in chat)
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <param name="url">The local or absolute url to the file</param>
        public static void OpenAiAddLink(this HttpServerRequest request, String url)
        {
            var c = request.Properties[RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return;
            c.AddLink(url);
        }

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        public static String OpenAiAddMessageFile(this HttpServerRequest request, String mime, String data, String filename)
        {
            var c = request.Properties[RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return null;
            return c.AddMessageFile(mime, data, filename);
        }

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        public static String OpenAiAddMessageFile(this HttpServerRequest request, String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            var c = request.Properties[RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return null;
            return c.AddMessageFile(mime, data, filename);
        }


    }


}
