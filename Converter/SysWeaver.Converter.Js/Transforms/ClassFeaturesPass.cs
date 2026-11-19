using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2022: class fields (public / private, instance / static), private methods and accessors, static blocks and private brand checks (#x in obj)
    /// </summary>
    sealed class ClassFeaturesPass : Pass
    {
        public ClassFeaturesPass(TransformContext ctx) : base(ctx)
        {
        }

        enum PrivKind
        {
            Field,
            Method,
            Accessor,
        }

        sealed class PrivInfo
        {
            public String Name;
            public PrivKind Kind;
            public bool IsStatic;
            /// <summary>Field: the WeakMap (instance) or the box (static), Method: the function</summary>
            public String Storage;
            public String Getter;
            public String Setter;
            /// <summary>The brand WeakSet (instance methods / accessors)</summary>
            public String Brand;
            /// <summary>The class reference (static elements)</summary>
            public String ClassRef;
        }

        /// <summary>
        /// The private names of the classes being transformed (innermost last)
        /// </summary>
        readonly List<Dictionary<String, PrivInfo>> PrivEnv = new List<Dictionary<String, PrivInfo>>();

        /// <summary>
        /// Classes that are not transformed (their private names shadow outer transformed names)
        /// </summary>
        PrivInfo Lookup(String name)
        {
            for (int i = PrivEnv.Count - 1; i >= 0; --i)
            {
                if (PrivEnv[i].TryGetValue(name, out var p))
                    return p;
            }
            return null;
        }

        #region Decision

        static bool HasPrivateIn(Class c, HashSet<String> names)
        {
            return Walker.Any(c, n => n is BinaryExpr { Op: "in", Left: PrivateName pn } && names.Contains(pn.Name));
        }

        bool ShouldLower(Class c)
        {
            if (Ctx.Needs(JsFeature.ClassFields))
            {
                if (c.Members.Exists(m => m is FieldDef || m is StaticBlock || (m is MethodDef md && md.Key is PrivateName)))
                    return true;
            }
            bool hasPrivMethods = c.Members.Exists(m => m is MethodDef md && md.Key is PrivateName);
            if (hasPrivMethods && Ctx.Needs(JsFeature.PrivateMethods))
                return true;
            if (Ctx.Needs(JsFeature.PrivateIn))
            {
                var names = new HashSet<String>(StringComparer.Ordinal);
                foreach (var m in c.Members)
                {
                    if (m is MethodDef { Key: PrivateName p1 })
                        names.Add(p1.Name);
                    else if (m is FieldDef { Key: PrivateName p2 })
                        names.Add(p2.Name);
                }
                if (names.Count > 0 && HasPrivateIn(c, names))
                    return true;
            }
            if (Ctx.Needs(JsFeature.ClassStaticBlock) && c.Members.Exists(m => m is StaticBlock))
            {
                //  Static blocks will be lowered alone (into a private static field), unless class fields are lowered
                return Ctx.Needs(JsFeature.ClassFields);
            }
            return false;
        }

        #endregion//Decision

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case ClassDecl cd:
                    return ClassDeclaration(cd, null);
                case ExportNamedDecl { Declaration: ClassDecl cd } en:
                    {
                        var r = ClassDeclaration(cd, en);
                        return r;
                    }
                case ExportDefaultDecl { Declaration: ClassDecl cd } ed:
                    {
                        if (cd.C.Id == null && ShouldLower(cd.C))
                            cd.C.Id = B.Id(Ctx.UniqueName("default"));
                        return ClassDeclaration(cd, ed);
                    }
                case ClassExpr ce:
                    return ClassExpression(ce, null);
                case VarDeclarator { Id: Identifier vid, Init: ClassExpr vce } vd when vce.C.Id == null:
                    vd.Init = (Expr)ClassExpression(vce, vid.Name);
                    vd.Id = (Expr)Rewrite(vd.Id);
                    return vd;
                case AssignExpr { Op: "=", Target: Identifier aid, Value: ClassExpr ace } ae when ace.C.Id == null:
                    ae.Value = (Expr)ClassExpression(ace, aid.Name);
                    return ae;
                case ChainExpr ch when Walker.Any(ch, x => x is MemberExpr { Property: PrivateName pn } && Lookup(pn.Name) != null, x => x is not Function):
                    {
                        //  Lower the optional chain so that the private member accesses can be transformed
                        var lowered = OptionalChain.Lower(ch.Expression, Temp, Ctx.Loose, false);
                        return Rewrite(lowered);
                    }
                case BinaryExpr { Op: "in", Left: PrivateName pn } b:
                    {
                        var p = Lookup(pn.Name);
                        if (p == null)
                            break;
                        Ctx.Did(JsFeature.PrivateIn);
                        var obj = B.Call(Helper("checkInRHS"), Expr(b.Right));
                        if (p.IsStatic)
                            return B.Bin("===", obj, B.Id(p.ClassRef)).At<Expr>(b);
                        return B.Call(B.Member(B.Id(p.Kind == PrivKind.Field ? p.Storage : p.Brand), "has"), obj).At<Expr>(b);
                    }
                case CallExpr { Callee: MemberExpr { Property: PrivateName pn } cm } call:
                    {
                        var p = Lookup(pn.Name);
                        if (p == null)
                            break;
                        //  obj.#m(args) => method.call(obj, args)
                        var objE = Expr(cm.Object);
                        Expr thisArg;
                        if (B.IsSimple(objE))
                            thisArg = objE.Clone<Expr>();
                        else
                        {
                            var t = Temp(B.NameHint(objE));
                            objE = B.Assign(t, objE);
                            thisArg = B.Id(t.Name);
                        }
                        var fn = PrivateRead(p, objE, cm);
                        MapList(call.Args);
                        var args = new List<Expr> { thisArg };
                        args.AddRange(call.Args);
                        return new CallExpr(B.Member(fn, "call"), args).At<Expr>(call);
                    }
                case AssignExpr { Target: MemberExpr { Property: PrivateName pn } am } a when a.Op == "=":
                    {
                        var p = Lookup(pn.Name);
                        if (p == null)
                            break;
                        if (p.Kind == PrivKind.Accessor)
                        {
                            var objE = Expr(am.Object);
                            var v = Expr(a.Value);
                            return B.Call(Helper("classPrivateSetter"), B.Id(p.IsStatic ? p.ClassRef : p.Brand), objE, p.Setter != null ? B.Id(p.Setter) : B.Void0(), v).At<Expr>(a);
                        }
                        if (p.Kind == PrivKind.Method)
                        {
                            var objE = Expr(am.Object);
                            var v = Expr(a.Value);
                            return B.Seq(objE, v, B.Call(Helper("readOnlyPrivate"), B.Str(p.Name)));
                        }
                        break;
                    }
                case MemberExpr { Property: PrivateName pn } m:
                    {
                        var p = Lookup(pn.Name);
                        if (p == null)
                            break;
                        var objE = Expr(m.Object);
                        return PrivateTarget(p, objE, m);
                    }
            }
            return Children(n);
        }

        /// <summary>
        /// The value of obj.#name (read only)
        /// </summary>
        Expr PrivateRead(PrivInfo p, Expr obj, Node at)
        {
            switch (p.Kind)
            {
                case PrivKind.Method:
                    return B.Call(Helper("assertClassBrand"), B.Id(p.IsStatic ? p.ClassRef : p.Brand), obj, B.Id(p.Storage)).At<Expr>(at);
                case PrivKind.Accessor:
                    return B.Call(Helper("classPrivateGetter"), B.Id(p.IsStatic ? p.ClassRef : p.Brand), obj, p.Getter != null ? B.Id(p.Getter) : B.Void0()).At<Expr>(at);
                default:
                    return PrivateTarget(p, obj, at);
            }
        }

        /// <summary>
        /// A member expression that can be read and written (used for all private member expressions that are not calls)
        /// </summary>
        Expr PrivateTarget(PrivInfo p, Expr obj, Node at)
        {
            switch (p.Kind)
            {
                case PrivKind.Field:
                    if (p.IsStatic)
                        return B.Member(B.Call(Helper("assertClassBrand"), B.Id(p.ClassRef), obj, B.Id(p.Storage)), "_").At<Expr>(at);
                    return B.Member(B.Call(Helper("classPrivateFieldBox"), B.Id(p.Storage), obj), "_").At<Expr>(at);
                case PrivKind.Accessor:
                    return B.Member(B.Call(Helper("privateAccessorBox"), B.Id(p.IsStatic ? p.ClassRef : p.Brand), obj,
                        p.Getter != null ? B.Id(p.Getter) : B.Void0(),
                        p.Setter != null ? B.Id(p.Setter) : B.Void0(),
                        B.Str(p.Name)), "_").At<Expr>(at);
                default:
                    //  Methods can only be read (writes are handled by the assignment case, other writes throw at runtime)
                    return PrivateRead(p, obj, at);
            }
        }

        #region Class transformation

        sealed class Lowered
        {
            /// <summary>Statements before the class (storage declarations, private method functions)</summary>
            public readonly List<Stmt> Before = new List<Stmt>();
            /// <summary>Statements after the class (static initialization)</summary>
            public readonly List<Stmt> After = new List<Stmt>();
            public String ClassRef;
        }

        Node ClassDeclaration(ClassDecl cd, Stmt export)
        {
            var c = cd.C;
            if (!ShouldLower(c))
            {
                if (Ctx.Needs(JsFeature.ClassStaticBlock) && c.Members.Exists(m => m is StaticBlock))
                    StaticBlocksToFields(c);
                PushShadow(c);
                Children(cd);
                PrivEnv.RemoveAt(PrivEnv.Count - 1);
                return export ?? (Stmt)cd;
            }
            var l = Lower(c, c.Id.Name, false);
            var list = new StatementList();
            list.Body.AddRange(l.Before);
            if (export != null)
            {
                list.Body.Add(export);
                if (export.LeadingComments != null)
                {
                    list.LeadingComments = export.LeadingComments;
                    export.LeadingComments = null;
                }
            }
            else
            {
                list.Body.Add(cd);
                if (cd.LeadingComments != null)
                {
                    list.LeadingComments = cd.LeadingComments;
                    cd.LeadingComments = null;
                }
            }
            list.Body.AddRange(l.After);
            return list;
        }

        /// <summary>
        /// True if a node contains yield or await outside of functions (can't be moved into an arrow function)
        /// </summary>
        static bool HasYieldOrAwait(Node n) => Walker.Any(n, x => x is YieldExpr || x is AwaitExpr || x is ForOfStmt { Await: true }, x => x is not Function);

        Node ClassExpression(ClassExpr ce, String inferredName)
        {
            var c = ce.C;
            if (!ShouldLower(c))
            {
                if (Ctx.Needs(JsFeature.ClassStaticBlock) && c.Members.Exists(m => m is StaticBlock))
                    StaticBlocksToFields(c);
                PushShadow(c);
                var r = Children(ce);
                PrivEnv.RemoveAt(PrivEnv.Count - 1);
                return r;
            }
            //  Name the class (inferred name or a generated name), the inferred name can't be used if the extends expression or a computed key references it
            if (c.Id == null)
            {
                bool selfRef = inferredName != null && (
                    Walker.Any(c.SuperClass, x => x is Identifier i && i.Name == inferredName)
                    || c.Members.Exists(m => (m is MethodDef { Computed: true } md && Walker.Any(md.Key, x => x is Identifier i && i.Name == inferredName))
                        || (m is FieldDef { Computed: true } fd && Walker.Any(fd.Key, x => x is Identifier i && i.Name == inferredName))));
                c.Id = B.Id(inferredName != null && !selfRef ? inferredName : Ctx.UniqueName("class"));
            }
            //  The extends expression and computed keys are evaluated in the arrow function, that doesn't work with yield / await
            bool canWrap = !HasYieldOrAwait(c.SuperClass) && !c.Members.Exists(m => (m is MethodDef { Computed: true } md && HasYieldOrAwait(md.Key)) || (m is FieldDef { Computed: true } fd && HasYieldOrAwait(fd.Key)));
            if (!canWrap)
            {
                Error(ce, "Can't transform a class expression with yield or await in the extends expression or a computed key");
                return ce;
            }
            var l = Lower(c, c.Id.Name, true);
            //  (() => { before; class C {...}; after; return C; })()
            var body = new List<Stmt>();
            body.AddRange(l.Before);
            body.Add(new ClassDecl(c).At<Stmt>(ce));
            body.AddRange(l.After);
            body.Add(new ReturnStmt(B.Id(c.Id.Name)));
            var arrow = new Function { IsArrow = true, Body = new BlockStmt(body) };
            return B.Call(new FunctionExpr(arrow)).At<Expr>(ce);
        }

        /// <summary>
        /// Push the private names of a class that isn't lowered (they shadow names of outer classes)
        /// </summary>
        void PushShadow(Class c)
        {
            var env = new Dictionary<String, PrivInfo>(StringComparer.Ordinal);
            foreach (var m in c.Members)
            {
                if (m is MethodDef { Key: PrivateName p1 })
                    env[p1.Name] = null;
                else if (m is FieldDef { Key: PrivateName p2 })
                    env[p2.Name] = null;
            }
            PrivEnv.Add(env);
        }

        /// <summary>
        /// Convert static blocks to private static fields: static #_ = (() => { ... })();
        /// </summary>
        void StaticBlocksToFields(Class c)
        {
            for (int i = 0; i < c.Members.Count; ++i)
            {
                if (c.Members[i] is not StaticBlock sb)
                    continue;
                Ctx.Did(JsFeature.ClassStaticBlock);
                var arrow = new Function { IsArrow = true, Body = new BlockStmt(sb.Body) };
                c.Members[i] = new FieldDef
                {
                    Static = true,
                    Key = new PrivateName(Ctx.UniqueName("staticBlock").Substring(1)),
                    Value = B.Call(new FunctionExpr(arrow)),
                    LeadingComments = sb.LeadingComments,
                }.At<FieldDef>(sb);
            }
        }

        /// <summary>
        /// Lower a class (the class node is modified)
        /// </summary>
        Lowered Lower(Class c, String classRef, bool isExpr)
        {
            var l = new Lowered { ClassRef = classRef };
            var env = new Dictionary<String, PrivInfo>(StringComparer.Ordinal);
            bool loose = Ctx.Loose;
            String brand = null;
            //  Declare the private names
            foreach (var m in c.Members)
            {
                switch (m)
                {
                    case FieldDef { Key: PrivateName pn } fd:
                        {
                            var p = new PrivInfo { Name = pn.Name, Kind = PrivKind.Field, IsStatic = fd.Static, ClassRef = classRef, Storage = Ctx.UniqueName(pn.Name) };
                            env[pn.Name] = p;
                            Expr init = fd.Static ? new ObjectExpr { Properties = { new Property(B.Id("_"), B.Void0()) } } : new NewExpr(B.Id("WeakMap"));
                            l.Before.Add(new VarDecl("const", B.Id(p.Storage), init));
                            break;
                        }
                    case MethodDef { Key: PrivateName pn } md:
                        {
                            if (!env.TryGetValue(pn.Name, out var p))
                            {
                                p = new PrivInfo { Name = pn.Name, IsStatic = md.Static, ClassRef = classRef };
                                env[pn.Name] = p;
                            }
                            if (md.Kind == "get" || md.Kind == "set")
                            {
                                p.Kind = PrivKind.Accessor;
                                var fn = Ctx.UniqueName((md.Kind == "get" ? "get_" : "set_") + pn.Name);
                                if (md.Kind == "get")
                                    p.Getter = fn;
                                else
                                    p.Setter = fn;
                            }
                            else
                            {
                                p.Kind = PrivKind.Method;
                                p.Storage = Ctx.UniqueName(pn.Name);
                            }
                            if (!md.Static)
                            {
                                if (brand == null)
                                {
                                    brand = Ctx.UniqueName(classRef + "_brand");
                                    l.Before.Add(new VarDecl("const", B.Id(brand), new NewExpr(B.Id("WeakSet"))));
                                }
                                p.Brand = brand;
                            }
                            break;
                        }
                }
            }
            if (env.Count > 0)
            {
                if (env.Values.Any(x => x.Kind == PrivKind.Field))
                    Ctx.Did(JsFeature.ClassFields);
                if (env.Values.Any(x => x.Kind != PrivKind.Field))
                    Ctx.Did(JsFeature.PrivateMethods);
            }
            PrivEnv.Add(env);
            try
            {
                //  Transform the super class and the members (private accesses inside are resolved with the env)
                c.SuperClass = Expr(c.SuperClass);
                var instanceInits = new List<Expr>();
                var staticInits = new List<Stmt>();
                if (brand != null)
                    instanceInits.Add(B.Call(Helper("classPrivateMethodInitSpec"), B.This(), B.Id(brand)));
                MethodDef ctor = null;
                var keep = new List<Node>();
                foreach (var m in c.Members)
                {
                    switch (m)
                    {
                        case MethodDef md when md.Key is PrivateName pn:
                            {
                                var p = env[pn.Name];
                                md.Value = (Function)Rewrite(md.Value);
                                var f = md.Value;
                                ReplaceSuper(f, classRef, md.Static ? 0 : 1, null);
                                var name = md.Kind == "get" ? p.Getter : md.Kind == "set" ? p.Setter : p.Storage;
                                f.Id = B.Id(name);
                                f.IsMethod = false;
                                l.Before.Add(new FunctionDecl(f).At<Stmt>(md));
                                break;
                            }
                        case MethodDef md:
                            if (md.Computed)
                                md.Key = Expr(md.Key);
                            md.Value = (Function)Rewrite(md.Value);
                            if (md.Kind == "constructor")
                                ctor = md;
                            keep.Add(md);
                            break;
                        case FieldDef fd:
                            {
                                Expr key = fd.Key;
                                if (fd.Computed)
                                {
                                    key = Expr(key);
                                    if (!B.IsConstant(key))
                                    {
                                        //  Computed keys are evaluated once, when the class is defined
                                        var t = Ctx.UniqueName("key");
                                        l.Before.Add(new VarDecl("const", B.Id(t), B.Call(Helper("toPropertyKey"), key)));
                                        key = B.Id(t);
                                    }
                                }
                                var value = fd.Value != null ? Expr(fd.Value) : null;
                                if (fd.Static)
                                {
                                    if (value != null)
                                    {
                                        value = (Expr)ReplaceThis(value, classRef);
                                        value = (Expr)ReplaceSuper(value, classRef, 0, classRef);
                                    }
                                    staticInits.Add(B.Stmt(FieldInit(fd, key, value ?? B.Void0(), B.Id(classRef), env, loose)).At<Stmt>(fd));
                                }
                                else
                                    instanceInits.Add(FieldInit(fd, key, value ?? B.Void0(), B.This(), env, loose));
                                break;
                            }
                        case StaticBlock sb:
                            {
                                Ctx.Did(JsFeature.ClassStaticBlock);
                                var f = new Function { Body = new BlockStmt(sb.Body) };
                                f = (Function)Rewrite(f);
                                ReplaceSuper(f, classRef, 0, null);
                                var call = B.Call(B.Member(new FunctionExpr(f), "call"), B.Id(classRef));
                                var st = B.Stmt(call);
                                st.LeadingComments = sb.LeadingComments;
                                staticInits.Add(st.At<Stmt>(sb));
                                break;
                            }
                        default:
                            keep.Add(m);
                            break;
                    }
                }
                c.Members = keep;
                if (instanceInits.Count > 0)
                    AddInstanceInits(c, ref ctor, instanceInits);
                l.After.AddRange(staticInits);
            }
            finally
            {
                PrivEnv.RemoveAt(PrivEnv.Count - 1);
            }
            return l;
        }

        /// <summary>
        /// The initialization of a field
        /// </summary>
        Expr FieldInit(FieldDef fd, Expr key, Expr value, Expr target, Dictionary<String, PrivInfo> env, bool loose)
        {
            if (fd.Key is PrivateName pn)
            {
                var p = env[pn.Name];
                if (fd.Static)
                    return B.Assign(B.Member(B.Id(p.Storage), "_"), value);
                return B.Call(Helper("classPrivateFieldInitSpec"), target, B.Id(p.Storage), value);
            }
            Ctx.Did(JsFeature.ClassFields);
            if (loose)
            {
                if (!fd.Computed && key is Identifier ki)
                    return B.Assign(B.Member(target, ki.Name), value);
                return B.Assign(B.Index(target, B.KeyExpr(key, fd.Computed)), value);
            }
            return B.Call(Helper("defineProperty"), target, B.KeyExpr(key, fd.Computed), value);
        }

        /// <summary>
        /// Add the instance initializers to the constructor (creates one if needed)
        /// </summary>
        void AddInstanceInits(Class c, ref MethodDef ctor, List<Expr> inits)
        {
            bool derived = c.SuperClass != null;
            if (ctor == null)
            {
                var f = new Function { IsMethod = true, Body = new BlockStmt() };
                if (derived)
                {
                    var args = B.Id(Ctx.UniqueName("args"));
                    f.Params.Add(new RestElement(args));
                    f.Body.Body.Add(B.Stmt(new CallExpr(new SuperExpr(), new SpreadElement(B.Id(args.Name)))));
                }
                ctor = new MethodDef { Kind = "constructor", Key = B.Id("constructor"), Value = f };
                c.Members.Insert(0, ctor);
            }
            var cf = ctor.Value;
            //  Rename constructor bindings that would shadow names used by the initializers
            var used = new HashSet<String>(StringComparer.Ordinal);
            foreach (var e in inits)
                Walker.Walk(e, n =>
                {
                    if (n is Identifier i)
                        used.Add(i.Name);
                    return true;
                });
            var sa = ScopeAnalyzer.Analyze(cf);
            var fs = sa.Get(cf);
            void RenameIn(Scope s)
            {
                foreach (var b in s.Bindings.Values.ToList())
                    if (used.Contains(b.Name))
                        b.Rename(Ctx.UniqueName(b.Name));
                foreach (var ch in s.Children)
                    if (ch.Kind != ScopeKind.Function && ch.Kind != ScopeKind.StaticBlock && ch.Kind != ScopeKind.Class && ch.Kind != ScopeKind.FunctionName)
                        RenameIn(ch);
            }
            if (fs != null)
                RenameIn(fs);
            if (!derived)
            {
                var stmts = inits.Select(x => (Stmt)B.Stmt(x)).ToList();
                cf.Body.Body.InsertRange(DirectiveCount(cf.Body.Body), stmts);
                return;
            }
            //  Derived: after every super() call
            int count = 0;
            new SuperCallRewriter(this, inits, () => ++count).Rewrite(cf.Body);
            if (count == 0)
                Warn(ctor, "A derived class constructor without a super() call, the field initializers are not added");
        }

        /// <summary>
        /// Adds the initializers after super() calls (not in nested non-arrow functions)
        /// </summary>
        sealed class SuperCallRewriter : Rewriter
        {
            readonly ClassFeaturesPass Owner;
            readonly List<Expr> Inits;
            readonly Action Count;
            int Uses;

            public SuperCallRewriter(ClassFeaturesPass owner, List<Expr> inits, Action count)
            {
                Owner = owner;
                Inits = inits;
                Count = count;
            }

            IEnumerable<Expr> Copy() => Uses++ == 0 ? Inits : Inits.Select(x => x.Clone<Expr>());

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function f when !f.IsArrow:
                        return n;
                    case Class c:
                        //  Only the extends expression and computed keys are in the constructor scope
                        c.SuperClass = Expr(c.SuperClass);
                        return n;
                    case ExprStmt { Expression: CallExpr { Callee: SuperExpr } sc } es:
                        {
                            sc.MapChildren(this);
                            Count();
                            var l = new StatementList();
                            l.Body.Add(es);
                            foreach (var x in Copy())
                                l.Body.Add(B.Stmt(x));
                            return l;
                        }
                    case CallExpr { Callee: SuperExpr } call:
                        {
                            call.MapChildren(this);
                            Count();
                            var s = new List<Expr> { call };
                            s.AddRange(Copy());
                            s.Add(B.This());
                            return B.Seq(s);
                        }
                }
                return base.Visit(n);
            }
        }

        #endregion//Class transformation

        #region this / super replacement

        /// <summary>
        /// Replace this with the class reference (static initializers), not inside non-arrow functions or nested class bodies
        /// </summary>
        static Node ReplaceThis(Node n, String classRef)
        {
            return new ThisReplacer(classRef).Rewrite(n);
        }

        sealed class ThisReplacer : Rewriter
        {
            readonly String Ref;
            public ThisReplacer(String r) => Ref = r;

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case ThisExpr:
                        return B.Id(Ref).At<Expr>(n);
                    case Function f when !f.IsArrow:
                        return n;
                    case Class c:
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
                return base.Visit(n);
            }
        }

        /// <summary>
        /// Replace super property accesses with helper calls (for code moved out of the class)
        /// </summary>
        /// <param name="n">The node (function or expression)</param>
        /// <param name="classRef">The class</param>
        /// <param name="flags">1 = instance (home object is the prototype), 0 = static</param>
        /// <param name="thisRef">The this value, null to use this</param>
        Node ReplaceSuper(Node n, String classRef, int flags, String thisRef)
        {
            return new SuperReplacer(this, classRef, flags, thisRef).Rewrite(n);
        }

        sealed class SuperReplacer : Rewriter
        {
            readonly ClassFeaturesPass P;
            readonly String Ref;
            readonly int Flags;
            readonly String ThisRef;
            int Depth;

            public SuperReplacer(ClassFeaturesPass p, String r, int flags, String thisRef)
            {
                P = p;
                Ref = r;
                Flags = flags;
                ThisRef = thisRef;
            }

            Expr This() => ThisRef != null ? B.Id(ThisRef) : B.This();

            Expr Key(MemberExpr m) => m.Computed ? Expr(m.Property) : B.Str(((Identifier)m.Property).Name);

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function f:
                        if (f.IsArrow || Depth == 0)
                        {
                            ++Depth;
                            base.Visit(n);
                            --Depth;
                        }
                        return n;
                    case Class c:
                        c.SuperClass = Expr(c.SuperClass);
                        return n;
                    case CallExpr { Callee: MemberExpr { Object: SuperExpr } sm } call:
                        {
                            MapList(call.Args);
                            var get = B.Call(P.Helper("superPropGet"), B.Id(Ref), Key(sm), This(), B.Num(Flags | 2));
                            return B.Call(get, new ArrayExpr(call.Args)).At<Expr>(call);
                        }
                    case AssignExpr { Target: MemberExpr { Object: SuperExpr } am } a:
                        {
                            var key = Key(am);
                            var v = Expr(a.Value);
                            if (a.Op != "=")
                            {
                                Expr k1 = key, k2 = key;
                                if (!B.IsConstant(key))
                                {
                                    var t = P.Temp("key");
                                    k1 = B.Assign(t, key);
                                    k2 = B.Id(t.Name);
                                }
                                var op = a.Op.Substring(0, a.Op.Length - 1);
                                var cur = B.Call(P.Helper("superPropGet"), B.Id(Ref), k1, This(), B.Num(Flags));
                                Expr nv = op switch
                                {
                                    "&&" or "||" or "??" => B.Bin(op, cur, v),
                                    _ => B.Bin(op, cur, v),
                                };
                                return B.Call(P.Helper("superPropSet"), B.Id(Ref), k2.Clone<Expr>(), nv, This(), B.Num(Flags | 4)).At<Expr>(a);
                            }
                            return B.Call(P.Helper("superPropSet"), B.Id(Ref), key, v, This(), B.Num(Flags | 4)).At<Expr>(a);
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
                            var get = B.Assign(old, B.Call(B.Id("Number"), B.Call(P.Helper("superPropGet"), B.Id(Ref), k1, This(), B.Num(Flags))));
                            var nv = B.Bin(u.Op == "++" ? "+" : "-", B.Id(old.Name), B.Num(1));
                            var set = B.Call(P.Helper("superPropSet"), B.Id(Ref), k2.Clone<Expr>(), nv, This(), B.Num(Flags | 4));
                            return u.Prefix ? B.Seq(get, set) : B.Seq(get, set, B.Id(old.Name));
                        }
                    case MemberExpr { Object: SuperExpr } m:
                        return B.Call(P.Helper("superPropGet"), B.Id(Ref), Key(m), This(), B.Num(Flags)).At<Expr>(m);
                }
                return base.Visit(n);
            }
        }

        #endregion//this / super replacement
    }
}
