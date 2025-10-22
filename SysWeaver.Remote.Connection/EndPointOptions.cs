using SysWeaver.Serialization;
using System;

namespace SysWeaver.Remote
{
    public sealed class EndPointOptions
    {
        public readonly ISerializerType Ser;
        public readonly ISerializerType PostSer;
        public readonly int TimeOutInMilliSeconds;

        /// <summary>
        /// Changes to this must be reflected in the 
        /// </summary>
        /// <param name="ser"></param>
        /// <param name="postSer"></param>
        /// <param name="timeOutInMilliSeconds"></param>
        public EndPointOptions(String ser, String postSer, int timeOutInMilliSeconds)
        {
            var s = String.IsNullOrEmpty(ser) ? null : SerManager.Get(ser);
            Ser = s;
            PostSer = String.IsNullOrEmpty(postSer) ? s : SerManager.Get(postSer);
            TimeOutInMilliSeconds = timeOutInMilliSeconds;
        }
    }

}
