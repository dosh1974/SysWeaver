using System;
using System.Collections.Generic;

namespace SysWeaver.Converter.Js.Ast
{
    /// <summary>
    /// A comment attached to a statement or class member (printed before it)
    /// </summary>
    public sealed class Comment
    {
        /// <summary>True for a block comment, false for a line comment</summary>
        public bool IsBlock;
        /// <summary>The comment text (without the comment delimiters)</summary>
        public String Text;
        /// <summary>The source position of the comment (or -1)</summary>
        public int Start = -1;

        /// <summary>
        /// True if this comment should be preserved when only license comments are kept (/*! ... */, @license or @preserve)
        /// </summary>
        public bool IsLicense => IsBlock && ((Text.Length > 0 && Text[0] == '!') || Text.Contains("@license", StringComparison.Ordinal) || Text.Contains("@preserve", StringComparison.Ordinal));
    }

    /// <summary>
    /// Base class of all nodes in the (mutable) syntax tree used by the converter.
    /// Child nodes are rewritten in place using a Rewriter (see MapChildren).
    /// </summary>
    public abstract class Node
    {
        /// <summary>Start position (char index) in the original source, -1 for synthesized nodes</summary>
        public int Start = -1;
        /// <summary>End position (char index) in the original source, -1 for synthesized nodes</summary>
        public int End = -1;
        /// <summary>Comments that should be printed before this node (statements and class members)</summary>
        public List<Comment> LeadingComments;

        /// <summary>
        /// Rewrite all child nodes using the supplied rewriter (the child references are replaced by the rewritten nodes)
        /// </summary>
        public abstract void MapChildren(Rewriter r);

        /// <summary>
        /// Called on a shallow copy, should replace all lists with copies (so that the copy can be modified)
        /// </summary>
        protected virtual void CopyLists()
        {
        }

        /// <summary>
        /// Create a deep copy of this node and all child nodes
        /// </summary>
        public T Clone<T>() where T : Node => (T)DeepClone();

        /// <summary>
        /// Create a deep copy of this node and all child nodes
        /// </summary>
        public Node DeepClone()
        {
            var c = (Node)MemberwiseClone();
            c.CopyLists();
            if (c.LeadingComments != null)
                c.LeadingComments = new List<Comment>(c.LeadingComments);
            c.MapChildren(Cloner.Instance);
            return c;
        }

        /// <summary>
        /// Copy the source position of another node (for diagnostics)
        /// </summary>
        public T At<T>(Node from) where T : Node
        {
            if (from != null)
            {
                Start = from.Start;
                End = from.End;
            }
            return (T)this;
        }

        sealed class Cloner : Rewriter
        {
            public static readonly Cloner Instance = new Cloner();
            protected override Node Visit(Node n) => n.DeepClone();
        }

        protected static List<T> Copy<T>(List<T> l) => l == null ? null : new List<T>(l);
    }

    /// <summary>
    /// Base class of all expressions (including patterns)
    /// </summary>
    public abstract class Expr : Node
    {
    }

    /// <summary>
    /// Base class of all statements
    /// </summary>
    public abstract class Stmt : Node
    {
    }

    #region Expressions

    public sealed class Identifier : Expr
    {
        public String Name;
        public Identifier(String name) => Name = name;
        public override void MapChildren(Rewriter r) { }
        public override String ToString() => Name;
    }

    /// <summary>
    /// A private name (#name), Name excludes the #
    /// </summary>
    public sealed class PrivateName : Expr
    {
        public String Name;
        public PrivateName(String name) => Name = name;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class NumberLiteral : Expr
    {
        public double Value;
        public NumberLiteral(double value) => Value = value;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class StringLiteral : Expr
    {
        public String Value;
        public StringLiteral(String value) => Value = value;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class BooleanLiteral : Expr
    {
        public bool Value;
        public BooleanLiteral(bool value) => Value = value;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class NullLiteral : Expr
    {
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class RegexLiteral : Expr
    {
        public String Pattern;
        public String Flags;
        public RegexLiteral(String pattern, String flags)
        {
            Pattern = pattern;
            Flags = flags ?? "";
        }
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class BigIntLiteral : Expr
    {
        /// <summary>The decimal digits (no n suffix)</summary>
        public String Digits;
        public BigIntLiteral(String digits) => Digits = digits;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class TemplateQuasi
    {
        /// <summary>The cooked value, null if the raw text contains an invalid escape sequence (only allowed in tagged templates)</summary>
        public String Cooked;
        /// <summary>The raw value (as written in the source, line endings normalized)</summary>
        public String Raw;
    }

    public sealed class TemplateLiteral : Expr
    {
        public List<TemplateQuasi> Quasis = new List<TemplateQuasi>();
        public List<Expr> Expressions = new List<Expr>();
        protected override void CopyLists()
        {
            Quasis = Copy(Quasis);
            Expressions = Copy(Expressions);
        }
        public override void MapChildren(Rewriter r) => r.MapList(Expressions);
    }

    public sealed class TaggedTemplate : Expr
    {
        public Expr Tag;
        public TemplateLiteral Quasi;
        public override void MapChildren(Rewriter r)
        {
            Tag = r.Expr(Tag);
            Quasi = (TemplateLiteral)r.Expr(Quasi);
        }
    }

    public sealed class ThisExpr : Expr
    {
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class SuperExpr : Expr
    {
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class ArrayExpr : Expr
    {
        /// <summary>Elements, null for holes, may contain SpreadElement</summary>
        public List<Expr> Elements = new List<Expr>();
        public ArrayExpr() { }
        public ArrayExpr(IEnumerable<Expr> elements) => Elements = new List<Expr>(elements);
        protected override void CopyLists() => Elements = Copy(Elements);
        public override void MapChildren(Rewriter r) => r.MapList(Elements);
    }

    public enum PropKind
    {
        Init,
        Get,
        Set,
    }

    /// <summary>
    /// A property in an object literal or an object pattern
    /// </summary>
    public sealed class Property : Node
    {
        public PropKind Kind;
        public Expr Key;
        public bool Computed;
        /// <summary>The value (a FunctionExpr for methods and accessors, a pattern in an ObjectPattern)</summary>
        public Expr Value;
        public bool Shorthand;
        public bool Method;

        public Property() { }
        public Property(Expr key, Expr value, bool computed = false)
        {
            Key = key;
            Value = value;
            Computed = computed;
        }

        public override void MapChildren(Rewriter r)
        {
            Key = r.Expr(Key);
            Value = r.Expr(Value);
        }
    }

    public sealed class ObjectExpr : Expr
    {
        /// <summary>Property or SpreadElement</summary>
        public List<Node> Properties = new List<Node>();
        protected override void CopyLists() => Properties = Copy(Properties);
        public override void MapChildren(Rewriter r) => r.MapList(Properties);
    }

    /// <summary>
    /// A function (declaration, expression, arrow, method, accessor)
    /// </summary>
    public sealed class Function : Node
    {
        public Identifier Id;
        public List<Expr> Params = new List<Expr>();
        /// <summary>The body (null for arrow functions with an expression body)</summary>
        public BlockStmt Body;
        /// <summary>The expression body of an arrow function (Body is null)</summary>
        public Expr ExprBody;
        public bool IsArrow;
        public bool IsAsync;
        public bool IsGenerator;
        /// <summary>True if this is a class or object method / accessor (can use super)</summary>
        public bool IsMethod;

        protected override void CopyLists() => Params = Copy(Params);

        public override void MapChildren(Rewriter r)
        {
            Id = (Identifier)r.Expr(Id);
            r.MapList(Params);
            if (Body != null)
                Body = r.Block(Body);
            if (ExprBody != null)
                ExprBody = r.Expr(ExprBody);
        }

        /// <summary>
        /// Make sure that the function has a block body (converts arrow expression bodies to { return expr; })
        /// </summary>
        public BlockStmt EnsureBlockBody()
        {
            if (Body == null)
            {
                Body = new BlockStmt();
                if (ExprBody != null)
                    Body.Body.Add(new ReturnStmt(ExprBody).At<ReturnStmt>(ExprBody));
                ExprBody = null;
            }
            return Body;
        }

        /// <summary>
        /// True if all parameters are plain identifiers (no defaults, patterns or rest)
        /// </summary>
        public bool HasSimpleParams
        {
            get
            {
                foreach (var p in Params)
                    if (p is not Identifier)
                        return false;
                return true;
            }
        }
    }

    public sealed class FunctionExpr : Expr
    {
        public Function F;
        public FunctionExpr(Function f) => F = f;
        public override void MapChildren(Rewriter r) => F = (Function)r.Rewrite(F);
    }

    /// <summary>
    /// A class (declaration or expression)
    /// </summary>
    public sealed class Class : Node
    {
        public Identifier Id;
        public Expr SuperClass;
        /// <summary>MethodDef, FieldDef or StaticBlock</summary>
        public List<Node> Members = new List<Node>();
        protected override void CopyLists() => Members = Copy(Members);
        public override void MapChildren(Rewriter r)
        {
            Id = (Identifier)r.Expr(Id);
            SuperClass = r.Expr(SuperClass);
            r.MapList(Members);
        }
    }

    public sealed class ClassExpr : Expr
    {
        public Class C;
        public ClassExpr(Class c) => C = c;
        public override void MapChildren(Rewriter r) => C = (Class)r.Rewrite(C);
    }

    public sealed class MethodDef : Node
    {
        public Expr Key;
        public bool Computed;
        public bool Static;
        /// <summary>"constructor", "method", "get" or "set"</summary>
        public String Kind = "method";
        public Function Value;
        public override void MapChildren(Rewriter r)
        {
            Key = r.Expr(Key);
            Value = (Function)r.Rewrite(Value);
        }
    }

    public sealed class FieldDef : Node
    {
        public Expr Key;
        public bool Computed;
        public bool Static;
        public Expr Value;
        public override void MapChildren(Rewriter r)
        {
            Key = r.Expr(Key);
            Value = r.Expr(Value);
        }
    }

    public sealed class StaticBlock : Node
    {
        public List<Stmt> Body = new List<Stmt>();
        protected override void CopyLists() => Body = Copy(Body);
        public override void MapChildren(Rewriter r) => r.MapStatements(Body);
    }

    public sealed class UnaryExpr : Expr
    {
        public String Op;
        public Expr Arg;
        public UnaryExpr(String op, Expr arg)
        {
            Op = op;
            Arg = arg;
        }
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    public sealed class UpdateExpr : Expr
    {
        /// <summary>"++" or "--"</summary>
        public String Op;
        public bool Prefix;
        public Expr Arg;
        public UpdateExpr(String op, bool prefix, Expr arg)
        {
            Op = op;
            Prefix = prefix;
            Arg = arg;
        }
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    /// <summary>
    /// Binary and logical (&amp;&amp;, ||, ??) expressions
    /// </summary>
    public sealed class BinaryExpr : Expr
    {
        public String Op;
        public Expr Left;
        public Expr Right;
        public BinaryExpr(String op, Expr left, Expr right)
        {
            Op = op;
            Left = left;
            Right = right;
        }
        public bool IsLogical => Op == "&&" || Op == "||" || Op == "??";
        public override void MapChildren(Rewriter r)
        {
            Left = r.Expr(Left);
            Right = r.Expr(Right);
        }
    }

    public sealed class AssignExpr : Expr
    {
        /// <summary>The operator, ex: "=", "+=", "??="</summary>
        public String Op;
        /// <summary>The target (Identifier, MemberExpr or a pattern for "=")</summary>
        public Expr Target;
        public Expr Value;
        public AssignExpr(String op, Expr target, Expr value)
        {
            Op = op;
            Target = target;
            Value = value;
        }
        public override void MapChildren(Rewriter r)
        {
            Target = r.Expr(Target);
            Value = r.Expr(Value);
        }
    }

    public sealed class ConditionalExpr : Expr
    {
        public Expr Test;
        public Expr Then;
        public Expr Else;
        public ConditionalExpr(Expr test, Expr then, Expr @else)
        {
            Test = test;
            Then = then;
            Else = @else;
        }
        public override void MapChildren(Rewriter r)
        {
            Test = r.Expr(Test);
            Then = r.Expr(Then);
            Else = r.Expr(Else);
        }
    }

    public sealed class CallExpr : Expr
    {
        public Expr Callee;
        public List<Expr> Args = new List<Expr>();
        /// <summary>True for an optional call (callee?.(args))</summary>
        public bool Optional;
        public CallExpr(Expr callee, params Expr[] args)
        {
            Callee = callee;
            Args.AddRange(args);
        }
        public CallExpr(Expr callee, List<Expr> args)
        {
            Callee = callee;
            Args = args;
        }
        protected override void CopyLists() => Args = Copy(Args);
        public override void MapChildren(Rewriter r)
        {
            Callee = r.Expr(Callee);
            r.MapList(Args);
        }
    }

    public sealed class NewExpr : Expr
    {
        public Expr Callee;
        public List<Expr> Args = new List<Expr>();
        public NewExpr(Expr callee, params Expr[] args)
        {
            Callee = callee;
            Args.AddRange(args);
        }
        protected override void CopyLists() => Args = Copy(Args);
        public override void MapChildren(Rewriter r)
        {
            Callee = r.Expr(Callee);
            r.MapList(Args);
        }
    }

    public sealed class MemberExpr : Expr
    {
        public Expr Object;
        /// <summary>An Identifier (not computed), a PrivateName or any expression (computed)</summary>
        public Expr Property;
        public bool Computed;
        /// <summary>True for an optional member access (obj?.prop)</summary>
        public bool Optional;
        public MemberExpr(Expr obj, Expr property, bool computed = false)
        {
            Object = obj;
            Property = property;
            Computed = computed;
        }
        public MemberExpr(Expr obj, String name)
        {
            Object = obj;
            Property = new Identifier(name);
        }
        public override void MapChildren(Rewriter r)
        {
            Object = r.Expr(Object);
            Property = r.Expr(Property);
        }
    }

    /// <summary>
    /// Marks the extent of an optional chain (a?.b.c)
    /// </summary>
    public sealed class ChainExpr : Expr
    {
        public Expr Expression;
        public ChainExpr(Expr e) => Expression = e;
        public override void MapChildren(Rewriter r) => Expression = r.Expr(Expression);
    }

    public sealed class SequenceExpr : Expr
    {
        public List<Expr> Expressions = new List<Expr>();
        public SequenceExpr() { }
        public SequenceExpr(IEnumerable<Expr> e) => Expressions.AddRange(e);
        protected override void CopyLists() => Expressions = Copy(Expressions);
        public override void MapChildren(Rewriter r) => r.MapList(Expressions);
    }

    public sealed class SpreadElement : Expr
    {
        public Expr Arg;
        public SpreadElement(Expr arg) => Arg = arg;
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    public sealed class YieldExpr : Expr
    {
        public Expr Arg;
        public bool Delegate;
        public YieldExpr(Expr arg, bool @delegate = false)
        {
            Arg = arg;
            Delegate = @delegate;
        }
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    public sealed class AwaitExpr : Expr
    {
        public Expr Arg;
        public AwaitExpr(Expr arg) => Arg = arg;
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    /// <summary>
    /// new.target or import.meta
    /// </summary>
    public sealed class MetaProperty : Expr
    {
        public String Meta;
        public String Prop;
        public MetaProperty(String meta, String prop)
        {
            Meta = meta;
            Prop = prop;
        }
        public override void MapChildren(Rewriter r) { }
    }

    /// <summary>
    /// A dynamic import: import(source, options)
    /// </summary>
    public sealed class ImportCall : Expr
    {
        public Expr Source;
        public Expr Options;
        public override void MapChildren(Rewriter r)
        {
            Source = r.Expr(Source);
            Options = r.Expr(Options);
        }
    }

    public sealed class ArrayPattern : Expr
    {
        /// <summary>Patterns, null for holes, the last one may be a RestElement</summary>
        public List<Expr> Elements = new List<Expr>();
        protected override void CopyLists() => Elements = Copy(Elements);
        public override void MapChildren(Rewriter r) => r.MapList(Elements);
    }

    public sealed class ObjectPattern : Expr
    {
        /// <summary>Property (value is a pattern) or a RestElement (last)</summary>
        public List<Node> Properties = new List<Node>();
        protected override void CopyLists() => Properties = Copy(Properties);
        public override void MapChildren(Rewriter r) => r.MapList(Properties);
    }

    /// <summary>
    /// A pattern with a default value (left = right)
    /// </summary>
    public sealed class AssignPattern : Expr
    {
        public Expr Left;
        public Expr Right;
        public AssignPattern(Expr left, Expr right)
        {
            Left = left;
            Right = right;
        }
        public override void MapChildren(Rewriter r)
        {
            Left = r.Expr(Left);
            Right = r.Expr(Right);
        }
    }

    public sealed class RestElement : Expr
    {
        public Expr Arg;
        public RestElement(Expr arg) => Arg = arg;
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    #endregion//Expressions

    #region Statements

    /// <summary>
    /// A transient list of statements, used by rewriters to replace one statement with several (flattened into the parent list)
    /// </summary>
    public sealed class StatementList : Stmt
    {
        public List<Stmt> Body = new List<Stmt>();
        public StatementList() { }
        public StatementList(IEnumerable<Stmt> s) => Body.AddRange(s);
        protected override void CopyLists() => Body = Copy(Body);
        public override void MapChildren(Rewriter r) => r.MapStatements(Body);
    }

    public sealed class ProgramNode : Node
    {
        public List<Stmt> Body = new List<Stmt>();
        public bool IsModule;
        /// <summary>Comments after the last statement</summary>
        public List<Comment> TrailingComments;
        protected override void CopyLists() => Body = Copy(Body);
        public override void MapChildren(Rewriter r) => r.MapStatements(Body);
    }

    public sealed class BlockStmt : Stmt
    {
        public List<Stmt> Body = new List<Stmt>();
        /// <summary>Comments after the last statement</summary>
        public List<Comment> TrailingComments;
        public BlockStmt() { }
        public BlockStmt(IEnumerable<Stmt> body) => Body.AddRange(body);
        protected override void CopyLists() => Body = Copy(Body);
        public override void MapChildren(Rewriter r) => r.MapStatements(Body);
    }

    public sealed class ExprStmt : Stmt
    {
        public Expr Expression;
        /// <summary>If this is a directive ("use strict"), the raw directive text (including quotes)</summary>
        public String Directive;
        public ExprStmt(Expr e) => Expression = e;
        public override void MapChildren(Rewriter r) => Expression = r.Expr(Expression);
    }

    public sealed class EmptyStmt : Stmt
    {
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class DebuggerStmt : Stmt
    {
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class WithStmt : Stmt
    {
        public Expr Object;
        public Stmt Body;
        public override void MapChildren(Rewriter r)
        {
            Object = r.Expr(Object);
            Body = r.Stmt(Body);
        }
    }

    public sealed class ReturnStmt : Stmt
    {
        public Expr Arg;
        public ReturnStmt(Expr arg = null) => Arg = arg;
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    public sealed class LabeledStmt : Stmt
    {
        public String Label;
        public Stmt Body;
        public LabeledStmt(String label, Stmt body)
        {
            Label = label;
            Body = body;
        }
        public override void MapChildren(Rewriter r) => Body = r.Stmt(Body);
    }

    public sealed class BreakStmt : Stmt
    {
        public String Label;
        public BreakStmt(String label = null) => Label = label;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class ContinueStmt : Stmt
    {
        public String Label;
        public ContinueStmt(String label = null) => Label = label;
        public override void MapChildren(Rewriter r) { }
    }

    public sealed class IfStmt : Stmt
    {
        public Expr Test;
        public Stmt Then;
        public Stmt Else;
        public IfStmt(Expr test, Stmt then, Stmt @else = null)
        {
            Test = test;
            Then = then;
            Else = @else;
        }
        public override void MapChildren(Rewriter r)
        {
            Test = r.Expr(Test);
            Then = r.Stmt(Then);
            Else = r.Stmt(Else);
        }
    }

    public sealed class SwitchCase : Node
    {
        /// <summary>The test, null for default</summary>
        public Expr Test;
        public List<Stmt> Body = new List<Stmt>();
        protected override void CopyLists() => Body = Copy(Body);
        public override void MapChildren(Rewriter r)
        {
            Test = r.Expr(Test);
            r.MapStatements(Body);
        }
    }

    public sealed class SwitchStmt : Stmt
    {
        public Expr Discriminant;
        public List<SwitchCase> Cases = new List<SwitchCase>();
        protected override void CopyLists() => Cases = Copy(Cases);
        public override void MapChildren(Rewriter r)
        {
            Discriminant = r.Expr(Discriminant);
            r.MapList(Cases);
        }
    }

    public sealed class ThrowStmt : Stmt
    {
        public Expr Arg;
        public ThrowStmt(Expr arg) => Arg = arg;
        public override void MapChildren(Rewriter r) => Arg = r.Expr(Arg);
    }

    public sealed class CatchClause : Node
    {
        /// <summary>The catch parameter (pattern), null for an optional catch binding</summary>
        public Expr Param;
        public BlockStmt Body;
        public override void MapChildren(Rewriter r)
        {
            Param = r.Expr(Param);
            Body = r.Block(Body);
        }
    }

    public sealed class TryStmt : Stmt
    {
        public BlockStmt Block;
        public CatchClause Handler;
        public BlockStmt Finalizer;
        public override void MapChildren(Rewriter r)
        {
            Block = r.Block(Block);
            Handler = (CatchClause)r.Rewrite(Handler);
            Finalizer = r.Block(Finalizer);
        }
    }

    public sealed class WhileStmt : Stmt
    {
        public Expr Test;
        public Stmt Body;
        public WhileStmt(Expr test, Stmt body)
        {
            Test = test;
            Body = body;
        }
        public override void MapChildren(Rewriter r)
        {
            Test = r.Expr(Test);
            Body = r.Stmt(Body);
        }
    }

    public sealed class DoWhileStmt : Stmt
    {
        public Stmt Body;
        public Expr Test;
        public override void MapChildren(Rewriter r)
        {
            Body = r.Stmt(Body);
            Test = r.Expr(Test);
        }
    }

    public sealed class ForStmt : Stmt
    {
        /// <summary>A VarDecl, an expression or null</summary>
        public Node Init;
        public Expr Test;
        public Expr Update;
        public Stmt Body;
        public override void MapChildren(Rewriter r)
        {
            Init = r.Rewrite(Init);
            Test = r.Expr(Test);
            Update = r.Expr(Update);
            Body = r.Stmt(Body);
        }
    }

    public sealed class ForInStmt : Stmt
    {
        /// <summary>A VarDecl (with one declarator) or a pattern / member expression</summary>
        public Node Left;
        public Expr Right;
        public Stmt Body;
        public override void MapChildren(Rewriter r)
        {
            Left = r.Rewrite(Left);
            Right = r.Expr(Right);
            Body = r.Stmt(Body);
        }
    }

    public sealed class ForOfStmt : Stmt
    {
        /// <summary>A VarDecl (with one declarator) or a pattern / member expression</summary>
        public Node Left;
        public Expr Right;
        public Stmt Body;
        public bool Await;
        public override void MapChildren(Rewriter r)
        {
            Left = r.Rewrite(Left);
            Right = r.Expr(Right);
            Body = r.Stmt(Body);
        }
    }

    public sealed class FunctionDecl : Stmt
    {
        public Function F;
        public FunctionDecl(Function f) => F = f;
        public override void MapChildren(Rewriter r) => F = (Function)r.Rewrite(F);
    }

    public sealed class VarDeclarator : Node
    {
        /// <summary>The binding (Identifier or pattern)</summary>
        public Expr Id;
        public Expr Init;
        public VarDeclarator(Expr id, Expr init = null)
        {
            Id = id;
            Init = init;
        }
        public override void MapChildren(Rewriter r)
        {
            Id = r.Expr(Id);
            Init = r.Expr(Init);
        }
    }

    public sealed class VarDecl : Stmt
    {
        /// <summary>"var", "let", "const", "using" or "await using"</summary>
        public String Kind;
        public List<VarDeclarator> Decls = new List<VarDeclarator>();
        public VarDecl(String kind) => Kind = kind;
        public VarDecl(String kind, Expr id, Expr init = null)
        {
            Kind = kind;
            Decls.Add(new VarDeclarator(id, init));
        }
        protected override void CopyLists() => Decls = Copy(Decls);
        public override void MapChildren(Rewriter r) => r.MapList(Decls);
    }

    public sealed class ClassDecl : Stmt
    {
        public Class C;
        public ClassDecl(Class c) => C = c;
        public override void MapChildren(Rewriter r) => C = (Class)r.Rewrite(C);
    }

    public sealed class ImportAttr
    {
        public Expr Key;
        public StringLiteral Value;
    }

    public enum ImportSpecKind
    {
        Named,
        Default,
        Namespace,
    }

    public sealed class ImportSpec : Node
    {
        public ImportSpecKind Kind;
        /// <summary>The imported name (Identifier or StringLiteral), only for Named</summary>
        public Expr Imported;
        public Identifier Local;
        public override void MapChildren(Rewriter r)
        {
            Imported = r.Expr(Imported);
            Local = (Identifier)r.Expr(Local);
        }
    }

    public sealed class ImportDecl : Stmt
    {
        public List<ImportSpec> Specifiers = new List<ImportSpec>();
        public StringLiteral Source;
        public List<ImportAttr> Attributes;
        protected override void CopyLists()
        {
            Specifiers = Copy(Specifiers);
            Attributes = Copy(Attributes);
        }
        public override void MapChildren(Rewriter r) => r.MapList(Specifiers);
    }

    public sealed class ExportSpec : Node
    {
        public Expr Local;
        public Expr Exported;
        public override void MapChildren(Rewriter r)
        {
            Local = r.Expr(Local);
            Exported = r.Expr(Exported);
        }
    }

    public sealed class ExportNamedDecl : Stmt
    {
        public Stmt Declaration;
        public List<ExportSpec> Specifiers = new List<ExportSpec>();
        public StringLiteral Source;
        public List<ImportAttr> Attributes;
        protected override void CopyLists()
        {
            Specifiers = Copy(Specifiers);
            Attributes = Copy(Attributes);
        }
        public override void MapChildren(Rewriter r)
        {
            Declaration = r.Stmt(Declaration);
            r.MapList(Specifiers);
        }
    }

    public sealed class ExportDefaultDecl : Stmt
    {
        /// <summary>A FunctionDecl, ClassDecl (id may be null) or an ExprStmt</summary>
        public Stmt Declaration;
        public override void MapChildren(Rewriter r) => Declaration = r.Stmt(Declaration);
    }

    public sealed class ExportAllDecl : Stmt
    {
        public Expr Exported;
        public StringLiteral Source;
        public List<ImportAttr> Attributes;
        protected override void CopyLists() => Attributes = Copy(Attributes);
        public override void MapChildren(Rewriter r) => Exported = r.Expr(Exported);
    }

    #endregion//Statements
}
