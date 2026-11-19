using System;
using System.Collections.Generic;
using System.Linq;
using Acornima;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;
using SysWeaver.Converter.Js.Helpers;
using SysWeaver.Converter.Js.Output;
using SysWeaver.Converter.Js.Transforms;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// The result of a conversion
    /// </summary>
    public sealed class JsConvertResult
    {
        /// <summary>The converted code, null if the conversion failed</summary>
        public String Code;
        /// <summary>Errors, warnings and information</summary>
        public List<JsDiagnostic> Diagnostics = new List<JsDiagnostic>();
        /// <summary>The names of the helpers that are used by the code (without the leading underscore, ex: "defineProperty")</summary>
        public List<String> Helpers = new List<String>();
        /// <summary>The features that were transformed</summary>
        public List<JsFeature> Transformed = new List<JsFeature>();
        /// <summary>The polyfills that are used (BuiltIns = Polyfill), inlined unless the helper mode is External</summary>
        public List<String> Polyfills = new List<String>();
        /// <summary>The target that was used</summary>
        public JsTarget Target;
        /// <summary>True if the input was parsed as a module</summary>
        public bool IsModule;

        /// <summary>True if the conversion succeeded (no errors, and no warnings if WarningsAsErrors is set)</summary>
        public bool Success => Code != null;

        /// <summary>True if there are any warnings</summary>
        public bool HasWarnings => Diagnostics.Exists(x => x.Level == JsDiagnosticLevel.Warning);

        /// <summary>True if there are any errors</summary>
        public bool HasErrors => Diagnostics.Exists(x => x.Level == JsDiagnosticLevel.Error);
    }

    /// <summary>
    /// Converts modern JavaScript to older JavaScript versions (for old browsers), like Babel's preset-env.
    /// </summary>
    public static class JsConverter
    {
        /// <summary>
        /// The version of the converter (changes when the output might change)
        /// </summary>
        public const String Version = "1.0.0";

        /// <summary>
        /// Convert JavaScript code
        /// </summary>
        /// <param name="source">The source code</param>
        /// <param name="p">The options, null to use the defaults</param>
        /// <returns>The result (check Success, the converted code is in Code)</returns>
        public static JsConvertResult Convert(String source, JsConverterParams p = null)
        {
            p ??= new JsConverterParams();
            var diag = new Diagnostics(source);
            return Convert(source, p, diag, null, false, null);
        }

        /// <summary>
        /// Get the feature set to transform for some options
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the target or a feature name is invalid</exception>
        public static HashSet<JsFeature> GetFeatures(JsConverterParams p, out JsTarget target)
        {
            target = JsTarget.Parse(p.Target);
            var f = new HashSet<JsFeature>(target.Features);
            foreach (var x in ParseFeatures(p.Include))
                f.Add(x);
            foreach (var x in ParseFeatures(p.Exclude))
                f.Remove(x);
            return f;
        }

        static IEnumerable<JsFeature> ParseFeatures(String s)
        {
            if (String.IsNullOrWhiteSpace(s))
                yield break;
            foreach (var x in s.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<JsFeature>(x, true, out var f))
                    throw new ArgumentException("Unknown feature \"" + x + "\", valid features are: " + String.Join(", ", Enum.GetNames<JsFeature>()));
                yield return f;
            }
        }

        static EcmaVersion ToEcmaVersion(int es) => es switch
        {
            <= 5 => EcmaVersion.ES5,
            2015 => EcmaVersion.ES2015,
            2016 => EcmaVersion.ES2016,
            2017 => EcmaVersion.ES2017,
            2018 => EcmaVersion.ES2018,
            2019 => EcmaVersion.ES2019,
            2020 => EcmaVersion.ES2020,
            2021 => EcmaVersion.ES2021,
            2022 => EcmaVersion.ES2022,
            2023 => EcmaVersion.ES2023,
            2024 => EcmaVersion.ES2024,
            2025 => EcmaVersion.ES2025,
            _ => EcmaVersion.Latest,
        };

        sealed class ParsedSource
        {
            public Acornima.Ast.Program Program;
            public List<Ast.Comment> Comments = new List<Ast.Comment>();
        }

        static ParsedSource Parse(String source, bool module, EcmaVersion version = EcmaVersion.Latest, bool collectComments = true, bool allowReturn = false)
        {
            var r = new ParsedSource();
            var po = new ParserOptions
            {
                EcmaVersion = version,
#pragma warning disable CS0618 // Skip: patterns are not converted to .NET regular expressions (the RegexPass handles them), deprecated in Acornima 1.x but still the documented way to skip
                RegExpParseMode = RegExpParseMode.Skip,
#pragma warning restore CS0618
                AllowHashBang = true,
                AllowReturnOutsideFunction = allowReturn,
            };
            if (collectComments)
            {
                po = po with
                {
                    OnComment = (in Acornima.Comment c) =>
                    {
                        if (c.Kind == CommentKind.HashBang)
                            return;
                        r.Comments.Add(new Ast.Comment
                        {
                            IsBlock = c.Kind == CommentKind.Block,
                            Text = source.Substring(c.ContentRange.Start, c.ContentRange.End - c.ContentRange.Start),
                            Start = c.Start,
                        });
                    },
                };
            }
            var parser = new Parser(po);
            r.Program = module ? parser.ParseModule(source) : parser.ParseScript(source);
            return r;
        }

        /// <summary>
        /// Parse source into a syntax tree (no comments), returns null if it can't be parsed
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="module">True: module, false: script, null: script, then module if that fails</param>
        /// <param name="isModule">True if parsed as a module</param>
        /// <param name="error">The syntax error if the source can't be parsed</param>
        internal static ProgramNode ParseProgram(String source, bool? module, out bool isModule, out String error)
        {
            error = null;
            isModule = module ?? false;
            try
            {
                return AcornimaImporter.Import(Parse(source, isModule, EcmaVersion.Latest, false).Program, new Diagnostics(source));
            }
            catch (ParseErrorException ex) when (module == null)
            {
                try
                {
                    isModule = true;
                    return AcornimaImporter.Import(Parse(source, true, EcmaVersion.Latest, false).Program, new Diagnostics(source));
                }
                catch (ParseErrorException)
                {
                    error = "line " + ex.LineNumber + ", column " + (ex.Column + 1) + ": " + ex.Description;
                }
            }
            catch (ParseErrorException ex)
            {
                error = "line " + ex.LineNumber + ", column " + (ex.Column + 1) + ": " + ex.Description;
            }
            catch (NotSupportedException ex)
            {
                error = ex.Message;
            }
            return null;
        }

        /// <summary>
        /// Convert (internal), also used for html script blocks
        /// </summary>
        /// <param name="source">The source</param>
        /// <param name="p">The options</param>
        /// <param name="diag">The diagnostics collector</param>
        /// <param name="reservedNames">Optional, names that must not be generated (ex: names used in other script blocks)</param>
        /// <param name="htmlSafe">Make the output safe to embed in an html script element</param>
        /// <param name="wrapper">Optional, used for html event handler attributes: the code is parsed as the body of a function</param>
        internal static JsConvertResult Convert(String source, JsConverterParams p, Diagnostics diag, HashSet<String> reservedNames, bool htmlSafe, Func<ProgramNode, ProgramNode> wrapper)
        {
            var result = new JsConvertResult();
            try
            {
                var features = GetFeatures(p, out var target);
                result.Target = target;
                //  Parse
                ParsedSource parsed = null;
                bool isModule = p.SourceType == JsSourceType.Module;
                try
                {
                    parsed = Parse(source, isModule, EcmaVersion.Latest, true, wrapper != null);
                }
                catch (ParseErrorException ex) when (p.SourceType == JsSourceType.Auto)
                {
                    try
                    {
                        parsed = Parse(source, true);
                        isModule = true;
                    }
                    catch (ParseErrorException)
                    {
                        diag.AddAt(JsDiagnosticLevel.Error, ex.LineNumber, ex.Column + 1, "Syntax error: " + ex.Description);
                        return Finish(result, diag, p);
                    }
                }
                catch (ParseErrorException ex)
                {
                    diag.AddAt(JsDiagnosticLevel.Error, ex.LineNumber, ex.Column + 1, "Syntax error: " + ex.Description);
                    return Finish(result, diag, p);
                }
                result.IsModule = isModule;
                //  Import
                var prog = AcornimaImporter.Import(parsed.Program, diag);
                if (diag.HaveErrors)
                    return Finish(result, diag, p);
                if (p.Comments != JsComments.None)
                    CommentAttacher.Attach(prog, parsed.Comments, p.Comments);
                if (wrapper != null)
                    prog = wrapper(prog);
                //  All names in use
                var names = new HashSet<String>(StringComparer.Ordinal);
                if (reservedNames != null)
                    names.UnionWith(reservedNames);
                Walker.Walk(prog, n =>
                {
                    switch (n)
                    {
                        case Identifier i:
                            names.Add(i.Name);
                            break;
                        case PrivateName pn:
                            names.Add(pn.Name);
                            break;
                    }
                    return true;
                });
                var sourceNames = new HashSet<String>(names, StringComparer.Ordinal);
                var ctx = new TransformContext(p, diag, target, features, names);
                //  Checks
                if (isModule && !target.SupportsModules)
                    diag.Warning(-1, "The source is an ES module (import / export), the target doesn't support modules");
                Checks.Run(prog, ctx);
                //  Transform
                foreach (var pass in Pipeline.Create(ctx))
                {
                    pass.Run(prog);
                    if (diag.HaveErrors)
                        return Finish(result, diag, p);
                }
                if (p.BuiltIns != JsBuiltIns.Ignore)
                    BuiltIns.Run(prog, ctx);
                //  Helpers
                if (p.Helpers == JsHelperMode.Inline && ctx.UsedHelpers.Count > 0)
                {
                    var helperStmts = BuildHelpers(JsHelpers.Ordered(ctx.UsedHelpers), ctx.HelperNames);
                    var at = Pass.DirectiveCount(prog.Body);
                    prog.Body.InsertRange(at, helperStmts);
                }
                else if (p.Helpers == JsHelperMode.External)
                {
                    foreach (var h in ctx.UsedHelpers)
                    {
                        var n = "_" + h;
                        if (sourceNames.Contains(n))
                            diag.Warning(-1, "The helper name \"" + n + "\" is also used in the source, external helpers might conflict");
                    }
                }
                result.Polyfills = new List<String>(ctx.Polyfills);
                if (ctx.Polyfills.Count > 0 && p.Helpers == JsHelperMode.Inline)
                {
                    var at = Pass.DirectiveCount(prog.Body);
                    prog.Body.InsertRange(at, BuiltIns.BuildPolyfills(ctx.Polyfills));
                }
                result.Helpers = JsHelpers.Ordered(ctx.UsedHelpers);
                result.Transformed = ctx.Transformed.OrderBy(x => x).ToList();
                //  Write
                var code = JsWriter.Write(prog, new JsWriter.Options
                {
                    Minify = p.Minify,
                    IndentUnit = p.Indent <= 0 ? "\t" : new String(' ', p.Indent),
                    AsciiOnly = p.AsciiOnly,
                    HtmlSafe = htmlSafe,
                    Comments = p.Comments,
                });
                //  Validate
                if (p.Validate && wrapper == null)
                    Validate(code, isModule, ValidationVersion(target, features), diag);
                result.Code = code;
            }
            catch (JsConvertException)
            {
                //  Already reported
            }
            catch (ArgumentException ex)
            {
                diag.Error(-1, ex.Message);
            }
            catch (NotSupportedException ex)
            {
                //  Unsupported syntax is reported by the importer
                if (!diag.HaveErrors)
                    diag.Error(-1, ex.Message);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diag.Error(-1, "Internal error (" + ex.GetType().Name + "): " + ex.Message + ". Please report this as a bug.");
            }
            return Finish(result, diag, p);
        }


        /// <summary>
        /// The EcmaScript version the output is validated with: the explicit EcmaScript limit of the target,
        /// lowered if a feature that the target needs isn't transformed (excluded), int.MaxValue if no limit.
        /// (A browser that lacks a single old feature, ex: safari 12 and template literals, doesn't lower it, that feature is transformed)
        /// </summary>
        static int ValidationVersion(JsTarget target, HashSet<JsFeature> features)
        {
            var v = target.EsLimit;
            foreach (var f in target.Features)
            {
                if (features.Contains(f))
                    continue;
                var es = JsFeatureInfo.Get(f).Es;
                v = Math.Min(v, es == 2015 ? 5 : es - 1);
            }
            return v;
        }

        /// <summary>
        /// Validate the output by parsing it with the latest version (internal error check) and the target version
        /// </summary>
        internal static void Validate(String code, bool isModule, int esVersion, Diagnostics diag)
        {
            try
            {
                Parse(code, isModule, EcmaVersion.Latest, false);
            }
            catch (ParseErrorException ex)
            {
                diag.Error(-1, "Internal error, the output is not valid JavaScript (line " + ex.LineNumber + ", column " + (ex.Column + 1) + "): " + ex.Description + ". Please report this as a bug.");
                return;
            }
            if (esVersion == int.MaxValue)
                return;
            //  Modules require ES2015 (a module with an older target is already reported)
            if (isModule && esVersion < 2015)
                esVersion = 2015;
            try
            {
                Parse(code, isModule, ToEcmaVersion(esVersion), false);
            }
            catch (ParseErrorException ex)
            {
                var es = esVersion == 5 ? "ES5" : ("ES" + esVersion);
                diag.Warning(-1, "The output contains syntax that isn't supported by " + es + " (output line " + ex.LineNumber + ", column " + (ex.Column + 1) + "): " + ex.Description);
            }
            catch (InvalidOperationException ex)
            {
                diag.Warning(-1, "The output couldn't be validated: " + ex.Message);
            }
        }

        static JsConvertResult Finish(JsConvertResult r, Diagnostics diag, JsConverterParams p)
        {
            r.Diagnostics = diag.Items.OrderBy(x => x.Line).ThenBy(x => x.Column).ToList();
            if (r.HasErrors || (p.WarningsAsErrors && r.HasWarnings))
                r.Code = null;
            return r;
        }

        /// <summary>
        /// Parse helper sources into statements, renaming the helpers to the names used
        /// </summary>
        static List<Stmt> BuildHelpers(IEnumerable<String> helpers, Dictionary<String, String> names)
        {
            var rename = new Dictionary<String, String>(StringComparer.Ordinal);
            foreach (var kv in names)
                rename["_" + kv.Key] = kv.Value;
            var r = new List<Stmt>();
            foreach (var h in helpers)
            {
                var src = JsHelpers.GetSource(h);
                var parsed = Parse(src, false, EcmaVersion.ES5, false);
                var prog = AcornimaImporter.Import(parsed.Program, new Diagnostics(src));
                Walker.Walk(prog, n =>
                {
                    if (n is Identifier i && rename.TryGetValue(i.Name, out var nn))
                        i.Name = nn;
                    return true;
                });
                r.AddRange(prog.Body);
            }
            return r;
        }

        /// <summary>
        /// Get the source code of helpers (for use with JsHelperMode.External)
        /// </summary>
        /// <param name="helpers">The helper names (as returned in JsConvertResult.Helpers), dependencies are added automatically</param>
        /// <param name="minify">Minify the output</param>
        /// <param name="polyfills">Optional polyfills to include (as returned in JsConvertResult.Polyfills), they are added first</param>
        /// <returns>The source code (ES5)</returns>
        public static String GetHelpersSource(IEnumerable<String> helpers, bool minify = false, IEnumerable<String> polyfills = null)
        {
            var ordered = JsHelpers.Ordered(helpers ?? Array.Empty<String>());
            var names = ordered.ToDictionary(x => x, x => "_" + x);
            var prog = new ProgramNode();
            if (polyfills != null)
                prog.Body.AddRange(BuiltIns.BuildPolyfills(polyfills));
            prog.Body.AddRange(BuildHelpers(ordered, names));
            return JsWriter.Write(prog, new JsWriter.Options { Minify = minify, Comments = JsComments.None });
        }

        /// <summary>
        /// The names of all available helpers
        /// </summary>
        public static IEnumerable<String> AllHelpers => JsHelpers.All;

        /// <summary>
        /// The names of all available polyfills
        /// </summary>
        public static IEnumerable<String> AllPolyfills => BuiltIns.AllPolyfills;

        /// <summary>
        /// Get the (ES5) source of polyfills
        /// </summary>
        /// <param name="polyfills">The polyfill names (see AllPolyfills), dependencies are added automatically</param>
        /// <param name="minify">Minify the output</param>
        public static String GetPolyfillsSource(IEnumerable<String> polyfills, bool minify = false) => GetHelpersSource(null, minify, polyfills);
    }
}
