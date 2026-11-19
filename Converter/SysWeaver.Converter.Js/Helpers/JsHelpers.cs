using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.Converter.Js.Helpers
{
    /// <summary>
    /// The runtime helper functions used by the transforms.
    /// All helpers are written in ES5 (so they work on every target), names start with an underscore (ex: _defineProperty).
    /// Most helpers are compatible with the Babel helpers of the same name.
    /// </summary>
    public static class JsHelpers
    {
        static readonly Dictionary<String, String> Sources = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            ["typeof"] = """
function _typeof(o) {
  return _typeof = "function" == typeof Symbol && "symbol" == typeof Symbol.iterator ? function (o) { return typeof o; } : function (o) { return o && "function" == typeof Symbol && o.constructor === Symbol && o !== Symbol.prototype ? "symbol" : typeof o; }, _typeof(o);
}
""",
            ["toPrimitive"] = """
function _toPrimitive(t, r) {
  if ("object" != _typeof(t) || !t) return t;
  var e = "function" == typeof Symbol ? t[Symbol.toPrimitive] : void 0;
  if (void 0 !== e) {
    var i = e.call(t, r || "default");
    if ("object" != _typeof(i)) return i;
    throw new TypeError("@@toPrimitive must return a primitive value.");
  }
  return ("string" === r ? String : Number)(t);
}
""",
            ["toPropertyKey"] = """
function _toPropertyKey(t) {
  var i = _toPrimitive(t, "string");
  return "symbol" == _typeof(i) ? i : i + "";
}
""",
            ["defineProperty"] = """
function _defineProperty(e, r, t) {
  return (r = _toPropertyKey(r)) in e ? Object.defineProperty(e, r, { value: t, enumerable: !0, configurable: !0, writable: !0 }) : e[r] = t, e;
}
""",
            ["defineAccessor"] = """
function _defineAccessor(e, r, n, t) {
  var c = { configurable: !0, enumerable: !0 };
  return c[e] = t, Object.defineProperty(r, n, c);
}
""",
            ["readOnlyError"] = """
function _readOnlyError(r) {
  throw new TypeError('"' + r + '" is read-only');
}
""",
            ["checkInRHS"] = """
function _checkInRHS(e) {
  if (Object(e) !== e) throw TypeError("right-hand side of 'in' should be an object, got " + (null !== e ? _typeof(e) : "null"));
  return e;
}
""",
            //  Private class members
            ["checkPrivateRedeclaration"] = """
function _checkPrivateRedeclaration(e, t) {
  if (t.has(e)) throw new TypeError("Cannot initialize the same private elements twice on an object");
}
""",
            ["classPrivateFieldInitSpec"] = """
function _classPrivateFieldInitSpec(e, t, a) {
  _checkPrivateRedeclaration(e, t), t.set(e, { _: a });
}
""",
            ["classPrivateMethodInitSpec"] = """
function _classPrivateMethodInitSpec(e, a) {
  _checkPrivateRedeclaration(e, a), a.add(e);
}
""",
            ["classPrivateFieldBox"] = """
function _classPrivateFieldBox(s, r) {
  var b = s.get(r);
  if (!b) throw new TypeError("Private element is not present on this object");
  return b;
}
""",
            ["assertClassBrand"] = """
function _assertClassBrand(e, t, n) {
  if ("function" == typeof e ? e === t : e.has(t)) return arguments.length < 3 ? t : n;
  throw new TypeError("Private element is not present on this object");
}
""",
            ["classPrivateGetter"] = """
function _classPrivateGetter(s, r, a) {
  return a.call(_assertClassBrand(s, r));
}
""",
            ["classPrivateSetter"] = """
function _classPrivateSetter(s, r, a, t) {
  return a.call(_assertClassBrand(s, r), t), t;
}
""",
            ["privateAccessorBox"] = """
function _privateAccessorBox(s, r, g, p, n) {
  var o = _assertClassBrand(s, r);
  return Object.defineProperty({}, "_", {
    get: function () { if (!g) throw new TypeError("'#" + n + "' was defined without a getter"); return g.call(o); },
    set: function (v) { if (!p) throw new TypeError("'#" + n + "' was defined without a setter"); p.call(o, v); }
  });
}
""",
            ["readOnlyPrivate"] = """
function _readOnlyPrivate(n) {
  throw new TypeError("Private method '#" + n + "' is not writable");
}
""",
            //  Object spread / rest
            ["ownKeys"] = """
function _ownKeys(e, r) {
  var t = Object.keys(e);
  if (Object.getOwnPropertySymbols) {
    var o = Object.getOwnPropertySymbols(e);
    r && (o = o.filter(function (r) { return Object.getOwnPropertyDescriptor(e, r).enumerable; })), t.push.apply(t, o);
  }
  return t;
}
""",
            ["objectSpread2"] = """
function _objectSpread2(e) {
  for (var r = 1; r < arguments.length; r++) {
    var t = null != arguments[r] ? arguments[r] : {};
    r % 2 ? _ownKeys(Object(t), !0).forEach(function (r) { _defineProperty(e, r, t[r]); }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(e, Object.getOwnPropertyDescriptors(t)) : _ownKeys(Object(t)).forEach(function (r) { Object.defineProperty(e, r, Object.getOwnPropertyDescriptor(t, r)); });
  }
  return e;
}
""",
            ["extends"] = """
function _extends() {
  return _extends = Object.assign ? Object.assign.bind() : function (n) {
    for (var e = 1; e < arguments.length; e++) {
      var t = arguments[e];
      for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]);
    }
    return n;
  }, _extends.apply(null, arguments);
}
""",
            ["objectWithoutPropertiesLoose"] = """
function _objectWithoutPropertiesLoose(r, e) {
  if (null == r) return {};
  var t = {};
  for (var n in r) if ({}.hasOwnProperty.call(r, n)) {
    if (-1 !== e.indexOf(n)) continue;
    t[n] = r[n];
  }
  return t;
}
""",
            ["objectWithoutProperties"] = """
function _objectWithoutProperties(e, t) {
  if (null == e) return {};
  var o, r, i = _objectWithoutPropertiesLoose(e, t);
  if (Object.getOwnPropertySymbols) {
    var n = Object.getOwnPropertySymbols(e);
    for (r = 0; r < n.length; r++) o = n[r], -1 === t.indexOf(o) && {}.propertyIsEnumerable.call(e, o) && (i[o] = e[o]);
  }
  return i;
}
""",
            ["objectDestructuringEmpty"] = """
function _objectDestructuringEmpty(t) {
  if (null == t) throw new TypeError("Cannot destructure " + t);
}
""",
            //  Arrays / iterables
            ["arrayLikeToArray"] = """
function _arrayLikeToArray(r, a) {
  (null == a || a > r.length) && (a = r.length);
  for (var e = 0, n = Array(a); e < a; e++) n[e] = r[e];
  return n;
}
""",
            ["unsupportedIterableToArray"] = """
function _unsupportedIterableToArray(r, a) {
  if (r) {
    if ("string" == typeof r) return _arrayLikeToArray(r, a);
    var t = {}.toString.call(r).slice(8, -1);
    return "Object" === t && r.constructor && (t = r.constructor.name), "Map" === t || "Set" === t ? Array.from(r) : "Arguments" === t || /^(?:Ui|I)nt(?:8|16|32)(?:Clamped)?Array$/.test(t) ? _arrayLikeToArray(r, a) : void 0;
  }
}
""",
            ["arrayWithoutHoles"] = """
function _arrayWithoutHoles(r) {
  if (Array.isArray(r)) return _arrayLikeToArray(r);
}
""",
            ["arrayWithHoles"] = """
function _arrayWithHoles(r) {
  if (Array.isArray(r)) return r;
}
""",
            ["iterableToArray"] = """
function _iterableToArray(r) {
  if ("undefined" != typeof Symbol && null != r[Symbol.iterator] || null != r["@@iterator"]) {
    if (Array.from) return Array.from(r);
    var t = ("undefined" != typeof Symbol && r[Symbol.iterator] || r["@@iterator"]).call(r), a = [], s;
    while (!(s = t.next()).done) a.push(s.value);
    return a;
  }
}
""",
            ["nonIterableSpread"] = """
function _nonIterableSpread() {
  throw new TypeError("Invalid attempt to spread non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
}
""",
            ["nonIterableRest"] = """
function _nonIterableRest() {
  throw new TypeError("Invalid attempt to destructure non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
}
""",
            ["toConsumableArray"] = """
function _toConsumableArray(r) {
  return _arrayWithoutHoles(r) || _iterableToArray(r) || _unsupportedIterableToArray(r) || _nonIterableSpread();
}
""",
            ["iterableToArrayLimit"] = """
function _iterableToArrayLimit(r, l) {
  var t = null == r ? null : "undefined" != typeof Symbol && r[Symbol.iterator] || r["@@iterator"];
  if (null != t) {
    var e, n, i, u, a = [], f = !0, o = !1;
    try {
      if (i = (t = t.call(r)).next, 0 === l) {
        if (Object(t) !== t) return;
        f = !1;
      } else for (; !(f = (e = i.call(t)).done) && (a.push(e.value), a.length !== l); f = !0);
    } catch (r) {
      o = !0, n = r;
    } finally {
      try {
        if (!f && null != t["return"] && (u = t["return"](), Object(u) !== u)) return;
      } finally {
        if (o) throw n;
      }
    }
    return a;
  }
}
""",
            ["slicedToArray"] = """
function _slicedToArray(r, e) {
  return _arrayWithHoles(r) || _iterableToArrayLimit(r, e) || _unsupportedIterableToArray(r, e) || _nonIterableRest();
}
""",
            ["toArray"] = """
function _toArray(r) {
  return _arrayWithHoles(r) || _iterableToArray(r) || _unsupportedIterableToArray(r) || _nonIterableRest();
}
""",
            ["maybeArrayLike"] = """
function _maybeArrayLike(r, a, e) {
  if (a && !Array.isArray(a) && "number" == typeof a.length) {
    var y = a.length;
    return _arrayLikeToArray(a, void 0 !== e && e < y ? e : y);
  }
  return r(a, e);
}
""",
            ["createForOfIteratorHelper"] = """
function _createForOfIteratorHelper(r, e) {
  var t = "undefined" != typeof Symbol && r[Symbol.iterator] || r["@@iterator"];
  if (!t) {
    if (Array.isArray(r) || (t = _unsupportedIterableToArray(r)) || e && r && "number" == typeof r.length) {
      t && (r = t);
      var n = 0, F = function () {};
      return { s: F, n: function () { return n >= r.length ? { done: !0 } : { done: !1, value: r[n++] }; }, e: function (r) { throw r; }, f: F };
    }
    throw new TypeError("Invalid attempt to iterate non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
  }
  var o, a = !0, u = !1;
  return {
    s: function () { t = t.call(r); },
    n: function () { var r = t.next(); return a = r.done, r; },
    e: function (r) { u = !0, o = r; },
    f: function () { try { a || null == t["return"] || t["return"](); } finally { if (u) throw o; } }
  };
}
""",
            ["construct"] = """
function _construct(t, e, r) {
  if (_isNativeReflectConstruct()) return Reflect.construct.apply(null, arguments);
  var o = [null];
  o.push.apply(o, e);
  var p = new (t.bind.apply(t, o))();
  return r && _setPrototypeOf(p, r.prototype), p;
}
""",
            //  Templates
            ["taggedTemplateLiteral"] = """
function _taggedTemplateLiteral(e, t) {
  return t || (t = e.slice(0)), Object.freeze(Object.defineProperties(e, { raw: { value: Object.freeze(t) } }));
}
""",
            //  Classes
            ["classCallCheck"] = """
function _classCallCheck(a, n) {
  if (!(a instanceof n)) throw new TypeError("Cannot call a class as a function");
}
""",
            ["defineProperties"] = """
function _defineProperties(e, r) {
  for (var t = 0; t < r.length; t++) {
    var o = r[t];
    o.enumerable = o.enumerable || !1, o.configurable = !0, "value" in o && (o.writable = !0), Object.defineProperty(e, _toPropertyKey(o.key), o);
  }
}
""",
            ["createClass"] = """
function _createClass(e, r, t) {
  return r && _defineProperties(e.prototype, r), t && _defineProperties(e, t), Object.defineProperty(e, "prototype", { writable: !1 }), e;
}
""",
            ["setPrototypeOf"] = """
function _setPrototypeOf(t, e) {
  return _setPrototypeOf = Object.setPrototypeOf ? Object.setPrototypeOf.bind() : function (t, e) { return t.__proto__ = e, t; }, _setPrototypeOf(t, e);
}
""",
            ["getPrototypeOf"] = """
function _getPrototypeOf(t) {
  return _getPrototypeOf = Object.setPrototypeOf ? Object.getPrototypeOf.bind() : function (t) { return t.__proto__ || Object.getPrototypeOf(t); }, _getPrototypeOf(t);
}
""",
            ["inherits"] = """
function _inherits(t, e) {
  if ("function" != typeof e && null !== e) throw new TypeError("Super expression must either be null or a function");
  t.prototype = Object.create(e && e.prototype, { constructor: { value: t, writable: !0, configurable: !0 } }), Object.defineProperty(t, "prototype", { writable: !1 }), e && _setPrototypeOf(t, e);
}
""",
            ["isNativeReflectConstruct"] = """
function _isNativeReflectConstruct() {
  try {
    var t = !Boolean.prototype.valueOf.call(Reflect.construct(Boolean, [], function () {}));
  } catch (t) {}
  return (_isNativeReflectConstruct = function () { return !!t; })();
}
""",
            ["assertThisInitialized"] = """
function _assertThisInitialized(e) {
  if (void 0 === e) throw new ReferenceError("this hasn't been initialised - super() hasn't been called");
  return e;
}
""",
            ["possibleConstructorReturn"] = """
function _possibleConstructorReturn(t, e) {
  if (e && ("object" == _typeof(e) || "function" == typeof e)) return e;
  if (void 0 !== e) throw new TypeError("Derived constructors may only return object or undefined");
  return _assertThisInitialized(t);
}
""",
            ["callSuper"] = """
function _callSuper(t, o, e) {
  return o = _getPrototypeOf(o), _possibleConstructorReturn(t, _isNativeReflectConstruct() ? Reflect.construct(o, e || [], _getPrototypeOf(t).constructor) : o.apply(t, e));
}
""",
            ["superPropBase"] = """
function _superPropBase(t, o) {
  for (; !{}.hasOwnProperty.call(t, o) && null !== (t = _getPrototypeOf(t)););
  return t;
}
""",
            ["get"] = """
function _get() {
  return _get = "undefined" != typeof Reflect && Reflect.get ? Reflect.get.bind() : function (e, t, r) {
    var p = _superPropBase(e, t);
    if (p) {
      var n = Object.getOwnPropertyDescriptor(p, t);
      return n.get ? n.get.call(arguments.length < 3 ? e : r) : n.value;
    }
  }, _get.apply(null, arguments);
}
""",
            ["superPropGet"] = """
function _superPropGet(t, o, e, r) {
  var p = _get(_getPrototypeOf(1 & r ? t.prototype : t), o, e);
  return 2 & r && "function" == typeof p ? function (t) { return p.apply(e, t); } : p;
}
""",
            ["superPropSet"] = """
function _superPropSet(t, o, v, e, r) {
  var h = _getPrototypeOf(1 & r ? t.prototype : t), p = _superPropBase(h, o), d = p ? Object.getOwnPropertyDescriptor(p, o) : void 0;
  if (d && d.set) d.set.call(e, v);
  else if (d && "value" in d && !d.writable) { if (r & 4) throw new TypeError("Cannot assign to read only property '" + String(o) + "'"); }
  else if ({}.hasOwnProperty.call(e, o) || !d || "value" in d) {
    var c = Object.getOwnPropertyDescriptor(e, o);
    if (c && !("value" in c)) { if (c.set) c.set.call(e, v); }
    else if (c) { if (c.writable) e[o] = v; else if (r & 4) throw new TypeError("Cannot assign to read only property '" + String(o) + "'"); }
    else Object.defineProperty(e, o, { value: v, writable: !0, enumerable: !0, configurable: !0 });
  }
  return v;
}
""",
            ["isNativeFunction"] = """
function _isNativeFunction(t) {
  try {
    return -1 !== Function.toString.call(t).indexOf("[native code]");
  } catch (n) {
    return "function" == typeof t;
  }
}
""",
            ["wrapNativeSuper"] = """
function _wrapNativeSuper(t) {
  var r = "function" == typeof Map ? new Map() : void 0;
  return _wrapNativeSuper = function (t) {
    if (null === t || !_isNativeFunction(t)) return t;
    if ("function" != typeof t) throw new TypeError("Super expression must either be null or a function");
    if (void 0 !== r) {
      if (r.has(t)) return r.get(t);
      r.set(t, Wrapper);
    }
    function Wrapper() {
      return _construct(t, arguments, _getPrototypeOf(this).constructor);
    }
    return Wrapper.prototype = Object.create(t.prototype, { constructor: { value: Wrapper, enumerable: !1, writable: !0, configurable: !0 } }), _setPrototypeOf(Wrapper, t);
  }, _wrapNativeSuper(t);
}
""",
            //  Async functions
            ["asyncGeneratorStep"] = """
function _asyncGeneratorStep(n, t, e, r, o, a, c) {
  try {
    var i = n[a](c), u = i.value;
  } catch (n) {
    return void e(n);
  }
  i.done ? t(u) : Promise.resolve(u).then(r, o);
}
""",
            ["asyncToGenerator"] = """
function _asyncToGenerator(n) {
  return function () {
    var t = this, e = arguments;
    return new Promise(function (r, o) {
      var a = n.apply(t, e);
      function _next(n) { _asyncGeneratorStep(a, r, o, _next, _throw, "next", n); }
      function _throw(n) { _asyncGeneratorStep(a, r, o, _next, _throw, "throw", n); }
      _next(void 0);
    });
  };
}
""",
            //  Async generators
            ["OverloadYield"] = """
function _OverloadYield(e, d) {
  this.v = e, this.k = d;
}
""",
            ["awaitAsyncGenerator"] = """
function _awaitAsyncGenerator(e) {
  return new _OverloadYield(e, 0);
}
""",
            ["AsyncGenerator"] = """
function _AsyncGenerator(e) {
  var r, t;
  function resume(r, t) {
    try {
      var n = e[r](t), o = n.value, u = o instanceof _OverloadYield;
      Promise.resolve(u ? o.v : o).then(function (t) {
        if (u) {
          var i = "return" === r ? "return" : "next";
          if (!o.k || t.done) return resume(i, t);
          t = e[i](t).value;
        }
        settle(n.done ? "return" : "normal", t);
      }, function (e) { resume("throw", e); });
    } catch (e) {
      settle("throw", e);
    }
  }
  function settle(e, n) {
    switch (e) {
      case "return": r.resolve({ value: n, done: !0 }); break;
      case "throw": r.reject(n); break;
      default: r.resolve({ value: n, done: !1 });
    }
    (r = r.next) ? resume(r.key, r.arg) : t = null;
  }
  this._invoke = function (e, n) {
    return new Promise(function (o, u) {
      var i = { key: e, arg: n, resolve: o, reject: u, next: null };
      t ? t = t.next = i : (r = t = i, resume(e, n));
    });
  }, "function" != typeof e["return"] && (this["return"] = void 0);
}
_AsyncGenerator.prototype["function" == typeof Symbol && Symbol.asyncIterator || "@@asyncIterator"] = function () { return this; };
_AsyncGenerator.prototype.next = function (e) { return this._invoke("next", e); };
_AsyncGenerator.prototype["throw"] = function (e) { return this._invoke("throw", e); };
_AsyncGenerator.prototype["return"] = function (e) { return this._invoke("return", e); };
""",
            ["wrapAsyncGenerator"] = """
function _wrapAsyncGenerator(e) {
  return function () {
    return new _AsyncGenerator(e.apply(this, arguments));
  };
}
""",
            ["asyncIterator"] = """
function _asyncIterator(r) {
  var n, t, o, e = 2;
  for ("undefined" != typeof Symbol && (t = Symbol.asyncIterator, o = Symbol.iterator); e--;) {
    if (t && null != (n = r[t])) return n.call(r);
    if (o && null != (n = r[o])) return new _AsyncFromSyncIterator(n.call(r));
    t = "@@asyncIterator", o = "@@iterator";
  }
  throw new TypeError("Object is not async iterable");
}
""",
            ["AsyncFromSyncIterator"] = """
function _AsyncFromSyncIterator(r) {
  function AsyncFromSyncIteratorContinuation(r) {
    if (Object(r) !== r) return Promise.reject(new TypeError(r + " is not an object."));
    var n = r.done;
    return Promise.resolve(r.value).then(function (r) { return { value: r, done: n }; });
  }
  return _AsyncFromSyncIterator = function (r) { this.s = r, this.n = r.next; }, _AsyncFromSyncIterator.prototype = {
    s: null,
    n: null,
    next: function () { return AsyncFromSyncIteratorContinuation(this.n.apply(this.s, arguments)); },
    "return": function (r) {
      var n = this.s["return"];
      return void 0 === n ? Promise.resolve({ value: r, done: !0 }) : AsyncFromSyncIteratorContinuation(n.apply(this.s, arguments));
    },
    "throw": function (r) {
      var n = this.s["return"];
      return void 0 === n ? Promise.reject(r) : AsyncFromSyncIteratorContinuation(n.apply(this.s, arguments));
    }
  }, new _AsyncFromSyncIterator(r);
}
""",
            ["asyncGeneratorDelegate"] = """
function _asyncGeneratorDelegate(t) {
  var e = {}, n = !1;
  function pump(e, r) {
    return n = !0, r = new Promise(function (n) { n(t[e](r)); }), { done: !1, value: new _OverloadYield(r, 1) };
  }
  return e["undefined" != typeof Symbol && Symbol.iterator || "@@iterator"] = function () { return this; }, e.next = function (t) {
    return n ? (n = !1, t) : pump("next", t);
  }, "function" == typeof t["throw"] && (e["throw"] = function (t) {
    if (n) throw n = !1, t;
    return pump("throw", t);
  }), "function" == typeof t["return"] && (e["return"] = function (t) {
    return n ? (n = !1, t) : pump("return", t);
  }), e;
}
""",
            //  Regular expressions
            ["wrapRegExp"] = """
function _wrapRegExp(e, r) {
  var x = e.exec;
  function groups(t) {
    if (t) {
      var g = {};
      for (var n in r) if ({}.hasOwnProperty.call(r, n)) g[n] = t[r[n]];
      t.groups = g;
    }
    return t;
  }
  e.exec = function (t) { return groups(x.call(this, t)); };
  if ("function" == typeof Symbol && Symbol.replace) {
    var p = RegExp.prototype[Symbol.replace];
    e[Symbol.replace] = function (t, a) {
      if ("string" == typeof a) a = a.replace(/\$<([^>]*)>/g, function (m, n) { var i = r[n]; return void 0 === i ? "" : "$" + i; });
      else if ("function" == typeof a) {
        var f = a;
        a = function () {
          var s = [].slice.call(arguments);
          if ("object" != _typeof(s[s.length - 1])) {
            var g = {};
            for (var n in r) if ({}.hasOwnProperty.call(r, n)) g[n] = s[r[n]];
            s.push(g);
          }
          return f.apply(this, s);
        };
      }
      return p.call(this, t, a);
    };
  }
  return e;
}
""",
            //  Misc
            ["newArrowCheck"] = """
function _newArrowCheck(n, r) {
  if (n !== r) throw new TypeError("Cannot instantiate an arrow function");
}
""",
            ["tdz"] = """
function _tdz(e) {
  throw new ReferenceError(e + " is not defined - temporal dead zone");
}
""",
        };

        static readonly Regex HelperRefRegex = new Regex(@"(?<![\w$.])_([A-Za-z][A-Za-z0-9]*)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly Dictionary<String, String[]> Deps = BuildDeps();

        static Dictionary<String, String[]> BuildDeps()
        {
            var r = new Dictionary<String, String[]>(StringComparer.Ordinal);
            foreach (var kv in Sources)
            {
                var d = new HashSet<String>(StringComparer.Ordinal);
                foreach (Match m in HelperRefRegex.Matches(kv.Value))
                {
                    var n = m.Groups[1].Value;
                    if (n != kv.Key && Sources.ContainsKey(n))
                        d.Add(n);
                }
                r[kv.Key] = d.ToArray();
            }
            return r;
        }

        /// <summary>
        /// Get the helpers that a helper depends on
        /// </summary>
        public static IReadOnlyList<String> Dependencies(String name)
        {
            if (Deps.TryGetValue(name, out var d))
                return d;
            if (RegeneratorRuntime.Name == name)
                return Array.Empty<String>();
            throw new ArgumentException("Unknown helper \"" + name + "\"", nameof(name));
        }

        /// <summary>
        /// Get the source of a helper (with the default names, ex: function _defineProperty(...) {...})
        /// </summary>
        public static String GetSource(String name)
        {
            if (Sources.TryGetValue(name, out var s))
                return s;
            if (RegeneratorRuntime.Name == name)
                return RegeneratorRuntime.Source;
            throw new ArgumentException("Unknown helper \"" + name + "\"", nameof(name));
        }

        /// <summary>
        /// The names of all helpers (without the leading underscore)
        /// </summary>
        public static IEnumerable<String> All => Sources.Keys.Append(RegeneratorRuntime.Name);

        /// <summary>
        /// Get all helpers (and their dependencies) in dependency order (dependencies first)
        /// </summary>
        public static List<String> Ordered(IEnumerable<String> names)
        {
            var r = new List<String>();
            var seen = new HashSet<String>(StringComparer.Ordinal);
            void Add(String n)
            {
                if (!seen.Add(n))
                    return;
                foreach (var d in Dependencies(n))
                    Add(d);
                r.Add(n);
            }
            foreach (var n in names)
                Add(n);
            return r;
        }
    }
}
