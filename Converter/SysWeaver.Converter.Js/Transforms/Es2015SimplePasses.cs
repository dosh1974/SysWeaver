using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;
using SysWeaver.Converter.Js.Output;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Shared ES2015 utilities
    /// </summary>
    static class Es2015
    {
        static readonly HashSet<String> Reserved = new HashSet<String>(StringComparer.Ordinal)
        {
            "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else", "enum", "export", "extends",
            "false", "finally", "for", "function", "if", "import", "in", "instanceof", "new", "null", "return", "super", "switch", "this",
            "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield", "let", "static", "implements", "interface", "package",
            "private", "protected", "public", "await", "arguments", "eval", "undefined", "NaN", "Infinity",
        };

        /// <summary>
        /// True if the name can be used as a function name (valid identifier, not reserved)
        /// </summary>
        public static bool IsValidName(String name)
        {
            if (String.IsNullOrEmpty(name) || Reserved.Contains(name))
                return false;
            for (int i = 0; i < name.Length; ++i)
            {
                var c = name[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '$' || (i > 0 && c >= '0' && c <= '9');
                if (!ok)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True if a function references an identifier with the name (so the function can't be named that without shadowing)
        /// </summary>
        public static bool References(Function f, String name) => Walker.Any(f, n => n is Identifier i && i.Name == name);

        /// <summary>
        /// Name a function (function expression for a method / property) if safe: the name is valid and not referenced inside the function
        /// </summary>
        public static void TryName(Function f, Expr key, bool computed)
        {
            if (f.Id != null || computed)
                return;
            String name = key switch
            {
                Identifier i => i.Name,
                StringLiteral s => s.Value,
                _ => null,
            };
            if (name == null || !IsValidName(name) || References(f, name))
                return;
            f.Id = B.Id(name);
        }

        /// <summary>
        /// Walks a function body (not nested non-arrow functions) and replaces nodes
        /// </summary>
        public sealed class LexicalRewriter : Rewriter
        {
            readonly Func<Node, Node> Replace;
            readonly bool IntoArrows;

            public LexicalRewriter(Func<Node, Node> replace, bool intoArrows = true)
            {
                Replace = replace;
                IntoArrows = intoArrows;
            }

            protected override Node Visit(Node n)
            {
                if (n is Function f && (!f.IsArrow || !IntoArrows))
                    return n;
                if (n is Class c)
                {
                    //  Only the extends expression and computed keys are in the outer scope
                    c.SuperClass = Expr(c.SuperClass);
                    foreach (var m in c.Members)
                    {
                        if (m is MethodDef { Computed: true } md)
                            md.Key = Expr(md.Key);
                        else if (m is FieldDef { Computed: true } fd)
                            fd.Key = Expr(fd.Key);
                    }
                    return n;
                }
                var r = Replace(n);
                if (r != null)
                    return r;
                //  Don't visit non computed property names
                if (n is MemberExpr m2 && !m2.Computed)
                {
                    m2.Object = Expr(m2.Object);
                    return n;
                }
                if (n is Property p && !p.Computed)
                {
                    p.Value = Expr(p.Value);
                    return n;
                }
                return base.Visit(n);
            }
        }

        /// <summary>
        /// Insert statements at the start of a function body (after directives)
        /// </summary>
        public static void Prepend(Function f, IEnumerable<Stmt> stmts)
        {
            var body = f.EnsureBlockBody().Body;
            body.InsertRange(Pass.DirectiveCount(body), stmts);
        }
    }

    /// <summary>
    /// ES2015: template literals
    /// </summary>
    sealed class TemplateLiteralsPass : Pass
    {
        public TemplateLiteralsPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case TaggedTemplate tt:
                    {
                        tt.Tag = Expr(tt.Tag);
                        MapList(tt.Quasi.Expressions);
                        Ctx.Did(JsFeature.TemplateLiterals);
                        var cooked = new ArrayExpr(tt.Quasi.Quasis.Select(q => q.Cooked != null ? (Expr)B.Str(q.Cooked) : B.Void0()));
                        bool same = tt.Quasi.Quasis.All(q => q.Cooked == q.Raw);
                        var args = new List<Expr> { cooked };
                        if (!same)
                            args.Add(new ArrayExpr(tt.Quasi.Quasis.Select(q => (Expr)B.Str(q.Raw))));
                        var obj = GlobalTemp("templateObject");
                        var cached = B.Bin("||", B.Id(obj.Name), B.Assign(B.Id(obj.Name), B.CallList(Helper("taggedTemplateLiteral"), args)));
                        var callArgs = new List<Expr> { cached };
                        callArgs.AddRange(tt.Quasi.Expressions);
                        return new CallExpr(tt.Tag, callArgs).At<Expr>(tt);
                    }
                case TemplateLiteral t:
                    {
                        Children(t);
                        Ctx.Did(JsFeature.TemplateLiterals);
                        return Lower(t).At<Expr>(t);
                    }
            }
            return Children(n);
        }

        Expr Lower(TemplateLiteral t)
        {
            var q = t.Quasis;
            var e = t.Expressions;
            if (e.Count == 0)
                return B.Str(q[0].Cooked ?? "");
            if (Ctx.Loose)
            {
                //  "a" + b + "c"
                Expr r = B.Str(q[0].Cooked);
                for (int i = 0; i < e.Count; ++i)
                {
                    r = B.Bin("+", r, e[i]);
                    if (q[i + 1].Cooked.Length > 0)
                        r = B.Bin("+", r, B.Str(q[i + 1].Cooked));
                }
                return r;
            }
            //  "a".concat(b, "c").concat(d) (ToString is called in order)
            Expr cur = B.Str(q[0].Cooked);
            for (int i = 0; i < e.Count; ++i)
            {
                var args = new List<Expr> { e[i] };
                if (q[i + 1].Cooked.Length > 0)
                    args.Add(B.Str(q[i + 1].Cooked));
                cur = new CallExpr(B.Member(cur, "concat"), args);
            }
            return cur;
        }
    }

    /// <summary>
    /// ES2015: new.target
    /// </summary>
    sealed class NewTargetPass : Pass
    {
        public NewTargetPass(TransformContext ctx) : base(ctx)
        {
        }

        /// <summary>The closest non-arrow function and how new.target is computed in it</summary>
        readonly List<(Function F, bool IsCtor, bool IsMethod)> Stack = new List<(Function, bool, bool)>();

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case MethodDef md:
                    {
                        md.Key = Expr(md.Key);
                        Stack.Add((md.Value, md.Kind == "constructor", true));
                        md.Value = (Function)Rewrite(md.Value);
                        Stack.RemoveAt(Stack.Count - 1);
                        return md;
                    }
                case Function f when !f.IsArrow && (Stack.Count == 0 || Stack[^1].F != f):
                    {
                        Stack.Add((f, false, f.IsMethod));
                        Children(f);
                        Stack.RemoveAt(Stack.Count - 1);
                        return f;
                    }
                case MetaProperty { Meta: "new", Prop: "target" } mp:
                    {
                        Ctx.Did(JsFeature.NewTarget);
                        if (Stack.Count == 0)
                            return B.Void0();
                        var top = Stack[^1];
                        if (top.IsCtor)
                            return B.Member(B.This(), "constructor").At<Expr>(mp);
                        if (top.IsMethod)
                            return B.Void0();
                        var f = top.F;
                        if (f.Id == null)
                            f.Id = B.Id(Ctx.UniqueName("target"));
                        //  this instanceof F ? this.constructor : void 0
                        return B.Cond(B.Bin("instanceof", B.This(), B.Id(f.Id.Name)), B.Member(B.This(), "constructor"), B.Void0()).At<Expr>(mp);
                    }
            }
            return Children(n);
        }
    }

    /// <summary>
    /// ES2015: arrow functions
    /// </summary>
    sealed class ArrowFunctionsPass : Pass
    {
        public ArrowFunctionsPass(TransformContext ctx) : base(ctx)
        {
        }

        sealed class Capture
        {
            public Node Owner;
            public String This;
            public String Arguments;
        }

        readonly List<Capture> Captures = new List<Capture>();
        int ArrowDepth;

        String ThisName(Capture c) => c.This ??= Ctx.UniqueName("this");
        String ArgsName(Capture c) => c.Arguments ??= Ctx.UniqueName("arguments");

        public override void Run(ProgramNode p)
        {
            Captures.Add(new Capture { Owner = p });
            Rewrite(p);
            var c = Captures[0];
            Captures.Clear();
            var decls = Decls(c);
            if (decls != null)
                p.Body.InsertRange(DirectiveCount(p.Body), decls);
        }

        static List<Stmt> Decls(Capture c)
        {
            if (c.This == null && c.Arguments == null)
                return null;
            var v = new VarDecl("var");
            if (c.This != null)
                v.Decls.Add(new VarDeclarator(B.Id(c.This), B.This()));
            if (c.Arguments != null)
                v.Decls.Add(new VarDeclarator(B.Id(c.Arguments), B.Id("arguments")));
            return new List<Stmt> { v };
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case Function f when !f.IsArrow:
                    {
                        var c = new Capture { Owner = f };
                        Captures.Add(c);
                        var depth = ArrowDepth;
                        ArrowDepth = 0;
                        Children(f);
                        ArrowDepth = depth;
                        Captures.RemoveAt(Captures.Count - 1);
                        var decls = Decls(c);
                        if (decls != null)
                            Es2015.Prepend(f, decls);
                        return f;
                    }
                case Function f:
                    {
                        ++ArrowDepth;
                        Children(f);
                        --ArrowDepth;
                        Ctx.Did(JsFeature.ArrowFunctions);
                        f.EnsureBlockBody();
                        f.IsArrow = false;
                        return f;
                    }
                case ThisExpr when ArrowDepth > 0:
                    return B.Id(ThisName(Captures[^1])).At<Expr>(n);
                case Identifier { Name: "arguments" } when ArrowDepth > 0:
                    return B.Id(ArgsName(Captures[^1])).At<Expr>(n);
                case MemberExpr { Computed: false } m:
                    m.Object = Expr(m.Object);
                    return m;
                case Property { Computed: false } p:
                    p.Value = Expr(p.Value);
                    return p;
                case MethodDef { Computed: false } md:
                    md.Value = (Function)Rewrite(md.Value);
                    return md;
                case FieldDef fd:
                    //  Field initializers have their own this
                    {
                        if (fd.Computed)
                            fd.Key = Expr(fd.Key);
                        var depth = ArrowDepth;
                        ArrowDepth = 0;
                        var c = new Capture { Owner = fd };
                        Captures.Add(c);
                        fd.Value = Expr(fd.Value);
                        Captures.RemoveAt(Captures.Count - 1);
                        ArrowDepth = depth;
                        if (c.This != null || c.Arguments != null)
                            Error(fd, "Arrow functions using this in class field initializers can't be transformed without transforming class fields");
                        return fd;
                    }
            }
            return Children(n);
        }
    }

    /// <summary>
    /// ES2015: shorthand properties, methods, computed property names and super in object literals
    /// </summary>
    sealed class ObjectLiteralsPass : Pass
    {
        public ObjectLiteralsPass(TransformContext ctx) : base(ctx)
        {
        }

        static bool UsesSuper(Function f) => Walker.Any(f.Body, n => n is SuperExpr, n => n is not Function fn || fn.IsArrow);

        protected override Node Transform(Node n)
        {
            if (n is not ObjectExpr o)
                return Children(n);
            Children(o);
            bool doShort = Ctx.Needs(JsFeature.ShorthandProperties);
            bool doComputed = Ctx.Needs(JsFeature.ComputedProperties);
            //  super in methods: needs the object as home object
            Identifier home = null;
            foreach (var x in o.Properties)
            {
                if (x is Property { Value: FunctionExpr fe } p && (p.Method || p.Kind != PropKind.Init) && UsesSuper(fe.F))
                {
                    home ??= Temp("obj");
                    Ctx.Did(JsFeature.ShorthandProperties);
                    new Es2015.LexicalRewriter(nn => SuperRewrite(nn, home.Name)).Rewrite(fe.F.Body);
                }
            }
            if (doShort)
            {
                foreach (var x in o.Properties)
                {
                    if (x is not Property p)
                        continue;
                    if (p.Shorthand)
                    {
                        p.Shorthand = false;
                        Ctx.Did(JsFeature.ShorthandProperties);
                    }
                    if (p.Method && p.Value is FunctionExpr fe)
                    {
                        p.Method = false;
                        fe.F.IsMethod = false;
                        Es2015.TryName(fe.F, p.Key, p.Computed);
                        Ctx.Did(JsFeature.ShorthandProperties);
                    }
                }
            }
            //  Duplicate keys (not allowed in ES5 strict mode): make the later ones computed
            if (doComputed)
            {
                var seen = new HashSet<String>(StringComparer.Ordinal);
                foreach (var x in o.Properties)
                {
                    if (x is not Property { Computed: false } p || p.Kind != PropKind.Init)
                        continue;
                    var k = p.Key switch
                    {
                        Identifier i => i.Name,
                        StringLiteral s => s.Value,
                        NumberLiteral nl => JsWriter.Number(nl.Value),
                        _ => null,
                    };
                    if (k == null || k == "__proto__")
                        continue;
                    if (!seen.Add(k))
                    {
                        p.Key = B.Str(k);
                        p.Computed = true;
                    }
                }
            }
            int first = doComputed ? o.Properties.FindIndex(x => x is Property { Computed: true }) : -1;
            if (first < 0)
            {
                if (home != null)
                    return B.Assign(home, o).At<Expr>(o);
                return o;
            }
            Ctx.Did(JsFeature.ComputedProperties);
            //  (_obj = { before }, _defineProperty(_obj, k, v), ..., _obj)
            var obj = home ?? Temp("obj");
            var lit = new ObjectExpr();
            lit.Properties.AddRange(o.Properties.Take(first));
            var seq = new List<Expr> { B.Assign(B.Id(obj.Name), lit) };
            for (int i = first; i < o.Properties.Count; ++i)
            {
                var x = o.Properties[i];
                if (x is SpreadElement se)
                {
                    seq.Add(B.Call(B.Member(B.Id("Object"), "assign"), B.Id(obj.Name), se.Arg));
                    continue;
                }
                var p = (Property)x;
                var key = B.KeyExpr(p.Key, p.Computed);
                if (p.Kind == PropKind.Init && p.Key is Identifier { Name: "__proto__" } && !p.Computed)
                {
                    seq.Add(B.Call(B.Member(B.Id("Object"), "setPrototypeOf"), B.Id(obj.Name), p.Value));
                    continue;
                }
                if (p.Kind == PropKind.Get || p.Kind == PropKind.Set)
                {
                    seq.Add(B.Call(Helper("defineAccessor"), B.Str(p.Kind == PropKind.Get ? "get" : "set"), B.Id(obj.Name), key, p.Value));
                    continue;
                }
                if (p.Value is FunctionExpr fe2 && p.Method)
                {
                    fe2.F.IsMethod = false;
                    Es2015.TryName(fe2.F, p.Key, p.Computed);
                }
                if (Ctx.Loose)
                    seq.Add(B.Assign(B.Index(B.Id(obj.Name), key), p.Value));
                else
                    seq.Add(B.Call(Helper("defineProperty"), B.Id(obj.Name), key, p.Value));
            }
            seq.Add(B.Id(obj.Name));
            return B.Seq(seq).At<Expr>(o);
        }

        Node SuperRewrite(Node n, String home)
        {
            static Expr Key(MemberExpr m) => m.Computed ? m.Property : B.Str(((Identifier)m.Property).Name);
            switch (n)
            {
                case CallExpr { Callee: MemberExpr { Object: SuperExpr } sm } call:
                    return B.Call(B.Call(Helper("superPropGet"), B.Id(home), Key(sm), B.This(), B.Num(2)), new ArrayExpr(call.Args)).At<Expr>(call);
                case AssignExpr { Op: "=", Target: MemberExpr { Object: SuperExpr } am } a:
                    return B.Call(Helper("superPropSet"), B.Id(home), Key(am), a.Value, B.This(), B.Num(4)).At<Expr>(a);
                case MemberExpr { Object: SuperExpr } m:
                    return B.Call(Helper("superPropGet"), B.Id(home), Key(m), B.This(), B.Num(0)).At<Expr>(m);
            }
            return null;
        }
    }

    /// <summary>
    /// ES2015: spread in calls, new and array literals
    /// </summary>
    sealed class SpreadPass : Pass
    {
        public SpreadPass(TransformContext ctx) : base(ctx)
        {
        }

        static bool HasSpread(List<Expr> l) => l.Exists(x => x is SpreadElement);

        /// <summary>
        /// Build an array expression from a list with spread elements: [a].concat(_toConsumableArray(b), [c])
        /// </summary>
        Expr ToArray(List<Expr> items)
        {
            var parts = new List<Expr>();
            ArrayExpr cur = null;
            foreach (var x in items)
            {
                if (x is SpreadElement se)
                {
                    cur = null;
                    if (Ctx.Loose || se.Arg is Identifier { Name: "arguments" })
                    {
                        parts.Add(se.Arg is Identifier { Name: "arguments" }
                            ? B.Call(B.Member(B.Member(B.Member(B.Id("Array"), "prototype"), "slice"), "call"), se.Arg)
                            : se.Arg);
                    }
                    else
                        parts.Add(B.Call(Helper("toConsumableArray"), se.Arg));
                    continue;
                }
                if (cur == null)
                {
                    cur = new ArrayExpr();
                    parts.Add(cur);
                }
                cur.Elements.Add(x);
            }
            if (parts.Count == 1)
            {
                if (Ctx.Loose && parts[0] is not ArrayExpr)
                    return B.Call(B.Member(new ArrayExpr(), "concat"), parts[0]);
                return parts[0];
            }
            var first = parts[0];
            if (first is not ArrayExpr)
            {
                if (Ctx.Loose)
                    first = B.Call(B.Member(new ArrayExpr(), "concat"), first);
            }
            return new CallExpr(B.Member(first, "concat"), parts.Skip(1).ToList());
        }

        protected override Node Transform(Node n)
        {
            Children(n);
            switch (n)
            {
                case ArrayExpr a when HasSpread(a.Elements):
                    Ctx.Did(JsFeature.Spread);
                    return ToArray(a.Elements).At<Expr>(a);
                case NewExpr ne when HasSpread(ne.Args):
                    Ctx.Did(JsFeature.Spread);
                    return B.Call(Helper("construct"), ne.Callee, ToArray(ne.Args)).At<Expr>(ne);
                case CallExpr c when HasSpread(c.Args):
                    {
                        Ctx.Did(JsFeature.Spread);
                        Expr args = c.Args.Count == 1 && c.Args[0] is SpreadElement { Arg: Identifier { Name: "arguments" } } only ? only.Arg : ToArray(c.Args);
                        Expr callee = c.Callee, thisArg;
                        if (callee is MemberExpr m)
                        {
                            if (m.Object is SuperExpr)
                                thisArg = B.This();
                            else if (B.IsSimple(m.Object))
                                thisArg = m.Object.Clone<Expr>();
                            else
                            {
                                var t = Temp(B.NameHint(m.Object));
                                m.Object = B.Assign(t, m.Object);
                                thisArg = B.Id(t.Name);
                            }
                        }
                        else
                            thisArg = B.Void0();
                        return B.Call(B.Member(callee, "apply"), thisArg, args).At<Expr>(c);
                    }
            }
            return n;
        }
    }

    /// <summary>
    /// ES2015: for ... of
    /// </summary>
    sealed class ForOfPass : Pass
    {
        public ForOfPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case LabeledStmt { Body: ForOfStmt { Await: false } lf } l:
                    Children(lf);
                    return Lower(lf, l.Label);
                case ForOfStmt { Await: false } f:
                    Children(f);
                    return Lower(f, null);
            }
            return Children(n);
        }

        static Stmt LoopVar(ForOfStmt f, Expr value)
        {
            if (f.Left is VarDecl vd)
                return new VarDecl(vd.Kind, vd.Decls[0].Id, value).At<Stmt>(vd);
            return B.Stmt(B.Assign((Expr)f.Left, value));
        }

        static BlockStmt Body(ForOfStmt f, Stmt first)
        {
            var b = new BlockStmt { Body = { first } };
            //  Keep the body as a nested block if it declares lexical names (could conflict with the loop variable)
            if (f.Body is BlockStmt bb && !bb.Body.Exists(s => s is VarDecl { Kind: not "var" } || s is ClassDecl || s is FunctionDecl))
                b.Body.AddRange(bb.Body);
            else
                b.Body.Add(f.Body);
            return b;
        }

        Stmt Lower(ForOfStmt f, String label)
        {
            Ctx.Did(JsFeature.ForOf);
            if (Ctx.Loose)
            {
                //  for (var _i = 0, _arr = right; _i < _arr.length; _i++) { var x = _arr[_i]; ... }
                var i = Temp("i");
                var arr = Temp("arr");
                var loop = new ForStmt
                {
                    Init = B.Seq(B.Assign(i, B.Num(0)), B.Assign(arr, f.Right)),
                    Test = B.Bin("<", B.Id(i.Name), B.Member(B.Id(arr.Name), "length")),
                    Update = new UpdateExpr("++", false, B.Id(i.Name)),
                    Body = Body(f, LoopVar(f, B.Index(B.Id(arr.Name), B.Id(i.Name)))),
                }.At<ForStmt>(f);
                loop.LeadingComments = f.LeadingComments;
                return label != null ? new LabeledStmt(label, loop) : loop;
            }
            var it = Temp("iterator");
            var step = Temp("step");
            var err = B.Id(Ctx.UniqueName("err"));
            var forLoop = new ForStmt
            {
                Init = B.Call(B.Member(B.Id(it.Name), "s")),
                Test = B.Not(B.Member(B.Assign(B.Id(step.Name), B.Call(B.Member(B.Id(it.Name), "n"))), "done")),
                Body = Body(f, LoopVar(f, B.Member(B.Id(step.Name), "value"))),
            }.At<ForStmt>(f);
            var r = new BlockStmt
            {
                Body =
                {
                    B.Stmt(B.Assign(it, B.Call(Helper("createForOfIteratorHelper"), f.Right))),
                    new TryStmt
                    {
                        Block = new BlockStmt { Body = { label != null ? new LabeledStmt(label, forLoop) : forLoop } },
                        Handler = new CatchClause { Param = err, Body = new BlockStmt { Body = { B.Stmt(B.Call(B.Member(B.Id(it.Name), "e"), B.Id(err.Name))) } } },
                        Finalizer = new BlockStmt { Body = { B.Stmt(B.Call(B.Member(B.Id(it.Name), "f"))) } },
                    },
                },
            };
            r.LeadingComments = f.LeadingComments;
            return r;
        }
    }
}
