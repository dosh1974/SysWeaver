class UndoEntry {
    constructor(doChange, doReverse, title, name, tracking) {
        this.DoChange = doChange;
        this.DoReverse = doReverse;
        this.Title = title;
        this.Name = name;
        this.Tracking = tracking;
    }

    toString() {
        return this.Name + ": " + this.Title;
    }

    DoChange = null;
    DoReverse = null;
    Title = null;
    Name = null;
    Tracking = null;
}

class UndoStack {

    toString() {
        return this.Pos+ "/" + this.Ops.length;
    }

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

    CanUndo() {
        return this.Pos > 0;
    }


    CanRedo() {
        return this.Pos < this.Ops.length;
    }


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


    Pos = 0;
    Ops = [];
}
