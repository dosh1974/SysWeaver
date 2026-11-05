/**
 * @file core/session.js - server push messages (the session manager) and server time.
 *
 * Purpose: `Sw.Session`, a wire compatible re-implementation of the old SessionManager, so that old and new
 * windows of the same origin share ONE master and exchange messages:
 *  - Master election: one top-level window per browser origin is the master (Web Lock "SysWeaver.SessionManager"
 *    when navigator.locks exists, plus InterOp "SessionManager.Ping" / "SessionManager.Pong" {To} with a 100 ms
 *    timeout). Other top windows are children and retry the election every second. Exactly one master: the master
 *    holds the Web Lock "SysWeaver.SessionManager.Master" while it is the master, so a window whose Ping the master
 *    answers late (a busy or throttled tab) stays a child; a master that gets a late Pong (another master: an old
 *    common.js window, or no Web Locks) steps down; a frozen master releases the lock. Without Web Locks (http on another host than
 *    localhost) the master also Pings every second and a Pong of another master carries "Ms" (when it became master): the younger
 *    master steps down, so two masters heal within about a second (AS6b-1). Frames (same-origin non-top
 *    windows) are never master, unless started with the url flag "top=true" (a frame whose same-origin host page
 *    does not load the SysWeaver core then takes part in the election like a top window, see actsAsTop).
 *  - The master long-polls `Api/application/GetMessages` (POST json {Cc, MessageTypes}; empty answers that return at once again and
 *    again back off 1 s .. 30 s); the reply is
 *    {Cc, Prefix, Messages} or null (90 s timeout, keep Cc). Every message gets the Prefix and is re-posted to
 *    all windows with InterOp (From = "Server"). After 3 consecutive failures "server.error" is posted (and Cc
 *    reset to 0); the next success posts "server.restore". Failures wait 5 s; a change of the subscribed types
 *    aborts the pending poll so it is re-issued with the new MessageTypes. After any failed poll the next one asks with Cc 0
 *    (the answer "server.connect" names the server instance): another instance reloads the page (a restart, also a quick one),
 *    the same instance (a network drop) keeps the Cc and nothing reloads.
 *  - Handover: when the master page is hidden/closed it posts "SessionManager.MasterClosed" {Cc, ServerEvents:
 *    [[name, count]]}; a child takes over with the same Cc and subscriptions.
 *  - Lost master (crashed, killed or frozen tab: no MasterClosed): the window that wins the election (polling, or a new
 *    window) only knows its own subscriptions, so it posts "SessionManager.GetEvents" and every other window and frame
 *    answers "SessionManager.OwnedEvents" {To, Events: [[name, count]]} with the subscriptions it owns. AddEvents /
 *    RemoveEvents of a window that has not answered yet are held back for up to 2 s (its answer already contains them;
 *    for a window that never answers, ex: an old common.js copy, they are applied when the 2 s are over); a late answer
 *    only adds the types the master does not know yet.
 *  - Subscriptions are ref counted and lower-cased: AddEvent / RemoveEvent post "SessionManager.AddEvents" /
 *    "SessionManager.RemoveEvents" {Events: [names]} to the master. Subscribe(type, fn) / Unsubscribe add a
 *    message handler (and the subscription). Forced types (user.login, user.logout, server.shutdown,
 *    server.pause, server.continue, server.restart, reload, refresh, filereload) need no subscription.
 *    Messages of unsubscribed types are dropped by the server, so subscribe before they are needed.
 *  - Server time: the master reads `serverTime` (first call "?<IANA time zone>;<language>"), estimates the
 *    offset (half the round trip as latency), smooths changes in 500 ms steps and distributes it with
 *    "SessionManager.TimeOffset" {O, Z?, To?} (Z: the server's UTC offset in minutes when known); others ask with "SessionManager.GetTimeOffset".
 *  - Default reactions (InitReactions, called by core/session_start.js): in top windows "reload" (reload, clear hash),
 *    "refresh" (reload), "user.login" / "user.logout" (reload after 100 ms, the hash cleared, as the old common.js; logout first
 *    calls navigator.credentials.preventSilentAccess) unless IgnoreUserChanges() was called or a page's own sign out handler
 *    (HandleSignOut; the app shell has none) handles it; SignedOut() posts "SysWeaver.SignedOut" {Top} at once from the window that
 *    signed out, which reloads the top windows the same way before the server's push arrives, and notes the sign out in the tab
 *    (sessionStorage "SysWeaver.SignedOut", NoteSignedOut / TakeSignedOut: the reloaded shell says "You are signed out" once), "server.connect" /
 *    "server.reconnect" (reload when the server instance changed, sessionStorage "SysWeaver.ServerInstance"; a window
 *    that knows no instance yet never reloads: it learns the instance from the master's answer to
 *    "SessionManager.GetTimeOffset" (field Si) or from the connect / reconnect message itself),
 *    and the block events: Sw.Events "server.block" {Type, Title, Text, Icon} for server.pause / restart /
 *    shutdown / error ("Server communication lost.", as the original; BlockOnLost = false leaves it to the calm offline line of
 *    Sw.Net / the shell, UX guide FBK-9; while blocked the first block stays, as the original) and "server.unblock" {Type} for
 *    server.continue / restore / (re)connect (the ui library shows the overlay, else core/session_start.js its fallback). The poll and
 *    the server time sync results decide the connection state (Sw.Net.NoteFailure(true) / NoteSuccess: no probe on top of them).
 *    WarnTimeOut({At, Extend}) asks the app shell to warn before a session time-out (ACC-7).
 *    In all windows "filereload" re-validates the changed urls and refreshes the
 *    src/href/data attributes and inline background/mask images that reference them.
 *    Sw.Events also gets "server.message" (msg) for every server message and the forced type names.
 *  - Back/forward cache: a page restored from the bfcache restarts its session manager and re-subscribes.
 *  - Reload(flushCache, clearHash): the page reload of the reactions (Sw.Page.Reload when the full core is loaded, else ReloadNow);
 *    IsTop: this window acts as a top window (see actsAsTop).
 *
 * Part of the small messaging bundle sw/session.js: works with the kernel only. Sw.Net (connection state, NoteFailure / NoteSuccess /
 * Probe / Online), Sw.Format (server zone), Sw.Page (Reload) and Sw.DevOptions are used when present (sw/core.js).
 * Load order: after core/interop.js. Dependencies: Sw.InterOp, Sw.Url, Sw.Util (Delay, WaitEvent), Sw.Storage, Sw.Events, Sw.Net.FlushCache
 * (core/include.js), the translation markers (core/i18n.js). Message buffering starts when this file is evaluated; core/session_start.js starts the manager.
 * Globals defined: none (Sw.Session).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const InterOp = Sw.InterOp;

    const prefix = "SessionManager";
    const logPrefix = prefix + ": ";
    const msgPing = prefix + ".Ping";
    const msgPong = prefix + ".Pong";
    const msgMasterClosed = prefix + ".MasterClosed";
    const msgAddEvents = prefix + ".AddEvents";
    const msgRemoveEvents = prefix + ".RemoveEvents";
    const msgTimeOffset = prefix + ".TimeOffset";
    const msgGetTimeOffset = prefix + ".GetTimeOffset";
    const msgGetEvents = prefix + ".GetEvents";
    const msgOwnedEvents = prefix + ".OwnedEvents";

    /** Time (ms) a new master waits for the "SessionManager.OwnedEvents" answers (see the file header). */
    const gatherTime = 2000;

    /** Web Lock name used for the master election. */
    const lockName = "SysWeaver.SessionManager";

    /** Web Lock the master holds while it is the master (exactly one master, see holdMasterLock). */
    const masterLockName = "SysWeaver.SessionManager.Master";

    /** Time (ms) a window that takes over after "SessionManager.MasterClosed" waits for the closing master to release its lock. */
    const handoverLockWait = 1000;

    /** sessionStorage key of the last seen server instance. */
    const serverInstanceKey = "SysWeaver.ServerInstance";

    /** The message types the server always delivers. */
    const forcedTypes = ["user.logout", "user.login", "server.shutdown", "server.pause", "server.continue", "server.restart", "reload", "refresh", "filereload"];

    // ------------------------------------------------------------------ Start up buffering

    /** @type {Array<object>|null} InterOp events received before the master started (replayed by the master). */
    let startUp = null;

    /** @type {boolean} True while startUp buffering is active. */
    let buffering = false;

    /** Raw InterOp listener used while buffering. @param {object} ev - Event. */
    function bufferMsg(ev) {
        startUp.push(ev);
    }

    /** Start buffering InterOp events (no-op if already buffering). */
    function startBuffer() {
        if (buffering)
            return;
        buffering = true;
        startUp = [];
        InterOp.AddListener(bufferMsg);
    }

    /**
     * Stop buffering.
     * @returns {Array<object>|null} The buffered events (null if not buffering).
     */
    function stopBuffer() {
        if (!buffering)
            return null;
        buffering = false;
        InterOp.RemoveListener(bufferMsg);
        const s = startUp;
        startUp = null;
        return s;
    }

    startBuffer();

    // ------------------------------------------------------------------ Subscriptions

    /** @type {Map<string, number>} All server events (ref counted); on the master: of all windows. */
    let serverEvents = new Map();

    /** @type {Map<string, number>} Server events registered by this window (ref counted). */
    let ownedEvents = new Map();

    /**
     * Increment a ref count.
     * @param {Map<string, number>} map - Map.
     * @param {string} name - Lower-case name.
     * @param {boolean} [isOwned] - Also count in ownedEvents.
     * @returns {boolean} true if this was the first reference.
     */
    function addRef(map, name, isOwned) {
        const count = (map.get(name) || 0) + 1;
        map.set(name, count);
        if (isOwned)
            addRef(ownedEvents, name);
        return count === 1;
    }

    /**
     * Decrement a ref count (the key is removed at 0).
     * @param {Map<string, number>} map - Map.
     * @param {string} name - Lower-case name.
     * @param {boolean} [isOwned] - Also decrement in ownedEvents.
     * @returns {boolean} true if this was the last reference.
     */
    function decRef(map, name, isOwned) {
        if (isOwned)
            decRef(ownedEvents, name);
        let count = map.get(name);
        if (!count) {
            map.delete(name);
            return false;
        }
        --count;
        if (count !== 0) {
            map.set(name, count);
            return false;
        }
        map.delete(name);
        return true;
    }

    /**
     * Start receiving a server message type (ref counted).
     * @param {string} name - Message type (case insensitive).
     * @returns {boolean} true if this was the first subscription (in this window).
     */
    function addEvent(name) {
        name = name.toLowerCase();
        if (addRef(serverEvents, name, true)) {
            InterOp.Post(msgAddEvents, { Events: [name] });
            return true;
        }
        return false;
    }

    /**
     * Stop receiving a server message type (ref counted).
     * @param {string} name - Message type (case insensitive).
     * @returns {boolean} true if this was the last subscription (in this window).
     */
    function removeEvent(name) {
        name = name.toLowerCase();
        if (decRef(serverEvents, name, true)) {
            InterOp.Post(msgRemoveEvents, { Events: [name] });
            return true;
        }
        return false;
    }

    /** @type {Map<string, Function[]>|null} Lower-case message type -> handlers (Subscribe). */
    let handlerMap = null;

    /**
     * Get (creating on first use) the handler map; installs a raw InterOp listener that awaits every handler
     * of the message type in order.
     * @returns {Map<string, Function[]>}
     */
    function getHandlerMap() {
        if (handlerMap)
            return handlerMap;
        const map = handlerMap = new Map();
        InterOp.AddListener(async ev => {
            const m = InterOp.GetMessage(ev);
            const t = m.Type;
            if (!t)
                return;
            const fns = map.get(t.toLowerCase());
            if (!fns)
                return;
            const list = fns.slice();
            const l = list.length;
            for (let i = 0; i < l; ++i) {
                try {
                    await list[i](m);
                }
                catch (e) {
                    console.error(logPrefix + "handler of \"" + t + "\" failed: " + (e && e.message ? e.message : e));
                }
            }
        });
        return map;
    }

    // ------------------------------------------------------------------ Server time

    /** @type {number|undefined} Server time offset in ms (server = browser + offset). */
    let timeOffset;

    /**
     * Estimated server tick (ms since 1970).
     * @returns {number}
     */
    function serverTick() {
        const now = Date.now();
        return typeof timeOffset === "number" ? now + timeOffset : now;
    }

    /**
     * The server's UTC offset in minutes as this window knows it (Sw.Format.ServerZone), sent with "SessionManager.TimeOffset" (Z) so
     * every window (other tabs too: session storage is per tab) learns it with the first time offset.
     * @returns {number|undefined} The offset, undefined = not known.
     */
    function zoneNow() {
        try {
            const z = Sw.Format && Sw.Format.ServerZone ? Sw.Format.ServerZone() : undefined;
            return typeof z === "number" ? z : undefined;
        }
        catch {
            return undefined;
        }
    }

    /**
     * Adopt the server zone of a "SessionManager.TimeOffset" message (Z), before its offset: a window that shows a clock then decides its
     * label (server time / your time) once, with the zone (shell connection line).
     * @param {object} data The message.
     */
    function adoptZone(data) {
        if (typeof data.Z === "number" && Sw.Format && Sw.Format.SetServerZone && zoneNow() !== data.Z)
            Sw.Format.SetServerZone(data.Z);
    }

    // ------------------------------------------------------------------ Session manager

    /** @type {boolean} True once Init ran in this window (reset when the page is hidden). */
    let started = false;

    /** @type {boolean} True while this window is the master. */
    let isMasterNow = false;

    /** @type {boolean} True once InitReactions ran. */
    let reactionsInstalled = false;

    /** @type {boolean} True once the bfcache restore listener is installed. */
    let restoreHooked = false;

    /** @type {string[]} Server events released when the page was hidden (re-added if restored from the bfcache). */
    let released = [];

    /** @type {boolean} Ignore user changes (no reloads on login / logout / reload / refresh). */
    let ignoreUser = false;

    /**
     * The window's sign out handler (Sw.Session.HandleSignOut, the app shell): asked first on "user.logout" and on a sign out in this
     * browser (Sw.Session.SignedOut); true = handled (no reload).
     * @type {function(object):(boolean|Promise<boolean>)|null}
     */
    let signOutHandler = null;

    /** InterOp message type of a sign out in this browser, posted at once by the signing out window (Sw.Session.SignedOut). */
    const msgSignedOut = "SysWeaver.SignedOut";

    /** @type {string|null} The server instance known to this window (top windows, set by the default reactions). */
    let knownInstance = null;

    /**
     * Same-origin test of the top window (a cross-origin top makes this window a "top" for the session). A frame started
     * with the url flag "top=true" also acts as a top window (its same-origin host does not load the SysWeaver core,
     * so no session manager would relay server messages to it).
     * @returns {boolean} true if this window acts as a top window.
     */
    function actsAsTop() {
        if (Sw.Url.Get("top") === "true")
            return true;
        const wtop = window.top;
        const wself = window.self;
        if (wtop === wself)
            return true;
        try {
            return wtop.location.origin !== wself.location.origin;
        }
        catch {
            return true;
        }
    }

    /** Re-subscribe after a bfcache restore and restart the manager. */
    function hookRestore() {
        if (restoreHooked)
            return;
        restoreHooked = true;
        window.addEventListener("pageshow", ev => {
            if (!ev.persisted || started)
                return;
            Sw.Log(logPrefix + "Page restored from the back/forward cache, restarting " + InterOp.Id);
            serverEvents = new Map();
            ownedEvents = new Map();
            const evs = released;
            released = [];
            evs.forEach(e => addEvent(e));
            init();
        });
    }

    /**
     * Collect the owned events (one entry per reference) and release them from serverEvents / ownedEvents.
     * @returns {string[]} The released names.
     */
    function releaseOwned() {
        const evs = [];
        ownedEvents.forEach((v, k) => {
            for (let i = 0; i < v; ++i)
                evs.push(k);
        });
        evs.forEach(v => decRef(serverEvents, v, true));
        return evs;
    }

    /**
     * Answer a new master's "SessionManager.GetEvents" with the subscriptions this window owns ([[name, count]]).
     * @param {object} data - The GetEvents message.
     */
    function answerGetEvents(data) {
        if (data.From === InterOp.Id)
            return;
        InterOp.Post(msgOwnedEvents, { To: data.From, Events: Array.from(ownedEvents.entries()) });
    }

    /**
     * Start the session manager in this window (once; see the file header).
     * @returns {Promise<void>}
     */
    async function init() {
        if (started)
            return;
        started = true;
        hookRestore();
        const id = InterOp.Id;

        if (!actsAsTop()) {
            //  Frames: never master, release owned events when hidden, adopt the master's time offset
            stopBuffer();
            const off = InterOp.On(msgTimeOffset, data => {
                if ((data.From !== id) && (typeof data.O === "number")) {
                    adoptZone(data);
                    timeOffset = data.O;
            }
            });
            //  A master that took over without a handover asks every window for its subscriptions
            const offGet = InterOp.On(msgGetEvents, answerGetEvents);
            /** pagehide handler of a frame. */
            const frameClose = () => {
                window.removeEventListener("pagehide", frameClose);
                off();
                offGet();
                started = false;
                Sw.Log(logPrefix + "Stopping iframe " + id);
                const evs = releaseOwned();
                released = evs;
                if (evs.length > 0) {
                    Sw.Log(logPrefix + "Unregistering owned events: " + evs);
                    InterOp.Post(msgRemoveEvents, { Events: evs });
                }
            };
            window.addEventListener("pagehide", frameClose);
            InterOp.Post(msgGetTimeOffset);
            return;
        }

        Sw.Log(logPrefix + "Top window " + id + " \"" + window.location.href + "\" initialized.");
        let isMaster = false;
        let gotMasterPong = false;
        /** Internal event target for "GotPong". */
        const events = new EventTarget();
        /** Aborted to restart the master's poll (subscription change). */
        let masterAbort = new AbortController();
        /** Change counter of the last poll response. */
        let cc = 0;
        /** True once this instance was stopped (page hidden). */
        let stopped = false;
        /** Counts the terms as master: a poll loop or time sync of an earlier term (before a step down) ends. */
        let masterTerm = 0;
        /** True while this window is the master of the term (the loops of an earlier term end). @param {number} term - The term. */
        const isTerm = term => isMaster && (term === masterTerm);
        /** Releases the master lock (see holdMasterLock), null while not held. @type {function()|null} */
        let releaseMasterLock = null;

        /**
         * Take the Web Lock "SysWeaver.SessionManager.Master" that the master holds for as long as it is the master. The Ping / Pong
         * alone cannot tell a missing master from a slow one: a master tab that is busy or throttled (a background tab, a loaded
         * machine) may answer the Ping after the 100 ms, and the asking window became a second master (two pollers, every server
         * message relayed twice). The lock makes it exact: only one window holds it. No Web Locks (an http page on another host than
         * localhost) or an error: true, the Ping / Pong decides alone (as the old common.js).
         * @param {number} waitMs - 0: only when it is free now; else wait up to this long (a handover: the closing master releases it).
         * @returns {Promise<boolean>} true when held (or no Web Locks), false when another window holds it.
         */
        function holdMasterLock(waitMs) {
            if (releaseMasterLock || !navigator.locks)
                return Promise.resolve(true);
            return new Promise(resolve => {
                let opt = { ifAvailable: true };
                if ((waitMs > 0) && (typeof AbortSignal.timeout === "function"))
                    opt = { signal: AbortSignal.timeout(waitMs) };
                navigator.locks.request(masterLockName, opt, lock => {
                    if (!lock || stopped) {
                        resolve(false);
                        return null;
                    }
                    return new Promise(release => {
                        releaseMasterLock = release;
                        resolve(true);
                    });
                }).catch(e => resolve(!(e && (e.name === "AbortError" || e.name === "TimeoutError"))));
            });
        }

        /** Release the master lock (step down, page hidden or frozen). */
        function dropMasterLock() {
            const r = releaseMasterLock;
            releaseMasterLock = null;
            if (r)
                r();
        }

        /**
         * Periodic server time synchronisation (works only while master); re-schedules itself.
         * @param {number} term - The master term (masterTerm) it belongs to: it ends with the term.
         */
        function startServerTimeSync(term) {
            let haveFirst = false;
            const base = Sw.Url.Root + "serverTime";
            const firstUrl = base + "?" + Intl.DateTimeFormat().resolvedOptions().timeZone + ";" + navigator.language;
            let nextUpdate = 5000;
            let updateDelay = 2;
            /** One sync step. @returns {Promise<void>} */
            const update = async () => {
                try {
                    if (!isTerm(term))
                        return;
                    //  (also while the connection is lost: its requests are the original's, and with the poll they decide when the
                    //  server is back; Sw.Net sends no probe of its own in the master, S6-Ops-3)
                    const start = Date.now();
                    let res;
                    try {
                        res = await fetch(haveFirst ? base : firstUrl, { method: "GET", mode: "cors", cache: "reload" });
                    }
                    catch (e) {
                        //  One connection state (Sw.Net.Online): a failed time sync did not reach the server (decides, no probe: S6-Ops-3)
                        if (Sw.Net && Sw.Net.NoteFailure)
                            Sw.Net.NoteFailure(true);
                        throw e;
                    }
                    if (res.status === 502 || res.status === 504) {
                        if (Sw.Net && Sw.Net.NoteFailure)
                            Sw.Net.NoteFailure(true);
                    }
                    else if (Sw.Net && Sw.Net.NoteSuccess)
                        Sw.Net.NoteSuccess();
                    if (res.status == 200) {
                        const text = await res.text();
                        const browserTime = Date.now();
                        const val = parseInt(text);
                        //  a server may append its UTC offset in minutes ("1760918400000;120"); old servers send only the
                        //  ms (parseInt reads the ms either way). Shared with the frames through session storage (Sw.Format.ServerZone).
                        const zp = String(text).split(";")[1];
                        if (zp !== undefined && zp.trim() !== "" && isFinite(+zp) && Sw.Format && Sw.Format.SetServerZone)
                            Sw.Format.SetServerZone(+zp);
                        const latency = (browserTime - start) * 0.5;
                        const offset = val + latency - browserTime;
                        if (!haveFirst) {
                            haveFirst = true;
                            timeOffset = offset;
                            Sw.Log("Initial server time adjustment: " + offset + " ms (latency: " + latency + " ms)");
                            InterOp.Post(msgTimeOffset, { O: offset, Z: zoneNow() });
                        } else {
                            const dstart = timeOffset;
                            if (offset !== dstart) {
                                const doffset = offset - dstart;
                                let step = 0;
                                /** Move the offset towards the new value in 500 ms steps. */
                                const updateTime = () => {
                                    if (!isTerm(term))
                                        return;
                                    ++step;
                                    if (step >= updateDelay) {
                                        timeOffset = offset;
                                        InterOp.Post(msgTimeOffset, { O: offset, Z: zoneNow() });
                                        return;
                                    }
                                    const offs = (doffset * step) / updateDelay + dstart;
                                    timeOffset = offs;
                                    InterOp.Post(msgTimeOffset, { O: offs, Z: zoneNow() });
                                    setTimeout(updateTime, 500);
                                };
                                updateTime();
                            }
                        }
                        setTimeout(update, nextUpdate);
                        if (nextUpdate < 300000) {
                            nextUpdate *= 2;
                            if (updateDelay < 30)
                                updateDelay *= 2;
                        }
                        return;
                    }
                }
                catch {
                }
                if (isTerm(term))
                    setTimeout(update, haveFirst ? 15000 : 2000);
            };
            update();
        }

        /** Abort the pending poll so it is re-issued with the new MessageTypes. */
        function serverEventsChanged() {
            try {
                if (!masterAbort.signal.aborted)
                    masterAbort.abort();
            }
            catch {
            }
        }

        /**
         * The master loop (see the file header).
         * @param {number} [startCc] - Change counter to continue from.
         * @returns {Promise<void>} Resolves when this window stops being master (or a new term started: a step down, then master again).
         */
        async function runMaster(startCc) {
            const term = ++masterTerm;
            cc = startCc ? startCc : 0;
            let errCount = 0;
            const sendErrorOn = 3;
            /**
             * After a failed poll the next one asks with Cc 0 (the server answers at once with "server.connect" {Value: its instance}):
             * a server that restarted while the poll was down is always noticed (the page reloads), also when it came back before the
             * 3rd failure (Cc reset) and its new session counter is not below this window's Cc (no "server.reconnect": before, such a
             * quick restart went unnoticed, as in the original). The same instance (a network drop): the window keeps its Cc, so no
             * message pushed meanwhile is lost, and nothing reloads (OWN3-Down-1).
             */
            let verify = false;
            /**
             * Count a failed poll.
             * @param {boolean} [lost] - The poll did not reach the server (a network error, 502 / 504).
             */
            const onError = lost => {
                ++errCount;
                verify = true;
                //  The connection state (Sw.Net.Online, the shell's offline line): a poll that did not reach the server decides it (its
                //  5 s retry says when the server is back: no probe on top, as the original's requests, S6-Ops-3); another failure probes
                if (Sw.Net && Sw.Net.NoteFailure)
                    Sw.Net.NoteFailure(!!lost);
                if (errCount === sendErrorOn) {
                    InterOp.Post("server.error");
                    cc = 0;
                }
            };
            startServerTimeSync(term);
            const startM = stopBuffer();
            if (startM) {
                const l = startM.length;
                for (let i = 0; i < l; ++i)
                    await eventHandler(startM[i]);
            }
            const url = Sw.Url.Root + "Api/application/GetMessages";
            /** @type {number} Empty answers in a row that came back at once (the long poll is not held: a proxy, a misconfigured server). */
            let quick = 0;
            /**
             * Wait before the next poll (ends early when the subscriptions change: masterAbort).
             * @param {number} ms Milliseconds.
             * @returns {Promise<void>}
             */
            const pause = ms => new Promise(resolve => {
                const sig = masterAbort.signal;
                const tm = setTimeout(done, ms);
                function done() {
                    clearTimeout(tm);
                    sig.removeEventListener("abort", done);
                    resolve();
                }
                sig.addEventListener("abort", done);
            });
            while (isTerm(term)) {
                const verifying = verify && (cc !== 0);
                const body = JSON.stringify({
                    Cc: verifying ? 0 : cc,
                    MessageTypes: Array.from(serverEvents.keys()),
                });
                await Sw.Util.Delay(1);
                if (!isTerm(term))
                    break;
                if (masterAbort.signal.aborted) {
                    masterAbort = new AbortController();
                    continue;
                }
                try {
                    const t0 = Date.now();
                    const res = await fetch(url, {
                        method: "POST",
                        mode: "cors",
                        cache: "default",
                        headers: { "Content-Type": "application/json" },
                        body: body,
                        signal: masterAbort.signal,
                    });
                    if (res.status === 200) {
                        if (Sw.Net && Sw.Net.NoteSuccess)
                            Sw.Net.NoteSuccess();
                        const response = await res.json();
                        //  A long poll that comes back at once without messages again and again would spin (thousands of requests a
                        //  minute): back off 1, 2, 4 ... 30 s until the server holds the poll again or messages arrive
                        const had = !!(response && response.Messages && response.Messages.length);
                        if (!had && Date.now() - t0 < 500) {
                            ++quick;
                            if (quick >= 3 && Sw.DevWarn)
                                Sw.DevWarn("Sw.Session: " + url + " answers at once without messages (the long poll is not held: a proxy or the server ends it); polling backs off up to 30 s", "session.quickpoll");
                        }
                        else
                            quick = 0;
                        verify = false;
                        if (response && isTerm(term)) {
                            //  a verification (see verify): the same instance keeps the Cc of this window, another one (or none known) starts over
                            const con = verifying ? (response.Messages || []).find(m => m && m.Type === "server.connect") : null;
                            if (!(con && knownInstance && (con.Value === knownInstance)))
                                cc = response.Cc;
                            const pre = response.Prefix;
                            const msgs = response.Messages;
                            if (msgs) {
                                const nel = msgs.length;
                                for (let i = 0; i < nel; ++i) {
                                    const m = msgs[i];
                                    m.Prefix = pre;
                                    InterOp.Post(m.Type, m, true);
                                }
                            }
                        }
                        if (errCount >= sendErrorOn)
                            InterOp.Post("server.restore");
                        errCount = 0;
                        if (quick > 1 && isTerm(term))
                            await pause(Math.min(30000, 1000 * Math.pow(2, Math.min(5, quick - 2))));
                        continue;
                    }
                    onError(res.status === 502 || res.status === 504);
                }
                catch (e) {
                    if (e && e.name === "AbortError")
                        continue;
                    onError(true);
                }
                await Sw.Util.Delay(5000);
            }
        }

        /**
         * Collecting the subscriptions of the other windows after a take over without handover, else null.
         * Answered: ids of the windows that answered; Held: AddEvents / RemoveEvents of windows that did not answer yet.
         * @type {{Answered: Set<number>, Held: Map<number, object[]>}|null}
         */
        let gather = null;

        /**
         * Apply an AddEvents / RemoveEvents message of another window to serverEvents.
         * @param {object} data - The message.
         * @returns {boolean} true if the subscribed types changed.
         */
        function applyEvents(data) {
            const add = data.Type === msgAddEvents;
            let changed = false;
            (data.Events || []).forEach(e => {
                const n = ("" + e).toLowerCase();
                changed |= add ? addRef(serverEvents, n) : decRef(serverEvents, n);
            });
            return changed;
        }

        /**
         * AddEvents / RemoveEvents while master. The master counted its own subscriptions already in addEvent / removeEvent:
         * only re-issue the poll. Messages of a window that did not answer GetEvents yet are held back (see the file header).
         * @param {object} data - The message.
         */
        function masterEvents(data) {
            if (data.From === id) {
                serverEventsChanged();
                return;
            }
            const g = gather;
            if (g && !g.Answered.has(data.From)) {
                let list = g.Held.get(data.From);
                if (!list)
                    g.Held.set(data.From, list = []);
                list.push(data);
                return;
            }
            if (applyEvents(data))
                serverEventsChanged();
        }

        /**
         * Became master without a handover (initial or from polling): the subscriptions of the other windows are unknown,
         * start from the owned ones and ask every window ("SessionManager.GetEvents", see the file header).
         */
        function gatherEvents() {
            serverEvents = new Map(ownedEvents);
            const g = gather = { Answered: new Set(), Held: new Map() };
            InterOp.Post(msgGetEvents);
            setTimeout(() => {
                if (gather !== g)
                    return;
                gather = null;
                if (!isMaster)
                    return;
                //  Windows that never answered (ex: old pages): apply what they sent meanwhile
                let changed = false;
                g.Held.forEach(list => list.forEach(m => {
                    changed |= applyEvents(m);
                }));
                if (changed)
                    serverEventsChanged();
            }, gatherTime);
        }

        /** @type {Map<string, function(object):*>} Handlers while master. */
        const masterMap = new Map();
        masterMap.set(msgAddEvents, masterEvents);
        masterMap.set(msgOwnedEvents, data => {
            if ((data.To !== id) || (data.From === id))
                return;
            const g = gather;
            const evs = Array.isArray(data.Events) ? data.Events : [];
            let changed = false;
            if (g && !g.Answered.has(data.From)) {
                //  The answer includes everything the window sent before it: drop what was held back
                g.Answered.add(data.From);
                g.Held.delete(data.From);
                evs.forEach(e => {
                    if (!Array.isArray(e))
                        return;
                    const n = ("" + e[0]).toLowerCase();
                    const c = Math.max(0, Math.min(1000, Math.floor(+e[1] || 0)));
                    for (let i = 0; i < c; ++i)
                        changed |= addRef(serverEvents, n);
                });
            } else if (!g) {
                //  Late answer: its AddEvents were applied already, only add the types not known yet
                evs.forEach(e => {
                    if (!Array.isArray(e))
                        return;
                    const n = ("" + e[0]).toLowerCase();
                    if (!serverEvents.has(n))
                        changed |= addRef(serverEvents, n);
                });
            }
            if (changed)
                serverEventsChanged();
        });
        masterMap.set(msgPing, data => {
            //  (without Web Locks with Ms, when this window became master: another master compares it, see heartbeat)
            if (data.From !== id)
                InterOp.Post(msgPong, navigator.locks ? { To: data.From } : { To: data.From, Ms: masterSince });
        });
        masterMap.set(msgRemoveEvents, masterEvents);
        masterMap.set(msgGetTimeOffset, data => {
            //  Si: the server instance the master knows, so a new window does not reload on its first reconnect
            const m = { To: data.From, O: timeOffset, Z: zoneNow() };
            if (knownInstance)
                m.Si = knownInstance;
            InterOp.Post(msgTimeOffset, m);
        });

        /** @type {Map<string, function(object):*>} Handlers while child. */
        const childMap = new Map();
        childMap.set(msgPong, data => {
            if (data.To === id) {
                notePong();
                gotMasterPong = true;
                events.dispatchEvent(new Event("GotPong"));
            }
        });
        childMap.set(msgMasterClosed, async data => {
            startBuffer();
            if (data.From === id)
                return;
            const ev = data.ServerEvents;
            serverEvents = ev ? new Map(ev) : new Map();
            handover = { Cc: data.Cc };
            await startMaster(1, data.Cc);
        });
        childMap.set(msgTimeOffset, data => {
            if ((data.From !== id) && (typeof data.O === "number")) {
                adoptZone(data);
                timeOffset = data.O;
                }
        });
        childMap.set(msgGetEvents, answerGetEvents);

        /**
         * Raw InterOp listener: dispatch to masterMap or childMap.
         * @param {object} ev - Event.
         * @returns {Promise<void>}
         */
        async function eventHandler(ev) {
            const data = InterOp.GetMessage(ev);
            const t = data.Type;
            if (!t)
                return;
            const fn = (isMaster ? masterMap : childMap).get(t);
            if (fn)
                await fn(data);
        }
        InterOp.AddListener(eventHandler);

        /** Interval of the child's periodic election attempt. */
        let checkMasterTimer = null;

        /** When the last election Ping was posted (Date.now()). */
        let pingAt = 0;
        /** How long (ms) an election waits for the Pong: 100 (the old common.js), longer once the master answered late (notePong). */
        let pongWait = 100;
        /** When this window became the master (Date.now(); 0 while a child): without Web Locks two masters compare it (heartbeat). */
        let masterSince = 0;

        /**
         * Without Web Locks nothing makes the master exact: two windows can both become master (all children take over at once after
         * "MasterClosed", a master that answers late). The master Pings every second (the election's own message); another master
         * answers with a Pong that carries its "Ms" (when it became master), and the younger of the two steps down (equal: the higher
         * id), so two masters heal within about a second (AS6b-1). An old common.js master answers without Ms: the new window steps
         * down (the old one never would). With Web Locks the master lock makes the master exact and nothing extra is sent.
         * @param {number} term - The master term (masterTerm) it belongs to: it ends with the term.
         */
        function heartbeat(term) {
            const beat = () => {
                if (!isTerm(term) || stopped)
                    return;
                InterOp.Post(msgPing);
                setTimeout(beat, 1000);
            };
            setTimeout(beat, 1000);
        }
        /** The handover of the last "SessionManager.MasterClosed" ({Cc}) until an election used it or ended as a child, else null. */
        let handover = null;

        /**
         * A Pong to this window arrived: when it came later than the election waits, the master is slow (a throttled tab): later
         * elections of this window wait twice that long (at most 1 s). Only an election without an answer waits longer, so the
         * timing of the usual cases (a master answers, the first window) stays the old one.
         */
        function notePong() {
            const d = Date.now() - pingAt;
            if ((pingAt > 0) && (d > pongWait))
                pongWait = Math.min(1000, Math.ceil(d * 2));
        }

        /**
         * Ping and wait 100 ms for a matching Pong (longer once a Pong came late, see notePong).
         * @param {number} waitMs - How long to wait for the Pong.
         * @returns {Promise<boolean>} true if a master answered.
         */
        async function haveMaster(waitMs) {
            gotMasterPong = false;
            let t0 = Date.now();
            await Sw.Util.WaitEvent(events, "GotPong", waitMs, () => {
                gotMasterPong = false;
                pingAt = Date.now();
                InterOp.Post(msgPing);
            });
            //  A busy window (its own start up: a big bundle, the page's load handlers; a loaded machine) may run the 100 ms timer before the
            //  master's Pong that arrived in time, and would become a second master (two pollers, every message relayed twice): while the
            //  time-out came late (the thread was busy), wait another 100 ms for the Pong (at most 10 times)
            for (let i = 0; !gotMasterPong && (i < 10) && (Date.now() - t0 > 150); ++i) {
                t0 = Date.now();
                await Sw.Util.WaitEvent(events, "GotPong", 100);
            }
            return gotMasterPong;
        }

        /** Re-entrancy guard of startMaster. */
        let isStarting = false;

        /**
         * Try to become master (inside the Web Lock when available).
         * @param {number} how - 0 initial, 1 from MasterClosed, 2 from polling (logging only).
         * @param {number} [startCc] - Change counter to continue from.
         * @returns {Promise<void>}
         */
        async function startMaster(how, startCc) {
            /** The locked part. @returns {Promise<void>} */
            async function start() {
                if (isStarting || !started || stopped)
                    return;
                isStarting = true;
                try {
                    //  No Web Locks (http on another host than localhost): nothing serializes the children that all got the same
                    //  "MasterClosed"; a random 0..300 ms start lets the first one's Pong keep the others children (AS6b-1; two
                    //  masters that still happen heal by the heartbeat, see heartbeat)
                    if ((how === 1) && !navigator.locks) {
                        await Sw.Util.Delay(Math.floor(Math.random() * 300));
                        if (isMaster || stopped)
                            return;
                    }
                    //  A handover waits the old 100 ms: the master lock tells a slow new master from none
                    if (await haveMaster(((how === 1) && navigator.locks) ? 100 : pongWait))
                        return;
                    if (isMaster || stopped)
                        return;
                    //  No Pong in time, but a master may only be slow: it holds the master lock (after a handover the closing master
                    //  releases it at once: wait for that)
                    if (!(await holdMasterLock(how === 1 ? handoverLockWait : 0)))
                        return;
                    if (isMaster || stopped) {
                        dropMasterLock();
                        return;
                    }
                    //  A "MasterClosed" that arrived while this election (from polling) was running: it takes over with the handover
                    //  (the Cc and the subscriptions of the closing master), as the election it queued would
                    if (handover && (how !== 1)) {
                        how = 1;
                        startCc = handover.Cc;
                    }
                    if (checkMasterTimer)
                        clearInterval(checkMasterTimer);
                    checkMasterTimer = null;
                    isMaster = true;
                    isMasterNow = true;
                    masterSince = Date.now();
                    Sw.Log(logPrefix + ["Starting as master ", "Switching to master ", "Switching to master "][how] + id + ["", " (from message)", " (from polling)"][how]);
                    //  Without a handover (MasterClosed) the subscriptions of the other windows are unknown: ask them
                    if (how !== 1)
                        gatherEvents();
                    runMaster(startCc);
                    if (!navigator.locks)
                        heartbeat(masterTerm);
                }
                finally {
                    isStarting = false;
                    //  Used (master) or another window took over (child): the queued handover election has its own Cc
                    handover = null;
                    stopBuffer();
                }
            }
            if (navigator.locks)
                await navigator.locks.request(lockName, start);
            else
                await start();
        }

        /** pagehide handler of a top window (see the file header). */
        function windowClose() {
            window.removeEventListener("pagehide", windowClose);
            stopped = true;
            started = false;
            InterOp.RemoveListener(eventHandler);
            gather = null;
            if (checkMasterTimer)
                clearInterval(checkMasterTimer);
            checkMasterTimer = null;
            document.removeEventListener("freeze", onFreeze);
            document.removeEventListener("resume", onResume);
            const evs = releaseOwned();
            released = evs;
            if (!isMaster) {
                Sw.Log(logPrefix + "Stopping slave " + id);
                if (evs.length > 0)
                    InterOp.Post(msgRemoveEvents, { Events: evs });
            } else {
                InterOp.Post(msgMasterClosed, {
                    Cc: cc,
                    ServerEvents: Array.from(serverEvents.entries()),
                });
                Sw.Log(logPrefix + "Stopping master " + id);
                isMaster = false;
                isMasterNow = false;
                serverEventsChanged();
            }
            //  After MasterClosed: the child that takes over waits for it (and a page in the back/forward cache holds no lock)
            dropMasterLock();
        }
        window.addEventListener("pagehide", windowClose);

        /**
         * Stop being the master: another window is the master too (it answered our Ping late: an old common.js window, or no Web
         * Locks). The pending poll ends, the other master keeps polling, and this window takes part in the election again as a child.
         * @param {string} why - For the log.
         */
        function stepDown(why) {
            if (!isMaster || stopped)
                return;
            Sw.Log(logPrefix + "Another master answered (" + why + "), " + id + " is a child again");
            isMaster = false;
            isMasterNow = false;
            gather = null;
            dropMasterLock();
            serverEventsChanged();
            if (!checkMasterTimer)
                checkMasterTimer = setInterval(() => startMaster(2), 1000);
        }
        //  A Pong to this window while it is the master: the answer to its last election Ping came late, the sender is a master too
        masterMap.set(msgPong, data => {
            if ((data.To === id) && (data.From !== id)) {
                notePong();
                //  Without Web Locks the Pong of a new master carries when it became master: the younger one steps down (heartbeat)
                const ms = data.Ms;
                if ((typeof ms === "number") && (ms > 0) && ((ms > masterSince) || ((ms === masterSince) && (data.From > id))))
                    return;
                stepDown("a late Pong");
            }
        });
        //  "MasterClosed" of another master while this window is the master (two masters for a moment, see stepDown): continue with
        //  its Cc and subscriptions, as a child that takes over would
        masterMap.set(msgMasterClosed, data => {
            if (data.From === id)
                return;
            if (typeof data.Cc === "number")
                cc = data.Cc;
            (Array.isArray(data.ServerEvents) ? data.ServerEvents : []).forEach(e => {
                const n = Array.isArray(e) ? ("" + e[0]).toLowerCase() : "";
                if (n && !serverEvents.has(n))
                    addRef(serverEvents, n);
            });
            serverEventsChanged();
        });

        //  A frozen tab (page lifecycle) answers nothing: the master releases its lock so a child can take over, and checks it when it
        //  runs again (another window took over: step down)
        /** document "freeze" handler. */
        function onFreeze() {
            if (isMaster)
                dropMasterLock();
        }
        /** document "resume" handler. */
        function onResume() {
            if (isMaster && !releaseMasterLock)
                holdMasterLock(0).then(held => {
                    if (!held)
                        stepDown("resumed after another window took over");
                });
        }
        document.addEventListener("freeze", onFreeze);
        document.addEventListener("resume", onResume);

        await startMaster(0);
        if (!isMaster && !stopped) {
            Sw.Log(logPrefix + "Starting as child " + id);
            InterOp.Post(msgGetTimeOffset);
            checkMasterTimer = setInterval(() => startMaster(2), 1000);
        }
    }

    // ------------------------------------------------------------------ Default reactions

    /**
     * Ask the window's sign out handler (HandleSignOut; the app shell has none: a sign out reloads it).
     * @param {{Own: boolean, Here: boolean, Reason: string, Push: boolean}} info - What happened.
     * @returns {Promise<boolean>} True when it handled the sign out.
     */
    async function askSignOutHandler(info) {
        if (!signOutHandler)
            return false;
        try {
            return (await signOutHandler(info)) === true;
        }
        catch (e) {
            Sw.Log("Sw.Session: the sign out handler failed: " + (e && e.message || e));
            return false;
        }
    }

    /** sessionStorage key of the "you are signed out" notice of this tab ({At, Kind}, SignedOut / NoteSignedOut, read once by TakeSignedOut). */
    const signedOutKey = "SysWeaver.SignedOut";

    /** A notice older than this (ms) is not shown any more (it is meant for the reload that follows the sign out at once). */
    const SignedOutNoticeMs = 20000;

    /**
     * Reload the page after 100 ms unless user changes are ignored (the old common.js delayedReload / delayedUserReload).
     * @param {boolean} hard - Clear the hash and flush (reload / user changes), else a plain reload (refresh).
     * @param {boolean} [restart] - A new server instance: reload also when user changes are ignored (an auth page, ex: the sign-in
     * form shown in place of a page): IgnoreUserChanges is about sign in / out, and a restarted server may bring a new front end
     * (S7a-A1-6; the original did not reload those pages).
     * @returns {Promise<void>}
     */
    async function delayedReload(hard, restart) {
        if (ignoreUser && !restart)
            return;
        await Sw.Util.Delay(100);
        reload(true, hard);
    }

    /**
     * Reload the page now, without the page's BeforeReload hook (Sw.Page.Reload runs that hook first, then this).
     * @param {boolean} [flushCache] - Request a forced reload (only honoured by some browsers).
     * @param {boolean} [clearHash] - Remove the hash (and the history state) first.
     */
    function reloadNow(flushCache, clearHash) {
        if (clearHash) {
            try {
                history.replaceState(null, null, ' ');
            }
            catch {
            }
        }
        if (flushCache)
            location.reload(true);
        else
            location.reload();
    }

    /**
     * Reload the page: Sw.Page.Reload when the page has the full core (sw/core.js: its BeforeReload hook), else reloadNow.
     * @param {boolean} [flushCache] - Request a forced reload.
     * @param {boolean} [clearHash] - Remove the hash (and the history state) first.
     */
    function reload(flushCache, clearHash) {
        if (Sw.Page && Sw.Page.Reload)
            Sw.Page.Reload(flushCache, clearHash);
        else
            reloadNow(flushCache, clearHash);
    }

    /**
     * A sign out: the page is blanked at once (nothing of the signed in user stays on screen while the page reloads: a browser keeps
     * painting the old page until the new one is shown), then it reloads (delayedReload). If the page is still here after 5 s (the
     * user kept it at a "leave page?" prompt) it shows again.
     * @returns {Promise<void>}
     */
    async function signOutReload() {
        if (ignoreUser)
            return;
        const de = document.documentElement;
        //  One reload per sign out: the sign out message and the server's push usually both arrive
        if (!de || de.hasAttribute("data-sw-signedout"))
            return;
        de.setAttribute("data-sw-signedout", "");
        const prev = de.style.visibility;
        de.style.visibility = "hidden";
        setTimeout(() => {
            de.style.visibility = prev;
            de.removeAttribute("data-sw-signedout");
        }, 5000);
        await delayedReload(true);
    }

    /** @type {object|null} The current block info (top windows) or null. */
    let blocked = null;

    /**
     * Emit "server.block". While already blocked the first block stays (as the original's StartBlock without replace: a restart whose
     * poll then fails keeps "Server is restarting.", a pause followed by a restart keeps "Server is paused."); the title is logged anyway.
     * @param {string} type - Message type.
     * @param {string} title - Translated title.
     * @param {string} text - Translated text.
     * @param {string} icon - Icon class.
     */
    function block(type, title, text, icon) {
        console.warn(title);
        if (blocked)
            return;
        blocked = { Type: type, Title: title, Text: text, Icon: icon, Time: new Date() };
        Sw.Events.Emit("server.block", blocked);
    }

    /**
     * Emit "server.unblock" (when blocked).
     * @param {string} type - Message type.
     */
    function unblock(type) {
        if (!blocked)
            return;
        blocked = null;
        Sw.Events.Emit("server.unblock", { Type: type });
    }

    /**
     * Refresh the elements that reference changed files (the "filereload" message {Prefix, Value, DidReload}).
     * The master re-validates the browser cache for each url and re-posts the message with DidReload = true;
     * the other windows act on that re-posted message.
     * @param {{Type:string, Prefix:string, Value:string[], DidReload:boolean}} msg - The message.
     * @returns {Promise<void>}
     */
    async function reloadFiles(msg) {
        const id = InterOp.Id;
        const fileMap = new Map();
        const pre = msg.Prefix ?? "";
        const files = msg.Value || [];
        if (isMasterNow) {
            if (msg.DidReload)
                return;
            Sw.Log(id + ": File reload on master: " + pre + " => " + files.join(", "));
            files.forEach(f => fileMap.set(pre + f, true));
            await Promise.all(Array.from(fileMap.keys()).map(u => Sw.Net.FlushCache(u)));
            msg.DidReload = true;
            InterOp.Post(msg.Type, msg);
        } else {
            if (!msg.DidReload)
                return;
            files.forEach(f => fileMap.set(pre + f, true));
        }
        if (!document.body)
            return;
        const items = document.body.getElementsByTagName("*");
        const current = window.location.href;
        const actions = [];
        const reloadMap = new Map();

        /**
         * Absolute url without query, or null.
         * @param {string} u - Url.
         * @returns {string|null}
         */
        const clean = u => {
            const pi = u.indexOf('?');
            if (pi >= 0)
                u = u.substring(0, pi);
            try {
                return Sw.Url.Absolute(u, current);
            }
            catch {
                return null;
            }
        };

        /** @param {Element} el - Element. @param {string} atr - Attribute name. */
        const updateAttribute = (el, atr) => {
            const a = el.getAttribute(atr);
            if (!a)
                return;
            const url = clean(a);
            if (!url || !fileMap.get(url))
                return;
            reloadMap.set(url, 1);
            actions.push(() => {
                el.removeAttribute(atr);
                el.setAttribute(atr, a);
            });
        };

        /**
         * Find url(...) references to changed files in a css value.
         * @param {string} a - Css value.
         * @returns {string[]} Absolute changed urls.
         */
        const getUrls = a => {
            const found = [];
            if (!a)
                return found;
            const rx = /url\(\s*(['"]?)(.*?)\1\s*\)/g;
            let m;
            while ((m = rx.exec(a)) !== null) {
                const u = clean(m[2]);
                if (u && fileMap.get(u))
                    found.push(u);
            }
            return found;
        };

        /** @param {HTMLElement} el - Element. @param {string} style - Style property name. */
        const updateStyle = (el, style) => {
            const els = el.style;
            if (!els)
                return;
            const a = els[style];
            const u = getUrls(a);
            if (u.length <= 0)
                return;
            u.forEach(x => reloadMap.set(x, 1));
            actions.push(() => {
                els[style] = null;
                els[style] = a;
            });
        };

        for (let i = items.length; i--;) {
            const e = items[i];
            updateAttribute(e, "href");
            updateAttribute(e, "src");
            updateAttribute(e, "data");
            updateStyle(e, "backgroundImage");
            updateStyle(e, "maskImage");
        }
        if (actions.length <= 0)
            return;
        await Promise.allSettled(Array.from(reloadMap.keys()).map(k => Sw.Net.FlushCache(k)));
        Sw.Log(id + ": All reloaded!");
        actions.forEach(a => a());
    }

    /**
     * Install the default reactions to server messages (once; see the file header).
     */
    function initReactions() {
        if (reactionsInstalled)
            return;
        reactionsInstalled = true;
        const on = InterOp.On;

        //  All windows: events
        on("*", msg => {
            const t = msg.Type;
            const fromServer = msg.From === "Server";
            if (fromServer || t.startsWith("server."))
                Sw.Events.Emit("server.message", msg);
            if (fromServer && (forcedTypes.indexOf(t) >= 0 || t.startsWith("server.")))
                Sw.Events.Emit(t, msg);
            else if ((t === "server.error") || (t === "server.restore"))
                Sw.Events.Emit(t, msg);
            //  The master lost / regained the server: every window's connection state follows (FBK-9), decided by the master's poll
            //  (no probe of its own: the original sent none; S6-Ops-3)
            if (t === "server.error" && Sw.Net && Sw.Net.NoteFailure)
                Sw.Net.NoteFailure(true);
            else if (t === "server.restore" && Sw.Net && Sw.Net.NoteSuccess)
                Sw.Net.NoteSuccess();
        });
        on("filereload", reloadFiles);

        if (!actsAsTop())
            return;

        let serverInstance = Sw.Storage.Session.Get(serverInstanceKey, null);
        if (serverInstance === "")
            serverInstance = null;
        knownInstance = serverInstance;
        /** @param {string|null} v - Server instance id to remember. */
        const setServerInstance = v => {
            serverInstance = v;
            knownInstance = v ? v : null;
            Sw.Storage.Session.Set(serverInstanceKey, v ? v : null);
        };
        //  A window that knows no instance yet (ex: a new tab) learns it from the master's answer to GetTimeOffset
        on(msgTimeOffset, msg => {
            if ((serverInstance == null) && (msg.To === InterOp.Id) && (typeof msg.Si === "string") && msg.Si)
                setServerInstance(msg.Si);
        });

        on("reload", () => delayedReload(true));
        on("refresh", () => delayedReload(false));
        on("user.logout", async msg => {
            try {
                await navigator.credentials.preventSilentAccess();
            }
            catch {
            }
            //  A page with its own sign out handler (Sw.Session.HandleSignOut) may handle it; else the whole page reloads (as before:
            //  nothing of the signed in user stays on screen; the app shell has no handler)
            if (await askSignOutHandler({ Own: false, Here: false, Reason: msg && typeof msg.Value === "string" ? msg.Value : "", Push: true }))
                return;
            await signOutReload();
        });
        //  A sign out in this browser (any window or frame, Sw.Session.SignedOut): the same reload at once, not only when the server's
        //  push arrives (the push may be late, ex: a backing off poll); both reload, the page loads once
        on(msgSignedOut, async msg => {
            if (await askSignOutHandler({ Own: true, Here: !!msg && msg.Top === InterOp.Id, Reason: "", Push: false }))
                return;
            await signOutReload();
        });
        on("user.login", () => {
            setServerInstance(null);
            delayedReload(true);
        });
        on("server.connect", async msg => {
            const isNew = (serverInstance !== msg.Value) && (serverInstance != null);
            Sw.Log("Current server instance: " + serverInstance);
            Sw.Log("New server instance: " + msg.Value);
            setServerInstance(msg.Value);
            if (isNew) {
                await delayedReload(true, true);
                return;
            }
            Sw.Log("SysWeaver: Connected to server " + serverInstance);
            unblock(msg.Type);
        });
        on("server.reconnect", async msg => {
            //  As server.connect: without a known instance (ex: a new tab that never saw a connect) there is nothing to compare
            //  with, so no reload; the windows that loaded their page from the old instance know it and reload
            const isNew = (serverInstance !== msg.Value) && (serverInstance != null);
            Sw.Log("Current server instance: " + serverInstance);
            Sw.Log("New server instance: " + msg.Value);
            setServerInstance(msg.Value);
            if (isNew) {
                await delayedReload(true, true);
                return;
            }
            Sw.Log("SysWeaver: Re-connected to server " + serverInstance);
            unblock(msg.Type);
        });
        on("server.pause", msg => block(msg.Type,
            _TF("Service is paused.", "Displayed as the title on a connected web page if the web service is paused"),
            _TF("Waiting for service to resume.", "Displayed as the details pon a connected web page if the web service is paused"),
            "IconPaused"));
        on("server.continue", msg => unblock(msg.Type));
        on("server.restart", msg => block(msg.Type,
            _TF("Service is restarting.", "Displayed as the title on a connected web page if the web service is restarting"),
            _TF("Waiting for new service instance to start.", "Displayed as the details pon a connected web page if the web service is restarting"),
            "IconReload"));
        on("server.shutdown", msg => block(msg.Type,
            _TF("Service has been shut down.", "Displayed as the title on a connected web page if the web service has been shutdown"),
            _TF("Waiting for new service instance to start.", "Displayed as the details pon a connected web page if the web service has been shutdown"),
            "IconPower"));
        //  A lost connection blocks every top window as the original did (owner parity, S6-Ops-2): after 3 failed polls (≈ 10-15 s) the
        //  master posts "server.error" → "Server communication lost." until "server.restore" (the first poll that succeeds) or a
        //  (re)connect of the same instance. The shell's "Offline, retrying…" line shows as well. Sw.Session.BlockOnLost = false: no
        //  overlay (the calm offline line only, UX guide FBK-9).
        on("server.error", msg => {
            if (Sw.Session.BlockOnLost)
                block(msg.Type,
                    _TF("Service communication lost.", "Displayed as the title on a connected web page if the web service is not reachable anymore"),
                    _TF("Waiting for network or service to become available.", "Displayed as the details pon a connected web page if the web service is not reachable anymore"),
                    "IconWarning");
        });
        on("server.restore", msg => unblock(msg.Type));
    }

    /**
     * @namespace Sw.Session - server push messages (session manager) and server time.
     */
    Sw.Session = {

        /** @type {string[]} Message types the server always delivers. */
        ForcedTypes: forcedTypes,

        /** @type {boolean} True while this window is the master (polls the server). */
        get IsMaster() {
            return isMasterNow;
        },

        /** @type {boolean} True once Init ran (and the page is not hidden). */
        get IsStarted() {
            return started;
        },

        /** @type {boolean} True if this window acts as a top window (top, a frame of another origin, or a frame started with "top=true"). */
        get IsTop() {
            return actsAsTop();
        },

        /**
         * Reload the page (the reload of the default reactions): Sw.Page.Reload when the full core is loaded (its BeforeReload hook),
         * else ReloadNow. The legacy ReloadAll.
         * @param {boolean} [flushCache] - Request a forced reload (only honoured by some browsers).
         * @param {boolean} [clearHash] - Remove the hash (and the history state) first.
         */
        Reload: reload,

        /**
         * Reload the page now, without a BeforeReload hook (Sw.Page.Reload ends with it).
         * @param {boolean} [flushCache] - Request a forced reload (only honoured by some browsers).
         * @param {boolean} [clearHash] - Remove the hash (and the history state) first.
         */
        ReloadNow: reloadNow,

        /** @type {object|null} The current server block {Type, Title, Text, Icon, Time} (top windows) or null. */
        get Blocked() {
            return blocked;
        },

        /**
         * @type {boolean} Block the page with the "Server communication lost." overlay when the message poll fails 3 times (top windows),
         * removed on "server.restore" / a (re)connect: the original's behaviour, the default (owner parity, S6-Ops-2). Set it to false
         * for no overlay: then a lost connection shows only the calm offline line of the app shell (UX guide FBK-9; a page without the
         * shell shows nothing, it reads Sw.Net.Online / "net.offline" itself).
         */
        BlockOnLost: true,

        /**
         * Warn before a session time-out (UX guide ACC-7): at least 20 s before At, the app shell shows "Your session is about to end"
         * above any open dialog with "Stay signed in" (calls Extend, default: a request that keeps the session alive) and "Sign out".
         * SysWeaver's own sessions end only after a time without any request, and an open page keeps them alive (the message poll and the
         * shell's session check), so the framework needs no warning of its own; use this for a limit the app knows (an absolute sign
         * in lifetime, a policy of the server). Called again it replaces the warning; WarnTimeOut(null) cancels it. In a framed page it
         * is passed to the app shell. Emits Sw.Events "session.warn" {At, Extend} (the shell draws the warning).
         * @param {{At: (Date|number), Extend: function():Promise<*>, WarnMs: number}|null} o At: when the session ends; Extend: keeps it
         * alive (may return a new At, a Date or ms); WarnMs: warn this long before (default 60 s, at least 20 s, at most 5 minutes).
         */
        WarnTimeOut(o) {
            if (o && Sw.DevOptions)
                Sw.DevOptions("Sw.Session.WarnTimeOut", o);
            try {
                if (window.top !== window && window.top.Sw && window.top.Sw.Session && window.top.Sw.Session !== Sw.Session && window.top.SysWeaverApp) {
                    window.top.Sw.Session.WarnTimeOut(o);
                    return;
                }
            }
            catch {
                //  Another origin: this window shows it (when it has a shell)
            }
            Sw.Events.Emit("session.warn", o ? {
                At: o.At instanceof Date ? o.At.getTime() : +o.At,
                Extend: typeof o.Extend === "function" ? o.Extend : () => Sw.Net && Sw.Net.Get
                    ? Sw.Net.Get(Sw.Url.Root + "Api/auth/GetUser", { Reload: true, NoNavigate: true })
                    : fetch(Sw.Url.Root + "Api/auth/GetUser", { cache: "reload" }).then(r => r.json()),
                WarnMs: Math.min(300000, Math.max(20000, +o.WarnMs || 60000)),
            } : null);
        },

        /** @type {number|undefined} Server time offset in ms (server = browser + offset). */
        get TimeOffset() {
            return timeOffset;
        },

        /**
         * Start the session manager (core/session_start.js calls InitReactions and Init).
         * @returns {Promise<void>}
         */
        Init: init,

        /** Install the default message reactions (see initReactions). */
        InitReactions: initReactions,

        /** Start receiving a server message type, ref counted (see addEvent). */
        AddEvent: addEvent,

        /** Stop receiving a server message type, ref counted (see removeEvent). */
        RemoveEvent: removeEvent,

        /**
         * Add a message handler and (unless noServer) subscribe to the server message type.
         * Handlers must be removed with Unsubscribe or they accumulate.
         * @param {string} type - Message type (case insensitive).
         * @param {function(object):*} fn - Handler (may be async; handlers of a type are awaited in order).
         * @param {boolean} [noServer] - Not a server message (no subscription), e.g. an InterOp message.
         * @returns {Function} fn.
         */
        Subscribe(type, fn, noServer) {
            const m = getHandlerMap();
            type = type.toLowerCase();
            let list = m.get(type);
            if (!list) {
                list = [];
                m.set(type, list);
            }
            list.push(fn);
            if (!noServer)
                addEvent(type);
            return fn;
        },

        /**
         * Remove a handler added with Subscribe (same arguments).
         * @param {string} type - Message type.
         * @param {Function} fn - Handler.
         * @param {boolean} [noServer] - Same as used in Subscribe.
         * @returns {boolean} True when the handler was found (and removed).
         */
        Unsubscribe(type, fn, noServer) {
            const m = getHandlerMap();
            type = type.toLowerCase();
            const list = m.get(type);
            if (!list)
                return false;
            const i = list.indexOf(fn);
            if (i < 0)
                return false;
            if (!noServer)
                removeEvent(type);
            list.splice(i, 1);
            if (list.length <= 0)
                m.delete(type);
            return true;
        },

        /**
         * Do not reload this window on user login / logout / reload / refresh messages (auth pages). IgnoreUserChanges(false) turns
         * the reloads on again (the legacy window.IgnoreUserChanged = false).
         * @param {boolean} [on=true] - Ignore them.
         */
        IgnoreUserChanges(on) {
            ignoreUser = on === undefined ? true : !!on;
        },

        /** @type {boolean} True if user changes are ignored. */
        get IsIgnoringUserChanges() {
            return ignoreUser;
        },

        /**
         * Handle sign outs in this (top) window instead of the default reload. Not used by the app shell: a sign out reloads the whole
         * page, so nothing of the signed in user stays on screen. Only for a special page that must not reload; it must then remove
         * everything of the user itself. The handler gets {Own, Here, Reason, Push} and returns true when it handled the sign out (else
         * the window reloads): Own: a sign out in this browser (SignedOut, posted at once by the signing out window; Here: by this tab or
         * a frame of it); Push: the server's "user.logout" (Reason: the server's text, ex: "User signed out", "Session expired"). One sign
         * out usually arrives twice (SignedOut, then the push).
         * @param {function(object):(boolean|Promise<boolean>)|null} fn - The handler (null removes it).
         */
        HandleSignOut(fn) {
            signOutHandler = typeof fn === "function" ? fn : null;
        },

        /**
         * Tell every window of this site that the user signed out here, at once (the sign out calls it when the server confirmed it:
         * auth/Auth.js Logout): every top window that doesn't ignore user changes reloads (the server's "user.logout" push does the same,
         * maybe later). The message names the tab (its top window) that signed out. When this tab's top window will reload (the app
         * shell, a sign out page in its frame), the tab remembers the sign out (NoteSignedOut "here"), so the reloaded page can say
         * "You are signed out" once (TakeSignedOut).
         */
        SignedOut() {
            let top = InterOp.Id;
            let reloads = !ignoreUser;
            try {
                if (window.top !== window) {
                    const ts = window.top.Sw;
                    if (ts && ts.InterOp)
                        top = ts.InterOp.Id;
                    reloads = !!(ts && ts.Session && !ts.Session.IsIgnoringUserChanges);
                }
            }
            catch {
                reloads = false;
            }
            if (reloads)
                Sw.Session.NoteSignedOut("here");
            InterOp.Post(msgSignedOut, { Top: top });
        },

        /**
         * Remember in this tab (sessionStorage "SysWeaver.SignedOut" {At, Kind}) that the user was signed out just before the page
         * reloads, so the reloaded page can say so once (TakeSignedOut).
         * @param {string} kind - "here" (the user signed out in this tab) or "ended" (the session ended on the server).
         */
        NoteSignedOut(kind) {
            Sw.Storage.Session.SetJson(signedOutKey, { At: Date.now(), Kind: kind === "ended" ? "ended" : "here" });
        },

        /**
         * Take the sign out note of this tab (NoteSignedOut): returns it and removes it, so one sign out gives one notice, ever. A note
         * older than 20 s is dropped (it belongs to the reload that follows the sign out at once).
         * @returns {{At: number, Kind: string}|null} The note, or null.
         */
        TakeSignedOut() {
            const s = Sw.Storage.Session;
            const n = s.GetJson(signedOutKey, null);
            if (n === null)
                return null;
            s.Remove(signedOutKey);
            if (!n || typeof n !== "object" || !(Math.abs(Date.now() - (+n.At || 0)) < SignedOutNoticeMs))
                return null;
            return { At: +n.At, Kind: n.Kind === "ended" ? "ended" : "here" };
        },

        /**
         * Estimated (latency corrected) server time.
         * @returns {Date}
         */
        ServerTime: () => new Date(serverTick()),

        /** Estimated server tick in ms since 1970 (see serverTick). */
        ServerTick: serverTick,

        /**
         * List the server message types currently subscribed (this window's view).
         * @returns {string[]}
         */
        Events: () => Array.from(serverEvents.keys()),

        /**
         * List the server message types this window subscribed (AddEvent / Subscribe of this window only).
         * @returns {string[]}
         */
        OwnedEvents: () => Array.from(ownedEvents.keys()),
    };

    //  The option schemas (Sw.DevOptions registry: checked in developer mode, listed by the walker of core/contracttest.html)
    if (Sw.DevOptions && Sw.DevOptions.Register)
        Sw.DevOptions.Register("Sw.Session.WarnTimeOut", { At: "object|number", Extend: "function", WarnMs: { Type: "number", Min: 0 } });
})();
