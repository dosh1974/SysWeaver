using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

using System.Linq.Expressions;

namespace SysWeaver
{
    /// <summary>
    /// A class that optimizes linq expressions
    /// </summary>
    public static class ExpressionOptimizer
    {

        /// <summary>
        /// The number of nodes removed by the optimizer
        /// </summary>
        public static long RemovedNodes;
        /// <summary>
        /// The number of nodes that was merged
        /// </summary>
        public static long MergedStaticNodes;


//        private static long Id;

        /// <summary>
        /// Optimizes a linq expression
        /// </summary>
        /// <param name="e">The linq expression to optimize</param>
        /// <returns>An optimized version of the expression</returns>
        public static Expression Optimize(this Expression e)
        {
            var count = NodeCount(e);
            bool changed = false;
            var v = new ReduceVisitor();
            do
            {
                v.Changed = false;
                e = v.Visit(e) ?? throw new NullReferenceException();
                changed |= v.Changed;
            } while (v.Changed);
            if (changed)
            {
                var dnode = count - NodeCount(e);
                Interlocked.Add(ref RemovedNodes, (long)dnode);
                Interlocked.Add(ref MergedStaticNodes, v.Merged);
            }
/*            for (; ; )
            {
                var f = CommonSubExpressionsFinder.Find(e).FirstOrDefault();
                if (f.Value < 2)
                    break;
                var se = f.Key;
                var p = Expression.Variable(se.Type, "_simp" + Interlocked.Increment(ref Id));

                var vars = new List<ParameterExpression>();
                var exps = new List<Expression>();
                var be = e as BlockExpression;
                if (be == null)
                {
                    exps.Add(e);
                }else {
                    vars.AddRange(be.Variables);
                    exps.AddRange(be.Expressions);
                }
                vars.Add(p);
                exps.Insert(0, Expression.Assign(p, se));
                e = Expression.Block(vars, exps);
                bool first = true;
                e = e.Clone((current, cloner) =>
                    {
                        if (current != se)
                            return current;
                        if (first)
                        {
                            first = false;
                            return current;
                        }
                        return p;
                    }, true);
            }*/
            return e;
        }


        /// <summary>
        /// Counts the number of nodes in the expression
        /// </summary>
        /// <param name="e">The linq expression to count nodes in</param>
        /// <returns>The number of nodes found in the supplied linq expression</returns>
        public static int NodeCount(this Expression e)
        {
            return NodeCountVisitor.Count(e);
        }


        private sealed class ReduceVisitor : ExpressionVisitor
        {


            private class Eval
            {

                public static Object Exp(Expression exp)
                {
                    var d = (Activator.CreateInstance(typeof(EvaluatorHelper<>).MakeGenericType(exp.Type), exp) as Eval) ?? throw new NullReferenceException();
                    return d.Result;
                }
                protected Eval(Object result)
                {
                    Result = result;
                }
                private readonly Object Result;
            }

            private sealed class EvaluatorHelper<T> : Eval
            {
                public EvaluatorHelper(Expression exp)
                    :base(Expression.Lambda<Func<T>>(exp).Compile()())
                {
                }
            }
            /*
            private class Compares
            {
                protected Compares(bool result)
                {
                    Result = result;
                }
                private readonly bool Result;

                public static bool Equal(Type t, Object a, Object b)
                {
                    return (Activator.CreateInstance(typeof(ComparesHelper<>).MakeGenericType(t), a, b) as Compares).Result;
                }
            }

            private sealed class ComparesHelper<T> : Compares where 
            {
                public ComparesHelper(Object a, Object b)
                    : base((a as T) == (b as T))
                {
                }
            }
            */
            public bool Changed;

            private enum Actions
            {
                None,
                UseOther,
                SetToDefault,
                NegateOther,
            }

            private Expression ApplyAction(Actions a, Expression current, Expression other)
            {
                if (other == null)
                    throw new Exception("Internal error!");
                switch (a)
                {
                    case Actions.UseOther:
                        Changed = true;
                        return other;
                    case Actions.SetToDefault:
                        Changed = true;
                        return Expression.Constant(Activator.CreateInstance(current.Type), current.Type);
                    case Actions.NegateOther:
                        Changed = true;
                        return Expression.Negate(other);
                }
                return current;
            }

            private static bool IsDefault(ConstantExpression c)
            {
                var o = Activator.CreateInstance(c.Type) ?? throw new NullReferenceException();
                bool res = o.Equals(c.Value);
                return res;
            }
            private static bool IsOne(ConstantExpression c)
            {
                if (!c.Type.GetTypeInfo().IsValueType)
                    return false;
                object c1;
				try {
					c1 = Convert.ChangeType(1, c.Type);
				} catch {
					c1 = null;
				}
                if (c1 == null)
                    return false;
                bool res = c1.Equals(c.Value);
                return res;

            }
            
            static readonly IReadOnlySet<Type> IntegerTypes = ReadOnlyData.Set(
                typeof(Int16),
                typeof(Int32),
                typeof(Int64),
                typeof(float),
                typeof(double),
                typeof(decimal)
            );

            private static bool IsNegOne(ConstantExpression c)
            {
                if (!c.Type.GetTypeInfo().IsValueType)
                    return false;
                if (!IntegerTypes.Contains(c.Type))
                    return false;
                object c1;
				try {
					c1 = Convert.ChangeType(-1, c.Type);
				} catch {
					c1 = null;
				}
                if (c1 == null)
                    return false;
                bool res = c1.Equals(c.Value);
                return res;
            }
            #region Binary

            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryLeftDefaultActions = new Dictionary<ExpressionType, Actions>
            {
                { ExpressionType.Add, Actions.UseOther },
                { ExpressionType.Multiply, Actions.SetToDefault },
                { ExpressionType.Subtract, Actions.NegateOther },
                { ExpressionType.And, Actions.SetToDefault },
                { ExpressionType.Or, Actions.UseOther },
                { ExpressionType.ExclusiveOr, Actions.UseOther },
                { ExpressionType.RightShift, Actions.SetToDefault },
                { ExpressionType.LeftShift, Actions.SetToDefault },
            }.Freeze();

            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryLeftIsOneActions = new Dictionary<ExpressionType, Actions>()
            {
                { ExpressionType.Multiply, Actions.UseOther },
            }.Freeze();

            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryLeftIsMinusOneActions = new Dictionary<ExpressionType, Actions>()
            {
                { ExpressionType.Multiply, Actions.NegateOther },
            }.Freeze();

            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryRightDefaultActions = new Dictionary<ExpressionType, Actions>()
            {
                { ExpressionType.Add, Actions.UseOther },
                { ExpressionType.Multiply, Actions.SetToDefault },
                { ExpressionType.Subtract, Actions.UseOther },
                { ExpressionType.And, Actions.SetToDefault },
                { ExpressionType.Or, Actions.UseOther },
                { ExpressionType.ExclusiveOr, Actions.UseOther },
                { ExpressionType.RightShift, Actions.UseOther },
                { ExpressionType.LeftShift, Actions.UseOther },
            }.Freeze();

            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryRightIsOneActions = new Dictionary<ExpressionType, Actions>()
            {
                { ExpressionType.Multiply, Actions.UseOther },
                { ExpressionType.Divide, Actions.UseOther },
            }.Freeze();


            private static readonly IReadOnlyDictionary<ExpressionType, Actions> BinaryRightIsMinusOneActions = new Dictionary<ExpressionType, Actions>()
            {
                { ExpressionType.Multiply, Actions.NegateOther },
                { ExpressionType.Divide, Actions.NegateOther },
            }.Freeze();

            private static readonly Tuple<Func<ConstantExpression, bool>, IReadOnlyDictionary<ExpressionType, Actions>, IReadOnlyDictionary<ExpressionType, Actions>>[] BinaryTests = new Tuple<Func<ConstantExpression, bool>, IReadOnlyDictionary<ExpressionType, Actions>, IReadOnlyDictionary<ExpressionType, Actions>>[]
            {
                Tuple.Create((Func<ConstantExpression, bool>)IsDefault, BinaryLeftDefaultActions, BinaryRightDefaultActions),
                Tuple.Create((Func<ConstantExpression, bool>)IsOne, BinaryLeftIsOneActions, BinaryRightIsOneActions),
                Tuple.Create((Func<ConstantExpression, bool>)IsNegOne, BinaryLeftIsMinusOneActions, BinaryRightIsMinusOneActions),
            };

            private static HashSet<ExpressionType> GetBinaryTypes()
            {
                var t = new HashSet<ExpressionType>();
                foreach (var x in BinaryTests)
                {
                    foreach (var y in x.Item2)
                        t.Add(y.Key);
                    foreach (var y in x.Item3)
                        t.Add(y.Key);
                }
                return t;
            }
            private static readonly IReadOnlySet<ExpressionType> BinaryTypes = GetBinaryTypes().Freeze();

            #endregion//Binary

            public int Merged { get; private set; }


            private static bool IsConstant(out Object o, Expression e)
            {
                o = null;
                if (e == null)
                    return true;
                var c = e as ConstantExpression;
                if (c != null)
                {
                    o = c.Value;
                    return true;
                }
                var p = e as ParameterExpression;
                if (p != null)
                {
                    o = p;
                    return true;
                }
                return false;
            }


            public static bool CanChangeType(object value, Type conversionType)
            {
                if (conversionType == null)
                    return false;
                if (value == null)
                    return false;
                return false;
//                IConvertible convertible = value as IConvertible;
//                if (convertible == null)
//                    return false;
//                return true;
            }

            public override Expression Visit(Expression node)
            {
                if (node == null)
                    return base.Visit(node);
            //  Merge nodes if possible
                var old = node;
                node = ObjectMerger.GetShared(node);
                if (node != old)
                {
                    Changed = true;
                    ++Merged;
                    return node;
                }
            //  Binary optimizations
                var b = node as BinaryExpression;
                if (b != null) 
                {
                    var left = b.Left as ConstantExpression;
                    var right = b.Right as ConstantExpression;
                    if ((left != null) && (right != null))
                    {
                        Changed = true;
                        return Expression.Constant(Eval.Exp(node), node.Type);
                    }
                    if (BinaryTypes.Contains(b.NodeType))
                    {
                        if (!b.Type.GetTypeInfo().IsValueType)
                            throw new Exception("Don't know how to process binary expression of non value types!");
                        if (left != null)
                        {
                            Actions a = Actions.None;
                            foreach (var x in BinaryTests)
                            {
                                if (x.Item1(left) && (x.Item2.TryGetValue(b.NodeType, out a)))
                                    break;
                            }
                            if (a != Actions.None)
                                return ApplyAction(a, node, b.Right);
                        }
                        if (right != null)
                        {
                            Actions a = Actions.None;
                            foreach (var x in BinaryTests)
                            {
                                if (x.Item1(right) && (x.Item3.TryGetValue(b.NodeType, out a)))
                                    break;
                            }
                            if (a != Actions.None)
                                return ApplyAction(a, node, b.Left);
                        }


                    }
                }
            //  Type specific optimizations
                switch (node.NodeType)
                {
                    case ExpressionType.Call:
                        {
                            var e = node as MethodCallExpression;
                            if (e != null)
                            {
                                if ((e.Object == null) && (e.Method.IsStatic))
                                {
                                    var count = e.Arguments.NullableCount();
                                    Object[] pr = new Object[count];
                                    var prs = pr.AsSpan();
                                    bool isConstant = true;
                                    for (int i = 0; i < count; ++i)
                                    {
                                        var t = e.Arguments[i] as ConstantExpression;
                                        if (t != null)
                                        {
                                            if (t.Type.GetTypeInfo().IsClass)
                                            {
                                                isConstant = false;
                                                break;
                                            }
                                            prs[i] = Eval.Exp(t);
                                        }
                                        else
                                        {
                                            isConstant = false;
                                            break;
                                        }
                                    }
                                    if (isConstant)
                                    {
                                        Changed = true;
                                        return Expression.Constant(e.Method.Invoke(null, pr), e.Type);
                                    }
                                }
                            }
                        }
                        break;
                    case ExpressionType.Convert:
                        {
                            var e = node as UnaryExpression;
                            if (e != null)
                            {
                                if (e.Operand.Type == e.Type)
                                {
                                    Changed = true;
                                    return e.Operand;
                                }
                                if (e.Type == typeof(Object))
                                {
                                    if (e.Operand.Type.GetTypeInfo().IsClass || e.Operand.Type.GetTypeInfo().IsInterface)
                                    {
                                        Changed = true;
                                        return e.Operand;
                                    }
                                    break;
                                }
                                var co = e.Operand as ConstantExpression;
                                if (co != null)
                                {
                                    if (CanChangeType(co.Value, e.Type))
                                    {
                                        try
                                        {
                                            var ce = Expression.Constant(Convert.ChangeType(co.Value, e.Type), e.Type);
                                            Changed = true;
                                            return ce;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case ExpressionType.New:
                        {
                            var e = node as NewExpression;
                            if ((e != null) && (e.Members == null))
                            {
                                Object[] consts = new Object[e.Arguments.Count];
                                var cs = consts.AsSpan();
                                int i = 0;
                                foreach (var arg in e.Arguments)
                                {
                                    var co = arg as ConstantExpression;
                                    if (co == null)
                                        break;
                                    cs[i] = co.Value;
                                    ++i;
                                }
                                if (i == consts.Length)
                                {
                                    try
                                    {
                                        var o = e.Constructor.Invoke(consts);
                                        var ce = Expression.Constant(o, e.Type);
                                        Changed = true;
                                        return ce;
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }
                        break;
                    case ExpressionType.MemberAccess:
                        {
                            var e = node as MemberExpression;
                            var co = e.Expression as ConstantExpression;
                            if (co != null)
                            {
                                var t = co.Type.GetTypeInfo().GetCustomAttributes(true);
                                bool isImmutable = co.Type.GetTypeInfo().IsValueType;
                                var fi = e.Member as FieldInfo;
                                if (fi != null)
                                {
                                    if (fi.IsInitOnly || isImmutable)
                                    {
                                        try
                                        {
                                            var o = fi.GetValue(co.Value);
                                            var ce = Expression.Constant(o, e.Type);
                                            Changed = true;
                                            return ce;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                else
                                {
                                    if (isImmutable)
                                    {
                                        var pi = e.Member as PropertyInfo;
                                        if (pi != null)
                                        {
                                            try
                                            {
                                                var o = pi.GetValue(co.Value, null);
                                                var ce = Expression.Constant(o, e.Type);
                                                Changed = true;
                                                return ce;
                                            }
                                            catch
                                            {
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        break;
                    case ExpressionType.Conditional:
                        {
                            var e = node as ConditionalExpression;
                            var co = e.Test as ConstantExpression;
                            if (co != null)
                            {
                                Changed = true;
                                return ((Boolean)co.Value) ? e.IfTrue : e.IfFalse;
                            }
                        }
                        break;
                }
                return base.Visit(node);
            }


        }

    }

    internal sealed class CommonSubExpressionsFinder : ExpressionVisitor
    {
        private CommonSubExpressionsFinder()
        {
        }

        public static IEnumerable<KeyValuePair<Expression, int>> Find(Expression exp)
        {
            var t = new CommonSubExpressionsFinder();
            t.Visit(exp);
            var res = t.UniqueExpressions.ToArray();
            Array.Sort(res, (a, b) => -a.Value.CompareTo(b.Value));
            return res;
        }

        private readonly Dictionary<Expression, int> UniqueExpressions = new Dictionary<Expression, int>();
        public override Expression Visit(Expression node)
        {
            if (node == null)
                return node;
            switch (node.NodeType)
            {
                case ExpressionType.Constant:
                case ExpressionType.Parameter:
                case ExpressionType.RuntimeVariables:
                    return node;
            }
            node = ObjectMerger.GetShared(node);
            if (UniqueExpressions.ContainsKey(node))
            {
                UniqueExpressions[node] += 1;
                return node;
            }
            UniqueExpressions.Add(node, 1);
            return base.Visit(node);
        }
    }

    internal sealed class NodeCountVisitor : ExpressionVisitor
    {

        private NodeCountVisitor()
        {
        }

        public static int Count(Expression e)
        {
            var t = new NodeCountVisitor();
            t.Visit(e);
            return t.InternalCount;
        }

        private int InternalCount;

        public override Expression Visit(Expression node)
        {
            ++InternalCount;
            return base.Visit(node) ?? throw new NullReferenceException();
        }
    }


}
