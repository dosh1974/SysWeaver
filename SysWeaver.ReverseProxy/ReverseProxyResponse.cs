using System;

namespace SysWeaver.ReverseProxy
{

    public class ReverseProxyBase
    {
        public String ClientId;
        public String RequestId;
        public String[] Headers;
        public Byte[] Data;
    }


    public class ReverseProxyResponse : ReverseProxyBase
    {
        public int StatusCode;
    }

}
