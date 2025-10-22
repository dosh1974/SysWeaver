class EditCollectionMap {


    static Inst = new EditCollectionMap();

    createVal() {
        return new Map();
    }

    countVal(c) {
        if (!c)
            return 0;
        return c.size;
    }

    getVal(c, key) {
        return c.get(key);
    }

    setVal(c, key, newValue) {
        c.set(key, newValue);
    }

    getKeyName(key) {
        return "[" + key + "]";
    }

    async íterate(c, onKeyValue) {
        if (!c)
            return;
        const i = c.entries();
        while (i.next()) {
            const v = i.value;
            if (!(await onKeyValue(v[0], v[1])))
                return false;
        }
        return true;
    }


    async onKeys(c, onKey) {
        if (!c)
            return;
        for (let i of c.keys())
            await onKey(i);
    }

    addVal(c, newValue, key) {
        c.set(key, newValue);
        return key;
    }

    removeVal(c, key) {
        c.delete(key);
        return false;
    }


}
