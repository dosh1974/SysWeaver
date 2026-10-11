using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser
{

    /// <summary>
    /// Character classification of a tokenizer (used by value readers to find the end of a value)
    /// </summary>
    public interface IStringTokenizer
    {
        /// <summary>
        /// Check if a character is the first character of a registered operator
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>True if an operator starts with this character</returns>
        bool IsOperatorChar(Char c);
        /// <summary>
        /// Check if a character is a whitespace (one of the extra whitespaces, or a <see cref="Char.IsWhiteSpace(char)"/> character if the default whitespaces are filtered)
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>True if the character is a whitespace</returns>
        bool IsWhitespaceChar(Char c);
    }

    /// <summary>
    /// The context passed to a value reader
    /// </summary>
    public interface IStringTokenizerContext
    {
        /// <summary>
        /// The tokenizer
        /// </summary>
        IStringTokenizer Tokenizer { get; }
        /// <summary>
        /// The expression being parsed
        /// </summary>
        String Expression { get; }
        /// <summary>
        /// The index of the token being read (0 for the first token of the expression)
        /// </summary>
        int TokenIndex { get; }
    }

    /// <summary>
    /// Splits an expression into tokens: registered operators, registered identifiers, parameters and values (read by a value reader).
    /// Operators and identifiers are added once (setup), parsing is thread safe if the tokenizer isn't modified at the same time
    /// </summary>
    /// <typeparam name="T">The token type</typeparam>
    public sealed class StringTokenizer<T> : IStringTokenizer
    {

        /// <summary>
        /// Create a token
        /// </summary>
        /// <param name="start">The start position of the token in the expression (inclusive)</param>
        /// <param name="end">The end position of the token in the expression (exclusive)</param>
        /// <returns>The token</returns>
        public delegate T TokenCreator(int start, int end);
        /// <summary>
        /// Check if there is a token at a position
        /// </summary>
        /// <param name="tokenizer">The tokenizer</param>
        /// <param name="value">Receives the token if found</param>
        /// <param name="index">The position to check, advanced past the token if found</param>
        /// <param name="t">The expression</param>
        /// <returns>True if a token was found</returns>
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

        /// <summary>
        /// Reads a value (anything that isn't an operator, identifier or parameter) from an expression
        /// </summary>
        /// <param name="context">The parsing context</param>
        /// <param name="index">The start position of the value, must be advanced past the value</param>
        /// <returns>The value token</returns>
        public delegate T ValueReader(IStringTokenizerContext context, ref int index);

        /// <summary>
        /// Create a tokenizer.
        /// Operators, identifiers and parameters are always matched case sensitively (ordinal comparison)
        /// </summary>
        public StringTokenizer()
        {
        }

        #region Options

        /// <summary>
        /// If true (default) all <see cref="Char.IsWhiteSpace(char)"/> characters are skipped
        /// </summary>
        public bool FilterDefaultWhitespaces = true;
        /// <summary>
        /// Extra characters that are skipped as whitespaces
        /// </summary>
        public readonly HashSet<Char> Whitespaces = new HashSet<char>();

        #endregion//Options

        #region Token parsers

        private sealed class IdentifierToken
        {
            public IdentifierToken(String value, TokenCreator token)
            {
                Value = value;
                Token = token;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    if (Value[i] != t[index + i])
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
        }

        private sealed class OperatorToken
        {
            public OperatorToken(String value, TokenCreator token)
            {
                Value = value;
                Token = token;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    if (Value[i] != t[index + i])
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
        }

        private sealed class ParameterToken
        {
            public ParameterToken(String value, TokenCreator token)
            {
                Value = value;
                Token = token;
            }
            public bool IsToken(StringTokenizer<T> tokenizer, ref T value, ref int index, String t)
            {
                if ((t.Length - index) < Value.Length)
                    return false;
                for (int i = 1; i < Value.Length; ++i)
                {
                    if (Value[i] != t[index + i])
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
        }

        #endregion//Token parsers

        /// <summary>
        /// All registered operators
        /// </summary>
        public IEnumerable<String> Operators
        {
            get
            {

                foreach (var a in AllOperators)
                    yield return a;
            }
        }
        /// <summary>
        /// All registered identifiers
        /// </summary>
        public IEnumerable<String> Identifiers
        {
            get
            {
                foreach (var a in AllIdentifiers)
                    yield return a;
            }
        }

        #region Setup

        readonly HashSet<String> AllOperators = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<String> AllIdentifiers = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Register an operator.
        /// An operator only matches if it's followed by the end of the expression, a character that can't be part of an operator, or another operator
        /// </summary>
        /// <param name="value">The operator text, can only contain characters that aren't letters, digits, '_' or whitespaces</param>
        /// <param name="token">Creates the token for the operator</param>
        /// <exception cref="ArgumentException">The operator is null, empty, contains an invalid character or is already added</exception>
        public void AddOperator(String value, TokenCreator token)
        {
            StringTokenizerHelper.ValidateOperator(value);
            if (!AllOperators.Add(value))
                throw new ArgumentException("Operator \"" + value + "\" already added!", "value");
            IsToken t = new OperatorToken(value, token).IsToken;
            var c = value[0];
            List<IsToken> l;
            if (!OperatorMap.TryGetValue(c, out l))
            {
                l = new List<IsToken>();
                OperatorMap.Add(c, l);
            }
            l.Add(t);
        }

        /// <summary>
        /// Register an identifier (ex: a function or a constant).
        /// An identifier only matches if it isn't followed by a letter, digit or '_'
        /// </summary>
        /// <param name="value">The identifier, must start with a letter, '@' or '_' followed by letters, digits or '_'</param>
        /// <param name="token">Creates the token for the identifier</param>
        /// <exception cref="ArgumentException">The identifier is null, empty, contains an invalid character or is already added</exception>
        public void AddIdentifier(String value, TokenCreator token)
        {
            StringTokenizerHelper.ValidateIdentifier(value);
            if (!AllIdentifiers.Add(value))
                throw new ArgumentException("Identifier \"" + value + "\" already added!", "value");
            IsToken t = new IdentifierToken(value, token).IsToken;
            var c = value[0];
            List<IsToken> l;
            if (!IdentifierMap.TryGetValue(c, out l))
            {
                l = new List<IsToken>();
                IdentifierMap.Add(c, l);
            }
            l.Add(t);
        }

        private readonly Dictionary<Char, List<IsToken>> OperatorMap = new Dictionary<char, List<IsToken>>();
        private readonly Dictionary<Char, List<IsToken>> IdentifierMap = new Dictionary<char, List<IsToken>>();

        /// <inheritdoc/>
        public bool IsOperatorChar(Char c)
        {
            List<IsToken> l;
            return OperatorMap.TryGetValue(c, out l) && (l.Count > 0);
        }

        /// <inheritdoc/>
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

        /// <summary>
        /// Build the parameter lookup used by <see cref="Parse(string, ValueReader, Dictionary{char, List{IsToken}}, TokenCreator)"/>
        /// </summary>
        /// <param name="parameters">The parameter names (null for none)</param>
        /// <param name="paramaterTokenCreator">Gets the token creator for a parameter name and its index (null for no parameters)</param>
        /// <returns>The parameter lookup (empty if any argument is null)</returns>
        /// <exception cref="ArgumentException">A parameter name is invalid, used twice or is a registered identifier (case sensitive, ordinal comparison)</exception>
        public Dictionary<Char, List<IsToken>> GetParameters(IEnumerable<String> parameters, Func<String, int, TokenCreator> paramaterTokenCreator)
        {
            var p = new Dictionary<Char, List<IsToken>>();
            if ((parameters == null) || (paramaterTokenCreator == null))
                return p;
            var all = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (var value in parameters)
            {
                StringTokenizerHelper.ValidateIdentifier(value, "parameters");
                if (AllIdentifiers.Contains(value))
                    throw new ArgumentException("Paramater \"" + value + "\" is a reserved operator name", "parameters");
                if (!all.Add(value))
                    throw new ArgumentException("Paramater \"" + value + "\" found more than once!", "parameters");
                var token = paramaterTokenCreator(value, index);
                IsToken t = new IdentifierToken(value, token).IsToken;
                var c = value[0];
                List<IsToken> l;
                if (!p.TryGetValue(c, out l))
                {
                    l = new List<IsToken>();
                    p.Add(c, l);
                }
                l.Add(t);
                ++index;
            }
            return p;
        }

        #endregion//Parameters

        #region Parsing

        /// <summary>
        /// Split an expression into tokens (lazily, errors are thrown when enumerating)
        /// </summary>
        /// <param name="expression">The expression</param>
        /// <param name="valueReader">Reads values (anything that isn't whitespace, an operator, identifier or parameter, and doesn't start like an identifier or with an unknown operator character)</param>
        /// <param name="parameters">The parameter names (null for none)</param>
        /// <param name="paramaterTokenCreator">Gets the token creator for a parameter name and its index</param>
        /// <param name="unknownIdentifierCreator">Creates tokens for unknown identifiers, if null an <see cref="ExpressionParserException"/> is thrown for unknown identifiers</param>
        /// <returns>The tokens</returns>
        /// <exception cref="ArgumentException">A parameter name is invalid, used twice or is a registered identifier</exception>
        public IEnumerable<T> Parse(String expression, ValueReader valueReader, IEnumerable<String> parameters = null, Func<String, int, TokenCreator> paramaterTokenCreator = null, TokenCreator unknownIdentifierCreator = null)
        {
            return Parse(expression, valueReader, GetParameters(parameters, paramaterTokenCreator), unknownIdentifierCreator);
        }
        /// <summary>
        /// Split an expression into tokens (lazily, errors are thrown when enumerating).
        /// At each position (after skipping whitespaces) operators are tried first, then identifiers, then parameters, then the value reader
        /// </summary>
        /// <param name="expression">The expression</param>
        /// <param name="valueReader">Reads values (anything that isn't whitespace, an operator, identifier or parameter, and doesn't start like an identifier or with an unknown operator character)</param>
        /// <param name="parameterList">The parameter lookup, see <see cref="GetParameters(IEnumerable{string}, Func{string, int, TokenCreator})"/></param>
        /// <param name="unknownIdentifierCreator">Creates tokens for unknown identifiers, if null an <see cref="ExpressionParserException"/> is thrown for unknown identifiers</param>
        /// <returns>The tokens</returns>
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
