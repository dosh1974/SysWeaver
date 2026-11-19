using System;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2021: logical assignment (a ||= b, a &amp;&amp;= b, a ??= b)
    /// </summary>
    sealed class LogicalAssignmentPass : Pass
    {
        public LogicalAssignmentPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            Children(n);
            if (n is AssignExpr { Op: "||=" or "&&=" or "??=" } a)
            {
                Ctx.Did(JsFeature.LogicalAssignment);
                MemoLhs(a.Target, out var first, out var second);
                var op = a.Op.Substring(0, 2);
                //  a ||= b => a || (a = b)
                return B.Bin(op, first, B.Assign(second, a.Value)).At<Expr>(a);
            }
            return n;
        }
    }

    /// <summary>
    /// ES2016: the exponentiation operator (a ** b, a **= b)
    /// </summary>
    sealed class ExponentiationPass : Pass
    {
        public ExponentiationPass(TransformContext ctx) : base(ctx)
        {
        }

        static Expr Pow(Expr a, Expr b) => B.Call(B.Member(B.Id("Math"), "pow"), a, b);

        protected override Node Transform(Node n)
        {
            Children(n);
            switch (n)
            {
                case BinaryExpr { Op: "**" } b:
                    Ctx.Did(JsFeature.Exponentiation);
                    if (b.Left is BigIntLiteral || b.Right is BigIntLiteral)
                        Warn(b, "BigInt exponentiation can't be converted to Math.pow");
                    return Pow(b.Left, b.Right).At<Expr>(b);
                case AssignExpr { Op: "**=" } a:
                    {
                        Ctx.Did(JsFeature.Exponentiation);
                        MemoLhs(a.Target, out var first, out var second);
                        //  The target is evaluated first: (_o = o)[_k = k] = Math.pow(_o[_k], b)
                        return B.Assign(first, Pow(second, a.Value)).At<Expr>(a);
                    }
            }
            return n;
        }
    }

    /// <summary>
    /// ES2019: optional catch binding (catch { })
    /// </summary>
    sealed class OptionalCatchBindingPass : Pass
    {
        public OptionalCatchBindingPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            if (n is CatchClause { Param: null } c)
            {
                Ctx.Did(JsFeature.OptionalCatchBinding);
                c.Param = B.Id(Ctx.UniqueName("unused"));
            }
            return Children(n);
        }
    }
}
