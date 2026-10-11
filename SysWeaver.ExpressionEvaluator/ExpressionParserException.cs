using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    /// <summary>
    /// Thrown when an expression can't be parsed, contains the expression and the position of the error
    /// </summary>
    public class ExpressionParserException : ArgumentException
    {
        /// <summary>
        /// The quoted expression, the position (if known) followed by a line that marks the first and last character of the error with a '^', and the base exception text
        /// </summary>
        /// <returns>A multi line text</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(1024);
            sb.Append('"').Append(Expression).Append('"');
            if (StartPosition >= 0)
            {
                sb.Append(" @ ").Append(StartPosition);
                if (EndPosition > StartPosition)
                    sb.Append(" to ").Append(EndPosition);
                sb.AppendLine();
                sb.Append(new String(' ', StartPosition + 1));
                sb.Append('^');
                if (EndPosition > StartPosition)
                {
                    var d = EndPosition - StartPosition - 1;
                    if (d > 1)
                        sb.Append(new String(' ', d - 1));
                    if (d > 0)
                        sb.Append('^');
                }
            }
            sb.AppendLine().Append(base.ToString());
            return sb.ToString();
        }

        /// <summary>
        /// Create an exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="expression">The expression that failed to parse</param>
        /// <param name="startPosition">The position of the first character of the error (negative if unknown)</param>
        /// <param name="endPosition">The position after the last character of the error (exclusive, -1 if unknown)</param>
        /// <param name="inner">An optional inner exception</param>
        /// <param name="argument">The parameter name, null for "expression"</param>
        public ExpressionParserException(String message, String expression, int startPosition, int endPosition = -1, Exception inner = null, String argument = null)
            : base(message, argument ?? "expression", inner)
        {
            Expression = expression;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
        /// <summary>
        /// The expression that failed to parse
        /// </summary>
        public readonly String Expression;
        /// <summary>
        /// The position of the first character of the error (negative if unknown)
        /// </summary>
        public readonly int StartPosition;
        /// <summary>
        /// The position after the last character of the error (exclusive, -1 if unknown)
        /// </summary>
        public readonly int EndPosition;
    }

}
