using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser
{

    public interface IStringTokenizer
    {
        bool IsOperatorChar(Char c);
        bool IsWhitespaceChar(Char c);
    }

    public interface IStringTokenizerContext
    {
        IStringTokenizer Tokenizer { get; }
        String Expression { get; }
        int TokenIndex { get; }
    }

    public sealed class StringTokenizer<T> : IStringTokenizer
    {

        public delegate T TokenCreator(int start, int end);
        public delegate bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t);


        private sealed class Context : IStringTokenizerContext
        {
            public Context(IStringTokenizer tokenizer, String expression)
            {
                Tokenizer = tokenizer;
                Expression = expression;
            }
            public IStringTokenizer Tokenizer { get; private set; }
            public string Expression { get; private set; }
            public int TokenIndex { get; internal set; }
        }

        public delegate T ValueReader(IStringTokenizerContext context, ref int index);

        public StringTokenizer(bool caseSensitive = false)
        {
            CaseSensitive = caseSensitive;
            StringComparer = caseSensitive ? ExpressionEvaluator.DefaultComparerCase : ExpressionEvaluator.DefaultComparer;
            StringComparison = caseSensitive ? ExpressionEvaluator.DefaultStringComparisonCase : ExpressionEvaluator.DefaultStringComparison;
            AllOperators = new HashSet<string>(StringComparer);
            AllIdentifiers = new HashSet<string>(StringComparer);
        }
        public readonly StringComparer StringComparer;
        public readonly StringComparison StringComparison;
        public readonly bool CaseSensitive;

        #region Options

        public bool FilterDefaultWhitespaces = true;
        public readonly HashSet<Char> Whitespaces = new HashSet<char>();

        #endregion//Options

        #region Token parsers

        private sealed class IdentifierToken
        {
            public IdentifierToken(String value, TokenCreator token, bool caseSensitive)
            {
                Value = caseSensitive ? value : value.FastToLower();
                Token = token;
                Case = caseSensitive;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    char c = t[index + i];
                    if (!Case)
                        c = CharExt.FastLower(c);
                    if (Value[i] != c)
                        return false;
                }
                var newI = index + Value.Length;
                if ((newI >= t.Length) || (!StringTokenizerHelper.IsValidIdentiferCharRest(t[newI])))
                {
                    value = Token(index, newI);
                    index = newI;
                    return true;
                }
                return false;
            }
            private readonly String Value;
            private readonly TokenCreator Token;
            private readonly bool Case;
        }

        private sealed class OperatorToken
        {
            public OperatorToken(String value, TokenCreator token, bool caseSensitive)
            {
                Value = caseSensitive ? value : value.FastToLower();
                Token = token;
                Case = caseSensitive;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    char c = t[index + i];
                    if (!Case)
                        c = CharExt.FastLower(c);
                    if (Value[i] != c)
                        return false;
                }
                var newI = index + Value.Length;
                if ((newI >= t.Length) || (!StringTokenizerHelper.IsValidOperatorChar(t[newI])) || tokenizer.IsOperator(t, newI))
                {
                    value = Token(index, newI);
                    index = newI;
                    return true;
                }
                return false;
            }
            private readonly String Value;
            private readonly TokenCreator Token;
            private readonly bool Case;
        }

        private sealed class ParameterToken
        {
            public ParameterToken(String value, TokenCreator token, bool caseSensitive)
            {
                Value = caseSensitive ? value : value.FastToLower();
                Token = token;
                Case = caseSensitive;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    char c = t[index + i];
                    if (!Case)
                        c = CharExt.FastLower(c);
                    if (Value[i] != c)
                        return false;
                }
                var newI = index + Value.Length;
                if ((newI >= t.Length) || (!StringTokenizerHelper.IsValidIdentiferCharRest(t[newI])))
                {
                    value = Token(index, newI);
                    index = newI;
                    return true;
                }
                return false;
            }
            private readonly String Value;
            private readonly TokenCreator Token;
            private readonly bool Case;
        }

        #endregion//Token parsers

        public IEnumerable<String> Operators
        {
            get
            {

                foreach (var a in AllOperators)
                    yield return a;
            }
        }
        public IEnumerable<String> Identifiers
        {
            get
            {
                foreach (var a in AllIdentifiers)
                    yield return a;
            }
        }

        #region Setup

        readonly HashSet<String> AllOperators;
        readonly HashSet<String> AllIdentifiers;

        private bool Other(ref Char c)
        {
            if (CaseSensitive)
                return false;
            var o = c;
            c = CharExt.FastUpper(o);
            if (c != o)
                return true;
            c = CharExt.FastLower(o);
            return c != o;
        }

        public void AddOperator(String value, TokenCreator token)
        {
            StringTokenizerHelper.ValidateOperator(value);
            if (!AllOperators.Add(value))
                throw new ArgumentException("Operator \"" + value + "\" already added!", "value");
            IsToken t = new OperatorToken(value, token, CaseSensitive).IsToken;
            var c = value[0];
            List<IsToken> l;
            if (!OperatorMap.TryGetValue(c, out l))
            {
                l = new List<IsToken>();
                OperatorMap.Add(c, l);
            }
            l.Add(t);
            if (Other(ref c))
            {
                if (!OperatorMap.TryGetValue(c, out l))
                {
                    l = new List<IsToken>();
                    OperatorMap.Add(c, l);
                }
                l.Add(t);
            }
        }

        public void AddIdentifier(String value, TokenCreator token)
        {
            StringTokenizerHelper.ValidateIdentifier(value);
            if (!AllIdentifiers.Add(value))
                throw new ArgumentException("Identifier \"" + value + "\" already added!", "value");
            IsToken t = new IdentifierToken(value, token, CaseSensitive).IsToken;
            var c = value[0];
            List<IsToken> l;
            if (!IdentifierMap.TryGetValue(c, out l))
            {
                l = new List<IsToken>();
                IdentifierMap.Add(c, l);
            }
            l.Add(t);
            if (Other(ref c))
            {
                if (!IdentifierMap.TryGetValue(c, out l))
                {
                    l = new List<IsToken>();
                    IdentifierMap.Add(c, l);
                }
                l.Add(t);
            }
        }

        private readonly Dictionary<Char, List<IsToken>> OperatorMap = new Dictionary<char, List<IsToken>>();
        private readonly Dictionary<Char, List<IsToken>> IdentifierMap = new Dictionary<char, List<IsToken>>();

        public bool IsOperatorChar(Char c)
        {
            List<IsToken> l;
            return OperatorMap.TryGetValue(c, out l) && (l.Count > 0);
        }

        public bool IsWhitespaceChar(Char c)
        {
            return Whitespaces.Contains(c) || (FilterDefaultWhitespaces && Char.IsWhiteSpace(c));
        }

        private static void ThrowIdentifier(String t, int pos)
        {
            int start = pos;
            int l = t.Length;
            ++pos;
            for (; pos < l; ++pos)
            {
                if (!StringTokenizerHelper.IsValidIdentiferCharRest(t[pos]))
                    break;
            }
            throw new ExpressionParserException("Unknown identifier \"" + t.Substring(start, pos - start) + "\" found at position " + start, t, start, pos);
        }
        private static void ThrowOperator(String t, int pos)
        {
            int start = pos;
            int l = t.Length;
            ++pos;
            for (; pos < l; ++pos)
            {
                if (!StringTokenizerHelper.IsValidIdentiferCharRest(t[pos]))
                    break;
            }
            throw new ExpressionParserException("Unknown operator \"" + t.Substring(start, pos - start) + "\" found at position " + start, t, start, pos);
        }

        private bool IsOperator(String t, int index)
        {
            List<IsToken> tokens;
            T v = default(T);
            if (OperatorMap.TryGetValue(t[index], out tokens))
            {
                foreach (var o in tokens)
                {
                    if (o(this, ref v, ref index, t))
                        return true;
                }
            }
            return false;
        }

        #endregion//Setup

        #region Parameters

        public Dictionary<Char, List<IsToken>> GetParameters(IEnumerable<String> parameters, Func<String, int, TokenCreator> paramaterTokenCreator)
        {
            var p = new Dictionary<Char, List<IsToken>>();
            if ((parameters == null) || (paramaterTokenCreator == null))
                return p;
            var all = new HashSet<string>(StringComparer);
            int index = 0;
            foreach (var value in parameters)
            {
                StringTokenizerHelper.ValidateIdentifier(value, "parameters");
                if (AllIdentifiers.Contains(value))
                    throw new ArgumentException("Paramater \"" + value + "\" is a reserved operator name", "parameters");
                if (!all.Add(value))
                    throw new ArgumentException("Paramater \"" + value + "\" found more than once!", "parameters");
                var token = paramaterTokenCreator(value, index);
                IsToken t = new IdentifierToken(value, token, CaseSensitive).IsToken;
                var c = value[0];
                List<IsToken> l;
                if (!p.TryGetValue(c, out l))
                {
                    l = new List<IsToken>();
                    p.Add(c, l);
                }
                l.Add(t);
                if (Other(ref c))
                {
                    if (!p.TryGetValue(c, out l))
                    {
                        l = new List<IsToken>();
                        p.Add(c, l);
                    }
                    l.Add(t);
                }
                ++index;
            }
            return p;
        }

        #endregion//Parameters

        #region Parsing

        public IEnumerable<T> Parse(String expression, ValueReader valueReader, IEnumerable<String> parameters = null, Func<String, int, TokenCreator> paramaterTokenCreator = null, TokenCreator unknownIdentifierCreator = null)
        {
            return Parse(expression, valueReader, GetParameters(parameters, paramaterTokenCreator), unknownIdentifierCreator);
        }
        public IEnumerable<T> Parse(String expression, ValueReader valueReader, Dictionary<Char, List<IsToken>> parameterList, TokenCreator unknownIdentifierCreator = null)
        {
            T v = default;
            var context = new Context(this, expression);
            int tokenIndex = 0;
            for (int i = 0; i < expression.Length; )
            {
                Char c = expression[i];
                if (Whitespaces.Contains(c) || (FilterDefaultWhitespaces && Char.IsWhiteSpace(c)))
                {
                    ++i;
                    continue;
                }
                List<IsToken> tokens;
                if (OperatorMap.TryGetValue(c, out tokens))
                {
                    bool did = false;
                    foreach (var o in tokens)
                    {
                        if (o(this, ref v, ref i, expression))
                        {
                            yield return v;
                            ++tokenIndex;
                            did = true;
                            break;
                        }
                    }
                    if (did)
                        continue;
                }
                if (IdentifierMap.TryGetValue(c, out tokens))
                {
                    bool did = false;
                    foreach (var o in tokens)
                    {
                        if (o(this, ref v, ref i, expression))
                        {
                            yield return v;
                            ++tokenIndex;
                            did = true;
                            break;
                        }
                    }
                    if (did)
                        continue;
                }
                if (parameterList.TryGetValue(c, out tokens))
                {
                    bool did = false;
                    foreach (var o in tokens)
                    {
                        if (o(this, ref v, ref i, expression))
                        {
                            yield return v;
                            ++tokenIndex;
                            did = true;
                            break;
                        }
                    }
                    if (did)
                        continue;
                }
                if (StringTokenizerHelper.IsValidIdentiferCharFirst(c))
                {
                    if (unknownIdentifierCreator == null)
                        ThrowIdentifier(expression, i);
                    var start = i;
                    ++i;
                    while (i < expression.Length)
                    {
                        if (!StringTokenizerHelper.IsValidIdentiferCharRest(expression[i]))
                            break;
                        ++i;
                    }
                    yield return unknownIdentifierCreator(start, i);
                    continue;
                }
                if (StringTokenizerHelper.IsValidOperatorChar(c))
                    ThrowOperator(expression, i);
                context.TokenIndex = tokenIndex;
                v = valueReader(context, ref i);
                yield return v;
                ++tokenIndex;
            }
        }

        #endregion//Parsing

    }

}
