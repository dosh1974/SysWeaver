using System;
using System.Collections.Generic;

namespace SysWeaver
{

    public class ManagedVars
    {
        /// <summary>
        /// The available html vars
        /// </summary>
        public static readonly IReadOnlySet<String> HtmlVars = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase,
            "[Site]",
            "[User]",
            "[UserName]",
            "[Email]",
            "[Password]",
            "[Link]",
            "[ShortCode]",
            "[Root]",
            "[BackgroundDark]",
            "[Background]",
            "[Color]",
            "[Acc1]",
            "[Acc2]",
            "[Header]",
            "[Footer]",
            "[Origin]",
            "[Logo]",
            "[LogoGradient]",
            "[0]",
            "[1]",
            "[2]",
            "[3]",
            "[4]"
        );

        /// <summary>
        /// The available text message vars
        /// </summary>
        public static readonly IReadOnlySet<String> TextVars = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase,
            "[Site]",
            "[User]",
            "[UserName]",
            "[Email]",
            "[Password]",
            "[Link]",
            "[ShortCode]",
            "[Root]",
            "[Header]",
            "[Footer]",
            "[0]",
            "[1]",
            "[2]",
            "[3]",
            "[4]"
        );

    }


    public class ManagedMailMessage
    {
        public override string ToString() => Subject;
        
        /// <summary>
        /// The subject template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Subject { get; set; }
        
        /// <summary>
        /// The body template, if this is an existing filename, the message is read from that
        /// </summary>
        public String Body { get; set; }
        
        /// <summary>
        /// Set to true if the body is html
        /// </summary>
        public bool IsHtml { get; set; }


        /// <summary>
        /// Get texts
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
                t = ManagedTools.GetTemplate(Subject, IsHtml ? ManagedVars.HtmlVars : ManagedVars.TextVars, GetType(), () => TempSubject = null);
                TempSubject = t;
                return t;
            }
        }

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
                t = ManagedTools.GetTemplate(Body, IsHtml ? ManagedVars.HtmlVars : ManagedVars.TextVars, GetType(), () => TempBody = null);
                TempBody = t;
                return t;
            }
        }
        volatile TextTemplate TempSubject;
        volatile TextTemplate TempBody;
    }


}
