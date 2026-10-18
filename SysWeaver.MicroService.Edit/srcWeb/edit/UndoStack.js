/**
 * @file Linear undo/redo history used by the object editor.
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs`. Defines the global classes `UndoEntry` and
 * `UndoStack`. No dependencies on other files.
 *
 * Usage: the root `Edit` creates one `UndoStack` (unless `options.UndoStack` supplies one) and child editors share
 * it. Changes are registered through `Edit.prototype.Invoke`, which wraps the callbacks in path-based closures and
 * calls `UndoStack.Invoke`. Ctrl+Z / Ctrl+Y in an editor call `Undo()` / `Redo()`.
 * Errors thrown by callbacks are logged with `console.warn` and never propagate.
 */

/**
 * One recorded, reversible operation. Created by `UndoStack.Invoke`; never created directly elsewhere.
 */
class UndoEntry {
    /**
     * Stores the operation callbacks and metadata.
     * @param {function(boolean=):Promise<void>} doChange - Performs (redo) the change; called with `true` the first
     *   time, `false` on redo and without argument when a tracked (coalesced) change is updated.
     * @param {function():Promise<void>} doReverse - Reverts the change (undo).
     * @param {string} title - Human readable description of the change.
     * @param {string} name - Path name of the changed item (used for coalescing).
     * @param {*} tracking - Coalescing token; consecutive operations with equal `name` and `tracking` are merged.
     */
    constructor(doChange, doReverse, title, name, tracking) {
        this.DoChange = doChange;
        this.DoReverse = doReverse;
        this.Title = title;
        this.Name = name;
        this.Tracking = tracking;
    }

    /**
     * Debug text of the entry.
     * @returns {string} `"<Name>: <Title>"`.
     */
    toString() {
        return this.Name + ": " + this.Title;
    }

    /*
     * Fields (class field initializers run before the constructor body, so the constructor values win).
     * `Focus` (an optional element focused after undo/redo) is read by `UndoStack.Undo` / `Redo` but never assigned.
     */
    /** @type {?function(boolean=):Promise<void>} Redo callback (replaced when a tracked change is coalesced). */
    DoChange = null;
    /** @type {?function():Promise<void>} Undo callback. */
    DoReverse = null;
    /** @type {?string} Display title (replaced when a tracked change is coalesced). */
    Title = null;
    /** @type {?string} Path name of the changed item. */
    Name = null;
    /** @type {*} Coalescing token. */
    Tracking = null;
}

/**
 * Linear undo/redo history of `UndoEntry` objects. `Pos` is the number of applied operations; entries at or above
 * `Pos` are redoable and are discarded when a new operation is invoked.
 * One instance per editor tree, created by the root `Edit` (or supplied via `options.UndoStack`).
 */
class UndoStack {

    /**
     * Debug text of the stack position.
     * @returns {string} `"<Pos>/<Ops.length>"`.
     */
    toString() {
        return this.Pos+ "/" + this.Ops.length;
    }

    /**
     * Performs a change and records it. First truncates the redo history (entries at index >= `Pos`).
     * If `tracking` is truthy and the top entry has the same `name` and `tracking`, the change is coalesced:
     * `doChange()` runs (no argument) and the top entry's `DoChange` and `Title` are replaced (its `DoReverse` is kept,
     * so one undo reverts the whole sequence). Otherwise `doChange(true)` runs and a new `UndoEntry` is pushed.
     * If `doChange` throws, the error is logged with `console.warn` and nothing is recorded.
     * @param {function(boolean=):Promise<void>} doChange - Performs the change.
     * @param {function():Promise<void>} doReverse - Reverts the change.
     * @param {string} title - Description of the change.
     * @param {string} name - Path name of the changed item.
     * @param {*} [tracking] - Optional coalescing token.
     * @returns {Promise<void>}
     */
    async Invoke(doChange, doReverse, title, name, tracking) {
        const ops = this.Ops;

        let pos = this.Pos;
        let ol = ops.length;
        if (pos != ol) {
            ops.splice(pos);
            ol = pos;
        }
        if (tracking) {

            if (ol > 0) {
                const last = ops[ol - 1];
                if (last.Name === name) {
                    if (last.Tracking === tracking) {
                        try {
                            await doChange();
                            last.Title = title;
                            last.DoChange = doChange;
                            //console.log("[UPDATE] " + last);
                        }
                        catch (e) {
                            console.warn("[ERROR UPDATE] " + last);
                            console.warn(e);
                        }
                        return;
                    }
                }
            }
        }
        const op = new UndoEntry(doChange, doReverse, title, name, tracking);
        try {
            await doChange(true);
            //console.log("[DO] " + op);
            ops.push(op);
            ++pos;
            this.Pos = pos;
        }
        catch (e) {
            console.warn("[ERROR DO] " + op);
            console.warn(e);
        }
    }

    /**
     * Whether there is an operation to undo.
     * @returns {boolean} `Pos > 0`.
     */
    CanUndo() {
        return this.Pos > 0;
    }


    /**
     * Whether there is an operation to redo.
     * @returns {boolean} `Pos < Ops.length`.
     */
    CanRedo() {
        return this.Pos < this.Ops.length;
    }


    /**
     * Undoes the operation below `Pos`: awaits its `DoReverse()`, decrements `Pos` and focuses `op.Focus` if set
     * (never set in practice). Errors are logged with `console.warn` and leave `Pos` unchanged.
     * @returns {Promise<boolean>} True if an operation was undone, false when there was nothing to undo or it failed.
     */
    async Undo() {
        let pos = this.Pos;
        if (pos <= 0)
            return false;
        const ops = this.Ops;
        --pos;
        const op = ops[pos];
        try {
            await op.DoReverse();
            //console.log("[UNDO] " + op);
            this.Pos = pos;
            const f = op.Focus;
            if (f && (document.activeElement !== f))
                f.focus();
            return true;
        }
        catch (e) {
            console.warn("[ERROR UNDO] " + op);
            console.warn(e);
        }
        return false;
    }

    /**
     * Redoes the operation at `Pos`: awaits its `DoChange(false)`, increments `Pos` and focuses `op.Focus` if set
     * (never set in practice). Errors are logged with `console.warn` and leave `Pos` unchanged.
     * @returns {Promise<boolean>} True if an operation was redone, false when there was nothing to redo or it failed.
     */
    async Redo() {
        const ops = this.Ops;
        const ol = ops.length;
        const pos = this.Pos;
        if (pos >= ol)
            return false;
        const op = ops[pos];
        try {
            await op.DoChange(false);
            //console.log("[REDO] " + op);
            this.Pos = pos + 1;
            const f = op.Focus;
            if (f && (document.activeElement !== f))
                f.focus();
            return true;
        }
        catch (e) {
            console.warn("[ERROR REDO] " + op);
            console.warn(e);
        }
        return false;
    }


    /** @type {number} Number of applied operations (= index of the next redo entry). */
    Pos = 0;
    /** @type {UndoEntry[]} Recorded operations (applied ones below `Pos`, redoable ones from `Pos`). */
    Ops = [];
}
