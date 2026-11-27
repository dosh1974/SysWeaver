using System;
using System.Collections.Generic;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// An action button on a row of the problems table (<see cref="ServiceManager.ProblemsTable"/>), ex: "Allow IP" on an intruder alert
    /// </summary>
    public sealed class ProblemAction
    {
        /// <summary>
        /// The text of the button
        /// </summary>
        public String Text;

        /// <summary>
        /// The tool tip of the button
        /// </summary>
        public String Title;

        /// <summary>
        /// The url of the GET request performed on click (relative to the table page, ex: "../ServerManager/AllowIp?\"1.2.3.4\"").
        /// If it starts with '@' the url is opened in a new tab, with '&amp;' in the same tab.
        /// </summary>
        public String Url;

        /// <summary>
        /// An optional icon class name or url
        /// </summary>
        public String Icon;

        /// <summary>
        /// The confirmation question, null for the text followed by a question mark (ex: "Allow IP?")
        /// </summary>
        public String Confirm;

        /// <summary>
        /// Create an action
        /// </summary>
        /// <param name="text">The text of the button</param>
        /// <param name="title">The tool tip of the button</param>
        /// <param name="url">The url of the GET request performed on click</param>
        /// <param name="icon">An optional icon class name or url</param>
        /// <param name="confirm">The confirmation question, null for the text followed by a question mark</param>
        public ProblemAction(String text, String title, String url, String icon = null, String confirm = null)
        {
            Text = text;
            Title = title;
            Url = url;
            Icon = icon;
            Confirm = confirm;
        }
    }

    /// <summary>
    /// A service that has actions for some of the problems it reports (through the exception trackers of its stats, see <see cref="ServiceManager.GetProblems"/>).
    /// </summary>
    /// <remarks>
    /// The actions are shown as buttons on the row of the problem (at most <see cref="ServiceManager.MaxProblemActions"/>).
    /// The texts and the url can't contain ',', '|' or ';' (they are replaced by a space).
    /// The url is a GET request, so the API it calls must require the right tokens (and should be audited).
    /// </remarks>
    public interface IHaveProblemActions
    {
        /// <summary>
        /// Get the actions of a problem
        /// </summary>
        /// <param name="system">The system of the problem (the system of the stats, usually the name of the service type)</param>
        /// <param name="source">The source of the problem (the name of the exception tracker)</param>
        /// <returns>The actions, null or empty if there are none</returns>
        IEnumerable<ProblemAction> GetProblemActions(String system, String source);
    }
}
