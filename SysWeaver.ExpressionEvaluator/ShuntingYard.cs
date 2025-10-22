using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    public enum TokenClasses
    {
        LeftAssociativeOperator,
        RightAssociativeOperator,
        Operand,
        Function,
        OpenParentheses,
        CloseParentheses,
        ArgumentSeparator
    }
    public static class ShuntingYard
    {
        static readonly IReadOnlySet<TokenClasses> Unary = ReadOnlyData.Set(
            TokenClasses.LeftAssociativeOperator,
            TokenClasses.RightAssociativeOperator,
            TokenClasses.OpenParentheses
        );

        public static IEnumerable<T> ReversePolishNotation<T>(IEnumerable<T> tokens, Func<T, TokenClasses> getTokenClass, Func<T, int> getOperatorPrecedence, Func<T, T> toUnary, Action<String, Exception, T> exceptionCasterN = null)
        {
            var exceptionCaster = exceptionCasterN ?? new Action<String, Exception, T>((message, innerExp, token) => { throw new ArgumentException(message + "\n" + token, "tokens", innerExp); });
            Stack<T> stack = new Stack<T>();
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
                        }
                        var lao = getOperatorPrecedence(token);
                        for (; ; )
                        {
                            if (stack.Count <= 0)
                                break;
                            T t = stack.Peek();
                            var t2class = getTokenClass(token);
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
                        }
                        var rao = getOperatorPrecedence(token);
                        for (; ; )
                        {
                            if (stack.Count <= 0)
                                break;
                            T t = stack.Peek();
                            var t2class = getTokenClass(token);
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
                        if (stack.Count > 0)
                        {
                            T t = stack.Peek();
                            if (getTokenClass(t) == TokenClasses.Function)
                            {
                                stack.Pop();
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
