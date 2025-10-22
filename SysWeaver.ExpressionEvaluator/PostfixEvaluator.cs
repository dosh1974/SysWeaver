using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{
    public static class PostfixEvaluator
    {
        public static R FromRpn<R, T>(IEnumerable<T> rpnExpression, Func<T, Stack<R>, R> calc, Func<T, bool> canEvaluate, Action<String, Exception, T> exceptionThrowerN = null)
        {
            var exceptionThrower = exceptionThrowerN ?? new Action<String, Exception, T>((text, ecx, t) => { throw new ArgumentException(text, "expression", ecx); });
            Stack<R> operands = new Stack<R>();
            foreach (var token in rpnExpression)
            {
                try
                {
                    if (canEvaluate(token))
                    {
                        operands.Push(calc(token, operands));
                        continue;
                    }
                    operands.Push(calc(token, null));
                }
                catch (Exception ex)
                {
                    exceptionThrower("Failed to apply operator \"" + token + "\"", ex, token);
                }
            }
            if (operands.Count != 1)
                throw new ArgumentException("Invalid input expression!", "expression");
            return operands.Pop();
        }
    }

}
