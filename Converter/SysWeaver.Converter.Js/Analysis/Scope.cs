using System;
using System.Collections.Generic;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Analysis
{
    enum BindingKind
    {
        Var,
        Let,
        Const,
        Function,
        Param,
        Class,
        /// <summary>The inner (immutable) name binding of a class</summary>
        ClassInner,
        /// <summary>The name of a function expression (visible inside the function)</summary>
        FunctionName,
        Catch,
        Import,
    }

    enum ScopeKind
    {
        Program,
        Function,
        /// <summary>Holds the name of a named function expression</summary>
        FunctionName,
        Block,
        /// <summary>The head of a for / for-in / for-of statement (let / const in the head)</summary>
        For,
        Catch,
        Class,
        Switch,
        StaticBlock,
    }

    /// <summary>
    /// A reference to a binding (or a global)
    /// </summary>
    sealed class Reference
    {
        public Identifier Id;
        public Scope Scope;
        /// <summary>True if the reference is assigned to (assignment, update, for-in/of target)</summary>
        public bool IsWrite;
        /// <summary>The binding, null for globals (unresolved)</summary>
        public Binding Binding;
    }

    sealed class Binding
    {
        public String Name;
        public BindingKind Kind;
        public Scope Scope;
        public readonly List<Identifier> Declarations = new List<Identifier>();
        public readonly List<Reference> References = new List<Reference>();

        public bool IsLexical => Kind == BindingKind.Let || Kind == BindingKind.Const || Kind == BindingKind.Class || Kind == BindingKind.ClassInner;

        /// <summary>
        /// True if any reference writes to the binding
        /// </summary>
        public bool IsAssigned
        {
            get
            {
                foreach (var r in References)
                    if (r.IsWrite)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Rename the binding (all declarations and references)
        /// </summary>
        public void Rename(String newName)
        {
            if (Scope.Bindings.TryGetValue(Name, out var b) && b == this)
                Scope.Bindings.Remove(Name);
            Name = newName;
            Scope.Bindings[newName] = this;
            foreach (var d in Declarations)
                d.Name = newName;
            foreach (var r in References)
                r.Id.Name = newName;
        }

        public override String ToString() => Kind + " " + Name;
    }

    sealed class Scope
    {
        public readonly ScopeKind Kind;
        public readonly Scope Parent;
        /// <summary>The node that owns this scope (ProgramNode, Function, BlockStmt, ForStmt, CatchClause, Class, SwitchStmt, StaticBlock)</summary>
        public readonly Node Node;
        public readonly Dictionary<String, Binding> Bindings = new Dictionary<String, Binding>(StringComparer.Ordinal);
        public readonly List<Scope> Children = new List<Scope>();
        /// <summary>True if the scope contains a direct eval call or a with statement (bindings can be accessed dynamically)</summary>
        public bool HasDynamic;

        public Scope(ScopeKind kind, Scope parent, Node node)
        {
            Kind = kind;
            Parent = parent;
            Node = node;
            parent?.Children.Add(this);
        }

        /// <summary>
        /// True if this scope is a var scope (function, program or static block)
        /// </summary>
        public bool IsVarScope => Kind == ScopeKind.Function || Kind == ScopeKind.Program || Kind == ScopeKind.StaticBlock;

        public Scope VarScope
        {
            get
            {
                var s = this;
                while (!s.IsVarScope)
                    s = s.Parent;
                return s;
            }
        }

        public Binding Lookup(String name)
        {
            for (var s = this; s != null; s = s.Parent)
                if (s.Bindings.TryGetValue(name, out var b))
                    return b;
            return null;
        }

        public override String ToString() => Kind + " (" + Bindings.Count + " bindings)";
    }

    /// <summary>
    /// Builds the scope tree of a program and resolves all references
    /// </summary>
    sealed class ScopeAnalyzer
    {
        readonly Dictionary<Node, Scope> ScopeOf = new Dictionary<Node, Scope>();
        readonly List<Reference> AllRefs = new List<Reference>();
        Scope Cur;

        public Scope Root { get; private set; }

        /// <summary>All references (resolved or not)</summary>
        public IReadOnlyList<Reference> References => AllRefs;

        /// <summary>
        /// Get the scope that a node owns (null if it doesn't own a scope)
        /// </summary>
        public Scope Get(Node n) => n != null && ScopeOf.TryGetValue(n, out var s) ? s : null;

        /// <summary>
        /// Analyze a tree, the root can be a program or a function
        /// </summary>
        public static ScopeAnalyzer Analyze(Node root)
        {
            var a = new ScopeAnalyzer();
            Walker.Walk(root, n =>
            {
                if (n is FunctionDecl fd)
                    a.DeclaredFunctions.Add(fd.F);
                return true;
            });
            if (root is ProgramNode p)
            {
                a.Root = a.Push(ScopeKind.Program, p);
                a.Statements(p.Body);
            }
            else if (root is Function f)
            {
                a.Root = a.Push(ScopeKind.Program, f);
                a.Function(f);
            }
            else
            {
                a.Root = a.Push(ScopeKind.Program, root);
                a.Any(root);
            }
            a.Resolve();
            return a;
        }

        Scope Push(ScopeKind k, Node n)
        {
            var s = new Scope(k, Cur, n);
            ScopeOf[n] = s;
            Cur = s;
            return s;
        }

        void Pop(Scope s) => Cur = s.Parent;

        Binding Declare(Scope s, Identifier id, BindingKind kind)
        {
            if (!s.Bindings.TryGetValue(id.Name, out var b))
            {
                b = new Binding { Name = id.Name, Kind = kind, Scope = s };
                s.Bindings[id.Name] = b;
            }
            else if (kind == BindingKind.Function || (b.Kind == BindingKind.Param && kind != BindingKind.Var))
                b.Kind = kind;
            b.Declarations.Add(id);
            return b;
        }

        void Ref(Identifier id, bool write = false)
        {
            AllRefs.Add(new Reference { Id = id, Scope = Cur, IsWrite = write });
        }

        void Resolve()
        {
            foreach (var r in AllRefs)
            {
                var b = r.Scope.Lookup(r.Id.Name);
                r.Binding = b;
                b?.References.Add(r);
            }
        }

        /// <summary>
        /// Collect the binding identifiers of a pattern
        /// </summary>
        public static void PatternNames(Expr p, List<Identifier> names)
        {
            switch (p)
            {
                case null:
                    return;
                case Identifier i:
                    names.Add(i);
                    return;
                case ArrayPattern ap:
                    foreach (var e in ap.Elements)
                        PatternNames(e, names);
                    return;
                case ObjectPattern op:
                    foreach (var x in op.Properties)
                    {
                        if (x is Property pp)
                            PatternNames(pp.Value, names);
                        else if (x is RestElement re)
                            PatternNames(re.Arg, names);
                    }
                    return;
                case AssignPattern asp:
                    PatternNames(asp.Left, names);
                    return;
                case RestElement r:
                    PatternNames(r.Arg, names);
                    return;
            }
        }

        /// <summary>
        /// Declare the names in a binding pattern, and visit the default values / computed keys
        /// </summary>
        void BindingPattern(Expr p, Scope s, BindingKind kind)
        {
            switch (p)
            {
                case null:
                    return;
                case Identifier i:
                    Declare(s, i, kind);
                    return;
                case ArrayPattern ap:
                    foreach (var e in ap.Elements)
                        BindingPattern(e, s, kind);
                    return;
                case ObjectPattern op:
                    foreach (var x in op.Properties)
                    {
                        if (x is Property pp)
                        {
                            if (pp.Computed)
                                Any(pp.Key);
                            BindingPattern(pp.Value, s, kind);
                        }
                        else if (x is RestElement re)
                            BindingPattern(re.Arg, s, kind);
                    }
                    return;
                case AssignPattern asp:
                    BindingPattern(asp.Left, s, kind);
                    Any(asp.Right);
                    return;
                case RestElement r:
                    BindingPattern(r.Arg, s, kind);
                    return;
                default:
                    //  Member expressions in assignment patterns
                    Any(p);
                    return;
            }
        }

        /// <summary>
        /// Visit an assignment target (pattern with references)
        /// </summary>
        void AssignTarget(Expr p)
        {
            switch (p)
            {
                case null:
                    return;
                case Identifier i:
                    Ref(i, true);
                    return;
                case ArrayPattern ap:
                    foreach (var e in ap.Elements)
                        AssignTarget(e);
                    return;
                case ObjectPattern op:
                    foreach (var x in op.Properties)
                    {
                        if (x is Property pp)
                        {
                            if (pp.Computed)
                                Any(pp.Key);
                            AssignTarget(pp.Value);
                        }
                        else if (x is RestElement re)
                            AssignTarget(re.Arg);
                    }
                    return;
                case AssignPattern asp:
                    AssignTarget(asp.Left);
                    Any(asp.Right);
                    return;
                case RestElement r:
                    AssignTarget(r.Arg);
                    return;
                default:
                    Any(p);
                    return;
            }
        }

        /// <summary>
        /// Hoist the declarations of a statement list into the current (block) scope and its var scope
        /// </summary>
        void HoistDeclarations(List<Stmt> body)
        {
            foreach (var s in body)
                HoistLexical(s);
        }

        void HoistLexical(Stmt s)
        {
            switch (s)
            {
                case VarDecl v when v.Kind != "var":
                    {
                        var kind = v.Kind == "const" || v.Kind.EndsWith("using", StringComparison.Ordinal) ? BindingKind.Const : BindingKind.Let;
                        var names = new List<Identifier>();
                        foreach (var d in v.Decls)
                            PatternNames(d.Id, names);
                        foreach (var n in names)
                            Declare(Cur, n, kind);
                        break;
                    }
                case FunctionDecl fd when fd.F.Id != null:
                    Declare(Cur, fd.F.Id, BindingKind.Function);
                    break;
                case ClassDecl cd when cd.C.Id != null:
                    Declare(Cur, cd.C.Id, BindingKind.Class);
                    break;
                case ExportNamedDecl en when en.Declaration != null:
                    HoistLexical(en.Declaration);
                    break;
                case ExportDefaultDecl ed when ed.Declaration is FunctionDecl || ed.Declaration is ClassDecl:
                    HoistLexical(ed.Declaration);
                    break;
                case ImportDecl im:
                    foreach (var sp in im.Specifiers)
                        Declare(Cur, sp.Local, BindingKind.Import);
                    break;
            }
        }

        void Statements(List<Stmt> body)
        {
            HoistDeclarations(body);
            foreach (var s in body)
                Statement(s);
        }

        void Statement(Stmt s)
        {
            switch (s)
            {
                case null:
                    return;
                case BlockStmt b:
                    {
                        var sc = Push(ScopeKind.Block, b);
                        Statements(b.Body);
                        Pop(sc);
                        return;
                    }
                case StatementList sl:
                    Statements(sl.Body);
                    return;
                case VarDecl v:
                    VarDecl(v);
                    return;
                case FunctionDecl fd:
                    Function(fd.F);
                    return;
                case ClassDecl cd:
                    Class(cd.C);
                    return;
                case ForStmt f:
                    {
                        var sc = Push(ScopeKind.For, f);
                        if (f.Init is VarDecl vd)
                        {
                            HoistLexical(vd);
                            VarDecl(vd);
                        }
                        else
                            Any(f.Init);
                        Any(f.Test);
                        Any(f.Update);
                        Statement(f.Body);
                        Pop(sc);
                        return;
                    }
                case ForInStmt f:
                    ForInOf(f, f.Left, f.Right, f.Body);
                    return;
                case ForOfStmt f:
                    ForInOf(f, f.Left, f.Right, f.Body);
                    return;
                case TryStmt t:
                    Statement(t.Block);
                    if (t.Handler != null)
                    {
                        var sc = Push(ScopeKind.Catch, t.Handler);
                        BindingPattern(t.Handler.Param, sc, BindingKind.Catch);
                        Statement(t.Handler.Body);
                        Pop(sc);
                    }
                    Statement(t.Finalizer);
                    return;
                case SwitchStmt sw:
                    {
                        Any(sw.Discriminant);
                        var sc = Push(ScopeKind.Switch, sw);
                        foreach (var c in sw.Cases)
                            HoistDeclarations(c.Body);
                        foreach (var c in sw.Cases)
                        {
                            Any(c.Test);
                            foreach (var x in c.Body)
                                Statement(x);
                        }
                        Pop(sc);
                        return;
                    }
                case WithStmt w:
                    Any(w.Object);
                    for (var x = Cur; x != null; x = x.Parent)
                        x.HasDynamic = true;
                    Statement(w.Body);
                    return;
                case ExportNamedDecl en:
                    if (en.Declaration != null)
                        Statement(en.Declaration);
                    else if (en.Source == null)
                    {
                        foreach (var sp in en.Specifiers)
                            if (sp.Local is Identifier li)
                                Ref(li);
                    }
                    return;
                case ExportDefaultDecl ed:
                    Statement(ed.Declaration);
                    return;
                case ImportDecl:
                case ExportAllDecl:
                    return;
                default:
                    //  Other statements: visit all children
                    s.MapChildren(Visitor);
                    return;
            }
        }

        void ForInOf(Stmt f, Node left, Expr right, Stmt body)
        {
            var sc = Push(ScopeKind.For, f);
            if (left is VarDecl vd)
            {
                HoistLexical(vd);
                VarDecl(vd);
            }
            else
                AssignTarget((Expr)left);
            Any(right);
            Statement(body);
            Pop(sc);
        }

        void VarDecl(VarDecl v)
        {
            if (v.Kind == "var")
            {
                var vs = Cur.VarScope;
                foreach (var d in v.Decls)
                {
                    BindingPattern(d.Id, vs, BindingKind.Var);
                    Any(d.Init);
                }
                return;
            }
            //  Lexical: already declared (hoisted), visit default values / initializers
            foreach (var d in v.Decls)
            {
                PatternDefaults(d.Id);
                Any(d.Init);
            }
        }

        /// <summary>
        /// Visit default values and computed keys in a pattern (without declaring)
        /// </summary>
        void PatternDefaults(Expr p)
        {
            switch (p)
            {
                case ArrayPattern ap:
                    foreach (var e in ap.Elements)
                        PatternDefaults(e);
                    return;
                case ObjectPattern op:
                    foreach (var x in op.Properties)
                    {
                        if (x is Property pp)
                        {
                            if (pp.Computed)
                                Any(pp.Key);
                            PatternDefaults(pp.Value);
                        }
                        else if (x is RestElement re)
                            PatternDefaults(re.Arg);
                    }
                    return;
                case AssignPattern asp:
                    PatternDefaults(asp.Left);
                    Any(asp.Right);
                    return;
                case RestElement r:
                    PatternDefaults(r.Arg);
                    return;
            }
        }

        void Function(Function f)
        {
            Scope nameScope = null;
            //  Named function expressions (and class / object methods are anonymous)
            if (f.Id != null && !IsDeclared(f))
            {
                nameScope = Push(ScopeKind.FunctionName, f.Id);
                Declare(nameScope, f.Id, BindingKind.FunctionName);
            }
            var sc = Push(ScopeKind.Function, f);
            foreach (var p in f.Params)
                BindingPattern(p, sc, BindingKind.Param);
            if (f.Body != null)
            {
                //  The function body shares the function scope
                ScopeOf[f.Body] = sc;
                Statements(f.Body.Body);
            }
            else
                Any(f.ExprBody);
            Pop(sc);
            if (nameScope != null)
                Pop(nameScope);
        }

        readonly HashSet<Function> DeclaredFunctions = new HashSet<Function>();

        bool IsDeclared(Function f) => DeclaredFunctions.Contains(f);

        void Class(Class c, bool isExpr = false)
        {
            if (c.SuperClass != null)
                Any(c.SuperClass);
            var sc = Push(ScopeKind.Class, c);
            if (c.Id != null)
                Declare(sc, isExpr ? c.Id : new Identifier(c.Id.Name), BindingKind.ClassInner);
            foreach (var m in c.Members)
            {
                switch (m)
                {
                    case MethodDef md:
                        if (md.Computed)
                            Any(md.Key);
                        Function(md.Value);
                        break;
                    case FieldDef fd:
                        if (fd.Computed)
                            Any(fd.Key);
                        if (fd.Value != null)
                        {
                            //  Field initializers behave like methods (own this / arguments not allowed)
                            var fs = Push(ScopeKind.Function, fd);
                            Any(fd.Value);
                            Pop(fs);
                        }
                        break;
                    case StaticBlock sb:
                        {
                            var bs = Push(ScopeKind.StaticBlock, sb);
                            Statements(sb.Body);
                            Pop(bs);
                            break;
                        }
                }
            }
            Pop(sc);
        }

        Rewriter Visitor => _Visitor ??= new Walker(n =>
        {
            Any(n);
            return false;
        });
        Rewriter _Visitor;

        /// <summary>
        /// Visit any node (expressions, statements)
        /// </summary>
        void Any(Node n)
        {
            switch (n)
            {
                case null:
                    return;
                case Stmt s:
                    Statement(s);
                    return;
                case Identifier i:
                    Ref(i);
                    return;
                case Function f:
                    Function(f);
                    return;
                case FunctionExpr fe:
                    Function(fe.F);
                    return;
                case ClassExpr ce:
                    {
                        //  A named class expression: the name is only visible inside the class
                        Class(ce.C, true);
                        return;
                    }
                case MemberExpr m:
                    Any(m.Object);
                    if (m.Computed)
                        Any(m.Property);
                    return;
                case Property p:
                    if (p.Computed)
                        Any(p.Key);
                    Any(p.Value);
                    return;
                case AssignExpr a:
                    if (a.Op == "=")
                        AssignTarget(a.Target);
                    else if (a.Target is Identifier ai)
                    {
                        Ref(ai, true);
                    }
                    else
                        Any(a.Target);
                    Any(a.Value);
                    return;
                case UpdateExpr u:
                    if (u.Arg is Identifier ui)
                        Ref(ui, true);
                    else
                        Any(u.Arg);
                    return;
                case CallExpr c:
                    if (c.Callee is Identifier { Name: "eval" })
                    {
                        for (var x = Cur; x != null; x = x.Parent)
                            x.HasDynamic = true;
                    }
                    Any(c.Callee);
                    foreach (var a in c.Args)
                        Any(a);
                    return;
                case MetaProperty:
                case PrivateName:
                    return;
                default:
                    n.MapChildren(Visitor);
                    return;
            }
        }
    }
}
