/**
 * @file core/interop.js - cross window messaging.
 *
 * Purpose:
 *  - `Sw.InterOp`: a message bus between all same-origin SysWeaver windows, tabs and frames, wire compatible with
 *    the old InterOp (so old and new pages interoperate): BroadcastChannel named "SysWeaver", messages are plain
 *    objects {Type, From, MagicXyz: "SysWeaver", ...data} where From is the sender's InterOp id (a number) or
 *    "Server" for messages relayed from the server by the session master, and To optionally targets one window.
 *    Posting also delivers the message synchronously to the local listeners (a BroadcastChannel does not echo).
 *    Without BroadcastChannel a fallback posts with window.postMessage to the parent chain and to the windows
 *    opened by this one (window.open is wrapped to track them, the original is kept as window._open).
 *    API: Id, Post(type, data, isFromServer), PostRaw(m), On(type, fn) / Off (type "*" = every message), AddListener /
 *    RemoveListener (raw listeners receiving the event {data}), GetMessage(ev).
 *  - `Sw.Frame`: raw window.postMessage helpers (messages {Type, ...} WITHOUT the InterOp marker), used for the
 *    frame protocol with the app shell and other hosts: PostParent, PostTop, PostAll, On(type, fn, anyOrigin).
 *    Security: posts reach windows of this origin only unless the caller passes the target origin "*" (explicit
 *    opt-in, never with secrets); On passes on same-origin messages only unless anyOrigin is set. The InterOp
 *    fallback transport drops messages of other origins (getMessage).
 *
 * Load order: after core/url.js. Dependencies: Sw.Url. Part of sw/session.js (and sw/core.js).
 * Globals defined: none (Sw.InterOp, Sw.Frame); the fallback transport replaces window.open (as before).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** The marker value of InterOp messages. */
    const magic = "SysWeaver";

    /**
     * Create the BroadcastChannel transport (throws if BroadcastChannel is unsupported).
     * @param {string} name - Channel name.
     * @returns {{Post:function(object):void, AddListener:function(Function):void, RemoveListener:function(Function):void}}
     */
    function broadcastTransport(name) {
        const s = new BroadcastChannel(name);
        const listeners = [];
        return {
            /**
             * Post to the other channel members and call the local listeners with {data: m}.
             * @param {object} m - Message (structured cloneable).
             */
            Post(m) {
                s.postMessage(m);
                const e = { data: m };
                const ls = listeners.slice();
                const c = ls.length;
                for (let i = 0; i < c; ++i) {
                    try {
                        ls[i](e);
                    }
                    catch (ex) {
                        console.warn("EventListener failed: " + ex.message);
                    }
                }
            },
            /** @param {Function} l - Listener (local and remote messages). */
            AddListener(l) {
                listeners.push(l);
                s.addEventListener('message', l);
            },
            /** @param {Function} l - Listener. */
            RemoveListener(l) {
                s.removeEventListener('message', l);
                const t = listeners.lastIndexOf(l);
                if (t >= 0)
                    listeners.splice(t, 1);
            },
        };
    }

    /**
     * Create the window.postMessage fallback transport (parent chain + windows opened by this one).
     * Note: the local window does not receive its own messages with this transport (as before).
     * @returns {{Post:function(object):void, AddListener:function(Function):void, RemoveListener:function(Function):void}}
     */
    function windowsTransport() {
        const windows = [];
        let w = window.parent;
        while (w && (w !== window) && (windows.indexOf(w) < 0)) {
            windows.push(w);
            if (w === w.parent)
                break;
            w = w.parent;
        }
        const org = window.open;
        window._open = org;
        /**
         * window.open replacement remembering same-origin (or relative) opened windows.
         * @param {string} url - Url.
         * @param {string} [name] - Target.
         * @param {string} [params] - Features.
         * @returns {Window|null}
         */
        window.open = (url, name, params) => {
            const win = org.call(window, url, name, params);
            if (win && ((!Sw.Url.IsAbsolute(url)) || Sw.Url.IsSameOrigin(url)))
                windows.push(win);
            return win;
        };
        return {
            /** @param {object} m - Message posted to every known (open) window. */
            Post(m) {
                for (let i = windows.length - 1; i >= 0; --i) {
                    const win = windows[i];
                    try {
                        if (!win.closed) {
                            win.postMessage(m, "/");
                            continue;
                        }
                    }
                    catch {
                    }
                    windows.splice(i, 1);
                }
            },
            /** @param {Function} l - Window "message" listener. */
            AddListener: l => window.addEventListener('message', l),
            /** @param {Function} l - Listener. */
            RemoveListener: l => window.removeEventListener('message', l),
        };
    }

    /** @type {number} This window's id (same form as the old InterOp.Id). */
    const id = new Date().valueOf() + Math.random();

    let transport;
    let isBroadcast = true;
    try {
        transport = broadcastTransport(magic);
    }
    catch {
        isBroadcast = false;
        if (Sw.DevWarn)
            Sw.DevWarn("Sw.InterOp: no BroadcastChannel, using the opened windows fallback", "interop.fallback");
        transport = windowsTransport();
    }

    /** @type {Map<string, Function[]>} Message type -> handlers registered with On. */
    const typed = new Map();

    /** @type {boolean} True once the typed dispatcher is attached to the transport. */
    let dispatching = false;

    /**
     * Return the InterOp message of an event, or {Type: null, From: null} for foreign messages.
     * @param {MessageEvent|{data:object}} ev - Event received by a raw listener.
     * @returns {{Type:string|null, From:(number|string|null)}}
     */
    function getMessage(ev) {
        //  Security: the window message fallback transport receives messages from any window (a foreign page framing this
        //  one, or opened by it); only same-origin windows belong to the bus (a BroadcastChannel is same-origin by itself)
        if (ev && (typeof ev.origin === "string") && ev.origin && (ev.origin !== window.location.origin))
            return { Type: null, From: null };
        const data = ev ? ev.data : null;
        if (data && (data.MagicXyz === magic) && (typeof data.Type === "string"))
            return data;
        return { Type: null, From: null };
    }

    /**
     * Raw listener dispatching to the typed handlers (exact type, then "*").
     * @param {MessageEvent|{data:object}} ev - Event.
     */
    function dispatch(ev) {
        const m = getMessage(ev);
        const t = m.Type;
        if (!t)
            return;
        const call = list => {
            if (!list)
                return;
            const fns = list.slice();
            const l = fns.length;
            for (let i = 0; i < l; ++i) {
                try {
                    const r = fns[i](m);
                    if (r && typeof r.catch === "function")
                        r.catch(e => console.error("Sw.InterOp: handler of \"" + t + "\" failed: " + (e && e.message ? e.message : e)));
                }
                catch (e) {
                    console.error("Sw.InterOp: handler of \"" + t + "\" failed: " + (e && e.message ? e.message : e));
                }
            }
        };
        call(typed.get(t));
        call(typed.get("*"));
    }

    /**
     * @namespace Sw.InterOp - same-origin cross window message bus (wire compatible with the old InterOp).
     */
    Sw.InterOp = {

        /** @type {number} Unique id of this window (Date.now() + Math.random()). */
        Id: id,

        /** @type {string} The message marker value ("SysWeaver"). */
        Magic: magic,

        /** @type {boolean} True if the BroadcastChannel transport is used. */
        IsBroadcast: isBroadcast,

        /**
         * Post a message to all windows (including this one).
         * @param {string} type - Message type (set as data.Type).
         * @param {object} [data] - Payload object (mutated: Type, From and MagicXyz are set; To may target a window).
         * @param {boolean} [isFromServer] - Only for the session master: sets From = "Server".
         */
        Post(type, data, isFromServer) {
            if ((typeof data === "object") && (data !== null))
                data.Type = type;
            else
                data = { Type: type };
            data.From = isFromServer ? "Server" : id;
            data.MagicXyz = magic;
            transport.Post(data);
        },

        /**
         * Post a ready made message object as it is (the old InterOp.B.Post): its From is kept (this window's id when missing), the
         * marker is set. Use Post for new messages.
         * @param {{Type: string, From: (number|string)}} m - The message (structured cloneable; mutated: MagicXyz, a missing From).
         */
        PostRaw(m) {
            if (!m || (typeof m !== "object") || (typeof m.Type !== "string"))
                return;
            if ((m.From === undefined) || (m.From === null))
                m.From = id;
            m.MagicXyz = magic;
            transport.Post(m);
        },

        /**
         * Add a handler for one message type (exact, case sensitive) or "*" for all messages.
         * @param {string} type - Message type.
         * @param {function(object):*} fn - Handler receiving the message (async handlers are not awaited).
         * @returns {function():void} Removes the handler.
         */
        On(type, fn) {
            if (!dispatching) {
                dispatching = true;
                transport.AddListener(dispatch);
            }
            let list = typed.get(type);
            if (!list) {
                list = [];
                typed.set(type, list);
            }
            list.push(fn);
            return () => Sw.InterOp.Off(type, fn);
        },

        /**
         * Remove a handler added with On.
         * @param {string} type - Message type.
         * @param {Function} fn - Handler.
         */
        Off(type, fn) {
            const list = typed.get(type);
            if (!list)
                return;
            const i = list.lastIndexOf(fn);
            if (i >= 0)
                list.splice(i, 1);
            if (list.length <= 0)
                typed.delete(type);
        },

        /**
         * Add a raw listener for all transport events (use GetMessage(ev) to validate / extract the message).
         * @param {function(MessageEvent|{data:object}):*} fn - Listener.
         * @returns {Function} fn.
         */
        AddListener(fn) {
            transport.AddListener(fn);
            return fn;
        },

        /**
         * Remove a raw listener.
         * @param {Function} fn - Listener.
         * @returns {Function} fn.
         */
        RemoveListener(fn) {
            transport.RemoveListener(fn);
            return fn;
        },

        /** Extract the InterOp message of an event (see getMessage). */
        GetMessage: getMessage,
    };

    /**
     * Post a raw {Type, ...data} message to a window (a failed post is logged as a console warning, never thrown).
     * @param {Window|null} target - Target window.
     * @param {string} name - Message type.
     * @param {object} [data] - Payload (not changed: the message is a shallow copy with Type set).
     * @param {string} [targetOrigin] - Target origin; default this page's origin ("/"): only a same-origin window receives
     * the message. Pass "*" explicitly (opt-in) for a host of another origin, never with secrets in the message.
     */
    function rawPost(target, name, data, targetOrigin) {
        if (!target)
            return;
        const msg = ((typeof data === "object") && (data !== null)) ? Object.assign({}, data, { Type: name }) : { Type: name };
        try {
            target.postMessage(msg, targetOrigin || "/");
        }
        catch (e) {
            console.warn("Sw.Frame: posting \"" + name + "\" failed: " + (e && e.message ? e.message : e));
        }
    }

    /**
     * @namespace Sw.Frame - raw postMessage protocol between frames / windows (no InterOp marker).
     */
    Sw.Frame = {

        /**
         * Post to the parent window, or to window.opener when this is the top window.
         * @param {string} name - Message type.
         * @param {object} [data] - Payload.
         * @param {string} [targetOrigin] - Target origin (default: this page's origin; "*" = any window, explicit opt-in).
         */
        PostParent(name, data, targetOrigin) {
            const p = window.parent;
            rawPost(p === window ? window.opener : p, name, data, targetOrigin);
        },

        /**
         * Post to the top window (also when this is the top window).
         * @param {string} name - Message type.
         * @param {object} [data] - Payload.
         * @param {string} [targetOrigin] - Target origin (default: this page's origin; "*" = any window, explicit opt-in).
         */
        PostTop(name, data, targetOrigin) {
            rawPost(window.top, name, data, targetOrigin);
        },

        /**
         * Post to window.opener, or window.top if there is no opener.
         * @param {string} name - Message type.
         * @param {object} [data] - Payload.
         * @param {string} [targetOrigin] - Target origin (default: this page's origin; "*" = any window, explicit opt-in).
         */
        PostAll(name, data, targetOrigin) {
            rawPost(window.opener ?? window.top, name, data, targetOrigin);
        },

        /**
         * Listen for raw window messages of a type ({Type: type, ...}). Only messages from windows of this origin are
         * passed on, unless anyOrigin is set (then the handler must check ev.origin / ev.source itself).
         * @param {string} type - Message type.
         * @param {function(object, MessageEvent):*} fn - Handler (data, event).
         * @param {boolean} [anyOrigin] - Also pass on messages from other origins (explicit opt-in).
         * @returns {function():void} Removes the listener.
         */
        On(type, fn, anyOrigin) {
            /** Window message listener filtering by type (and origin). @param {MessageEvent} ev - Event. */
            const l = ev => {
                if (!anyOrigin && ev.origin !== window.location.origin)
                    return;
                const d = ev.data;
                if (d && (d.Type === type))
                    fn(d, ev);
            };
            window.addEventListener("message", l);
            return () => window.removeEventListener("message", l);
        },
    };
})();
