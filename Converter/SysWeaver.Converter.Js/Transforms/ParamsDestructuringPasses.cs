using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;
using SysWeaver.Converter.Js.Output;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2015: default and rest parameters (destructuring parameters are moved into the body)
    /// </summary>
    sealed class ParametersPass : Pass
    {
        public ParametersPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            if (n is Function f)
            {
                Children(f);
                if (!f.HasSimpleParams)
                {
                    if (f.IsArrow)
                        Warn(f, "Default / rest / destructuring parameters of arrow functions can't be transformed without transforming arrow functions");
                    else
                        Lower(f);
                }
                return f;
            }
            return Children(n);
        }

        static Expr Arg(int i) => B.Index(B.Id("arguments"), B.Num(i));

        void Lower(Function f)
        {
            Ctx.Did(JsFeature.Parameters);
            //  Names used by default values must not be shadowed by declarations in the body
            var used = new HashSet<String>(StringComparer.Ordinal);
            foreach (var p in f.Params)
            {
                Walker.Walk(p, x =>
                {
                    if (x is Identifier i)
                        used.Add(i.Name);
                    return true;
                });
            }
            var paramNames = new HashSet<String>(StringComparer.Ordinal);
            var pn = new List<Identifier>();
            foreach (var p in f.Params)
                ScopeAnalyzer.PatternNames(p, pn);
            foreach (var i in pn)
                paramNames.Add(i.Name);
            if (f.Body != null)
            {
                var sa = ScopeAnalyzer.Analyze(f);
                var fs = sa.Get(f);
                if (fs != null)
                {
                    //  The analyzer puts parameters and the body in one scope, references in the parameters must keep their name
                    var paramIds = new HashSet<Identifier>();
                    foreach (var p in f.Params)
                        Walker.Walk(p, x =>
                        {
                            if (x is Identifier i)
                                paramIds.Add(i);
                            return true;
                        });
                    foreach (var b in fs.Bindings.Values.ToList())
                    {
                        if (b.Kind == BindingKind.Param || !used.Contains(b.Name) || paramNames.Contains(b.Name))
                            continue;
                        var nn = Ctx.UniqueName(b.Name);
                        foreach (var d in b.Declarations)
                            d.Name = nn;
                        foreach (var r in b.References)
                            if (!paramIds.Contains(r.Id))
                                r.Id.Name = nn;
                    }
                }
            }
            var stmts = new List<Stmt>();
            var newParams = new List<Expr>();
            int firstDefault = f.Params.FindIndex(p => p is AssignPattern || p is RestElement);
            bool loose = Ctx.Loose;
            for (int i = 0; i < f.Params.Count; ++i)
            {
                var p = f.Params[i];
                if (firstDefault < 0 || i < firstDefault)
                {
                    if (p is Identifier)
                        newParams.Add(p);
                    else
                    {
                        var t = B.Id(Ctx.UniqueName("ref"));
                        newParams.Add(t);
                        stmts.Add(new VarDecl("var", p, B.Id(t.Name)));
                    }
                    continue;
                }
                switch (p)
                {
                    case AssignPattern ap:
                        if (loose && ap.Left is Identifier li && !stmts.Exists(s => s is ForStmt))
                        {
                            //  if (a === void 0) a = default;
                            newParams.Add(B.Id(li.Name));
                            stmts.Add(new IfStmt(B.Bin("===", B.Id(li.Name), B.Void0()), B.Stmt(B.Assign(B.Id(li.Name), ap.Right))));
                            break;
                        }
                        //  var a = arguments.length > i && arguments[i] !== undefined ? arguments[i] : default;
                        stmts.Add(new VarDecl("var", ap.Left, B.Cond(
                            B.Bin("&&", B.Bin(">", B.Member(B.Id("arguments"), "length"), B.Num(i)), B.Bin("!==", Arg(i), B.Void0())),
                            Arg(i), ap.Right)).At<Stmt>(ap));
                        break;
                    case RestElement re:
                        {
                            var len = Ctx.UniqueName("len");
                            var key = Ctx.UniqueName("key");
                            Expr target = re.Arg;
                            Identifier arr = target as Identifier;
                            if (arr == null)
                                arr = B.Id(Ctx.UniqueName("rest"));
                            Expr size = i == 0 ? B.Id(len) : B.Cond(B.Bin(">", B.Id(len), B.Num(i)), B.Bin("-", B.Id(len), B.Num(i)), B.Num(0));
                            Expr index = i == 0 ? B.Id(key) : B.Bin("-", B.Id(key), B.Num(i));
                            var init = new VarDecl("var");
                            init.Decls.Add(new VarDeclarator(B.Id(len), B.Member(B.Id("arguments"), "length")));
                            init.Decls.Add(new VarDeclarator(B.Id(arr.Name), new NewExpr(B.Id("Array"), size)));
                            init.Decls.Add(new VarDeclarator(B.Id(key), B.Num(i)));
                            stmts.Add(new ForStmt
                            {
                                Init = init,
                                Test = B.Bin("<", B.Id(key), B.Id(len)),
                                Update = new UpdateExpr("++", false, B.Id(key)),
                                Body = B.Stmt(B.Assign(B.Index(B.Id(arr.Name), index), B.Index(B.Id("arguments"), B.Id(key)))),
                            });
                            if (target is not Identifier)
                                stmts.Add(new VarDecl("var", target, B.Id(arr.Name)));
                            break;
                        }
                    default:
                        if (loose && p is Identifier)
                        {
                            newParams.Add(p);
                            break;
                        }
                        stmts.Add(new VarDecl("var", p, Arg(i)));
                        break;
                }
            }
            f.Params = newParams;
            Es2015.Prepend(f, stmts);
        }
    }

    /// <summary>
    /// ES2015: destructuring (declarations, assignments, parameters, catch and for-in / for-of heads)
    /// </summary>
    sealed class DestructuringPass : Pass
    {
        public DestructuringPass(TransformContext ctx) : base(ctx)
        {
        }

        static bool IsPattern(Node n) => n is ObjectPattern || n is ArrayPattern || n is AssignPattern;

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case Function f when f.Params.Exists(IsPattern) || f.Params.Exists(p => p is RestElement re && IsPattern(re.Arg)):
                    {
                        Children(f);
                        if (f.IsArrow)
                        {
                            Warn(f, "Destructuring parameters of arrow functions can't be transformed without transforming arrow functions");
                            return f;
                        }
                        var stmts = new List<Stmt>();
                        for (int i = 0; i < f.Params.Count; ++i)
                        {
                            var p = f.Params[i];
                            Expr pattern = p;
                            Func<Identifier, Expr> wrap = x => x;
                            if (p is AssignPattern ap)
                            {
                                pattern = ap.Left;
                                wrap = x => new AssignPattern(x, ap.Right);
                            }
                            else if (p is RestElement re)
                            {
                                pattern = re.Arg;
                                wrap = x => new RestElement(x);
                            }
                            if (!IsPattern(pattern))
                                continue;
                            var t = B.Id(Ctx.UniqueName("ref"));
                            f.Params[i] = wrap(t);
                            stmts.Add(LowerDecl(new VarDecl("var", pattern, B.Id(t.Name))));
                        }
                        Es2015.Prepend(f, stmts);
                        return f;
                    }
                case CatchClause c when IsPattern(c.Param):
                    {
                        Children(c);
                        var t = B.Id(Ctx.UniqueName("e"));
                        c.Body.Body.Insert(0, LowerDecl(new VarDecl("let", c.Param, B.Id(t.Name))));
                        c.Param = t;
                        return c;
                    }
                case ForInStmt fi when LeftIsPattern(fi.Left):
                    Children(fi);
                    fi.Left = ForLeft(fi.Left, out var pre1);
                    fi.Body = Prepend(pre1, fi.Body);
                    return fi;
                case ForOfStmt fo when LeftIsPattern(fo.Left):
                    Children(fo);
                    fo.Left = ForLeft(fo.Left, out var pre2);
                    fo.Body = Prepend(pre2, fo.Body);
                    return fo;
                case VarDecl v when v.Decls.Exists(d => IsPattern(d.Id) && d.Init != null):
                    Children(v);
                    return LowerDecl(v);
                case ExprStmt { Expression: AssignExpr { Op: "=" } a } es when IsPattern(a.Target):
                    {
                        Children(a);
                        var outp = AssignExpand(a, false);
                        return new StatementList(outp.Select(x => (Stmt)B.Stmt(x).At<Stmt>(es))) { LeadingComments = es.LeadingComments };
                    }
                case AssignExpr { Op: "=" } a when IsPattern(a.Target):
                    {
                        Children(a);
                        return B.Seq(AssignExpand(a, true)).At<Expr>(a);
                    }
            }
            return Children(n);
        }

        static bool LeftIsPattern(Node left) => left switch
        {
            VarDecl vd => vd.Decls.Count == 1 && IsPattern(vd.Decls[0].Id),
            Expr e => IsPattern(e),
            _ => false,
        };

        static Stmt Prepend(Stmt first, Stmt body)
        {
            var b = new BlockStmt { Body = { first } };
            if (body is BlockStmt bb && !bb.Body.Exists(s => s is VarDecl { Kind: not "var" } || s is ClassDecl || s is FunctionDecl))
                b.Body.AddRange(bb.Body);
            else
                b.Body.Add(body);
            return b;
        }

        Node ForLeft(Node left, out Stmt pre)
        {
            Ctx.Did(JsFeature.Destructuring);
            if (left is VarDecl vd)
            {
                var t = B.Id(Ctx.UniqueName("ref"));
                pre = LowerDecl(new VarDecl(vd.Kind, vd.Decls[0].Id, B.Id(t.Name)));
                return new VarDecl(vd.Kind, t);
            }
            var tmp = Temp("ref");
            var outp = new List<(Expr Target, Expr Value)>();
            Expand((Expr)left, B.Id(tmp.Name), outp, h => Temp(h));
            pre = new ExprStmt(B.Seq(outp.Select(x => (Expr)B.Assign(x.Target, x.Value))));
            return B.Id(tmp.Name);
        }

        /// <summary>
        /// Lower a declaration with patterns
        /// </summary>
        VarDecl LowerDecl(VarDecl v)
        {
            Ctx.Did(JsFeature.Destructuring);
            var r = new VarDecl(v.Kind).At<VarDecl>(v);
            r.LeadingComments = v.LeadingComments;
            foreach (var d in v.Decls)
            {
                if (!IsPattern(d.Id) || d.Init == null)
                {
                    r.Decls.Add(d);
                    continue;
                }
                var outp = new List<(Expr Target, Expr Value)>();
                Expand(d.Id, d.Init, outp, h => B.Id(Ctx.UniqueName(h)));
                foreach (var x in outp)
                    r.Decls.Add(new VarDeclarator(x.Target, x.Value));
            }
            return r;
        }

        /// <summary>
        /// Expand an assignment with a pattern target to a list of simple assignments
        /// </summary>
        List<Expr> AssignExpand(AssignExpr a, bool needValue)
        {
            Ctx.Did(JsFeature.Destructuring);
            var outp = new List<(Expr Target, Expr Value)>();
            Expr src = a.Value;
            Expr result = null;
            if (src is Identifier si && !Assigns(a.Target, si.Name))
            {
                if (needValue)
                    result = src.Clone<Expr>();
            }
            else
            {
                var t = Temp(B.NameHint(src));
                outp.Add((t, src));
                src = B.Id(t.Name);
                if (needValue)
                    result = B.Id(t.Name);
            }
            Expand(a.Target, src, outp, h => Temp(h), true);
            var r = outp.Select(x => (Expr)B.Assign(x.Target, x.Value)).ToList();
            if (result != null)
                r.Add(result);
            return r;
        }

        /// <summary>
        /// True if a pattern assigns to a name
        /// </summary>
        static bool Assigns(Expr pattern, String name)
        {
            var l = new List<Identifier>();
            ScopeAnalyzer.PatternNames(pattern, l);
            return l.Exists(i => i.Name == name);
        }

        /// <summary>
        /// Expand a pattern into (target, value) pairs, targets are identifiers or member expressions
        /// </summary>
        void Expand(Expr pattern, Expr value, List<(Expr Target, Expr Value)> outp, Func<String, Identifier> newTemp, bool isTopLevel = true)
        {
            Expr Memo(Expr v, String hint, bool force = false)
            {
                if (!force && v is Identifier vi && !Assigns(pattern, vi.Name))
                    return v;
                var t = newTemp(hint);
                outp.Add((t, v));
                return B.Id(t.Name);
            }
            switch (pattern)
            {
                case AssignPattern ap:
                    {
                        var v = Memo(value, B.NameHint(value), value is not Identifier);
                        var def = B.Cond(B.Bin("===", v, B.Void0()), ap.Right, v.Clone<Expr>());
                        Expand(ap.Left, def, outp, newTemp, false);
                        return;
                    }
                case ObjectPattern op:
                    {
                        if (op.Properties.Count == 0)
                        {
                            var src0 = Memo(value, "ref", value is not Identifier);
                            outp.Add((newTemp("unused"), B.Call(Helper("objectDestructuringEmpty"), src0)));
                            return;
                        }
                        bool single = op.Properties.Count == 1 && op.Properties[0] is Property;
                        var src = single ? value : Memo(value, B.NameHint(value), value is not Identifier);
                        var keys = new List<Expr>();
                        foreach (var x in op.Properties)
                        {
                            if (x is RestElement re)
                            {
                                var h = Helper(Ctx.Loose ? "objectWithoutPropertiesLoose" : "objectWithoutProperties");
                                outp.Add((re.Arg, B.Call(h, src.Clone<Expr>(), new ArrayExpr(keys))));
                                continue;
                            }
                            var p = (Property)x;
                            Expr access;
                            if (p.Computed)
                            {
                                var key = p.Key;
                                if (op.Properties.Exists(y => y is RestElement) && !B.IsConstant(key))
                                {
                                    var kt = newTemp("key");
                                    outp.Add((kt, key));
                                    key = B.Id(kt.Name);
                                    keys.Add(B.Call(Helper("toPropertyKey"), B.Id(kt.Name)));
                                }
                                else
                                    keys.Add(key.Clone<Expr>());
                                access = B.Index(src.Clone<Expr>(), key);
                            }
                            else
                            {
                                switch (p.Key)
                                {
                                    case Identifier ki:
                                        access = B.Member(src.Clone<Expr>(), ki.Name);
                                        keys.Add(B.Str(ki.Name));
                                        break;
                                    case NumberLiteral nk:
                                        access = B.Index(src.Clone<Expr>(), p.Key);
                                        keys.Add(B.Str(JsWriter.Number(nk.Value)));
                                        break;
                                    default:
                                        access = B.Index(src.Clone<Expr>(), p.Key);
                                        keys.Add(p.Key.Clone<Expr>());
                                        break;
                                }
                            }
                            Expand(p.Value, access, outp, newTemp, false);
                        }
                        return;
                    }
                case ArrayPattern ap:
                    {
                        bool hasRest = ap.Elements.Exists(e => e is RestElement);
                        var count = ap.Elements.Count - (hasRest ? 1 : 0);
                        Expr src;
                        if (value is ArrayExpr ae && !ae.Elements.Exists(e => e is SpreadElement) && !hasRest)
                            src = Memo(value, "ref", true);
                        else if (Ctx.Loose)
                            src = Memo(value, B.NameHint(value), value is not Identifier);
                        else
                            src = Memo(hasRest ? B.Call(Helper("toArray"), value) : B.Call(Helper("slicedToArray"), value, B.Num(count)), B.NameHint(value), true);
                        for (int i = 0; i < ap.Elements.Count; ++i)
                        {
                            var e = ap.Elements[i];
                            if (e == null)
                                continue;
                            if (e is RestElement re)
                            {
                                Expand(re.Arg, B.Call(B.Member(src.Clone<Expr>(), "slice"), B.Num(i)), outp, newTemp, false);
                                continue;
                            }
                            Expand(e, B.Index(src.Clone<Expr>(), B.Num(i)), outp, newTemp, false);
                        }
                        return;
                    }
                default:
                    outp.Add((pattern, value));
                    return;
            }
        }
    }
}
