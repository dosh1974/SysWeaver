/**
 * @file `Map` container strategy for the collection editor.
 *
 * Loaded on demand by `EditTypeCollection.Init()` (EditTypeCollection.js) via `includeJs`. Defines the global class
 * `EditCollectionMap`. No dependencies on other files.
 *
 * Same duck-typed container interface as `EditCollectionArray` (createVal, countVal, getVal, setVal, getKeyName,
 * íterate, onKeys, addVal, removeVal) but without the indexed operations insertAt / insertAfter / move.
 */

/**
 * Container strategy for dictionaries stored as JS `Map` (members with both `Key` and `Element` in their description).
 * Stateless; used through the singleton `EditCollectionMap.Inst` returned by `EditTypeCollection.GetContainer`.
 * In all methods `c` is the `Map` and `key` a map key.
 */
class EditCollectionMap {


    /** @type {EditCollectionMap} Shared singleton instance. */
    static Inst = new EditCollectionMap();

    /**
     * Creates a new empty collection value.
     * @returns {Map} A new empty map.
     */
    createVal() {
        return new Map();
    }

    /**
     * Counts the entries.
     * @param {?Map} c - The map (may be null/undefined).
     * @returns {number} `c.size`, or 0 for a falsy `c`.
     */
    countVal(c) {
        if (!c)
            return 0;
        return c.size;
    }

    /**
     * Gets a value.
     * @param {Map} c - The map.
     * @param {*} key - Map key.
     * @returns {*} `c.get(key)`.
     */
    getVal(c, key) {
        return c.get(key);
    }

    /**
     * Sets a value.
     * @param {Map} c - The map.
     * @param {*} key - Map key.
     * @param {*} newValue - Value to store.
     * @returns {void}
     */
    setVal(c, key, newValue) {
        c.set(key, newValue);
    }

    /**
     * Display / path name of a key, used as item title and in undo paths.
     * @param {*} key - Map key.
     * @returns {string} `"[" + key + "]"`.
     */
    getKeyName(key) {
        return "[" + key + "]";
    }

    /**
     * Iterates the entries, awaiting the callback for each (note: the name uses a non-ASCII `í`).
     * Stops at the first entry for which the callback returns a falsy value.
     * Only used by `EditTypeCollection.Validate`.
     * @param {?Map} c - The map.
     * @param {function(*, *):Promise<boolean>} onKeyValue - Called with (key, value); return truthy to continue.
     * @returns {Promise<boolean|undefined>} False if stopped early, true if all entries were visited,
     *   undefined for a falsy `c`.
     */
    async íterate(c, onKeyValue) {
        if (!c)
            return;
        const i = c.entries();
        let r;
        while (!(r = i.next()).done) {
            const v = r.value;
            if (!(await onKeyValue(v[0], v[1])))
                return false;
        }
        return true;
    }


    /**
     * Awaits the callback for each key (in insertion order).
     * @param {?Map} c - The map; nothing happens for a falsy value.
     * @param {function(*):(Promise<void>|void)} onKey - Called with each key.
     * @returns {Promise<void>}
     */
    async onKeys(c, onKey) {
        if (!c)
            return;
        for (let i of c.keys())
            await onKey(i);
    }

    /**
     * Adds (or replaces) an entry with `c.set(key, newValue)`. When called without a key (as the collection
     * "Add item" menu does) the entry is stored under the key `undefined`.
     * @param {Map} c - The map.
     * @param {*} newValue - Value to add.
     * @param {*} key - Map key.
     * @returns {*} `key`.
     */
    addVal(c, newValue, key) {
        c.set(key, newValue);
        return key;
    }

    /**
     * Deletes an entry.
     * @param {Map} c - The map.
     * @param {*} key - Map key.
     * @returns {boolean} Always false: other keys are unaffected, so no renumbering is needed.
     */
    removeVal(c, key) {
        c.delete(key);
        return false;
    }


}
