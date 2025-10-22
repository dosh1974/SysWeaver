class EditCollectionArray {


    static Inst = new EditCollectionArray();

    createVal() {
        return [];
    }

    countVal(c) {
        if (!c)
            return 0;
        return c.length;
    }

    getVal(c, key) {
        return c[key];
    }

    setVal(c, key, newValue) {
        c[key] = newValue;
    }

    getKeyName(key) {
        return "[" + key + "]";
    }

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

    async onKeys(c, onKey) {
        if (!c)
            return;
        const l = c.length;
        for (let i = 0; i < l; ++i)
            await onKey(i);
    }

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

    removeVal(c, key) {
        c.splice(key, 1);
        return true;
    }

    //  Only for indexed:
    insertAt(c, newValue, key) {
        c.splice(key, 0, newValue);
        return key;
    }

    insertAfter(c, newValue, key) {
        ++key;
        c.splice(key, 0, newValue);
        return key;
    }

    move(c, from, to) {
        c.splice(to, 0, c.splice(from, 1)[0]);
    }



}
