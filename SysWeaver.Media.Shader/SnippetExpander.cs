using System.Text;

namespace EffectWeaver.Shaders;

/// <summary>
/// Snippets as built in functions: a source may call any Ew… function / use any EW_… macro of the snippet library without defining it.
/// Before compiling, the snippets the source uses but doesn't define (and their dependencies) are inserted right before the first top level
/// declaration that uses them (so snippets calling the effect's own functions through macros like EW_SDF still come after them).
/// <c>#line</c> directives keep the line numbers of the source: each inserted snippet is its own source string (<c>#line 1 k</c>, k = 1, 2, ...,
/// see <see cref="Expansion.Sources"/>), the source continues with <c>#line n 0</c>. So compile errors are "0:line" for the source and "k:line" for a snippet.
/// </summary>
public static class SnippetExpander
{
    /// <param name="Body">The source with the snippets inserted (without the appended code)</param>
    /// <param name="Inserted">The inserted snippets in order</param>
    /// <param name="Sources">Source string numbers (for the #line directives) of the inserted snippets, the appended code (Id "") last</param>
    /// <param name="Uses">The line (1 based) of the source where a snippet is used first (a dependency: where the snippet using it is used), for error messages</param>
    public sealed record Expansion(string Body, List<string> Inserted, List<(int Index, string Id)> Sources, Dictionary<string, int> Uses);

    /// <summary>
    /// Insert the snippets a source uses. <paramref name="append"/>: code added at the end (e.g. a plot program), snippets only it uses go right before it.
    /// <paramref name="clean"/>: no #line directives (readable code for exports, compile errors then have other line numbers).
    /// </summary>
    public static Expansion Expand(SnippetLibrary lib, string source, string? append, bool clean = false)
    {
        var src = (source ?? "").Replace("\r\n", "\n");
        var lines = src.Split('\n');
        var code = SnippetLibrary.StripComments(src);
        var userDefined = SnippetLibrary.DefinedIn(code);
        var lineStarts = new int[lines.Length];
        for (int i = 1; i < lines.Length; ++i)
            lineStarts[i] = lineStarts[i - 1] + lines[i - 1].Length + 1;
        int LineOf(int pos)
        {
            var i = Array.BinarySearch(lineStarts, pos);
            return i >= 0 ? i : ~i - 1;
        }

        // Insertions per line index (lines.Length = at the end)
        var at = new SortedDictionary<int, List<SnippetLibrary.Snippet>>();
        var inserted = new HashSet<string>(StringComparer.Ordinal);
        var uses = new Dictionary<string, int>(StringComparer.Ordinal);
        // The first line (1 based) of the source using one of some names, 0 if none
        var codeLines = code.Split('\n');
        int FirstUse(HashSet<string> names)
        {
            for (int i = 0; i < codeLines.Length; ++i)
                if (SnippetLibrary.UsedNames(codeLines[i]).Any(names.Contains))
                    return i + 1;
            return 0;
        }
        void Need(IEnumerable<string> names, int line)
        {
            foreach (var name in names)
            {
                if (userDefined.Contains(name) || lib.ByDefinedName(name) is not { } root)
                    continue;
                var rootUse = FirstUse(root.Names);
                foreach (var s in lib.WithDependencies(root))
                {
                    if (inserted.Contains(s.Id) || (s.Names.Count > 0 && s.Names.All(userDefined.Contains)))
                        continue;
                    inserted.Add(s.Id);
                    // Errors in a snippet are shown where the source uses it (a dependency: where its user is used)
                    var use = FirstUse(s.Names);
                    uses[s.Id] = use > 0 ? use : rootUse > 0 ? rootUse : Math.Min(line, lines.Length) + 1;
                    if (!at.TryGetValue(line, out var list))
                        at[line] = list = [];
                    list.Add(s);
                }
            }
        }

        // Top level declarations: from the first code character at depth 0 to the closing brace / semicolon.
        // Names in preprocessor lines (macros expand where they are used) go with the next declaration.
        var pending = new HashSet<string>(StringComparer.Ordinal);
        int depth = 0, start = -1;
        for (int i = 0; i < code.Length; ++i)
        {
            var c = code[i];
            if (depth == 0 && start < 0 && c == '#' && code.AsSpan(lineStarts[LineOf(i)], i - lineStarts[LineOf(i)]).IsWhiteSpace())
            {
                // The directive with its continuation lines
                var end = i;
                while (true)
                {
                    var nl = code.IndexOf('\n', end);
                    if (nl < 0)
                    {
                        end = code.Length;
                        break;
                    }
                    var trimmed = code[end..nl].TrimEnd();
                    end = nl;
                    if (!trimmed.EndsWith('\\'))
                        break;
                    ++end;
                }
                pending.UnionWith(SnippetLibrary.UsedNames(code[i..end]));
                i = end;
                continue;
            }
            if (depth == 0 && start < 0 && !char.IsWhiteSpace(c) && c != ';')
                start = i;
            if (c == '{')
                ++depth;
            else if (c == '}')
            {
                if (depth > 0 && --depth == 0 && start >= 0)
                {
                    Declaration(start, i + 1);
                    start = -1;
                }
            }
            else if (c == ';' && depth == 0 && start >= 0)
            {
                Declaration(start, i + 1);
                start = -1;
            }
        }
        if (start >= 0)
            Declaration(start, code.Length);
        void Declaration(int s, int e)
        {
            var names = SnippetLibrary.UsedNames(code[s..e]).ToList();
            names.AddRange(pending);
            pending.Clear();
            Need(names, LineOf(s));
        }
        var tail = new List<string>(pending);
        if (append != null)
            tail.AddRange(SnippetLibrary.UsedNames(SnippetLibrary.StripComments(append.Replace("\r\n", "\n"))));
        Need(tail, lines.Length);

        var sb = new StringBuilder(src.Length + 4096);
        var sources = new List<(int, string)>();
        void Emit(List<SnippetLibrary.Snippet> list, int line)
        {
            foreach (var s in list)
            {
                sources.Add((sources.Count + 1, s.Id));
                // Clean: readable code (exports), the snippets separated by empty lines
                if (clean)
                    sb.Append(s.Code.TrimEnd()).Append("\n\n");
                else
                    sb.Append("#line 1 ").Append(sources.Count).Append('\n').Append(s.Code.TrimEnd()).Append('\n');
            }
            if (!clean)
                sb.Append("#line ").Append(line + 1).Append(" 0\n");
        }
        for (int i = 0; i < lines.Length; ++i)
        {
            if (at.TryGetValue(i, out var list))
                Emit(list, i);
            sb.Append(lines[i]);
            if (i < lines.Length - 1)
                sb.Append('\n');
        }
        if (at.TryGetValue(lines.Length, out var last))
        {
            sb.Append('\n');
            Emit(last, lines.Length);
        }
        // The appended code itself is added by the caller (after parsing: it may have its own main), after "#line 1 <its index>"
        if (append != null)
            sources.Add((sources.Count + 1, ""));
        return new Expansion(sb.ToString(), inserted.Count == 0 ? [] : sources.Where(x => x.Item2 != "").Select(x => x.Item2).ToList(), sources, uses);
    }
}
