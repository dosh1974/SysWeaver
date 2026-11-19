using System;
using System.Collections.Generic;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Shared code for the async function and async generator transforms
    /// </summary>
    abstract class AsyncPassBase : Pass
    {
        protected AsyncPassBase(TransformContext ctx) : base(ctx)
        {
        }

        #region for await

        /// <summary>
        /// Lower a for await loop to a for loop using _asyncIterator and await (Babel compatible)
        /// </summary>
        protected Stmt LowerForAwait(ForOfStmt f, String label)
        {
            Ctx.Did(JsFeature.AsyncGenerators);
            var abrupt = Temp("iteratorAbruptCompletion");
            var didErr = Temp("didIteratorError");
            var err = Temp("iteratorError");
            var it = Temp("iterator");
            var step = Temp("step");
            var errParam = B.Id(Ctx.UniqueName("err"));
            //  The loop variable
            Stmt assign;
            Expr value = B.Member(B.Id(step.Name), "value");
            if (f.Left is VarDecl vd)
                assign = new VarDecl(vd.Kind, vd.Decls[0].Id, value);
            else
                assign = B.Stmt(B.Assign((Expr)f.Left, value));
            var body = new BlockStmt { Body = { assign } };
            if (f.Body is BlockStmt bb && !(f.Left is VarDecl { Kind: not "var" } lvd && DeclaresAny(bb, lvd)))
                body.Body.AddRange(bb.Body);
            else
                body.Body.Add(f.Body);
            var loop = new ForStmt
            {
                Init = B.Assign(it, B.Call(Helper("asyncIterator"), f.Right)),
                Test = B.Assign(B.Id(abrupt.Name), B.Not(B.Member(new AwaitExpr(B.Call(B.Member(B.Id(it.Name), "next"))), "done"))),
                Update = B.Assign(B.Id(abrupt.Name), B.Bool(false)),
                Body = body,
            };
            //  _step = await _iterator.next() inside the test
            ((UnaryExpr)((AssignExpr)loop.Test).Value).Arg = B.Member(B.Assign(B.Id(step.Name), new AwaitExpr(B.Call(B.Member(B.Id(it.Name), "next")))), "done");
            Stmt loopStmt = label != null ? new LabeledStmt(label, loop) : loop;
            var tryStmt = new TryStmt
            {
                Block = new BlockStmt { Body = { loopStmt } },
                Handler = new CatchClause
                {
                    Param = errParam,
                    Body = new BlockStmt
                    {
                        Body =
                        {
                            B.Stmt(B.Assign(B.Id(didErr.Name), B.Bool(true))),
                            B.Stmt(B.Assign(B.Id(err.Name), B.Id(errParam.Name))),
                        },
                    },
                },
                Finalizer = new BlockStmt
                {
                    Body =
                    {
                        new TryStmt
                        {
                            Block = new BlockStmt
                            {
                                Body =
                                {
                                    new IfStmt(
                                        B.Bin("&&", B.Id(abrupt.Name), B.Bin("!=", B.Member(B.Id(it.Name), "return"), B.Null())),
                                        new BlockStmt { Body = { B.Stmt(new AwaitExpr(B.Call(B.Member(B.Id(it.Name), "return")))) } }),
                                },
                            },
                            Finalizer = new BlockStmt
                            {
                                Body = { new IfStmt(B.Id(didErr.Name), new BlockStmt { Body = { new ThrowStmt(B.Id(err.Name)) } }) },
                            },
                        },
                    },
                },
            };
            var r = new StatementList();
            r.Body.Add(B.Stmt(B.Seq(B.Assign(abrupt, B.Bool(false)), B.Assign(didErr, B.Bool(false)))));
            r.Body.Add(tryStmt);
            r.LeadingComments = f.LeadingComments;
            r.Start = f.Start;
            return new BlockStmt(r.Body) { LeadingComments = f.LeadingComments, Start = f.Start };
        }

        /// <summary>
        /// True if a block declares (let / const / class / function) any of the names in a declaration
        /// </summary>
        static bool DeclaresAny(BlockStmt b, VarDecl d)
        {
            var names = new List<Identifier>();
            foreach (var x in d.Decls)
                Analysis.ScopeAnalyzer.PatternNames(x.Id, names);
            var set = new HashSet<String>(StringComparer.Ordinal);
            foreach (var n in names)
                set.Add(n.Name);
            foreach (var s in b.Body)
            {
                switch (s)
                {
                    case VarDecl v:
                        {
                            var l = new List<Identifier>();
                            foreach (var x in v.Decls)
                                Analysis.ScopeAnalyzer.PatternNames(x.Id, l);
                            if (l.Exists(i => set.Contains(i.Name)))
                                return true;
                            break;
                        }
                    case FunctionDecl fd when fd.F.Id != null && set.Contains(fd.F.Id.Name):
                        return true;
                    case ClassDecl cd when cd.C.Id != null && set.Contains(cd.C.Id.Name):
                        return true;
                }
            }
            return false;
        }

        #endregion//for await

        #region Wrapping

        /// <summary>
        /// Rewrites the body of an async function / async generator that is moved into a generator function
        /// </summary>
        sealed class BodyRewriter : Rewriter
        {
            readonly AsyncPassBase P;
            readonly bool AsyncGen;
            public readonly bool IsArrow;
            Identifier SuperGet;
            Identifier SuperSet;
            Identifier NewTarget;
            public readonly List<Stmt> Prologue = new List<Stmt>();
            public bool UsesArguments;

            public BodyRewriter(AsyncPassBase p, bool asyncGen, bool isArrow)
            {
                P = p;
                AsyncGen = asyncGen;
                IsArrow = isArrow;
            }

            Identifier Get()
            {
                if (SuperGet == null)
                {
                    SuperGet = B.Id(P.Ctx.UniqueName("superprop_get"));
                    var prop = B.Id(P.Ctx.UniqueName("prop"));
                    var arrow = new Function { IsArrow = true, Params = { prop }, ExprBody = B.Index(new SuperExpr(), B.Id(prop.Name)) };
                    Prologue.Add(new VarDecl("var", SuperGet, new FunctionExpr(arrow)));
                }
                return B.Id(SuperGet.Name);
            }

            Identifier Set()
            {
                if (SuperSet == null)
                {
                    SuperSet = B.Id(P.Ctx.UniqueName("superprop_set"));
                    var prop = B.Id(P.Ctx.UniqueName("prop"));
                    var value = B.Id(P.Ctx.UniqueName("value"));
                    var arrow = new Function { IsArrow = true, Params = { prop, value }, ExprBody = B.Assign(B.Index(new SuperExpr(), B.Id(prop.Name)), B.Id(value.Name)) };
                    Prologue.Add(new VarDecl("var", SuperSet, new FunctionExpr(arrow)));
                }
                return B.Id(SuperSet.Name);
            }

            static Expr Key(MemberExpr m) => m.Computed ? m.Property : B.Str(((Identifier)m.Property).Name);

            protected override Node Visit(Node n)
            {
                switch (n)
                {
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
                    case Identifier { Name: "arguments" }:
                        UsesArguments = true;
                        return n;
                    case AwaitExpr a:
                        {
                            var arg = Expr(a.Arg);
                            if (AsyncGen)
                                return new YieldExpr(B.Call(P.Helper("awaitAsyncGenerator"), arg)).At<Expr>(a);
                            return new YieldExpr(arg).At<Expr>(a);
                        }
                    case YieldExpr { Delegate: true } y when AsyncGen:
                        {
                            var arg = Expr(y.Arg);
                            return new YieldExpr(B.Call(P.Helper("asyncGeneratorDelegate"), B.Call(P.Helper("asyncIterator"), arg)), true).At<Expr>(y);
                        }
                    case MetaProperty { Meta: "new", Prop: "target" } mp:
                        if (!IsArrow)
                            return B.Void0();
                        NewTarget ??= P.CaptureNewTarget();
                        return NewTarget == null ? B.Void0() : B.Id(NewTarget.Name);
                    case CallExpr { Callee: MemberExpr { Object: SuperExpr } sm } call:
                        {
                            MapList(call.Args);
                            var args = new List<Expr> { B.This() };
                            args.AddRange(call.Args);
                            return new CallExpr(B.Member(B.Call(Get(), Expr(Key(sm))), "call"), args).At<Expr>(call);
                        }
                    case AssignExpr { Target: MemberExpr { Object: SuperExpr } am } a:
                        {
                            var key = Expr(Key(am));
                            var v = Expr(a.Value);
                            if (a.Op == "=")
                                return B.Call(Set(), key, v).At<Expr>(a);
                            Expr k1 = key, k2 = key;
                            if (!B.IsConstant(key))
                            {
                                var t = P.Temp("key");
                                k1 = B.Assign(t, key);
                                k2 = B.Id(t.Name);
                            }
                            var op = a.Op.Substring(0, a.Op.Length - 1);
                            var cur = B.Call(Get(), k1);
                            if (op == "&&" || op == "||" || op == "??")
                                return B.Bin(op, cur, B.Call(Set(), k2.Clone<Expr>(), v)).At<Expr>(a);
                            return B.Call(Set(), k2.Clone<Expr>(), B.Bin(op, cur, v)).At<Expr>(a);
                        }
                    case UpdateExpr { Arg: MemberExpr { Object: SuperExpr } um } u:
                        {
                            var key = Expr(Key(um));
                            Expr k1 = key, k2 = key;
                            if (!B.IsConstant(key))
                            {
                                var t = P.Temp("key");
                                k1 = B.Assign(t, key);
                                k2 = B.Id(t.Name);
                            }
                            var old = P.Temp("old");
                            var get = B.Assign(old, B.Call(B.Id("Number"), B.Call(Get(), k1)));
                            var set = B.Call(Set(), k2.Clone<Expr>(), B.Bin(u.Op == "++" ? "+" : "-", B.Id(old.Name), B.Num(1)));
                            return u.Prefix ? B.Seq(get, set) : B.Seq(get, set, B.Id(old.Name));
                        }
                    case MemberExpr { Object: SuperExpr } m:
                        return B.Call(Get(), Expr(Key(m))).At<Expr>(m);
                }
                return base.Visit(n);
            }
        }

        /// <summary>
        /// Capture new.target in the closest non-arrow function (var _newtarget = new.target), returns null at the top level
        /// </summary>
        Identifier CaptureNewTarget()
        {
            for (var s = Scope; s != null; s = s.Parent)
            {
                if (s.Owner is Function f && !f.IsArrow)
                {
                    var id = B.Id(Ctx.UniqueName("newtarget"));
                    s.Prologue.Add(B.Var(id.Name, new MetaProperty("new", "target")));
                    return id;
                }
            }
            return null;
        }

        /// <summary>
        /// Convert an async function (or async generator) to a regular function that calls a helper with a generator:
        /// function f(a) { return helper(function* (a) { ... }).apply(this, arguments); }
        /// Arrow functions: (a) => helper(function* () { ... }).call(this)
        /// </summary>
        protected void Wrap(Function f, String helper, bool asyncGen)
        {
            var rw = new BodyRewriter(this, asyncGen, f.IsArrow);
            var body = f.EnsureBlockBody();
            body = rw.Block(body);
            var gen = new Function
            {
                IsGenerator = true,
                Body = body,
                Start = f.Start,
            };
            Expr call;
            if (f.IsArrow)
            {
                //  The arrow keeps its parameters, this and arguments are lexical
                var callee = B.Call(Helper(helper), new FunctionExpr(gen));
                call = rw.UsesArguments
                    ? B.Call(B.Member(callee, "apply"), B.This(), B.Id("arguments"))
                    : B.Call(B.Member(callee, "call"), B.This());
            }
            else
            {
                //  The generator gets the parameters (errors in default values are thrown inside the generator), the function keeps placeholders (for length)
                gen.Params = f.Params;
                var ph = new List<Expr>();
                foreach (var p in f.Params)
                {
                    if (p is AssignPattern || p is RestElement)
                        break;
                    ph.Add(B.Id(Ctx.UniqueName("x")));
                }
                f.Params = ph;
                var callee = B.Call(Helper(helper), new FunctionExpr(gen));
                call = B.Call(B.Member(callee, "apply"), B.This(), B.Id("arguments"));
            }
            var nb = new BlockStmt();
            //  Keep directives in the outer function
            int dc = DirectiveCount(body.Body);
            for (int i = 0; i < dc; ++i)
                nb.Body.Add(body.Body[i]);
            body.Body.RemoveRange(0, dc);
            nb.Body.AddRange(rw.Prologue);
            nb.Body.Add(new ReturnStmt(call));
            f.Body = nb;
            f.ExprBody = null;
            f.IsAsync = false;
            f.IsGenerator = false;
        }

        #endregion//Wrapping
    }

    /// <summary>
    /// ES2018: async generator functions and for await
    /// </summary>
    sealed class AsyncGeneratorsPass : AsyncPassBase
    {
        public AsyncGeneratorsPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case LabeledStmt { Body: ForOfStmt { Await: true } lf } l:
                    Children(lf);
                    return LowerForAwait(lf, l.Label);
                case ForOfStmt { Await: true } f:
                    Children(f);
                    return LowerForAwait(f, null);
                case Function { IsAsync: true, IsGenerator: true } fn:
                    Children(fn);
                    Ctx.Did(JsFeature.AsyncGenerators);
                    Wrap(fn, "wrapAsyncGenerator", true);
                    return fn;
            }
            return Children(n);
        }
    }

    /// <summary>
    /// ES2017: async functions and await
    /// </summary>
    sealed class AsyncFunctionsPass : AsyncPassBase
    {
        public AsyncFunctionsPass(TransformContext ctx) : base(ctx)
        {
        }

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case LabeledStmt { Body: ForOfStmt { Await: true } lf } l:
                    Children(lf);
                    return LowerForAwait(lf, l.Label);
                case ForOfStmt { Await: true } f:
                    Children(f);
                    return LowerForAwait(f, null);
                case Function { IsAsync: true } fn:
                    Children(fn);
                    if (fn.IsGenerator)
                    {
                        Ctx.Did(JsFeature.AsyncGenerators);
                        Wrap(fn, "wrapAsyncGenerator", true);
                    }
                    else
                    {
                        Ctx.Did(JsFeature.AsyncFunctions);
                        Wrap(fn, "asyncToGenerator", false);
                    }
                    return fn;
            }
            return Children(n);
        }
    }
}
