/**
 * @file Linear undo / redo history of the object editor (`Sw.Edit.UndoStack`, `Sw.Edit.UndoEntry`; legacy globals
 * `UndoStack`, `UndoEntry`).
 *
 * Loaded by Edit.js. One stack per editor tree: the root `Sw.Edit` creates it on first use (unless `options.UndoStack`
 * supplies one, ex: mediaView/media.js) and child editors share it. Changes are registered through `Sw.Edit.Invoke`, which
 * wraps the callbacks in path based closures. Errors thrown by callbacks are logged and never propagate.
 */
(function () {
    "use strict";

    /**
     * One recorded, reversible operation (created by UndoStack.Invoke).
     */
    class UndoEntry {
        /**
         * @param {function(boolean=):Promise<void>} doChange - Performs (redo) the change; `true` the first time, `false` on redo.
         * @param {function():Promise<void>} doReverse - Reverts the change.
         * @param {string} title - Human readable description.
         * @param {string} name - Path of the changed item (coalescing key).
         * @param {*} tracking - Coalescing token: consecutive operations with equal name and token are merged.
         */
        constructor(doChange, doReverse, title, name, tracking) {
            /** @type {function} Redo callback (replaced when a tracked change is coalesced). */
            this.DoChange = doChange;
            /** @type {function} Undo callback. */
            this.DoReverse = doReverse;
            /** @type {string} Display title. */
            this.Title = title;
            /** @type {string} Path of the changed item. */
            this.Name = name;
            /** @type {*} Coalescing token. */
            this.Tracking = tracking;
            /** @type {?HTMLElement} Optional element focused after undo / redo. */
            this.Focus = null;
        }

        /**
         * Debug text.
         * @returns {string} "<Name>: <Title>".
         */
        toString() {
            return this.Name + ": " + this.Title;
        }
    }

    /**
     * Linear undo / redo history. `Pos` is the number of applied operations; entries at or above `Pos` are redoable and are
     * discarded when a new operation is invoked. Operations run one at a time (calls are queued).
     */
    class UndoStack {
        /** Create an empty history. */
        constructor() {
            /** @type {number} Number of applied operations. */
            this.Pos = 0;
            /** @type {UndoEntry[]} Recorded operations. */
            this.Ops = [];
            /** @type {Promise<void>} Serializes the operations. */
            this.Queue = Promise.resolve();
            /** @type {Array<function(UndoStack):void>} Change listeners (OnChange). */
            this.Listeners = [];
        }

        /**
         * Debug text.
         * @returns {string} "<Pos>/<Count>".
         */
        toString() {
            return this.Pos + "/" + this.Ops.length;
        }

        /**
         * Listen for changes of the history (an operation was done, undone or redone).
         * @param {function(UndoStack):void} fn - Listener.
         * @returns {function():void} Removes the listener.
         */
        OnChange(fn) {
            this.Listeners.push(fn);
            return () => {
                const i = this.Listeners.indexOf(fn);
                if (i >= 0)
                    this.Listeners.splice(i, 1);
            };
        }

        /** Notify the listeners. */
        Notify() {
            for (const fn of this.Listeners.slice()) {
                try {
                    fn(this);
                }
                catch (e) {
                    console.warn(e);
                }
            }
        }

        /**
         * Run a function after the queued operations.
         * @param {function():Promise<*>} fn - The operation.
         * @returns {Promise<*>} Its result.
         */
        Run(fn) {
            const p = this.Queue.then(fn, fn);
            this.Queue = p.then(() => { }, () => { });
            return p;
        }

        /**
         * Perform a change and record it (the redo history is discarded). With a truthy `tracking` equal to the top entry's
         * (same name), the change is coalesced into it: one undo reverts the whole sequence (continuous typing, a slider drag).
         * @param {function(boolean=):Promise<void>} doChange - Performs the change.
         * @param {function():Promise<void>} doReverse - Reverts the change.
         * @param {string} title - Description.
         * @param {string} name - Path of the changed item.
         * @param {*} [tracking] - Coalescing token.
         * @returns {Promise<void>}
         */
        Invoke(doChange, doReverse, title, name, tracking) {
            return this.Run(async () => {
                const ops = this.Ops;
                if (this.Pos !== ops.length)
                    ops.splice(this.Pos);
                const last = ops[ops.length - 1];
                if (tracking && last && last.Name === name && last.Tracking === tracking) {
                    try {
                        await doChange(true);
                        last.Title = title;
                        last.DoChange = doChange;
                    }
                    catch (e) {
                        console.warn("[UNDO UPDATE FAILED] " + last, e);
                    }
                    this.Notify();
                    return;
                }
                const op = new UndoEntry(doChange, doReverse, title, name, tracking);
                try {
                    await doChange(true);
                    ops.push(op);
                    this.Pos = ops.length;
                }
                catch (e) {
                    console.warn("[DO FAILED] " + op, e);
                }
                this.Notify();
            });
        }

        /**
         * Test if there is an operation to undo.
         * @returns {boolean} True when Pos > 0.
         */
        CanUndo() {
            return this.Pos > 0;
        }

        /**
         * Test if there is an operation to redo.
         * @returns {boolean} True when Pos < Ops.length.
         */
        CanRedo() {
            return this.Pos < this.Ops.length;
        }

        /**
         * Title of the next undo operation.
         * @returns {?string} The title.
         */
        UndoTitle() {
            return this.Pos > 0 ? this.Ops[this.Pos - 1].Title : null;
        }

        /**
         * Title of the next redo operation.
         * @returns {?string} The title.
         */
        RedoTitle() {
            return this.Pos < this.Ops.length ? this.Ops[this.Pos].Title : null;
        }

        /**
         * Undo the last applied operation.
         * @returns {Promise<boolean>} True if undone.
         */
        Undo() {
            return this.Run(async () => {
                if (this.Pos <= 0)
                    return false;
                const op = this.Ops[this.Pos - 1];
                try {
                    await op.DoReverse();
                    this.Pos--;
                    if (op.Focus && document.activeElement !== op.Focus)
                        op.Focus.focus();
                    this.Notify();
                    return true;
                }
                catch (e) {
                    console.warn("[UNDO FAILED] " + op, e);
                }
                return false;
            });
        }

        /**
         * Redo the next operation.
         * @returns {Promise<boolean>} True if redone.
         */
        Redo() {
            return this.Run(async () => {
                if (this.Pos >= this.Ops.length)
                    return false;
                const op = this.Ops[this.Pos];
                try {
                    await op.DoChange(false);
                    this.Pos++;
                    if (op.Focus && document.activeElement !== op.Focus)
                        op.Focus.focus();
                    this.Notify();
                    return true;
                }
                catch (e) {
                    console.warn("[REDO FAILED] " + op, e);
                }
                return false;
            });
        }

        /** Forget the whole history. */
        Clear() {
            this.Ops = [];
            this.Pos = 0;
            this.Notify();
        }
    }

    Sw.Edit.UndoStack = UndoStack;
    Sw.Edit.UndoEntry = UndoEntry;
    if (!window.UndoStack)
        window.UndoStack = UndoStack;
    if (!window.UndoEntry)
        window.UndoEntry = UndoEntry;
})();
