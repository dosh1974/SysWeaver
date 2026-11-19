# SysWeaver.Converter.Js

[⬆ Converter](../README.md)

> Converts modern JavaScript to older JavaScript versions (ES2017, ES2015, ES5 or a list of browsers) so that pages can be served to old and embedded browsers — a C# alternative to Babel's `preset-env`.

| | |
|---|---|
| **Layer** | Tooling |
| **Kind** | Library |

## Purpose

Web pages written with current JavaScript (class fields, `?.`, `??`, private members, async functions, ...) don't run on old browsers, typically the embedded browsers / web views of devices that can't be updated. This library lowers the syntax that the target doesn't support to equivalent older syntax, adds the runtime helpers that the converted code needs, and reports (or polyfills) built-in functions that might be missing. It replaces converting files manually with the Babel REPL.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Src["modern .js / .html"] --> Parse["Acornima parser"]
  Parse --> Ast["mutable syntax tree"]
  Ast --> Passes["transform passes<br/>newest feature first"]
  Passes --> Helpers["helpers / polyfills"]
  Helpers --> Writer["code writer<br/>pretty or minified"]
  Writer --> Validate["validation<br/>re-parse at target version"]
  Validate --> Out["ES2017 / ES2015 / ES5 code"]
```

The [JsConvert](https://github.com/dosh1974/SysWeaver.Tools/blob/main/JsConvert/README.md) tool in SysWeaver.Tools is the command line front end (files in, folder out). The library has no SysWeaver dependencies, so it can be used anywhere (ex: a web server transformer that converts scripts per user agent).

## Key features

- **Targets**: an EcmaScript version (`es5`, `es2015` ... `es2022`), browsers with versions (`chrome 69`, `safari 12`, `firefox 60`, `edge 18`, `ios 12`, `samsung 10.1`, `opera 56`, `node 12`, `ie 11`) or both (`es2015, firefox 60`); a feature is transformed when the EcmaScript version doesn't include it or a browser doesn't support it (the support data is from `@babel/compat-data`)
- **Every syntax feature from ES2015 to ES2022** can be lowered (see the table below), with per-feature `Include` / `Exclude` overrides
- **Babel compatible output**: the helpers (`_defineProperty`, `_createClass`, `_asyncToGenerator`, `_regeneratorRuntime` ...) work like Babel's, so the output looks like what the Babel REPL produces
- **Correctness first**: evaluation order, `this` / `arguments` / `super` / `new.target` capture, per-iteration `let` bindings in loops with closures, iterator closing, getters and setters, symbols and private brand checks follow the specification; the test suite runs every case with the original and the converted code and compares the results
- **Loose mode**: smaller and faster output where the specification is relaxed (assignments instead of property definitions, arrays assumed for iterables)
- **Helpers** are added once at the start of the output (renamed if the source uses the same names), or left out to be loaded from a shared file (`JsHelperMode.External`, `GetHelpersSource`)
- **Built-ins**: methods and objects that might be missing in the target (`Promise`, `Object.entries`, `"".padStart`, `[].includes`, `.at()`, ...) are reported, and with `JsBuiltIns.Polyfill` small guarded, non-enumerable ES5 polyfills are added (including a Promise/A+ implementation and WeakMap / WeakSet for private fields)
- **Html**: `JsHtmlConverter` converts inline `<script>` elements (classic and modules) and event handler attributes (`onclick="..."`), skips other script types (shaders, templates, JSON) and leaves scripts that don't need transforms untouched
- **Sites**: `JsSite.Collect` finds all files used by html pages (scripts, modules, style sheets and assets, following html elements, css `@import` / `url()`, script imports, workers and literal script paths) and `JsSite.CloneAsync` writes the site to a folder with all pages and scripts converted (dynamically created dependencies with computed urls, iframes and links to other pages are not followed)
- **Diagnostics** with line and column: syntax errors, constructs that can't be converted for the target (BigInt, lookbehind, `\p{...}`, `import.meta` ...), assignments to constants, and a validation warning if the output still contains syntax the target doesn't support (the output is re-parsed with the target's EcmaScript version)
- **Output options**: pretty printed (indent size or tabs) or minified (white space only, names are kept), comments kept (all / license only / none), ASCII only, html safe (`</script` is escaped in scripts embedded in html)

## Supported transforms

| Version | Feature (`JsFeature`) | Output |
|---|---|---|
| ES2022 | `ClassFields`, `PrivateMethods`, `PrivateIn`, `ClassStaticBlock` | Fields are initialized in the constructor (after `super()`) or after the class; private fields use WeakMaps, private methods / accessors a WeakSet brand; class expressions are wrapped in an arrow function |
| ES2021 | `LogicalAssignment`, `NumericSeparator` | `a \|\| (a = b)` with the target evaluated once; separators are removed by the writer |
| ES2020 | `OptionalChaining`, `NullishCoalescing` | Conditionals with temporaries (`this` is kept for optional calls), `delete a?.b` |
| ES2019 | `OptionalCatchBinding` | A named catch parameter |
| ES2018 | `ObjectRestSpread`, `AsyncGenerators` (+ `for await`), `RegexDotAll`, `RegexNamedGroups` | `_objectSpread2` / `_objectWithoutProperties`, `_wrapAsyncGenerator`, `[\s\S]`, `_wrapRegExp` (`.groups`, `$<name>`) |
| ES2017 | `AsyncFunctions` | Generators with `_asyncToGenerator` (super, arguments and new.target are captured) |
| ES2016 | `Exponentiation` | `Math.pow` |
| ES2015 | `Classes`, `ArrowFunctions`, `BlockScoping`, `Destructuring`, `Parameters`, `Spread`, `ForOf`, `Generators`, `TemplateLiterals`, `ShorthandProperties`, `ComputedProperties`, `NewTarget`, `RegexUnicode`, `RegexSticky` | Babel compatible: `_createClass` / `_inherits` / `_callSuper`, `_loop` functions for closures capturing loop bindings, `_slicedToArray`, `arguments`, `_toConsumableArray` / `_construct`, `_createForOfIteratorHelper`, a regenerator state machine, `"".concat()`, `_defineProperty`, surrogate pair expansion for `u` regular expressions |

Binary / octal literals, `\u{...}` escapes and U+2028 / U+2029 in strings are always written in a form that every version accepts.

## Key components

- [`JsConverter`](JsConverter.cs) — `Convert(source, params)`, `GetHelpersSource`, `GetPolyfillsSource`, `GetFeatures`
- [`JsConverterParams`](JsConverterParams.cs) — the options (also the command line options of JsConvert)
- [`JsHtmlConverter`](Html/JsHtmlConverter.cs) — converts the scripts of an html document
- [`JsSite`](Html/JsSite.cs) — collects the files of html pages and clones a site with converted scripts
- [`JsTarget`](Targets/JsTarget.cs), [`JsFeature`](Targets/JsFeature.cs) — targets and the browser support data
- [`Ast/`](Ast) — the mutable syntax tree, the Acornima importer and the rewriter base classes
- [`Transforms/`](Transforms) — one pass per feature group, run newest first ([`Pipeline`](Transforms/Pipeline.cs))
- [`Output/JsWriter`](Output/JsWriter.cs) — the code generator (precedence, statement starts, minification)
- [`Analysis/Scope`](Analysis/Scope.cs) — scope analysis (bindings and references, used for safe renaming)
- [`Helpers/`](Helpers) — the ES5 runtime helpers and the generator runtime

## Using it

```csharp
var r = JsConverter.Convert(File.ReadAllText("common.js"), new JsConverterParams
{
    Target = "chrome 69",               // or "es5", "es2017", "safari 12, firefox 60" ...
    BuiltIns = JsBuiltIns.Polyfill,     // add polyfills for missing built-ins
});
foreach (var d in r.Diagnostics)
    Console.WriteLine(d);               // Warning (12:5): ...
if (r.Success)
    File.WriteAllText("common.chrome69.js", r.Code);

// Html pages (inline scripts and event handlers)
var h = JsHtmlConverter.Convert(File.ReadAllText("page.html"), new JsConverterParams { Target = "es5" });

// Shared helpers instead of helpers in every file
var p = new JsConverterParams { Target = "es5", Helpers = JsHelperMode.External };
var a = JsConverter.Convert(srcA, p);
var b = JsConverter.Convert(srcB, p);
File.WriteAllText("helpers.js", JsConverter.GetHelpersSource(a.Helpers.Concat(b.Helpers)));
```

## Limitations and considerations

- **Runtime requirements**: the output only uses ES5 syntax, but some transforms need built-ins at runtime: `Promise` (async functions), `WeakMap` / `WeakSet` (private members), `Symbol.iterator` for non-array iterables (spread, destructuring and for-of of Maps, Sets and generators; arrays, strings and `arguments` work without it), `Reflect.construct` is used when available (extending built-ins like `Error` and `Array` relies on `__proto__` / `Object.setPrototypeOf` otherwise). Use `BuiltIns = Polyfill` to add the polyfills that exist (Promise, WeakMap, WeakSet and many methods)
- **Can't be converted** (reported as warnings): BigInt, `import.meta`, dynamic `import()`, top level `await`, regular expression lookbehind, unicode property escapes (`\p{...}`), the `d` and `v` flags and modifiers; the sticky flag `y` is moved into `new RegExp(...)` so old parsers accept the file, the target must still support it. ES modules (`import` / `export`) are kept (a warning is reported for targets without module support). Decorators, auto accessors and `using` declarations are errors
- **Not emulated** (like Babel): the temporal dead zone of `let` / `const` / classes, the exact `name` and `length` of some generated functions, `document.all`-like objects in `?.` / `??`, case insensitive matching of non-ASCII letters with the `u` flag, empty-match iteration of `u` regular expressions by code point
- **Loose mode** deliberately deviates: class fields and methods are assigned (setters run, methods are enumerable), spread / destructuring / for-of assume arrays or array-likes, object spread copies with `Object.assign` semantics, template literals use `+`, default parameters change `length`
- **Comments** are attached to statements; comments inside expressions are moved before the statement
- **Built-in detection** of instance methods is by name (`x.includes(...)` reports both `Array.prototype.includes` and `String.prototype.includes`), polyfills are added for all matching built-ins
- Html parsing is a lightweight tag scanner (comments, raw text elements and quoted attributes are handled; it is not a full html5 parser)
- Performance: around 3 MB of source per second (single thread, conversion to ES5 with validation)

## Testing

The unit tests are in [SysWeaver.Tests](https://github.com/dosh1974/SysWeaver.Tests) (`UnitTests/ConverterJs`): every JavaScript case in `ConverterJs/Cases` is executed with the Jint engine before and after conversion to 10 targets (es2021 ... es5, chrome 69, safari 12, loose and minified) and the outputs are compared; all JavaScript files of this repository are converted without errors and re-written as a fixpoint; plus tests of the writer, targets, options, html conversion, helpers and polyfills.

## Relationships

- **Project:** [`SysWeaver.Converter.Js.csproj`](SysWeaver.Converter.Js.csproj)
- **Builds on:** no SysWeaver projects
- **Used by:** [JsConvert](https://github.com/dosh1974/SysWeaver.Tools/blob/main/JsConvert/README.md), [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) (-JsTarget)
- **Third-party:** [Acornima](https://github.com/adams85/acornima) (BSD-3-Clause, JavaScript parser)
