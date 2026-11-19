using System;
using System.Collections.Generic;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2020: optional chaining (a?.b, a?.[b], a?.(b)) and nullish coalescing (a ?? b)
    /// </summary>
    sealed class OptionalChainingPass : Pass
    {
        readonly bool DoChain;
        readonly bool DoNullish;

        public OptionalChainingPass(TransformContext ctx) : base(ctx)
        {
            DoChain = ctx.Needs(JsFeature.OptionalChaining);
            DoNullish = ctx.Needs(JsFeature.NullishCoalescing);
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case UnaryExpr { Op: "delete", Arg: ChainExpr ch } u when DoChain:
                    Children(ch);
                    Ctx.Did(JsFeature.OptionalChaining);
                    return OptionalChain.Lower(ch.Expression, Temp, Ctx.Loose, true);
                case ChainExpr ch when DoChain:
                    Children(ch);
                    Ctx.Did(JsFeature.OptionalChaining);
                    return OptionalChain.Lower(ch.Expression, Temp, Ctx.Loose, false).At<Expr>(ch);
                case BinaryExpr { Op: "??" } b when DoNullish:
                    {
                        Children(b);
                        Ctx.Did(JsFeature.NullishCoalescing);
                        Expr first, second;
                        if (B.IsSimple(b.Left))
                        {
                            first = b.Left;
                            second = b.Left.Clone<Expr>();
                        }
                        else
                        {
                            var t = Temp(B.NameHint(b.Left));
                            first = B.Assign(t, b.Left);
                            second = B.Id(t.Name);
                        }
                        var third = second.Clone<Expr>();
                        Expr test = Ctx.Loose
                            ? B.Bin("!=", first, B.Null())
                            : B.IsNotNullish(first, second);
                        return B.Cond(test, third, b.Right).At<Expr>(b);
                    }
            }
            return Children(n);
        }
    }

    /// <summary>
    /// Lowers an optional chain to conditional expressions
    /// </summary>
    static class OptionalChain
    {
        /// <summary>
        /// True if an expression (inside a ChainExpr) contains optional links
        /// </summary>
        static void Flatten(Expr e, List<Expr> links, out Expr @base)
        {
            while (true)
            {
                switch (e)
                {
                    case MemberExpr m:
                        links.Add(m);
                        e = m.Object;
                        continue;
                    case CallExpr c:
                        links.Add(c);
                        e = c.Callee;
                        continue;
                }
                break;
            }
            links.Reverse();
            @base = e;
        }

        /// <summary>
        /// Lower an optional chain (the expression inside a ChainExpr)
        /// </summary>
        /// <param name="e">The chain expression (MemberExpr / CallExpr with optional links)</param>
        /// <param name="temp">Creates a temporary variable</param>
        /// <param name="loose">Use == null instead of === null || === void 0</param>
        /// <param name="isDelete">The chain is the operand of delete (the result is true if short circuited)</param>
        public static Expr Lower(Expr e, Func<String, Identifier> temp, bool loose, bool isDelete)
        {
            var links = new List<Expr>();
            Flatten(e, links, out var cur);
            var checks = new List<Expr>();
            Expr thisObj = null;
            var c = links.Count;
            for (int i = 0; i < c; ++i)
            {
                var link = links[i];
                bool optional = link is MemberExpr om ? om.Optional : ((CallExpr)link).Optional;
                if (optional)
                {
                    //  Memoize the current value and check it
                    Expr first, second;
                    if (B.IsSimple(cur))
                    {
                        first = cur;
                        second = cur.Clone<Expr>();
                    }
                    else
                    {
                        var t = temp(B.NameHint(cur));
                        first = B.Assign(t, cur);
                        second = B.Id(t.Name);
                    }
                    checks.Add(loose ? B.Bin("==", first, B.Null()) : B.IsNullish(first, second));
                    cur = second.Clone<Expr>();
                }
                switch (link)
                {
                    case MemberExpr m:
                        {
                            var obj = cur;
                            thisObj = null;
                            //  If the next link is an optional call, the object (this) must be memoized
                            if (i + 1 < c && links[i + 1] is CallExpr { Optional: true })
                            {
                                if (obj is SuperExpr)
                                    thisObj = B.This();
                                else if (B.IsSimple(obj))
                                    thisObj = obj.Clone<Expr>();
                                else
                                {
                                    var t = temp(B.NameHint(obj));
                                    obj = B.Assign(t, obj);
                                    thisObj = B.Id(t.Name);
                                }
                            }
                            cur = new MemberExpr(obj, m.Property, m.Computed).At<MemberExpr>(m);
                            break;
                        }
                    case CallExpr call:
                        {
                            if (call.Optional && thisObj != null)
                            {
                                //  cur is the memoized function, call it with the memoized this
                                var args = new List<Expr> { thisObj };
                                args.AddRange(call.Args);
                                cur = new CallExpr(B.Member(cur, "call"), args).At<CallExpr>(call);
                            }
                            else
                                cur = new CallExpr(cur, call.Args).At<CallExpr>(call);
                            thisObj = null;
                            break;
                        }
                }
            }
            if (isDelete)
                cur = new UnaryExpr("delete", cur);
            if (checks.Count == 0)
                return cur;
            Expr test = checks[0];
            for (int i = 1; i < checks.Count; ++i)
                test = B.Bin("||", test, checks[i]);
            return B.Cond(test, isDelete ? B.Bool(true) : B.Void0(), cur);
        }
    }
}
