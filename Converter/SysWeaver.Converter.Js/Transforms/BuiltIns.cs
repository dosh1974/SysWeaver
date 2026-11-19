using System;
using System.Collections.Generic;
using System.Linq;
using Acornima;
using SysWeaver.Converter.Js.Analysis;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Detects built-in objects and methods (ex: Promise, Object.entries, "".padStart) that might be missing in the target,
    /// reports them and optionally adds polyfills (ES5 code, guarded, non-enumerable).
    /// Instance methods are detected by name (the type of the object is unknown), ex: x.includes(...) matches both Array and String includes.
    /// </summary>
    static class BuiltIns
    {
        enum Kind
        {
            Global,
            Static,
            Proto,
        }

        sealed class Info
        {
            public String Name;
            public Kind Kind;
            /// <summary>For Static: "Object.entries", for Proto: the property name (ex: "includes"), for Global: the global name</summary>
            public String Key;
            public int Es;
            public double Chrome;
            public double Firefox;
            public double Safari;
            /// <summary>The polyfill name, null if there is no polyfill</summary>
            public String Polyfill;
        }

        static Info G(String name, int es, double c, double f, double s, String pf = null) => new Info { Name = name, Kind = Kind.Global, Key = name, Es = es, Chrome = c, Firefox = f, Safari = s, Polyfill = pf };
        static Info S(String name, int es, double c, double f, double s, String pf = null) => new Info { Name = name, Kind = Kind.Static, Key = name, Es = es, Chrome = c, Firefox = f, Safari = s, Polyfill = pf };
        static Info P(String prop, String name, int es, double c, double f, double s, String pf = null) => new Info { Name = name, Kind = Kind.Proto, Key = prop, Es = es, Chrome = c, Firefox = f, Safari = s, Polyfill = pf };

        static readonly Info[] Table =
        [
            //  Globals
            G("Promise", 2015, 32, 29, 8, "Promise"),
            G("Map", 2015, 38, 13, 8),
            G("Set", 2015, 38, 13, 8),
            G("WeakMap", 2015, 36, 6, 8, "WeakMap"),
            G("WeakSet", 2015, 36, 34, 9, "WeakSet"),
            G("Symbol", 2015, 38, 36, 9),
            G("Proxy", 2015, 49, 18, 10),
            G("Reflect", 2015, 49, 42, 10),
            G("globalThis", 2020, 71, 65, 12.1, "globalThis"),
            G("fetch", 2015, 42, 39, 10.1),
            G("structuredClone", 2022, 98, 94, 15.4),
            G("queueMicrotask", 2020, 71, 69, 12.1, "queueMicrotask"),
            G("BigInt", 2020, 67, 68, 14),
            G("AggregateError", 2021, 85, 79, 14),
            //  Static methods
            S("Object.assign", 2015, 45, 34, 9, "Object.assign"),
            S("Object.entries", 2017, 54, 47, 10.1, "Object.entries"),
            S("Object.values", 2017, 54, 47, 10.1, "Object.values"),
            S("Object.fromEntries", 2019, 73, 63, 12.1, "Object.fromEntries"),
            S("Object.getOwnPropertyDescriptors", 2017, 54, 50, 10, "Object.getOwnPropertyDescriptors"),
            S("Object.hasOwn", 2022, 93, 92, 15.4, "Object.hasOwn"),
            S("Object.groupBy", 2024, 117, 119, 17.4, "Object.groupBy"),
            S("Object.is", 2015, 30, 22, 9, "Object.is"),
            S("Object.setPrototypeOf", 2015, 34, 31, 9),
            S("Array.from", 2015, 45, 32, 9, "Array.from"),
            S("Array.of", 2015, 45, 25, 9, "Array.of"),
            S("Number.isInteger", 2015, 34, 16, 9, "Number.isInteger"),
            S("Number.isSafeInteger", 2015, 34, 32, 9, "Number.isSafeInteger"),
            S("Number.isNaN", 2015, 25, 15, 9, "Number.isNaN"),
            S("Number.isFinite", 2015, 19, 16, 9, "Number.isFinite"),
            S("Number.parseFloat", 2015, 34, 25, 9, "Number.parseFloat"),
            S("Number.parseInt", 2015, 34, 25, 9, "Number.parseInt"),
            S("Number.EPSILON", 2015, 34, 25, 9, "Number.EPSILON"),
            S("Number.MAX_SAFE_INTEGER", 2015, 34, 31, 9, "Number.MAX_SAFE_INTEGER"),
            S("Number.MIN_SAFE_INTEGER", 2015, 34, 31, 9, "Number.MIN_SAFE_INTEGER"),
            S("Math.trunc", 2015, 38, 25, 8, "Math.trunc"),
            S("Math.sign", 2015, 38, 25, 9, "Math.sign"),
            S("Math.log2", 2015, 38, 25, 8, "Math.log2"),
            S("Math.log10", 2015, 38, 25, 8, "Math.log10"),
            S("Math.cbrt", 2015, 38, 25, 8, "Math.cbrt"),
            S("Math.hypot", 2015, 38, 27, 8, "Math.hypot"),
            S("String.fromCodePoint", 2015, 41, 29, 9),
            S("String.raw", 2015, 41, 34, 9),
            S("Promise.allSettled", 2020, 76, 71, 13, "Promise.allSettled"),
            S("Promise.any", 2021, 85, 79, 14),
            S("Promise.withResolvers", 2024, 119, 121, 17.4, "Promise.withResolvers"),
            //  Prototype methods (by name)
            P("includes", "Array.prototype.includes / String.prototype.includes", 2016, 47, 43, 9, "includes"),
            P("find", "Array.prototype.find", 2015, 45, 25, 8, "Array.prototype.find"),
            P("findIndex", "Array.prototype.findIndex", 2015, 45, 25, 8, "Array.prototype.findIndex"),
            P("fill", "Array.prototype.fill", 2015, 45, 31, 8, "Array.prototype.fill"),
            P("flat", "Array.prototype.flat", 2019, 69, 62, 12, "Array.prototype.flat"),
            P("flatMap", "Array.prototype.flatMap", 2019, 69, 62, 12, "Array.prototype.flatMap"),
            P("at", "Array.prototype.at / String.prototype.at", 2022, 92, 90, 15.4, "at"),
            P("findLast", "Array.prototype.findLast", 2023, 97, 104, 15.4, "Array.prototype.findLast"),
            P("findLastIndex", "Array.prototype.findLastIndex", 2023, 97, 104, 15.4, "Array.prototype.findLastIndex"),
            P("toSorted", "Array.prototype.toSorted", 2023, 110, 115, 16, "Array.prototype.toSorted"),
            P("toReversed", "Array.prototype.toReversed", 2023, 110, 115, 16, "Array.prototype.toReversed"),
            P("startsWith", "String.prototype.startsWith", 2015, 41, 17, 9, "String.prototype.startsWith"),
            P("endsWith", "String.prototype.endsWith", 2015, 41, 17, 9, "String.prototype.endsWith"),
            P("repeat", "String.prototype.repeat", 2015, 41, 24, 9, "String.prototype.repeat"),
            P("padStart", "String.prototype.padStart", 2017, 57, 48, 10, "String.prototype.padStart"),
            P("padEnd", "String.prototype.padEnd", 2017, 57, 48, 10, "String.prototype.padEnd"),
            P("trimStart", "String.prototype.trimStart", 2019, 66, 61, 12, "String.prototype.trimStart"),
            P("trimEnd", "String.prototype.trimEnd", 2019, 66, 61, 12, "String.prototype.trimEnd"),
            P("replaceAll", "String.prototype.replaceAll", 2021, 85, 77, 13.1, "String.prototype.replaceAll"),
            P("matchAll", "String.prototype.matchAll", 2020, 73, 67, 13),
            P("codePointAt", "String.prototype.codePointAt", 2015, 41, 29, 9),
            P("normalize", "String.prototype.normalize", 2015, 34, 31, 10),
            P("finally", "Promise.prototype.finally", 2018, 63, 58, 11.1, "Promise.prototype.finally"),
        ];

        static bool Supported(Info i, JsTarget t)
        {
            if (t.EsLimit < i.Es)
                return false;
            foreach (var b in t.Browsers)
            {
                var min = b.Key switch
                {
                    "chrome" => i.Chrome,
                    "firefox" => i.Firefox,
                    "safari" => i.Safari,
                    "edge" => i.Es <= 2016 ? 14 : 79,
                    _ => 0,
                };
                if (b.Value < min)
                    return false;
            }
            return true;
        }

        public static void Run(ProgramNode p, TransformContext ctx)
        {
            var t = ctx.Target;
            var missing = Table.Where(x => !Supported(x, t)).ToList();
            if (missing.Count == 0)
                return;
            var globals = missing.Where(x => x.Kind == Kind.Global).ToDictionary(x => x.Key, StringComparer.Ordinal);
            var statics = missing.Where(x => x.Kind == Kind.Static).ToDictionary(x => x.Key, StringComparer.Ordinal);
            var protos = missing.Where(x => x.Kind == Kind.Proto).ToDictionary(x => x.Key, StringComparer.Ordinal);
            //  Names declared by the program shadow the globals
            var sa = ScopeAnalyzer.Analyze(p);
            var unresolved = new HashSet<Identifier>(sa.References.Where(r => r.Binding == null).Select(r => r.Id));
            var found = new Dictionary<String, (Info Info, int Pos)>(StringComparer.Ordinal);
            void Found(Info i, Node at)
            {
                if (!found.ContainsKey(i.Name))
                    found[i.Name] = (i, at.Start);
            }
            Walker.Walk(p, n =>
            {
                switch (n)
                {
                    case Identifier id when unresolved.Contains(id) && globals.TryGetValue(id.Name, out var gi):
                        Found(gi, id);
                        break;
                    case MemberExpr { Computed: false, Property: Identifier prop } m:
                        {
                            //  window.Promise, globalThis.Map
                            if (m.Object is Identifier { Name: "window" or "globalThis" or "self" } wo && unresolved.Contains(wo) && globals.TryGetValue(prop.Name, out var wgi))
                                Found(wgi, m);
                            //  Object.entries
                            if (m.Object is Identifier so && unresolved.Contains(so) && statics.TryGetValue(so.Name + "." + prop.Name, out var si))
                                Found(si, m);
                            //  x.includes
                            else if (protos.TryGetValue(prop.Name, out var pi))
                                Found(pi, m);
                            break;
                        }
                }
                return true;
            });
            bool polyfill = ctx.Params.BuiltIns == JsBuiltIns.Polyfill;
            var es = t.ToString();
            foreach (var f in found.Values.OrderBy(x => x.Pos))
            {
                var i = f.Info;
                if (polyfill && i.Polyfill != null)
                {
                    if (!ctx.Polyfills.Contains(i.Polyfill))
                        ctx.Polyfills.Add(i.Polyfill);
                    ctx.Diag.Info(f.Pos, "Added a polyfill for " + i.Name + " (ES" + i.Es + ")");
                    continue;
                }
                var how = i.Polyfill != null ? " (use the Polyfill option to add a polyfill)" : " (no polyfill available, the target must provide it)";
                ctx.Diag.Info(f.Pos, i.Name + " (ES" + i.Es + ") might not be available in " + es + how);
            }
            if (ctx.Polyfills.Count > 0)
            {
                var all = Ordered(ctx.Polyfills);
                ctx.Polyfills.Clear();
                ctx.Polyfills.AddRange(all);
            }
        }

        /// <summary>
        /// Order polyfills (dependencies first)
        /// </summary>
        public static List<String> Ordered(IEnumerable<String> names)
        {
            var r = new List<String>();
            void Add(String n)
            {
                if (r.Contains(n))
                    return;
                if (PolyfillDeps.TryGetValue(n, out var deps))
                    foreach (var d in deps)
                        Add(d);
                r.Add(n);
            }
            foreach (var n in names)
                Add(n);
            return r;
        }

        static readonly Dictionary<String, String[]> PolyfillDeps = new Dictionary<String, String[]>(StringComparer.Ordinal)
        {
            ["Promise.allSettled"] = ["Promise"],
            ["Promise.prototype.finally"] = ["Promise"],
            ["Promise.withResolvers"] = ["Promise"],
            ["queueMicrotask"] = ["Promise"],
            ["WeakSet"] = ["WeakMap"],
            ["Array.prototype.flatMap"] = ["Array.prototype.flat"],
            ["Object.groupBy"] = ["Array.from"],
        };

        /// <summary>
        /// Parse the polyfills into statements
        /// </summary>
        public static List<Stmt> BuildPolyfills(IEnumerable<String> names)
        {
            var r = new List<Stmt>();
            var src = String.Join("\n", Ordered(names).Select(GetPolyfill));
            if (src.Length == 0)
                return r;
            var parser = new Parser(new ParserOptions { EcmaVersion = EcmaVersion.ES5 });
            var prog = AcornimaImporter.Import(parser.ParseScript(src), new Diagnostics(src));
            r.AddRange(prog.Body);
            return r;
        }

        /// <summary>
        /// The names of all polyfills
        /// </summary>
        public static IEnumerable<String> AllPolyfills => PolyfillSources.Keys;

        /// <summary>
        /// Get the (ES5) source of a polyfill
        /// </summary>
        public static String GetPolyfill(String name)
        {
            if (PolyfillSources.TryGetValue(name, out var s))
                return s;
            throw new ArgumentException("Unknown polyfill \"" + name + "\"", nameof(name));
        }

        const String Def = "(function (o, n, f) { if (!(n in o)) Object.defineProperty(o, n, { value: f, writable: true, configurable: true }); })";

        static String Method(String target, String name, String fn) => Def + "(" + target + ", \"" + name + "\", " + fn.Trim() + ");\n";

        const String Global = "(typeof self !== \"undefined\" ? self : typeof window !== \"undefined\" ? window : this)";

        static readonly Dictionary<String, String> PolyfillSources = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            ["globalThis"] = "(function (g) { if (typeof globalThis === \"undefined\") g.globalThis = g; })" + Global + ";\n",
            ["queueMicrotask"] = "(function (g) { if (typeof g.queueMicrotask !== \"function\") g.queueMicrotask = function (f) { Promise.resolve().then(f); }; })" + Global + ";\n",
            ["Promise"] = """
(function (g) {
  if (typeof g.Promise === "function") return;
  var defer = typeof setImmediate === "function" ? setImmediate : function (f) { setTimeout(f, 0); };
  function P(executor) {
    if (!(this instanceof P)) throw new TypeError("Promise must be called with new");
    if (typeof executor !== "function") throw new TypeError("Promise resolver is not a function");
    this._s = 0; this._v = void 0; this._h = [];
    var self = this, done = false;
    function res(v) { if (!done) { done = true; resolve(self, v); } }
    function rej(e) { if (!done) { done = true; settle(self, 2, e); } }
    try { executor(res, rej); } catch (e) { rej(e); }
  }
  function settle(p, s, v) {
    if (p._s) return;
    p._s = s; p._v = v;
    var h = p._h; p._h = null;
    for (var i = 0; i < h.length; i++) handle(p, h[i]);
  }
  function resolve(p, v) {
    if (v === p) return settle(p, 2, new TypeError("A promise cannot be resolved with itself"));
    if (v && (typeof v === "object" || typeof v === "function")) {
      var then;
      try { then = v.then; } catch (e) { return settle(p, 2, e); }
      if (typeof then === "function") {
        var called = false;
        try {
          then.call(v, function (x) { if (!called) { called = true; resolve(p, x); } }, function (e) { if (!called) { called = true; settle(p, 2, e); } });
        } catch (e) { if (!called) { called = true; settle(p, 2, e); } }
        return;
      }
    }
    settle(p, 1, v);
  }
  function handle(p, h) {
    if (!p._s) { p._h.push(h); return; }
    defer(function () {
      var cb = p._s === 1 ? h.f : h.r;
      if (typeof cb !== "function") { (p._s === 1 ? h.res : h.rej)(p._v); return; }
      var x;
      try { x = cb(p._v); } catch (e) { h.rej(e); return; }
      h.res(x);
    });
  }
  P.prototype.then = function (f, r) {
    var self = this;
    return new P(function (res, rej) { handle(self, { f: f, r: r, res: res, rej: rej }); });
  };
  P.prototype["catch"] = function (r) { return this.then(null, r); };
  P.resolve = function (v) { return v instanceof P ? v : new P(function (res) { res(v); }); };
  P.reject = function (e) { return new P(function (res, rej) { rej(e); }); };
  P.all = function (list) {
    return new P(function (res, rej) {
      var a = Array.prototype.slice.call(list), n = a.length, out = new Array(n);
      if (!n) return res(out);
      for (var i = 0; i < a.length; i++) (function (i) {
        P.resolve(a[i]).then(function (v) { out[i] = v; if (--n === 0) res(out); }, rej);
      })(i);
    });
  };
  P.race = function (list) {
    return new P(function (res, rej) {
      var a = Array.prototype.slice.call(list);
      for (var i = 0; i < a.length; i++) P.resolve(a[i]).then(res, rej);
    });
  };
  g.Promise = P;
})
""".Trim() + Global + ";\n",
            ["Promise.allSettled"] = """
if (!Promise.allSettled) Promise.allSettled = function (list) {
  return Promise.all(Array.prototype.slice.call(list).map(function (p) {
    return Promise.resolve(p).then(function (v) { return { status: "fulfilled", value: v }; }, function (e) { return { status: "rejected", reason: e }; });
  }));
};
""",
            ["Promise.withResolvers"] = """
if (!Promise.withResolvers) Promise.withResolvers = function () {
  var r = {};
  r.promise = new this(function (a, b) { r.resolve = a; r.reject = b; });
  return r;
};
""",
            ["Promise.prototype.finally"] = Method("Promise.prototype", "finally", """
function (f) {
  var C = this.constructor || Promise;
  if (typeof f !== "function") return this.then(f, f);
  return this.then(function (v) { return C.resolve(f()).then(function () { return v; }); }, function (e) { return C.resolve(f()).then(function () { throw e; }); });
}
"""),
            ["WeakMap"] = """
(function (g) {
  if (typeof g.WeakMap === "function") return;
  var id = 0, key = "__weakmap_" + Math.random().toString(36).slice(2);
  function check(o) { if (Object(o) !== o) throw new TypeError("Invalid value used as weak map key"); return o; }
  function WeakMap() { this._k = key + (id++); }
  WeakMap.prototype.get = function (o) { var e = Object(o) === o ? o[this._k] : void 0; return e && e[0] === o ? e[1] : void 0; };
  WeakMap.prototype.has = function (o) { var e = Object(o) === o ? o[this._k] : void 0; return !!e && e[0] === o; };
  WeakMap.prototype.set = function (o, v) {
    check(o);
    var e = o[this._k];
    if (e && e[0] === o) e[1] = v;
    else Object.defineProperty(o, this._k, { value: [o, v], writable: true, configurable: true });
    return this;
  };
  WeakMap.prototype["delete"] = function (o) { if (!this.has(o)) return false; o[this._k][0] = void 0; return true; };
  g.WeakMap = WeakMap;
})
""".Trim() + Global + ";\n",
            ["WeakSet"] = """
(function (g) {
  if (typeof g.WeakSet === "function") return;
  function WeakSet() { this._m = new WeakMap(); }
  WeakSet.prototype.add = function (o) { this._m.set(o, true); return this; };
  WeakSet.prototype.has = function (o) { return this._m.has(o); };
  WeakSet.prototype["delete"] = function (o) { return this._m["delete"](o); };
  g.WeakSet = WeakSet;
})
""".Trim() + Global + ";\n",
            ["Object.assign"] = Method("Object", "assign", """
function (t) {
  if (t == null) throw new TypeError("Cannot convert undefined or null to object");
  var to = Object(t);
  for (var i = 1; i < arguments.length; i++) {
    var s = arguments[i];
    if (s != null) for (var k in s) if (Object.prototype.hasOwnProperty.call(s, k)) to[k] = s[k];
  }
  return to;
}
"""),
            ["Object.entries"] = Method("Object", "entries", "function (o) { var k = Object.keys(o), r = []; for (var i = 0; i < k.length; i++) r.push([k[i], o[k[i]]]); return r; }"),
            ["Object.values"] = Method("Object", "values", "function (o) { var k = Object.keys(o), r = []; for (var i = 0; i < k.length; i++) r.push(o[k[i]]); return r; }"),
            ["Object.fromEntries"] = Method("Object", "fromEntries", """
function (it) {
  var o = {};
  if (Array.isArray(it)) { for (var i = 0; i < it.length; i++) o[it[i][0]] = it[i][1]; return o; }
  if (typeof it.forEach === "function" && typeof Map === "function" && it instanceof Map) { it.forEach(function (v, k) { o[k] = v; }); return o; }
  var a = Array.from(it);
  for (var j = 0; j < a.length; j++) o[a[j][0]] = a[j][1];
  return o;
}
"""),
            ["Object.getOwnPropertyDescriptors"] = Method("Object", "getOwnPropertyDescriptors", "function (o) { var r = {}, k = Object.getOwnPropertyNames(o); for (var i = 0; i < k.length; i++) r[k[i]] = Object.getOwnPropertyDescriptor(o, k[i]); return r; }"),
            ["Object.hasOwn"] = Method("Object", "hasOwn", "function (o, k) { if (o == null) throw new TypeError(\"Cannot convert undefined or null to object\"); return Object.prototype.hasOwnProperty.call(Object(o), k); }"),
            ["Object.groupBy"] = Method("Object", "groupBy", "function (items, f) { var r = Object.create(null), a = Array.from(items); for (var i = 0; i < a.length; i++) { var k = f(a[i], i); (r[k] || (r[k] = [])).push(a[i]); } return r; }"),
            ["Object.is"] = Method("Object", "is", "function (a, b) { return a === b ? a !== 0 || 1 / a === 1 / b : a !== a && b !== b; }"),
            ["Array.from"] = Method("Array", "from", """
function (items, map, thisArg) {
  if (items == null) throw new TypeError("Array.from requires an array-like object");
  var r = [], it = typeof Symbol === "function" && items[Symbol.iterator];
  if (typeof it === "function" && typeof items !== "string") {
    var i = it.call(items), s;
    while (!(s = i.next()).done) r.push(s.value);
  } else {
    var o = Object(items), n = o.length >>> 0;
    for (var j = 0; j < n; j++) r.push(o[j]);
  }
  if (map) for (var k = 0; k < r.length; k++) r[k] = map.call(thisArg, r[k], k);
  return r;
}
"""),
            ["Array.of"] = Method("Array", "of", "function () { return Array.prototype.slice.call(arguments); }"),
            ["Array.prototype.find"] = Method("Array.prototype", "find", "function (f, t) { for (var i = 0; i < this.length; i++) if (f.call(t, this[i], i, this)) return this[i]; }"),
            ["Array.prototype.findIndex"] = Method("Array.prototype", "findIndex", "function (f, t) { for (var i = 0; i < this.length; i++) if (f.call(t, this[i], i, this)) return i; return -1; }"),
            ["Array.prototype.findLast"] = Method("Array.prototype", "findLast", "function (f, t) { for (var i = this.length - 1; i >= 0; i--) if (f.call(t, this[i], i, this)) return this[i]; }"),
            ["Array.prototype.findLastIndex"] = Method("Array.prototype", "findLastIndex", "function (f, t) { for (var i = this.length - 1; i >= 0; i--) if (f.call(t, this[i], i, this)) return i; return -1; }"),
            ["Array.prototype.fill"] = Method("Array.prototype", "fill", """
function (v, s, e) {
  var n = this.length >>> 0, a = s >> 0, b = e === void 0 ? n : e >> 0;
  for (var i = a < 0 ? Math.max(n + a, 0) : Math.min(a, n), end = b < 0 ? Math.max(n + b, 0) : Math.min(b, n); i < end; i++) this[i] = v;
  return this;
}
"""),
            ["Array.prototype.flat"] = Method("Array.prototype", "flat", """
function (d) {
  d = d === void 0 ? 1 : Number(d);
  var r = [];
  (function f(a, d) {
    for (var i = 0; i < a.length; i++) {
      if (!(i in a)) continue;
      if (d > 0 && Array.isArray(a[i])) f(a[i], d - 1); else r.push(a[i]);
    }
  })(this, d);
  return r;
}
"""),
            ["Array.prototype.flatMap"] = Method("Array.prototype", "flatMap", "function (f, t) { return Array.prototype.map.call(this, f, t).flat(1); }"),
            ["Array.prototype.toSorted"] = Method("Array.prototype", "toSorted", "function (f) { return Array.prototype.slice.call(this).sort(f); }"),
            ["Array.prototype.toReversed"] = Method("Array.prototype", "toReversed", "function () { return Array.prototype.slice.call(this).reverse(); }"),
            ["includes"] =
                Method("Array.prototype", "includes", "function (v, from) { var n = this.length >>> 0, i = from >> 0; if (i < 0) i = Math.max(n + i, 0); for (; i < n; i++) { var x = this[i]; if (x === v || (x !== x && v !== v)) return true; } return false; }")
                + Method("String.prototype", "includes", "function (s, p) { if (Object.prototype.toString.call(s) === \"[object RegExp]\") throw new TypeError(\"First argument must not be a regular expression\"); return String(this).indexOf(s, p) !== -1; }"),
            ["at"] =
                Method("Array.prototype", "at", "function (i) { var n = this.length >>> 0; i = i >> 0; if (i < 0) i += n; return i < 0 || i >= n ? void 0 : this[i]; }")
                + Method("String.prototype", "at", "function (i) { var s = String(this), n = s.length; i = i >> 0; if (i < 0) i += n; return i < 0 || i >= n ? void 0 : s.charAt(i); }"),
            ["String.prototype.startsWith"] = Method("String.prototype", "startsWith", "function (s, p) { p = p > 0 ? p | 0 : 0; return String(this).substring(p, p + String(s).length) === String(s); }"),
            ["String.prototype.endsWith"] = Method("String.prototype", "endsWith", "function (s, e) { var t = String(this); s = String(s); e = e === void 0 || e > t.length ? t.length : e | 0; return t.substring(e - s.length, e) === s; }"),
            ["String.prototype.repeat"] = Method("String.prototype", "repeat", """
function (c) {
  var s = String(this), r = "";
  c = c | 0;
  if (c < 0 || c === Infinity) throw new RangeError("Invalid count value");
  while (c > 0) { if (c & 1) r += s; c >>= 1; s += s; }
  return r;
}
"""),
            ["String.prototype.padStart"] = Method("String.prototype", "padStart", """
function (n, f) {
  var s = String(this); n = n >> 0; f = f === void 0 ? " " : String(f);
  if (s.length >= n || !f.length) return s;
  n -= s.length;
  while (f.length < n) f += f;
  return f.slice(0, n) + s;
}
"""),
            ["String.prototype.padEnd"] = Method("String.prototype", "padEnd", """
function (n, f) {
  var s = String(this); n = n >> 0; f = f === void 0 ? " " : String(f);
  if (s.length >= n || !f.length) return s;
  n -= s.length;
  while (f.length < n) f += f;
  return s + f.slice(0, n);
}
"""),
            ["String.prototype.trimStart"] = Method("String.prototype", "trimStart", "function () { return String(this).replace(/^[\\s\\uFEFF\\xA0]+/, \"\"); }"),
            ["String.prototype.trimEnd"] = Method("String.prototype", "trimEnd", "function () { return String(this).replace(/[\\s\\uFEFF\\xA0]+$/, \"\"); }"),
            ["String.prototype.replaceAll"] = Method("String.prototype", "replaceAll", """
function (p, r) {
  var s = String(this);
  if (Object.prototype.toString.call(p) === "[object RegExp]") {
    if (String(p.flags !== void 0 ? p.flags : (p.global ? "g" : "")).indexOf("g") < 0) throw new TypeError("replaceAll must be called with a global RegExp");
    return s.replace(p, r);
  }
  p = String(p);
  var parts = s.split(p);
  if (typeof r === "function") {
    var out = parts[0], pos = parts[0].length;
    for (var i = 1; i < parts.length; i++) { out += String(r(p, pos, s)) + parts[i]; pos += p.length + parts[i].length; }
    return out;
  }
  r = String(r);
  if (r.indexOf("$") < 0) return parts.join(r);
  return s.replace(new RegExp(p.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "g"), r);
}
"""),
            ["Number.isInteger"] = Method("Number", "isInteger", "function (v) { return typeof v === \"number\" && isFinite(v) && Math.floor(v) === v; }"),
            ["Number.isSafeInteger"] = Method("Number", "isSafeInteger", "function (v) { return typeof v === \"number\" && isFinite(v) && Math.floor(v) === v && Math.abs(v) <= 9007199254740991; }"),
            ["Number.isNaN"] = Method("Number", "isNaN", "function (v) { return typeof v === \"number\" && v !== v; }"),
            ["Number.isFinite"] = Method("Number", "isFinite", "function (v) { return typeof v === \"number\" && isFinite(v); }"),
            ["Number.parseFloat"] = Method("Number", "parseFloat", "parseFloat"),
            ["Number.parseInt"] = Method("Number", "parseInt", "parseInt"),
            ["Number.EPSILON"] = Method("Number", "EPSILON", "Math.pow(2, -52)"),
            ["Number.MAX_SAFE_INTEGER"] = Method("Number", "MAX_SAFE_INTEGER", "9007199254740991"),
            ["Number.MIN_SAFE_INTEGER"] = Method("Number", "MIN_SAFE_INTEGER", "-9007199254740991"),
            ["Math.trunc"] = Method("Math", "trunc", "function (v) { v = +v; return isFinite(v) && v !== 0 ? (v < 0 ? Math.ceil(v) : Math.floor(v)) : v; }"),
            ["Math.sign"] = Method("Math", "sign", "function (v) { v = +v; return v > 0 ? 1 : v < 0 ? -1 : v; }"),
            ["Math.log2"] = Method("Math", "log2", "function (v) { return Math.log(v) / Math.LN2; }"),
            ["Math.log10"] = Method("Math", "log10", "function (v) { return Math.log(v) / Math.LN10; }"),
            ["Math.cbrt"] = Method("Math", "cbrt", "function (v) { v = +v; var r = Math.pow(Math.abs(v), 1 / 3); return v < 0 ? -r : r; }"),
            ["Math.hypot"] = Method("Math", "hypot", "function () { var s = 0; for (var i = 0; i < arguments.length; i++) { var v = +arguments[i]; if (v === Infinity || v === -Infinity) return Infinity; s += v * v; } return Math.sqrt(s); }"),
        };
    }
}
