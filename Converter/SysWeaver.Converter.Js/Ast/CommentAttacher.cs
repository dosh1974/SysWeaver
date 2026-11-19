using System.Collections.Generic;

namespace SysWeaver.Converter.Js.Ast
{
    /// <summary>
    /// Attaches comments to the statements / class members that follow them.
    /// Comments inside expressions are attached to the next statement (so they are moved before the statement they were in, or after it).
    /// </summary>
    static class CommentAttacher
    {
        public static void Attach(ProgramNode p, List<Comment> comments, JsComments mode)
        {
            if (comments == null || comments.Count == 0)
                return;
            comments.Sort((a, b) => a.Start.CompareTo(b.Start));
            if (mode == JsComments.License)
                comments.RemoveAll(c => !c.IsLicense);
            if (comments.Count == 0)
                return;
            int idx = 0;
            List<Comment> Take(int before)
            {
                List<Comment> r = null;
                while (idx < comments.Count && comments[idx].Start < before)
                {
                    r ??= new List<Comment>();
                    r.Add(comments[idx]);
                    ++idx;
                }
                return r;
            }
            Walker.Walk(p, n =>
            {
                if (n.Start < 0)
                    return true;
                switch (n)
                {
                    case Stmt:
                    case MethodDef:
                    case FieldDef:
                    case StaticBlock:
                    case SwitchCase:
                        {
                            var c = Take(n.Start);
                            if (c != null)
                            {
                                if (n.LeadingComments == null)
                                    n.LeadingComments = c;
                                else
                                    n.LeadingComments.AddRange(c);
                            }
                            break;
                        }
                }
                return true;
            }, n =>
            {
                if (n is BlockStmt b && b.End >= 0)
                {
                    var c = Take(b.End);
                    if (c != null)
                    {
                        if (b.TrailingComments == null)
                            b.TrailingComments = c;
                        else
                            b.TrailingComments.AddRange(c);
                    }
                }
            });
            if (idx < comments.Count)
            {
                p.TrailingComments ??= new List<Comment>();
                p.TrailingComments.AddRange(comments.GetRange(idx, comments.Count - idx));
            }
        }
    }
}
