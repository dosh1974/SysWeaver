/**
 * @file core/base.js - foundation of the UI-less SysWeaver core library (namespace `Sw`).
 *
 * Purpose (extends core/kernel.js, which creates `window.Sw`, Sw.Log / Sw.DevMode / Sw.DevWarn, Sw.Emitter / Sw.Events, Sw.Platform and
 * Sw.Util.Delay / WaitEvent / Animator):
 *  - Developer guard rails (Sw.DevMode only): `Sw.DevOptions(where, o, schema)` (unknown / mistyped /
 *    colliding options of a framework api are warned with the valid ones; used by core, ui, dialogs and tables), its registry
 *    (`Sw.DevOptions.Register(where, schema)` next to each api, `NoOptions`, `Schema`, `Names`) and its walker `Sw.DevOptions.Walk()`
 *    (every public api that takes options without a schema; the test page core/contracttest.html fails on it).
 *  - `Sw.Cancel` (the one cancellation model; `Sw.Cancel.Controller()` = a signal that never aborts a sent write) and `Sw.Sleep`.
 *  - `Sw.Util`: async helpers (DelayFrame, DelayAbort, AbortHandler, Debounce, Throttle; Delay and WaitEvent are in the kernel),
 *    math/random helpers, byte helpers (base64, utf-8, hex, concat, stream read, deflate/inflate), hashing
 *    (SHA-256 and SHA-1, both with a pure JS fallback for insecure contexts) and JSON helpers.
 *  - `Sw.Ready(name, fn)` / `Sw.Extend.EditSpec` / `Sw.Extend.CellFormat`: deferred registration with a library that only some pages
 *    load (Sw.Edit, Sw.Cells, Sw.Table ...), queued until it loads; an unguarded use at the top level of app/app.js is queued too.
 *
 * Load order: after core/kernel.js (sw/core.js: kernel.js, base.js, i18n.js, url.js, storage.js, include.js, format.js, net.js,
 * interop.js, session.js, layout.js, session_start.js, theme.js, page.js, activity.js, draft.js, access.js, init.js). Works concatenated
 * (sw/core.js bundle) or loaded one by one. Dependencies: core/kernel.js.
 * Globals defined: `window.Sw` (only).
 * No visible UI is created by any core module.
 */
(function () {
    "use strict";

    /** @namespace Sw - root namespace of the SysWeaver client library (core + ui). */
    const Sw = window.Sw = window.Sw || {};

    /**
     * The type name of a value as an option check sees it ("string", "number", "boolean", "function", "array", "signal", "element",
     * "promise", "null", "object", "undefined"). Realm safe (a signal or an element of another frame counts).
     * @param {*} v The value.
     * @returns {string} The type name.
     */
    function optType(v) {
        if (v === null)
            return "null";
        if (Array.isArray(v))
            return "array";
        const t = typeof v;
        if (t !== "object")
            return t;
        if (typeof v.aborted === "boolean" && typeof v.addEventListener === "function")
            return "signal";
        if (typeof v.nodeType === "number" && typeof v.nodeName === "string")
            return "element";
        if (typeof v.then === "function")
            return "promise";
        return "object";
    }

    /**
     * The distance of two option names (case insensitive edit distance, for "did you mean").
     * @param {string} a A name.
     * @param {string} b A name.
     * @returns {number} The distance.
     */
    function nameDistance(a, b) {
        a = a.toLowerCase();
        b = b.toLowerCase();
        if (a === b)
            return 0;
        const d = [];
        for (let i = 0; i <= a.length; ++i)
            d[i] = [i];
        for (let j = 1; j <= b.length; ++j)
            d[0][j] = j;
        for (let i = 1; i <= a.length; ++i)
            for (let j = 1; j <= b.length; ++j)
                d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
        return d[a.length][b.length];
    }

    /**
     * Check the options object of a framework api in developer mode (Sw.DevMode): an unknown option name (a typo, an option of another
     * api), a value of the wrong type, a value out of range, or an option that collides with another meaning (ex: Read: true on
     * Sw.Net.Send, where Read is the body reader function) is a Sw.DevWarn that names the api, the option, what was given and the valid
     * options ("did you mean …"). Nothing in production; never throws; returns o unchanged. The one helper for core, ui, dialogs and
     * tables (the right option is easy to find, a wrong one is never silently ignored).
     * Schema: { Name: spec }, spec = a type name ("string", "number", "boolean", "function", "object", "array", "signal", "element",
     * "promise", "null", "any"), several ("boolean|string" or ["boolean", "string"]), or { Type, Collides, Min, Max, Values, Check, Hint,
     * Deprecated }: Collides = the explanation shown when the value has another type (the name means something else there); Min / Max
     * for numbers; Values = the allowed values; Check(value, o) -> "" or a message; Hint = added to every warning of the option;
     * Deprecated = a warning whenever the option is used. A schema key "$Check": function(o) -> messages (options that collide with each
     * other). Undefined values are never reported (an option left out). Option names starting with "_" are private and not checked.
     * @param {string} where The api ("Sw.Net.Send").
     * @param {object} o The options (null / undefined: nothing to check).
     * @param {Object<string, (string|Array<string>|object)>} schema The valid options.
     * @returns {object} o.
     */
    Sw.DevOptions = function (where, o, schema) {
        try {
            if (schema === undefined)
                schema = optionSchemas.get(String(where)) || null;
            if (!o || typeof o !== "object" || !schema || !(typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode))
                return o;
            const names = Object.keys(schema).filter(n => n[0] !== "$");
            const valid = () => names.join(", ");
            //  Checks of the whole object ($Check(o) -> messages: options that collide with each other)
            if (typeof schema.$Check === "function") {
                const ms = [].concat(schema.$Check(o) || []).filter(Boolean);
                for (const m of ms)
                    Sw.DevWarn(where + ": " + m, "opt:" + where + ":$:" + m);
            }
            for (const k of Object.keys(o)) {
                const v = o[k];
                if (v === undefined || k[0] === "_")
                    continue;
                const spec = schema[k];
                if (spec === undefined) {
                    let best = null, bd = 99;
                    for (const n of names) {
                        const dd = nameDistance(k, n);
                        if (dd < bd) {
                            bd = dd;
                            best = n;
                        }
                    }
                    const hint = best && (bd <= 2 || best.toLowerCase().includes(k.toLowerCase()) || k.toLowerCase().includes(best.toLowerCase())) ? ' Did you mean "' + best + '"?' : "";
                    Sw.DevWarn(where + ': unknown option "' + k + '" (it is ignored).' + hint + " Valid options: " + valid() + ".", "opt:" + where + ":" + k);
                    continue;
                }
                const sp = typeof spec === "string" || Array.isArray(spec) ? { Type: spec } : (spec || {});
                //  ("boolean|string" is the same as ["boolean", "string"])
                const types = sp.Type === undefined ? ["any"] : (Array.isArray(sp.Type) ? sp.Type : String(sp.Type).split("|"));
                const got = optType(v);
                const ok = types.includes("any") || types.includes(got) || (got === "array" && types.includes("object")) || (got === "signal" && types.includes("object"))
                    || (got === "element" && types.includes("object")) || (got === "promise" && types.includes("object"));
                const shown = got === "string" ? JSON.stringify(v.length > 40 ? v.substring(0, 40) + "…" : v) : (got === "number" || got === "boolean" ? String(v) : got);
                const extra = sp.Hint ? " " + sp.Hint : "";
                if (!ok) {
                    Sw.DevWarn(where + ": option " + k + " has the wrong type: expected " + types.join(" or ") + ", got " + shown + "." + (sp.Collides ? " " + sp.Collides : "") + extra, "opt:" + where + ":" + k + ":type");
                    continue;
                }
                if (sp.Deprecated)
                    Sw.DevWarn(where + ": option " + k + " is deprecated: " + sp.Deprecated + extra, "opt:" + where + ":" + k + ":old");
                if (got === "number" && ((typeof sp.Min === "number" && v < sp.Min) || (typeof sp.Max === "number" && v > sp.Max) || !Number.isFinite(v)))
                    Sw.DevWarn(where + ": option " + k + " must be a finite number" + (typeof sp.Min === "number" ? " from " + sp.Min : "") + (typeof sp.Max === "number" ? " to " + sp.Max : "") + ", got " + shown + "." + extra, "opt:" + where + ":" + k + ":range");
                if (Array.isArray(sp.Values) && !sp.Values.includes(v))
                    Sw.DevWarn(where + ": option " + k + " must be one of " + sp.Values.map(x => JSON.stringify(x)).join(", ") + ", got " + shown + "." + extra, "opt:" + where + ":" + k + ":value");
                if (typeof sp.Check === "function") {
                    const m = sp.Check(v, o);
                    if (m)
                        Sw.DevWarn(where + ": option " + k + ": " + m + extra, "opt:" + where + ":" + k + ":check");
                }
            }
        }
        catch {
            //  (a check never breaks the api it checks)
        }
        return o;
    };

    /** @type {Map<string, object>} The registered option schemas by api name ("Sw.Card"): Sw.DevOptions.Register. */
    const optionSchemas = new Map();

    /** @type {Map<string, string>} The apis that take no options object (or are internal), with the reason: Sw.DevOptions.NoOptions. */
    const noOptions = new Map();

    /**
     * Register the option schema of a public api, next to the api (the one registry: Sw.DevOptions(where, o) then needs no schema
     * argument, and the walker of the test page core/optionstest.html knows the api is checked). A later Register replaces it.
     * @param {string} where The api's path name ("Sw.Card", "Sw.Net.Send", "Sw.Form field").
     * @param {object} schema The schema (see Sw.DevOptions).
     * @returns {object} The schema.
     */
    Sw.DevOptions.Register = (where, schema) => {
        if (where && schema && typeof schema === "object")
            optionSchemas.set(String(where), schema);
        return schema;
    };

    /**
     * Say that an api has no options object to check (it only looks like it to the walker: an "o" that is a value, an internal
     * helper). The walker lists it as Exempt with the reason.
     * @param {string} where The api's path name.
     * @param {string} why Why (for the test page).
     */
    Sw.DevOptions.NoOptions = (where, why) => {
        if (where)
            noOptions.set(String(where), String(why || ""));
    };

    /**
     * The registered schema of an api.
     * @param {string} where The api's path name.
     * @returns {object|null} The schema, null when none is registered.
     */
    Sw.DevOptions.Schema = where => optionSchemas.get(String(where)) || null;

    /** @returns {Array<string>} The names of the apis with a registered schema. */
    Sw.DevOptions.Names = () => [...optionSchemas.keys()].sort();

    /** The parameter names that hold an options object (the walker's rule). */
    const OptionParam = /^(?:o|opts|options|opt|props|cfg|config|settings|params)$/;

    /**
     * The parameter list of a function or class (its constructor) as names; a destructured parameter is "{}".
     * @param {Function} fn The function.
     * @returns {Array<string>} The names ([] when it can't be read: native code).
     */
    function paramsOf(fn) {
        let s;
        try {
            s = Function.prototype.toString.call(fn);
        }
        catch {
            return [];
        }
        if (/\{\s*\[native code\]\s*\}\s*$/.test(s))
            return [];
        if (/^class\b/.test(s)) {
            const m = /\bconstructor\s*\(([^)]*)\)/.exec(s);
            return m ? split(m[1]) : [];
        }
        //  "async x => …", "(a, b) => …", "function name(a, b) {", "name(a, b) {" (a method)
        const arrow = /^(?:async\s+)?([A-Za-z_$][\w$]*)\s*=>/.exec(s);
        if (arrow)
            return [arrow[1]];
        const i = s.indexOf("(");
        if (i < 0)
            return [];
        let depth = 0, j = i;
        for (; j < s.length; ++j) {
            const c = s[j];
            if (c === "(" || c === "{" || c === "[")
                ++depth;
            else if (c === ")" || c === "}" || c === "]") {
                if (--depth === 0)
                    break;
            }
        }
        return split(s.substring(i + 1, j));
        /** @param {string} p A parameter list. @returns {Array<string>} The names. */
        function split(p) {
            const out = [];
            let d = 0, cur = "";
            for (const c of p) {
                if (c === "{" || c === "[" || c === "(")
                    ++d;
                else if (c === "}" || c === "]" || c === ")")
                    --d;
                if (c === "," && d === 0) {
                    out.push(cur);
                    cur = "";
                }
                else
                    cur += c;
            }
            if (cur.trim())
                out.push(cur);
            return out.map(x => {
                x = x.replace(/\/\*[\s\S]*?\*\//g, "").trim();
                if (x[0] === "{")
                    return "{}";
                return x.replace(/^\.\.\./, "").split("=")[0].trim();
            }).filter(Boolean);
        }
    }

    /**
     * Walk the public api (the Sw namespace, 3 levels; public names start with an upper-case letter) and report every function / class
     * that takes an options object (a parameter named o / opts / options / opt / props / cfg / config / settings / params, or a
     * destructured one) without a registered schema (Sw.DevOptions.Register) and not exempt (Sw.DevOptions.NoOptions). The test page
     * core/optionstest.html fails on Missing (guard rail: every api checks its options, by mechanism, not by list).
     * @param {object} [root=Sw] The namespace.
     * @param {string} [name="Sw"] Its name.
     * @returns {{Missing: Array<{Name: string, Params: Array<string>}>, Checked: Array<string>, Exempt: Array<{Name: string, Why: string}>}}
     */
    Sw.DevOptions.Walk = (root, name) => {
        root = root || Sw;
        name = name || "Sw";
        const out = { Missing: [], Checked: [], Exempt: [] };
        const seen = new Set();
        /** @type {Map<Function, Array<string>>} Every api function and its path names (an alias: MessageText = Message). */
        const fns = new Map();
        const visit = (obj, path, depth) => {
            if (!obj || (typeof obj !== "object" && typeof obj !== "function") || seen.has(obj) || depth > 3)
                return;
            seen.add(obj);
            let keys;
            try {
                keys = Object.getOwnPropertyNames(obj);
            }
            catch {
                return;
            }
            for (const k of keys) {
                if (!/^[A-Z]/.test(k))
                    continue;
                let v;
                try {
                    const d = Object.getOwnPropertyDescriptor(obj, k);
                    //  (a getter is a value, not an api: never called here)
                    if (!d || d.get || d.set)
                        continue;
                    v = d.value;
                }
                catch {
                    continue;
                }
                const p = path + "." + k;
                if (typeof v === "function") {
                    if (!fns.has(v))
                        fns.set(v, []);
                    fns.get(v).push(p);
                    //  (static members of a class / function namespace: Sw.Dialog.Confirm)
                    visit(v, p, depth + 1);
                }
                else if (v && typeof v === "object" && !Array.isArray(v) && !(typeof Node === "function" && v instanceof Node) && v !== window)
                    visit(v, p, depth + 1);
            }
        };
        visit(root, name, 0);
        for (const [fn, paths] of fns) {
            const params = paramsOf(fn);
            if (!params.some(x => x === "{}" || OptionParam.test(x)))
                continue;
            const reg = paths.find(p => optionSchemas.has(p));
            const ex = paths.find(p => noOptions.has(p));
            if (reg)
                out.Checked.push(reg);
            else if (ex)
                out.Exempt.push({ Name: ex, Why: noOptions.get(ex) });
            else
                out.Missing.push({ Name: paths[0], Params: params, Aliases: paths.slice(1) });
        }
        out.Missing.sort((a, b) => a.Name.localeCompare(b.Name));
        out.Checked.sort();
        return out;
    };

    // ------------------------------------------------------------------ Async helpers

    /**
     * Minimal re-usable abort signal: listeners registered with On are called when Raise is called (may be
     * raised several times). Accepted by Sw.Util.DelayAbort (as is a standard AbortSignal).
     */
    class AbortHandler {

        /** Creates the handler (backed by an EventTarget, no DOM element). */
        constructor() {
            /** @type {EventTarget} Event target used for the listeners (private). */
            this.Target = new EventTarget();
        }

        /** Call all registered listeners synchronously. */
        Raise() {
            this.Target.dispatchEvent(new Event("AbortHandlerEvent"));
        }

        /** @param {function(Event):void} fn - Listener to add. */
        On(fn) {
            this.Target.addEventListener("AbortHandlerEvent", fn);
        }

        /** @param {function(Event):void} fn - Listener previously added with On. */
        Off(fn) {
            this.Target.removeEventListener("AbortHandlerEvent", fn);
        }

        /** Legacy alias of Raise (old member name). */
        raise() {
            this.Raise();
        }

        /** Legacy alias of On (old member name). @param {function(Event):void} fn - Listener. */
        addListener(fn) {
            this.On(fn);
        }

        /** Legacy alias of Off (old member name). @param {function(Event):void} fn - Listener. */
        removeListener(fn) {
            this.Off(fn);
        }
    }

    /** Hex digits used by ToHex. */
    const hexDigits = "0123456789abcdef";

    /** @type {function(Uint8Array):Uint8Array|null} Lazily created pure JS SHA-256 (see legacySha256). */
    let legacySha256Fn = null;

    /**
     * Pure JavaScript SHA-256, used when crypto.subtle is unavailable (e.g. pages served over plain HTTP).
     * The implementation (round constants etc.) is created on first use.
     * @param {Uint8Array} data - Data to hash.
     * @returns {Uint8Array} The 32 byte digest.
     */
    function legacySha256(data) {
        if (!legacySha256Fn) {
            const littleEndian = !!new Uint8Array(new Uint32Array([1]).buffer)[0];
            /** Swap to big endian on little endian systems. @param {number} w - 32-bit word. @returns {number} */
            const convertEndian = w => littleEndian ? ((w >>> 24) | (((w >>> 16) & 0xff) << 8) | ((w & 0xff00) << 8) | (w << 24)) : w;
            /** Rotate right. @param {number} w - Word. @param {number} b - Bits. @returns {number} */
            const rightRotate = (w, b) => (w >>> b) | (w << (32 - b));
            /** First 32 bits of the fractional part. @param {number} n - Real number. @returns {number} */
            const frac = n => ((n - (n | 0)) * Math.pow(2, 32)) | 0;
            const defaultState = new Uint32Array(8);
            const roundConstants = [];
            const m = new Uint32Array(64);
            for (let n = 2, nPrime = 0; nPrime < 64; ++n) {
                let isPrime = true;
                for (let f = 2; f <= n / 2; ++f)
                    if (n % f === 0)
                        isPrime = false;
                if (isPrime) {
                    if (nPrime < 8)
                        defaultState[nPrime] = frac(Math.pow(n, 1 / 2));
                    roundConstants[nPrime] = frac(Math.pow(n, 1 / 3));
                    ++nPrime;
                }
            }
            legacySha256Fn = input => {
                const state = defaultState.slice();
                const len = input.length;
                const bitLength = len * 8;
                const newBitLength = (512 - ((bitLength + 64) % 512) - 1) + bitLength + 65;
                const bytes = new Uint8Array(newBitLength / 8);
                const words = new Uint32Array(bytes.buffer);
                bytes.set(input, 0);
                bytes[len] = 0x80;
                words[words.length - 1] = convertEndian(bitLength);
                for (let block = 0; block < newBitLength / 32; block += 16) {
                    const w = state.slice();
                    for (let round = 0; round < 64; ++round) {
                        let mr;
                        if (round < 16) {
                            mr = convertEndian(words[block + round]);
                        } else {
                            const g0 = m[round - 15];
                            const g1 = m[round - 2];
                            mr = m[round - 7] + m[round - 16] +
                                (rightRotate(g0, 7) ^ rightRotate(g0, 18) ^ (g0 >>> 3)) +
                                (rightRotate(g1, 17) ^ rightRotate(g1, 19) ^ (g1 >>> 10));
                        }
                        m[round] = mr |= 0;
                        const t1 = (rightRotate(w[4], 6) ^ rightRotate(w[4], 11) ^ rightRotate(w[4], 25)) +
                            ((w[4] & w[5]) ^ (~w[4] & w[6])) + w[7] + mr + roundConstants[round];
                        const t2 = (rightRotate(w[0], 2) ^ rightRotate(w[0], 13) ^ rightRotate(w[0], 22)) +
                            ((w[0] & w[1]) ^ (w[2] & (w[0] ^ w[1])));
                        for (let i = 7; i > 0; --i)
                            w[i] = w[i - 1];
                        w[0] = (t1 + t2) | 0;
                        w[4] = (w[4] + t1) | 0;
                    }
                    for (let i = 0; i < 8; ++i)
                        state[i] = (state[i] + w[i]) | 0;
                }
                return new Uint8Array(new Uint32Array(Array.from(state, v => convertEndian(v))).buffer);
            };
        }
        return legacySha256Fn(data);
    }

    /**
     * Pure JavaScript SHA-1 (FIPS 180-4), used when crypto.subtle is unavailable (pages served over plain HTTP from a host other than
     * localhost: not a secure context). Used by the password leak check (k-anonymity: only 5 hex chars of the digest leave the browser).
     * @param {Uint8Array} data - Data to hash.
     * @returns {Uint8Array} The 20 byte digest.
     */
    function legacySha1(data) {
        const l = data.length;
        //  Padding: 0x80, zeros, the bit length as a 64 bit big endian number; a multiple of 64 bytes
        const n = (((l + 8) >>> 6) + 1) << 4;
        const w = new Int32Array(n);
        for (let i = 0; i < l; ++i)
            w[i >>> 2] |= data[i] << (24 - ((i & 3) << 3));
        w[l >>> 2] |= 0x80 << (24 - ((l & 3) << 3));
        w[n - 2] = Math.floor(l / 0x20000000);
        w[n - 1] = (l << 3) | 0;
        let h0 = 0x67452301, h1 = 0xefcdab89 | 0, h2 = 0x98badcfe | 0, h3 = 0x10325476, h4 = 0xc3d2e1f0 | 0;
        const m = new Int32Array(80);
        for (let b = 0; b < n; b += 16) {
            for (let t = 0; t < 16; ++t)
                m[t] = w[b + t];
            for (let t = 16; t < 80; ++t) {
                const x = m[t - 3] ^ m[t - 8] ^ m[t - 14] ^ m[t - 16];
                m[t] = (x << 1) | (x >>> 31);
            }
            let a = h0, c1 = h1, c2 = h2, c3 = h3, c4 = h4;
            for (let t = 0; t < 80; ++t) {
                let f, k;
                if (t < 20) {
                    f = (c1 & c2) | (~c1 & c3);
                    k = 0x5a827999;
                }
                else if (t < 40) {
                    f = c1 ^ c2 ^ c3;
                    k = 0x6ed9eba1;
                }
                else if (t < 60) {
                    f = (c1 & c2) | (c1 & c3) | (c2 & c3);
                    k = 0x8f1bbcdc | 0;
                }
                else {
                    f = c1 ^ c2 ^ c3;
                    k = 0xca62c1d6 | 0;
                }
                const tmp = (((a << 5) | (a >>> 27)) + f + c4 + k + m[t]) | 0;
                c4 = c3;
                c3 = c2;
                c2 = (c1 << 30) | (c1 >>> 2);
                c1 = a;
                a = tmp;
            }
            h0 = (h0 + a) | 0;
            h1 = (h1 + c1) | 0;
            h2 = (h2 + c2) | 0;
            h3 = (h3 + c3) | 0;
            h4 = (h4 + c4) | 0;
        }
        const r = new Uint8Array(20);
        const hs = [h0, h1, h2, h3, h4];
        for (let i = 0; i < 20; ++i)
            r[i] = (hs[i >>> 2] >>> (24 - ((i & 3) << 3))) & 0xff;
        return r;
    }

    /**
     * Convert any binary input to a Uint8Array (no copy when possible).
     * @param {Uint8Array|ArrayBuffer|ArrayBufferView|number[]} data - Input.
     * @returns {Uint8Array}
     */
    function toBytes(data) {
        if (data instanceof Uint8Array)
            return data;
        if (data instanceof ArrayBuffer)
            return new Uint8Array(data);
        if (ArrayBuffer.isView(data))
            return new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
        return new Uint8Array(data);
    }

    /**
     * Make a standard base64 string uri safe: '-' instead of '+', '_' instead of '/' and no padding.
     * @param {string} str - Standard base64.
     * @returns {string} Base64url without padding.
     */
    function base64UrlSafe(str) {
        str = str.replaceAll('+', '-').replaceAll('/', '_');
        const l = str.indexOf('=');
        return l >= 0 ? str.substring(0, l) : str;
    }

    /**
     * Encode bytes as base64 (uses the native Uint8Array.prototype.toBase64 when available).
     * @param {Uint8Array|ArrayBuffer|ArrayBufferView} bytes - The data.
     * @param {boolean} [uriSafe] - Use the base64url alphabet without padding.
     * @returns {string}
     */
    function base64Encode(bytes, uriSafe) {
        bytes = toBytes(bytes);
        if (bytes.toBase64) {
            try {
                return uriSafe ? bytes.toBase64({ alphabet: "base64url", omitPadding: true }) : bytes.toBase64();
            }
            catch {
            }
        }
        let bin = "";
        const l = bytes.length;
        const chunk = 0x8000;
        for (let i = 0; i < l; i += chunk)
            bin += String.fromCharCode.apply(null, bytes.subarray(i, Math.min(i + chunk, l)));
        const s = btoa(bin);
        return uriSafe ? base64UrlSafe(s) : s;
    }

    /**
     * Decode base64 (standard or url-safe alphabet, padding optional) to bytes.
     * @param {string} str - Base64 text.
     * @returns {Uint8Array|null} The bytes (null for a falsy input; throws on invalid input).
     */
    function base64Decode(str) {
        if (!str)
            return null;
        str = ("" + str).replaceAll('-', '+').replaceAll('_', '/');
        const pad = str.length % 4;
        if (pad)
            str += "===".substring(pad - 1);
        const bin = atob(str);
        const l = bin.length;
        const r = new Uint8Array(l);
        for (let i = 0; i < l; ++i)
            r[i] = bin.charCodeAt(i);
        return r;
    }

    /**
     * Concatenate byte arrays into one Uint8Array (the single input itself when only one is given).
     * @param {Array<Uint8Array|ArrayBuffer>} arrays - Arrays to join.
     * @returns {Uint8Array}
     */
    function concatBytes(arrays) {
        const l = arrays.length;
        if (l <= 0)
            return new Uint8Array(0);
        if (l === 1)
            return toBytes(arrays[0]);
        let size = 0;
        for (let i = 0; i < l; ++i)
            size += arrays[i].byteLength;
        const r = new Uint8Array(size);
        let d = 0;
        for (let i = 0; i < l; ++i) {
            const a = toBytes(arrays[i]);
            r.set(a, d);
            d += a.length;
        }
        return r;
    }

    /**
     * Read all data from a stream.
     * @param {ReadableStream<Uint8Array>} stream - Source stream.
     * @returns {Promise<Uint8Array>} All bytes.
     */
    async function readAll(stream) {
        const parts = [];
        const reader = stream.getReader();
        for (; ;) {
            const r = await reader.read();
            if (r.done)
                break;
            parts.push(r.value);
        }
        return concatBytes(parts);
    }

    /**
     * Pipe bytes through a CompressionStream / DecompressionStream.
     * @param {Uint8Array|ArrayBuffer} data - Input bytes.
     * @param {TransformStream} transform - The (de)compression stream.
     * @returns {Promise<Uint8Array>} Output bytes.
     */
    function pipeBytes(data, transform) {
        const inp = new ReadableStream({
            /** Enqueue the whole buffer and close. @param {ReadableStreamDefaultController} c - Controller. */
            start(c) {
                c.enqueue(toBytes(data));
                c.close();
            },
        });
        return readAll(inp.pipeThrough(transform));
    }

    /**
     * SHA-256 digest (crypto.subtle, with a pure JS fallback when subtle is unavailable).
     * @param {Uint8Array|ArrayBuffer|string} data - Data (strings are UTF-8 encoded).
     * @returns {Promise<Uint8Array>} The 32 byte digest.
     */
    async function sha256(data) {
        if (typeof data === "string")
            data = new TextEncoder().encode(data);
        data = toBytes(data);
        try {
            return new Uint8Array(await crypto.subtle.digest("SHA-256", data));
        }
        catch {
            return legacySha256(data);
        }
    }

    /**
     * SHA-1 digest (crypto.subtle, with a pure JS fallback when subtle is unavailable: plain HTTP from a host other than localhost).
     * @param {Uint8Array|ArrayBuffer|string} data - Data (strings are UTF-8 encoded).
     * @returns {Promise<Uint8Array>} The 20 byte digest.
     */
    async function sha1(data) {
        if (typeof data === "string")
            data = new TextEncoder().encode(data);
        data = toBytes(data);
        try {
            return new Uint8Array(await crypto.subtle.digest("SHA-1", data));
        }
        catch {
            return legacySha1(data);
        }
    }

    /** @type {string[]|null} Hex lookup table for RandomGuid's Math.random fallback. */
    let guidLut = null;

    // ------------------------------------------------------------------ one cancellation model (UX guide POP-6, FBK-4, FBK-5)

    /**
     * An AbortError (the error fetch throws on abort): what work throws when it honours Cancel.
     * @param {*} [reason] The signal's reason (an AbortError reason is used as is).
     * @returns {Error} The error (name "AbortError").
     */
    function cancelError(reason) {
        if (reason && reason.name === "AbortError")
            return reason;
        try {
            return new DOMException("The work was cancelled", "AbortError");
        }
        catch {
            const e = new Error("The work was cancelled");
            e.name = "AbortError";
            return e;
        }
    }

    /**
     * @namespace Sw.Cancel - the one cancellation model of dialogs, buttons, the activity list (Sw.Activity.Run / Track / server jobs)
     * and server jobs. States: "running" -> (Cancel asked) "stopping" -> "cancelled" (the work CONFIRMED it stopped: it threw an
     * AbortError, the server answered "cancelled") | "done" (it finished anyway: AfterCancel) | "failed". "cancelled" is never shown
     * before the work confirmed it ("Stopping…" until then); work that ignores the signal ends "done" and says so.
     */
    const Cancel = {
        /** The states (the "stopped" end of the model is State "cancelled", the value jobs and the server use). */
        States: Object.freeze({ Running: "running", Stopping: "stopping", Cancelled: "cancelled", Done: "done", Failed: "failed" }),

        /** Create an AbortError. @param {*} [reason] The signal's reason. @returns {Error} */
        Error: cancelError,

        /**
         * Test if an error means "the work stopped because it was cancelled" (an AbortError).
         * @param {*} e The error.
         * @returns {boolean} True for an AbortError.
         */
        IsAbort: e => !!e && e.name === "AbortError",

        /**
         * Throw an AbortError when the signal is aborted (signal.throwIfAborted for every browser): call it between steps of work.
         * @param {AbortSignal} [signal] The signal.
         */
        Check(signal) {
            if (signal && signal.aborted)
                throw cancelError(signal.reason);
        },

        /**
         * The honest end of work: waits for it and tells how it ended. Never rejects.
         * @param {Promise<*>|*} work The work.
         * @param {AbortSignal} [signal] Its signal (aborted = Cancel was asked).
         * @returns {Promise<{State: string, Value: *, Error: *, AfterCancel: boolean}>} State "done" (AfterCancel: Cancel was asked but
         * the work finished), "cancelled" (it threw an AbortError: it stopped), "failed".
         */
        async Settle(work, signal) {
            try {
                const v = await work;
                return { State: "done", Value: v, Error: null, AfterCancel: !!(signal && signal.aborted) };
            }
            catch (e) {
                //  (a job that finished although Cancel was asked rejects its Finished, so nothing is delivered: still "done")
                if (e && e.FinishedAnyway === true)
                    return { State: "done", Value: e.Value, Error: null, AfterCancel: true };
                if (Cancel.IsAbort(e))
                    return { State: "cancelled", Value: undefined, Error: e, AfterCancel: true };
                return { State: "failed", Value: undefined, Error: e, AfterCancel: !!(signal && signal.aborted) };
            }
        },

        /** The texts of the model (the same words everywhere). */
        Texts: {
            /** @returns {string} "Stopping…" (Cancel asked, the work did not return yet). */
            Stopping: () => _TF("Stopping…", "State of running work after the user chose Cancel, until the work confirms that it stopped"),
            /** @returns {string} "Cancelled" (the work confirmed it stopped). */
            Cancelled: () => _TF("Cancelled", "State of work that stopped after the user chose Cancel"),
            /** @returns {string} The work finished although Cancel was asked. */
            FinishedAnyway: () => _TF("Finished before it could stop", "State of work that finished although the user chose Cancel (it could not stop in time)"),
            /** @param {*} why The reason. @returns {string} Cancel was refused / failed. */
            CouldNot: why => why
                ? _T("Could not cancel: {0}", why, "State of running work when Cancel failed.{0} is replaced with the reason")
                : _TF("Could not cancel: the work goes on.", "State of running work when Cancel failed without a reason"),
            /** @returns {string} Cancel could not be sent (the connection is lost): it is kept and sent when the server can be reached. */
            Pending: () => _TF("Cancel not sent yet: the connection is lost. It is sent when the service can be reached.", "State of running work after the user chose Cancel while the connection to the service was lost (the cancel is sent when it is back)"),
            /** @returns {string} Cancel asked while a write was on its way: the server decides, the write is never aborted. */
            StoppingWrite: () => _TF("Stopping… (what was already sent may still be done)", "State of running work after the user chose Cancel while data it sent was still on its way to the server (it is not aborted)"),
            /** @returns {string} The work stopped, but a write it had sent before the Cancel was done. */
            CancelledAfterWrite: () => _TF("Stopped; what was sent before the Cancel was done", "State of work that stopped after the user chose Cancel, but a request it had sent before was already done on the server"),
            /**
             * @param {string} name What ran. @returns {string} The result sentence of work that finished although Cancel was asked
             * ("Weekly sales: finished before it could stop"): toasts, announcements and the list use it, never "Done: …".
             */
            FinishedAnywayOf: name => name
                ? _T("{0}: finished before it could stop", name, "Toast and announcement when work finished although the user chose Cancel.{0} is replaced with the work, ex: Weekly sales")
                : _TF("Finished before it could stop", "State of work that finished although the user chose Cancel (it could not stop in time)"),
            /**
             * @param {string} name The file. @returns {string} An export / server job whose file was ready before the Cancel reached it:
             * "report.csv was ready before it could stop; it was not saved." (the activity list, the table's export, restored history).
             */
            ReadyNotSaved: name => _T("{0} was ready before it could stop; it was not saved.", name, "Result of a server job in the activity list when the user cancelled it but the server had already finished.{0} is replaced with the file name"),
        },

        /**
         * An AbortController of the cancellation model: its signal is marked (SwCancel), so Sw.Net never aborts a WRITE that was sent with
         * it (the server may have it already: the work gets the server's answer and ends "Finished before it could stop"), while reads,
         * Sw.Sleep and requests not sent yet stop at once. Sw.Activity.Run / Track and the dialogs' / buttons' work signals use it.
         * @returns {AbortController} The controller.
         */
        Controller() {
            const ac = new AbortController();
            Cancel.Mark(ac.signal);
            return ac;
        },

        /**
         * Mark a signal as one of the cancellation model (see Controller).
         * @param {AbortSignal} signal The signal.
         * @returns {AbortSignal} The signal.
         */
        Mark(signal) {
            try {
                if (signal && typeof signal === "object") {
                    signal.SwCancel = true;
                    if (!signal.SwSent)
                        signal.SwSent = { Writes: 0, Pending: 0, DoneAfterAbort: 0 };
                }
            }
            catch {
                //  (a frozen signal: a plain signal then)
            }
            return signal;
        },

        /**
         * The writes sent with a signal of the model: { Writes (sent), Pending (on their way now), DoneAfterAbort (answered after the
         * Cancel) }, null for a signal of another kind.
         * @param {AbortSignal} signal The signal.
         * @returns {{Writes: number, Pending: number, DoneAfterAbort: number}|null} The counts.
         */
        Sent(signal) {
            return signal && signal.SwSent && typeof signal.SwSent === "object" ? signal.SwSent : null;
        },
    };
    Sw.Cancel = Cancel;

    //  A cancel that the activity list / a dialog already said (an AbortError marked SwSaid, ex: `await Sw.Activity.Run(...)` in code
    //  without a catch, cancelled from the list) is no page error: not reported as an unhandled rejection (console, error monitors)
    try {
        window.addEventListener("unhandledrejection", ev => {
            const r = ev && ev.reason;
            if (r && r.name === "AbortError" && r.SwSaid === true)
                ev.preventDefault();
        });
    }
    catch {
        //  (no window events: nothing to keep quiet)
    }

    /**
     * Wait ms, THROW an AbortError when the signal is aborted (Sw.Util.Sleep): the abort-aware delay of work that honours Cancel.
     * @param {number} ms Milliseconds.
     * @param {AbortSignal} [signal] The signal.
     * @returns {Promise<void>}
     */
    Sw.Sleep = (ms, signal) => Sw.Util.Sleep(ms, signal);

    /**
     * @namespace Sw.Util - UI-less helpers (async, bytes, hashing, json, frame loop).
     */
    Sw.Util = Object.assign(Sw.Util || {}, {

        /** @type {typeof AbortHandler} Re-usable abort signal class. */
        AbortHandler: AbortHandler,

        /**
         * Delay until the next animation frame.
         * @returns {Promise<number>} The requestAnimationFrame time stamp.
         */
        DelayFrame: () => new Promise(resolve => requestAnimationFrame(resolve)),

        /**
         * Delay execution with the option to abort.
         * @param {number} ms - Milliseconds to wait.
         * @param {AbortHandler|AbortSignal} [abort] - Aborts the wait when raised / aborted.
         * @returns {Promise<boolean>} true if aborted, false if the full delay elapsed.
         */
        DelayAbort(ms, abort) {
            return new Promise(resolve => {
                if (!abort) {
                    setTimeout(() => resolve(false), ms);
                    return;
                }
                const isSignal = (typeof AbortSignal !== "undefined") && (abort instanceof AbortSignal);
                if (isSignal && abort.aborted) {
                    resolve(true);
                    return;
                }
                /** Abort listener: stop the timer and resolve true. */
                const onAbort = () => {
                    clearTimeout(th);
                    if (isSignal)
                        abort.removeEventListener("abort", onAbort);
                    else
                        abort.Off(onAbort);
                    resolve(true);
                };
                const th = setTimeout(() => {
                    if (isSignal)
                        abort.removeEventListener("abort", onAbort);
                    else
                        abort.Off(onAbort);
                    resolve(false);
                }, ms);
                if (isSignal)
                    abort.addEventListener("abort", onAbort);
                else
                    abort.On(onAbort);
            });
        },

        /**
         * Wait, and stop at once when the signal is aborted: THROWS an AbortError (like fetch), so `await Sw.Sleep(500, signal)` in a loop
         * ends the loop on Cancel and the work reports "cancelled" honestly (Sw.Cancel). Prefer it to DelayAbort (which resolves true on
         * abort: a loop that ignores the result keeps working after Cancel).
         * @param {number} ms - Milliseconds to wait.
         * @param {AbortSignal|AbortHandler} [signal] - Aborts the wait.
         * @returns {Promise<void>} Resolves after ms; rejects with an AbortError when aborted (also when it was aborted already).
         */
        Sleep(ms, signal) {
            return new Promise((resolve, reject) => {
                //  (realm safe: a signal of another window, ex: the app shell's, counts too)
                const isSignal = !!signal && typeof signal.aborted === "boolean" && typeof signal.addEventListener === "function";
                if (isSignal && signal.aborted) {
                    reject(cancelError(signal.reason));
                    return;
                }
                const onAbort = () => {
                    clearTimeout(th);
                    reject(cancelError(isSignal ? signal.reason : undefined));
                };
                const th = setTimeout(() => {
                    if (isSignal)
                        signal.removeEventListener("abort", onAbort);
                    else if (signal && signal.Off)
                        signal.Off(onAbort);
                    resolve();
                }, ms);
                if (isSignal)
                    signal.addEventListener("abort", onAbort, { once: true });
                else if (signal && signal.On)
                    signal.On(onAbort);
            });
        },

        /**
         * Create a debounced function: calls fn once, ms after the last call (with the last arguments).
         * @param {Function} fn - Function to call.
         * @param {number} ms - Quiet time in ms.
         * @returns {Function} The debounced function (has a Cancel() member).
         */
        Debounce(fn, ms) {
            let th = null;
            /** The debounced wrapper. @param {...*} args - Arguments. */
            const f = function (...args) {
                if (th)
                    clearTimeout(th);
                th = setTimeout(() => {
                    th = null;
                    fn.apply(this, args);
                }, ms);
            };
            /** Cancel a pending call. */
            f.Cancel = () => {
                if (th)
                    clearTimeout(th);
                th = null;
            };
            return f;
        },

        /**
         * Create a throttled function: calls fn at most once every ms (leading call, plus a trailing call with
         * the last arguments if calls happened while waiting).
         * @param {Function} fn - Function to call.
         * @param {number} ms - Minimum interval in ms.
         * @returns {Function} The throttled function.
         */
        Throttle(fn, ms) {
            let last = 0;
            let th = null;
            let pending = null;
            /** The throttled wrapper. @param {...*} args - Arguments. */
            return function (...args) {
                const now = Date.now();
                const wait = last + ms - now;
                if (wait <= 0) {
                    last = now;
                    fn.apply(this, args);
                    return;
                }
                pending = args;
                if (th)
                    return;
                th = setTimeout(() => {
                    th = null;
                    last = Date.now();
                    const a = pending;
                    pending = null;
                    fn.apply(this, a);
                }, wait);
            };
        },

        /**
         * Clamp a value to a range.
         * @param {number} v - Value.
         * @param {number} min - Minimum.
         * @param {number} max - Maximum.
         * @returns {number}
         */
        Clamp: (v, min, max) => v < min ? min : (v > max ? max : v),

        /**
         * Random number in [min, max) (Math.random, not cryptographic).
         * @param {number} [min=0] - Minimum.
         * @param {number} [max=1] - Maximum (exclusive).
         * @returns {number}
         */
        Random: (min, max) => {
            min = min ?? 0;
            max = max ?? 1;
            return min + Math.random() * (max - min);
        },

        /**
         * Random signed 32-bit integer (crypto.getRandomValues, Math.random fallback).
         * @returns {number}
         */
        RandomInt32() {
            try {
                const t = new Int32Array(1);
                crypto.getRandomValues(t);
                return t[0];
            }
            catch {
            }
            return (Math.random() * 0xffffffff) | 0;
        },

        /**
         * Random GUID ("xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx"), crypto.randomUUID or a Math.random fallback
         * (randomUUID throws in insecure contexts).
         * @returns {string}
         */
        RandomGuid() {
            try {
                return crypto.randomUUID();
            }
            catch {
            }
            let lut = guidLut;
            if (!lut) {
                lut = [];
                for (let i = 0; i < 256; i++)
                    lut[i] = (i < 16 ? '0' : '') + i.toString(16);
                guidLut = lut;
            }
            const d0 = (Math.random() * 0xffffffff) | 0;
            const d1 = (Math.random() * 0xffffffff) | 0;
            const d2 = (Math.random() * 0xffffffff) | 0;
            const d3 = (Math.random() * 0xffffffff) | 0;
            return lut[d0 & 0xff] + lut[d0 >> 8 & 0xff] + lut[d0 >> 16 & 0xff] + lut[d0 >> 24 & 0xff] + '-' +
                lut[d1 & 0xff] + lut[d1 >> 8 & 0xff] + '-' + lut[d1 >> 16 & 0x0f | 0x40] + lut[d1 >> 24 & 0xff] + '-' +
                lut[d2 & 0x3f | 0x80] + lut[d2 >> 8 & 0xff] + '-' + lut[d2 >> 16 & 0xff] + lut[d2 >> 24 & 0xff] +
                lut[d3 & 0xff] + lut[d3 >> 8 & 0xff] + lut[d3 >> 16 & 0xff] + lut[d3 >> 24 & 0xff];
        },

        /** Convert binary input to a Uint8Array (see toBytes). */
        ToBytes: toBytes,

        /**
         * UTF-8 encode a string.
         * @param {string} s - Text.
         * @returns {Uint8Array}
         */
        Utf8Encode: s => new TextEncoder().encode(s),

        /**
         * UTF-8 decode bytes.
         * @param {Uint8Array|ArrayBuffer} b - Bytes.
         * @returns {string}
         */
        Utf8Decode: b => new TextDecoder().decode(toBytes(b)),

        /** Encode bytes as base64 (bytes, uriSafe) (see base64Encode). */
        Base64Encode: base64Encode,

        /** Decode standard or url-safe base64 to bytes (see base64Decode). */
        Base64Decode: base64Decode,

        /** Make a standard base64 string url safe (no padding) (see base64UrlSafe). */
        Base64UrlSafe: base64UrlSafe,

        /**
         * Base64 encode a string (UTF-8 encoded first).
         * @param {string} s - Text.
         * @returns {string} Standard, padded base64.
         */
        Base64EncodeString: s => base64Encode(new TextEncoder().encode(s)),

        /**
         * Decode base64 to a string (bytes interpreted as UTF-8).
         * @param {string} s - Base64 text (throws on invalid input).
         * @returns {string}
         */
        Base64DecodeString: s => new TextDecoder().decode(base64Decode(s) || new Uint8Array(0)),

        /**
         * Convert bytes to a lower-case hex string.
         * @param {Uint8Array|ArrayBuffer|number[]} buffer - Data.
         * @returns {string|null} Hex text (null for a falsy input).
         */
        ToHex(buffer) {
            if (!buffer)
                return null;
            const b = toBytes(buffer);
            const l = b.length;
            let r = "";
            for (let i = 0; i < l; ++i) {
                const c = b[i];
                r += hexDigits.charAt(c >> 4) + hexDigits.charAt(c & 0xf);
            }
            return r;
        },

        /**
         * Read binary data (or a Blob) as a data url, or only its base64 part.
         * @param {Blob|Uint8Array|ArrayBuffer} data - The data (non Blobs are wrapped in a Blob).
         * @param {boolean} [keepUri] - Return the full "data:...;base64,..." url.
         * @returns {Promise<string|null>} Base64 (or data url), null for a falsy input.
         */
        async ToBase64Async(data, keepUri) {
            if (!data)
                return null;
            if (!(data instanceof Blob)) {
                if (!keepUri)
                    return base64Encode(data);
                data = new Blob([data]);
            }
            const url = await new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => resolve(reader.result);
                reader.onerror = () => reject(reader.error);
                reader.readAsDataURL(data);
            });
            return keepUri ? url : url.slice(url.indexOf(',') + 1);
        },

        /** Concatenate byte arrays (see concatBytes). */
        ConcatBytes: concatBytes,

        /** Read all bytes of a ReadableStream (see readAll). */
        ReadAll: readAll,

        /**
         * Compress bytes with raw deflate (CompressionStream "deflate-raw", as expected by the server for
         * "Content-Encoding: deflate" and "_d" GET arguments).
         * @param {Uint8Array|ArrayBuffer} data - Input.
         * @returns {Promise<Uint8Array>}
         */
        Deflate: data => pipeBytes(data, new CompressionStream("deflate-raw")),

        /**
         * Decompress raw deflate data.
         * @param {Uint8Array|ArrayBuffer} data - Compressed input.
         * @returns {Promise<Uint8Array>}
         */
        Inflate: data => pipeBytes(data, new DecompressionStream("deflate-raw")),

        /** SHA-256 digest of bytes or a string (see sha256). */
        Sha256: sha256,

        /**
         * Synchronous pure JavaScript SHA-256 (no crypto.subtle; slower, but usable where a promise can't be awaited,
         * ex: the legacy global legacySha256 used by the chat name colors).
         * @param {Uint8Array|ArrayBuffer} data - Data to hash.
         * @returns {Uint8Array} The 32 byte digest.
         */
        Sha256Sync: data => legacySha256(data instanceof Uint8Array ? data : new Uint8Array(data)),

        /**
         * SHA-256 of a string or bytes as standard base64 (the login hashing primitive).
         * @param {string|Uint8Array|ArrayBuffer} data - Data (strings are UTF-8 encoded).
         * @returns {Promise<string>}
         */
        Sha256B64: async data => base64Encode(await sha256(data)),

        /**
         * Same as Sha256B64 (name kept from the old hashString).
         * @param {string} s - Text.
         * @returns {Promise<string>} Base64 SHA-256.
         */
        HashString: async s => base64Encode(await sha256(s)),

        /** SHA-1 digest (crypto.subtle, pure JS fallback outside a secure context) (see sha1). */
        Sha1: sha1,

        /**
         * SHA-1 as base64.
         * @param {string|Uint8Array|ArrayBuffer} data - Data.
         * @returns {Promise<string>}
         */
        Sha1B64: async data => base64Encode(await sha1(data)),

        /**
         * Recursively delete all properties whose value is null (in place). Arrays are traversed but null
         * elements are kept.
         * @param {*} o - Value to clean.
         * @returns {boolean} true if o itself is null.
         */
        RemoveNulls(o) {
            if (typeof o !== "object")
                return false;
            if (o === null)
                return true;
            if (Array.isArray(o)) {
                o.forEach(x => Sw.Util.RemoveNulls(x));
                return false;
            }
            const toRemove = [];
            for (const k in o)
                if (Sw.Util.RemoveNulls(o[k]))
                    toRemove.push(k);
            toRemove.forEach(k => delete o[k]);
            return false;
        },

        /**
         * Resolve a dotted path ("Sw.Net.Send") starting at a context object.
         * @param {string} path - Dotted path.
         * @param {object} [context=window] - Root object.
         * @returns {*} The value, or undefined if any part is missing.
         */
        Resolve(path, context) {
            let c = context ?? window;
            const parts = ("" + path).split(".");
            const l = parts.length;
            for (let i = 0; i < l; ++i) {
                if ((c === null) || (typeof c === "undefined"))
                    return undefined;
                c = c[parts[i]];
            }
            return c;
        },
    });

    /*
     * Deferred registration (Sw.Ready / Sw.Extend): app wide code (app/app.js) runs in pages that load a library (Sw.Edit, Sw.Table,
     * Sw.Chart ...) and in pages that do not, or load it later (a shell pop-up editor). Sw.Ready queues the code until the library
     * object is assigned (an accessor on Sw notices the assignment and runs the queue in order, then becomes a plain property again).
     * While app/app.js itself is evaluated, a missing library also reads as a recording stand-in, so an unguarded
     * `Sw.Edit.RegisterSpec(...)` / `Sw.Cells.FormatFormatters.set(...)` at the top level of app.js is queued instead of throwing and
     * stopping the rest of the file (outside of app.js a missing library is undefined, as before, so `if (Sw.Edit)` tests keep working).
     */

    /** @type {Map<string, function[]>} Library name -> callbacks waiting for it. */
    const readyQueue = new Map();

    /** @type {Map<string, object>} Library name -> its app.js stand-in (one per name). */
    const standIns = new Map();

    /** The libraries that may be missing on a page and get a stand-in while app.js runs (other names work with Sw.Ready only). */
    const DeferredLibs = ["Edit", "Cells", "Table", "Chart", "Media", "Auth"];

    /**
     * The canonical library name: "edit" -> "Edit" (the Sw member name).
     * @param {string} name The name.
     * @returns {string} The member name.
     */
    function libName(name) {
        const s = String(name || "");
        return s.charAt(0).toUpperCase() + s.slice(1);
    }

    /**
     * Test if the running script is the app script (app/app.js), at its top level.
     * @returns {boolean}
     */
    function inAppScript() {
        try {
            const s = document.currentScript;
            return !!s && /(^|\/)app\/app\.js([?#]|$)/.test(s.getAttribute("src") || "");
        }
        catch {
            return false;
        }
    }

    /**
     * Run the callbacks queued for a library (each one guarded: a failing plug-in does not stop the others).
     * @param {string} name The library name.
     * @param {object} lib The library.
     */
    function flushReady(name, lib) {
        const q = readyQueue.get(name);
        if (!q)
            return;
        readyQueue.delete(name);
        for (const fn of q) {
            try {
                fn(lib);
            }
            catch (e) {
                console.error("Sw.Ready(\"" + name + "\"): a queued registration failed:", e);
            }
        }
    }

    /**
     * A recording stand-in for a missing library (app/app.js only): member calls and assignments are replayed on the real library
     * when it loads, reads of other members return nested stand-ins, and `Ready` is a promise that resolves when the library loaded
     * (after its own Ready, if any).
     * @param {string} name The library name.
     * @param {string[]} path The member path from the library.
     * @returns {Proxy}
     */
    function standIn(name, path) {
        const resolvePath = (lib, p) => p.reduce((o, k) => (o === null || o === undefined) ? o : o[k], lib);
        return new Proxy(function () { }, {
            get(t, key) {
                if (typeof key === "symbol" || key === "then")
                    return undefined;
                if (key === "Ready" && path.length === 0)
                    return new Promise(r => queue(name, lib => r(lib.Ready && typeof lib.Ready.then === "function" ? lib.Ready.then(() => lib) : lib)));
                return standIn(name, path.concat(key));
            },
            set(t, key, value) {
                queue(name, lib => {
                    resolvePath(lib, path)[key] = value;
                });
                return true;
            },
            apply(t, self, args) {
                queue(name, lib => {
                    const owner = resolvePath(lib, path.slice(0, -1));
                    const f = owner ? owner[path[path.length - 1]] : undefined;
                    if (typeof f !== "function")
                        throw new TypeError("Sw." + name + "." + path.join(".") + " is not a function");
                    f.apply(owner, args);
                });
                return undefined;
            },
            construct() {
                throw new TypeError("Sw." + name + " is not loaded on this page: create objects in Sw.Ready(\"" + name + "\", " + name + " => ...)");
            },
        });
    }

    /**
     * Queue a callback for a library, watching Sw[name] for its assignment.
     * @param {string} name The library name.
     * @param {function(object)} fn The callback.
     */
    function queue(name, fn) {
        let q = readyQueue.get(name);
        if (!q)
            readyQueue.set(name, q = []);
        q.push(fn);
        watch(name);
    }

    /**
     * Watch Sw[name] (once): an accessor that runs the queue when the library is assigned, and gives app/app.js a stand-in meanwhile.
     * @param {string} name The library name.
     */
    function watch(name) {
        if (Object.getOwnPropertyDescriptor(Sw, name))
            return;
        Object.defineProperty(Sw, name, {
            configurable: true,
            enumerable: false,
            get() {
                if (!inAppScript())
                    return undefined;
                let s = standIns.get(name);
                if (!s)
                    standIns.set(name, s = standIn(name, []));
                return s;
            },
            set(v) {
                Object.defineProperty(Sw, name, { configurable: true, enumerable: true, writable: true, value: v });
                if (v !== undefined && v !== null)
                    flushReady(name, v);
            },
        });
    }

    /**
     * Run code once a library is loaded: at once when Sw[name] exists, else as soon as a script assigns it (in this window; a pop-up
     * editor that loads edit/Edit.js later counts too). The way for app/app.js (and any shared script) to extend a library that only
     * some pages load. Usage: `Sw.Ready("Edit", E => E.RegisterSpec("Stars", factory))`; shorthands in Sw.Extend.
     * @param {string} name The library: "Edit", "Cells", "Table", "Chart" ... (the Sw member name; "edit" works too).
     * @param {function(object)} [fn] Called with the library (an error is logged, not thrown).
     * @returns {Promise<object>} Resolves with the library after fn ran.
     */
    Sw.Ready = function (name, fn) {
        name = libName(name);
        return new Promise(resolve => {
            const run = lib => {
                try {
                    if (fn)
                        fn(lib);
                }
                catch (e) {
                    console.error("Sw.Ready(\"" + name + "\"):", e);
                }
                resolve(lib);
            };
            const d = Object.getOwnPropertyDescriptor(Sw, name);
            if (d && !d.get && d.value !== undefined && d.value !== null)
                run(d.value);
            else {
                //  A misspelled name never runs: say so to the developer (a name of an own library is fine once it loads)
                if (DeferredLibs.indexOf(name) < 0) {
                    const check = () => setTimeout(() => {
                        const v = Object.getOwnPropertyDescriptor(Sw, name);
                        if (!v || v.get)
                            Sw.DevWarn("Sw.Ready(\"" + name + "\"): no library Sw." + name + " was loaded on this page (known: " + DeferredLibs.join(", ") + "); is the name misspelled?", "ready." + name);
                    }, 2000);
                    if (document.readyState === "complete")
                        check();
                    else
                        window.addEventListener("load", check, { once: true });
                }
                queue(name, run);
            }
        });
    };

    /** Shorthands of Sw.Ready for the common extension points (safe on every page, deferred until the library loads). */
    Sw.Extend = {
        /**
         * Register an edit plug-in for a spec: Sw.Edit.RegisterSpec(spec, factory) once edit/Edit.js loaded.
         * @param {string} spec The spec name (ex: "Stars" for a member with the edit spec "Stars:5").
         * @param {function} factory The plug-in factory (edit/README.md, plug-ins).
         * @returns {Promise<object>}
         */
        EditSpec: (spec, factory) => Sw.Ready("Edit", E => E.RegisterSpec(spec, factory)),
        /**
         * Register a table / card cell format: Sw.Cells.FormatFormatters.set(name, renderer) once ui/cells.js loaded.
         * @param {string} name The format name (the part before the first ";" of a column format, ex: "Stars").
         * @param {function} renderer (e, value, formats, nextValue, flash, type, onRefresh) => e (explore/README.md "Custom cell formats").
         * @param {{Replace: boolean}} [o] Replace: true to replace a built-in format on purpose (else a developer warning says it is replaced).
         * @returns {Promise<object>}
         */
        CellFormat: (name, renderer, o) => {
            if (o)
                Sw.DevOptions("Sw.Extend.CellFormat", o);
            return Sw.Ready("Cells", C => {
                if (C.FormatFormatters.has(name) && !(o && o.Replace))
                    Sw.DevWarn("Sw.Extend.CellFormat(\"" + name + "\"): replaces the built-in format \"" + name + "\" on every page; use another name, or pass { Replace: true } when that is intended", "cellformat." + name);
                C.FormatFormatters.set(name, renderer);
            });
        },
    };

    //  The option schemas of this file (Sw.DevOptions registry) and the apis whose "o" is not an options object
    Sw.DevOptions.Register("Sw.Extend.CellFormat", { Replace: "boolean" });
    Sw.DevOptions.NoOptions("Sw.DevOptions", "the option checker itself (o is the checked object)");
    Sw.DevOptions.NoOptions("Sw.Util.RemoveNulls", "o is the value to clean");

    for (const n of DeferredLibs)
        if (Sw[n] === undefined)
            watch(n);

    // Polyfill kept from the old library (all current browsers have it)
    if (!String.prototype.replaceAll) {
        /**
         * String.prototype.replaceAll polyfill.
         * @param {string|RegExp} e - Search value (a RegExp must be global).
         * @param {string|Function} t - Replacement.
         * @returns {string}
         */
        String.prototype.replaceAll = function (e, t) {
            if (e instanceof RegExp && !e.global)
                throw new TypeError("String.prototype.replaceAll called with a non-global RegExp argument");
            return e instanceof RegExp ? this.replace(e, t) : this.split(e).join(t);
        };
    }
})();
