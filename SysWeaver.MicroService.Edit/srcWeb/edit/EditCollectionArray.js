/**
 * @file Array container strategy for the collection editor.
 *
 * Loaded on demand by `EditTypeCollection.Init()` (EditTypeCollection.js) via `includeJs`. Defines the global class
 * `EditCollectionArray`. No dependencies on other files.
 *
 * A "container strategy" abstracts the storage of a collection value so `EditTypeCollection` can treat JS arrays
 * and `Map`s alike (duck-typed interface shared with `EditCollectionMap`: createVal, countVal, getVal, setVal,
 * getKeyName, íterate, onKeys, addVal, removeVal; arrays additionally support insertAt, insertAfter, move).
 */

/**
 * Container strategy for JS arrays (collections without a `Key` in their member description).
 * Stateless; used through the singleton `EditCollectionArray.Inst` returned by `EditTypeCollection.GetContainer`.
 * In all methods `c` is the array and `key` an integer index.
 */
class EditCollectionArray {


    /** @type {EditCollectionArray} Shared singleton instance. */
    static Inst = new EditCollectionArray();

    /**
     * Creates a new empty collection value.
     * @returns {Array} A new empty array.
     */
    createVal() {
        return [];
    }

    /**
     * Counts the elements.
     * @param {?Array} c - The array (may be null/undefined).
     * @returns {number} `c.length`, or 0 for a falsy `c`.
     */
    countVal(c) {
        if (!c)
            return 0;
        return c.length;
    }

    /**
     * Gets an element.
     * @param {Array} c - The array.
     * @param {number} key - Index.
     * @returns {*} `c[key]`.
     */
    getVal(c, key) {
        return c[key];
    }

    /**
     * Sets an element in place.
     * @param {Array} c - The array.
     * @param {number} key - Index.
     * @param {*} newValue - Value to store.
     * @returns {void}
     */
    setVal(c, key, newValue) {
        c[key] = newValue;
    }

    /**
     * Display / path name of a key, used as item title and in undo paths.
     * @param {number} key - Index.
     * @returns {string} `"[" + key + "]"`.
     */
    getKeyName(key) {
        return "[" + key + "]";
    }

    /**
     * Iterates the elements in order, awaiting the callback for each (note: the name uses a non-ASCII `í`).
     * Stops at the first element for which the callback returns a falsy value.
     * @param {?Array} c - The array.
     * @param {function(number, *):Promise<boolean>} onKeyValue - Called with (index, value); return truthy to continue.
     * @returns {Promise<boolean|undefined>} False if stopped early, true if all elements were visited,
     *   undefined for a falsy `c`.
     */
    async íterate(c, onKeyValue) {
        if (!c)
            return;
        const l = c.length;
        for (let i = 0; i < l; ++i) {
            if (!(await onKeyValue(i, c[i])))
                return false;
        }
        return true;
    }

    /**
     * Awaits the callback for each index in order (the length is read once before iterating).
     * @param {?Array} c - The array; nothing happens for a falsy value.
     * @param {function(number):(Promise<void>|void)} onKey - Called with each index.
     * @returns {Promise<void>}
     */
    async onKeys(c, onKey) {
        if (!c)
            return;
        const l = c.length;
        for (let i = 0; i < l; ++i)
            await onKey(i);
    }

    /**
     * Adds an element: inserts it at `key` when `key` is defined and `< c.length`, otherwise appends it.
     * @param {Array} c - The array (modified in place).
     * @param {*} newValue - Value to add.
     * @param {number} [key] - Optional insert position.
     * @returns {number} The index of the new element.
     */
    addVal(c, newValue, key) {

        if (typeof key !== "undefined") {
            if (key < c.length) {
                c.splice(key, 0, newValue);
                return key;
            }
        }
        c.push(newValue);
        return c.length - 1;
    }

    /**
     * Removes the element at `key` (splice), shifting later elements down.
     * @param {Array} c - The array (modified in place).
     * @param {number} key - Index to remove.
     * @returns {boolean} Always true: the keys of later elements changed and the item editors must be renumbered
     *   (`reOrder` in EditTypeCollection).
     */
    removeVal(c, key) {
        c.splice(key, 1);
        return true;
    }

    //  Only for indexed:
    /**
     * Inserts an element at `key` (indexed collections only; `EditCollectionMap` has no such method).
     * Used for "Insert before" and to undo a removal.
     * @param {Array} c - The array (modified in place).
     * @param {*} newValue - Value to insert.
     * @param {number} key - Insert position.
     * @returns {number} `key`.
     */
    insertAt(c, newValue, key) {
        c.splice(key, 0, newValue);
        return key;
    }

    /**
     * Inserts an element after `key`, i.e. at `key + 1` (indexed collections only).
     * @param {Array} c - The array (modified in place).
     * @param {*} newValue - Value to insert.
     * @param {number} key - Index of the element to insert after.
     * @returns {number} The index of the new element (`key + 1`).
     */
    insertAfter(c, newValue, key) {
        ++key;
        c.splice(key, 0, newValue);
        return key;
    }

    /**
     * Moves one element from index `from` to index `to` (indexed collections only).
     * @param {Array} c - The array (modified in place).
     * @param {number} from - Current index.
     * @param {number} to - Target index (after the element was removed).
     * @returns {void}
     */
    move(c, from, to) {
        c.splice(to, 0, c.splice(from, 1)[0]);
    }



}
