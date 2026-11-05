/**
 * @file Type handler for colors (`Sw.Edit.Handlers.Color`, legacy global `EditTypeColor`), selected by the member spec
 * `Color[:<Format>[:Alpha][:<MaxIntensity>]]` (C#: `[EditColor(EditColorFormat.X, alpha, maxIntensity)]` in SysWeaver.Common).
 *
 * Loaded by Edit.js. The value cell shows a swatch (Sw.ColorPicker: saturation / value square, hue bar, hex field; a
 * checkerboard shows through transparent colors), a text box with the stored form (any css color, "0x" hex or a number list
 * can be typed), an alpha slider (formats with alpha) and an intensity box (HDR formats: colors brighter than white are
 * stored as color × intensity). Every change is one undoable step (a slider drag or a picker session coalesces).
 *
 * Formats (the stored value; internally a color is [r, g, b, a] with r, g, b >= 0 (above 1 for HDR) and a in 0..1):
 * | Format | Stored as |
 * |---|---|
 * | Auto | from the member type: String = Css, integers = UInt32Argb, Byte[] = Rgb8 / Rgba8 (by length), float arrays = Rgb / Rgba (by length), objects = Struct |
 * | Css | a css color string: "#rrggbb" ("#rrggbbaa" with Alpha); any css color is accepted when typed / pasted (named, rgb(), hsl() ...) |
 * | Hex | a css color string, always stored as "#rrggbb" ("#rrggbbaa" with Alpha): a typed css color (named, rgb() ...) is converted (for servers that read only hex) |
 * | Rgb, Rgba | float arrays [r, g, b(, a)] in 0..1 |
 * | HdrRgb, HdrRgba | float arrays, r, g, b may exceed 1 (intensity up to MaxIntensity, default 16) |
 * | Rgb8, Rgba8 | byte arrays [r, g, b(, a)] in 0..255 |
 * | UInt32Rgb, UInt32Rgba, UInt32Argb, UInt32Abgr | one integer 0xRRGGBB, 0xRRGGBBAA, 0xAARRGGBB (System.Drawing ToArgb), 0xAABBGGRR (RGBA bytes little endian) |
 * | Struct | an object with R, G, B(, A) (or r, g, b, a / X, Y, Z, W) float members in 0..1 (HDR when the format is HdrRgb / HdrRgba) |
 * Signed integer members (System.Int32 / Int64) store the bit pattern as the signed value.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const clamp = (v, a, b) => Math.min(b, Math.max(a, v));

    /** Formats with an alpha channel. */
    const AlphaFormats = new Set(["Rgba", "HdrRgba", "Rgba8", "UInt32Rgba", "UInt32Argb", "UInt32Abgr"]);
    /** Integer type names. */
    const IntTypes = new Set(["System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Nullable`1[[System.Int32]]", "System.Nullable`1[[System.UInt32]]"]);

    /**
     * Color handler (one instance per spec and member type).
     */
    class EditTypeColor extends E.Handler {
        /**
         * @param {string} typeName - The member type.
         * @param {string[]} [parts] - The spec parts ("Color", format, "Alpha", max intensity).
         */
        constructor(typeName, parts) {
            super();
            parts = parts || [];
            /** @type {string} Member type name. */
            this.TypeName = typeName || "";
            /** @type {string} The declared format (Auto resolves per value). */
            this.Format = parts[1] || "Auto";
            /** @type {boolean} Css colors with alpha ("#rrggbbaa"). */
            this.Alpha = parts.indexOf("Alpha") >= 2;
            const mi = parseFloat(parts[3]);
            /** @type {number} Largest HDR intensity. */
            this.MaxIntensity = mi > 1 ? mi : 16;
        }

        /**
         * The effective format of a value.
         * @param {*} [v] - A value (Auto: decides between 3 and 4 components, objects).
         * @returns {string} The format.
         */
        Kind(v) {
            const f = this.Format;
            if (f !== "Auto")
                return f;
            const tn = this.TypeName;
            if (tn === "System.String")
                return "Css";
            if (IntTypes.has(tn))
                return "UInt32Argb";
            if (v && typeof v === "object" && !Array.isArray(v))
                return "Struct";
            const four = Array.isArray(v) && v.length > 3;
            if (/Byte\[\]$|System\.Byte\]\]$/.test(tn))
                return four ? "Rgba8" : "Rgb8";
            return four ? "Rgba" : "Rgb";
        }

        /**
         * True when the format has an alpha channel.
         * @param {string} k - The format.
         * @param {*} [v] - The value (structs: an A member).
         * @returns {boolean} The result.
         */
        HasAlpha(k, v) {
            if (k === "Css" || k === "Hex")
                return this.Alpha;
            if (k === "Struct")
                return !!(v && EditTypeColor.Keys(v)[3]);
            return AlphaFormats.has(k);
        }

        /**
         * True for HDR formats.
         * @param {string} k - The format.
         * @returns {boolean} The result.
         */
        IsHdr(k) {
            return k === "HdrRgb" || k === "HdrRgba" || (k === "Struct" && /^Hdr/.test(this.Format));
        }

        /**
         * The member names of a color struct.
         * @param {object} v - The object.
         * @returns {string[]} [r, g, b, a] member names (a may be undefined).
         */
        static Keys(v) {
            for (const set of [["R", "G", "B", "A"], ["r", "g", "b", "a"], ["X", "Y", "Z", "W"], ["x", "y", "z", "w"], ["Red", "Green", "Blue", "Alpha"]])
                if (set.slice(0, 3).every(k => typeof v[k] === "number"))
                    return [set[0], set[1], set[2], typeof v[set[3]] === "number" ? set[3] : undefined];
            return [];
        }

        /**
         * Convert a stored value to [r, g, b, a].
         * @param {*} v - The value.
         * @returns {?number[]} The color or null when it isn't one.
         */
        ToColor(v) {
            if (v === null || v === undefined)
                return null;
            const k = this.Kind(v);
            if (typeof v === "string")
                return EditTypeColor.ParseText(v, k, this);
            if (typeof v === "number")
                return isFinite(v) ? EditTypeColor.FromUInt(v, k) : null;
            if (Array.isArray(v)) {
                if (v.length < 3 || !v.every(x => typeof x === "number" && isFinite(x)))
                    return null;
                const s = k === "Rgb8" || k === "Rgba8" ? 255 : 1;
                return [v[0] / s, v[1] / s, v[2] / s, v.length > 3 ? v[3] / s : 1].map(x => Math.max(0, x));
            }
            if (typeof v === "object") {
                const ks = EditTypeColor.Keys(v);
                if (!ks.length)
                    return null;
                return [v[ks[0]], v[ks[1]], v[ks[2]], ks[3] ? v[ks[3]] : 1].map(x => Math.max(0, x));
            }
            return null;
        }

        /**
         * Convert [r, g, b, a] to the stored form.
         * @param {number[]} c - The color.
         * @param {*} prev - The previous value (keeps the array length / struct members, the format of Auto).
         * @returns {*} The value.
         */
        FromColor(c, prev) {
            const k = this.Kind(prev);
            const hdr = this.IsHdr(k);
            const max = hdr ? this.MaxIntensity : 1;
            const r = clamp(c[0], 0, max), g = clamp(c[1], 0, max), b = clamp(c[2], 0, max), a = clamp(c[3] ?? 1, 0, 1);
            const byte = x => Math.round(clamp(x, 0, 1) * 255);
            const round = x => Math.round(x * 10000) / 10000;
            const hex2 = x => byte(x).toString(16).padStart(2, "0");
            switch (k) {
                case "Css": case "Hex":
                    return "#" + hex2(r) + hex2(g) + hex2(b) + (this.Alpha && a < 1 ? hex2(a) : "");
                case "Rgb": case "HdrRgb":
                    return [round(r), round(g), round(b)];
                case "Rgba": case "HdrRgba":
                    return [round(r), round(g), round(b), round(a)];
                case "Rgb8":
                    return [byte(r), byte(g), byte(b)];
                case "Rgba8":
                    return [byte(r), byte(g), byte(b), byte(a)];
                case "Struct": {
                    const o = Object.assign({}, prev);
                    const ks = EditTypeColor.Keys(prev);
                    o[ks[0]] = round(r);
                    o[ks[1]] = round(g);
                    o[ks[2]] = round(b);
                    if (ks[3])
                        o[ks[3]] = round(a);
                    return o;
                }
                default: {
                    let n;
                    switch (k) {
                        case "UInt32Rgb": n = (byte(r) << 16) | (byte(g) << 8) | byte(b); break;
                        case "UInt32Rgba": n = ((byte(r) << 24) | (byte(g) << 16) | (byte(b) << 8) | byte(a)); break;
                        case "UInt32Abgr": n = ((byte(a) << 24) | (byte(b) << 16) | (byte(g) << 8) | byte(r)); break;
                        default: n = ((byte(a) << 24) | (byte(r) << 16) | (byte(g) << 8) | byte(b)); break;
                    }
                    //  Signed members store the bit pattern as the signed value (System.Drawing.Color.ToArgb), unsigned ones as unsigned
                    return /Int32|Int64/.test(this.TypeName) && !/UInt/.test(this.TypeName) ? (n | 0) : (n >>> 0);
                }
            }
        }

        /**
         * The text form of a stored value (the text box).
         * @param {*} v - The value.
         * @returns {string} The text.
         */
        ToText(v) {
            if (v === null || v === undefined)
                return "";
            if (typeof v === "string")
                return v;
            if (typeof v === "number") {
                const k = this.Kind(v);
                return "0x" + (v >>> 0).toString(16).toUpperCase().padStart(k === "UInt32Rgb" ? 6 : 8, "0");
            }
            if (Array.isArray(v))
                return v.join(", ");
            const c = this.ToColor(v);
            return c ? c.slice(0, EditTypeColor.Keys(v)[3] ? 4 : 3).join(", ") : JSON.stringify(v);
        }

        /**
         * Unpack an integer color.
         * @param {number} v - The integer (signed values are read as their 32 bit pattern).
         * @param {string} k - The format (UInt32Rgb, UInt32Rgba, UInt32Abgr, else UInt32Argb).
         * @returns {number[]} [r, g, b, a].
         */
        static FromUInt(v, k) {
            const n = v >>> 0;
            const b = s => ((n >>> s) & 255) / 255;
            switch (k) {
                case "UInt32Rgb": return [b(16), b(8), b(0), 1];
                case "UInt32Rgba": return [b(24), b(16), b(8), b(0)];
                case "UInt32Abgr": return [b(0), b(8), b(16), b(24)];
                default: return [b(16), b(8), b(0), b(24)];
            }
        }

        /**
         * The layout "0x" hex text is read in: the member's own integer layout (the text box shows that form), else 0xRRGGBBAA (the
         * order of css #rrggbbaa). Up to 6 digits are always 0xRRGGBB (opaque).
         * @param {string} k - The format.
         * @param {number} digits - The number of hex digits.
         * @returns {string} The UInt32 layout.
         */
        static HexLayout(k, digits) {
            if (digits <= 6)
                return "UInt32Rgb";
            return k.startsWith("UInt32") ? k : "UInt32Rgba";
        }

        /**
         * Parse a typed / pasted text: a css color (named, #rgb, #rrggbb, #rrggbbaa, rgb(), hsl() ...), "0x" hex (see HexLayout), or
         * a list of 3 / 4 numbers. Number lists are read as 0..255 values when every number is a whole number and one is above 1
         * (above the maximum intensity for HDR formats) or the alpha is above 1; as 0..1 values when one has a fraction; else in the
         * scale of the format (0..255 for the byte formats, 0..1 for the others). Values outside the scale are refused (null).
         * @param {string} text - The text.
         * @param {string} k - The format.
         * @param {EditTypeColor} h - The handler.
         * @param {?object} [info] - Gets how the text was read: `Scale` (255 / 1, number lists), `Layout` (hex), `Css` (true).
         * @returns {?number[]} [r, g, b, a] or null.
         */
        static ParseText(text, k, h, info) {
            info = info || {};
            const s = ("" + text).trim();
            if (!s)
                return null;
            const hex = /^0x([0-9a-f]{1,8})$/i.exec(s);
            if (hex) {
                info.Layout = EditTypeColor.HexLayout(k, hex[1].length);
                return EditTypeColor.FromUInt(parseInt(hex[1], 16), info.Layout);
            }
            const h8 = /^#([0-9a-f]{8})$/i.exec(s);
            if (h8) {
                const n = parseInt(h8[1], 16) >>> 0;
                return [(n >>> 24 & 255) / 255, (n >>> 16 & 255) / 255, (n >>> 8 & 255) / 255, (n & 255) / 255];
            }
            const h4 = /^#([0-9a-f]{4})$/i.exec(s);
            if (h4)
                return h4[1].split("").map(x => parseInt(x + x, 16) / 255);
            const nums = s.replace(/^[\[({]|[\])}]$/g, "").split(/[\s,;]+/).filter(x => x);
            if (nums.length >= 3 && nums.length <= 4 && nums.every(x => /^-?\d*\.?\d+(e[-+]?\d+)?$/i.test(x))) {
                const raw = nums.map(x => parseFloat(x));
                const byteFormat = k === "Rgb8" || k === "Rgba8";
                const hdr = h ? h.IsHdr(k) : /^Hdr/.test(k);
                const maxRgb = hdr ? (h ? h.MaxIntensity : 16) : 1;
                const whole = raw.every(x => Number.isInteger(x));
                const fraction = !whole;
                let scale;
                if (fraction)
                    scale = 1;
                else if ((raw.length > 3 && raw[3] > 1) || raw.slice(0, 3).some(x => x > maxRgb))
                    scale = 255;
                else
                    scale = byteFormat ? 255 : 1;
                const v = raw.map(x => x / scale);
                //  Out of the scale (ex: 300 as a byte, an alpha of 2.5): not a color
                if (v.some(x => x < 0) || (v.length > 3 && v[3] > 1) || v.slice(0, 3).some(x => x > (scale === 255 ? 1 : maxRgb)))
                    return null;
                info.Scale = scale;
                return [v[0], v[1], v[2], v.length > 3 ? v[3] : 1];
            }
            info.Css = true;
            const css = Sw.Color && Sw.Color.ParseCss ? Sw.Color.ParseCss(s) : null;
            if (!css)
                return null;
            return [css[0] / 255, css[1] / 255, css[2] / 255, clamp(css[3], 0, 1)];
        }

        /**
         * Accept colors in any form this handler can read (stored forms and texts).
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            if (typeof obj === "string")
                return !!EditTypeColor.ParseText(obj, this.Kind(), this);
            return !!this.ToColor(obj);
        }

        /**
         * Convert an accepted value to the stored form of this member.
         * @param {*} obj - The value.
         * @returns {*} The stored value.
         */
        Condition(obj) {
            const c = typeof obj === "string" ? EditTypeColor.ParseText(obj, this.Kind(), this) : this.ToColor(obj);
            if (!c)
                return obj;
            const k = this.Kind(obj);
            //  A css string member keeps a typed css color as is when it is one (named colors stay named)
            if (k === "Css" && typeof obj === "string" && Sw.Color.ParseCss(obj) && !/^0x/i.test(obj) && (this.Alpha || c[3] >= 1))
                return obj.trim();
            return this.FromColor(c, typeof obj === "object" ? obj : undefined);
        }

        /**
         * Validate: null only with AcceptNull, else a readable color.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            if (this.IsOfType(obj))
                return null;
            return [name, (member.DisplayName || member.Name) + ": " + _T("\"{0}\" is not a valid color", typeof obj === "object" ? JSON.stringify(obj) : obj, "Validation error of a color value.{0} is replaced with the invalid value.")];
        }

        /**
         * The declared default (a json array / number / css text), else black (opaque), null with AcceptNull.
         * @param {object} member - The member.
         * @returns {*} The default.
         */
        GetDefault(member) {
            const d = member.Default;
            if (Has(d) && d !== "\t") {
                if (typeof d !== "string")
                    return d;
                const s = d.trim();
                if (/^[\[{]/.test(s)) {
                    try {
                        return JSON.parse(s);
                    }
                    catch {
                    }
                }
                if (/^-?\d+$/.test(s) && !/^(Css|Hex)$/.test(this.Kind()))
                    return parseInt(s, 10);
                if (this.Kind() !== "Css") {
                    const c = EditTypeColor.ParseText(s, this.Kind(), this);
                    if (c)
                        return this.FromColor(c);
                }
                return s;
            }
            if ((member.Flags & F.AcceptNull) !== 0)
                return null;
            const k = this.Kind();
            if (k === "Struct")
                return null;
            return this.FromColor([0, 0, 0, 1], k === "Rgba" || k === "HdrRgba" ? [0, 0, 0, 1] : k === "Rgba8" ? [0, 0, 0, 255] : undefined);
        }

        /**
         * Display text.
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        ToString(obj) {
            return this.ToText(obj);
        }

        /**
         * Build the swatch, text box, alpha slider and intensity box.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const t = this;
            const mn = member.Name;
            const cell = editContext.Element;
            const readOnly = this.IsReadOnly(member, options);
            const def = this.GetDefault(member);
            if (obj[mn] === undefined)
                obj[mn] = E.CloneValue(def);
            const dn = member.DisplayName || mn;
            const nullOk = (member.Flags & F.AcceptNull) !== 0;
            const kind = () => t.Kind(obj[mn] ?? def);
            const color = () => t.ToColor(obj[mn]) || [0, 0, 0, 1];
            const commit = async (c, token, undoText) => {
                const old = obj[mn];
                const v = t.FromColor(c, old ?? def);
                if (!E.IsDifferent(old, v))
                    return;
                await editor.SetMemberValue(member, v, old, undoText || _T("Set value: \"{0}\"", t.ToText(v), "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            };
            const wrap = Sw.el("div.sw-edit-color");
            //  The swatch (picker): rgb only, alpha and intensity have their own controls
            let pickToken = null;
            const picker = new Sw.ColorPicker({
                Title: _T("Pick the color of {0}", dn, "Tool tip and accessible name of the color swatch of a color value; a click opens a color picker.{0} is replaced with the name of the value"),
                Disabled: readOnly,
                OnInput: rgb => {
                    const c = color();
                    pickToken = pickToken || {};
                    commit([rgb[0], rgb[1], rgb[2], c[3]], pickToken);
                },
                OnChange: () => pickToken = null,
            });
            picker.E.classList.add("sw-edit-colorswatch");
            /** The note after the text box: how a typed text was read (number scale, hex layout) or that its alpha was dropped. */
            const note = Sw.el("span.sw-edit-hint.sw-edit-colornote", { attrs: { "aria-live": "polite" } });
            /** @type {?{Value: *, Text: string, Warn: boolean}} The note of the last typed text (shown while the value is that result). */
            let textNote = null;
            const text = E.CreateTextInput(() => obj[mn], async (v, ev, token) => {
                const old = obj[mn];
                if (!E.IsDifferent(old, v)) {
                    sync();
                    return;
                }
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", t.ToText(v), "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            }, s => {
                s = ("" + s).trim();
                if (!s)
                    return nullOk ? null : obj[mn];
                const info = {};
                const k = kind();
                const c = EditTypeColor.ParseText(s, k, t, info);
                if (!c)
                    return obj[mn];
                const hasAlpha = t.HasAlpha(k, obj[mn] ?? def);
                //  A valid css color typed into a css member is kept as typed
                const v = k === "Css" && Sw.Color.ParseCss(s) && !/^0x/i.test(s) && (t.Alpha || c[3] >= 1) ? s : t.FromColor(c, obj[mn] ?? def);
                //  Say what was not obvious: an alpha the member can't store (never dropped silently), the scale of a number list, the
                //  layout of a 0x hex text
                if (!hasAlpha && c[3] < 1)
                    textNote = {
                        Value: v, Warn: true,
                        Text: _T("No alpha in this value: stored opaque ({0} % opacity dropped)", Math.round(c[3] * 100), "Note after the text box of a color value that has no alpha channel, when the typed color was transparent.{0} is the opacity in percent that was dropped"),
                    };
                else if (info.Scale && info.Scale !== (k === "Rgb8" || k === "Rgba8" ? 255 : 1))
                    textNote = {
                        Value: v, Warn: false,
                        Text: info.Scale === 255 ? _TF("Read as 0 to 255 values", "Note after the text box of a color value when a typed list of numbers was read as bytes (0 to 255)")
                            : _TF("Read as 0 to 1 values", "Note after the text box of a color value when a typed list of numbers was read as fractions (0 to 1)"),
                    };
                else if (info.Layout && info.Layout !== "UInt32Rgb" && !k.startsWith("UInt32"))
                    textNote = { Value: v, Warn: false, Text: _TF("Read as 0xRRGGBBAA", "Note after the text box of a color value when a typed 0x hex text was read with the alpha last") };
                else if (!(textNote && !E.IsDifferent(textNote.Value, v)))
                    textNote = null; // the shown result parsed again (leaving the box) keeps its note
                return v;
            }, v => t.ToText(v), _TF("Any css color, ex: #ff8800, rgb(255 136 0), orange", "Placeholder of the text box of a color value"), null, null, 64, def, readOnly);
            text.classList.add("sw-edit-colortext");
            text.setAttribute("aria-label", dn);
            //  Invalid text: shown as an error while typing (the value keeps the last valid color); committing it (Enter / leaving
            //  the box) restores the valid text and says why
            text.addEventListener("change", () => {
                const s = text.value.trim();
                if (s && !EditTypeColor.ParseText(s, kind(), t))
                    Sw.Fail(_T("\"{0}\" is not a valid color", s, "Validation error of a color value.{0} is replaced with the invalid value."));
            }, true);
            text.addEventListener("input", () => {
                const s = text.value.trim();
                const bad = !!s && !EditTypeColor.ParseText(s, kind(), t);
                text.classList.toggle("sw-invalid", bad);
                text.setAttribute("aria-invalid", bad ? "true" : "false");
                text.title = bad ? _TF("Not a color: type a css color (#ff8800, rgb(255 136 0), orange), 0x hex or 3 to 4 numbers", "Tool tip of the text box of a color value when the text is not a color") : formatTip;
            });
            /** The tool tip of the text box: what can be typed and how 0x hex / number lists are read for this member. */
            const formatTip = (() => {
                const k = kind();
                const lay = k.startsWith("UInt32") ? { UInt32Rgb: "0xRRGGBB", UInt32Rgba: "0xRRGGBBAA", UInt32Argb: "0xAARRGGBB", UInt32Abgr: "0xAABBGGRR" }[k] : "0xRRGGBBAA";
                let s = _T("Type any css color, {0} hex or 3 to 4 numbers (0 to 1, or 0 to 255 when whole numbers above 1).", lay, "Tool tip of the text box of a color value.{0} is the hex layout, ex: 0xAARRGGBB");
                if (!t.HasAlpha(k, obj[mn] ?? def))
                    s += "\n" + _TF("This value has no alpha: transparent colors are stored opaque.", "Second line of the tool tip of the text box of a color value without an alpha channel");
                return s;
            })();
            text.title = formatTip;
            wrap.append(picker.E, text, note);
            let alpha = null, intensity = null;
            //  Read only: no alpha slider / intensity box (they would look live); the text shows the whole value
            if (!readOnly && t.HasAlpha(kind(), obj[mn] ?? def)) {
                let aToken = null;
                alpha = new Sw.Slider({
                    Min: 0, Max: 1, Step: 0.01, Value: color()[3], Default: 1, Disabled: readOnly,
                    Title: _T("Alpha (opacity) of {0}", dn, "Accessible name of the alpha slider of a color value.{0} is replaced with the name of the value"),
                    OnStart: () => aToken = {},
                    OnInput: a => {
                        const c = color();
                        commit([c[0], c[1], c[2], a], aToken || (aToken = {}));
                    },
                    OnChange: a => {
                        const c = color();
                        const tk = aToken;
                        aToken = null;
                        return commit([c[0], c[1], c[2], a], tk);
                    },
                });
                const label = Sw.el("span.sw-edit-colorlabel", { text: _TF("Alpha", "Label of the alpha (opacity) slider of a color value") });
                wrap.append(Sw.el("div.sw-edit-coloralpha", null, label, alpha.E));
            }
            if (!readOnly && t.IsHdr(kind())) {
                const inten = () => Math.max(1, ...color().slice(0, 3));
                intensity = new Sw.NumberBox({
                    Min: 1, Max: t.MaxIntensity, Step: 0.1, Value: inten(), Disabled: readOnly,
                    AriaLabel: _T("Intensity of {0}", dn, "Accessible name of the intensity (HDR brightness) box of a color value.{0} is replaced with the name of the value"),
                    Title: _TF("Intensity: colors brighter than white are stored as color × intensity (HDR)", "Tool tip of the intensity box of a HDR color value"),
                    Format: v => (Math.round(v * 100) / 100).toString(),
                    OnChange: x => {
                        const c = color();
                        const cur = inten();
                        const f = x / cur;
                        commit([c[0] * f, c[1] * f, c[2] * f, c[3]], null, _T("Set intensity: {0}", x, "Command text stored in a log when the intensity of a HDR color was changed.{0} is replaced with the new intensity."));
                    },
                });
                wrap.append(Sw.el("div.sw-edit-colorhdr", null, Sw.el("span.sw-edit-colorlabel", { text: _TF("Intensity", "Label of the intensity slider of a color picker (colors brighter than white)") }), intensity.E));
            }
            cell.append(wrap);
            const sync = () => {
                const v = obj[mn];
                const c = t.ToColor(v);
                const isNull = v === null || v === undefined;
                picker.Set(c ? c.slice(0, 3) : [0, 0, 0], true);
                //  Checkerboard + the color with its alpha (the picker shows the opaque color)
                const css = c ? "rgba(" + c.slice(0, 3).map(x => Math.round(clamp(x / Math.max(1, c[0], c[1], c[2]), 0, 1) * 255)).join(",") + "," + c[3] + ")" : "transparent";
                //  SwWeb:allow-design (the swatch shows the real colour over a checkerboard)
                picker.Swatch.style.background = "linear-gradient(" + css + "," + css + "), repeating-conic-gradient(#999 0 25%, #fff 0 50%) 0 0 / 10px 10px";
                picker.E.classList.toggle("sw-edit-colornull", isNull);
                if (isNull)
                    text.placeholder = "null";
                text.UpdateValue();
                text.classList.remove("sw-invalid");
                const n = textNote && !E.IsDifferent(textNote.Value, v) ? textNote : null;
                note.textContent = n ? n.Text : "";
                note.hidden = !n;
                note.classList.toggle("sw-edit-hintwarn", !!(n && n.Warn));
                if (alpha)
                    alpha.Set(c ? c[3] : 1, true, true);
                if (intensity)
                    intensity.Set(c ? Math.max(1, c[0], c[1], c[2]) : 1, true);
            };
            sync();
            const defText = def === null ? "null" : t.ToText(def);
            return E.Handler.Item(obj, editor, member, {
                Focus: text, Title: E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), defText),
                DefValue: def, DefValueText: defText, Update: sync,
            });
        }
    }

    /**
     * The plug-in factory (old hook name).
     * @param {string} typeName - The member type.
     * @param {string[]} parts - The spec parts.
     * @returns {?EditTypeColor} The handler (null for string collections).
     */
    function EditTypeColorFactory(typeName, parts) {
        //  A list of css strings (String[]) is a collection: its items get the spec with EditElementAttributes; numeric arrays are one color
        if (typeName && typeName !== "System.String" && /String/.test(typeName))
            return null;
        return new EditTypeColor(typeName, parts);
    }

    E.Handlers.Color = EditTypeColor;
    E.RegisterSpec("Color", EditTypeColorFactory);
    if (!window.EditTypeColor)
        window.EditTypeColor = EditTypeColor;
    if (!window.EditTypeColorFactory)
        window.EditTypeColorFactory = EditTypeColorFactory;
})();
