using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Paramaters for the REST API validation tool
    /// </summary>
    public sealed class OpenAiRestCall
    {
        /// <summary>
        /// The absolute url of the REST API end point.
        /// </summary>
        public String ApiUrl;

        /// <summary>
        /// The data to send to the Api, encoded as a json string.
        /// </summary>
        public String PostJsonData;
    }

}
