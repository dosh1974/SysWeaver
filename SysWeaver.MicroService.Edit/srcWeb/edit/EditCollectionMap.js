/**
 * @file Collection strategy for dictionaries (`Sw.Edit.Collections.Map`, legacy global `EditCollectionMap`).
 *
 * Loaded by Edit.js; used by EditTypeCollection.js for collections with a key type (`member.KeyTypeName`, ex:
 * `Dictionary<String, Int32>`). The json value of a dictionary is a plain object (`{"key": value}`); JS `Map` instances are
 * supported too. Keys are strings (numeric keys are stored as their text, like json does). Not indexed: no insert / move.
 */
(function () {
    "use strict";

    /**
     * Dictionary strategy (stateless; the shared instance is `EditCollectionMap.Inst`).
     */
    class EditCollectionMap {
        /** @type {boolean} Items are not ordered by index. */
        get Indexed() {
            return false;
        }

        /** @returns {object} A new empty dictionary (plain object). */
        createVal() {
            return {};
        }

        /**
         * Number of entries.
         * @param {?(object|Map)} c - The dictionary.
         * @returns {number} The count.
         */
        countVal(c) {
            if (!c)
                return 0;
            return c instanceof Map ? c.size : Object.keys(c).filter(k => k !== "$type").length;
        }

        /**
         * Get a value.
         * @param {object|Map} c - The dictionary.
         * @param {string} key - Key.
         * @returns {*} The value.
         */
        getVal(c, key) {
            return c instanceof Map ? c.get(key) : c[key];
        }

        /**
         * Set a value.
         * @param {object|Map} c - The dictionary.
         * @param {string} key - Key.
         * @param {*} v - The value.
         */
        setVal(c, key, v) {
            if (c instanceof Map)
                c.set(key, v);
            else
                c[key] = v;
        }

        /**
         * Test if a key exists.
         * @param {?(object|Map)} c - The dictionary.
         * @param {string} key - Key.
         * @returns {boolean} True if present.
         */
        hasKey(c, key) {
            if (!c)
                return false;
            return c instanceof Map ? c.has(key) : Object.prototype.hasOwnProperty.call(c, key);
        }

        /**
         * Display text of a key.
         * @param {string} key - Key.
         * @returns {string} "[key]".
         */
        getKeyName(key) {
            return "[" + key + "]";
        }

        /**
         * The keys (insertion order).
         * @param {?(object|Map)} c - The dictionary.
         * @returns {string[]} The keys.
         */
        keys(c) {
            if (!c)
                return [];
            return c instanceof Map ? Array.from(c.keys()) : Object.keys(c).filter(k => k !== "$type");
        }

        /**
         * Iterate the entries, awaiting the callback; stops when it returns falsy.
         * @param {?(object|Map)} c - The dictionary.
         * @param {function(string, *):Promise<boolean>} onKeyValue - Callback.
         * @returns {Promise<boolean>} False when stopped early.
         */
        async iterate(c, onKeyValue) {
            for (const k of this.keys(c))
                if (!(await onKeyValue(k, this.getVal(c, k))))
                    return false;
            return true;
        }

        /**
         * Old (misspelled, non ASCII) name of iterate.
         * @param {?(object|Map)} c - The dictionary.
         * @param {function(string, *):Promise<boolean>} fn - Callback.
         * @returns {Promise<boolean>} See iterate.
         */
        íterate(c, fn) {
            return this.iterate(c, fn);
        }

        /**
         * Await a callback for each key.
         * @param {?(object|Map)} c - The dictionary.
         * @param {function(string):Promise<void>} onKey - Callback.
         * @returns {Promise<void>}
         */
        async onKeys(c, onKey) {
            for (const k of this.keys(c))
                await onKey(k);
        }

        /**
         * Add (or replace) an entry.
         * @param {object|Map} c - The dictionary.
         * @param {*} v - The value.
         * @param {string} key - Key.
         * @returns {string} The key.
         */
        addVal(c, v, key) {
            this.setVal(c, key, v);
            return key;
        }

        /**
         * Remove an entry.
         * @param {object|Map} c - The dictionary.
         * @param {string} key - Key.
         * @returns {boolean} False: other keys are unchanged.
         */
        removeVal(c, key) {
            if (c instanceof Map)
                c.delete(key);
            else
                delete c[key];
            return false;
        }

        /**
         * Re-insert an entry (undo of a remove; the position is not kept for plain objects).
         * @param {object|Map} c - The dictionary.
         * @param {*} v - The value.
         * @param {string} key - Key.
         * @returns {string} The key.
         */
        insertAt(c, v, key) {
            return this.addVal(c, v, key);
        }
    }

    /** @type {EditCollectionMap} Shared instance. */
    EditCollectionMap.Inst = new EditCollectionMap();
    Sw.Edit.Collections.Map = EditCollectionMap.Inst;
    if (!window.EditCollectionMap)
        window.EditCollectionMap = EditCollectionMap;
})();
