using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// The language features that can be transformed (lowered) by the converter.
    /// Each feature can be included or excluded explicitly using its name.
    /// </summary>
    public enum JsFeature
    {
        //  ES2022

        /// <summary>Class static initialization blocks: static { ... } (ES2022)</summary>
        ClassStaticBlock,
        /// <summary>Private brand checks: #x in obj (ES2022)</summary>
        PrivateIn,
        /// <summary>Private methods and accessors: #m() { } (ES2022)</summary>
        PrivateMethods,
        /// <summary>Public and private class fields, static and instance: x = 1; static #y = 2; (ES2022)</summary>
        ClassFields,

        //  ES2021

        /// <summary>Logical assignment operators: a ||= b, a &amp;&amp;= b, a ??= b (ES2021)</summary>
        LogicalAssignment,
        /// <summary>Numeric separators: 1_000_000 (ES2021, always removed by the code generator)</summary>
        NumericSeparator,

        //  ES2020

        /// <summary>Optional chaining: a?.b, a?.[b], a?.(b) (ES2020)</summary>
        OptionalChaining,
        /// <summary>Nullish coalescing: a ?? b (ES2020)</summary>
        NullishCoalescing,

        //  ES2019

        /// <summary>Optional catch binding: try { } catch { } (ES2019)</summary>
        OptionalCatchBinding,

        //  ES2018

        /// <summary>Async generator functions and for await (ES2018)</summary>
        AsyncGenerators,
        /// <summary>Object rest and spread properties: { ...a }, const { a, ...rest } = obj (ES2018)</summary>
        ObjectRestSpread,
        /// <summary>The regular expression dotAll flag: /./s (ES2018)</summary>
        RegexDotAll,
        /// <summary>Regular expression named capture groups: /(?&lt;year&gt;\d{4})/ (ES2018)</summary>
        RegexNamedGroups,

        //  ES2017

        /// <summary>Async functions and await (ES2017)</summary>
        AsyncFunctions,

        //  ES2016

        /// <summary>The exponentiation operator: a ** b, a **= b (ES2016)</summary>
        Exponentiation,

        //  ES2015

        /// <summary>Template literals and tagged templates (ES2015)</summary>
        TemplateLiterals,
        /// <summary>The new.target meta property (ES2015)</summary>
        NewTarget,
        /// <summary>Classes (ES2015)</summary>
        Classes,
        /// <summary>Arrow functions (ES2015)</summary>
        ArrowFunctions,
        /// <summary>Default and rest parameters (ES2015)</summary>
        Parameters,
        /// <summary>Destructuring (ES2015)</summary>
        Destructuring,
        /// <summary>Spread in calls, new and array literals (ES2015)</summary>
        Spread,
        /// <summary>Shorthand properties and methods in object literals (ES2015)</summary>
        ShorthandProperties,
        /// <summary>Computed property names in object literals (ES2015)</summary>
        ComputedProperties,
        /// <summary>for ... of loops (ES2015)</summary>
        ForOf,
        /// <summary>Generator functions (ES2015)</summary>
        Generators,
        /// <summary>let, const and block scoped functions (ES2015)</summary>
        BlockScoping,
        /// <summary>The regular expression unicode flag: /\u{1F600}/u (ES2015)</summary>
        RegexUnicode,
        /// <summary>The regular expression sticky flag: /a/y (ES2015)</summary>
        RegexSticky,
    }

    /// <summary>
    /// Browser support data for a feature
    /// </summary>
    sealed class JsFeatureInfo
    {
        public readonly JsFeature Feature;
        /// <summary>The EcmaScript version (year) that introduced the feature (5 is ES5, 2015 is ES2015)</summary>
        public readonly int Es;
        /// <summary>The first version of a browser that supports the feature, by browser name</summary>
        public readonly Dictionary<String, double> Support;

        JsFeatureInfo(JsFeature f, int es, double chrome, double firefox, double safari, double edge)
        {
            Feature = f;
            Es = es;
            Support = new Dictionary<String, double>(StringComparer.Ordinal)
            {
                ["chrome"] = chrome,
                ["firefox"] = firefox,
                ["safari"] = safari,
                ["edge"] = edge,
            };
        }

        /// <summary>
        /// All features in the order they are transformed (newest first)
        /// </summary>
        public static readonly JsFeatureInfo[] All =
        [
            //  Version numbers from @babel/compat-data (plugins.json), edge is legacy edge (12-18), 79+ is chromium
            new(JsFeature.ClassStaticBlock, 2022, 94, 93, 16.4, 94),
            new(JsFeature.PrivateIn, 2022, 91, 90, 15, 91),
            new(JsFeature.PrivateMethods, 2022, 84, 90, 15, 84),
            new(JsFeature.ClassFields, 2022, 74, 90, 14.1, 79),
            new(JsFeature.LogicalAssignment, 2021, 85, 79, 14, 85),
            new(JsFeature.NumericSeparator, 2021, 75, 70, 13, 79),
            new(JsFeature.OptionalChaining, 2020, 91, 74, 13.1, 91),
            new(JsFeature.NullishCoalescing, 2020, 80, 72, 13.1, 80),
            new(JsFeature.OptionalCatchBinding, 2019, 66, 58, 11.1, 79),
            new(JsFeature.AsyncGenerators, 2018, 63, 57, 12, 79),
            new(JsFeature.ObjectRestSpread, 2018, 60, 55, 11.1, 79),
            new(JsFeature.RegexDotAll, 2018, 62, 78, 11.1, 79),
            new(JsFeature.RegexNamedGroups, 2018, 64, 78, 11.1, 79),
            new(JsFeature.AsyncFunctions, 2017, 55, 52, 11, 15),
            new(JsFeature.Exponentiation, 2016, 52, 52, 10.1, 14),
            new(JsFeature.TemplateLiterals, 2015, 41, 34, 13, 13),
            new(JsFeature.NewTarget, 2015, 46, 41, 10, 14),
            new(JsFeature.Classes, 2015, 46, 45, 10, 13),
            new(JsFeature.ArrowFunctions, 2015, 47, 43, 10, 13),
            new(JsFeature.Parameters, 2015, 49, 53, 10, 18),
            new(JsFeature.Destructuring, 2015, 51, 53, 10, 15),
            new(JsFeature.Spread, 2015, 46, 45, 10, 13),
            new(JsFeature.ShorthandProperties, 2015, 43, 33, 9, 12),
            new(JsFeature.ComputedProperties, 2015, 44, 34, 7.1, 12),
            new(JsFeature.ForOf, 2015, 51, 53, 10, 15),
            new(JsFeature.Generators, 2015, 50, 53, 10, 13),
            new(JsFeature.BlockScoping, 2015, 50, 53, 11, 14),
            new(JsFeature.RegexUnicode, 2015, 50, 46, 12, 13),
            new(JsFeature.RegexSticky, 2015, 49, 3, 10, 13),
        ];

        static readonly Dictionary<JsFeature, JsFeatureInfo> ByFeature = All.ToDictionary(x => x.Feature);

        public static JsFeatureInfo Get(JsFeature f) => ByFeature[f];
    }
}
