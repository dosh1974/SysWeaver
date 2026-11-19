using System;
using System.Collections.Generic;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Regular expressions: the u flag (ES2015, lowered like regexpu), the y flag (ES2015, converted to a RegExp constructor call),
    /// the s flag (ES2018) and named capture groups (ES2018).
    /// Also reports regular expression features that can't be converted (lookbehind, unicode property escapes, the d and v flags, modifiers).
    /// Handles regular expression literals and RegExp("pattern", "flags") / new RegExp(...) calls with string literal arguments.
    /// </summary>
    sealed class RegexPass : Pass
    {
        readonly bool LookbehindOk;
        readonly bool DFlagOk;
        readonly bool VFlagOk;
        readonly bool ModifiersOk;
        readonly bool DuplicateNamesOk;

        public RegexPass(TransformContext ctx) : base(ctx)
        {
            LookbehindOk = Supports(2018, 62, 78, 16.4);
            DFlagOk = Supports(2022, 90, 88, 15);
            VFlagOk = Supports(2024, 112, 116, 17);
            ModifiersOk = Supports(2025, 125, 132, 18.4);
            DuplicateNamesOk = Supports(2025, 125, 129, 17);
        }

        bool Supports(int es, double chrome, double firefox, double safari)
        {
            var t = Ctx.Target;
            if (t.EsVersion < es)
                return false;
            foreach (var b in t.Browsers)
            {
                var min = b.Key switch
                {
                    "chrome" => chrome,
                    "firefox" => firefox,
                    "safari" => safari,
                    "edge" => 1000,
                    _ => 0,
                };
                if (b.Value < min)
                    return false;
            }
            return true;
        }

        static bool IsRegExpCtor(Expr callee, List<Expr> args) =>
            callee is Identifier { Name: "RegExp" }
            && args.Count >= 1 && args.Count <= 2
            && args[0] is StringLiteral
            && (args.Count == 1 || args[1] is StringLiteral);

        protected override Node Transform(Node n)
        {
            switch (n)
            {
                case RegexLiteral r:
                    return Process(r.Pattern, r.Flags, r, true) ?? r;
                case NewExpr ne when IsRegExpCtor(ne.Callee, ne.Args):
                    return Process(((StringLiteral)ne.Args[0]).Value, ne.Args.Count > 1 ? ((StringLiteral)ne.Args[1]).Value : "", ne, false) ?? ne;
                case CallExpr ce when IsRegExpCtor(ce.Callee, ce.Args):
                    return Process(((StringLiteral)ce.Args[0]).Value, ce.Args.Count > 1 ? ((StringLiteral)ce.Args[1]).Value : "", ce, false) ?? ce;
                case CallExpr { Callee: MemberExpr { Computed: false, Property: Identifier { Name: "replace" or "replaceAll" } } } call
                    when call.Args.Count >= 2 && call.Args[0] is RegexLiteral rl && call.Args[1] is StringLiteral rep && Ctx.Needs(JsFeature.RegexNamedGroups):
                    {
                        //  Static rewrite of $<name> in the replacement string
                        var info = RegexRewriter.Scan(rl.Pattern, rl.Flags.Contains('u') || rl.Flags.Contains('v'));
                        if (info.Names.Count > 0 && !info.HasDuplicateNames && rep.Value.Contains("$<", StringComparison.Ordinal))
                            rep.Value = RegexRewriter.RewriteReplacement(rep.Value, info);
                        break;
                    }
            }
            return Children(n);
        }

        static String Remove(String flags, char f) => flags.Replace(f.ToString(), "", StringComparison.Ordinal);

        /// <summary>
        /// Process a regular expression, returns the replacement node or null if unchanged
        /// </summary>
        Expr Process(String pattern, String flags, Expr node, bool isLiteral)
        {
            bool u = flags.Contains('u');
            bool v = flags.Contains('v');
            RegexInfo info;
            try
            {
                info = RegexRewriter.Scan(pattern, u || v);
            }
            catch (Exception)
            {
                return null;
            }
            //  Report features that can't be converted
            if (info.HasLookbehind && !LookbehindOk)
                Warn(node, "Regular expression lookbehind assertions can't be converted, the target doesn't support them");
            if (info.HasModifiers && !ModifiersOk)
                Warn(node, "Regular expression modifiers can't be converted, the target doesn't support them");
            if (flags.Contains('d') && !DFlagOk)
                Warn(node, "The regular expression d flag (match indices) can't be converted, the target doesn't support it");
            if (v)
            {
                if (!VFlagOk)
                    Warn(node, "The regular expression v flag (unicode sets) can't be converted, the target doesn't support it");
                return null;
            }
            bool lowerU = u && Ctx.Needs(JsFeature.RegexUnicode);
            bool lowerS = flags.Contains('s') && Ctx.Needs(JsFeature.RegexDotAll);
            bool lowerNamed = info.Names.Count > 0 && Ctx.Needs(JsFeature.RegexNamedGroups);
            bool lowerY = flags.Contains('y') && Ctx.Needs(JsFeature.RegexSticky);
            bool forceCtor = false;
            if (info.HasDuplicateNames)
            {
                if (lowerNamed)
                {
                    Warn(node, "Duplicate named capture groups can't be converted");
                    lowerNamed = false;
                }
                else if (!DuplicateNamesOk)
                    Warn(node, "Duplicate named capture groups can't be converted, the target doesn't support them");
            }
            var newPattern = pattern;
            var newFlags = flags;
            bool changed = false;
            if (lowerU && info.HasUnicodeProperty)
            {
                Warn(node, "Regular expression unicode property escapes (\\p{...}) can't be converted, the regular expression is created at runtime (fails if the target doesn't support it)");
                lowerU = false;
                forceCtor = isLiteral;
            }
            if (lowerU)
            {
                try
                {
                    newPattern = UnicodeRegexLowering.Lower(pattern, flags.Contains('s'), lowerNamed, flags.Contains('i'), info, out var caseWarning);
                    newFlags = Remove(newFlags, 'u');
                    if (lowerS)
                        newFlags = Remove(newFlags, 's');
                    if (caseWarning)
                        Warn(node, "Case insensitive matching of non-ASCII characters in a regular expression with the u flag might behave differently after conversion");
                    Ctx.Did(JsFeature.RegexUnicode);
                    if (lowerS)
                        Ctx.Did(JsFeature.RegexDotAll);
                    changed = true;
                }
                catch (FormatException ex)
                {
                    Warn(node, "Can't convert the regular expression with the u flag: " + ex.Message);
                    lowerU = false;
                    forceCtor = isLiteral;
                }
            }
            if (!lowerU && (lowerS || lowerNamed))
            {
                newPattern = RegexRewriter.LowerSimple(pattern, u, lowerS, lowerNamed, info);
                if (lowerS)
                {
                    newFlags = Remove(newFlags, 's');
                    Ctx.Did(JsFeature.RegexDotAll);
                }
                changed = true;
            }
            if (!changed && !lowerY && !forceCtor)
                return null;
            Expr r;
            if (lowerY || forceCtor || !isLiteral)
            {
                var args = new List<Expr> { B.Str(newPattern) };
                if (newFlags.Length > 0 || (node is NewExpr ne && ne.Args.Count > 1) || (node is CallExpr ce && ce.Args.Count > 1))
                    args.Add(B.Str(newFlags));
                var nn = new NewExpr(B.Id("RegExp"));
                nn.Args.AddRange(args);
                r = nn;
                if (lowerY)
                {
                    Ctx.Did(JsFeature.RegexSticky);
                    Ctx.Diag.Info(node.Start, "The regular expression y flag (sticky) is kept, the regular expression is created at runtime (requires runtime support)");
                }
            }
            else
                r = new RegexLiteral(newPattern, newFlags);
            r.At<Expr>(node);
            if (lowerNamed)
            {
                Ctx.Did(JsFeature.RegexNamedGroups);
                var groups = new ObjectExpr();
                foreach (var kv in info.Names)
                    groups.Properties.Add(new Property(B.Str(kv.Key), B.Num(kv.Value)));
                r = B.Call(Helper("wrapRegExp"), r, groups).At<Expr>(node);
            }
            return r;
        }
    }
}
