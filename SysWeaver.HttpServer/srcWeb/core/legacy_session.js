/**
 * @file core/legacy_session.js - the legacy messaging names of the old common/common.js (SessionManager, InterOp, SysWeaverInit ...),
 * mapped onto Sw.Session / Sw.InterOp / Sw.Layout. Part of the small bundle sw/session.js and of the legacy bundles common/common.js and
 * sw/compat.js (same file, it runs once per page); not part of sw/core.js (new format pages use the Sw.* api, SwWeb check --strict).
 *
 * Defines, with the old names, signatures and return values (window properties; in the original SessionManager and InterOp were
 * lexical class globals, so this is a superset):
 *  - SessionManager: Create() / Init() (no-ops: the core starts itself), AddMessageHandler / RemoveMessageHandler, AddServerEvent /
 *    RemoveServerEvent, IsMaster, Url, Current, ServerEvents / OwnedEvents: the live ref count Maps with the original's counts
 *    (S6 F5: the master's own AddEvents echo counts twice, as the original; other windows' AddEvents / RemoveEvents while master; the
 *    handover's list): what old code reads. The real subscriptions are Sw.Session's (correct ref counts).
 *  - InterOp: Id, B (the transport: Post keeps the message's From), Post, AddListener / RemoveListener (return fn), GetMessage;
 *    InterOpBroadcast, InterOpWindows (thin wrappers).
 *  - SysWeaverInit (async as the original: returns a Promise; Sw.Init when the full core is loaded, else nothing more),
 *    SysWeaverIgnoreUserChanges, GetServerTime, GetServerTick,
 *    PostAll / PostTop / PostParent (Sw.Frame: this origin unless a target origin is passed; data.Type is set to the name, as the
 *    original), ReloadAll (Sw.Session.Reload),
 *    getUrlParams, isMobile, getPlatform, getMobileParams.
 *  - Live window state: Mobile, Portrait, Narrow, SelfPortrait, SelfNarrow, SwWidth, SwHeight (Sw.Layout), IsTop, UseTheme (the
 *    requested theme, Sw.Session.RequestedTheme: with sw/theme.js an assignment followed by applyTheme() applies it),
 *    SysWeaverServerTimeOffset, SysWeaverId, IgnoreUserChanged, HaveSysWeaverInit, HaveSessionManager.
 *  - Sw.Legacy.Def / Sw.Legacy.Live: the helpers that define such globals (ui/compat_common.js uses them too).
 *
 * Load order: after core/session_start.js. Dependencies: Sw.Session, Sw.InterOp, Sw.Frame, Sw.Layout, Sw.Url, Sw.Platform.
 * Globals defined: the names above.
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const S = Sw.Session;

    /**
     * Define a global (a window property, like the old top level declarations).
     * @param {string} name - The global name.
     * @param {*} value - The value.
     */
    function def(name, value) {
        window[name] = value;
    }

    /**
     * Define a live global (an accessor on window) for old window state variables.
     * @param {string} name - The global name.
     * @param {function():*} get - Getter.
     * @param {function(*):void} [set] - Setter (default: assignments replace the accessor with a plain value).
     * @param {object} [target=window] - The object to define it on.
     */
    function live(name, get, set, target) {
        const t = target || window;
        try {
            Object.defineProperty(t, name, {
                configurable: true,
                enumerable: true,
                get,
                set: set || (v => Object.defineProperty(t, name, { configurable: true, enumerable: true, writable: true, value: v })),
            });
        }
        catch (e) {
            console.warn("legacy_session.js: can't define " + name + ": " + e.message);
        }
    }

    /** @namespace Sw.Legacy - helpers that define the legacy globals (used by core/legacy_session.js and ui/compat_common.js). */
    Sw.Legacy = {
        /** Define a global (see def). */
        Def: def,
        /** Define a live global, an accessor (see live). */
        Live: live,
    };

    // ------------------------------------------------------------------ window state variables (live)

    const L = () => Sw.Layout || {};
    live("Mobile", () => L().Mobile);
    live("Portrait", () => L().Portrait);
    live("Narrow", () => L().Narrow);
    live("SelfPortrait", () => L().SelfPortrait);
    live("SelfNarrow", () => L().SelfNarrow);
    live("SwWidth", () => L().Width);
    live("SwHeight", () => L().Height);
    live("IsTop", () => (Sw.Page ? Sw.Page.IsTop : S.IsTop));
    live("UseTheme", () => S.RequestedTheme, v => {
        S.RequestedTheme = v;
    });
    live("SysWeaverServerTimeOffset", () => S.TimeOffset);
    live("SysWeaverId", () => Sw.InterOp.Id);
    //  (false turns the reloads on again, as the original's plain variable; S6 F3)
    live("IgnoreUserChanged", () => S.IsIgnoringUserChanges, v => S.IgnoreUserChanges(!!v));
    def("HaveSysWeaverInit", true);
    def("HaveSessionManager", true);

    // ------------------------------------------------------------------ server time, session, interop

    /**
     * Get the current server time (latency corrected).
     * @returns {Date} The server time.
     */
    def("GetServerTime", () => S.ServerTime());

    /**
     * Get the current server time as a tick (ms since 1970).
     * @returns {number} The tick.
     */
    def("GetServerTick", () => S.ServerTick());

    // ------------------------------------------------------------------ the old ref count maps (S6 F5)

    /** @type {Map<string, number>} SessionManager.ServerEvents: the original's counts (see the file header). */
    let serverMap = new Map();

    /** @type {Map<string, number>} SessionManager.OwnedEvents: the types this window subscribed, ref counted. */
    let ownedMap = new Map();

    /**
     * The original's AddRef: increment name in map (and in OwnedEvents when owned).
     * @param {Map<string, number>} map - The map.
     * @param {string} name - The lower case type.
     * @param {boolean} [owned] - Also count it in OwnedEvents.
     * @returns {boolean} True when it was the first reference.
     */
    function addRef(map, name, owned) {
        const count = (map.get(name) || 0) + 1;
        map.set(name, count);
        if (owned)
            addRef(ownedMap, name);
        return count === 1;
    }

    /**
     * The original's DecRef: decrement name in map (and in OwnedEvents when owned), removing it at 0.
     * @param {Map<string, number>} map - The map.
     * @param {string} name - The lower case type.
     * @param {boolean} [owned] - Also in OwnedEvents.
     * @returns {boolean} True when it was the last reference.
     */
    function decRef(map, name, owned) {
        if (owned)
            decRef(ownedMap, name);
        const count = map.get(name);
        if (!count) {
            map.delete(name);
            return false;
        }
        if (count > 1) {
            map.set(name, count - 1);
            return false;
        }
        map.delete(name);
        return true;
    }

    //  The master counts the AddEvents / RemoveEvents of the other windows (its own echo is counted where it posts, below); a child that
    //  gets a handover takes the closing master's list (as the original)
    Sw.InterOp.On("SessionManager.AddEvents", m => {
        if (m.From !== Sw.InterOp.Id && S.IsMaster)
            (m.Events || []).forEach(e => addRef(serverMap, ("" + e).toLowerCase()));
    });
    Sw.InterOp.On("SessionManager.RemoveEvents", m => {
        if (m.From !== Sw.InterOp.Id && S.IsMaster)
            (m.Events || []).forEach(e => decRef(serverMap, ("" + e).toLowerCase()));
    });
    Sw.InterOp.On("SessionManager.MasterClosed", m => {
        if (m.From !== Sw.InterOp.Id && Array.isArray(m.ServerEvents)) {
            serverMap.clear();
            m.ServerEvents.forEach(kv => {
                if (Array.isArray(kv) && kv.length > 1)
                    serverMap.set("" + kv[0], kv[1]);
            });
        }
    });

    /**
     * The old session manager (server push messages), mapped onto Sw.Session.
     */
    class SessionManager {
        /** Old start up (the core starts the session manager by itself): no-op. */
        static Create() {
        }

        /**
         * Old start up (the core starts the session manager by itself): no-op.
         * @returns {Promise<void>}
         */
        static async Init() {
        }

        /** @type {string} The url of the script (old: the url of common.js). */
        static get Current() {
            return Sw.Url.Script;
        }

        /** @type {string} The url of the GetMessages api. */
        static get Url() {
            return Sw.Url.Root + "Api/application/GetMessages";
        }

        /** @type {boolean} True if this window polls the server for all windows. */
        static get IsMaster() {
            return S.IsMaster;
        }

        /** @type {Map<string, number>} All subscribed server message types, ref counted (the original's live Map and counts). */
        static get ServerEvents() {
            return serverMap;
        }

        static set ServerEvents(v) {
            serverMap = v instanceof Map ? v : new Map();
        }

        /** @type {Map<string, number>} The server message types this window subscribed, ref counted (the original's live Map). */
        static get OwnedEvents() {
            return ownedMap;
        }

        static set OwnedEvents(v) {
            ownedMap = v instanceof Map ? v : new Map();
        }

        /**
         * Subscribe to a server message type (ref counted). The return value is the original's: true when ServerEvents had no
         * reference yet (in the master its own AddEvents echo counts too, so a second call returns false, as the original).
         * @param {string} name - The message type.
         * @returns {boolean} True if it was the first subscription.
         */
        static AddServerEvent(name) {
            name = ("" + name).toLowerCase();
            const first = addRef(serverMap, name, true);
            S.AddEvent(name);
            //  (the original posted AddEvents only for the first reference, and the master counted its own echo)
            if (first && S.IsMaster)
                addRef(serverMap, name);
            return first;
        }

        /**
         * Unsubscribe from a server message type (the original's return value, see AddServerEvent).
         * @param {string} name - The message type.
         * @returns {boolean} True if it was the last subscription.
         */
        static RemoveServerEvent(name) {
            name = ("" + name).toLowerCase();
            const last = decRef(serverMap, name, true);
            S.RemoveEvent(name);
            if (last && S.IsMaster)
                decRef(serverMap, name);
            return last;
        }

        /**
         * Add a message handler (and subscribe to the server message type unless ignoreServer).
         * @param {string} messageType - The message type (case insensitive).
         * @param {function(object):*} fn - The handler.
         * @param {boolean} [ignoreServer] - Don't subscribe to the server type (InterOp messages only).
         * @returns {function} fn.
         */
        static AddMessageHandler(messageType, fn, ignoreServer) {
            S.Subscribe(messageType, fn, true);
            if (!ignoreServer)
                SessionManager.AddServerEvent(messageType);
            return fn;
        }

        /**
         * Remove a message handler added with AddMessageHandler.
         * @param {string} messageType - The message type.
         * @param {function(object):*} fn - The handler.
         * @param {boolean} [ignoreServer] - Must match the AddMessageHandler call.
         * @returns {boolean} True when the handler was found.
         */
        static RemoveMessageHandler(messageType, fn, ignoreServer) {
            const found = S.Unsubscribe(messageType, fn, true);
            if (found && !ignoreServer)
                SessionManager.RemoveServerEvent(messageType);
            return found;
        }
    }
    def("SessionManager", SessionManager);

    /**
     * The old cross window message bus (BroadcastChannel "SysWeaver"), mapped onto Sw.InterOp (same wire format).
     */
    class InterOp {
        /** @type {number} This window's id. */
        static get Id() {
            return Sw.InterOp.Id;
        }

        /** @type {{Post: function(object):void, AddListener: function(function):function, RemoveListener: function(function):function}} The transport (old internal). */
        static get B() {
            return {
                Post: m => Sw.InterOp.PostRaw(m),
                AddListener: fn => Sw.InterOp.AddListener(fn),
                RemoveListener: fn => Sw.InterOp.RemoveListener(fn),
            };
        }

        /**
         * Post a message to all same origin windows (including this one).
         * @param {string} type - Message type.
         * @param {object} [data] - Payload (the envelope members are added).
         * @param {boolean} [isFromServer] - Mark as a server message (From = "Server").
         */
        static Post(type, data, isFromServer) {
            Sw.InterOp.Post(type, data, isFromServer);
        }

        /**
         * Add a raw listener (gets {data} / MessageEvent objects, use GetMessage).
         * @param {function(object):*} fn - The listener.
         * @returns {function(object):*} fn.
         */
        static AddListener(fn) {
            return Sw.InterOp.AddListener(fn);
        }

        /**
         * Remove a raw listener.
         * @param {function(object):*} fn - The listener.
         * @returns {function(object):*} fn.
         */
        static RemoveListener(fn) {
            return Sw.InterOp.RemoveListener(fn);
        }

        /**
         * Get the message of a listener event.
         * @param {object} ev - The event.
         * @returns {object} The message ({Type, From, ...}).
         */
        static GetMessage(ev) {
            return Sw.InterOp.GetMessage(ev);
        }
    }
    def("InterOp", InterOp);

    /**
     * Old internal InterOp transport class (BroadcastChannel); kept as a thin wrapper of Sw.InterOp.
     */
    class InterOpBroadcast {
        /** @param {string} [name] - Ignored (the channel is always "SysWeaver"). */
        constructor(name) {
            this.Name = name;
        }

        /** @param {object} msg - The message ({Type, From, ...}, posted as it is). */
        Post(msg) {
            Sw.InterOp.PostRaw(msg);
        }

        /**
         * @param {function(object):*} fn - Raw listener.
         * @returns {function(object):*} fn.
         */
        AddListener(fn) {
            return Sw.InterOp.AddListener(fn);
        }

        /**
         * @param {function(object):*} fn - Raw listener.
         * @returns {function(object):*} fn.
         */
        RemoveListener(fn) {
            return Sw.InterOp.RemoveListener(fn);
        }
    }
    def("InterOpBroadcast", InterOpBroadcast);

    /** Old internal InterOp transport class (window messages); kept as a thin wrapper of Sw.InterOp. */
    class InterOpWindows extends InterOpBroadcast {
    }
    def("InterOpWindows", InterOpWindows);

    // ------------------------------------------------------------------ url parameters, platform, frames

    /**
     * Get the url parameters of the page (lower-cased keys, cached).
     * @returns {URLSearchParams}
     */
    def("getUrlParams", () => Sw.Url.Params());

    /**
     * True if it's likely a mobile device.
     * @returns {boolean}
     */
    def("isMobile", () => Sw.Platform.IsMobileDevice);

    /**
     * Lower case platform name ("win...", "mac...", "android", ...).
     * @returns {string}
     */
    def("getPlatform", () => Sw.Platform.Name);

    /**
     * Mobile parameters [isMobile, singleColumn, isPortrait, scale].
     * @returns {Array}
     */
    def("getMobileParams", () => Sw.Platform.MobileParams());

    def("ReloadAll", (flushCache, clearHash) => S.Reload(flushCache, clearHash));
    /**
     * The original's frame posts set data.Type to the name (the caller's object is changed, S6 F5).
     * @param {string} name - The message type.
     * @param {object} [data] - The payload.
     * @returns {object|undefined} data.
     */
    const typed = (name, data) => {
        if (data && typeof data === "object")
            data.Type = name;
        return data;
    };
    def("PostAll", (name, data, targetOrigin) => Sw.Frame.PostAll(name, typed(name, data), targetOrigin));
    def("PostTop", (name, data, targetOrigin) => Sw.Frame.PostTop(name, typed(name, data), targetOrigin));
    def("PostParent", (name, data, targetOrigin) => Sw.Frame.PostParent(name, typed(name, data), targetOrigin));

    // ------------------------------------------------------------------ session start up

    def("SysWeaverIgnoreUserChanges", () => S.IgnoreUserChanges());
    /**
     * The old start up (the core starts itself): async as the original, so SysWeaverInit().then(...) works (S6 F4).
     * @returns {Promise<void>}
     */
    def("SysWeaverInit", async () => {
        if (Sw.Init)
            await Sw.Init();
    });
})();
