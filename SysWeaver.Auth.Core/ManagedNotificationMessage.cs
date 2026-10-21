using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// A mail message template (subject and body), where each part can be a file name, an embedded resource name or the template text itself.
    /// </summary>
    /// <remarks>
    /// Templates are parsed lazily and cached, if a part is read from a file the cached template is dropped when the file changes.
    /// Thread safe.
    /// </remarks>
    public class ManagedNotificationMessage
    {
        /// <summary>
        /// Create a mail message template that only uses the standard variables.
        /// </summary>
        public ManagedNotificationMessage()
        {
        }

        /// <summary>
        /// Create a mail message template that may use some additional variables.
        /// </summary>
        /// <param name="vars">Additional variables, ex: "[Amount]"</param>
        public ManagedNotificationMessage(IReadOnlySet<String> vars)
        {
            Vars = vars.Freeze();
        }

        /// <summary>
        /// Additional variables (in addition to <see cref="ManagedVars"/>) that the templates may use, may be null.
        /// </summary>
        public readonly IReadOnlySet<String> Vars;

        /// <inheritdoc/>
        public override string ToString() => Headline;

        /// <summary>
        /// The subject template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Headline { get; set; }

        /// <summary>
        /// The body template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Body { get; set; }


        /// <summary>
        /// Evaluate the subject and body templates
        /// </summary>
        /// <param name="subject">The subject after evaluation</param>
        /// <param name="body">The body after evaluation</param>
        /// <param name="vars">The variables</param>
        public void GetMessage(out string subject, out string body, IReadOnlyDictionary<String, String> vars)
        {
            subject = GetSubject().Get(vars);
            body = GetBody().Get(vars);
        }

        /// <summary>
        /// Get the text template for the subject
        /// </summary>
        /// <returns>A text template</returns>
        public TextTemplate GetSubject()
        {
            var t = TempSubject;
            if (t != null)
                return t;
            lock (this)
            {
                t = TempSubject;
                if (t != null)
                    return t;
                t = ManagedTools.GetTemplate(Headline, AllVars, GetType(), () => TempSubject = null);
                TempSubject = t;
                return t;
            }
        }

        IReadOnlySet<String> AllVars => ManagedVars.TextVars.Merge(true, Vars);


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
        volatile TextTemplate TempSubject;
        volatile TextTemplate TempBody;
    }



}
