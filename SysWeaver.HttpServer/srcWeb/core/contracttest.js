/**
 * @file core/contracttest.js - the core contract test (core/contracttest.html).
 *
 * Purpose: one test per documented core api (core/README "API reference"): it is called with its documented arguments (also the
 * awkward ones the README promises to handle: an Error, a Sw.Net.Error with and without a reason, a text, null) and its documented
 * return type and behaviour are asserted. Then Sw.DevOptions.Walk lists the public apis that take an options object without a
 * registered schema. Requests only go to the mocks of core/contractmock.js. Result: window.SwContractTest = { Done, Problems, Passed,
 * Missing, Blocked } (a smoke run fails on Problems.length or Missing.length).
 * The texts are test names and test data for developers, not translated (SwWeb:no-tr-file: a developer page).
 * Dependencies: the core and ui libraries, core/contractmock.js. Globals defined: window.SwContractTest.
 */
(function () {
    "use strict";

    const result = window.SwContractTest = { Done: false, Problems: [], Passed: 0, Missing: [], Blocked: [], Results: [] };
    const api = name => Sw.Url.Root + "Api/ct/" + name;

    //  ------------------------------------------------------------------ checks

    const isText = v => typeof v === "string" && v.trim().length > 0 && v.indexOf("undefined") < 0 && v.indexOf("[object") < 0 || "a non-empty text, got " + show(v);
    const isStr = v => typeof v === "string" || "a string, got " + show(v);
    const isBool = v => typeof v === "boolean" || "a boolean, got " + show(v);
    const isNum = v => typeof v === "number" && Number.isFinite(v) || "a number, got " + show(v);
    const isFn = v => typeof v === "function" || "a function, got " + show(v);
    const isArr = v => Array.isArray(v) || "an array, got " + show(v);
    const isObj = v => !!v && typeof v === "object" || "an object, got " + show(v);
    const isEl = v => v instanceof Element || "an element, got " + show(v);
    const isUndef = v => v === undefined || "nothing, got " + show(v);
    const eq = x => v => v === x || JSON.stringify(x) + ", got " + show(v);
    const has = (...keys) => v => (!!v && typeof v === "object" && keys.every(k => k in v)) || "an object with " + keys.join(", ") + ", got " + show(v);
    const abort = v => (v && v.name === "AbortError") || "an AbortError, got " + show(v);
    const all = (...cs) => v => {
        for (const c of cs) {
            const r = c(v);
            if (r !== true)
                return r;
        }
        return true;
    };

    /** @param {*} v A value. @returns {string} It, short. */
    function show(v) {
        if (v === undefined)
            return "undefined";
        if (typeof v === "function")
            return "a function";
        if (v instanceof Error)
            return v.name + ": " + v.message;
        try {
            const s = typeof v === "string" ? JSON.stringify(v) : (v instanceof Element ? "<" + v.tagName.toLowerCase() + ">" : JSON.stringify(v));
            return s && s.length > 120 ? s.substring(0, 120) + "…" : String(s);
        }
        catch {
            return String(v);
        }
    }

    /** @type {Array<{Name: string, Run: function(): *, Check: function(*): (boolean|string), Rejects: boolean}>} */
    const tests = [];

    /**
     * A test: Run's value (awaited) must pass Check.
     * @param {string} name What is tested (the README row).
     * @param {function(): *} run The call.
     * @param {function(*): (boolean|string)} check true, or what was expected.
     */
    const t = (name, run, check) => tests.push({ Name: name, Run: run, Check: check, Rejects: false });

    /**
     * A test whose call must REJECT (or throw): the error must pass Check.
     * @param {string} name What is tested.
     * @param {function(): *} run The call.
     * @param {function(*): (boolean|string)} check true, or what was expected.
     */
    const r = (name, run, check) => tests.push({ Name: name, Run: run, Check: check, Rejects: true });

    /** @param {string} name A test api. @returns {Promise<*>} The error of a Send of it. */
    const errOf = name => Sw.Net.Send(api(name), { A: 1 }, { NoNavigate: true }).then(() => null, e => e);

    const aborted = () => {
        const c = new AbortController();
        c.abort();
        return c.signal;
    };

    //  ------------------------------------------------------------------ Sw (base.js)

    t("Sw.Version → string", () => Sw.Version, isText);
    t("Sw.Log(...args) → nothing", () => Sw.Log("contract test"), isUndef);
    t("Sw.DevMode() → boolean", () => Sw.DevMode(), isBool);
    t("Sw.DevWarn(msg, key) → nothing", () => Sw.DevWarn("contract test warning", "contracttest"), isUndef);
    t("Sw.Ready(name, fn) → promise of the library", () => Sw.Ready("Net", () => null), eq(Sw.Net));
    t("Sw.Emitter On → off function, Emit → results", () => {
        const e = new Sw.Emitter();
        const off = e.On("x", a => a + 1);
        const res = e.Emit("x", 1);
        off();
        return { off, res, after: e.Emit("x", 1) };
    }, v => (typeof v.off === "function" && Array.isArray(v.res) && v.res[0] === 2 && v.after.length === 0) || "On → off, Emit → [2], after off → [], got " + show(v));
    t("Sw.Events.On → off function", () => {
        const off = Sw.Events.On("contracttest", () => 1);
        off();
        return off;
    }, isFn);

    //  Sw.DevOptions (registry)
    t("Sw.DevOptions(where, o, schema) → o", () => {
        const o = { A: 1 };
        return Sw.DevOptions("Contract.Test", o, { A: "number" }) === o;
    }, eq(true));
    t("Sw.DevOptions.Register(where, schema) → schema; Schema(where) → it; DevOptions(where, o) uses it", () => {
        const s = { Title: "string" };
        const back = Sw.DevOptions.Register("Contract.Registered", s);
        return back === s && Sw.DevOptions.Schema("Contract.Registered") === s && Sw.DevOptions.Names().includes("Contract.Registered");
    }, eq(true));
    t("Sw.DevOptions(where, o) with a registered schema warns a typo", () => {
        const warned = [];
        const w = console.warn;
        const was = Sw.DevMode;
        Sw.DevMode = () => true;
        console.warn = m => warned.push(String(m));
        try {
            Sw.DevOptions("Contract.Registered", { Titel: "x" });
        }
        finally {
            console.warn = w;
            Sw.DevMode = was;
        }
        return warned.join(" ");
    }, v => /Did you mean "Title"/.test(v) || "a warning with 'Did you mean \"Title\"', got " + show(v));
    t("Sw.DevOptions.Walk() → { Missing, Checked, Exempt }", () => Sw.DevOptions.Walk(), has("Missing", "Checked", "Exempt"));

    //  ------------------------------------------------------------------ Sw.Util

    t("Sw.Util.Delay(ms) → promise", () => Sw.Util.Delay(1).then(() => "ok"), eq("ok"));
    t("Sw.Util.DelayFrame() → promise", () => Sw.Util.DelayFrame().then(() => "ok"), eq("ok"));
    t("Sw.Util.Sleep(ms, signal) resolves", () => Sw.Util.Sleep(1).then(() => "ok"), eq("ok"));
    r("Sw.Sleep(ms, aborted signal) → throws an AbortError", () => Sw.Sleep(50, aborted()), abort);
    t("Sw.Util.DelayAbort(ms, aborted) → true", () => Sw.Util.DelayAbort(50, aborted()), eq(true));
    t("Sw.Util.WaitEvent(target, names, timeout) → null on time out", () => Sw.Util.WaitEvent(document.createElement("div"), "x", 10), eq(null));
    t("Sw.Util.Debounce(fn, ms) → function with Cancel", () => Sw.Util.Debounce(() => 1, 10).Cancel, isFn);
    t("Sw.Util.Throttle(fn, ms) → function", () => Sw.Util.Throttle(() => 1, 10), isFn);
    t("Sw.Util.Clamp(5, 0, 3) → 3", () => Sw.Util.Clamp(5, 0, 3), eq(3));
    t("Sw.Util.RandomGuid() → text", () => Sw.Util.RandomGuid(), isText);
    t("Sw.Util.RandomInt32() → number", () => Sw.Util.RandomInt32(), isNum);
    t("Sw.Util.Utf8Encode / Utf8Decode round trip", () => Sw.Util.Utf8Decode(Sw.Util.Utf8Encode("Åsa ✓")), eq("Åsa ✓"));
    t("Sw.Util.Base64EncodeString / Base64DecodeString round trip", () => Sw.Util.Base64DecodeString(Sw.Util.Base64EncodeString("Åsa ✓")), eq("Åsa ✓"));
    t("Sw.Util.ToHex(bytes) → text", () => Sw.Util.ToHex(new Uint8Array([1, 255])), eq("01ff"));
    t("Sw.Util.Deflate / Inflate round trip", async () => Sw.Util.Utf8Decode(await Sw.Util.Inflate(await Sw.Util.Deflate(Sw.Util.Utf8Encode("abcabcabc")))), eq("abcabcabc"));
    t("Sw.Util.Sha256B64(data) → text", () => Sw.Util.Sha256B64(Sw.Util.Utf8Encode("x")), isText);
    t("Sw.Util.RemoveNulls(o) removes null members in place", () => {
        const o = { A: 1, B: null, C: { D: null } };
        Sw.Util.RemoveNulls(o);
        return JSON.stringify(o);
    }, eq('{"A":1,"C":{}}'));
    t("Sw.Util.Resolve(path, ctx)", () => Sw.Util.Resolve("a.b", { a: { b: 7 } }), eq(7));

    //  ------------------------------------------------------------------ Sw.Cancel

    for (const k of ["Stopping", "Cancelled", "FinishedAnyway", "Pending", "StoppingWrite", "CancelledAfterWrite"])
        t("Sw.Cancel.Texts." + k + "() → text", () => Sw.Cancel.Texts[k](), isText);
    t("Sw.Cancel.Texts.CouldNot(why) → text", () => Sw.Cancel.Texts.CouldNot("busy"), isText);
    t("Sw.Cancel.Texts.FinishedAnywayOf(name) → text", () => Sw.Cancel.Texts.FinishedAnywayOf("Weekly sales"), isText);
    t("Sw.Cancel.Texts.ReadyNotSaved(name) → text", () => Sw.Cancel.Texts.ReadyNotSaved("report.csv"), v => (isText(v) === true && v.indexOf("report.csv") >= 0) || "a text with the name, got " + show(v));
    t("Sw.Cancel.Settle(promise) → { State: done, Value }", () => Sw.Cancel.Settle(Promise.resolve(5)), v => (v && v.State === "done" && v.Value === 5) || "done / 5, got " + show(v));
    t("Sw.Cancel.Settle(rejected abort) → cancelled", () => Sw.Cancel.Settle(Promise.reject(Sw.Cancel.Error())), v => (v && v.State === "cancelled") || "cancelled, got " + show(v));
    t("Sw.Cancel.IsAbort(Sw.Cancel.Error()) → true", () => Sw.Cancel.IsAbort(Sw.Cancel.Error()), eq(true));
    r("Sw.Cancel.Check(aborted signal) throws an AbortError", () => Sw.Cancel.Check(aborted()), abort);
    t("Sw.Cancel.Controller() → a marked controller", () => Sw.Cancel.Controller().signal.SwCancel, eq(true));
    t("Sw.Cancel.Sent(signal) → counts", () => Sw.Cancel.Sent(Sw.Cancel.Controller().signal), has("Writes", "Pending", "DoneAfterAbort"));

    //  ------------------------------------------------------------------ i18n, url, storage, format

    t("Sw.I18n.FormatArgs(format, args)", () => Sw.I18n.FormatArgs("{0}-{1}", ["a", "b"]), eq("a-b"));
    t("Sw.I18n.Format(format, ...args)", () => Sw.I18n.Format("{0}!", "x"), eq("x!"));
    t("Sw.I18n.Language() → text", () => Sw.I18n.Language(), isText);
    t("translation marker with arguments → formatted text", () => _T("{0} items", 3, "contract test"), eq("3 items"));
    t("plain translation marker → text", () => _TF("Close", "contract test"), eq("Close"));
    t("Sw.Url.Root → ends with /", () => Sw.Url.Root, v => (typeof v === "string" && v.endsWith("/")) || "an url ending with /, got " + show(v));
    t("Sw.Url.Params() → object", () => Sw.Url.Params(), isObj);
    t("Sw.Url.Has(name) → boolean", () => Sw.Url.Has("nothere"), eq(false));
    t("Sw.Url.AddParam puts the parameter before #hash", () => Sw.Url.AddParam("a.html#h", "x", "1"), v => /^a\.html\?x=1#h$/.test(v) || "a.html?x=1#h, got " + show(v));
    t("Sw.Url.SafeSameOrigin(javascript:) → null", () => Sw.Url.SafeSameOrigin("javascript:alert(1)"), eq(null));
    t("Sw.Url.SafeSameOrigin(own url) → text", () => Sw.Url.SafeSameOrigin(Sw.Url.Root + "x.html"), isText);
    t("Sw.Url.IsPageUrl(ServerManager/KillProcess) → false", () => Sw.Url.IsPageUrl(Sw.Url.Root + "ServerManager/KillProcess"), eq(false));
    t("Sw.Url.IsPageUrl(KillProcess%3F.html) → false", () => Sw.Url.IsPageUrl(Sw.Url.Root + "ServerManager/KillProcess%3F.html"), eq(false));
    t("Sw.Url.IsPageUrl(page.html) → true", () => Sw.Url.IsPageUrl(Sw.Url.Root + "app/Home.html"), eq(true));
    t("Sw.Url.IsPageUrl(Api/debug/explore) → true", () => Sw.Url.IsPageUrl(Sw.Url.Root + "Api/debug/explore"), eq(true));
    t("Sw.Url.IsPageUrl(Api/debug/GetApiInfo) → false", () => Sw.Url.IsPageUrl(Sw.Url.Root + "Api/debug/GetApiInfo"), eq(false));
    t("Sw.Url.SitePath(../Api/x) = Api/x", () => Sw.Url.SitePath("Api/debug/ApiTable"), eq("Api/debug/ApiTable"));
    t("Sw.Storage.Local.Get(missing, def) → def", () => Sw.Storage.Local.Get("SysWeaver.ContractTest.None", "d"), eq("d"));
    t("Sw.Storage.Session Set / Get / Remove", () => {
        Sw.Storage.Session.Set("SysWeaver.ContractTest", "v");
        const v = Sw.Storage.Session.Get("SysWeaver.ContractTest");
        Sw.Storage.Session.Remove("SysWeaver.ContractTest");
        return v;
    }, eq("v"));
    t("Sw.Format.Number(8.6e-7, 8) → 0.00000086", () => Sw.Format.Number(8.6e-7, 8), eq("0.00000086"));
    t("Sw.Format.Bytes(v) → text", () => Sw.Format.Bytes(12345), isText);
    t("Sw.Format.TimeStamp(date) → [tooltip, text]", () => Sw.Format.TimeStamp(new Date()), v => (Array.isArray(v) && v.length === 2) || "[tooltip, text], got " + show(v));
    t("Sw.Format.Value(type, value) → { Kind, Text, Title }", () => Sw.Format.Value("Int32", 5), has("Kind", "Text"));
    t("Sw.Format.OffsetText(120) → UTC+2", () => Sw.Format.OffsetText(120), eq("UTC+2"));
    t("Sw.Format.ZoneHint() → text", () => Sw.Format.ZoneHint(), isText);
    t("Sw.Format.ParseDate(no zone) is UTC", () => Sw.Format.ParseDate("2026-01-01T00:00:00").getTime(), eq(Date.UTC(2026, 0, 1)));
    t("Sw.Format.HtmlSafe(<) → &lt;", () => Sw.Format.HtmlSafe("<"), eq("&lt;"));

    //  ------------------------------------------------------------------ Sw.Net

    t("Sw.Net.Send(url, arg) → decoded json", () => Sw.Net.Send(api("Echo"), { A: 1 }), v => (v && v.Echo && v.Echo.A === 1) || "the echo, got " + show(v));
    t("Sw.Net.Read(url, arg) → json (ReadOnly)", () => Sw.Net.Read(api("Echo"), { A: 2 }), v => (v && v.Echo && v.Echo.A === 2) || "the echo, got " + show(v));
    t("Sw.Net.Live(url, arg) → json (Detached)", () => Sw.Net.Live(api("Echo"), { A: 3 }), v => (v && v.Echo && v.Echo.A === 3) || "the echo, got " + show(v));
    t("Sw.Net.Get(url) → json", () => Sw.Net.Get(api("Echo")), v => (v && v.Method === "GET") || "GET echo, got " + show(v));
    t("Sw.Net.Send({ Read: true }) → json (taken as ReadOnly)", () => Sw.Net.Send(api("Echo"), { A: 4 }, { Read: true }), v => (v && v.Echo && v.Echo.A === 4) || "the echo, got " + show(v));
    t("Sw.Net.IsWrite(undefined, Send) → true", () => Sw.Net.IsWrite(undefined, "Send"), eq(true));
    t("Sw.Net.IsWrite({ ReadOnly }) → false", () => Sw.Net.IsWrite({ ReadOnly: true }, "Send"), eq(false));
    t("Sw.Net.IsWrite({ AbortSafe }) → false", () => Sw.Net.IsWrite({ AbortSafe: true }), eq(false));
    t("Sw.Net.IsWrite({}, Get) → false", () => Sw.Net.IsWrite({}, "Get"), eq(false));
    t("Sw.Net.IsWrite({}, POST) → true", () => Sw.Net.IsWrite({}, "POST"), eq(true));
    t("Sw.Net.IsWrite({}, HEAD) → false", () => Sw.Net.IsWrite({}, "HEAD"), eq(false));
    t("Sw.Net.IsWrite(Request POST) → true", () => Sw.Net.IsWrite(new Request(api("Echo"), { method: "POST", body: "1" })), eq(true));
    t("Sw.Net.IsWrite(Request GET) → false", () => Sw.Net.IsWrite(new Request(api("Echo"))), eq(false));
    t("Sw.Net.IsWrite(Request marked SwWrite) → its mark", () => {
        const q = new Request(api("Echo"));
        q.SwWrite = true;
        return Sw.Net.IsWrite(q);
    }, eq(true));
    t("Sw.Net.Error → class", () => new Sw.Net.Error("x", 409) instanceof Error, eq(true));
    t("a reasoned 409 without X-Sw-Outcome: Reason, Outcome Refused (never guessed as already)", () => errOf("Refused"),
        v => (v && v.Status === 409 && v.Reason === "Process #42 is protected by policy." && v.Outcome === "Refused") || "409 / reason / Refused, got " + show(v && { Status: v.Status, Reason: v.Reason, Outcome: v.Outcome }));
    t("X-Sw-Outcome: AlreadyDone → Outcome AlreadyDone", () => errOf("Already").then(e => e && e.Outcome), eq("AlreadyDone"));
    t("X-Sw-Outcome: Gone → Outcome Gone", () => errOf("Gone").then(e => e && e.Outcome), eq("Gone"));
    t("X-Sw-Outcome: Invalid → Outcome Invalid", () => errOf("Invalid").then(e => e && e.Outcome), eq("Invalid"));
    t("a reasoned 404 without the header → Refused", () => errOf("NotFound").then(e => e && e.Outcome), eq("Refused"));
    t("X-Sw-Denied: role → RoleDenied, Outcome Denied", () => errOf("Role").then(e => e && [e.RoleDenied, e.Outcome].join()), eq("true,Denied"));
    t("a 500 → Outcome empty", () => errOf("Fail").then(e => e && e.Outcome), eq(""));
    t("Sw.Net.OutcomeKind(e) → the kind", () => errOf("Already").then(e => Sw.Net.OutcomeKind(e)), eq("AlreadyDone"));
    t("Sw.Net.OutcomeKind(new Error) → empty", () => Sw.Net.OutcomeKind(new Error("x")), eq(""));
    t("Sw.Net.OutcomeKind(null) → empty", () => Sw.Net.OutcomeKind(null), eq(""));

    //  The one composer: every documented input gives a non-empty string
    const inputs = [
        ["an Error", () => new Error("disk full")],
        ["a Sw.Net.Error with a reason", () => errOf("Refused")],
        ["a Sw.Net.Error without a reason", () => errOf("Fail")],
        ["a text", () => "Could not save: 404 - Not Found"],
        ["null", () => null],
    ];
    for (const [what, make] of inputs) {
        t("Sw.Net.FailText(name, " + what + ") → string", async () => Sw.Net.FailText("Save", await make()), isText);
        t("Sw.Net.Message(what, " + what + ") → string", async () => Sw.Net.Message("Could not save", await make()), isText);
        t("Sw.Net.MessageText(what, " + what + ") → string", async () => Sw.Net.MessageText("Could not save", await make()), isText);
        t("Sw.Net.MessageParts(what, " + what + ") → { Text, Kind, … }", async () => Sw.Net.MessageParts("Could not save", await make()), all(has("Text", "Details", "Status", "What", "Why", "Do", "Reason", "Kind"), v => isText(v.Text)));
        t("Sw.Net.ErrorText(" + what + ") → { Text }", async () => Sw.Net.ErrorText(await make()), all(has("Text", "Status", "Reason"), v => isStr(v.Text)));
    }
    t("Sw.Net.FailText keeps the server's reason", () => errOf("Refused").then(e => Sw.Net.FailText("Kill", e)), v => /^Kill: not done\. Process #42 is protected by policy\.$/.test(v) || "Kill: not done. <reason>, got " + show(v));
    t("Sw.Net.FailText(\"\", e) → Not done. …", () => Sw.Net.FailText("", new Error("disk full")), v => /^Not done\./.test(v) || "Not done. …, got " + show(v));
    t("Sw.Net.Message(e, name) swapped → still the right text", () => Sw.Net.Message(new Error("Name already used"), "Rename"), v => /^Rename: Name already used\.?$/.test(v) || "Rename: Name already used., got " + show(v));
    t("Sw.Net.Message string value of MessageParts = Text", () => {
        const p = Sw.Net.MessageParts("Save", new Error("x"));
        return String(p) === p.Text && ("" + p) === p.Text;
    }, eq(true));
    t("Sw.Net.Compose(what, why, todo) → text", () => Sw.Net.Compose("Withdraw: not done.", "it was used", "try again"), eq("Withdraw: not done. It was used. Try again."));
    t("Sw.Net.Compose never repeats advice that what already says", () => Sw.Net.Compose("OpsVictim is no longer listed. Nothing was sent.", "", "Nothing was sent."), eq("OpsVictim is no longer listed. Nothing was sent."));
    t("Sw.Net.ReasonOf(body) → the reason", () => Sw.Net.ReasonOf("The session has already ended."), eq("The session has already ended."));
    t("Sw.Net.ReasonOf(html) → empty", () => Sw.Net.ReasonOf("<html><body>x</body></html>"), eq(""));
    t("Sw.Net.ShouldReport(AbortError) → false", () => Sw.Net.ShouldReport(Sw.Cancel.Error()), eq(false));
    t("Sw.Net.ShouldReport(server error) → true", () => errOf("Refused").then(e => Sw.Net.ShouldReport(e)), eq(true));
    t("Sw.Net.ShouldReportAsync(e) → promise of boolean", () => Sw.Net.ShouldReportAsync(new Error("x")), eq(true));
    t("Sw.Net.IsNetworkError(TypeError) → true", () => Sw.Net.IsNetworkError(Object.assign(new TypeError("Failed to fetch"), { Status: 0 })), eq(true));
    t("Sw.Net.Online → boolean", () => Sw.Net.Online, isBool);
    t("Sw.Net.OfflineSince → number", () => Sw.Net.OfflineSince, isNum);
    //  (behavioural, S6-Ops-1: Online / OfflineSince are LIVE getters, not values copied once by Object.assign)
    t("Sw.Net.Online / OfflineSince are live getters (offline → false / a time, back → true / 0)", () => {
        const d = Object.getOwnPropertyDescriptor(Sw.Net, "Online");
        const d2 = Object.getOwnPropertyDescriptor(Sw.Net, "OfflineSince");
        if (!d || typeof d.get !== "function" || !d2 || typeof d2.get !== "function")
            return "accessors";
        Sw.Net.SyncState(false);
        const off = [Sw.Net.Online, Sw.Net.OfflineSince > 0];
        Sw.Net.SyncState(true);
        const on = [Sw.Net.Online, Sw.Net.OfflineSince];
        return JSON.stringify([off, on]);
    }, eq("[[false,true],[true,0]]"));
    t("Sw.Net.WaitOnline(ms) → true while online", () => Sw.Net.WaitOnline(100), eq(true));
    t("Sw.Net.ToTypedJson($type first)", () => Sw.Net.ToTypedJson({ A: 1 }, "T"), eq('{"$type":"T","A":1}'));
    t("Sw.Net.Api(path) → Root + Api/ + path", () => Sw.Net.Api("x/y"), eq(Sw.Url.Root + "Api/x/y"));
    t("Sw.Net.Cached(name, key, ms, fn) memoizes", async () => {
        let n = 0;
        const f = () => Promise.resolve(++n);
        await Sw.Net.Cached("ct", 1, 10000, f);
        return Sw.Net.Cached("ct", 1, 10000, f);
    }, eq(1));
    //  SwWeb:allow-online (nothing is requested: the test checks that another site is refused)
    t("Sw.Net.IncludeJs of another site → refused, nothing requested", () => Sw.Net.IncludeJs(null, "https://example.com/x.js", true), v => (v && typeof v.error === "string" && v.loaded === false) || "{ error }, got " + show(v));

    //  Latest wins: the slow older answer never replaces the newer one
    t("Sw.Net.Latest(key).Read: an older answer is never delivered", async () => {
        const q = Sw.Net.Latest("contracttest.search");
        const shown = [];
        const a = q.Read(api("Slow"), { Q: "a", Ms: 500 }).then(v => shown.push(v.Q), e => shown.push(e.name + (e.SwSuperseded ? ":superseded" : "")));
        await Sw.Util.Delay(30);
        const b = q.Read(api("Slow"), { Q: "ab", Ms: 50 }).then(v => shown.push(v.Q), e => shown.push(e.name));
        await Promise.all([a, b]);
        return shown.join(",");
    }, eq("AbortError:superseded,ab"));
    t("Sw.Net.Latest(key).Send: an older WRITE is not aborted, its answer is dropped", async () => {
        const q = Sw.Net.Latest("contracttest.write");
        const shown = [];
        const a = q.Send(api("Slow"), { Q: "w1", Ms: 300 }).then(v => shown.push(v.Q), e => shown.push(e.name));
        await Sw.Util.Delay(30);
        const b = q.Send(api("Slow"), { Q: "w2", Ms: 30 }).then(v => shown.push(v.Q), e => shown.push(e.name));
        await Promise.all([a, b]);
        //  (the mock saw the first write answered: it was not aborted)
        return shown.join(",");
    }, eq("w2,AbortError"));
    t("Sw.Net.Latest(key) → the same sequence for the key", () => Sw.Net.Latest("contracttest.search") === Sw.Net.Latest("contracttest.search"), eq(true));
    t("Sw.Net.Latest(key).Run(fn) → its value", () => Sw.Net.Latest("contracttest.run").Run(async () => 7), eq(7));

    //  ------------------------------------------------------------------ Sw.Page

    t("Sw.Page.Target(_TOP) → _top", () => Sw.Page.Target("_TOP"), eq("_top"));
    t("Sw.Page.LinkText(text, max) gives plain text (bidi / control characters become a space)", () => Sw.Page.LinkText("a" + String.fromCharCode(0x202E) + "b \n c" + String.fromCharCode(7), 80), eq("a b c"));
    t("Sw.Page.LinkText cuts at max with an ellipsis", () => Sw.Page.LinkText("abcdefghij", 5), eq("abcd…"));
    t("Sw.Page.NameFromUrl(../Api/debug/ServicesTable) → Services", () => Sw.Page.NameFromUrl("../Api/debug/ServicesTable"), eq("Services"));
    t("Sw.Page.WorkText(name, server) → text", () => Sw.Page.WorkText("Kill devenv", "server"), isText);
    t("Sw.Page.SetDirty / IsDirty / DirtyTexts / ClearDirty", () => {
        Sw.Page.SetDirty("contracttest", "Your test text is lost.");
        const a = Sw.Page.IsDirty();
        const texts = Sw.Page.DirtyTexts();
        Sw.Page.ClearDirty("contracttest");
        return [a, Array.isArray(texts), Sw.Page.IsDirty()].join();
    }, eq("true,true,false"));
    t("Sw.Page.Announce(text) → nothing", () => Sw.Page.Announce("Contract test"), isUndef);
    t("Sw.Page.AppName() → promise of text", () => Sw.Page.AppName(), isStr);

    //  ------------------------------------------------------------------ Sw.Activity

    t("Sw.Activity.Start(o) → running job", () => {
        const j = Sw.Activity.Start({ Key: "ct.start", Title: "Contract job", Kind: "local" });
        const s = j.State;
        j.Update({ Progress: 0.5, Text: "half" });
        j.Done({ Text: "Contract job done" });
        return [s, j.State, j.EndText.Kind, typeof j.EndText.Text].join();
    }, eq("running,done,ok,string"));
    t("Sw.Activity.Track(promise, o) → thenable with the value", () => Sw.Activity.Track(Promise.resolve(3), { Title: "Contract track", Kind: "local" }), eq(3));
    t("Sw.Activity.Run(o, fn) → the value", () => Sw.Activity.Run({ Title: "Contract run", Kind: "local" }, async () => 4), eq(4));
    r("Sw.Activity.Run cancelled → rejects (AbortError, SwSaid)", () => {
        const j = Sw.Activity.Run({ Title: "Contract cancel", Kind: "local" }, signal => Sw.Sleep(2000, signal));
        setTimeout(() => j.Cancel(), 20);
        return j;
    }, e => (e && e.name === "AbortError" && e.SwSaid === true) || "AbortError + SwSaid, got " + show(e));
    r("Sw.Activity.RunServer refused start → rejects SwSaid with a shown text", () => {
        const shown = [];
        const f = Sw.Fail;
        Sw.Fail = m => shown.push(m);
        return Sw.Activity.RunServer(api("NotFound"), {}, { Title: "Contract export" }).finally(() => {
            Sw.Fail = f;
            result.RunServerShown = shown;
        });
    }, e => (e && e.SwSaid === true && result.RunServerShown.length === 1 && isText(result.RunServerShown[0]) === true && result.RunServerShown[0].indexOf("not found") > 0) || "SwSaid + one shown text with the reason, got " + show(result.RunServerShown));
    t("Sw.Activity.Find(key) → job or null", () => Sw.Activity.Find("ct.none"), eq(null));
    t("Sw.Activity.List() → array", () => Sw.Activity.List(), isArr);
    t("Sw.Activity.Counts() → { Running, Failed, Done }", () => Sw.Activity.Counts(), has("Running", "Failed", "Done"));
    t("Sw.Activity.On(fn) → off function", () => {
        const off = Sw.Activity.On(() => null);
        off();
        return off;
    }, isFn);
    t("Sw.Activity.Supported → boolean", () => Sw.Activity.Supported, isBool);

    //  ------------------------------------------------------------------ Sw.Draft

    t("Sw.Draft.Attach(field, { Context }) → handle; Clear → promise", async () => {
        const f = document.createElement("textarea");
        document.body.append(f);
        const h = Sw.Draft.Attach(f, { Context: "contracttest.notes" });
        const p = h.Clear();
        const ok = typeof h.Active === "boolean" && p && typeof p.then === "function";
        await p;
        h.Dispose();
        f.remove();
        return ok;
    }, eq(true));
    t("Sw.Draft.Attach(password field) → inactive handle", () => {
        const f = document.createElement("input");
        f.type = "password";
        document.body.append(f);
        const h = Sw.Draft.Attach(f, { Context: "contracttest.pw" });
        f.remove();
        return h.Active;
    }, eq(false));
    //  (the example key is put together at run time: a key shaped literal in the source is blocked by the secret scanners, ex: GitHub push protection)
    t("Sw.Draft.LooksSecret(sk_live_…) → true", () => Sw.Draft.LooksSecret(["sk", "live", "51HxQabcdefghijklmnopqrstuv"].join("_")), eq(true));
    t("Sw.Draft.List(prefix) → promise of array", () => Sw.Draft.List("contracttest."), isArr);

    //  ------------------------------------------------------------------ Sw.Access

    t("Sw.Access.Check(null) → true (open)", () => Sw.Access.Check(null, []), eq(true));
    t("Sw.Access.Check(\"-\") → false", () => Sw.Access.Check("-", ["admin"]), eq(false));
    t("Sw.Access.RoleText(auth) → text", () => Sw.Access.RoleText("Admin,Ops"), isText);
    t("Sw.Access.IsDenied(403) → true", () => errOf("Role").then(e => Sw.Access.IsDenied(e)), eq(true));
    t("Sw.Access.IsRoleDenied(role 403) → true", () => errOf("Role").then(e => Sw.Access.IsRoleDenied(e)), eq(true));
    t("Sw.Access.IsRoleDenied(409) → false", () => errOf("Refused").then(e => Sw.Access.IsRoleDenied(e)), eq(false));
    t("Sw.Access.NotAllowed(opts) → element", () => Sw.Access.NotAllowed({ Reason: "Contract" }), isEl);
    t("Sw.Access.NotFound(opts) → element", () => Sw.Access.NotFound({ Name: "Contract" }), isEl);

    //  ------------------------------------------------------------------ session, theme, layout

    t("Sw.Session.Subscribe(type, fn, noServer) → fn", () => {
        const fn = () => null;
        const back = Sw.Session.Subscribe("contracttest", fn, true);
        Sw.Session.Unsubscribe("contracttest", fn, true);
        return back === fn;
    }, eq(true));
    t("Sw.Session.ServerTime() → Date", () => Sw.Session.ServerTime(), v => (v instanceof Date && !isNaN(v.getTime())) || "a Date, got " + show(v));
    //  One sign out gives one "You are signed out" notice, ever: the note is taken once (a second take, a reload, finds nothing)
    t("Sw.Session.NoteSignedOut + TakeSignedOut → one notice per sign out", () => {
        Sw.Session.NoteSignedOut("here");
        const first = Sw.Session.TakeSignedOut();
        const second = Sw.Session.TakeSignedOut();
        Sw.Session.NoteSignedOut("ended");
        const ended = Sw.Session.TakeSignedOut();
        return [first && first.Kind, second, ended && ended.Kind, Sw.Session.TakeSignedOut()];
    }, v => JSON.stringify(v) === JSON.stringify(["here", null, "ended", null]) || '["here", null, "ended", null], got ' + show(v));
    t("Sw.Session.TakeSignedOut() → null for a stale note (and removes it)", () => {
        Sw.Storage.Session.SetJson("SysWeaver.SignedOut", { At: Date.now() - 60000, Kind: "here" });
        const stale = Sw.Session.TakeSignedOut();
        return [stale, Sw.Storage.Session.Get("SysWeaver.SignedOut", null)];
    }, v => JSON.stringify(v) === JSON.stringify([null, null]) || "[null, null], got " + show(v));
    t("Sw.Theme.Get() → text", () => Sw.Theme.Get(), isText);
    t("Sw.Theme.OnChange(fn) → off", () => {
        const off = Sw.Theme.OnChange(() => null);
        off();
        return off;
    }, isFn);
    t("Sw.Layout.Width → number", () => Sw.Layout.Width, isNum);

    //  ------------------------------------------------------------------ Sw.Cells row actions (S6-Final, admin N53, G47)

    //  Whether a row action is final is declared by the server: the 8th part of the action format ("final" / "", C#
    //  TableDataActionFlags.Final), on the button as data-sw-final (the data table's actionIsFinal reads it). An older format (7 parts or
    //  fewer) has none (not final). The words never decide: Force password reset, whose tool tip says "…their open sessions end", is not
    //  final, and a "Delete" without the part is not declared.
    const actionFinals = fmts => {
        const td = document.createElement("td");
        Sw.Cells.Actions(td, "tok,ann", ["Actions"].concat(fmts), "", false, "System.String", () => { });
        return [...td.querySelectorAll("button.sw-cell-action")].map(b => b.getAttribute("aria-label") + "=" + (b.getAttribute("data-sw-final") ?? "none")).join(", ");
    };
    t("Sw.Cells.Actions: data-sw-final from the server's 8th part, never from the words", () => actionFinals([
        "Force password reset|The current password stops working and their open sessions end|RowForce?{0}||||{2} must choose a new password.|",
        "Withdraw invitation|Withdraw this invitation|Cancel?{0}||||The invitation of {2} is withdrawn.|final",
        "Delete|Delete it|Delete?{0}|",
    ]), eq("Force password reset=false, Withdraw invitation=true, Delete=none"));

    //  ------------------------------------------------------------------ run

    /** Run every test, then the walker; draw the results. */
    async function run() {
        const list = document.getElementById("ctList");
        //  (a library that failed to load: every check after it would be meaningless)
        for (const e of (window.SwContractMock && window.SwContractMock.Errors) || [])
            result.Problems.push("page error: " + e);
        for (const x of tests) {
            let ok, got;
            try {
                const v = await x.Run();
                if (x.Rejects) {
                    ok = false;
                    got = "resolved " + show(v) + " (expected it to fail)";
                }
                else {
                    const c = x.Check(v);
                    ok = c === true;
                    got = ok ? "" : String(c);
                }
            }
            catch (e) {
                if (x.Rejects) {
                    const c = x.Check(e);
                    ok = c === true;
                    got = ok ? "" : String(c);
                }
                else {
                    ok = false;
                    got = "threw " + show(e);
                }
            }
            result.Results.push({ Name: x.Name, Ok: ok, Got: got });
            if (ok)
                ++result.Passed;
            else
                result.Problems.push(x.Name + ": expected " + got);
            const li = document.createElement("li");
            li.className = ok ? "ct-ok" : "ct-bad";
            li.textContent = (ok ? "ok  " : "FAIL  ") + x.Name + (ok ? "" : " — expected " + got);
            list.append(li);
        }
        //  The option-schema walker (Controls / Dialogs / Tables register theirs)
        const w = Sw.DevOptions.Walk();
        result.Missing = w.Missing.map(m => m.Name + "(" + m.Params.join(", ") + ")");
        result.Checked = w.Checked;
        result.Exempt = w.Exempt;
        document.getElementById("ctWalk").textContent = w.Checked.length + " apis have a schema, " + w.Exempt.length + " are exempt, " + w.Missing.length + " take options without a schema" + (w.Missing.length ? ":" : ".");
        const ul = document.getElementById("ctMissing");
        for (const m of result.Missing) {
            const li = document.createElement("li");
            li.className = "ct-bad";
            li.textContent = m;
            ul.append(li);
        }
        result.Blocked = (window.SwContractMock && window.SwContractMock.Blocked) || [];
        //  The README contracts (sw/readme_contracts.js, generated by "SwWeb contracts" from core / ui / shell / explore README.md)
        if (window.SwReadmeContracts) {
            const rc = window.SwReadmeContracts.Run();
            result.ReadmeRows = rc;
            result.Problems.push(...rc.Problems);
            const p = document.createElement("p");
            p.className = rc.Problems.length ? "ct-bad" : "ct-ok";
            p.textContent = "README contracts: " + rc.Passed + " documented apis checked, " + rc.Problems.length + " problems, " + rc.Skipped.length + " skipped (library not loaded)" + (rc.Problems.length ? ": " + rc.Problems.join("; ") : ".");
            const walk = document.getElementById("ctWalk");
            (walk.previousElementSibling || walk).before(p);
        }
        result.Done = true;
        const st = document.getElementById("ctState");
        st.textContent = result.Problems.length || result.Missing.length
            ? "Failed: " + result.Problems.length + " problems (" + tests.length + " contract checks), " + result.Missing.length + " apis without an option schema."
            : "Passed: " + tests.length + " contract checks, every api with options has a schema.";
        st.className = result.Problems.length || result.Missing.length ? "ct-bad" : "ct-ok";
    }

    //  After the page's own load (the core's Init ran)
    if (document.readyState === "complete")
        setTimeout(run, 50);
    else
        window.addEventListener("load", () => setTimeout(run, 50));
})();
