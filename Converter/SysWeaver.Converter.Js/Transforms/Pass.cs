using System;
using System.Collections.Generic;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Base class of transform passes.
    /// Tracks the current var scope (program, function, static block) so that temporary variables can be declared.
    /// Override Transform (call Children(n) to process the children).
    /// </summary>
    abstract class Pass : Rewriter
    {
        protected readonly TransformContext Ctx;

        protected Pass(TransformContext ctx)
        {
            Ctx = ctx;
        }

        /// <summary>
        /// Run the pass on a program
        /// </summary>
        public virtual void Run(ProgramNode p) => Rewrite(p);

        protected sealed class TempScope
        {
            public TempScope Parent;
            /// <summary>ProgramNode, Function or StaticBlock</summary>
            public Node Owner;
            public readonly List<String> Temps = new List<String>();
            /// <summary>Statements to add at the start of the scope (after the temps)</summary>
            public readonly List<Stmt> Prologue = new List<Stmt>();
        }

        /// <summary>The current var scope</summary>
        protected TempScope Scope;

        /// <summary>
        /// The current function (null at the top level), arrow functions included
        /// </summary>
        protected Function CurrentFunction
        {
            get
            {
                for (var s = Scope; s != null; s = s.Parent)
                    if (s.Owner is Function f)
                        return f;
                return null;
            }
        }

        protected override Node Visit(Node n)
        {
            switch (n)
            {
                case ProgramNode:
                case Function:
                case StaticBlock:
                    {
                        var s = new TempScope { Parent = Scope, Owner = n };
                        Scope = s;
                        var r = Transform(n);
                        Scope = s.Parent;
                        DeclareTemps(s);
                        return r;
                    }
            }
            return Transform(n);
        }

        /// <summary>
        /// Transform a node, the default implementation processes the children
        /// </summary>
        protected virtual Node Transform(Node n) => Children(n);

        /// <summary>
        /// Process the children of a node (function parameters are processed in the parent scope)
        /// </summary>
        protected Node Children(Node n)
        {
            if (n is Function f && Scope.Owner == f)
            {
                f.Id = (Identifier)Expr(f.Id);
                var s = Scope;
                //  Temporaries needed by parameter expressions can't be declared in the function body (separate scope)
                if (s.Parent != null)
                    Scope = s.Parent;
                MapList(f.Params);
                Scope = s;
                if (f.Body != null)
                    f.Body = Block(f.Body);
                if (f.ExprBody != null)
                    f.ExprBody = Expr(f.ExprBody);
                return f;
            }
            n.MapChildren(this);
            return n;
        }

        void DeclareTemps(TempScope s)
        {
            if (s.Temps.Count == 0 && s.Prologue.Count == 0)
                return;
            List<Stmt> body;
            switch (s.Owner)
            {
                case ProgramNode p:
                    body = p.Body;
                    break;
                case Function f:
                    body = f.EnsureBlockBody().Body;
                    break;
                case StaticBlock sb:
                    body = sb.Body;
                    break;
                default:
                    throw new InvalidOperationException();
            }
            var stmts = new List<Stmt>();
            if (s.Temps.Count > 0)
            {
                var v = new VarDecl("var");
                foreach (var t in s.Temps)
                    v.Decls.Add(new VarDeclarator(new Identifier(t)));
                stmts.Add(v);
            }
            stmts.AddRange(s.Prologue);
            body.InsertRange(DirectiveCount(body), stmts);
        }

        /// <summary>
        /// The number of directives at the start of a statement list
        /// </summary>
        public static int DirectiveCount(List<Stmt> body)
        {
            int i = 0;
            while (i < body.Count && body[i] is ExprStmt { Directive: not null })
                ++i;
            return i;
        }

        /// <summary>
        /// Create a temporary variable in the current var scope
        /// </summary>
        internal Identifier Temp(String hint = "ref")
        {
            var name = Ctx.UniqueName(hint);
            Scope.Temps.Add(name);
            return new Identifier(name);
        }

        /// <summary>
        /// Create a temporary variable in the top level (program) scope
        /// </summary>
        protected Identifier GlobalTemp(String hint = "ref")
        {
            var s = Scope;
            while (s.Parent != null)
                s = s.Parent;
            var name = Ctx.UniqueName(hint);
            s.Temps.Add(name);
            return new Identifier(name);
        }

        /// <summary>
        /// Memoize an expression that will be used twice.
        /// If the expression is simple (no side effects) both are copies of it, else first is "(_t = e)" and second is "_t".
        /// </summary>
        protected void Memo(Expr e, out Expr first, out Expr second, String hint = null)
        {
            if (B.IsSimple(e))
            {
                first = e;
                second = e.DeepClone() as Expr;
                return;
            }
            var t = Temp(hint ?? B.NameHint(e));
            first = B.Assign(t, e);
            second = new Identifier(t.Name);
        }

        /// <summary>
        /// Memoize an assignment target that is used twice (read and write), the first use contains the memoization assignments.
        /// Ex: a[b()] becomes (_a = a)[_b = b()] and _a[_b].
        /// </summary>
        protected void MemoLhs(Expr target, out Expr first, out Expr second)
        {
            if (target is MemberExpr m)
            {
                Expr obj = m.Object, obj2;
                if (obj is SuperExpr || B.IsSimple(obj))
                    obj2 = obj.Clone<Expr>();
                else
                {
                    var t = Temp(B.NameHint(obj));
                    obj = B.Assign(t, obj);
                    obj2 = B.Id(t.Name);
                }
                Expr prop = m.Property, prop2;
                if (!m.Computed || B.IsConstant(prop))
                    prop2 = prop.Clone<Expr>();
                else
                {
                    var t = Temp(B.NameHint(prop));
                    prop = B.Assign(t, prop);
                    prop2 = B.Id(t.Name);
                }
                first = new MemberExpr(obj, prop, m.Computed).At<MemberExpr>(m);
                second = new MemberExpr(obj2, prop2, m.Computed).At<MemberExpr>(m);
                return;
            }
            first = target;
            second = target.Clone<Expr>();
        }

        /// <summary>
        /// Add a warning
        /// </summary>
        protected void Warn(Node n, String message) => Ctx.Diag.Warning(n?.Start ?? -1, message);

        /// <summary>
        /// Add an error
        /// </summary>
        protected void Error(Node n, String message) => Ctx.Diag.Error(n?.Start ?? -1, message);

        /// <summary>
        /// Get a helper identifier
        /// </summary>
        internal Identifier Helper(String name) => Ctx.Helper(name);
    }

    /// <summary>
    /// Syntax tree builders and utilities
    /// </summary>
    static class B
    {
        public static Identifier Id(String name) => new Identifier(name);
        public static StringLiteral Str(String s) => new StringLiteral(s);
        public static NumberLiteral Num(double v) => new NumberLiteral(v);
        public static Expr Void0() => new UnaryExpr("void", new NumberLiteral(0));
        public static Expr Bool(bool v) => new BooleanLiteral(v);
        public static NullLiteral Null() => new NullLiteral();
        public static ThisExpr This() => new ThisExpr();
        public static AssignExpr Assign(Expr target, Expr value) => new AssignExpr("=", target, value);
        public static MemberExpr Member(Expr obj, String name) => new MemberExpr(obj, name);
        public static MemberExpr Index(Expr obj, Expr key) => new MemberExpr(obj, key, true);
        public static CallExpr Call(Expr callee, params Expr[] args) => new CallExpr(callee, args);
        public static CallExpr CallList(Expr callee, List<Expr> args) => new CallExpr(callee, args);
        public static ExprStmt Stmt(Expr e) => new ExprStmt(e).At<ExprStmt>(e);
        public static BinaryExpr Bin(String op, Expr l, Expr r) => new BinaryExpr(op, l, r);
        public static UnaryExpr Not(Expr e) => new UnaryExpr("!", e);
        public static ConditionalExpr Cond(Expr t, Expr a, Expr b) => new ConditionalExpr(t, a, b);
        public static VarDecl Var(String name, Expr init) => new VarDecl("var", new Identifier(name), init);
        public static VarDecl Var(Expr id, Expr init) => new VarDecl("var", id, init);

        /// <summary>
        /// A key expression for a property name (string literal for identifiers)
        /// </summary>
        public static Expr KeyExpr(Expr key, bool computed)
        {
            if (computed)
                return key;
            return key switch
            {
                Identifier i => new StringLiteral(i.Name),
                PrivateName p => new StringLiteral(p.Name),
                _ => key,
            };
        }

        /// <summary>
        /// Build a sequence expression (flattens nested sequences, returns the single expression if only one)
        /// </summary>
        public static Expr Seq(params Expr[] items) => Seq((IEnumerable<Expr>)items);

        public static Expr Seq(IEnumerable<Expr> items)
        {
            var s = new SequenceExpr();
            foreach (var x in items)
            {
                if (x == null)
                    continue;
                if (x is SequenceExpr se)
                    s.Expressions.AddRange(se.Expressions);
                else
                    s.Expressions.Add(x);
            }
            if (s.Expressions.Count == 1)
                return s.Expressions[0];
            return s;
        }

        /// <summary>
        /// "x === null || x === void 0" (with two copies of x)
        /// </summary>
        public static Expr IsNullish(Expr first, Expr second) =>
            Bin("||", Bin("===", first, Null()), Bin("===", second, Void0()));

        /// <summary>
        /// "x !== null &amp;&amp; x !== void 0" (with two copies of x)
        /// </summary>
        public static Expr IsNotNullish(Expr first, Expr second) =>
            Bin("&&", Bin("!==", first, Null()), Bin("!==", second, Void0()));

        /// <summary>
        /// True if an expression is free of side effects and cheap, so it can be evaluated twice
        /// </summary>
        public static bool IsSimple(Expr e) => e switch
        {
            Identifier => true,
            ThisExpr => true,
            NumberLiteral or StringLiteral or BooleanLiteral or NullLiteral or BigIntLiteral => true,
            UnaryExpr { Op: "void", Arg: NumberLiteral } => true,
            _ => false,
        };

        /// <summary>
        /// True if an expression is a literal (or undefined) and has no side effects
        /// </summary>
        public static bool IsConstant(Expr e) => e switch
        {
            NumberLiteral or StringLiteral or BooleanLiteral or NullLiteral or BigIntLiteral => true,
            UnaryExpr { Op: "void" or "-" or "+" or "!", Arg: var a } => IsConstant(a),
            _ => false,
        };

        /// <summary>
        /// A name hint for a temporary that holds the value of an expression
        /// </summary>
        public static String NameHint(Expr e)
        {
            switch (e)
            {
                case Identifier i:
                    return i.Name;
                case MemberExpr m:
                    if (!m.Computed && m.Property is Identifier pi)
                        return pi.Name;
                    if (m.Property is StringLiteral ps)
                        return ps.Value;
                    return NameHint(m.Object);
                case CallExpr c:
                    return NameHint(c.Callee);
                case ThisExpr:
                    return "this";
                case ChainExpr ch:
                    return NameHint(ch.Expression);
                case AssignExpr a:
                    return NameHint(a.Target);
            }
            return "ref";
        }

        /// <summary>
        /// Wrap statements in a block if there are more than one
        /// </summary>
        public static Stmt Block(List<Stmt> stmts)
        {
            if (stmts.Count == 1 && stmts[0] is not VarDecl { Kind: not "var" })
                return stmts[0];
            return new BlockStmt(stmts);
        }

        /// <summary>
        /// An immediately invoked function: (function () { body }).call(this) or (function () { body })()
        /// </summary>
        public static Expr Iife(List<Stmt> body, bool callWithThis)
        {
            var f = new Function { Body = new BlockStmt(body) };
            var fe = new FunctionExpr(f);
            if (callWithThis)
                return Call(Member(fe, "call"), This());
            return Call(fe);
        }

        /// <summary>
        /// Returns the statements in a statement (block contents or the single statement)
        /// </summary>
        public static List<Stmt> StatementsOf(Stmt s)
        {
            if (s is BlockStmt b)
                return b.Body;
            return new List<Stmt> { s };
        }
    }
}
