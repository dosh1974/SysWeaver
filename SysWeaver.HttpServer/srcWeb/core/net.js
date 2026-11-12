/**
 * @file core/net.js - requests to the SysWeaver server and dynamic includes.
 *
 * Purpose: `Sw.Net` with
 *  - `Send(url, arg, opts)`: call an api method with one argument. The argument is serialized with ToTypedJson
 *    ("$type" as the FIRST member when present, as the server requires). Encoding (same as the old sendRequest):
 *    unless POST is forced and the json is < 16384 chars: url-encoded json < 128 chars => GET url?json;
 *    otherwise the UTF-8 json is raw-deflated (used when < 7/8 of the original) and the query becomes
 *    "_d"+base64url(deflated) or "_u"+base64url(utf8) when shorter than the url-encoded json, and GET is used
 *    when that is < 4096 chars. Otherwise POST "application/json" (with "Content-Encoding: deflate" and the
 *    deflated body when compression was computed). In DEBUG builds (the blocks between the DEBUG markers are
 *    stripped by the build in release) POST is the default, unless the web server's Params "OptimizedSend" is true
 *    (published in the templated app/manifest.json as "sysweaver_optimized_send" and in the meta
 *    "sysweaver-optimized-send" of the templated index.html, read without a request): then debug builds use the
 *    release encodings too.
 *  - `Get(url, opts)`: plain GET (the url may already contain a single JSON argument after "?").
 *  - Errors: non-200 responses throw `Sw.Net.Error` (Status, StatusText, Text = response body, Reason = the server's reason for people,
 *    Url), message
 *    "status - statusText" plus the body of a 500 and the reason of any other status (a plain text body of at most 300 characters,
 *    not HTML / a stack trace: the server's HttpResponseException reason is user text by contract). Message (a STRING, alias
 *    MessageText) composes "What: Why. Do." (the reason is Why; the status sentence only without one); MessageParts / ErrorText give
 *    the parts (objects whose string value is the text). Errors carry the server's outcome kind (X-Sw-Outcome: Outcome, OutcomeKind(e):
 *    AlreadyDone | Gone | Refused | Denied | Invalid, never guessed from the status). `Latest(key)`: latest wins (an older answer is never
 *    delivered). `IsWrite(opts | request, kind)`: the one write test. `Read(url, arg)` = a Send marked ReadOnly (a Cancel may abort it); every
 *    other Send is a write, never aborted once sent by a signal of the cancellation model (Sw.Cancel.Controller). Options are checked
 *    in developer mode (Sw.DevOptions: unknown, mistyped, Read that is not a function). A 403 also
 *    has Denied = true (the user's role doesn't allow it; Sw.Access.IsDenied, the shared "not allowed" state).
 *    Every failure is also emitted as Sw.Events "net.error" {Url, Status, Error}.
 *  - Not signed in: an api call without a signed in user is answered with a 401 and the sign in page in the "X-Login-Url"
 *    header; older servers (and urls that are not apis) answer with a 302 to the sign in page (fetch follows it,
 *    so the response is "redirected"). Both mean "not signed in": the page emits "auth.redirect" {Url} and navigates to
 *    the sign in page (Sw.Page.Navigate "_self", only to a http(s) url of this site; a page framed in the app shell never leaves: the
 *    shell shows "You were signed out" over it and opens the same page again after the sign in; a SIGNED IN user is not sent there:
 *    the call throws a "not allowed" Sw.Net.Error, Status 403, Denied true) and the call does not settle while the page
 *    leaves (it never returns null); if the page is still here after 15 s it rejects with a Sw.Net.Error (Status 401 or
 *    302, SignIn true, Redirect true for a 302, "not signed in" message). opts.NoNavigate throws that error at once
 *    instead. A 401 without "X-Login-Url" (ex: a bad api key) is a plain Sw.Net.Error (Status 401).
 *  - Connection state (UX guide FBK-9), one source of truth for the app: `Online` (false while the server can't be reached: network
 *    errors, 502 / 504, the browser's offline event, a probe without an answer in 8 s). A failed request probes "serverTime" and
 *    waits for that answer (at most 3 s) before it throws, so whoever catches the error sees the right `Online` (no error toast
 *    before the state flips; the error has Offline true for a lost connection). An outage costs the original's requests (S6-Ops-3):
 *    the window whose session polls sends no probe, its poll (5 s retry) and server time sync (15 s) decide (NoteFailure(true) /
 *    NoteSuccess); a child tab follows the master's "server.error" / "server.restore" and probes every 15 s, a page without a
 *    session every 15 s (30 s hidden); one probe when a request fails while still online; no probe while another request keeps
 *    failing (ex: a live table's own 3 s retry: its answer decides). A same-origin framed page mirrors
 *    the top window's state (the app shell's) and forwards its failures there; its own Sw.Events fire too. Sw.Events
 *    "net.offline" / "net.online", `OfflineSince`, `IsNetworkError(e)`, `ShouldReport(e)` (sync; false for a lost connection: the one
 *    rule for error messages; `ShouldReportAsync(e)` waits for the check), `WaitOnline(maxMs)`, `Probe()`, `Check()`, `NoteFailure(lost)` / `NoteSuccess()` (other channels: the
 *    session poll, the server time); option `Retry: "online"` waits for the connection and sends again (reads and saves that are
 *    safe to repeat).
 *  - `Beacon`, `Download`, `DownloadText`, `ToTypedJson`.
 *  - `FlushCache`, `IncludeJs`, `IncludeCss`: defined by core/include.js (part of sw/session.js too); this file adds the rest of Sw.Net.
 *  - `Cached(name, key, lifeTimeMs, fetchFn)`, `CachedGet`, `CachedSend`: in-memory memoization.
 *
 * Load order: after core/include.js. Dependencies: Sw.Util, Sw.Url, Sw.Events; Sw.Page (at call time, redirects).
 * Globals defined: none (Sw.Net).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Error thrown by Sw.Net for non-200 responses (and for redirects when opts.NoNavigate is set).
     */
    class NetError extends Error {
        /**
         * @param {string} message - Error message.
         * @param {number} status - HTTP status (0 for network errors).
         * @param {string} [statusText] - HTTP status text.
         * @param {string} [text] - Response body text (if read).
         * @param {string} [url] - Request url (or the redirect target for a redirect).
         */
        constructor(message, status, statusText, text, url) {
            super(message);
            this.name = "NetError";
            /** @type {number} HTTP status. */
            this.Status = status;
            /** @type {string} HTTP status text. */
            this.StatusText = statusText ?? "";
            /** @type {string} Response body (text/plain error message from the server, may be empty). */
            this.Text = text ?? "";
            /** @type {string} Url. */
            this.Url = url ?? "";
        }
    }

    /**
     * Serialize to json with "$type" as the FIRST member (the server only honours it there). Uses data.$type
     * or optionalType (when data has no $type); without a type plain JSON.stringify is used.
     * @param {*} data - Value (null/undefined => "null").
     * @param {string} [optionalType] - Type name to use when data has no "$type".
     * @returns {string} Json text.
     */
    function toTypedJson(data, optionalType) {
        if (data == null)
            return "null";
        const ht = data["$type"];
        if (!optionalType)
            optionalType = ht;
        if (!optionalType)
            return JSON.stringify(data);
        if (ht) {
            if (Object.keys(data)[0] === "$type")
                return JSON.stringify(data);
            data = Object.assign({}, data);
            delete data["$type"];
        }
        return JSON.stringify(Object.assign({ $type: optionalType }, data));
    }

    /**
     * Create, report (Sw.Events "net.error") and return an error for a failed response.
     * @param {Response} res - The response.
     * @param {string} url - Request url.
     * @param {string} bodySep - Separator before the 500 body in the message ("\n" for Send, ": " for Get).
     * @returns {Promise<NetError>}
     */
    async function responseError(res, url, bodySep) {
        let text = "";
        try {
            text = await res.text();
        }
        catch {
        }
        const st = res.statusText;
        let msg = (st && st.length > 0) ? (res.status + " - " + st) : ('' + res.status);
        //  The server's reason is user text by contract (HttpResponseException(status, reason): 400 / 403 / 404 / 409 / 422 / 429 ...):
        //  it travels in the message too, so code that shows e.message (or passes it to ErrorText) shows the reason, never only the
        //  status sentence (FBK-1, TXT-30). A 500 keeps its whole body as before.
        //  "X-Sw-Reason: 1": the server marks a body written for people (any status); without it (older servers, proxies) a plain text
        //  body that passes reasonOf is used, except for a 404 (a plain "not found" page of a proxy or a static file)
        const marked = !!(res.headers && res.headers.get("X-Sw-Reason") === "1");
        const reason = marked ? reasonOf(text, "", true) : (res.status === 500 ? text : (res.status === 404 ? "" : reasonOf(text, res.headers ? res.headers.get("Content-Type") : "")));
        if (reason)
            msg += bodySep + reason;
        const e = new NetError(msg, res.status, st, text, url);
        /** @type {string} The server's reason for people ("" when the body is empty, HTML or technical). */
        e.Reason = res.status === 500 && !marked ? reasonOf(text, "") : reason;
        //  403: refused (Sw.Access.IsDenied), not a transient failure. Two kinds, two signals (never guessed from the text): the user's
        //  ROLE doesn't allow the api (the server's "X-Sw-Denied: role" header; a 403 without a reason of an older server counts as one):
        //  RoleDenied, the button that asked is disabled; or a refusal of THIS request with its reason (ex: "only your own domain"):
        //  RoleDenied false, the button stays usable
        if (res.status === 403) {
            e.Denied = true;
            const d = res.headers ? (res.headers.get("X-Sw-Denied") || "") : "";
            /** @type {boolean} The user's role doesn't allow it (Sw.Access disables the target): not about this request's input. */
            e.RoleDenied = d ? /^(?:role|1|true|yes)$/i.test(d.trim()) : !e.Reason;
        }
        /**
         * @type {string} What the server says happened (its "X-Sw-Outcome" header, never guessed from the status): "AlreadyDone" (the
         * goal is met already: "not needed"), "Gone" (it no longer exists), "Refused" (not done, the reason says why), "Denied" (the
         * user's role doesn't allow it), "Invalid" (the input was not accepted); without the header "Denied" for a role refusal,
         * "Refused" for any other 4xx (a 404 / 409 / 410 without it is NOT "already"), "" for a 5xx.
         */
        e.Outcome = outcomeOf(res.headers ? res.headers.get("X-Sw-Outcome") : "", res.status, e.RoleDenied === true);
        return report(e);
    }

    /** The outcome kinds of the server's "X-Sw-Outcome" header (lower case -> the kind). */
    const OutcomeKinds = {
        alreadydone: "AlreadyDone", already: "AlreadyDone", "already-done": "AlreadyDone", "already_done": "AlreadyDone",
        refused: "Refused", gone: "Gone", denied: "Denied", invalid: "Invalid",
    };

    /**
     * The outcome kind of a failed answer: the server's "X-Sw-Outcome" (the one source: the client never guesses "already" from a
     * status); without it "Denied" for a role refusal, "Refused" for another 4xx, "" for anything else. An unknown header value counts
     * as "Refused" (not done).
     * @param {string|null} header The header value.
     * @param {number} status The HTTP status.
     * @param {boolean} roleDenied The user's role doesn't allow it (X-Sw-Denied: role).
     * @returns {string} The kind.
     */
    function outcomeOf(header, status, roleDenied) {
        const h = String(header || "").trim().toLowerCase();
        if (h)
            return OutcomeKinds[h] || "Refused";
        if (roleDenied)
            return "Denied";
        return status >= 400 && status < 500 ? "Refused" : "";
    }

    /** The longest server reason shown to people (a longer body is technical: a log, a page). */
    const MaxReason = 300;

    /**
     * The server's reason in an error body, when it is text for people: plain text (or a JSON { Reason | Message | message | error }),
     * at most MaxReason characters, not HTML, not a stack trace, not just the status phrase. "" otherwise.
     * @param {string} text The body.
     * @param {string} [contentType] The response's Content-Type.
     * @param {boolean} [marked] The server marked it as text for people (X-Sw-Reason: 1): only HTML is refused, any length.
     * @returns {string} The reason ("" when there is none).
     */
    function reasonOf(text, contentType, marked) {
        let t = String(text ?? "").trim();
        if (!t)
            return "";
        if (marked)
            return /<\s*(?:!doctype|html|body)\b/i.test(t) ? "" : t.replace(/\s*\n\s*/g, " ");
        if (/html|xml/i.test(contentType || ""))
            return "";
        if (t[0] === "{" || t[0] === "\"") {
            try {
                const j = JSON.parse(t);
                const v = typeof j === "string" ? j : (j && (j.Reason || j.reason || j.Message || j.message || j.error || j.title));
                t = typeof v === "string" ? v.trim() : "";
            }
            catch {
            }
        }
        if (!t || t.length > MaxReason)
            return "";
        //  HTML (an error page), a stack trace or an exception dump is not for people
        if (/<\s*(?:!doctype|html|head|body|div|p|h\d|pre|table|span|br)\b/i.test(t) || /^\s*at\s+\S+\(|^\s+at\s/m.test(t) || /Exception\s*:/.test(t))
            return "";
        t = t.replace(/\s*\n\s*/g, " ");
        if (Object.values(reasons).some(r => r.toLowerCase() === t.toLowerCase()))
            return "";
        return t;
    }

    /**
     * Emit "net.error" for an error.
     * @param {Error} e - The error.
     * @returns {Error} e.
     */
    function report(e) {
        try {
            Sw.Events.Emit("net.error", { Url: e.Url ?? "", Status: e.Status ?? 0, Error: e });
        }
        catch {
        }
        return e;
    }

    /** Time (ms) a redirected call waits for the page to leave (to the sign in page) before it rejects. */
    const RedirectTimeoutMs = 15000;

    /**
     * The sign in page of a 401 response: the server answers an api call without a signed in user with a 401 and the sign in
     * page in the "X-Login-Url" header (relative to the request url; older servers redirect with a 302 instead).
     * @param {Response} res - The 401 response.
     * @param {string} url - The request url.
     * @returns {string|null} The absolute url of the sign in page, null when the server didn't name one.
     */
    function signInUrl(res, url) {
        const h = res.headers.get("X-Login-Url");
        if (!h)
            return null;
        try {
            return new URL(h, new URL(res.url || url, window.location.href)).href;
        }
        catch {
            return null;
        }
    }

    /**
     * Handle "not signed in": a redirected response (older servers, and urls that are not apis: a 302 to the sign in page,
     * fetch follows it, so the response is "redirected") or a 401 with the sign in page in "X-Login-Url" (api calls).
     * Emit "auth.redirect" and navigate to the sign in page (the sign in flow returns to this page). The call then does not
     * settle while the page leaves, so a caller never mistakes it for a (null) result; if the page is still here after
     * RedirectTimeoutMs (the navigation was blocked or cancelled) it rejects with a Sw.Net.Error (Status 302 or 401, Redirect
     * true for a 302, SignIn true, a "not signed in" message).
     * Throws at once on a retry, with opts.NoNavigate, or when the target is not a http(s) url of this site.
     * When the user is signed in (Sw.Access.User asks the server again) it means "not allowed for this role": it throws a
     * Sw.Net.Error with Status 403 and Denied true (and Redirect for a 302) instead of navigating.
     * @param {string} target - The sign in page (the url of the redirected response, or the X-Login-Url of a 401).
     * @param {number} status - 302 (redirected) or 401.
     * @param {number} count - Attempt number (a redirect on a retry throws).
     * @param {object} opts - Request options (NoNavigate).
     * @param {string} [url] - The request url (the Url of a "not allowed" error).
     * @param {string} [text] - The response body (the text of a 401).
     * @returns {Promise<never>} Does not resolve (rejects after the time out), or throws.
     */
    async function handleSignIn(target, status, count, opts, url, text) {
        const isRedirect = status === 302;
        const redirectError = (msg, statusText) => {
            const e = new NetError(msg, status, statusText, text || "", isRedirect ? target : (url || target));
            e.Redirect = isRedirect;
            e.SignIn = true;
            return report(e);
        };
        const plain = isRedirect ? ["302 - Redirected", "Redirected"] : ["401 - Unauthorized", "Unauthorized"];
        //  The session may have ended on the server: the app shell asks again who is signed in (shell.js WatchSession)
        try {
            if (Sw.InterOp)
                Sw.InterOp.Post("SysWeaver.AuthCheck", {});
        }
        catch {
        }
        if (count > 0)
            throw redirectError(_TF("Request was redirected!", "An exception text when a request was redirected multiple times"), "");
        if (opts.NoNavigate)
            throw redirectError(plain[0], plain[1]);
        //  Only follow to this site (never navigate to a foreign or script url)
        target = Sw.Url.SafeSameOrigin(target);
        if (!target)
            throw redirectError(plain[0], plain[1]);
        //  Signed in, but sent to the sign in page: the user's role doesn't allow the call (not "not signed in"), so the
        //  page shows "not allowed" (Sw.Access.IsDenied) instead of leaving for a sign in page that says "signed in"
        if (Sw.Access && await Sw.Access.User(true)) {
            const e = new NetError("403 - Forbidden", 403, "Forbidden", "", url || target);
            e.Redirect = isRedirect;
            e.Denied = true;
            e.RoleDenied = true;
            throw report(e);
        }
        Sw.Events.Emit("auth.redirect", { Url: target });
        //  A page in the app shell never leaves for the sign in page by itself: the shell checks the session (AuthCheck above) and
        //  reloads as a whole when it ended (shell.js SessionGone)
        const shell = shellSignIn();
        if (shell) {
            return new Promise((resolve, reject) => {
                const wait = () => setTimeout(() => {
                    //  The shell found the session gone and is reloading: keep waiting
                    if (shellSignIn() && shellSignIn().SignedOut)
                        wait();
                    else
                        reject(redirectError(_TF("You are not signed in (or the session has ended). Please sign in and try again.", "Error message when a request needs a signed in user, but the user is not signed in"), plain[1]));
                }, RedirectTimeoutMs);
                wait();
            });
        }
        if (Sw.Page && Sw.Page.Navigate)
            Sw.Page.Navigate(target, "_self");
        else
            window.location.href = target;
        return new Promise((resolve, reject) => setTimeout(() => reject(redirectError(
            _TF("You are not signed in (or the session has ended). Please sign in and try again.", "Error message when a request needs a signed in user, but the user is not signed in"),
            plain[1])), RedirectTimeoutMs));
    }

    /**
     * The app shell that handles "not signed in" for a framed page (Sw.Shell.SignIn of the same-origin top window, set while it watches
     * the session: { SignedOut } ), or null (a page alone, another origin, an older shell).
     * @returns {{SignedOut: boolean}|null} The shell's sign in state.
     */
    function shellSignIn() {
        try {
            if (window.top === window)
                return null;
            const s = window.top.Sw && window.top.Sw.Shell && window.top.Sw.Shell.SignIn;
            return s && typeof s === "object" ? s : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Handle a redirected response (see handleSignIn).
     * @param {Response} res - The redirected response.
     * @param {number} count - Attempt number.
     * @param {object} opts - Request options (NoNavigate).
     * @param {string} [url] - The request url.
     * @returns {Promise<never>}
     */
    function handleRedirect(res, count, opts, url) {
        return handleSignIn(res.url, 302, count, opts, url);
    }

    /**
     * Handle a 401 response: with the sign in page in "X-Login-Url" (an api call without a signed in user) it is "not signed
     * in" (see handleSignIn), else (ex: a bad api key, no auth service) a plain Sw.Net.Error (Status 401).
     * @param {Response} res - The 401 response.
     * @param {number} count - Attempt number.
     * @param {object} opts - Request options (NoNavigate).
     * @param {string} url - The request url.
     * @param {string} bodySep - Separator before the body in the message of a plain error.
     * @returns {Promise<never>}
     */
    async function handleUnauthorized(res, count, opts, url, bodySep) {
        const target = signInUrl(res, url);
        if (!target)
            throw await responseError(res, url, bodySep);
        let text = "";
        try {
            text = await res.text();
        }
        catch {
        }
        return handleSignIn(target, 401, count, opts, url, text);
    }

    /**
     * Read the response body as requested.
     * @param {Response} res - Response.
     * @param {object} opts - Options (Read, Blob, Text).
     * @returns {Promise<*>} Parsed json (undefined if not decodable), Blob, text or the Read result.
     */
    async function readBody(res, opts) {
        try {
            if (opts.Read)
                return await opts.Read(res);
            if (opts.Blob)
                return await res.blob();
            if (opts.Text)
                return await res.text();
            return await res.json();
        }
        catch {
            return undefined;
        }
    }

    // ------------------------------------------------------------------ connection state (UX guide FBK-9)

    /** @type {boolean} The server answers (false while it can't be reached; see Sw.Net.Online). */
    let online = true;

    /** @type {Promise<boolean>|null} The running reachability probe. */
    let probeP = null;

    /** @type {number} The timer of the next probe while offline. */
    let probeTimer = 0;

    /** @type {number} Probes done since the connection was lost (back off). */
    let probeCount = 0;

    /** @type {Array<function():void>} Waiters of waitOnline. */
    const onlineWaiters = [];

    /** @type {number} Date.now() when the connection was lost (0 while online). */
    let offlineSince = 0;

    /** @type {number} Date.now() of the last request that failed to reach the server (this window's or a framed page's, netFailed). */
    let lastFailure = 0;

    /**
     * While another request keeps trying the server (ex: a live table retries every 3 s, as the original table) the probe waits: that
     * request's answer decides the state, so an outage costs the original's requests, not the table's plus the probe's (S5-A-13,
     * S5-Ops-1). A probe is due only when no request failed for this long (ms).
     */
    const OtherTrafficMs = 3500;

    /** A probe that gets no answer in this time (ms) means "not reachable" (a hung server or proxy). */
    const ProbeTimeoutMs = 8000;

    /** A failed request waits at most this long (ms) for the probe before it throws (so Sw.Net.Online is right when it is caught). */
    const SettleMs = 3000;

    /**
     * The connection state of the top window when this is a same-origin framed page (one source of truth for the app: the shell's
     * Sw.Net decides, a framed page mirrors it and forwards its failures). Null in the top window or another origin.
     * @returns {object|null} The top window's Sw.Net.
     */
    function topNet() {
        try {
            if (window.top === window)
                return null;
            const n = window.top.Sw && window.top.Sw.Net;
            return n && n !== Sw.Net && typeof n.SyncFrames === "function" ? n : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Tell the same-origin frames of this window the new state (they emit their own "net.offline" / "net.online"; nothing is kept
     * from them here, so a removed frame is never held).
     * @param {Window} w - The window whose frames get it.
     * @param {boolean} on - Reachable.
     */
    function syncFrames(w, on) {
        for (let i = 0; i < w.frames.length; ++i) {
            try {
                const n = w.frames[i].Sw && w.frames[i].Sw.Net;
                if (n && typeof n.SyncState === "function")
                    n.SyncState(on);
                else
                    syncFrames(w.frames[i], on);
            }
            catch {
                //  Another origin
            }
        }
    }

    /**
     * Set the connection state; emits Sw.Events "net.offline" / "net.online" on a change and schedules the probes while offline.
     * @param {boolean} on - Reachable.
     * @param {boolean} [mirror] - The state comes from the top window (don't forward it back).
     */
    function setOnline(on, mirror) {
        const top = mirror ? null : topNet();
        if (top) {
            //  A framed page: the app's state is decided by the top window (it probes and tells every frame)
            try {
                if (on)
                    top.NoteSuccess();
                else
                    top.NoteFailure(true);
            }
            catch {
            }
            return;
        }
        if (on) {
            clearTimeout(probeTimer);
            probeTimer = 0;
            probeCount = 0;
        }
        if (on === online) {
            if (!on && !mirror)
                scheduleProbe();
            return;
        }
        online = on;
        offlineSince = on ? 0 : Date.now();
        try {
            Sw.Events.Emit(on ? "net.online" : "net.offline", {});
            //  A framed page that is not under a Sw shell top window tells the top (old protocol)
            if (!mirror && window.top !== window && Sw.Frame)
                Sw.Frame.PostTop("SwNetState", { Online: on });
        }
        catch {
        }
        if (on)
            onlineWaiters.splice(0).forEach(w => w());
        else if (!mirror)
            scheduleProbe();
        if (!mirror || window.frames.length)
            syncFrames(window, on);
    }

    /**
     * The probe interval (ms) of a visible tab while offline and no message poll of this window decides: 15 s, the original's retry of
     * its server time sync (S6-Ops-3: an outage costs the original's requests).
     */
    const ProbeMs = 15000;

    /** The probe interval (ms) of a hidden tab while offline. */
    const HiddenProbeMs = 30000;

    /**
     * True while this window's session is the master: its GetMessages poll retries every 5 s while the server is away (and its server
     * time sync every 15 s), as the original's; their answers decide the connection state (Sw.Session feeds NoteFailure / NoteSuccess),
     * so no probe is sent on top of them (S6-Ops-3).
     * @returns {boolean} True when the session's own requests decide.
     */
    function sessionDecides() {
        try {
            const s = Sw.Session;
            return !!(s && s.IsMaster);
        }
        catch {
            return false;
        }
    }

    /**
     * Probe again later while offline. No probe while this window's message poll runs (sessionDecides: its 5 s retry says "back"), or
     * while another request keeps failing (OtherTrafficMs: its retry decides, ex: a live table's 3 s retry); else every 15 s (30 s
     * while the tab is hidden). One probe at a time: no timer while a probe runs (the probe schedules the next one when it found no
     * server), so a hung server never piles up requests. The original sent no probe at all (S6-Ops-3: the Server page sent 8 serverTime
     * in a 30 s outage with the old 1, 2, 2, 2.5 s back off, the original 2).
     */
    function scheduleProbe() {
        if (probeTimer || probeP)
            return;
        ++probeCount;
        const wait = () => document.visibilityState === "hidden" ? HiddenProbeMs : ProbeMs;
        const fire = () => {
            probeTimer = 0;
            if (isOnline())
                return;
            //  The message poll decides (a timer without a request: it looks again in case this window stops being the master)
            if (sessionDecides()) {
                probeTimer = setTimeout(fire, wait());
                return;
            }
            //  Another request failed a moment ago (it retries by itself): no probe on top of it, look again later
            const quiet = Date.now() - lastFailure;
            if (quiet < OtherTrafficMs && document.visibilityState !== "hidden") {
                probeTimer = setTimeout(fire, OtherTrafficMs - quiet);
                return;
            }
            probe();
        };
        probeTimer = setTimeout(fire, wait());
    }

    /**
     * Test if the server can be reached (one request at a time): any answer but 502 / 504 counts (the proxy can't reach the app); no
     * answer within ProbeTimeoutMs counts as not reachable.
     * @returns {Promise<boolean>} True when it answered.
     */
    function probe() {
        const top = topNet();
        if (top)
            return top.Probe();
        if (!probeP) {
            const u = (Sw.Url && Sw.Url.Root ? Sw.Url.Root : "/") + "serverTime";
            const ac = typeof AbortController === "function" ? new AbortController() : null;
            const timer = ac ? setTimeout(() => ac.abort(), ProbeTimeoutMs) : 0;
            probeP = fetch(u, { method: "GET", cache: "no-store", credentials: "same-origin", signal: ac ? ac.signal : undefined })
                .then(r => r.status !== 502 && r.status !== 504, () => false)
                .then(ok => {
                    clearTimeout(timer);
                    probeP = null;
                    setOnline(ok);
                    //  (still offline: the next probe after the back off; scheduleProbe refuses while a probe runs)
                    if (!ok)
                        scheduleProbe();
                    return ok;
                });
        }
        return probeP;
    }

    /**
     * True while another tab's session is the master (this window's session runs as a child): that master's poll tells every window
     * when the server is lost / back ("server.error" / "server.restore").
     * @returns {boolean} True for a child session.
     */
    function sessionChild() {
        try {
            const s = Sw.Session;
            return !!(s && s.IsStarted && !s.IsMaster);
        }
        catch {
            return false;
        }
    }

    /**
     * A request could not reach the server: find out whether the connection is lost (one probe while online). A child tab sends no
     * probe of its own: it is offline at once, the master's poll ("server.restore") or its probe every 15 s says when the server is back
     * (S6-Ops-3: an outage costs the original's requests).
     * @returns {Promise<boolean>} The probe (true when the server answers).
     */
    function netFailed() {
        const top = topNet();
        if (top) {
            try {
                return top.Check();
            }
            catch {
            }
        }
        lastFailure = Date.now();
        if (online && !sessionChild())
            return probe();
        if (online)
            setOnline(false);
        else
            scheduleProbe();
        return Promise.resolve(false);
    }

    /**
     * Wait (at most SettleMs) until a failed request's probe decided the state, so the caller that catches the error sees the right
     * Sw.Net.Online (Sw.Fail stays quiet while offline: no "Failed to fetch" toast before the state flips). No answer in time = offline.
     * @returns {Promise<boolean>} True when the server is reachable.
     */
    async function settle() {
        const p = netFailed();
        let late = false;
        const ok = await Promise.race([p, Sw.Util.Delay(SettleMs).then(() => {
            late = true;
            return false;
        })]);
        if (late && isOnline())
            setOnline(false);
        return ok && isOnline();
    }

    /**
     * The connection state: the top window's for a same-origin framed page (one source of truth), else this window's.
     * @returns {boolean} True while the server can be reached.
     */
    function isOnline() {
        const top = topNet();
        if (top) {
            try {
                return top.Online !== false;
            }
            catch {
            }
        }
        return online;
    }

    /**
     * Wait until the server can be reached again.
     * @param {number} until - Give up at this time (Date.now based).
     * @param {AbortSignal} [signal] - Abort.
     * @returns {Promise<boolean>} True when online again, false when the time is over.
     */
    function waitOnline(until, signal) {
        if (isOnline()) {
            online = true;
            return Promise.resolve(true);
        }
        return new Promise((resolve, reject) => {
            let done = false;
            const finish = v => {
                if (done)
                    return;
                done = true;
                clearTimeout(t);
                const i = onlineWaiters.indexOf(w);
                if (i >= 0)
                    onlineWaiters.splice(i, 1);
                resolve(v);
            };
            const w = () => finish(true);
            const t = setTimeout(() => finish(false), Math.max(0, until - Date.now()));
            onlineWaiters.push(w);
            if (signal)
                signal.addEventListener("abort", () => {
                    if (!done) {
                        done = true;
                        clearTimeout(t);
                        reject(new DOMException("Aborted", "AbortError"));
                    }
                }, { once: true });
        });
    }

    //  The browser knows it lost the network: offline at once; back: probe. A framed page under the shell takes the shell's state.
    try {
        window.addEventListener("offline", () => setOnline(false));
        //  (no probe when this window's message poll decides: its retry says "back", as the original's requests; S6-Ops-3)
        window.addEventListener("online", () => {
            if (!sessionDecides() && !topNet())
                probe();
        });
        document.addEventListener("visibilitychange", () => {
            if (!isOnline() && document.visibilityState === "visible" && !sessionDecides() && !topNet()) {
                clearTimeout(probeTimer);
                probeTimer = 0;
                probe();
            }
        });
        const top = topNet();
        if (top && top.Online === false) {
            online = false;
            offlineSince = Date.now();
        }
    }
    catch {
    }

    /**
     * The fetch init of a Sw.Net request: the signal, and Detached (a request that is not a click's work).
     * @param {object} opts - Options (Signal, Detached / Background).
     * @returns {object|undefined} The init.
     */
    function fetchInit(opts, keepWrite) {
        const detached = isDetached(opts);
        if ((!opts.Signal || keepWrite) && !detached)
            return undefined;
        const init = {};
        //  (a write sent with a signal of the cancellation model is never aborted: see fetchRetry)
        if (opts.Signal && !keepWrite)
            init.signal = opts.Signal;
        //  Detached is a property of the REQUEST: the fetch of Sw.Net carries it, so a click's work tracker (ui/base.js) never collects it
        //  or gives it the click's signal, whichever call started it (Send, Get, a nested helper)
        if (detached)
            init.Detached = true;
        return init;
    }

    /**
     * A request that is not the work of the click that may have started it (telemetry, a live view, a long poll, a server job's
     * WaitJob): options Detached or Background.
     * @param {object} opts The options.
     * @returns {boolean} True when detached.
     */
    function isDetached(opts) {
        return !!(opts && (opts.Detached === true || opts.Background === true));
    }

    /**
     * Is a request a WRITE (the server may change something, so a Cancel never aborts it once it was sent: the one cancellation model)?
     * Sw.Net.Send is a write unless it is marked ReadOnly (a read api; also the old spelling Read: true) or AbortSafe (the server stops /
     * undoes the work when the request is aborted); Sw.Net.Get and GET / HEAD requests are reads.
     * The ONE write test of the framework (dialogs, buttons, drafts, Sw.Activity and Sw.Net use it; no other code decides "write"). It
     * also takes a Request or a fetch init ({ method }): a Request sent by Sw.Net says it itself (SwWrite); else GET / HEAD / OPTIONS
     * are reads, every other method a write.
     * @param {object|Request} [opts] The options of the request (Sw.Net.Send / Get), or a Request / fetch init.
     * @param {string} [kind] "Send" (default), "Get", or an http method (a page's own fetch).
     * @returns {boolean} True for a write.
     */
    function isWrite(opts, kind) {
        //  A Request (or a fetch init with a method): Sw.Net marked it; else its method decides
        if (opts && typeof opts === "object" && typeof opts.method === "string" && kind === undefined) {
            if (typeof opts.SwWrite === "boolean")
                return opts.SwWrite;
            return !/^(?:GET|HEAD|OPTIONS)$/i.test(opts.method);
        }
        if (opts && opts.AbortSafe === true)
            return false;
        if (kind === "Get")
            return false;
        if (kind && kind !== "Send")
            return !/^(?:GET|HEAD|OPTIONS)$/i.test(String(kind));
        return !(opts && (opts.ReadOnly === true || opts.Read === true));
    }

    /** The options of Sw.Net.Get (Sw.DevOptions schema). */
    const GetOptions = {
        Reload: "boolean", Blob: "boolean", Text: "boolean",
        Read: {
            Type: "function", Collides: "Read is the body reader (a function that gets the Response and returns the value). To mark a request "
                + "that only reads (a Cancel may abort it) use { ReadOnly: true } or Sw.Net.Read(url, arg); Read: true is taken as ReadOnly: true and the json is returned.",
        },
        ReadOnly: "boolean", AbortSafe: "boolean", Setup: "function", Signal: "signal",
        /* SwWeb:no-tr (a developer warning, English like every Sw.DevWarn) */
        Retry: { Type: ["number", "string"], Hint: 'Retry is a number of retries or "online" (wait for the connection and send again).' },
        RetryMs: { Type: "number", Min: 0 }, NoNavigate: "boolean", Detached: "boolean", Background: "boolean",
    };

    /** The options of Sw.Net.Send (Sw.DevOptions schema). */
    const SendOptions = Object.assign({}, GetOptions, { Post: "boolean", ErrHandler: "function" });

    /**
     * Check (developer mode) and normalize the options of a request: Read that is not a function (the old spelling Read: true of a
     * read mark) becomes ReadOnly, so the body is still the json (never undefined).
     * @param {string} where The api ("Sw.Net.Send").
     * @param {object} [opts] The options.
     * @returns {object} The options to use.
     */
    function requestOptions(where, opts) {
        opts = opts || {};
        if (Sw.DevOptions)
            Sw.DevOptions(where, opts, where === "Sw.Net.Get" ? GetOptions : SendOptions);
        if (opts.Read !== undefined && typeof opts.Read !== "function")
            opts = Object.assign({}, opts, { Read: undefined, ReadOnly: opts.ReadOnly === undefined ? !!opts.Read : opts.ReadOnly });
        if (opts.Retry !== undefined && opts.Retry !== "online" && !(typeof opts.Retry === "number" && opts.Retry >= 0) && Sw.DevWarn)
            Sw.DevWarn(where + ': option Retry must be a number of retries or "online", got ' + JSON.stringify(opts.Retry) + " (no retries are made).", "opt:" + where + ":Retry:value");
        return opts;
    }

    /**
     * Fetch with optional retries on network errors and 502/503/504: opts.Retry times (500 ms * attempt back off), or with
     * opts.Retry "online" until the server answers again (waits for the connection, at most opts.RetryMs, default 5 minutes; for
     * reads and saves that are safe to repeat). Every network error and answer updates the connection state (Sw.Net.Online).
     * A Detached request marks its Request (SwDetached) and its init (Detached), so no click scope collects it.
     * The one cancellation model in code: a WRITE (see isWrite) sent with a signal of the model (Sw.Cancel.Controller / Mark: Sw.Activity
     * Run / Track, dialogs, buttons) is never aborted once it was sent: a Cancel before it is sent throws an AbortError (nothing was
     * sent); after that the work gets the server's answer (the job / dialog then says "Finished before it could stop"), and
     * Sw.Cancel.Sent(signal) counts the writes (Writes, Pending, DoneAfterAbort). Every Request is marked SwNet (a click's tracker counts
     * the outer Sw.Net call, not its inner fetch) and SwWrite.
     * @param {function():Request} make - Creates a fresh Request for every attempt (a body can only be sent once).
     * @param {object} opts - Options (Retry, RetryMs, Signal, Detached).
     * @param {string} url - Url (for errors).
     * @param {boolean} [write] - The request is a write.
     * @returns {Promise<Response>}
     */
    async function fetchRetry(make, opts, url, write) {
        const detached = isDetached(opts);
        const m0 = make;
        make = () => {
            const r = m0();
            try {
                r.SwNet = true;
                r.SwWrite = !!write;
                if (detached)
                    r.SwDetached = true;
            }
            catch {
            }
            return r;
        };
        //  A write of the cancellation model: not aborted once sent (the server may have it already)
        const sig = opts.Signal;
        const keep = !!(write && sig && sig.SwCancel === true);
        const sent = keep && Sw.Cancel && Sw.Cancel.Sent ? Sw.Cancel.Sent(sig) : null;
        const wait = opts.Retry === "online";
        const retries = wait ? Infinity : (opts.Retry > 0 ? opts.Retry : 0);
        const until = wait ? Date.now() + (opts.RetryMs > 0 ? opts.RetryMs : 300000) : 0;
        for (let attempt = 0; ; ++attempt) {
            let res;
            try {
                if (keep && sig.aborted)
                    throw (Sw.Cancel && Sw.Cancel.Error ? Sw.Cancel.Error(sig.reason) : new DOMException("Aborted", "AbortError"));
                if (sent) {
                    ++sent.Writes;
                    ++sent.Pending;
                }
                try {
                    res = await fetch(make(), fetchInit(opts, keep));
                }
                finally {
                    if (sent) {
                        --sent.Pending;
                        if (sig.aborted)
                            ++sent.DoneAfterAbort;
                    }
                }
            }
            catch (e) {
                if (e && e.name === "AbortError")
                    throw e;
                if (attempt >= retries || (wait && Date.now() >= until)) {
                    //  One code path decides "offline or error": the probe settles first, so the caller that catches this sees the
                    //  right Sw.Net.Online (Sw.Fail is quiet while offline; e.Offline says it was the connection)
                    const reachable = await settle();
                    report(Object.assign(e, { Url: url, Status: 0, Offline: !reachable }));
                    throw e;
                }
                netFailed();
                if (wait) {
                    //  Wait for the connection (the probe decides), then try again at once; a failure while "online" backs off
                    await Sw.Util.Delay(Math.min(5000, 500 * (attempt + 1)));
                    await waitOnline(until, opts.Signal);
                }
                else
                    await Sw.Util.Delay(500 * (attempt + 1));
                continue;
            }
            const gw = res.status === 502 || res.status === 504;
            if (gw) {
                if (attempt >= retries)
                    await settle();
                else
                    netFailed();
            }
            else
                setOnline(true);
            if ((attempt < retries) && (gw || res.status === 503) && !(wait && Date.now() >= until)) {
                await Sw.Util.Delay(Math.min(wait ? 15000 : 5000, 500 * (attempt + 1)));
                if (wait && gw)
                    await waitOnline(until, opts.Signal);
                continue;
            }
            return res;
        }
    }

    /**
     * The request header with the page language for a same origin url ({} for another origin: a custom header would need a CORS preflight).
     * @param {string} url - The request url (relative or absolute).
     * @returns {object} { "X-Sw-Lang": language } or {}.
     */
    function langHeader(url) {
        try {
            if (new URL(url, document.baseURI).origin !== location.origin)
                return {};
            const l = (Sw.I18n && Sw.I18n.Language) ? Sw.I18n.Language() : (document.documentElement.getAttribute("lang") || "");
            return l ? { "X-Sw-Lang": l } : {};
        }
        catch {
            return {};
        }
    }

    /** @type {Map<string, {Value:Promise<*>, Exp:number}>} Memoization cache. */
    const cache = new Map();

    /** @type {boolean} True while the prune timer runs. */
    let pruning = false;

    /** Remove expired cache entries every 5 s while the cache is not empty. */
    function prune() {
        const n = Date.now();
        cache.forEach((v, k) => {
            if (n >= v.Exp)
                cache.delete(k);
        });
        if (cache.size <= 0) {
            pruning = false;
            return;
        }
        setTimeout(prune, 5000);
    }

//DEBUG_BEGIN
    /** @type {Promise<boolean>|null} DEBUG builds only: the memoized debugOptimizedSend answer. */
    let optimizedSendP = null;

    /**
     * DEBUG builds only (this block is stripped from release builds, which always use the GET encodings): true when the web
     * server's Params "OptimizedSend" asks for the release encodings in debug builds too, so release only encoding
     * problems show up while debugging. Known without a request (the original always POSTed in debug builds and asked
     * nothing): the templated shell page (index.html) has it in the meta "sysweaver-optimized-send" ("true" / "false"),
     * read from this window or a same origin ancestor, and the shell remembers it per site in localStorage
     * "SysWeaver.OptimizedSend" for pages opened on their own; false when unknown.
     * @returns {Promise<boolean>}
     */
    function debugOptimizedSend() {
        if (optimizedSendP)
            return optimizedSendP;
        const key = "SysWeaver.OptimizedSend";
        let v = null;
        let w = window;
        try {
            for (;;) {
                const m = w.document.querySelector("meta[name='sysweaver-optimized-send']");
                const c = m ? m.getAttribute("content") : null;
                if (c === "true" || c === "false") {
                    v = c === "true";
                    if (w === window) {
                        const map = Object.assign({}, Sw.Storage.GetJson(key, null));
                        if (map[Sw.Url.Root] !== v) {
                            map[Sw.Url.Root] = v;
                            Sw.Storage.SetJson(key, map);
                        }
                    }
                    break;
                }
                if (!w.parent || w.parent === w)
                    break;
                w = w.parent;
            }
        }
        catch {
            //  A cross origin ancestor
        }
        if (v === null) {
            const map = Sw.Storage.GetJson(key, null);
            v = !!(map && typeof map === "object" && map[Sw.Url.Root] === true);
        }
        optimizedSendP = Promise.resolve(v);
        return optimizedSendP;
    }
//DEBUG_END

    /**
     * Click a temporary hidden download link.
     * @param {string} href - Url.
     * @param {string} filename - Suggested file name.
     */
    function clickDownload(href, filename) {
        const a = document.createElement('a');
        a.setAttribute('href', href);
        a.setAttribute('download', filename);
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        a.remove();
    }

    /** The standard reason phrases (what servers put after "404 - "). */
    const reasons = {
        400: "Bad Request", 401: "Unauthorized", 403: "Forbidden", 404: "Not Found", 405: "Method Not Allowed", 408: "Request Timeout",
        409: "Conflict", 410: "Gone", 413: "Payload Too Large", 415: "Unsupported Media Type", 422: "Unprocessable Entity",
        429: "Too Many Requests", 500: "Internal Server Error", 501: "Not Implemented", 502: "Bad Gateway", 503: "Service Unavailable",
        504: "Gateway Timeout",
    };

    /**
     * What a failed status means for the user (Why) and what to do (Do), for a failure without a reason of the server (never a raw
     * "404 - Not Found" toast).
     * @param {number} s The HTTP status (0 = the server could not be reached).
     * @returns {{Why: string, Do: string}} The two sentences ("" when there is nothing to do).
     */
    function statusParts(s) {
        const p = (why, d) => ({ Why: why, Do: d || "" });
        const again = () => _TF("Try again.", "Advice after a failed request: try the same action again");
        switch (s) {
            case 0: return p(_TF("Could not reach the service.", "Error message when a request could not reach the service (network error)"), _TF("Check the connection and try again.", "Advice after a request could not reach the service"));
            case 400: case 422: return p(_TF("The service did not accept the request.", "Error message of a failed request (HTTP 400 Bad Request / 422)"), _TF("Check the values and try again.", "Advice after the service did not accept a request (HTTP 400 / 422)"));
            case 401: return p(_TF("You are not signed in, or the session has ended.", "Error message of a failed request (HTTP 401 Unauthorized)"), _TF("Sign in and try again.", "Advice after a failed request (HTTP 401 Unauthorized)"));
            case 403: return p(_TF("You are not allowed to do this.", "Error message of a failed request (HTTP 403 Forbidden)"), _TF("Ask an administrator if you need access.", "Advice after a failed request (HTTP 403 Forbidden)"));
            case 404: case 410: return p(_TF("The service could not find it. It may have been removed or renamed.", "Error message of a failed request (HTTP 404 Not Found)"), _TF("Reload the page and try again.", "Advice after a failed request (HTTP 404 Not Found)"));
            case 405: case 501: return p(_TF("The service does not support this action.", "Error message of a failed request (HTTP 405 / 501)"));
            case 408: case 504: return p(_TF("The service took too long to answer.", "Error message of a failed request (HTTP 408 / 504 time out)"), _TF("Try again in a moment.", "Advice after a failed request that may work a bit later (time out, server restarting)"));
            case 409: return p(_TF("It was changed in the meantime.", "Error message of a failed request (HTTP 409 Conflict)"), _TF("Reload the page and try again.", "Advice after a failed request (HTTP 409 Conflict)"));
            case 413: return p(_TF("It is too large for the service.", "Error message of a failed request (HTTP 413 Payload Too Large)"));
            case 429: return p(_TF("Too many requests.", "Error message of a failed request (HTTP 429 Too Many Requests)"), _TF("Wait a moment and try again.", "Advice after a failed request (HTTP 429 Too Many Requests)"));
            case 502: case 503: return p(_TF("The service is not available right now (it may be restarting).", "Error message of a failed request (HTTP 502 / 503)"), _TF("Try again in a moment.", "Advice after a failed request that may work a bit later (time out, server restarting)"));
            default: return s >= 500
                ? p(_TF("The service failed to do it.", "Error message of a failed request (an HTTP 5xx server error)"), _TF("Try again; if it keeps failing, tell an administrator.", "Advice after a failed request (an HTTP 5xx server error)"))
                : p(_TF("The request failed.", "Error message of a failed request (another HTTP 4xx status)"), again());
        }
    }

    /**
     * What a failed status means for the user, and what to do: statusParts as one sentence.
     * @param {number} s The HTTP status.
     * @returns {string} The sentence.
     */
    function statusSentence(s) {
        const p = statusParts(s);
        return p.Do ? p.Why + " " + p.Do : p.Why;
    }

    /**
     * End a sentence: a full stop unless it ends with one (., !, ?, …, :).
     * @param {string} s The text.
     * @returns {string} The sentence.
     */
    function sentence(s) {
        s = String(s ?? "").trim();
        return !s || /[.!?…。！？]$/.test(s) ? s : s + ".";
    }

    /**
     * The one message composer: "What: Why. Do." (FBK-1, TXT-25: what failed, why, what to do; one advice only).
     *  - What ends with a sentence mark ("Withdraw invitation: not done.") or a colon: Why follows after a space (never ".: ").
     *  - Do is left out when Why already says it (the same sentence), so a page can't add a second "try again".
     * @param {string} what What failed ("" for none).
     * @param {string} why Why (the server's reason, or the status sentence).
     * @param {string} [todo] What to do.
     * @returns {string} The message.
     */
    function compose(what, why, todo) {
        what = String(what ?? "").trim();
        why = sentence(why);
        todo = sentence(todo);
        const norm = s => s.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, " ").trim();
        //  (advice that the what or the why already says is never repeated: "… Nothing was sent. Nothing was sent.")
        if (todo && ((why && norm(why).includes(norm(todo))) || (what && norm(what).includes(norm(todo)))))
            todo = "";
        let text = why;
        //  (the advice is a sentence of its own: "… was used. Try again.")
        if (todo)
            text = text ? text + " " + todo.charAt(0).toLocaleUpperCase() + todo.substring(1) : todo;
        if (what) {
            if (!text)
                return what;
            //  A new sentence starts with a capital ("… nothing was changed. The server gave no delete token.")
            if (/[.!?…。！？]$/.test(what))
                text = text.charAt(0).toLocaleUpperCase() + text.substring(1);
            text = /[.!?…:。！？]$/.test(what) ? what + " " + text : what + ": " + text;
        }
        else if (text)
            text = text.charAt(0).toLocaleUpperCase() + text.substring(1);
        return text;
    }

    /**
     * Turn a failed request (a Sw.Net.Error, an Error or a message text such as "Could not save: 404 - Not Found", "409 - Conflict\nThe
     * session has already ended." or "500 - Internal Server Error\nUser "x" does not exist!") into a message for people: What failed
     * (the caller's prefix), Why (the SERVER'S REASON for every status that has one: a 400 / 403 / 404 / 409 / 422 / 429 / 5xx body
     * that is text for people; the status sentence only without one) and what to Do (the status' advice; none when the server gave a
     * reason: it says it). A text without an HTTP status is returned as is.
     * @param {Error|string} x The error or text.
     * @returns {{Text: string, Details: string, Status: (number|undefined), What: string, Why: string, Do: string, Reason: string}} Text =
     * the message (compose(What, Why, Do)), Details = the raw text ("" when Text is the raw text), Status = the HTTP status found,
     * Reason = the server's reason ("" when it gave none).
     */
    function errorText(x) {
        const raw = x && typeof x === "object" ? String(x.message ?? x) : String(x ?? "");
        let status = x && typeof x === "object" && typeof x.Status === "number" ? x.Status : undefined;
        if (x instanceof TypeError && /fetch|network/i.test(raw))
            status = 0;
        const m = /(?:^|:\s+|\n|\s)([45]\d\d)(?:\s+-\s+|\s*$)/.exec(raw);
        if (!m && status === undefined)
            return textual({ Text: raw, Details: "", Status: undefined, What: "", Why: raw, Do: "", Reason: "" });
        let before = "";
        let rest = "";
        if (m) {
            const code = +m[1];
            const phrase = reasons[code];
            rest = raw.substring(m.index + m[0].length);
            //  Only a real status: its reason phrase follows, or nothing, or the server's reason on the next line
            if (phrase && rest.startsWith(phrase))
                rest = rest.substring(phrase.length);
            else if (rest && code !== 500 && status === undefined)
                return textual({ Text: raw, Details: "", Status: undefined, What: "", Why: raw, Do: "", Reason: "" });
            status = status ?? code;
            before = raw.substring(0, m.index).replace(/[\s:]+$/, "");
            rest = rest.replace(/^[\s:.-]+/, "").replace(/\s*\[\d{3}\]\s*$/, "").trim();
        }
        //  The server's reason: the error's own (Sw.Net.Error.Reason), else the body (Text) of the error, else the text after the status
        let reason = x && typeof x === "object" && typeof x.Reason === "string" ? x.Reason : "";
        if (!reason && x && typeof x === "object" && typeof x.Text === "string" && typeof x.Reason !== "string")
            reason = reasonOf(x.Text, "");
        if (!reason && rest && !(x && typeof x === "object" && typeof x.Reason === "string"))
            reason = status >= 500 ? reasonOf(rest.split("\n")[0], "") : reasonOf(rest, "");
        const sp = statusParts(status);
        const why = reason || sp.Why;
        //  A reason of the server says what happened and what to do (it is written for people); the status advice only without one
        const todo = reason && status < 500 ? "" : sp.Do;
        const text = compose(before, why, todo);
        return textual({ Text: text, Details: text === raw ? "" : raw, Status: status, What: before, Why: why, Do: todo, Reason: reason });
    }

    /**
     * The message for people of a failure, the one composer for every error text (Sw.Fail, Sw.Work, row actions, forms):
     * Message("Withdraw invitation: not done.", e) = "Withdraw invitation: not done. The invitation was already used." (the server's
     * reason), Message("Failed to send invitation", e, { Do: "Check the entries and try again." }) = the caller's advice replaces the
     * status' advice (never two "try again"); with a reason of the server for a 4xx no advice is added (o.Do too, unless o.Always).
     * @param {string} what What failed ("" for none).
     * @param {*} error The error (a Sw.Net.Error, an Error, a text).
     * @param {{Do: string, Always: boolean}} [o] Do: what to do (the caller knows best); Always: add Do even after a server reason.
     * @returns {{Text: string, Details: string, Status: (number|undefined), What: string, Why: string, Do: string, Reason: string}} The
     * message (Text) and its parts.
     */
    function messageParts(what, error, o) {
        o = o || {};
        if (Sw.DevOptions)
            Sw.DevOptions("Sw.Net.Message", o);
        //  The arguments swapped (Message(e, "Rename")): the action is the text, the error the object (a developer warning; the message
        //  is still right)
        if (what && typeof what === "object" && (typeof error === "string" || error == null)) {
            if (Sw.DevWarn)
                Sw.DevWarn("Sw.Net.Message / MessageParts / FailText: the first argument is the action's name (a string) and the second the error; got an error first (the arguments are swapped).", "net.message.swapped");
            const t = what;
            what = error;
            error = t;
        }
        else if (what != null && typeof what !== "string" && Sw.DevWarn)
            Sw.DevWarn("Sw.Net.Message / MessageParts / FailText: the action's name must be a string, got " + typeof what + ".", "net.message.what");
        if (typeof what !== "string")
            what = what == null ? "" : String(what);
        const et = errorText(error);
        const isReq = et.Status !== undefined;
        const todo = !isReq ? (o.Do || "") : (et.Reason && et.Status < 500 && !o.Always ? "" : (o.Do || et.Do));
        const w = [what, et.What].filter(Boolean).join(": ");
        const text = compose(w, et.Why, todo);
        const raw = error && typeof error === "object" ? String(error.message ?? error) : String(error ?? "");
        //  (the composer never returns nothing: a failure always says something)
        if (!text && Sw.DevWarn)
            Sw.DevWarn("Sw.Net.Message: nothing to say (no action name and no error text): pass the action's name and the error.", "net.message.empty");
        return textual({
            Text: text || statusParts(undefined).Why, Details: et.Details || (raw && raw !== text && isReq ? raw : ""), Status: et.Status, What: w, Why: et.Why, Do: todo,
            Reason: et.Reason, Kind: outcomeKind(error),
        });
    }

    /**
     * What the server says happened to a failed request (see outcomeOf): the error's Outcome ("AlreadyDone", "Gone", "Refused",
     * "Denied", "Invalid"), "" for a network error, a 5xx, an AbortError or anything that is not an answer of the server.
     * @param {*} e The error.
     * @returns {string} The kind.
     */
    function outcomeKind(e) {
        if (!e || typeof e !== "object")
            return "";
        const k = typeof e.Outcome === "string" ? e.Outcome : "";
        return k ? (OutcomeKinds[k.toLowerCase()] || "Refused") : "";
    }

    /**
     * Give a message object its text as its string value (toString / valueOf / Symbol.toPrimitive, not enumerable): an object that is
     * used as text by mistake (textContent = parts, Sw.Fail(parts), "x" + parts) shows its Text, never "[object Object]".
     * @param {{Text: string}} m The message parts.
     * @returns {object} m.
     */
    function textual(m) {
        const t = () => m.Text;
        try {
            Object.defineProperty(m, "toString", { value: t, enumerable: false, configurable: true });
            Object.defineProperty(m, "valueOf", { value: t, enumerable: false, configurable: true });
            Object.defineProperty(m, Symbol.toPrimitive, { value: t, enumerable: false, configurable: true });
            //  (Sw.Fail / toasts that read an error's message get the text too)
            Object.defineProperty(m, "message", { get: t, enumerable: false, configurable: true });
        }
        catch {
        }
        return m;
    }

    /**
     * The message for people of a failure as TEXT (the one composer; see messageParts): "What: Why. Do.".
     * @param {string} what What failed ("" for none).
     * @param {*} error The error.
     * @param {{Do: string, Always: boolean}} [o] See messageParts.
     * @returns {string} The message.
     */
    function message(what, error, o) {
        return messageParts(what, error, o).Text;
    }

    /** @type {Map<string, object>} The latest-wins sequences by key (Sw.Net.Latest). */
    const latest = new Map();

    /**
     * The error of a call that a newer call of its Sw.Net.Latest sequence replaced: an AbortError (quiet everywhere).
     * @returns {DOMException} The error.
     */
    function superseded() {
        const e = new DOMException("A newer request replaced this one", "AbortError");
        try {
            Object.defineProperty(e, "SwSuperseded", { value: true });
        }
        catch {
        }
        return e;
    }

    /**
     * @namespace Sw.Net - server requests, includes and caching.
     * Extended with Object.defineProperties, NOT Object.assign: assign calls the Online / OfflineSince getters once and stores their
     * values, so the connection state froze at "online" once core/include.js had created Sw.Net first (S6-Ops-1).
     */
    Sw.Net = Object.defineProperties(Sw.Net || {}, Object.getOwnPropertyDescriptors({

        /** @type {typeof NetError} The error class thrown for failed requests. */
        Error: NetError,

        /**
         * @type {boolean} False while the server can't be reached (network errors, 502 / 504, the browser's offline event; the next
         * answer decides when it is back: the session's poll, its time sync, or a probe, see the file header). Changes emit Sw.Events "net.offline" / "net.online" (the app shell shows
         * the one calm line "Offline, retrying…" / "Back online"; pages refresh their data on "net.online").
         */
        get Online() {
            return isOnline();
        },

        /**
         * @type {number} When the connection was lost (Date.now() ms), 0 while online. The time of the "Not updated since" texts of data
         * that can't refresh. One source of truth with Online (a framed page reads the shell's).
         */
        get OfflineSince() {
            const top = topNet();
            if (top) {
                try {
                    return top.OfflineSince || 0;
                }
                catch {
                }
            }
            return offlineSince;
        },

        /**
         * Decide whether an error should be shown to the user (one rule for every error message, FBK-9). SYNCHRONOUS: use it as a
         * predicate, `if (!Sw.Net.ShouldReport(e)) return;` (awaiting it works too). False for an AbortError and for a lost connection
         * (the shell's offline line says it once): a Sw.Net error already waited for the connection check before it was thrown
         * (e.Offline says the result); a raw fetch network error whose check is not decided yet also counts as a lost connection (the
         * check is started, the offline line follows). True for every other error (an answer of the server).
         * @param {*} e - The error.
         * @returns {boolean} True to show it.
         */
        ShouldReport(e) {
            if (!e)
                return true;
            if (e.name === "AbortError")
                return false;
            if (!Sw.Net.IsNetworkError(e))
                return true;
            if (e.Offline === true || !isOnline())
                return false;
            //  A Sw.Net error whose connection check found the server answering: a real failure of this request
            if (e.Offline === false)
                return true;
            //  A raw network error: start the check (the state follows), quiet now
            try {
                netFailed();
            }
            catch {
            }
            return false;
        },

        /**
         * ShouldReport after the connection check of a raw fetch network error decided (at most a few seconds): true when the server
         * answered the check (the request failed for another reason).
         * @param {*} e - The error.
         * @returns {Promise<boolean>} True to show it.
         */
        async ShouldReportAsync(e) {
            if (!e)
                return true;
            if (e.name === "AbortError")
                return false;
            if (!Sw.Net.IsNetworkError(e))
                return true;
            if (e.Offline === true || !isOnline())
                return false;
            if (e.Offline === false)
                return true;
            return await settle();
        },

        /**
         * A request failed to reach the server: check the connection now (one probe at a time).
         * @returns {Promise<boolean>} True when the server answers.
         */
        Check() {
            return netFailed();
        },

        /**
         * Internal (the shell's frames): take the connection state of the top window (a framed page mirrors it, see Online).
         * @param {boolean} on - Reachable.
         */
        SyncState(on) {
            on = !!on;
            if (on !== online) {
                offlineSince = on ? 0 : Date.now();
                online = on;
                try {
                    Sw.Events.Emit(on ? "net.online" : "net.offline", {});
                }
                catch {
                }
                if (on)
                    onlineWaiters.splice(0).forEach(w => w());
            }
            syncFrames(window, on);
        },

        /**
         * Internal: tell the same-origin frames of this window the state (marks a Sw.Net that frames can mirror).
         */
        SyncFrames() {
            syncFrames(window, isOnline());
        },

        /**
         * Test if an error means that the server could not be reached (a network error, a 502 / 504), not an answer of the server.
         * Use it to keep a form's values and say "Not saved: the connection was lost" with "Try again" (FBK-9).
         * @param {*} e - The error.
         * @returns {boolean} True for a lost connection.
         */
        IsNetworkError(e) {
            if (!e || e.name === "AbortError")
                return false;
            if (e.Offline === true || e.Status === 502 || e.Status === 504)
                return true;
            return (e instanceof TypeError) && (e.Status === 0 || e.Status === undefined);
        },

        /**
         * Wait until the server can be reached again (resolves at once when it can).
         * @param {number} [maxMs=300000] - Give up after this long.
         * @param {AbortSignal} [signal] - Abort.
         * @returns {Promise<boolean>} True when online, false when the time was over.
         */
        WaitOnline(maxMs, signal) {
            return waitOnline(Date.now() + (maxMs > 0 ? maxMs : 300000), signal);
        },

        /**
         * Report that the server could not be reached by other means (ex: the session manager's message poll failed): the connection
         * state is probed. With lost = true the caller's failure already decides (the message poll failed, or the master's
         * "server.error"): offline at once, no probe (its own retry says when the server is back; S6-Ops-3).
         * @param {boolean} [lost] - The failure decides (no probe).
         */
        NoteFailure(lost) {
            if (lost) {
                lastFailure = Date.now();
                setOnline(false);
                return;
            }
            netFailed();
        },

        /** Report that the server answered by other means (ex: the session manager's message poll, its "server.restore"): online. */
        NoteSuccess() {
            setOnline(true);
        },

        /** Probe the connection now (ex: a "Try now" button). @returns {Promise<boolean>} True when the server answered. */
        Probe() {
            return probe();
        },

        /**
         * A message for people from a failed request (Sw.Fail uses it): ErrorText("Could not save: 404 - Not Found") =
         * { Text: "Could not save: The server could not find it. ...", Details: "Could not save: 404 - Not Found", Status: 404 }.
         */
        ErrorText: errorText,

        /**
         * The one message composer for failures, as TEXT (a string: use it anywhere a text goes): Message("Withdraw invitation: not
         * done.", e) = the server's reason when it gave one ("Withdraw invitation: not done. The invitation was already used."), else the
         * status sentence and its advice; o.Do = the caller's advice (replaces the status' advice, never two); o.Always = add Do even
         * after a server reason. The parts: MessageParts.
         * @type {function(string, *, {Do: string, Always: boolean}=): string}
         */
        Message: message,

        /** The same as Message (a string); the name says it. */
        MessageText: message,

        /**
         * The message and its parts: { Text, Details, Status, What, Why, Do, Reason } (the object's string value is Text, so it never
         * shows "[object Object]" when it is used as text by mistake).
         */
        MessageParts: messageParts,

        /**
         * Is a request a write (a Cancel never aborts it once it was sent)? IsWrite(opts, "Send" | "Get" | method): Send unless ReadOnly /
         * AbortSafe (or the old Read: true), never a Get / GET / HEAD. Dialogs and buttons use it for the requests they record.
         */
        IsWrite: isWrite,

        /** Join "What: Why. Do." the one way (no ".: ", no repeated advice): Compose(what, why, todo) -> text. */
        Compose: compose,

        /**
         * The failure sentence of an action: FailText("End session (debug)", e) = "End session (debug): not done. The session has
         * already ended." (the server's reason), else "<name>: not done. <status sentence> <advice>" (Message with "<name>: not done.").
         * @param {string} name The action ("" for none: "Not done. …").
         * @param {*} error The error.
         * @param {{Do: string, Always: boolean}} [o] See Message.
         * @returns {string} The sentence.
         */
        FailText(name, error, o) {
            const what = name
                ? _T("{0}: not done.", name, "Start of the message after an action failed. {0} is the action's name, ex: 'Withdraw invitation'; the reason follows")
                : _TF("Not done.", "Start of the message after an action failed; the reason follows");
            return message(what, error, o);
        },

        /** The server's reason in an error body when it is text for people, else "" (see reasonOf). */
        ReasonOf: reasonOf,

        /**
         * A request that is NOT the work of a click (a long poll, a live view, telemetry): Send with { Detached: true }, so a button that
         * started it never becomes busy or abortable by it, and its leave question does not wait for it.
         * @param {string} url - Api url.
         * @param {*} arg - The argument.
         * @param {object} [opts] - Options of Send.
         * @returns {Promise<*>} The answer.
         */
        Live(url, arg, opts) {
            return Sw.Net.Send(url, arg, Object.assign({}, opts || {}, { Detached: true }));
        },

        /**
         * Call an api that only READS (changes nothing on the server): Send with { ReadOnly: true }, so a Cancel may abort it (a dialog
         * offers a real Cancel, an activity job stops at once), and it is safe to repeat. The answer as Send.
         * @param {string} url - Api url.
         * @param {*} arg - The argument.
         * @param {object} [opts] - Options of Send.
         * @returns {Promise<*>} The answer.
         */
        Read(url, arg, opts) {
            return Sw.Net.Send(url, arg, Object.assign({}, opts || {}, { ReadOnly: true }));
        },

        /**
         * Latest wins (search as you type, a filter, a detail that follows a selection: UX guide FBK-7, no stale data replaces newer):
         * one sequence per key and window; every call supersedes the one before it. An older call's answer is NEVER delivered: it
         * rejects with an AbortError (SwSuperseded true: Sw.Net.ShouldReport is false, Sw.Cancel.IsAbort true, Sw.Work counts it as
         * stopped, nothing is shown), and an older READ is aborted too (a write is never aborted, only its answer is dropped).
         * ex: `const q = Sw.Net.Latest("contacts.search"); box.oninput = () => q.Read(api + "Search", { Q: box.value }).then(show, e => Sw.Net.ShouldReport(e) && Sw.Fail(e));`
         * @param {string} key The sequence (ex: "contacts.search"; the same key returns the same sequence).
         * @returns {{Key: string, Send: function(string, *, object=): Promise<*>, Read: function(string, *, object=): Promise<*>,
         *  Get: function(string, object=): Promise<*>, Run: function(function(AbortSignal): Promise<*>): Promise<*>, Stop: function(): void,
         *  IsLatest: function(number): boolean, Seq: number}} The sequence (Seq = the number of the newest call).
         */
        Latest(key) {
            key = String(key ?? "");
            if (!key && Sw.DevWarn)
                Sw.DevWarn("Sw.Net.Latest(key): pass a key that names the sequence (ex: \"contacts.search\"); every call without one shares one sequence.", "net.latest.key");
            let q = latest.get(key);
            if (q)
                return q;
            let ctl = null;
            q = {
                Key: key,
                Seq: 0,
                IsLatest: n => n === q.Seq,
                /** Supersede the running call (its answer is dropped; a read is aborted). */
                Stop() {
                    ++q.Seq;
                    if (ctl) {
                        const c = ctl;
                        ctl = null;
                        try {
                            c.abort(superseded());
                        }
                        catch {
                        }
                    }
                },
                /**
                 * Run any work in the sequence: fn(signal) (the signal aborts when a newer call starts; a write sent by Sw.Net with it is
                 * never aborted: Sw.Cancel model) -> its value when it is still the newest, else an AbortError.
                 */
                async Run(fn) {
                    q.Stop();
                    const n = q.Seq;
                    const c = Sw.Cancel && typeof Sw.Cancel.Controller === "function" ? Sw.Cancel.Controller() : new AbortController();
                    ctl = c;
                    let v, err = null;
                    try {
                        v = await fn(c.signal);
                    }
                    catch (e) {
                        err = e;
                    }
                    if (ctl === c)
                        ctl = null;
                    if (n !== q.Seq)
                        throw superseded();
                    if (err)
                        throw err;
                    return v;
                },
                Send: (url, arg, opts) => q.Run(signal => Sw.Net.Send(url, arg, Object.assign({}, opts || {}, { Signal: signal }))),
                Read: (url, arg, opts) => q.Run(signal => Sw.Net.Send(url, arg, Object.assign({}, opts || {}, { Signal: signal, ReadOnly: true }))),
                Get: (url, opts) => q.Run(signal => Sw.Net.Get(url, Object.assign({}, opts || {}, { Signal: signal }))),
            };
            latest.set(key, q);
            return q;
        },

        /**
         * What the server says happened to a failed request, from its "X-Sw-Outcome" header (never guessed from the status code):
         * "AlreadyDone" (the goal is met already: say "not needed"), "Gone" (it no longer exists), "Refused" (not done: any other 4xx,
         * also a 404 / 409 / 410 without the header), "Denied" (the user's role doesn't allow it), "Invalid" (the input was not
         * accepted); "" for a network error, a 5xx, an abort or a value that is not an error of the server.
         * @param {*} e The error.
         * @returns {string} The kind.
         */
        OutcomeKind: outcomeKind,

        /** Serialize with "$type" first (see toTypedJson). */
        ToTypedJson: toTypedJson,

        /**
         * Call an api with one argument (see the file header for the encoding).
         * @param {string} url - Api url.
         * @param {*} arg - The argument (serialized with ToTypedJson).
         * @param {object} [opts] - Options:
         *  Reload {boolean} bypass the cache; Post {boolean} force POST (true) or allow GET encodings (false);
         *  Blob {boolean} return a Blob; Text {boolean} return the text; Read {function(Response)} custom body reader (a function;
         *  Read: true is taken as ReadOnly with a developer warning); ReadOnly {boolean} the api only reads (a Cancel may abort it:
         *  Sw.Net.Read); AbortSafe {boolean} the server stops / undoes the work when the request is aborted; without them a Send is a
         *  WRITE: a signal of the cancellation model (Sw.Cancel.Controller, Sw.Activity.Run) never aborts it once it was sent;
         *  ErrHandler {async function(number):boolean} called on errors (and redirects), return true to retry;
         *  Setup {function(Request)} adjust each Request; Signal {AbortSignal}; Retry {number|"online"} automatic retries on
         *  network errors and 502/503/504 ("online": wait for the connection and send again, at most RetryMs (default 5 min);
         *  only for reads and saves that are safe to repeat); NoNavigate {boolean} throw on redirects instead of navigating;
         *  Detached {boolean} (alias Background) not the work of a click: never makes a button busy or abortable (it travels with the
         *  request: Send, Get and their inner fetch).
         * @returns {Promise<*>} The decoded json (undefined if not decodable), Blob/text. A redirect (not signed in) navigates to the
         *  sign in page and does not settle (see handleRedirect).
         */
        async Send(url, arg, opts) {
            opts = requestOptions("Sw.Net.Send", opts);
            const write = isWrite(opts, "Send");
            let usePost = opts.Post;
//DEBUG_BEGIN
            if (typeof (usePost) === 'undefined')
                usePost = !(await debugOptimizedSend());
//DEBUG_END
            const json = toTypedJson(arg);
            const cacheMode = opts.Reload ? "reload" : "default";
            let getUrl = null;
            let compData = null;
            if ((!usePost) && (json.length < 16384)) {
                let uri = encodeURIComponent(json);
                if (uri.length < 128) {
                    getUrl = url + "?" + uri;
                } else {
                    let data = new TextEncoder().encode(json);
                    let testData = null;
                    try {
                        testData = await Sw.Util.Deflate(data);
                    }
                    catch {
                    }
                    if (testData && (testData.length < ((data.length * 7) >> 3))) {
                        compData = testData;
                        data = testData;
                    }
                    const compDataStr = (compData ? "_d" : "_u") + Sw.Util.Base64Encode(data, true);
                    if (compDataStr.length < uri.length)
                        uri = compDataStr;
                    if (compDataStr.length < 4096)
                        getUrl = url + "?" + uri;
                }
            }
            //  (the page language: the server answers cacheable api calls with "Vary: X-Sw-Lang", so the browser keeps a copy per language
            //  and a language change doesn't show cached texts of the old language; same origin only, another origin would need a preflight)
            const lang = langHeader(url);
            /** @returns {Request} A fresh request for one attempt. */
            const make = () => {
                let r;
                if (getUrl) {
                    r = new Request(getUrl, { method: "GET", mode: "cors", cache: cacheMode, headers: lang });
                } else if (compData) {
                    r = new Request(url, {
                        method: "POST", mode: "cors", cache: cacheMode,
                        headers: Object.assign({ "Content-Type": "application/json", "Content-Encoding": "deflate" }, lang),
                        body: compData,
                    });
                } else {
                    r = new Request(url, {
                        method: "POST", mode: "cors", cache: cacheMode,
                        headers: Object.assign({ "Content-Type": "application/json" }, lang),
                        body: json,
                    });
                }
                if (opts.Setup)
                    opts.Setup(r);
                return r;
            };
            const errHandler = opts.ErrHandler;
            for (let count = 0; ; ++count) {
                const res = await fetchRetry(make, opts, url, write);
                if ((res.status != 200) || (res.redirected && errHandler)) {
                    if (errHandler) {
                        if (await errHandler(res.status)) {
                            --count;
                            continue;
                        }
                    }
                    if (res.status === 401)
                        return handleUnauthorized(res, count, opts, url, "\n");
                    throw await responseError(res, url, "\n");
                }
                if (res.redirected)
                    return handleRedirect(res, count, opts, url);
                return await readBody(res, opts);
            }
        },

        /**
         * GET an url (an api argument may be appended as "?<json>").
         * @param {string|Request} url - Url or a prepared Request (then opts.Setup is not used).
         * @param {object} [opts] - Options: Reload, Blob, Text, Read, ReadOnly, AbortSafe, Setup, Signal, Retry, NoNavigate, Detached (see
         * Send; a Get is a read).
         * @returns {Promise<*>} The decoded json (undefined if not decodable), Blob/text/Read result (a redirect: see Send).
         */
        async Get(url, opts) {
            opts = requestOptions("Sw.Net.Get", opts);
            const isReq = typeof url !== "string";
            const u = isReq ? url.url : url;
            /** @returns {Request} A request for one attempt. */
            const make = () => {
                if (isReq)
                    return url.clone();
                const r = new Request(url, { method: "GET", mode: "cors", cache: opts.Reload ? "reload" : "default" });
                if (opts.Setup)
                    opts.Setup(r);
                return r;
            };
            const res = await fetchRetry(make, opts, u, false);
            if (res.status === 401)
                return handleUnauthorized(res, 0, opts, u, ": ");
            if (res.status != 200)
                throw await responseError(res, u, ": ");
            if (res.redirected)
                return handleRedirect(res, 0, opts, u);
            return await readBody(res, opts);
        },

        /**
         * Post data without waiting (navigator.sendBeacon; data is sent as a json Blob).
         * @param {string} url - Target url.
         * @param {*} [data] - Payload.
         * @returns {boolean} true if queued.
         */
        Beacon(url, data) {
            try {
                if (data)
                    return navigator.sendBeacon(url, new Blob([JSON.stringify(data)], { type: "application/json" }));
                return navigator.sendBeacon(url);
            }
            catch {
                return false;
            }
        },

        /**
         * Download an url (temporary hidden download link).
         * @param {string} url - Url.
         * @param {string} [filename] - Suggested name (default the last path segment, without the query, uri decoded: Sw.Url.FileName).
         */
        Download(url, filename) {
            if (!filename)
                filename = Sw.Url.FileName(url);
            clickDownload(url, filename);
        },

        /**
         * Download a text as a file (no server round trip).
         * @param {string} filename - Suggested name.
         * @param {string} text - Content (UTF-8).
         */
        DownloadText(filename, text) {
            clickDownload('data:text/plain;charset=utf-8,' + encodeURIComponent(text), filename);
        },

        /**
         * Get a value from the memoization cache or by calling fetchFn (concurrent misses share one call;
         * failures are not cached). Keyed by name + "|" + JSON(key).
         * @param {string} name - Cache name (e.g. an api url).
         * @param {*} key - Key object (e.g. the request argument).
         * @param {number} lifeTime - Milliseconds to re-use the value.
         * @param {function(*, string):Promise<*>} fetchFn - Called as fetchFn(key, name).
         * @returns {Promise<*>}
         */
        async Cached(name, key, lifeTime, fetchFn) {
            const k = "" + name + "|" + JSON.stringify(key);
            const now = Date.now();
            const ce = cache.get(k);
            if (ce && (now < ce.Exp))
                return await ce.Value;
            const p = Promise.resolve().then(() => fetchFn(key, name));
            cache.set(k, { Value: p, Exp: now + lifeTime });
            if (!pruning) {
                pruning = true;
                setTimeout(prune, 5000);
            }
            try {
                return await p;
            }
            catch (e) {
                if (cache.get(k)?.Value === p)
                    cache.delete(k);
                throw e;
            }
        },

        /**
         * Cached GET.
         * @param {string} url - Url.
         * @param {number} lifeTime - Milliseconds to re-use.
         * @returns {Promise<*>}
         */
        CachedGet: (url, lifeTime) => Sw.Net.Cached("_Greq" + url, null, lifeTime, () => Sw.Net.Get(url)),

        /**
         * Cached Send.
         * @param {string} url - Api url.
         * @param {*} arg - Argument (part of the key).
         * @param {number} lifeTime - Milliseconds to re-use.
         * @returns {Promise<*>}
         */
        CachedSend: (url, arg, lifeTime) => Sw.Net.Cached("_Sreq" + url, arg, lifeTime, () => Sw.Net.Send(url, arg)),

        /**
         * Absolute url of an api below the site root, e.g. Api("application/GetMessages").
         * @param {string} path - Path below "Api/".
         * @returns {string}
         */
        Api: path => Sw.Url.Root + "Api/" + path,
    }));

    //  The option schemas of the public apis (Sw.DevOptions registry: checked in developer mode, listed by the walker)
    if (Sw.DevOptions && Sw.DevOptions.Register) {
        const MessageOptions = { Do: "string", Always: "boolean" };
        Sw.DevOptions.Register("Sw.Net.Send", SendOptions);
        Sw.DevOptions.Register("Sw.Net.Read", SendOptions);
        Sw.DevOptions.Register("Sw.Net.Live", SendOptions);
        Sw.DevOptions.Register("Sw.Net.Get", GetOptions);
        Sw.DevOptions.Register("Sw.Net.Message", MessageOptions);
        Sw.DevOptions.Register("Sw.Net.MessageParts", MessageOptions);
        Sw.DevOptions.Register("Sw.Net.FailText", MessageOptions);
        Sw.DevOptions.NoOptions("Sw.Net.IsWrite", "opts is the request's options (Send / Get) or a Request: read, not checked");
    }
})();
