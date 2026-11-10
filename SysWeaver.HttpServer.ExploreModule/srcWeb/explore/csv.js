/**
 * @file The csv / tsv viewer explore/csv.html (`Sw.CsvView`): a delimited text file shown as a table, in `Sw.Table` with local rows
 * (search, filters, sort, paging, cards on phones, Download of the shown rows: all in the browser). The page frame (url parameters r / n,
 * header actions Text and Reload, states) is Sw.FileView (../view/view.js, SysWeaver.HttpServer).
 * Registered as the extension viewer of .csv / .tsv files by ExploreHttpServerService (C#, `explore/csv.html?r={0}`).
 *
 * Url parameters (besides r / n):
 * - `delim` the field delimiter: `comma`, `semicolon`, `tab`, `pipe` or one character (default: an Excel "sep=;" first line, else tab for
 *   .tsv, else detected: each candidate , ; tab | parses the first 64 kB, the one that splits the rows into the most consistent number of
 *   fields (at least 2) wins, so commas inside quoted fields or decimal commas don't decide it).
 * - `header=true|false` the first row names the columns (default: detected, HasHeader: per column, a first cell that differs in kind from
 *   the cells below (a word over numbers, another length over fixed length codes) votes for a header, one that looks like them against;
 *   a tie is a header when the first row is unique, filled in and not repeated below). Without a header the columns are "Col 1", "Col 2" ...
 * The bar above the table shows what was used ("Separator: Semicolon (;), detected", "First row: header, detected") as two drop downs that
 * change it (the page opens again with delim / header).
 * Parsing: RFC 4180 quotes ("a ""b"", c" is one field; quoted fields may hold delimiters and line breaks), \r\n / \n / \r line ends, a byte
 * order mark is skipped, trailing empty lines are dropped. A column whose values are all numbers (empty cells allowed) is a number column
 * (integers System.Int64, else System.Double; NumberRx: "12,5" is a decimal comma unless the delimiter is ",", digit groups "1 234,50",
 * "1.234,50" and "1,234.50" are read too), so it sorts and filters as numbers.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /** Named delimiters of the `delim` url parameter. */
    const Delims = { comma: ",", semicolon: ";", tab: "\t", pipe: "|" };

    /** The candidates of the detection, in the order of preference on a tie. */
    const Candidates = [",", ";", "\t", "|"];

    /** The size of the sample the detection parses. */
    const SampleSize = 65536;

    /**
     * Split a delimited text into rows of fields (RFC 4180 quotes, any line end, a byte order mark skipped).
     * @param {string} text - The text.
     * @param {string} delim - The field delimiter (one character).
     * @returns {string[][]} The rows.
     */
    function ParseCsv(text, delim) {
        const rows = [];
        let row = [], f = "", q = false, i = text.charCodeAt(0) === 0xfeff ? 1 : 0;
        const n = text.length;
        for (; i < n; ++i) {
            const c = text[i];
            if (q) {
                if (c !== '"')
                    f += c;
                else if (text[i + 1] === '"') {
                    f += '"';
                    ++i;
                }
                else
                    q = false;
            }
            else if (c === '"' && !f)
                q = true;
            else if (c === delim) {
                row.push(f);
                f = "";
            }
            else if (c === "\n" || c === "\r") {
                if (c === "\r" && text[i + 1] === "\n")
                    ++i;
                row.push(f);
                rows.push(row);
                row = [];
                f = "";
            }
            else
                f += c;
        }
        if (f || row.length) {
            row.push(f);
            rows.push(row);
        }
        //  (trailing lines of white space only are no rows)
        while (rows.length && rows[rows.length - 1].every(x => !x.trim()))
            rows.pop();
        return rows;
    }

    /**
     * Detect the delimiter: each candidate parses a sample; the one whose most common field count (at least 2) covers the most rows wins
     * (then the larger field count, then the order of Candidates). "," when none splits the rows.
     * @param {string} text - The text.
     * @returns {string} The delimiter.
     */
    function DetectDelimiter(text) {
        let sample = text.length > SampleSize ? text.substring(0, SampleSize) : text;
        //  (a cut sample loses its last, partial row)
        if (sample.length < text.length) {
            const cut = Math.max(sample.lastIndexOf("\n"), sample.lastIndexOf("\r"));
            if (cut > 0)
                sample = sample.substring(0, cut);
        }
        let best = ",", bestScore = -1;
        for (const d of Candidates) {
            const rows = ParseCsv(sample, d).filter(r => r.length > 1 || r[0].trim());
            if (!rows.length)
                continue;
            const freq = new Map();
            for (const r of rows)
                freq.set(r.length, (freq.get(r.length) || 0) + 1);
            let mode = 0, modeN = 0;
            for (const [len, k] of freq)
                if (k > modeN || (k === modeN && len > mode)) {
                    mode = len;
                    modeN = k;
                }
            if (mode < 2)
                continue;
            const score = Math.round(modeN / rows.length * 1000) * 1000 + Math.min(mode, 999);
            if (score > bestScore) {
                best = d;
                bestScore = score;
            }
        }
        return best;
    }

    /**
     * Test if the first row is a header (see the file description). One row only: a header when all its cells are words.
     * @param {string[][]} rows - The rows.
     * @param {RegExp} numRx - What a number looks like.
     * @returns {boolean} The first row names the columns.
     */
    function HasHeader(rows, numRx) {
        const head = rows[0] || [];
        const isNum = v => numRx.test(v.trim());
        if (rows.length < 2)
            return head.length > 0 && head.every(v => v.trim() && !isNum(v));
        const data = rows.slice(1, 101);
        let votes = 0;
        for (let c = 0; c < head.length; ++c) {
            const h = (head[c] || "").trim();
            const vals = data.map(r => (r[c] || "").trim()).filter(v => v);
            if (!vals.length)
                continue;
            if (vals.every(isNum)) {
                if (h)
                    votes += isNum(h) ? -1 : 1;
                continue;
            }
            const len = vals[0].length;
            if (vals.every(v => v.length === len))
                votes += h.length === len ? -1 : 1;
        }
        if (votes !== 0)
            return votes > 0;
        //  A tie (all columns are free text): a header names every column once and its names don't show up below
        const names = head.map(v => v.trim());
        if (names.some(v => !v) || new Set(names.map(v => v.toLowerCase())).size !== names.length)
            return false;
        return names.every((v, c) => !data.some(r => (r[c] || "").trim() === v));
    }

    /**
     * The columns and rows of the table: unique column names (from the header row, else "Col 1" ...), number columns typed and their values
     * converted.
     * @param {string[][]} rows - The parsed rows (consumed: the header row is removed).
     * @param {boolean} header - The first row names the columns.
     * @param {RegExp} numRx - What a number looks like.
     * @returns {{Cols: object[], Rows: Array<Array<*>>}} The Sw.Table local data.
     */
    function ToTable(rows, header, numRx) {
        const head = header ? (rows.shift() || []) : [];
        let n = head.length;
        for (const r of rows)
            if (r.length > n)
                n = r.length;
        const intRx = /^[-+]?\d+$/;
        const used = new Set();
        const cols = [];
        for (let c = 0; c < n; ++c) {
            let any = false, num = true, int = true;
            for (const r of rows) {
                const v = (r[c] || "").trim();
                if (!v)
                    continue;
                any = true;
                if (!numRx.test(v)) {
                    num = int = false;
                    break;
                }
                const s = NormNumber(v, numRx.Delim);
                if (int && (!intRx.test(s) || !Number.isSafeInteger(Number(s))))
                    int = false;
            }
            let name = (head[c] || "").trim() || _T("Col {0}", c + 1, "Name of a column of the csv viewer when the file has no header row (or the header cell is empty). {0} is replaced by the column number");
            if (used.has(name.toLowerCase())) {
                let k = 2;
                while (used.has((name + " " + k).toLowerCase()))
                    ++k;
                name = name + " " + k;
            }
            used.add(name.toLowerCase());
            cols.push({ Name: name, Title: name, Type: any && num ? (int ? "System.Int64" : "System.Double") : "System.String" });
        }
        const out = rows.map(r => cols.map((col, c) => {
            const v = r[c] === undefined ? "" : r[c];
            if (col.Type === "System.String")
                return v;
            const s = v.trim();
            return s ? Number(NormNumber(s, numRx.Delim)) : null;
        }));
        return { Cols: cols, Rows: out };
    }

    /** The digit group separators of numbers (space, no-break space, narrow no-break space). */
    const GroupSpaces = "[ \\u00a0\\u202f]";

    /**
     * What a number looks like in a file with this delimiter: plain (12, -3.5, 1e6), a decimal comma unless the delimiter is "," (12,5),
     * digit groups with spaces (1 234,50), with commas when the decimal mark is "." (1,234.50: quoted in a comma file), with dots when it is
     * a comma (1.234,50), a percent sign at the end is not a number. The minus sign may be U+2212.
     * @param {string} delim - The delimiter.
     * @returns {RegExp} The expression (with the delimiter as its Delim property, for NormNumber).
     */
    function NumberRx(delim) {
        const comma = delim !== ",";
        const parts = [
            comma ? "\\d+(?:[.,]\\d+)?" : "\\d+(?:\\.\\d+)?",
            comma ? "[.,]\\d+" : "\\.\\d+",
            "\\d{1,3}(?:" + GroupSpaces + "\\d{3})+(?:" + (comma ? "[.,]" : "\\.") + "\\d+)?",
            "\\d{1,3}(?:,\\d{3})+(?:\\.\\d+)?",
        ];
        if (comma)
            parts.push("\\d{1,3}(?:\\.\\d{3})+,\\d+");
        const rx = new RegExp("^[-+\\u2212]?(?:" + parts.join("|") + ")(?:[eE][-+]?\\d+)?$");
        rx.Delim = delim;
        return rx;
    }

    /**
     * A number text (one NumberRx accepts) in the form Number() reads: digit groups removed, the decimal mark a ".".
     * @param {string} s - The text.
     * @param {string} delim - The delimiter of the file.
     * @returns {string} The number text.
     */
    function NormNumber(s, delim) {
        s = s.trim().replace(new RegExp(GroupSpaces, "g"), "").replace("−", "-");
        const dot = s.includes("."), comma = s.includes(",");
        if (dot && comma)
            //  The last mark is the decimal mark, the other one groups digits
            return s.lastIndexOf(",") > s.lastIndexOf(".") ? s.replace(/\./g, "").replace(",", ".") : s.replace(/,/g, "");
        if (comma)
            //  1,234 / 1,234,567 groups digits in a comma file (a quoted value); a decimal comma otherwise
            return delim === "," || /,\d{3},/.test(s) ? s.replace(/,/g, "") : s.replace(",", ".");
        return s;
    }

    /**
     * The name of a delimiter for people ("Semicolon (;)").
     * @param {string} d - The delimiter.
     * @returns {string} The name.
     */
    function DelimName(d) {
        switch (d) {
            case ",": return _TF("Comma (,)", "A field separator of a csv file, in the separator drop down of the csv viewer");
            case ";": return _TF("Semicolon (;)", "A field separator of a csv file, in the separator drop down of the csv viewer");
            case "\t": return _TF("Tab", "A field separator of a csv file (the tab character), in the separator drop down of the csv viewer");
            case "|": return _TF("Pipe (|)", "A field separator of a csv file (the | character), in the separator drop down of the csv viewer");
        }
        return '"' + d + '"';
    }

    /**
     * Open the page again with a url parameter set (or removed for "").
     * @param {string} name - The parameter (delim, header).
     * @param {string} value - The value.
     */
    function Reopen(name, value) {
        let s = location.search.replace(new RegExp("([?&])" + name + "=[^&]*", "gi"), "$1").replace(/&{2,}/g, "&").replace(/[?&]+$/, "").replace(/\?&/, "?");
        if (value)
            s += (s ? "&" : "?") + name + "=" + encodeURIComponent(value);
        Sw.Page.Replace(location.pathname + s + location.hash);
    }

    const CsvView = {
        /**
         * Page main.
         * @returns {Promise<void>}
         */
        async Main() {
            await Sw.FileView.Start({
                Kind: "CSV", Type: "fill", Class: "csv-main", Host: false,
                Render: ctx => this.Render(ctx),
                Reload: () => location.reload(),
            });
        },

        /**
         * Parse the text and show the bar and the table.
         * @param {object} ctx - The Sw.FileView context.
         * @returns {Promise<void>}
         */
        async Render(ctx) {
            const U = Sw.Url;
            const path = ctx.Url.split("?")[0];
            const tsv = /\.tsv$/i.test(ctx.Name) || /\.tsv$/i.test(path);
            if (tsv && !ctx.Header.FromMenu)
                ctx.Header.SetDescription("TSV");
            let text = ctx.Text;
            //  Excel's "sep=;" first line names the delimiter (and is no row)
            let sepLine = null;
            const sm = /^﻿?sep=(.)\r?\n/i.exec(text);
            if (sm) {
                sepLine = sm[1];
                text = text.substring(sm[0].length);
            }
            const dp = (U.Get("delim") || "").toLowerCase();
            const asked = Delims[dp] || (dp.length === 1 ? dp : "");
            const detected = sepLine || (tsv ? "\t" : DetectDelimiter(text));
            const delim = asked || detected;
            const numRx = NumberRx(delim);
            const rows = ParseCsv(text, delim);
            const hp = (U.Get("header") || "").toLowerCase();
            const headerFound = HasHeader(rows, numRx);
            const header = hp === "true" ? true : hp === "false" ? false : headerFound;
            this.Delimiter = delim;
            this.Header = header;
            const data = ToTable(rows, header, numRx);

            //  The bar: what was used, and drop downs to change it
            const how = sepLine
                ? _TF("from the file", "How the csv viewer chose the separator: the file names it (an Excel sep= line)")
                : tsv ? _TF("tsv file", "How the csv viewer chose the separator: the file is a .tsv file (tab separated)")
                    : _TF("detected", "How the csv viewer chose the separator or the header: it looked at the data");
            const sepOpts = [{ Value: "", Label: _T("Auto: {0}", DelimName(detected), "Option of a drop down of the csv viewer that lets the viewer choose (detect) the value. {0} is what it chose") + " (" + how + ")" }]
                .concat(Candidates.map(d => ({ Value: Object.keys(Delims).find(k => Delims[k] === d), Label: DelimName(d) })));
            if (asked && !Candidates.includes(asked))
                sepOpts.push({ Value: dp, Label: DelimName(asked) });
            const sep = new Sw.Select({
                Options: sepOpts, Value: asked ? (Object.keys(Delims).find(k => Delims[k] === asked) || dp) : "", Small: true,
                AriaLabel: _TF("Separator", "Label of the drop down of the csv viewer that chooses the field separator"),
                Title: _TF("The character between the fields of a row", "Tool tip of the separator drop down of the csv viewer"),
                OnChange: v => Reopen("delim", v),
            });
            const hdr = new Sw.Select({
                Options: [
                    {
                        Value: "", Label: _T("Auto: {0}", headerFound
                            ? _TF("names the columns", "The first row of a csv file names the columns (a header row), in the csv viewer")
                            : _TF("is data", "The first row of a csv file is a data row (no header), in the csv viewer"), "Option of a drop down of the csv viewer that lets the viewer choose (detect) the value. {0} is what it chose") + " (" + _TF("detected", "How the csv viewer chose the separator or the header: it looked at the data") + ")",
                    },
                    { Value: "true", Label: _TF("Names the columns", "Option of the first row drop down of the csv viewer: the first row is a header") },
                    { Value: "false", Label: _TF("Is data (columns Col 1, Col 2 ...)", "Option of the first row drop down of the csv viewer: the first row is data, the columns get numbered names") },
                ],
                Value: hp === "true" || hp === "false" ? hp : "", Small: true,
                AriaLabel: _TF("First row", "Label of the drop down of the csv viewer that says whether the first row names the columns"),
                Title: _TF("Whether the first row names the columns (a header) or is data", "Tool tip of the first row drop down of the csv viewer"),
                OnChange: v => Reopen("header", v),
            });
            const lbl = s => el("span.csv-label", { text: s });
            ctx.Page.Add(el("div.csv-bar", null,
                el("span.csv-pick", null, lbl(_TF("Separator", "Label of the drop down of the csv viewer that chooses the field separator")), sep.E),
                el("span.csv-pick", null, lbl(_TF("First row", "Label of the drop down of the csv viewer that says whether the first row names the columns")), hdr.E)));
            if (!data.Cols.length) {
                ctx.State({
                    Kind: "empty", Icon: "file",
                    Title: _TF("The file has no rows", "Title of the empty state of the csv viewer when the file has no data rows"),
                    Text: _TF("There are no rows in this file.", "Text of the empty state of the csv viewer when the file has no rows"),
                });
                return;
            }
            this.Table = await Sw.FillTable(ctx.Page, null, { Name: ctx.Name, Rows: data.Rows, Cols: data.Cols });
        },
    };

    Sw.CsvView = CsvView;
    Sw.CsvView.ParseCsv = ParseCsv;
    Sw.CsvView.DetectDelimiter = DetectDelimiter;
    Sw.CsvView.HasHeader = HasHeader;
    Sw.Page.Main(() => CsvView.Main());
})();
