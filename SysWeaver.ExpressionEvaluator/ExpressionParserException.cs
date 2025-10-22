using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    public class ExpressionParserException : ArgumentException
    {
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

        public ExpressionParserException(String message, String expression, int startPosition, int endPosition = -1, Exception inner = null, String argument = null)
            : base(message, argument ?? "expression", inner)
        {
            Expression = expression;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
        public readonly String Expression;
        public readonly int StartPosition;
        public readonly int EndPosition;
    }

}
