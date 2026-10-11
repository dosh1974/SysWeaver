using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{
    /// <summary>
    /// Evaluates tokens in reverse polish notation (postfix), see <see cref="ShuntingYard"/>
    /// </summary>
    public static class PostfixEvaluator
    {
        /// <summary>
        /// Evaluate tokens in reverse polish notation using an operand stack
        /// </summary>
        /// <typeparam name="R">The result (operand) type</typeparam>
        /// <typeparam name="T">The token type</typeparam>
        /// <param name="rpnExpression">The tokens in postfix order</param>
        /// <param name="calc">Computes the value of a token, for operators and functions it pops its arguments from the stack (the last argument is on top), for operands the stack is null</param>
        /// <param name="canEvaluate">Returns true for operators and functions (tokens that take arguments from the stack), false for operands</param>
        /// <param name="exceptionThrowerN">Called with a message, the inner exception and the token when <paramref name="calc"/> throws, it should throw.
        /// If null an <see cref="ArgumentException"/> is thrown</param>
        /// <returns>The value of the expression</returns>
        /// <exception cref="ArgumentException">The expression didn't result in exactly one value (ex: it's empty or has two operands without an operator)</exception>
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
