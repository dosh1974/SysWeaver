/**
 * @file core/format.js - pure (DOM free) value formatting.
 *
 * Purpose: `Sw.Format` re-implements every text producing part of the old ValueFormat (and a few small helpers):
 *  - numbers: Number (thousands grouping + fixed decimals), Amount (currency, CurrencyOptions), Suffix
 *    (k/M/G/T/E), Bytes, ByteSpeed;
 *  - time: DurationMs (elapsed ms as one translated unit), TimeSpan (.NET TimeSpan text or seconds), TimeStamp
 *    ([tooltip, text as the format locale writes it]), Locale / LocaleChoice / AutoLocale / SetLocale / LocalText (the locale of dates and
 *    times: the user's choice in localStorage "SysWeaver.FormatLocale", else detected from the time zone by core/region.js, loaded once),
 *    LocalToUtc / UtcToLocal, ParseDate (zone-less ISO = UTC, "yyyy-MM-dd" = local day), Uptime (elapsed since a date);
 *    zones (the time rule in core/README "Times"): OffsetText, ZoneText, ZoneHint, ServerZone / SetServerZone / ServerZoneFrom /
 *    ServerZoneText, ServerToDate, ServerText, ServerToLocalText;
 *  - text: StringFormat / StringFormatArgs (same as Sw.I18n.Format), CountSuffix, RemoveCamelCase,
 *    KeyValueSplit, JoinNonEmpty, AddNonNullLine, HtmlSafe, HtmlAttributeSafe, JsonToHtml, RankScoreMatch;
 *  - validation: IsValidEmail, IsValidPhone, IsLetter;
 *  - files: FileExtension, Ext (extension tables), IsImageExt/IsVideoExt/IsAudioExt/IsProgrammingExt/
 *    IsCompressibleExt, IsImageFile, IsVideoFile;
 *  - typed values: Value(type, value, format, nextValue) -> {Text, Title, Kind} for the simple cell formats
 *    (Default, Number, Boolean, DateTime, Duration/TimeSpan, ByteSize, ByteSpeed, Uptime); the DOM cell
 *    renderers live in the ui library.
 * User visible unit texts are translated (translation markers with literals).
 *
 * Load order: after core/i18n.js. Dependencies: Sw.I18n.
 * Globals defined: none (Sw.Format).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Bit flags for Sw.Format.Amount. The low two bits (mask Symbol = 3) select the style.
     * @type {Readonly<{None:number, IsoPrefix:number, IsoSuffix:number, Symbol:number, ForceRounding:number, ApplyThousandsSeparator:number, AutomaticRounding:number, Default:number}>}
     */
    const CurrencyOptions = Object.freeze({
        /** No currency code or symbol. */
        None: 0,
        /** ISO 4217 code prefix, ex: "USD 100.00". */
        IsoPrefix: 1,
        /** ISO 4217 code suffix, ex: "100.00 USD". */
        IsoSuffix: 2,
        /** Symbol formatting, ex: "$100.00", "100.00 kr". */
        Symbol: 3,
        /** Round to an integer (no under units). */
        ForceRounding: 4,
        /** Apply the thousands separator. */
        ApplyThousandsSeparator: 8,
        /** Remove zero under units, ex: "$100.00" => "$100". */
        AutomaticRounding: 16,
        /** Default: Symbol | ApplyThousandsSeparator. */
        Default: 3 | 8,
    });

    /**
     * Convert a number to a string with thousands separator and a fixed number of decimals.
     * Never uses exponent notation: 8.6e-7 with 8 decimals is "0.00000086", 1e21 is "1 000 000 000 000 000 000 000"
     * (a tiny value that rounds to zero with the given decimals is "0.00", as the original). Without decimals the fraction is cut off (12.7 -> "12").
     * NaN / Infinity / non numbers are returned as their text.
     * @param {number} value - The value.
     * @param {number} [decimals] - Decimals (0..100); negative = that many decimals only for non-integers.
     * @param {string} [thousandSeparator=" "] - Thousands separator.
     * @param {string} [decimalSeparator="."] - Decimal separator.
     * @returns {string}
     */
    function number(value, decimals, thousandSeparator, decimalSeparator) {
        thousandSeparator = thousandSeparator || ' ';
        decimalSeparator = decimalSeparator || '.';
        const n = typeof value === "number" ? value : parseFloat(value);
        if (!isFinite(n))
            return '' + value;
        if (decimals < 0)
            decimals = Math.floor(n) === n ? 0 : -decimals;
        if (decimals > 100)
            decimals = 100;
        const haveDecimals = (!!decimals) && (decimals > 0);
        //  Digits without exponent notation ('' + 8.6e-7 is "8.6e-7", '' + 1e21 is "1e+21")
        let v;
        if (Math.abs(n) >= 1e21)
            v = BigInt(Math.trunc(n)).toString() + (haveDecimals ? '.' : '');
        else if (haveDecimals) {
            const scale = Math.pow(10, decimals);
            const rounded = Math.round(n * scale) / scale;
            v = (isFinite(rounded) ? rounded : n).toFixed(decimals);
        } else
            v = '' + Math.trunc(n);
        let sign = '';
        if (v[0] === '-') {
            sign = '-';
            v = v.slice(1);
            if (/^[0.]*$/.test(v))
                sign = '';
        }
        let r = v;
        let f = '';
        const p = v.indexOf('.');
        if (p >= 0) {
            r = v.slice(0, p);
            f = v.slice(p + 1);
        }
        let rl = r.length;
        for (; ;) {
            rl -= 3;
            if (rl <= 0)
                break;
            r = r.slice(0, rl) + thousandSeparator + r.slice(rl);
        }
        if (!haveDecimals)
            return sign + r;
        const fl = f.length;
        if (fl < decimals)
            f = f.padEnd(decimals, '0');
        else if (fl > decimals)
            f = f.slice(0, decimals);
        return sign + r + decimalSeparator + f;
    }

    /**
     * Format a value with a k/M/G/T/E unit prefix (values below 5*k as integers with emptySuffix, otherwise
     * divided by k until below 5*k, one decimal). Negative values get a "- " prefix.
     * @param {number} value - Value.
     * @param {string} suffix - Unit after the prefix letter (e.g. "B").
     * @param {string} emptySuffix - Unit without prefix (e.g. "bytes").
     * @param {number} [k=1024] - Step factor.
     * @param {number} [decimals=1] - Decimals of a prefixed value.
     * @param {string[]} [prefixes] - Prefix letters for k^1..k^5 (default k, M, G, T, E).
     * @returns {string}
     */
    function suffix(value, suffix, emptySuffix, k, decimals, prefixes) {
        const isNeg = value < 0;
        const prefix = isNeg ? "- " : "";
        if (isNeg)
            value = -value;
        if (!k)
            k = 1024;
        if (typeof decimals !== "number")
            decimals = 1;
        const cutOff = k * 5;
        if (value < cutOff)
            return prefix + number(value, 0) + ' ' + emptySuffix;
        const units = prefixes || suffixPrefixes;
        const last = units.length - 1;
        for (let i = 0; i < last; ++i) {
            value /= k;
            if (value < cutOff)
                return prefix + number(value, decimals) + ' ' + units[i] + suffix;
        }
        value /= k;
        return prefix + number(value, decimals) + ' ' + units[last] + suffix;
    }

    /** Default unit prefixes of suffix (k^1..k^5). */
    const suffixPrefixes = ["k", "M", "G", "T", "E"];

    /** Unit prefixes of byte values (K^1..K^5, upper case K as in KB). */
    const bytePrefixes = ["K", "M", "G", "T", "E"];


    /**
     * Format a duration in ms as one translated unit: ms (< 100), seconds (1 decimal below 10 s), minutes,
     * hours, days, years.
     * @param {number} value - Duration in ms.
     * @param {boolean|string} [blankIfNeg] - For negative values return "" (true) or this string; otherwise a "-" prefix.
     * @returns {string}
     */
    function durationMs(value, blankIfNeg) {
        const isNeg = value < 0;
        if (isNeg) {
            if (blankIfNeg)
                return typeof blankIfNeg === "string" ? blankIfNeg : "";
            value = -value;
        }
        const prefix = isNeg ? "-" : "";
        if (value < 100)
            return prefix + _T("{0} ms", number(value, 0), 'Text formatting for an elapsed time in milliseconds, example: "13 ms". {0} is the formatted number of milliseconds, example: 13');
        value /= 1000;
        if (value < 10)
            return prefix + _T("{0} seconds", number(value, 1), 'Text formatting for an elapsed time in seconds, example: "48 seconds". {0} is the formatted number of seconds, it may have one decimal, example: 48');
        if (value < 600)
            return prefix + _T("{0} seconds", number(value, 0), 'Text formatting for an elapsed time in seconds, example: "48 seconds". {0} is the formatted number of seconds, it may have one decimal, example: 48');
        value /= 60;
        if (value < 600)
            return prefix + _T("{0} minutes", number(value, 0), 'Text formatting for an elapsed time in minutes, example: "5 minutes". {0} is the formatted number of minutes, example: 5');
        value /= 60;
        if (value < (3 * 24))
            return prefix + _T("{0} hours", number(value, 0), 'Text formatting for an elapsed time in hours, example: "9 hours". {0} is the formatted number of hours, example: 9');
        value /= 24;
        if (value < (3 * 365))
            return prefix + _T("{0} days", number(value, 0), 'Text formatting for an elapsed time in days, example: "5 days". {0} is the formatted number of days, example: 5');
        value /= 365.242374;
        return prefix + _T("{0} years", number(value, 0), 'Text formatting for an elapsed time in years, example: "7 years". {0} is the formatted number of years, example: 7');
    }

    /**
     * Format a .NET TimeSpan ("[-][d.]hh:mm:ss[.fffffff]") or a number of seconds as one translated unit:
     * years (>= 3*365 days), days (>= 3), hours (>= 10), minutes (>= 10), seconds (>= 10, 1 decimal), ms (>= 10), µs. A number of whole
     * seconds is shown without the decimal ("1 second", "30 seconds"), and over 1 500 000 000 s as "∞" (the server's "forever").
     * @param {string|number} value - TimeSpan text or seconds.
     * @param {string} [zeroAsThis] - Text for a zero duration (default "0").
     * @returns {string} The text ("-" for an empty value).
     */
    function timeSpan(value, zeroAsThis) {
        value = "" + value;
        if (value.length == 0)
            return "-";
        const isNeg = value[0] == '-';
        const prefix = isNeg ? "- " : "";
        if (isNeg)
            value = value.substring(1);
        let parts = value.split(':');
        //  (a number of seconds: as "d.hh:mm:ss", so long ones are days / years; whole seconds without a decimal: "30 seconds"; over
        //  1 500 000 000 s (47 years: the server's "forever", ex: the static request cache of 50 years) "∞", as the explore pages)
        let whole = false;
        if (parts.length === 1) {
            const vp = parseFloat(value);
            if (vp > 1500000000)
                return prefix + "∞";
            const h = Math.floor(vp / 3600);
            parts = [Math.floor(h / 24) + "." + (h % 24), "" + (Math.floor(vp / 60) % 60), "" + (vp % 60)];
            whole = Number.isInteger(vp);
        }
        const dh = parts[0].split('.');
        const dhl = dh.length;
        const days = dhl > 1 ? parseInt(dh[0]) : 0;
        if (days >= (3 * 365))
            return prefix + _T("{0} years", number(days / 365.242374, 0), 'Text formatting for an elapsed time in years, example: "7 years". {0} is the formatted number of years, example: 7');
        if (days >= 3)
            return prefix + _T("{0} days", number(days, 0), 'Text formatting for an elapsed time in days, example: "5 days". {0} is the formatted number of days, example: 5');
        const hours = parseInt(dh[dhl - 1]) + (days * 24);
        if (hours >= 10)
            return prefix + _T("{0} hours", number(hours, 0), 'Text formatting for an elapsed time in hours, example: "9 hours". {0} is the formatted number of hours, example: 9');
        const minutes = parseInt(parts[1]) + (hours * 60);
        if (minutes >= 10)
            return prefix + _T("{0} minutes", number(minutes, 0), 'Text formatting for an elapsed time in minutes, example: "5 minutes". {0} is the formatted number of minutes, example: 5');
        const seconds = parseFloat(parts[2]) + (minutes * 60);
        if (seconds == 0)
            return zeroAsThis ? zeroAsThis : "0";
        if (whole && seconds === 1)
            return prefix + _TF("1 second", 'Text formatting for an elapsed time of exactly one second');
        if (seconds >= 10 || whole)
            return prefix + _T("{0} seconds", number(seconds, whole ? 0 : 1), 'Text formatting for an elapsed time in seconds, example: "48 seconds". {0} is the formatted number of seconds, it may have one decimal, example: 48');
        const ms = seconds * 1000;
        if (ms >= 10)
            return prefix + _T("{0} ms", number(ms, 1), 'Text formatting for an elapsed time in milliseconds, example: "13 ms". {0} is the formatted number of milliseconds, example: 13');
        return prefix + _T("{0} µs", number(ms * 1000, 1), 'Text formatting for an elapsed time in microseconds, example: "27 µs". {0} is the formatted number of microseconds, example: 27');
    }

    /**
     * Shift a Date by the local timezone offset (so that its local fields show the UTC time).
     * @param {Date} date - Input.
     * @returns {Date} New shifted Date.
     */
    function localToUtc(date) {
        //  copy the fields instead of adding the offset and then calling setHours on the shifted
        //  day (that lost a day whenever the UTC and local dates differ, and broke on DST and :30 / :45 zones)
        const n = new Date(0);
        n.setFullYear(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
        n.setHours(date.getUTCHours(), date.getUTCMinutes(), date.getUTCSeconds(), date.getUTCMilliseconds());
        return n;
    }

    /**
     * Shift a Date by minus the local timezone offset (so that its UTC fields show the local time).
     * Exact for every zone, across DST and for offsets that are not whole hours (Asia/Kolkata +5:30, Asia/Kathmandu +5:45).
     * @param {Date} date - Input.
     * @returns {Date} New shifted Date.
     */
    function utcToLocal(date) {
        //  the UTC fields of the result are the local fields of the input (no setHours on a shifted day)
        const n = new Date(0);
        n.setUTCFullYear(date.getFullYear(), date.getMonth(), date.getDate());
        n.setUTCHours(date.getHours(), date.getMinutes(), date.getSeconds(), date.getMilliseconds());
        return n;
    }

    /** "yyyy-MM-dd" (a C# DateOnly): a calendar day, not an instant. */
    const dateOnlyRx = /^\d{4}-\d{2}-\d{2}$/;

    /** An ISO 8601 date time without a zone designator (a C# DateTime of kind Unspecified, written by the "o" format). */
    const noZoneRx = /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/;

    /**
     * Parse a time stamp value the way the server means it.
     * - An ISO date time without a zone is UTC (the table data convention, the same as the C# exporters'
     *   AssumeUniversal); new Date() would read it as browser local time.
     * - A date-only text ("2026-10-20") is the local midnight of that day; new Date() would read it as UTC midnight,
     *   which is the previous day west of UTC.
     * - Anything else (Z or +hh:mm suffix, a number, a Date) as new Date().
     * @param {string|number|Date} v - Value.
     * @returns {Date} The Date (may be invalid: check isNaN(d.getTime())).
     */
    function parseDate(v) {
        if (v instanceof Date)
            return v;
        if (typeof v === "string") {
            const s = v.trim();
            if (dateOnlyRx.test(s)) {
                const d = new Date(0);
                d.setFullYear(+s.substring(0, 4), +s.substring(5, 7) - 1, +s.substring(8, 10));
                d.setHours(0, 0, 0, 0);
                return d;
            }
            if (noZoneRx.test(s))
                return new Date(s.replace(" ", "T") + "Z");
            return new Date(s);
        }
        return new Date(v);
    }

    // ------------------------------------------------------------------ the format locale (dates and times as the user's region writes them)

    /** localStorage key of the user's choice (a locale, ex: "sv-SE"; missing or "" = automatic). */
    const localeKey = "SysWeaver.FormatLocale";
    /** localStorage key of the detected locale: "time zone|locale" (detected again when the time zone changes). */
    const localeAutoKey = "SysWeaver.FormatLocaleAuto";

    /** @returns {string} The viewer's time zone, ex: "Europe/Stockholm" ("" if not known). */
    function tzName() {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone || "";
        }
        catch {
            return "";
        }
    }

    /** @param {?string} l A locale. @returns {boolean} True when the browser can format with it. */
    function validLocale(l) {
        try {
            return !!l && Intl.DateTimeFormat.supportedLocalesOf([l]).length > 0;
        }
        catch {
            return false;
        }
    }

    /** @returns {?string} The detected locale for the current time zone (null when not detected yet). */
    function autoLocale() {
        const a = String(Sw.Storage ? Sw.Storage.Get(localeAutoKey, "") || "" : "").split("|");
        return a.length === 2 && a[0] === tzName() && validLocale(a[1]) ? a[1] : null;
    }

    /** @returns {string|undefined} The locale to format with: the user's choice, else the detected one, else undefined (the browser's). */
    function readLocale() {
        const c = Sw.Storage ? Sw.Storage.Get(localeKey, "") : "";
        return validLocale(c) ? c : (autoLocale() || undefined);
    }

    let fmtLocale = readLocale();

    /** Use the changed choice / detection (another page of the site changed it, or this one). */
    function localeChanged() {
        const l = readLocale();
        if (l === fmtLocale)
            return;
        fmtLocale = l;
        if (Sw.Events)
            Sw.Events.Emit("format.locale", { Locale: l });
    }

    window.addEventListener("storage", e => {
        if (e.key === localeKey || e.key === localeAutoKey)
            localeChanged();
    });

    //  Not detected for this time zone yet: detect it (core/region.js, loaded only then) and remember it. The page formats with the
    //  browser's locale until it's done (only the first page of a browser, or after a time zone change).
    if (!autoLocale())
        setTimeout(() => {
            if (!Sw.Net || !Sw.Url || autoLocale())
                return;
            Sw.Net.IncludeJs(null, Sw.Url.Root + "core/region.js").then(() => {
                const l = Sw.FormatRegion && Sw.FormatRegion.Detect();
                if (validLocale(l) && Sw.Storage) {
                    Sw.Storage.Set(localeAutoKey, tzName() + "|" + l);
                    localeChanged();
                }
            }).catch(e => console.warn("Sw.Format: the format locale could not be detected: " + e));
        }, 0);

    /**
     * A date (and time) as the format locale writes it with numbers, ex: "2018-01-30 07:30:40" (sv-SE), "01/30/2018, 07:30:40 AM" (en-US).
     * @param {Date} d - The date, its UTC fields are the wall clock to show.
     * @param {boolean} [onlyDate] - Only the date.
     * @returns {string} The text.
     */
    function localText(d, onlyDate) {
        const o = { timeZone: "UTC", year: "numeric", month: "2-digit", day: "2-digit" };
        if (!onlyDate) {
            o.hour = "2-digit";
            o.minute = "2-digit";
            o.second = "2-digit";
        }
        try {
            return new Intl.DateTimeFormat(fmtLocale, o).format(d);
        }
        catch {
            return isoText(d, onlyDate);
        }
    }

    // ------------------------------------------------------------------ date / time fields typed by the user (Sw.Edit, Sw.DatePicker)

    /**
     * A Date whose UTC fields are a calendar day / wall clock (years 1 - 9999, no 1900 shift for years below 100).
     * @param {number} y Year.
     * @param {number} m Month (1-12; overflows into the next / previous year).
     * @param {number} d Day (overflows into the next / previous month).
     * @param {number} [h] Hours.
     * @param {number} [mi] Minutes.
     * @param {number} [s] Seconds.
     * @returns {Date} The Date.
     */
    function utcFields(y, m, d, h, mi, s) {
        const t = new Date(0);
        t.setUTCFullYear(y, m - 1, d);
        t.setUTCHours(h || 0, mi || 0, s || 0, 0);
        return t;
    }

    /**
     * The order of the date fields of the format locale and a hint of its form: { Order: "ymd", Hint: "yyyy-mm-dd" } (sv-SE),
     * { Order: "mdy", Hint: "mm/dd/yyyy" } (en-US), { Order: "dmy", Hint: "dd/mm/yyyy" } (en-GB).
     * @returns {{Order: string, Hint: string}} The pattern.
     */
    function datePattern() {
        try {
            const parts = new Intl.DateTimeFormat(fmtLocale, { timeZone: "UTC", year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(utcFields(2033, 11, 22));
            let order = "", hint = "";
            for (const p of parts) {
                if (p.type === "year") {
                    order += "y";
                    hint += "yyyy";
                }
                else if (p.type === "month") {
                    order += "m";
                    hint += "mm";
                }
                else if (p.type === "day") {
                    order += "d";
                    hint += "dd";
                }
                else if (p.type === "literal")
                    hint += p.value;
            }
            if (order.length === 3)
                return { Order: order, Hint: hint };
        }
        catch {
        }
        return { Order: "ymd", Hint: "yyyy-mm-dd" };
    }

    /**
     * Read a date typed by the user: three numbers in the order of the format locale ("2026-01-30", "1/30/2026", "30.1.26"), a
     * first number of 3 or 4 digits is the year (ISO order in every locale), a 2 digit year is in 2000 - 2099.
     * @param {string} text The text.
     * @returns {?{Y: number, M: number, D: number}|undefined} The day, null for an empty text, undefined when it is no valid date.
     */
    function parseLocalDate(text) {
        const s = String(text ?? "").trim();
        if (!s)
            return null;
        const g = s.match(/\d+/g);
        if (!g || g.length !== 3)
            return undefined;
        const order = g[0].length >= 3 ? "ymd" : datePattern().Order;
        const v = {};
        for (let i = 0; i < 3; ++i)
            v[order[i]] = +g[i];
        if (g[order.indexOf("y")].length <= 2)
            v.y += 2000;
        if (v.y < 1 || v.y > 9999 || v.m < 1 || v.m > 12 || v.d < 1)
            return undefined;
        const t = utcFields(v.y, v.m, v.d);
        if (t.getUTCMonth() !== v.m - 1 || t.getUTCDate() !== v.d)
            return undefined;
        return { Y: v.y, M: v.m, D: v.d };
    }

    /**
     * The day period of a typed time in the format locale ("PM", "em", "p.m."), also "am" / "pm" / "a" / "p" in every locale.
     * @param {string} s The text.
     * @returns {?string} "am", "pm" or null.
     */
    function dayPeriod(s) {
        const norm = x => String(x).toLowerCase().replace(/[\s.  ]/g, "");
        const letters = norm(s.replace(/[\d:.,]/g, " "));
        if (!letters)
            return null;
        try {
            const f = new Intl.DateTimeFormat(fmtLocale, { timeZone: "UTC", hour: "numeric", hour12: true });
            const am = f.formatToParts(utcFields(2000, 1, 1, 1)).find(p => p.type === "dayPeriod");
            const pm = f.formatToParts(utcFields(2000, 1, 1, 13)).find(p => p.type === "dayPeriod");
            if (am && letters === norm(am.value))
                return "am";
            if (pm && letters === norm(pm.value))
                return "pm";
        }
        catch {
        }
        if (letters === "am" || letters === "a")
            return "am";
        if (letters === "pm" || letters === "p")
            return "pm";
        return undefined;
    }

    /**
     * Read a time of day typed by the user: "14:05", "14.05.09", "1405", "2:05 PM" (the day period of the format locale or am / pm).
     * @param {string} text The text.
     * @returns {?{H: number, M: number, S: number}|undefined} The time, null for an empty text, undefined when it is no valid time.
     */
    function parseLocalTime(text) {
        const s = String(text ?? "").trim();
        if (!s)
            return null;
        const g = s.match(/\d+/g);
        if (!g || g.length > 3)
            return undefined;
        let h, m, sec;
        if (g.length === 1 && g[0].length > 2) {
            const x = g[0];
            if (x.length !== 4 && x.length !== 6)
                return undefined;
            h = +x.substring(0, 2);
            m = +x.substring(2, 4);
            sec = x.length === 6 ? +x.substring(4, 6) : 0;
        }
        else {
            h = +g[0];
            m = +(g[1] || 0);
            sec = +(g[2] || 0);
        }
        const p = dayPeriod(s);
        if (p === undefined)
            return undefined;
        if (p) {
            if (h < 1 || h > 12)
                return undefined;
            h = h % 12 + (p === "pm" ? 12 : 0);
        }
        if (h > 23 || m > 59 || sec > 59)
            return undefined;
        return { H: h, M: m, S: sec };
    }

    /**
     * A hint of the time form of the format locale: "hh:mm:ss" (sv-SE), "hh:mm:ss AM" (en-US).
     * @param {boolean} [seconds] With seconds (default true).
     * @returns {string} The hint.
     */
    function timePattern(seconds) {
        try {
            const o = { timeZone: "UTC", hour: "2-digit", minute: "2-digit" };
            if (seconds !== false)
                o.second = "2-digit";
            let hint = "";
            for (const p of new Intl.DateTimeFormat(fmtLocale, o).formatToParts(utcFields(2000, 1, 1, 13, 14, 15)))
                hint += p.type === "hour" ? "hh" : p.type === "minute" ? "mm" : p.type === "second" ? "ss" : p.type === "dayPeriod" ? p.value : p.value;
            return hint;
        }
        catch {
            return seconds !== false ? "hh:mm:ss" : "hh:mm";
        }
    }

    /**
     * Format a Date as "yyyy-MM-dd HH:mm:ss" from its UTC fields.
     * @param {Date} d - Date.
     * @param {boolean} [onlyDate] - Only "yyyy-MM-dd".
     * @returns {string}
     */
    function isoText(d, onlyDate) {
        const c = d.toISOString().split('T');
        const r = c[0] + " " + c[1].split('.')[0];
        return onlyDate ? r.substring(0, r.length - 9) : r;
    }

    /**
     * Get the display text and tooltip of a time stamp (same as the old getTimeStampTitle).
     * Default (no fmt): the value is treated as UTC and shown in local time.
     * @param {string|number|Date} dd - The date.
     * @param {boolean} [onlyDate] - Only show the date part.
     * @param {string} [prefix] - Tooltip prefix (default translated "At: ").
     * @param {string} [tab] - Tooltip row prefix.
     * @param {string} [fmt] - "utc" or "local".
     * @returns {string[]} [0] tooltip, [1] text.
     */
    function timeStamp(dd, onlyDate, prefix, tab, fmt) {
        if (!prefix)
            prefix = _TF("At", 'Tool tip row prefix to some time stamp, ex: "At: 1997-04-12 15:32:30"') + ": ";
        //  a date-only value is a calendar day in every zone and every mode (no shift), written as the format locale writes dates
        if ((typeof dd === "string") && dateOnlyRx.test(dd.trim())) {
            const day = new Date(dd.trim() + "T00:00:00Z");
            const text = isNaN(day.getTime()) ? dd.trim() : localText(day, true);
            return [prefix + text + "\n" + _TF("ISO", "Tool tip row prefix to some time stamp written using js Date.toISOString() syntax") + ": " + dd.trim(), text];
        }
        const raw = dd;
        if ((typeof dd === "string") || (typeof dd === "number"))
            dd = parseDate(dd);
        //  An unparsable value is shown as is (toISOString would throw)
        if (!(dd instanceof Date) || isNaN(dd.getTime()))
            return ["" + raw, "" + raw];
        const utc = dd;
        //  The "Local" tooltip row is always the real local time (it used to show the UTC time in the "utc" mode)
        const loc = dd;
        let isLocal = false;
        if (fmt === "utc") {
            //  Shown as the UTC time (isoText reads the UTC fields)
        } else {
            isLocal = fmt === "local";
            if (!isLocal)
                dd = utcToLocal(dd);
        }
        if (!tab)
            tab = "";
        if (!prefix)
            prefix = _TF("At", 'Tool tip row prefix to some time stamp, ex: "At: 1997-04-12 15:32:30"') + ": ";
        tab = "\n" + tab;
        //  As the format locale writes it (the user's region, see Locale); the ISO text is in the tool tip
        const c = localText(dd, onlyDate);
        let title = prefix + c;
        //  The time rule (core/README "Times"): the tool tip says which clock the text is ("At: ... (WEST, UTC+1)")
        if (!isLocal && !onlyDate)
            title += " (" + (fmt === "utc" ? "UTC" : zoneText(utc)) + ")";
        if (!isLocal) {
            title += tab +
                _TF("Local", "Tool tip row prefix to some time stamp written using js Date.toLocaleString() syntax") + ": " + loc.toLocaleString(fmtLocale) + tab +
                _TF("Full", "Tool tip row prefix to some time stamp written using js Date.toString() syntax") + ": " + utc.toString() + tab +
                _TF("UTC", "Tool tip row prefix to some time stamp written using js Date.toUTCString() syntax") + ": " + utc.toUTCString() + tab +
                _TF("ISO", "Tool tip row prefix to some time stamp written using js Date.toISOString() syntax") + ": " + utc.toISOString();
        }
        return [title, c];
    }

    // ------------------------------------------------------------------ time zones (core/README "Times")

    /**
     * An UTC offset as text: "UTC", "UTC+2", "UTC-5", "UTC+5:30", "UTC-3:30".
     * @param {number} minutes Minutes east of UTC (Lisbon summer: 60; New York winter: -300).
     * @returns {string} The text ("" for a non number).
     */
    function offsetText(minutes) {
        if (typeof minutes !== "number" || !isFinite(minutes))
            return "";
        if (minutes === 0)
            return "UTC";
        const a = Math.abs(Math.round(minutes));
        const h = Math.floor(a / 60);
        const m = a % 60;
        return "UTC" + (minutes < 0 ? "-" : "+") + h + (m ? ":" + String(m).padStart(2, "0") : "");
    }

    /**
     * The viewer's time zone at an instant: its short name (when the browser has a real one, ex: "WEST", "EDT", "CET") and the UTC
     * offset: "WEST, UTC+1"; "UTC+5:30" where the short name is only an offset ("GMT+5:30").
     * @param {Date|number|string} [at] The instant (default now; the offset depends on DST).
     * @returns {string} The zone text.
     */
    function zoneText(at) {
        const d = at === undefined ? new Date() : parseDate(at);
        const t = isNaN(d.getTime()) ? new Date() : d;
        const off = offsetText(-t.getTimezoneOffset());
        let name = "";
        try {
            const p = new Intl.DateTimeFormat(fmtLocale, { timeZoneName: "short" }).formatToParts(t).find(x => x.type === "timeZoneName");
            name = p ? p.value : "";
        }
        catch {
            name = "";
        }
        return name && !/^(GMT|UTC)([+-]|$)/.test(name) ? name + ", " + off : off;
    }

    /** The session storage key of the server's UTC offset (minutes; shared by the shell and its framed pages). */
    const serverZoneKey = "SysWeaver.ServerZone";

    /** @type {number|undefined} The server's UTC offset in minutes east of UTC, undefined while unknown. */
    let serverZone;

    /**
     * The server's UTC offset (minutes east of UTC), or undefined while unknown. Known from the serverTime reply ("ms;offset", a server
     * that sends it), or from a page that knows it (SetServerZone, ex: ServerManager from GetServerInfo); shared through session storage.
     * @returns {number|undefined} The offset.
     */
    function getServerZone() {
        if (typeof serverZone === "number")
            return serverZone;
        try {
            const v = window.sessionStorage.getItem(serverZoneKey);
            if (v !== null && v !== "" && isFinite(+v))
                return +v;
        }
        catch {
        }
        return undefined;
    }

    /**
     * Set the server's UTC offset.
     * @param {number|null|undefined} minutes Minutes east of UTC (rounded to a quarter of an hour), null / undefined = unknown.
     */
    function setServerZone(minutes) {
        const ok = typeof minutes === "number" && isFinite(minutes);
        serverZone = ok ? Math.round(minutes / 15) * 15 : undefined;
        try {
            if (ok)
                window.sessionStorage.setItem(serverZoneKey, String(serverZone));
            else
                window.sessionStorage.removeItem(serverZoneKey);
        }
        catch {
        }
    }

    /** A server wall clock text: "yyyy-MM-dd HH:mm[:ss[.fff]]" or "yyyy-MM-ddTHH:mm..." or only "HH:mm[:ss[.fff]]". */
    const wallRx = /^(?:(\d{4})-(\d{2})-(\d{2})[T ])?(\d{1,2}):(\d{2})(?::(\d{2})(?:[.,](\d{1,7}))?)?$/;

    /**
     * Read a server wall clock time (a text in the server's local time without a zone: a log file line, GetServerInfo.Time) as an
     * instant. A time without a date is the latest such time at or before `near` on the server's clock (log lines of today;
     * just after the server's midnight a "23:59:58" is yesterday).
     * @param {string} text The wall clock text.
     * @param {number} [zone] The server's offset in minutes (default ServerZone()).
     * @param {Date|number} [near] For a time without a date: the instant it is close to (default the server time now).
     * @returns {Date|null} The instant, or null when the text is not a wall clock time or the server's zone is unknown.
     */
    function serverToDate(text, zone, near) {
        const z = typeof zone === "number" ? zone : getServerZone();
        const m = wallRx.exec(String(text ?? "").trim());
        if (!m || typeof z !== "number")
            return null;
        const ms = m[7] ? +(m[7] + "00").substring(0, 3) : 0;
        if (m[1])
            return new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +(m[6] || 0), ms) - z * 60000);
        const nowMs = near !== undefined ? +near : ((Sw.Session && Sw.Session.ServerTick) ? Sw.Session.ServerTick() : Date.now());
        //  The server's calendar day at `near`, then that day's time; one day back if that is in the future (more than a minute)
        const wall = new Date(nowMs + z * 60000);
        let t = Date.UTC(wall.getUTCFullYear(), wall.getUTCMonth(), wall.getUTCDate(), +m[4], +m[5], +(m[6] || 0), ms) - z * 60000;
        if (t > nowMs + 60000)
            t -= 86400000;
        return new Date(t);
    }

    /**
     * Format an instant on the server's clock: "yyyy-MM-dd HH:mm:ss" (or only "HH:mm" / "HH:mm:ss" with the part option).
     * @param {Date|number|string} at The instant.
     * @param {"datetime"|"time"|"hm"} [part="datetime"] What to show.
     * @param {number} [zone] The offset (default ServerZone()).
     * @returns {string} The text ("" when the zone is unknown or the value is not a date).
     */
    function serverText(at, part, zone) {
        const z = typeof zone === "number" ? zone : getServerZone();
        const d = parseDate(at);
        if (typeof z !== "number" || isNaN(d.getTime()))
            return "";
        const s = isoText(new Date(d.getTime() + z * 60000));
        return part === "time" ? s.substring(11) : part === "hm" ? s.substring(11, 16) : s;
    }

    /**
     * Escape a value for a (quoted) HTML/XML attribute: & ' " < > become entities, line breaks become "&#13;"
     * (preserveCR) or "\n".
     * @param {*} s - Value.
     * @param {boolean} [preserveCR] - Encode line breaks as "&#13;".
     * @returns {string}
     */
    function htmlAttributeSafe(s, preserveCR) {
        const cr = preserveCR ? '&#13;' : '\n';
        return ('' + s)
            .replace(/&/g, '&amp;')
            .replace(/'/g, '&apos;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/\r\n/g, cr)
            .replace(/[\r\n]/g, cr);
    }

    /** @type {HTMLElement|null} Detached element used by htmlSafe. */
    let tempEl = null;

    /**
     * Escape text for use as HTML content (line breaks become br), same result as assigning innerText.
     * @param {*} text - Plain text.
     * @returns {string} HTML.
     */
    function htmlSafe(text) {
        if (!tempEl)
            tempEl = document.createElement("SysWeaver-Temp");
        tempEl.innerText = "" + text;
        return tempEl.innerHTML;
    }

    /**
     * Syntax highlight json text: HTML-escape it and wrap tokens in span class key|string|number|boolean|null.
     * @param {string} jsonText - (Pretty printed) json.
     * @returns {string} HTML.
     */
    function jsonToHtml(jsonText) {
        jsonText = jsonText.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        return jsonText.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
            let cls = 'number';
            if (/^"/.test(match))
                cls = /:$/.test(match) ? 'key' : 'string';
            else if (/true|false/.test(match))
                cls = 'boolean';
            else if (/null/.test(match))
                cls = 'null';
            return '<span class="' + cls + '">' + match + '</span>';
        });
    }

    /** Lower-case extension tables (value 1 = member). Image, Video and Audio: keep in sync with MediaExtensions in MediaService.cs. */
    const Ext = Object.freeze({
        /** Image extensions. */
        Image: Object.freeze({ png: 1, gif: 1, jpg: 1, jpeg: 1, avif: 1, webp: 1, tiff: 1, tif: 1, svg: 1, jfif: 1, jpe: 1, apng: 1, bmp: 1, ico: 1 }),
        /** Video extensions. */
        Video: Object.freeze({ webm: 1, mp4: 1, m4v: 1, ogg: 1, ogv: 1, mov: 1, mkv: 1 }),
        /** Audio extensions. */
        Audio: Object.freeze({ mp3: 1, wav: 1, aac: 1, flac: 1, m4a: 1, m4b: 1, opus: 1, oga: 1, weba: 1 }),
        /** Source code / markup extensions. */
        Programming: Object.freeze({ js: 1, json: 1, css: 1, html: 1, xml: 1, cs: 1, cpp: 1, c: 1, h: 1, vbs: 1, bat: 1, vb: 1 }),
        /** Other well compressible (text) extensions. */
        Compress: Object.freeze({ txt: 1, log: 1, cfg: 1, svg: 1 }),
    });

    /**
     * Get the extension of a path (text after the last '.'), case preserved.
     * @param {string} path - Path or file name.
     * @returns {string} Extension without dot, "" if none.
     */
    function fileExtension(path) {
        const i = path.lastIndexOf('.');
        return i < 0 ? "" : path.substring(i + 1);
    }

    /** @type {Readonly<Object<string, boolean>>} C# types that are non-integer numbers. */
    const decimalTypes = Object.freeze({ "System.Single": true, "System.Double": true, "System.Decimal": true });

    /** @type {Readonly<Object<string, string>>} C# type -> default simple format kind. */
    const typeKinds = Object.freeze({
        "System.SByte": "Number", "System.Int16": "Number", "System.Int32": "Number", "System.Int64": "Number",
        "System.Byte": "Number", "System.UInt16": "Number", "System.UInt32": "Number", "System.UInt64": "Number",
        "System.Single": "Number", "System.Double": "Number", "System.Decimal": "Number",
        "System.DateTime": "DateTime", "System.TimeSpan": "Duration", "System.Boolean": "Boolean",
    });

    /**
     * @namespace Sw.Format - pure value formatting.
     */
    Sw.Format = {

        /** Currency formatting flags (see CurrencyOptions). */
        CurrencyOptions: CurrencyOptions,

        /** Number with thousands separator and decimals (value, decimals, thousandSep, decimalSep). */
        Number: number,

        /**
         * Format an amount for a currency (an IsoCurrency record from iso_data/currencies.js).
         * @param {{DecimalSeparator:string, ThousandSeparator:string, DecimalNumbers:number, UnderUnits:number, Iso4217:string, SymbolPrefix:string, SymbolSuffix:string}} currencyInfo - Currency.
         * @param {number} amount - The value.
         * @param {number} [options=CurrencyOptions.Default] - Flags.
         * @param {string} [decimalSeparatorOverride] - Forced decimal separator.
         * @param {string} [thousandSeparatorOverride] - Forced thousands separator.
         * @returns {string}
         */
        Amount(currencyInfo, amount, options, decimalSeparatorOverride, thousandSeparatorOverride) {
            options = typeof options !== "number" ? CurrencyOptions.Default : options;
            const decimalSeparator = decimalSeparatorOverride ?? currencyInfo.DecimalSeparator;
            let value = null;
            const underUnits = currencyInfo.UnderUnits;
            const thousands = (options & CurrencyOptions.ApplyThousandsSeparator) !== 0;
            if ((underUnits > 1) && ((options & CurrencyOptions.ForceRounding) === 0)) {
                let f = Math.round(Math.abs(amount) * underUnits);
                const sign = Math.sign(amount);
                let a = Math.trunc(f / underUnits);
                f %= underUnits;
                if ((f !== 0) || ((options & CurrencyOptions.AutomaticRounding) === 0)) {
                    const fs = ("" + (f | 0)).padEnd(currencyInfo.DecimalNumbers, '0');
                    a *= sign;
                    value = thousands
                        ? (number(a, 0, thousandSeparatorOverride) + decimalSeparator + fs)
                        : ("" + (a | 0) + decimalSeparator + fs);
                }
            }
            if (!value) {
                amount = Math.round(amount);
                value = thousands ? number(amount, 0, thousandSeparatorOverride) : ("" + (amount | 0));
            }
            const style = options & CurrencyOptions.Symbol;
            if (style === CurrencyOptions.IsoPrefix)
                value = currencyInfo.Iso4217 + ' ' + value;
            if (style === CurrencyOptions.IsoSuffix)
                value = value + ' ' + currencyInfo.Iso4217;
            if (style === CurrencyOptions.Symbol)
                value = currencyInfo.SymbolPrefix + value + currencyInfo.SymbolSuffix;
            return value;
        },

        /** k/M/G/T/E unit formatting (value, suffix, emptySuffix, k, decimals, prefixes). */
        Suffix: suffix,

        /**
         * Format a byte count as the original ValueFormat.formatByteSize, e.g. "1 000 bytes", "12.3 KB", "5.0 MB", "63.4 GB" (k = 1024).
         * @param {number} value - Bytes.
         * @param {number} [decimals=1] - Decimals of KB and larger values (e.g. 2 to match a server text like "63.35 GB").
         * @returns {string}
         */
        Bytes: (value, decimals) => suffix(value, "B", "bytes", 1024, decimals, bytePrefixes),

        /**
         * Format a byte speed, e.g. "12.3 KB/s".
         * @param {number} value - Bytes per second.
         * @param {number} [decimals=1] - Decimals of KB/s and larger values.
         * @returns {string}
         */
        ByteSpeed: (value, decimals) => suffix(value, "B/s", "bytes/s", 1024, decimals, bytePrefixes),

        /** Elapsed ms as text (value, blankIfNeg). */
        DurationMs: durationMs,

        /** .NET TimeSpan text or seconds as text (value, zeroAsThis). */
        TimeSpan: timeSpan,

        /**
         * Time elapsed since (or, with invert, until) a date as text (the "Uptime" cell text).
         * "0001-01-01T00:00:00" gives "-", a falsy value gives the translated "Now".
         * @param {string|number|Date} value - The date.
         * @param {boolean|string} [blankIfNeg] - See DurationMs.
         * @param {boolean} [invert] - Time until the date.
         * @param {number} [now=Date.now()] - Reference time (ms).
         * @returns {string}
         */
        Uptime(value, blankIfNeg, invert, now) {
            if (value === "0001-01-01T00:00:00")
                return "-";
            if (!value)
                return _TF("Now", "Text that indicate that an event happened right now");
            const dd = parseDate(value);
            return durationMs(((now ?? Date.now()) - dd.getTime()) * (invert ? -1 : 1), blankIfNeg);
        },

        /** Time stamp [tooltip, text] (date, onlyDate, prefix, tab, fmt): the text as the format locale writes it. */
        TimeStamp: timeStamp,

        /**
         * The locale used for dates and times (pass it to toLocaleString / Intl.DateTimeFormat): the user's choice (SetLocale), else the
         * locale detected for the time zone (core/region.js: the region's formats, as the operating system has them), else undefined (the
         * browser's language).
         * @returns {string|undefined} The locale.
         */
        Locale: () => fmtLocale,

        /** @returns {string} The user's choice of the format locale, "" = automatic (see Locale). */
        LocaleChoice: () => {
            const c = Sw.Storage ? Sw.Storage.Get(localeKey, "") : "";
            return validLocale(c) ? c : "";
        },

        /** @returns {?string} The locale detected for the time zone (null when not detected yet). */
        AutoLocale: autoLocale,

        /**
         * Choose the locale of dates and times (localStorage "SysWeaver.FormatLocale": every page of the site in this browser; the open
         * pages get the event "format.locale", pages already showing dates should reload).
         * @param {?string} l The locale, ex: "sv-SE"; "" or null = automatic.
         */
        SetLocale(l) {
            if (Sw.Storage) {
                if (validLocale(l))
                    Sw.Storage.Set(localeKey, l);
                else
                    Sw.Storage.Remove(localeKey);
            }
            localeChanged();
        },

        /** A date (and time) with numbers as the format locale writes it (date, onlyDate; the UTC fields are shown). */
        LocalText: localText,

        /**
         * The time of day of an instant in the viewer's zone, as the format locale writes it, ex: "14:05:09" (sv-SE), "02:05:09 PM" (en-US).
         * Use this (or DateTime / DateText) instead of toLocaleTimeString(), which ignores the user's format choice.
         * @param {Date|number|string} d - The instant.
         * @param {boolean} [seconds] - Include the seconds (default true).
         * @param {boolean} [wall] - The UTC fields are the wall clock to show (ex: a server's clock), not an instant.
         * @returns {string} The text.
         */
        Time(d, seconds, wall) {
            const t = d instanceof Date ? d : parseDate(d);
            const o = { hour: "2-digit", minute: "2-digit" };
            if (seconds !== false)
                o.second = "2-digit";
            if (wall)
                o.timeZone = "UTC";
            return t.toLocaleTimeString(fmtLocale, o);
        },

        /**
         * A date and time of an instant in the viewer's zone, as the format locale writes it (toLocaleString with the format locale).
         * @param {Date|number|string} d - The instant.
         * @param {object} [options] - Intl.DateTimeFormat options (default { dateStyle: "medium", timeStyle: "medium" }).
         * @returns {string} The text.
         */
        DateTime(d, options) {
            const t = d instanceof Date ? d : parseDate(d);
            return t.toLocaleString(fmtLocale, options || { dateStyle: "medium", timeStyle: "medium" });
        },

        /**
         * The date of an instant in the viewer's zone, as the format locale writes it (toLocaleDateString with the format locale).
         * @param {Date|number|string} d - The instant.
         * @param {object} [options] - Intl.DateTimeFormat options (default { dateStyle: "medium" }).
         * @returns {string} The text.
         */
        DateText(d, options) {
            const t = d instanceof Date ? d : parseDate(d);
            return t.toLocaleDateString(fmtLocale, options || { dateStyle: "medium" });
        },

        /**
         * A calendar day as the format locale writes it with numbers, for a field the user can type in ("2026-01-30", "01/30/2026").
         * @param {{Y: number, M: number, D: number}} v The day.
         * @returns {string} The text ("" for null).
         */
        DayText: v => v ? localText(utcFields(v.Y, v.M, v.D), true) : "",

        /**
         * A wall clock time as the format locale writes it, for a field the user can type in ("14:05:09", "02:05:09 PM").
         * @param {{H: number, M: number, S: number}} v The time.
         * @param {boolean} [seconds] With seconds (default true).
         * @returns {string} The text ("" for null).
         */
        ClockText(v, seconds) {
            return v ? Sw.Format.Time(utcFields(1970, 1, 1, v.H, v.M, v.S), seconds, true) : "";
        },

        /** Read a typed date (see DayText): {Y, M, D}, null for "", undefined when invalid. */
        ParseDay: parseLocalDate,

        /** Read a typed time (see ClockText): {H, M, S}, null for "", undefined when invalid. */
        ParseClock: parseLocalTime,

        /** @returns {string} The form of a typed date in the format locale ("yyyy-mm-dd", "mm/dd/yyyy"): a placeholder. */
        DayHint: () => datePattern().Hint,

        /** The form of a typed time in the format locale ("hh:mm:ss", "hh:mm:ss PM"): a placeholder (seconds, default true). */
        ClockHint: timePattern,

        /** "yyyy-MM-dd HH:mm:ss" from the UTC fields of a Date (date, onlyDate). */
        IsoText: isoText,

        /** Shift a local Date so its local fields show UTC (old convertLocalDateToUTCDate). */
        LocalToUtc: localToUtc,

        /** Shift a UTC Date so its UTC fields show local time (old convertUTCDateToLocalDate). */
        UtcToLocal: utcToLocal,

        /** Parse a time stamp the way the server means it: a zone-less ISO date time is UTC, "yyyy-MM-dd" is the local day. */
        ParseDate: parseDate,

        //  The time rule (core/README "Times"): every time in the ui is the viewer's local time; a time on another clock
        //  (the server's wall clock, UTC) is labelled with its zone. These are the helpers to label and convert.

        /** An UTC offset as text: OffsetText(120) = "UTC+2", OffsetText(330) = "UTC+5:30", OffsetText(0) = "UTC". */
        OffsetText: offsetText,

        /** The viewer's zone at an instant (default now): "WEST, UTC+1", or "UTC+5:30" when the browser has no short name. */
        ZoneText: zoneText,

        /**
         * The tool tip / hint of a time column or field in local time: "Times in your time zone (WEST, UTC+1)".
         * @param {Date|number|string} [at] The instant the offset is taken at (default now).
         * @returns {string} The hint.
         */
        ZoneHint: at => _T("Times in your time zone ({0})", zoneText(at), "Tool tip of a column or field whose times are in the time zone of the viewer.{0} is replaced with the zone, ex: WEST, UTC+1"),

        /** The server's UTC offset in minutes east of UTC (undefined while unknown). */
        ServerZone: getServerZone,

        /** Set the server's UTC offset in minutes (a page that knows it, ex: from GetServerInfo; null = unknown). */
        SetServerZone: setServerZone,

        /**
         * The server's offset from a pair of server stamps, ex: GetServerInfo { Time: "2026-10-20 02:11:11.508" (wall clock, no zone),
         * Utc: "2026-10-20T00:11:11.508Z" } = 120; rounded to a quarter of an hour.
         * @param {string} wall The server's wall clock text.
         * @param {string|Date} utc The same instant as UTC.
         * @returns {number|undefined} The offset in minutes.
         */
        ServerZoneFrom(wall, utc) {
            const w = Date.parse(String(wall || "").trim().replace(" ", "T") + "Z");
            const u = parseDate(utc).getTime();
            const m = (w - u) / 60000;
            return isFinite(m) ? Math.round(m / 15) * 15 : undefined;
        },

        /** The server's zone as text: "UTC+2" ("" while unknown). */
        ServerZoneText: () => offsetText(getServerZone()),

        /** Read a server wall clock text ("2026-10-20 02:15:19", or "02:15:19" = the latest such time) as an instant, or null (see serverToDate). */
        ServerToDate: serverToDate,

        /** Format an instant on the server's clock: ServerText(d) = "2026-10-20 02:15:19", ServerText(d, "hm") = "02:15" ("" while unknown). */
        ServerText: serverText,

        /**
         * A server wall clock text as the viewer's local time, for log viewers: "02:15:19" (server, UTC+2) is "01:15:19" in
         * Lisbon. Keeps the input's form (with or without the date, seconds and fraction). Returns null when the zone is unknown or
         * the text is not a wall clock time (then show it as is, labelled "server time").
         * @param {string} text The wall clock text.
         * @param {number} [zone] The server's offset (default ServerZone()).
         * @returns {string|null} The local text.
         */
        ServerToLocalText(text, zone) {
            const s = String(text ?? "").trim();
            const m = wallRx.exec(s);
            const d = serverToDate(s, zone);
            if (!m || !d)
                return null;
            const l = isoText(utcToLocal(d));
            let r = m[1] ? l : l.substring(11);
            if (!m[6])
                r = r.substring(0, r.length - 3);
            else if (m[7])
                r += s.substring(s.length - m[7].length - 1);
            return r;
        },

        /**
         * Format a template with variable arguments (same as Sw.I18n.Format).
         * @param {string} format - Template.
         * @param {...*} args - Arguments.
         * @returns {string|undefined}
         */
        StringFormat: (format, ...args) => Sw.I18n.FormatArgs(format, args),

        /**
         * Format a template with an argument array (same as Sw.I18n.FormatArgs).
         * @param {string} format - Template.
         * @param {Array<*>} args - Arguments.
         * @returns {string|undefined}
         */
        StringFormatArgs: (format, args) => Sw.I18n.FormatArgs(format, args),

        /**
         * Spell out a count 0-9 as a translated word plus suffix, other counts as digits + pluralSuffix.
         * @param {number} count - The count.
         * @param {string} singularSuffix - Suffix for 1.
         * @param {string} pluralSuffix - Suffix for other counts.
         * @returns {string}
         */
        CountSuffix(count, singularSuffix, pluralSuffix) {
            switch (count) {
                case 0: return _TF("zero", 'The prefix part of some text, such as "zero bananas"') + pluralSuffix;
                case 1: return _TF("one", 'The prefix part of some text, such as "one banana"') + singularSuffix;
                case 2: return _TF("two", 'The prefix part of some text, such as "two bananas"') + pluralSuffix;
                case 3: return _TF("three", 'The prefix part of some text, such as "three bananas"') + pluralSuffix;
                case 4: return _TF("four", 'The prefix part of some text, such as "four bananas"') + pluralSuffix;
                case 5: return _TF("five", 'The prefix part of some text, such as "five bananas"') + pluralSuffix;
                case 6: return _TF("six", 'The prefix part of some text, such as "six bananas"') + pluralSuffix;
                case 7: return _TF("seven", 'The prefix part of some text, such as "seven bananas"') + pluralSuffix;
                case 8: return _TF("eight", 'The prefix part of some text, such as "eight bananas"') + pluralSuffix;
                case 9: return _TF("nine", 'The prefix part of some text, such as "nine bananas"') + pluralSuffix;
            }
            return "" + count + pluralSuffix;
        },

        /**
         * Split a CamelCase identifier into words: "MaxValueCount" => "Max value count".
         * @param {string} str - Identifier.
         * @param {string} [space=" "] - Separator.
         * @returns {string}
         */
        RemoveCamelCase(str, space) {
            if (!space)
                space = ' ';
            let sb = "";
            let prevIsUpper = true;
            const sl = str.length;
            for (let i = 0; i < sl; ++i) {
                const c = str.charAt(i);
                const isUpper = (c === c.toUpperCase());
                if (isUpper && (!prevIsUpper)) {
                    sb += space + c.toLowerCase();
                    prevIsUpper = true;
                    continue;
                }
                sb += c;
                prevIsUpper = isUpper;
            }
            return sb;
        },

        /**
         * Split text at the first separator into a trimmed [key, value] pair ([text, ""] if missing).
         * @param {string} text - Text.
         * @param {string} split - Separator.
         * @returns {string[]}
         */
        KeyValueSplit(text, split) {
            const s = text.indexOf(split);
            if (s < 0)
                return [text, ""];
            return [text.substring(0, s).trim(), text.substring(s + split.length).trim()];
        },

        /**
         * Join the truthy arguments.
         * @param {string} [delim="\n"] - Delimiter.
         * @param {...*} args - Values (falsy ones are skipped).
         * @returns {string}
         */
        JoinNonEmpty: (delim, ...args) => args.filter(Boolean).join(delim ?? '\n'),

        /**
         * Append a "header: value" line to a text, skipping null/undefined/empty values; multi-line values
         * are written as "header:\nvalue".
         * @param {string} to - Text to append to (may be null).
         * @param {string} [header] - Optional header.
         * @param {*} value - Value.
         * @returns {string}
         */
        AddNonNullLine(to, header, value) {
            if ((value === null) || (typeof value === "undefined"))
                return to;
            if ((typeof value === "string") && (value.length <= 0))
                return to;
            if (!to)
                to = "";
            if (to.length > 0)
                to += "\n";
            value = ("" + value).trim();
            const isMultiLine = (value.indexOf('\n') >= 0) || (value.indexOf('\r') >= 0);
            if (header)
                to += (header + (isMultiLine ? ":\n" : ": "));
            return to + value;
        },

        /** Escape text as HTML content (see htmlSafe). */
        HtmlSafe: htmlSafe,

        /** Escape a value for an HTML attribute (see htmlAttributeSafe). */
        HtmlAttributeSafe: htmlAttributeSafe,

        /** Highlight json as HTML (see jsonToHtml). */
        JsonToHtml: jsonToHtml,

        /**
         * Rank how well a set of strings matches a search string (all strings with the same casing); earlier
         * texts and earlier positions score higher; 0 = no match.
         * @param {string} searchFor - Non empty search string.
         * @param {...string} inTexts - Texts.
         * @returns {number}
         */
        RankScoreMatch(searchFor, ...inTexts) {
            let rank = 0;
            const l = inTexts.length;
            const sl = searchFor.length;
            for (let i = 0; i < l; ++i) {
                const text = inTexts[i];
                if (!text)
                    continue;
                const c = 1.0 / (1 + i * 0.5);
                for (let j = 0; ;) {
                    const p = text.indexOf(searchFor, j);
                    if (p < 0)
                        break;
                    j = p + sl;
                    rank += c / (1 + p * 0.5);
                }
            }
            return rank;
        },

        /**
         * Simple email syntax check (no surrounding whitespace, one '@' not first, 2-4 non empty domain parts).
         * @param {string} s - Candidate.
         * @returns {boolean}
         */
        IsValidEmail(s) {
            if (s.trim() != s)
                return false;
            const i = s.indexOf('@');
            if (i < 1)
                return false;
            const pre = s.substring(0, i);
            if (pre.trim() != pre)
                return false;
            const post = s.substring(i + 1);
            if (post.trim() != post)
                return false;
            if (post.indexOf('@') >= 0)
                return false;
            const x = post.split('.');
            if ((x.length < 2) || (x.length > 4))
                return false;
            return x.every(v => (v.trim() == v) && (v.length > 0));
        },

        /**
         * Simple phone check (no surrounding whitespace, at least 3 chars).
         * @param {string} s - Candidate.
         * @returns {boolean}
         */
        IsValidPhone: s => (s.trim() == s) && (s.length >= 3),

        /**
         * Test if a character is a letter: true if it has distinct upper/lower case, false for other ASCII,
         * otherwise true if it can start a JavaScript identifier (no eval).
         * @param {string} c - A single character.
         * @returns {boolean}
         */
        IsLetter(c) {
            if (c.toUpperCase() != c.toLowerCase())
                return true;
            if (c.charCodeAt(0) < 128)
                return false;
            return /^[\p{ID_Start}]$/u.test(c);
        },

        /** Extension of a path (see fileExtension). */
        FileExtension: fileExtension,

        /** Extension tables (Image, Video, Audio, Programming, Compress). */
        Ext: Ext,

        /** @param {string} ext - Lower-case extension. @returns {boolean} true for an image extension. */
        IsImageExt: ext => Ext.Image[ext] === 1,
        /** @param {string} ext - Lower-case extension. @returns {boolean} true for a video extension. */
        IsVideoExt: ext => Ext.Video[ext] === 1,
        /** @param {string} ext - Lower-case extension. @returns {boolean} true for an audio extension. */
        IsAudioExt: ext => Ext.Audio[ext] === 1,
        /** @param {string} ext - Lower-case extension. @returns {boolean} true for a programming / markup extension. */
        IsProgrammingExt: ext => Ext.Programming[ext] === 1,
        /** @param {string} ext - Lower-case extension. @returns {boolean} true if well compressible. */
        IsCompressibleExt: ext => (Ext.Programming[ext] === 1) || (Ext.Compress[ext] === 1),
        /** @param {string} path - Path. @returns {boolean} true for an image file (case insensitive). */
        IsImageFile: path => Ext.Image[fileExtension(path).toLowerCase()] === 1,
        /** @param {string} path - Path. @returns {boolean} true for a video file (case insensitive). */
        IsVideoFile: path => Ext.Video[fileExtension(path).toLowerCase()] === 1,

        /** @type {Readonly<Object<string, boolean>>} C# types that are non-integer numbers (default decimals). */
        DecimalTypes: decimalTypes,

        /** @type {Readonly<Object<string, string>>} C# type name -> simple format kind. */
        TypeKinds: typeKinds,

        /**
         * Compute the text and tooltip of a typed value for the simple cell formats (the DOM free part of the
         * old ValueFormat renderers; copy hints and classes are added by the ui cell renderers).
         * Kinds: "Number" (formats[1] decimals, [2] text template, [3] title template), "Boolean", "DateTime"
         * (formats[0] = "utc"/"local" or the kind), "Duration" (formats[1] text, [2] title, [4] zero text),
         * "ByteSize" / "ByteSpeed" ([2] text, [3] title), "Uptime" ([2] text template), "Default" ([1] text, [2] title).
         * Template args: {0} formatted value, {1} nextValue, {2} raw value.
         * @param {string} type - C# type name (e.g. "System.Int32").
         * @param {*} value - The value.
         * @param {string} [format] - Format specification "Kind;arg1;arg2..." (TableDataColumn.Format).
         * @param {*} [nextValue] - The next column value.
         * @returns {{Kind:string, Text:string, Title:string}}
         */
        Value(type, value, format, nextValue) {
            const formats = format ? format.split(';') : [];
            let kind = typeKinds[type] ?? "Default";
            const f0 = formats[0];
            if (f0 === "Number" || f0 === "ByteSize" || f0 === "ByteSpeed" || f0 === "Duration" || f0 === "Uptime" || f0 === "Default")
                kind = f0;
            const fmt = (t, ...args) => Sw.I18n.FormatArgs(t, args);
            switch (kind) {
                case "Number": {
                    let decimals = formats[1] ? parseInt(formats[1]) : -2;
                    if (decimals < 0)
                        decimals = decimalTypes[type] ? -decimals : 0;
                    const vs = number(value, decimals);
                    return { Kind: kind, Text: fmt(formats[2] ?? "{0}", vs, nextValue, value), Title: fmt(formats[3] ?? _TF("Raw: {2}", "Tool tip of a value in a table cell: the unformatted value. {2} is the raw value, ex: 1234567.5. {0} and {1} are other values of the cell, not used in this text"), vs, nextValue, value) };
                }
                case "Boolean":
                    return {
                        Kind: kind,
                        Text: value ? _TF('☑ True', 'A boolean true indicator, displayed when something is true') : _TF('☐ False', 'A boolean false indicator, displayed when something is false'),
                        Title: "" + value,
                    };
                case "DateTime": {
                    if (value === "0001-01-01T00:00:00")
                        return { Kind: kind, Text: "-", Title: _TF("Not set", "Tool tip indicating that the value is not set (is undefined or null)") };
                    const v = timeStamp(value ? value : new Date(), false, null, null, (f0 === "utc" || f0 === "local") ? f0 : null);
                    return { Kind: kind, Text: v[1], Title: v[0] };
                }
                case "Duration": {
                    const c = timeSpan(value, formats[4]);
                    return { Kind: kind, Text: fmt(formats[1] ?? "{0}", c, nextValue, value), Title: fmt(formats[2] ?? _TF("Raw: {2}", "Tool tip of a value in a table cell: the unformatted value. {2} is the raw value, ex: 1234567.5. {0} and {1} are other values of the cell, not used in this text"), c, nextValue, value) };
                }
                case "ByteSize":
                case "ByteSpeed": {
                    const vs = kind === "ByteSize" ? Sw.Format.Bytes(value) : Sw.Format.ByteSpeed(value);
                    return { Kind: kind, Text: fmt(formats[2] ?? "{0}", vs, nextValue, value), Title: fmt(formats[3] ?? _TF("Raw: {2}", "Tool tip of a value in a table cell: the unformatted value. {2} is the raw value, ex: 1234567.5. {0} and {1} are other values of the cell, not used in this text"), vs, nextValue, value) };
                }
                case "Uptime": {
                    const t = Sw.Format.Uptime(value);
                    const title = (value && value !== "0001-01-01T00:00:00") ? timeStamp(value)[0] : "";
                    return { Kind: kind, Text: fmt(formats[2] ?? "{0}", t), Title: title };
                }
            }
            const text = value === null ? "" : fmt(formats[1] ?? "{0}", value, nextValue);
            return { Kind: "Default", Text: text, Title: fmt(formats[2] ?? (text != value ? _TF("Raw: {2}", "Tool tip of a value in a table cell: the unformatted value. {2} is the raw value, ex: 1234567.5. {0} and {1} are other values of the cell, not used in this text") : ""), text, nextValue, value) ?? "" };
        },
    };

    //  (Sw.DevOptions walker: options here are the browser's Intl.NumberFormat options, not a framework option object)
    if (Sw.DevOptions && Sw.DevOptions.NoOptions)
        Sw.DevOptions.NoOptions("Sw.Format.Amount", "options are Intl.NumberFormat options (passed to the browser)");
})();
