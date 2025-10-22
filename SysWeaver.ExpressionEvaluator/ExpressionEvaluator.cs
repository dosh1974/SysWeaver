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




    public interface IExpressionEvaluator
    {
        Object ToValue(String expression);
        Object ToValue(String expression, IEnumerable<String> parameters, params Object[] values);
        IEnumerable<String> Operators { get; }
        IEnumerable<String> Identifiers { get; }
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, IEnumerable<String> parameters, bool allowUnknownIdentifiers = false);
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers);
        IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers, params String[] parameters);

        Func<Object[], Object> ObjectEvaluator(String expression, IEnumerable<String> parameters = null);

        Func<Object[], Object> ObjectEvaluator(String expression, params String[] parameters);


    }

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

        public static readonly ExpressionEvaluator<Double> Double = new ExpressionEvaluator<Double>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsDecimal, ExpressionEvaluatorValueReader.Double, Extensions);
        public static readonly ExpressionEvaluator<Int64> Int64 = new ExpressionEvaluator<Int64>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsInteger, ExpressionEvaluatorValueReader.Int64, Extensions);
        public static readonly ExpressionEvaluator<UInt64> UInt64 = new ExpressionEvaluator<UInt64>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsInteger, ExpressionEvaluatorValueReader.UInt64, Extensions);
        public static readonly ExpressionEvaluator<Decimal> Decimal = new ExpressionEvaluator<Decimal>(ExpressionTypeFlags.IsNumeric | ExpressionTypeFlags.IsDecimal, ExpressionEvaluatorValueReader.Decimal, Extensions);

        public static readonly StringComparer DefaultComparerCase = StringComparer.Ordinal;
        public static readonly StringComparer DefaultComparer = StringComparer.OrdinalIgnoreCase;

        public static readonly StringComparison DefaultStringComparisonCase = StringComparison.Ordinal;
        public static readonly StringComparison DefaultStringComparison = StringComparison.OrdinalIgnoreCase;

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

        public static IExpressionEvaluator Get(Type type)
        {
            return Evaluators[type];
        }


    }


    [Flags]
    public enum ExpressionTypeFlags
    {
        CaseSensitive = 1,
        IsNumeric = 2,
        IsDecimal = 4,
        IsInteger = 8,
    }


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
        public Expression Exp(out ParameterExpression inputParameters, String expression, IEnumerable<String> parameters = null, bool optimize = false)
        {
            return Exp(out inputParameters, expression, optimize, parameters.Nullable().ToArray());
        }

        public Expression Exp(out ParameterExpression inputParameters, String expression, bool optimize, params String[] parameters)
        {
            inputParameters = InputParameters;
            var exception = new ExceptionThrower(expression);
            try
            {
                var tokens = Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, stack => Expression.ArrayAccess(InputParameters, Expression.Constant(index))); });
                var rpn = ShuntingYard.ReversePolishNotation(tokens, token => token.Class, token => token.Precedence, token => token.Unary(token.Start, token.End), exception.OnEvaluatorException);
                var exp = PostfixEvaluator.FromRpn<Expression, Token>(rpn, (token, stack) => token.Eval(stack), token => token.Class != TokenClasses.Operand, exception.OnEvaluatorException);
                if (optimize)
                    exp = exp.Optimize();
                return exp;
            }
            catch (Exception ex)
            {
                exception.OnEvaluatorException("Unknown error", ex, null);
                throw;
            }
        }

        public Expression Exp(String expression, IEnumerable<String> parameters, IEnumerable<Expression> parameterValues, bool optimize = false)
        {
            return Exp(expression, parameters.ToArray(), parameterValues.ToArray(), optimize);
        }

        public Expression Exp(String expression, String[] parameters, Expression[] parameterValues, bool optimize = false)
        {
            var exception = new ExceptionThrower(expression);
            try
            {
                var tokens = Tokenizer.Parse(expression, ValueReader, parameters, 
                    (name, index) => 
                    { 
                        return (start, end) => new Token(start, end, TokenClasses.Operand, stack => parameterValues[index]); 
                    });
                var rpn = ShuntingYard.ReversePolishNotation(tokens, token => token.Class, token => token.Precedence, token => token.Unary(token.Start, token.End), exception.OnEvaluatorException);
                var exp = PostfixEvaluator.FromRpn<Expression, Token>(rpn, (token, stack) => token.Eval(stack), token => token.Class != TokenClasses.Operand, exception.OnEvaluatorException);
                if (optimize)
                    exp = exp.Optimize();
                return exp;
            }
            catch (Exception ex)
            {
                exception.OnEvaluatorException("Unknown error", ex, null);
                throw;
            }
        }


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
                        temp[i] = (T)Convert.ChangeType(p[i], typeof(T));
                }
                return ev(temp);
            };
        }


        public Func<Object[], Object> ObjectEvaluator(String expression, params String[] parameters)
        {
            var ev = Evaluator(expression, parameters);
            return p =>
            {
                var l = p?.Length ?? 0;
                T[] temp = null;
                if (l > 0)
                {
                    for (int i = 0; i < l; ++i)
                        temp[i] = (T)Convert.ChangeType(temp[i], typeof(T));
                }
                return ev(temp);
            };
        }

        public Func<T[], T> Evaluator(String expression, IEnumerable<String> parameters = null)
        {
            ParameterExpression p;
            var exp = Exp(out p, expression, parameters);
            return Expression.Lambda<Func<T[], T>>(exp, p).Compile();
        }

        public Func<T[], T> Evaluator(String expression, params String[] parameters)
        {
            ParameterExpression p;
            var exp = Exp(out p, expression, parameters);
            return Expression.Lambda<Func<T[], T>>(exp, p).Compile();
        }

        public T Value(String expression, IEnumerable<String> parameters = null, params T[] values)
        {
            return Evaluator(expression, parameters)(values);
        }

        public ExpressionEvaluator(ExpressionTypeFlags flags, StringTokenizer<Token>.ValueReader valueReader, IEnumerable<KeyValuePair<Type, bool>> customTypes)
        {
            var x = new StringTokenizer<Token>(flags.HasFlag(ExpressionTypeFlags.CaseSensitive));
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
                HashSet<String> seen = new HashSet<string>();
                foreach (var customType in customTypes)
                {
                    var typeInfo = customType.Key.GetTypeInfo();
                    var typeName = customType.Key.Name;
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
                        if (!seen.Add(f.Name))
                            continue;
                        var mi = f;
                        x.AddIdentifier((customType.Value ? typeName : String.Empty) + f.Name, (start, end) => new Token(start, end, TokenClasses.Function, stack =>
                        {
                            Expression[] args = GC.AllocateUninitializedArray<Expression>(l);
                            int i = l;
                            while (i > 0)
                            {
                                --i;
                                args[i] = stack.Pop();
                            }
                            return Expression.Call(mi, args);
                        }));
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
                        x.AddIdentifier(f.Name, (start, end) => new Token(start, end, TokenClasses.Operand, stack => val));
                    }
                    foreach (var f in typeInfo.FindFields(ReflectionFlags.IsStatic | ReflectionFlags.IsPublic))
                    {
                        if (f.FieldType != operandType)
                            continue;
                        if (!(f.IsInitOnly || f.IsLiteral))
                            continue;
                        var val = Expression.Constant(f.GetValue(null));
                        x.AddIdentifier(f.Name, (start, end) => new Token(start, end, TokenClasses.Operand, stack => val));
                    }
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
        }

        private readonly StringTokenizer<Token> Tokenizer;
        private readonly StringTokenizer<Token>.ValueReader ValueReader;

        public sealed class Token
        {
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
            internal Token(int start, int end, TokenClasses classs, Func<Stack<Expression>, Expression> eval, int precedence = 0, StringTokenizer<Token>.TokenCreator unary = null)
            {
                Start = start;
                End = end;
                Class = classs;
                Eval = eval;
                Precedence = precedence;
                Unary = unary;
            }
            public readonly int Start;
            public readonly int End;
            internal readonly TokenClasses Class;
            internal readonly Func<Stack<Expression>, Expression> Eval;
            internal readonly int Precedence;
            internal readonly StringTokenizer<Token>.TokenCreator Unary;
            public override string ToString()
            {
                return String.Join(' ', Class, Precedence, Unary);
            }
        }

        private static readonly Expression Const2 = Expression.Constant(Convert.ChangeType(2, typeof(T)));

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
                            return Expression.Power(left, right);
                        }, 30)),
                    Make("<<", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Multiply(left, Expression.Power(Const2, right));
                        }, 5)),
                    Make(">>", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.Divide(left, Expression.Power(Const2, right));
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
                        }, 1)),
                    Make("^", (start, end) => new Token(start, end, TokenClasses.LeftAssociativeOperator, s => 
                        { 
                            var right = s.Pop();
                            var left = s.Pop();
                            return Expression.ExclusiveOr(left, right);
                        }, 1)),
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


        public object ToValue(string expression)
        {
            return Value(expression);
        }

        public object ToValue(string expression, IEnumerable<string> parameters, params object[] values)
        {
            var l = values.Length;
            T[] d = GC.AllocateUninitializedArray<T>(l);
            var ds = d.AsSpan();
            for (int i = 0; i < l; ++ i)
                ds[i] = (T)values[i];
            return Value(expression, parameters, d);
        }

        public IEnumerable<string> Operators
        {
            get
            {
                foreach (var x in Tokenizer.Operators)
                    yield return x;
            }
        }

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
                throw new Exception("Can't evaluate an unknown identifier!");
            });
        }

        public IEnumerable<Tuple<TokenClasses, int, int, string>> ToTokens(string expression, IEnumerable<string> parameters, bool allowUnknownIdentifiers = false)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End- token.Start));
        }

        public IEnumerable<Tuple<TokenClasses, int, int, string>> ToTokens(string expression, bool allowUnknownIdentifiers, params string[] parameters)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, parameters, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End - token.Start));
        }

        public IEnumerable<Tuple<TokenClasses, int, int, String>> ToTokens(String expression, bool allowUnknownIdentifiers)
        {
            foreach (var token in Tokenizer.Parse(expression, ValueReader, null, (name, index) => { return (start, end) => new Token(start, end, TokenClasses.Operand, null); }, allowUnknownIdentifiers ? UnknownIdentifierCreator : (StringTokenizer<Token>.TokenCreator)null))
                yield return Tuple.Create(token.Class, token.Start, token.End, expression.Substring(token.Start, token.End - token.Start));
        }

    }

    public static class ExpressionEvaluatorValueReader
    {
        public static readonly StringTokenizer<ExpressionEvaluator<Double>.Token>.ValueReader Double = new DoubleValueReader<ExpressionEvaluator<Double>.Token>((start, end, value) => new ExpressionEvaluator<Double>.Token(start, end, value)).ReadValue;
        public static readonly StringTokenizer<ExpressionEvaluator<Int64>.Token>.ValueReader Int64 = new Int64ValueReader<ExpressionEvaluator<Int64>.Token>((start, end, value) => new ExpressionEvaluator<Int64>.Token(start, end, value)).ReadValue;
        public static readonly StringTokenizer<ExpressionEvaluator<UInt64>.Token>.ValueReader UInt64 = new UInt64ValueReader<ExpressionEvaluator<UInt64>.Token>((start, end, value) => new ExpressionEvaluator<UInt64>.Token(start, end, value)).ReadValue;
        public static readonly StringTokenizer<ExpressionEvaluator<Decimal>.Token>.ValueReader Decimal = new DecimalValueReader<ExpressionEvaluator<Decimal>.Token>((start, end, value) => new ExpressionEvaluator<Decimal>.Token(start, end, value)).ReadValue;
    }

}
