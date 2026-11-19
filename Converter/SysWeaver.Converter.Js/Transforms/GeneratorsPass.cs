using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// ES2015: generator functions, lowered to a state machine (regenerator style) using the _regeneratorRuntime helper.
    /// </summary>
    sealed class GeneratorsPass : Pass
    {
        public GeneratorsPass(TransformContext ctx) : base(ctx)
        {
        }

        /// <summary>The mark statements to add at the start of the current statement list</summary>
        List<Stmt> PendingMarks = new List<Stmt>();

        /// <summary>The expression that references the outer (marked) function of a generator</summary>
        readonly Dictionary<Function, String> OuterRef = new Dictionary<Function, String>();

        Expr Runtime() => B.Call(Helper("regeneratorRuntime"));

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case ProgramNode p:
                    return WithMarks(n, p.Body);
                case BlockStmt b:
                    return WithMarks(n, b.Body);
                case SwitchCase sc:
                    return WithMarks(n, sc.Body);
                case StaticBlock sb:
                    return WithMarks(n, sb.Body);
                case FunctionDecl fd when fd.F.IsGenerator && fd.F.Id != null:
                    {
                        var m = Ctx.UniqueName("marked");
                        OuterRef[fd.F] = m;
                        Children(fd);
                        PendingMarks.Add(B.Var(m, B.Call(B.Member(Runtime(), "mark"), B.Id(fd.F.Id.Name))));
                        return fd;
                    }
                case FunctionExpr fe when fe.F.IsGenerator && !fe.F.IsMethod && fe.F.Id != null:
                    OuterRef[fe.F] = fe.F.Id.Name;
                    Children(fe);
                    return B.Call(B.Member(Runtime(), "mark"), fe).At<Expr>(fe);
                case Function f when f.IsGenerator:
                    Children(f);
                    if (f.IsAsync)
                    {
                        Error(f, "Async generator functions must be transformed before generators (enable AsyncGenerators)");
                        return f;
                    }
                    Ctx.Did(JsFeature.Generators);
                    OuterRef.TryGetValue(f, out var outer);
                    new GeneratorLowering(this, f, outer).Run();
                    return f;
            }
            return Children(n);
        }

        Node WithMarks(Node n, List<Stmt> body)
        {
            var saved = PendingMarks;
            PendingMarks = new List<Stmt>();
            Children(n);
            if (PendingMarks.Count > 0)
                body.InsertRange(DirectiveCount(body), PendingMarks);
            PendingMarks = saved;
            return n;
        }

        internal Identifier HelperId(String name) => Helper(name);
        internal TransformContext Context => Ctx;
        internal void ReportError(Node n, String msg) => Error(n, msg);
        internal void ReportWarning(Node n, String msg) => Warn(n, msg);
    }

    /// <summary>
    /// Lowers one generator function (in place)
    /// </summary>
    sealed class GeneratorLowering
    {
        readonly GeneratorsPass P;
        readonly TransformContext Ctx;
        readonly Function F;
        readonly String OuterRef;

        /// <summary>Hoisted variable names (declared in the outer function)</summary>
        readonly List<String> Vars = new List<String>();
        readonly HashSet<String> VarSet = new HashSet<String>(StringComparer.Ordinal);
        /// <summary>Hoisted function declarations</summary>
        readonly List<Stmt> Functions = new List<Stmt>();
        /// <summary>Temporaries that are only assigned once (safe to read after a yield)</summary>
        readonly HashSet<String> Temps = new HashSet<String>(StringComparer.Ordinal);
        String ArgsName;
        readonly String CtxName;

        public GeneratorLowering(GeneratorsPass p, Function f, String outerRef)
        {
            P = p;
            Ctx = p.Context;
            F = f;
            OuterRef = outerRef;
            CtxName = Ctx.UniqueName("context");
        }

        void AddVar(String name)
        {
            if (VarSet.Add(name))
                Vars.Add(name);
        }

        Identifier NewTemp(String hint = "t")
        {
            var n = Ctx.UniqueName(hint);
            AddVar(n);
            Temps.Add(n);
            return B.Id(n);
        }

        public void Run()
        {
            var body = F.Body;
            var directives = body.Body.Take(Pass.DirectiveCount(body.Body)).ToList();
            body.Body.RemoveRange(0, directives.Count);
            //  super can't be used in the inner function
            if (Walker.Any(body, x => x is SuperExpr, x => x is not Function || ((Function)x).IsArrow))
                P.ReportError(F, "super inside a generator method can't be transformed unless classes / object methods are transformed too");
            Prepare(body);
            //  Explode
            var em = new Emitter(this);
            em.Statements(body.Body);
            var inner = em.Finish(out var tryLocs);
            //  Assemble the outer function
            var outerBody = new List<Stmt>();
            outerBody.AddRange(directives);
            var decl = new VarDecl("var");
            foreach (var v in Vars)
                decl.Decls.Add(new VarDeclarator(B.Id(v), v == ArgsName ? B.Id("arguments") : null));
            if (decl.Decls.Count > 0)
                outerBody.Add(decl);
            outerBody.AddRange(Functions);
            var innerFn = new Function { Id = B.Id(Ctx.UniqueName((F.Id?.Name ?? "callee") + "$")), Body = inner };
            innerFn.Params.Add(B.Id(CtxName));
            var args = new List<Expr>
            {
                new FunctionExpr(innerFn),
                OuterRef != null ? B.Id(OuterRef) : B.Null(),
                B.This(),
            };
            if (tryLocs != null)
                args.Add(tryLocs);
            outerBody.Add(new ReturnStmt(B.CallList(B.Member(P_Runtime(), "wrap"), args)));
            F.Body = new BlockStmt(outerBody).At<BlockStmt>(body);
            F.IsGenerator = false;
        }

        Expr P_Runtime() => B.Call(P.HelperId("regeneratorRuntime"));

        #region Preparation (hoisting, renaming, arguments)

        void Prepare(BlockStmt body)
        {
            //  Rename block scoped bindings that would conflict when hoisted, find all bindings to hoist
            var sa = ScopeAnalyzer.Analyze(F);
            var fs = sa.Get(F);
            var counts = new Dictionary<String, int>(StringComparer.Ordinal);
            Walker.Walk(F, n =>
            {
                if (n is Identifier i)
                    counts[i.Name] = counts.TryGetValue(i.Name, out var c) ? c + 1 : 1;
                return true;
            });
            //  Catch params of try statements that will be exploded
            var explodedCatch = new HashSet<CatchClause>();
            Walker.Walk(body, n =>
            {
                if (n is Function)
                    return false;
                if (n is TryStmt t && t.Handler != null && LeapFinder.Contains(t))
                    explodedCatch.Add(t.Handler);
                return true;
            });
            void Collect(Scope s, bool top)
            {
                foreach (var b in s.Bindings.Values.ToList())
                {
                    if (b.Kind == BindingKind.Param || b.Kind == BindingKind.FunctionName || b.Kind == BindingKind.ClassInner || b.Kind == BindingKind.Import)
                        continue;
                    if (b.Kind == BindingKind.Catch && !(s.Node is CatchClause cc && explodedCatch.Contains(cc)))
                        continue;
                    if (!top)
                    {
                        var uses = b.Declarations.Count + b.References.Count;
                        if ((counts.TryGetValue(b.Name, out var c) ? c : 0) > uses || VarSet.Contains(b.Name))
                            b.Rename(Ctx.UniqueName(b.Name));
                        if (b.IsLexical && IsInLoop(s) && b.References.Exists(r => IsInNestedFunction(r.Scope, s)))
                            P.ReportWarning(b.Declarations.FirstOrDefault(), "The loop variable \"" + b.Name + "\" is captured by a closure inside a generator, each iteration shares the same variable (transform BlockScoping too to keep per iteration bindings)");
                    }
                    AddVar(b.Name);
                }
                foreach (var ch in s.Children)
                    if (ch.Kind != ScopeKind.Function && ch.Kind != ScopeKind.StaticBlock && ch.Kind != ScopeKind.Class && ch.Kind != ScopeKind.FunctionName)
                        Collect(ch, false);
            }
            if (fs != null)
                Collect(fs, true);
            //  Replace declarations with assignments, hoist function declarations
            new DeclRewriter(this).Rewrite(body);
            //  arguments
            if (Walker.Any(body, x => x is Identifier { Name: "arguments" }, x => x is not Function f || f.IsArrow))
            {
                ArgsName = Ctx.UniqueName("args");
                AddVar(ArgsName);
                new ArgumentsRewriter(ArgsName).Rewrite(body);
            }
        }

        static bool IsInLoop(Scope s)
        {
            for (var x = s; x != null && x.Kind != ScopeKind.Function; x = x.Parent)
                if (x.Kind == ScopeKind.For)
                    return true;
            return false;
        }

        static bool IsInNestedFunction(Scope refScope, Scope declScope)
        {
            for (var x = refScope; x != null && x != declScope; x = x.Parent)
                if (x.Kind == ScopeKind.Function)
                    return true;
            return false;
        }

        /// <summary>
        /// Replaces declarations with assignments (the names are hoisted), moves function declarations to the outer function
        /// </summary>
        sealed class DeclRewriter : Rewriter
        {
            readonly GeneratorLowering L;
            public DeclRewriter(GeneratorLowering l) => L = l;

            Expr Assignments(VarDecl v)
            {
                var list = new List<Expr>();
                foreach (var d in v.Decls)
                {
                    d.Init = Expr(d.Init);
                    d.Id = Expr(d.Id);
                    if (d.Init != null)
                        list.Add(B.Assign(d.Id, d.Init).At<Expr>(d));
                    else if (v.Kind != "var" && d.Id is Identifier)
                        list.Add(B.Assign(d.Id, B.Void0()));
                }
                return list.Count == 0 ? null : B.Seq(list);
            }

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function:
                        return n;
                    case VarDecl v:
                        {
                            var e = Assignments(v);
                            return e == null ? null : B.Stmt(e).At<Stmt>(v);
                        }
                    case ForStmt f when f.Init is VarDecl iv:
                        f.Init = Assignments(iv);
                        f.Test = Expr(f.Test);
                        f.Update = Expr(f.Update);
                        f.Body = Stmt(f.Body);
                        return f;
                    case ForInStmt f when f.Left is VarDecl lv:
                        if (lv.Decls[0].Init != null)
                        {
                            //  for (var x = init in obj): annex B, evaluate the initializer first
                            var s = new StatementList();
                            s.Body.Add(B.Stmt(B.Assign(lv.Decls[0].Id, Expr(lv.Decls[0].Init))));
                            f.Left = lv.Decls[0].Id;
                            f.Right = Expr(f.Right);
                            f.Body = Stmt(f.Body);
                            s.Body.Add(f);
                            return s;
                        }
                        f.Left = Expr(lv.Decls[0].Id);
                        f.Right = Expr(f.Right);
                        f.Body = Stmt(f.Body);
                        return f;
                    case ForOfStmt f when f.Left is VarDecl lv:
                        f.Left = Expr(lv.Decls[0].Id);
                        f.Right = Expr(f.Right);
                        f.Body = Stmt(f.Body);
                        return f;
                    case FunctionDecl fd:
                        L.Functions.Add(fd);
                        return null;
                    case ClassDecl cd:
                        {
                            var name = cd.C.Id.Name;
                            cd.MapChildren(this);
                            return B.Stmt(B.Assign(B.Id(name), new ClassExpr(cd.C))).At<Stmt>(cd);
                        }
                    case ClassExpr:
                        return n;
                }
                return base.Visit(n);
            }
        }

        sealed class ArgumentsRewriter : Rewriter
        {
            readonly String Name;
            public ArgumentsRewriter(String name) => Name = name;

            protected override Node Visit(Node n)
            {
                switch (n)
                {
                    case Function f when !f.IsArrow:
                        return n;
                    case Identifier { Name: "arguments" } i:
                        i.Name = Name;
                        return i;
                    case MemberExpr m when !m.Computed:
                        m.Object = Expr(m.Object);
                        return m;
                    case Property p when !p.Computed:
                        p.Value = Expr(p.Value);
                        return p;
                }
                return base.Visit(n);
            }
        }

        #endregion//Preparation

        #region Leap detection

        static bool HasYield(Node n) => n != null && Walker.Any(n, x => x is YieldExpr, x => x is not Function);

        /// <summary>
        /// Finds "leaps": yield, return and break / continue that jump out of the statement
        /// </summary>
        sealed class LeapFinder : Rewriter
        {
            bool Found;
            int Loops;
            int Switches;
            readonly HashSet<String> Labels = new HashSet<String>(StringComparer.Ordinal);

            public static bool Contains(Node n)
            {
                if (n == null)
                    return false;
                var f = new LeapFinder();
                f.Rewrite(n);
                return f.Found;
            }

            protected override Node Visit(Node n)
            {
                if (Found)
                    return n;
                switch (n)
                {
                    case Function:
                        return n;
                    case YieldExpr:
                    case ReturnStmt:
                        Found = true;
                        return n;
                    case BreakStmt b:
                        if (b.Label == null ? Loops + Switches == 0 : !Labels.Contains(b.Label))
                            Found = true;
                        return n;
                    case ContinueStmt c:
                        if (c.Label == null ? Loops == 0 : !Labels.Contains(c.Label))
                            Found = true;
                        return n;
                    case ForStmt:
                    case ForInStmt:
                    case ForOfStmt:
                    case WhileStmt:
                    case DoWhileStmt:
                        ++Loops;
                        base.Visit(n);
                        --Loops;
                        return n;
                    case SwitchStmt:
                        ++Switches;
                        base.Visit(n);
                        --Switches;
                        return n;
                    case LabeledStmt l:
                        {
                            bool added = Labels.Add(l.Label);
                            base.Visit(n);
                            if (added)
                                Labels.Remove(l.Label);
                            return n;
                        }
                }
                return base.Visit(n);
            }
        }

        #endregion//Leap detection

        #region Emitter

        sealed class Loc
        {
            public int Value = -1;
            public readonly List<NumberLiteral> Refs = new List<NumberLiteral>();
        }

        abstract class Entry
        {
        }

        sealed class LoopEntry : Entry
        {
            public Loc Break, Continue;
            public String Label;
        }

        sealed class SwitchEntry : Entry
        {
            public Loc Break;
        }

        sealed class LabeledEntry : Entry
        {
            public Loc Break;
            public String Label;
        }

        sealed class TryEntry : Entry
        {
            public Loc First, Catch, Finally, After;
        }

        sealed class FinallyEntry : Entry
        {
        }

        sealed class Emitter
        {
            readonly GeneratorLowering L;
            readonly List<Stmt> Listing = new List<Stmt>();
            readonly HashSet<int> Marked = new HashSet<int>();
            readonly List<Loc> AllLocs = new List<Loc>();
            readonly List<TryEntry> TryEntries = new List<TryEntry>();
            readonly List<Entry> Stack = new List<Entry>();
            int DelegateCount;

            public Emitter(GeneratorLowering l)
            {
                L = l;
                Mark(NewLoc());
            }

            Identifier CtxId => B.Id(L.CtxName);
            MemberExpr CtxProp(String name) => B.Member(CtxId, name);

            Loc NewLoc()
            {
                var l = new Loc();
                AllLocs.Add(l);
                return l;
            }

            Expr LocExpr(Loc l)
            {
                var n = new NumberLiteral(-1);
                l.Refs.Add(n);
                return n;
            }

            Loc Mark(Loc l)
            {
                l.Value = Listing.Count;
                Marked.Add(l.Value);
                return l;
            }

            /// <summary>
            /// A location at the current position that doesn't start a new case
            /// </summary>
            Loc Unmarked()
            {
                var l = NewLoc();
                l.Value = Listing.Count;
                return l;
            }

            void Emit(Stmt s) => Listing.Add(s);

            void EmitExpr(Expr e)
            {
                if (e != null)
                    Emit(B.Stmt(e));
            }

            void Assign(Expr target, Expr value) => Emit(B.Stmt(B.Assign(target, value)));

            void UpdatePrev(Loc l) => Assign(CtxProp("prev"), LocExpr(l));

            List<Stmt> JumpStmts(Loc l) => new List<Stmt> { B.Stmt(B.Assign(CtxProp("next"), LocExpr(l))), new BreakStmt() };

            void Jump(Loc l) => Listing.AddRange(JumpStmts(l));

            void JumpIf(Expr test, Loc l) => Emit(new IfStmt(test, new BlockStmt(JumpStmts(l))));

            void JumpIfNot(Expr test, Loc l)
            {
                Expr neg = test is UnaryExpr { Op: "!" } u ? u.Arg : B.Not(test);
                JumpIf(neg, l);
            }

            void WithEntry(Entry e, Action a)
            {
                Stack.Add(e);
                a();
                Stack.RemoveAt(Stack.Count - 1);
            }

            /// <summary>
            /// Jump to a break / continue location, through finally blocks if needed
            /// </summary>
            void LeapTo(Loc target, Entry targetEntry, String type)
            {
                bool crossesFinally = false;
                for (int i = Stack.Count - 1; i >= 0; --i)
                {
                    var e = Stack[i];
                    if (e == targetEntry)
                        break;
                    if ((e is TryEntry te && te.Finally != null) || e is FinallyEntry)
                        crossesFinally = true;
                }
                if (crossesFinally)
                    Emit(new ReturnStmt(B.Call(CtxProp("abrupt"), B.Str(type), LocExpr(target))));
                else
                    Jump(target);
            }

            public BlockStmt Finish(out Expr tryLocs)
            {
                var end = NewLoc();
                Mark(end);
                //  Patch the locations
                foreach (var l in AllLocs)
                    foreach (var r in l.Refs)
                        r.Value = l.Value;
                var sw = new SwitchStmt { Discriminant = B.Assign(CtxProp("prev"), CtxProp("next")) };
                SwitchCase cur = null;
                for (int i = 0; i <= Listing.Count; ++i)
                {
                    if (Marked.Contains(i))
                    {
                        cur = new SwitchCase { Test = B.Num(i) };
                        sw.Cases.Add(cur);
                    }
                    if (i < Listing.Count)
                        cur.Body.Add(Listing[i]);
                }
                sw.Cases.Add(new SwitchCase { Test = B.Str("end"), Body = { new ReturnStmt(B.Call(CtxProp("stop"))) } });
                var body = new BlockStmt();
                body.Body.Add(new WhileStmt(B.Num(1), sw));
                tryLocs = null;
                if (TryEntries.Count > 0)
                {
                    var arr = new ArrayExpr();
                    foreach (var t in TryEntries)
                    {
                        var e = new ArrayExpr();
                        e.Elements.Add(B.Num(t.First.Value));
                        if (t.Catch != null || t.Finally != null)
                            e.Elements.Add(t.Catch != null ? B.Num(t.Catch.Value) : null);
                        if (t.Finally != null)
                        {
                            e.Elements.Add(B.Num(t.Finally.Value));
                            e.Elements.Add(B.Num(t.After.Value));
                        }
                        arr.Elements.Add(e);
                    }
                    tryLocs = arr;
                }
                return body;
            }

            #region Statements

            public void Statements(List<Stmt> list)
            {
                foreach (var s in list)
                    Statement(s, null);
            }

            void Statement(Stmt s, String label)
            {
                if (s == null)
                    return;
                if (!LeapFinder.Contains(s))
                {
                    if (s is EmptyStmt)
                        return;
                    Emit(s);
                    return;
                }
                switch (s)
                {
                    case BlockStmt b:
                        Statements(b.Body);
                        return;
                    case StatementList sl:
                        Statements(sl.Body);
                        return;
                    case ExprStmt e:
                        EmitExpr(X(e.Expression, true));
                        return;
                    case LabeledStmt ls:
                        {
                            if (ls.Body is ForStmt || ls.Body is WhileStmt || ls.Body is DoWhileStmt || ls.Body is ForInStmt || ls.Body is ForOfStmt)
                            {
                                Statement(ls.Body, ls.Label);
                                return;
                            }
                            var after = NewLoc();
                            WithEntry(new LabeledEntry { Break = after, Label = ls.Label }, () => Statement(ls.Body, null));
                            Mark(after);
                            return;
                        }
                    case IfStmt i:
                        {
                            var elseLoc = i.Else != null ? NewLoc() : null;
                            var after = NewLoc();
                            JumpIfNot(X(i.Test), elseLoc ?? after);
                            Statement(i.Then, null);
                            if (i.Else != null)
                            {
                                Jump(after);
                                Mark(elseLoc);
                                Statement(i.Else, null);
                            }
                            Mark(after);
                            return;
                        }
                    case WhileStmt w:
                        {
                            var before = NewLoc();
                            var after = NewLoc();
                            Mark(before);
                            JumpIfNot(X(w.Test), after);
                            WithEntry(new LoopEntry { Break = after, Continue = before, Label = label }, () => Statement(w.Body, null));
                            Jump(before);
                            Mark(after);
                            return;
                        }
                    case DoWhileStmt d:
                        {
                            var first = NewLoc();
                            var test = NewLoc();
                            var after = NewLoc();
                            Mark(first);
                            WithEntry(new LoopEntry { Break = after, Continue = test, Label = label }, () => Statement(d.Body, null));
                            Mark(test);
                            JumpIf(X(d.Test), first);
                            Mark(after);
                            return;
                        }
                    case ForStmt f:
                        {
                            var head = NewLoc();
                            var update = NewLoc();
                            var after = NewLoc();
                            if (f.Init != null)
                            {
                                if (f.Init is Expr ie)
                                    EmitExpr(X(ie, true));
                                else
                                    Emit((Stmt)f.Init);
                            }
                            Mark(head);
                            if (f.Test != null)
                                JumpIfNot(X(f.Test), after);
                            WithEntry(new LoopEntry { Break = after, Continue = update, Label = label }, () => Statement(f.Body, null));
                            Mark(update);
                            if (f.Update != null)
                                EmitExpr(X(f.Update, true));
                            Jump(head);
                            Mark(after);
                            return;
                        }
                    case ForInStmt f:
                        {
                            var head = NewLoc();
                            var after = NewLoc();
                            var keys = L.NewTemp("keys");
                            var step = L.NewTemp("key");
                            Assign(keys, B.Call(B.Member(L.P_Runtime(), "keys"), X(f.Right)));
                            Mark(head);
                            Assign(step, B.Call(B.Id(keys.Name)));
                            JumpIf(B.Member(B.Id(step.Name), "done"), after);
                            EmitExpr(X(B.Assign((Expr)f.Left, B.Member(B.Id(step.Name), "value")), true));
                            WithEntry(new LoopEntry { Break = after, Continue = head, Label = label }, () => Statement(f.Body, null));
                            Jump(head);
                            Mark(after);
                            return;
                        }
                    case ForOfStmt f:
                        {
                            if (f.Await)
                            {
                                L.P.ReportError(f, "for await must be transformed before generators (enable AsyncGenerators)");
                                return;
                            }
                            //  for (left of right) body => iterator helper with try / catch / finally (closes the iterator on abrupt exits)
                            var it = L.NewTemp("iterator");
                            var step = L.NewTemp("step");
                            var err = L.NewTemp("err");
                            var loopBody = new BlockStmt();
                            loopBody.Body.Add(B.Stmt(B.Assign((Expr)f.Left, B.Member(B.Id(step.Name), "value"))));
                            loopBody.Body.Add(f.Body);
                            var loop = new ForStmt
                            {
                                Init = B.Call(B.Member(B.Id(it.Name), "s")),
                                Test = B.Not(B.Member(B.Assign(B.Id(step.Name), B.Call(B.Member(B.Id(it.Name), "n"))), "done")),
                                Body = loopBody,
                            };
                            Stmt loopStmt = label != null ? new LabeledStmt(label, loop) : loop;
                            var tr = new TryStmt
                            {
                                Block = new BlockStmt { Body = { loopStmt } },
                                Handler = new CatchClause { Param = B.Id(err.Name), Body = new BlockStmt { Body = { B.Stmt(B.Call(B.Member(B.Id(it.Name), "e"), B.Id(err.Name))) } } },
                                Finalizer = new BlockStmt { Body = { B.Stmt(B.Call(B.Member(B.Id(it.Name), "f"))) } },
                            };
                            Assign(it, B.Call(L.P.HelperId("createForOfIteratorHelper"), X(f.Right)));
                            Statement(tr, null);
                            return;
                        }
                    case BreakStmt b:
                        {
                            for (int i = Stack.Count - 1; i >= 0; --i)
                            {
                                var e = Stack[i];
                                if (b.Label == null)
                                {
                                    if (e is LoopEntry le)
                                    {
                                        LeapTo(le.Break, e, "break");
                                        return;
                                    }
                                    if (e is SwitchEntry se)
                                    {
                                        LeapTo(se.Break, e, "break");
                                        return;
                                    }
                                }
                                else
                                {
                                    if (e is LoopEntry le && le.Label == b.Label)
                                    {
                                        LeapTo(le.Break, e, "break");
                                        return;
                                    }
                                    if (e is LabeledEntry lb && lb.Label == b.Label)
                                    {
                                        LeapTo(lb.Break, e, "break");
                                        return;
                                    }
                                }
                            }
                            L.P.ReportError(b, "Invalid break target in a generator");
                            return;
                        }
                    case ContinueStmt c:
                        {
                            for (int i = Stack.Count - 1; i >= 0; --i)
                            {
                                if (Stack[i] is LoopEntry le && (c.Label == null || le.Label == c.Label))
                                {
                                    LeapTo(le.Continue, le, "continue");
                                    return;
                                }
                            }
                            L.P.ReportError(c, "Invalid continue target in a generator");
                            return;
                        }
                    case ReturnStmt r:
                        {
                            var args = new List<Expr> { B.Str("return") };
                            if (r.Arg != null)
                                args.Add(X(r.Arg));
                            Emit(new ReturnStmt(B.CallList(CtxProp("abrupt"), args)).At<Stmt>(r));
                            return;
                        }
                    case ThrowStmt t:
                        Emit(new ThrowStmt(X(t.Arg)).At<Stmt>(t));
                        return;
                    case SwitchStmt sw:
                        {
                            var disc = L.NewTemp("disc");
                            Assign(disc, X(sw.Discriminant));
                            var after = NewLoc();
                            var locs = sw.Cases.Select(_ => NewLoc()).ToList();
                            Loc defaultLoc = null;
                            for (int i = 0; i < sw.Cases.Count; ++i)
                            {
                                var c = sw.Cases[i];
                                if (c.Test == null)
                                    defaultLoc = locs[i];
                                else
                                    JumpIf(B.Bin("===", B.Id(disc.Name), X(c.Test)), locs[i]);
                            }
                            Jump(defaultLoc ?? after);
                            WithEntry(new SwitchEntry { Break = after }, () =>
                            {
                                for (int i = 0; i < sw.Cases.Count; ++i)
                                {
                                    Mark(locs[i]);
                                    Statements(sw.Cases[i].Body);
                                }
                            });
                            Mark(after);
                            return;
                        }
                    case TryStmt t:
                        {
                            var after = NewLoc();
                            var te = new TryEntry
                            {
                                First = Unmarked(),
                                Catch = t.Handler != null ? NewLoc() : null,
                                Finally = t.Finalizer != null ? NewLoc() : null,
                                After = after,
                            };
                            TryEntries.Add(te);
                            UpdatePrev(te.First);
                            WithEntry(te, () =>
                            {
                                Statements(t.Block.Body);
                                if (te.Catch != null)
                                {
                                    Jump(te.Finally ?? after);
                                    Mark(te.Catch);
                                    UpdatePrev(te.Catch);
                                    var call = B.Call(B.Index(CtxId, B.Str("catch")), LocExpr(te.First));
                                    if (t.Handler.Param != null)
                                        EmitExpr(B.Assign(t.Handler.Param, call));
                                    else
                                        EmitExpr(call);
                                    Statements(t.Handler.Body.Body);
                                }
                            });
                            if (te.Finally != null)
                            {
                                Mark(te.Finally);
                                UpdatePrev(te.Finally);
                                WithEntry(new FinallyEntry(), () => Statements(t.Finalizer.Body));
                                Emit(new ReturnStmt(B.Call(CtxProp("finish"), LocExpr(te.Finally))));
                            }
                            Mark(after);
                            UpdatePrev(after);
                            return;
                        }
                    case WithStmt w:
                        L.P.ReportError(w, "with statements containing yield can't be transformed");
                        return;
                }
                L.P.ReportError(s, "Can't transform a " + s.GetType().Name + " in a generator");
            }

            #endregion//Statements

            #region Expressions

            bool IsSafe(Expr e) => e == null || B.IsConstant(e) || e is ThisExpr || e is FunctionExpr || (e is Identifier i && L.Temps.Contains(i.Name));

            /// <summary>
            /// Store a value in a temporary (if it isn't safe to read later)
            /// </summary>
            Expr Save(Expr e)
            {
                if (IsSafe(e))
                    return e;
                var t = L.NewTemp();
                Assign(t, e);
                return B.Id(t.Name);
            }

            /// <summary>
            /// Explode a list of child expressions (in evaluation order), values that are followed by a yield are stored in temporaries
            /// </summary>
            List<Expr> Kids(List<Expr> kids)
            {
                int last = -1;
                for (int i = 0; i < kids.Count; ++i)
                    if (HasYield(kids[i]))
                        last = i;
                var r = new List<Expr>(kids.Count);
                for (int i = 0; i < kids.Count; ++i)
                {
                    var k = kids[i];
                    if (k == null)
                    {
                        r.Add(null);
                        continue;
                    }
                    if (k is SpreadElement sp)
                    {
                        var a = X(sp.Arg);
                        if (i < last)
                            a = Save(a);
                        r.Add(new SpreadElement(a).At<Expr>(sp));
                        continue;
                    }
                    var x = X(k);
                    if (i < last)
                        x = Save(x);
                    r.Add(x);
                }
                return r;
            }

            Expr Unsupported(Expr e)
            {
                L.P.ReportError(e, "yield inside a " + e.GetType().Name + " can't be transformed");
                return e;
            }

            /// <summary>
            /// Explode an expression: statements are emitted for the parts that contain yield, the returned expression is the value
            /// </summary>
            Expr X(Expr e, bool ignore = false)
            {
                if (e == null || !HasYield(e))
                    return e;
                switch (e)
                {
                    case YieldExpr y:
                        {
                            var arg = X(y.Arg);
                            var after = NewLoc();
                            if (y.Delegate)
                            {
                                var name = "t" + DelegateCount++;
                                Emit(new ReturnStmt(B.Call(CtxProp("delegateYield"), arg, B.Str(name), LocExpr(after))));
                                Mark(after);
                                return ignore ? null : CtxProp(name);
                            }
                            Assign(CtxProp("next"), LocExpr(after));
                            Emit(new ReturnStmt(arg).At<Stmt>(y));
                            Mark(after);
                            return ignore ? null : CtxProp("sent");
                        }
                    case SequenceExpr s:
                        {
                            var c = s.Expressions.Count;
                            for (int i = 0; i < c - 1; ++i)
                                EmitExpr(X(s.Expressions[i], true));
                            return X(s.Expressions[c - 1], ignore);
                        }
                    case BinaryExpr b when b.IsLogical && HasYield(b.Right):
                        {
                            var after = NewLoc();
                            var left = X(b.Left);
                            Identifier result = null;
                            Expr test;
                            if (!ignore || b.Op == "??")
                            {
                                result = L.NewTemp();
                                Assign(result, left);
                                test = B.Id(result.Name);
                            }
                            else
                                test = left;
                            switch (b.Op)
                            {
                                case "&&":
                                    JumpIfNot(test, after);
                                    break;
                                case "||":
                                    JumpIf(test, after);
                                    break;
                                default:
                                    JumpIf(B.IsNotNullish(test, test.Clone<Expr>()), after);
                                    break;
                            }
                            var right = X(b.Right, ignore);
                            if (!ignore)
                                Assign(B.Id(result.Name), right);
                            else
                                EmitExpr(right);
                            Mark(after);
                            return ignore ? null : B.Id(result.Name);
                        }
                    case ConditionalExpr c when HasYield(c.Then) || HasYield(c.Else):
                        {
                            var elseLoc = NewLoc();
                            var after = NewLoc();
                            var result = ignore ? null : L.NewTemp();
                            JumpIfNot(X(c.Test), elseLoc);
                            var t = X(c.Then, ignore);
                            if (!ignore)
                                Assign(B.Id(result.Name), t);
                            else
                                EmitExpr(t);
                            Jump(after);
                            Mark(elseLoc);
                            var el = X(c.Else, ignore);
                            if (!ignore)
                                Assign(B.Id(result.Name), el);
                            else
                                EmitExpr(el);
                            Mark(after);
                            return ignore ? null : B.Id(result.Name);
                        }
                    case ConditionalExpr c:
                        c.Test = X(c.Test);
                        return c;
                    case BinaryExpr b:
                        {
                            var k = Kids(new List<Expr> { b.Left, b.Right });
                            return new BinaryExpr(b.Op, k[0], k[1]).At<Expr>(b);
                        }
                    case AssignExpr a:
                        return Assignment(a);
                    case UpdateExpr u:
                        {
                            if (u.Arg is MemberExpr um)
                            {
                                var k = Kids(new List<Expr> { um.Object, um.Computed ? um.Property : null });
                                return new UpdateExpr(u.Op, u.Prefix, new MemberExpr(k[0], um.Computed ? k[1] : um.Property, um.Computed)).At<Expr>(u);
                            }
                            return Unsupported(e);
                        }
                    case UnaryExpr u:
                        {
                            if (u.Arg is MemberExpr um && u.Op == "delete")
                            {
                                var k = Kids(new List<Expr> { um.Object, um.Computed ? um.Property : null });
                                return new UnaryExpr("delete", new MemberExpr(k[0], um.Computed ? k[1] : um.Property, um.Computed)).At<Expr>(u);
                            }
                            return new UnaryExpr(u.Op, X(u.Arg)).At<Expr>(u);
                        }
                    case MemberExpr m:
                        {
                            if (m.Object is SuperExpr)
                                return new MemberExpr(m.Object, X(m.Property), m.Computed).At<Expr>(m);
                            var k = Kids(new List<Expr> { m.Object, m.Computed ? m.Property : null });
                            return new MemberExpr(k[0], m.Computed ? k[1] : m.Property, m.Computed).At<Expr>(m);
                        }
                    case CallExpr c:
                        return Call(c);
                    case NewExpr ne:
                        {
                            var list = new List<Expr> { ne.Callee };
                            list.AddRange(ne.Args);
                            var k = Kids(list);
                            var r = new NewExpr(k[0]).At<NewExpr>(ne);
                            r.Args.AddRange(k.Skip(1));
                            return r;
                        }
                    case ArrayExpr ar:
                        return new ArrayExpr(Kids(ar.Elements)).At<Expr>(ar);
                    case ObjectExpr o:
                        {
                            //  Flatten the keys and values in evaluation order
                            var list = new List<Expr>();
                            foreach (var p in o.Properties)
                            {
                                if (p is Property pp)
                                {
                                    list.Add(pp.Computed ? pp.Key : null);
                                    list.Add(pp.Value);
                                }
                                else if (p is SpreadElement se)
                                {
                                    list.Add(null);
                                    list.Add(se.Arg);
                                }
                            }
                            var k = Kids(list);
                            var r = new ObjectExpr().At<ObjectExpr>(o);
                            int idx = 0;
                            foreach (var p in o.Properties)
                            {
                                var key = k[idx++];
                                var val = k[idx++];
                                if (p is Property pp)
                                    r.Properties.Add(new Property { Kind = pp.Kind, Key = pp.Computed ? key : pp.Key, Computed = pp.Computed, Value = val, Method = pp.Method, Shorthand = pp.Shorthand && val is Identifier vi && pp.Key is Identifier ki && vi.Name == ki.Name }.At<Property>(pp));
                                else
                                    r.Properties.Add(new SpreadElement(val).At<Expr>(p));
                            }
                            return r;
                        }
                    case TemplateLiteral t:
                        {
                            var k = Kids(t.Expressions);
                            var r = new TemplateLiteral { Quasis = t.Quasis }.At<TemplateLiteral>(t);
                            r.Expressions.AddRange(k);
                            return r;
                        }
                    case TaggedTemplate tt:
                        {
                            if (tt.Tag is MemberExpr && HasYield(tt.Quasi))
                                return Unsupported(e);
                            var list = new List<Expr> { tt.Tag };
                            list.AddRange(tt.Quasi.Expressions);
                            var k = Kids(list);
                            var q = new TemplateLiteral { Quasis = tt.Quasi.Quasis }.At<TemplateLiteral>(tt.Quasi);
                            q.Expressions.AddRange(k.Skip(1));
                            return new TaggedTemplate { Tag = k[0], Quasi = q }.At<Expr>(tt);
                        }
                    case SpreadElement sp:
                        return new SpreadElement(X(sp.Arg)).At<Expr>(sp);
                    case ChainExpr ch:
                        return X(OptionalChain.Lower(ch.Expression, h => L.NewTemp(h), Ctx.Loose, false), ignore);
                }
                return Unsupported(e);
            }

            TransformContext Ctx => L.Ctx;

            Expr Call(CallExpr c)
            {
                if (c.Callee is MemberExpr m && m.Object is not SuperExpr && c.Args.Exists(a => HasYield(a)))
                {
                    //  Preserve this: obj.f(yield x) => _o = obj, _f = _o.f, ..., _f.call(_o, sent)
                    var obj = Save(X(m.Object));
                    var prop = m.Computed ? Save(X(m.Property)) : m.Property;
                    var fn = Save(new MemberExpr(obj, prop, m.Computed).At<Expr>(m));
                    var args = Kids(c.Args);
                    var all = new List<Expr> { obj.Clone<Expr>() };
                    all.AddRange(args);
                    return B.CallList(B.Member(fn, "call"), all).At<Expr>(c);
                }
                if (c.Callee is MemberExpr m2 && !c.Args.Exists(a => HasYield(a)))
                {
                    //  Only the callee contains yield: keep the member call (this is preserved)
                    var callee = X(m2);
                    return B.CallList(callee, c.Args).At<Expr>(c);
                }
                var list = new List<Expr> { c.Callee };
                list.AddRange(c.Args);
                var k = Kids(list);
                return B.CallList(k[0], k.Skip(1).ToList()).At<Expr>(c);
            }

            Expr Assignment(AssignExpr a)
            {
                var op = a.Op;
                if (op == "||=" || op == "&&=" || op == "??=")
                {
                    if (a.Target is not Identifier)
                        return Unsupported(a);
                    //  a ||= b => a || (a = b)
                    var logical = B.Bin(op.Substring(0, 2), a.Target, B.Assign(a.Target.Clone<Expr>(), a.Value));
                    return X(logical);
                }
                switch (a.Target)
                {
                    case Identifier id:
                        {
                            if (op == "=")
                                return B.Assign(id, X(a.Value)).At<Expr>(a);
                            //  Read the old value before the yield
                            var old = Save(id.Clone<Expr>());
                            var v = X(a.Value);
                            return B.Assign(id, B.Bin(op.Substring(0, op.Length - 1), old, v)).At<Expr>(a);
                        }
                    case MemberExpr m:
                        {
                            Expr obj, prop;
                            if (m.Object is SuperExpr)
                            {
                                obj = m.Object;
                                prop = m.Computed ? Save(X(m.Property)) : m.Property;
                            }
                            else
                            {
                                obj = Save(X(m.Object));
                                prop = m.Computed ? Save(X(m.Property)) : m.Property;
                            }
                            var target = new MemberExpr(obj, prop, m.Computed).At<Expr>(m);
                            if (op == "=")
                                return new AssignExpr("=", target, X(a.Value)).At<Expr>(a);
                            var old = Save(target.Clone<Expr>());
                            var v = X(a.Value);
                            return new AssignExpr("=", target, B.Bin(op.Substring(0, op.Length - 1), old, v)).At<Expr>(a);
                        }
                    default:
                        if (HasYield(a.Target))
                        {
                            L.P.ReportError(a, "yield inside a destructuring pattern (default value or computed key) can't be transformed");
                            return a;
                        }
                        return new AssignExpr(op, a.Target, X(a.Value)).At<Expr>(a);
                }
            }

            #endregion//Expressions
        }

        #endregion//Emitter
    }
}
