using SysWeaver;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SysWeaver.Media;


/// <summary>
/// The thread motif of a mark (drawn around / behind the initials)
/// </summary>
enum MarkMotif
{
    /// <summary>
    /// Just the initials
    /// </summary>
    None,
    /// <summary>
    /// A curved thread under the initials, rising past the last letter (users: a gentle curve under the letters)
    /// </summary>
    Swoosh,
    /// <summary>
    /// Plain weave background with a single copper thread, the initials on a solid band
    /// </summary>
    WeavePanel,
    /// <summary>
    /// Two sine shaped threads woven with each other (over / under) behind the initials
    /// </summary>
    Waves,
    /// <summary>
    /// A short thread under the initials
    /// </summary>
    Underline,
}

/// <summary>
/// How the initials are colored
/// </summary>
enum LetterStyle
{
    /// <summary>
    /// All letters in the main color, threads in the accent color
    /// </summary>
    Mono,
    /// <summary>
    /// The first letter in the main color, the others in the accent color (like the S and W in the brand mark)
    /// </summary>
    TwoTone,
    /// <summary>
    /// All letters in the accent color, threads in the main color
    /// </summary>
    Accent,
    /// <summary>
    /// All letters in the tint color (from the raw complement hue), threads in the accent color
    /// </summary>
    Tint,
    /// <summary>
    /// The first letter in the main color, the others in the tint color, threads in the accent color
    /// </summary>
    TintTwoTone,
}

/// <summary>
/// All (deterministic) design choices for a mark (icon) and logo, derived from the name, colors and seed.
/// The icon choices are made first (in a fixed order), so the icon of an app and the mark in its logo are the same.
/// </summary>
sealed class MarkDesign
{
    public String Name;
    public bool IsUser;
    public String Initials;
    public SvgFont Font;
    public HashColors Colors;
    public BrandPalette Palette;
    /// <summary>
    /// The lightness step of the palette [0, 2]
    /// </summary>
    public int Shade;
    /// <summary>
    /// The first letter color for <see cref="LetterStyle.TwoTone"/> (null if no color differs enough in luminance)
    /// </summary>
    public String TwoToneFirst;
    /// <summary>
    /// The first letter color for <see cref="LetterStyle.TintTwoTone"/> (null if no color differs enough in luminance)
    /// </summary>
    public String TintFirst;
    /// <summary>
    /// The tile corner radius as a fraction of the size, 0.5 is a circle
    /// </summary>
    public double TileRadius;
    public MarkMotif Motif;
    public LetterStyle Letters;
    public bool Mirror;
    public double Tilt;
    public double Phase;
    public int CopperColumn;
    public double ThreadWidth;
    /// <summary>
    /// Small (favicon) variant accent shape: 0 = none, 1 = a solid band at the bottom, 2 = a solid bar at the left (a shape cue, not only a color cue)
    /// </summary>
    public int SmallAccent;
    /// <summary>
    /// The layout of the mark used for names without drawable initials (ring count and arrangement)
    /// </summary>
    public int FallbackVariant;

    //  Logo choices

    public bool LogoHorizontal;
    public bool LogoShowTile;
    public bool LogoWeave;
    /// <summary>
    /// The index of the first char in the name to highlight (accent color) in the wordmark, -1 for none
    /// </summary>
    public int LogoHighlightStart;
    /// <summary>
    /// The number of chars to highlight
    /// </summary>
    public int LogoHighlightLength;
    public BrandPalette LogoPalette;

    public bool IsCircle => TileRadius >= 0.5;

    static T Pick<T>(Random rng, params (int Weight, T Value)[] options)
    {
        int total = 0;
        foreach (var x in options)
            total += x.Weight;
        var r = rng.Next(total);
        foreach (var x in options)
        {
            r -= x.Weight;
            if (r < 0)
                return x.Value;
        }
        return options[^1].Value;
    }

    /// <summary>
    /// Split a name into words: on any non letter / digit char and on camel case boundaries ("SysWeaver" => "Sys", "Weaver", "HTTPServer" => "HTTP", "Server").
    /// Words with only digits are removed (unless all words are digits).
    /// </summary>
    /// <param name="s">The name</param>
    /// <returns>The start index and length of each word</returns>
    public static List<(int Start, int Length)> SplitWords(String s)
    {
        var res = new List<(int Start, int Length)>();
        int start = -1;
        var l = s.Length;
        for (int i = 0; i <= l; ++i)
        {
            bool isWord = (i < l) && Char.IsLetterOrDigit(s[i]);
            bool boundary = false;
            if (isWord && (start >= 0))
            {
                var c = s[i];
                var p = s[i - 1];
                if (Char.IsUpper(c) && Char.IsLower(p))
                    boundary = true;
                else if (Char.IsUpper(c) && Char.IsUpper(p) && (i + 1 < l) && Char.IsLower(s[i + 1]))
                    boundary = true;
                else if (Char.IsDigit(c) != Char.IsDigit(p))
                    boundary = true;
            }
            if ((start >= 0) && (!isWord || boundary))
            {
                res.Add((start, i - start));
                start = -1;
            }
            if (isWord && (start < 0))
                start = i;
        }
        var letters = res.Where(w => !Char.IsDigit(s[w.Start])).ToList();
        return letters.Count > 0 ? letters : res;
    }

    static String GetInitials(String name, List<(int Start, int Length)> words, bool isUser, SvgFont font, Random rng, out int secondIndex)
    {
        secondIndex = -1;
        //  Always consume the same amount of random numbers
        var r0 = rng.Next(100);
        var r1 = rng.Next(100);
        var w = words.Where(x => BrandText.HasGlyph(font, name[x.Start])).ToList();
        if (w.Count <= 0)
            return "";
        String t;
        if (w.Count == 1)
        {
            var word = w[0];
            var first = Char.ToUpperInvariant(name[word.Start]).ToString();
            t = first;
            if ((word.Length > 1) && (r0 < 50) && BrandText.HasGlyph(font, name[word.Start + 1]))
                t += Char.ToLowerInvariant(name[word.Start + 1]);
            return t;
        }
        //  Users: first and last name, apps: the first two (or three) words
        var second = isUser ? w[^1] : w[1];
        secondIndex = second.Start;
        t = String.Concat(Char.ToUpperInvariant(name[w[0].Start]), Char.ToUpperInvariant(name[second.Start]));
        if (!isUser && (w.Count >= 3) && (r1 < 35))
            t += Char.ToUpperInvariant(name[w[2].Start]);
        return t;
    }

    /// <summary>
    /// Create the design for a name
    /// </summary>
    /// <param name="name">The app or user name</param>
    /// <param name="color">The theme, null to derive it from the name and seed</param>
    /// <param name="seed">The seed</param>
    /// <param name="isUser">True for a user icon, false for an app icon / logo</param>
    /// <returns>The design</returns>
    public static MarkDesign Create(String name, HashColors color, int seed, bool isUser)
    {
        name = name ?? "";
        color = color ?? new HashColors(name, seed);
        //  The layout depends on the name too, so different names using the same colors (ex: the app colors) still get different marks
        var rng = new Random(color.Seed ^ HashColors.SeedFromString(name) ^ (seed * 0x5bd1e995) ^ (isUser ? 0x2c1b3c6d : 0));
        var d = new MarkDesign
        {
            Name = name,
            IsUser = isUser,
            Colors = color,
        };
        //  Users are often email addresses
        var shown = name;
        if (isUser)
        {
            var at = name.IndexOf('@');
            if (at > 0)
                shown = name.Substring(0, at);
        }
        var words = SplitWords(shown);
        d.Font = BrandFonts.Saira;
        d.Initials = GetInitials(shown, words, isUser, d.Font, rng, out var secondIndex);
        BrandPaletteKind kind;
        if (isUser)
        {
            kind = Pick(rng, (40, BrandPaletteKind.Vivid), (30, BrandPaletteKind.Deep), (15, BrandPaletteKind.Bright), (15, BrandPaletteKind.Dark));
            d.TileRadius = Pick(rng, (60, 0.5), (40, 0.2234));
            //  Only motifs that stay clear of the letters
            d.Motif = Pick(rng,
                (45, MarkMotif.None),
                (30, MarkMotif.Underline),
                (25, MarkMotif.Swoosh));
            d.ThreadWidth = 0.05;
        }
        else
        {
            //  Mostly slate (brand), Vivid is rare and toned down
            kind = Pick(rng, (44, BrandPaletteKind.Dark), (32, BrandPaletteKind.Deep), (14, BrandPaletteKind.Bright), (10, BrandPaletteKind.Vivid));
            d.TileRadius = Pick(rng, (70, 0.2234), (15, 0.18), (15, 0.27));
            //  Only motifs that don't strike through the letters
            d.Motif = Pick(rng,
                (28, MarkMotif.Swoosh),
                (20, MarkMotif.WeavePanel),
                (24, MarkMotif.Waves),
                (28, MarkMotif.Underline));
            d.ThreadWidth = 0.05;
        }
        //  Dark olive / brown backgrounds look muddy: amber / terracotta hues use a mid tone, yellow / yellow green hues the bright tile
        if (((kind == BrandPaletteKind.Dark) || (kind == BrandPaletteKind.Deep)) && BrandPalette.IsMuddyDark(color.Hue))
            kind = color.Hue < 45 ? BrandPaletteKind.Vivid : BrandPaletteKind.Bright;
        //  No acid yellow green tiles
        if ((kind == BrandPaletteKind.Vivid) && (color.Hue >= 45) && (color.Hue <= 110))
            kind = BrandPaletteKind.Bright;
        //  Three lightness steps within a palette kind, so apps with the same hue and kind still differ
        d.Shade = rng.Next(3);
        d.Palette = BrandPalette.Create(color, kind, d.Shade);
        var pal = d.Palette;
        var twoLetters = d.Initials.Length > 1;
        //  Two tone letters must differ in luminance too (not only in hue), else they merge for color blind users.
        //  Steel vs copper (the brand mark) is just below 1.5:1, so the first letter uses the (lighter / darker) text color when needed.
        String First(String other) =>
            BrandPalette.Contrast(pal.Main, other) >= 1.5 ? pal.Main : (BrandPalette.Contrast(pal.Text, other) >= 1.5 ? pal.Text : null);
        d.TwoToneFirst = twoLetters ? First(pal.AccentLetters) : null;
        d.TintFirst = twoLetters ? First(pal.Tint) : null;
        //  The tint (raw complement hue) is never lime / yellow / green, those pastels look off brand
        var tintOk = !((color.ComplementHue >= 55) && (color.ComplementHue <= 120));
        //  Apps: the letter color is a second identification key next to the tile color, five recipes (the brand steel + copper two tone is the most common)
        d.Letters = isUser
            ? Pick(rng,
                (d.TwoToneFirst != null ? 15 : 0, LetterStyle.TwoTone),
                (65, LetterStyle.Mono),
                (tintOk ? 15 : 0, LetterStyle.Tint))
            : Pick(rng,
                (d.TwoToneFirst != null ? 35 : 0, LetterStyle.TwoTone),
                (20, LetterStyle.Mono),
                (15, LetterStyle.Accent),
                (tintOk ? 12 : 0, LetterStyle.Tint),
                (tintOk && (d.TintFirst != null) ? 8 : 0, LetterStyle.TintTwoTone));
        d.Mirror = rng.Next(2) == 0;
        d.Tilt = (rng.Next(2) == 0 ? -1 : 1) * (5 + rng.Next(6));
        d.Phase = rng.NextDouble() * Math.PI * 2;
        d.CopperColumn = 1 + rng.Next(6);
        d.SmallAccent = isUser ? 0 : Pick(rng, (45, 0), (55, 1));
        //  Only horizontal arrangements (a vertical pair reads as an "8")
        d.FallbackVariant = rng.Next(3);

        //  Logo choices (after all icon choices)
        var visibleLength = name.Count(Char.IsLetterOrDigit);
        d.LogoHorizontal = (rng.Next(100) < 50) && (visibleLength <= 10);
        var canBeTileless = (d.Motif == MarkMotif.Underline) || (d.Motif == MarkMotif.None);
        d.LogoShowTile = !canBeTileless || (rng.Next(100) < 40);
        d.LogoWeave = false;
        //  Highlight a whole word (the second), never a single letter inside a word
        d.LogoHighlightStart = -1;
        if ((rng.Next(100) < 50) && (words.Count > 1) && !isUser)
        {
            d.LogoHighlightStart = words[1].Start;
            d.LogoHighlightLength = words[1].Length;
        }
        if (d.LogoShowTile)
        {
            //  The tile needs contrast against the panel
            d.LogoPalette = BrandPalette.Create(color, d.Palette.IsBright ? BrandPaletteKind.Dark : BrandPaletteKind.Bright, d.Palette.IsBright ? d.Shade : 0);
        }
        else
        {
            d.LogoPalette = d.Palette;
        }
        return d;
    }
}
