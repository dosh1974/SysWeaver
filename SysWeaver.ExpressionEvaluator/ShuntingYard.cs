using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    /// <summary>
    /// The class of a token
    /// </summary>
    public enum TokenClasses
    {
        /// <summary>
        /// A left associative operator (a - b - c = (a - b) - c), also used for unary (prefix) operators
        /// </summary>
        LeftAssociativeOperator,
        /// <summary>
        /// A right associative operator (a ^ b ^ c = a ^ (b ^ c))
        /// </summary>
        RightAssociativeOperator,
        /// <summary>
        /// A value (constant or parameter)
        /// </summary>
        Operand,
        /// <summary>
        /// A function (followed by its arguments in parentheses)
        /// </summary>
        Function,
        /// <summary>
        /// An opening parenthesis
        /// </summary>
        OpenParentheses,
        /// <summary>
        /// A closing parenthesis
        /// </summary>
        CloseParentheses,
        /// <summary>
        /// The separator between function arguments
        /// </summary>
        ArgumentSeparator
    }

    /// <summary>
    /// The shunting-yard algorithm: converts infix tokens to reverse polish notation (postfix)
    /// </summary>
    public static class ShuntingYard
    {
        static readonly IReadOnlySet<TokenClasses> Unary = ReadOnlyData.Set(
            TokenClasses.LeftAssociativeOperator,
            TokenClasses.RightAssociativeOperator,
            TokenClasses.OpenParentheses,
            TokenClasses.ArgumentSeparator
        );

        /// <summary>
        /// Convert infix tokens to reverse polish notation (postfix), lazily.
        /// An operator at the start, after another operator, after an opening parenthesis or after an argument separator is a unary (prefix) operator,
        /// it's replaced by the token returned by <paramref name="toUnary"/>.
        /// Operators with a higher precedence are applied first.
        /// </summary>
        /// <typeparam name="T">The token type</typeparam>
        /// <param name="tokens">The tokens in infix order</param>
        /// <param name="getTokenClass">Get the class of a token</param>
        /// <param name="getOperatorPrecedence">Get the precedence of an operator token (higher binds tighter)</param>
        /// <param name="toUnary">Get the unary version of an operator token (may throw if the operator has no unary version)</param>
        /// <param name="exceptionCasterN">Called with a message, an optional inner exception and the offending token when the input is invalid, it should throw.
        /// If null an <see cref="ArgumentException"/> is thrown</param>
        /// <returns>The tokens in postfix order, parentheses and argument separators are removed</returns>
        public static IEnumerable<T> ReversePolishNotation<T>(IEnumerable<T> tokens, Func<T, TokenClasses> getTokenClass, Func<T, int> getOperatorPrecedence, Func<T, T> toUnary, Action<String, Exception, T> exceptionCasterN = null)
            => ReversePolishNotation(tokens, getTokenClass, getOperatorPrecedence, toUnary, null, exceptionCasterN);

        /// <summary>
        /// Convert infix tokens to reverse polish notation (postfix), lazily.
        /// An operator at the start, after another operator, after an opening parenthesis or after an argument separator is a unary (prefix) operator,
        /// it's replaced by the token returned by <paramref name="toUnary"/>.
        /// Operators with a higher precedence are applied first.
        /// The arguments of a function call ("f(a, b)") are counted, and the function token is replaced by the token returned by <paramref name="withArgumentCount"/>
        /// (a function that isn't followed by an opening parenthesis is returned as is).
        /// </summary>
        /// <typeparam name="T">The token type</typeparam>
        /// <param name="tokens">The tokens in infix order</param>
        /// <param name="getTokenClass">Get the class of a token</param>
        /// <param name="getOperatorPrecedence">Get the precedence of an operator token (higher binds tighter)</param>
        /// <param name="toUnary">Get the unary version of an operator token (may throw if the operator has no unary version)</param>
        /// <param name="withArgumentCount">Get the version of a function token taking the given number of arguments (0 for "f()"), may throw if there is none.
        /// If null the function tokens are returned as is</param>
        /// <param name="exceptionCasterN">Called with a message, an optional inner exception and the offending token when the input is invalid, it should throw.
        /// If null an <see cref="ArgumentException"/> is thrown</param>
        /// <returns>The tokens in postfix order, parentheses and argument separators are removed</returns>
        public static IEnumerable<T> ReversePolishNotation<T>(IEnumerable<T> tokens, Func<T, TokenClasses> getTokenClass, Func<T, int> getOperatorPrecedence, Func<T, T> toUnary, Func<T, int, T> withArgumentCount, Action<String, Exception, T> exceptionCasterN = null)
        {
            var exceptionCaster = exceptionCasterN ?? new Action<String, Exception, T>((message, innerExp, token) => { throw new ArgumentException(message + "\n" + token, "tokens", innerExp); });
            Stack<T> stack = new Stack<T>();
            //  One entry per open parenthesis: the number of arguments found so far if it's the parenthesis of a function call, else -1
            Stack<int> argCounts = withArgumentCount == null ? null : new Stack<int>();
            int tokenIndex = 0;
            TokenClasses prev = TokenClasses.OpenParentheses;
            foreach (var tokenIt in tokens)
            {
                var token = tokenIt;
                var tokenClass = getTokenClass(token);
                switch (tokenClass)
                {
                    case TokenClasses.Operand:
                        yield return token;
                        break;
                    case TokenClasses.Function:
                        stack.Push(token);
                        break;
                    case TokenClasses.ArgumentSeparator:
                        for (; ; )
                        {
                            if (stack.Count == 0)
                                exceptionCaster("Misplaced argument separator token", null, token);
                            T t = stack.Peek();
                            if (getTokenClass(t) == TokenClasses.OpenParentheses)
                                break;
                            stack.Pop();
                            yield return t;
                        }
                        if ((argCounts != null) && (argCounts.Count > 0) && (argCounts.Peek() >= 0))
                            argCounts.Push(argCounts.Pop() + 1);
                        break;
                    case TokenClasses.LeftAssociativeOperator:
                        if (Unary.Contains(prev))
                        {
                            try
                            {
                                token = toUnary(token);
                            }
                            catch (Exception ex)
                            {
                                exceptionCaster("Failed to make unary token from \"" + token + "\"", ex, token);
                            }
                            tokenClass = getTokenClass(token);
                            //  A prefix (unary) operator has no left operand, so no operator on the stack can be applied before it
                            stack.Push(token);
                            break;
                        }
                        var lao = getOperatorPrecedence(token);
                        for (; ; )
                        {
                            if (stack.Count <= 0)
                                break;
                            T t = stack.Peek();
                            var t2class = getTokenClass(t);
                            if ((t2class != TokenClasses.LeftAssociativeOperator) && (t2class != TokenClasses.RightAssociativeOperator))
                                break;
                            if (lao > getOperatorPrecedence(t))
                                break;
                            stack.Pop();
                            yield return t;
                        }
                        stack.Push(token);
                        break;
                    case TokenClasses.RightAssociativeOperator:
                        if (Unary.Contains(prev))
                        {
                            try
                            {
                                token = toUnary(token);
                            }
                            catch (Exception ex)
                            {
                                exceptionCaster("Failed to make unary token from \"" + token + "\"", ex, token);
                            }
                            tokenClass = getTokenClass(token);
                            //  A prefix (unary) operator has no left operand, so no operator on the stack can be applied before it
                            stack.Push(token);
                            break;
                        }
                        var rao = getOperatorPrecedence(token);
                        for (; ; )
                        {
                            if (stack.Count <= 0)
                                break;
                            T t = stack.Peek();
                            var t2class = getTokenClass(t);
                            if ((t2class != TokenClasses.LeftAssociativeOperator) && (t2class != TokenClasses.RightAssociativeOperator))
                                break;
                            if (rao >= getOperatorPrecedence(t))
                                break;
                            stack.Pop();
                            yield return t;
                        }
                        stack.Push(token);
                        break;
                    case TokenClasses.OpenParentheses:
                        stack.Push(token);
                        argCounts?.Push(prev == TokenClasses.Function ? 1 : -1);
                        break;
                    case TokenClasses.CloseParentheses:
                        for (; ; )
                        {
                            if (stack.Count <= 0)
                                exceptionCaster("Mismatched closing parentheses - no matching opening parentheses found", null, token);
                            T t = stack.Pop();
                            if (getTokenClass(t) == TokenClasses.OpenParentheses)
                                break;
                            yield return t;
                        }
                        var argCount = -1;
                        if (argCounts != null)
                        {
                            argCount = argCounts.Pop();
                            //  "f()" has no arguments
                            if ((argCount > 0) && (prev == TokenClasses.OpenParentheses))
                                argCount = 0;
                        }
                        if (stack.Count > 0)
                        {
                            T t = stack.Peek();
                            if (getTokenClass(t) == TokenClasses.Function)
                            {
                                stack.Pop();
                                if (argCount >= 0)
                                    t = withArgumentCount(t, argCount);
                                yield return t;
                            }
                        }
                        break;
                }
                ++tokenIndex;
                prev = tokenClass;
            }
            while (stack.Count > 0)
            {
                T token = stack.Pop();
                switch (getTokenClass(token))
                {
                    case TokenClasses.OpenParentheses:
                        exceptionCaster("Mismatched opening parentheses - no matching closing parentheses found", null, token);
                        break;
                    case TokenClasses.CloseParentheses:
                        exceptionCaster("Mismatched closing parentheses - no matching opening parentheses found", null, token);
                        break;
                }
                yield return token;
            }
        }
    }

}
