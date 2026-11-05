/**
 * @file core/sessiontest.js - the script of the stand-alone messaging test pages core/sessiontest.html (sw/session.js only) and
 * core/sessiontest_theme.html (sw/session.js + sw/theme.js).
 *
 * Shows the live state of the messaging core (InterOp id, master / child, server time, subscriptions, the automatic classes, the
 * requested theme, the sources that ran), logs every InterOp message, the theme requests and the layout events, and checks what such
 * a page must (and must not) get. Uses the Sw.* api only (the legacy names are checked by name).
 * Result: window.SwSessionTest = { Done, Problems: [text], State: {..} } (read by the parity tests).
 * The texts are test data (SwWeb:no-tr-file: a developer test page, English only).
 *
 * Load order: after ../sw/session.js (and ../sw/theme.js on the theme page), a classic script in head (it waits for the DOM).
 * Globals defined: SwSessionTest.
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const result = window.SwSessionTest = { Done: false, Problems: [], State: null };
    if (!Sw || !Sw.Session || !Sw.InterOp) {
        result.Problems.push("sw/session.js is not loaded");
        result.Done = true;
        return;
    }
    //  The theme page loads sw/theme.js (core/theme.js ran); the page itself never calls the theme (a messaging only page must not)
    const withTheme = /sessiontest_theme.html$/.test(location.pathname);

    /** @type {string[]} The log lines (newest last). */
    const lines = [];

    /**
     * Add a log line (time stamp + text).
     * @param {string} text The text.
     */
    function log(text) {
        const d = new Date();
        lines.push(d.toLocaleTimeString() + "." + ("00" + d.getMilliseconds()).slice(-3) + " " + text);
        if (lines.length > 400)
            lines.splice(0, lines.length - 400);
        const e = document.getElementById("stLog");
        if (e)
            e.textContent = lines.join("\n");
    }

    //  Every InterOp message (also the server messages relayed by the master: From "Server")
    Sw.InterOp.On("*", m => {
        const copy = Object.assign({}, m);
        delete copy.MagicXyz;
        log("InterOp " + JSON.stringify(copy));
    });
    Sw.Events.On("theme.request", e => log("theme.request " + JSON.stringify(e)));
    Sw.Events.On("layout.changed", l => log("layout.changed Portrait=" + l.Portrait + " Narrow=" + l.Narrow + " Width=" + l.Width));

    /** The legacy names that sw/session.js defines (checked by name). */
    const legacyFunctions = ["SessionManager", "InterOp", "InterOpBroadcast", "InterOpWindows", "SysWeaverInit", "SysWeaverIgnoreUserChanges",
        "GetServerTime", "GetServerTick", "PostAll", "PostTop", "PostParent", "ReloadAll", "getUrlParams", "isMobile", "getPlatform",
        "getMobileParams"];

    /** The theme globals: only with sw/theme.js. */
    const themeFunctions = ["getTheme", "applyTheme", "setTheme", "resetTheme", "toggleTheme", "appendTheme"];

    /**
     * The current state (shown and returned in SwSessionTest.State).
     * @returns {object}
     */
    function state() {
        const de = document.documentElement;
        const inline = Array.from(de.style).filter(n => n.startsWith("--Theme"));
        let time = null;
        try {
            time = Sw.Session.ServerTime().toISOString();
        }
        catch {
        }
        return {
            Id: Sw.InterOp.Id,
            IsMaster: Sw.Session.IsMaster,
            IsTop: Sw.Session.IsTop,
            TimeOffset: Sw.Session.TimeOffset,
            ServerTime: time,
            Events: Sw.Session.Events(),
            OwnedEvents: Sw.Session.OwnedEvents(),
            Html: de.className,
            Body: document.body ? document.body.className : null,
            DataTheme: de.getAttribute("data-theme"),
            DataScheme: de.getAttribute("data-sw-scheme"),
            DataDensity: de.getAttribute("data-density"),
            InlineTheme: inline,
            RequestedTheme: Sw.Session.RequestedTheme,
            StoredTheme: Sw.Session.StoredTheme,
            ThemeRan: !!(Sw.Sources && Sw.Sources["core/theme.js"]),
            Sources: Object.assign({}, Sw.Sources || {}),
            Legacy: legacyFunctions.filter(n => typeof window[n] !== "function"),
            ThemeGlobals: themeFunctions.filter(n => typeof window[n] === "function"),
            Styles: Array.from(document.querySelectorAll("link[rel~='stylesheet']")).map(l => l.getAttribute("href")),
            Visibility: getComputedStyle(de).visibility,
        };
    }

    /** Show the state. */
    function show() {
        const s = result.State = state();
        const e = document.getElementById("stState");
        if (e)
            e.textContent = JSON.stringify(s, null, 2);
    }

    /** Check what a sw/session.js (+ sw/theme.js) page must get. */
    function check() {
        const p = result.Problems;
        const s = result.State = state();
        if (s.Legacy.length)
            p.push("missing legacy names: " + s.Legacy.join(", "));
        if (!/\bSw(Desktop|Mobile)\b/.test(s.Html) || !/\bSw(Portrait|Landscape)\b/.test(s.Html) || !/\bSw(Narrow|Wide)\b/.test(s.Html))
            p.push("missing automatic classes on html: " + s.Html);
        if (!/\bSw(Desktop|Mobile)\b/.test(s.Body || "") || !/\bSwSelf(Portrait|Landscape)\b/.test(s.Body || ""))
            p.push("missing automatic classes on body: " + s.Body);
        if (typeof s.Id !== "number")
            p.push("no InterOp id");
        if (s.Visibility !== "visible")
            p.push("the page is hidden: " + s.Visibility);
        if (withTheme) {
            if (s.ThemeGlobals.length !== themeFunctions.length)
                p.push("missing theme globals: " + themeFunctions.filter(n => s.ThemeGlobals.indexOf(n) < 0).join(", "));
            if (!s.DataScheme)
                p.push("sw/theme.js applied no theme (no data-sw-scheme)");
        }
        else {
            if (s.ThemeGlobals.length)
                p.push("theme globals without sw/theme.js: " + s.ThemeGlobals.join(", "));
            if (s.DataTheme !== null || s.DataScheme !== null || s.DataDensity !== null)
                p.push("theme attributes without sw/theme.js: data-theme=" + s.DataTheme + " data-sw-scheme=" + s.DataScheme + " data-density=" + s.DataDensity);
            if (s.InlineTheme.length)
                p.push("inline theme variables without sw/theme.js: " + s.InlineTheme.join(", "));
            if (s.ThemeRan)
                p.push("core/theme.js ran without sw/theme.js");
        }
        if (s.Styles.some(h => /(app\/app|common\/theme|sw\/ui)\.css/.test(h || "")))
            p.push("framework style sheets were added: " + s.Styles.join(", "));
        const e = document.getElementById("stChecks");
        if (e)
            e.textContent = p.length ? "Problems: " + p.join(" | ") : "All checks passed (" + (withTheme ? "sw/session.js + sw/theme.js" : "sw/session.js only") + ").";
        result.Done = true;
    }

    /** @type {function(object):void} The test.message handler (Subscribe / Unsubscribe). */
    const onTest = m => log("test.message handler: " + JSON.stringify(m.Value));

    /** Wire the buttons, start the state display, check after the page loaded. */
    function start() {
        const click = (id, fn) => document.getElementById(id).addEventListener("click", fn);
        click("stSubscribe", () => {
            Sw.Session.Subscribe("test.message", onTest);
            log("subscribed test.message");
        });
        click("stUnsubscribe", () => {
            Sw.Session.Unsubscribe("test.message", onTest);
            log("unsubscribed test.message");
        });
        click("stHello", () => Sw.InterOp.Post("Test.Hello", { Text: "hello from " + Sw.InterOp.Id }));
        click("stPause", () => {
            //  This window only (the core's block event, as Sw.Session emits it for "server.pause"): the fallback overlay of sw/session.js
            Sw.Events.Emit("server.block", { Type: "server.pause", Title: "Server is paused.", Text: "Waiting for server to resume.", Icon: "IconPaused", Time: new Date() });
            setTimeout(() => Sw.Events.Emit("server.unblock", { Type: "server.continue" }), 3000);
        });
        click("stClear", () => {
            lines.length = 0;
            log("log cleared");
        });
        ["SwOrientation", "SwLayout", "SwSelfOrientation", "SwSelfLayout"].forEach(n => document.body.addEventListener(n, e => log("body event " + n + " " + e.detail)));
        show();
        setInterval(show, 500);
        const done = () => setTimeout(check, 300);
        if (document.readyState === "complete")
            done();
        else
            window.addEventListener("load", done, { once: true });
    }

    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", start, { once: true });
    else
        start();
})();
