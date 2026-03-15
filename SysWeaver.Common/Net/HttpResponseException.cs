using System;

namespace SysWeaver.Net
{
    public sealed class HttpResponseException : Exception
    {

        static String DefMsg(int code)
        {
            String text = "Http request error [";
            switch (code)
            {
                case 404:
                    text = "Not Found - The server cannot find the requested resource [";
                    break;
                case 429:
                    text = "Too Many Requests - The client has sent too many requests in a given amount of time [";
                    break;
            }
            return String.Concat(text, code, ']');
        }


        public HttpResponseException(int responseCode, String message = null, String translateFrom = "en") : base(message ?? DefMsg(responseCode))
        {
            ResponseCode = responseCode;
            Translate = translateFrom;
        }
        public readonly int ResponseCode;
        public readonly String Translate;
    }

}