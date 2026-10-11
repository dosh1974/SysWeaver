namespace SysWeaver.Minifier
{
    /// <summary>
    /// The options of <see cref="SvgMinifier.Optimize"/>.
    /// "Shape elements" below are: circle, ellipse, line, path, polygon, polyline and rect.
    /// </summary>
    public class SvgMinifierParams
    {
        /// <summary>
        /// Remove all fill attributes (except fill="none"), so that the fill can be set using css.
        /// The fill of the svg element is removed if any other fill was removed or if it isn't "none".
        /// </summary>
        public bool RemoveFill = false;
        /// <summary>
        /// Remove the width and height attributes of the svg element (and x / y attributes that are zero), also when there is no view box.
        /// Note that width and height are always removed when the svg element has a view box with a positive size.
        /// </summary>
        public bool RemoveSize = false;

        /// <summary>
        /// Remove comments.
        /// </summary>
        public bool RemoveComments = true;
        /// <summary>
        /// Remove insignificant white space (the document is parsed without preserving white space and saved without formatting)
        /// </summary>
        public bool RemoveWhiteSpaces = true;

        /// <summary>
        /// Remove the first rect element if its position and size equals the view box (missing attributes are treated as 0),
        /// repeated while the new first rect matches too, and the enable-background attribute of the svg element
        /// </summary>
        public bool RemoveBackground = false;

        /// <summary>
        /// Remove clip-rule attributes of shape elements if they don't affect the output (requires a view box, checked by rendering the document with System.Drawing, Windows only)
        /// </summary>
        public bool RemoveClipRule = true;

        /// <summary>
        /// Remove fill-rule attributes of shape elements if they don't affect the output (requires a view box, checked by rendering the document with System.Drawing, Windows only)
        /// </summary>
        public bool RemoveFillRule = true;

        /// <summary>
        /// Try to remove every attribute of shape elements, an attribute is removed if it doesn't affect the output
        /// (requires a view box, checked by rendering the document with System.Drawing, Windows only, slow)
        /// </summary>
        public bool TryRemoveAttributes = false;

        /// <summary>
        /// Remove spaces in all attribute values: ", " is replaced with "," and multiple spaces with one space
        /// </summary>
        public bool RemoveAttributeSpaces = true;

        /// <summary>
        /// Remove elements and attributes that are not in the svg, xlink, xmlns or the empty namespace (ex: editor data, xml:space)
        /// </summary>
        public bool RemoveNonSvg = true;

        /// <summary>
        /// Remove the namespace declarations of the svg element, except the default (xmlns) and xlink declarations.
        /// Unused namespace declarations are always removed.
        /// </summary>
        public bool RemoveNamespaces = true;


        /// <summary>
        /// Remove title elements
        /// </summary>
        public bool RemoveTitle = true;

        /// <summary>
        /// Remove metadata elements
        /// </summary>
        public bool RemoveMetadata = true;

        /// <summary>
        /// Remove the xml declaration ("&lt;?xml ...?&gt;"), if false a declaration is written (also if the input had none), it always says encoding="utf-8".
        /// Note that the DOCTYPE is always removed.
        /// </summary>
        public bool RemoveDocType = true;

        /// <summary>
        /// Select the shortest color representation of fill and stroke attributes (not in style attributes), ex: "#FF0000" =&gt; "Red"
        /// </summary>
        public bool ShortenColors = true;

        /// <summary>
        /// Shorten path data: commas, tabs and line breaks are replaced by spaces, repeated L / l / A / a commands are joined, unnecessary spaces are removed
        /// and the arc flags are written without separators (ex: "a5 5 0 0 1 10 10" =&gt; "a5 5 0 0110 10")
        /// </summary>
        public bool ShortenPath = true;

        /// <summary>
        /// Remove the version attribute of svg elements
        /// </summary>
        public bool RemoveVersion = true;

        /// <summary>
        /// Remove spaces before an /&gt;
        /// </summary>
        public bool TrimTagClose = true;

        /// <summary>
        /// Shorten transform attributes: the rotate values are rounded to 4 decimals and a "0 0" center is removed,
        /// scale with two equal values is written with one value, a translate y of 0 is removed, commas are replaced by spaces
        /// </summary>
        public bool TrimTransform = true;

        /// <summary>
        /// Remove the leading zeros of numbers in the coordinate attributes of shape elements, i.e "0.5" =&gt; ".5" and "-0.3" to "-.3" (and "-0" to "0")
        /// </summary>
        public bool TrimLeadingZeros = true;

        /// <summary>
        /// Remove trailing zeros of the numbers in numerical attribute values, i.e "10.500" =&gt; "10.5", "13.0" =&gt; "13", "0.0" =&gt; "0", ".10" =&gt; ".1", ".0" =&gt; "0", "-0.0" =&gt; "0", "-.000" =&gt; "0".
        /// Only applied to attributes with numerical values (coordinates, sizes, path data, points, transforms, viewBox, opacities, stroke and font sizes, filter and animation timing numbers,
        /// animation values of numerical attributes) and to the numerical css properties of style attributes;
        /// other attributes (ex: href, id, class, font-family) are never changed.
        /// </summary>
        public bool TrimTrailingZeros = true;

        /// <summary>
        /// Unwrap g elements without attributes (unless they contain an animation element) and remove empty g elements
        /// </summary>
        public bool RemoveGraphs = true;

        /// <summary>
        /// Remove empty (white space only) text nodes
        /// </summary>
        public bool RemoveTextNodes = true;

        /// <summary>
        /// Remove hidden elements (with a display="none" attribute, the css display is not checked)
        /// </summary>
        public bool RemoveHidden = true;

        /// <summary>
        /// Remove id attributes that are not referenced (by href="#id", or url(#id) in an attribute or a style element), and attributes referencing a missing id (except fill and stroke).
        /// In a style attribute only the css declarations referencing a missing id are removed (the attribute is removed if nothing is left).
        /// Ids that may be used by text the minifier can't rewrite (style elements, ex: "#id" selectors, script elements, "on..." event attributes,
        /// begin / end and aria id reference attributes) are never removed (a conservative word match: any word "a" in a script keeps an id "a").
        /// </summary>
        public bool RemoveIds = true;

        /// <summary>
        /// Remove width and height attributes with the value "100%" of use elements
        /// </summary>
        public bool RemoveUseSize = true;


        /// <summary>
        /// Rename all ids to short names and update the references (href="#id", url(#id) in attributes and style elements).
        /// The ids are ordered by their number of uses (the id attribute + references, ties: first occurrence first), the most used id gets "a",
        /// the next "b" .. "z", then two char names ("aa", "ba" ..) and so on.
        /// The generated names never collide: ids that may be used by text that can't be rewritten (see <see cref="RemoveIds"/>) and references to ids
        /// that are not defined in the document keep their name, and no other id is renamed to that name (or to any word of such a text).
        /// Note: ids that are only referenced from outside the document (ex: "sprite.svg#icon" or css of a html page) are renamed too.
        /// </summary>
        public bool RenameIds = true;


        /// <summary>
        /// If greater than zero (and the svg has a view box), the number of decimals is selected automatically so that the smallest view box dimension
        /// (scaled by the largest scale transform) can be divided into at least this many coordinates. Overrides <see cref="MaxDecimals"/>.
        /// </summary>
        public int MaxDecimalRes = 0;

        /// <summary>
        /// The maximum number of decimals to keep in the numerical attributes of shape elements and transforms, less than zero to disable.
        /// Numbers are rounded half to even (2.25 => 2.2 with 1 decimal); compact numbers ("1.5.5"), exponents ("1e-3") and arc flags in path data are handled,
        /// a space is inserted if two rounded numbers would merge ("1.5.5" with 0 decimals => "2 0").
        /// </summary>
        public int MaxDecimals = -1;


        /// <summary>
        /// Render the document before and after each step and throw an exception if the output changed (Windows only, slow, for debugging).
        /// Defaults to true in DEBUG builds of the library and false in RELEASE builds.
        /// Nothing is written to disk unless <see cref="ErrorDir"/> is set.
        /// </summary>
#if DEBUG
        public bool BitmapValidation = true;
#else//DEBUG
        public bool BitmapValidation;
#endif//DEBUG

        /// <summary>
        /// A folder where a failed bitmap validation saves the reference image, the failed image and the source svg
        /// (named "SvgMinifier_&lt;source hash&gt;_&lt;step&gt;_&lt;random&gt;_Ref.png", "_Failed.png" and "_Source.svg", the folder is created if needed,
        /// the paths are in the exception message). If null (the default) or empty, nothing is written.
        /// </summary>
        public System.String ErrorDir;

        /// <summary>
        /// A key (letters, digits and '_', safe in file names) that identifies the output: <see cref="SvgMinifier.Version"/> followed by the options that change the output.
        /// Used by SvgOpt and SvgTransformer to name (or validate) cached outputs, so that a new minifier version or other options invalidate them.
        /// ErrorDir and BitmapValidation don't change the output, they are not part of the key.
        /// </summary>
        public System.String CacheKey => System.String.Concat(
            SvgMinifier.Version,
            TryRemoveAttributes ? 'R' : '_',
            TrimTrailingZeros ? 'Z' : '_',
            TrimLeadingZeros ? 'Z' : '_',
            TrimTransform ? 'T' : '_',
            TrimTagClose ? 'T' : '_',
            RemoveVersion ? 'V' : '_',
            ShortenPath ? 'P' : '_',
            ShortenColors ? 'S' : '_',
            RemoveDocType ? 'D' : '_',
            RemoveMetadata ? 'M' : '_',
            RemoveTitle ? 'T' : '_',
            RemoveAttributeSpaces ? 'A' : '_',
            RemoveClipRule ? 'C' : '_',
            RemoveFillRule ? 'F' : '_',
            RemoveBackground ? 'B' : '_',
            RemoveWhiteSpaces ? 'W' : '_',
            RemoveComments ? 'C' : '_',
            RemoveSize ? 'S' : '_',
            RemoveFill ? 'F' : '_',
            RemoveGraphs ? 'G' : '_',
            RemoveTextNodes ? 'T' : '_',
            RemoveHidden ? 'H' : '_',
            RemoveIds ? 'I' : '_',
            RemoveUseSize ? 'U' : '_',
            RenameIds ? 'R' : '_',
            RemoveNonSvg ? 'N' : '_',
            RemoveNamespaces ? 'N' : '_',
            MaxDecimalRes < 0 ? "0" : MaxDecimalRes.ToString(System.Globalization.CultureInfo.InvariantCulture), '_',
            MaxDecimals < 0 ? "" : MaxDecimals.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

    }


}
