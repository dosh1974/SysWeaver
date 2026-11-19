using System;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// How helper functions (ex: _defineProperty, _asyncToGenerator) are emitted
    /// </summary>
    public enum JsHelperMode
    {
        /// <summary>The helpers that are used are added to the start of each converted file (or html script block)</summary>
        Inline,
        /// <summary>The helpers are not added, they must be loaded separately (see JsConverter.GetHelpersSource), helpers are referenced by their default names (ex: _defineProperty)</summary>
        External,
    }

    /// <summary>
    /// Which comments to keep
    /// </summary>
    public enum JsComments
    {
        /// <summary>Remove all comments</summary>
        None,
        /// <summary>Keep license comments only (block comments starting with ! or containing @license or @preserve)</summary>
        License,
        /// <summary>Keep all comments (comments inside expressions are moved before the statement)</summary>
        All,
    }

    /// <summary>
    /// How to parse the source
    /// </summary>
    public enum JsSourceType
    {
        /// <summary>Parse as a script, if that fails because of import / export statements, parse as a module</summary>
        Auto,
        /// <summary>A classic script</summary>
        Script,
        /// <summary>An ES module</summary>
        Module,
    }

    /// <summary>
    /// What to do with built-in functions (ex: Array.prototype.includes, Object.entries) that might not exist in the target
    /// </summary>
    public enum JsBuiltIns
    {
        /// <summary>Ignore built-ins</summary>
        Ignore,
        /// <summary>Report (as information) built-ins that are used and might be missing in the target</summary>
        Report,
        /// <summary>Add polyfills for the built-ins that are used and might be missing in the target (only for the built-ins that have a polyfill)</summary>
        Polyfill,
    }

    /// <summary>
    /// The options of the JavaScript converter
    /// </summary>
    public class JsConverterParams
    {
        /// <summary>
        /// The target to convert to, an EcmaScript version and/or browsers with versions, separated by ','.
        /// Ex: "es5", "es2015", "es2017", "chrome 69", "chrome 69, safari 12", "es2015, firefox 60".
        /// Browsers: chrome (also android / webview), edge, firefox, safari (also ios), opera, samsung, node and ie (same as es5).
        /// "esnext" means no transforms.
        /// </summary>
        public String Target = "es2017";

        /// <summary>
        /// Features to always transform (even if the target supports them), separated by ','.
        /// Feature names: ClassStaticBlock, PrivateIn, PrivateMethods, ClassFields, LogicalAssignment, NumericSeparator, OptionalChaining, NullishCoalescing,
        /// OptionalCatchBinding, AsyncGenerators, ObjectRestSpread, RegexDotAll, RegexNamedGroups, AsyncFunctions, Exponentiation, TemplateLiterals,
        /// NewTarget, Classes, ArrowFunctions, Parameters, Destructuring, Spread, ShorthandProperties, ComputedProperties, ForOf, Generators,
        /// BlockScoping, RegexUnicode, RegexSticky.
        /// </summary>
        public String Include;

        /// <summary>
        /// Features to never transform (even if the target doesn't support them), separated by ','.
        /// See Include for the feature names.
        /// </summary>
        public String Exclude;

        /// <summary>
        /// Loose mode: smaller and faster output that doesn't follow the specification in some edge cases:
        /// class fields and methods are assigned (not defined), iterables are assumed to be arrays (or array like) in
        /// spread, destructuring and for-of (no Symbol.iterator is required), template literals use + instead of String.prototype.concat.
        /// </summary>
        public bool Loose;

        /// <summary>
        /// How helper functions are emitted.
        /// </summary>
        public JsHelperMode Helpers = JsHelperMode.Inline;

        /// <summary>
        /// Write minified output (only white space is removed, names are kept).
        /// </summary>
        public bool Minify;

        /// <summary>
        /// The number of spaces to indent with (0 = use a tab), ignored when minifying.
        /// </summary>
        public int Indent = 2;

        /// <summary>
        /// Which comments to keep.
        /// </summary>
        public JsComments Comments = JsComments.All;

        /// <summary>
        /// Only write ASCII characters (non-ASCII characters in strings, templates, regular expressions and identifiers are escaped).
        /// </summary>
        public bool AsciiOnly;

        /// <summary>
        /// How to parse the source.
        /// </summary>
        public JsSourceType SourceType = JsSourceType.Auto;

        /// <summary>
        /// Validate the output by parsing it using the EcmaScript version of the target, a warning is reported if the output contains newer syntax.
        /// </summary>
        public bool Validate = true;

        /// <summary>
        /// Treat warnings as errors (the conversion fails if there are warnings).
        /// </summary>
        public bool WarningsAsErrors;

        /// <summary>
        /// What to do with built-in functions (ex: Array.prototype.includes, Object.entries) that might not exist in the target.
        /// </summary>
        public JsBuiltIns BuiltIns = JsBuiltIns.Report;

        /// <summary>
        /// Html files: also convert inline event handler attributes (ex: onclick="a?.b()").
        /// </summary>
        public bool HtmlAttributes = true;

        /// <summary>
        /// Returns a copy of these options
        /// </summary>
        public JsConverterParams Clone() => (JsConverterParams)MemberwiseClone();
    }
}
