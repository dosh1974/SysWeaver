using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SysWeaver.Media;

/// <summary>
/// Generated (deterministic) app icons, app logos and user icons in the style of the SysWeaver brand guide:
/// a "thread" mark on a soft square tile, slate / steel / copper colors (derived from a <see cref="HashColors"/> theme, see <see cref="BrandPalette"/>),
/// Saira Semi Condensed SemiBold for initials and wordmarks and Barlow for sub titles, flat colors (no effects).
/// The initials are combined with one of several thread motifs (woven over / under the letters and each other), see <see cref="MarkMotif"/>.
/// The same name, colors and seed always gives the same result, and the logo of an app uses the same mark as the app icon.
/// </summary>
public static class SvgSceneBrandExt
{
    /// <summary>
    /// Add a generated app icon (made for a 256 x 256 scene, other sizes are scaled to fit and centered)
    /// </summary>
    /// <param name="scene">The scene to add the icon to</param>
    /// <param name="name">The name of the app</param>
    /// <param name="color">The colors to use (should be the app colors, <see cref="HashColors.AppColors"/>), null to derive them from the name and seed</param>
    /// <param name="seed">A seed, used to derive the colors (if <paramref name="color"/> is null) and to vary the design</param>
    /// <param name="opaque">If true the icon is full bleed (a square covering the scene), else a soft square with transparent corners</param>
    public static void AddAppIcon(this SvgScene scene, String name, HashColors color = null, int seed = 0, bool opaque = false)
        => AddIcon(scene, MarkDesign.Create(name, color, seed, false), opaque);

    /// <summary>
    /// Add a generated user icon (made for a 256 x 256 scene, other sizes are scaled to fit and centered).
    /// The initials are the first letters of the first and last name (an email address uses the part before the '@').
    /// </summary>
    /// <param name="scene">The scene to add the icon to</param>
    /// <param name="name">The user name</param>
    /// <param name="color">The colors to use, null to derive them from the name and seed (recommended, makes users distinguishable)</param>
    /// <param name="seed">A seed, used to derive the colors (if <paramref name="color"/> is null) and to vary the design</param>
    /// <param name="opaque">If true the icon is full bleed (a square covering the scene), else a circle or soft square with a transparent background</param>
    public static void AddUserIcon(this SvgScene scene, String name, HashColors color = null, int seed = 0, bool opaque = false)
        => AddIcon(scene, MarkDesign.Create(name, color, seed, true), opaque);

    /// <summary>
    /// Add a generated app logo (made for a 512 x 384 scene, other sizes are scaled to fit and centered): the app mark (same as <see cref="AddAppIcon"/>)
    /// with the name as a wordmark and an optional sub title, stacked or side by side, on a solid panel (optionally framed by a plain weave).
    /// </summary>
    /// <param name="scene">The scene to add the logo to</param>
    /// <param name="name">The name of the app</param>
    /// <param name="subTitle">An optional sub title (ignored if null, empty or the same as the name)</param>
    /// <param name="color">The colors to use (should be the app colors, <see cref="HashColors.AppColors"/>), null to derive them from the name and seed</param>
    /// <param name="seed">A seed, used to derive the colors (if <paramref name="color"/> is null) and to vary the design</param>
    /// <param name="opaque">If true the panel covers the scene, else the panel is a soft rectangle with transparent corners</param>
    public static void AddAppLogo(this SvgScene scene, String name, String subTitle, HashColors color = null, int seed = 0, bool opaque = false)
    {
        const double LW = 512;
        const double LH = 384;
        var d = MarkDesign.Create(name, color, seed, false);
        name = d.Name.Trim();
        var pal = d.LogoPalette;
        var bg = pal.Background;
        var w = scene.Width;
        var h = scene.Height;
        var s = Math.Min(w / LW, h / LH);
        var g = new StringBuilder();
        if (opaque)
            g.Append($"<rect width='{F(w)}' height='{F(h)}' fill='{bg}'/>");
        g.Append($"<g transform='translate({F((w - LW * s) * 0.5)} {F((h - LH * s) * 0.5)}) scale({S(s)})'>");
        var panel = opaque ? "<rect width='512' height='384'/>" : "<rect width='512' height='384' rx='40'/>";
        if (!opaque)
            g.Append(WithFill(panel, bg));
        const double m = 40;
        //  Minimum cap heights (in the 512 x 384 layout): a name that would get smaller is wrapped to two lines, a smaller sub title is dropped
        const double MinNameCap = 26;
        const double MinSubCap = 9;
        //  Text
        var sub = subTitle?.Trim();
        if (String.IsNullOrEmpty(sub) || String.Equals(sub, name, StringComparison.OrdinalIgnoreCase))
            sub = null;
        var cw = LW - 2 * m;
        var ch = LH - 2 * m;
        //  A mark without a tile has more padding (the tile is the clear space), so draw it larger
        var markScale = d.LogoShowTile ? 1.0 : 1.35;
        var markPal = d.LogoShowTile ? d.Palette : pal;
        var one = new BrandText(name, BrandFonts.Saira);
        var hasName = !one.IsEmpty;
        var horizontal = d.LogoHorizontal && hasName;
        if (horizontal)
        {
            var icon = ch * (sub != null ? 0.5 : 0.46);
            //  Very short names (ex: "X"): a larger tile, so the lockup is not a tiny tile beside a lone letter
            if (name.Count(Char.IsLetterOrDigit) <= 2)
                icon *= 1.35;
            var gap = icon * (d.LogoShowTile ? 0.17 : 0.26);
            var textW = cw - icon - gap;
            var capN = Math.Min(icon * 0.5, textW / one.WidthPerCap);
            if (capN < MinNameCap)
            {
                horizontal = false;
            }
            else
            {
                var nameW = capN * one.WidthPerCap;
                var subLines = SubTitleLines(sub, capN * 0.42, MinSubCap, textW, out var capS, out var subW);
                var gapS = subLines.Count > 0 ? Math.Max(capN * 0.36, one.DescentPerCap * capN + capS * 0.5) : 0;
                var blockH = capN + (subLines.Count > 0 ? gapS + capS + (subLines.Count - 1) * capS * 1.7 : 0);
                var totalW = icon + gap + Math.Max(nameW, subW);
                var x0 = m + (cw - totalW) * 0.5;
                var mark = icon * markScale;
                DrawMark(scene, g, d, markPal, x0 + (icon - mark) * 0.5, m + (ch - mark) * 0.5, mark, d.LogoShowTile, false, 0);
                var tx = x0 + icon + gap;
                var ty = m + (ch - blockH) * 0.5;
                Wordmark(g, one, 0, capN, tx, ty, pal, d.LogoHighlightStart, d.LogoHighlightLength);
                var sy = ty + capN + gapS;
                foreach (var line in subLines)
                {
                    Fill(g, line.Place(capS, tx, sy), pal.Secondary);
                    sy += capS * 1.7;
                }
            }
        }
        if (!horizontal)
        {
            var icon = ch * (sub != null ? 0.46 : 0.54);
            if (!hasName)
                icon = ch * 0.8;
            var gap1 = ch * (d.LogoShowTile ? 0.08 : 0.06);
            double capN = 0;
            var nameLines = hasName ? NameLines(name, one, ch * 0.15, MinNameCap, cw, out capN) : new List<(BrandText Text, int Offset)>();
            if (nameLines.Count > 1)
                icon *= 0.8;
            var lineStep = nameLines.Count > 0 ? capN * (1.3 + nameLines.Max(x => x.Text.DescentPerCap)) : 0;
            var nameH = nameLines.Count > 0 ? capN + (nameLines.Count - 1) * lineStep : 0;
            double capS = 0;
            var subLines = hasName ? SubTitleLines(sub, ch * 0.064, MinSubCap, cw, out capS, out _) : new List<BrandText>();
            var gapS = subLines.Count > 0 ? Math.Max(ch * 0.06, nameLines[^1].Text.DescentPerCap * capN + ch * 0.03) : 0;
            var total = icon + (hasName ? gap1 + nameH : 0) + (subLines.Count > 0 ? gapS + capS + (subLines.Count - 1) * capS * 1.7 : 0);
            var y0 = m + (ch - total) * 0.5;
            var mark = icon * markScale;
            DrawMark(scene, g, d, markPal, (LW - mark) * 0.5, y0 + (icon - mark) * 0.5, mark, d.LogoShowTile, false, 0);
            if (hasName)
            {
                var ty = y0 + icon + gap1;
                foreach (var (text, offset) in nameLines)
                {
                    Wordmark(g, text, offset, capN, (LW - capN * text.WidthPerCap) * 0.5, ty, pal, d.LogoHighlightStart, d.LogoHighlightLength);
                    ty += lineStep;
                }
                var sy = y0 + icon + gap1 + nameH + gapS;
                foreach (var line in subLines)
                {
                    Fill(g, line.Place(capS, (LW - capS * line.WidthPerCap) * 0.5, sy), pal.Secondary);
                    sy += capS * 1.7;
                }
            }
        }
        g.Append("</g>");
        scene.AddGeometry(g.ToString());
    }

    /// <summary>
    /// Add a generated app banner (made for a 1260 x 280 scene, other sizes are scaled to fit and centered): the app icon (same as <see cref="AddAppIcon"/>) in the
    /// left 280 x 280 square, with the tile background stretched to the full width, and the name as a wordmark with an optional sub title to the right.
    /// </summary>
    /// <param name="scene">The scene to add the banner to</param>
    /// <param name="name">The name of the app</param>
    /// <param name="subTitle">An optional sub title (ignored if null, empty or the same as the name)</param>
    /// <param name="color">The colors to use (should be the app colors, <see cref="HashColors.AppColors"/>), null to derive them from the name and seed</param>
    /// <param name="seed">A seed, used to derive the colors (if <paramref name="color"/> is null) and to vary the design</param>
    /// <param name="opaque">If true the banner covers the scene, else the banner is a soft rectangle with transparent corners (the same corners as the icon)</param>
    public static void AddAppBanner(this SvgScene scene, String name, String subTitle, HashColors color = null, int seed = 0, bool opaque = false)
    {
        const double BW = 1260;
        const double BH = 280;
        var d = MarkDesign.Create(name, color, seed, false);
        name = d.Name.Trim();
        var pal = d.Palette;
        var w = scene.Width;
        var h = scene.Height;
        var s = Math.Min(w / BW, h / BH);
        var g = new StringBuilder();
        if (opaque)
            g.Append($"<rect width='{F(w)}' height='{F(h)}' fill='{pal.Background}'/>");
        g.Append($"<g transform='translate({F((w - BW * s) * 0.5)} {F((h - BH * s) * 0.5)}) scale({S(s)})'>");
        //  The icon, with the tile stretched to the full width (small scenes get the same small variant as the icon would at that size)
        var px = BH * s;
        DrawMark(scene, g, d, pal, 0, 0, BH, true, opaque, px <= SmallSize ? px : 0, U * BW / BH);
        //  Text, left aligned next to the icon (the mark has its own padding inside the tile) and centered vertically on the icon
        const double m = 30;
        const double gap = 6;
        //  Minimum cap heights (in the 1260 x 280 layout): a name that would get smaller is wrapped to two lines, a smaller sub title is dropped
        const double MinNameCap = 46;
        const double MinSubCap = 10;
        var sub = subTitle?.Trim();
        if (String.IsNullOrEmpty(sub) || String.Equals(sub, name, StringComparison.OrdinalIgnoreCase))
            sub = null;
        var one = new BrandText(name, BrandFonts.Saira);
        if (!one.IsEmpty)
        {
            var tx = BH + gap;
            var textW = BW - tx - m;
            var nameLines = NameLines(name, one, sub != null ? 92 : 110, MinNameCap, textW, out var capN);
            var lineStep = capN * (1.3 + nameLines.Max(x => x.Text.DescentPerCap));
            var subLines = SubTitleLines(sub, capN * 0.5, MinSubCap, textW, out var capS, out _);
            var gapS = subLines.Count > 0 ? Math.Max(capN * 0.36, nameLines[^1].Text.DescentPerCap * capN + capS * 0.5) : 0;
            var subStep = capS * 1.7;
            var nameH = capN + (nameLines.Count - 1) * lineStep;
            var blockH = nameH + (subLines.Count > 0 ? gapS + capS + (subLines.Count - 1) * subStep : 0);
            //  Too tall (two name lines and two sub title lines): scale the whole text block down
            var k = Math.Min(1, (BH - 2 * m) / blockH);
            capN *= k;
            lineStep *= k;
            capS *= k;
            gapS *= k;
            subStep *= k;
            nameH *= k;
            blockH *= k;
            var ty = (BH - blockH) * 0.5;
            var sy = ty + nameH + gapS;
            foreach (var (text, offset) in nameLines)
            {
                Wordmark(g, text, offset, capN, tx, ty, pal, d.LogoHighlightStart, d.LogoHighlightLength);
                ty += lineStep;
            }
            foreach (var line in subLines)
            {
                Fill(g, line.Place(capS, tx, sy), pal.Secondary);
                sy += subStep;
            }
        }
        g.Append("</g>");
        scene.AddGeometry(g.ToString());
    }

    /// <summary>
    /// Layout a name: one line if it fits at a cap height of at least minCap, else split into two lines at the word (or camel case) boundary closest to the middle
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="one">The name shaped as a single line</param>
    /// <param name="maxCap">The max cap height</param>
    /// <param name="minCap">The min cap height</param>
    /// <param name="width">The available width</param>
    /// <param name="cap">The cap height to use</param>
    /// <returns>The lines and the char offset of each line in the name</returns>
    static List<(BrandText Text, int Offset)> NameLines(String name, BrandText one, double maxCap, double minCap, double width, out double cap)
    {
        cap = Math.Min(maxCap, width / one.WidthPerCap);
        var words = MarkDesign.SplitWords(name);
        if ((cap >= minCap) || (words.Count < 2))
            return new List<(BrandText Text, int Offset)> { (one, 0) };
        var mid = name.Length / 2;
        var split = words.Skip(1).Select(x => x.Start).MinBy(x => Math.Abs(x - mid));
        var a = new BrandText(name.Substring(0, split).TrimEnd(), BrandFonts.Saira);
        var b = new BrandText(name.Substring(split), BrandFonts.Saira);
        cap = Math.Min(maxCap, width / Math.Max(a.WidthPerCap, b.WidthPerCap));
        return new List<(BrandText Text, int Offset)> { (a, 0), (b, split) };
    }

    /// <summary>
    /// Layout a sub title: one line if it fits at (close to) the max cap height, else split into two lines (at the space closest to the middle).
    /// Nothing is returned if the sub title would get smaller than minCap.
    /// </summary>
    /// <param name="sub">The sub title, null for none</param>
    /// <param name="maxCap">The max cap height</param>
    /// <param name="minCap">The min cap height</param>
    /// <param name="width">The available width</param>
    /// <param name="cap">The cap height to use</param>
    /// <param name="maxWidth">The width of the widest line</param>
    /// <returns>The lines (0, 1 or 2)</returns>
    static List<BrandText> SubTitleLines(String sub, double maxCap, double minCap, double width, out double cap, out double maxWidth)
    {
        cap = 0;
        maxWidth = 0;
        var res = new List<BrandText>();
        if (sub == null)
            return res;
        var one = new BrandText(sub, BrandFonts.BarlowMedium);
        if (one.IsEmpty)
            return res;
        var oneCap = Math.Min(maxCap, width / one.WidthPerCap);
        var mid = sub.Length / 2;
        var split = -1;
        for (int i = 0; i < sub.Length; ++i)
            if ((sub[i] == ' ') && ((split < 0) || (Math.Abs(i - mid) < Math.Abs(split - mid))))
                split = i;
        //  Only wrap if the width actually limits the size
        if ((oneCap >= maxCap * 0.85) || (split < 0))
        {
            if (oneCap < minCap)
                return res;
            cap = oneCap;
            res.Add(one);
            maxWidth = cap * one.WidthPerCap;
            return res;
        }
        var a = new BrandText(sub.Substring(0, split), BrandFonts.BarlowMedium);
        var b = new BrandText(sub.Substring(split + 1), BrandFonts.BarlowMedium);
        var wpc = Math.Max(a.WidthPerCap, b.WidthPerCap);
        var twoCap = Math.Min(maxCap, width / wpc);
        if (twoCap < minCap)
            return res;
        cap = twoCap;
        maxWidth = cap * wpc;
        res.Add(a);
        res.Add(b);
        return res;
    }


    #region Implementation

    /// <summary>
    /// The size of the mark coordinate system
    /// </summary>
    const double U = 256;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static String F(double v) => v.ToString("0.##", Inv);

    static String S(double v) => v.ToString("0.#####", Inv);

    static String WithFill(String element, String fill) => element.Replace("/>", $" fill='{fill}'/>");

    static void Fill(StringBuilder g, IEnumerable<String> paths, String color)
    {
        var d = SvgPath.JoinPaths(paths);
        if (!String.IsNullOrEmpty(d))
            g.Append($"<path d='{d}' fill='{color}'/>");
    }

    static void Wordmark(StringBuilder g, BrandText text, int offset, double cap, double x, double capTop, BrandPalette pal, int highlightStart, int highlightLength)
    {
        var glyphs = text.Place(cap, x, capTop);
        var map = text.CharIndex;
        if ((highlightStart < 0) || (map == null))
        {
            Fill(g, glyphs, pal.Text);
            return;
        }
        var normal = new List<String>();
        var accent = new List<String>();
        for (int i = 0; i < glyphs.Count; ++i)
        {
            var c = map[i] + offset;
            ((c >= highlightStart) && (c < highlightStart + highlightLength) ? accent : normal).Add(glyphs[i]);
        }
        Fill(g, normal, pal.Text);
        //  The accent letter color (at least 4.5:1, the wordmark is not "large text" at the minimum logo size)
        Fill(g, accent, pal.AccentLetters);
    }

    static void AddIcon(SvgScene scene, MarkDesign d, bool opaque)
    {
        var w = scene.Width;
        var h = scene.Height;
        var size = Math.Min(w, h);
        var g = new StringBuilder();
        if (opaque)
            g.Append($"<rect width='{F(w)}' height='{F(h)}' fill='{d.Palette.Background}'/>");
        DrawMark(scene, g, d, d.Palette, (w - size) * 0.5, (h - size) * 0.5, size, true, opaque, size <= SmallSize ? size : 0);
        scene.AddGeometry(g.ToString());
    }

    /// <summary>
    /// A thread (a stroked poly line with round caps)
    /// </summary>
    sealed class Thread
    {
        public List<(double X, double Y)> Pts;
        public String Color;
        public double Width;
        /// <summary>
        /// Point index ranges where this thread goes over other threads (drawn before the letters)
        /// </summary>
        public readonly List<(int From, int To)> OverThreads = new();
        /// <summary>
        /// Point index ranges where this thread goes over the letters (drawn after the letters)
        /// </summary>
        public readonly List<(int From, int To)> OverLetters = new();
    }

    static List<(double X, double Y)> Cubic((double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, int n = 64)
    {
        var res = new List<(double X, double Y)>(n + 1);
        for (int i = 0; i <= n; ++i)
        {
            var t = (double)i / n;
            var it = 1 - t;
            var a = it * it * it;
            var b = 3 * it * it * t;
            var c = 3 * it * t * t;
            var e = t * t * t;
            res.Add((a * p0.X + b * p1.X + c * p2.X + e * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + e * p3.Y));
        }
        return res;
    }

    static List<(double X, double Y)> Ellipse(double cx, double cy, double rx, double ry, double tiltDegrees, double t0, double t1, int n)
    {
        var res = new List<(double X, double Y)>(n + 1);
        var th = tiltDegrees * Math.PI / 180;
        var ct = Math.Cos(th);
        var st = Math.Sin(th);
        for (int i = 0; i <= n; ++i)
        {
            var t = t0 + (t1 - t0) * i / n;
            var ex = rx * Math.Cos(t);
            var ey = ry * Math.Sin(t);
            res.Add((cx + ex * ct - ey * st, cy + ex * st + ey * ct));
        }
        return res;
    }

    static List<(double X, double Y)> MirrorX(List<(double X, double Y)> pts, bool mirror)
    {
        if (!mirror)
            return pts;
        return pts.Select(p => (U - p.X, p.Y)).ToList();
    }

    static String ToPath(List<(double X, double Y)> pts, int from, int to)
    {
        var sb = new StringBuilder();
        for (int i = from; i <= to; ++i)
        {
            var p = pts[i];
            sb.Append(i == from ? 'M' : 'L').Append(F(p.X)).Append(' ').Append(F(p.Y));
        }
        return sb.ToString();
    }

    static void Stroke(StringBuilder g, String path, String color, double width)
        => g.Append($"<path d='{path}' fill='none' stroke='{color}' stroke-width='{F(width)}' stroke-linecap='round' stroke-linejoin='round'/>");

    /// <summary>
    /// Draw a part of a thread over what's already drawn, with a gap (in the background color) around it
    /// </summary>
    static void Over(StringBuilder g, Thread t, (int From, int To) r, String bg, double gap)
    {
        var pts = t.Pts;
        //  The gap has round caps, so trim it to end where the thread part ends
        var trim = t.Width * 0.5 + gap;
        int a = r.From;
        double len = 0;
        while ((a < r.To) && (len < trim))
        {
            len += Dist(pts[a], pts[a + 1]);
            ++a;
        }
        int b = r.To;
        len = 0;
        while ((b > a) && (len < trim))
        {
            len += Dist(pts[b], pts[b - 1]);
            --b;
        }
        if (b > a)
            Stroke(g, ToPath(pts, a, b), bg, t.Width + gap * 2);
        Stroke(g, ToPath(pts, r.From, r.To), t.Color, t.Width);
    }

    static double Dist((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Draw a tone on tone plain weave (like the brand weave pattern), covering 0, 0 - width, height (and a bit more)
    /// </summary>
    /// <param name="g">The output</param>
    /// <param name="width">The width of the area</param>
    /// <param name="height">The height of the area</param>
    /// <param name="pitch">The distance between threads</param>
    /// <param name="tw">The thread width</param>
    /// <param name="gap">The gap between a thread and a crossing thread above it</param>
    /// <param name="tone">The thread color</param>
    /// <param name="copperColumn">The index of a vertical thread to draw in a different color, -1 for none</param>
    /// <param name="copper">The color of that thread</param>
    static void Weave(StringBuilder g, double width, double height, double pitch, double tw, double gap, String tone, int copperColumn, String copper)
    {
        int nx = (int)Math.Ceiling(width / pitch);
        int ny = (int)Math.Ceiling(height / pitch);
        var off = (pitch - tw) * 0.5;
        var tp = new StringBuilder();
        var cp = new StringBuilder();
        static void Rect(StringBuilder sb, double x, double y, double w, double h)
        {
            if ((w > 0) && (h > 0))
                sb.Append($"M{F(x)} {F(y)}h{F(w)}v{F(h)}h{F(-w)}z");
        }
        //  Horizontal threads, under the vertical threads where (i + j) is even
        for (int j = 0; j < ny; ++j)
        {
            var y = j * pitch + off;
            var start = -pitch;
            for (int i = -1; i <= nx; ++i)
            {
                if (((i + j + 2) & 1) != 0)
                    continue;
                var xi = i * pitch + off;
                Rect(tp, start, y, xi - gap - start, tw);
                start = xi + tw + gap;
            }
            Rect(tp, start, y, (nx + 1) * pitch - start, tw);
        }
        //  Vertical threads, under the horizontal threads where (i + j) is odd
        for (int i = 0; i < nx; ++i)
        {
            var x = i * pitch + off;
            var sb = i == copperColumn ? cp : tp;
            var start = -pitch;
            for (int j = -1; j <= ny; ++j)
            {
                if (((i + j + 2) & 1) == 0)
                    continue;
                var yj = j * pitch + off;
                Rect(sb, x, start, tw, yj - gap - start);
                start = yj + tw + gap;
            }
            Rect(sb, x, start, tw, (ny + 1) * pitch - start);
        }
        if (tp.Length > 0)
            g.Append($"<path d='{tp}' fill='{tone}'/>");
        if (cp.Length > 0)
            g.Append($"<path d='{cp}' fill='{copper}'/>");
    }

    /// <summary>
    /// Scenes up to this size (in pixels) get the small (favicon) variant: initials only (one letter at 20 px and below), larger and bolder,
    /// no thread motif (an optional solid accent band instead) and a one pixel edge, like the 32 px "small master" of the brand guide.
    /// </summary>
    public const double SmallSize = 48;

    /// <summary>
    /// Keep the longest run of points that are inside an area
    /// </summary>
    static List<(double X, double Y)> KeepInside(List<(double X, double Y)> pts, Func<(double X, double Y), bool> inside)
    {
        int bestStart = 0, bestLen = 0, start = -1;
        for (int i = 0; i <= pts.Count; ++i)
        {
            var ok = (i < pts.Count) && inside(pts[i]);
            if (ok && (start < 0))
                start = i;
            if (!ok && (start >= 0))
            {
                if (i - start > bestLen)
                {
                    bestLen = i - start;
                    bestStart = start;
                }
                start = -1;
            }
        }
        return pts.GetRange(bestStart, bestLen);
    }

    /// <summary>
    /// Draw a mark (initials + thread motif, optionally on a tile), the mark is designed in a 256 x 256 coordinate system and scaled to the size.
    /// </summary>
    /// <param name="scene">The scene (used to add masks)</param>
    /// <param name="g">The output</param>
    /// <param name="d">The design</param>
    /// <param name="pal">The palette to use</param>
    /// <param name="x">The left position</param>
    /// <param name="y">The top position</param>
    /// <param name="size">The size</param>
    /// <param name="tile">True to draw the tile (background)</param>
    /// <param name="square">True to draw a full bleed square tile</param>
    /// <param name="px">If greater than zero, draw the small variant made for this pixel size</param>
    /// <param name="tileWidth">The width of the tile (in the 256 x 256 mark coordinate system), a wider tile stretches the background to the right (the mark itself is unchanged)</param>
    static void DrawMark(SvgScene scene, StringBuilder g, MarkDesign d, BrandPalette pal, double x, double y, double size, bool tile, bool square, double px, double tileWidth = U)
    {
        var bg = pal.Background;
        var small = px > 0;
        g.Append($"<g transform='translate({F(x)} {F(y)}) scale({S(size / U)})'>");
        var circle = d.IsCircle && !square;
        var radius = d.TileRadius * U;
        var wide = tileWidth > U;
        if (tile)
        {
            var shape = square
                ? "<rect width='256' height='256'/>"
                : (circle ? "<circle cx='128' cy='128' r='128'/>" : $"<rect width='256' height='256' rx='{F(radius)}'/>");
            //  A wide tile only stretches the background, the contents are still clipped to the square tile shape (so the mark is identical)
            var bgShape = !wide
                ? shape
                : (square ? $"<rect width='{F(tileWidth)}' height='256'/>" : $"<rect width='{F(tileWidth)}' height='256' rx='{F(radius)}'/>");
            g.Append(WithFill(bgShape, bg));
            var mask = scene.AddMask(shape);
            g.Append($"<g mask='url(#{mask})'>");
        }
        var tw = d.ThreadWidth * U;
        var gap = tw * 0.42;
        var motif = small ? MarkMotif.None : d.Motif;
        //  Thread colors, the letters and the threads use different colors
        var threadColor = d.Letters switch
        {
            LetterStyle.Accent => pal.Main,
            LetterStyle.TwoTone => pal.Accent2,
            LetterStyle.Tint => pal.Accent,
            LetterStyle.TintTwoTone => pal.Accent,
            _ => pal.Accent,
        };
        var threadColor2 = threadColor == pal.Accent ? pal.Accent2 : pal.Accent;
        //  Safe area: threads (including their round caps) stay at least 8% inside the tile
        var safe = 0.08 * U + tw * 0.5;
        Func<(double X, double Y), bool> inside;
        if (!tile)
        {
            inside = p => (p.X >= 0.04 * U) && (p.X <= 0.96 * U) && (p.Y >= 0.04 * U) && (p.Y <= 0.96 * U);
        }
        else if (circle)
        {
            inside = p => Dist(p, (U * 0.5, U * 0.5)) <= U * 0.5 - safe;
        }
        else
        {
            //  Inside the tile shape inset by the safe distance (a rounded rect with the radius reduced by the inset)
            var ir = Math.Max(0, (square ? 0 : radius) - safe);
            inside = p =>
            {
                if ((p.X < safe) || (p.X > U - safe) || (p.Y < safe) || (p.Y > U - safe))
                    return false;
                var ccx = Math.Clamp(p.X, safe + ir, U - safe - ir);
                var ccy = Math.Clamp(p.Y, safe + ir, U - safe - ir);
                return Dist(p, (ccx, ccy)) <= ir + 0.001;
            };
        }

        //  Small variant accent shape (a solid band or bar, at least 3 pixels), not at the tiniest sizes where the letters need all the space
        var smallAccent = small && tile && (px >= 24) ? d.SmallAccent : 0;
        var barSize = Math.Max(0.2 * U, 3 * U / Math.Max(px, 1));
        //  The band uses a color the letters don't use
        var bandColor = d.Letters switch
        {
            LetterStyle.TwoTone => pal.Tint,
            LetterStyle.Accent => pal.Tint,
            _ => pal.Accent,
        };
        if (smallAccent == 1)
            g.Append($"<rect y='{F(U - barSize)}' width='256' height='{F(barSize)}' fill='{bandColor}'/>");
        else if (smallAccent == 2)
            g.Append($"<rect width='{F(barSize)}' height='256' fill='{bandColor}'/>");

        //  Letter layout, at the smallest sizes two letters are kept (the condensed font allows it), so apps sharing a first letter still differ
        var initials = d.Initials;
        if (small && (initials.Length > 2))
            initials = initials.Substring(0, 2);
        //  Upper case only at the smallest sizes (a lower case letter is only about 5 px high at 16 px)
        if (small && (px <= 20))
            initials = initials.ToUpperInvariant();
        var n = initials.Length;
        var text = new BrandText(initials, d.Font);
        double cap, maxW;
        if (small)
        {
            cap = (n == 1 ? 0.56 : (px <= 20 ? 0.44 : 0.46)) * U;
            maxW = (n == 1 ? 0.7 : (px <= 20 ? 0.9 : 0.84)) * U;
        }
        else
        {
            cap = (n == 1 ? 0.44 : (n == 2 ? 0.38 : 0.29)) * U;
            maxW = (n == 1 ? 0.56 : (n == 2 ? 0.66 : 0.72)) * U;
        }
        double cx = U * 0.5;
        double cy = U * 0.5;
        if (smallAccent == 1)
        {
            cap *= 0.9;
            cy -= barSize * 0.5;
        }
        else if (smallAccent == 2)
        {
            maxW = Math.Min(maxW, U - barSize - 0.12 * U);
            cx += barSize * 0.5;
        }
        switch (motif)
        {
            case MarkMotif.None:
                if (!small)
                {
                    cap *= 1.06;
                    maxW *= 1.06;
                }
                break;
            case MarkMotif.Swoosh:
                cy -= 0.06 * U;
                if (!d.IsUser)
                {
                    //  Room for the rising end of the hook
                    maxW *= 0.9;
                    cx += (d.Mirror ? 0.05 : -0.05) * U;
                }
                break;
            case MarkMotif.WeavePanel:
                cap = Math.Min(cap, 0.28 * U);
                maxW = Math.Min(maxW, 0.66 * U);
                break;
            case MarkMotif.Waves:
                cap *= 0.9;
                cy -= 0.08 * U;
                break;
            case MarkMotif.Underline:
                cy -= 0.05 * U;
                break;
        }
        if (circle)
            maxW *= 0.92;
        if ((text.WidthPerCap > 0) && (text.WidthPerCap * cap > maxW))
            cap = maxW / text.WidthPerCap;
        var lw = text.WidthPerCap * cap;
        var glyphs = text.Place(cap, cx - lw * 0.5, cy - cap * 0.5);
        var lb = SvgPath.GetMinMaxForPaths(glyphs);
        var baseline = cy + cap * 0.5;

        //  Threads
        var threads = new List<Thread>();
        Thread Add(List<(double X, double Y)> pts, String color, double width = 0)
        {
            var t = new Thread
            {
                Pts = pts,
                Color = color,
                Width = width > 0 ? width : tw,
            };
            threads.Add(t);
            return t;
        }
        bool halo = motif != MarkMotif.None;
        if (n == 0)
        {
            //  No drawable initials (ex: non latin names): interlocked thread rings (weaving over / under each other),
            //  the ring count and arrangement varies, so these marks differ in shape (not only in color)
            const int count = 120;
            (double X, double Y)[] centers;
            double rr;
            switch (d.FallbackVariant)
            {
                case 1:
                    centers = [(0.32 * U, 0.5 * U), (0.5 * U, 0.5 * U), (0.68 * U, 0.5 * U)];
                    rr = 0.14 * U;
                    break;
                case 2:
                    centers = [(0.39 * U, 0.55 * U), (0.61 * U, 0.45 * U)];
                    rr = 0.18 * U;
                    break;
                default:
                    centers = [(0.4 * U, 0.5 * U), (0.6 * U, 0.5 * U)];
                    rr = 0.19 * U;
                    break;
            }
            var rw = rr * 0.38;
            String[] cols = [pal.Main, pal.Accent, pal.Accent2];
            var rings = centers.Select((c, i) => Add(Ellipse(c.X, c.Y, rr, rr, 0, 0, Math.PI * 2, count), cols[i % cols.Length], rw)).ToList();
            //  Each pair of overlapping rings cross twice: the first ring goes over at one crossing, the second at the other
            int Index(double ax, double ay, (double X, double Y) c) => (int)Math.Round(((Math.Atan2(ay - c.Y, ax - c.X) + Math.PI * 2) % (Math.PI * 2)) / (Math.PI * 2) * count) % count;
            for (int i = 0; i < centers.Length; ++i)
            {
                for (int j = i + 1; j < centers.Length; ++j)
                {
                    var a = centers[i];
                    var b = centers[j];
                    var dd = Dist(a, b);
                    if ((dd >= rr * 2) || (dd <= 0))
                        continue;
                    //  The crossing points
                    var mx = (a.X + b.X) * 0.5;
                    var my = (a.Y + b.Y) * 0.5;
                    var hh = Math.Sqrt(rr * rr - dd * dd * 0.25);
                    var ox = -(b.Y - a.Y) / dd * hh;
                    var oy = (b.X - a.X) / dd * hh;
                    var span = count / 18;
                    var ia = Index(mx + ox, my + oy, a);
                    var ib = Index(mx - ox, my - oy, b);
                    if ((ia - span >= 0) && (ia + span <= count))
                        rings[i].OverThreads.Add((ia - span, ia + span));
                    if ((ib - span >= 0) && (ib + span <= count))
                        rings[j].OverThreads.Add((ib - span, ib + span));
                }
            }
            halo = false;
        }
        else
        {
            switch (motif)
            {
                case MarkMotif.Swoosh:
                    if (d.IsUser)
                    {
                        //  Under the letters, never crossing them
                        var by = baseline;
                        Add(KeepInside(MirrorX(Cubic((lb.MinX - 0.05 * U, by + 0.15 * U), (cx - 0.1 * U, by + 0.21 * U), (lb.MaxX - 0.04 * U, by + 0.13 * U), (lb.MaxX + 0.07 * U, by + 0.02 * U)), d.Mirror), inside), threadColor);
                    }
                    else
                    {
                        //  An asymmetric hook: a flat thread under the letters that rises steeply past the last letter (not a centered "smile")
                        var by = baseline + 0.1 * U;
                        //  The curve is made unmirrored (rising at the right) and mirrored afterwards
                        var ex = d.Mirror ? U - lb.MinX + 0.13 * U : lb.MaxX + 0.13 * U;
                        Add(KeepInside(MirrorX(Cubic((0.1 * U, by + 0.02 * U), (0.55 * U, by + 0.04 * U), (ex, by + 0.03 * U), (ex + 0.02 * U, cy + cap * 0.05)), d.Mirror), inside), threadColor);
                    }
                    break;
                case MarkMotif.WeavePanel:
                    {
                        Weave(g, U, U, 32, 9, 2.5, pal.Surface, d.Mirror ? 7 - d.CopperColumn : d.CopperColumn, pal.Accent);
                        g.Append($"<rect x='{F(0.08 * U)}' y='{F(0.27 * U)}' width='{F(0.84 * U)}' height='{F(0.46 * U)}' rx='{F(0.06 * U)}' fill='{bg}'/>");
                        halo = false;
                    }
                    break;
                case MarkMotif.Waves:
                    {
                        //  A calm braid of two thinner threads under the letters (one period, two over / under crossings), ending inside the tile
                        const int count = 160;
                        var bw = 0.04 * U;
                        var y0 = baseline + 0.12 * U + bw;
                        var amp = 0.032 * U;
                        var freq = Math.PI * 2 * 1.0 / (0.7 * U);
                        double x0 = 0, x1 = U;
                        while ((x0 < U * 0.5) && !(inside((x0, y0 - amp)) && inside((x0, y0 + amp))))
                            x0 += 1;
                        while ((x1 > U * 0.5) && !(inside((x1, y0 - amp)) && inside((x1, y0 + amp))))
                            x1 -= 1;
                        List<(double X, double Y)> Wave(double phase)
                        {
                            var res = new List<(double X, double Y)>(count + 1);
                            for (int i = 0; i <= count; ++i)
                            {
                                var wx = x0 + (x1 - x0) * i / count;
                                res.Add((wx, y0 + amp * Math.Sin(wx * freq + phase)));
                            }
                            return res;
                        }
                        var w1 = Add(Wave(d.Phase), threadColor, bw);
                        var w2 = Add(Wave(d.Phase + Math.PI), threadColor2, bw);
                        var half = (int)Math.Round(0.07 * U / ((x1 - x0) / count));
                        int k = 0;
                        for (int i = 1; i <= count; ++i)
                        {
                            var s0 = Math.Sin(w1.Pts[i - 1].X * freq + d.Phase);
                            var s1 = Math.Sin(w1.Pts[i].X * freq + d.Phase);
                            if ((s0 < 0) == (s1 < 0))
                                continue;
                            var r = (Math.Max(0, i - half), Math.Min(count, i + half));
                            ((k & 1) == 0 ? w1 : w2).OverThreads.Add(r);
                            ++k;
                        }
                    }
                    break;
                case MarkMotif.Underline:
                    {
                        var uy = baseline + 0.085 * U;
                        var ux0 = lb.MinX + tw * 0.5;
                        var ux1 = lb.MaxX - tw * 0.5;
                        if (d.Mirror)
                            ux1 = ux0 + (ux1 - ux0) * 0.55;
                        Add(new List<(double X, double Y)> { (ux0, uy), (ux1, uy) }, threadColor);
                    }
                    break;
            }
        }
        //  Threads behind the letters
        foreach (var t in threads)
            if (t.Pts.Count > 1)
                Stroke(g, ToPath(t.Pts, 0, t.Pts.Count - 1), t.Color, t.Width);
        foreach (var t in threads)
            foreach (var r in t.OverThreads)
                Over(g, t, r, bg, gap);
        //  Letters, with a gap to the threads behind
        if (halo && (threads.Count > 0) && (glyphs.Count > 0))
            g.Append($"<path d='{SvgPath.JoinPaths(glyphs)}' fill='{bg}' stroke='{bg}' stroke-width='{F(gap * 2)}' stroke-linejoin='round'/>");
        //  Small sizes: slightly bolder letters (a third of a pixel)
        var bold = small ? U / px * 0.33 : 0;
        void Letters(IEnumerable<String> paths, String color)
        {
            var path = SvgPath.JoinPaths(paths);
            if (String.IsNullOrEmpty(path))
                return;
            if (bold > 0)
                g.Append($"<path d='{path}' fill='{color}' stroke='{color}' stroke-width='{F(bold)}' stroke-linejoin='round'/>");
            else
                g.Append($"<path d='{path}' fill='{color}'/>");
        }
        switch (d.Letters)
        {
            case LetterStyle.TwoTone:
                Letters(glyphs.Take(1), d.TwoToneFirst ?? pal.Main);
                Letters(glyphs.Skip(1), pal.AccentLetters);
                break;
            case LetterStyle.Accent:
                Letters(glyphs, pal.AccentLetters);
                break;
            case LetterStyle.Tint:
                Letters(glyphs, pal.Tint);
                break;
            case LetterStyle.TintTwoTone:
                Letters(glyphs.Take(1), d.TintFirst ?? pal.Main);
                Letters(glyphs.Skip(1), pal.Tint);
                break;
            default:
                Letters(glyphs, pal.Main);
                break;
        }
        //  Threads in front of the letters
        foreach (var t in threads)
            foreach (var r in t.OverLetters)
                Over(g, t, r, bg, gap);
        if (tile)
        {
            //  A thin edge, so the tile is visible on any background: one pixel for small sizes,
            //  a subtle tone on tone edge for large bright tiles (none for large dark tiles, no "outline" effect)
            var edge = small ? pal.Edge : pal.EdgeLarge;
            //  The edge of a wide tile is outside of the (square) mask
            if (wide)
                g.Append("</g>");
            if (!square && (edge != null))
            {
                var ew = small ? U / px : 0.012 * U;
                var h = ew * 0.5;
                if (circle && !wide)
                    g.Append($"<circle cx='128' cy='128' r='{F(128 - h)}' fill='none' stroke='{pal.Edge}' stroke-width='{F(ew)}'/>");
                else
                    g.Append($"<rect x='{F(h)}' y='{F(h)}' width='{F(tileWidth - ew)}' height='{F(U - ew)}' rx='{F(Math.Max(0, radius - h))}' fill='none' stroke='{pal.Edge}' stroke-width='{F(ew)}'/>");
            }
            if (!wide)
                g.Append("</g>");
        }
        g.Append("</g>");
    }

    #endregion
}
