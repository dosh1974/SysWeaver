using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.MicroService
{
    public partial class ServiceManager
    {
        /// <summary>
        /// The maximum number of action buttons on a row of the problems table (see <see cref="IHaveProblemActions"/>)
        /// </summary>
        public const int MaxProblemActions = 4;

        /// <summary>
        /// The number of value parts per action in <see cref="ProblemRow.Actions"/>: text, title, url, icon, confirm and visible
        /// </summary>
        const int ProblemActionParts = 6;

        /// <summary>
        /// Remove the separators of the action format (',' separates the value parts, '|' and ';' the formats)
        /// </summary>
        static String ProblemActionPart(String s) => (s ?? "").Replace(',', ' ').Replace('|', ' ').Replace(';', ' ');

        /// <summary>
        /// Add the actions of the services that implement <see cref="IHaveProblemActions"/> to the problems
        /// </summary>
        /// <param name="rows">The problems</param>
        /// <returns>The problems</returns>
        List<ProblemRow> WithProblemActions(List<ProblemRow> rows)
        {
            var handlers = GetAll<IHaveProblemActions>().ToList();
            if (handlers.Count == 0)
                return rows;
            foreach (var row in rows)
                row.Actions = GetProblemActions(handlers, row.System, row.Source);
            return rows;
        }

        /// <summary>
        /// Get the value of the actions column of a problem: "-" followed by <see cref="ProblemActionParts"/> parts per action
        /// (the unused actions have empty parts, so they are hidden)
        /// </summary>
        /// <returns>The value, null if there are no actions</returns>
        static String GetProblemActions(List<IHaveProblemActions> handlers, String system, String source)
        {
            StringBuilder sb = null;
            int n = 0;
            foreach (var h in handlers)
            {
                IEnumerable<ProblemAction> actions;
                try
                {
                    actions = h.GetProblemActions(system, source);
                }
                catch
                {
                    continue;
                }
                if (actions == null)
                    continue;
                foreach (var a in actions)
                {
                    if ((a == null) || String.IsNullOrEmpty(a.Url))
                        continue;
                    if (n >= MaxProblemActions)
                        break;
                    sb ??= new StringBuilder("-");
                    sb
                        .Append(',').Append(ProblemActionPart(a.Text))
                        .Append(',').Append(ProblemActionPart(a.Title))
                        .Append(',').Append(ProblemActionPart(a.Url))
                        .Append(',').Append(ProblemActionPart(a.Icon))
                        .Append(',').Append(ProblemActionPart(a.Confirm ?? (a.Text + "?")))
                        .Append(",1");
                    ++n;
                }
            }
            if (sb == null)
                return null;
            for (; n < MaxProblemActions; ++n)
                sb.Append(',', ProblemActionParts);
            return sb.ToString();
        }
    }
}
