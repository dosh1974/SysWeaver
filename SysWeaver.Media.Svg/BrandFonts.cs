
using CommunityToolkit.HighPerformance;
using System;
using System.IO;

namespace SysWeaver.Media;

/// <summary>
/// The brand fonts (embedded resources, SIL Open Font License 1.1, see Fonts/OFL-*.txt).
/// Saira Semi Condensed SemiBold is the display font (initials, wordmarks), Barlow is the text font (sub titles).
/// </summary>
public static class BrandFonts
{
    static SvgFont Load(String file)
    {
        var type = typeof(BrandFonts);
        var n = type.Namespace + ".data." + file;
        var r = type.Assembly.GetUncompressedResourceData(ref n);
        using (var s = r.AsStream())
            return SvgFont.GetOrCreateCreateFromStream(s, file);
    }

    static readonly Lazy<SvgFont> InternalSaira = new(() => Load("SairaSemiCondensed-SemiBold.ttf"));
    static readonly Lazy<SvgFont> InternalBarlowMedium = new(() => Load("Barlow-Medium.ttf"));
    static readonly Lazy<SvgFont> InternalBarlowRegular = new(() => Load("Barlow-Regular.ttf"));

    /// <summary>
    /// Saira Semi Condensed SemiBold, the display font
    /// </summary>
    public static SvgFont Saira => InternalSaira.Value;

    /// <summary>
    /// Barlow Medium, the text font (emphasis)
    /// </summary>
    public static SvgFont BarlowMedium => InternalBarlowMedium.Value;

    /// <summary>
    /// Barlow Regular, the text font
    /// </summary>
    public static SvgFont BarlowRegular => InternalBarlowRegular.Value;

    /// <summary>
    /// Get the raw font file data of an embedded font (ex: to use the font in SkiaSharp)
    /// </summary>
    /// <param name="file">The font file name, ex: "Barlow-Medium.ttf"</param>
    /// <returns>A stream with the font file data, the caller must dispose it</returns>
    public static Stream OpenFile(String file)
    {
        var type = typeof(BrandFonts);
        return type.Assembly.GetManifestResourceStream(type.Namespace + ".data." + file)
            ?? throw new FileNotFoundException("Embedded font not found", file);
    }
}
