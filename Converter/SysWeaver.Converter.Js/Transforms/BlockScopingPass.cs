using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2015: let, const and block scoped function declarations.
    /// Bindings are renamed where converting them to var would change the meaning, loops with closures that capture
    /// per-iteration bindings are converted to a _loop function (like Babel).
    /// </summary>
    sealed class BlockScopingPass : Pass
    {
        public BlockScopingPass(TransformContext ctx) : base(ctx)
        {
        }

        sealed class LoopInfo
        {
            /// <summary>Bindings declared in the loop head (let / const)</summary>
            public List<Binding> Head = new List<Binding>();
        }

        /// <summary>Loops that must be wrapped in a _loop function</summary>
        readonly Dictionary<Stmt, LoopInfo> Wrap = new Dictionary<Stmt, LoopInfo>();
        /// <summary>Declarations that were var in the source (they are hoisted out of _loop functions)</summary>
        readonly HashSet<VarDecl> OriginalVars = new HashSet<VarDecl>();
        /// <summary>Declarations in for-in / for-of heads (can't have an initializer)</summary>
        readonly HashSet<VarDecl> LoopHeads = new HashSet<VarDecl>();
        /// <summary>Identifiers that are writes to const bindings</summary>
        readonly HashSet<Identifier> ConstWrites = new HashSet<Identifier>();
        /// <summary>Nesting depth of loops in the current function (let without init in loops get = void 0)</summary>
        int LoopDepth;

        public override void Run(ProgramNode p)
        {
            HoistBlockFunctions(p);
            var sa = ScopeAnalyzer.Analyze(p);
            Walker.Walk(p, n =>
            {
                switch (n)
                {
                    case VarDecl { Kind: "var" } v:
                        OriginalVars.Add(v);
                        break;
                    case ForInStmt { Left: VarDecl hv }:
                        LoopHeads.Add(hv);
                        break;
                    case ForOfStmt { Left: VarDecl hv2 }:
                        LoopHeads.Add(hv2);
                        break;
                }
                return true;
            });
            RenameConflicts(sa);
            FindLoops(sa);
            foreach (var r in sa.References)
                if (r.IsWrite && r.Binding != null && r.Binding.Kind == BindingKind.Const && !r.Binding.Declarations.Contains(r.Id))
                    ConstWrites.Add(r.Id);
            Rewrite(p);
        }

        #region Block functions

        /// <summary>
        /// Convert function declarations in blocks to let (strict) / var (sloppy) declarations at the start of the block
        /// </summary>
        void HoistBlockFunctions(ProgramNode p)
        {
            bool programStrict = p.IsModule || IsStrict(p.Body);
            void Process(List<Stmt> list, bool strict)
            {
                var fns = new List<Stmt>();
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i] is FunctionDecl fd && fd.F.Id != null)
                    {
                        Ctx.Did(JsFeature.BlockScoping);
                        var name = fd.F.Id.Name;
                        var v = new VarDecl(strict ? "let" : "var", B.Id(name), new FunctionExpr(fd.F)).At<VarDecl>(fd);
                        v.LeadingComments = fd.LeadingComments;
                        fns.Add(v);
                        list.RemoveAt(i);
                        --i;
                    }
                }
                list.InsertRange(0, fns);
            }
            void Walk(Node n, bool strict, bool isFunctionBody)
            {
                switch (n)
                {
                    case null:
                        return;
                    case Function f:
                        {
                            var s = strict || (f.Body != null && IsStrict(f.Body.Body));
                            foreach (var x in f.Params)
                                Walk(x, s, false);
                            if (f.Body != null)
                                Walk(f.Body, s, true);
                            else
                                Walk(f.ExprBody, s, false);
                            return;
                        }
                    case Class c:
                        Walker.Walk(c, x =>
                        {
                            if (x is Function fx)
                            {
                                Walk(fx, true, false);
                                return false;
                            }
                            return true;
                        });
                        return;
                    case BlockStmt b:
                        if (!isFunctionBody)
                            Process(b.Body, strict);
                        foreach (var s in b.Body)
                            Walk(s, strict, false);
                        return;
                    case SwitchCase sc:
                        Process(sc.Body, strict);
                        Walk(sc.Test, strict, false);
                        foreach (var s in sc.Body)
                            Walk(s, strict, false);
                        return;
                    case IfStmt i when i.Then is FunctionDecl || i.Else is FunctionDecl:
                        if (i.Then is FunctionDecl)
                            i.Then = new BlockStmt { Body = { i.Then } };
                        if (i.Else is FunctionDecl)
                            i.Else = new BlockStmt { Body = { i.Else } };
                        break;
                    case LabeledStmt l when l.Body is FunctionDecl:
                        l.Body = new BlockStmt { Body = { l.Body } };
                        break;
                }
                var w = new Walker(x =>
                {
                    Walk(x, strict, false);
                    return false;
                });
                n.MapChildren(w);
            }
            foreach (var s in p.Body)
                Walk(s, programStrict, false);
        }

        static bool IsStrict(List<Stmt> body)
        {
            foreach (var s in body)
            {
                if (s is not ExprStmt { Directive: not null } d)
                    return false;
                if (d.Directive == "\"use strict\"" || d.Directive == "'use strict'")
                    return true;
            }
            return false;
        }

        #endregion//Block functions

        #region Analysis

        static bool IsBlockScoped(Binding b) => b.Kind == BindingKind.Let || b.Kind == BindingKind.Const || b.Kind == BindingKind.Class;

        static bool IsAncestor(Scope anc, Scope s)
        {
            for (var x = s; x != null; x = x.Parent)
                if (x == anc)
                    return true;
            return false;
        }

        /// <summary>
        /// Rename block scoped bindings that would conflict with other bindings / references after being converted to var
        /// </summary>
        void RenameConflicts(ScopeAnalyzer sa)
        {
            var refsByName = sa.References.GroupBy(r => r.Id.Name).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            var all = new List<Scope>();
            void Collect(Scope s)
            {
                all.Add(s);
                foreach (var c in s.Children)
                    Collect(c);
            }
            Collect(sa.Root);
            //  Bindings by var scope and name
            var byVarScope = new Dictionary<(Scope, String), List<Binding>>();
            foreach (var s in all)
            {
                var vs = s.VarScope;
                foreach (var b in s.Bindings.Values)
                {
                    if (b.Kind == BindingKind.FunctionName || b.Kind == BindingKind.ClassInner)
                        continue;
                    var k = (vs, b.Name);
                    if (!byVarScope.TryGetValue(k, out var l))
                        byVarScope[k] = l = new List<Binding>();
                    l.Add(b);
                }
            }
            foreach (var s in all)
            {
                if (s.IsVarScope)
                    continue;
                var vs = s.VarScope;
                foreach (var b in s.Bindings.Values.ToList())
                {
                    if (!IsBlockScoped(b))
                        continue;
                    bool conflict = byVarScope.TryGetValue((vs, b.Name), out var same) && same.Count > 1;
                    if (!conflict && refsByName.TryGetValue(b.Name, out var refs))
                    {
                        foreach (var r in refs)
                        {
                            if (r.Binding == b || !IsAncestor(vs, r.Scope))
                                continue;
                            //  A reference inside the var scope that resolves to something else
                            if (r.Binding == null || r.Binding.Scope.VarScope == vs || IsAncestor(r.Binding.Scope, vs))
                            {
                                conflict = true;
                                break;
                            }
                        }
                    }
                    //  The catch parameter with the same name in an enclosing catch
                    if (!conflict)
                    {
                        for (var x = s.Parent; x != null && x != vs; x = x.Parent)
                            if (x.Kind == ScopeKind.Catch && x.Bindings.ContainsKey(b.Name))
                                conflict = true;
                    }
                    if (conflict)
                    {
                        var old = b.Name;
                        if (byVarScope.TryGetValue((vs, old), out var l))
                            l.Remove(b);
                        b.Rename(Ctx.UniqueName(old));
                        Ctx.Did(JsFeature.BlockScoping);
                    }
                }
            }
        }

        /// <summary>
        /// Find loops with per-iteration bindings that are captured by closures
        /// </summary>
        void FindLoops(ScopeAnalyzer sa)
        {
            Walker.Walk(sa.Root.Node, n =>
            {
                switch (n)
                {
                    case ForStmt:
                    case ForInStmt:
                    case ForOfStmt:
                    case WhileStmt:
                    case DoWhileStmt:
                        Check(sa, (Stmt)n);
                        break;
                }
                return true;
            });
        }

        void Check(ScopeAnalyzer sa, Stmt loop)
        {
            //  All scopes of the loop (head scope and the body scopes, not nested functions)
            var scopes = new List<Scope>();
            var head = sa.Get(loop);
            void Add(Scope s)
            {
                scopes.Add(s);
                foreach (var c in s.Children)
                    if (!c.IsVarScope && c.Kind != ScopeKind.FunctionName)
                        Add(c);
            }
            if (head != null)
                Add(head);
            else
            {
                var body = loop is WhileStmt w ? w.Body : ((DoWhileStmt)loop).Body;
                Walker.Walk(body, x =>
                {
                    var s = sa.Get(x);
                    if (s != null && !s.IsVarScope && s.Kind != ScopeKind.FunctionName)
                    {
                        Add(s);
                        return false;
                    }
                    return x is not Function;
                });
            }
            bool captured = false;
            foreach (var s in scopes)
            {
                foreach (var b in s.Bindings.Values)
                {
                    if (!IsBlockScoped(b))
                        continue;
                    var vs = b.Scope.VarScope;
                    if (b.References.Exists(r => r.Scope.VarScope != vs))
                    {
                        captured = true;
                        break;
                    }
                }
                if (captured)
                    break;
            }
            if (!captured)
                return;
            //  yield / await in the body can't be moved into a function (await can't, yield uses a generator)
            var bodyStmt = loop switch
            {
                ForStmt f => f.Body,
                ForInStmt f => f.Body,
                ForOfStmt f => f.Body,
                WhileStmt w => w.Body,
                DoWhileStmt d => d.Body,
                _ => null,
            };
            if (Walker.Any(bodyStmt, x => x is AwaitExpr || (x is ForOfStmt fo && fo.Await), x => x is not Function))
            {
                Warn(loop, "A loop with closures capturing let / const bindings contains await, the bindings are not per iteration");
                return;
            }
            var info = new LoopInfo();
            if (head != null)
                foreach (var b in head.Bindings.Values)
                    if (IsBlockScoped(b))
                        info.Head.Add(b);
            Wrap[loop] = info;
        }

        #endregion//Analysis

        #region Transform

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case Function f:
                    {
                        var d = LoopDepth;
                        LoopDepth = 0;
                        Children(f);
                        LoopDepth = d;
                        return f;
                    }
                case LabeledStmt l when IsLoop(l.Body):
                    {
                        ++LoopDepth;
                        Children(l.Body);
                        --LoopDepth;
                        if (Wrap.TryGetValue(l.Body, out var info))
                            return WrapLoop(l.Body, l.Label, info, l);
                        return l;
                    }
                case Stmt s when IsLoop(s):
                    {
                        ++LoopDepth;
                        Children(s);
                        --LoopDepth;
                        if (Wrap.TryGetValue(s, out var info))
                            return WrapLoop(s, null, info, null);
                        return s;
                    }
                case VarDecl v when v.Kind == "let" || v.Kind == "const":
                    {
                        Children(v);
                        Ctx.Did(JsFeature.BlockScoping);
                        if (v.Kind == "let" && LoopDepth > 0 && !LoopHeads.Contains(v))
                        {
                            foreach (var d in v.Decls)
                                if (d.Init == null && d.Id is Identifier)
                                    d.Init = B.Void0();
                        }
                        v.Kind = "var";
                        return v;
                    }
                case AssignExpr { Target: Identifier ti } a when ConstWrites.Contains(ti):
                    Children(a);
                    Warn(a, "Assignment to the constant \"" + ti.Name + "\" (throws a TypeError at runtime)");
                    return B.Seq(a.Value, B.Call(Helper("readOnlyError"), B.Str(ti.Name))).At<Expr>(a);
                case UpdateExpr { Arg: Identifier ui } u when ConstWrites.Contains(ui):
                    Warn(u, "Assignment to the constant \"" + ui.Name + "\" (throws a TypeError at runtime)");
                    return B.Call(Helper("readOnlyError"), B.Str(ui.Name)).At<Expr>(u);
                case ForInStmt { Left: VarDecl } fi:
                    {
                        //  for (let x in o) without init: var (the body is processed as a loop)
                        ++LoopDepth;
                        Children(fi);
                        --LoopDepth;
                        if (Wrap.TryGetValue(fi, out var info))
                            return WrapLoop(fi, null, info, null);
                        return fi;
                    }
            }
            return Children(n);
        }

        static bool IsLoop(Stmt s) => s is ForStmt || s is ForInStmt || s is ForOfStmt || s is WhileStmt || s is DoWhileStmt;

        /// <summary>
        /// Wrap a loop body in a _loop function
        /// </summary>
        Stmt WrapLoop(Stmt loop, String label, LoopInfo info, Stmt labeled)
        {
            Ctx.Did(JsFeature.BlockScoping);
            Stmt body = loop switch
            {
                ForStmt f => f.Body,
                ForInStmt f => f.Body,
                ForOfStmt f => f.Body,
                WhileStmt w => w.Body,
                DoWhileStmt d => d.Body,
                _ => throw new InvalidOperationException(),
            };
            var bodyIds = new HashSet<Identifier>();
            Walker.Walk(body, x =>
            {
                if (x is Identifier i)
                    bodyIds.Add(i);
                return true;
            });
            //  Head bindings used in the body are passed as parameters (with a write back if assigned in the body)
            var ps = new List<Expr>();
            var args = new List<Expr>();
            var writeBack = new List<Stmt>();
            foreach (var b in info.Head)
            {
                var refs = b.References.Where(r => bodyIds.Contains(r.Id)).ToList();
                if (refs.Count == 0)
                    continue;
                var pn = Ctx.UniqueName(b.Name);
                foreach (var rf in refs)
                    rf.Id.Name = pn;
                ps.Add(B.Id(pn));
                args.Add(B.Id(b.Name));
                if (refs.Exists(r => r.IsWrite))
                    writeBack.Add(B.Stmt(B.Assign(B.Id(b.Name), B.Id(pn))));
            }
            //  Rewrite the body: control flow, this / arguments, hoist original vars
            var flow = new FlowRewriter(this, label, writeBack);
            var fbody = new BlockStmt(B.StatementsOf(flow.Stmt(body)));
            fbody.Body.AddRange(writeBack.Select(x => x.Clone<Stmt>()));
            bool isGen = Walker.Any(fbody, x => x is YieldExpr, x => x is not Function);
            var fn = new Function { Params = ps, Body = fbody, IsGenerator = isGen };
            var loopName = Ctx.UniqueName("loop");
            var decl = B.Var(loopName, new FunctionExpr(fn));
            //  The call
            Expr call = new CallExpr(B.Id(loopName), args);
            if (isGen)
                call = new YieldExpr(call, true);
            var newBody = new BlockStmt();
            if (flow.Breaks || flow.Returns || flow.Jumps.Count > 0)
            {
                var ret = Ctx.UniqueName("ret");
                newBody.Body.Add(B.Var(ret, call));
                if (flow.Breaks)
                    newBody.Body.Add(new IfStmt(B.Bin("===", B.Id(ret), B.Str("break")), new BreakStmt(label)));
                foreach (var j in flow.Jumps)
                {
                    var isBreak = j.StartsWith("break|", StringComparison.Ordinal);
                    var lbl = j.Substring(j.IndexOf('|') + 1);
                    newBody.Body.Add(new IfStmt(B.Bin("===", B.Id(ret), B.Str(j)), isBreak ? new BreakStmt(lbl) : new ContinueStmt(lbl)));
                }
                if (flow.Returns)
                    newBody.Body.Add(new IfStmt(B.Bin("===", new UnaryExpr("typeof", B.Id(ret)), B.Str("object")), new ReturnStmt(B.Member(B.Id(ret), "v"))));
            }
            else
                newBody.Body.Add(B.Stmt(call));
            switch (loop)
            {
                case ForStmt f:
                    f.Body = newBody;
                    break;
                case ForInStmt f:
                    f.Body = newBody;
                    break;
                case ForOfStmt f:
                    f.Body = newBody;
                    break;
                case WhileStmt w:
                    w.Body = newBody;
                    break;
                case DoWhileStmt d:
                    d.Body = newBody;
                    break;
            }
            //  this / arguments in the body
            if (flow.ThisName != null || flow.ArgsName != null)
            {
                for (var s = Scope; s != null; s = s.Parent)
                {
                    if (s.Owner is Function || s.Owner is ProgramNode)
                    {
                        if (flow.ThisName != null)
                            s.Prologue.Add(B.Var(flow.ThisName, B.This()));
                        if (flow.ArgsName != null)
                            s.Prologue.Add(B.Var(flow.ArgsName, B.Id("arguments")));
                        break;
                    }
                }
            }
            //  Hoisted vars
            foreach (var h in flow.Hoisted)
                Scope.Temps.Add(h);
            var outer = labeled ?? loop;
            var r = new StatementList { Body = { decl, outer } };
            r.LeadingComments = outer.LeadingComments;
            outer.LeadingComments = null;
            return r;
        }

        /// <summary>
        /// Rewrites the body of a loop that is moved into a function
        /// </summary>
        sealed class FlowRewriter : Rewriter
        {
            readonly BlockScopingPass P;
            readonly String Label;
            readonly List<Stmt> WriteBack;
            public bool Breaks;
            public bool Returns;
            public readonly List<String> Jumps = new List<String>();
            public readonly List<String> Hoisted = new List<String>();
            public String ThisName;
            public String ArgsName;
            /// <summary>Nesting of loops / switches inside the body (unlabeled break / continue target them)</summary>
            int InnerLoops;
            int InnerSwitches;
            readonly List<String> InnerLabels = new List<String>();

            public FlowRewriter(BlockScopingPass p, String label, List<Stmt> writeBack)
            {
                P = p;
                Label = label;
                WriteBack = writeBack;
            }

            Stmt Exit(Expr value)
            {
                var r = new ReturnStmt(value);
                if (WriteBack.Count == 0)
                    return r;
                var b = new BlockStmt(WriteBack.Select(x => x.Clone<Stmt>()));
                b.Body.Add(r);
                return b;
            }

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function:
                        return n;
                    case Class c:
                        c.SuperClass = Expr(c.SuperClass);
                        return n;
                    case ThisExpr:
                        return B.Id(ThisName ??= P.Ctx.UniqueName("this"));
                    case Identifier { Name: "arguments" }:
                        return B.Id(ArgsName ??= P.Ctx.UniqueName("arguments"));
                    case MemberExpr { Computed: false } m:
                        m.Object = Expr(m.Object);
                        return m;
                    case Property { Computed: false } pr:
                        pr.Value = Expr(pr.Value);
                        return pr;
                    case LabeledStmt l:
                        {
                            InnerLabels.Add(l.Label);
                            var r = base.Visit(n);
                            InnerLabels.RemoveAt(InnerLabels.Count - 1);
                            return r;
                        }
                    case ForStmt or ForInStmt or ForOfStmt or WhileStmt or DoWhileStmt:
                        {
                            ++InnerLoops;
                            var r = base.Visit(n);
                            --InnerLoops;
                            return r;
                        }
                    case SwitchStmt:
                        {
                            ++InnerSwitches;
                            var r = base.Visit(n);
                            --InnerSwitches;
                            return r;
                        }
                    case BreakStmt b:
                        if (b.Label == null)
                        {
                            if (InnerLoops > 0 || InnerSwitches > 0)
                                return n;
                            Breaks = true;
                            return Exit(B.Str("break"));
                        }
                        if (InnerLabels.Contains(b.Label))
                            return n;
                        if (b.Label == Label)
                        {
                            Breaks = true;
                            return Exit(B.Str("break"));
                        }
                        {
                            var j = "break|" + b.Label;
                            if (!Jumps.Contains(j))
                                Jumps.Add(j);
                            return Exit(B.Str(j));
                        }
                    case ContinueStmt c:
                        if (c.Label == null)
                        {
                            if (InnerLoops > 0)
                                return n;
                            return Exit(null);
                        }
                        if (InnerLabels.Contains(c.Label))
                            return n;
                        if (c.Label == Label)
                            return Exit(null);
                        {
                            var j = "continue|" + c.Label;
                            if (!Jumps.Contains(j))
                                Jumps.Add(j);
                            return Exit(B.Str(j));
                        }
                    case ReturnStmt r:
                        {
                            Returns = true;
                            var v = Expr(r.Arg) ?? B.Void0();
                            return Exit(new ObjectExpr { Properties = { new Property(B.Id("v"), v) } });
                        }
                    case VarDecl v when v.Kind == "var" && P.OriginalVars.Contains(v):
                        {
                            //  Hoist: var a = 1, b; => a = 1;
                            MapList(v.Decls);
                            var assigns = new List<Expr>();
                            foreach (var d in v.Decls)
                            {
                                var names = new List<Identifier>();
                                ScopeAnalyzer.PatternNames(d.Id, names);
                                foreach (var x in names)
                                    if (!Hoisted.Contains(x.Name))
                                        Hoisted.Add(x.Name);
                                if (d.Init != null)
                                    assigns.Add(B.Assign(d.Id, d.Init));
                            }
                            if (assigns.Count == 0)
                                return null;
                            return B.Stmt(B.Seq(assigns));
                        }
                }
                return base.Visit(n);
            }

            /// <summary>
            /// for (var x ...) heads: the declaration is hoisted
            /// </summary>
            public override String ToString() => "FlowRewriter";
        }

        #endregion//Transform
    }
}
