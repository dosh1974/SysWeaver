using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2015: classes (Babel compatible output using _createClass, _inherits, _callSuper)
    /// </summary>
    sealed class ClassesPass : Pass
    {
        public ClassesPass(TransformContext ctx) : base(ctx)
        {
        }

        static readonly HashSet<String> NativeClasses = new HashSet<String>(StringComparer.Ordinal)
        {
            "Error", "EvalError", "RangeError", "ReferenceError", "SyntaxError", "TypeError", "URIError", "AggregateError",
            "Array", "Map", "Set", "WeakMap", "WeakSet", "Promise", "RegExp", "Date", "String", "Number", "Boolean", "Function",
            "EventTarget", "Event", "CustomEvent", "Node", "Element", "HTMLElement", "DocumentFragment", "Image", "Audio",
        };

        static bool IsNative(Expr e) => e is Identifier i && (NativeClasses.Contains(i.Name) || (i.Name.StartsWith("HTML", StringComparison.Ordinal) && i.Name.EndsWith("Element", StringComparison.Ordinal)));

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case ClassDecl cd:
                    {
                        Children(cd);
                        var name = cd.C.Id.Name;
                        var v = new VarDecl("let", B.Id(name), Lower(cd.C)).At<Stmt>(cd);
                        v.LeadingComments = cd.LeadingComments;
                        return v;
                    }
                case ExportDefaultDecl { Declaration: ClassDecl cd } ed:
                    {
                        Children(cd);
                        if (cd.C.Id == null)
                        {
                            ed.Declaration = B.Stmt(Lower(cd.C));
                            return ed;
                        }
                        var name = cd.C.Id.Name;
                        var v = new VarDecl("let", B.Id(name), Lower(cd.C)).At<Stmt>(cd);
                        v.LeadingComments = ed.LeadingComments;
                        return new StatementList { Body = { v, new ExportDefaultDecl { Declaration = B.Stmt(B.Id(name)) } } };
                    }
                case VarDeclarator { Id: Identifier vid, Init: ClassExpr vce } vd when vce.C.Id == null && Es2015.IsValidName(vid.Name)
                    && !Walker.Any(vce.C.SuperClass, x => x is Identifier i && i.Name == vid.Name):
                    vce.C.Id = B.Id(vid.Name);
                    vd.Init = Expr(vce);
                    return vd;
                case ClassExpr ce:
                    Children(ce);
                    return Lower(ce.C).At<Expr>(ce);
            }
            return Children(n);
        }

        /// <summary>
        /// Lower a class to an immediately invoked function
        /// </summary>
        Expr Lower(Class c)
        {
            Ctx.Did(JsFeature.Classes);
            var name = c.Id?.Name ?? Ctx.UniqueName("class");
            bool derived = c.SuperClass != null;
            String superParam = derived ? Ctx.UniqueName(c.SuperClass is Identifier si ? si.Name : "super") : null;
            MethodDef ctor = null;
            var methods = new List<MethodDef>();
            foreach (var m in c.Members)
            {
                switch (m)
                {
                    case MethodDef md when md.Kind == "constructor":
                        ctor = md;
                        break;
                    case MethodDef md:
                        methods.Add(md);
                        break;
                    default:
                        Error(m, "Class fields and static blocks must be transformed when classes are transformed (don't exclude ClassFields / ClassStaticBlock)");
                        break;
                }
            }
            //  The constructor
            var cf = ctor?.Value ?? new Function();
            cf.IsMethod = false;
            cf.Id = B.Id(name);
            var body = cf.EnsureBlockBody();
            var check = B.Stmt(B.Call(Helper("classCallCheck"), B.This(), B.Id(name)));
            if (!derived)
            {
                ReplaceSuper(cf, name, 1);
                body.Body.Insert(DirectiveCount(body.Body), check);
            }
            else if (ctor == null)
            {
                //  function C() { _classCallCheck(this, C); return _callSuper(this, C, arguments); }
                body.Body.Add(check);
                body.Body.Add(new ReturnStmt(B.Call(Helper("callSuper"), B.This(), B.Id(name), B.Id("arguments"))));
            }
            else
                DerivedConstructor(cf, name, check);
            //  Methods
            var proto = new List<Expr>();
            var stat = new List<Expr>();
            var looseStmts = new List<Stmt>();
            var accessors = new Dictionary<String, ObjectExpr>(StringComparer.Ordinal);
            foreach (var md in methods)
            {
                var f = md.Value;
                ReplaceSuper(f, name, md.Static ? 0 : 1);
                f.IsMethod = false;
                Es2015.TryName(f, md.Key, md.Computed);
                var target = md.Static ? stat : proto;
                Expr key = B.KeyExpr(md.Key, md.Computed);
                if (md.Kind == "method" && Ctx.Loose)
                {
                    //  C.prototype.m = function m() {}
                    Expr owner = md.Static ? B.Id(name) : B.Member(B.Id(name), "prototype");
                    Expr member = !md.Computed && md.Key is Identifier ki ? B.Member(owner, ki.Name) : B.Index(owner, key);
                    looseStmts.Add(B.Stmt(B.Assign(member, new FunctionExpr(f))).At<Stmt>(md));
                    continue;
                }
                if (md.Kind == "get" || md.Kind == "set")
                {
                    //  Merge getters and setters with the same (non computed) key
                    String k = !md.Computed ? (md.Static ? "s:" : "p:") + (key as StringLiteral)?.Value : null;
                    if (k != null && md.Key is NumberLiteral)
                        k = null;
                    if (k != null && accessors.TryGetValue(k, out var existing))
                    {
                        existing.Properties.Add(new Property(B.Id(md.Kind), new FunctionExpr(f)));
                        continue;
                    }
                    var d = new ObjectExpr { Properties = { new Property(B.Id("key"), key), new Property(B.Id(md.Kind), new FunctionExpr(f)) } };
                    if (k != null)
                        accessors[k] = d;
                    target.Add(d);
                    continue;
                }
                target.Add(new ObjectExpr { Properties = { new Property(B.Id("key"), key), new Property(B.Id("value"), new FunctionExpr(f)) } });
            }
            //  The function body: "use strict"; function C() {...} _inherits(C, _super); return _createClass(C, [...], [...]);
            var fb = new List<Stmt>
            {
                new ExprStmt(B.Str("use strict")) { Directive = "\"use strict\"" },
                new FunctionDecl(cf).At<Stmt>(ctor ?? (Node)c),
            };
            if (derived)
                fb.Add(B.Stmt(B.Call(Helper("inherits"), B.Id(name), B.Id(superParam))));
            fb.AddRange(looseStmts);
            if (Ctx.Loose && proto.Count == 0 && stat.Count == 0)
                fb.Add(new ReturnStmt(B.Id(name)));
            else
            {
                var args = new List<Expr> { B.Id(name) };
                if (proto.Count > 0 || stat.Count > 0)
                    args.Add(proto.Count > 0 ? new ArrayExpr(proto) : B.Null());
                if (stat.Count > 0)
                    args.Add(new ArrayExpr(stat));
                fb.Add(new ReturnStmt(B.CallList(Helper("createClass"), args)));
            }
            var outer = new Function { Body = new BlockStmt(fb) };
            if (derived)
                outer.Params.Add(B.Id(superParam));
            var call = new CallExpr(new FunctionExpr(outer));
            if (derived)
                call.Args.Add(IsNative(c.SuperClass) ? B.Call(Helper("wrapNativeSuper"), c.SuperClass) : c.SuperClass);
            return call;
        }

        /// <summary>
        /// A derived class constructor: super() => _this = _callSuper(this, C, [args]), this => _this
        /// </summary>
        void DerivedConstructor(Function cf, String name, Stmt check)
        {
            var thisVar = Ctx.UniqueName("this");
            //  super.x first (uses this, replaced below)
            ReplaceSuper(cf, name, 1);
            var body = cf.Body;
            new DerivedRewriter(this, name, thisVar).MapStatements(body.Body);
            var dc = DirectiveCount(body.Body);
            body.Body.Insert(dc, check);
            body.Body.Insert(dc, new VarDecl("var", B.Id(thisVar)));
            if (body.Body.Count == 0 || body.Body[^1] is not ReturnStmt)
                body.Body.Add(new ReturnStmt(B.Call(Helper("assertThisInitialized"), B.Id(thisVar))));
        }

        /// <summary>
        /// Rewrites a derived constructor body (and its arrow functions)
        /// </summary>
        sealed class DerivedRewriter : Rewriter
        {
            readonly ClassesPass P;
            readonly String Name;
            readonly String ThisVar;
            int ArrowDepth;

            public DerivedRewriter(ClassesPass p, String name, String thisVar)
            {
                P = p;
                Name = name;
                ThisVar = thisVar;
            }

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function f:
                        if (!f.IsArrow)
                            return n;
                        ++ArrowDepth;
                        base.Visit(n);
                        --ArrowDepth;
                        return n;
                    case Class c:
                        c.SuperClass = Expr(c.SuperClass);
                        foreach (var m in c.Members)
                            if (m is MethodDef { Computed: true } md)
                                md.Key = Expr(md.Key);
                        return n;
                    case CallExpr { Callee: SuperExpr } sc:
                        {
                            MapList(sc.Args);
                            Expr args = sc.Args.Count == 1 && sc.Args[0] is SpreadElement { Arg: Identifier { Name: "arguments" } } only
                                ? only.Arg
                                : new ArrayExpr(sc.Args);
                            return B.Assign(B.Id(ThisVar), B.Call(P.Helper("callSuper"), B.This(), B.Id(Name), args)).At<Expr>(sc);
                        }
                    case ThisExpr:
                        return B.Id(ThisVar).At<Expr>(n);
                    case ReturnStmt r when ArrowDepth == 0:
                        {
                            if (r.Arg == null)
                                return new ReturnStmt(B.Call(P.Helper("assertThisInitialized"), B.Id(ThisVar))).At<Stmt>(r);
                            var arg = Expr(r.Arg);
                            return new ReturnStmt(B.Call(P.Helper("possibleConstructorReturn"), B.Id(ThisVar), arg)).At<Stmt>(r);
                        }
                    case MemberExpr { Computed: false } m:
                        m.Object = Expr(m.Object);
                        return m;
                    case Property { Computed: false } p:
                        p.Value = Expr(p.Value);
                        return p;
                }
                return base.Visit(n);
            }
        }

        /// <summary>
        /// Replace super.x in a method (and its arrow functions)
        /// </summary>
        void ReplaceSuper(Function f, String name, int flags)
        {
            var rw = new SuperRewriter(this, name, flags);
            if (f.Body != null)
                f.Body = rw.Block(f.Body);
            else
                f.ExprBody = rw.Expr(f.ExprBody);
        }

        sealed class SuperRewriter : Rewriter
        {
            readonly ClassesPass P;
            readonly String Name;
            readonly int Flags;

            public SuperRewriter(ClassesPass p, String name, int flags)
            {
                P = p;
                Name = name;
                Flags = flags;
            }

            Expr Key(MemberExpr m) => m.Computed ? Expr(m.Property) : B.Str(((Identifier)m.Property).Name);

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function f when !f.IsArrow:
                        return n;
                    case Class c:
                        c.SuperClass = Expr(c.SuperClass);
                        foreach (var m in c.Members)
                            if (m is MethodDef { Computed: true } md)
                                md.Key = Expr(md.Key);
                        return n;
                    case CallExpr { Callee: MemberExpr { Object: SuperExpr } sm } call:
                        {
                            var key = Key(sm);
                            MapList(call.Args);
                            var get = B.Call(P.Helper("superPropGet"), B.Id(Name), key, B.This(), B.Num(Flags | 2));
                            return B.Call(get, new ArrayExpr(call.Args)).At<Expr>(call);
                        }
                    case AssignExpr { Target: MemberExpr { Object: SuperExpr } am } a:
                        {
                            var key = Key(am);
                            var v = Expr(a.Value);
                            if (a.Op == "=")
                                return B.Call(P.Helper("superPropSet"), B.Id(Name), key, v, B.This(), B.Num(Flags | 4)).At<Expr>(a);
                            Expr k1 = key, k2 = key;
                            if (!B.IsConstant(key))
                            {
                                var t = P.Temp("key");
                                k1 = B.Assign(t, key);
                                k2 = B.Id(t.Name);
                            }
                            var op = a.Op.Substring(0, a.Op.Length - 1);
                            var cur = B.Call(P.Helper("superPropGet"), B.Id(Name), k1, B.This(), B.Num(Flags));
                            return B.Call(P.Helper("superPropSet"), B.Id(Name), k2.Clone<Expr>(), B.Bin(op, cur, v), B.This(), B.Num(Flags | 4)).At<Expr>(a);
                        }
                    case UpdateExpr { Arg: MemberExpr { Object: SuperExpr } um } u:
                        {
                            var key = Key(um);
                            Expr k1 = key, k2 = key;
                            if (!B.IsConstant(key))
                            {
                                var t = P.Temp("key");
                                k1 = B.Assign(t, key);
                                k2 = B.Id(t.Name);
                            }
                            var old = P.Temp("old");
                            var get = B.Assign(old, B.Call(B.Id("Number"), B.Call(P.Helper("superPropGet"), B.Id(Name), k1, B.This(), B.Num(Flags))));
                            var set = B.Call(P.Helper("superPropSet"), B.Id(Name), k2.Clone<Expr>(), B.Bin(u.Op == "++" ? "+" : "-", B.Id(old.Name), B.Num(1)), B.This(), B.Num(Flags | 4));
                            return u.Prefix ? B.Seq(get, set) : B.Seq(get, set, B.Id(old.Name));
                        }
                    case MemberExpr { Object: SuperExpr } m:
                        return B.Call(P.Helper("superPropGet"), B.Id(Name), Key(m), B.This(), B.Num(Flags)).At<Expr>(m);
                    case MemberExpr { Computed: false } m2:
                        m2.Object = Expr(m2.Object);
                        return m2;
                    case Property { Computed: false } p:
                        p.Value = Expr(p.Value);
                        return p;
                }
                return base.Visit(n);
            }
        }
    }
}
