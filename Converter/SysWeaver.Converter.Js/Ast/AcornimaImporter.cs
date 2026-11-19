using System;
using System.Collections.Generic;
using A = Acornima.Ast;
using Acornima;

namespace SysWeaver.Converter.Js.Ast
{
    /// <summary>
    /// Converts an Acornima syntax tree to the converter syntax tree
    /// </summary>
    sealed class AcornimaImporter
    {
        readonly Diagnostics Diag;

        AcornimaImporter(Diagnostics diag)
        {
            Diag = diag;
        }

        /// <summary>
        /// Import an Acornima program
        /// </summary>
        public static ProgramNode Import(A.Program p, Diagnostics diag)
        {
            var i = new AcornimaImporter(diag);
            var r = new ProgramNode
            {
                IsModule = p.SourceType == SourceType.Module,
            };
            Pos(r, p);
            foreach (var s in p.Body)
                r.Body.Add(i.Statement(s));
            return r;
        }

        static T Pos<T>(T n, A.Node src) where T : Node
        {
            n.Start = src.Start;
            n.End = src.End;
            return n;
        }

        NotSupportedException Unsupported(A.Node n, String what)
        {
            Diag.Error(n.Start, what + " is not supported");
            return new NotSupportedException(what + " is not supported");
        }

        #region Statements

        List<Stmt> Statements(in A.NodeList<A.Statement> list)
        {
            var r = new List<Stmt>(list.Count);
            foreach (var s in list)
                r.Add(Statement(s));
            return r;
        }

        BlockStmt Block(A.BlockStatement b)
        {
            if (b == null)
                return null;
            var r = Pos(new BlockStmt(), b);
            foreach (var s in b.Body)
                r.Body.Add(Statement(s));
            return r;
        }

        Stmt Statement(A.Statement s)
        {
            if (s == null)
                return null;
            return Pos(StatementInner(s), s);
        }

        Stmt StatementInner(A.Statement s)
        {
            switch (s)
            {
                case A.Directive d:
                    return new ExprStmt(Expression(d.Expression))
                    {
                        Directive = (d.Expression as A.Literal)?.Raw ?? ("\"" + d.Value + "\""),
                    };
                case A.ExpressionStatement e:
                    return new ExprStmt(Expression(e.Expression));
                case A.BlockStatement b:
                    return Block(b);
                case A.EmptyStatement:
                    return new EmptyStmt();
                case A.DebuggerStatement:
                    return new DebuggerStmt();
                case A.WithStatement w:
                    return new WithStmt { Object = Expression(w.Object), Body = Statement(w.Body) };
                case A.ReturnStatement r:
                    return new ReturnStmt(Expression(r.Argument));
                case A.LabeledStatement l:
                    return new LabeledStmt(l.Label.Name, Statement(l.Body));
                case A.BreakStatement b:
                    return new BreakStmt(b.Label?.Name);
                case A.ContinueStatement c:
                    return new ContinueStmt(c.Label?.Name);
                case A.IfStatement i:
                    return new IfStmt(Expression(i.Test), Statement(i.Consequent), Statement(i.Alternate));
                case A.SwitchStatement sw:
                    {
                        var r = new SwitchStmt { Discriminant = Expression(sw.Discriminant) };
                        foreach (var c in sw.Cases)
                        {
                            var sc = Pos(new SwitchCase { Test = Expression(c.Test) }, c);
                            foreach (var x in c.Consequent)
                                sc.Body.Add(Statement(x));
                            r.Cases.Add(sc);
                        }
                        return r;
                    }
                case A.ThrowStatement t:
                    return new ThrowStmt(Expression(t.Argument));
                case A.TryStatement t:
                    {
                        var r = new TryStmt
                        {
                            Block = Block(t.Block),
                            Finalizer = Block(t.Finalizer),
                        };
                        var h = t.Handler;
                        if (h != null)
                            r.Handler = Pos(new CatchClause { Param = Pattern(h.Param), Body = Block(h.Body) }, h);
                        return r;
                    }
                case A.WhileStatement w:
                    return new WhileStmt(Expression(w.Test), Statement(w.Body));
                case A.DoWhileStatement d:
                    return new DoWhileStmt { Body = Statement(d.Body), Test = Expression(d.Test) };
                case A.ForStatement f:
                    return new ForStmt
                    {
                        Init = f.Init switch
                        {
                            null => null,
                            A.VariableDeclaration vd => Statement(vd),
                            A.Expression e => Expression(e),
                            _ => throw Unsupported(f.Init, f.Init.TypeText),
                        },
                        Test = Expression(f.Test),
                        Update = Expression(f.Update),
                        Body = Statement(f.Body),
                    };
                case A.ForInStatement f:
                    return new ForInStmt { Left = ForLeft(f.Left), Right = Expression(f.Right), Body = Statement(f.Body) };
                case A.ForOfStatement f:
                    return new ForOfStmt { Left = ForLeft(f.Left), Right = Expression(f.Right), Body = Statement(f.Body), Await = f.Await };
                case A.FunctionDeclaration fd:
                    return new FunctionDecl(Function(fd.Id, fd.Params, fd.Body, null, false, fd.Async, fd.Generator, fd));
                case A.VariableDeclaration vd:
                    {
                        var r = new VarDecl(vd.Kind switch
                        {
                            A.VariableDeclarationKind.Var => "var",
                            A.VariableDeclarationKind.Let => "let",
                            A.VariableDeclarationKind.Const => "const",
                            A.VariableDeclarationKind.Using => "using",
                            A.VariableDeclarationKind.AwaitUsing => "await using",
                            _ => throw Unsupported(vd, "Variable declaration kind " + vd.Kind),
                        });
                        foreach (var d in vd.Declarations)
                            r.Decls.Add(Pos(new VarDeclarator(Pattern(d.Id), Expression(d.Init)), d));
                        return r;
                    }
                case A.ClassDeclaration cd:
                    return new ClassDecl(Class(cd.Id, cd.SuperClass, cd.Body, cd.Decorators, cd));
                case A.ImportDeclaration im:
                    {
                        var r = new ImportDecl
                        {
                            Source = (StringLiteral)Expression(im.Source),
                            Attributes = Attributes(im.Attributes),
                        };
                        if (im.Phase != A.ImportPhase.None)
                            throw Unsupported(im, "Import phase " + im.Phase);
                        foreach (var sp in im.Specifiers)
                        {
                            var x = sp switch
                            {
                                A.ImportDefaultSpecifier d => new ImportSpec { Kind = ImportSpecKind.Default, Local = Ident(d.Local) },
                                A.ImportNamespaceSpecifier n => new ImportSpec { Kind = ImportSpecKind.Namespace, Local = Ident(n.Local) },
                                A.ImportSpecifier n => new ImportSpec { Kind = ImportSpecKind.Named, Imported = Expression(n.Imported), Local = Ident(n.Local) },
                                _ => throw Unsupported(sp, sp.TypeText),
                            };
                            r.Specifiers.Add(Pos(x, sp));
                        }
                        return r;
                    }
                case A.ExportNamedDeclaration en:
                    {
                        var r = new ExportNamedDecl
                        {
                            Declaration = Statement(en.Declaration),
                            Source = (StringLiteral)Expression(en.Source),
                            Attributes = Attributes(en.Attributes),
                        };
                        foreach (var sp in en.Specifiers)
                            r.Specifiers.Add(Pos(new ExportSpec { Local = Expression(sp.Local), Exported = Expression(sp.Exported) }, sp));
                        return r;
                    }
                case A.ExportDefaultDeclaration ed:
                    {
                        Stmt d = ed.Declaration switch
                        {
                            A.FunctionDeclaration fd => Pos(new FunctionDecl(Function(fd.Id, fd.Params, fd.Body, null, false, fd.Async, fd.Generator, fd)), fd),
                            A.ClassDeclaration cd => Pos(new ClassDecl(Class(cd.Id, cd.SuperClass, cd.Body, cd.Decorators, cd)), cd),
                            A.Expression e => Pos(new ExprStmt(Expression(e)), e),
                            _ => throw Unsupported(ed.Declaration, ed.Declaration.TypeText),
                        };
                        return new ExportDefaultDecl { Declaration = d };
                    }
                case A.ExportAllDeclaration ea:
                    return new ExportAllDecl
                    {
                        Exported = Expression(ea.Exported),
                        Source = (StringLiteral)Expression(ea.Source),
                        Attributes = Attributes(ea.Attributes),
                    };
            }
            throw Unsupported(s, s.TypeText);
        }

        List<ImportAttr> Attributes(in A.NodeList<A.ImportAttribute> list)
        {
            if (list.Count == 0)
                return null;
            var r = new List<ImportAttr>(list.Count);
            foreach (var a in list)
                r.Add(new ImportAttr { Key = Expression(a.Key), Value = (StringLiteral)Expression(a.Value) });
            return r;
        }

        Node ForLeft(A.Node n)
        {
            if (n is A.VariableDeclaration vd)
                return Statement(vd);
            return Pattern(n);
        }

        #endregion//Statements

        #region Functions and classes

        Function Function(A.Identifier id, in A.NodeList<A.Node> ps, A.StatementOrExpression body, A.Node exprBody, bool arrow, bool isAsync, bool isGenerator, A.Node src)
        {
            var f = Pos(new Function
            {
                Id = Ident(id),
                IsArrow = arrow,
                IsAsync = isAsync,
                IsGenerator = isGenerator,
            }, src);
            foreach (var p in ps)
                f.Params.Add(Pattern(p));
            if (body is A.BlockStatement b)
                f.Body = Block(b);
            else
                f.ExprBody = Expression((A.Expression)body);
            return f;
        }

        Function Function(A.FunctionExpression fe, bool isMethod)
        {
            var f = Function(fe.Id, fe.Params, fe.Body, null, false, fe.Async, fe.Generator, fe);
            f.IsMethod = isMethod;
            return f;
        }

        Class Class(A.Identifier id, A.Expression superClass, A.ClassBody body, in A.NodeList<A.Decorator> decorators, A.Node src)
        {
            if (decorators.Count > 0)
                throw Unsupported(decorators[0], "Decorators");
            var c = Pos(new Class
            {
                Id = Ident(id),
                SuperClass = Expression(superClass),
            }, src);
            foreach (var m in body.Body)
            {
                Node x;
                switch (m)
                {
                    case A.MethodDefinition md:
                        if (md.Decorators.Count > 0)
                            throw Unsupported(md.Decorators[0], "Decorators");
                        x = new MethodDef
                        {
                            Key = Expression(md.Key),
                            Computed = md.Computed,
                            Static = md.Static,
                            Kind = md.Kind switch
                            {
                                A.PropertyKind.Constructor => "constructor",
                                A.PropertyKind.Get => "get",
                                A.PropertyKind.Set => "set",
                                _ => "method",
                            },
                            Value = Function(md.Value, true),
                        };
                        break;
                    case A.PropertyDefinition pd:
                        if (pd.Decorators.Count > 0)
                            throw Unsupported(pd.Decorators[0], "Decorators");
                        x = new FieldDef
                        {
                            Key = Expression(pd.Key),
                            Computed = pd.Computed,
                            Static = pd.Static,
                            Value = Expression(pd.Value),
                        };
                        break;
                    case A.StaticBlock sb:
                        {
                            var b = new StaticBlock();
                            foreach (var s in sb.Body)
                                b.Body.Add(Statement(s));
                            x = b;
                            break;
                        }
                    case A.AccessorProperty ap:
                        throw Unsupported(ap, "Auto accessors (accessor keyword)");
                    default:
                        throw Unsupported(m, m.TypeText);
                }
                c.Members.Add(Pos(x, m));
            }
            return c;
        }

        #endregion//Functions and classes

        #region Expressions

        Identifier Ident(A.Identifier i) => i == null ? null : Pos(new Identifier(i.Name), i);

        /// <summary>
        /// Import a pattern (binding / assignment target)
        /// </summary>
        Expr Pattern(A.Node n)
        {
            if (n == null)
                return null;
            switch (n)
            {
                case A.ArrayPattern ap:
                    {
                        var r = Pos(new ArrayPattern(), ap);
                        foreach (var e in ap.Elements)
                            r.Elements.Add(Pattern(e));
                        return r;
                    }
                case A.ObjectPattern op:
                    {
                        var r = Pos(new ObjectPattern(), op);
                        foreach (var p in op.Properties)
                        {
                            if (p is A.RestElement re)
                            {
                                r.Properties.Add(Pos(new RestElement(Pattern(re.Argument)), re));
                                continue;
                            }
                            var pp = (A.Property)p;
                            r.Properties.Add(Pos(new Property
                            {
                                Key = Expression(pp.Key),
                                Computed = pp.Computed,
                                Shorthand = pp.Shorthand,
                                Value = Pattern(pp.Value),
                            }, pp));
                        }
                        return r;
                    }
                case A.AssignmentPattern asp:
                    return Pos(new AssignPattern(Pattern(asp.Left), Expression(asp.Right)), asp);
                case A.RestElement re:
                    return Pos(new RestElement(Pattern(re.Argument)), re);
                case A.Expression e:
                    return Expression(e);
            }
            throw Unsupported(n, n.TypeText);
        }

        List<Expr> Expressions(in A.NodeList<A.Expression> list)
        {
            var r = new List<Expr>(list.Count);
            foreach (var e in list)
                r.Add(Expression(e));
            return r;
        }

        Expr Expression(A.Node e)
        {
            if (e == null)
                return null;
            return Pos(ExpressionInner(e), e);
        }

        static String Op(Operator op) => op switch
        {
            Operator.Assignment => "=",
            Operator.NullishCoalescingAssignment => "??=",
            Operator.LogicalOrAssignment => "||=",
            Operator.LogicalAndAssignment => "&&=",
            Operator.BitwiseOrAssignment => "|=",
            Operator.BitwiseXorAssignment => "^=",
            Operator.BitwiseAndAssignment => "&=",
            Operator.LeftShiftAssignment => "<<=",
            Operator.RightShiftAssignment => ">>=",
            Operator.UnsignedRightShiftAssignment => ">>>=",
            Operator.AdditionAssignment => "+=",
            Operator.SubtractionAssignment => "-=",
            Operator.MultiplicationAssignment => "*=",
            Operator.DivisionAssignment => "/=",
            Operator.RemainderAssignment => "%=",
            Operator.ExponentiationAssignment => "**=",
            Operator.NullishCoalescing => "??",
            Operator.LogicalOr => "||",
            Operator.LogicalAnd => "&&",
            Operator.BitwiseOr => "|",
            Operator.BitwiseXor => "^",
            Operator.BitwiseAnd => "&",
            Operator.Equality => "==",
            Operator.Inequality => "!=",
            Operator.StrictEquality => "===",
            Operator.StrictInequality => "!==",
            Operator.LessThan => "<",
            Operator.LessThanOrEqual => "<=",
            Operator.GreaterThan => ">",
            Operator.GreaterThanOrEqual => ">=",
            Operator.In => "in",
            Operator.InstanceOf => "instanceof",
            Operator.LeftShift => "<<",
            Operator.RightShift => ">>",
            Operator.UnsignedRightShift => ">>>",
            Operator.Addition => "+",
            Operator.Subtraction => "-",
            Operator.Multiplication => "*",
            Operator.Division => "/",
            Operator.Remainder => "%",
            Operator.Exponentiation => "**",
            Operator.Increment => "++",
            Operator.Decrement => "--",
            Operator.LogicalNot => "!",
            Operator.BitwiseNot => "~",
            Operator.UnaryPlus => "+",
            Operator.UnaryNegation => "-",
            Operator.TypeOf => "typeof",
            Operator.Void => "void",
            Operator.Delete => "delete",
            _ => throw new NotSupportedException("Operator " + op + " is not supported"),
        };

        Expr ExpressionInner(A.Node e)
        {
            switch (e)
            {
                case A.Identifier i:
                    return new Identifier(i.Name);
                case A.PrivateIdentifier pi:
                    return new PrivateName(pi.Name);
                case A.StringLiteral s:
                    return new StringLiteral(s.Value);
                case A.NumericLiteral n:
                    return new NumberLiteral(n.Value);
                case A.BooleanLiteral b:
                    return new BooleanLiteral(b.Value);
                case A.NullLiteral:
                    return new NullLiteral();
                case A.RegExpLiteral r:
                    return new RegexLiteral(r.RegExp.Pattern, r.RegExp.Flags);
                case A.BigIntLiteral bi:
                    return new BigIntLiteral(bi.Value.ToString());
                case A.TemplateLiteral t:
                    return Template(t);
                case A.TaggedTemplateExpression tt:
                    return new TaggedTemplate { Tag = Expression(tt.Tag), Quasi = Template(tt.Quasi) };
                case A.ThisExpression:
                    return new ThisExpr();
                case A.Super:
                    return new SuperExpr();
                case A.ArrayExpression a:
                    {
                        var r = new ArrayExpr();
                        foreach (var x in a.Elements)
                            r.Elements.Add(Expression(x));
                        return r;
                    }
                case A.ObjectExpression o:
                    {
                        var r = new ObjectExpr();
                        foreach (var p in o.Properties)
                        {
                            if (p is A.SpreadElement se)
                            {
                                r.Properties.Add(Pos(new SpreadElement(Expression(se.Argument)), se));
                                continue;
                            }
                            var pp = (A.Property)p;
                            var isFn = pp.Method || pp.Kind == A.PropertyKind.Get || pp.Kind == A.PropertyKind.Set;
                            Expr value = isFn
                                ? Pos(new FunctionExpr(Function((A.FunctionExpression)pp.Value, true)), pp.Value)
                                : Expression(pp.Value);
                            r.Properties.Add(Pos(new Property
                            {
                                Kind = pp.Kind switch
                                {
                                    A.PropertyKind.Get => PropKind.Get,
                                    A.PropertyKind.Set => PropKind.Set,
                                    _ => PropKind.Init,
                                },
                                Key = Expression(pp.Key),
                                Computed = pp.Computed,
                                Shorthand = pp.Shorthand,
                                Method = pp.Method,
                                Value = value,
                            }, pp));
                        }
                        return r;
                    }
                case A.FunctionExpression fe:
                    return new FunctionExpr(Function(fe, false));
                case A.ArrowFunctionExpression af:
                    return new FunctionExpr(Function(null, af.Params, af.Body, null, true, af.Async, false, af));
                case A.ClassExpression ce:
                    return new ClassExpr(Class(ce.Id, ce.SuperClass, ce.Body, ce.Decorators, ce));
                case A.UpdateExpression u:
                    return new UpdateExpr(Op(u.Operator), u.Prefix, Expression(u.Argument));
                case A.UnaryExpression u:
                    return new UnaryExpr(Op(u.Operator), Expression(u.Argument));
                case A.BinaryExpression b:
                    return new BinaryExpr(Op(b.Operator), Expression(b.Left), Expression(b.Right));
                case A.AssignmentExpression a:
                    return new AssignExpr(Op(a.Operator), Pattern(a.Left), Expression(a.Right));
                case A.ConditionalExpression c:
                    return new ConditionalExpr(Expression(c.Test), Expression(c.Consequent), Expression(c.Alternate));
                case A.CallExpression c:
                    return new CallExpr(Expression(c.Callee), Expressions(c.Arguments)) { Optional = c.Optional };
                case A.NewExpression n:
                    {
                        var r = new NewExpr(Expression(n.Callee));
                        r.Args.AddRange(Expressions(n.Arguments));
                        return r;
                    }
                case A.MemberExpression m:
                    return new MemberExpr(Expression(m.Object), Expression(m.Property), m.Computed) { Optional = m.Optional };
                case A.ChainExpression c:
                    return new ChainExpr(Expression(c.Expression));
                case A.SequenceExpression s:
                    return new SequenceExpr(Expressions(s.Expressions));
                case A.SpreadElement s:
                    return new SpreadElement(Expression(s.Argument));
                case A.YieldExpression y:
                    return new YieldExpr(Expression(y.Argument), y.Delegate);
                case A.AwaitExpression a:
                    return new AwaitExpr(Expression(a.Argument));
                case A.MetaProperty mp:
                    return new MetaProperty(mp.Meta.Name, mp.Property.Name);
                case A.ImportExpression ie:
                    if (ie.Phase != A.ImportPhase.None)
                        throw Unsupported(ie, "Import phase " + ie.Phase);
                    return new ImportCall { Source = Expression(ie.Source), Options = Expression(ie.Options) };
                case A.ParenthesizedExpression pe:
                    return Expression(pe.Expression);
                case A.ArrayPattern:
                case A.ObjectPattern:
                case A.AssignmentPattern:
                case A.RestElement:
                    return Pattern(e);
            }
            throw Unsupported(e, e.TypeText);
        }

        TemplateLiteral Template(A.TemplateLiteral t)
        {
            var r = Pos(new TemplateLiteral(), t);
            foreach (var q in t.Quasis)
                r.Quasis.Add(new TemplateQuasi { Cooked = q.Value.Cooked, Raw = q.Value.Raw });
            r.Expressions.AddRange(Expressions(t.Expressions));
            return r;
        }

        #endregion//Expressions
    }
}
