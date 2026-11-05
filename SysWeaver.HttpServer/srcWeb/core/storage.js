/**
 * @file core/storage.js - safe web storage and per page state.
 *
 * Purpose:
 *  - `Sw.Storage`: localStorage / sessionStorage wrappers that never throw (private mode, disabled storage,
 *    quota): `Sw.Storage.Local` and `Sw.Storage.Session` each have Get/Set/Remove/GetJson/SetJson; `Sw.Storage.Get`
 *    etc. are shortcuts to Local. Keys are used as given (the SysWeaver keys are "SysWeaver.<Name>", e.g.
 *    "SysWeaver.Theme", "SysWeaver.Mobile" (local) and "SysWeaver.ServerInstance" (session)).
 *  - `Sw.History`: the page state store with the same semantics as the old Get/Set/Update/ClearHistoryState:
 *    top windows keep the state object in history.state (history.replaceState), frames keep it as json in
 *    sessionStorage under their location.href. Keys used by the core: "ScrollX", "ScrollY".
 *    If history.state is not an object (the app shell stores a string key there) the state is read only
 *    (writes are ignored, like the old non-strict code did), so the shell state is never overwritten.
 *
 * Load order: after core/kernel.js. Dependencies: none.
 * Globals defined: none (Sw.Storage, Sw.History).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Create a never throwing wrapper around a Storage.
     * @param {function():Storage} getStore - Returns the storage (may throw when access is denied).
     * @returns {{Get:function(string,*):*, Set:function(string,*):boolean, Remove:function(string):void, GetJson:function(string,*):*, SetJson:function(string,*):boolean}}
     */
    function wrap(getStore) {
        /** @returns {Storage|null} The store or null if unavailable. */
        function store() {
            try {
                return getStore();
            }
            catch {
                return null;
            }
        }
        return {
            /**
             * Read a string value.
             * @param {string} key - Key.
             * @param {*} [def=null] - Returned if missing or unavailable.
             * @returns {string|*}
             */
            Get(key, def) {
                try {
                    const v = store()?.getItem(key);
                    return (v === null || typeof v === "undefined") ? (def ?? null) : v;
                }
                catch {
                    return def ?? null;
                }
            },
            /**
             * Write a value (converted to string); null / undefined removes the key.
             * @param {string} key - Key.
             * @param {*} value - Value.
             * @returns {boolean} true if written.
             */
            Set(key, value) {
                try {
                    const s = store();
                    if (!s)
                        return false;
                    if (value === null || typeof value === "undefined")
                        s.removeItem(key);
                    else
                        s.setItem(key, "" + value);
                    return true;
                }
                catch {
                    return false;
                }
            },
            /**
             * Remove a key.
             * @param {string} key - Key.
             */
            Remove(key) {
                try {
                    store()?.removeItem(key);
                }
                catch {
                }
            },
            /**
             * Read and parse a json value.
             * @param {string} key - Key.
             * @param {*} [def=null] - Returned if missing, unavailable or not valid json.
             * @returns {*}
             */
            GetJson(key, def) {
                try {
                    const v = store()?.getItem(key);
                    if (v === null || typeof v === "undefined")
                        return def ?? null;
                    return JSON.parse(v);
                }
                catch {
                    return def ?? null;
                }
            },
            /**
             * Write a value as json.
             * @param {string} key - Key.
             * @param {*} value - Value (must be json serializable).
             * @returns {boolean} true if written.
             */
            SetJson(key, value) {
                try {
                    const s = store();
                    if (!s)
                        return false;
                    s.setItem(key, JSON.stringify(value));
                    return true;
                }
                catch {
                    return false;
                }
            },
        };
    }

    const local = wrap(() => window.localStorage);
    const session = wrap(() => window.sessionStorage);

    /**
     * @namespace Sw.Storage - never throwing web storage access.
     */
    Sw.Storage = {
        /** localStorage wrapper. */
        Local: local,
        /** sessionStorage wrapper. */
        Session: session,
        /** Shortcut to Local.Get. */
        Get: local.Get,
        /** Shortcut to Local.Set. */
        Set: local.Set,
        /** Shortcut to Local.Remove. */
        Remove: local.Remove,
        /** Shortcut to Local.GetJson. */
        GetJson: local.GetJson,
        /** Shortcut to Local.SetJson. */
        SetJson: local.SetJson,
    };

    // ------------------------------------------------------------------ Page state

    /** @type {object|*|null} Cached page state (object, or the foreign history.state value). */
    let state = null;

    /** @returns {boolean} true if this is the top window (state in history.state). */
    const isTop = () => window.top === window.self;

    /**
     * Get the (cached) page state.
     * @returns {object|*} The state object, or a non-object history.state value (read only).
     */
    function getState() {
        if (state)
            return state;
        if (isTop()) {
            let s = null;
            try {
                s = history.state;
            }
            catch {
            }
            state = s ? s : {};
        } else {
            state = session.GetJson(window.location.href, null) ?? {};
            if (typeof state !== "object")
                state = {};
        }
        return state;
    }

    /**
     * Persist the state (only when it is an object).
     * @param {object} s - The state.
     */
    function save(s) {
        if ((typeof s !== "object") || (s === null))
            return;
        if (isTop()) {
            try {
                history.replaceState(s, "");
            }
            catch (e) {
                console.warn("Sw.History: failed to save the page state: " + e.message);
            }
        } else {
            session.SetJson(window.location.href, s);
        }
    }

    /**
     * @namespace Sw.History - per page state that survives reloads of the same history entry / frame url.
     */
    Sw.History = {

        /**
         * Read a value from the page state.
         * @param {string} key - Key.
         * @param {*} [ifNotFound] - Returned when the key is missing.
         * @returns {*}
         */
        Get(key, ifNotFound) {
            const s = getState();
            if ((typeof s !== "object") || (s === null))
                return ifNotFound;
            const v = s[key];
            return typeof v === "undefined" ? ifNotFound : v;
        },

        /**
         * Set a value in the page state and persist it.
         * @param {string} key - Key.
         * @param {*} value - Value (structured cloneable and json serializable).
         */
        Set(key, value) {
            const s = getState();
            if ((typeof s !== "object") || (s === null))
                return;
            s[key] = value;
            save(s);
        },

        /**
         * Modify the page state in place; it is persisted when fn returns true.
         * @param {function(object):boolean} fn - Mutator.
         */
        Update(fn) {
            const s = getState();
            if ((typeof s !== "object") || (s === null))
                return;
            if (fn(s))
                save(s);
        },

        /**
         * Clear the page state (history.replaceState(null) in the top window, the sessionStorage entry in frames).
         */
        Clear() {
            state = null;
            if (isTop()) {
                try {
                    history.replaceState(null, "");
                }
                catch {
                }
            } else {
                session.Remove(window.location.href);
            }
        },

        /**
         * Remove the stored (frame) state of another page.
         * @param {string} url - The page url (its sessionStorage key).
         */
        ClearOther(url) {
            session.Remove(url);
        },
    };
})();
