using System;

namespace SysWeaver.Net
{
    public sealed class HttpResponseException : Exception
    {

        static String DefMsg(int code)
        {
            switch (code)
            {
                case 404:
                    return "Not Found - The server cannot find the requested resource.";
                case 429:
                    return "Too Many Requests - The client has sent too many requests in a given amount of time.";
            }
            return "Http request error";
        }


        public HttpResponseException(int responseCode, String message = null) : base(message ?? DefMsg(responseCode))
        {
            ResponseCode = responseCode;
        }
        public readonly int ResponseCode;
    }

}