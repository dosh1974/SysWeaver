class ParseTools {


    static DecodeObject(val) {
        if (val.startsWith('$')) {
            switch (val.length) {
                case 4:
                    return {
                        Red: parseInt(val.substring(1, 2), 16) / 15.0,
                        Green: parseInt(val.substring(2, 3), 16) / 15.0,
                        Blue: parseInt(val.substring(3, 4), 16) / 15.0,
                        Alpha: 1.0,
                    };
                case 5:
                    return {
                        Red: parseInt(val.substring(1, 2), 16) / 15.0,
                        Green: parseInt(val.substring(2, 3), 16) / 15.0,
                        Blue: parseInt(val.substring(3, 4), 16) / 15.0,
                        Alpha: parseInt(val.substring(4, 5), 16) / 15.0,
                    };
                case 7:
                    return {
                        Red: parseInt(val.substring(1, 3), 16) / 255.0,
                        Green: parseInt(val.substring(3, 5), 16) / 255.0,
                        Blue: parseInt(val.substring(5, 7), 16) / 255.0,
                        Alpha: 1.0,
                    };
                case 9:
                    return {
                        Red: parseInt(val.substring(1, 3), 16) / 255.0,
                        Green: parseInt(val.substring(3, 5), 16) / 255.0,
                        Blue: parseInt(val.substring(5, 7), 16) / 255.0,
                        Alpha: parseInt(val.substring(7, 9), 16) / 255.0,
                    };
            }
        }
        if (val.startsWith('{') || val.startsWith('['))
            return JSON.parse(val);
        return val;
    }

    static SetToObject(obj, key, val, onNotFound, logPrefix) {
        logPrefix = logPrefix ?? "";
        try {
            const kt = typeof obj[key];
            if (kt === "undefined") {
                if (onNotFound)
                    onNotFound(key, val);
                else
                    console.warn('"' + key + '" is not found!');
                return;
            }
            if (kt === "object") {
                const v = ParseTools.DecodeObject(val);
                obj[key] = v;
                console.log(logPrefix + key + ' = ' + JSON.stringify(v));
                return;
            }                
            if (kt === "string") {
                if (val.startsWith('$'))
                    val = "#" + val.substring(1);
                obj[key] = val;
                console.log(logPrefix + key + ' = "' + val + '"');
                return;
            }
            if (kt === "boolean") {
                const v = val === "true" || val === "1"
                obj[key] = v;
                console.log(logPrefix + key + ' = ' + (v ? "true" : "false"));
                return;
            }
            if (kt === "number") {
                const v = parseFloat(val);
                if (!isNaN(v)) {
                    obj[key] = v;
                    console.log(logPrefix + key + ' = ' + v);
                }
                return;
            }
        }
        catch (e) {
            console.warn(logPrefix + key + ": " + e.text);
        }
    }

    /**
     * Copy search params to an existing object.
     * @param {URLSearchParams} searchParams Search parameters
     * @param {object} dest The target object, any parameter that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     */
    static ParamsToObject(searchParams, dest, onNotFound, logPrefix) {
        for (let k of searchParams) {
            const key = k[0];
            const val = k[1];
            ParseTools.SetToObject(dest, key, val, onNotFound, logPrefix);
        }
    }

    /**
     * Copy object members to an existing object
     * @param {object} srcObject The source object, any member that exist in the destination will be copied
     * @param {object} dest The target object, any source member that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     */
    static ObjectToObject(srcObject, dest, onNotFound, logPrefix) {
        for (let key in srcObject) {
            const val = srcObject[key];
            if (typeof val !== "function")
                ParseTools.SetToObject(dest, key, "" + val, onNotFound, logPrefix);
        }
    }


    /**
     * Load some properties from a file
     * @param {string} srcUrl The url of a json file with properties
     * @param {object} dest The target object, any source member that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     */
    static async FileToObject(srcUrl, dest, onNotFound, logPrefix) {

        let effectVars = null;
        try {
            const res = await fetch(new Request(srcUrl, {
                method: "GET",
                mode: "cors",
                cache: "default",
            }));
            if (res.status === 200) {
                effectVars = await res.json();
/*                const t = await res.text();
                if (t) {
                    const a = JSON.parse(t);
                    effectVars = a;
                }*/
            }
        }
        catch {
        }
        if (effectVars)
            ParseTools.ObjectToObject(effectVars, dest, onNotFound, logPrefix);
    }




}
