using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// A text (SMS / message) template, where the body can be a file name, an embedded resource name or the template text itself.
    /// </summary>
    /// <remarks>
    /// The template is parsed lazily and cached, if the body is read from a file the cached template is dropped when the file changes.
    /// Thread safe.
    /// </remarks>
    public class ManagedTextMessage
    {


        /// <summary>
        /// The body template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Body { get; set; }

        /// <summary>
        /// Evaluate the body template
        /// </summary>
        /// <param name="vars">The variables</param>
        /// <returns>The message text</returns>
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
                t = ManagedTools.GetTemplate(Body, AllVars, GetType(), () => TempBody = null);
                TempBody = t;
                return t;
            }
        }

        IReadOnlySet<String> AllVars => ManagedVars.TextVars.Merge(true, Vars);


        volatile TextTemplate TempBody;
        /// <summary>
        /// Create a text message template, set <see cref="Body"/> before use.
        /// </summary>
        /// <param name="vars">Additional variables (in addition to <see cref="ManagedVars.TextVars"/>), may be null</param>
        public ManagedTextMessage(IReadOnlySet<String> vars)
        {
            Vars = vars.Freeze();
        }

        /// <summary>
        /// Create a text message template.
        /// </summary>
        /// <param name="body">A file name, embedded resource name or the template text</param>
        /// <param name="vars">Additional variables (in addition to <see cref="ManagedVars.TextVars"/>), may be null</param>
        public ManagedTextMessage(String body, IReadOnlySet<String> vars)
        {
            Body = body;
            Vars = vars.Freeze();
        }

        /// <summary>
        /// Additional variables that the template may use (frozen), may be null.
        /// </summary>
        public readonly IReadOnlySet<String> Vars;

    }


}
