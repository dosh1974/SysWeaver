using System;
using System.Collections.Generic;

namespace SysWeaver.Converter.Js.Ast
{
    /// <summary>
    /// Base class for tree rewriters.
    /// Override Visit to change nodes, call base.Visit (or n.MapChildren(this)) to process the children.
    /// A statement can be replaced with a StatementList (flattened into the parent list) or removed by returning null.
    /// </summary>
    public abstract class Rewriter
    {
        /// <summary>
        /// Rewrite a node (null safe)
        /// </summary>
        public Node Rewrite(Node n) => n == null ? null : Visit(n);

        /// <summary>
        /// Visit a node (never null), the default implementation rewrites all children and returns the node
        /// </summary>
        protected virtual Node Visit(Node n)
        {
            n.MapChildren(this);
            return n;
        }

        /// <summary>
        /// Rewrite an expression (null safe)
        /// </summary>
        public Expr Expr(Expr e)
        {
            if (e == null)
                return null;
            var r = Visit(e);
            if (r is Expr x)
                return x;
            throw new InvalidOperationException("An expression was rewritten to a " + r?.GetType().Name);
        }

        /// <summary>
        /// Rewrite a statement in a single statement position (null safe).
        /// A StatementList is converted to a block, a removed statement to an empty statement.
        /// </summary>
        public Stmt Stmt(Stmt s)
        {
            if (s == null)
                return null;
            var r = Visit(s);
            if (r == null)
                return new EmptyStmt();
            if (r is StatementList l)
            {
                if (l.Body.Count == 1 && l.Body[0] is not VarDecl { Kind: not "var" })
                    return l.Body[0];
                return new BlockStmt(l.Body);
            }
            return (Stmt)r;
        }

        /// <summary>
        /// Rewrite a block (null safe), the result is always a block
        /// </summary>
        public BlockStmt Block(BlockStmt b)
        {
            if (b == null)
                return null;
            var r = Visit(b);
            if (r is BlockStmt bb)
                return bb;
            if (r == null)
                return new BlockStmt();
            if (r is StatementList l)
                return new BlockStmt(l.Body);
            return new BlockStmt { Body = { (Stmt)r } };
        }

        /// <summary>
        /// Rewrite a list of statements, StatementList's are flattened and removed (null) statements are removed
        /// </summary>
        public void MapStatements(List<Stmt> list)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                var s = list[i];
                var r = Visit(s);
                if (r == null)
                {
                    list.RemoveAt(i);
                    --i;
                    continue;
                }
                if (r is StatementList l)
                {
                    list.RemoveAt(i);
                    list.InsertRange(i, l.Body);
                    if (l.LeadingComments != null && l.Body.Count > 0)
                        AddComments(l.Body[0], l.LeadingComments);
                    i += l.Body.Count - 1;
                    continue;
                }
                if (!ReferenceEquals(r, s) && s.LeadingComments != null && r.LeadingComments == null)
                    r.LeadingComments = s.LeadingComments;
                list[i] = (Stmt)r;
            }
        }

        static void AddComments(Node n, List<Comment> c)
        {
            if (n.LeadingComments == null)
                n.LeadingComments = c;
            else
                n.LeadingComments.InsertRange(0, c);
        }

        /// <summary>
        /// Rewrite all nodes in a list (null entries, ex: array holes, are kept)
        /// </summary>
        public void MapList<T>(List<T> list) where T : Node
        {
            if (list == null)
                return;
            var c = list.Count;
            for (int i = 0; i < c; ++i)
            {
                var s = list[i];
                if (s == null)
                    continue;
                list[i] = (T)Visit(s);
            }
        }
    }

    /// <summary>
    /// A read only tree walker
    /// </summary>
    public sealed class Walker : Rewriter
    {
        readonly Func<Node, bool> Pre;
        readonly Action<Node> Post;

        /// <summary>
        /// Create a walker
        /// </summary>
        /// <param name="pre">Called before the children are visited, return false to skip the children</param>
        /// <param name="post">Optionally called after the children are visited</param>
        public Walker(Func<Node, bool> pre, Action<Node> post = null)
        {
            Pre = pre;
            Post = post;
        }

        protected override Node Visit(Node n)
        {
            if (Pre(n))
                n.MapChildren(this);
            Post?.Invoke(n);
            return n;
        }

        /// <summary>
        /// Walk a tree
        /// </summary>
        /// <param name="root">The root node</param>
        /// <param name="pre">Called before the children are visited, return false to skip the children</param>
        /// <param name="post">Optionally called after the children are visited</param>
        public static void Walk(Node root, Func<Node, bool> pre, Action<Node> post = null)
        {
            if (root == null)
                return;
            new Walker(pre, post).Rewrite(root);
        }

        /// <summary>
        /// Returns true if any node in the tree matches the predicate
        /// </summary>
        /// <param name="root">The root node</param>
        /// <param name="match">The predicate</param>
        /// <param name="enter">Optional, return false to not search the children of a node</param>
        public static bool Any(Node root, Func<Node, bool> match, Func<Node, bool> enter = null)
        {
            bool found = false;
            Walk(root, n =>
            {
                if (found)
                    return false;
                if (match(n))
                {
                    found = true;
                    return false;
                }
                return enter == null || enter(n);
            });
            return found;
        }
    }
}
