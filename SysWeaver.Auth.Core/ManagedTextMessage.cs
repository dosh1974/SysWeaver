using System;
using System.Collections.Generic;

namespace SysWeaver
{
    public class ManagedTextMessage
    {


        /// <summary>
        /// The body template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Body { get; set; }

        /// <summary>
        /// Get text
        /// </summary>
        /// <param name="vars">The variables</param>
        public String GetMessage(IReadOnlyDictionary<String, String> vars)
            => GetBody().Get(vars);

        /// <summary>
        /// Get the text template for the body
        /// </summary>
        /// <returns>A text template</returns>
        public TextTemplate GetBody()
        {
            var t = TempBody;
            if (t != null)
                return t;
            lock (this)
            {
                t = TempBody;
                if (t != null)
                    return t;
                t = ManagedTools.GetTemplate(Body, ManagedVars.TextVars, GetType(), () => TempBody = null);
                TempBody = t;
                return t;
            }
        }

        volatile TextTemplate TempBody;
        public ManagedTextMessage()
        {
        }

        public ManagedTextMessage(String body)
        {
            Body = body;
            TempBody = null;
        }

    }


}
