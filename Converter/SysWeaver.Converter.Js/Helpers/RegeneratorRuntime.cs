using System;

namespace SysWeaver.Converter.Js.Helpers
{
    /// <summary>
    /// The generator runtime used by the generator transform (ES5, works without Symbol).
    /// The API is compatible with regenerator-runtime: mark, wrap, awrap, keys, values, async, AsyncIterator, isGeneratorFunction.
    /// </summary>
    static class RegeneratorRuntime
    {
        public const String Name = "regeneratorRuntime";

        public const String Source = """
function _regeneratorRuntime() {
  var exports = {}, Op = Object.prototype, hasOwn = Op.hasOwnProperty, undef;
  _regeneratorRuntime = function () { return exports; };
  var $Symbol = "function" == typeof Symbol ? Symbol : {};
  var iteratorSymbol = $Symbol.iterator || "@@iterator";
  var asyncIteratorSymbol = $Symbol.asyncIterator || "@@asyncIterator";
  var toStringTagSymbol = $Symbol.toStringTag || "@@toStringTag";
  function define(obj, key, value) {
    try {
      Object.defineProperty(obj, key, { value: value, enumerable: !1, configurable: !0, writable: !0 });
    } catch (e) {
      obj[key] = value;
    }
    return obj[key];
  }
  var GenStateSuspendedStart = 1, GenStateSuspendedYield = 2, GenStateExecuting = 3, GenStateCompleted = 4;
  var ContinueSentinel = {};
  function Generator() {}
  function GeneratorFunction() {}
  function GeneratorFunctionPrototype() {}
  var IteratorPrototype = {};
  define(IteratorPrototype, iteratorSymbol, function () { return this; });
  var getProto = Object.getPrototypeOf, NativeIteratorPrototype = getProto && getProto(getProto(values([])));
  if (NativeIteratorPrototype && NativeIteratorPrototype !== Op && hasOwn.call(NativeIteratorPrototype, iteratorSymbol)) IteratorPrototype = NativeIteratorPrototype;
  var Gp = GeneratorFunctionPrototype.prototype = Generator.prototype = Object.create(IteratorPrototype);
  GeneratorFunction.prototype = GeneratorFunctionPrototype;
  define(Gp, "constructor", GeneratorFunctionPrototype);
  define(GeneratorFunctionPrototype, "constructor", GeneratorFunction);
  GeneratorFunction.displayName = define(GeneratorFunctionPrototype, toStringTagSymbol, "GeneratorFunction");
  function defineIteratorMethods(prototype) {
    ["next", "throw", "return"].forEach(function (method) {
      define(prototype, method, function (arg) { return this._invoke(method, arg); });
    });
  }
  defineIteratorMethods(Gp);
  define(Gp, toStringTagSymbol, "Generator");
  define(Gp, iteratorSymbol, function () { return this; });
  define(Gp, "toString", function () { return "[object Generator]"; });
  function tryCatch(fn, obj, arg) {
    try {
      return { type: "normal", arg: fn.call(obj, arg) };
    } catch (err) {
      return { type: "throw", arg: err };
    }
  }
  function wrap(innerFn, outerFn, self, tryLocsList) {
    var protoGenerator = outerFn && outerFn.prototype instanceof Generator ? outerFn : Generator;
    var generator = Object.create(protoGenerator.prototype);
    var context = new Context(tryLocsList || []);
    define(generator, "_invoke", makeInvokeMethod(innerFn, self, context));
    return generator;
  }
  exports.wrap = wrap;
  exports.isGeneratorFunction = function (genFun) {
    var ctor = "function" == typeof genFun && genFun.constructor;
    return !!ctor && (ctor === GeneratorFunction || "GeneratorFunction" === (ctor.displayName || ctor.name));
  };
  exports.mark = function (genFun) {
    if (Object.setPrototypeOf) Object.setPrototypeOf(genFun, GeneratorFunctionPrototype);
    else {
      genFun.__proto__ = GeneratorFunctionPrototype;
      define(genFun, toStringTagSymbol, "GeneratorFunction");
    }
    genFun.prototype = Object.create(Gp);
    return genFun;
  };
  exports.awrap = function (arg) {
    return { __await: arg };
  };
  function AsyncIterator(generator, PromiseImpl) {
    function invoke(method, arg, resolve, reject) {
      var record = tryCatch(generator[method], generator, arg);
      if ("throw" === record.type) reject(record.arg);
      else {
        var result = record.arg, value = result.value;
        return value && "object" == typeof value && hasOwn.call(value, "__await") ? PromiseImpl.resolve(value.__await).then(function (value) {
          invoke("next", value, resolve, reject);
        }, function (err) {
          invoke("throw", err, resolve, reject);
        }) : PromiseImpl.resolve(value).then(function (unwrapped) {
          result.value = unwrapped, resolve(result);
        }, function (error) {
          return invoke("throw", error, resolve, reject);
        });
      }
    }
    var previousPromise;
    define(this, "_invoke", function (method, arg) {
      function callInvokeWithMethodAndArg() {
        return new PromiseImpl(function (resolve, reject) { invoke(method, arg, resolve, reject); });
      }
      return previousPromise = previousPromise ? previousPromise.then(callInvokeWithMethodAndArg, callInvokeWithMethodAndArg) : callInvokeWithMethodAndArg();
    });
  }
  defineIteratorMethods(AsyncIterator.prototype);
  define(AsyncIterator.prototype, asyncIteratorSymbol, function () { return this; });
  exports.AsyncIterator = AsyncIterator;
  exports.async = function (innerFn, outerFn, self, tryLocsList, PromiseImpl) {
    void 0 === PromiseImpl && (PromiseImpl = Promise);
    var iter = new AsyncIterator(wrap(innerFn, outerFn, self, tryLocsList), PromiseImpl);
    return exports.isGeneratorFunction(outerFn) ? iter : iter.next().then(function (result) {
      return result.done ? result.value : iter.next();
    });
  };
  function makeInvokeMethod(innerFn, self, context) {
    var state = GenStateSuspendedStart;
    return function (method, arg) {
      if (state === GenStateExecuting) throw new Error("Generator is already running");
      if (state === GenStateCompleted) {
        if ("throw" === method) throw arg;
        return { value: undef, done: !0 };
      }
      context.method = method;
      context.arg = arg;
      while (1) {
        var delegate = context.delegate;
        if (delegate) {
          var delegateResult = maybeInvokeDelegate(delegate, context);
          if (delegateResult) {
            if (delegateResult === ContinueSentinel) continue;
            return delegateResult;
          }
        }
        if ("next" === context.method) context.sent = context._sent = context.arg;
        else if ("throw" === context.method) {
          if (state === GenStateSuspendedStart) {
            state = GenStateCompleted;
            throw context.arg;
          }
          context.dispatchException(context.arg);
        } else if ("return" === context.method) context.abrupt("return", context.arg);
        state = GenStateExecuting;
        var record = tryCatch(innerFn, self, context);
        if ("normal" === record.type) {
          state = context.done ? GenStateCompleted : GenStateSuspendedYield;
          if (record.arg === ContinueSentinel) continue;
          return { value: record.arg, done: context.done };
        }
        state = GenStateCompleted;
        context.method = "throw";
        context.arg = record.arg;
      }
    };
  }
  function maybeInvokeDelegate(delegate, context) {
    var methodName = context.method, method = delegate.iterator[methodName];
    if (method === undef) {
      context.delegate = null;
      if ("throw" === methodName && delegate.iterator["return"]) {
        context.method = "return";
        context.arg = undef;
        maybeInvokeDelegate(delegate, context);
        if ("throw" === context.method) return ContinueSentinel;
      }
      if ("return" !== methodName) {
        context.method = "throw";
        context.arg = new TypeError("The iterator does not provide a '" + methodName + "' method");
      }
      return ContinueSentinel;
    }
    var record = tryCatch(method, delegate.iterator, context.arg);
    if ("throw" === record.type) {
      context.method = "throw";
      context.arg = record.arg;
      context.delegate = null;
      return ContinueSentinel;
    }
    var info = record.arg;
    if (!info || "object" != typeof info) {
      context.method = "throw";
      context.arg = new TypeError("iterator result is not an object");
      context.delegate = null;
      return ContinueSentinel;
    }
    if (!info.done) return info;
    context[delegate.resultName] = info.value;
    context.next = delegate.nextLoc;
    if ("return" !== context.method) {
      context.method = "next";
      context.arg = undef;
    } else context.arg = info.value;
    context.delegate = null;
    return ContinueSentinel;
  }
  function pushTryEntry(locs) {
    var entry = { tryLoc: locs[0] };
    if (1 in locs) entry.catchLoc = locs[1];
    if (2 in locs) {
      entry.finallyLoc = locs[2];
      entry.afterLoc = locs[3];
    }
    this.tryEntries.push(entry);
  }
  function resetTryEntry(entry) {
    var record = entry.completion || {};
    record.type = "normal";
    delete record.arg;
    entry.completion = record;
  }
  function Context(tryLocsList) {
    this.tryEntries = [{ tryLoc: "root" }];
    for (var i = 0; i < tryLocsList.length; i++) pushTryEntry.call(this, tryLocsList[i]);
    this.reset(!0);
  }
  function values(iterable) {
    if (iterable || "" === iterable) {
      var iteratorMethod = iterable[iteratorSymbol];
      if (iteratorMethod) return iteratorMethod.call(iterable);
      if ("function" == typeof iterable.next) return iterable;
      if (!isNaN(iterable.length)) {
        var i = -1, next = function next() {
          while (++i < iterable.length) if (hasOwn.call(iterable, i)) {
            next.value = iterable[i];
            next.done = !1;
            return next;
          }
          next.value = undef;
          next.done = !0;
          return next;
        };
        return next.next = next;
      }
    }
    throw new TypeError(typeof iterable + " is not iterable");
  }
  exports.values = values;
  exports.keys = function (val) {
    var object = Object(val), keys = [];
    for (var key in object) keys.push(key);
    keys.reverse();
    return function next() {
      while (keys.length) {
        var key = keys.pop();
        if (key in object) {
          next.value = key;
          next.done = !1;
          return next;
        }
      }
      next.done = !0;
      return next;
    };
  };
  Context.prototype = {
    constructor: Context,
    reset: function (skipTempReset) {
      this.prev = 0;
      this.next = 0;
      this.sent = this._sent = undef;
      this.done = !1;
      this.delegate = null;
      this.method = "next";
      this.arg = undef;
      for (var i = 0; i < this.tryEntries.length; i++) resetTryEntry(this.tryEntries[i]);
      if (!skipTempReset) for (var name in this) "t" === name.charAt(0) && hasOwn.call(this, name) && !isNaN(+name.slice(1)) && (this[name] = undef);
    },
    stop: function () {
      this.done = !0;
      var rootRecord = this.tryEntries[0].completion;
      if ("throw" === rootRecord.type) throw rootRecord.arg;
      return this.rval;
    },
    dispatchException: function (exception) {
      if (this.done) throw exception;
      var context = this;
      function handle(loc, caught) {
        record.type = "throw";
        record.arg = exception;
        context.next = loc;
        if (caught) {
          context.method = "next";
          context.arg = undef;
        }
        return !!caught;
      }
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i], record = entry.completion;
        if ("root" === entry.tryLoc) return handle("end");
        if (entry.tryLoc <= this.prev) {
          var hasCatch = hasOwn.call(entry, "catchLoc"), hasFinally = hasOwn.call(entry, "finallyLoc");
          if (hasCatch && hasFinally) {
            if (this.prev < entry.catchLoc) return handle(entry.catchLoc, !0);
            if (this.prev < entry.finallyLoc) return handle(entry.finallyLoc);
          } else if (hasCatch) {
            if (this.prev < entry.catchLoc) return handle(entry.catchLoc, !0);
          } else if (hasFinally) {
            if (this.prev < entry.finallyLoc) return handle(entry.finallyLoc);
          } else throw new Error("try statement without catch or finally");
        }
      }
    },
    abrupt: function (type, arg) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];
        if (entry.tryLoc <= this.prev && hasOwn.call(entry, "finallyLoc") && this.prev < entry.finallyLoc) {
          var finallyEntry = entry;
          break;
        }
      }
      if (finallyEntry && ("break" === type || "continue" === type) && finallyEntry.tryLoc <= arg && arg <= finallyEntry.finallyLoc) finallyEntry = null;
      var record = finallyEntry ? finallyEntry.completion : {};
      record.type = type;
      record.arg = arg;
      if (finallyEntry) {
        this.method = "next";
        this.next = finallyEntry.finallyLoc;
        return ContinueSentinel;
      }
      return this.complete(record);
    },
    complete: function (record, afterLoc, fromFinally) {
      if ("throw" === record.type) throw record.arg;
      if ("break" === record.type || "continue" === record.type) {
        if (fromFinally) return this.abrupt(record.type, record.arg);
        this.next = record.arg;
      } else if ("return" === record.type) {
        this.rval = this.arg = record.arg;
        this.method = "return";
        this.next = "end";
      } else if ("normal" === record.type && afterLoc) this.next = afterLoc;
      return ContinueSentinel;
    },
    finish: function (finallyLoc) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];
        if (entry.finallyLoc === finallyLoc) {
          var record = entry.completion, copy = { type: record.type, arg: record.arg };
          resetTryEntry(entry);
          return this.complete(copy, entry.afterLoc, !0);
        }
      }
    },
    "catch": function (tryLoc) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];
        if (entry.tryLoc === tryLoc) {
          var record = entry.completion, thrown;
          if ("throw" === record.type) {
            thrown = record.arg;
            resetTryEntry(entry);
          }
          return thrown;
        }
      }
      throw new Error("illegal catch attempt");
    },
    delegateYield: function (iterable, resultName, nextLoc) {
      this.delegate = { iterator: values(iterable), resultName: resultName, nextLoc: nextLoc };
      if ("next" === this.method) this.arg = undef;
      return ContinueSentinel;
    }
  };
  return exports;
}
""";
    }
}
