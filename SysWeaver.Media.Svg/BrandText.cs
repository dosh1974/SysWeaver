using System.Globalization;
using System;
using System.IO;
using System.Collections.Generic;

namespace SysWeaver.Media;


/// <summary>
/// A shaped line of text (glyph paths at a fixed size), with the font metrics needed to place it using the cap height
/// (so that descenders, like the "y" in SysWeaver, don't change the size or position of the text).
/// </summary>
sealed class BrandText
{
    const double Size = 100;

    /// <summary>
    /// The glyph paths (absolute coordinates at the shaping size)
    /// </summary>
    public readonly List<String> Paths;

    /// <summary>
    /// The index of the char in the text for each glyph path, null if the mapping is unknown
    /// </summary>
    public readonly int[] CharIndex;

    /// <summary>
    /// The bounds of all glyphs
    /// </summary>
    public readonly SvgMinMaxState Bounds;

    /// <summary>
    /// The y position of the top of a capital letter ("H")
    /// </summary>
    public readonly double CapTop;

    /// <summary>
    /// The y position of the base line
    /// </summary>
    public readonly double Baseline;

    /// <summary>
    /// The cap height (Baseline - CapTop)
    /// </summary>
    public double CapHeight => Baseline - CapTop;

    /// <summary>
    /// The width of the text when the cap height is 1
    /// </summary>
    public double WidthPerCap => Bounds.Width / CapHeight;

    /// <summary>
    /// The depth of the descenders (below the base line) when the cap height is 1
    /// </summary>
    public double DescentPerCap => Math.Max(0, Bounds.MaxY - Baseline) / CapHeight;

    /// <summary>
    /// True if there are no glyphs
    /// </summary>
    public bool IsEmpty => Paths.Count == 0;

    public BrandText(String text, SvgFont font)
    {
        Paths = SvgPath.GetSvgTextPaths(text, font, Size, 2);
        //  Glyphs without an outline (space, missing chars) don't produce a path, so map each path to a char
        var idx = new List<int>(text.Length);
        for (int i = 0; i < text.Length; ++i)
            if (HasGlyph(font, text[i]))
                idx.Add(i);
        CharIndex = idx.Count == Paths.Count ? idx.ToArray() : null;
        Bounds = SvgPath.GetMinMaxForPaths(Paths);
        var h = SvgPath.GetMinMaxForPaths(SvgPath.GetSvgTextPaths("H", font, Size, 2));
        CapTop = h.MinY;
        Baseline = h.MaxY;
    }

    /// <summary>
    /// Check if a font have a visible glyph for a char
    /// </summary>
    /// <param name="font">The font</param>
    /// <param name="c">The char</param>
    /// <returns>True if the char produces an outline</returns>
    public static bool HasGlyph(SvgFont font, Char c)
        => !Char.IsWhiteSpace(c) && SvgPath.GetSvgTextPaths(c.ToString(), font, 10, 0).Count > 0;

    /// <summary>
    /// Get the glyph paths scaled and translated
    /// </summary>
    /// <param name="capHeight">The cap height to use</param>
    /// <param name="left">The left position of the text (glyph bounds)</param>
    /// <param name="capTop">The y position of the top of the capital letters</param>
    /// <returns>New glyph paths</returns>
    public List<String> Place(double capHeight, double left, double capTop)
    {
        var s = capHeight / CapHeight;
        var paths = new List<String>(Paths);
        SvgPath.TransformPaths(paths, s, s, left - Bounds.MinX * s, capTop - CapTop * s, 2);
        return paths;
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Format a number for svg (invariant, max 2 decimals)
    /// </summary>
    public static String F(double v) => v.ToString("0.##", Inv);
}
