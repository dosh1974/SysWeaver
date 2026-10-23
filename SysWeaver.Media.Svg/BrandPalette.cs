using System.Globalization;
using System;
using System.IO;

namespace SysWeaver.Media;


/// <summary>
/// The kind of palette, decides how the brand roles are mapped to the hues of a <see cref="HashColors"/> theme
/// </summary>
public enum BrandPaletteKind
{
    /// <summary>
    /// The brand look: a slate background (main hue, low value), steel (main hue, light) and copper (warm accent).
    /// For the main hue 200 and complement hue 25 this is the actual brand palette (Slate 900, Steel 300, Copper 500).
    /// </summary>
    Dark,
    /// <summary>
    /// The bright brand look: a mist background, slate text, steel 600 and copper 600 threads
    /// </summary>
    Bright,
    /// <summary>
    /// A dark background with a more saturated main hue (more colorful, still dark)
    /// </summary>
    Deep,
    /// <summary>
    /// A saturated mid tone background in the main hue, light text on dark hues and dark text on luminous hues (yellow, green, cyan)
    /// </summary>
    Vivid,
}

/// <summary>
/// Brand color roles, derived from a <see cref="HashColors"/> theme.
/// The brand palette is slate (dark blue grey, hue ~205), steel (light / mid blue grey) and copper (orange, hue ~24),
/// copper is roughly the complement of slate, so: slate + steel use the theme main hue and the accent is derived from the theme complement hue,
/// pulled into a warm "copper" band (gold to red copper), or into a cool "steel" band if the main hue itself is warm.
/// The saturation of the theme scales the saturation of the slate and steel colors (0.75x - 1.25x).
/// Minimum contrasts (WCAG) are enforced: main / secondary 4.5:1, text 7:1, threads 3:1, accent colored letters 4.5:1.
/// </summary>
public sealed class BrandPalette
{
    /// <summary>
    /// The kind of palette
    /// </summary>
    public readonly BrandPaletteKind Kind;

    /// <summary>
    /// The background (tile / panel) color, also used for the "gap" (halo) around woven threads and letters
    /// </summary>
    public readonly String Background;

    /// <summary>
    /// A raised surface color (tone on tone weave pattern)
    /// </summary>
    public readonly String Surface;

    /// <summary>
    /// The main mark color (initials, steel thread), at least 4.5:1 against the background
    /// </summary>
    public readonly String Main;

    /// <summary>
    /// The text color (wordmark), at least 7:1 against the background (4.5:1 for light text on a mid tone)
    /// </summary>
    public readonly String Text;

    /// <summary>
    /// The secondary text color (sub title), at least 4.5:1 against the background
    /// </summary>
    public readonly String Secondary;

    /// <summary>
    /// The accent color (copper thread), at least 3:1 against the background
    /// </summary>
    public readonly String Accent;

    /// <summary>
    /// The accent color for letters, at least 4.5:1 against the background
    /// </summary>
    public readonly String AccentLetters;

    /// <summary>
    /// The second thread color (steel thread), at least 3:1 against the background
    /// </summary>
    public readonly String Accent2;

    /// <summary>
    /// A thin edge around tiles, so that the tile is visible on any page or browser chrome (null for none)
    /// </summary>
    public readonly String Edge;

    /// <summary>
    /// A subtle edge for large tiles (null for none)
    /// </summary>
    public readonly String EdgeLarge;

    /// <summary>
    /// A letter color from the raw complement hue (more varied than the copper accent), at least 4.5:1 against the background
    /// </summary>
    public readonly String Tint;

    /// <summary>
    /// True if the background is bright
    /// </summary>
    public readonly bool IsBright;

    BrandPalette(BrandPaletteKind kind, bool isBright, String background, String surface, String main, String text, String secondary, String accent, String accentLetters, String accent2, String tint, String edge, String edgeLarge)
    {
        Kind = kind;
        IsBright = isBright;
        Background = background;
        Surface = surface;
        Main = main;
        Text = text;
        Secondary = secondary;
        Accent = accent;
        AccentLetters = accentLetters;
        Accent2 = accent2;
        Edge = edge;
        EdgeLarge = edgeLarge;
        Tint = tint;
    }

    static String Hsv(double hue, double sat, double value) => HashColors.GetWeb(((hue % 360) + 360) % 360, Math.Clamp(sat, 0, 1), Math.Clamp(value, 0, 1));

    static double Channel(int c)
    {
        var v = c / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// The relative luminance (WCAG) of a "#rrggbb" color
    /// </summary>
    public static double Luminance(String color)
    {
        var v = int.Parse(color.AsSpan(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return 0.2126 * Channel((v >> 16) & 255) + 0.7152 * Channel((v >> 8) & 255) + 0.0722 * Channel(v & 255);
    }

    /// <summary>
    /// The contrast ratio (WCAG) between two "#rrggbb" colors [1, 21]
    /// </summary>
    public static double Contrast(String a, String b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>
    /// Get a color with at least some contrast against a background, the value is moved away from the background (brighter on dark backgrounds, darker on bright).
    /// On dark backgrounds the saturation is lowered if the value can't be increased any more.
    /// </summary>
    static String Ensure(double hue, double sat, double value, String bg, double minContrast)
    {
        var col = Hsv(hue, sat, value);
        var up = Luminance(bg) < 0.18;
        for (int i = 0; (i < 80) && (Contrast(col, bg) < minContrast); ++i)
        {
            if (up)
            {
                if (value < 1)
                    value = Math.Min(1, value + 0.02);
                else
                    sat = Math.Max(0, sat - 0.03);
            }
            else
            {
                value = Math.Max(0, value - 0.02);
            }
            col = Hsv(hue, sat, value);
        }
        return col;
    }

    /// <summary>
    /// The accent hue: the complement hue pulled into the warm copper band (10 - 40 degrees, 25 for the brand complement),
    /// or into a muted cool steel band (197 - 213 degrees) if the main hue itself is copper like (red, orange)
    /// </summary>
    public static double AccentHue(double mainHue, double complementHue)
    {
        var warmMain = (mainHue >= 345) || (mainHue <= 45);
        var center = warmMain ? 205.0 : 25.0;
        return (center + 8 * Math.Sin((complementHue - center) * Math.PI / 180) + 360) % 360;
    }

    /// <summary>
    /// True if a hue is perceived as bright (yellow, green, cyan)
    /// </summary>
    static bool IsLuminousHue(double h) => (h >= 40) && (h <= 190);

    /// <summary>
    /// True if a hue makes a muddy (olive / brown) dark background
    /// </summary>
    public static bool IsMuddyDark(double h) => (h >= 10) && (h <= 90);

    /// <summary>
    /// Create a palette from a theme.
    /// Contrast floors have a little headroom (4.7 for 4.5, 3.1 for 3) so rounding and anti aliasing can't push them below the target.
    /// </summary>
    /// <param name="c">The theme</param>
    /// <param name="kind">The kind of palette</param>
    /// <param name="shade">The lightness step of the background within the kind [0, 2], spreads apps of the same hue and kind apart</param>
    /// <returns>The palette</returns>
    public static BrandPalette Create(HashColors c, BrandPaletteKind kind, int shade = 1)
    {
        var h = c.Hue;
        var a = AccentHue(h, c.ComplementHue);
        //  The tint uses the raw complement hue (more varied than the copper accent), for letters only
        var th = c.ComplementHue;
        shade = Math.Clamp(shade, 0, 2);
        //  The theme saturation [0.2, 0.65] scales the "grey" brand colors [0.75, 1.25]
        var k = 0.75 + Math.Clamp((c.Saturation - 0.2) / 0.45, 0, 1) * 0.5;
        //  Yellow / green accents are perceived as brighter
        var light = Math.Max(0, Math.Cos((a - 100) * Math.PI / 180));
        var accVal = 0.9 - 0.12 * light;
        //  A cool accent (only used for copper like main hues) is a muted steel, not a saturated blue
        var accSatK = (a > 150) && (a < 260) ? 0.6 : 1.0;
        String bg, surface, main, text, secondary, accent, accentLetters, accent2, tint, edge, edgeLarge = null;
        bool bright;
        switch (kind)
        {
            case BrandPaletteKind.Bright:
                bright = true;
                //  A pale tinted mist (not near white), so bright tiles still carry the app hue
                bg = Hsv(h, (0.05 + 0.025 * shade) * k, 0.96);
                surface = Hsv(h, (0.09 + 0.025 * shade) * k, 0.91);
                main = Ensure(h, 0.43 * k, 0.2, bg, 5);           //  Slate 900
                text = Ensure(h, 0.43 * k, 0.2, bg, 7.2);           //  Slate 900
                secondary = Ensure(h, 0.39 * k, 0.5, bg, 4.7);      //  Steel 600
                accent = Ensure(a, accSatK * 0.78, accVal * 0.87, bg, 3.1);   //  Copper 600
                accentLetters = Ensure(a, accSatK * 0.8, accVal * 0.75, bg, 5);
                accent2 = Ensure(h, 0.39 * k, 0.5, bg, 3.1);        //  Steel 600
                tint = Ensure(th, 0.75, 0.45, bg, 5);
                edge = Ensure(h, 0.3 * k, 0.7, bg, 2);              //  Visible on white / mist pages
                edgeLarge = Hsv(h, 0.06, 0.86);                     //  A neutral mist edge for large sizes
                break;
            case BrandPaletteKind.Deep:
                bright = false;
                bg = Hsv(h, 0.68 * k, 0.25 + 0.055 * shade);
                surface = Hsv(h, 0.6 * k, 0.33 + 0.055 * shade);
                main = Ensure(h, 0.14 * k, 0.93, bg, 5);
                text = Ensure(h, 0.03, 0.98, bg, 7.2);
                secondary = Ensure(h, 0.24 * k, 0.82, bg, 4.7);
                accent = Ensure(a, accSatK * 0.66, accVal, bg, 3.1);
                accentLetters = Ensure(a, accSatK * 0.6, accVal, bg, 5);
                accent2 = Ensure(h, 0.3 * k, 0.8, bg, 3.1);
                tint = Ensure(th, 0.3, 0.93, bg, 5);
                edge = Ensure(h, 0.45 * k, 0.55, "#35363a", 3.3);     //  At least 3.3:1 against dark browser chrome
                break;
            case BrandPaletteKind.Vivid:
                if (IsLuminousHue(h))
                {
                    //  A bright, muted tile with dark letters
                    bright = true;
                    bg = Hsv(h, Math.Min(0.42, 0.4 * k), 0.76);
                    surface = Hsv(h, Math.Min(0.48, 0.45 * k), 0.7);
                    main = Ensure(h, 0.7, 0.22, bg, 5);
                    text = Ensure(h, 0.7, 0.18, bg, 7.2);
                    secondary = Ensure(h, 0.7, 0.3, bg, 4.7);
                    accent = Ensure(h, 0.8, 0.4, bg, 3.1);           //  Dark thread
                    accentLetters = Ensure(a, accSatK * 0.85, 0.4, bg, 5);
                    accent2 = Ensure(h, 0.9, 0.3, bg, 3.1);
                    tint = Ensure(th, 0.85, 0.35, bg, 5);
                }
                else
                {
                    //  A muted mid tone tile with light letters
                    bright = false;
                    bg = Hsv(h, Math.Min(0.5, 0.45 * k), 0.5);
                    surface = Hsv(h, Math.Min(0.5, 0.45 * k), 0.45);
                    main = Ensure(h, 0.04, 0.99, bg, 5);
                    text = Ensure(h, 0.03, 1, bg, 4.7);
                    secondary = Ensure(h, 0.12, 0.97, bg, 4.7);
                    accent = Ensure(h, 0.6 * k, 0.26, bg, 3.1);     //  Dark thread
                    accentLetters = Ensure(a, accSatK * 0.35, 1, bg, 5);
                    accent2 = Ensure(h, 0.2 * k, 0.95, bg, 3.1);
                    tint = Ensure(th, 0.25, 1, bg, 5);
                }
                edge = null;
                break;
            default:
                bright = false;
                bg = Hsv(h, 0.43 * k, 0.16 + 0.045 * shade);        //  Slate 900 (the middle shade)
                surface = Hsv(h, 0.42 * k, 0.23 + 0.045 * shade);   //  Between Slate 900 and Slate 700
                main = Ensure(h, 0.2 * k, 0.8, bg, 5);            //  Steel 300
                text = Ensure(h, 0.016, 0.976, bg, 7.2);            //  Mist 50
                secondary = Ensure(h, 0.2 * k, 0.76, bg, 4.7);      //  Steel 300 (a bit darker)
                accent = Ensure(a, accSatK * 0.72, accVal, bg, 3.1);          //  Copper 500
                accentLetters = Ensure(a, accSatK * 0.68, accVal, bg, 5);
                accent2 = Ensure(h, 0.2 * k, 0.8, bg, 3.1);         //  Steel 300
                tint = Ensure(th, 0.3, 0.93, bg, 5);
                edge = Ensure(h, 0.35 * k, 0.5, "#35363a", 3.3);      //  At least 3.3:1 against dark browser chrome
                break;
        }
        return new BrandPalette(kind, bright, bg, surface, main, text, secondary, accent, accentLetters, accent2, tint, edge, edgeLarge);
    }
}
