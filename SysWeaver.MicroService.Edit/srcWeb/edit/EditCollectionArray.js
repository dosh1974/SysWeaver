/**
 * @file Collection strategy for arrays / lists (`Sw.Edit.Collections.Array`, legacy global `EditCollectionArray`).
 *
 * Loaded by Edit.js; used by EditTypeCollection.js for every collection without a key type. Keys are the indices.
 * All methods tolerate a null collection where noted.
 */
(function () {
    "use strict";

    /**
     * Array strategy (stateless; the shared instance is `EditCollectionArray.Inst`).
     */
    class EditCollectionArray {
        /** @type {boolean} Items are ordered (insert / move are possible). */
        get Indexed() {
            return true;
        }

        /** @returns {Array} A new empty collection. */
        createVal() {
            return [];
        }

        /**
         * Number of items.
         * @param {?Array} c - The collection.
         * @returns {number} The count (0 for null).
         */
        countVal(c) {
            return c ? c.length : 0;
        }

        /**
         * Get an item.
         * @param {Array} c - The collection.
         * @param {number} key - Index.
         * @returns {*} The item.
         */
        getVal(c, key) {
            return c[key];
        }

        /**
         * Set an item.
         * @param {Array} c - The collection.
         * @param {number} key - Index.
         * @param {*} v - The value.
         */
        setVal(c, key, v) {
            c[key] = v;
        }

        /**
         * Display text of a key.
         * @param {number} key - Index.
         * @returns {string} "[key]".
         */
        getKeyName(key) {
            return "[" + key + "]";
        }

        /**
         * The keys in order.
         * @param {?Array} c - The collection.
         * @returns {number[]} The indices.
         */
        keys(c) {
            return c ? c.map((x, i) => i) : [];
        }

        /**
         * Iterate the items in order, awaiting the callback; stops when it returns falsy.
         * @param {?Array} c - The collection.
         * @param {function(number, *):Promise<boolean>} onKeyValue - Callback.
         * @returns {Promise<boolean>} False when stopped early.
         */
        async iterate(c, onKeyValue) {
            if (!c)
                return true;
            for (let i = 0; i < c.length; ++i)
                if (!(await onKeyValue(i, c[i])))
                    return false;
            return true;
        }

        /**
         * Old (misspelled, non ASCII) name of iterate.
         * @param {?Array} c - The collection.
         * @param {function(number, *):Promise<boolean>} fn - Callback.
         * @returns {Promise<boolean>} See iterate.
         */
        íterate(c, fn) {
            return this.iterate(c, fn);
        }

        /**
         * Await a callback for each key.
         * @param {?Array} c - The collection.
         * @param {function(number):Promise<void>} onKey - Callback.
         * @returns {Promise<void>}
         */
        async onKeys(c, onKey) {
            for (const k of this.keys(c))
                await onKey(k);
        }

        /**
         * Add an item (at key when given and inside, else at the end).
         * @param {Array} c - The collection.
         * @param {*} v - The value.
         * @param {number} [key] - Insert position.
         * @returns {number} The key of the new item.
         */
        addVal(c, v, key) {
            if (key !== undefined && key !== null && key < c.length) {
                c.splice(key, 0, v);
                return key;
            }
            c.push(v);
            return c.length - 1;
        }

        /**
         * Remove an item.
         * @param {Array} c - The collection.
         * @param {number} key - Index.
         * @returns {boolean} True: the following keys changed (re-number).
         */
        removeVal(c, key) {
            c.splice(key, 1);
            return true;
        }

        /**
         * Insert an item before an index.
         * @param {Array} c - The collection.
         * @param {*} v - The value.
         * @param {number} key - Index.
         * @returns {number} The new key.
         */
        insertAt(c, v, key) {
            c.splice(key, 0, v);
            return key;
        }

        /**
         * Insert an item after an index.
         * @param {Array} c - The collection.
         * @param {*} v - The value.
         * @param {number} key - Index.
         * @returns {number} The new key.
         */
        insertAfter(c, v, key) {
            c.splice(key + 1, 0, v);
            return key + 1;
        }

        /**
         * Move an item.
         * @param {Array} c - The collection.
         * @param {number} from - Source index.
         * @param {number} to - Target index.
         */
        move(c, from, to) {
            c.splice(to, 0, c.splice(from, 1)[0]);
        }
    }

    /** @type {EditCollectionArray} Shared instance. */
    EditCollectionArray.Inst = new EditCollectionArray();
    Sw.Edit.Collections.Array = EditCollectionArray.Inst;
    if (!window.EditCollectionArray)
        window.EditCollectionArray = EditCollectionArray;
})();
