using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Ast;
using SysWeaver.Converter.Js.Output;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2018: object spread ({ ...a }) and object rest in destructuring (const { a, ...rest } = obj)
    /// </summary>
    sealed class ObjectRestSpreadPass : Pass
    {
        public ObjectRestSpreadPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case Function f:
                    Children(f);
                    Params(f);
                    return f;
                case CatchClause c when HasObjectRest(c.Param):
                    {
                        Children(c);
                        Ctx.Did(JsFeature.ObjectRestSpread);
                        var t = B.Id(Ctx.UniqueName("e"));
                        var decl = new VarDecl("let", c.Param, B.Id(t.Name));
                        c.Param = t;
                        c.Body.Body.InsertRange(0, LowerDecl(decl));
                        return c;
                    }
                case ForInStmt fi when ForLeftHasRest(fi.Left):
                    Children(fi);
                    fi.Left = ForLeft(fi.Left, out var pre1);
                    fi.Body = Prepend(pre1, fi.Body);
                    return fi;
                case ForOfStmt fo when ForLeftHasRest(fo.Left):
                    Children(fo);
                    fo.Left = ForLeft(fo.Left, out var pre2);
                    fo.Body = Prepend(pre2, fo.Body);
                    return fo;
                case VarDecl v when v.Decls.Exists(d => HasObjectRest(d.Id)):
                    {
                        Children(v);
                        var r = LowerDecl(v);
                        if (r.Count == 1)
                            return r[0];
                        return new StatementList(r);
                    }
                case AssignExpr { Op: "=" } a when HasObjectRest(a.Target):
                    {
                        Children(a);
                        Ctx.Did(JsFeature.ObjectRestSpread);
                        var outp = new List<(Expr Target, Expr Value)>();
                        var src = a.Value;
                        Expr result;
                        if (src is Identifier)
                            result = src.Clone<Expr>();
                        else
                        {
                            var t = Temp(B.NameHint(src));
                            outp.Add((t, src));
                            src = B.Id(t.Name);
                            result = B.Id(t.Name);
                        }
                        Expand(a.Target, src, outp, false);
                        var seq = outp.Select(x => (Expr)B.Assign(x.Target, x.Value)).ToList();
                        seq.Add(result);
                        return B.Seq(seq).At<Expr>(a);
                    }
                case ObjectExpr o when o.Properties.Exists(p => p is SpreadElement):
                    Children(o);
                    Ctx.Did(JsFeature.ObjectRestSpread);
                    return Spread(o).At<Expr>(o);
            }
            return Children(n);
        }

        #region Spread

        Expr Spread(ObjectExpr o)
        {
            //  Split into literal groups and spread sources
            var args = new List<Expr>();
            ObjectExpr group = null;
            bool loose = Ctx.Loose;
            void Flush()
            {
                if (group == null)
                    return;
                if (!loose && args.Count % 2 == 1)
                    args.Add(new ObjectExpr());
                args.Add(group);
                group = null;
            }
            foreach (var p in o.Properties)
            {
                if (p is SpreadElement se)
                {
                    if (args.Count == 0)
                    {
                        //  The target
                        args.Add(group ?? new ObjectExpr());
                        group = null;
                    }
                    else
                        Flush();
                    //  Spread sources are at odd positions
                    if (!loose && args.Count % 2 == 0)
                        args.Add(new ObjectExpr());
                    args.Add(se.Arg);
                    continue;
                }
                group ??= new ObjectExpr();
                group.Properties.Add(p);
            }
            Flush();
            return B.CallList(Helper(loose ? "extends" : "objectSpread2"), args);
        }

        #endregion//Spread

        #region Rest

        /// <summary>
        /// True if a pattern contains an object pattern with a rest element
        /// </summary>
        public static bool HasObjectRest(Expr p)
        {
            switch (p)
            {
                case ObjectPattern op:
                    foreach (var x in op.Properties)
                    {
                        if (x is RestElement)
                            return true;
                        if (x is Property pp && HasObjectRest(pp.Value))
                            return true;
                    }
                    return false;
                case ArrayPattern ap:
                    foreach (var e in ap.Elements)
                        if (HasObjectRest(e))
                            return true;
                    return false;
                case AssignPattern asp:
                    return HasObjectRest(asp.Left);
                case RestElement re:
                    return HasObjectRest(re.Arg);
            }
            return false;
        }

        static bool ForLeftHasRest(Node left) => left switch
        {
            VarDecl vd => vd.Decls.Count == 1 && HasObjectRest(vd.Decls[0].Id),
            Expr e => HasObjectRest(e),
            _ => false,
        };

        static Stmt Prepend(List<Stmt> pre, Stmt body)
        {
            var b = new BlockStmt(pre);
            b.Body.Add(body);
            return b;
        }

        Node ForLeft(Node left, out List<Stmt> pre)
        {
            Ctx.Did(JsFeature.ObjectRestSpread);
            if (left is VarDecl vd)
            {
                var t = B.Id(Ctx.UniqueName("ref"));
                var decl = new VarDecl(vd.Kind, vd.Decls[0].Id, B.Id(t.Name));
                pre = LowerDecl(decl);
                return new VarDecl(vd.Kind, t);
            }
            var tmp = Temp("ref");
            var outp = new List<(Expr Target, Expr Value)>();
            Expand((Expr)left, B.Id(tmp.Name), outp, false);
            pre = outp.Select(x => (Stmt)B.Stmt(B.Assign(x.Target, x.Value))).ToList();
            return B.Id(tmp.Name);
        }

        void Params(Function f)
        {
            int idx = f.Params.FindIndex(HasObjectRest);
            if (idx < 0)
                return;
            Ctx.Did(JsFeature.ObjectRestSpread);
            var stmts = new List<Stmt>();
            for (int i = idx; i < f.Params.Count; ++i)
            {
                var p = f.Params[i];
                //  Plain identifiers don't evaluate anything
                if (p is Identifier)
                    continue;
                var t = B.Id(Ctx.UniqueName("ref"));
                Expr pattern;
                switch (p)
                {
                    case AssignPattern ap when i > idx || !HasObjectRest(ap.Left):
                        {
                            //  A later parameter with a default value: the default can reference bindings of the earlier patterns,
                            //  so it's evaluated in the body (in order), the parameter keeps a default (so the function length doesn't change)
                            f.Params[i] = new AssignPattern(t, B.Void0()).At<Expr>(ap);
                            var value = B.Cond(B.Bin("===", B.Id(t.Name), B.Void0()), ap.Right, B.Id(t.Name));
                            var d = new VarDecl("var", ap.Left, value);
                            stmts.AddRange(HasObjectRest(ap.Left) ? LowerDecl(d) : new List<Stmt> { d });
                            continue;
                        }
                    case AssignPattern ap:
                        pattern = ap.Left;
                        f.Params[i] = new AssignPattern(t, ap.Right).At<Expr>(ap);
                        break;
                    case RestElement re:
                        pattern = re.Arg;
                        f.Params[i] = new RestElement(t).At<Expr>(re);
                        break;
                    default:
                        pattern = p;
                        f.Params[i] = t;
                        break;
                }
                var decl = new VarDecl("var", pattern, B.Id(t.Name));
                stmts.AddRange(HasObjectRest(pattern) ? LowerDecl(decl) : new List<Stmt> { decl });
            }
            var body = f.EnsureBlockBody().Body;
            body.InsertRange(DirectiveCount(body), stmts);
        }

        /// <summary>
        /// Lower a declaration with object rest patterns into declarations without them
        /// </summary>
        List<Stmt> LowerDecl(VarDecl v)
        {
            Ctx.Did(JsFeature.ObjectRestSpread);
            var r = new VarDecl(v.Kind).At<VarDecl>(v);
            r.LeadingComments = v.LeadingComments;
            foreach (var d in v.Decls)
            {
                if (!HasObjectRest(d.Id))
                {
                    r.Decls.Add(d);
                    continue;
                }
                var outp = new List<(Expr Target, Expr Value)>();
                var src = d.Init;
                if (src == null)
                {
                    //  for-in / for-of left without init (shouldn't happen here)
                    r.Decls.Add(d);
                    continue;
                }
                Expand(d.Id, src, outp, true);
                foreach (var x in outp)
                    r.Decls.Add(new VarDeclarator(x.Target, x.Value));
            }
            return new List<Stmt> { r };
        }

        /// <summary>
        /// Expand a pattern with object rest into a list of (target, value) pairs without object rest at the top level
        /// </summary>
        /// <param name="pattern">The pattern</param>
        /// <param name="value">The value (evaluated once)</param>
        /// <param name="outp">The output</param>
        /// <param name="isDecl">True if the output is used in a declaration (temps are declared in the declaration), false for assignments (temps are vars)</param>
        void Expand(Expr pattern, Expr value, List<(Expr Target, Expr Value)> outp, bool isDecl)
        {
            if (!HasObjectRest(pattern))
            {
                outp.Add((pattern, value));
                return;
            }
            Identifier NewTemp(String hint)
            {
                if (isDecl)
                    return B.Id(Ctx.UniqueName(hint));
                return Temp(hint);
            }
            Expr src = value;
            if (src is not Identifier)
            {
                var t = NewTemp(B.NameHint(value));
                outp.Add((t, value));
                src = B.Id(t.Name);
            }
            var nested = new List<(Expr Pattern, Expr Temp)>();
            switch (pattern)
            {
                case ObjectPattern op:
                    {
                        var np = new ObjectPattern().At<ObjectPattern>(op);
                        var keys = new List<Expr>();
                        Expr restTarget = null;
                        foreach (var x in op.Properties)
                        {
                            if (x is RestElement re)
                            {
                                restTarget = re.Arg;
                                continue;
                            }
                            var p = (Property)x;
                            //  The excluded key
                            if (p.Computed)
                            {
                                if (p.Key is StringLiteral)
                                    keys.Add(p.Key.Clone<Expr>());
                                else if (p.Key is NumberLiteral nk)
                                    keys.Add(B.Str(JsWriter.Number(nk.Value)));
                                else
                                {
                                    var kt = NewTemp("key");
                                    if (isDecl)
                                    {
                                        //  Can't declare inside the pattern, use a var
                                        kt = Temp("key");
                                    }
                                    p.Key = B.Assign(kt, p.Key);
                                    keys.Add(B.Call(Helper("toPropertyKey"), B.Id(kt.Name)));
                                }
                            }
                            else
                            {
                                keys.Add(p.Key switch
                                {
                                    Identifier ki => B.Str(ki.Name),
                                    NumberLiteral nk => B.Str(JsWriter.Number(nk.Value)),
                                    _ => p.Key.Clone<Expr>(),
                                });
                            }
                            //  Nested patterns with object rest
                            if (HasObjectRest(p.Value))
                            {
                                var nt = NewTemp("ref");
                                if (p.Value is AssignPattern ap && HasObjectRest(ap.Left))
                                {
                                    nested.Add((ap.Left, B.Id(nt.Name)));
                                    p.Value = new AssignPattern(nt, ap.Right);
                                }
                                else
                                {
                                    nested.Add((p.Value, B.Id(nt.Name)));
                                    p.Value = nt;
                                }
                                p.Shorthand = false;
                            }
                            np.Properties.Add(p);
                        }
                        outp.Add((np, src));
                        if (restTarget != null)
                        {
                            var h = Helper(Ctx.Loose ? "objectWithoutPropertiesLoose" : "objectWithoutProperties");
                            outp.Add((restTarget, B.Call(h, src.Clone<Expr>(), new ArrayExpr(keys))));
                        }
                        break;
                    }
                case ArrayPattern ap:
                    {
                        var na = new ArrayPattern().At<ArrayPattern>(ap);
                        foreach (var e in ap.Elements)
                        {
                            if (e != null && HasObjectRest(e))
                            {
                                var nt = NewTemp("ref");
                                switch (e)
                                {
                                    case AssignPattern asp when HasObjectRest(asp.Left):
                                        nested.Add((asp.Left, B.Id(nt.Name)));
                                        na.Elements.Add(new AssignPattern(nt, asp.Right));
                                        break;
                                    case RestElement re:
                                        nested.Add((re.Arg, B.Id(nt.Name)));
                                        na.Elements.Add(new RestElement(nt));
                                        break;
                                    default:
                                        nested.Add((e, B.Id(nt.Name)));
                                        na.Elements.Add(nt);
                                        break;
                                }
                                continue;
                            }
                            na.Elements.Add(e);
                        }
                        outp.Add((na, src));
                        break;
                    }
                default:
                    outp.Add((pattern, src));
                    break;
            }
            foreach (var x in nested)
                Expand(x.Pattern, x.Temp, outp, isDecl);
        }

        #endregion//Rest
    }
}
