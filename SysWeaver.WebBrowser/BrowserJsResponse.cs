using System;

namespace SysWeaver.WebBrowser
{
    public sealed class BrowserJsResponse
    {
        public override string ToString() => Ok ? Value?.ToString() : ("Failed: " + Message);


        public readonly bool Ok;
        public readonly Object Value;
        public readonly String Message;

        public BrowserJsResponse(bool ok, object value, string message)
        {
            Ok = ok;
            Value = value;
            Message = message;
        }
    }


}
