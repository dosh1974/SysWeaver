using Svg;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SysWeaver.Minifier
{
    /// <summary>
    /// Svg minifier / optimizer (XDocument based), see <see cref="Optimize"/>
    /// </summary>
    public static class SvgMinifier
    {
        /// <summary>
        /// The output version, change it when the output of <see cref="Optimize"/> changes (it's the first character of <see cref="SvgMinifierParams.CacheKey"/>,
        /// used by SvgOpt and SvgTransformer to invalidate their cached outputs).
        /// 'm': svg-cov-29 / svg-cov-49 (numeric only trailing zeros, fixed point optimization), 'n': svg-cov-53 (the view box is rounded).
        /// </summary>
        public const Char Version = 'n';

        static readonly IReadOnlySet<String> KeepNamespaces = ReadOnlyData.Set(StringComparer.Ordinal,
            "http://www.w3.org/2000/svg",
            "http://www.w3.org/2000/xmlns/",
            "http://www.w3.org/1999/xlink",
            ""
        );



        /// <summary>
        /// Minify an svg document (or an xml fragment with several root elements) according to the options.
        /// If the first svg element has a view box with a positive size, its width and height attributes are always removed (also when
        /// <see cref="SvgMinifierParams.RemoveSize"/> is false), and x / y attributes that are zero are removed.
        /// Some optimizations (clip-rule / fill-rule removal, <see cref="SvgMinifierParams.TryRemoveAttributes"/> and <see cref="SvgMinifierParams.BitmapValidation"/>)
        /// render the document with System.Drawing (Windows only, they are skipped silently if rendering fails).
        /// The DOCTYPE is always removed.
        /// The optimization steps are repeated until the document doesn't change any more (at most 8 passes), so minifying the output again doesn't change it (svg-cov-49).
        /// A step is only repeated if a step that can enable further optimizations of it changed the document after its last run (see <see cref="Steps"/>).
        /// </summary>
        /// <param name="svgText">The svg document</param>
        /// <param name="msg">Not used, may be null</param>
        /// <param name="opt">The options</param>
        /// <returns>The minified svg</returns>
        /// <exception cref="XmlException">The text is not valid xml</exception>
        /// <exception cref="InvalidOperationException"><see cref="SvgMinifierParams.BitmapValidation"/> is enabled and an optimization changed the rendering
        /// (or the optimized document can't be rendered). If <see cref="SvgMinifierParams.ErrorDir"/> is set, the reference image, the failed image and the source svg
        /// are saved in that folder and their paths are in the message, nothing is written otherwise</exception>
        /// <exception cref="ArgumentNullException"><paramref name="svgText"/> is null, or <paramref name="opt"/> is null (DEBUG builds only)</exception>
        public static String Optimize(String svgText, IMessageHost msg, SvgMinifierParams opt)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(opt);
#endif//DEBUG
            var sourceText = svgText;
            XDocument xdoc;
            bool stripRoot = false;
            try
            {
                xdoc = XDocument.Parse(svgText, opt.RemoveWhiteSpaces ? LoadOptions.None : LoadOptions.PreserveWhitespace);
            }
            catch (XmlException ex)
            {
                if (ex.Message.FastStartsWith("There are multiple root elements"))
                {
                    stripRoot = true;
                    svgText = String.Concat("<DummyRoot>", svgText, "</DummyRoot>");
                    xdoc = XDocument.Parse(svgText, opt.RemoveWhiteSpaces ? LoadOptions.None : LoadOptions.PreserveWhitespace);
                }else
                {
                    throw;
                }
            }
            var xSvg = FindFirst(xdoc.Root, "svg");
            var va = xSvg?.Attribute("viewBox");
            double x = 0, y = 0, width = 0, height = 0;
            if (va != null)
            {
                var coords = va.Value.Trim().Split(SplitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (coords.Length >= 4)
                {
                    double.TryParse(coords[0], CultureInfo.InvariantCulture, out x);
                    double.TryParse(coords[1], CultureInfo.InvariantCulture, out y);
                    double.TryParse(coords[2], CultureInfo.InvariantCulture, out width);
                    double.TryParse(coords[3], CultureInfo.InvariantCulture, out height);
                }
            }
            xdoc.DocumentType?.Remove();

            bool haveViewport = width > 0 && height > 0;
            bool canDoVisualOpts = haveViewport;
            if (haveViewport || opt.RemoveSize)
            {
                RemoveAttribute(xSvg, "width");
                RemoveAttribute(xSvg, "height");
                bool doRemove(XAttribute v)
                {
                    if (double.TryParse(TrimNonDigits(v.Value), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && (d == 0))
                        return true;
                    canDoVisualOpts = false;
                    return false;
                }
                RemoveAttributeIf(xSvg, "x", doRemove);
                RemoveAttributeIf(xSvg, "y", doRemove);
            }

            int decimalLimit = opt.MaxDecimals;
            if (opt.MaxDecimalRes > 0)
            {
                //  TODO: Repsect scale transforms
                double trScale = 1;
                OnAllElementsX(xdoc.Root, element =>
                {
                    var tr = element.Attribute(XName.Get("transform"));
                    if (tr == null)
                        return;
                    var s = tr.Value.Split('(');
                    var sl = s.Length - 1;
                    for (int i = 0; i < sl; ++i)
                    {
                        var ss = s[i].Trim();
                        if (ss.EndsWith("scale", StringComparison.OrdinalIgnoreCase))
                        {
                            ss = s[i + 1].Trim();
                            var si = ss.IndexOf(')');
                            if (si >= 0)
                            {
                                ss = ss.Substring(0, si).TrimEnd().Replace(',', ' ');
                                var cords = ss.Split(' ');
                                foreach (var x in cords)
                                {
                                    if (double.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var zx))
                                    {
                                        if (zx > trScale)
                                            trScale = zx;
                                    }
                                }
                            }
                        }
                    }
                });
                if (haveViewport)
                {
                    var scale = Math.Ceiling((double)trScale * opt.MaxDecimalRes / Math.Min(width, height));
                    int decimals = 0;
                    double tscale = 1;
                    while (tscale < scale)
                    {
                        ++decimals;
                        tscale *= 10;
                    }
                    decimalLimit = decimals;
                }
            }

            var c = new OptContext
            {
                Doc = xdoc,
                Svg = xSvg,
                Opt = opt,
                SourceText = sourceText,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                CanDoVisualOpts = canDoVisualOpts,
                DecimalLimit = decimalLimit,
            };
            try
            {
                RunSteps(c);
            }
            finally
            {
                c.Ref?.Dispose();
            }

            String text;
            if (stripRoot)
            {
                text = String.Concat(xdoc.Root.Elements().Select(cc =>
                {
                    using var tw = new Utf8StringWriter();
                    cc.Save(tw, SaveOptions.OmitDuplicateNamespaces | (opt.RemoveWhiteSpaces ? SaveOptions.DisableFormatting : SaveOptions.None));
                    var t = tw.ToString();
                    t = t.Substring(t.FastIndexOf("?>") + 2);
                    return t;
                }));
            }
            else
            {
                using var tw = new Utf8StringWriter();
                xdoc.Save(tw, SaveOptions.OmitDuplicateNamespaces | (opt.RemoveWhiteSpaces ? SaveOptions.DisableFormatting : SaveOptions.None));
                text = tw.ToString();
            }

            int pos = -1;
            if (opt.RemoveDocType)
            {
                pos = text.IndexOf("?>", StringComparison.Ordinal);
                if (pos >= 0)
                    pos += 2;
            }
            else
            {
                pos = text.IndexOf("<?", StringComparison.Ordinal);
            }
            if (pos < 0)
                pos = 0;
            text = text.Substring(pos);
            if (opt.TrimTagClose)
                text = text.Replace(" />", "/>");
            return text;
        }


        #region Optimization steps (svg-cov-49)

        /// <summary>
        /// The state of one <see cref="Optimize"/> call, shared by the steps
        /// </summary>
        sealed class OptContext
        {
            public XDocument Doc;
            /// <summary>
            /// The first svg element (may be null)
            /// </summary>
            public XElement Svg;
            public SvgMinifierParams Opt;
            /// <summary>
            /// The svg text passed to <see cref="Optimize"/> (saved with the validation errors)
            /// </summary>
            public String SourceText;
            /// <summary>
            /// The view box of the first svg element (0 if none)
            /// </summary>
            public double X, Y, Width, Height;
            /// <summary>
            /// True if the document has a view box with a positive size (and no x / y offset), required by the rendering based optimizations
            /// </summary>
            public bool CanDoVisualOpts;
            /// <summary>
            /// The number of decimals to keep (from <see cref="SvgMinifierParams.MaxDecimals"/> or <see cref="SvgMinifierParams.MaxDecimalRes"/>), less than zero to keep all
            /// </summary>
            public int DecimalLimit;
            /// <summary>
            /// The running step (used in validation errors)
            /// </summary>
            public StepId Step;
            /// <summary>
            /// The bitmap validation reference image, rendered before a validated step (null if not rendered yet, or if rendering failed, see <see cref="RefFailed"/>)
            /// </summary>
            public Bitmap Ref;
            /// <summary>
            /// True if rendering the reference image failed (validation is skipped until a step changes the rendering)
            /// </summary>
            public bool RefFailed;

            /// <summary>
            /// Render the reference image (if not done already)
            /// </summary>
            public void EnsureRef()
            {
                if ((Ref != null) || RefFailed)
                    return;
                Ref = GetValidationRef(Doc);
                RefFailed = Ref == null;
            }

            /// <summary>
            /// A step changed the rendering on purpose (rounding, removed fills or background), the reference must be rendered again
            /// </summary>
            public void InvalidateRef()
            {
                Ref?.Dispose();
                Ref = null;
                RefFailed = false;
            }
        }

        /// <summary>
        /// Optimization steps in the order they are executed (the values are the indices in <see cref="Steps"/>)
        /// </summary>
        enum StepId
        {
            Comments,
            Namespaces,
            NonSvg,
            Title,
            Metadata,
            Hidden,
            Decimals,
            Fill,
            Background,
            AttributeSpaces,
            Colors,
            Version,
            TrailingZeros,
            UseSize,
            LeadingZeros,
            ShortenPath,
            Transform,
            Rules,
            Ids,
            RenameIds,
            Graphs,
            NamespaceCleanup,
            TextNodes,
        }

        [Flags]
        enum StepFlags
        {
            None = 0,
            /// <summary>
            /// The step must not change the rendering: if it changed the document, the document is compared with the reference image (if <see cref="SvgMinifierParams.BitmapValidation"/> is on)
            /// </summary>
            Validate = 1,
            /// <summary>
            /// Like <see cref="Validate"/>, but the step validates each change itself
            /// </summary>
            ValidatesItself = 2,
            /// <summary>
            /// The step changes the rendering on purpose, the validation reference image is rendered again after it changed the document
            /// </summary>
            ChangesRendering = 4,
        }

        /// <summary>
        /// An optimization step
        /// </summary>
        sealed class Step
        {
            public Step(StepId id, Func<SvgMinifierParams, bool> isEnabled, Func<OptContext, bool> run, StepFlags flags, params StepId[] enabledBy)
            {
                Id = id;
                IsEnabled = isEnabled;
                Run = run;
                Flags = flags;
                ulong m = 0;
                foreach (var e in enabledBy)
                    m |= 1UL << (int)e;
                EnabledBy = m;
            }
            public readonly StepId Id;
            /// <summary>
            /// True if the step is enabled by the options
            /// </summary>
            public readonly Func<SvgMinifierParams, bool> IsEnabled;
            /// <summary>
            /// Execute the step, returns true if the document was changed.
            /// A step must reach its own fixed point (running it again directly after itself doesn't change anything).
            /// </summary>
            public readonly Func<OptContext, bool> Run;
            public readonly StepFlags Flags;
            /// <summary>
            /// A bit mask of the steps (1 &lt;&lt; StepId) whose changes can enable further optimizations of this step.
            /// After its first run, the step is only executed again if one of these steps changed the document since then.
            /// </summary>
            public readonly ulong EnabledBy;
        }

        /// <summary>
        /// The maximum number of passes over the steps (the first pass runs all enabled steps, the following passes only the steps that may find more to do)
        /// </summary>
        const int MaxPasses = 8;

        /// <summary>
        /// All optimization steps, in execution order, with the steps that can enable further optimizations of each step (the dependency graph).
        /// The order is chosen so that most dependencies point backwards (one pass is usually enough):
        /// - Element removal (NonSvg, Title, Metadata, Hidden, Background, Graphs) and attribute removal (NonSvg, Fill, Rules, Ids, UseSize, Version)
        ///   can make ids unused (Ids, RenameIds), g elements attribute-less or empty (Graphs), namespace declarations unused (NamespaceCleanup),
        ///   and text nodes neighbours (TextNodes).
        /// - Ids can remove the last attribute of a g element (Graphs), Graphs can remove an empty g element with an id or a reference (Ids, RenameIds).
        ///   These two are the only dependency cycle that points forward (rare, handled by a second pass).
        /// - RenameIds depends on everything that changes the use counts of ids (Ids and all removals above), renaming an already renamed document is a no-op.
        /// - The rendering based Rules (clip-rule / fill-rule / attribute removal) depends on steps that change the geometry or the painting
        ///   (Decimals, Transform rounding, Fill, Background), so it runs after them.
        /// - Background (the first rect) depends on element removals before it and on the rounding (Decimals).
        /// - UseSize ("100%") depends on TrailingZeros ("100.0%").
        /// - The value formatting steps (AttributeSpaces, Colors, TrailingZeros, LeadingZeros, ShortenPath, Transform) are idempotent, run after the rounding (Decimals)
        ///   in an order where each one keeps the output of the previous ones, and are not affected by removals: they run once.
        /// - Comments, Namespaces, NonSvg, Title, Metadata, Decimals, Fill and Version don't depend on any other step: they run once.
        /// </summary>
        static readonly Step[] Steps = MakeSteps(
            new(StepId.Comments, o => o.RemoveComments, StepRemoveComments, StepFlags.None),
            new(StepId.Namespaces, o => o.RemoveNamespaces, StepRemoveNamespaces, StepFlags.None),
            new(StepId.NonSvg, o => o.RemoveNonSvg, StepRemoveNonSvg, StepFlags.Validate),
            new(StepId.Title, o => o.RemoveTitle, c => RemoveElements(c, "title"), StepFlags.None),
            new(StepId.Metadata, o => o.RemoveMetadata, c => RemoveElements(c, "metadata"), StepFlags.None),
            //  A foreign display attribute (removed by NonSvg) may hide the svg one
            new(StepId.Hidden, o => o.RemoveHidden, StepRemoveHidden, StepFlags.None,
                StepId.NonSvg),
            new(StepId.Decimals, o => true, StepDecimals, StepFlags.ChangesRendering),
            new(StepId.Fill, o => o.RemoveFill, StepRemoveFill, StepFlags.ChangesRendering),
            //  Removing elements can make another rect the first one, rounding can make it match the view box
            new(StepId.Background, o => o.RemoveBackground, StepRemoveBackground, StepFlags.ChangesRendering,
                StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Decimals),
            new(StepId.AttributeSpaces, o => o.RemoveAttributeSpaces, StepRemoveAttributeSpaces, StepFlags.Validate),
            new(StepId.Colors, o => o.ShortenColors, StepShortenColors, StepFlags.Validate),
            new(StepId.Version, o => o.RemoveVersion, StepRemoveVersion, StepFlags.None),
            new(StepId.TrailingZeros, o => o.TrimTrailingZeros, StepTrimTrailingZeros, StepFlags.Validate),
            //  "100.0%" becomes "100%"
            new(StepId.UseSize, o => o.RemoveUseSize, StepRemoveUseSize, StepFlags.None,
                StepId.TrailingZeros),
            new(StepId.LeadingZeros, o => o.TrimLeadingZeros, StepTrimLeadingZeros, StepFlags.Validate),
            new(StepId.ShortenPath, o => o.ShortenPath, StepShortenPath, StepFlags.Validate),
            new(StepId.Transform, o => o.TrimTransform, StepTrimTransform, StepFlags.ValidatesItself),
            //  The rendering based removal depends on the geometry and painting
            new(StepId.Rules, o => o.RemoveClipRule || o.RemoveFillRule || o.TryRemoveAttributes, StepRemoveRules, StepFlags.None,
                StepId.Decimals, StepId.Fill, StepId.Background, StepId.Transform),
            //  Removed elements / attributes can make ids unused or references missing, Graphs can remove an empty g with an id
            new(StepId.Ids, o => o.RemoveIds, StepRemoveIds, StepFlags.None,
                StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Fill, StepId.Background, StepId.Rules, StepId.Graphs),
            //  Anything that changes the use counts of the ids
            new(StepId.RenameIds, o => o.RenameIds, StepRenameIds, StepFlags.None,
                StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Fill, StepId.Background, StepId.Rules, StepId.Ids, StepId.Graphs),
            //  Removed attributes (of g elements) or removed child elements
            new(StepId.Graphs, o => o.RemoveGraphs, StepRemoveGraphs, StepFlags.None,
                StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Fill, StepId.Background, StepId.Ids),
            //  Any removed element or attribute can make a namespace unused
            new(StepId.NamespaceCleanup, o => true, StepRemoveUnusedNamespaces, StepFlags.None,
                StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Fill, StepId.Background, StepId.Version, StepId.UseSize, StepId.Rules, StepId.Ids, StepId.Graphs),
            //  Removed elements can leave white space text nodes
            new(StepId.TextNodes, o => o.RemoveTextNodes, StepRemoveTextNodes, StepFlags.None,
                StepId.Comments, StepId.NonSvg, StepId.Title, StepId.Metadata, StepId.Hidden, StepId.Background, StepId.Graphs)
        );

        static Step[] MakeSteps(params Step[] steps)
        {
            var l = steps.Length;
            if (l > 64)
                throw new InvalidOperationException("Too many optimization steps for the dependency mask");
            for (int i = 0; i < l; ++i)
            {
                if ((int)steps[i].Id != i)
                    throw new InvalidOperationException("The optimization steps must be in the StepId order, " + steps[i].Id + " found at index " + i);
            }
            return steps;
        }

        /// <summary>
        /// Run the steps until no step can find anything more to do (or <see cref="MaxPasses"/> is reached).
        /// The first pass runs all enabled steps, later passes only run a step if one of the steps in its <see cref="Step.EnabledBy"/> changed the document after its last run.
        /// </summary>
        static void RunSteps(OptContext c)
        {
            var opt = c.Opt;
            var validate = opt.BitmapValidation;
            var steps = Steps;
            var sc = steps.Length;
            //  The sequence number of the last run / last change of each step (0 = never)
            Span<int> ranAt = stackalloc int[sc];
            Span<int> changedAt = stackalloc int[sc];
            int seq = 0;
            ulong enabled = 0;
            for (int i = 0; i < sc; ++i)
            {
                if (steps[i].IsEnabled(opt))
                    enabled |= 1UL << i;
            }
            for (int pass = 0; pass < MaxPasses; ++pass)
            {
                bool ranAny = false;
                for (int i = 0; i < sc; ++i)
                {
                    if ((enabled & (1UL << i)) == 0)
                        continue;
                    var s = steps[i];
                    var last = ranAt[i];
                    if (last > 0)
                    {
                        //  Only run again if a step that can enable more optimizations changed the document since the last run
                        bool pending = false;
                        var m = s.EnabledBy & enabled;
                        while (m != 0)
                        {
                            if (changedAt[BitOperations.TrailingZeroCount(m)] > last)
                            {
                                pending = true;
                                break;
                            }
                            m &= m - 1;
                        }
                        if (!pending)
                            continue;
                    }
                    ranAny = true;
                    var flags = s.Flags;
                    c.Step = s.Id;
                    if (validate && ((flags & (StepFlags.Validate | StepFlags.ValidatesItself)) != 0))
                        c.EnsureRef();
                    var changed = s.Run(c);
                    ranAt[i] = ++seq;
                    if (!changed)
                        continue;
                    changedAt[i] = seq;
                    if ((flags & StepFlags.ChangesRendering) != 0)
                        c.InvalidateRef();
                    else if ((flags & StepFlags.Validate) != 0)
                        Validate(c);
                }
                if (!ranAny)
                    break;
            }
        }

        static bool StepRemoveComments(OptContext c)
        {
            List<XComment> l = null;
            foreach (var n in c.Doc.DescendantNodes())
            {
                if (n is XComment x)
                    (l ??= new()).Add(x);
            }
            if (l == null)
                return false;
            foreach (var n in l)
                n.Remove();
            return true;
        }

        static bool StepRemoveNamespaces(OptContext c)
        {
            var xSvg = c.Svg;
            if (xSvg == null)
                return false;
            List<XAttribute> doDel = null;
            foreach (var a in xSvg.Attributes())
            {
                if (!a.IsNamespaceDeclaration)
                    continue;
                if (!a.Name.LocalName.FastEquals("xmlns"))
                    if (!a.Name.LocalName.FastEquals("xlink"))
                        (doDel ??= new()).Add(a);
            }
            if (doDel == null)
                return false;
            foreach (var a in doDel)
                a.Remove();
            return true;
        }

        static bool StepRemoveNonSvg(OptContext c)
        {
            List<XElement> delEl = null;
            List<XAttribute> delAtrs = null;
            foreach (var el in c.Doc.Descendants())
            {
                if (!KeepNamespaces.Contains(el.Name.NamespaceName))
                {
                    (delEl ??= new()).Add(el);
                    continue;
                }
                foreach (var at in el.Attributes())
                {
                    if (!KeepNamespaces.Contains(at.Name.NamespaceName))
                        (delAtrs ??= new()).Add(at);
                }
            }
            if (delAtrs != null)
            {
                foreach (var d in delAtrs)
                    d.Remove();
            }
            if (delEl != null)
            {
                foreach (var d in delEl)
                    d.Remove();
            }
            return (delEl != null) || (delAtrs != null);
        }

        static bool RemoveElements(OptContext c, String localName)
        {
            List<XElement> els = null;
            foreach (var el in c.Doc.Descendants())
            {
                if (el.Name.LocalName == localName)
                    (els ??= new()).Add(el);
            }
            if (els == null)
                return false;
            var ec = els.Count;
            while (ec > 0)
            {
                --ec;
                els[ec].Remove();
            }
            return true;
        }

        static bool StepRemoveHidden(OptContext c)
        {
            List<XElement> els = null;
            foreach (var el in c.Doc.Descendants())
            {
                var da = el.Attributes().LastOrDefault(y => y.Name.LocalName == "display");
                if (da == null)
                    continue;
                if (da.Value?.Trim() == "none")
                    (els ??= new()).Add(el);
            }
            if (els == null)
                return false;
            //  Removing an element that is inside an already removed element is harmless
            foreach (var el in els)
                el.Remove();
            return true;
        }

        static bool StepDecimals(OptContext c)
        {
            bool changed = false;
            var decimalLimit = c.DecimalLimit;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                var en = element.Name.LocalName;
                if (decimalLimit >= 0)
                {
                    if (DecimalLimits.TryGetValue(en, out var attrs))
                    {
                        foreach (var attr in element.Attributes())
                        {
                            var an = attr.Name.LocalName;
                            if (an != "transform")
                                if (Array.IndexOf(attrs, an) < 0)
                                    continue;
                            var org = attr.Value;
                            var val = SetMaxDecimals(org, decimalLimit, an == "d");
                            if (!ReferenceEquals(org, val))
                            {
                                attr.Value = val;
                                changed = true;
                            }
                        }
                    }
                }
                if (DecimalLimitsLong.TryGetValue(en, out var lattrs))
                {
                    //  Animating the path data
                    var isPath = element.Attribute("attributeName")?.Value?.Trim() == "d";
                    foreach (var attr in element.Attributes())
                    {
                        var an = attr.Name.LocalName;
                        if (Array.IndexOf(lattrs, an) < 0)
                            continue;
                        //  Only the values of numerical attributes (ex: not attributeName="class", svg-cov-29)
                        if (!IsNumericAttribute(element, an))
                            continue;
                        var org = attr.Value;
                        var val = SetMaxDecimals(org, 10, isPath);
                        if (!ReferenceEquals(org, val))
                        {
                            attr.Value = val;
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        static bool StepRemoveFill(OptContext c)
        {
            var xSvg = c.Svg;
            bool didRemove = false;
            OnAllElementsX(c.Doc.Root, element => RemoveAttributeIf(element, "fill", a =>
            {
                if (element == xSvg)
                    return false;
                if (a.Value == "none")
                    return false;
                didRemove = true;
                return true;
            }));
            bool changed = didRemove;
            RemoveAttributeIf(xSvg, "fill", a =>
            {
                if (didRemove || (a.Value != "none"))
                {
                    changed = true;
                    return true;
                }
                return false;
            });
            return changed;
        }

        static bool StepRemoveBackground(OptContext c)
        {
            bool changed = false;
            //  Repeat, the next rect may match too (a second Optimize call would remove it)
            for (; ; )
            {
                var e = FindFirst(c.Doc.Root, "rect");
                if (!MatchSize(e, c.X, c.Y, c.Width, c.Height))
                    break;
                e.Remove();
                changed = true;
            }
            var xSvg = c.Svg;
            if (xSvg?.Attribute("enable-background") != null)
            {
                RemoveAttribute(xSvg, "enable-background");
                changed = true;
            }
            return changed;
        }

        static bool StepRemoveAttributeSpaces(OptContext c)
        {
            bool changed = false;
            foreach (var el in c.Doc.Descendants())
            {
                foreach (var at in el.Attributes())
                {
                    var org = at.Value;
                    var value = org.Replace(", ", ",");
                    for (; ; )
                    {
                        var p = value;
                        value = value.Replace("  ", " ");
                        if (p == value)
                            break;
                    }
                    if (org != value)
                    {
                        at.Value = value;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        static bool StepShortenColors(OptContext c)
        {
            bool changed = false;
            var colA = ColorAttributes;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                foreach (var attr in element.Attributes())
                {
                    if (!colA.Contains(attr.Name.LocalName.FastToLower()))
                        continue;
                    var org = attr.Value;
                    var color = HtmlColors.GetShortest(org);
                    if (color != org)
                    {
                        attr.Value = color;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        static bool StepRemoveVersion(OptContext c)
        {
            bool changed = false;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                if (element.Name.LocalName.FastToLower() != "svg")
                    continue;
                var attr = element.Attribute(XName.Get("version"));
                if (attr == null)
                    continue;
                attr.Remove();
                changed = true;
            }
            return changed;
        }

        static bool StepTrimTrailingZeros(OptContext c)
        {
            bool changed = false;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                foreach (var attr in element.Attributes())
                {
                    if (attr.IsNamespaceDeclaration)
                        continue;
                    var xn = attr.Name;
                    //  Only svg attributes (svg-cov-29)
                    if (xn.NamespaceName.Length > 0)
                        continue;
                    var an = xn.LocalName;
                    var org = attr.Value;
                    String val;
                    if (an == "style")
                        val = TrimTrailingZerosStyle(org);
                    else if (IsNumericAttribute(element, an))
                        val = TrimTrailingZeros(org);
                    else
                        continue;
                    if (!ReferenceEquals(org, val))
                    {
                        attr.Value = val;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        static bool StepRemoveUseSize(OptContext c)
        {
            bool changed = false;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                if (element.Name.LocalName != "use")
                    continue;
                RemoveAttributeIf(element, "width", x =>
                {
                    if (x.Value != "100%")
                        return false;
                    changed = true;
                    return true;
                });
                RemoveAttributeIf(element, "height", x =>
                {
                    if (x.Value != "100%")
                        return false;
                    changed = true;
                    return true;
                });
            }
            return changed;
        }

        static bool StepTrimLeadingZeros(OptContext c)
        {
            bool changed = false;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                var elName = element.Name.LocalName;
                if (!DecimalLimits.TryGetValue(elName, out var a))
                    continue;
                bool isPath = elName == "path";
                foreach (var attrName in a)
                {
                    var attr = element.Attribute(XName.Get(attrName));
                    if (attr == null)
                        continue;
                    var org = attr.Value;
                    var val = TrimLeadingZeros(org, isPath && (attrName == "d"));
                    if (val != org)
                    {
                        attr.Value = val;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        static bool StepShortenPath(OptContext c)
        {
            bool changed = false;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                if (element.Name.LocalName.FastToLower() != "path")
                    continue;
                var attr = element.Attribute(XName.Get("d"));
                if (attr == null)
                    continue;
                var org = attr.Value;
                var path = OptimizePath(org);
                if (org != path)
                {
                    attr.Value = path;
                    changed = true;
                }
            }
            return changed;
        }

        static bool StepTrimTransform(OptContext c)
        {
            bool changed = false;
            var decimalLimit = c.DecimalLimit;
            foreach (var element in c.Doc.Root.DescendantsAndSelf())
            {
                var attr = element.Attribute(XName.Get("transform"));
                if (attr == null)
                    continue;
                var org = attr.Value;
                var val = FixTransform(org, decimalLimit);
                if (org != val)
                {
                    attr.Value = val;
                    changed = true;
                    Validate(c);
                }
            }
            return changed;
        }

        static bool StepRemoveRules(OptContext c)
        {
            if (!c.CanDoVisualOpts)
                return false;
            var opt = c.Opt;
            var xSvg = c.Svg;
            bool changed = false;
            //  Rendered when the first attribute is tested (most documents have none)
            Bitmap refImage = null;
            bool refFailed = false;
            bool tryRemove(XElement element, String name)
            {
                //  The document can't be rendered: nothing can be verified (don't render it again for every attribute)
                if (refFailed)
                    return false;
                if (element.Attribute(name) == null)
                    return false;
                if (refImage == null)
                {
                    refImage = GetValidationRef(element.Document);
                    if (refImage == null)
                    {
                        refFailed = true;
                        return false;
                    }
                }
                return TryRemoveAttribute(ref refImage, element, name);
            }
            try
            {
                //  Repeat until nothing more is removed, a removal can make another attribute removable (ex: a removed size hides an element,
                //  a removed fill-rule adds coverage that hides the change of another element)
                for (; ; )
                {
                    bool removed = false;
                    foreach (var element in c.Doc.Root.DescendantsAndSelf())
                    {
                        if (!DecimalLimits.ContainsKey(element.Name.LocalName))
                            continue;
                        if (element == xSvg)
                            continue;
                        if (opt.TryRemoveAttributes)
                        {
                            var attr = element.Attributes();
                            foreach (var at in attr.Select(x => x.Name.LocalName).ToList())
                                removed |= tryRemove(element, at);
                        }
                        else
                        {
                            if (opt.RemoveClipRule)
                                removed |= tryRemove(element, "clip-rule");
                            if (opt.RemoveFillRule)
                                removed |= tryRemove(element, "fill-rule");
                        }
                    }
                    if (!removed)
                        break;
                    changed = true;
                }
            }
            finally
            {
                if (refImage != null)
                    refImage.Dispose();
            }
            return changed;
        }

        static bool StepRemoveIds(OptContext c)
        {
            bool changed = false;
            for (; ; )
            {
                var data = GetIds(c.Doc);
                //  Set if a removed value also referenced an id that wasn't removable (its use count dropped, it may be removable now)
                bool again = false;
                foreach (var attr in data.Attributes)
                {
                    if (TryReadId(attr.Name.LocalName, attr.Value, out var aids))
                        changed |= RemoveIds(attr, aids, data, ref again);
                }
                if (!again)
                    return changed;
            }
        }

        static bool StepRenameIds(OptContext c) => RenameIds(GetIds(c.Doc));

        static bool StepRemoveGraphs(OptContext c)
        {
            var xdoc = c.Doc;
            bool changed = false;
            //  Unwrap g elements without attributes (unless they have an animation child), in document order (nested ones are unwrapped later)
            List<XElement> gs = null;
            foreach (var el in xdoc.Descendants())
            {
                if (el.Name.LocalName == "g")
                    (gs ??= new()).Add(el);
            }
            if (gs == null)
                return false;
            foreach (var x in gs)
            {
                //  The root element can't be unwrapped
                if (x.Parent == null)
                    continue;
                if (x.Attributes().FirstOrDefault() != null)
                    continue;
                bool haveAnim = false;
                foreach (var v in x.Elements())
                {
                    haveAnim = v.Name.LocalName.FastStartsWith("animate");
                    if (haveAnim)
                        break;
                }
                if (haveAnim)
                    continue;
                var addAfter = x;
                for (; ; )
                {
                    var t = x.Elements().FirstOrDefault();
                    if (t == null)
                        break;
                    t.Remove();
                    addAfter.AddAfterSelf(t);
                    addAfter = t;
                }
                x.Remove();
                changed = true;
            }
            //  Remove empty g elements, children first (a g that only contains empty g elements is removed too)
            for (int i = gs.Count - 1; i >= 0; --i)
            {
                var x = gs[i];
                //  Unwrapped (removed) or the root element
                if (x.Parent == null)
                    continue;
                if (x.Elements().FirstOrDefault() != null)
                    continue;
                x.Remove();
                changed = true;
            }
            return changed;
        }

        static bool StepRemoveUnusedNamespaces(OptContext c)
        {
            var xdoc = c.Doc;
            HashSet<String> namespaces = new HashSet<string>();
            foreach (var el in xdoc.Descendants())
            {
                namespaces.Add(el.Name.NamespaceName);
                foreach (var at in el.Attributes())
                {
                    if (!at.IsNamespaceDeclaration)
                        namespaces.Add(at.Name.NamespaceName);
                }
            }
            bool changed = false;
            List<XAttribute> toDelete = null;
            foreach (var el in xdoc.Descendants())
            {
                foreach (var at in el.Attributes())
                {
                    if (!at.IsNamespaceDeclaration)
                        continue;
                    if (namespaces.Contains(at.Value))
                        continue;
                    (toDelete ??= new()).Add(at);
                }
                if (toDelete == null)
                    continue;
                var l = toDelete.Count;
                if (l <= 0)
                    continue;
                while (l > 0)
                {
                    --l;
                    toDelete[l].Remove();
                }
                toDelete.Clear();
                changed = true;
            }
            return changed;
        }

        static bool StepRemoveTextNodes(OptContext c)
        {
            List<XText> l = null;
            foreach (var n in c.Doc.DescendantNodes())
            {
                if ((n is XText t) && String.IsNullOrEmpty(t.Value?.Trim()))
                    (l ??= new()).Add(t);
            }
            if (l == null)
                return false;
            foreach (var t in l)
                t.Remove();
            return true;
        }

        #endregion//Optimization steps (svg-cov-49)


        /// <summary>
        /// A StringWriter that reports UTF-8 as its encoding, so that a saved xml declaration says encoding="utf-8" (svg-cov-30)
        /// </summary>
        sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter() : base(CultureInfo.InvariantCulture)
            {
            }

            public override Encoding Encoding => Encoding.UTF8;
        }


        /// <summary>
        /// The ids and id references of a document
        /// </summary>
        sealed class IdData
        {
            /// <summary>
            /// The number of uses (id attributes + references) per id
            /// </summary>
            public readonly Dictionary<String, int> Hist = new(StringComparer.Ordinal);
            /// <summary>
            /// All ids (defined or referenced) in the order of their first occurrence
            /// </summary>
            public readonly List<String> Order = new();
            /// <summary>
            /// All attributes that define or reference an id (id, href="#..", url(#..))
            /// </summary>
            public readonly List<XAttribute> Attributes = new();
            /// <summary>
            /// The texts of style elements with url(#id) references (they are counted and renamed, but never removed)
            /// </summary>
            public readonly List<XText> StyleTexts = new();
            /// <summary>
            /// Ids defined by an id attribute
            /// </summary>
            public readonly HashSet<String> Defined = new(StringComparer.Ordinal);
            /// <summary>
            /// Text that may reference ids, but can't be rewritten (style elements without their url(#id) references, script elements, event, animation timing and aria attributes), null if none
            /// </summary>
            public String OpaqueText;
            /// <summary>
            /// The words (runs of letters, digits, '_' and '-') of the <see cref="OpaqueText"/>, null if none
            /// </summary>
            public HashSet<String> OpaqueWords;

            /// <summary>
            /// True if the name may be used by the opaque text (conservative)
            /// </summary>
            public bool IsOpaque(String name)
            {
                var w = OpaqueWords;
                if (w == null)
                    return false;
                if (w.Contains(name))
                    return true;
                return (!IsWord(name)) && OpaqueText.Contains(name, StringComparison.Ordinal);
            }

            /// <summary>
            /// True if an id can be renamed (it's defined in the document and not used by any opaque text)
            /// </summary>
            public bool CanRename(String id) => Defined.Contains(id) && !IsOpaque(id);

            /// <summary>
            /// True if a reference or an id that is used once can be removed
            /// </summary>
            public bool CanRemove(String id) => (Hist[id] == 1) && !IsOpaque(id);
        }

        static bool IsWordChar(Char c) => Char.IsLetterOrDigit(c) || (c == '_') || (c == '-');

        static bool IsWord(String s)
        {
            foreach (var c in s)
                if (!IsWordChar(c))
                    return false;
            return true;
        }

        static void AddWords(HashSet<String> words, String text)
        {
            var tl = text.Length;
            int i = 0;
            while (i < tl)
            {
                if (!IsWordChar(text[i]))
                {
                    ++i;
                    continue;
                }
                var s = i;
                while ((i < tl) && IsWordChar(text[i]))
                    ++i;
                words.Add(text.Substring(s, i - s));
            }
        }

        /// <summary>
        /// Attributes that may reference ids in a way that the minifier can't rewrite
        /// </summary>
        static readonly HashSet<String> OpaqueAttributes = new(StringComparer.Ordinal)
        {
            "begin", "end",
            "aria-activedescendant", "aria-controls", "aria-describedby", "aria-details", "aria-errormessage", "aria-flowto", "aria-labelledby", "aria-owns",
        };

        static IdData GetIds(XDocument xdoc)
        {
            var data = new IdData();
            var hist = data.Hist;
            void count(String[] ids)
            {
                foreach (var id in ids)
                {
                    if (hist.TryGetValue(id, out var c))
                    {
                        hist[id] = c + 1;
                    }
                    else
                    {
                        hist.Add(id, 1);
                        data.Order.Add(id);
                    }
                }
            }
            StringBuilder opaque = null;
            foreach (var element in xdoc.Root.DescendantsAndSelf())
            {
                var en = element.Name.LocalName;
                if (en == "style")
                {
                    //  Css: url(#id) references are rewritten, anything else (such as "#id" selectors) is opaque
                    foreach (var node in element.Nodes())
                    {
                        if (node is not XText t)
                            continue;
                        var v = t.Value;
                        if (TryReadId(en, v, out var sids))
                        {
                            data.StyleTexts.Add(t);
                            count(sids);
                            v = RemoveUrlIds(v);
                        }
                        (opaque ??= new StringBuilder()).Append(v).Append('\n');
                    }
                }
                else if (en == "script")
                {
                    (opaque ??= new StringBuilder()).Append(element.Value).Append('\n');
                }
                foreach (var attr in element.Attributes())
                {
                    if (attr.IsNamespaceDeclaration)
                        continue;
                    var an = attr.Name.LocalName;
                    if (OpaqueAttributes.Contains(an) || an.StartsWith("on", StringComparison.Ordinal))
                    {
                        (opaque ??= new StringBuilder()).Append(attr.Value).Append('\n');
                        continue;
                    }
                    if (!TryReadId(an, attr.Value, out var aids))
                        continue;
                    data.Attributes.Add(attr);
                    if (an == "id")
                        data.Defined.Add(aids[0]);
                    count(aids);
                }
            }
            if (opaque != null)
            {
                var text = opaque.ToString();
                data.OpaqueText = text;
                var words = new HashSet<String>(StringComparer.Ordinal);
                AddWords(words, text);
                data.OpaqueWords = words;
            }
            return data;
        }

        /// <summary>
        /// Read the ids defined (id attribute) or referenced (href="#id" or url(#id), several are possible) by an attribute.
        /// Empty ids are ignored.
        /// </summary>
        static bool TryReadId(String attr, String value, out String[] id)
        {
            if (attr == "id")
            {
                value = value.Trim();
                if (value.Length > 0)
                {
                    id = [value];
                    return true;
                }
                id = null;
                return false;
            }
            if (attr == "href")
            {
                value = value.Trim();
                if ((value.Length > 1) && (value[0] == '#'))
                {
                    id = [value.Substring(1)];
                    return true;
                }
                id = null;
                return false;
            }
            var f = value.IndexOf("url(#", StringComparison.Ordinal);
            if (f < 0)
            {
                id = null;
                return false;
            }
            List<String> cids = null;
            for (; ; )
            {
                var s = f + 5;
                var e = value.IndexOf(')', s);
                if (e < 0)
                    break;
                var t = value.AsSpan(s, e - s).Trim();
                if (t.Length > 0)
                    (cids ??= new List<String>(1)).Add(t.ToString());
                f = value.IndexOf("url(#", e + 1, StringComparison.Ordinal);
                if (f < 0)
                    break;
            }
            if (cids != null)
            {
                id = cids.ToArray();
                return true;
            }
            id = null;
            return false;
        }


        static readonly HashSet<String> Keep = new HashSet<string>()
        {
            "fill", "stroke",
        };

        /// <summary>
        /// Remove the references to removable ids (used once, see <see cref="IdData.CanRemove(string)"/>) of an attribute (or an unused id attribute).
        /// In a style attribute only the css declarations that reference such an id are removed (svg-cov-46), the attribute is removed if it becomes empty.
        /// A fill or stroke attribute is kept, any other attribute is removed.
        /// </summary>
        /// <param name="x">The attribute</param>
        /// <param name="ids">The ids the attribute defines or references</param>
        /// <param name="data">The ids of the document</param>
        /// <param name="collateral">Set to true if a removed value also referenced an id that can't be removed (its use count dropped, it may be removable now)</param>
        /// <returns>True if the attribute was changed or removed</returns>
        static bool RemoveIds(XAttribute x, String[] ids, IdData data, ref bool collateral)
        {
            bool any = false;
            bool other = false;
            foreach (var id in ids)
            {
                if (data.CanRemove(id))
                    any = true;
                else
                    other = true;
            }
            if (!any)
                return false;
            var name = x.Name.LocalName;
            if (name == "style")
            {
                var decls = x.Value.Split(';');
                var sb = new StringBuilder(x.Value.Length);
                bool first = true;
                bool empty = true;
                foreach (var decl in decls)
                {
                    if (TryReadId(name, decl, out var dids))
                    {
                        bool remove = false;
                        bool otherInDecl = false;
                        foreach (var id in dids)
                        {
                            if (data.CanRemove(id))
                                remove = true;
                            else
                                otherInDecl = true;
                        }
                        if (remove)
                        {
                            collateral |= otherInDecl;
                            continue;
                        }
                    }
                    if (!first)
                        sb.Append(';');
                    first = false;
                    sb.Append(decl);
                    if (empty)
                        empty = String.IsNullOrWhiteSpace(decl);
                }
                if (empty)
                    x.Remove();
                else
                    x.Value = sb.ToString();
                return true;
            }
            if (Keep.Contains(name))
                return false;
            collateral |= other;
            x.Remove();
            return true;
        }

        /// <summary>
        /// Rename all url(#id) references of a value using the map (in one pass, svg-cov-33)
        /// </summary>
        /// <returns>The new value, or the value itself if nothing changed</returns>
        static String RenameUrlIds(String value, Dictionary<String, String> map)
        {
            var lookup = map.GetAlternateLookup<ReadOnlySpan<Char>>();
            StringBuilder sb = null;
            //  The value is copied to sb up until last
            int last = 0;
            int s = 0;
            for (; ; )
            {
                var f = value.IndexOf("url(#", s, StringComparison.Ordinal);
                if (f < 0)
                    break;
                var ns = f + 5;
                var e = value.IndexOf(')', ns);
                if (e < 0)
                    break;
                if (lookup.TryGetValue(value.AsSpan(ns, e - ns).Trim(), out var newId))
                {
                    sb ??= new StringBuilder(value.Length);
                    //  Text before the id (including "url(#"), the ')' is copied later
                    sb.Append(value, last, ns - last);
                    sb.Append(newId);
                    last = e;
                }
                s = e + 1;
            }
            if (sb == null)
                return value;
            sb.Append(value, last, value.Length - last);
            return sb.ToString();
        }

        /// <summary>
        /// Remove the ids of all url(#id) references of a value ("url(#a)" =&gt; "url(#)")
        /// </summary>
        static String RemoveUrlIds(String value)
        {
            var sb = new StringBuilder(value.Length);
            int last = 0;
            int s = 0;
            for (; ; )
            {
                var f = value.IndexOf("url(#", s, StringComparison.Ordinal);
                if (f < 0)
                    break;
                var ns = f + 5;
                var e = value.IndexOf(')', ns);
                if (e < 0)
                    break;
                sb.Append(value, last, ns - last);
                last = e;
                s = e + 1;
            }
            sb.Append(value, last, value.Length - last);
            return sb.ToString();
        }

        /// <summary>
        /// Rename all ids that can be renamed (see <see cref="IdData.CanRename(string)"/>) to short names, the most used id (definition + references) gets "a" (ties: first occurrence first).
        /// Generated names never equal an id that isn't renamed (references to missing ids, ids used by opaque text) or a word of the opaque text.
        /// All references (id, href="#id", url(#id) in attributes and in style elements) are updated, each value is rewritten once (svg-cov-33 / svg-cov-34).
        /// Renaming an already renamed document doesn't change it (the names are assigned in the same order).
        /// </summary>
        /// <returns>True if any value was changed</returns>
        static bool RenameIds(IdData data)
        {
            var hist = data.Hist;
            List<String> rename = null;
            HashSet<String> reserved = null;
            foreach (var id in data.Order)
            {
                if (data.CanRename(id))
                    (rename ??= new List<String>()).Add(id);
                else
                    (reserved ??= new HashSet<String>(StringComparer.Ordinal)).Add(id);
            }
            if (rename == null)
                return false;
            var map = new Dictionary<String, String>(rename.Count, StringComparer.Ordinal);
            var opaqueWords = data.OpaqueWords;
            int index = 0;
            bool anyNew = false;
            //  OrderByDescending is stable: ties keep the first occurrence order
            foreach (var id in rename.OrderByDescending(x => hist[x]))
            {
                String n;
                do
                {
                    n = GenShort(index);
                    ++index;
                }
                while (((reserved != null) && reserved.Contains(n)) || ((opaqueWords != null) && opaqueWords.Contains(n)));
                map.Add(id, n);
                if (!anyNew)
                    anyNew = id != n;
            }
            //  All ids keep their names (already renamed), only white space around the ids could change: nothing to do
            if (!anyNew)
                return false;
            bool changed = false;
            foreach (var attr in data.Attributes)
            {
                var name = attr.Name.LocalName;
                var value = attr.Value;
                String nv;
                if (name == "id")
                {
                    if (!map.TryGetValue(value.Trim(), out nv))
                        continue;
                }
                else if (name == "href")
                {
                    if (!map.TryGetValue(value.Trim().Substring(1), out nv))
                        continue;
                    nv = "#" + nv;
                }
                else
                {
                    nv = RenameUrlIds(value, map);
                }
                if (nv != value)
                {
                    attr.Value = nv;
                    changed = true;
                }
            }
            foreach (var t in data.StyleTexts)
            {
                var value = t.Value;
                var nv = RenameUrlIds(value, map);
                if (nv != value)
                {
                    t.Value = nv;
                    changed = true;
                }
            }
            return changed;
        }



        static Func<SvgElement, T> GetInternalProp<T>(String name)
        {
            var c = Expression.Variable(typeof(SvgElement));
            return Expression.Lambda<Func<SvgElement, T>>(
                Expression.Property(c, typeof(SvgElement).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)), c).Compile();
        }

        static readonly Func<SvgElement, SvgAttributeCollection> GetAttributes = GetInternalProp<SvgAttributeCollection>("Attributes");
        static readonly Func<SvgElement, String> GetElementName = GetInternalProp<String>("ElementName");



        static void OnAllElements<T>(SvgElement doc, Action<SvgElement, T> onNode, T t)
        {
            onNode(doc, t);
            foreach (var x in doc.Descendants())
                OnAllElements(x, onNode, t);
        }

        static void OnAllElements(SvgElement element, Action<SvgElement> onElement)
        {
            onElement(element);
            foreach (var x in element.Descendants())
                OnAllElements(x, onElement);
        }

        static void OnAllElementsX(XElement element, Action<XElement> onElement)
        {
            foreach (var x in element.DescendantsAndSelf())
                onElement(x);
        }


        static void RemoveAttribute(XElement element, String name)
        {
            if (element == null)
                return;
            foreach (var a in element.Attributes())
            {
                if (a.Name.LocalName == name)
                    a.Remove();
            }
        }

        static void RemoveAttributeIf(XElement element, String name, Func<XAttribute, bool> doRemove)
        {
            if (element == null)
                return;
            foreach (var a in element.Attributes())
            {
                if (a.Name.LocalName == name)
                    if (doRemove(a))
                        a.Remove();
            }
        }

        static void RemoveAttribute(SvgElement element, String name)
        {
            if (element == null)
                return;
            var a = GetAttributes(element);
            if (!a.ContainsKey(name))
                return;
            a.Remove(name);
        }


        static String GetAttributeValue(SvgElement element, String name, String defaultValue = null)
        {
            if (element == null)
                return defaultValue;
            return GetAttributes(element).TryGetValue(name, out var value) ? value?.ToString() : defaultValue;
        }

        static SvgElement FindFirst(SvgElement element, String name)
        {
            if (GetElementName(element) == name)
                return element;
            foreach (var x in element.Descendants())
            {
                var y = FindFirst(x, name);
                if (y != null)
                    return y;
            }
            return null;
        }


        static XElement FindFirst(XElement element, String name)
        {
            foreach (var e in element.DescendantNodesAndSelf())
            {
                var x = e as XElement;
                if (x == null)
                    continue;
                if (x.Name.LocalName == name)
                    return x;
            }
            return null;
        }

        static double ReadDoubleAttr(XElement element, String attr, double def = 0)
        {
            var a = element.Attribute(attr);
            if (a == null)
                return def;
            return double.TryParse(a.Value, CultureInfo.InvariantCulture, out var value) ? value : def;
        }

        static bool MatchSize(XElement element, double x, double y, double width, double height)
        {
            if (element == null)
                return false;
            if (ReadDoubleAttr(element, "width") != width)
                return false;
            if (ReadDoubleAttr(element, "height") != height)
                return false;
            if (ReadDoubleAttr(element, "x") != x)
                return false;
            if (ReadDoubleAttr(element, "y") != y)
                return false;
            return true;
        }


        static bool AreEqual(Bitmap a, Bitmap b)
        {
            var w = a.Width;
            var h = a.Height;
            if (w != b.Width)
                return false;
            if (h != b.Height)
                return false;
            var r = new Rectangle(0, 0, w, h);
            var al = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                try
                {
                    var ad = new ReadOnlySpan<Byte>(al.Scan0.ToPointer(), h * al.Stride);
                    var bl = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        var bd = new ReadOnlySpan<Byte>(bl.Scan0.ToPointer(), h * bl.Stride);
                        var rowLen = w * 4;
                        for (int i = 0; i < h; ++i)
                        {
                            if (!ad.Slice(i * al.Stride, rowLen).SequenceEqual(bd.Slice(i * bl.Stride, rowLen)))
                                return false;
                        }
                    }
                    finally
                    {
                        b.UnlockBits(bl);
                    }
                }
                finally
                {
                    a.UnlockBits(al);
                }
            }
            return true;
        }



        static bool GetRender(SvgDocument svg, out int w, out int h)
        {
            if (svg == null)
            {
                w = 0;
                h = 0;
                return false;
            }
            w = (int)Math.Ceiling(svg.ViewBox.Width * 10);
            h = (int)Math.Ceiling(svg.ViewBox.Height * 10);
            const int max = 2048;
            bool l = false;
            if (w > max)
            {
                h = (int)((((long)h) * (long)max + (w - 1L)) / w);
                w = max;
                l = true;
            }
            if (h > max)
            {
                w = (int)((((long)w) * (long)max + (h - 1L)) / h);
                h = max;
                l = true;
            }
            return l;
        }


        //static int CP = 0;

        static Bitmap GetValidationRef(XDocument doc)
        {
            try
            {
                SvgDocument svg;
                using (var ms = new MemoryStream())
                {
                    doc.Save(ms);
                    ms.Position = 0;
                    svg = SvgDocument.Open<SvgDocument>(ms);
                    if (svg == null)
                        return null;
                }
                GetRender(svg, out var w, out var h);
                var bm = new Bitmap(w, h);
                try
                {
                    using var g = Graphics.FromImage(bm);
                    g.Clear(Color.Red);
                    g.FillRectangle(Brushes.Green, 0, 0, w, h >> 1);
                    svg.Draw(g, new SizeF(w, h));
                    //bm.Save("DebugApa" + CP + ".png");
                    //++CP;
                    return bm;
                }
                catch
                {
                    bm.Dispose();
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        static bool IsSame(Bitmap bitmap, XDocument doc)
        {
            if (bitmap == null)
                return false;
            try
            {
                using (var d = GetValidationRef(doc))
                    return AreEqual(bitmap, d);
            }
            catch
            {
            }
            return false;
        }


        /// <summary>
        /// Compare the rendering of the document with the reference image (if any), throws an <see cref="InvalidOperationException"/> if it differs or if the document can't be rendered.
        /// Files are only written if <see cref="SvgMinifierParams.ErrorDir"/> is set (svg-cov-35).
        /// </summary>
        static void Validate(OptContext c)
        {
            var bitmap = c.Ref;
            if (bitmap == null)
                return;
            using var d = GetValidationRef(c.Doc);
            if ((d != null) && AreEqual(bitmap, d))
                return;
            var sb = new StringBuilder("Bitmap validation failed after the ");
            sb.Append(c.Step).Append(" step, the optimized svg ").Append(d == null ? "can't be rendered" : "renders differently");
            var dir = c.Opt.ErrorDir;
            if (String.IsNullOrWhiteSpace(dir))
            {
                sb.Append(" (set ErrorDir to save the images)");
            }
            else
            {
                try
                {
                    dir = Path.GetFullPath(dir);
                    Directory.CreateDirectory(dir);
                    //  Unique per failure: the hash of the source + a random part (parallel runs on the same source don't overwrite each other)
                    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(c.SourceText ?? "")).AsSpan(0, 8));
                    var baseName = Path.Combine(dir, String.Concat("SvgMinifier_", hash, "_", c.Step.ToString(), "_", Guid.NewGuid().ToString("N").Substring(0, 8)));
                    var refName = baseName + "_Ref.png";
                    var failedName = baseName + "_Failed.png";
                    var srcName = baseName + "_Source.svg";
                    bitmap.Save(refName, ImageFormat.Png);
                    d?.Save(failedName, ImageFormat.Png);
                    File.WriteAllText(srcName, c.SourceText ?? "");
                    sb.Append(" (reference image: ").Append(refName);
                    if (d != null)
                        sb.Append(", failed image: ").Append(failedName);
                    sb.Append(", source: ").Append(srcName).Append(')');
                }
                catch (Exception ex)
                {
                    sb.Append(" (saving the images to ").Append(dir).Append(" failed: ").Append(ex.Message).Append(')');
                }
            }
            throw new InvalidOperationException(sb.ToString());
        }

        static bool TryRemoveAttribute(ref Bitmap refImage, XElement element, String attribName)
        {
            var attr = element.Attribute(attribName);
            if (attr == null)
                return false;
            var xname = attr.Name;
            var prev = attr.Value;
            if (refImage == null)
            {
                refImage = GetValidationRef(element.Document);
                //refImage.Save(@"D:\Temp\SvgOut\a.png");
            }
            attr.Remove();
            if (IsSame(refImage, element.Document))
                return true;
            element.SetAttributeValue(xname, prev);
            /*            using (var img = doc.Draw(w, h))
                            img.Save(@"D:\Temp\SvgOut\c.png");*/
            return false;
        }


        static bool TryRemoveElement(ref Bitmap refImage, XElement element)
        {
            if (refImage == null)
            {
                refImage = GetValidationRef(element.Document);
                //refImage.Save(@"D:\Temp\SvgOut\a.png");
            }
            var an = XName.Get("visibility");
            var vis = element.Attribute(an);
            var old = vis?.Value;
            if (old == "hidden")
            {
                element.Remove();
                return true;
            }
            element.SetAttributeValue(an, "hidden");
            if (IsSame(refImage, element.Document))
            {
                element.Remove();
                return true;
            }
            if (vis != null)
            {
                element.SetAttributeValue(an, old);
            }
            else
            {
                element.Attribute(an).Remove();
            }
            return false;
        }

        static readonly HashSet<String> DoNotRemove = new HashSet<string>
        {
            "svg",
        };

        static readonly HashSet<String> ColorAttributes = new HashSet<string>
        {
            "fill",
            "stroke",
        };


        /// <summary>
        /// Shape elements and their numerical attributes (the decimals limit, leading zeros and rendering based removals are applied to these)
        /// </summary>
        static readonly Dictionary<String, String[]> DecimalLimits = new(StringComparer.Ordinal)
        {
            { "circle", ["cx", "cy", "r", "stroke-width"] },
            { "ellipse", ["cx", "cy", "rx", "ry", "stroke-width"] },
            { "line", ["x1", "y1", "x2", "y2", "stroke-width"] },
            { "path", ["d", "stroke-width"] },
            { "polygon", ["points", "stroke-width"] },
            { "polyline", ["points", "stroke-width"] },
            { "rect", ["x", "y", "width", "height", "rx", "ry", "stroke-width"] },
            { "svg", ["viewBox"] },
        };

        static readonly Dictionary<String, String[]> DecimalLimitsLong = new(StringComparer.Ordinal)
        {
            { "animate", ["values", "from", "to"] },
            { "animateTransform", ["values", "from", "to"] },
        };

        /// <summary>
        /// Attributes (and css properties of style attributes) with numerical values (numbers, lengths, number lists, path data, transforms), <see cref="SvgMinifierParams.TrimTrailingZeros"/>
        /// is only applied to these (svg-cov-29)
        /// </summary>
        static readonly HashSet<String> NumericAttributes = new(StringComparer.Ordinal)
        {
            //  Geometry
            "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry", "fx", "fy", "fr", "width", "height", "d", "points", "dx", "dy", "rotate",
            "viewBox", "transform", "gradientTransform", "patternTransform", "offset", "pathLength", "textLength", "startOffset",
            "refX", "refY", "markerWidth", "markerHeight", "enable-background",
            //  Presentation
            "stroke-width", "stroke-dasharray", "stroke-dashoffset", "stroke-miterlimit", "opacity", "fill-opacity", "stroke-opacity", "stop-opacity", "flood-opacity",
            "font-size", "letter-spacing", "word-spacing", "baseline-shift", "kerning",
            //  Filters
            "stdDeviation", "k1", "k2", "k3", "k4", "scale", "radius", "baseFrequency", "seed", "elevation", "azimuth", "specularExponent", "specularConstant",
            "diffuseConstant", "surfaceScale", "kernelMatrix", "kernelUnitLength", "divisor", "bias", "tableValues", "slope", "intercept", "amplitude", "exponent",
            "limitingConeAngle", "pointsAtX", "pointsAtY", "pointsAtZ", "z",
            //  Animation timing
            "keyTimes", "keySplines", "keyPoints", "dur", "repeatCount", "repeatDur",
        };

        /// <summary>
        /// Animation value attributes, numerical if the animated attribute is
        /// </summary>
        static readonly HashSet<String> AnimationValueAttributes = new(StringComparer.Ordinal)
        {
            "values", "from", "to", "by",
        };

        /// <summary>
        /// True if the attribute of the element has a numerical value (see <see cref="NumericAttributes"/>).
        /// The values / from / to / by attributes are numerical for animateTransform, animateMotion and feColorMatrix, and for animate / set if the attributeName is numerical.
        /// </summary>
        static bool IsNumericAttribute(XElement element, String name)
        {
            if (NumericAttributes.Contains(name))
                return true;
            if (!AnimationValueAttributes.Contains(name))
                return false;
            var en = element.Name.LocalName;
            if ((en == "animateTransform") || (en == "animateMotion") || (en == "feColorMatrix"))
                return true;
            if ((en != "animate") && (en != "set"))
                return false;
            var an = element.Attribute("attributeName")?.Value?.Trim();
            return (an != null) && NumericAttributes.Contains(an);
        }


        static readonly HashSet<Char> NumbersAndDecimalPoint = new HashSet<char>("0123456789.");

        /// <summary>
        /// Round all numbers in the value that have a decimal point or an exponent to at most maxDecimals decimals (banker's rounding).
        /// Numbers are tokenized like svg numbers: digits, an optional '.' with digits and an optional exponent ("e-5"),
        /// a '.' after a number's decimals starts a new number ("1.5.5" is 1.5 and .5); the sign is left as is, unless the number is rounded to zero ("-0.001" => "0").
        /// Numbers with a decimal point are written without leading / trailing zeros ("0.50" => ".5", "1.0" => "1"),
        /// numbers with an exponent are only changed if rounding changes their value, integers are never changed.
        /// A space is inserted between two numbers if the second would otherwise merge with the first ("1.5.5" with 0 decimals => "2 0").
        /// If isPath is true, the value is path data: the large-arc-flag and sweep-flag of arcs are single chars ("a5 5 0 0110 10") and are kept as is.
        /// </summary>
        static String SetMaxDecimals(String value, int maxDecimals, bool isPath = false)
        {
            var vl = value.Length;
            StringBuilder b = null;
            int last = 0;
            //  The end of the previous number and if a '.' directly after its output starts a new number
            int prevEnd = -1;
            bool prevDotSafe = false;
            //  Path data: the current command and the index of the next number of the command
            Char cmd = (Char)0;
            int argIndex = 0;
            int i = 0;
            while (i < vl)
            {
                var c = value[i];
                if (!Char.IsAsciiDigit(c))
                {
                    if ((c != '.') || ((i + 1) >= vl) || (!Char.IsAsciiDigit(value[i + 1])))
                    {
                        if (isPath && Char.IsAsciiLetter(c))
                        {
                            cmd = c;
                            argIndex = 0;
                        }
                        ++i;
                        continue;
                    }
                }
                else
                {
                    if (((cmd | 0x20) == 'a') && (c <= '1'))
                    {
                        var ai = argIndex % 7;
                        if ((ai == 3) || (ai == 4))
                        {
                            //  An arc flag, a single char
                            ++argIndex;
                            ++i;
                            prevEnd = -1;
                            continue;
                        }
                    }
                }
                ++argIndex;
                //  Tokenize a number: digits, optional '.' + digits, optional exponent
                var start = i;
                while ((i < vl) && Char.IsAsciiDigit(value[i]))
                    ++i;
                int dot = -1;
                if ((i < vl) && (value[i] == '.'))
                {
                    dot = i;
                    ++i;
                    while ((i < vl) && Char.IsAsciiDigit(value[i]))
                        ++i;
                }
                bool haveExp = false;
                if ((i < vl) && ((value[i] | 0x20) == 'e'))
                {
                    var e = i + 1;
                    if ((e < vl) && ((value[e] == '-') || (value[e] == '+')))
                        ++e;
                    if ((e < vl) && Char.IsAsciiDigit(value[e]))
                    {
                        haveExp = true;
                        i = e + 1;
                        while ((i < vl) && Char.IsAsciiDigit(value[i]))
                            ++i;
                    }
                }
                var end = i;
                //  Compute the new text (null = keep the original)
                String x = null;
                if (haveExp || ((dot >= 0) && ((end - dot - 1) > maxDecimals)))
                {
                    //  Must round
                    if (Decimal.TryParse(value.AsSpan(start, end - start), NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var val))
                    {
                        var r = Math.Round(val, maxDecimals);
                        if ((!haveExp) || (r != val))
                        {
                            x = r.ToString(CultureInfo.InvariantCulture);
                            if (x.IndexOf('.') >= 0)
                            {
                                x = x.TrimEnd('0').TrimEnd('.');
                                if (x.Length == 0)
                                    x = "0";
                            }
                            if (x.StartsWith("0.", StringComparison.Ordinal))
                                x = x.Substring(1);
                        }
                    }
                }
                else if (dot >= 0)
                {
                    //  Only normalize: remove leading zeros of the integer part and trailing zeros of the decimals
                    var intStart = start;
                    while ((intStart < dot) && (value[intStart] == '0'))
                        ++intStart;
                    var fracEnd = end;
                    while ((fracEnd > (dot + 1)) && (value[fracEnd - 1] == '0'))
                        --fracEnd;
                    var haveFrac = fracEnd > (dot + 1);
                    if ((intStart != start) || (fracEnd != end) || (!haveFrac))
                    {
                        if (haveFrac)
                            x = value.Substring(intStart, fracEnd - intStart);
                        else
                            x = intStart < dot ? value.Substring(intStart, dot - intStart) : "0";
                    }
                }
                bool startsWithDot;
                bool dotSafe;
                if (x == null)
                {
                    startsWithDot = value[start] == '.';
                    dotSafe = (dot >= 0) || haveExp;
                }
                else
                {
                    startsWithDot = x[0] == '.';
                    dotSafe = x.IndexOf('.') >= 0;
                }
                //  A number that started with '.' directly after the previous one needs a separator if it could merge with it
                var needSep = (start == prevEnd) && !(prevDotSafe && startsWithDot);
                if ((x != null) || needSep)
                {
                    b ??= new StringBuilder(vl + 8);
                    if ((x == "0") && (start > last) && (value[start - 1] == '-'))
                    {
                        //  "-0.001" => "0" instead of "-0" (the '-' is replaced with a space if it separated two numbers)
                        b.Append(value, last, start - 1 - last);
                        var pc = start >= 2 ? value[start - 2] : ' ';
                        if (Char.IsAsciiDigit(pc) || (pc == '.'))
                            b.Append(' ');
                        last = start;
                    }
                    b.Append(value, last, start - last);
                    if (needSep)
                        b.Append(' ');
                    if (x != null)
                        b.Append(x);
                    else
                        b.Append(value, start, end - start);
                    last = end;
                }
                prevEnd = end;
                prevDotSafe = dotSafe;
            }
            if (b == null)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();
        }

        static int SkipSpaces(String value, int start)
        {
            var vl = value.Length;
            for (; ; )
            {
                if (start >= vl)
                    return vl;
                if (value[start] != ' ')
                    return start;
                ++start;
            }
        }

        static int SkipNumber(String value, int start)
        {
            var vl = value.Length;
            if (start >= vl)
                return vl;
            if ((value[start] == '-') || (value[start] == '+'))
                ++start;
            bool haveDecimals = false;
            for (; ; )
            {
                if (start >= vl)
                    return vl;
                var c = value[start];
                if (Char.IsDigit(c))
                {
                    ++start;
                    continue;
                }
                if (c == '.')
                {
                    if (haveDecimals)
                        return start;
                    haveDecimals = true;
                    ++start;
                    continue;
                }
                //  An exponent ("1e-5")
                if ((c | 0x20) == 'e')
                {
                    var e = start + 1;
                    if ((e < vl) && ((value[e] == '-') || (value[e] == '+')))
                        ++e;
                    if ((e < vl) && Char.IsAsciiDigit(value[e]))
                    {
                        start = e + 1;
                        while ((start < vl) && Char.IsAsciiDigit(value[start]))
                            ++start;
                    }
                }
                return start;
            }
        }

        static String OptimizePathArcs(String value)
        {
            var vl = value.Length;
            var b = new StringBuilder(vl);
            int i = 0;
            int last = 0;
            for (; ; )
            {
                if (i >= vl)
                    break;
                i = value.IndexOf("a", i, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                    break;
                ++i;
                for (; ; )
                {
                    //  Skip rx
                    i = SkipNumber(value, i);
                    i = SkipSpaces(value, i);
                    //  Skip ry
                    i = SkipNumber(value, i);
                    i = SkipSpaces(value, i);
                    //  Skip x-axis-rotation
                    i = SkipNumber(value, i);
                    i = SkipSpaces(value, i);
                    ++i;
                    //  Incomplete arc (invalid path data)
                    if (i >= vl)
                        break;
                    //  If large-arc-flag end with a space, remove it
                    if (value[i] == ' ')
                    {
                        b.Append(value, last, i - last);
                        ++i;
                        last = i;
                    }
                    //  If sweep-flag end with a space, remove it
                    ++i;
                    if (i >= vl)
                        break;
                    if (value[i] == ' ')
                    {
                        b.Append(value, last, i - last);
                        ++i;
                        last = i;
                    }
                    //  Skip x/dx
                    i = SkipNumber(value, i);
                    i = SkipSpaces(value, i);
                    //  Skip y/dy
                    i = SkipNumber(value, i);
                    i = SkipSpaces(value, i);
                    if (i >= vl)
                        break;
                    Char c = value[i];
                    if (Char.IsLetter(c))
                        break;
                    if (c == ' ')
                        ++i;
                }
                if (i < 0)
                    break;
            }
            if (last == 0)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();
        }

        static int IndexOf(String value, int startIndex, Func<Char, int, bool> isFound)
        {
            var vl = value.Length;
            for (int i = startIndex; i < vl; ++i)
            {
                if (isFound(value[i], i))
                    return i;
            }
            return -1;
        }

        static readonly HashSet<Char> AllowedJoins = new HashSet<char>("aAlL");

        static String JoinPathCommands(String value)
        {
            var vl = value.Length;
            Char[] str = null;
            int i = 0;
            for (; ; )
            {
                i = IndexOf(value, i, (c, p) => Char.IsLetter(c));
                if (i < 0)
                    break;
                var start = i;
                Char cmd = value[i];
                ++i;
                if (!AllowedJoins.Contains(cmd))
                    continue;
                for (; ; )
                {
                    i = IndexOf(value, i, (c, p) => Char.IsLetter(c));
                    if (i < 0)
                        break;
                    var nextCmd = value[i];
                    if (nextCmd != cmd)
                        break;
                    var join = value.Substring(start);
                    if (str == null)
                        str = value.ToCharArray();
                    str[i] = ' ';
                    ++i;
                }
                if (i < 0)
                    break;
            }
            if (str == null)
                return value;
            return new string(str);
        }

        static String TrimDoubleZeros(String value, bool isArc)
        {
            var vl = value.Length;
            var b = new StringBuilder(vl);
            int i = 0;
            int last = 0;
            var nums = NumbersAndDecimalPoint;
            int ok = -1;
            for (; ; )
            {
                i = value.IndexOf('0', i);
                if (i < 0)
                    break;
                ++i;
                if (i >= vl)
                    continue;
                if (!nums.Contains(value[i]))
                    continue;
                if ((i > 1) && (i != ok) && nums.Contains(value[i - 2]))
                    continue;
                if (isArc)
                {
                    char cmd = '0';
                    var o = i - 1;
                    while (o > 0)
                    {
                        --o;
                        cmd = value[o];
                        //  'e' / 'E' is an exponent, not a command
                        if (char.IsLetter(cmd) && ((cmd | 0x20) != 'e'))
                            break;
                    }
                    if ((cmd == 'a') || (cmd == 'A'))
                        continue;
                }
                ok = i + 1;
                b.Append(value, last, i - 1 - last);
                last = i;
            }
            if (last == 0)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();
        }


        static String TrimLeadingZeros(String value, bool isArc)
        {
            value = TrimDoubleZeros(value, isArc);
            var vl = value.Length;
            var b = new StringBuilder(vl);
            int i = 0;
            int last = 0;
            for (; ; )
            {
                i = value.IndexOf('.', i);
                if (i < 0)
                    break;
                ++i;
                if (i < 2)
                    continue;
                if (value[i - 2] != '0')
                    continue;
                bool canRemove = i < 3 || (!Char.IsDigit(value[i - 3]));
                if (!canRemove)
                    continue;
                int r = 2;
                if (i > 4)
                {
                    if (value[i - 3] == ' ')
                        if (!Char.IsDigit(value[i - 4]))
                            ++r;
                }
                b.Append(value, last, i - r - last);
                last = i - 1;
            }
            if (last == 0)
                return NegZeroToZero(value);
            b.Append(value, last, vl - last);
            return NegZeroToZero(b.ToString());
        }

        static String NegZeroToZero(String value)
        {
            var vl = value.Length;
            var b = new StringBuilder(vl);
            int i = 0;
            int last = 0;
            for (; ; )
            {
                i = value.IndexOf('-', i);
                if (i < 0)
                    break;
                ++i;
                if (i >= vl)
                    break;
                if (value[i] != '0')
                    continue;
                ++i;
                if ((i < vl) && (value[i] == '.'))
                    continue;
                if ((i > 2) && NumbersAndDecimalPoint.Contains(value[i - 3]))
                    continue;
                b.Append(value, last, i - 2 - last);
                --i;
                last = i;
            }
            if (last == 0)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();

        }


        /// <summary>
        /// Apply <see cref="TrimTrailingZeros(string)"/> to the values of the numerical css properties (<see cref="NumericAttributes"/>) of a style attribute value (svg-cov-29)
        /// </summary>
        /// <returns>The new value, or the value itself if nothing changed</returns>
        static String TrimTrailingZerosStyle(String value)
        {
            if (value.IndexOf('.') < 0)
                return value;
            StringBuilder b = null;
            //  The value is copied to b up until last
            int last = 0;
            var vl = value.Length;
            int s = 0;
            while (s < vl)
            {
                var e = value.IndexOf(';', s);
                if (e < 0)
                    e = vl;
                var colon = value.IndexOf(':', s, e - s);
                if (colon > 0)
                {
                    var prop = value.AsSpan(s, colon - s).Trim();
                    if (NumericAttributes.GetAlternateLookup<ReadOnlySpan<Char>>().Contains(prop))
                    {
                        var org = value.Substring(colon + 1, e - colon - 1);
                        var val = TrimTrailingZeros(org);
                        if (!ReferenceEquals(org, val))
                        {
                            b ??= new StringBuilder(vl);
                            b.Append(value, last, colon + 1 - last);
                            b.Append(val);
                            last = e;
                        }
                    }
                }
                s = e + 1;
            }
            if (b == null)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();
        }

        /// <summary>
        /// Remove trailing zeros of the decimals of all numbers in the value ("10.500" =&gt; "10.5", "13.0" =&gt; "13", ".0" =&gt; "0", "-.00" =&gt; "0")
        /// </summary>
        /// <returns>The new value, or the value itself if nothing changed</returns>
        static String TrimTrailingZeros(String value)
        {
            var vl = value.Length;
            StringBuilder b = null;
            int i = 0;
            int last = 0;
            for (; ; )
            {
                i = value.IndexOf('.', i);
                if (i < 0)
                    break;
                ++i;
                var e = IndexOf(value, i, (c, p) => !Char.IsDigit(c));
                if (e < 0)
                    e = vl;
                var end = e;
                while (e > i)
                {
                    --e;
                    if (value[e] != '0')
                    {
                        ++e;
                        break;
                    }
                }
                if (e == end)
                    continue;
                b ??= new StringBuilder(vl);
                if (e == i)
                {
                    --e;
                    //  All decimals
                    //  1.00 => 1      .000 => 0    -.00 => 0
                    bool prevIsDigit = i > 1 && Char.IsDigit(value[i - 2]);
                    if (prevIsDigit)
                    {
                        // 1.00 => 1
                        b.Append(value, last, e - last);
                    }
                    else
                    {
                        // .00 => 0   or  -.00 => 0
                        if ((i > 1) && (value[i - 2] == '-'))
                            --e;
                        b.Append(value, last, e - last);
                        b.Append('0');
                    }
                }
                else
                {
                    //  Some zeros
                    // 4.10 => 4.1
                    b.Append(value, last, e - last);
                }
                last = end;
                i = end;
            }
            if (b == null)
                return value;
            b.Append(value, last, vl - last);
            return b.ToString();
        }

        static String OptimizePath(String value)
        {
            value = value.Replace(',', ' ');
            value = value.Replace('\t', ' ');
            value = value.Replace('\n', ' ');
            value = value.Replace('\r', ' ');
            value = JoinPathCommands(value);
            for (; ; )
            {
                var t = value;
                value = value.Replace("  ", " ");
                if (value == t)
                    break;
            }
            //  Trim after the replacements (a trailing separator or a joined command becomes a space, svg-cov-31), all spaces below have chars on both sides
            value = value.Trim();
            var vl = value.Length;
            var b = new StringBuilder(vl);
            int i = 0;
            int last = 0;
            for (; ; )
            {
                i = value.IndexOf(' ', i);
                if (i < 0)
                    break;
                var prev = value[i - 1];
                //  A number ending with '.' ("1. 5") must also be kept separated (svg-cov-32)
                var pd = Char.IsDigit(prev) || (prev == '.');
                if (pd == NumbersAndDecimalPoint.Contains(value[i + 1]))
                {
                    if (pd)
                    {
                        ++i;
                        continue;
                    }
                }
                b.Append(value, last, i - last);
                ++i;
                last = i;
            }
            if (last == 0)
                return OptimizePathArcs(value);
            b.Append(value, last, vl - last);
            return OptimizePathArcs(b.ToString());
        }

        static String ReplaceAll(String s, String v = " ", String to = "")
        {
            for (; ; )
            {
                var t = s;
                s = s.Replace(v, to);
                if (s == t)
                    return s;
            }
        }

        static String FixValuesUpUntilP(String value, int decimalLimit)
        {
            var l = value.IndexOf(')');
            if (l < 0)
                return value;
            return SetMaxDecimals(value.Substring(0, l), decimalLimit) + value.Substring(l);
        }

        static String FixTransform(String value, int decimalLimit)
        {
            value = value.Trim();
            value = ReplaceAll(value, "  ", " ");
            value = value.Replace(',', ' ');
            value = ReplaceAll(value, "  ", " ");
            var parts = value.Split('(', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var l = parts.Length - 1;
            for (int i = 0; i < l; ++i)
            {
                var x = parts[i];
                if (x.EndsWith("rotate", StringComparison.OrdinalIgnoreCase))
                {
                    var y = parts[i + 1];
                    y = FixValuesUpUntilP(y, 4);
                    y = y.Replace(" 0 0)", ")");
                    parts[i + 1] = y;
                    continue;
                }
                if (x.EndsWith("scale", StringComparison.OrdinalIgnoreCase))
                {
                    var y = parts[i + 1];
                    y = FixValuesUpUntilP(y, 10);
                    var pp = y.IndexOf(')');
                    if (pp > 0)
                    {
                        var p = y.Substring(0, pp).Split(' ');
                        if (p.Length == 2)
                        {
                            if (p[0] == p[1])
                                y = p[0] + y.Substring(pp);
                        }
                    }
                    parts[i + 1] = y;
                    continue;
                }

                if (x.EndsWith("translate", StringComparison.OrdinalIgnoreCase))
                {
                    var y = parts[i + 1];
                    y = FixValuesUpUntilP(y, decimalLimit >= 0 ? decimalLimit : 10);
                    y = ReplaceAll(y, " 0)", ")");
                    parts[i + 1] = y;
                    continue;
                }
            }
            value = String.Join('(', parts);
            value = ReplaceAll(value, "  ", " ");
            return value;

        }

        static String TrimNonDigits(string value, bool acceptDecimalComma = true)
        {
            value = value.Trim();
            var l = value.Length;
            bool haveComma = false;
            int i;
            for (i = 0; i < l; ++i)
            {
                var c = value[i];
                if ((c >= '0') && (c <= '9'))
                    continue;
                if (i == 0)
                {
                    if (c == '-')
                        continue;
                    if (c == '+')
                        continue;
                }
                if (acceptDecimalComma)
                {
                    if (c == '.')
                    {
                        if (!haveComma)
                        {
                            haveComma = true;
                            continue;
                        }
                    }
                }
                break;
            }
            return value.Substring(0, i);
        }

        const String First = "abcdefghijklmnopqrstuvwxyz";
        const String Other = First + "_0123456789";

        /// <summary>
        /// Generate a unique short name: index 0 - 25 => "a" - "z", the following indices use all two char names ("aa", "ba" .. "z9"), then three chars and so on.
        /// The first char is a lower case letter, the others are lower case letters, '_' or digits (bijective numeration, every index gives a different name).
        /// </summary>
        static String GenShort(int index)
        {
            var fl = First.Length;
            if (index < fl)
                return First[index].ToString();
            var sb = new StringBuilder(4);
            sb.Append(First[index % fl]);
            var ol = Other.Length;
            index /= fl;
            while (index > 0)
            {
                --index;
                sb.Append(Other[index % ol]);
                index /= ol;
            }
            return sb.ToString();
        }


        static readonly Char[] SplitChars = " ,".ToCharArray();


    }


}
