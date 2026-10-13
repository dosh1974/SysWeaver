using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using SysWeaver.Parser.ValueReader;

namespace SysWeaver.MicroService
{

}

namespace SysWeaver.Parser
{




    /// <summary>
    /// Non generic access to an <see cref="ExpressionEvaluator{T}"/> (values are passed and returned as objects)
    /// </summary>
    public interface IExpressionEvaluator
    {
        /// <summary>
        /// Evaluate an expression without parameters
        /// </summary>
        /// <param name="expression">The expression, ex: "1 + 2 * 3"</param>
        /// <returns>The (boxed) value of the expression, of the evaluator type</returns>
        Object ToValue(String expression);
        /// <summary>
        /// Evaluate an expression with parameters
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value</param>
        /// <param name="values">The parameter values, must be (boxed) values of exactly the evaluator type (they are unboxed, not converted)</param>
        /// <returns>The (boxed) value of the expression, of the evaluator type</returns>
        Object ToValue(String expression, IEnumerable<String> parameters, params Object[] values);
        /// <summary>
        /// All operators (including the parentheses and the argument separator) known by this evaluator
        /// </summary>
        IEnumerable<String> Operators { get; }
        /// <summary>
        /// All identifiers (functions and constants) known by this evaluator
        /// </summary>
        IEnumerable<String> Identifiers { get; }
        /// <summary>
        /// Tokenize an expression (no evaluation is done), useful for validation or syntax highlighting
        /// </summary>
        /// <param name="expression">The expression to tokenize</param>
        /// <param name="parameters">The parameter names (null for none)</param>
        /// <param name="allowUnknownIdentifiers">If true unknown identifiers are returned as operands, else an <see cref="ExpressionParserException"/> is thrown</param>
        /// <returns>The tokens (lazily): the token class, the start (inclusive) and end (exclusive) position and the text of the token</returns>
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, IEnumerable<String> parameters, bool allowUnknownIdentifiers = false);
        /// <summary>
        /// Tokenize an expression without parameters (no evaluation is done)
        /// </summary>
        /// <param name="expression">The expression to tokenize</param>
        /// <param name="allowUnknownIdentifiers">If true unknown identifiers are returned as operands, else an <see cref="ExpressionParserException"/> is thrown</param>
        /// <returns>The tokens (lazily): the token class, the start (inclusive) and end (exclusive) position and the text of the token</returns>
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers);
        /// <summary>
        /// Tokenize an expression (no evaluation is done)
        /// </summary>
        /// <param name="expression">The expression to tokenize</param>
        /// <param name="allowUnknownIdentifiers">If true unknown identifiers are returned as operands, else an <see cref="ExpressionParserException"/> is thrown</param>
        /// <param name="parameters">The parameter names</param>
        /// <returns>The tokens (lazily): the token class, the start (inclusive) and end (exclusive) position and the text of the token</returns>
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers, params String[] parameters);

        /// <summary>
        /// Compile an expression to a function taking the parameter values as objects.
        /// The values are converted to the evaluator type using <see cref="Convert.ChangeType(object, Type)"/> (strings are converted using the current culture)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value (null for none)</param>
        /// <returns>A function that evaluates the expression, the result is a boxed value of the evaluator type</returns>
        Func<Object[], Object> ObjectEvaluator(String expression, IEnumerable<String> parameters = null);

        /// <summary>
        /// Compile an expression to a function taking the parameter values as objects.
        /// The values are converted to the evaluator type using <see cref="Convert.ChangeType(object, Type)"/> (strings are converted using the current culture)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value</param>
        /// <returns>A function that evaluates the expression, the result is a boxed value of the evaluator type</returns>
        Func<Object[], Object> ObjectEvaluator(String expression, params String[] parameters);


    }

    /// <summary>
    /// The standard expression evaluators and helpers.
    /// The standard evaluators support the operators "+ - * / %" (and unary "+ -"), parentheses, "," as the function argument separator,
    /// and all public static functions, constant fields and read only properties of <see cref="Math"/> that only use the evaluator type,
    /// with the name of the member (ex: "Sin", "Max", "PI"). Overloads are selected by the number of arguments (ex: "Log(8)" and "Log(8, 2)").
    /// Function, constant and parameter names are case sensitive (ordinal comparison): "pi" is not "PI", so it can be used as a parameter name.
    /// The <see cref="Double"/> and <see cref="Decimal"/> evaluators add "^" (power, right associative), "&lt;&lt;" and "&gt;&gt;" (multiply / divide by a power of 2).
    /// The <see cref="Int64"/> and <see cref="UInt64"/> evaluators add the bit operators "&lt;&lt; &gt;&gt; &amp; ^ |" ("^" is XOR) and the unary "~" and "!" (both bitwise not).
    /// Precedence (high to low): unary operators, "^" (power), "* / %", "+ -", "&lt;&lt; &gt;&gt;", "&amp;", "^" (XOR), "|" (the integer operators use the same order as C#).
    /// Numbers are parsed with the invariant culture (the current culture is never used), "0x" (hexadecimal), "0b" (binary) and "0o" (octal) prefixes are supported (the prefixes are case insensitive).
    /// </summary>
    public static class ExpressionEvaluator
    {
        private static readonly IReadOnlyDictionary<Type, bool> Extensions = new Dictionary<Type, bool>()
        {
            { typeof(Math), false },
/*            { typeof(IntMath), false },
            { typeof(Bit), true },
            { typeof(Gcd), true },
            { typeof(Interpolation), false },
            { typeof(SimplexNoise), true },*/
        }.Freeze();

        //  Use one of the standard evaluators below or instanciate a custom
        //  Then use any of the methods: Exp, Evaluator, Value to get the expression, function or value given an input expression (with optional parameters).

        /// <summary>
        /// Evaluator using Double arithmetics (IEEE semantics: 1/0 is infinity, 0/0 is NaN).
        /// Constants: "E", "PI" and "Tau"
        /// </summary>
        public static readonly ExpressionEvaluator<Double> Double = new ExpressionEvaluator<Double>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsDecimal, ExpressionEvaluatorValueReader.Double, Extensions);
        /// <summary>
        /// Evaluator using Int64 arithmetics (unchecked: overflows wrap around, division by zero throws a <see cref="DivideByZeroException"/> when evaluated)
        /// </summary>
        public static readonly ExpressionEvaluator<Int64> Int64 = new ExpressionEvaluator<Int64>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsInteger, ExpressionEvaluatorValueReader.Int64, Extensions);
        /// <summary>
        /// Evaluator using UInt64 arithmetics (unchecked: overflows wrap around, division by zero throws a <see cref="DivideByZeroException"/> when evaluated).
        /// The unary minus is not supported (use "0-x")
        /// </summary>
        public static readonly ExpressionEvaluator<UInt64> UInt64 = new ExpressionEvaluator<UInt64>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsInteger, ExpressionEvaluatorValueReader.UInt64, Extensions);
        /// <summary>
        /// Evaluator using Decimal arithmetics (overflows and division by zero throw when evaluated).
        /// "x^y" is computed exactly (by repeated multiplication) when y is an integer, else using <see cref="Math.Pow(double, double)"/> on the values converted to Double.
        /// "x&lt;&lt;y" is x * 2^y and "x&gt;&gt;y" is x / 2^y (computed the same way)
        /// </summary>
        public static readonly ExpressionEvaluator<Decimal> Decimal = new ExpressionEvaluator<Decimal>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsDecimal, ExpressionEvaluatorValueReader.Decimal, Extensions);

        private static readonly IReadOnlyDictionary<Type, IExpressionEvaluator> Evaluators = new Dictionary<Type, IExpressionEvaluator>()
        {
            { typeof(Single), Double },
            { typeof(Double), Double },
            { typeof(Decimal), Decimal },
            { typeof(SByte), Int64 },
            { typeof(Int16), Int64 },
            { typeof(Int32), Int64 },
            { typeof(Int64), Int64 },
            { typeof(Byte), UInt64 },
            { typeof(UInt16), UInt64 },
            { typeof(UInt32), UInt64 },
            { typeof(UInt64), UInt64 },
        }.Freeze();

        /// <summary>
        /// Get the standard evaluator to use for a numeric type:
        /// <see cref="Double"/> for Single and Double, <see cref="Decimal"/> for Decimal,
        /// <see cref="Int64"/> for the signed integer types and <see cref="UInt64"/> for the unsigned integer types
        /// (the evaluator works on the 64-bit type, the caller must convert the result).
        /// </summary>
        /// <param name="type">The numeric type</param>
        /// <returns>The evaluator to use</returns>
        /// <exception cref="KeyNotFoundException">There is no evaluator for the type (ex: String, Boolean, Char, Int128)</exception>
        public static IExpressionEvaluator Get(Type type)
        {
            return Evaluators[type];
        }


    }


    /// <summary>
    /// Options for an <see cref="ExpressionEvaluator{T}"/> (operators, identifiers and parameter names are always case sensitive)
    /// </summary>
    [Flags]
    public enum ExpressionTypeFlags
    {
        /// <summary>
        /// Add the numeric operators "+ - * / %" (and unary "+" and "-")
        /// </summary>
        IsNumeric = 2,
        /// <summary>
        /// Add the operators "^" (power), "&lt;&lt;" and "&gt;&gt;" (multiply / divide by a power of 2), can't be combined with <see cref="IsInteger"/>
        /// </summary>
        IsDecimal = 4,
        /// <summary>
        /// Add the bit operators "&lt;&lt; &gt;&gt; | &amp; ^" (XOR) and unary "~ !" (bitwise not), can't be combined with <see cref="IsDecimal"/>
        /// </summary>
        IsInteger = 8,
    }


    /// <summary>
    /// Parses text expressions and compiles them to linq expressions / delegates operating on the type T.
    /// Parameters are named in the expression and passed as an array of T (the index of the name is the index of the value).
    /// Parse errors throw an <see cref="ArgumentException"/>, usually an <see cref="ExpressionParserException"/> with the position of the error.
    /// Instances are thread safe (they are not modified after construction).
    /// </summary>
    /// <typeparam name="T">The operand type</typeparam>
    public sealed class ExpressionEvaluator<T> : IExpressionEvaluator
    {
        private static readonly ParameterExpression InputParameters = Expression.Parameter(typeof(T[]), "parameters");
/*
        public IEnumerable<Token> Tokens(String expression, IEnumerable<String> parameters = null, bool allowUnknownIdentifiers = false)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers))
                yield return token;
        }

        public IEnumerable<Token> Tokens(String expression, params String[] parameters)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }))
                yield return token;
        }
*/
        /// <summary>
        /// Parse an expression to a linq expression of type T, the parameters are read from an array of T
        /// </summary>
        /// <param name="inputParameters">Receives the parameter (of type T[]) that the expression reads the parameter values from (the same instance for all calls)</param>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names (null for none), the index of a name is the index of its value in the array</param>
        /// <param name="optimize">True to run the <see cref="ExpressionOptimizer"/> on the result (constant folding etc; floating point "x + 0" is folded to "x", so x = -0 gives -0 instead of +0, see <see cref="ExpressionOptimizer.Optimize(Expression)"/>)</param>
        /// <returns>The linq expression</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        /// <exception cref="ArgumentNullException">The expression is null (DEBUG builds only)</exception>
        public Expression Exp(out ParameterExpression inputParameters, String expression, IEnumerable<String> parameters = null, bool optimize = false)
        {
            return Exp(out inputParameters, expression, optimize, parameters.Nullable().ToArray());
        }

        /// <summary>
        /// Parse an expression to a linq expression of type T, the parameters are read from an array of T
        /// </summary>
        /// <param name="inputParameters">Receives the parameter (of type T[]) that the expression reads the parameter values from (the same instance for all calls)</param>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="optimize">True to run the <see cref="ExpressionOptimizer"/> on the result (constant folding etc; floating point "x + 0" is folded to "x", so x = -0 gives -0 instead of +0, see <see cref="ExpressionOptimizer.Optimize(Expression)"/>)</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value in the array</param>
        /// <returns>The linq expression</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        /// <exception cref="ArgumentNullException">The expression is null (DEBUG builds only)</exception>
        public Expression Exp(out ParameterExpression inputParameters, String expression, bool optimize, params String[] parameters)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(expression);
#endif//DEBUG
            inputParameters = InputParameters;
            var exception = new ExceptionThrower(expression);
            try
            {
                var tokens = Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, stack => Expression.ArrayAccess(InputParameters, Expression.Constant(index))); });
                var rpn = ShuntingYard.ReversePolishNotation(tokens, token => token.Class, token => token.Precedence, token => token.Unary(token.Start, token.End), exception.WithArgumentCount, exception.OnEvaluatorException);
                var exp = PostfixEvaluator.FromRpn<Expression, Token>(rpn, (token, stack) => token.Eval(stack), token => token.Class != TokenClasses.Operand, exception.OnEvaluatorException);
                if (optimize)
                    exp = exp.Optimize();
                return exp;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                exception.OnEvaluatorException("Unknown error", ex, null);
                throw;
            }
        }

        /// <summary>
        /// Parse an expression to a linq expression of type T, using the supplied linq expressions as the parameter values
        /// (useful for embedding the expression into a larger linq expression)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names</param>
        /// <param name="parameterValues">The linq expressions (of type T) to use for the parameters, the index of a name is the index of its value</param>
        /// <param name="optimize">True to run the <see cref="ExpressionOptimizer"/> on the result (constant folding etc; floating point "x + 0" is folded to "x", so x = -0 gives -0 instead of +0, see <see cref="ExpressionOptimizer.Optimize(Expression)"/>)</param>
        /// <returns>The linq expression</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        public Expression Exp(String expression, IEnumerable<String> parameters, IEnumerable<Expression> parameterValues, bool optimize = false)
        {
            return Exp(expression, parameters.ToArray(), parameterValues.ToArray(), optimize);
        }

        /// <summary>
        /// Parse an expression to a linq expression of type T, using the supplied linq expressions as the parameter values
        /// (useful for embedding the expression into a larger linq expression)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names</param>
        /// <param name="parameterValues">The linq expressions (of type T) to use for the parameters, the index of a name is the index of its value</param>
        /// <param name="optimize">True to run the <see cref="ExpressionOptimizer"/> on the result (constant folding etc; floating point "x + 0" is folded to "x", so x = -0 gives -0 instead of +0, see <see cref="ExpressionOptimizer.Optimize(Expression)"/>)</param>
        /// <returns>The linq expression</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        /// <exception cref="ArgumentNullException">The expression is null (DEBUG builds only)</exception>
        public Expression Exp(String expression, String[] parameters, Expression[] parameterValues, bool optimize = false)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(expression);
#endif//DEBUG
            var exception = new ExceptionThrower(expression);
            try
            {
                var tokens = Tokenizer.Parse(expression, ValueReader, parameters, 
                    (name, index) => 
                    { 
                        return (start, end) => new Token(start, end, TokenClasses.Operand, stack => parameterValues[index]); 
                    });
                var rpn = ShuntingYard.ReversePolishNotation(tokens, token => token.Class, token => token.Precedence, token => token.Unary(token.Start, token.End), exception.WithArgumentCount, exception.OnEvaluatorException);
                var exp = PostfixEvaluator.FromRpn<Expression, Token>(rpn, (token, stack) => token.Eval(stack), token => token.Class != TokenClasses.Operand, exception.OnEvaluatorException);
                if (optimize)
                    exp = exp.Optimize();
                return exp;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                exception.OnEvaluatorException("Unknown error", ex, null);
                throw;
            }
        }


        /// <summary>
        /// Compile an expression to a function taking the parameter values as objects.
        /// The values are converted to T using <see cref="Convert.ChangeType(object, Type)"/> (strings are converted using the current culture)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value (null for none)</param>
        /// <returns>A function that evaluates the expression, the result is a boxed T</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        public Func<Object[], Object> ObjectEvaluator(String expression, IEnumerable<String> parameters = null)
        {
            var ev = Evaluator(expression, parameters);
            return p =>
            {
                var l = p?.Length ?? 0;
                T[] temp = null;
                if (l > 0)
                {
                    temp = new T[l];
                    for (int i = 0; i < l; ++i)
                        temp[i] = ConvertParameter(p[i]);
                }
                return ev(temp);
            };
        }

        /// <summary>
        /// True if T is a primitive type or decimal (Convert.ChangeType returns the same value if the value already is a T)
        /// </summary>
        static readonly bool IsBuiltInValueType = typeof(T).IsPrimitive || (typeof(T) == typeof(Decimal));

        /// <summary>
        /// Same as (T)Convert.ChangeType(value, typeof(T)), but values that already are a (built in) T are just unboxed
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The value as a T</returns>
        static T ConvertParameter(Object value)
            => (IsBuiltInValueType && (value != null) && (value.GetType() == typeof(T))) ? (T)value : (T)Convert.ChangeType(value, typeof(T));


        /// <summary>
        /// Compile an expression to a function taking the parameter values as objects.
        /// The values are converted to T using <see cref="Convert.ChangeType(object, Type)"/> (strings are converted using the current culture)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value</param>
        /// <returns>A function that evaluates the expression, the result is a boxed T</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        public Func<Object[], Object> ObjectEvaluator(String expression, params String[] parameters)
        {
            var ev = Evaluator(expression, parameters);
            return p =>
            {
                var l = p?.Length ?? 0;
                T[] temp = null;
                if (l > 0)
                {
                    temp = new T[l];
                    for (int i = 0; i < l; ++i)
                        temp[i] = ConvertParameter(p[i]);
                }
                return ev(temp);
            };
        }

        /// <summary>
        /// Compile an expression to a function
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names (null for none), the index of a name is the index of its value in the array passed to the function</param>
        /// <returns>A function that evaluates the expression, the array must contain (at least) one value per parameter (it may be null if there are no parameters)</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        public Func<T[], T> Evaluator(String expression, IEnumerable<String> parameters = null)
        {
            ParameterExpression p;
            var exp = Exp(out p, expression, parameters);
            return Expression.Lambda<Func<T[], T>>(exp, p).Compile();
        }

        /// <summary>
        /// Compile an expression to a function
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names, the index of a name is the index of its value in the array passed to the function</param>
        /// <returns>A function that evaluates the expression, the array must contain (at least) one value per parameter (it may be null if there are no parameters)</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        public Func<T[], T> Evaluator(String expression, params String[] parameters)
        {
            ParameterExpression p;
            var exp = Exp(out p, expression, parameters);
            return Expression.Lambda<Func<T[], T>>(exp, p).Compile();
        }

        /// <summary>
        /// Compile and evaluate an expression (compiling is comparatively slow, use <see cref="Evaluator(string, IEnumerable{string})"/> to evaluate an expression many times)
        /// </summary>
        /// <param name="expression">The expression, ex: "x * 2 + y"</param>
        /// <param name="parameters">The parameter names (null for none), the index of a name is the index of its value</param>
        /// <param name="values">The parameter values</param>
        /// <returns>The value of the expression</returns>
        /// <exception cref="ArgumentException">The expression is invalid (usually an <see cref="ExpressionParserException"/> with the position of the error), or a parameter name is invalid, used twice or the name of a function / constant</exception>
        /// <exception cref="IndexOutOfRangeException">There are fewer values than parameters</exception>
        /// <exception cref="DivideByZeroException">An integer or decimal division by zero</exception>
        public T Value(String expression, IEnumerable<String> parameters = null, params T[] values)
        {
            return Evaluator(expression, parameters)(values);
        }

        /// <summary>
        /// Create a custom evaluator (the standard ones are in <see cref="ExpressionEvaluator"/>)
        /// </summary>
        /// <param name="flags">The operators to add</param>
        /// <param name="valueReader">The reader for numeric values, see <see cref="ExpressionEvaluatorValueReader"/></param>
        /// <param name="customTypes">Types whose public static members (that only use the type T) are made available (null for none):
        /// methods with at least one parameter become functions, constant / read only fields and read only properties become constants.
        /// The value is true to prefix the function and constant names with the type name (ex: "MathSin", "MathPI").
        /// Overloads are selected by the number of arguments; if several types (or several methods) register the same name with the same number of arguments, the first one found is used.
        /// All names are case sensitive</param>
        /// <exception cref="ArgumentException">Both <see cref="ExpressionTypeFlags.IsDecimal"/> and <see cref="ExpressionTypeFlags.IsInteger"/> are set, or two constants (or a constant and a function) have the same name</exception>
        /// <exception cref="ArgumentNullException">The value reader is null</exception>
        public ExpressionEvaluator(ExpressionTypeFlags flags, StringTokenizer<Token>.ValueReader valueReader, IEnumerable<KeyValuePair<Type, bool>> customTypes)
        {
            ArgumentNullException.ThrowIfNull(valueReader);
            var x = new StringTokenizer<Token>();
            Add(x, CommonTokens);
            if (flags.HasFlag(ExpressionTypeFlags.IsNumeric))
                Add(x, NumericTokens);
            if (flags.HasFlag(ExpressionTypeFlags.IsDecimal))
            {
                if (flags.HasFlag(ExpressionTypeFlags.IsInteger))
                    throw new ArgumentException("Can not have both the IsDecimal and IsInteger flags set!", "flags");
                Add(x, DecimalTokens);
            }
            if (flags.HasFlag(ExpressionTypeFlags.IsInteger))
            {
                if (flags.HasFlag(ExpressionTypeFlags.IsDecimal))
                    throw new ArgumentException("Can not have both the IsInteger and IsDecimal flags set!", "flags");
                Add(x, IntegerTokens);
            }
            if (customTypes != null)
            {
                var operandType = typeof(T);
                //  The overloads of each function, by the registered (prefixed) name, indexed by the number of arguments
                var functions = new Dictionary<String, List<MethodInfo>>(StringComparer.Ordinal);
                var functionOrder = new List<String>();
                foreach (var customType in customTypes)
                {
                    var typeInfo = customType.Key.GetTypeInfo();
                    var prefix = customType.Value ? customType.Key.Name : String.Empty;
                    foreach (var f in typeInfo.FindMethods(ReflectionFlags.IsStatic | ReflectionFlags.IsPublic))
                    {
                        if (f.ReturnType != operandType)
                            continue;
                        var a = f.GetParameters();
                        int l = a.Length;
                        if (l <= 0)
                            continue;
                        bool ok = true;
                        foreach (var p in a)
                            ok &= (p.ParameterType == operandType);
                        if (!ok)
                            continue;
                        var name = prefix + f.Name;
                        if (!functions.TryGetValue(name, out var overloads))
                        {
                            overloads = new List<MethodInfo>();
                            functions.Add(name, overloads);
                            functionOrder.Add(name);
                        }
                        while (overloads.Count <= l)
                            overloads.Add(null);
                        //  The first method found with a name and number of arguments is used
                        overloads[l] ??= f;
                    }
                    foreach (var f in typeInfo.FindProperties(ReflectionFlags.IsStatic | ReflectionFlags.IsPublic))
                    {
                        if (f.PropertyType != operandType)
                            continue;
                        if (!f.CanRead)
                            continue;
                        if (f.CanWrite)
                            continue;
                        var val = Expression.Constant(f.GetValue(null, null));
                        x.AddIdentifier(prefix + f.Name, (start, end) => new Token(start, end, TokenClasses.Operand, stack => val));
                    }
                    foreach (var f in typeInfo.FindFields(ReflectionFlags.IsStatic | ReflectionFlags.IsPublic))
                    {
                        if (f.FieldType != operandType)
                            continue;
                        if (!(f.IsInitOnly || f.IsLiteral))
                            continue;
                        var val = Expression.Constant(f.GetValue(null));
                        x.AddIdentifier(prefix + f.Name, (start, end) => new Token(start, end, TokenClasses.Operand, stack => val));
                    }
                }
                foreach (var name in functionOrder)
                {
                    var methods = functions[name];
                    var ml = methods.Count;
                    var evals = new Func<Stack<Expression>, Expression>[ml];
                    Func<Stack<Expression>, Expression> def = null;
                    for (int l = 0; l < ml; ++l)
                    {
                        var mi = methods[l];
                        if (mi == null)
                            continue;
                        var argCount = l;
                        evals[l] = stack =>
                        {
                            Expression[] args = GC.AllocateUninitializedArray<Expression>(argCount);
                            int i = argCount;
                            while (i > 0)
                            {
                                --i;
                                args[i] = stack.Pop();
                            }
                            return Expression.Call(mi, args);
                        };
                        //  Used if the function isn't followed by parentheses (the argument count is unknown): the overload with the fewest arguments
                        def ??= evals[l];
                    }
                    x.AddIdentifier(name, (start, end) => new Token(start, end, TokenClasses.Function, def, 0, null, evals));
                }
            }
            ValueReader = valueReader;
            Tokenizer = x;
        }

        private static void Add(StringTokenizer<Token> x, IEnumerable<Tuple<String, StringTokenizer<Token>.TokenCreator>> tokens)
        {
            foreach (var t in tokens)
                x.AddOperator(t.Item1, t.Item2);
        }

        private sealed class ExceptionThrower
        {
            public ExceptionThrower(String expression, String argName = "expression")
            {
                Expression = expression;
                ArgName = argName;
            }
            private readonly String Expression;
            private readonly String ArgName;

            public void OnEvaluatorException(String message, Exception ex, Token token)
            {
                if (token != null)
                    throw new ExpressionParserException(message, Expression, token.Start, token.End, ex, ArgName);
                throw new ArgumentException(message, ArgName, ex);
            }

            /// <summary>
            /// Get the overload of a function taking a number of arguments, throws if there is none
            /// </summary>
            public Token WithArgumentCount(Token token, int count)
            {
                var o = token.Overloads;
                if (o == null)
                    return token;
                if ((count < o.Length) && (o[count] != null))
                    return new Token(token.Start, token.End, TokenClasses.Function, o[count], token.Precedence, token.Unary, o);
                var name = Expression.Substring(token.Start, token.End - token.Start);
                OnEvaluatorException("The function \"" + name + "\" can't take " + count + " argument(s)", null, token);
                return token;
            }
        }

        private readonly StringTokenizer<Token> Tokenizer;
        private readonly StringTokenizer<Token>.ValueReader ValueReader;

        /// <summary>
        /// A parsed token of an expression (used by the value readers to create constant operands)
        /// </summary>
        public sealed class Token
        {
            /// <summary>
            /// Create a constant operand token
            /// </summary>
            /// <param name="start">The start position of the token in the expression (inclusive)</param>
            /// <param name="end">The end position of the token in the expression (exclusive)</param>
            /// <param name="value">The constant value</param>
            public Token(int start, int end, T value)
            {
                Start = start;
                End = end;
                Class = TokenClasses.Operand;
                Eval = f => Expression.Constant(value);
            }
            internal Token(int start, int end, Expression value)
            {
                Start = start;
                End = end;
                Class = TokenClasses.Operand;
                Eval = f => value;
            }
            internal Token(int start, int end, TokenClasses classs)
            {
                Start = start;
                End = end;
                Class = classs;
            }
            internal Token(int start, int end, TokenClasses classs, Func<Stack<Expression>, Expression> eval, int precedence = 0, StringTokenizer<Token>.TokenCreator unary = null, Func<Stack<Expression>, Expression>[] overloads = null)
            {
                Start = start;
                End = end;
                Class = classs;
                Eval = eval;
                Precedence = precedence;
                Unary = unary;
                Overloads = overloads;
            }
            /// <summary>
            /// The start position of the token in the expression (inclusive)
            /// </summary>
            public readonly int Start;
            /// <summary>
            /// The end position of the token in the expression (exclusive)
            /// </summary>
            public readonly int End;
            internal readonly TokenClasses Class;
            internal readonly Func<Stack<Expression>, Expression> Eval;
            internal readonly int Precedence;
            internal readonly StringTokenizer<Token>.TokenCreator Unary;
            /// <summary>
            /// Functions only: the evaluation of the overloads indexed by the number of arguments (null if there is no overload with that number of arguments)
            /// </summary>
            internal readonly Func<Stack<Expression>, Expression>[] Overloads;
            /// <summary>
            /// The class, precedence and unary creator of the token (for debugging)
            /// </summary>
            /// <returns>The text</returns>
            public override string ToString()
            {
                return String.Join(' ', Class, Precedence, Unary);
            }
        }

        private static readonly Expression Const2 = Expression.Constant(Convert.ChangeType(2, typeof(T)));

        /// <summary>
        /// The method used for "^" (power), null to use the default (only exists for Double)
        /// </summary>
        private static readonly MethodInfo PowerMethod = typeof(T) == typeof(Decimal) ? typeof(DecimalMath).GetMethod(nameof(DecimalMath.Pow)) : null;

        private static readonly StringTokenizer<Token>.TokenCreator UnaryAdd = (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => s.Pop(), 40);
        private static readonly StringTokenizer<Token>.TokenCreator UnarySub = (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => Expression.Negate(s.Pop()), 40);
        private static readonly StringTokenizer<Token>.TokenCreator UnaryNot = (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => Expression.Not(s.Pop()), 40);

        private static Tuple<String, StringTokenizer<Token>.TokenCreator> Make(String s, StringTokenizer<Token>.TokenCreator creator)
        {
            return new Tuple<String, StringTokenizer<Token>.TokenCreator>(s, creator);
        }

        private static readonly Tuple<String, StringTokenizer<Token>.TokenCreator>[] CommonTokens = new Tuple<String, StringTokenizer<Token>.TokenCreator>[]
                {
                    Make("(", (start, end) => new Token(start, end, TokenClasses.OpenParentheses)),
                    Make(")", (start, end) => new Token(start, end, TokenClasses.CloseParentheses)),
                    Make(",", (start, end) => new Token(start, end, TokenClasses.ArgumentSeparator)),
                };
        private static readonly Tuple<String, StringTokenizer<Token>.TokenCreator>[] NumericTokens = new Tuple<string, StringTokenizer<Token>.TokenCreator>[]
                {
                    Make("+", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Add(left, right);
                        }, 10, UnaryAdd)),
                    Make("-", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Subtract(left, right);
                        }, 10, UnarySub)),
                    Make("*", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Multiply(left, right);
                        }, 20)),
                    Make("/", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Divide(left, right);
                        }, 20)),
                    Make("%", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Modulo(left, right);
                        }, 20)),
                };
        private static readonly Tuple<String, StringTokenizer<Token>.TokenCreator>[] DecimalTokens = new Tuple<string, StringTokenizer<Token>.TokenCreator>[]
                {
                    Make("^", (start, end) => new Token(start, end, TokenClasses.RightAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Power(left, right, PowerMethod);
                        }, 30)),
                    Make("<<", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s =>
                        {
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Multiply(left, Expression.Power(Const2, right, PowerMethod));
                        }, 5)),
                    Make(">>", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s =>
                        {
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Divide(left, Expression.Power(Const2, right, PowerMethod));
                        }, 5)),
                };
        private static readonly Tuple<String, StringTokenizer<Token>.TokenCreator>[] IntegerTokens = new Tuple<string, StringTokenizer<Token>.TokenCreator>[]
                {
                    Make("<<", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.LeftShift(left, Expression.Convert(right, typeof(Int32)));
                        }, 5)),
                    Make(">>", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.RightShift(left, Expression.Convert(right, typeof(Int32)));
                        }, 5)),
                    //  Same precedence order as C#: & > ^ > | (all below the shifts)
                    Make("|", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s =>
                        {
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Or(left, right);
                        }, 1)),
                    Make("&", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s =>
                        {
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.And(left, right);
                        }, 3)),
                    Make("^", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s =>
                        {
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.ExclusiveOr(left, right);
                        }, 2)),
                    Make("~", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            return Expression.Not(right);
                        }, 40, UnaryNot)),
                    Make("!", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            return Expression.Not(right);
                        }, 40, UnaryNot)),
                };


        /// <inheritdoc/>
        public object ToValue(string expression)
        {
            return Value(expression);
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidCastException">A value is not a boxed T</exception>
        public object ToValue(string expression, IEnumerable<string> parameters, params object[] values)
        {
            var l = values.Length;
            T[] d = GC.AllocateUninitializedArray<T>(l);
            var ds = d.AsSpan();
            for (int i = 0; i < l; ++ i)
                ds[i] = (T)values[i];
            return Value(expression, parameters, d);
        }

        /// <inheritdoc/>
        public IEnumerable<string> Operators
        {
            get
            {
                foreach (var x in Tokenizer.Operators)
                    yield return x;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> Identifiers
        {
            get
            {
                foreach (var x in Tokenizer.Identifiers)
                    yield return x;
            }
        }

        private static Token UnknownIdentifierCreator(int start, int end)
        {
            return new Token(start, end, TokenClasses.Operand, stack => 
            {
                throw new InvalidOperationException("Can't evaluate an unknown identifier!");
            });
        }

        /// <inheritdoc/>
        public IEnumerable<Tuple<TokenClasses, int, int, string>> ToTokens(string expression, IEnumerable<string> parameters, bool allowUnknownIdentifiers = false)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End- token.Start));
        }

        /// <inheritdoc/>
        public IEnumerable<Tuple<TokenClasses, int, int, string>> ToTokens(string expression, bool allowUnknownIdentifiers, params string[] parameters)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End - token.Start));
        }

        /// <inheritdoc/>
        public IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, null, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End - token.Start));
        }

    }

    /// <summary>
    /// Decimal helpers used by the <see cref="ExpressionEvaluator.Decimal"/> evaluator
    /// </summary>
    internal static class DecimalMath
    {
        /// <summary>
        /// Raise a value to a power.
        /// An integer exponent is computed exactly (by repeated squaring, a negative exponent gives 1 / x^-y),
        /// other exponents use <see cref="Math.Pow(double, double)"/> on the values converted to Double
        /// </summary>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        /// <exception cref="OverflowException">The result is too large for a Decimal (or not a number)</exception>
        /// <exception cref="DivideByZeroException">x is 0 and y is a negative integer</exception>
        public static Decimal Pow(Decimal x, Decimal y)
        {
            if ((y == Decimal.Truncate(y)) && (y >= Int64.MinValue + 1) && (y <= Int64.MaxValue))
            {
                var n = (Int64)y;
                if (n >= 0)
                    return PowInt(x, (UInt64)n);
                var un = (UInt64)(-n);
                Decimal p;
                try
                {
                    p = PowInt(x, un);
                }
                catch (OverflowException)
                {
                    //  The result is (close to) zero, the precision of 1/x doesn't matter much here
                    return PowInt(Decimal.One / x, un);
                }
                return Decimal.One / p;
            }
            return (Decimal)Math.Pow((Double)x, (Double)y);
        }

        static Decimal PowInt(Decimal x, UInt64 n)
        {
            Decimal r = Decimal.One;
            while (n != 0)
            {
                if ((n & 1) != 0)
                    r *= x;
                n >>= 1;
                if (n != 0)
                    x *= x;
            }
            return r;
        }
    }

    /// <summary>
    /// The value readers of the standard evaluators (numbers are parsed with the invariant culture)
    /// </summary>
    public static class ExpressionEvaluatorValueReader
    {
        /// <summary>
        /// Reads Double values (<see cref="DoubleValueReader{T}"/>)
        /// </summary>
        public static readonly StringTokenizer<ExpressionEvaluator<Double>.Token>.ValueReader Double = new DoubleValueReader<ExpressionEvaluator<Double>.Token>((start, end, value) => new ExpressionEvaluator<Double>.Token(start, end, value)).ReadValue;
        /// <summary>
        /// Reads Int64 values (<see cref="Int64ValueReader{T}"/>)
        /// </summary>
        public static readonly StringTokenizer<ExpressionEvaluator<Int64>.Token>.ValueReader Int64 = new Int64ValueReader<ExpressionEvaluator<Int64>.Token>((start, end, value) => new ExpressionEvaluator<Int64>.Token(start, end, value)).ReadValue;
        /// <summary>
        /// Reads UInt64 values (<see cref="UInt64ValueReader{T}"/>)
        /// </summary>
        public static readonly StringTokenizer<ExpressionEvaluator<UInt64>.Token>.ValueReader UInt64 = new UInt64ValueReader<ExpressionEvaluator<UInt64>.Token>((start, end, value) => new ExpressionEvaluator<UInt64>.Token(start, end, value)).ReadValue;
        /// <summary>
        /// Reads Decimal values (<see cref="DecimalValueReader{T}"/>)
        /// </summary>
        public static readonly StringTokenizer<ExpressionEvaluator<Decimal>.Token>.ValueReader Decimal = new DecimalValueReader<ExpressionEvaluator<Decimal>.Token>((start, end, value) => new ExpressionEvaluator<Decimal>.Token(start, end, value)).ReadValue;
    }

}
