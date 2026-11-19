using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Output
{
    /// <summary>
    /// Writes a syntax tree as JavaScript source code
    /// </summary>
    sealed class JsWriter
    {
        /// <summary>
        /// Writer options
        /// </summary>
        public sealed class Options
        {
            public bool Minify;
            public String IndentUnit = "  ";
            public bool AsciiOnly;
            /// <summary>Escape "&lt;/script" and "&lt;!--" so that the output can be embedded in an html script element</summary>
            public bool HtmlSafe;
            public JsComments Comments = JsComments.All;
        }

        readonly StringBuilder O = new StringBuilder();
        readonly Options Opt;
        readonly bool Min;
        int Level;
        bool PendingIndent;
        /// <summary>Set while writing the init part of a for statement (the in operator must be parenthesized)</summary>
        bool NoIn;

        public JsWriter(Options opt)
        {
            Opt = opt ?? new Options();
            Min = Opt.Minify;
        }

        public override String ToString() => O.ToString();

        /// <summary>
        /// Write a program
        /// </summary>
        public static String Write(ProgramNode p, Options opt)
        {
            var w = new JsWriter(opt);
            w.Program(p);
            return w.ToString();
        }

        /// <summary>
        /// Write a single expression (debugging / tests)
        /// </summary>
        public static String WriteExpr(Expr e, Options opt = null)
        {
            var w = new JsWriter(opt);
            w.Expr(e, 0);
            return w.ToString();
        }

        #region Low level output

        static bool IsIdChar(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '$' || c == '\\' || c > 127;

        char Last => O.Length == 0 ? '\0' : O[O.Length - 1];

        /// <summary>
        /// Write a token, a space is inserted if required to separate it from the previous token
        /// </summary>
        void W(String s)
        {
            if (String.IsNullOrEmpty(s))
                return;
            if (PendingIndent)
            {
                PendingIndent = false;
                for (int i = 0; i < Level; ++i)
                    O.Append(Opt.IndentUnit);
            }
            else if (O.Length > 0)
            {
                var a = Last;
                var b = s[0];
                if ((IsIdChar(a) && IsIdChar(b))
                    || (a == '+' && b == '+')
                    || (a == '-' && b == '-')
                    || (a == '/' && (b == '/' || b == '*'))
                    || (a == '<' && b == '!')
                    || (a == '-' && b == '>'))
                    O.Append(' ');
            }
            O.Append(s);
        }

        void W(char c) => W(c.ToString());

        /// <summary>
        /// Write any pending indentation
        /// </summary>
        void Flush()
        {
            if (!PendingIndent)
                return;
            PendingIndent = false;
            for (int i = 0; i < Level; ++i)
                O.Append(Opt.IndentUnit);
        }

        /// <summary>
        /// A space (not when minifying)
        /// </summary>
        void S()
        {
            if (!Min)
                O.Append(' ');
        }

        /// <summary>
        /// A new line (not when minifying)
        /// </summary>
        void NL()
        {
            if (Min)
                return;
            //  Trim trailing spaces
            while (O.Length > 0 && O[O.Length - 1] == ' ')
                O.Length--;
            O.Append('\n');
            PendingIndent = true;
        }

        /// <summary>
        /// A forced new line (also when minifying, used after line comments)
        /// </summary>
        void ForceNL()
        {
            O.Append('\n');
            PendingIndent = !Min;
        }

        #endregion//Low level output

        #region Comments

        bool KeepComment(Comment c) => Opt.Comments switch
        {
            JsComments.All => true,
            JsComments.License => c.IsLicense,
            _ => false,
        };

        void Comments(List<Comment> list)
        {
            if (list == null || Opt.Comments == JsComments.None)
                return;
            foreach (var c in list)
            {
                if (!KeepComment(c))
                    continue;
                var t = c.Text;
                if (Opt.HtmlSafe)
                    t = t.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
                if (c.IsBlock)
                {
                    W("/*" + t.Replace("*/", "* /") + "*/");
                    if (Min)
                        continue;
                    NL();
                }
                else
                {
                    W("//" + t.Replace('\n', ' ').Replace('\r', ' '));
                    ForceNL();
                }
            }
        }

        #endregion//Comments

        #region Statements

        void Program(ProgramNode p)
        {
            bool first = true;
            foreach (var s in p.Body)
            {
                if (!first)
                    NL();
                first = false;
                Statement(s);
            }
            if (p.TrailingComments != null && Opt.Comments != JsComments.None)
            {
                if (!first)
                    NL();
                Comments(p.TrailingComments);
            }
            if (!Min && O.Length > 0 && Last != '\n')
                O.Append('\n');
        }

        void StatementsBlock(List<Stmt> body, List<Comment> trailing = null)
        {
            W('{');
            bool hasTrailing = trailing != null && Opt.Comments != JsComments.None && trailing.Exists(KeepComment);
            if (body.Count == 0 && !hasTrailing)
            {
                W('}');
                return;
            }
            ++Level;
            foreach (var s in body)
            {
                NL();
                Statement(s);
            }
            if (hasTrailing)
            {
                NL();
                Comments(trailing);
            }
            --Level;
            NL();
            W('}');
        }

        void Block(BlockStmt b) => StatementsBlock(b.Body, b.TrailingComments);

        /// <summary>
        /// The body of a control statement (if, for, while ...)
        /// </summary>
        void Body(Stmt s)
        {
            if (s is BlockStmt b)
            {
                S();
                Block(b);
                return;
            }
            if (s is EmptyStmt)
            {
                W(';');
                return;
            }
            if (s is StatementList sl)
            {
                S();
                StatementsBlock(sl.Body);
                return;
            }
            S();
            Statement(s);
        }

        void Statement(Stmt s)
        {
            Comments(s.LeadingComments);
            switch (s)
            {
                case ExprStmt e:
                    if (e.Directive != null)
                    {
                        W(e.Directive);
                        W(';');
                        return;
                    }
                    ExprStatementStart(e.Expression);
                    W(';');
                    return;
                case BlockStmt b:
                    Block(b);
                    return;
                case StatementList sl:
                    {
                        bool first = true;
                        foreach (var x in sl.Body)
                        {
                            if (!first)
                                NL();
                            first = false;
                            Statement(x);
                        }
                        return;
                    }
                case EmptyStmt:
                    W(';');
                    return;
                case DebuggerStmt:
                    W("debugger");
                    W(';');
                    return;
                case VarDecl v:
                    VarDecl(v);
                    W(';');
                    return;
                case FunctionDecl fd:
                    Function(fd.F, false);
                    return;
                case ClassDecl cd:
                    Class(cd.C);
                    return;
                case ReturnStmt r:
                    W("return");
                    if (r.Arg != null)
                    {
                        S();
                        Expr(r.Arg, 0);
                    }
                    W(';');
                    return;
                case IfStmt i:
                    {
                        W("if");
                        S();
                        W('(');
                        Expr(i.Test, 0);
                        W(')');
                        var then = i.Then;
                        if (i.Else != null && then is not BlockStmt)
                            then = new BlockStmt { Body = { then } };
                        Body(then);
                        if (i.Else != null)
                        {
                            if (then is BlockStmt)
                                S();
                            else
                                NL();
                            W("else");
                            if (i.Else is IfStmt)
                            {
                                O.Append(' ');
                                Statement(i.Else);
                            }
                            else
                                Body(i.Else);
                        }
                        return;
                    }
                case ForStmt f:
                    W("for");
                    S();
                    W('(');
                    if (f.Init != null)
                    {
                        var old = NoIn;
                        NoIn = true;
                        if (f.Init is VarDecl vd)
                            VarDecl(vd);
                        else
                            Expr((Expr)f.Init, 0);
                        NoIn = old;
                    }
                    W(';');
                    if (f.Test != null)
                    {
                        S();
                        Expr(f.Test, 0);
                    }
                    W(';');
                    if (f.Update != null)
                    {
                        S();
                        Expr(f.Update, 0);
                    }
                    W(')');
                    Body(f.Body);
                    return;
                case ForInStmt f:
                    W("for");
                    S();
                    W('(');
                    ForLeft(f.Left, false);
                    W("in");
                    S();
                    Expr(f.Right, 0);
                    W(')');
                    Body(f.Body);
                    return;
                case ForOfStmt f:
                    W("for");
                    if (f.Await)
                        W("await");
                    S();
                    W('(');
                    ForLeft(f.Left, true);
                    W("of");
                    S();
                    Expr(f.Right, P_ASSIGN);
                    W(')');
                    Body(f.Body);
                    return;
                case WhileStmt w:
                    W("while");
                    S();
                    W('(');
                    Expr(w.Test, 0);
                    W(')');
                    Body(w.Body);
                    return;
                case DoWhileStmt d:
                    W("do");
                    if (d.Body is BlockStmt)
                        Body(d.Body);
                    else
                    {
                        O.Append(' ');
                        Statement(d.Body);
                    }
                    S();
                    W("while");
                    S();
                    W('(');
                    Expr(d.Test, 0);
                    W(')');
                    W(';');
                    return;
                case SwitchStmt sw:
                    W("switch");
                    S();
                    W('(');
                    Expr(sw.Discriminant, 0);
                    W(')');
                    S();
                    W('{');
                    ++Level;
                    foreach (var c in sw.Cases)
                    {
                        NL();
                        Comments(c.LeadingComments);
                        if (c.Test != null)
                        {
                            W("case");
                            S();
                            Expr(c.Test, 0);
                        }
                        else
                            W("default");
                        W(':');
                        ++Level;
                        foreach (var x in c.Body)
                        {
                            NL();
                            Statement(x);
                        }
                        --Level;
                    }
                    --Level;
                    NL();
                    W('}');
                    return;
                case ThrowStmt t:
                    W("throw");
                    S();
                    Expr(t.Arg, 0);
                    W(';');
                    return;
                case TryStmt t:
                    W("try");
                    S();
                    Block(t.Block);
                    if (t.Handler != null)
                    {
                        S();
                        W("catch");
                        S();
                        if (t.Handler.Param != null)
                        {
                            W('(');
                            Pattern(t.Handler.Param);
                            W(')');
                            S();
                        }
                        Block(t.Handler.Body);
                    }
                    if (t.Finalizer != null)
                    {
                        S();
                        W("finally");
                        S();
                        Block(t.Finalizer);
                    }
                    return;
                case LabeledStmt l:
                    W(l.Label);
                    W(':');
                    Body(l.Body);
                    return;
                case BreakStmt b:
                    W("break");
                    if (b.Label != null)
                        W(b.Label);
                    W(';');
                    return;
                case ContinueStmt c:
                    W("continue");
                    if (c.Label != null)
                        W(c.Label);
                    W(';');
                    return;
                case WithStmt w:
                    W("with");
                    S();
                    W('(');
                    Expr(w.Object, 0);
                    W(')');
                    Body(w.Body);
                    return;
                case ImportDecl im:
                    Import(im);
                    return;
                case ExportNamedDecl en:
                    ExportNamed(en);
                    return;
                case ExportDefaultDecl ed:
                    W("export");
                    W("default");
                    S();
                    if (ed.Declaration is ExprStmt es)
                    {
                        var start = O.Length;
                        Expr(es.Expression, P_ASSIGN);
                        if (StartsWithKeyword(start, "function") || StartsWithKeyword(start, "class") || StartsWithKeyword(start, "async"))
                        {
                            O.Insert(start, '(');
                            O.Append(')');
                        }
                        W(';');
                    }
                    else
                        Statement(ed.Declaration);
                    return;
                case ExportAllDecl ea:
                    W("export");
                    S();
                    W('*');
                    if (ea.Exported != null)
                    {
                        S();
                        W("as");
                        ModuleName(ea.Exported);
                    }
                    S();
                    W("from");
                    S();
                    Literal(ea.Source);
                    ImportAttributes(ea.Attributes);
                    W(';');
                    return;
            }
            throw new NotSupportedException("Can't write a " + s.GetType().Name);
        }

        void ForLeft(Node left, bool isOf)
        {
            if (left is VarDecl vd)
                VarDecl(vd);
            else
            {
                var e = (Expr)left;
                //  for (async of x) is not allowed, for ((let) in x) isn't valid either
                if (e is Identifier { Name: "async" or "let" })
                {
                    W('(');
                    Expr(e, 0);
                    W(')');
                }
                else
                    Pattern(e);
            }
            O.Append(' ');
        }

        void VarDecl(VarDecl v)
        {
            W(v.Kind);
            O.Append(' ');
            bool first = true;
            foreach (var d in v.Decls)
            {
                if (!first)
                {
                    W(',');
                    S();
                }
                first = false;
                Pattern(d.Id);
                if (d.Init != null)
                {
                    S();
                    W('=');
                    S();
                    Expr(d.Init, P_ASSIGN);
                }
            }
        }

        bool StartsWithKeyword(int start, String kw)
        {
            var l = kw.Length;
            if (O.Length - start < l)
                return false;
            for (int i = 0; i < l; ++i)
                if (O[start + i] != kw[i])
                    return false;
            if (O.Length - start == l)
                return true;
            return !IsIdChar(O[start + l]);
        }

        /// <summary>
        /// Write an expression at the start of an expression statement (parenthesized if it would be parsed as something else)
        /// </summary>
        void ExprStatementStart(Expr e)
        {
            Flush();
            var start = O.Length;
            Expr(e, 0);
            if (start < O.Length && O[start] == ' ')
                ++start;
            bool wrap = false;
            if (start < O.Length)
            {
                var c = O[start];
                //  A string literal would become a directive
                if (c == '{' || e is StringLiteral)
                    wrap = true;
                else if (StartsWithKeyword(start, "function") || StartsWithKeyword(start, "class"))
                    wrap = true;
                else if (StartsWithKeyword(start, "async"))
                {
                    //  async function
                    var p = start + 5;
                    while (p < O.Length && O[p] == ' ')
                        ++p;
                    if (StartsWithKeyword(p, "function"))
                        wrap = true;
                }
                else if (StartsWithKeyword(start, "let"))
                {
                    var p = start + 3;
                    while (p < O.Length && O[p] == ' ')
                        ++p;
                    if (p < O.Length && O[p] == '[')
                        wrap = true;
                }
            }
            if (wrap)
            {
                O.Insert(start, '(');
                O.Append(')');
            }
        }

        void Import(ImportDecl im)
        {
            W("import");
            S();
            if (im.Specifiers.Count > 0)
            {
                bool first = true;
                bool inBraces = false;
                foreach (var s in im.Specifiers)
                {
                    if (s.Kind == ImportSpecKind.Named && !inBraces)
                    {
                        if (!first)
                        {
                            W(',');
                            S();
                        }
                        W('{');
                        S();
                        inBraces = true;
                        first = true;
                    }
                    if (!first)
                    {
                        W(',');
                        S();
                    }
                    first = false;
                    switch (s.Kind)
                    {
                        case ImportSpecKind.Default:
                            Ident(s.Local.Name);
                            break;
                        case ImportSpecKind.Namespace:
                            W('*');
                            S();
                            W("as");
                            Ident(s.Local.Name);
                            break;
                        default:
                            ModuleName(s.Imported);
                            if (!(s.Imported is Identifier ii && ii.Name == s.Local.Name))
                            {
                                W("as");
                                Ident(s.Local.Name);
                            }
                            break;
                    }
                }
                if (inBraces)
                {
                    S();
                    W('}');
                }
                S();
                W("from");
                S();
            }
            Literal(im.Source);
            ImportAttributes(im.Attributes);
            W(';');
        }

        void ModuleName(Expr e)
        {
            if (e is Identifier i)
                Ident(i.Name);
            else
                Literal(e);
        }

        void ImportAttributes(List<ImportAttr> attrs)
        {
            if (attrs == null || attrs.Count == 0)
                return;
            S();
            W("with");
            S();
            W('{');
            bool first = true;
            foreach (var a in attrs)
            {
                if (!first)
                    W(',');
                first = false;
                S();
                PropertyKey(a.Key, false);
                W(':');
                S();
                Literal(a.Value);
            }
            S();
            W('}');
        }

        void ExportNamed(ExportNamedDecl en)
        {
            W("export");
            if (en.Declaration != null)
            {
                O.Append(' ');
                Statement(en.Declaration);
                return;
            }
            S();
            W('{');
            bool first = true;
            foreach (var s in en.Specifiers)
            {
                if (!first)
                    W(',');
                first = false;
                S();
                ModuleName(s.Local);
                if (!(s.Local is Identifier l && s.Exported is Identifier x && l.Name == x.Name))
                {
                    W("as");
                    ModuleName(s.Exported);
                }
            }
            S();
            W('}');
            if (en.Source != null)
            {
                S();
                W("from");
                S();
                Literal(en.Source);
                ImportAttributes(en.Attributes);
            }
            W(';');
        }

        #endregion//Statements

        #region Functions and classes

        void Params(List<Expr> ps)
        {
            W('(');
            bool first = true;
            var old = NoIn;
            NoIn = false;
            foreach (var p in ps)
            {
                if (!first)
                {
                    W(',');
                    S();
                }
                first = false;
                Pattern(p);
            }
            NoIn = old;
            W(')');
        }

        void FunctionBody(BlockStmt b)
        {
            var old = NoIn;
            NoIn = false;
            S();
            Block(b);
            NoIn = old;
        }

        /// <summary>
        /// Write a function declaration or expression (not methods)
        /// </summary>
        void Function(Function f, bool isExpr)
        {
            if (f.IsArrow)
            {
                if (f.IsAsync)
                {
                    W("async");
                    S();
                }
                Params(f.Params);
                S();
                W("=>");
                if (f.Body != null)
                {
                    FunctionBody(f.Body);
                    return;
                }
                S();
                var old = NoIn;
                NoIn = false;
                var start = O.Length;
                Expr(f.ExprBody, P_ASSIGN);
                if (start < O.Length && O[start] == '{')
                {
                    O.Insert(start, '(');
                    O.Append(')');
                }
                NoIn = old;
                return;
            }
            if (f.IsAsync)
                W("async");
            W("function");
            if (f.IsGenerator)
                W('*');
            if (f.Id != null)
            {
                if (f.IsGenerator)
                    S();
                Ident(f.Id.Name);
            }
            else if (!f.IsGenerator)
                S();
            Params(f.Params);
            FunctionBody(f.Body);
        }

        /// <summary>
        /// Write a method (class or object), the key and the function
        /// </summary>
        void Method(String kind, bool isStatic, Expr key, bool computed, Function f)
        {
            if (isStatic)
                W("static");
            if (kind == "get" || kind == "set")
                W(kind);
            if (f.IsAsync)
                W("async");
            if (f.IsGenerator)
                W('*');
            PropertyKey(key, computed);
            Params(f.Params);
            FunctionBody(f.Body);
        }

        void Class(Class c)
        {
            W("class");
            if (c.Id != null)
                Ident(c.Id.Name);
            if (c.SuperClass != null)
            {
                W("extends");
                S();
                Expr(c.SuperClass, P_LHS);
            }
            S();
            W('{');
            if (c.Members.Count == 0)
            {
                W('}');
                return;
            }
            ++Level;
            var old = NoIn;
            NoIn = false;
            foreach (var m in c.Members)
            {
                NL();
                Comments(m.LeadingComments);
                switch (m)
                {
                    case MethodDef md:
                        Method(md.Kind, md.Static, md.Key, md.Computed, md.Value);
                        break;
                    case FieldDef fd:
                        if (fd.Static)
                            W("static");
                        PropertyKey(fd.Key, fd.Computed);
                        if (fd.Value != null)
                        {
                            S();
                            W('=');
                            S();
                            Expr(fd.Value, P_ASSIGN);
                        }
                        W(';');
                        break;
                    case StaticBlock sb:
                        W("static");
                        S();
                        StatementsBlock(sb.Body);
                        break;
                    default:
                        throw new NotSupportedException("Can't write a class member of type " + m.GetType().Name);
                }
            }
            NoIn = old;
            --Level;
            NL();
            W('}');
        }

        #endregion//Functions and classes

        #region Expressions

        const int P_SEQ = 0;
        const int P_ASSIGN = 1;
        const int P_COND = 2;
        const int P_OR = 3;
        const int P_AND = 4;
        const int P_BOR = 5;
        const int P_BXOR = 6;
        const int P_BAND = 7;
        const int P_EQ = 8;
        const int P_REL = 9;
        const int P_SHIFT = 10;
        const int P_ADD = 11;
        const int P_MUL = 12;
        const int P_EXP = 13;
        const int P_UNARY = 14;
        const int P_POSTFIX = 15;
        const int P_LHS = 16;
        const int P_PRIMARY = 17;

        static int BinPrec(String op) => op switch
        {
            "??" or "||" => P_OR,
            "&&" => P_AND,
            "|" => P_BOR,
            "^" => P_BXOR,
            "&" => P_BAND,
            "==" or "!=" or "===" or "!==" => P_EQ,
            "<" or ">" or "<=" or ">=" or "in" or "instanceof" => P_REL,
            "<<" or ">>" or ">>>" => P_SHIFT,
            "+" or "-" => P_ADD,
            "*" or "/" or "%" => P_MUL,
            "**" => P_EXP,
            _ => throw new NotSupportedException("Unknown binary operator " + op),
        };

        static int Prec(Expr e) => e switch
        {
            SequenceExpr => P_SEQ,
            AssignExpr or YieldExpr or SpreadElement => P_ASSIGN,
            FunctionExpr { F.IsArrow: true } => P_ASSIGN,
            ConditionalExpr => P_COND,
            BinaryExpr b => BinPrec(b.Op),
            UnaryExpr or AwaitExpr => P_UNARY,
            UpdateExpr u => u.Prefix ? P_UNARY : P_POSTFIX,
            CallExpr or NewExpr or MemberExpr or ChainExpr or TaggedTemplate or ImportCall => P_LHS,
            NumberLiteral n => (n.Value < 0 || (n.Value == 0 && double.IsNegative(n.Value))) ? P_UNARY : P_PRIMARY,
            _ => P_PRIMARY,
        };

        /// <summary>
        /// True if the expression is (or starts with) a call in its member chain (new (a().b)() requires parens)
        /// </summary>
        static bool HasCallInChain(Expr e)
        {
            while (true)
            {
                switch (e)
                {
                    case CallExpr:
                    case ImportCall:
                        return true;
                    case MemberExpr m:
                        e = m.Object;
                        continue;
                    case TaggedTemplate t:
                        e = t.Tag;
                        continue;
                    default:
                        return false;
                }
            }
        }

        void Expr(Expr e, int minPrec, bool forceParen = false)
        {
            var p = Prec(e);
            bool paren = forceParen || p < minPrec || (NoIn && e is BinaryExpr { Op: "in" });
            if (paren)
            {
                W('(');
                var old = NoIn;
                NoIn = false;
                ExprInner(e);
                NoIn = old;
                W(')');
                return;
            }
            ExprInner(e);
        }

        void ExprInner(Expr e)
        {
            switch (e)
            {
                case Identifier i:
                    Ident(i.Name);
                    return;
                case PrivateName pn:
                    W("#" + EscapeIdent(pn.Name));
                    return;
                case NumberLiteral:
                case StringLiteral:
                case BooleanLiteral:
                case NullLiteral:
                case RegexLiteral:
                case BigIntLiteral:
                    Literal(e);
                    return;
                case ThisExpr:
                    W("this");
                    return;
                case SuperExpr:
                    W("super");
                    return;
                case TemplateLiteral t:
                    Template(t);
                    return;
                case TaggedTemplate tt:
                    Expr(tt.Tag, P_LHS, tt.Tag is ChainExpr);
                    Template(tt.Quasi);
                    return;
                case ArrayExpr a:
                    ArrayElements(a.Elements, false);
                    return;
                case ArrayPattern ap:
                    ArrayElements(ap.Elements, true);
                    return;
                case ObjectExpr o:
                    ObjectLiteral(o.Properties, false);
                    return;
                case ObjectPattern op:
                    ObjectLiteral(op.Properties, true);
                    return;
                case FunctionExpr f:
                    Function(f.F, true);
                    return;
                case ClassExpr c:
                    Class(c.C);
                    return;
                case UnaryExpr u:
                    W(u.Op);
                    Expr(u.Arg, P_UNARY);
                    return;
                case UpdateExpr u:
                    if (u.Prefix)
                    {
                        W(u.Op);
                        Expr(u.Arg, P_UNARY);
                    }
                    else
                    {
                        Expr(u.Arg, P_LHS);
                        W(u.Op);
                    }
                    return;
                case BinaryExpr b:
                    Binary(b);
                    return;
                case AssignExpr a:
                    if (a.Target is ObjectPattern || a.Target is ArrayPattern)
                        Pattern(a.Target);
                    else
                        Expr(a.Target, P_LHS);
                    S();
                    W(a.Op);
                    S();
                    Expr(a.Value, P_ASSIGN);
                    return;
                case ConditionalExpr c:
                    Expr(c.Test, P_OR);
                    S();
                    W('?');
                    S();
                    Expr(c.Then, P_ASSIGN);
                    S();
                    W(':');
                    S();
                    Expr(c.Else, P_ASSIGN);
                    return;
                case CallExpr c:
                    Expr(c.Callee, P_LHS, c.Callee is ChainExpr);
                    if (c.Optional)
                        W("?.");
                    Args(c.Args);
                    return;
                case NewExpr n:
                    W("new");
                    S();
                    Expr(n.Callee, P_LHS, n.Callee is ChainExpr || HasCallInChain(n.Callee));
                    Args(n.Args);
                    return;
                case MemberExpr m:
                    Member(m);
                    return;
                case ChainExpr ch:
                    Expr(ch.Expression, P_LHS);
                    return;
                case SequenceExpr s:
                    {
                        bool first = true;
                        foreach (var x in s.Expressions)
                        {
                            if (!first)
                            {
                                W(',');
                                S();
                            }
                            first = false;
                            Expr(x, P_ASSIGN);
                        }
                        return;
                    }
                case SpreadElement sp:
                    W("...");
                    Expr(sp.Arg, P_ASSIGN);
                    return;
                case RestElement r:
                    W("...");
                    Pattern(r.Arg);
                    return;
                case AssignPattern ap:
                    Pattern(ap.Left);
                    S();
                    W('=');
                    S();
                    Expr(ap.Right, P_ASSIGN);
                    return;
                case YieldExpr y:
                    W("yield");
                    if (y.Delegate)
                        W('*');
                    if (y.Arg != null)
                    {
                        S();
                        Expr(y.Arg, P_ASSIGN);
                    }
                    return;
                case AwaitExpr a:
                    W("await");
                    S();
                    Expr(a.Arg, P_UNARY);
                    return;
                case MetaProperty mp:
                    W(mp.Meta);
                    W('.');
                    W(mp.Prop);
                    return;
                case ImportCall ic:
                    W("import");
                    W('(');
                    Expr(ic.Source, P_ASSIGN);
                    if (ic.Options != null)
                    {
                        W(',');
                        S();
                        Expr(ic.Options, P_ASSIGN);
                    }
                    W(')');
                    return;
            }
            throw new NotSupportedException("Can't write a " + e.GetType().Name);
        }

        void Binary(BinaryExpr b)
        {
            var p = BinPrec(b.Op);
            var op = b.Op;
            if (op == "**")
            {
                Expr(b.Left, p + 1, b.Left is UnaryExpr || b.Left is AwaitExpr);
                S();
                W(op);
                S();
                Expr(b.Right, p);
                return;
            }
            bool isCoalesce = op == "??";
            bool isLogical = op == "&&" || op == "||";
            bool MixParen(Expr x) => x is BinaryExpr bx && ((isCoalesce && (bx.Op == "&&" || bx.Op == "||")) || (isLogical && bx.Op == "??"));
            Expr(b.Left, p, MixParen(b.Left));
            if (op == "in" || op == "instanceof")
                O.Append(' ');
            else
                S();
            W(op);
            if (op == "in" || op == "instanceof")
                O.Append(' ');
            else
                S();
            Expr(b.Right, p + 1, MixParen(b.Right));
        }

        void Member(MemberExpr m)
        {
            var obj = m.Object;
            bool force = obj is ChainExpr;
            if (!force && obj is NumberLiteral nl && !m.Computed)
            {
                var s = Number(nl.Value);
                if (s.IndexOfAny(['.', 'e', 'x', 'E']) < 0)
                    force = true;
            }
            Expr(obj, P_LHS, force);
            if (m.Computed)
            {
                if (m.Optional)
                    W("?.");
                W('[');
                var old = NoIn;
                NoIn = false;
                Expr(m.Property, P_SEQ);
                NoIn = old;
                W(']');
                return;
            }
            W(m.Optional ? "?." : ".");
            if (m.Property is PrivateName pn)
                W("#" + EscapeIdent(pn.Name));
            else
                O.Append(EscapeIdent(((Identifier)m.Property).Name));
        }

        void Args(List<Expr> args)
        {
            W('(');
            var old = NoIn;
            NoIn = false;
            bool first = true;
            foreach (var a in args)
            {
                if (!first)
                {
                    W(',');
                    S();
                }
                first = false;
                Expr(a, P_ASSIGN);
            }
            NoIn = old;
            W(')');
        }

        void ArrayElements(List<Expr> elements, bool isPattern)
        {
            W('[');
            var old = NoIn;
            NoIn = false;
            var c = elements.Count;
            for (int i = 0; i < c; ++i)
            {
                if (i > 0)
                {
                    W(',');
                    if (elements[i] != null)
                        S();
                }
                var x = elements[i];
                if (x == null)
                {
                    if (i == c - 1)
                        W(',');
                    continue;
                }
                if (isPattern)
                    Pattern(x);
                else
                    Expr(x, P_ASSIGN);
            }
            NoIn = old;
            W(']');
        }

        static bool IsComplexValue(Node n) => n switch
        {
            Property p => p.Value is FunctionExpr || p.Value is ClassExpr || (p.Value is ObjectExpr o && o.Properties.Count > 0) || p.Kind != PropKind.Init || p.Method,
            SpreadElement s => s.Arg is ObjectExpr o2 && o2.Properties.Count > 0,
            _ => false,
        };

        void ObjectLiteral(List<Node> props, bool isPattern)
        {
            W('{');
            if (props.Count == 0)
            {
                W('}');
                return;
            }
            var old = NoIn;
            NoIn = false;
            bool multi = !Min && !isPattern && (props.Count > 6 || props.Exists(IsComplexValue));
            if (multi)
                ++Level;
            bool first = true;
            foreach (var p in props)
            {
                if (!first)
                    W(',');
                first = false;
                if (multi)
                    NL();
                else
                    S();
                switch (p)
                {
                    case Property pr:
                        ObjectProperty(pr, isPattern);
                        break;
                    case SpreadElement se:
                        W("...");
                        Expr(se.Arg, P_ASSIGN);
                        break;
                    case RestElement re:
                        W("...");
                        Pattern(re.Arg);
                        break;
                    default:
                        throw new NotSupportedException("Can't write a " + p.GetType().Name + " in an object");
                }
            }
            if (multi)
            {
                --Level;
                NL();
            }
            else
                S();
            NoIn = old;
            W('}');
        }

        void ObjectProperty(Property p, bool isPattern)
        {
            if (!isPattern && (p.Kind != PropKind.Init || p.Method) && p.Value is FunctionExpr fe)
            {
                Method(p.Kind == PropKind.Get ? "get" : p.Kind == PropKind.Set ? "set" : "method", false, p.Key, p.Computed, fe.F);
                return;
            }
            if (p.Shorthand && !p.Computed && p.Key is Identifier k)
            {
                if (p.Value is Identifier v && v.Name == k.Name)
                {
                    Ident(k.Name);
                    return;
                }
                if (isPattern && p.Value is AssignPattern ap && ap.Left is Identifier l && l.Name == k.Name)
                {
                    Pattern(ap);
                    return;
                }
            }
            PropertyKey(p.Key, p.Computed);
            W(':');
            S();
            if (isPattern)
                Pattern(p.Value);
            else
                Expr(p.Value, P_ASSIGN);
        }

        void PropertyKey(Expr key, bool computed)
        {
            if (computed)
            {
                W('[');
                Expr(key, P_ASSIGN);
                W(']');
                return;
            }
            switch (key)
            {
                case Identifier i:
                    W(EscapeIdent(i.Name));
                    return;
                case PrivateName pn:
                    W("#" + EscapeIdent(pn.Name));
                    return;
                default:
                    Literal(key);
                    return;
            }
        }

        /// <summary>
        /// Write a pattern (binding or assignment target)
        /// </summary>
        void Pattern(Expr p)
        {
            switch (p)
            {
                case Identifier i:
                    Ident(i.Name);
                    return;
                case ArrayPattern:
                case ObjectPattern:
                case AssignPattern:
                case RestElement:
                    ExprInner(p);
                    return;
                default:
                    Expr(p, P_LHS);
                    return;
            }
        }

        void Template(TemplateLiteral t)
        {
            W('`');
            var c = t.Quasis.Count;
            var old = NoIn;
            NoIn = false;
            for (int i = 0; i < c; ++i)
            {
                O.Append(EscapeTemplateRaw(t.Quasis[i].Raw));
                if (i < t.Expressions.Count)
                {
                    O.Append("${");
                    Expr(t.Expressions[i], P_SEQ);
                    O.Append('}');
                }
            }
            NoIn = old;
            O.Append('`');
        }

        #endregion//Expressions

        #region Literals

        void Ident(String name) => W(EscapeIdent(name));

        String EscapeIdent(String name)
        {
            if (!Opt.AsciiOnly)
                return name;
            StringBuilder sb = null;
            for (int i = 0; i < name.Length; ++i)
            {
                var c = name[i];
                if (c < 128)
                {
                    sb?.Append(c);
                    continue;
                }
                if (sb == null)
                {
                    sb = new StringBuilder();
                    sb.Append(name, 0, i);
                }
                if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
                {
                    sb.Append("\\u{").Append(char.ConvertToUtf32(c, name[i + 1]).ToString("X", CultureInfo.InvariantCulture)).Append('}');
                    ++i;
                    continue;
                }
                sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            return sb?.ToString() ?? name;
        }

        String EscapeTemplateRaw(String raw)
        {
            if (Opt.HtmlSafe)
                raw = raw.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase).Replace("<!--", "<\\!--", StringComparison.Ordinal);
            if (!Opt.AsciiOnly)
                return raw;
            var sb = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c < 128)
                    sb.Append(c);
                else
                    sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Format a number as a JavaScript numeric literal
        /// </summary>
        public static String Number(double v)
        {
            if (double.IsNaN(v))
                return "NaN";
            if (double.IsPositiveInfinity(v))
                return "1e999";
            if (double.IsNegativeInfinity(v))
                return "-1e999";
            if (v == 0)
                return double.IsNegative(v) ? "-0" : "0";
            if (v == Math.Floor(v) && Math.Abs(v) < 9007199254740992.0)
                return ((long)v).ToString(CultureInfo.InvariantCulture);
            //  The shortest round trip digits, formatted like JavaScript's Number.prototype.toString
            var s = v.ToString("R", CultureInfo.InvariantCulture);
            var e = s.IndexOfAny(['E', 'e']);
            if (e < 0)
                return s;
            var mant = s.Substring(0, e);
            var exp = int.Parse(s.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            bool neg = mant.StartsWith('-');
            if (neg)
                mant = mant.Substring(1);
            var dot = mant.IndexOf('.');
            var digits = dot < 0 ? mant : mant.Remove(dot, 1);
            var pointPos = (dot < 0 ? mant.Length : dot) + exp;
            //  Integers below 1e21 are written without exponent (ex: 123456789012345680000)
            if (exp > 0 && pointPos <= 21 && digits.Length <= pointPos)
                return (neg ? "-" : "") + digits + new String('0', pointPos - digits.Length);
            return (neg ? "-" : "") + mant + "e" + (exp > 0 ? "+" : "") + exp.ToString(CultureInfo.InvariantCulture);
        }

        void Literal(Expr e)
        {
            switch (e)
            {
                case NumberLiteral n:
                    {
                        var s = Number(n.Value);
                        if (Min && s.StartsWith("0.", StringComparison.Ordinal))
                            s = s.Substring(1);
                        else if (Min && s.StartsWith("-0.", StringComparison.Ordinal))
                            s = "-" + s.Substring(2);
                        W(s);
                        return;
                    }
                case StringLiteral s:
                    W(Quote(s.Value));
                    return;
                case BooleanLiteral b:
                    W(b.Value ? "true" : "false");
                    return;
                case NullLiteral:
                    W("null");
                    return;
                case BigIntLiteral bi:
                    W(bi.Digits + "n");
                    return;
                case RegexLiteral r:
                    {
                        var pat = r.Pattern;
                        if (pat.Length == 0)
                            pat = "(?:)";
                        if (Opt.HtmlSafe)
                            pat = pat.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase).Replace("<!--", "\\x3C!--", StringComparison.Ordinal);
                        if (Opt.AsciiOnly)
                            pat = EscapeTemplateRaw(pat);
                        W("/" + pat + "/" + r.Flags);
                        return;
                    }
            }
            throw new NotSupportedException("Can't write a " + e.GetType().Name + " as a literal");
        }

        /// <summary>
        /// Quote a string as a JavaScript string literal
        /// </summary>
        public String Quote(String s) => Quote(s, Opt.AsciiOnly, Opt.HtmlSafe);

        /// <summary>
        /// Quote a string as a JavaScript string literal
        /// </summary>
        public static String Quote(String s, bool asciiOnly, bool htmlSafe)
        {
            int dq = 0, sq = 0;
            foreach (var ch in s)
            {
                if (ch == '"')
                    ++dq;
                else if (ch == '\'')
                    ++sq;
            }
            var q = dq > sq ? '\'' : '"';
            var sb = new StringBuilder(s.Length + 2);
            sb.Append(q);
            var l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        continue;
                    case '\n':
                        sb.Append("\\n");
                        continue;
                    case '\r':
                        sb.Append("\\r");
                        continue;
                    case '\t':
                        sb.Append("\\t");
                        continue;
                    case '\b':
                        sb.Append("\\b");
                        continue;
                    case '\f':
                        sb.Append("\\f");
                        continue;
                    case '\v':
                        sb.Append("\\v");
                        continue;
                    case '\0':
                        if (i + 1 < l && s[i + 1] >= '0' && s[i + 1] <= '9')
                            sb.Append("\\x00");
                        else
                            sb.Append("\\0");
                        continue;
                    case (char)0x2028:
                        sb.Append("\\u2028");
                        continue;
                    case (char)0x2029:
                        sb.Append("\\u2029");
                        continue;
                }
                if (c == q)
                {
                    sb.Append('\\').Append(c);
                    continue;
                }
                if (c < 0x20 || c == 0x7f)
                {
                    sb.Append("\\x").Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                    continue;
                }
                if (char.IsSurrogate(c))
                {
                    bool pair = char.IsHighSurrogate(c) && i + 1 < l && char.IsLowSurrogate(s[i + 1]);
                    if (pair && !asciiOnly)
                    {
                        sb.Append(c).Append(s[i + 1]);
                        ++i;
                        continue;
                    }
                    sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    continue;
                }
                if (c > 0x7e && (asciiOnly || c == 0xfeff))
                {
                    sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    continue;
                }
                if (htmlSafe && c == '<' && i + 1 < l && (s[i + 1] == '/' || s[i + 1] == '!'))
                {
                    sb.Append("\\x3C");
                    continue;
                }
                sb.Append(c);
            }
            sb.Append(q);
            return sb.ToString();
        }

        #endregion//Literals
    }
}
