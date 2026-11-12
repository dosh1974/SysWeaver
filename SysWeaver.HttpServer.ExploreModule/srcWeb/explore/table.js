/**
 * @file explore/table.js - the reusable live data table (Sw.Table) and the page script of explore/table.html (tableMain).
 *
 * A table shows the rows of any table api (a C# method `TableData X(TableDataRequest)`): paging (automatic page size), up to three sort
 * columns, filter sets (OR-ed, one filter per column), a free text search, live refresh (the server's change counter `Cc` and
 * `RefreshRate`), exports (current view / filtered / all, through `Api/GetTableExporters`), charts of a column (`chart/chart.html`),
 * local / UTC time stamps and cell formats (rendered by Sw.Cells, the TableDataColumn.Format grammar).
 *
 * Input: mouse, keyboard (the grid is one tab stop: arrows, Home / End, Ctrl+Home / End, Page Up / Down = previous / next page, Enter
 * activates a cell or sorts by a column, Space / the context menu key / Shift+F10 open the cell / column menu, "/" focuses the search,
 * "f" the filter of the current column, Ctrl+C copies the cell) and touch (scrolling, long press on a cell or a column header opens its menu).
 * table.html (option Page) adds page level shortcuts that work outside the grid too ("/" search, "g" grid, "f" filter, "?" help) and
 * the simple filter row is a single tab stop, so the grid is the third Tab stop.
 * Compact screens (narrower than 600 px, or a touch / mobile device narrower than 1000 px) show every row as a card (one
 * "label: value" line per column, no sideways scrolling; the header and filter rows are replaced by a tool bar with a "Sort" menu and a
 * "Filter" dialog that have the same options). A footer button
 * ("Table view" / "Card view", only shown on compact screens) switches to the grid instead, which scrolls horizontally with a sticky row number
 * and first column (the choice is remembered per browser in `localStorage["SysWeaver.Table.Layout"]`). On desktop the grid with its
 * sticky header row is always used. The footer buttons have short visible labels (beside the icon on desktop, under it on compact
 * screens) and a tool tip. The range text has the row count ("1-15 of 15" at the end, or a total the server counted; "1-25, more follow" while more rows follow: the server counts at most one row after the page).
 * The export menu explains each choice in one line (which rows, what the file format is good for).
 *
 * Loading: a classic script that only needs Sw (sw/core.js + sw/ui.js, or the legacy bundle common/common.js which contains both).
 * New pages: `const t = await Sw.Table.Create({ Url, Element })`. Old pages keep the global api `Table.addTable(...)` and the classes
 * `TableDataProps`, `TableDataFilterOps`, `TableDataFilter`, `TableDataState` (legacy aliases, documented in README.md).
 *
 * Server contract: TableDataRequest `{Cc, Row, MaxRowCount, LookAheadCount, Order[], Filters[], Param, SearchText}` is posted to the table api,
 * TableData `{Cols[] {Name, Title, Desc, Type, Format, Props}, Rows[] ({Values: []} or typed objects), RowCount, Title, Cc, RefreshRate}`
 * comes back (`Cols` may be omitted when unchanged). Also `Api/GetTableExporters`, `Api/StartExportTableData` / `Api/StartExportTableApi` +
 * `Api/jobs/WaitJob` / `GetJobResult` / `CancelJob` (exports are server jobs; old servers: `Api/ExportTableData`, `Api/ExportTableApi`),
 * `Api/TableChartAvailable`, `chart/chart.html?q=../Api/TableChart?{json}`.
 * Storage: `localStorage["SysWeaver.Table." + Sw.Util.HashString(url)]` = JSON TableDataState (same key and shape as before). A caller
 * that passes `KeyParam` (legacy `addTable(..., keyParam)`, table.html: the `?uniqueParam=` url param) instead keys by
 * `HashString(url + "\0" + keyParam)`, so that value keeps its own state; by default one endpoint shares a single state across all params.
 * Updates: a user change (sort, filter, search, page, Refresh) supersedes a running request at once (its answer is ignored, the
 * request itself finishes, as the original table: a refresh may be a long poll the server holds until the data changes); timed refreshes never overlap; the automatic page
 * size is not a user change (AutoFetch: never aborts, at most one per second); the request guard (RequestGuard) throttles a table that
 * sends more than twice its refresh rate's requests without user input (a loop) and warns the developer (Sw.DevWarn).
 * Framed (window.parent is another window): each server request first posts `{Type: "TableSearchParams", Param, Filters, SearchText}`
 * to the parent (an external host, ex: Fenrir, scopes a background search to the shown rows).
 * Infinite scrolling (option Scroll, table.html `?scroll`): the rows load on demand (ScrollEnsure) instead of pages; loading more is not part
 * of the update loop (ScrollMore: a held refresh must not block it), a live refresh reads the rows in view (ScrollPlan / ScrollMerge).
 * Card templates (option Card, table.html `?card=name@path`, TableData.Card): each row is an item of an html template (CardTemplate,
 * RenderCards), with a switch to the table view unless the name starts with "-"; see explore/README "Card templates".
 * Styled by explore/table.css.
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    // ------------------------------------------------------------------ contract enums

    /**
     * Column `Props` bit flags, mirror of the C# enum TableDataColumnProps. The primary key position 1..7 is a 3 bit number in bits 256 / 512 / 1024.
     * @type {Readonly<Object<string, number>>}
     */
    const Props = Object.freeze({
        CanSort: 1,
        SortedDesc: 2,
        Hide: 4,
        Filter: 8,
        TextFilter: 16,
        OrderFilter: 32,
        CanChart: 64,
        IsKey: 128,
        IsPrimaryKey1: 256,
        IsPrimaryKey2: 512,
        IsPrimaryKey3: 768,
        IsPrimaryKey4: 1024,
        IsPrimaryKey5: 1280,
        IsPrimaryKey6: 1536,
        IsPrimaryKey7: 1792,
        IsComputed: 2048,
        IsReadOnly: 4096,
        WordWrap: 8192,
        NoTitle: 16384,
        AnyFilters: 8 | 16 | 32,
        AnyKey: 1792 | 128,
    });

    /**
     * Filter operations, mirror of the C# enum TableDataFilterOps (ids 0..12), with the translated names / descriptions and the column
     * prop each operation needs (arrays indexed by the id).
     */
    const Ops = {
        Equals: 0, NotEqual: 1, LessThan: 2, GreaterThan: 3, LessEqual: 4, GreaterEqual: 5, Contains: 6, StartsWith: 7, EndsWith: 8,
        AnyOf: 9, NoneOf: 10, InRange: 11, OutsideRange: 12, Count: 13,
        /** @type {number[]} Default operation preference: the first one the column allows. */
        DefaultOrders: [6, 0, 3],
        /** @type {number[]} The column prop needed by each operation. */
        Props: [Props.Filter, Props.Filter, Props.OrderFilter, Props.OrderFilter, Props.OrderFilter, Props.OrderFilter,
            Props.TextFilter, Props.TextFilter, Props.TextFilter, Props.Filter, Props.Filter, Props.OrderFilter, Props.OrderFilter],
        /** @type {string[]} Short names (select options, value placeholders). */
        Names: [
            _TF("Equals", "Short name of a filter operation that allow values to pass through that equals a reference value"),
            _TF("Not equal", "Short name of a filter operation that allow values to pass through that does not equal a reference value"),
            _TF("Less than", "Short name of a filter operation that allow values to pass through that is less than a reference value"),
            _TF("Greater than", "Short name of a filter operation that allow values to pass through that is greater than a reference value"),
            _TF("Less or equal", "Short name of a filter operation that allow values to pass through that is less than or equal to a reference value"),
            _TF("Greater or equal", "Short name of a filter operation that allow values to pass through that is greater than or equal to a reference value"),
            _TF("Contains", "Short name of a filter operation that allow values to pass through that contains any part of a reference value (compared using text)"),
            _TF("Starts with", "Short name of a filter operation that allow values to pass through that starts with a reference value (compared using text)"),
            _TF("Ends with", "Short name of a filter operation that allow values to pass through that ends with a reference value (compared using text)"),
            _TF("Any of", "Short name of a filter operation that allow values to pass through that matches any of the reference values (separated by a comma)"),
            _TF("None of", "Short name of a filter operation that allow values to pass through that does not match any of the reference values (separated by a comma)"),
            _TF("In range", "Short name of a filter operation that allow values to pass through that is greater or eqaul to the reference minimum AND less than the reference maximum"),
            _TF("Outside range", "Short name of a filter operation that allow values to pass through that is less than the reference minimum OR greater than the reference maximum"),
        ],
        /** @type {string[]} Descriptions (option tooltips). */
        Descriptions: [
            _TF("The data value must be equal to the filter value.", "A tool tip description of a filter operation that allow values to pass through that equals a reference value"),
            _TF("The data value may not be equal to the filter value.", "A tool tip description of a filter operation that allow values to pass through that does not equal a reference value"),
            _TF("The data value must be less than (smaller than) the filter value.", "A tool tip description of a filter operation that allow values to pass through that is less than a reference value"),
            _TF("The data value must be greater than (larger than) the filter value.", "A tool tip description of a filter operation that allow values to pass through that is greater than a reference value"),
            _TF("The data value must be less than (smaller than) or equal to the filter value.", "A tool tip description of a filter operation that allow values to pass through that is less than or equal to a reference value"),
            _TF("The data value must be greater than (larger than) or equal to the filter value.", "A tool tip description of a filter operation that allow values to pass through that is greater than or equal to a reference value"),
            _TF("The data value (as text) must contain the filter value.", "A tool tip description of a filter operation that allow values to pass through that contains any part of a reference value (compared using text)"),
            _TF("The data value (as text) must start (begin) with the filter value.", "A tool tip description of a filter operation that allow values to pass through that starts with a reference value (compared using text)"),
            _TF("The data value (as text) must end with the filter value.", "A tool tip description of a filter operation that allow values to pass through that ends with a reference value (compared using text)"),
            _TF("The data value must be one of the comma separated filter values.", "A tool tip description of a filter operation that allow values to pass through that matches any of the reference values (separated by a comma)"),
            _TF("The data value may not be one of the comma separated filter values.", "A tool tip description of a filter operation that allow values to pass through that does not match any of the reference values (separated by a comma)"),
            _TF("The data value must be greater than or equal to the minimum of the comma separated filter values AND less than the maximum of the comma separated filter values.", "A tool tip description of a filter operation that allow values to pass through that is greater or eqaul to the reference minimum AND less than the reference maximum"),
            _TF("The data value must be less than the minimum of the comma separated filter values OR greater than the maximum of the comma separated filter values.", "A tool tip description of a filter operation that allow values to pass through that is less than the reference minimum OR greater than the reference maximum"),
        ],
    };

    /** The "applied on the raw value" hint of the filter controls. */
    const rawHint = _TF("Filtering is applied on the raw value before any formatting.", "A tool tip description letting the user know that any filtering is performed on the raw values and not as displayed after formatting");

    /**
     * One filter of one visible column (the C# TableDataFilter; only filters with a non empty Value are sent).
     */
    class Filter {
        /**
         * @param {string} colName Column name.
         */
        constructor(colName) {
            /** @type {boolean} Keep the rows that do NOT match. */
            this.Invert = false;
            /** @type {boolean} Case sensitive text matching. */
            this.CaseSensitive = false;
            /** @type {number} Operation id (Ops), -1 = not chosen yet. */
            this.Op = -1;
            /** @type {string} The filter value ("" = no filter). */
            this.Value = "";
            /** @type {string} Column name. */
            this.ColName = colName || "";
            /** @type {number} Index of the column in TableData.Cols (hidden columns included). */
            this.ColumnIndex = 0;
        }
    }

    /**
     * The look ahead row count of every request (the server default of the old page): the server counts at most one row after
     * the page, so tables of billions of rows are never counted further than needed. The RowCount of a reply means "at least this
     * many rows": when it is larger than the last shown row there are more rows ("1-25 of 26+", tool tip "more than 25"), else
     * it is the exact total ("1-15 of 15").
     * @type {number}
     */
    const LookAhead = 1;

    /**
     * Time out of a table data request (ms, option Timeout; 0 = none, the default, as the original table: a refresh may be a long poll
     * the server holds until the data changes).
     * @type {number}
     */
    const RequestTimeout = 0;

    /**
     * The retry delay after a failed request (ms): the original table retried every 3 s, also before the first data and after a 403 / 404.
     * @type {number}
     */
    const RetryDelay = 3000;

    /**
     * The minimum time between two automatic page size requests (Fit, AutoFetch; ms). 0: as the original, the page size follows each
     * answer at once (25, 40, 55 ... 100 in a few hundred ms); the request guard (RequestGuard) still stops a loop.
     * @type {number}
     */
    const AutoMinGap = 0;

    /**
     * The data rows of the original table page in this window (S7a-A2-4), from its onResizeFn: windowHeight = footer.offsetTop -
     * table.offsetTop - 16 - 24, with the footer fixed at the bottom (36 px) and the table at the top, less the table head (the title
     * row and the filter row: 53 px), in its 28 px rows (measured on 8310 at 1080 px: footer at 1044, head 53, rows 28: 33 rows).
     * A page table asks the page sizes the original asked in the same window (Fit).
     * @returns {number} The whole rows (at least 0).
     */
    function OrigPageRows() {
        return Math.max(0, Math.floor((window.innerHeight - 36 - 16 - 24 - 53) / 28));
    }

    /**
     * The request guard (RequestGuard): the window (ms) in which the requests sent without user input are counted. More than
     * `4 + 2 * GuardWindow / refresh rate` of them (twice what the server's refresh rate asks for, plus a few) is a loop: a developer
     * warning (Sw.DevWarn) and a hard throttle (one request per refresh rate, at least GuardGap) until the next user input.
     * @type {number}
     */
    const GuardWindow = 5000;

    /**
     * The minimum time between two requests of a throttled table (RequestGuard; ms).
     * @type {number}
     */
    const GuardGap = 1000;

    /**
     * The pause after the last key stroke before the typed search text is applied (ms; live search, Enter applies at once).
     * @type {number}
     */
    const SearchDelay = 300;

    /**
     * The default TableDataRequest.
     * @returns {{Cc: number, Row: number, MaxRowCount: number, LookAheadCount: number, Order: (string[]|null)}} The request.
     */
    function DefaultRequest() {
        return { Cc: 0, Row: 0, MaxRowCount: 25, LookAheadCount: LookAhead, Order: null };
    }

    /**
     * Persisted table state (mirror of the C# TableDataState plus TimeMode).
     */
    class State {
        constructor() {
            /** @type {number} Number of filter sets (1..3). */
            this.FilterRows = 1;
            /** @type {Filter[][]} One filter array (one per visible column) per set. */
            this.Filters = [[]];
            /** @type {number} Filter ui: 0 hidden, 1 simple (values of the first set), 2 advanced (everything). */
            this.Expanded = 1;
            /** @type {object} The TableDataRequest. */
            this.RequestParams = DefaultRequest();
            /** @type {boolean} Fit the page size to the available height (first page only). */
            this.AutoRowCount = true;
            /** @type {string|null} Unformatted System.DateTime columns: null = local time, "utc". */
            this.TimeMode = null;
        }
    }

    /**
     * Repair a loaded (or missing) state: missing parts get defaults, numbers are coerced, the filter set count matches.
     * @param {object|null} s The parsed state.
     * @returns {State} The state (the same object when it was usable).
     */
    function RepairState(s) {
        if (!s || typeof s !== "object")
            s = new State();
        const r = s.RequestParams && typeof s.RequestParams === "object" ? s.RequestParams : (s.RequestParams = DefaultRequest());
        r.Cc = +r.Cc || 0;
        r.Row = Math.max(0, +r.Row || 0);
        r.MaxRowCount = Math.max(1, +r.MaxRowCount || 25);
        //  Never count further than needed: stored states of the previous version asked for a look ahead of 500
        r.LookAheadCount = LookAhead;
        if (!Array.isArray(r.Order) || r.Order.length <= 0)
            r.Order = null;
        if (!Array.isArray(s.Filters) || s.Filters.length <= 0) {
            s.Filters = [[]];
            if (typeof s.Expanded !== "number")
                s.Expanded = 1;
        }
        s.Filters = s.Filters.slice(0, 3).map(f => Array.isArray(f) ? f.filter(x => x && typeof x === "object") : []);
        s.Filters.forEach(set => set.forEach(f => {
            f.Op = Number.isFinite(+f.Op) ? +f.Op : -1;
            f.Value = f.Value === null || f.Value === undefined ? "" : String(f.Value);
            f.Invert = !!f.Invert;
            f.CaseSensitive = !!f.CaseSensitive;
        }));
        s.FilterRows = s.Filters.length;
        const e = +s.Expanded;
        s.Expanded = e === 0 || e === 1 || e === 2 ? e : 1;
        if (typeof s.AutoRowCount !== "boolean")
            s.AutoRowCount = true;
        if (s.TimeMode !== "utc")
            s.TimeMode = null;
        return s;
    }

    // ------------------------------------------------------------------ sort order helpers (Order = up to 3 names, "-" prefix = descending)

    const Sort = {
        /**
         * Any sort set.
         * @param {object} req The request.
         * @returns {boolean} True if sorted.
         */
        Has: req => !!(req.Order && req.Order.length > 0),
        /**
         * Position of a column in the order.
         * @param {object} req The request.
         * @param {string} col Column name.
         * @returns {{Index: number, Desc: boolean}} Index -1 when not sorted by it.
         */
        Find(req, col) {
            const o = req.Order || [];
            for (let i = 0; i < o.length; ++i) {
                if (o[i] === col)
                    return { Index: i, Desc: false };
                if (o[i] === "-" + col)
                    return { Index: i, Desc: true };
            }
            return { Index: -1, Desc: false };
        },
        /**
         * Remove a column from the order.
         * @param {object} req The request.
         * @param {string} col Column name.
         */
        Remove(req, col) {
            const o = (req.Order || []).filter(x => x !== col && x !== "-" + col);
            req.Order = o.length ? o : null;
        },
        /**
         * Make a column the primary sort with a direction (other occurrences removed, max 3 columns).
         * @param {object} req The request.
         * @param {string} col Column name.
         * @param {boolean} desc Descending.
         */
        Set(req, col, desc) {
            const o = (req.Order || []).filter(x => x !== col && x !== "-" + col);
            o.unshift((desc ? "-" : "") + col);
            req.Order = o.slice(0, 3);
        },
        /**
         * Sort click: flip the direction of the primary column, else make the column primary in its default direction.
         * @param {object} req The request.
         * @param {string} col Column name.
         * @param {boolean} defDesc Default direction of the column.
         */
        Toggle(req, col, defDesc) {
            const f = Sort.Find(req, col);
            Sort.Set(req, col, f.Index === 0 ? !f.Desc : defDesc);
        },
        /**
         * Plain sort click: flip the direction of the primary column (the other keys kept), else sort by this column only, in its
         * default direction (Shift + click adds the column as the primary key and keeps the others: Toggle).
         * @param {object} req The request.
         * @param {string} col Column name.
         * @param {boolean} defDesc Default direction of the column.
         */
        Only(req, col, defDesc) {
            const f = Sort.Find(req, col);
            if (f.Index === 0)
                Sort.Set(req, col, !f.Desc);
            else
                req.Order = [(defDesc ? "-" : "") + col];
        },
    };

    /**
     * The first sort direction of a column: descending only when the server says so (C# SortedDesc), like the original table.
     * @param {object} c The column.
     * @returns {boolean} True to sort descending first.
     */
    function sortDescFirst(c) {
        return (c.Props & Props.SortedDesc) !== 0;
    }

    // ------------------------------------------------------------------ shared server lookups

    /** @type {Promise<boolean>|null} Charts available (cached). */
    let chartAvailable = null;

    /**
     * Test once if table charts are available (SysWeaver.MicroService.ChartJs).
     * @returns {Promise<boolean>} True if "Show as chart" can be offered.
     */
    function CanShowChart() {
        if (!chartAvailable)
            /* SwWeb:allow ux-fbk-5 (no answer = no chart service: "Show as chart" is not offered; nothing the user asked for failed) */
            chartAvailable = Sw.Net.Get(Sw.Net.Api("TableChartAvailable")).then(x => x === true, () => false);
        return chartAvailable;
    }

    /** @type {Promise<Array>|null} The table exporters (cached). */
    let exporters = null;

    /**
     * Get the table exporters once (WebMenuItem[] {Id, Name, Title, IconClass}).
     * @returns {Promise<Array>} The exporters (empty on failure).
     */
    function GetExporters() {
        if (!exporters)
            exporters = Sw.Net.Get(Sw.Net.Api("GetTableExporters")).then(x => Array.isArray(x) ? x : [], e => {
                exporters = null;
                throw e;
            });
        return exporters;
    }

    /**
     * The display name of a table exporter (the export menu): the server name, with the spelling of the built in ids fixed (
     * the ids "Mark down" and "CSV (semi colon)" stay, scripts and older clients export with them).
     * @param {{Id: string, Name: ?string}} x The exporter.
     * @returns {string} The name.
     */
    function ExporterName(x) {
        switch (x.Id) {
            case "Mark down": return "Markdown";
            case "CSV (semi colon)": return _TF("CSV (semicolon)", "Name of a table export format in the export menu: a CSV file whose values are separated by semicolons");
            default: return String(x.Name || x.Id || "");
        }
    }

    /**
     * A one line, plain words description of a table exporter (the export menu): what the file is good for. The built in
     * exporters (by Id) have a fixed text, other exporters use the first line of their server description (Title).
     * @param {{Id: string, Title: ?string}} x The exporter.
     * @returns {string} The description ("" if none).
     */
    function ExporterDesc(x) {
        switch (x.Id) {
            case "CSV (comma)": return _TF("Opens in Excel and other spreadsheet programs", "One line description of the CSV (comma separated) table export format");
            case "CSV (semi colon)": return _TF("For Excel in countries that write decimals with a comma", "One line description of the CSV (semicolon separated) table export format");
            case "CSV (tab)": return _TF("Spreadsheets and text editors, columns separated by tabs", "One line description of the CSV (tab separated) table export format");
            case "Excel Workbook": return _TF("An Excel file (.xlsx) with a formatted table", "One line description of the Excel workbook table export format");
            case "PDF document": return _TF("A document to print or share, cannot be edited", "One line description of the PDF table export format");
            case "Simple HTML": return _TF("A web page that opens in any browser", "One line description of the HTML table export format");
            case "Mark down": return _TF("A text table for documents and wikis", "One line description of the Markdown table export format");
            case "JSON (verbose)": return _TF("For programs, easy to read", "One line description of the formatted JSON table export format");
            case "JSON (compact)": return _TF("For programs, smallest file", "One line description of the compact JSON table export format");
            case "Private link": return _TF("A link that only you can open", "One line description of the private link table export (saves the data and gives a link)");
            case "Protected link": return _TF("A link that every signed in user can open", "One line description of the protected link table export (saves the data and gives a link)");
            case "Public link": return _TF("A link that anyone can open", "One line description of the public link table export (saves the data and gives a link)");
            default: return String(x.Title || "").split("\n")[0];
        }
    }

    /**
     * The file extension a table exporter makes (to name the file before the server answers: "Exporting Log 2026-10-20 15-04 UTC+2.xlsx").
     * @param {{Id: string}} x The exporter.
     * @returns {string} The extension with the dot, "" when unknown or not a file (the link exporters).
     */
    function ExporterExt(x) {
        const id = String(x && x.Id || "");
        if (/^CSV/i.test(id))
            return ".csv";
        if (/Excel/i.test(id))
            return ".xlsx";
        if (/PDF/i.test(id))
            return ".pdf";
        if (/HTML/i.test(id))
            return ".html";
        if (/^Mark ?down/i.test(id))
            return ".md";
        if (/^JSON/i.test(id))
            return ".json";
        return "";
    }

    /**
     * The time stamp of an export file name: when the data was taken, in the viewer's time with its zone, without ":" (file names):
     * "2026-10-20 15-04 UTC+2", "2026-10-20 15-04 UTC+5-30", "2026-10-20 13-04 UTC".
     * @param {Date} [now] The time (default: now).
     * @returns {string} The stamp.
     */
    function FileStamp(now) {
        now = now || new Date();
        const p2 = n => String(n).padStart(2, "0");
        const off = -now.getTimezoneOffset();
        const a = Math.abs(off);
        const zone = off === 0 ? "UTC" : "UTC" + (off > 0 ? "+" : "-") + Math.floor(a / 60) + (a % 60 ? "-" + p2(a % 60) : "");
        return now.getFullYear() + "-" + p2(now.getMonth() + 1) + "-" + p2(now.getDate()) + " " + p2(now.getHours()) + "-" + p2(now.getMinutes()) + " " + zone;
    }

    /** The FileStamp pattern in a text ("2026-10-20 15-04 UTC+2", "… UTC+5-30", "… UTC"; KeepStamp). */
    const StampRx = /\d{4}-\d{2}-\d{2} \d{2}-\d{2}(?: UTC(?:[+-]\d{1,2}(?:-\d{2})?)?)?/g;

    /**
     * A text that shows a file name, for the screen: every time stamp of an export file name (FileStamp) stays on one line (non breaking
     * hyphens U+2011 and spaces U+00A0), so "Log 2026-10-20 15-04 UTC+2.csv is ready" never wraps inside the time (UX guide TXT-17,
     * LAY-8). Use it for every visible text with a file name (titles, toasts, results, announcements); the saved file name keeps plain
     * characters. Texts without a stamp are returned unchanged.
     * @param {string} text The text.
     * @returns {string} The text with unbreakable stamps.
     */
    function KeepStamp(text) {
        return text === null || text === undefined ? "" : String(text).replace(StampRx, s => s.replace(/-/g, "‑").replace(/ /g, " "));
    }

    /**
     * The table request of an export of all pages: from the first row, the column info not needed; mode 2 ("All") without the filters
     * and the search.
     * @param {object} r The table request.
     * @param {number} mode 1 filtered, 2 all.
     * @returns {object} The request to export.
     */
    function exportReq(r, mode) {
        const req = Object.assign({}, r, { Cc: 0, Row: 0 });
        if (mode === 2) {
            req.Filters = null;
            req.SearchIndex = null;
            req.SearchText = null;
        }
        return req;
    }

    /**
     * Run a data table export as a server background job (UX guide FBK-4): `Api/Start<name>` starts it, `Api/jobs/WaitJob` waits
     * until it ended (long poll), `Api/jobs/GetJobResult` returns the file. The server keeps the job and its file for 30 minutes
     * after it ended, so the app shell can show it again after a reload (`Api/jobs/GetJobs`, push message "sw.job"). The server
     * decides how it ends: a Cancel is only a request (exportCancel: `Api/jobs/CancelJob`, the job says "stopping" until the server
     * answers), so the loop goes on until the job is "cancelled" (an AbortError), "failed" or "done" (also after a Cancel that came
     * too late: the file is returned, the caller says so). An abort of the signal only stops waiting (leaving the page: the job goes
     * on and its file is listed again after a reload). A server without the job API (404 on the start) gets the old one call export.
     * @param {string} name "ExportTableData" or "ExportTableApi".
     * @param {object} body The body of the export (Key and Title are passed on to the job).
     * @param {object} sendOpt The Sw.Net options (Signal, NoNavigate, Retry).
     * @param {{Started: function(object):void, Job: ?object, Starting: boolean}} ctl The control of the export: Started is called with
     * the server job once it runs on the server (the page may now be left); Job is the server job while it runs, Starting true until
     * the start answered (exportCancel reads them).
     * @returns {Promise<object>} The file {Name, Mime, Data} (or a link {Name}).
     */
    async function exportJob(name, body, sendOpt, ctl) {
        let job;
        ctl.Starting = true;
        try {
            job = await Sw.Net.Send(Sw.Net.Api("Start" + name), body, sendOpt);
        }
        catch (e) {
            ctl.Starting = false;
            if (e && e.Status === 404) {
                //  (no job api: the old one call export, a plain request; a Cancel asked meanwhile aborts it)
                if (ctl.Local)
                    ctl.Local();
                return Sw.Net.Send(Sw.Net.Api(name), body, sendOpt);
            }
            throw e;
        }
        ctl.Starting = false;
        ctl.Job = job || null;
        if (ctl.Started && job)
            ctl.Started(job);
        const sig = sendOpt.Signal;
        try {
            //  ("stopping": the server got the Cancel and waits for the work to return; the job still runs)
            while (job && (job.State === "running" || job.State === "stopping")) {
                if (sig && sig.aborted)
                    throw new DOMException("Aborted", "AbortError");
                //  SwWeb:allow ux-fbk-4 (the page path: no activity list (table.html on its own, a working dialog) or the start failed; in the app shell Export uses Sw.Activity.StartServer)
                job = await Sw.Net.Send(Sw.Net.Api("jobs/WaitJob"), { Id: job.Id, Ms: 20000 }, sendOpt);
                if (job)
                    ctl.Job = job;
            }
            /** (the job's last state: `CancelRequested` + done = a Cancel came too late, also one from another tab) */
            ctl.Last = job;
            if (!job)
                throw new Error(_TF("The export ended in the service without a result.", "Error of a data table export when the service job ended without a file"));
            if (job.State === "cancelled")
                throw new DOMException("Aborted", "AbortError");
            if (job.State !== "done")
                throw new Error(job.Error || _TF("The export failed in the service.", "Error of a data table export when the service job failed without a reason"));
            return await Sw.Net.Send(Sw.Net.Api("jobs/GetJobResult"), job.Id, sendOpt);
        }
        finally {
            ctl.Job = null;
        }
    }

    /**
     * Ask the server to cancel a running export job (exportJob) and wait for its answer: the shared cancellation model (UX guide FBK-4,
     * FBK-5: "stopping" until the server confirms). `Api/jobs/CancelJob` answers the job: "cancelled" = stopped (the caller stops
     * waiting), "done" = too late (the file was made; the caller says so and offers it), "failed" = its own failure, "stopping" /
     * "running" = the server waits for the work to return (exportJob's loop sees the end). A failed CancelJob request throws (the
     * export goes on, the caller says "could not cancel").
     * @param {{Job: ?object, Starting: boolean}} ctl The control of the export (exportJob).
     * @returns {Promise<string>} "cancelled", "done", "failed", "stopping", or "local" when the export does not run as a server job (no
     *   job API, or not started yet: the caller aborts its request, which writes nothing).
     */
    async function exportCancel(ctl) {
        const job = ctl.Job;
        if (!job || !job.Id)
            return "local";
        const x = await Sw.Net.Send(Sw.Net.Api("jobs/CancelJob"), job.Id, { NoNavigate: true });
        const st = x && typeof x.State === "string" ? x.State : "";
        //  (an old server answers true / nothing: it cancelled the job's token, the loop sees the end)
        return st === "cancelled" || st === "done" || st === "failed" ? st : "stopping";
    }

    /**
     * An icon only button.
     * @param {string} icon Icon (inline name or icon class).
     * @param {string} title Tooltip / accessible name.
     * @param {function(object, Event):*} onClick Click handler.
     * @param {string} [cls] Extra class.
     * @returns {object} The Sw.Button.
     */
    function iconButton(icon, title, onClick, cls) {
        return new Sw.Button({ Icon: icon, Title: title, Kind: "ghost", Small: true, Class: cls, OnClick: onClick });
    }

    /**
     * A footer button: an icon button with a short visible label (span.sw-table-blabel, beside the icon on desktop, below it on compact / touch
     * screens by table.css; the accessible name stays the tool tip).
     * @param {string} icon Icon (inline name or icon class).
     * @param {string} label Short visible label.
     * @param {string} title Tooltip / accessible name.
     * @param {function(object, Event):*} onClick Click handler.
     * @returns {object} The Sw.Button (member SwLabel: the label element).
     */
    function footButton(icon, label, title, onClick) {
        const b = iconButton(icon, title, onClick, "sw-table-fbtn");
        b.SwLabel = el("span.sw-table-blabel", { text: label, attrs: { "aria-hidden": "true" } });
        b.E.append(b.SwLabel);
        return b;
    }

    /**
     * True if a rendered cell shows nothing: no text (or only the "-" of a value that is not set, ex: the Start time and Duration of
     * the Idle / System processes, Sw.Cells DateTime / Uptime for "0001-01-01T00:00:00") and no picture, icon or control (the card
     * layout leaves such fields out, see table.css `.sw-table-blank`).
     * @param {HTMLElement} td The cell.
     * @returns {boolean}
     */
    function isBlankCell(td) {
        const s = td.textContent.trim();
        return (!s || s === "-") && !td.querySelector("img,svg,canvas,video,audio,input,select,textarea,button,.sw-icon,[class*='Icon']");
    }

    /** @type {RegExp} A word of 14 or more characters without a space (a name, path, url) that needs break opportunities. */
    const LongWordRx = /\S{14,}/;

    /** @type {RegExp} The break opportunities inside a long word: after . / \ _ : , - ? & = and between a lower case letter and an
     *  upper case letter (camelCase: "ServerManager|Service"). */
    const SoftBreakRx = /([./\\_:,\-?&=]+)(?=[^./\\_:,\-?&=])|([a-z])(?=[A-Z])/g;

    /**
     * Insert soft break opportunities (<wbr>) into the long words of a plain text cell (or of its only element, ex: a link), so long
     * names wrap at word, camelCase and punctuation boundaries ("SysWeaver.MicroService.|ServerManagerService") instead of in the
     * middle of a word in the cards (table.css keeps overflow-wrap: anywhere only as the last resort). The text (textContent) is
     * unchanged; Sw.Cells replaces the content when the value changes.
     * @param {HTMLElement} td The cell.
     */
    function softBreaks(td) {
        let e = td;
        if (e.childElementCount === 1 && !e.firstElementChild.childElementCount && e.childNodes.length === 1)
            e = e.firstElementChild;
        if (e.childElementCount || e.childNodes.length !== 1)
            return;
        const s = e.textContent;
        if (s.length > 400 || !LongWordRx.test(s))
            return;
        const parts = s.replace(/\S{14,}/g, w => w.replace(SoftBreakRx, "$1$2\u0000")).split("\u0000");
        if (parts.length < 2)
            return;
        const nodes = [];
        parts.forEach((p, i) => {
            if (i)
                nodes.push(document.createElement("wbr"));
            nodes.push(document.createTextNode(p));
        });
        e.replaceChildren(...nodes);
    }

    /**
     * True if the server hides a column by default (C# TableDataHideAttribute, Props.Hide): the column is in the data, in the grid and
     * in the column chooser, hidden until the viewer shows it (Sw.Table ApplyHidden / ColsMenu; a card shows it only when opened).
     * @param {object} c The column (TableDataColumn).
     * @returns {boolean}
     */
    function srvHidden(c) {
        return !!c && (c.Props & Props.Hide) !== 0;
    }

    /**
     * What a column with a cryptic one letter title (ex: Process information "C" / "M", a chart link; not "Id") is about: its
     * description, else the fixed tool tip of its Url / Img format without the "Click to show" start ("Click to show Cpu use details"
     * -> "Cpu use details"). Used as the card label, the header label and in the header tool tip (the titles themselves come from the
     * server).
     * @param {object} c The column (TableDataColumn).
     * @returns {string} The text ("" for normal titles or when nothing explains the column).
     */
    function shortTitleHint(c) {
        const title = String(c.Title || c.Name || "");
        if (title.length > 1)
            return "";
        let s = String(c.Desc || "").trim();
        if (!s) {
            const p = String(c.Format || "").split(";");
            if ((p[0] === "Url" || p[0] === "Img") && p[3] && p[3].indexOf("{") < 0)
                s = p[3].trim().replace(/^(click|tap)( here)? to (show |open |view )?/i, "").replace(/[.!]+$/, "");
        }
        if (!s)
            return "";
        if (s.length > 40)
            s = s.substring(0, 39) + "…";
        return s.charAt(0).toUpperCase() + s.substring(1);
    }

    /**
     * Clearer column titles for known server columns whose member name says less than the data (the server title is the member name):
     * `When(cols)` tells if the rule applies to the table, `Cols` maps a column name to a function returning the translated title.
     * Ex: a text message table (IncomingTextMessage: Country flag, then "Name" = the name of the country of the phone prefix, not
     * the sender) could show "Country name". Empty by default: the column titles are the server's, as the original (P2-A). Extend with
     * Sw.Table.KnownTitles.push({ When, Cols }).
     * @type {Array<{When: function(Array<object>): boolean, Cols: Object<string, function(): string>}>}
     */
    const KnownTitles = [];

    /**
     * Apply KnownTitles to the columns of a response (changes Title in place; the column Name stays, the server filters / sorts on it).
     * @param {Array<object>} cols TableData.Cols.
     */
    function ApplyKnownTitles(cols) {
        if (!Array.isArray(cols))
            return;
        KnownTitles.forEach(k => {
            if (!k.When(cols))
                return;
            cols.forEach(c => {
                const f = k.Cols[c.Name];
                if (f && (!c.Title || c.Title === c.Name))
                    c.Title = f();
            });
        });
    }

    /**
     * Health hints of known kinds of columns (table rows and cells that need attention get a danger / warning colour plus an icon and a
     * word, never the colour alone; RowHealth / CellHealth):
     * - FailCount: a count of failures / exceptions (ex: Sources "Fails", Scheduled tasks "Exception count") above 0 marks the row as failing;
     * - LastFail: a last failure / exception text (a string column, ex: Sources "Last fail") that is set marks the row as failing;
     * - UsedPercent: a used percentage (ex: Drive information "Used", a "Number" format with "%") gets a warning from Warn %, danger from Bad %.
     * @type {{FailCount: RegExp, LastFail: RegExp, UsedPercent: RegExp, Warn: number, Bad: number}}
     */
    const Health = {
        FailCount: /^(fails?|failcount|failures|failurecount|errorcount|exceptioncount)$/i,
        LastFail: /^(lastfail|lastfailure|lasterror|lastexception|lastexceptionex)$/i,
        UsedPercent: /^(used|usedpercent|percentused|diskused|driveused|diskusage|driveusage)$/i,
        Warn: 80,
        Bad: 90,
    };

    /**
     * Known values of enum column types (the server sends enum numbers and no names): the filter value inputs of these columns get a
     * drop down list (a datalist) of the names the server filters on (Value, untranslated) with a translated label.
     * Extend with Sw.Table.EnumValues[type] = () => [{Value, Label}].
     * @type {Object<string, function(): Array<{Value: string, Label: string}>>}
     */
    const EnumValues = {
        "SysWeaver.MessageLevels": () => [
            { Value: "Error", Label: _TF("Error", "Name of the log message level Error") },
            { Value: "Warning", Label: _TF("Warning", "Name of the log message level Warning") },
            { Value: "Info", Label: _TF("Info", "Name of the log message level Info (normal operation)") },
            { Value: "Debug", Label: _TF("Debug", "Name of the log message level Debug (detailed diagnostic messages)") },
        ],
    };

    /**
     * Known enum column types that the server sends as numbers (TableData has no enum names): the cells show the translated name
     * in the tool tip (the cell shows the number, as the original; ex: FakeText Status 3 "Delivered"), and the filter value inputs get the names (Name, untranslated:
     * the server filters on them). Flags: a flags enum (a value with several bits shows the names joined by ", "). RowText(value, field):
     * an optional text that depends on the row (field(name) = the value of another column of the row), null for the normal name
     * (ex: the status Unknown of an incoming text message shows "Received").
     * Extend with Sw.Table.EnumNames[type] = { Flags, RowText, Values: () => [{Value, Name, Label}] }.
     * @type {Object<string, {Flags: (boolean|undefined), RowText: (function(number, function(string): *): ?string|undefined), Values: function(): Array<{Value: number, Name: string, Label: string}>}>}
     */
    const EnumNames = {
        "SysWeaver.MicroService.TextMessageStatuses": {
            //  An incoming message has no delivery status (the server sends Unknown): "Received"
            RowText: (value, field) => value === 0 && field("Incoming") === true
                ? _TF("Received", "Status of a text message that the system received (incoming messages have no delivery status)")
                : null,
            Values: () => [
                { Value: 0, Name: "Unknown", Label: _TF("Unknown", "Status of a text message: not known") },
                { Value: 1, Name: "Accepted", Label: _TF("Accepted", "Status of a text message: accepted by the provider") },
                { Value: 2, Name: "Sent", Label: _TF("Sent", "Status of a text message: sent") },
                { Value: 3, Name: "Delivered", Label: _TF("Delivered", "Status of a text message: delivered to the phone") },
                { Value: 4, Name: "Read", Label: _TF("Read", "Status of a text message: read by the receiver") },
                { Value: 5, Name: "Failed", Label: _TF("Failed", "Status of a text message: failed") },
            ],
        },
        "SysWeaver.MicroService.TextMessageFailures": {
            Values: () => [
                { Value: 0, Name: "None", Label: _TF("None", "Failure reason of a text message: no failure") },
                { Value: 1, Name: "NotOnChannel", Label: _TF("Not on the channel", "Failure reason of a text message: the receiver is not on the channel (ex: no WhatsApp)") },
                { Value: 2, Name: "InvalidNumber", Label: _TF("Invalid number", "Failure reason of a text message: the phone number is not valid") },
                { Value: 3, Name: "Blocked", Label: _TF("Blocked", "Failure reason of a text message: blocked") },
                { Value: 4, Name: "OutsideWindow", Label: _TF("Outside the reply window", "Failure reason of a text message: sent outside the allowed reply window") },
                { Value: 5, Name: "Unreachable", Label: _TF("Unreachable", "Failure reason of a text message: the phone could not be reached") },
                { Value: 6, Name: "Rejected", Label: _TF("Rejected", "Failure reason of a text message: rejected") },
                { Value: 7, Name: "RateLimited", Label: _TF("Rate limited", "Failure reason of a text message: too many messages") },
                { Value: 8, Name: "Expired", Label: _TF("Expired", "Failure reason of a text message: expired before delivery") },
                { Value: 9, Name: "Other", Label: _TF("Other", "Failure reason of a text message: another reason") },
            ],
        },
        "SysWeaver.MicroService.TextMessageChannels": {
            Flags: true,
            Values: () => [
                { Value: 0, Name: "Any", Label: _TF("Any", "Channel of a text message: any channel") },
                { Value: 1, Name: "Sms", Label: _TF("SMS", "Channel of a text message: SMS") },
                { Value: 2, Name: "Mms", Label: _TF("MMS", "Channel of a text message: MMS") },
                { Value: 4, Name: "WhatsApp", Label: _TF("WhatsApp", "Channel of a text message: WhatsApp") },
                { Value: 8, Name: "Rcs", Label: _TF("RCS", "Channel of a text message: RCS (rich communication services)") },
                { Value: 0x40000000, Name: "Other", Label: _TF("Other", "Channel of a text message: another channel") },
            ],
        },
        "SysWeaver.Scheduler+TaskTypes": {
            Values: () => [
                { Value: 0, Name: "TypeAction", Label: _TF("Action", "Kind of a scheduled task: a plain function") },
                { Value: 1, Name: "TypeTask", Label: _TF("Task", "Kind of a scheduled task: an async function (Task)") },
                { Value: 2, Name: "TypeValueTask", Label: _TF("Value task", "Kind of a scheduled task: an async function (ValueTask)") },
            ],
        },
    };

    /** @type {Map<string, Array<{Value: number, Name: string, Label: string}>>} The EnumNames values per type (made once). */
    const enumCache = new Map();

    /**
     * The values of a known enum type (EnumNames).
     * @param {string} type The C# type.
     * @returns {Array<{Value: number, Name: string, Label: string}>|null} The values, null when unknown.
     */
    function enumValues(type) {
        const d = type && EnumNames[type];
        if (!d)
            return null;
        let v = enumCache.get(type);
        if (!v)
            enumCache.set(type, v = d.Values());
        return v;
    }

    /**
     * The shown text of a number of a known enum type: the translated name (a flags enum: the names of the bits, joined by ", "); an
     * unknown number stays a number.
     * @param {string} type The C# type (a key of EnumNames).
     * @param {number} value The number.
     * @returns {string|null} The text, null when the type is not known or the value is not a number.
     */
    function enumText(type, value) {
        const list = enumValues(type);
        if (!list || typeof value !== "number")
            return null;
        const hit = list.find(x => x.Value === value);
        if (hit)
            return hit.Label;
        if (EnumNames[type].Flags && value > 0) {
            const parts = list.filter(x => x.Value > 0 && (value & x.Value) === x.Value);
            if (parts.length && parts.reduce((a, x) => a | x.Value, 0) === value)
                return parts.map(x => x.Label).join(", ");
        }
        return String(value);
    }

    /**
     * The id of a (shared, created once per page) datalist with the known values of an enum column type, for a filter value input.
     * @param {string} type The C# type of the column.
     * @returns {string|null} The datalist id, null when the type has no known values.
     */
    function enumList(type) {
        const f = type && EnumValues[type];
        const named = f ? null : enumValues(type);
        if (!f && !named)
            return null;
        const id = "sw-table-enum-" + String(type).replace(/[^A-Za-z0-9_-]/g, "_");
        if (!document.getElementById(id)) {
            const opts = f ? f() : named.map(x => ({ Value: x.Name, Label: x.Label }));
            document.body.append(el("datalist", { id }, ...opts.map(x => el("option", { value: x.Value, label: x.Label }))));
        }
        return id;
    }

    /** @type {string} Local storage key of the viewer's layout choice on compact screens ("grid", "cards"; default: cards on phones in
     *  portrait, the compact grid on phones in landscape). */
    const LayoutKey = "SysWeaver.Table.Layout";

    /** @type {string} Local storage key of the striped rows choice of every table in this browser ("on", "off"; none = automatic: striped
     *  in the compact density only, Design v2 section 9). */
    const ZebraKey = "SysWeaver.Table.Zebra";

    /** @type {string} Local storage key of the density of the whole site (the same key and values as core/theme.js, Design v2, PORTING.md B9). */
    const DensityKey = "SysWeaver.Density";

    /** @type {number} Values shown on a closed card (the title = the first column, then 4 fields in two columns; Table.CompactCard,
     *  "Show all n fields" in the card's menu shows the rest). Every closed card has the same height. */
    const CardFields = 5;

    /** @type {number} Skeleton rows shown while the first data loads (the real row height, so nothing moves when the rows come). */
    const SkeletonRows = 12;

    /**
     * Line icons of the table chrome (24 grid, 1.75 stroke, currentColor; Design v2, PORTING.md B15) that the Sw icon set has no name for yet.
     * @type {Object<string, string>}
     */
    const LineIcons = {
        first: '<path d="M11 17l-5-5 5-5"/><path d="M18 17l-5-5 5-5"/>',
        prev: '<path d="M15 18l-6-6 6-6"/>',
        next: '<path d="M9 6l6 6-6 6"/>',
        columns: '<rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M9.5 4.5v15M14.5 4.5v15"/>',
        rows: '<path d="M4 6h16M4 12h16M4 18h16"/>',
        kebab: '<circle cx="12" cy="5.5" r="1.3" fill="currentColor" stroke="none"/><circle cx="12" cy="12" r="1.3" fill="currentColor" stroke="none"/><circle cx="12" cy="18.5" r="1.3" fill="currentColor" stroke="none"/>',
        sort: '<path d="M7 4v16M3.5 16.5 7 20l3.5-3.5"/><path d="M17 20V4M13.5 7.5 17 4l3.5 3.5"/>',
        empty: '<rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M3.5 9.5h17M9 14h6"/>',
        x: '<path d="M7 7l10 10M17 7 7 17"/>',
        plus: '<path d="M12 5v14M5 12h14"/>',
        chart: '<path d="M4 20h16"/><path d="M7 16v-5M12 16V6M17 16v-8"/>',
        check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    };

    /**
     * True if a row action ends or removes its row's item (End session, Kill, Delete, Withdraw ...) as the SERVER declared it: Sw.Cells'
     * `data-sw-final="true"` from the action's "final" part (C# TableDataActionsFullAttribute with TableDataActionFlags.Final). After
     * such an action succeeded the row is updated in place and the action is not offered again on it (ActionDone, MarkActed). It is
     * never guessed from the words of the action's text or tool tip, so rewording or translating them can't change the behaviour (G47;
     * Admin N53: the tool tip "…their open sessions end" made Force password reset final and hid the row's re-read actions). An action
     * without the part (an older format: TableDataActions / Ex, or a page's own format) is not final: the row is re-read after it, as the
     * original table did after every action.
     * @param {?Element} b The action's button.
     * @returns {boolean} True for a declared final action.
     */
    function actionIsFinal(b) {
        return !!b && b.getAttribute("data-sw-final") === "true";
    }

    /** @type {Set<string>} The unknown cell format names already warned about (cellFormat). */
    const WarnedFormats = new Set();

    /**
     * The format a cell is rendered with (Sw.Cells.Update):
     * - an unknown format name (not in Sw.Cells.FormatFormatters, ex: a custom "Stars;5" whose renderer was not registered on this page)
     *   renders the plain value ("Default"), with one console warning per name; without this Sw.Cells used the renderer of the C# type,
     *   and a number read the ";5" as 5 decimals ("2.00000"). The DateTime conversions "utc" / "local" are not format names.
     * @param {?string} fmt The column format.
     * @returns {?string} The format to render with.
     */
    function cellFormat(fmt) {
        if (!fmt)
            return fmt;
        const p = String(fmt).split(";");
        const F = Sw.Cells && Sw.Cells.FormatFormatters;
        if (F && !F.has(p[0]) && p[0] !== "utc" && p[0] !== "local") {
            if (!WarnedFormats.has(p[0])) {
                WarnedFormats.add(p[0]);
                console.warn("Sw.Table: unknown cell format \"" + p[0] + "\" (register it with Sw.Cells.FormatFormatters.set before the table renders, see explore/README.md \"Custom cell formats\"); the plain value is shown");
            }
            return "Default";
        }
        //  (a "Text" cap, ex: "Text;30", cuts the value at that length, as the original)
        return fmt;
    }

    /**
     * The clock of a time column (the time rule, core/README "Times"): a DateTime column rendered by the DateTime cells shows the
     * viewer's local time (the default, "local") or UTC (the column menu "Show UTC times", or the format "utc").
     * @param {object} c The column.
     * @param {?string} timeMode The table's time mode (State.TimeMode: null = local, "utc").
     * @returns {?{Local: boolean, Text: string}} The hint for the header tool tip, null for other columns.
     */
    function timeZoneHint(c, timeMode) {
        if (c.Type !== "System.DateTime")
            return null;
        const f = c.Format ? String(c.Format).split(";")[0] : (timeMode || "local");
        if (f !== "utc" && f !== "local")
            return null;
        if (f === "utc")
            return { Local: false, Text: _TF("Times in UTC", "Tool tip of a data table time column whose times are shown in UTC") };
        const F = Sw.Format;
        return { Local: true, Text: typeof F.ZoneHint === "function" ? F.ZoneHint() : _TF("Times in your time zone", "Tool tip of a data table time column whose times are shown in the viewer's time zone") };
    }

    /**
     * A row menu with the row's actions: the safe actions first, then the other items, the destructive actions (Danger: Kill, End
     * session, Delete ...) last after a separator, so the item a menu focuses first is never a destructive one (when there is another).
     * @param {Array<object>} acts The row action items (RowActionItems).
     * @param {Array<object|string>} rest The other items.
     * @returns {Array<object|string>} The menu items.
     */
    function safeFirst(acts, rest) {
        const out = acts.filter(a => !a.Danger);
        if (rest.length) {
            if (out.length)
                out.push("-");
            out.push(...rest);
        }
        const danger = acts.filter(a => a.Danger);
        if (danger.length) {
            if (out.length)
                out.push("-");
            out.push(...danger);
        }
        return out;
    }

    /**
     * Is a table the first tab stop of its page (no visible tab stop before it, ex: page header actions or tabs)? Only then (or when
     * the table makes the page header itself: table.html) the page starts in its grid; otherwise the focus stays where the
     * page put it and Tab walks the page in order (P3-A-6).
     * @param {HTMLElement} e The table root.
     * @returns {boolean}
     */
    function firstStop(e) {
        if (!e || !e.isConnected)
            return false;
        const sel = "a[href], button, input:not([type=hidden]), select, textarea, [tabindex]:not([tabindex='-1']), [contenteditable='true']";
        for (const x of document.querySelectorAll(sel)) {
            if (e.contains(x))
                return true;
            if (x.disabled || x.closest("[inert], [hidden], .sw-page-live") || !x.getClientRects().length)
                continue;
            //  A tab stop before the table (ex: the page header's actions, tabs): the page starts there, not in the grid (P3-A-6)
            return false;
        }
        return true;
    }

    /**
     * The keyboard focus is lost: on the page body (or nothing), or on an element that left the page or is hidden.
     * @returns {boolean}
     */
    function focusLost() {
        const ae = document.activeElement;
        return !ae || ae === document.body || ae === document.documentElement || !ae.isConnected || !ae.getClientRects().length;
    }

    /**
     * An overlay (dialog, menu) is open in this page or in the shell around it (the focus is the overlay's then).
     * @returns {boolean}
     */
    function openOverlay() {
        const sel = ".sw-dialog-layer, [role=menu], [role=alertdialog], [role=dialog][aria-modal=true]";
        if (document.querySelector(sel))
            return true;
        try {
            return window.top !== window && !!window.top.document.querySelector(sel);
        }
        catch {
            return false;
        }
    }

    /**
     * The page tables of this page that have the page keys (BindPageKeys): `List` in creation order, `Last` the one used last, `Offs`
     * the Sw.Keys removers (one registration per page).
     * @type {{List: Array<object>, Last: ?object, Offs: Array<function():void>}}
     */
    const PageKeyTables = { List: [], Last: null, Offs: [] };

    /**
     * The table a page key goes to: of the page tables that are shown (connected, visible, not in a hidden tab / inert part), the one
     * that has the focus, else the one used last, else the first. Null when none is shown (the key does nothing).
     * @returns {?object} The table.
     */
    function PageKeyTable() {
        const shown = PageKeyTables.List.filter(t => !t.Dead && t.E.isConnected && t.E.getClientRects().length > 0 && !t.E.closest("[hidden], [inert]"));
        const ae = document.activeElement;
        return shown.find(t => ae && t.E.contains(ae)) || (shown.indexOf(PageKeyTables.Last) >= 0 ? PageKeyTables.Last : null) || shown[0] || null;
    }

    /**
     * Add a page table to the page keys, or remove it (BindPageKeys, Destroy). The keys ("/", "g", "f", "?") are registered once per
     * page and go to PageKeyTable, so two tables on one page (tabs: Users and Pending invitations) never steal each other's keys
     * (before, each table registered its own: the newest won, also while its tab was hidden, with a "registered already" warning).
     * @param {object} t The table.
     * @param {boolean} [remove] Remove it (the keys go when the last one goes).
     */
    function pageKeys(t, remove) {
        const P = PageKeyTables;
        const i = P.List.indexOf(t);
        if (remove) {
            if (i >= 0)
                P.List.splice(i, 1);
            if (P.Last === t)
                P.Last = null;
            if (!P.List.length) {
                P.Offs.forEach(f => f());
                P.Offs = [];
            }
            return;
        }
        if (i < 0)
            P.List.push(t);
        if (P.Offs.length)
            return;
        const K = Sw.Keys;
        const on = fn => ev => {
            const x = PageKeyTable();
            if (x)
                fn(x, ev);
        };
        P.Offs.push(
            K.Add("/", on(x => x.Search.Focus(true)), { Title: _TF("Search the table", "Description of the keyboard shortcut that focuses the search field of a data table") }),
            K.Add("g", on(x => x.FocusGrid()), { Title: _TF("Go to the table cells", "Description of the keyboard shortcut that moves the focus to the cells of a data table") }),
            K.Add("f", on(x => x.FocusFilter(Math.max(0, x.Active.C - 1))), { Title: _TF("Filter the current column", "Description of the keyboard shortcut that focuses the filter value of the current column of a data table") }),
            K.Add("?", on(x => x.ShowKeys()), { Title: _TF("Show the keyboard shortcuts", "Description of the keyboard shortcut that shows a list of all keyboard shortcuts") }));
    }

    /**
     * A row is on the screen: at least a part of it inside the window (and inside the window's frame in the app shell).
     * @param {HTMLElement} tr The row.
     * @returns {boolean}
     */
    function rowOnScreen(tr) {
        const r = tr.getBoundingClientRect();
        return r.bottom > 0 && r.top < window.innerHeight && r.right > 0 && r.left < window.innerWidth;
    }

    /**
     * The state word of a row after a final action, for the range text ("1 row (withdrawn, shown until you refresh)"): from the
     * action's verb (only the wording of the state: whether the action is final is declared, actionIsFinal), "ended" when it is not known.
     * @param {string} action The action text (ex: "Withdraw invitation", "Kill", "End session").
     * @returns {string} The translated state.
     */
    function finalState(action) {
        const a = String(action || "").toLowerCase();
        if (/\bwithdraw/.test(a))
            return _TF("withdrawn", "State of a data table row after the user withdrew it (ex: an invitation), shown in the range text: 1 row (withdrawn, shown until you refresh)");
        if (/\brevoke/.test(a))
            return _TF("revoked", "State of a data table row after the user revoked it (ex: a token), shown in the range text: 1 row (revoked, shown until you refresh)");
        if (/\bcancel/.test(a))
            return _TF("cancelled", "State of a data table row after the user cancelled it, shown in the range text: 1 row (cancelled, shown until you refresh)");
        if (/\bban\b/.test(a))
            return _TF("banned", "State of a data table row after the user banned it, shown in the range text: 1 row (banned, shown until you refresh)");
        if (/\b(delete|erase|purge|wipe|drop|destroy)/.test(a))
            return _TF("deleted", "State of a data table row after the user deleted it, shown in the range text: 1 row (deleted, shown until you refresh)");
        if (/\b(remove|uninstall)/.test(a))
            return _TF("removed", "State of a data table row after the user removed it, shown in the range text: 1 row (removed, shown until you refresh)");
        return _TF("ended", "State of a data table row after the user ended it (a session, a killed process), shown in the range text: 1 row (ended, shown until you refresh)");
    }

    /**
     * The state word of a row the server stopped listing while a row action's confirmation was open (RowGone), for the range text.
     * @returns {string} "no longer listed".
     */
    function goneState() {
        return _TF("no longer listed", "State of a data table row that the server stopped listing while a confirmation on it was open, shown in the range text: 1 row (no longer listed, shown until you refresh)");
    }

    /**
     * A row action's failure that may mean the row's item is gone already (someone else ended / withdrew it): a `false` answer, or a
     * 404 / 409 / 410 (the row action convention: "409 with the reason" for work that can't be done now), or a failure whose status is
     * not known (Sw.Cells passes the reason text only). Not: an aborted request, another known status (403, 5xx), offline.
     * ActionDone checks the list (a quiet re-read: when the row is still listed the failure stands).
     * @param {object} out The outcome ({ Ok: false, Value, Error, Status, Aborted }).
     * @returns {boolean}
     */
    function maybeGone(out) {
        if (!out || out.Ok || out.Aborted || (Sw.Net && Sw.Net.Online === false))
            return false;
        if (out.Value === false)
            return true;
        const e = out.Error;
        const s = out.Status || (e && typeof e === "object" ? (e.Status || e.status || 0) : 0);
        return !s || s === 404 || s === 409 || s === 410;
    }

    /**
     * The outcome kind of a failed row action, as the server said it (Sw.Cells' `sw-cell-result` detail `Outcome`, from the server's
     * `X-Sw-Outcome` through Sw.Net; never guessed from the status code): "AlreadyDone" (the goal was met already, ex: the process had
     * ended), "Gone" (the item is no longer there), "Refused", "Denied", "Invalid", or "" (not known: an older Sw.Cells, a lost
     * connection, a server fault). An outcome with `Final: true` but no kind counts as "AlreadyDone".
     * @param {?object} out The outcome.
     * @returns {string} The kind.
     */
    function outcomeKind(out) {
        if (!out || out.Ok)
            return "";
        if (typeof out.Outcome === "string" && out.Outcome)
            return out.Outcome;
        const e = out.Error;
        if (e && typeof e === "object" && typeof e.Outcome === "string" && e.Outcome)
            return e.Outcome;
        return out.Final === true ? "AlreadyDone" : "";
    }

    /**
     * Does an outcome kind end the row's item (the goal is met or the item is gone: the row is final, its actions are not offered
     * again)?
     * @param {string} kind The kind (outcomeKind).
     * @returns {boolean}
     */
    const endsItem = kind => kind === "AlreadyDone" || kind === "Gone";

    /**
     * A reason as a sentence for the row result texts: trimmed, a status line ("409 - Conflict\n…") dropped, the first letter capital,
     * an end mark added ("process #4711 has already ended" -> "Process #4711 has already ended.").
     * @param {string} s The reason.
     * @returns {string} The sentence.
     */
    function sentence(s) {
        let x = String(s || "").trim();
        //  (an Error message of Sw.Net: "409 - Conflict\n<reason>": the reason)
        const nl = x.indexOf("\n");
        if (nl > 0 && /^\d{3}\b/.test(x))
            x = x.slice(nl + 1).trim();
        if (!x)
            return x;
        x = x.charAt(0).toLocaleUpperCase() + x.slice(1);
        return /[.!?…:)"'»]$/.test(x) ? x : x + ".";
    }

    /**
     * The number of error toasts shown so far on this page (merged ones count each time; ActionDone with an older Sw.Cells that
     * reports no outcome: a row action that failed shows one).
     * @returns {number} The count.
     */
    function toastErrors() {
        let n = 0;
        for (const h of document.querySelectorAll(".sw-toasts"))
            for (const x of h.children)
                if (x.SwToast && x.SwToast.Kind === "error")
                    n += x.SwToast.Count || 1;
        return n;
    }

    /**
     * Move the keyboard focus to an element after a modifier click (UX guide INP-2) and show the focus ring there: the shared helper
     * when the framework has it (Sw.Focus.Show: the class sw-focus-shown where the browser shows no ring), else
     * `focus({ focusVisible: true })` plus the class `sw-table-modfocus` (the same ring, table.css), removed on the next pointer down
     * or when the focus leaves (browsers that ignore the option, and an element that already had the focus from the mouse down).
     * @param {HTMLElement} e The element.
     */
    function showFocus(e) {
        if (!e || !e.isConnected)
            return;
        if (Sw.Focus && typeof Sw.Focus.Show === "function") {
            Sw.Focus.Show(e);
            return;
        }
        if (document.activeElement !== e)
            e.focus({ preventScroll: true, focusVisible: true });
        if (document.activeElement !== e || e.matches(":focus-visible"))
            return;
        e.classList.add("sw-table-modfocus");
        const off = () => {
            e.classList.remove("sw-table-modfocus");
            window.removeEventListener("pointerdown", off, true);
            e.removeEventListener("blur", off);
        };
        window.addEventListener("pointerdown", off, true);
        e.addEventListener("blur", off);
    }

    /**
     * The text a cell copies (Ctrl+C, the cell menu "Copy value"; UX guide INP-31: exactly the value): a number shown as a plain
     * number is copied without the thousands separators ("1234567", not "1 234 567"); anything else as shown (a local time, an enum
     * name, a size with its unit; the cell menu also has "Copy raw value").
     * @param {HTMLElement} td The cell.
     * @param {*} raw The value the server sent (undefined: not known).
     * @returns {string} The text.
     */
    function cellCopyText(td, raw) {
        const text = td.textContent.trim();
        if (typeof raw === "number" && isFinite(raw) && /^[-+−]?[\d\s  .,']+$/.test(text))
            return String(raw);
        return text;
    }

    /**
     * A line icon (LineIcons, else the Sw icon of that name).
     * @param {string} name The icon name.
     * @param {string} [cls] Extra class.
     * @returns {Element} The svg element (class sw-icon).
     */
    function lineIcon(name, cls) {
        if (!LineIcons[name])
            return Sw.Icon(name, cls);
        const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("class", "sw-icon sw-table-licon" + (cls ? " " + cls : ""));
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        svg.innerHTML = LineIcons[name];
        return svg;
    }

    /**
     * A ghost icon button of the table tool bar / footer with a line icon (the tool tip is the accessible name).
     * @param {string} icon Icon name (LineIcons or Sw.Icons).
     * @param {string} title Tool tip / accessible name.
     * @param {function(object, Event):*} onClick Click handler.
     * @param {string} [cls] Extra class.
     * @returns {object} The Sw.Button.
     */
    function toolButton(icon, title, onClick, cls) {
        const b = new Sw.Button({ Title: title, Kind: "ghost", Class: "sw-table-tbtn" + (cls ? " " + cls : ""), OnClick: onClick, Label: Sw.ActionName(title) || title });
        b.E.classList.add("sw-icon-only");
        b.E.append(lineIcon(icon));
        if (Sw.Hint)
            Sw.Hint.Attach(b.E);
        return b;
    }

    /**
     * The density of the site (html data-density, set by core/theme.js; the default follows the device: touch on coarse pointers).
     * @returns {string} "compact", "comfortable" or "touch".
     */
    function currentDensity() {
        const d = document.documentElement.getAttribute("data-density");
        if (d === "compact" || d === "comfortable" || d === "touch")
            return d;
        return window.matchMedia && matchMedia("(pointer: coarse)").matches ? "touch" : "comfortable";
    }

    /**
     * Change the density of the site: through core/theme.js when it has the api (it stores and forwards it to every frame), else on
     * this page (html data-density) and in the same storage key, so the shell and the other pages pick it up when they load.
     * @param {string} d "compact", "comfortable" or "touch".
     */
    function setDensity(d) {
        const T = Sw.Theme;
        if (T && typeof T.SetDensity === "function") {
            T.SetDensity(d);
            return;
        }
        document.documentElement.setAttribute("data-density", d);
        Sw.Storage.Local.Set(DensityKey, d);
    }

    /** @type {number} The last table id (Table.Tid). */
    let TableIds = 0;

    /** @type {RegExp} Error texts of a server that could not reach another system it depends on (a provider: a web api, a database). */
    const ProviderErrorRx = /No connection could be made|actively refused|Retry failed|could not be resolved|No such host|Name or service not known|SocketException|HttpRequestException|Connection refused|connection was (closed|reset)|SSL connection|certificate|\b(502|503|504)\b/i;

    /**
     * A plain words summary of a failed table data request for the error state (the technical text is shown behind "Technical
     * details"), written for the people who run the server (ops): what failed (a provider the server depends on, named by its host
     * when the technical text has one; no answer in time; no connection; not found; another server error) and where to look (the server
     * log: the Log page or the Log file viewer, which has the failed request), then what to do.
     * @param {string} msg The error text.
     * @returns {string} The summary (two or three sentences).
     */
    function ErrorSummary(msg) {
        const s = String(msg || "");
        const log = _TF("The service log (Log, or the Log file viewer) has the details of the failure.", "Sentence of the plain words error of a data table that says where to find the cause (the service log pages)");
        if (ProviderErrorRx.test(s)) {
            const m = /https?:\/\/([^\/\s)"'<>]+)/i.exec(s) || /\(([\w.-]+:\d{2,5})\)/.exec(s);
            const what = m
                ? _T("The service could not reach {0}, a service it depends on.", m[1], "Plain words error of a data table when the service could not reach another system it depends on (ex: an AI or exchange rate web service), {0} is its host name")
                : _TF("The service could not reach another service it depends on (its provider).", "Plain words error of a data table when the service could not reach another system it depends on (ex: an AI or exchange rate web service)");
            return what + " " + _TF("Check that the provider is running and can be reached from the server (address, network, credentials).", "Sentence of the plain words error of a data table when a provider could not be reached: what to check") + " " + log;
        }
        let what;
        if (/did not answer within|timed? ?out/i.test(s))
            what = _TF("The service did not answer in time.", "Plain words error of a data table when the data request timed out");
        else if (/Failed to fetch|NetworkError|Load failed/i.test(s))
            return _TF("The service could not be reached.", "Plain words error of a data table when the browser could not connect to the service") + " " +
                _TF("Check the network connection and that the service is running, then try again.", "What to do when a data table could not connect to the service");
        else if (/^\s*404\b/.test(s))
            what = _TF("This table was not found in the service.", "Plain words error of a data table when the table api does not exist (404)");
        else
            what = _TF("The data could not be loaded.", "Plain words error of a data table when loading the data failed for another reason");
        return what + " " + _TF("Try again. If it keeps failing:", "Start of the what to do sentence of the plain words error of a data table (the place to look follows)") + " " + log;
    }

    // ------------------------------------------------------------------ local data (rows given by the page)

    /** @type {RegExp} Number column types (local data: numeric compare and filters). */
    const NumTypes = /^System\.(Int16|Int32|Int64|UInt16|UInt32|UInt64|Byte|SByte|Single|Double|Decimal|Half)$/;

    /**
     * The rows of a table given by the page (option `Rows`, no table api): the same TableData replies, made in the browser. Sort, filters
     * (the same operations, all must match), search (every word in some value, case insensitive) and paging run on the array; the
     * reply's RowCount is the exact total.
     */
    class LocalData {
        /**
         * @param {Array<object|Array>} rows The rows: objects (values by column name) or arrays (values in column order).
         * @param {Array<object>} [cols] The columns ({Name, Title, Desc, Type, Format, Props}); default: from the first row (the
         * property names; the C# type from the JavaScript value: number, boolean, Date / ISO time text, else text).
         * @param {string} title The table title.
         */
        constructor(rows, cols, title) {
            /** @type {string} The table title (TableData.Title). */
            this.Title = title || "";
            /** @type {number} Change counter (SetRows). */
            this.Cc = 1;
            /** @type {Array<object>} The TableData columns. */
            this.Cols = LocalData.MakeCols(rows, cols);
            this.SetRows(rows);
        }

        /**
         * The columns of local rows: the given ones completed (Title = the name in words, Type = text, Props: sort and the filters of
         * the type), else made from the first row.
         * @param {Array} rows The rows.
         * @param {Array<object>} [cols] The given columns.
         * @returns {Array<object>} TableData columns.
         */
        static MakeCols(rows, cols) {
            const first = rows && rows.length ? rows[0] : null;
            if (!Array.isArray(cols) || !cols.length) {
                const names = Array.isArray(first) ? first.map((x, i) => "Column" + (i + 1)) : (first && typeof first === "object" ? Object.keys(first) : []);
                cols = names.map((n, i) => {
                    const v = Array.isArray(first) ? first[i] : first[n];
                    let type = "System.String";
                    if (typeof v === "number")
                        type = Number.isInteger(v) ? "System.Int64" : "System.Double";
                    else if (typeof v === "boolean")
                        type = "System.Boolean";
                    else if (v instanceof Date || (typeof v === "string" && /^\d{4}-\d\d-\d\dT\d\d:\d\d/.test(v)))
                        type = "System.DateTime";
                    return { Name: n, Type: type };
                });
            }
            return cols.map(c => {
                const type = c.Type || "System.String";
                const ord = NumTypes.test(type) || /^System\.(DateTime|DateTimeOffset|DateOnly|TimeOnly|TimeSpan)$/.test(type);
                const def = Props.CanSort | Props.Filter | (type === "System.String" ? Props.TextFilter : 0) | (ord ? Props.OrderFilter : 0);
                return {
                    Name: String(c.Name), Title: c.Title || Sw.Format.RemoveCamelCase(String(c.Name)), Desc: c.Desc || null, Type: type,
                    Format: c.Format || null, Props: (typeof c.Props === "number" ? c.Props : def) & ~Props.CanChart,
                };
            });
        }

        /**
         * Replace the rows (values as arrays in column order; Date values as ISO text, as the server sends them).
         * @param {Array<object|Array>} rows The rows.
         */
        SetRows(rows) {
            const cols = this.Cols;
            /** @type {Array<Array>} The rows as value arrays. */
            this.Rows = (rows || []).map(r => cols.map((c, i) => {
                const v = Array.isArray(r) ? r[i] : (r ? r[c.Name] : undefined);
                return v instanceof Date ? v.toISOString() : v;
            }));
            ++this.Cc;
        }

        /**
         * A value as the filters, search and sort see it.
         * @param {*} v The value.
         * @param {object} c The column.
         * @returns {*} A number (numbers, times), else the text; null for no value.
         */
        static Key(v, c) {
            if (v === null || v === undefined || v === "")
                return null;
            if (NumTypes.test(c.Type))
                return isNaN(+v) ? String(v) : +v;
            if (/^System\.(DateTime|DateTimeOffset|DateOnly)$/.test(c.Type)) {
                const t = Date.parse(v);
                return isNaN(t) ? String(v) : t;
            }
            return String(v);
        }

        /**
         * Test one filter on a value.
         * @param {object} f The filter (Op, Value, Invert, CaseSensitive).
         * @param {*} v The value.
         * @param {object} c The column.
         * @returns {boolean} True if the row passes.
         */
        static Match(f, v, c) {
            const k = LocalData.Key(v, c);
            const num = typeof k === "number";
            const low = s => f.CaseSensitive ? String(s) : String(s).toLowerCase();
            const fv = s => num ? LocalData.Key(String(s).trim(), c) : low(String(s).trim());
            const text = v === null || v === undefined ? "" : low(v);
            const val = num ? k : text;
            const list = String(f.Value).split(",").map(fv);
            const a = list[0];
            const lo = list.length > 1 && list[1] < list[0] ? list[1] : a;
            const hi = list.length > 1 ? (list[1] < list[0] ? list[0] : list[1]) : a;
            const tv = low(f.Value);
            let ok;
            switch (f.Op) {
                case Ops.Equals: ok = val === a; break;
                case Ops.NotEqual: ok = val !== a; break;
                case Ops.LessThan: ok = k !== null && val < a; break;
                case Ops.GreaterThan: ok = k !== null && val > a; break;
                case Ops.LessEqual: ok = k !== null && val <= a; break;
                case Ops.GreaterEqual: ok = k !== null && val >= a; break;
                case Ops.StartsWith: ok = text.startsWith(tv); break;
                case Ops.EndsWith: ok = text.endsWith(tv); break;
                case Ops.AnyOf: ok = list.some(x => x === val); break;
                case Ops.NoneOf: ok = !list.some(x => x === val); break;
                case Ops.InRange: ok = k !== null && val >= lo && val < hi; break;
                case Ops.OutsideRange: ok = k !== null && (val < lo || val > hi); break;
                default: ok = text.indexOf(tv) >= 0; break;
            }
            return f.Invert ? !ok : ok;
        }

        /**
         * Answer a TableDataRequest.
         * @param {object} r The request (Row, MaxRowCount, Order, Filters, SearchText).
         * @param {boolean} [all] All matching rows (no paging).
         * @returns {object} TableData {Cols, Rows: [{Values}], RowCount (the exact total), Title, Cc, Exact}.
         */
        Query(r, all) {
            const cols = this.Cols;
            const at = n => cols.findIndex(c => c.Name === n);
            let rows = this.Rows;
            const fl = (r.Filters || []).map(f => ({ F: f, I: at(f.ColName) })).filter(x => x.I >= 0 && x.F.Value !== "");
            const words = String(r.SearchText || "").trim().toLowerCase().split(/\s+/).filter(x => x);
            if (fl.length || words.length)
                rows = rows.filter(row => fl.every(x => LocalData.Match(x.F, row[x.I], cols[x.I])) &&
                    words.every(w => row.some(v => v !== null && v !== undefined && String(v).toLowerCase().indexOf(w) >= 0)));
            const order = (r.Order || []).map(o => ({ I: at(String(o).replace(/^-/, "")), D: String(o).startsWith("-") ? -1 : 1 })).filter(x => x.I >= 0);
            if (order.length) {
                const cmp = (a, b, c) => {
                    const x = LocalData.Key(a, c), y = LocalData.Key(b, c);
                    if (x === null || y === null)
                        return x === y ? 0 : (x === null ? 1 : -1);
                    if (typeof x === "number" && typeof y === "number")
                        return x - y;
                    return String(x).localeCompare(String(y), undefined, { numeric: true, sensitivity: "base" });
                };
                rows = rows.map((v, i) => ({ V: v, I: i })).sort((a, b) => {
                    for (const o of order) {
                        const d = cmp(a.V[o.I], b.V[o.I], cols[o.I]);
                        if (d)
                            return d * o.D;
                    }
                    return a.I - b.I;
                }).map(x => x.V);
            }
            const from = all ? 0 : Math.max(0, r.Row || 0);
            const page = all ? rows : rows.slice(from, from + Math.max(1, r.MaxRowCount || 25));
            return { Cols: cols, Rows: page.map(v => ({ Values: v })), RowCount: rows.length, Title: this.Title, Cc: this.Cc, Exact: true };
        }

        /**
         * All rows of the current search, filters and sort as CSV (the raw values; the Download of local data).
         * @param {object} r The request.
         * @returns {string} The CSV text (comma separated, quoted when needed, the first line has the column titles).
         */
        Csv(r) {
            const q = v => {
                const s = v === null || v === undefined ? "" : String(v);
                return /[",\r\n]/.test(s) ? "\"" + s.replace(/"/g, "\"\"") + "\"" : s;
            };
            return [this.Cols.map(c => q(c.Title || c.Name)).join(",")]
                .concat(this.Query(r, true).Rows.map(x => x.Values.map(q).join(","))).join("\r\n");
        }
    }

    // ------------------------------------------------------------------ card templates

    /** @type {RegExp} A variable of a card template: `${Name}`, with optional modifiers before the name. */
    const CardVarRx = /\$\{([^{}]*)\}/g;

    /** @type {string} Elements a card template may not contain (removed when it is compiled): a crafted `card=` link must not run script. */
    const CardBlocked = "script,object,embed,applet,iframe,frame,frameset,base,meta,link";

    /**
     * Is this the type of a date column (also a nullable one)?
     * @param {string} type The type name of the column.
     * @returns {boolean} True for a date.
     */
    function isDateType(type) {
        return type === "System.DateTime" || (type || "").startsWith("System.Nullable`1[[System.DateTime,");
    }

    /** @type {Set<string>} File types without an icon (CardTemplate.Ext). */
    const CardNoExt = new Set();

    /** @type {string[]} The grades of CardTemplate.Level, the worst last. */
    const CardLevels = ["ok", "warn", "danger"];

    /** @type {RegExp} Attributes that hold a url: a value with a variable is checked when it is rendered (cardUrlOk). */
    const CardUrlAttr = /^(?:href|src|action|formaction|data|poster|ping|background|xlink:href)$/i;

    /**
     * Test the url of an attribute (a script url is never rendered, also not from a row value).
     * @param {string} v The url.
     * @returns {boolean} True if the attribute may have this value.
     */
    function cardUrlOk(v) {
        const s = String(v).replace(/[\u0000- ]+/g, "").toLowerCase();
        return !/^(?:javascript|vbscript):/.test(s) && (!s.startsWith("data:") || s.startsWith("data:image/"));
    }

    /**
     * An error of a card template (a missing file or template, a bad `name@path`): permanent, it is shown as is and not retried by
     * itself ("Try again" does).
     * @param {string} text The message.
     * @returns {Error} The error.
     */
    function cardError(text) {
        const e = new Error(text);
        e.SwCardError = true;
        return e;
    }

    /**
     * Parse a card selection ("name@path").
     * @param {string} card The selection. The name may start with "-" (cards only, no table view) or "+" (the table view is the default).
     * @returns {{Name: string, Path: string, Mode: string}} Mode is "cards" (the default view), "table" ("+") or "only" ("-").
     */
    function parseCard(card) {
        const s = String(card).trim();
        const at = s.indexOf("@");
        let name = at > 0 ? s.substring(0, at) : "";
        const path = at > 0 ? s.substring(at + 1).trim() : "";
        const c = name.charAt(0);
        const mode = c === "-" ? "only" : c === "+" ? "table" : "cards";
        if (mode !== "cards")
            name = name.substring(1);
        if (!name || !path)
            throw cardError(_T('The card "{0}" is not valid: it must be written as name@path.', s, "Error shown instead of a data table when its card template selection has the wrong format, {0} is the selection"));
        return { Name: name, Path: path, Mode: mode };
    }

    /** @type {Map<string, Promise<Document>>} The loaded card files by url. */
    const CardFiles = new Map();

    /**
     * Load a card file once: the html file (required) and the css file next to it (optional, errors are ignored). Only files of this
     * site are loaded, the path may come from a link.
     * @param {string} path The path (Sw.Url.SiteUrl rule). ".html" is added unless it ends with .html or .htm.
     * @returns {Promise<Document>} The parsed (inert) html.
     */
    function loadCardFile(path) {
        const abs = Sw.Url.SafeSameOrigin(Sw.Url.SiteUrl(path));
        if (!abs)
            return Promise.reject(cardError(_T('The card file "{0}" cannot be used: it must be on this site.', path, "Error shown instead of a data table when its card template file is on another web site (not allowed), {0} is the path")));
        const u = new URL(abs);
        if (!/\.html?$/i.test(u.pathname))
            u.pathname += ".html";
        const key = u.href;
        let p = CardFiles.get(key);
        if (p)
            return p;
        p = (async () => {
            let res;
            try {
                res = await fetch(key, { credentials: "same-origin" });
            }
            catch (e) {
                throw cardError(_T('The card file "{0}" could not be loaded: {1}', u.pathname, e && e.message ? e.message : String(e), "Error shown instead of a data table when its card template file could not be loaded, {0} is the path, {1} the reason"));
            }
            if (!res.ok)
                throw cardError(_T('The card file "{0}" could not be loaded: {1}', u.pathname, String(res.status), "Error shown instead of a data table when its card template file could not be loaded, {0} is the path, {1} the reason"));
            const doc = new DOMParser().parseFromString(await res.text(), "text/html");
            //  (a file without a style sheet can say so: <meta name="sw-card-css" content="none">, no request and no 404 in the console)
            if (doc.querySelector('meta[name="sw-card-css"][content="none"]'))
                return doc;
            //  The optional style sheet: waited for (so the first cards are styled), a missing one is ignored
            const css = new URL(u.href);
            css.pathname = css.pathname.replace(/\.html?$/i, ".css");
            await new Promise(done => {
                const link = el("link", { rel: "stylesheet", href: css.href });
                const timer = setTimeout(done, 3000);
                const end = () => {
                    clearTimeout(timer);
                    done();
                };
                link.addEventListener("load", end);
                link.addEventListener("error", () => {
                    link.remove();
                    end();
                });
                document.head.append(link);
            });
            return doc;
        })();
        CardFiles.set(key, p);
        //  (a failed load may work the next time: "Try again")
        p.catch(() => CardFiles.delete(key));
        return p;
    }

    /**
     * A compiled card template: the html of one item, with the variables replaced by slots, so a card is a fast clone of the prepared
     * nodes plus its slots. A variable is `${Name}`, in any attribute value or element text; Name is a column name. Before the name, like
     * C# TextTemplate, one format and one encoder may be given (in that order, ex: `${_#Name}`):
     * formats `_` lower case, `^` upper case, `~` remove camel case, `+` the column's cell format (Sw.Cells: links, sizes, times, row
     * actions); encoders `@` html attribute, `#` html value, `%` url, `-` the value is markdown (sanitized, Sw.Markdown).
     *
     * The slots are filled through the DOM, so a value can't add markup: `@`, `#` and a variable without an encoder all show the value
     * as text. Only `+` and `-` add elements, made by the cell renderer and the markdown sanitizer. A variable with an unknown column stays
     * as written (as in C#), with a developer warning.
     */
    class CardTemplate {
        /**
         * @param {HTMLTemplateElement} tpl The template element (of an inert document).
         * @param {string} name The name.
         */
        constructor(tpl, name) {
            /** @type {string} The name of the template. */
            this.Name = name;
            /** @type {DocumentFragment} The prepared nodes, without the variables. */
            this.Proto = tpl.content.cloneNode(true);
            /** @type {Array<object>} What to fill: { Path, Var } text, or { Path, Attr, Ns, Parts } an attribute. */
            this.Slots = [];
            /** @type {?string} The column signature the variables are bound to (Bind). */
            this.Sig = null;
            /** @type {boolean} True when an element has data-level or data-days (Level). */
            this.Levels = false;
            this.Compile(this.Proto, []);
        }

        /**
         * Grade the value of an element, so a card can react to a number or a date without code:
         * data-level="${Used}" data-warn="80" data-danger="92" (high is bad; low is bad when danger is below warn), or
         * data-days="${To}" data-warn="30" data-danger="7" (a date: the days left until it).
         * A meter (.sw-meter) gets its is-warn / is-danger class; the element and its card get data-sw-level (ok, warn, danger; a card keeps
         * its worst), which shows the .sw-when-ok / -warn / -danger parts. A value that isn't a number or a date grades nothing.
         * @param {Element} e The element.
         */
        static Level(e) {
            const d = e.dataset;
            const days = d.days !== undefined;
            const v = days ? (Date.parse(d.days) - Date.now()) / 864e5 : parseFloat(d.level);
            if (isNaN(v))
                return;
            const warn = parseFloat(d.warn);
            const danger = parseFloat(d.danger);
            const low = days || danger < warn;
            const hit = t => !isNaN(t) && (low ? v <= t : v >= t);
            const level = hit(danger) ? "danger" : hit(warn) ? "warn" : "ok";
            e.setAttribute("data-sw-level", level);
            if (e.classList.contains("sw-meter")) {
                e.classList.toggle("is-warn", level === "warn");
                e.classList.toggle("is-danger", level === "danger");
            }
            const card = e.closest(".sw-card");
            if (card && card !== e) {
                const was = CardLevels.indexOf(card.getAttribute("data-sw-level"));
                if (CardLevels.indexOf(level) > was)
                    card.setAttribute("data-sw-level", level);
            }
        }

        /**
         * The file type icon of a value: <span class="sw-card-tile" data-sw-ext="${Name}" data-sw-fallback="fileKey"></span>. The extension is taken
         * from the end of the first word of the value ("Auth.key", ".gz .gzip", "json"). A type without an icon shows the fallback icon, and is
         * asked for only once per page.
         * @param {Element} e The element.
         */
        static Ext(e) {
            const m = /(?:^|\.)([a-z0-9_+-]{1,16})$/.exec(String(e.getAttribute("data-sw-ext") || "").trim().split(/[\s,]+/)[0].toLowerCase());
            const fallback = () => {
                const icon = Sw.Icon(e.getAttribute("data-sw-fallback") || "file");
                icon.setAttribute("aria-hidden", "true");
                e.replaceChildren(icon);
            };
            if (!m || CardNoExt.has(m[1])) {
                fallback();
                return;
            }
            const img = el("img", { attrs: { alt: "", src: "../icons/ext/" + m[1] + ".svg" } });
            img.addEventListener("error", () => {
                CardNoExt.add(m[1]);
                fallback();
            });
            e.replaceChildren(img);
        }

        /**
         * The days left until a date, as a plain number: <dd data-sw-days-left="${To}"></dd>. The whole number of days, rounded up and at least 0.
         * A missing or invalid date leaves the element empty (the card css shows a dash).
         * @param {Element} e The element.
         */
        static DaysLeft(e) {
            const t = Date.parse(e.getAttribute("data-sw-days-left"));
            if (isNaN(t))
                return;
            const days = Math.max(0, Math.ceil((t - Date.now()) / 864e5));
            e.textContent = Sw.Format && typeof Sw.Format.Number === "function" ? Sw.Format.Number(days, 0, " ") : String(days);
        }

        /**
         * Show the part that fits a value: <span data-sw-match="${Status}"><span data-when="Running,Started" class="sw-badge sw-ok">Running</span>...</span>.
         * The children whose data-when names the value stay (a data-when="*" child when none does), so a state can have its own word, icon and colour.
         * @param {Element} e The element.
         */
        static Match(e) {
            const v = e.getAttribute("data-sw-match");
            const kids = Array.from(e.children);
            const hit = k => (k.getAttribute("data-when") || "").split(",").includes(v);
            const any = kids.some(hit);
            for (const k of kids)
                if (any ? !hit(k) : k.getAttribute("data-when") !== "*")
                    k.remove();
        }

        /**
         * Parse the text of a variable.
         * @param {string} key The text between the braces.
         * @returns {{Key: string, Name: string, Format: string, Encoder: string, Col: ?object, Index: number}} The variable.
         */
        static Parse(key) {
            let i = 0;
            let f = "";
            let e = "";
            if (key && "_^~+".includes(key.charAt(0)))
                f = key.charAt(i++);
            if (i < key.length && "@#%-".includes(key.charAt(i)))
                e = key.charAt(i++);
            return { Key: key, Name: key.substring(i), Format: f, Encoder: e, Col: null, Index: -1 };
        }

        /**
         * Split a text at its variables.
         * @param {string} text The text.
         * @returns {Array<string|object>} The static texts and the variables, in order.
         */
        static Split(text) {
            const parts = [];
            let last = 0;
            for (const m of text.matchAll(CardVarRx)) {
                if (m.index > last)
                    parts.push(text.substring(last, m.index));
                parts.push(CardTemplate.Parse(m[1]));
                last = m.index + m[0].length;
            }
            if (last < text.length)
                parts.push(text.substring(last));
            return parts;
        }

        /**
         * Prepare the children of a node: remove what may not run, turn the variables into slots.
         * @param {Node} node The node.
         * @param {number[]} path The child indices from the root to the node.
         */
        Compile(node, path) {
            for (let i = 0; i < node.childNodes.length; ++i) {
                const c = node.childNodes[i];
                if (c.nodeType === 1) {
                    if (c.matches(CardBlocked)) {
                        c.remove();
                        --i;
                        continue;
                    }
                    //  <i data-sw-icon="drive"></i>: an icon of the framework's set, made once here
                    if (c.hasAttribute("data-sw-icon")) {
                        const icon = Sw.Icon(c.getAttribute("data-sw-icon"));
                        icon.setAttribute("aria-hidden", "true");
                        c.replaceWith(icon);
                        continue;
                    }
                    const at = path.concat(i);
                    if (c.matches("[data-level],[data-days],[data-sw-match],[data-sw-ext],[data-sw-days-left]"))
                        this.Levels = true;
                    for (const a of Array.from(c.attributes)) {
                        const n = a.name.toLowerCase();
                        if (n.startsWith("on") || n === "srcdoc") {
                            c.removeAttributeNode(a);
                            continue;
                        }
                        const parts = a.value.indexOf("${") >= 0 ? CardTemplate.Split(a.value) : null;
                        if (parts && parts.some(x => typeof x !== "string")) {
                            this.Slots.push({ Path: at, Attr: a.name, Ns: a.namespaceURI, Parts: parts, Url: CardUrlAttr.test(a.name) });
                            //  (never in the prepared nodes: an image must not load "${Url}")
                            c.removeAttributeNode(a);
                        }
                        else if (CardUrlAttr.test(a.name) && !cardUrlOk(a.value))
                            c.removeAttributeNode(a);
                    }
                    if (!c.matches("template,style"))
                        this.Compile(c, at);
                    continue;
                }
                if (c.nodeType !== 3 || c.nodeValue.indexOf("${") < 0)
                    continue;
                const parts = CardTemplate.Split(c.nodeValue);
                if (!parts.some(x => typeof x !== "string"))
                    continue;
                //  One text node per part; the empty ones of the variables are cloned too, so the paths hold
                const nodes = parts.map(x => document.createTextNode(typeof x === "string" ? x : ""));
                c.replaceWith(...nodes);
                parts.forEach((x, k) => {
                    if (typeof x !== "string")
                        this.Slots.push({ Path: path.concat(i + k), Var: x });
                });
                i += parts.length - 1;
            }
        }

        /**
         * All variables of the template.
         * @returns {Array<object>} The variables.
         */
        Vars() {
            const res = [];
            for (const s of this.Slots) {
                if (s.Var)
                    res.push(s.Var);
                else
                    s.Parts.forEach(x => {
                        if (typeof x !== "string")
                            res.push(x);
                    });
            }
            return res;
        }

        /**
         * Bind the variables to the columns (once per column set): by name, else ignoring the case; a column whose name starts with a
         * modifier character is found by the whole text.
         * @param {Array<object>} cols The columns.
         * @param {string} sig The column signature.
         */
        Bind(cols, sig) {
            if (this.Sig === sig)
                return;
            this.Sig = sig;
            const unknown = [];
            for (const v of this.Vars()) {
                const low = v.Name.toLowerCase();
                let i = cols.findIndex(c => c.Name === v.Key);
                if (i >= 0) {
                    v.Format = "";
                    v.Encoder = "";
                }
                else {
                    i = cols.findIndex(c => c.Name === v.Name);
                    if (i < 0)
                        i = cols.findIndex(c => String(c.Name).toLowerCase() === low);
                }
                v.Index = i;
                v.Col = i >= 0 ? cols[i] : null;
                if (i < 0)
                    unknown.push(v.Key);
            }
            if (unknown.length && Sw.DevWarn)
                Sw.DevWarn("Sw.Table: the card template \"" + this.Name + "\" uses unknown columns: " + unknown.join(", ") + ". They are shown as written. Columns: " + cols.map(c => c.Name).join(", "), "table:card:" + this.Name + ":" + unknown.join(","));
        }

        /**
         * The text of a variable for a row.
         * @param {object} v The variable.
         * @param {object} x The render values: { Row, Get, Cell }.
         * @returns {string} The text.
         */
        static Text(v, x) {
            if (!v.Col)
                return "${" + v.Key + "}";
            let s;
            if (v.Format === "+")
                s = x.Cell(v).textContent;
            else {
                const raw = x.Get(x.Row, v.Index);
                s = raw === null || raw === undefined ? "" : typeof raw === "object" ? JSON.stringify(raw) : String(raw);
                if (v.Format === "_")
                    s = s.toLowerCase();
                else if (v.Format === "^")
                    s = s.toUpperCase();
                else if (v.Format === "~")
                    s = Sw.Format.RemoveCamelCase(s);
            }
            return v.Encoder === "%" ? encodeURIComponent(s).replace(/%20/g, "+") : s;
        }

        /**
         * Make the nodes of one item.
         * @param {object} row The row.
         * @param {function(object, number): *} get The value of a column index.
         * @param {function(object, object): HTMLElement} cell Render a `+` variable (the variable, the row).
         * @returns {DocumentFragment} The nodes.
         */
        Render(row, get, cell) {
            const frag = this.Proto.cloneNode(true);
            const x = { Row: row, Get: get, Cell: v => cell(v, row) };
            //  (the nodes first: a slot may replace its text node with elements, the later paths must still hold)
            const nodes = this.Slots.map(s => {
                let n = frag;
                for (const i of s.Path)
                    n = n.childNodes[i];
                return n;
            });
            this.Slots.forEach((s, i) => {
                const n = nodes[i];
                //  A part that needs a column this table may not send (ex: the actions only an admin gets) goes away with it
                if (s.Var && !s.Var.Col && n.parentNode && n.parentNode.closest) {
                    const opt = n.parentNode.closest("[data-sw-optional]");
                    if (opt) {
                        opt.remove();
                        return;
                    }
                }
                if (s.Parts) {
                    const value = s.Parts.map(p => typeof p === "string" ? p : CardTemplate.Text(p, x)).join("");
                    if (!s.Url || cardUrlOk(value)) {
                        if (s.Ns)
                            n.setAttributeNS(s.Ns, s.Attr, value);
                        else
                            n.setAttribute(s.Attr, value);
                    }
                    return;
                }
                const v = s.Var;
                const textOnly = n.parentNode && n.parentNode.nodeType === 1 && n.parentNode.matches("textarea,option,title");
                if (v.Col && v.Encoder === "-" && !textOnly) {
                    const md = el("div.sw-card-md");
                    const text = CardTemplate.Text(v, x);
                    md.textContent = text;
                    n.replaceWith(md);
                    Sw.Markdown.ToHtml(text).then(html => md.innerHTML = html, e => console.warn("Card markdown: " + e));
                }
                else if (v.Col && v.Format === "+" && !v.Encoder && !textOnly) {
                    const c = x.Cell(v);
                    //  (an empty value, and a date that isn't set, "-", look the same: nothing)
                    if ((c.textContent.trim() && c.textContent.trim() !== "-") || c.querySelector("img, svg, button, a"))
                        n.replaceWith(c);
                    else
                        n.nodeValue = "";
                }
                else
                    n.nodeValue = CardTemplate.Text(v, x);
            });
            if (this.Levels) {
                frag.querySelectorAll("[data-level],[data-days]").forEach(CardTemplate.Level);
                frag.querySelectorAll("[data-sw-match]").forEach(CardTemplate.Match);
                frag.querySelectorAll("[data-sw-ext]").forEach(CardTemplate.Ext);
                frag.querySelectorAll("[data-sw-days-left]").forEach(CardTemplate.DaysLeft);
            }
            return frag;
        }
    }

    // ------------------------------------------------------------------ the table

    /**
     * A live data table bound to a table api. Create with `await Sw.Table.Create(options)`.
     *
     * Options: `Url` (api url relative to the page; required unless `Rows` is given), `Rows` (local data: the rows as objects or value
     * arrays, sorted / filtered / searched / paged in the browser with the same ui, no api; `Cols` the columns {Name, Title, Desc, Type,
     * Format, Props}, default from the first row; Download saves a CSV; `SetRows(rows)` replaces them), `Element` (container, default body), `Param` (RequestParams.Param),
     * `KeyParam` (when truthy, folded into the saved state key so this value keeps its own filter / sort state: key =
     * `HashString(Url + "\0" + KeyParam)`; absent: `HashString(Url)`, all params of the endpoint share one state; table.html passes
     * the `?uniqueParam=` url param), `Name` (title for exports / charts; default the server Title or the url's last segment), `State` (TableDataState json used
     * instead of the stored state), `Filter` (TableDataFilter json, one or an array, replaces the filters), `Order` (default sort order
     * on the first open, ex: ["-Id"]; none by default, like the original table), `OnFirstLoad()` (after the
     * first response or error), `OnFirstData(data, table)`, `OnCount({ Count, Shown, First, More, Kept }, table)` (each time the counts
     * change, also after a row action: for tab badges; the same as the bubbling `sw-table-count` event), `Page` (true: fill the window, used by table.html), `Header` (the compact
     * header above the frame with the title and the description / note; default: true for page tables, false for embedded ones; an
     * embedded table gets an h2 title), `Fill` (embedded tables: take the free height of the container, so the footer is at the bottom
     * of it like on a page table; default: true when the container `Element` has the class `sw-fill` (ui/page.css fill pages) or
     * `sw-table-fill`; false: the frame takes the height of its rows, the footer follows the last row and stays sticky at the bottom of
     * the window), `EmptyTitle` / `EmptyText` (the empty state of the unfiltered table, ex: "No messages yet" / "Simulated texts show
     * up here."; default "No rows to show" / "There is nothing in <table name> right now."; the filtered empty state is always "No
     * matching rows" with "Clear search and filters"), `ColumnLines` (true: subtle rules between the columns; default: row hairlines
     * only), `RowActions` ("always", the default and only mode, kept for compatibility: the row action buttons of an Actions column show
     * on every row, quiet at rest; UX guide LST-2), `CardTitle` (the column name of the card title and of the row in action messages; default: TitleCol), `MenuName` (a promise of the page header title, table.html: the menu item name; the
     * title stays hidden until it resolves, so it never changes after the first frame), `Scroll` (true: infinite scrolling instead of pages, the rows load on demand with half a
     * screen ready below the view; "Current view" of an export is the rows in view), `Card` ("name@path": a card template, explore/README "Card templates"; default TableData.Card; false: none), `AutoHideEmpty` (true: a grid wider than its area hides
     * the columns that are empty on the shown rows; default false: every column is shown, like the original table), `HostFit` (embedded
     * tables in a host with a fixed height: default on when the frame overflows the host; true: the host has a fixed height, keep the
     * table in it from the first fit, so "Auto" rows fill the host also when the table sits far down a long page; false: never).
     *
     * Members: `E` (root element), `State`, `Data` (last TableData), `Cols`, `UpdateNow()`, `SetRows(rows)` (local data), `Destroy()`.
     * A reply with more rows than the page (an api that ignores MaxRowCount) shows the page only, with one console warning.
     */
    /**
     * The options of Sw.Table.Create (the Sw.DevOptions schema of core/base.js; see the Table class for their meaning). A page that
     * passes an unknown (misspelled, another api's) or wrongly typed option gets a developer warning that names it and the valid ones,
     * instead of a silently ignored option.
     */
    const TableOptions = {
        Url: "string",
        Rows: "array",
        Cols: "array",
        Element: "element",
        Param: "any",
        KeyParam: "any",
        AutoHideEmpty: "boolean",
        Name: "string",
        State: ["string", "object"],
        Filter: ["string", "object", "array"],
        Order: ["array", "string"],
        OnFirstLoad: "function",
        OnFirstData: "function",
        OnCount: "function",
        Page: "boolean",
        Header: "boolean",
        HeaderTitle: "boolean",
        Fill: "boolean",
        HostFit: "boolean",
        EmptyTitle: "string",
        EmptyText: "string",
        ColumnLines: "boolean",
        RowActions: { Type: "string", Values: ["always"] },
        CardTitle: "string",
        MenuName: ["promise", "string"],
        Timeout: { Type: "number", Min: 1 },
        Card: ["string", "boolean"],
        Scroll: "boolean",
    };

    /**
     * The options of one local column (Sw.Table.Create `Cols`, with `Rows`): see LocalData.MakeCols.
     */
    const TableColOptions = {
        Name: "string",
        Title: "string",
        Desc: "string|null",
        Type: "string",
        Format: "any",
        Props: "number",
    };

    //  Row action icon files of the server with a twin in the line icon set (Sw.Icons): drawn as the line glyph in the grid
    //  (QuietCell), so a filled icon never sits among the outline ones (ex: ServerManager's Kill: skull.svg)
    if (Sw.Icons && typeof Sw.Icons.AddLine === "function" && typeof Sw.Icons.LineOf === "function")
        try {
            if (!Sw.Icons.LineOf("../icons/skull.svg"))
                Sw.Icons.AddLine("skull.svg", "kill");
        }
        catch {
        }

    //  The one option-schema registry (core/base.js Sw.DevOptions.Register): the schema is found by the api name, so the shared check,
    //  the namespace walker (core/optionstest.html) and the docs see the same list (an older core without the registry: the local check)
    if (Sw.DevOptions && typeof Sw.DevOptions.Register === "function") {
        try {
            Sw.DevOptions.Register("Sw.Table.Create", TableOptions);
            Sw.DevOptions.Register("Sw.Table.Create Cols[]", TableColOptions);
        }
        catch {
        }
    }

    /**
     * The words page authors use for an option of Sw.Table.Create that has another name (a "did you mean" the edit distance can't find).
     * @type {Object<string, string>}
     */
    const TableOptionAliases = {
        /* SwWeb:no-tr (option names for a developer warning, not texts for people) */
        Title: "Name", Container: "Element", Parent: "Element", Host: "Element", Target: "Element", Params: "Param", Parameters: "Param",
        Sort: "Order", OrderBy: "Order", Columns: "Cols", Data: "Rows", Items: "Rows", OnLoad: "OnFirstLoad", OnData: "OnFirstData",
        Api: "Url", Source: "Url", Empty: "EmptyText", Actions: "RowActions",
    };

    /**
     * Check the options of Sw.Table.Create in developer mode (Sw.DevOptions; nothing in production, never throws): unknown or mistyped
     * options, and options that collide (Url and Rows: the local rows win and the url is ignored; Cols without Rows: ignored, the server
     * sends the columns; neither Url nor Rows: the table has nothing to show).
     * @param {object} o The options.
     */
    function checkOptions(o) {
        try {
            const dev = typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode;
            if (!dev || !o || typeof o !== "object" || !Sw.DevWarn)
                return;
            const where = "Sw.Table.Create";
            //  (a known other name: the right one at once; the rest goes through the shared check)
            const known = {};
            for (const k of Object.keys(o)) {
                const to = TableOptionAliases[k];
                if (to && !(k in TableOptions) && o[k] !== undefined && o[k] !== null)
                    Sw.DevWarn(where + ': unknown option "' + k + '" (it is ignored). Use "' + to + '".', "opt:" + where + ":" + k);
                //  (null = left out, as undefined: table.html passes the url parameters it did not get as null)
                else if (o[k] !== null)
                    known[k] = o[k];
            }
            if (Sw.DevOptions)
                Sw.DevOptions(where, known, TableOptions);
            else
                //  (an older core without the shared check: at least the unknown names)
                for (const k of Object.keys(known))
                    if (!(k in TableOptions) && known[k] !== undefined && k[0] !== "_")
                        Sw.DevWarn(where + ': unknown option "' + k + '" (it is ignored). Valid options: ' + Object.keys(TableOptions).join(", ") + ".", "opt:" + where + ":" + k);
            const rows = o.Rows !== undefined && o.Rows !== null;
            if (rows && o.Url)
                Sw.DevWarn(where + ": both Url and Rows are given: the table shows the local Rows and ignores the Url. Pass one of them.", "opt:" + where + ":UrlRows");
            if (!rows && !o.Url)
                Sw.DevWarn(where + ": neither Url (a table api) nor Rows (local data) is given: the table has nothing to show.", "opt:" + where + ":noUrl");
            if (o.Cols !== undefined && o.Cols !== null && !rows)
                Sw.DevWarn(where + ": Cols is only used with Rows (local data); a table api sends its own columns, so it is ignored.", "opt:" + where + ":Cols");
            //  (the nested column options: each local column is checked like an options object, "Sw.Table.Create Cols[]")
            if (Array.isArray(o.Cols) && Sw.DevOptions)
                o.Cols.forEach(c => {
                    if (c && typeof c === "object")
                        Sw.DevOptions(where + " Cols[]", c, TableColOptions);
                    else
                        Sw.DevWarn(where + ": every item of Cols must be an object ({ Name, Title, Type, Format, Props }).", "opt:" + where + ":ColsItem");
                });
        }
        catch {
            //  (a check never breaks the table)
        }
    }

    class Table {
        /**
         * Create a table and start loading.
         * @param {object} o Options (see the class).
         * @returns {Promise<Table>} The table.
         */
        static async Create(o) {
            const t = new Table(o);
            await t.Init();
            return t;
        }

        /**
         * @param {object} o Options (see the class).
         */
        constructor(o) {
            o = o || {};
            //  (developer mode: unknown, misspelled, wrong typed or colliding options are named in the console, TableOptions)
            checkOptions(o);
            this.O = o;
            /** @type {?LocalData} The rows given by the page (option Rows: sorted, filtered and paged in the browser), null = a table api. */
            this.Local = Array.isArray(o.Rows) ? new LocalData(o.Rows, o.Cols, o.Name || "") : null;
            /** @type {string} The api url (local data: "local:" and the name or the page path, the key of the stored state). */
            this.Url = o.Url || (this.Local ? "local:" + (o.Name || location.pathname) : o.Url);
            //  (the row action results of this table in this tab survive the page being rebuilt, ex: the shell loads the page again
            //  when the window becomes a phone layout; LoadActs / SaveActs)
            this.LoadActs();
            /** @type {boolean} The name comes from the server Title. */
            this.NameFromTitle = !o.Name;
            if (!o.Name) {
                const p = String(this.Local ? "" : o.Url || "").split("?")[0].split("/");
                /** @type {string} The table name (exports, charts). */
                this.Name = Sw.Format.RemoveCamelCase(p[p.length - 1] || "");
            }
            else
                this.Name = o.Name;
            /** @type {object|null} The last TableData. */
            this.Data = null;
            /** @type {Array|null} The columns. */
            this.Cols = null;
            /** @type {string} Signature of the rendered columns. */
            this.ColSig = "";
            /** @type {Array<{Col: object, Index: number}>} The visible columns. */
            this.Vis = [];
            /** @type {number} Last server change counter. */
            this.Cc = 0;
            /** @type {boolean} The next request is caused by the user (no flash, reload). */
            this.UserChange = true;
            /** @type {boolean} True until the first response. */
            this.First = true;
            /** @type {number} Refresh timer. */
            this.Timer = 0;
            /** @type {boolean} A request is running. */
            this.Running = false;
            /** @type {boolean} Run again when the current request completes. */
            this.Again = false;
            /**
             * @type {number} Incremented by every update cycle; a cycle whose number isn't the latest has been superseded (by a user
             * change, Update) and ignores its result: a refresh may be held by the server until the data changes (long polling) and
             * user changes (sorting, filtering etc) must be applied at once.
             */
            this.Gen = 0;
            /** @type {Array<function>} Resolvers of UpdateNow promises taken by the running update cycle (a superseding cycle takes them over). */
            this.RunWaiters = [];
            /** @type {Array<function>} Resolvers of UpdateNow promises. */
            this.Waiters = [];
            /** @type {number} The due time of the refresh timer (Date.now() based, Schedule; 0 = none). */
            this.TimerDue = 0;
            /** @type {boolean} The next request is the automatic page size of Fit (shown like a user change: no flash; AutoFetch). */
            this.AutoSize = false;
            /** @type {number} When the last automatic page size request was asked (AutoFetch: at most one per AutoMinGap). */
            this.LastAutoSize = 0;
            /** @type {number} The time of the last user input in the page (pointer down, key, input; RequestGuard). */
            this.LastInput = Date.now();
            /** @type {number[]} The start times of the requests since the last user input, within the guard window (RequestGuard). */
            this.Sent = [];
            /** @type {boolean} The request guard throttles this table (too many requests without user input, RequestGuard). */
            this.Throttled = false;
            /** @type {{R: number, C: number}} The roving focus position (R 0 = header row, 1.. = data rows; C = cell index). */
            this.Active = { R: 1, C: 1 };
            /** @type {HTMLElement|null} The cell with tabindex 0. */
            this.ActiveEl = null;
            /** @type {Array<function>} Cleanup functions. */
            this.Offs = [];
            /** @type {boolean} Destroyed. */
            this.Dead = false;
            /** @type {Set<number>} Row numbers of the opened cards ("Show all n fields", CompactCard). */
            this.OpenRows = new Set();
            /** @type {number[]} Column indices that name a row (ItemText). */
            this.KeyCols = [];
            /** @type {?string} The column signature KeyCols was made for. */
            this.KeySig = null;
            /** @type {number} Unique id of the table on the page (data-sw-tid: scopes the hidden column rules, ApplyHidden). */
            this.Tid = ++TableIds;
            /** @type {?{Hide: string[], Show: string[]}} The viewer's column choices (ColPrefs). */
            this.ColPref = null;
            /** @type {string[]} The columns hidden automatically to fit a wide grid (UpdateFit). */
            this.AutoHidden = [];
            /** @type {Set<number>} Cell indices of the hidden columns (ApplyHidden; the keyboard skips them). */
            this.HiddenIdx = new Set();
            /** @type {number[]} The locked widths of the visible columns (px, LockWidths: only grow while the table is shown). */
            this.ColW = [];
            /** @type {string} What ColW was measured for (LockWidths). */
            this.ColWKey = "";
            /** @type {?{Name: string, Path: string, Mode: string}} The card template selection (SetupCard), null = no card template. */
            this.CardSpec = null;
            /** @type {?CardTemplate} The card template. */
            this.CardMain = null;
            /** @type {?CardTemplate} The card template of compact screens ("<name>-mobile"), null = none. */
            this.CardMobile = null;
            /** @type {string} The view of a table with a card template: "cards" or "table". */
            this.View = "table";
            /** @type {boolean} Infinite scrolling instead of pages (option Scroll): Req.Row stays 0, Data.Rows has all loaded rows. */
            this.ScrollOn = !!o.Scroll;
            /** @type {Array<object>} The loaded rows (infinite scrolling). */
            this.SRows = [];
            /** @type {boolean} More rows follow the loaded ones (infinite scrolling). */
            this.SMore = false;
            /** @type {boolean} The next request starts the list again: first load, other sort, filter or search (infinite scrolling). */
            this.ScrollReset = true;
            /** @type {number} Changes when the tail of the loaded rows changes: the answer of a running ScrollMore is then dropped. */
            this.SVer = 0;
            /** @type {boolean} ScrollMore runs. */
            this.SLoading = false;
            /** @type {boolean} The last ScrollMore failed (the end of the list offers "Try again"). */
            this.SFailed = false;
            /** @type {number} The first row a render has to look at (ScrollMore appends; 0 = all). */
            this.AppendFrom = 0;
        }

        /**
         * Load the state, build the elements and start the update loop.
         * @returns {Promise<void>}
         */
        async Init() {
            const o = this.O;
            /** @type {string} The local storage key of the state. */
            //  By default a table is identified by its endpoint (url) alone, so one endpoint serving many params shares a single saved
            //  state (the same filter whatever the param is; no unbounded growth for a high-cardinality param). A caller that needs
            //  independent state per value (ex: Fenrir's tables that all POST one table api with different params) passes KeyParam:
            //  folded into the key, each distinct value keeps its own state (also the filter row and column choices, keyed by Key).
            const kp = o.KeyParam;
            const keySeed = kp ? (this.Url + "\u0000" + (typeof kp === "string" ? kp : JSON.stringify(kp))) : this.Url;
            this.Key = "SysWeaver.Table." + await Sw.Util.HashString(keySeed);
            const raw = o.State || Sw.Storage.Local.Get(this.Key, null);
            let s = null;
            try {
                s = raw ? JSON.parse(raw) : null;
            }
            catch (e) {
                console.warn("Invalid table state: " + raw + "\n" + e);
            }
            s = RepairState(s);
            //  First open (no stored / given state): the caller's default sort order (option Order); none by default, like the original
            //  table (the server's own order applies)
            if (!raw && !s.RequestParams.Order) {
                const def = Array.isArray(o.Order) ? o.Order : null;
                if (def && def.length)
                    s.RequestParams.Order = def.slice(0, 3);
            }
            if (o.Filter) {
                try {
                    const f = JSON.parse(o.Filter);
                    s.Filters = [Array.isArray(f) ? f : [f]];
                    s = RepairState(s);
                }
                catch (e) {
                    console.warn("Invalid table filter: " + o.Filter + "\n" + e);
                }
            }
            //  The filter row (State.Expanded: 0 hidden / 1 the filter row / 2 all filter options) is part of the table state, like the
            //  original table: shown by default (1), a saved choice (also "hidden") is kept with the state
            /** @type {State} The state. */
            this.State = s;
            /** @type {object} The request (State.RequestParams). */
            this.Req = s.RequestParams;
            //  As the original request: table.html always sends it (null without "p"); an in-page table without a Param leaves it out
            //  (the original Table.addTable sent no Param then; P3-A-12)
            if (o.Param === undefined)
                delete this.Req.Param;
            else
                this.Req.Param = o.Param;
            this.Save();
            this.Build();
            this.Update();
        }

        /**
         * The automatic page size for a height (Fit), as the original table: the whole rows that fit, rounded down to a multiple of 5,
         * 5..100. Fit passes the page's own rows plus the space left under the shown rows (the original onResizeFn), so a page with
         * fewer rows than its size grows step by step. The first request asks for the stored size (25 for a new table, see
         * DefaultRequest) and the size then follows the rendered rows (Fit after each render, resize).
         * @param {number} avail The height for the rows (px).
         * @param {number} rowH The row height (px).
         * @returns {number} The rows per page.
         */
        PageRows(avail, rowH) {
            const n = avail / rowH;
            return Math.max(5, Math.min(100, Math.floor(n / 5) * 5));
        }

        /** Persist the state. */
        Save() {
            Sw.Storage.Local.Set(this.Key, JSON.stringify(this.State));
        }

        /**
         * A user change that needs new data: save, (optionally) back to the first page and fetch now.
         * @param {boolean} [keepRow] Don't go back to the first page.
         * @returns {Promise<void>} Resolves after the new data is shown.
         */
        Changed(keepRow) {
            if (!keepRow) {
                this.Req.Row = 0;
                //  Other rows now (sort, filter, search): close the opened cards
                this.OpenRows.clear();
                //  (infinite scrolling: the list starts again, a running ScrollMore is for the old rows)
                this.ScrollReset = true;
                ++this.SVer;
            }
            this.Save();
            this.UserChange = true;
            //  Other rows: the page size history of Fit starts again (N5). Its oscillation check is for the layout's own feedback
            //  without input; a search with fewer hits than a page grows the page to 100 (as the original) and the full page after
            //  Clear shrinks it back: two sizes the user asked for, not an oscillation (it pinned the page and warned)
            this.FitHist = [];
            this.FitPin = 0;
            this.FitCap = 0;
            const p = this.UpdateNow();
            /** @type {Promise<void>|null} The data request of the last user change, until its rows are shown (FlushPending). */
            this.PendingUser = p;
            p.then(() => {
                if (this.PendingUser === p)
                    this.PendingUser = null;
            });
            return p;
        }

        /**
         * Apply what the user typed but is not applied yet (the search text and filter values waiting for their pause) and wait until
         * the rows of the last user change are shown, so what is used next (ex: the export menu's row count and the "Current view"
         * file) matches the screen.
         * @returns {Promise<void>} Resolves when the shown rows are up to date.
         */
        async FlushPending() {
            const t = this;
            if (t.Search) {
                t.SearchApply.Cancel();
                t.Search.Commit();
            }
            t.FilterBody.querySelectorAll("input.sw-table-fval").forEach(i => i.dispatchEvent(new Event("change")));
            if (t.PendingUser)
                await t.PendingUser;
        }

        // -------------------------------------------------------------- elements

        /** Build the static elements (root, grid, footer) and the event handlers. */
        Build() {
            const t = this;
            const o = t.O;
            t.Grid = el("table.sw-table-grid", { attrs: { role: "grid", "aria-label": t.Name } });
            t.HeadRow = el("tr.sw-table-head");
            t.Head = el("thead", null, t.HeadRow);
            t.FilterBody = el("tbody.sw-table-filters");
            t.Body = el("tbody.sw-table-body");
            t.Grid.append(t.Head, t.FilterBody, t.Body);
            /** @type {HTMLElement} The items of a card template (RenderCards), shown instead of the grid (class sw-table-tmpl). */
            t.CardList = el("div.sw-table-cardlist", { tabIndex: 0, attrs: { role: "list", "aria-label": t.Name } });
            /** @type {HTMLElement} The end of an infinite list: loading, "try again" or the last item message (UpdateScrollEnd). */
            t.ScrollEnd = el("div.sw-table-scrollend", { attrs: { role: "status" } });
            t.ScrollEnd.hidden = true;
            t.Scroll = el("div.sw-table-scroll", null, t.Grid, t.CardList, t.ScrollEnd);
            /** @type {HTMLElement} The title of the empty state (UpdateEmpty). */
            t.EmptyTitle = el("h3.sw-table-state-title");
            /** @type {HTMLElement} The sentence of the empty state (UpdateEmpty). */
            t.EmptyText = el("p.sw-table-state-text");
            /** @type {object} "Clear search and filters" of the empty state (only while something is filtered). */
            t.EmptyClearB = new Sw.Button({
                Text: _TF("Clear search and filters", "Text of the button in the empty state of a data table that removes the search text and every filter"),
                Icon: "clearInput",
                OnClick: () => t.ClearAll(),
            });
            /** @type {HTMLElement} The empty state: inside the frame under the header (tool bar and footer stay), an icon, a title, one
             *  sentence and an action when filtered (UpdateEmpty). */
            t.Empty = el("div.sw-table-empty.sw-table-state", { attrs: { role: "status" } },
                el("span.sw-table-state-icon", null, lineIcon("empty")), t.EmptyTitle, t.EmptyText, t.EmptyClearB.E);
            t.Empty.hidden = true;
            /** @type {HTMLElement} The error state (shown while no data could be loaded; text + retry button). */
            t.ErrorText = el("div.sw-table-error-text");
            /** @type {HTMLElement} The technical text of the error state (inside the "Technical details" disclosure). */
            t.ErrorDetailText = el("pre.sw-table-error-detail-text");
            /** @type {HTMLDetailsElement} The "Technical details" disclosure of the error state: the raw error and a Copy button. */
            t.ErrorDetail = el("details.sw-table-error-detail", null,
                el("summary", { text: _TF("Technical details", "Text of a disclosure in the error state of a data table that shows the technical error text (for an administrator or developer)") }),
                t.ErrorDetailText,
                new Sw.Button({
                    Text: _TF("Copy", "Text of a button that copies the technical error text of a data table"),
                    Icon: "copy", Kind: "ghost", Small: true,
                    Title: _TF("Copy the technical details (to send them to an administrator)", "Tool tip of a button that copies the technical error text of a data table"),
                    OnClick: () => Sw.Clipboard.Copy(t.ErrorDetailText.textContent, { Generic: true }),
                }).E);
            t.Error = el("div.sw-table-error.sw-table-state", { attrs: { role: "alert" } },
                el("span.sw-table-state-icon.sw-table-state-danger", null, Sw.Icon("warning", "sw-table-error-icon")),
                el("h3.sw-table-state-title", { text: _TF("Could not load the data", "Title of the error state of a data table (the data could not be loaded)") }),
                t.ErrorText,
                new Sw.Button({
                    Text: _TF("Try again", "Text of a button that retries loading the data of a table after an error"),
                    Icon: "refresh", Kind: "primary",
                    Title: _TF("Try to load the data again", "Tool tip of a button that retries loading the data of a table after an error"),
                    OnClick: () => t.Retry(),
                }).E,
                t.ErrorDetail);
            t.Error.hidden = true;
            /** @type {HTMLElement} Host of the shared "not allowed" / "not found" states (ShowDenied / ShowNotFound; no "Try again"). */
            t.DeniedHost = el("div.sw-table-denied.sw-table-state");
            t.DeniedHost.hidden = true;
            /** @type {HTMLElement} The loading state: skeleton rows of the real row height (no spinner, nothing moves when the data comes). */
            t.Skel = el("div.sw-table-skel", { attrs: { "aria-hidden": "true" } },
                el("div.sw-table-skel-head"),
                ...Array.from({ length: SkeletonRows }, (x, i) => el("div.sw-table-skel-row", null,
                    ...[38, 14, 22, 12, 18].map((w, k) => el("span", { style: { width: (w + ((i * 7 + k * 5) % 9)) + "%" } })))));
            //  (data-sw-toastavoid: toasts keep clear of the pager, in this window and from the app shell around a framed page, FBK-8)
            t.Foot = el("div.sw-table-foot", { attrs: { role: "toolbar", "aria-label": _TF("Pages", "Accessible name of the bar below a data table (the shown rows, rows per page and the page buttons)"), "data-sw-toastavoid": "" } });
            /** @type {HTMLElement} The tool bar above the grid: search, Filter, then Columns, Density, Download, Refresh (BuildToolbar). */
            t.Toolbar = el("div.sw-table-toolbar", { attrs: { role: "toolbar", "aria-label": _TF("Table tools", "Accessible name of the tool bar above a data table (search, filter, columns, download, refresh)") } });
            /** @type {HTMLElement} The active filters as removable chips under the tool bar (UpdateChips; hidden when nothing is filtered). */
            t.Chips = el("div.sw-table-chips", { attrs: { role: "group", "aria-label": _TF("Active filters", "Accessible name of the bar with the active filters of a data table (one removable chip per filter)") } });
            t.Chips.hidden = true;
            /** @type {HTMLElement} The card layout tool bar (kept for old style sheets; the Sort button of the cards is in the tool bar now). */
            t.CardBar = el("div.sw-table-cardbar", { attrs: { role: "toolbar", "aria-label": _TF("Sort and filter", "Accessible name of the tool bar above the cards of a data table on phones (sort and filter buttons)") } });
            /** @type {HTMLElement} Left edge cue of the grid: a hairline and a short fade while columns are scrolled out to the left (UpdateEdges). */
            t.EdgeL = el("div.sw-table-edge.sw-table-edge-l", { attrs: { "aria-hidden": "true" } });
            /** @type {HTMLElement} Right edge cue: a hairline and a short fade while more columns are to the right (UpdateEdges). */
            t.EdgeR = el("div.sw-table-edge.sw-table-edge-r", { attrs: { "aria-hidden": "true" } });
            t.EdgeL.hidden = t.EdgeR.hidden = true;
            /** @type {HTMLElement} The rows area of the frame: the scrolled grid and, over it, the loading / empty / error states. Takes
             *  the free height of a page table, so the footer is always at the bottom of the frame. */
            t.Area = el("div.sw-table-area", null, t.Scroll, t.Skel, t.Empty, t.Error, t.DeniedHost, t.EdgeL, t.EdgeR);
            /** @type {HTMLStyleElement} The rules that hide the chosen / automatically hidden columns of this table (ApplyHidden). */
            t.ColStyle = el("style");
            /** @type {HTMLStyleElement} The widths given to text columns from the free width of a wide frame (SpreadWidths). */
            t.WidthStyle = el("style");
            /** @type {HTMLElement} A note above the table (UpdateNote; ex: log tables say which time span they cover). Outside the table
             *  frame (the root is the bordered frame of the tool bar, the rows and the footer), right before the root; on the table page
             *  it is the description of the page header. */
            t.Note = el("p.sw-table-note");
            t.Note.hidden = true;
            /** @type {boolean} Embedded table that takes the free height of its container (option Fill, else the container class). */
            t.Fill = !o.Page && (o.Fill !== undefined ? !!o.Fill : !!(o.Element && o.Element.classList && (o.Element.classList.contains("sw-fill") || o.Element.classList.contains("sw-table-fill"))));
            if (o.Header !== undefined ? !!o.Header : !!o.Page) {
                /** @type {HTMLElement} The title of the page header (table.html: h1; an embedded table with Header: true: h2). */
                t.PHTitle = el(o.Page ? "h1.sw-table-ph-title" : "h2.sw-table-ph-title", { text: t.Name });
                //  Stable from the first frame: the menu item name when it is known at once (the page in the app shell,
                //  Sw.MenuTitle) or given (Name); while it is looked up (option MenuName, a promise; table.html on its own) the title is
                //  hidden with its line reserved, so the api name ("Get llm model table") never shows first
                const menu = o.Page && !o.Name && typeof Sw.MenuTitle === "function" ? Sw.MenuTitle() : null;
                /** @type {boolean} The header title is final (the menu or link name): the server title does not replace it. */
                t.HeadNamed = !!o.Name || !!menu;
                if (menu) {
                    t.PHTitle.textContent = menu;
                    document.title = menu;
                }
                else if (!o.Name && o.MenuName && typeof o.MenuName.then === "function") {
                    t.PHTitle.textContent = "\u00a0";
                    t.PHTitle.classList.add("sw-table-ph-pending");
                    /** @type {Promise<?string>} The menu name lookup (option MenuName; sets the header title when found). */
                    t.MenuNameP = o.MenuName.catch(() => null).then(n => {
                        /** @type {boolean} The menu name lookup finished. */
                        t.MenuDone = true;
                        if (n)
                            t.SetHeaderName(n, true);
                        //  No menu item: the server title (or the url name) once the first answer came
                        else if (!t.First)
                            t.SetHeaderName(t.Name, false);
                        return n;
                    });
                }
                /** @type {HTMLElement} The description line of the page header: the log note, else the refresh interval (UpdateNote). */
                t.PHDesc = el("p.sw-table-ph-desc");
                /** @type {HTMLElement} The compact page header above the table frame (Design v2, PORTING.md B10; hidden on phones). */
                t.PH = el("header.sw-table-ph", null, el("div.sw-table-ph-text", null, t.PHTitle, t.PHDesc));
                //  Option HeaderTitle false (table.html "title=false"): no title in the header, only the description (the live line); the
                //  element stays (hidden), so the naming code (SetHeaderName, the document title) works as before
                if (o.HeaderTitle === false)
                    t.PHTitle.hidden = true;
            }
            t.E = el("div.sw-table", { class: o.Page ? "sw-table-page sw-table-loading" : t.Fill ? "sw-table-fill sw-table-loading" : "sw-table-loading", attrs: { "data-sw-tid": String(t.Tid) } }, t.ColStyle, t.WidthStyle, t.Toolbar, t.Chips, t.CardBar, t.Area, t.Foot);
            /** (the table of a root element: Sw.Table.Of(element)) */
            t.E.SwTable = t;
            //  Calm grid options (table.css): column rules (row actions are always shown: RowActions "always" is the only mode)
            if (o.ColumnLines)
                t.E.classList.add("sw-table-vlines");
            //  (infinite scrolling: no pager, table.css)
            t.E.classList.toggle("sw-table-scrollmode", t.ScrollOn);
            t.UpdateZebra();
            t.BuildCardBar();
            t.BuildFoot();
            t.BuildToolbar();
            if (t.PH)
                (o.Element || document.body).append(t.PH, t.E);
            else
                (o.Element || document.body).append(t.Note, t.E);
            t.Offs.push(() => {
                t.Note.remove();
                if (t.PH)
                    t.PH.remove();
            });
            //  The site density changes the row height: measure again (page size, column widths)
            if (window.MutationObserver) {
                const mo = new MutationObserver(() => {
                    t.UpdateZebra();
                    t.ColWKey = "";
                    t.FitKey = "";
                    requestAnimationFrame(() => {
                        t.UpdateFit(true);
                        t.Fit();
                    });
                });
                mo.observe(document.documentElement, { attributes: true, attributeFilter: ["data-density"] });
                t.Offs.push(() => mo.disconnect());
            }

            //  A modifier click anywhere in the table (Shift / Ctrl / Alt / Meta + click) only moves the focus (ModClick); first, before the
            //  capture handlers of the rows below
            t.E.addEventListener("click", ev => t.ModClick(ev), true);
            //  Alt+click on a link copies its address (the original tables copied on a modifier click; Ctrl / Shift / Meta + click keep
            //  the browser's new tab / window), the cell menu has "Copy link" too (CellMenu)
            if (Sw.Clipboard.AltClickLinks)
                t.Offs.push(Sw.Clipboard.AltClickLinks(t.E));
            //  Live refreshes keep the row order while the pointer is over the rows or the user put the focus in the grid (HoldOrder)
            t.Area.addEventListener("pointerenter", ev => {
                if (ev.pointerType !== "touch")
                    t.PtrIn = true;
            });
            t.Area.addEventListener("pointerleave", ev => {
                if (ev.pointerType === "touch")
                    return;
                t.PtrIn = false;
                t.ReleaseHold();
            });
            t.Area.addEventListener("pointerdown", ev => {
                if (t.Grid.contains(ev.target))
                    t.UserFocus = true;
            });
            //  (a finger on the rows, also while it scrolls them: touch events, a scroll cancels the pointer events at once)
            t.Area.addEventListener("touchstart", () => t.TouchIn = true, { passive: true });
            const touchEnd = ev => {
                if (!t.TouchIn || (ev.touches && ev.touches.length))
                    return;
                t.TouchIn = false;
                t.ReleaseHold();
            };
            window.addEventListener("touchend", touchEnd, true);
            window.addEventListener("touchcancel", touchEnd, true);
            t.Offs.push(() => {
                window.removeEventListener("touchend", touchEnd, true);
                window.removeEventListener("touchcancel", touchEnd, true);
            });
            t.Grid.addEventListener("keydown", () => t.UserFocus = true, true);
            t.Grid.addEventListener("focusout", () => setTimeout(() => {
                if (t.Dead || t.Grid.contains(document.activeElement))
                    return;
                t.UserFocus = false;
                t.ReleaseHold();
            }, 0));
            //  Grid input
            t.Grid.addEventListener("keydown", ev => t.OnKey(ev));
            t.Body.addEventListener("click", ev => t.ToggleClamp(ev), true);
            //  Row actions: remember the clicked action, so its result can be confirmed (ActionClick / ActionDone)
            t.Body.addEventListener("click", ev => t.ActionClick(ev), true);
            //  The outcome of a row action (Sw.Cells, before it shows any toast): the table shows it in the row (ActionResult)
            t.Body.addEventListener("sw-cell-result", ev => t.ActionResult(ev));
            //  The keyboard place survives a row that is rebuilt or a button that goes away (KeepFocusPlace)
            t.Grid.addEventListener("focusin", ev => t.NoteFocus(ev.target));
            t.Grid.addEventListener("focusout", ev => {
                const lf = t.LastFocus;
                const e = ev.target;
                //  (the user moved the focus away, or the window lost it: not a lost place; a removed / hidden element is)
                setTimeout(() => {
                    if (lf && t.LastFocus === lf && lf.El === e && e.isConnected && e.getClientRects().length)
                        lf.Left = true;
                }, 0);
            });
            t.Grid.addEventListener("focusin", ev => {
                const c = ev.target.closest && ev.target.closest("td,th");
                if (c && c.closest("table") === t.Grid && (c.parentElement.parentElement === t.Body || c.parentElement === t.HeadRow))
                    t.SetActive(c);
            });
            //  Filter rows: one tab stop in the simple filter mode (roving, see UpdateFilterTabs / OnFilterKey)
            t.FilterBody.addEventListener("focusin", ev => t.SetFilterStop(ev.target));
            t.FilterBody.addEventListener("keydown", ev => t.OnFilterKey(ev));
            if (o.Page)
                t.BindPageKeys();
            const menuOpts = { Label: _TF("Cell options", "Accessible name of the menu with the options of a data table cell") };
            t.Offs.push(Sw.Menu.Context(t.Head, ev => t.HeadMenu(ev.target), menuOpts));
            t.Offs.push(Sw.Menu.Context(t.Body, ev => t.CellMenu(ev.target), menuOpts));
            t.Offs.push(Sw.Menu.Context(t.CardList, ev => t.CardMenu(ev.target), { Label: _TF("Card options", "Accessible name of the menu with the options of a card of a data table") }));
            //  Infinite scrolling: load when the view comes near the end (also a page that scrolls around an embedded table)
            if (t.ScrollOn) {
                const near = () => t.ScrollEnsure();
                window.addEventListener("scroll", near, { capture: true, passive: true });
                t.Offs.push(() => window.removeEventListener("scroll", near, { capture: true }));
            }

            //  User input in the page (the request guard counts the requests sent since the last one, RequestGuard)
            const input = () => {
                t.LastInput = Date.now();
                t.Throttled = false;
            };
            ["pointerdown", "keydown", "input", "change"].forEach(n => window.addEventListener(n, input, true));
            t.Offs.push(() => ["pointerdown", "keydown", "input", "change"].forEach(n => window.removeEventListener(n, input, true)));

            //  Automatic page size, narrow / compact layout
            const fit = Sw.Util.Debounce(() => t.Fit(), 150);
            window.addEventListener("resize", fit);
            t.Offs.push(() => window.removeEventListener("resize", fit));
            const layout = () => {
                t.UpdateLayout();
                t.UpdateEdges();
                t.UpdateFit();
                //  (a table shown again, ex: its tab: the page size Fit skipped while it was hidden)
                if (t.FitHidden && t.E.getClientRects().length)
                    requestAnimationFrame(() => t.Fit());
            };
            window.addEventListener("resize", layout);
            t.Offs.push(() => window.removeEventListener("resize", layout));
            if (window.ResizeObserver) {
                const ro = new ResizeObserver(layout);
                ro.observe(t.E);
                ro.observe(t.Grid);
                t.Offs.push(() => ro.disconnect());
            }
            layout();
            t.Scroll.addEventListener("scroll", () => {
                t.UpdateEdges();
                t.UpdateCardBarHide();
            }, { passive: true });
            //  A hidden (scrolled away) card tool bar comes back when it gets the keyboard focus
            t.CardBar.addEventListener("focusin", () => {
                t.CardBar.classList.remove("sw-table-barhide");
                t.UpdateSnapPadding();
            });

            //  No polling while hidden
            const vis = () => {
                if (!document.hidden && t.Hidden) {
                    t.Hidden = false;
                    t.Schedule(0);
                }
            };
            document.addEventListener("visibilitychange", vis);
            t.Offs.push(() => document.removeEventListener("visibilitychange", vis));
            //  Links to other sites are plain text while offline (Sw.Cells.External): render the cells again when the connection changes
            const net = () => {
                if (!t.Data || t.Dead)
                    return;
                for (const tr of t.Body.rows)
                    for (const td of tr.cells)
                        td.SwKey = null;
                //  (not the user's refresh: row results and kept rows stay)
                t.QuietChange = true;
                t.RenderRows(t.Data, true);
            };
            window.addEventListener("online", net);
            window.addEventListener("offline", net);
            //  Back online (the server answers again, Sw.Net; UX guide FBK-9): fetch at once, also from the error state
            if (Sw.Events && typeof Sw.Events.On === "function") {
                const off = Sw.Events.On("net.online", () => {
                    if (t.Dead || t.Local || t.IsDenied)
                        return;
                    t.UpdateNote(!!t.NoteRun);
                    //  (a request still in flight, ex: the one that failed while the server was gone: one more right after it only when
                    //  it fails, else its failure would schedule the slow offline fallback and the stale chip would stay until then. When it
                    //  succeeds, it is the fresh data: often the table's own retry is the request that told Sw.Net the server is back, and a
                    //  second fetch went out 8 ms after it, S5b-Ops-1)
                    if (t.Running)
                        /** @type {boolean} "net.online" came while a request ran: fetch again at once if that request fails (Update). */
                        t.OnlineAgain = true;
                    else
                        t.Schedule(0);
                });
                if (typeof off === "function")
                    t.Offs.push(off);
            }
            t.Offs.push(() => {
                window.removeEventListener("online", net);
                window.removeEventListener("offline", net);
            });
        }

        /**
         * Update the layout classes from the current width: `sw-table-narrow` (< 600 px), `sw-table-compact` (a compact screen: narrower
         * than 600 px, or a touch / mobile device narrower than 1000 px) and `sw-table-cards` (Design v2, PORTING.md B10: phones in portrait show
         * cards, phones in landscape the compact grid; the layout button in the tool bar switches and the choice is remembered).
         */
        UpdateLayout() {
            const t = this;
            //  The border box: the layout classes change the frame's own borders (the narrow grid and the cards have no side borders), so
            //  the inner width (clientWidth) changed with the layout it chose: at 598 / 600 px it switched between the grid and the cards
            //  in every frame (owner report 4 #7, the pinned menu and Managed services at an inner width of 896 px)
            const w = t.E.offsetWidth;
            if (!w)
                return;
            const E = t.E.classList;
            //  Hysteresis against any other feedback (ex: a scroll bar of the page that comes or goes with the layout): a layout is left
            //  only when the width is clearly past its limit (a scroll bar's width), so one width never alternates between two layouts
            const band = 16;
            const narrow = w < (E.contains("sw-table-narrow") ? 600 + band : 600);
            const mobile = !!((Sw.Layout && Sw.Layout.Mobile) || Sw.Platform.IsTouch);
            const compact = narrow || (mobile && w < (E.contains("sw-table-compact") ? 1000 + band : 1000));
            const pref = Sw.Storage.Local.Get(LayoutKey, "");
            const low = window.innerHeight <= 500;
            //  (a card template replaces the built in cards of compact screens: its table view is the compact grid)
            const cards = !t.CardSpec && compact && (low ? pref === "cards" : pref !== "grid");
            const tmpl = t.CardsOn();
            E.toggle("sw-table-narrow", narrow);
            const changed = E.contains("sw-table-cards") !== cards;
            E.toggle("sw-table-cards", cards);
            E.toggle("sw-table-compact", compact);
            const b = t.LayoutB;
            if (b) {
                //  (with a card template the switch is always there, unless it is cards only: "-name@path")
                const asCards = t.CardSpec ? tmpl : cards;
                b.E.hidden = t.CardSpec ? t.CardSpec.Mode === "only" : !compact;
                b.E.replaceChildren(lineIcon(asCards ? "table" : "layers"));
                b.SetTitle(asCards
                    ? _TF("Show the rows as a grid (scroll sideways to see all columns)", "Tool tip of a button below a data table on phones that shows the rows as a grid instead of cards")
                    : _TF("Show each row as a card (all columns, no sideways scrolling)", "Tool tip of a button below a data table on phones that shows each row as a card instead of a grid"));
                b.SetLabel(asCards
                    ? _TF("Table view", "Short label of a button below a data table on phones that shows the rows as a table grid (one row per line, scroll sideways) instead of cards")
                    : _TF("Card view", "Short label of a button below a data table on phones that shows each row as a card (one line per column) instead of a table grid"));
            }
            if (t.CardSortB)
                t.CardSortB.E.hidden = !(cards || tmpl);
            //  (the template of this screen size, "<name>-mobile" on compact screens)
            if (tmpl && t.Data && t.CardList.SwTpl && t.CardList.SwTpl !== t.CardTpl()) {
                t.QuietChange = true;
                t.RenderRows(t.Data, true);
            }
            t.UpdateSnapPadding();
            if (changed) {
                t.ColWKey = "";
                if (t.Data) {
                    if (!cards) {
                        //  Measure the row numbers again in the grid layout (the corner cell keeps its minimum width meanwhile)
                        t.E.style.removeProperty("--sw-table-rnw");
                        t.UpdateRnw();
                    }
                    t.UpdateFit(true);
                    requestAnimationFrame(() => t.Fit());
                }
                //  Row outcomes survive the rows <-> cards switch: every marked row is marked again for the new layout (the result line
                //  moves between the grid's band layer and the card, the notes follow; no re-render needed)
                t.RemarkActed();
            }
            t.UpdateClamps();
        }

        /**
         * The edge cues of a grid wider than its area (Design v2, PORTING.md B13: a 1 px line and a short fade, no arrow over the content): at the
         * right of the sticky columns while columns are scrolled out to the left, and at the right edge (left of the pinned row actions)
         * while more columns follow. Placed over the visible part of the scrolled area (above its scroll bar); none in the card layout.
         * Throttled to one update per frame.
         */
        UpdateEdges() {
            const t = this;
            if (t.EdgeFrame)
                return;
            /** @type {number} The pending animation frame of UpdateEdges. */
            t.EdgeFrame = requestAnimationFrame(() => {
                t.EdgeFrame = 0;
                const s = t.Scroll;
                const max = s.scrollWidth - s.clientWidth;
                const on = !t.E.classList.contains("sw-table-cards") && !t.CardsOn() && !t.Dead && max > 2 && s.clientHeight > 0;
                const left = on && s.scrollLeft > 2;
                const right = on && s.scrollLeft < max - 2;
                t.EdgeL.hidden = !left;
                t.EdgeR.hidden = !right;
                if (!left && !right)
                    return;
                const top = s.offsetTop + (s.contains(t.CardBar) ? t.CardBar.offsetHeight : 0);
                const h = s.clientHeight + "px";
                [t.EdgeL, t.EdgeR].forEach(x => {
                    x.style.top = top + "px";
                    x.style.height = h;
                });
                //  (right of the sticky columns: the row numbers and the pinned identifying columns, UpdatePins)
                const r0 = t.Body.rows[0];
                const E = t.E.classList;
                let lw = r0 ? r0.cells[0].offsetWidth : 0;
                if (r0 && r0.cells[1] && (E.contains("sw-table-pin") || E.contains("sw-table-narrow")))
                    lw += r0.cells[1].offsetWidth;
                if (r0 && r0.cells[2] && E.contains("sw-table-pin2") && !E.contains("sw-table-narrow"))
                    lw += r0.cells[2].offsetWidth;
                t.EdgeL.style.left = s.offsetLeft + lw + "px";
                //  (left of the pinned row actions, UpdatePins)
                t.EdgeR.style.left = (s.offsetLeft + s.clientWidth - (t.ActPinW || 0)) + "px";
                //  (the header band of its fade: the header colour there, table.css .sw-table-edge-r::after)
                const hh = (t.Head.offsetHeight || 0) + "px";
                if (t.EdgeR.style.getPropertyValue("--sw-table-headh") !== hh)
                    t.EdgeR.style.setProperty("--sw-table-headh", hh);
            });
        }

        /**
         * Low screens (the card tool bar is inside the scrolled area, sticky at its top): hide the bar (slide it up, class
         * `sw-table-barhide`) while the cards are scrolled down, and show it again as soon as they are scrolled up or back to the start,
         * so Sort / Filter are always one small scroll up away. Elsewhere the bar is always shown.
         */
        UpdateCardBarHide() {
            const t = this;
            const s = t.Scroll;
            const y = s.scrollTop;
            const last = t.LastScrollY || 0;
            /** @type {number} The last vertical scroll position of the scrolled area (UpdateCardBarHide). */
            t.LastScrollY = y;
            const b = t.CardBar;
            if (b.parentElement !== s)
                return;
            let hide;
            if (y <= b.offsetHeight)
                hide = false;
            else if (y > last + 2)
                hide = !b.contains(document.activeElement);
            else if (y < last - 2)
                hide = false;
            else
                return;
            b.classList.toggle("sw-table-barhide", hide);
            t.UpdateSnapPadding();
        }

        /**
         * Low screens: while the sticky card tool bar is shown inside the scrolled area, the cards snap below it (scroll-padding-top =
         * the bar height), so scrolling back up never leaves the first card's "#1" heading under the bar; no padding while the bar is
         * hidden (slid away) or outside the scrolled area.
         */
        UpdateSnapPadding() {
            const t = this;
            const b = t.CardBar;
            const on = b.parentElement === t.Scroll && !b.classList.contains("sw-table-barhide");
            const v = on ? b.offsetHeight + "px" : "";
            if (t.Scroll.style.scrollPaddingTop !== v)
                t.Scroll.style.scrollPaddingTop = v;
        }

        /** Switch between cards and grid on compact screens (the layout button; the choice is remembered). */
        ToggleLayout() {
            const t = this;
            if (t.CardSpec) {
                t.SetView(t.CardsOn() ? "table" : "cards", true);
                return;
            }
            Sw.Storage.Local.Set(LayoutKey, t.E.classList.contains("sw-table-cards") ? "grid" : "cards");
            t.UpdateLayout();
        }

        // -------------------------------------------------------------- card templates

        /**
         * The card template is shown (not the table view).
         * @returns {boolean}
         */
        CardsOn() {
            return !!this.CardSpec && this.View === "cards";
        }

        /**
         * The card template of the current screen size: "<name>-mobile" on compact screens when it exists.
         * @returns {CardTemplate} The template.
         */
        CardTpl() {
            return (this.E.classList.contains("sw-table-compact") && this.CardMobile) || this.CardMain;
        }

        /**
         * Choose the card template, once, with the first answer (the option Card, table.html's "card" url parameter, comes first, then
         * TableData.Card). A file or template that can't be found stops the table with its error (cardError).
         * @param {?(string|boolean)} card "name@path"; empty: none; false: no card template, also not the server's.
         * @returns {Promise<void>}
         */
        async SetupCard(card) {
            const t = this;
            if (!card || t.CardSpec)
                return;
            const spec = parseCard(card);
            const doc = await loadCardFile(spec.Path);
            const find = name => Array.from(doc.querySelectorAll("template")).find(x => x.id === name) || null;
            const tpl = find(spec.Name);
            if (!tpl)
                throw cardError(_T('The card template "{0}" was not found in "{1}".', spec.Name, spec.Path, "Error shown instead of a data table when its card template file has no template with that name, {0} is the name, {1} the file"));
            const mobile = find(spec.Name + "-mobile");
            t.CardMain = new CardTemplate(tpl, spec.Name);
            t.CardMobile = mobile ? new CardTemplate(mobile, spec.Name + "-mobile") : null;
            t.CardSpec = spec;
            //  The viewer's choice per table, unless the table has no other view
            const pref = Sw.Storage.Local.Get(t.Key + ".View", "");
            const view = spec.Mode === "only" ? "cards" : pref === "cards" || pref === "table" ? pref : spec.Mode === "table" ? "table" : "cards";
            t.SetView(view, false);
        }

        /**
         * Show the cards or the table.
         * @param {string} view "cards" or "table".
         * @param {boolean} save Remember the viewer's choice.
         */
        SetView(view, save) {
            const t = this;
            t.View = view;
            if (save)
                Sw.Storage.Local.Set(t.Key + ".View", view);
            const E = t.E.classList;
            E.toggle("sw-table-tmpl", view === "cards");
            //  (one of the two holds the rows: the other is empty, so nothing measures or updates it)
            if (view === "cards") {
                t.Body.replaceChildren();
                t.ActiveEl = null;
                t.TabRow = null;
                t.E.style.removeProperty("--sw-row-fill");
                E.remove("sw-table-dense");
            }
            else {
                t.CardList.replaceChildren();
                t.CardList.SwTpl = null;
                t.ColWKey = "";
                t.AutoKey = "";
                t.FitKey = "";
            }
            /** @type {string} What the page size of paged cards was measured for (FitCards). */
            t.CardFitKey = "";
            t.UpdateLayout();
            if (!t.Data)
                return;
            t.QuietChange = true;
            t.Render(t.Data, true);
            requestAnimationFrame(() => {
                t.UpdateFit(true);
                t.Fit();
            });
        }

        /**
         * Say what the actions and the icon links of a card act on (UX guide TIP-2: not 20 times "Remove"). The item is named by the heading
         * of the card; a sub title equal to the heading is removed, a text with slashes may break after them (.sw-card-path).
         * @param {HTMLElement} item The card item.
         */
        NameCardActions(item) {
            item.querySelectorAll('[data-v]:not([data-v=""]) > .sw-if-empty').forEach(e => e.remove());
            const h = item.querySelector("h1, h2, h3, h4");
            const name = h ? h.textContent.trim() : "";
            //  (a confirmation dialog of an action on the card names the item: Sw.Cells RowName)
            if (name)
                item.dataset.swName = name;
            item.querySelectorAll(".sw-card-cell button, .sw-card-cell a, .sw-card-foot > a").forEach(b => {
                const text = (b.getAttribute("aria-label") || b.textContent || "").trim();
                const word = /[\p{L}\p{N}]/u.test(text);
                if (word && ((b.tagName === "A" && !b.closest(".sw-card-foot")) || !name || text.includes(name)))
                    return;
                const label = word ? text : b.closest(".sw-card-cell").dataset.swTitle;
                b.setAttribute("aria-label", name ? _T("{0}: {1}", label, name, "Accessible name of a button or link on a card of a data table. {0}: what it does, ex: Remove; {1}: the item it acts on, ex: OrderService") : label);
            });
            //  The status of an item (a badge, a flag, a matched value): a group with a name, unless it shows nothing (the levels are set by Render before this)
            const level = (item.querySelector(".sw-card") || item).getAttribute("data-sw-level");
            item.querySelectorAll(".sw-card-lead").forEach(lead => Array.from(lead.children).forEach(s => {
                if (!s.matches(".sw-badge, [data-on], [data-sw-match]"))
                    return;
                //  (a badge with no text, ex: no error, shows nothing)
                if (s.matches(".sw-badge") && !s.textContent.trim())
                    return;
                if (s.matches("[data-on]")) {
                    const on = s.getAttribute("data-on");
                    const sel = on === "true" || on === "True" ? ":scope > .sw-when-on" : on === "false" || on === "False" ? ":scope > .sw-when-off" : null;
                    if (!sel || !s.querySelector(sel))
                        return;
                }
                const when = ["ok", "warn", "danger"].filter(l => s.classList.contains("sw-when-" + l));
                if (when.length && !when.includes(level))
                    return;
                s.setAttribute("role", "group");
                s.setAttribute("aria-label", _TF("Status", "Accessible name of the status of an item on a card of a data table, ex: Running"));
            }));
            const sub = item.querySelector(".sw-card-lead-text > .sw-card-sub");
            if (sub && name && sub.textContent.trim() === name)
                sub.remove();
            //  Only the text nodes are replaced, so a link or another element inside stays; a <wbr> follows each separator
            const breakAfter = (root, re, need) => {
                const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                const nodes = [];
                for (let n = walker.nextNode(); n; n = walker.nextNode())
                    nodes.push(n);
                nodes.forEach(n => {
                    const text = n.nodeValue;
                    if (need && !need(text))
                        return;
                    const parts = text.split(re);
                    if (parts.length > 1)
                        n.replaceWith(...parts.flatMap((p, i) => i < parts.length - 1 ? [p, el("wbr")] : [p]));
                });
            };
            item.querySelectorAll(".sw-card-path").forEach(e => breakAfter(e, /(?<=[\\/])/));
            //  An address or a path in a link may break after its separators, not inside a word
            item.querySelectorAll(".sw-card-sub a, .sw-facts a").forEach(a => breakAfter(a, /(?<=[\\/.:])/, text => /^[a-z]+:\/\//i.test(text) || /[\\/]/.test(text)));
            item.querySelectorAll(".sw-meter[aria-label]").forEach(m => {
                if (name)
                    m.setAttribute("aria-label", _T("{0}: {1}", m.getAttribute("aria-label"), name, "Accessible name of a meter on a card of a data table. {0}: what it measures, ex: Used; {1}: the item, ex: C:"));
            });
        }

        /**
         * Render the rows as card template items. An unchanged row keeps its item; a live refresh that would move items while the pointer
         * or the focus is in them waits (ReleaseHold, UX guide FBK-7).
         * @param {object} data TableData.
         * @param {boolean} user Caused by the user (no flashing).
         */
        RenderCards(data, user) {
            const t = this;
            const cols = t.Cols;
            const rows = data.Rows || [];
            const generic = rows.length <= 0 || Array.isArray(rows[0].Values);
            const get = generic ? (row, i) => row.Values[i] : (row, i) => row[cols[i].Name];
            const list = t.CardList;
            const tpl = t.CardTpl();
            tpl.Bind(cols, t.ColSig);
            if (list.SwTpl !== tpl) {
                list.replaceChildren();
                list.SwTpl = tpl;
                list.dataset.card = tpl.Name;
            }
            const items = list.children;
            /** @type {boolean} A live refresh waits for the pointer or the focus to leave the items (ReleaseHold). */
            t.CardHeld = false;
            if (!user && items.length && t.HoldOrder()) {
                const n = Math.min(items.length, rows.length);
                for (let i = 0; i < n; ++i)
                    if (items[i].SwItem !== t.ItemText(rows[i], get)) {
                        t.CardHeld = true;
                        return;
                    }
            }
            const timeMode = t.State.TimeMode;
            //  (a row action in a card: the rows are read again, Sw.Cells shows its result)
            const onRefresh = () => {
                t.QuietChange = true;
                t.Changed(true);
            };
            const cell = (v, row) => {
                const c = v.Col;
                let fmt = c.Format;
                if (isDateType(c.Type) && !fmt)
                    fmt = timeMode;
                fmt = cellFormat(fmt);
                const next = v.Index + 1 < cols.length ? get(row, v.Index + 1) : undefined;
                const value = get(row, v.Index);
                const e = el("span.sw-card-cell");
                e.dataset.swTitle = c.Title || c.Name;
                Sw.Cells.Update(e, c.Type, value, null, fmt, next, onRefresh);
                const en = fmt ? null : enumText(c.Type, value);
                if (en !== null && en !== String(value))
                    e.textContent = en;
                return e;
            };
            const from = Math.min(t.AppendFrom, items.length);
            for (let i = from; i < rows.length; ++i) {
                const row = rows[i];
                const key = (timeMode || "") + JSON.stringify(row);
                const old = items[i];
                if (old && old.SwKey === key)
                    continue;
                const item = el("div.sw-table-carditem", { attrs: { role: "listitem" } });
                item.append(tpl.Render(row, get, cell));
                t.NameCardActions(item);
                item.SwKey = key;
                item.SwRow = row;
                item.SwItem = t.ItemText(row, get);
                if (old) {
                    const focused = old.contains(document.activeElement);
                    //  (changed values of the same item get the tint of the grid's live refresh)
                    if (!user && old.SwItem === item.SwItem) {
                        item.classList.add("sw-flash");
                        setTimeout(() => item.classList.remove("sw-flash"), 1050);
                    }
                    old.replaceWith(item);
                    if (focused)
                        list.focus({ preventScroll: true });
                }
                else
                    list.append(item);
            }
            while (items.length > rows.length)
                items[items.length - 1].remove();
            t.KeptShown = 0;
            t.KeptStates = [];
            t.Moves = null;
            t.HoldE.hidden = true;
            t.Empty.hidden = rows.length > 0;
            t.UpdateFit();
        }

        /**
         * The menu of a card: copy its row.
         * @param {EventTarget} target The event target.
         * @returns {?Array<object>} The items, null outside a card.
         */
        CardMenu(target) {
            const t = this;
            const item = target && target.closest ? target.closest(".sw-table-carditem") : null;
            const row = item ? item.SwRow : null;
            if (!row)
                return null;
            const obj = {};
            t.Cols.forEach((c, i) => obj[c.Name] = Array.isArray(row.Values) ? row.Values[i] : row[c.Name]);
            return [{
                Text: _TF("Copy row as JSON", "The text of a menu item on a table cell that copies all values of the row as JSON to the clipboard"), Icon: "IconCopy",
                OnClick: () => Sw.Clipboard.Copy(JSON.stringify(obj, null, 4)),
            }];
        }

        /**
         * The automatic page size of paged cards: the items that fit the rows area. Measured once per window size, template and column set,
         * so the size never follows the answers (no request loop, see Fit).
         */
        FitCards() {
            const t = this;
            const key = [t.Scroll.clientWidth, t.Scroll.clientHeight, t.CardTpl().Name, t.ColSig].join("|");
            if (key === t.CardFitKey)
                return;
            const m = t.ScrollMetrics();
            if (!m || !t.CardList.children.length)
                return;
            t.CardFitKey = key;
            const n = Math.max(1, Math.min(100, m.Screen));
            if (n === t.Req.MaxRowCount)
                return;
            t.Req.MaxRowCount = n;
            t.UpdateSizeSel();
            t.AutoFetch();
        }

        // -------------------------------------------------------------- infinite scrolling

        /**
         * The elements of the shown items, one per row, in order.
         * @returns {HTMLCollection} Card items or table rows.
         */
        ScrollItems() {
            return this.CardsOn() ? this.CardList.children : this.Body.rows;
        }

        /**
         * Measure what the rows area shows: the view (the rows area inside the window, below the sticky header), the items that are at
         * least partly in it and how many items fit one screen (cards may lie side by side).
         * @returns {?{First: number, Last: number, Screen: number}} First / Last are -1 without items. Null when the table isn't rendered.
         */
        ScrollMetrics() {
            const t = this;
            if (!t.E.getClientRects().length)
                return null;
            const sr = t.Scroll.getBoundingClientRect();
            const top = Math.max(sr.top, 0) + (t.CardsOn() ? 0 : t.Head.offsetHeight);
            const bottom = Math.min(sr.bottom, window.innerHeight);
            const vh = bottom - top;
            if (!(vh > 0))
                return null;
            const items = t.ScrollItems();
            const n = items.length;
            if (!n)
                return { First: -1, Last: -1, Screen: Math.max(1, Math.ceil(vh / 28)) };
            const r0 = items[0].getBoundingClientRect();
            if (!(r0.height > 0))
                return null;
            let perRow = 1;
            while (perRow < n && Math.abs(items[perRow].getBoundingClientRect().top - r0.top) < 1)
                ++perRow;
            const lines = Math.ceil(n / perRow);
            const pitch = Math.max(1, (items[n - 1].getBoundingClientRect().bottom - r0.top) / lines);
            const screen = Math.max(1, Math.ceil(vh / pitch)) * perRow;
            //  The first item that ends below the top of the view, the last one that starts above its bottom
            let lo = 0, hi = n;
            while (lo < hi) {
                const m = (lo + hi) >> 1;
                if (items[m].getBoundingClientRect().bottom > top)
                    hi = m;
                else
                    lo = m + 1;
            }
            const first = lo;
            hi = n;
            while (lo < hi) {
                const m = (lo + hi) >> 1;
                if (items[m].getBoundingClientRect().top < bottom)
                    lo = m + 1;
                else
                    hi = m;
            }
            return { First: first < n ? first : -1, Last: lo - 1, Screen: screen };
        }

        /**
         * The rows at least partly in view ("Current view" of an export).
         * @returns {Array<object>} The rows.
         */
        VisibleRows() {
            const m = this.ScrollMetrics();
            if (!m || m.First < 0 || m.Last < m.First)
                return [];
            return Array.prototype.slice.call(this.ScrollItems(), m.First, m.Last + 1).map(x => x.SwRow).filter(x => x);
        }

        /**
         * What the next request of the update loop asks for: from the first row when the list starts again (about 1.5 screens: the size
         * of the items is not known yet, ScrollEnsure corrects it), else the rows in view and the part below them (a live refresh of the
         * whole list would grow with it).
         * @returns {{Row: number, Max: number, Reset: boolean}} The request.
         */
        ScrollPlan() {
            const t = this;
            if (t.ScrollReset) {
                const h = t.Area.clientHeight > 28 ? t.Area.clientHeight : window.innerHeight;
                return { Row: 0, Max: Math.max(10, Math.min(200, Math.ceil(h / 28 * 1.5))), Reset: true };
            }
            const m = t.ScrollMetrics();
            const from = m && m.First > 0 ? m.First : 0;
            const shown = m && m.First >= 0 ? m.Last - m.First + 1 : 0;
            return { Row: from, Max: Math.min(1000, Math.max(10, shown, Math.ceil((m ? m.Screen : 25) * 1.5))), Reset: false };
        }

        /**
         * Merge an answer of the update loop into the loaded rows.
         * A changed part of the list drops the rows below it: they are stale (a row may have moved over the seam, and would show twice) and
         * load again on demand; the rows above stay until they are in view at a refresh.
         * @param {object} d The answer.
         * @param {{Row: number, Max: number, Reset: boolean}} p The request (ScrollPlan).
         * @returns {?object} The table data to render (all loaded rows), null when nothing changed.
         */
        ScrollMerge(d, p) {
            const t = this;
            const rows = d.Rows;
            const end = p.Row + rows.length;
            const more = rows.length > 0 && (d.RowCount || 0) > end;
            const old = t.SRows;
            let changed = true;
            if (p.Reset) {
                t.ScrollReset = false;
                ++t.SVer;
                t.SRows = rows.slice();
                t.SMore = more;
                t.SFailed = false;
                t.ScrollTop = true;
            }
            else if (!rows.length && p.Row > 0) {
                //  The list got shorter than the part in view: start again
                t.ScrollReset = true;
                ++t.SVer;
                t.Schedule(0);
                return null;
            }
            else {
                changed = !more && old.length > end;
                for (let i = 0; i < rows.length && !changed; ++i)
                    changed = JSON.stringify(rows[i]) !== JSON.stringify(old[p.Row + i]);
                if (changed) {
                    ++t.SVer;
                    t.SRows = old.slice(0, p.Row).concat(rows);
                    t.SMore = more;
                }
                //  (only the last part of the list says if more rows follow; a row added at the end continues the list)
                else if (old.length <= end && t.SMore !== more) {
                    t.SMore = more;
                    changed = true;
                }
            }
            if (!changed && !d.Cols)
                return null;
            return t.ScrollData(d);
        }

        /**
         * The table data of all loaded rows, with the counts of the answer.
         * @param {object} d The answer.
         * @returns {object} The table data.
         */
        ScrollData(d) {
            const t = this;
            const n = t.SRows.length;
            return Object.assign({}, d, { Rows: t.SRows, RowCount: t.SMore ? Math.max(n + 1, d.RowCount || 0) : n });
        }

        /**
         * Keep half a screen of items loaded below the view. Runs at most once per frame, after a scroll, a render or a layout change.
         */
        ScrollEnsure() {
            const t = this;
            if (!t.ScrollOn || t.ScrollFrame || t.Dead)
                return;
            /** @type {number} The pending animation frame of ScrollEnsure. */
            t.ScrollFrame = requestAnimationFrame(() => {
                t.ScrollFrame = 0;
                if (t.Dead || !t.Data || t.First || t.ScrollReset || t.SLoading || t.SFailed || !t.SMore)
                    return;
                const m = t.ScrollMetrics();
                if (!m)
                    return;
                const need = Math.max(m.Last, 0) + 1 + Math.ceil(m.Screen * 0.5);
                if (t.SRows.length < need)
                    t.ScrollMore(Math.max(m.Screen, need - t.SRows.length));
            });
        }

        /**
         * Load the next rows and append them. Not a cycle of the update loop: a refresh may be held by the server until the data changes,
         * and scrolling must not wait for it (and the refresh timer goes on as it was).
         * @param {number} count The rows to ask for (at least a screen: fewer, larger requests).
         * @returns {Promise<void>}
         */
        async ScrollMore(count) {
            const t = this;
            const ver = t.SVer;
            const from = t.SRows.length;
            const max = Math.min(1000, count);
            t.SLoading = true;
            t.UpdateScrollEnd();
            const body = Object.assign({}, t.Req, { Row: from, MaxRowCount: max, Cc: t.Cc });
            const busy = t.Local ? null : t.LiveDot();
            let d;
            try {
                d = t.Local ? t.Local.Query(body) : await Sw.Net.Send(t.Url, body, {});
                if (!d || !Array.isArray(d.Rows))
                    throw new Error(_TF("The service sent data in an unexpected format", "An error message displayed when the service reply to a table data request was json but not table data"));
            }
            catch (e) {
                t.SLoading = false;
                if (busy)
                    busy();
                if (t.Dead || ver !== t.SVer) {
                    t.UpdateScrollEnd();
                    t.ScrollEnsure();
                    return;
                }
                console.warn("Sw.Table " + t.Url + ": " + (e && e.message ? e.message : e));
                t.SFailed = true;
                t.UpdateScrollEnd();
                return;
            }
            t.SLoading = false;
            if (busy)
                busy();
            if (t.Dead)
                return;
            //  The rows changed meanwhile (a refresh, another sort): this answer doesn't fit them
            if (ver !== t.SVer) {
                t.UpdateScrollEnd();
                t.ScrollEnsure();
                return;
            }
            //  Other columns: the rows are read again with them
            if (d.Cols && JSON.stringify(d.Cols) !== t.ColSig) {
                t.Changed();
                return;
            }
            let rows = d.Rows;
            if (rows.length > max)
                rows = from > 0 && (d.RowCount || 0) <= rows.length ? rows.slice(from, from + max) : rows.slice(0, max);
            t.SRows.push(...rows);
            t.SMore = rows.length > 0 && (d.RowCount || 0) > from + rows.length;
            t.AppendFrom = from;
            t.QuietChange = true;
            try {
                t.Render(t.ScrollData(Object.assign({}, d, { Cols: null })), true);
            }
            finally {
                t.AppendFrom = 0;
            }
            t.UpdateScrollEnd();
            t.ScrollEnsure();
        }

        /**
         * The end of an infinite list: the loading animation while a request runs, "try again" after a failure, the last item message when
         * all rows are loaded (nothing while more rows wait to be scrolled to).
         */
        UpdateScrollEnd() {
            const t = this;
            const e = t.ScrollEnd;
            if (!t.ScrollOn)
                return;
            const n = t.SRows.length;
            const state = t.SLoading ? "load" : t.SFailed ? "fail" : !t.SMore && n > 0 ? "end" + n : "";
            if (e.SwState === state)
                return;
            e.SwState = state;
            e.hidden = !state;
            //  (half a screen is loaded ahead, so the end of the list is often below the view: loading and a failure stay in view, table.css)
            e.classList.toggle("sw-table-scrollnow", t.SLoading || t.SFailed);
            if (t.SLoading)
                e.replaceChildren(new Sw.Spinner({ Class: "sw-table-scrollspin" }).E, el("span", { text: _TF("Loading more rows...", "Text next to the loading animation at the bottom of a data table with infinite scrolling, while more rows are requested") }));
            else if (t.SFailed)
                e.replaceChildren(el("span", { text: _TF("Could not load more rows.", "Text at the bottom of a data table with infinite scrolling when loading more rows failed") }), new Sw.Button({
                    Text: _TF("Try again", "Text of a button that retries loading the data of a table after an error"), Icon: "refresh", Small: true,
                    OnClick: () => {
                        t.SFailed = false;
                        t.UpdateScrollEnd();
                        t.ScrollEnsure();
                    },
                }).E);
            else if (state)
                e.replaceChildren(el("span", { text: n === 1
                    ? _TF("End of the list: 1 row", "Text at the bottom of a data table with infinite scrolling after the last item (one row in all)")
                    : _T("End of the list: {0} rows", Sw.Format.Number(n, 0, " "), "Text at the bottom of a data table with infinite scrolling after the last item, {0} is the number of rows in all") }));
        }

        // -------------------------------------------------------------- tool bar (search, Filter, Sort, Columns, Density, Download, Refresh)

        /**
         * Create the card layout buttons: "Sort" (a menu: the sortable columns, the direction, clear; shown in the tool bar in the card
         * layout, where the header row is hidden) and the filter dialog button of old pages (kept, not shown: the tool bar's Filter
         * button opens the same dialog in the cards).
         */
        BuildCardBar() {
            const t = this;
            /** @type {object} The "Sort" button of the card layout (in the tool bar, BuildToolbar). */
            t.CardSortB = new Sw.Button({ Kind: "ghost", Class: "sw-table-tbtn sw-table-sortbtn", OnClick: b => t.SortMenu(b.E) });
            t.CardSortB.E.setAttribute("aria-haspopup", "menu");
            t.CardSortB.E.classList.add("sw-icon-only");
            t.CardSortB.E.hidden = true;
            /** @type {object} The filter dialog button of the old card tool bar (not shown; see FilterB). */
            t.CardFilterB = new Sw.Button({ Icon: "filter", Text: "", Kind: "ghost", Class: "sw-table-cardbtn", OnClick: () => t.FilterSheet() });
        }

        /**
         * Build the tool bar above the grid (Design v2, PORTING.md B13): the search field and the Filter button (a count badge while filters are
         * active) at the left; the "Warnings and errors" quick filter of log tables; at the right the icon tools: Sort (cards only),
         * Columns, Density, Download, Refresh and the cards / grid switch (compact screens).
         */
        BuildToolbar() {
            const t = this;
            /** @type {HTMLElement} The count badge of the Filter button. */
            t.FilterBadge = el("span.sw-table-badge", { attrs: { "aria-hidden": "true" } });
            t.FilterBadge.hidden = true;
            /** @type {object} The Filter button: a menu (add a filter, show the filter row, clear) on the grid, the filter dialog on cards. */
            t.FilterTB = new Sw.Button({
                Kind: "ghost", Class: "sw-table-tbtn sw-table-filterbtn2",
                Title: _TF("Show only the rows that match some values", "Tool tip of the filter button above the cards of a data table on phones"),
                OnClick: b => t.E.classList.contains("sw-table-cards") || t.CardsOn() ? t.FilterSheet() : t.FilterMenu(b.E),
            });
            t.FilterTB.E.setAttribute("aria-haspopup", "menu");
            t.FilterTB.E.append(Sw.Icon("filter"), el("span.sw-table-tlabel", { text: _TF("Filter", "Text of the filter button above the cards of a data table on phones (no active filters)") }), t.FilterBadge);
            if (Sw.Hint)
                Sw.Hint.Attach(t.FilterTB.E);
            //  (an icon and a word: not an icon only square)
            t.ProblemsB.E.classList.add("sw-table-tbtn");
            t.ProblemsB.E.classList.remove("sw-icon-only");
            t.Toolbar.append(
                t.Search.E,
                t.FilterTB.E,
                t.ProblemsB.E,
                el("div.sw-table-tspacer"),
                el("div.sw-table-ttools", null, t.CardSortB.E, t.ColsB.E, t.DensityB.E, t.ExportB.E, t.RefreshB.E, t.LayoutB.E, t.MoreTB.E));
            t.UpdateCardBar();
        }

        /**
         * The overflow menu of a narrow tool bar (table.css shows its button instead of Download, Refresh, Density and the
         * cards / grid switch below 480 px, so the search field keeps its width on phones): the same commands.
         * @param {HTMLElement} at The anchor.
         */
        MoreMenu(at) {
            const t = this;
            const items = [
                { Text: _TF("Download", "Menu item of the overflow menu of a data table tool bar on phones that shows the export (download) choices"), Title: t.ExportB.E.title || null, Disabled: t.ExportB.E.disabled, OnClick: () => t.ExportMenu(at) },
                t.Local ? null : { Text: _TF("Refresh now", "A tool tip description on a button that if clicked will reload the data of a table"), Disabled: t.RefreshB.E.disabled, OnClick: () => t.Changed(true) },
                { Text: _TF("Row density and stripes", "Tool tip of the density button of a data table (the height of the rows, striped rows)"), OnClick: () => t.DensityMenu(at) },
            ].filter(x => x);
            if (!t.LayoutB.E.hidden)
                items.push("-", { Text: t.LayoutB.E.getAttribute("aria-label") || t.LayoutB.E.title || "", Title: t.LayoutB.E.title || null, OnClick: () => t.ToggleLayout() });
            Sw.Menu.Show(at, items, { Align: "end", Label: _TF("More tools", "Accessible name of the overflow menu of a data table tool bar on phones") });
        }

        /**
         * Update the tool bar from the state: the Sort button of the cards (the current sort column and direction in its tool tip), the
         * Filter button (pressed with a count badge while filters are active) and the filter chips (UpdateChips).
         */
        UpdateCardBar() {
            const t = this;
            if (!t.CardSortB)
                return;
            const r = t.Req;
            const first = Sort.Has(r) ? r.Order[0] : null;
            const desc = !!first && first.charAt(0) === "-";
            const name = first ? (desc ? first.substring(1) : first) : null;
            const col = name && t.Cols ? t.Cols.find(c => c.Name === name) : null;
            const title = col ? (col.Title || col.Name) : name;
            t.CardSortB.E.replaceChildren(lineIcon("sort"));
            t.CardSortB.E.classList.toggle("sw-table-on", !!title);
            t.CardSortB.SetTitle(title
                ? (desc
                    ? _T("Sorted by {0} in descending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                    : _T("Sorted by {0} in ascending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title"))
                : _TF("Choose the column to sort by", "Tool tip of the sort button above the cards of a data table on phones"));
            t.CardSortB.SetLabel(title
                ? _T("Sort: {0}", title, "Text of the sort button above the cards of a data table on phones, {0} is the title of the column the table is sorted by")
                : _TF("Sort", "Text of the sort button above the cards of a data table on phones (the table is not sorted)"));
            const n = t.ActiveFilters().length;
            t.CardFilterB.SetContent(n > 0
                ? _T("Filter ({0})", n, "Text of the filter button above the cards of a data table on phones, {0} is the number of active filters")
                : _TF("Filter", "Text of the filter button above the cards of a data table on phones (no active filters)"), "filter");
            if (t.FilterTB) {
                t.FilterBadge.textContent = n > 0 ? String(n) : "";
                t.FilterBadge.hidden = n <= 0;
                t.FilterTB.E.classList.toggle("sw-table-on", n > 0);
                t.FilterTB.SetLabel(n > 0
                    ? _T("Filter ({0})", n, "Text of the filter button above the cards of a data table on phones, {0} is the number of active filters")
                    : _TF("Filter", "Text of the filter button above the cards of a data table on phones (no active filters)"));
            }
            t.UpdateChips();
        }

        /**
         * The active filters (a value is set), of every filter set, in order.
         * @returns {Array<{F: Filter, Set: number, Vi: number}>} The filters with their set and visible column index.
         */
        ActiveFilters() {
            const res = [];
            (this.State.Filters || []).forEach((set, si) => set.forEach((f, vi) => {
                if (f && f.Value !== "" && f.Value !== null && f.Value !== undefined)
                    res.push({ F: f, Set: si, Vi: vi });
            }));
            return res;
        }

        /**
         * The text of a filter chip: "Memory > 100", "Name contains sql", "not: Name contains sql".
         * @param {Filter} f The filter.
         * @param {number} vi The visible column index.
         * @returns {{Col: string, Op: string, Value: string}} The parts.
         */
        ChipText(f, vi) {
            const v = this.Vis[vi];
            const col = v ? (shortTitleHint(v.Col) || v.Col.Title || v.Col.Name) : f.ColName;
            const op = f.Op >= 0 && f.Op < Ops.Count ? Ops.Names[f.Op].toLowerCase() : "";
            return { Col: col, Op: (f.Invert ? _TF("not", "Word before the operation of an inverted filter in a filter chip of a data table (ex: Name not contains x)") + " " : "") + op, Value: String(f.Value) };
        }

        /**
         * The filter chips under the tool bar (Design v2, PORTING.md B13): one removable chip per active filter ("Memory  greater than  100  x"; a
         * click edits it), "Add filter" and "Clear all". Hidden when nothing is filtered (and in the card layout, which has the filter
         * dialog). Several filter sets: the chips of set 2 and 3 start with "or".
         */
        UpdateChips() {
            const t = this;
            if (!t.Chips || !t.Vis)
                return;
            const list = t.ActiveFilters();
            const sig = JSON.stringify(list.map(x => [x.Set, x.Vi, x.F.Op, x.F.Value, x.F.Invert, x.F.CaseSensitive])) + "|" + (t.Vis.length);
            if (sig === t.ChipSig)
                return;
            /** @type {string} What the chips show (UpdateChips). */
            t.ChipSig = sig;
            const wasHidden = t.Chips.hidden;
            t.Chips.hidden = list.length <= 0;
            const parts = [];
            let lastSet = 0;
            list.forEach(x => {
                const p = t.ChipText(x.F, x.Vi);
                const label = p.Col + " " + p.Op + " " + p.Value;
                if (x.Set !== lastSet) {
                    lastSet = x.Set;
                    parts.push(el("span.sw-table-chip-or", { text: _TF("or", "Word between the filter chips of two filter sets of a data table (rows matching any set are shown)") }));
                }
                const edit = el("button.sw-table-chip-main", {
                    type: "button",
                    title: _T("Edit the filter: {0}", label, "Tool tip of a filter chip of a data table, {0} is the filter (column, operation, value)"),
                }, el("span.sw-table-chip-col", { text: p.Col }), el("span.sw-table-chip-op", { text: p.Op }), el("b.sw-table-chip-val", { text: p.Value }));
                edit.addEventListener("click", ev => {
                    if (!Sw.BadClick(ev))
                        t.EditFilter(x.Vi, x.Set);
                });
                const rm = el("button.sw-table-chip-x", {
                    type: "button",
                    title: _T("Remove the filter: {0}", label, "Tool tip of the remove button of a filter chip of a data table, {0} is the filter (column, operation, value)"),
                    attrs: { "aria-label": _T("Remove the filter: {0}", label, "Tool tip of the remove button of a filter chip of a data table, {0} is the filter (column, operation, value)") },
                }, lineIcon("x"));
                rm.addEventListener("click", ev => {
                    if (Sw.BadClick(ev))
                        return;
                    x.F.Value = "";
                    t.BuildFilters();
                    t.Changed();
                    //  Keep the keyboard in the bar
                    requestAnimationFrame(() => {
                        const n = t.Chips.querySelector(".sw-table-chip-x, .sw-table-chip-add");
                        if (n && t.E.contains(document.activeElement) === false)
                            n.focus();
                    });
                });
                parts.push(el("span.sw-table-chip", null, edit, rm));
            });
            const add = el("button.sw-table-chip-add", { type: "button" }, lineIcon("plus"), el("span", { text: _TF("Add filter", "Text of the button in the filter chips bar of a data table that adds a filter") }));
            add.addEventListener("click", ev => {
                if (!Sw.BadClick(ev))
                    t.EditFilter(-1, 0);
            });
            const clear = el("button.sw-table-chip-clear", { type: "button", text: _TF("Clear all", "Text of the button in the filter chips bar of a data table that removes every filter") });
            clear.addEventListener("click", ev => {
                if (!Sw.BadClick(ev))
                    t.ClearFilters();
            });
            t.Chips.replaceChildren(...parts, add, clear);
            //  The bar changes the free height of a page table: fit the page size again (the footer stays at the bottom of the frame)
            if (wasHidden !== t.Chips.hidden && t.Data)
                requestAnimationFrame(() => t.Fit());
        }

        /**
         * The Filter menu of the grid: add a filter (EditFilter), the filter row (shown by default; hidden / "Show the filter row" /
         * "Show all filter options", remembered per table and user), clear all filters.
         * @param {HTMLElement} at The anchor.
         */
        FilterMenu(at) {
            const t = this;
            const n = t.ActiveFilters().length;
            const ex = t.State.Expanded;
            const set = v => {
                t.SetExpanded(v);
                if (v)
                    requestAnimationFrame(() => t.FocusFilter(Math.max(0, t.Active.C - 1)));
            };
            Sw.Menu.Show(at, [
                {
                    Text: _TF("Add a filter", "Menu item of the filter menu of a data table that opens the dialog to add a filter"), Icon: "plus",
                    OnClick: () => t.EditFilter(-1, 0),
                },
                {
                    Text: _TF("Clear all filters", "Menu item of the filter menu of a data table that removes every filter"), Icon: "clearInput", Disabled: n <= 0,
                    OnClick: () => t.ClearFilters(),
                },
                "-",
                { Title: _TF("Filter row", "Heading in the filter menu of a data table (the options of the row of filter fields under the column titles)") },
                {
                    Text: _TF("Hidden (filter chips)", "Menu item of the filter menu of a data table: no filter row, the filters are shown as chips"), Radio: true, Checked: ex === 0,
                    OnClick: () => set(0),
                },
                {
                    Text: _TF("Show the filter row", "Menu item of the filter menu of a data table that shows a row of filter values under the column titles"), Radio: true, Checked: ex === 1,
                    Desc: _TF("A filter value field under every column title", "Description of the menu item that shows the filter row of a data table"),
                    OnClick: () => set(1),
                },
                {
                    Text: _TF("Show all filter options", "Menu item of the filter menu of a data table that shows the advanced filter rows (operation, value, Not, Match case, filter sets)"), Radio: true, Checked: ex === 2,
                    Desc: _TF("Operations, Not, Match case and up to 3 filter sets", "Description of the menu item that shows the advanced filter rows of a data table"),
                    OnClick: () => set(2),
                },
            ], { Label: _TF("Filter", "Text of the filter button above the cards of a data table on phones (no active filters)") });
        }

        /**
         * Show / hide the filter row (State.Expanded: 0 hidden, 1 the values, 2 everything), remembered with the table state (as the original).
         * @param {number} v The mode.
         */
        SetExpanded(v) {
            const t = this;
            t.State.Expanded = v;
            t.ShowFilters();
            t.Save();
            t.UpdateFit(true);
            t.Fit();
        }

        /**
         * The filter editor (the Filter menu "Add a filter", a click on a chip, "f" or the column menu "Filter" while the filter row is
         * hidden): the column, the operation, the value and the Not / Match case options of one filter of a filter set.
         * @param {number} vi The visible column index (-1: choose; the current column first).
         * @param {number} si The filter set.
         * @returns {Promise<void>} Resolves when the dialog is closed.
         */
        async EditFilter(vi, si) {
            const t = this;
            const s = t.State;
            if (!t.Cols || !t.Vis.length)
                return;
            si = Math.max(0, Math.min(s.Filters.length - 1, si | 0));
            const cols = [];
            t.Vis.forEach((v, i) => {
                if ((v.Col.Props & Props.AnyFilters) !== 0)
                    cols.push(i);
            });
            if (!cols.length) {
                Sw.Info(_TF("This table cannot be filtered", "Message shown when a filter is added to a data table that has no filterable column"));
                return;
            }
            if (cols.indexOf(vi) < 0) {
                const cur = Math.max(0, t.Active.C - 1);
                vi = cols.indexOf(cur) >= 0 && vi !== -2 ? cur : cols[0];
                //  (a new filter: the first column without a value in this set when the current one has one)
                const set = s.Filters[si];
                if (set[vi] && set[vi].Value !== "") {
                    const free = cols.find(i => !set[i] || set[i].Value === "");
                    if (free !== undefined)
                        vi = free;
                }
            }
            const uid = "swfe" + Math.random().toString(36).substring(2, 8);
            const colSel = el("select.sw-table-fop", { id: uid + "c", attrs: { autocomplete: "off" } }, ...cols.map(i => el("option", { value: String(i), text: shortTitleHint(t.Vis[i].Col) || t.Vis[i].Col.Title || t.Vis[i].Col.Name })));
            const opSel = el("select.sw-table-fop", { id: uid + "o", attrs: { autocomplete: "off" } });
            const val = el("input.sw-table-fval", { type: "text", id: uid + "v", title: rawHint, attrs: { autocomplete: "off", spellcheck: "false", enterkeyhint: "done" } });
            const inv = el("input", { type: "checkbox", id: uid + "i", attrs: { autocomplete: "off" } });
            const cs = el("input", { type: "checkbox", id: uid + "s", attrs: { autocomplete: "off" } });
            const csRow = el("label.sw-table-fe-check", { attrs: { for: uid + "s" } }, cs, el("span", { text: _TF("Match case (upper and lower case letters must match)", "Check box of the filter dialog of a data table that makes a text filter case sensitive") }));
            /** Load the controls from the filter of the chosen column. */
            const load = () => {
                const c = t.Vis[vi].Col;
                const p = c.Props;
                const f = s.Filters[si][vi] || new Filter(c.Name);
                opSel.replaceChildren();
                for (let oi = 0; oi < Ops.Count; ++oi)
                    if ((p & Ops.Props[oi]) !== 0)
                        opSel.append(el("option", { value: String(oi), text: Ops.Names[oi], title: Ops.Descriptions[oi] + "\n" + rawHint }));
                let op = f.Op;
                if (op < 0 || (p & Ops.Props[op]) === 0)
                    op = Ops.DefaultOrders.find(x => (p & Ops.Props[x]) !== 0) ?? Ops.Props.findIndex(x => (p & x) !== 0);
                opSel.value = String(op);
                val.value = f.Value || "";
                val.placeholder = Ops.Names[op];
                const list = enumList(c.Type);
                if (list)
                    val.setAttribute("list", list);
                else
                    val.removeAttribute("list");
                inv.checked = !!f.Invert;
                cs.checked = !!f.CaseSensitive;
                csRow.hidden = (p & Props.TextFilter) === 0;
            };
            colSel.value = String(vi);
            load();
            colSel.addEventListener("change", () => {
                vi = +colSel.value;
                load();
            });
            opSel.addEventListener("change", () => val.placeholder = Ops.Names[+opSel.value]);
            const had = !!(s.Filters[si][vi] && s.Filters[si][vi].Value !== "");
            const field = (id, text, ctl) => el("div.sw-table-fe-row", null, el("label.sw-table-flabel", { text, attrs: { for: id } }), ctl);
            const body = el("div.sw-table-fe",
                null,
                s.Filters.length > 1 ? el("p.sw-table-fsheet-note", { text: _T("Filter set {0}", si + 1, "Heading of one set of filters in the filter dialog of a data table, {0} is the set number") }) : null,
                field(uid + "c", _TF("Column", "Label of the column select of the filter dialog of a data table"), colSel),
                field(uid + "o", _TF("Operation", "Label of the operation select of the filter dialog of a data table"), opSel),
                field(uid + "v", _TF("Value", "Label of the value field of the filter dialog of a data table"), val),
                el("label.sw-table-fe-check", { attrs: { for: uid + "i" } }, inv, el("span", { text: _TF("Not: show the rows that do NOT match", "Check box of the filter dialog of a data table that inverts the filter") })),
                csRow);
            val.addEventListener("keydown", ev => {
                if (ev.key === "Enter") {
                    ev.preventDefault();
                    const ok = document.querySelector(".sw-table-fedialog .sw-primary");
                    if (ok)
                        ok.click();
                }
            });
            const r = await Sw.Dialog.Show({
                Title: had ? _TF("Edit filter", "Title of the filter dialog of a data table when an existing filter is edited") : _TF("Add a filter", "Menu item of the filter menu of a data table that opens the dialog to add a filter"),
                Icon: "filter",
                Content: body,
                Class: "sw-table-fdialog sw-table-fedialog",
                Focus: val,
                Buttons: [
                    had ? { Text: _TF("Remove", "Text of the button of the filter dialog of a data table that removes the filter"), Icon: "trash", Value: "remove" } : null,
                    { Text: _TF("Cancel", "Text of the button that closes the filter dialog of a data table without changes"), Value: false },
                    { Text: had ? _TF("Apply", "Text of the button of the filter dialog of a data table that applies the edited filter") : _TF("Add", "Text of the button of the filter dialog of a data table that adds the filter"), Kind: "primary", Value: true },
                ].filter(x => x),
            });
            if (!r)
                return;
            const v = t.Vis[vi];
            let f = s.Filters[si][vi];
            if (!f) {
                f = s.Filters[si][vi] = new Filter(v.Col.Name);
                f.ColumnIndex = v.Index;
            }
            if (r === "remove")
                f.Value = "";
            else {
                f.Op = +opSel.value;
                f.Value = val.value;
                f.Invert = inv.checked;
                f.CaseSensitive = !csRow.hidden && cs.checked;
            }
            t.BuildFilters();
            t.Changed();
        }

        /**
         * Remove every filter value (all sets; the operations and options stay; the extra sets go). The search stays.
         */
        ClearFilters() {
            const t = this;
            const s = t.State;
            const changed = t.ActiveFilters().length > 0;
            s.Filters = [s.Filters[0]];
            s.FilterRows = 1;
            s.Filters[0].forEach(f => f.Value = "");
            t.BuildFilters();
            if (changed)
                t.Changed();
            else
                t.Save();
        }

        /** "Clear search and filters" (the empty state): no search text, no filter values. */
        ClearAll() {
            const t = this;
            t.Req.SearchText = "";
            t.Search.Set("", true);
            t.ClearFilters();
            t.Changed();
        }

        /**
         * The empty state texts: filtered ("No matching rows" + "Clear search and filters"), the log quick filter, or an empty table.
         * @param {string} [problems] The text of the log table quick filter (UpdateProblems), else null.
         */
        UpdateEmpty(problems) {
            const t = this;
            const o = t.O;
            const filtered = !!t.Req.SearchText || t.ActiveFilters().length > 0;
            //  The unfiltered texts are per table (options EmptyTitle / EmptyText, table.html url parameters "et" / "ed"), else the
            //  sentence names the table (the header title = the menu name on a page, else the table name)
            const name = ((t.PHTitle && t.PHTitle.textContent) || t.Name || "").trim();
            const title = filtered
                ? _TF("No matching rows", "Title of the empty state of a data table when the search or the filters match no row")
                : (o.EmptyTitle || _TF("No rows to show", "Text shown in a data table when there are no rows (nothing matches the filters or the table is empty)"));
            const text = problems || (filtered
                ? _TF("Nothing matches the search and the filters.", "Sentence of the empty state of a data table when the search or the filters match no row")
                : (o.EmptyText || (o.EmptyTitle ? ""
                    : name ? _T("There is nothing in {0} right now.", name, "Sentence of the empty state of a data table when the table has no rows, {0} is the name of the table (ex: the menu name Active sessions)")
                        : _TF("This table has no data right now.", "Sentence of the empty state of a data table when the table has no rows"))));
            if (t.EmptyTitle.textContent !== title)
                t.EmptyTitle.textContent = title;
            if (t.EmptyText.textContent !== text)
                t.EmptyText.textContent = text;
            t.EmptyText.hidden = !text;
            t.EmptyClearB.E.hidden = !filtered;
        }

        /**
         * Striped rows (Design v2 section 9): the viewer's choice (ZebraKey "on" / "off"), else only in the compact density.
         */
        UpdateZebra() {
            const t = this;
            const z = Sw.Storage.Local.Get(ZebraKey, "");
            const on = z === "on" || (z !== "off" && currentDensity() === "compact");
            t.E.classList.toggle("sw-table-zebra", on);
        }

        /**
         * The Density menu: the row density of the site (Compact, Comfortable, Touch; core/theme.js keeps it per browser) and striped rows.
         * @param {HTMLElement} at The anchor.
         */
        DensityMenu(at) {
            const t = this;
            const d = currentDensity();
            const z = Sw.Storage.Local.Get(ZebraKey, "");
            const zOn = z === "on" || (z !== "off" && d === "compact");
            const item = (v, text, desc) => ({ Text: text, Desc: desc, Radio: true, Checked: d === v, OnClick: () => setDensity(v) });
            Sw.Menu.Show(at, [
                { Title: _TF("Density", "Heading of the density menu of a data table (the height of the rows)") },
                item("compact", _TF("Compact", "Density option: small rows and controls"), _TF("More rows on the screen", "Description of the compact density option")),
                item("comfortable", _TF("Comfortable", "Density option: the normal size of rows and controls"), _TF("The default with a mouse", "Description of the comfortable density option")),
                item("touch", _TF("Touch", "Density option: large rows and controls for touch screens"), _TF("Large targets for fingers", "Description of the touch density option")),
                "-",
                {
                    Text: _TF("Striped rows", "Menu item of the density menu of a data table that shades every other row"), Checked: zOn,
                    Title: _TF("Shade every other row (on by default in the compact density)", "Tool tip of the striped rows menu item of a data table"),
                    OnClick: () => {
                        Sw.Storage.Local.Set(ZebraKey, zOn ? "off" : "on");
                        t.UpdateZebra();
                    },
                },
            ], { Align: "end", Label: _TF("Density", "Heading of the density menu of a data table (the height of the rows)") });
        }

        /**
         * The sort menu of the card layout: one radio item per sortable column (a click on the current column flips the direction),
         * the direction of the current sort and "Clear sort". Same effect as the column header sort buttons / menu.
         * @param {HTMLElement} at The anchor.
         */
        SortMenu(at) {
            const t = this;
            const r = t.Req;
            /** Apply a sort change. */
            const sorted = () => {
                t.UpdateHead();
                t.Changed();
            };
            const items = [{ Title: _TF("Sort by", "Heading of the sort menu of a data table on phones (a list of the columns follows)") }];
            let cur = null;
            t.Vis.forEach(v => {
                const c = v.Col;
                if ((c.Props & Props.CanSort) === 0)
                    return;
                const f = Sort.Find(r, c.Name);
                if (f.Index === 0)
                    cur = { Col: c, Desc: f.Desc };
                items.push({
                    Text: c.Title || c.Name, Title: c.Desc || null, Radio: true, Checked: f.Index === 0,
                    Hint: f.Index === 0 ? (f.Desc ? "↓" : "↑") : null,
                    OnClick: () => {
                        Sort.Toggle(r, c.Name, sortDescFirst(c));
                        sorted();
                    },
                });
            });
            if (items.length <= 1) {
                Sw.Info(_TF("This table cannot be sorted", "Message shown when the sort button of a data table is used and no column can be sorted"));
                return;
            }
            items.push("-",
                {
                    Text: _TF("Ascending (A to Z, 1 to 9)", "Menu item of the sort menu of a data table on phones that sorts the current column in ascending order"),
                    Icon: "IconAscending", Radio: true, Checked: !!cur && !cur.Desc, Disabled: !cur,
                    OnClick: () => {
                        Sort.Set(r, cur.Col.Name, false);
                        sorted();
                    },
                },
                {
                    Text: _TF("Descending (Z to A, 9 to 1)", "Menu item of the sort menu of a data table on phones that sorts the current column in descending order"),
                    Icon: "IconDescending", Radio: true, Checked: !!cur && cur.Desc, Disabled: !cur,
                    OnClick: () => {
                        Sort.Set(r, cur.Col.Name, true);
                        sorted();
                    },
                },
                {
                    Text: _TF("Clear sort", "The text of a menu item that if clicked will remove any sorting of a table"),
                    Icon: "IconClearSort", Disabled: !Sort.Has(r),
                    OnClick: () => {
                        r.Order = null;
                        sorted();
                    },
                });
            Sw.Menu.Show(at, items, { Label: _TF("Sort by", "Heading of the sort menu of a data table on phones (a list of the columns follows)") });
        }

        /**
         * The filter dialog of the card layout: the same options as the filter rows (per column: operation, value, invert, case
         * sensitive; up to 3 filter sets of which any may match; reset). Changes apply at once (values after a short pause or Enter);
         * the hidden filter rows are rebuilt from the state when the dialog closes.
         * @returns {Promise<void>} Resolves when the dialog is closed.
         */
        async FilterSheet() {
            const t = this;
            const s = t.State;
            if (!t.Cols)
                return;
            const body = el("div.sw-table-fsheet");
            /** Render the sets. */
            const render = () => {
                const parts = [];
                if (s.Filters.length > 1)
                    parts.push(el("p.sw-table-fsheet-note", { text: _TF("Rows that match any of the filter sets are shown.", "Explanation in the filter dialog of a data table when there is more than one filter set") }));
                s.Filters.forEach((set, si) => {
                    const box = el("div.sw-table-fset");
                    if (s.Filters.length > 1)
                        box.append(el("div.sw-table-fset-head", null,
                            el("span", { text: _T("Filter set {0}", si + 1, "Heading of one set of filters in the filter dialog of a data table, {0} is the set number") }),
                            si > 0 ? new Sw.Button({
                                Icon: "IconClose", Kind: "ghost", Small: true,
                                Title: _TF("Remove this filter set.", "A tool tip description on a button that if clicked will remove a set of filters"),
                                OnClick: () => {
                                    const changed = set.some(f => f.Value !== "");
                                    s.Filters.splice(si, 1);
                                    s.FilterRows = s.Filters.length;
                                    if (changed)
                                        t.Changed();
                                    else
                                        t.Save();
                                    render();
                                },
                            }).E : null));
                    t.Vis.forEach((v, vi) => {
                        const p = v.Col.Props;
                        if ((p & Props.AnyFilters) === 0)
                            return;
                        let f = set[vi];
                        if (!f) {
                            f = set[vi] = new Filter(v.Col.Name);
                            f.ColumnIndex = v.Index;
                        }
                        const label = v.Col.Title || v.Col.Name;
                        const sel = el("select.sw-table-fop", { attrs: { autocomplete: "off", "aria-label": _T("Filter operation of {0}", label, "Accessible name of the select with the filter operation of a table column, {0} is the column title") } });
                        for (let oi = 0; oi < Ops.Count; ++oi)
                            if ((p & Ops.Props[oi]) !== 0)
                                sel.append(el("option", { value: String(oi), text: Ops.Names[oi], title: Ops.Descriptions[oi] + "\n" + rawHint }));
                        if (f.Op < 0 || (p & Ops.Props[f.Op]) === 0)
                            f.Op = Ops.DefaultOrders.find(x => (p & Ops.Props[x]) !== 0) ?? Ops.Props.findIndex(x => (p & x) !== 0);
                        sel.value = String(f.Op);
                        const id = "swfs" + si + "_" + vi + "_" + Math.random().toString(36).substring(2, 7);
                        const input = el("input.sw-table-fval", {
                            type: "text", value: f.Value, placeholder: Ops.Names[f.Op], title: rawHint, id,
                            attrs: { autocomplete: "off", spellcheck: "false", enterkeyhint: "done", list: enumList(v.Col.Type) },
                        });
                        sel.addEventListener("change", () => {
                            f.Op = +sel.value;
                            input.placeholder = Ops.Names[f.Op];
                            if (f.Value !== "")
                                t.Changed();
                            else
                                t.Save();
                        });
                        /** Apply the typed value. */
                        const commit = () => {
                            apply.Cancel();
                            if (input.value === f.Value)
                                return;
                            f.Value = input.value;
                            t.UpdateCardBar();
                            t.Changed();
                        };
                        const apply = Sw.Util.Debounce(commit, 600);
                        input.addEventListener("input", apply);
                        input.addEventListener("change", commit);
                        input.addEventListener("keydown", ev => {
                            if (ev.key === "Enter") {
                                ev.preventDefault();
                                commit();
                            }
                        });
                        const inv = t.FlagButton(_TF("Not", "Short text of the toggle button of a data table column filter that keeps the rows that do NOT match the filter (inverts it)"), true, () => f.Invert, x => f.Invert = x, f,
                            _TF("Not is on: showing the rows that do NOT match this filter.", "Tool tip of the pressed Not toggle button of a data table column filter (the filter is inverted)") + "\n" + _TF("Show the rows that match instead.", "Second line of the tool tip of the pressed Not toggle button of a data table column filter"),
                            _TF("Not is off: showing the rows that match this filter.", "Tool tip of the Not toggle button of a data table column filter when it is not pressed") + "\n" + _TF("Show the rows that do NOT match instead.", "Second line of the tool tip of the Not toggle button of a data table column filter when it is not pressed"));
                        const flags = [inv];
                        if (p & Props.TextFilter)
                            flags.push(t.FlagButton(_TF("Match case", "Short text of the toggle button of a data table column text filter that makes it case sensitive (upper and lower case letters must match)"), true, () => f.CaseSensitive, x => f.CaseSensitive = x, f,
                                _TF("Match case is on: upper and lower case letters must match (A does not find a).", "Tool tip of the pressed Match case toggle button of a data table column text filter") + "\n" + _TF("Ignore upper and lower case.", "Second line of the tool tip of the pressed Match case toggle button of a data table column text filter"),
                                _TF("Match case is off: upper and lower case letters are ignored (A finds a).", "Tool tip of the Match case toggle button of a data table column text filter when it is not pressed") + "\n" + _TF("Make upper and lower case letters matter.", "Second line of the tool tip of the Match case toggle button of a data table column text filter when it is not pressed")));
                        inv.setAttribute("aria-label", _TF("Invert (keep the rows that do not match)", "Accessible name of the toggle button that inverts a filter of a data table column"));
                        if (flags[1])
                            flags[1].setAttribute("aria-label", _TF("Case sensitive", "Accessible name of the toggle button that makes a text filter of a data table column case sensitive"));
                        box.append(el("div.sw-table-frow2", null,
                            el("label.sw-table-flabel", { text: label, attrs: { for: id } }),
                            el("div.sw-table-fctl", null, sel, input, el("div.sw-table-ftoggles", null, ...flags))));
                    });
                    parts.push(box);
                });
                const add = new Sw.Button({
                    Icon: "IconPlus", Kind: "ghost",
                    Text: _TF("Add a filter set", "Text of a button in the filter dialog of a data table that adds another set of filters (rows matching any set are shown)"),
                    Title: _TF("Add a new set of filters.", "A tool tip description on a button that if clicked will add a new set of filter options"),
                    Disabled: s.Filters.length >= 3 ? _TF("At most 3 filter sets", "Reason why the button that adds a filter set of a data table is not available (the most filter sets are used)") : false,
                    OnClick: () => {
                        s.Filters.push([]);
                        s.FilterRows = s.Filters.length;
                        t.Save();
                        render();
                    },
                });
                parts.push(el("div.sw-table-fsheet-tools", null, add.E));
                body.replaceChildren(...parts);
            };
            render();
            await Sw.Dialog.Show({
                Title: _TF("Filter", "Title of the filter dialog of a data table on phones"),
                Icon: "filter",
                Content: body,
                Class: "sw-table-fdialog",
                Buttons: [
                    {
                        Text: _TF("Reset", "Text of a button in the filter dialog of a data table that removes all filters"), Icon: "IconReset",
                        Title: _TF("Reset all filter values and parameters to their default (no filtering).", "A tool tip description on a button that if clicked will reset all parameters to default"),
                        OnClick: () => {
                            t.ResetFilters();
                            render();
                            return false;
                        },
                    },
                    { Text: _TF("Done", "Text of the button that closes the filter dialog of a data table"), Kind: "primary", Value: true },
                ],
            });
            //  Apply a value that is still waiting for its pause, then sync the (hidden) filter rows
            body.querySelectorAll("input.sw-table-fval").forEach(i => i.dispatchEvent(new Event("change")));
            t.BuildFilters();
            t.UpdateCardBar();
        }

        /**
         * Build the footer (Design v2, PORTING.md B13: always at the bottom of the frame, fixed slot widths, tabular numbers, so nothing moves when
         * the numbers change): the range text ("1-50 of 671", "1-25 of 26+") and the "last refresh failed" / "Changed" notes at the left;
         * "Rows" (the page size: Auto fits the height) and the icon pager (first, previous, "Page 3", next) at the right. Also creates
         * the tool bar controls (BuildToolbar places them): search, Refresh, Download, Columns, Density, the layout switch and the log
         * quick filter.
         */
        BuildFoot() {
            const t = this;
            const r = t.Req;
            t.HomeB = toolButton("first", _TF("Go to the first page", "A tool tip description on a button that if clicked will navigate to the first page"), () => {
                if (r.Row > 0) {
                    r.Row = 0;
                    t.Changed(true);
                }
            }, "sw-table-pbtn");
            t.PrevB = toolButton("prev", _TF("Go to the previous page", "A tool tip description on a button that if clicked will navigate to the previous page"), () => t.Page(-1), "sw-table-pbtn");
            t.NextB = toolButton("next", _TF("Go to the next page", "A tool tip description on a button that if clicked will navigate to the next page"), () => t.Page(1), "sw-table-pbtn");
            [t.HomeB, t.PrevB, t.NextB].forEach(b => b.SetDisabled(true, _TF("The rows are still loading", "Reason (tool tip) of the disabled page buttons of a data table before its first data arrived")));
            t.RangeE = el("span.sw-table-range", { attrs: { "aria-live": "polite" } });
            t.RangeE.textContent = _TF("Loading...", "The range text of a data table while its first data loads");
            /** @type {HTMLElement} "Page 3" / "Page 3 of 14" between the page buttons (a slot that only grows, UpdateFoot). */
            t.PageE = el("span.sw-table-pageno");
            /** @type {HTMLSelectElement} Rows per page: Auto (fits the free height), 10, 25, 50, 100. */
            t.SizeSel = el("select.sw-table-pps-sel", {
                id: "swpps" + t.Tid,
                //  No form state restoration / autofill: the browser may put the values of another table (same layout) into the
                //  controls of a table, ex: an iframe created while the host goes back in its history (all table controls: autocomplete off)
                attrs: { autocomplete: "off" },
                title: _TF("Rows per page (Auto: as many as fit the window)", "Tool tip of the rows per page select below a data table"),
            });
            t.SizeSel.addEventListener("change", () => t.SetPageSize(+t.SizeSel.value));
            t.UpdateSizeSel();
            /** @type {HTMLElement} The legend of the tint of changed values (shown after the first one, ShowChangedLegend). */
            t.ChangedE = el("span.sw-table-changed", null,
                el("span.sw-table-swatch", { attrs: { "aria-hidden": "true" } }),
                el("span", { text: _TF("Changed", "Legend below a data table: values with a light tint changed at the last automatic refresh") }));
            t.ChangedE.hidden = true;
            /** @type {HTMLElement} The warning chip of a failed live refresh while the last data stays ("Last refresh failed 20:10"). */
            t.StaleE = el("span.sw-table-stale", null, Sw.Icon("warning", "sw-table-stale-icon"), el("span.sw-table-stale-long"), el("span.sw-table-stale-short"));
            t.StaleE.hidden = true;
            /** @type {HTMLElement} "Order on hold" while a live refresh keeps the rows in place (HoldRows; the last item, so it moves nothing). */
            t.HoldE = el("span.sw-table-hold", null,
                el("span.sw-table-hold-mark", { attrs: { "aria-hidden": "true" }, text: "↕" }),
                el("span", { text: _TF("Order on hold", "Legend below a live data table while the rows keep their place (the pointer or the keyboard focus is in the rows); the new order is shown when it leaves") }));
            t.HoldE.hidden = true;
            t.RefreshB = toolButton("refresh", _TF("Refresh now", "A tool tip description on a button that if clicked will reload the data of a table"), () => t.Changed(true));
            //  (local rows, option Rows: the page owns the data, there is nothing to load again)
            t.RefreshB.E.hidden = !!t.Local;
            t.ExportB = toolButton("download", _TF("Download the data as a file (export)", "A tool tip description on a button that if clicked will show a menu with data table export (download) options"), () => t.ExportMenu());
            t.ExportB.E.setAttribute("aria-haspopup", "menu");
            /** @type {object} Cards / grid switch (compact screens only, see UpdateLayout). */
            t.LayoutB = toolButton("table", "", () => t.ToggleLayout());
            t.LayoutB.E.hidden = true;
            /** @type {HTMLElement} The count of hidden columns on the Columns button (UpdateColsButton). */
            t.ColsBadge = el("span.sw-table-badge", { attrs: { "aria-hidden": "true" } });
            t.ColsBadge.hidden = true;
            /** @type {object} Column chooser (the grid layouts: show / hide columns, row numbers; a badge counts the hidden ones, UpdateColsButton). */
            t.ColsB = toolButton("columns", _TF("Choose the columns to show", "Tool tip of the column chooser button below a data table"), () => t.ColsMenu());
            t.ColsB.E.append(t.ColsBadge);
            t.ColsB.E.setAttribute("aria-haspopup", "menu");
            t.ColsB.E.hidden = true;
            /** @type {object} Density (row height of the site) and striped rows (DensityMenu). */
            t.DensityB = toolButton("rows", _TF("Row density and stripes", "Tool tip of the density button of a data table (the height of the rows, striped rows)"), b => t.DensityMenu(b.E), "sw-table-densbtn");
            t.DensityB.E.setAttribute("aria-haspopup", "menu");
            /** @type {object} The overflow menu of narrow tool bars (MoreMenu; shown by table.css below 480 px). */
            t.MoreTB = toolButton("kebab", _TF("More tools", "Accessible name of the overflow menu of a data table tool bar on phones"), b => t.MoreMenu(b.E), "sw-table-morebtn2");
            t.MoreTB.E.setAttribute("aria-haspopup", "menu");
            t.ExportB.E.classList.add("sw-table-tover");
            t.RefreshB.E.classList.add("sw-table-tover");
            t.LayoutB.E.classList.add("sw-table-tover");
            t.DensityB.E.classList.add("sw-table-tover");
            /** @type {object} "Warnings and errors" quick filter (log tables: a visible SysWeaver.MessageLevels column; ToggleProblems). */
            t.ProblemsB = new Sw.Button({
                Icon: "warning", Kind: "ghost", Small: true, Class: "sw-table-fbtn sw-table-problems", Pressable: true, AutoToggle: false,
                Title: _TF("Show only warnings and errors (the level is Warning or worse); press again to show every message", "Tool tip of the quick filter button below a log table that shows only warnings and errors"),
                OnClick: () => t.ToggleProblems(),
            });
            t.ProblemsB.SwLabel = el("span.sw-table-blabel.sw-table-tlabel", { text: _TF("Warnings and errors", "Short label of the quick filter button below a log table that shows only warnings and errors"), attrs: { "aria-hidden": "true" } });
            t.ProblemsB.E.append(t.ProblemsB.SwLabel);
            t.ProblemsB.E.hidden = true;
            //  The search applies on Enter / leaving the field / Clear (the input's change), like the original table (one request per
            //  search, none while typing). SearchApply stays for callers (FlushPending): it commits the typed text.
            /** @type {function} The pending (debounced) apply of the typed search text (Cancel() drops it; FlushPending applies it). */
            t.SearchApply = Sw.Util.Debounce(() => t.Search.Commit(), SearchDelay);
            t.Search = new Sw.TextBox({
                Value: r.SearchText || "",
                Placeholder: _TF("Search...", "A placehodler text for an input field that contains textual keyword used for searching a table of data"),
                Title: _TF("Search the table (press Enter to search)", "Tool tip of the search field of a data table, the search is applied on Enter or when leaving the field"),
                Icon: "search",
                Clear: true,
                OnChange: v => {
                    t.SearchApply.Cancel();
                    const st = v.trim();
                    //  (the original refetched on every change, also with the same text; a new text starts at the first page)
                    if (st === (r.SearchText || "")) {
                        t.Changed(true);
                        return;
                    }
                    r.SearchText = st;
                    t.Changed();
                },
            });
            t.Search.E.classList.add("sw-table-search");
            //  The clear (×) button says what it does (the search text stays visible until it is cleared, UX guide LST-7)
            const clearB = t.Search.E.querySelector(".sw-text-clear");
            if (clearB)
                clearB.title = _TF("Clear the search and show all rows again", "Tool tip of the clear (x) button in the search field of a data table");
            t.Foot.append(
                //  (the legend before the stale chip: the legend stays once shown, the chip comes and goes after it, so nothing shown moves)
                el("div.sw-table-fleft", null, t.RangeE, t.ChangedE, t.StaleE, t.HoldE),
                el("label.sw-table-pps", { attrs: { for: t.SizeSel.id } }, el("span.sw-table-pps-label", { text: _TF("Rows", "Label of the rows per page select below a data table") }), t.SizeSel),
                el("div.sw-table-nav", null, t.HomeB.E, t.PrevB.E, t.PageE, t.NextB.E));
        }

        /**
         * The options of the rows per page select: Auto (fit the height), 10, 25, 50, 100 and the current size when it is another one.
         */
        UpdateSizeSel() {
            const t = this;
            const auto = !!t.State.AutoRowCount;
            const cur = t.Req.MaxRowCount;
            const sizes = [10, 25, 50, 100];
            const opts = [el("option", { value: "0", text: auto ? _T("Auto ({0})", cur, "Option of the rows per page select of a data table: as many rows as fit the window, {0} is the current number") : _TF("Auto", "Option of the rows per page select of a data table: as many rows as fit the window") })];
            if (!auto && sizes.indexOf(cur) < 0)
                sizes.push(cur);
            sizes.sort((a, b) => a - b).forEach(n => opts.push(el("option", { value: String(n), text: String(n) })));
            const sig = (auto ? "a" : "m") + cur;
            if (t.SizeSig !== sig) {
                /** @type {string} What the size select shows (UpdateSizeSel). */
                t.SizeSig = sig;
                t.SizeSel.replaceChildren(...opts);
            }
            t.SizeSel.value = auto ? "0" : String(cur);
        }

        /**
         * Rows per page: 0 = Auto (fit the free height, Fit), else a fixed size (remembered with the table state). Back to the first page.
         * @param {number} n The size.
         */
        SetPageSize(n) {
            const t = this;
            const r = t.Req;
            if (n > 0) {
                t.State.AutoRowCount = false;
                r.MaxRowCount = Math.max(1, Math.min(1000, n));
            }
            else
                t.State.AutoRowCount = true;
            r.Row = 0;
            t.UpdateSizeSel();
            if (n <= 0) {
                t.FitKey = "";
                t.Save();
                t.Fit();
            }
            t.Changed(true);
        }

        /**
         * Go one page back or forward.
         * @param {number} dir -1 or 1.
         */
        Page(dir) {
            const r = this.Req;
            if (dir < 0) {
                if (r.Row <= 0)
                    return;
                r.Row = Math.max(0, r.Row - r.MaxRowCount);
            }
            else {
                if (!this.Data || this.Data.RowCount <= r.Row + this.Data.Rows.length)
                    return;
                r.Row += r.MaxRowCount;
            }
            this.Changed(true);
        }

        // -------------------------------------------------------------- "Warnings and errors" quick filter (log tables)

        /**
         * The visible index of the log level column (C# type SysWeaver.MessageLevels, filterable by order), -1 if none.
         * @returns {number} The index in Vis.
         */
        LevelCol() {
            return (this.Vis || []).findIndex(v => v.Col.Type === "SysWeaver.MessageLevels" && (v.Col.Props & Props.OrderFilter) !== 0);
        }

        /**
         * True if the "Warnings and errors" quick filter is on: every filter set has the level filter "Greater or equal Warning"
         * (the server compares the level order: Warning and Error pass).
         * @returns {boolean}
         */
        ProblemsOn() {
            const vi = this.LevelCol();
            return vi >= 0 && this.State.Filters.every(set => {
                const f = set[vi];
                return !!f && f.Op === Ops.GreaterEqual && !f.Invert && String(f.Value).trim().toLowerCase() === "warning";
            });
        }

        /**
         * The "Warnings and errors" button: on sets the level filter of every filter set to "Greater or equal Warning" (the same filter as
         * choosing it in the filter row, so it shows there and is remembered with the table state), off clears that filter again.
         */
        ToggleProblems() {
            const t = this;
            const vi = t.LevelCol();
            if (vi < 0)
                return;
            const on = !t.ProblemsOn();
            const v = t.Vis[vi];
            t.State.Filters.forEach(set => {
                let f = set[vi];
                if (!f) {
                    f = set[vi] = new Filter(v.Col.Name);
                    f.ColumnIndex = v.Index;
                }
                if (on) {
                    f.Op = Ops.GreaterEqual;
                    f.Invert = false;
                    f.Value = "Warning";
                }
                else if (f.Op === Ops.GreaterEqual && String(f.Value).trim().toLowerCase() === "warning")
                    f.Value = "";
            });
            t.BuildFilters();
            t.UpdateCardBar();
            t.UpdateProblems();
            t.Changed();
        }

        /** Show the "Warnings and errors" button on log tables only, with its state, and the matching empty state text. */
        UpdateProblems() {
            const t = this;
            const has = t.LevelCol() >= 0;
            t.ProblemsB.E.hidden = !has;
            const on = has && t.ProblemsOn();
            if (t.ProblemsB.Pressed !== on)
                t.ProblemsB.SetPressed(on);
            const run = has && t.IsRunLog();
            t.UpdateEmpty(on
                ? (run
                    ? _TF("No warnings or errors since the service last started. Press \"Warnings and errors\" again to show every message.", "Text shown in the log table (messages since the service started) when the Warnings and errors quick filter is on and no row matches")
                    : _TF("No warnings or errors. Press \"Warnings and errors\" again to show every message.", "Text shown in a log table when the Warnings and errors quick filter is on and no row matches"))
                : null);
            t.UpdateNote(run);
        }

        /**
         * True for the server's in memory log table (Api/debug/LogTable): it holds only the messages since the server last started.
         * @returns {boolean}
         */
        IsRunLog() {
            return /\/LogTable$/i.test(String(this.Url || "").split(/[?#]/)[0]);
        }

        /**
         * The note above the table: the in memory log table says that it covers only the messages since the server last started, and
         * links to the Log file viewer (logFile/logfile.html) for older runs, and to the Problems table (Api/debug/ProblemsTable: the
         * exception trackers count every failure, not every failure is logged). Hidden for other tables. On the table page (the page
         * header) the note is the header's description, else the description says how often the table refreshes.
         * @param {boolean} run True for the in memory log table (IsRunLog, with a level column).
         */
        UpdateNote(run) {
            const t = this;
            /** @type {boolean} The last UpdateNote argument (the in memory log table). */
            t.NoteRun = !!run;
            if (t.PH) {
                //  Local rows (option Rows) have no live refresh: the description says nothing about refreshing
                if (t.Local && !run) {
                    if (t.PHSig !== "local") {
                        t.PHSig = "local";
                        t.PHDesc.replaceChildren();
                    }
                    return;
                }
                const rate = t.Data && t.Data.RefreshRate > 0 ? t.Data.RefreshRate : 5000;
                const secs = Math.max(1, Math.round(rate / 1000));
                //  While the connection is lost the live refresh is paused (ShowOffline): the header says so, not "Live" (UX guide FBK-9)
                const paused = !!t.WaitOnline;
                const sig = run ? "log" : (paused ? "p" : "r") + secs;
                if (t.PHSig === sig)
                    return;
                /** @type {string} What the page header description shows (UpdateNote). */
                t.PHSig = sig;
                if (run) {
                    t.FillNote(t.PHDesc);
                    t.PHDesc.classList.add("sw-table-note");
                }
                else
                    t.PHDesc.replaceChildren(el("span.sw-table-live-dot" + (paused ? ".sw-table-live-paused" : "") + (t.LiveBusy ? ".sw-table-live-busy" : ""), { attrs: { "aria-hidden": "true" } }),
                        el("span", {
                            text: paused
                                ? _TF("Paused: not connected (refreshes when the connection is back)", "Description in the header of a live data table page while the connection to the server is lost")
                                : _T("Live: refreshes every {0} s", secs, "Description in the header of a data table page: the table data is refreshed automatically, {0} is the interval in seconds"),
                        }));
                return;
            }
            if (!run) {
                t.Note.hidden = true;
                return;
            }
            if (!t.Note.firstChild)
                t.FillNote(t.Note);
            t.Note.hidden = false;
        }

        /**
         * A data request starts: the live dot of the page header (UpdateNote) grows (class sw-table-live-busy) until the last running
         * request ends, then shrinks back over 0.25 s (table.css). It stays big for at least 150 ms, so a fast answer is seen too: the
         * dot shows when the table asks the service and when the values were refreshed.
         * @returns {function():void} Call when the request ended (once).
         */
        LiveDot() {
            const t = this;
            /** @type {number} The data requests running now (LiveDot). */
            t.LiveBusy = (t.LiveBusy || 0) + 1;
            const at = Date.now();
            const dot = () => t.PHDesc ? t.PHDesc.querySelector(".sw-table-live-dot") : null;
            const d = dot();
            if (d)
                d.classList.add("sw-table-live-busy");
            let done = false;
            return () => {
                if (done)
                    return;
                done = true;
                setTimeout(() => {
                    t.LiveBusy = Math.max(0, t.LiveBusy - 1);
                    const e = dot();
                    if (e && !t.LiveBusy)
                        e.classList.remove("sw-table-live-busy");
                }, Math.max(0, 150 - (Date.now() - at)));
            };
        }

        /**
         * Fill the log table note (UpdateNote) into an element.
         * @param {HTMLElement} e The element.
         */
        FillNote(e) {
            const url = new URL("logFile/logfile.html", Sw.Url.Root).href;
            e.replaceChildren(
                Sw.Icon("info", "sw-table-note-icon"),
                el("span", { text: _TF("Messages since the service last started (kept in memory). Older runs are in the", "Note above the log table, followed by a link to the log file viewer (the link text is \"Log file\")") + " " }),
                Sw.Cells.CreateLink(url, _TF("Log file", "Link text in the note above the log table: opens the log file viewer that has the messages of all runs"), "_self",
                    _TF("Open the log file viewer (the messages of every run, also before the last start)", "Tool tip of the Log file link in the note above the log table")),
                el("span", { text: ". " + _TF("Not every failure is logged (to keep the log readable under load); how often each part failed, and why, is shown in", "Note above the log table, followed by a link to the Problems table (the link text is \"Problems\")") + " " }),
                //  The exception trackers (ServiceManager ProblemsTable): a failure is always counted there, also when it is not logged
                Sw.Cells.CreateLink(new URL("explore/table.html?q=" + encodeURIComponent("../Api/debug/ProblemsTable"), Sw.Url.Root).href,
                    _TF("Problems", "Link text in the note above the log table: opens the Problems table (how often each part of the system failed)"), "_self",
                    _TF("Open the Problems table: the number of failures per part of the system, the time of the last one and the last error", "Tool tip of the Problems link in the note above the log table")),
                el("span", { text: "." }));
        }

        // -------------------------------------------------------------- wide tables: fit, hidden columns, column chooser

        /**
         * The viewer's column choices, by column name, remembered per table api (localStorage[Key + ".Cols"]): Hide = hidden with the
         * column chooser, Show = shown with the column chooser although the automatic fit would hide it, Wrap = columns whose long
         * texts wrap (the column menu "Wrap text"; rows are one line by default, Design v2, PORTING.md B13), RowNums = show the row numbers (on by
         * default, like the original table; stored only when turned off).
         * @returns {{Hide: string[], Show: string[], Wrap: string[], RowNums: boolean}}
         */
        ColPrefs() {
            const t = this;
            if (!t.ColPref) {
                const p = Sw.Storage.Local.GetJson(t.Key + ".Cols", null);
                const list = x => p && Array.isArray(p[x]) ? p[x].filter(y => typeof y === "string") : [];
                t.ColPref = { Hide: list("Hide"), Show: list("Show"), Wrap: list("Wrap"), RowNums: !(p && p.RowNums === false) };
            }
            return t.ColPref;
        }

        /** Remember the column choices (ColPrefs) and fit the grid again. */
        SaveColPrefs() {
            const t = this;
            const p = t.ColPrefs();
            if (p.Hide.length || p.Show.length || p.Wrap.length || !p.RowNums)
                Sw.Storage.Local.SetJson(t.Key + ".Cols", p);
            else
                Sw.Storage.Local.Remove(t.Key + ".Cols");
            t.ColWKey = "";
            t.UpdateFit(true);
        }

        /**
         * True if a visible column may be hidden automatically: not the first column (the name of the row), a key column, a sorted or
         * filtered column, the log level, or a row action / toggle column.
         * @param {number} vi The index in Vis.
         * @returns {boolean}
         */
        CanAutoHide(vi) {
            const t = this;
            const c = t.Vis[vi].Col;
            if (vi === 0 || (c.Props & Props.IsKey) || vi === t.LevelCol() || /^(Actions|Toggle)\b/.test(c.Format || ""))
                return false;
            if (Sort.Find(t.Req, c.Name).Index >= 0)
                return false;
            return !(t.State.Filters || []).some(set => {
                const f = set && set[vi];
                return !!f && String(f.Value ?? "").trim() !== "";
            });
        }

        /**
         * The columns that are empty on every shown row (all cells blank, isBlankCell) and may be hidden (CanAutoHide).
         * @returns {string[]} Column names.
         */
        EmptyCols() {
            const t = this;
            const rows = t.Body.rows;
            const res = [];
            t.Vis.forEach((v, vi) => {
                if (!t.CanAutoHide(vi))
                    return;
                for (const tr of rows) {
                    const td = tr.cells[vi + 1];
                    if (td && !td.classList.contains("sw-table-blank"))
                        return;
                }
                res.push(v.Col.Name);
            });
            return res;
        }

        /**
         * The first column of the grid (the row number cells, the header corner, the filter row buttons; shown by default like the original
         * table, the viewer can turn it off in Columns): "num" = row numbers (the viewer's choice, default on, or while the filter row is shown, whose reset / clear / add set buttons
         * live there), "status" = a narrow status column for tables with health columns (Health.FailCount / LastFail: the "Failing"
         * warning icon of a row, so it is never told by colour alone; decided by the columns, never by the data, so it never comes and
         * goes), "" = hidden.
         * @returns {string} The mode.
         */
        RnMode() {
            const t = this;
            if (t.ColPrefs().RowNums || t.State.Expanded > 0)
                return "num";
            const health = (t.Vis || []).some(v => Health.FailCount.test(v.Col.Name) || (Health.LastFail.test(v.Col.Name) && v.Col.Type === "System.String"));
            return health ? "status" : "";
        }

        /**
         * Hide columns in the grid layouts (never in the cards, which show every value): the viewer's hidden columns plus the given
         * automatically hidden ones (unless the viewer chose to show them), and the first column when it has nothing to show (RnMode).
         * One style rule per table (scoped by data-sw-tid) hides the header, filter and data cells, so the visible column indices
         * (filters, sort, the keyboard) stay the same.
         * @param {string[]} auto Automatically hidden column names.
         */
        ApplyHidden(auto) {
            const t = this;
            const p = t.ColPrefs();
            const hide = new Set(p.Hide.concat(auto.filter(n => !p.Show.includes(n))));
            const idx = new Set();
            const srv = new Set();
            const rn = t.RnMode();
            if (!rn)
                idx.add(0);
            t.E.classList.toggle("sw-table-rnstatus", rn === "status");
            t.Vis.forEach((v, vi) => {
                //  (the server's default hidden columns, C# TableDataHide: hidden until the viewer shows them)
                const s = srvHidden(v.Col) && !p.Show.includes(v.Col.Name);
                if (s)
                    srv.add(vi + 1);
                if (s || hide.has(v.Col.Name))
                    idx.add(vi + 1);
            });
            t.HiddenIdx = idx;
            /** @type {Set<number>} Cell indices of the columns the server hides by default and the viewer did not show (ApplyHidden: not counted on the Columns button, only in the opened card). */
            t.SrvHiddenIdx = srv;
            const sel = Array.from(idx).map(i => ".sw-table[data-sw-tid=\"" + t.Tid + "\"]:not(.sw-table-cards) .sw-table-grid tr > :nth-child(" + (i + 1) + ")");
            const css = sel.length ? sel.join(",\n") + " { display: none; }" : "";
            if (t.ColStyle.textContent !== css)
                t.ColStyle.textContent = css;
        }

        /**
         * Fit a wide grid into its area (desktop; the cards show every value and the narrow phone grid scrolls sideways by design).
         * When the columns are wider than the area, the columns that are empty on the shown rows are hidden (EmptyCols; never the name,
         * key, sorted, filtered or action columns). Nothing is lost: the "Columns" button says how many columns are hidden and shows
         * them again (ColsMenu, remembered). Then the column widths are locked (LockWidths). Rows stay one line (Design v2, PORTING.md B13; long
         * texts are cut with an ellipsis, the whole value is in the tool tip, the cell menu and the cards). Throttled to one update per
         * frame; `now` runs it at once.
         * @param {boolean} [now] Run now (after a column choice).
         */
        UpdateFit(now) {
            const t = this;
            const run = () => {
                t.FitFrame = 0;
                if (t.Dead || !t.Cols)
                    return;
                const E = t.E.classList;
                const grid = !E.contains("sw-table-cards") && !E.contains("sw-table-narrow");
                const s = t.Scroll;
                const over = () => s.scrollWidth > s.clientWidth + 2;
                let auto = t.AutoHidden || [];
                //  The automatic choice is made again for another column set or width (WidthKey) and after a user change (new rows: paging,
                //  sort, filter, search; AutoAgain), never by a live refresh, so a refresh never makes columns come and go
                const key = t.WidthKey();
                if (key !== t.AutoKey || t.AutoAgain) {
                    const prev = JSON.stringify(auto);
                    if (key !== t.AutoKey)
                        t.ClearWidths();
                    t.AutoAgain = false;
                    auto = [];
                    t.ApplyHidden(auto);
                    //  (no automatic hiding: the original table showed every column, a wide grid scrolls sideways; option AutoHideEmpty
                    //  turns the hiding of the columns that are empty on the shown rows on)
                    if (t.O.AutoHideEmpty === true && grid && t.Body.rows.length && s.clientWidth > 0 && over())
                        auto = t.EmptyCols();
                    if (JSON.stringify(auto) !== prev)
                        t.ClearWidths();
                    t.ApplyHidden(auto);
                    if (t.Body.rows.length)
                        /** @type {string} The WidthKey the automatic hidden columns were chosen for (UpdateFit). */
                        t.AutoKey = key;
                }
                else
                    t.ApplyHidden(auto);
                t.AutoHidden = auto;
                t.LockWidths();
                //  (the pinned column offsets follow the shown columns and their locked widths)
                t.UpdateRnw();
                t.UpdateColsButton();
                t.UpdateEdges();
            };
            if (now) {
                if (t.FitFrame)
                    cancelAnimationFrame(t.FitFrame);
                t.AutoKey = "";
                run();
                return;
            }
            if (!t.FitFrame)
                /** @type {number} The pending animation frame of UpdateFit. */
                t.FitFrame = requestAnimationFrame(run);
        }

        /**
         * What the column widths depend on: the columns, the frame width, the layout, the density, the viewer's column choices and the
         * filter row.
         * @returns {string} The key.
         */
        WidthKey() {
            const t = this;
            const E = t.E.classList;
            return [t.ColSig.length, t.ColSig.substring(0, 64), t.E.clientWidth, E.contains("sw-table-cards"), E.contains("sw-table-narrow"), currentDensity(),
                JSON.stringify(t.ColPrefs()), t.State.Expanded, t.State.Filters.length].join("|");
        }

        /** Forget the locked column widths (LockWidths measures them again). */
        ClearWidths() {
            const t = this;
            t.ColW = [];
            t.ColWKey = "";
            for (const th of t.HeadRow.cells)
                if (th.style.minWidth)
                    th.style.removeProperty("min-width");
            if (t.WidthStyle && t.WidthStyle.textContent)
                t.WidthStyle.textContent = "";
            if (t.Grid && t.Grid.style.minWidth)
                t.Grid.style.removeProperty("min-width");
            if (t.E)
                t.E.classList.remove("sw-table-measure");
        }

        /**
         * Header titles of at most two lines: a title of several words gets the minimum width of its best two line split
         * (the longest of the two lines, measured with the title font), so a narrow numeric column never wraps its title into three or
         * more lines ("Nonpaged / system / memory" -> "Nonpaged system / memory"); table.css still clamps the title to two lines (the
         * tool tip has the whole title). `measure` also caps the title at that width, so it does not widen its column while the natural
         * widths are measured (SpreadWidths). Not for icon only, hinted (explained one letter) or word wrapped titles, nor when the
         * titles are one line (table.css low screens).
         * @param {boolean} measure Cap the titles too (max-width), for the measuring pass.
         */
        FitHeadTitles(measure) {
            const t = this;
            const ctx = t.MeasureCtx || (t.MeasureCtx = document.createElement("canvas").getContext("2d"));
            for (const th of t.HeadRow.cells) {
                const s = th.querySelector(".sw-table-title");
                if (!s)
                    continue;
                if (!measure) {
                    s.style.removeProperty("max-width");
                    continue;
                }
                s.style.removeProperty("min-width");
                s.style.removeProperty("max-width");
                if (!ctx || th.offsetWidth <= 0 || th.classList.contains("sw-table-notitle") || th.classList.contains("sw-table-hinted") || th.classList.contains("sw-table-wrap"))
                    continue;
                const cs = getComputedStyle(s);
                if (cs.whiteSpace === "nowrap")
                    continue;
                const words = s.textContent.trim().split(/\s+/);
                if (words.length < 2)
                    continue;
                ctx.font = cs.fontStyle + " " + cs.fontWeight + " " + cs.fontSize + " " + cs.fontFamily;
                const w = x => ctx.measureText(x).width;
                let best = Infinity;
                for (let k = 1; k < words.length; ++k)
                    best = Math.min(best, Math.max(w(words.slice(0, k).join(" ")), w(words.slice(k).join(" "))));
                const px = Math.ceil(best + parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight) + 2) + "px";
                s.style.minWidth = px;
                s.style.maxWidth = px;
            }
        }

        /**
         * Column widths on a wide frame (after the first render of a column set / frame width): the natural (content)
         * width of every column is measured (titles at most two lines, FitHeadTitles); when they need less than the frame, the free width
         * first goes to the cut text columns (left aligned, ellipsis: up to their whole value, at most a readable measure: 40 % of the
         * frame and 48em), then a little to every column (at most 2.5em each, so the columns are not crammed), and the rest is an empty
         * filler cell at the row end (UpdateFill, table.css sw-table-filled: the row lines, stripes, hover and the header band still reach
         * the frame edge), so a sparse table on 2560 px is not spread over the whole width with empty columns of 500 px. Numbers, dates,
         * icons and actions never get more than their content. When the columns need more than the frame, nothing changes (the grid
         * scrolls sideways).
         * Row actions (UX guide LST-2: always visible, quiet at rest): the Actions column is a real column with the buttons on every row,
         * pinned at the right edge (UpdatePins). When the columns need more than the frame and a row has three or more actions, the
         * column shows a narrow row menu button instead (ActMode "menu", sw-table-actmenu, RowActionItems), so the actions never take
         * the room of the values.
         */
        SpreadWidths() {
            const t = this;
            const g = t.Grid;
            const E = t.E.classList;
            t.FitHeadTitles(true);
            if (E.contains("sw-table-cards") || E.contains("sw-table-narrow") || !t.Body.rows.length) {
                t.FitHeadTitles(false);
                return;
            }
            //  The width for the columns leaves room for a vertical scroll bar when none is shown (classic, non overlay bars; the
            //  filler takes it meanwhile), so a vertical bar that comes with more rows never makes the grid scroll sideways, and the
            //  horizontal bar never takes the height of a row (R1-Table-1: the bars fed each other, the page size flipped)
            const W = t.Scroll.clientWidth - (t.VBarShown() ? 0 : t.BarSize());
            //  (the row actions mode is decided again: the buttons first)
            /** @type {?string} The row actions mode on mouse screens: null = the buttons, "menu" = the row menu button (no room). */
            t.ActMode = null;
            t.UpdateRnw();
            t.E.style.setProperty("--sw-table-fill", "0px");
            g.style.minWidth = "0";
            g.style.width = "max-content";
            const cells = t.HeadRow.cells;
            const nat = Array.from(cells, th => th.offsetWidth);
            //  (the grid's exact width: the column widths are fractional, a sum of rounded widths may be a few pixels short)
            const S = Math.ceil(g.getBoundingClientRect().width);
            const rows = t.Body.rows;
            const text = [];
            const need = [];
            if (S < W * 1.25) {
                const first = rows[0];
                for (let i = 1; i < cells.length; ++i) {
                    const th = cells[i];
                    const td = first.cells[i];
                    if (!nat[i] || !td || th.classList.contains("sw-table-thr") || th.classList.contains("sw-table-thc") || th.classList.contains("sw-table-notitle"))
                        continue;
                    if (td.classList.contains(Sw.Cells.Classes.Actions) || td.classList.contains("sw-table-iconcol"))
                        continue;
                    const cs = getComputedStyle(td);
                    if (cs.textOverflow !== "ellipsis" || cs.whiteSpace !== "nowrap")
                        continue;
                    let n = 0;
                    for (const r of rows) {
                        const c = r.cells[i];
                        if (c && c.scrollWidth > n)
                            n = c.scrollWidth;
                    }
                    text.push(i);
                    need[i] = n;
                }
            }
            g.style.removeProperty("width");
            g.style.removeProperty("min-width");
            t.FitHeadTitles(false);
            const em = parseFloat(getComputedStyle(t.E).fontSize) || 14;
            const sel = i => ".sw-table[data-sw-tid=\"" + t.Tid + "\"]:not(.sw-table-cards) .sw-table-body tr > :nth-child(" + (i + 1) + ")";
            if (!(S < W)) {
                //  A little too wide (ex: 1 px): the cut text columns give the width (the widest first, down
                //  to 12em each), so the grid does not scroll sideways for a few pixels; a grid that is much wider scrolls as before
                /** Narrow the cut text columns by `over` px; false (nothing changed) when they can not give that much. */
                const shrink = over => {
                    const css = [];
                    for (const i of text.slice().sort((a, b) => nat[b] - nat[a])) {
                        const take = Math.min(over, Math.floor(nat[i] - 12 * em));
                        if (take <= 0)
                            continue;
                        css.push(sel(i) + " { max-width: " + (nat[i] - take) + "px; }");
                        over -= take;
                        if (over <= 0)
                            break;
                    }
                    if (over > 0)
                        return false;
                    t.WidthStyle.textContent = css.join("\n");
                    return true;
                };
                if (!shrink(Math.ceil(S - W) + 1) && t.ActMost() > 2) {
                    //  no room for three or more action buttons per row: the row menu button (a narrow pinned column)
                    const last = rows[0] ? rows[0].cells[rows[0].cells.length - 1] : null;
                    const bw = last ? last.offsetWidth : 0;
                    t.ActMode = "menu";
                    t.UpdateRnw();
                    const kw = last ? last.offsetWidth : 0;
                    if (S - bw + kw > W)
                        shrink(Math.ceil(S - bw + kw - W) + 1);
                }
                return;
            }
            const R = Math.max(12 * em, Math.min(W * 0.4, 48 * em));
            const out = nat.slice();
            let free = W - S;
            //  1. the cut text columns, up to their whole value (at most R)
            for (const i of text) {
                const want = Math.min(Math.max(need[i], nat[i]), Math.max(R, nat[i])) - nat[i];
                const give = Math.max(0, Math.min(want, free));
                out[i] += give;
                free -= give;
            }
            //  1b. still cut text next to free width: the rest of their value too (no text is cut while the frame has room)
            for (const i of text) {
                const give = Math.max(0, Math.min(need[i] - out[i], free));
                out[i] += give;
                free -= give;
            }
            //  2. a little air for every column
            const shown = out.reduce((a, w, i) => a + (i > 0 && w > 0 ? 1 : 0), 0);
            if (free > 0 && shown > 0) {
                const each = Math.floor(Math.min(2.5 * em, free / shown));
                for (let i = 1; i < out.length; ++i)
                    if (out[i] > 0) {
                        out[i] += each;
                        free -= each;
                    }
            }
            const css = [];
            for (let i = 1; i < out.length; ++i) {
                if (!(out[i] > 0))
                    continue;
                cells[i].style.minWidth = out[i] + "px";
                //  (a widened text column may show more than the usual cell maximum)
                if (text.indexOf(i) >= 0 && out[i] > nat[i])
                    css.push(sel(i) + " { max-width: " + out[i] + "px; }");
            }
            t.WidthStyle.textContent = css.join("\n");
            //  The rest is the filler (set now, so LockWidths measures the columns without a
            //  stretched grid; UpdateFill measures it again after LockWidths)
            //  (of the whole client width: the room left for a vertical bar is filler too, so the grid is never stretched over it)
            const fill = Math.max(0, Math.floor(t.Scroll.clientWidth - out.reduce((a, b) => a + (b > 0 ? b : 0), 0)));
            E.toggle("sw-table-filled", fill > 0);
            //  (with a filler the grid is exactly the columns plus the filler: never stretched, so LockWidths never locks a stretched width
            //  and the columns do not creep 1 px per refresh)
            if (fill > 0)
                g.style.minWidth = "0";
            else
                g.style.removeProperty("min-width");
            t.E.style.setProperty("--sw-table-fill", fill + "px");
        }

        /**
         * The thickness of a classic scroll bar of the rows area (px; 0 with overlay scroll bars), measured once per table with a
         * probe that has the area's scroll bar style.
         * @returns {number} The thickness.
         */
        BarSize() {
            const t = this;
            if (t.BarPx >= 0)
                return t.BarPx;
            const d = document.createElement("div");
            const cs = getComputedStyle(t.Scroll);
            d.style.cssText = "position:absolute;left:-9999px;top:0;width:100px;height:100px;overflow:scroll;visibility:hidden";
            d.style.scrollbarWidth = cs.scrollbarWidth || "";
            (t.Area || document.body).append(d);
            /** @type {number} The scroll bar thickness (BarSize; -1 until measured). */
            t.BarPx = Math.max(0, d.offsetWidth - d.clientWidth);
            d.remove();
            return t.BarPx;
        }

        /**
         * The rows area shows a vertical scroll bar now (classic bars: it takes width).
         * @returns {boolean}
         */
        VBarShown() {
            const s = this.Scroll;
            const cs = getComputedStyle(s);
            return s.offsetWidth - s.clientWidth - (parseFloat(cs.borderLeftWidth) || 0) - (parseFloat(cs.borderRightWidth) || 0) > 0.5;
        }

        /**
         * The filler at the row end (table.css sw-table-filled: an empty cell after the last column, `--sw-table-fill` wide, in the
         * row colours): the grid always reaches the frame edge, the columns keep their widths. Measured after every LockWidths (a
         * column that grew takes it from the filler, the grid never scrolls sideways because of the filler). In developer mode a shown
         * column that is 0 px wide is a `[Sw UX]` warning (ZeroCols: its values, ex: the row actions, could not be seen or clicked).
         */
        UpdateFill() {
            const t = this;
            const E = t.E.classList;
            const st = t.E.style;
            if (E.contains("sw-table-cards") || E.contains("sw-table-narrow") || !t.Body.rows.length) {
                E.remove("sw-table-filled");
                st.removeProperty("--sw-table-fill");
                return;
            }
            const g = t.Grid;
            const measure = () => {
                st.setProperty("--sw-table-fill", "0px");
                const om = g.style.minWidth;
                g.style.minWidth = "0";
                g.style.width = "max-content";
                const S = Math.ceil(g.getBoundingClientRect().width);
                g.style.removeProperty("width");
                g.style.minWidth = om;
                return Math.max(0, Math.floor(t.Scroll.clientWidth - S));
            };
            const fill = measure();
            E.toggle("sw-table-filled", fill > 0);
            //  (with a filler the grid is exactly the columns plus the filler: never stretched, so LockWidths never locks a stretched width
            //  and the columns do not creep 1 px per refresh)
            if (fill > 0)
                g.style.minWidth = "0";
            else
                g.style.removeProperty("min-width");
            st.setProperty("--sw-table-fill", fill + "px");
            //  (developer guard rail: a shown column that takes no width hides its values, ex: row actions nobody can see or click)
            const dev = typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode;
            if (dev && Sw.DevWarn) {
                const zero = t.ZeroCols();
                if (zero.length)
                    Sw.DevWarn("Sw.Table " + (t.Key || "") + ": the shown column(s) " + zero.map(n => '"' + n + '"').join(", ") +
                        " are 0 px wide in the grid, so their values can't be seen or clicked. A column is either shown with its content width or hidden (ApplyHidden).", "table:zerocol:" + t.Key + ":" + zero.join(","));
            }
        }

        /**
         * The self check of the grid widths (UpdateFill warns in developer mode; tests call it): the shown columns (not hidden by
         * ApplyHidden) that are 0 px wide in the grid layout while the table is on screen.
         * @returns {string[]} The column names, empty when every shown column has a width (or the grid is not shown: cards, no rows, no frame).
         */
        ZeroCols() {
            const t = this;
            const E = t.E.classList;
            const res = [];
            if (!t.HeadRow || !t.Vis || E.contains("sw-table-cards") || !t.Body.rows.length || !(t.Scroll.clientWidth > 0) || !t.E.getClientRects().length)
                return res;
            const cells = t.HeadRow.cells;
            t.Vis.forEach((v, vi) => {
                const th = cells[vi + 1];
                if (th && !t.HiddenIdx.has(vi + 1) && th.getBoundingClientRect().width < 1)
                    res.push(v.Col.Name);
            });
            return res;
        }

        /**
         * The most row action buttons a row offers (the last grid column has the Actions format; 0 = no row actions).
         * @returns {number}
         */
        ActMost() {
            const t = this;
            const last = t.Vis && t.Vis.length > 1 ? t.Vis[t.Vis.length - 1].Col : null;
            if (!last || !/^Actions(;|$)/.test(String(last.Format || "")))
                return 0;
            let most = 0;
            for (const r of t.Body.rows) {
                const td = r.cells[r.cells.length - 1];
                const n = td ? td.querySelectorAll("button.sw-cell-action:not([hidden])").length : 0;
                if (n > most)
                    most = n;
            }
            return most;
        }

        /**
         * Stable column widths (Design v2, PORTING.md B13): the widths of the header cells are measured after a render and kept as their minimum
         * widths; they only grow while the table is shown (a wider value on a later page or refresh), never shrink, so paging and live
         * refreshes never re-flow the columns. Measured again when the columns, the frame width, the layout, the density or the column
         * choices change (WidthKey). Not in the cards.
         */
        LockWidths() {
            const t = this;
            if (t.E.classList.contains("sw-table-cards") || !t.Body.rows.length) {
                t.UpdateFill();
                return;
            }
            const key = t.WidthKey();
            if (key !== t.ColWKey) {
                t.ClearWidths();
                t.ColWKey = key;
                t.SpreadWidths();
            }
            const cells = t.HeadRow.cells;
            const w = Array.from(cells, th => th.offsetWidth);
            //  min-width is set in the cell's own box model: table.css makes the table border-box on any page, but an app
            //  style may still make a header content-box; then the padding and borders are taken off, else every pass would add them
            //  (the grid grew ~5 px per column per frame on pages without sw-root)
            const cs = cells.length ? getComputedStyle(cells[0]) : null;
            const extra = cs && cs.boxSizing === "content-box"
                ? Array.from(cells, th => {
                    const s = getComputedStyle(th);
                    return parseFloat(s.paddingLeft) + parseFloat(s.paddingRight) + parseFloat(s.borderLeftWidth) + parseFloat(s.borderRightWidth);
                })
                : null;
            for (let i = 0; i < cells.length; ++i) {
                if (!w[i])
                    continue;
                if (!(t.ColW[i] >= w[i])) {
                    t.ColW[i] = w[i];
                    cells[i].style.minWidth = Math.max(0, w[i] - (extra ? extra[i] || 0 : 0)) + "px";
                }
            }
            t.UpdateFill();
        }

        /** The column chooser button: shown in the grid layouts of tables with more than one column; a badge counts the hidden columns. */
        UpdateColsButton() {
            const t = this;
            const b = t.ColsB;
            //  (the columns the server hides by default are not counted: they are the usual view of the table; the tool tip says that
            //  they can be shown)
            const srv = t.SrvHiddenIdx ? t.SrvHiddenIdx.size : 0;
            let n = t.HiddenIdx.size - srv;
            if (t.HiddenIdx.has(0))
                --n;
            b.E.hidden = t.E.classList.contains("sw-table-cards") || t.CardsOn() || t.Vis.length < 2;
            t.ColsBadge.textContent = n > 0 ? String(n) : "";
            t.ColsBadge.hidden = n <= 0;
            b.SetTitle(n
                ? (n === 1 ? _TF("1 hidden column", "Short label of the column chooser button below a data table when one column is hidden")
                    : _T("{0} hidden columns", n, "Short label of the column chooser button below a data table, {0} is the number of hidden columns (2 or more)")) + "\n" +
                _TF("Some columns are hidden to fit the table (empty ones, or hidden by you): choose the columns to show", "Tool tip of the column chooser button below a data table when columns are hidden")
                : srv > 0
                    ? _T("Choose the columns to show: {0} more columns can be shown", srv, "Tool tip of the column chooser button below a data table that has columns hidden by default, {0} is their number")
                    : _TF("Choose the columns to show", "Tool tip of the column chooser button below a data table"));
            b.SetLabel(n
                ? (n === 1 ? _TF("1 hidden column", "Short label of the column chooser button below a data table when one column is hidden")
                    : _T("{0} hidden columns", n, "Short label of the column chooser button below a data table, {0} is the number of hidden columns (2 or more)"))
                : _TF("Columns", "Short label of the column chooser button below a data table (show or hide columns)"));
            b.E.classList.toggle("sw-table-colshidden", n > 0);
        }

        /** The column chooser menu: one check item per column (kept open), the row numbers, then "Show all columns". */
        ColsMenu() {
            const t = this;
            const p = t.ColPrefs();
            const items = [{ Title: _TF("Columns", "Heading of the column chooser menu of a data table (a list of the columns follows)") }];
            t.Vis.forEach((v, vi) => {
                const c = v.Col;
                const name = c.Name;
                const auto = t.AutoHidden.includes(name) && !p.Show.includes(name);
                const srv = srvHidden(c) && !p.Show.includes(name);
                items.push({
                    Text: shortTitleHint(c) || c.Title || name,
                    Title: auto ? _TF("Hidden to fit the table: it is empty on the shown rows", "Tool tip of a column in the column chooser of a data table that was hidden automatically")
                        : srv ? (c.Desc ? c.Desc + "\n" : "") + _TF("Hidden by default: check it to show the column", "Tool tip of a column in the column chooser of a data table that the server hides by default")
                            : (c.Desc || null),
                    Checked: !t.HiddenIdx.has(vi + 1),
                    Keep: true,
                    OnClick: it => {
                        p.Hide = p.Hide.filter(x => x !== name);
                        p.Show = p.Show.filter(x => x !== name);
                        if (!it.Checked)
                            p.Hide.push(name);
                        else if (t.AutoHidden.includes(name) || srvHidden(c))
                            p.Show.push(name);
                        t.SaveColPrefs();
                    },
                });
            });
            items.push("-", {
                Text: _TF("Row numbers", "Menu item of the column chooser of a data table that shows the row numbers (the first column)"),
                Title: _TF("Show the number of each row in a first column", "Tool tip of the row numbers item of the column chooser of a data table"),
                Checked: !!p.RowNums, Keep: true,
                OnClick: it => {
                    p.RowNums = !!it.Checked;
                    t.SaveColPrefs();
                },
            }, {
                Text: _TF("Show all columns", "Menu item of the column chooser of a data table that shows every column"),
                Icon: "eye",
                OnClick: () => {
                    p.Hide = [];
                    p.Show = t.Vis.filter(v => t.AutoHidden.includes(v.Col.Name) || srvHidden(v.Col)).map(v => v.Col.Name);
                    t.SaveColPrefs();
                },
            });
            Sw.Menu.Show(t.ColsB.E, items, { Align: "end", Label: _TF("Columns", "Heading of the column chooser menu of a data table (a list of the columns follows)") });
        }

        // -------------------------------------------------------------- health hints (Health)

        /**
         * Mark a failing row (Health.FailCount above 0, or Health.LastFail set): class sw-table-bad (table.css: danger tint and edge) and a
         * warning icon with the word "Failing" in the row number cell (the reason in its tool tip / accessible name), so it is not told
         * by colour alone.
         * @param {HTMLTableRowElement} tr The row.
         * @param {function(object, number): *} get The value of a column of a row.
         * @param {object} row The row data.
         */
        RowHealth(tr, get, row) {
            const t = this;
            const why = [];
            t.Vis.forEach(v => {
                const c = v.Col;
                const val = get(row, v.Index);
                if (Health.FailCount.test(c.Name) && typeof val === "number" && val > 0)
                    why.push((c.Title || c.Name) + ": " + val);
                else if (Health.LastFail.test(c.Name) && c.Type === "System.String" && typeof val === "string" && val.trim() && val.trim() !== "-")
                    why.push((c.Title || c.Name) + ": " + val.trim().split("\n")[0].substring(0, 160));
            });
            const bad = why.length > 0;
            tr.classList.toggle("sw-table-bad", bad);
            const rn = tr.cells[0];
            let h = rn.SwHealth;
            if (!bad) {
                if (h)
                    h.hidden = true;
                return;
            }
            if (!h) {
                h = rn.SwHealth = el("span.sw-table-health", null,
                    Sw.Icon("warning", "sw-table-health-icon"),
                    el("span.sw-table-health-text", { text: _TF("Failing", "Word next to the warning icon of a data table row that reports a failure (ex: a source with failures)") }));
                rn.insertBefore(h, rn.SwNum ? rn.SwNum.nextSibling : rn.firstChild);
            }
            h.hidden = false;
            const tip = _TF("This row reports a failure", "Tool tip of the warning icon of a failing data table row, the reasons follow") + ":\n" + why.join("\n");
            if (h.title !== tip) {
                h.title = tip;
                h.setAttribute("aria-label", tip);
            }
        }

        /**
         * Mark a high used percentage (Health.UsedPercent column with a "%" number format): from Health.Warn % a warning, from Health.Bad %
         * a danger colour, each with an icon and a word after the value ("High" / "Critical").
         * @param {HTMLTableCellElement} td The cell.
         * @param {object} c The column.
         * @param {*} value The value.
         */
        CellHealth(td, c, value) {
            const old = td.querySelector(":scope > .sw-health-tag");
            let lvl = 0;
            if (typeof value === "number" && Health.UsedPercent.test(c.Name) && /^Number\b.*%/.test(c.Format || ""))
                lvl = value >= Health.Bad ? 2 : value >= Health.Warn ? 1 : 0;
            td.classList.toggle("sw-health-warn", lvl === 1);
            td.classList.toggle("sw-health-bad", lvl === 2);
            if (!lvl) {
                if (old)
                    old.remove();
                return;
            }
            const text = lvl === 2
                ? _TF("Critical", "Word after a used percentage in a data table (ex: a drive) that is at or above the danger level")
                : _TF("High", "Word after a used percentage in a data table (ex: a drive) that is at or above the warning level");
            if (old && old.dataset.lvl === String(lvl))
                return;
            if (old)
                old.remove();
            const tag = el("span.sw-health-tag", {
                title: lvl === 2
                    ? _T("{0}% or more is used", Health.Bad, "Tool tip of the Critical mark of a used percentage in a data table, {0} is the danger level")
                    : _T("{0}% or more is used", Health.Warn, "Tool tip of the High mark of a used percentage in a data table, {0} is the warning level"),
            }, Sw.Icon("warning", "sw-health-icon"), el("span", { text: text }));
            tag.dataset.lvl = String(lvl);
            td.append(tag);
        }

        // -------------------------------------------------------------- update loop

        /**
         * Replace the rows of a local data table (option Rows) and show them (the page, sort, search and filters are kept; the row
         * number is kept when the page still has rows).
         * @param {Array<object|Array>} rows The new rows (objects by column name, or value arrays in column order).
         * @param {boolean} [live] The rows are a live update (new values of a polling page, not the result of a user action): shown like a
         *  live refresh of a server table, changed values are tinted and the row order is held while the pointer or the focus is in the
         *  rows (UX guide FBK-7). Default: shown at once like a user change.
         * @returns {Promise<void>} Resolves when the rows are shown.
         */
        SetRows(rows, live) {
            if (!this.Local)
                throw new Error("Sw.Table.SetRows: only for a table created with the option Rows (local data)");
            this.Local.SetRows(rows);
            if (this.Req.Row >= (rows || []).length)
                this.Req.Row = 0;
            return live ? this.UpdateNow() : this.Changed(true);
        }

        /**
         * Fetch now (cancels the refresh timer; a user change supersedes a running request, else it runs right after it).
         * @returns {Promise<void>} Resolves when the new data is shown (or the request failed).
         */
        UpdateNow() {
            const p = new Promise(res => this.Waiters.push(res));
            this.Schedule(0);
            return p;
        }

        /**
         * (Re)start the refresh timer.
         * @param {number} ms Delay (negative: no automatic refresh, ex: the error state waits for "Try again").
         */
        Schedule(ms) {
            clearTimeout(this.Timer);
            this.TimerDue = 0;
            if (ms < 0)
                return;
            if (this.Dead)
                return;
            //  Not allowed / not found (still retried every RetryDelay, like the original table): UpdateNow callers must not wait
            if (this.IsDenied) {
                const w = this.Waiters;
                this.Waiters = [];
                w.forEach(f => f());
            }
            this.TimerDue = Date.now() + ms;
            this.Timer = setTimeout(() => this.Update(), ms);
        }

        /**
         * The automatic page size changed (Fit): fetch the new page without making it a user change, so it never aborts a running
         * request (it runs right after it, Again) and is never sent faster than one per AutoMinGap (the first one after a change of
         * the window, the layout or the columns runs at once). A refresh already due sooner takes the new size with it. The rows are
         * shown like a user change (no flash, the server's order: AutoSize) and keep their row results and kept rows (QuietChange).
         */
        AutoFetch() {
            const t = this;
            t.Save();
            t.QuietChange = true;
            t.AutoSize = true;
            const now = Date.now();
            const wait = Math.max(0, t.LastAutoSize + AutoMinGap - now);
            if (t.Running) {
                //  (Update: a non user cycle asked while one runs runs right after it)
                t.Again = true;
                t.LastAutoSize = now;
                return;
            }
            if (t.TimerDue && t.TimerDue <= now + wait)
                return;
            t.LastAutoSize = now + wait;
            t.Schedule(wait);
        }

        /**
         * The request guard (a framework safety net against request loops, ex: a layout that changes the page size on every render):
         * the requests sent since the last user input (pointer, key, input) are counted over GuardWindow; more than twice what the
         * server's refresh rate asks for (plus 4) throttles the table hard (one request per refresh rate, at least GuardGap) until the
         * next user input, with one developer warning (Sw.DevWarn). Every request passes here (Update), user flagged or not: a loop of
         * code that calls Changed() is caught too.
         * @param {number} now Date.now().
         * @returns {number} How long the next request must wait (ms, 0 = send now).
         */
        RequestGuard(now) {
            const t = this;
            const from = Math.max(now - GuardWindow, t.LastInput);
            t.Sent = t.Sent.filter(x => x > from);
            if (!t.Sent.length) {
                t.Throttled = false;
                return 0;
            }
            const d = t.Data;
            const rate = Math.max(100, d && d.RefreshRate > 0 ? d.RefreshRate : 5000);
            const max = 4 + 2 * Math.ceil(GuardWindow / rate);
            if (!t.Throttled && t.Sent.length < max)
                return 0;
            if (!t.Throttled) {
                t.Throttled = true;
                Sw.DevWarn("Sw.Table " + t.Url + ": " + t.Sent.length + " requests in " + (GuardWindow / 1000) + " s without user input (the server's refresh rate is " +
                    rate + " ms): a request loop (ex: a layout or code that changes the request on every render)? Throttled to one request per " +
                    Math.max(rate, GuardGap) + " ms until the next user input", "Sw.Table.Guard." + t.Url);
            }
            return Math.max(0, t.Sent[t.Sent.length - 1] + Math.max(rate, GuardGap) - now);
        }

        /**
         * One update cycle: fetch, render, schedule the next refresh. While the page is hidden periodic refreshes pause.
         * Timed refreshes never overlap (one asked while a request runs runs right after it, Again). A user change (UserChange: sort,
         * filter, search, page, Refresh) supersedes the running cycle at once: its answer is ignored (Gen); its request is not aborted
         * but finishes, as the original table (a refresh may be a long poll the server holds until the data changes). The
         * automatic page size is not a user change (AutoFetch). Every request passes the request guard (RequestGuard).
         * @returns {Promise<void>}
         */
        async Update() {
            const t = this;
            clearTimeout(t.Timer);
            t.TimerDue = 0;
            if (t.Dead)
                return;
            //  The request guard: a table that sends too many requests without user input waits (the state, the user flag and the
            //  waiters stay for the delayed cycle; a running request is not touched)
            if (!t.Local && !t.First) {
                const hold = t.RequestGuard(Date.now());
                if (hold > 0) {
                    t.TimerDue = Date.now() + hold;
                    t.Timer = setTimeout(() => t.Update(), hold);
                    return;
                }
            }
            /** @type {Array<function>} The waiters of a superseded cycle (resolved when this one completes). */
            let taken = [];
            if (t.Running) {
                if (!t.UserChange) {
                    t.Again = true;
                    return;
                }
                //  Supersede: the running cycle ignores its answer and leaves the state (Running, waiters, timer) to this one. Its
                //  request is not aborted: it finishes and its answer is ignored, as the original table (S5-A-5, AS5-7)
                ++t.Gen;
                taken = t.RunWaiters;
                t.RunWaiters = [];
                t.Again = false;
            }
            //  (parity with the original table: the refresh goes on while the page is hidden or offline; a failure retries in RetryDelay)
            t.Running = true;
            const gen = ++t.Gen;
            const waiters = taken.concat(t.Waiters);
            t.Waiters = [];
            t.RunWaiters = waiters;
            const user = t.UserChange;
            t.UserChange = false;
            //  (the automatic page size: shown like a user change, without being one)
            const auto = t.AutoSize;
            t.AutoSize = false;
            if (!t.Local)
                t.Sent.push(Date.now());
            const busy = user ? setTimeout(() => t.E.classList.add("sw-table-busy"), 150) : 0;
            let next = 3000;
            try {
                next = await t.Fetch(user, gen, auto);
            }
            catch (e) {
                //  Superseded (aborted, or answered after a newer cycle started): the newer cycle shows its own result
                if (gen !== t.Gen)
                    return;
                t.ShowError(e);
                t.FirstLoaded();
                //  Retry like the original table (every RetryDelay, also in the error, offline, not allowed and not found states; "Try
                //  again" and "net.online" fetch at once)
                next = t.OnlineAgain ? 0 : RetryDelay;
                //  (a card template that can't be used stays that way: "Try again" tries again)
                if (e && e.SwCardError)
                    next = -1;
            }
            finally {
                clearTimeout(busy);
                //  (a superseded cycle leaves the state, the waiters and the next refresh to the newer one)
                if (gen === t.Gen) {
                    t.OnlineAgain = false;
                    t.E.classList.remove("sw-table-busy");
                    t.Running = false;
                    t.RunWaiters = [];
                    waiters.forEach(w => w());
                    if (t.Again) {
                        t.Again = false;
                        t.Schedule(0);
                    }
                    else
                        t.Schedule(next);
                }
            }
        }

        /**
         * Send the request and render the response. Nothing is rendered when a newer update cycle superseded this one (Update): it
         * throws an AbortError instead.
         * @param {boolean} user The request is caused by the user.
         * @param {number} [gen] The update cycle (Gen) of this request (default: the current one).
         * @param {boolean} [auto] The automatic page size (AutoFetch): rendered like a user change (no flash, the server's order).
         * @returns {Promise<number>} Delay until the next refresh (ms).
         */
        async Fetch(user, gen, auto) {
            const t = this;
            const r = t.Req;
            if (gen === undefined)
                gen = t.Gen;
            /** Throws when a newer update cycle superseded this one (its answer is ignored). */
            const superseded = () => {
                if (gen !== t.Gen)
                    throw new DOMException("Superseded by a newer table update", "AbortError");
            };
            r.Cc = t.Cc;
            const fl = [];
            t.State.Filters.forEach(set => set.forEach(f => {
                if (f.Value !== "")
                    fl.push(f);
            }));
            r.Filters = fl.length ? fl : null;
            //  Infinite scrolling: the part of the list this cycle asks for (the request itself stays at row 0)
            const plan = t.ScrollOn ? t.ScrollPlan() : null;
            const body = plan ? Object.assign({}, r, { Row: plan.Row, MaxRowCount: plan.Max }) : r;
            //  Tell a hosting window (ex: Fenrir) the current filters, so it can scope a background search to the shown rows
            //  (only framed server tables; any origin, as the host may be another site)
            try {
                if (!t.Local && window.parent && window.parent !== window)
                    window.parent.postMessage({
                        Type: "TableSearchParams",
                        Param: r.Param ?? null,
                        Filters: r.Filters,
                        SearchText: r.SearchText ?? null,
                    }, "*");
            }
            catch {
            }
            /**
             * Fetch and validate a reply (an empty or unreadable reply throws; a superseded cycle throws an AbortError).
             * @param {boolean} reload Bypass caches.
             * @returns {Promise<object>} The table data.
             */
            const get = async reload => {
                //  A server that does not answer (ex: a table that calls an unreachable provider) ends in the error state with "Try again"
                const ms = t.O.Timeout > 0 ? t.O.Timeout : RequestTimeout;
                const ac = new AbortController();
                let timedOut = false;
                const timer = ms > 0 ? setTimeout(() => {
                    timedOut = true;
                    ac.abort();
                }, ms) : 0;
                let d;
                //  The live dot of the page header grows while the request runs (LiveDot)
                const busy = t.Local ? null : t.LiveDot();
                try {
                    d = t.Local ? t.Local.Query(body) : await Sw.Net.Send(t.Url, body, { Reload: reload, Signal: ac.signal });
                }
                catch (e) {
                    superseded();
                    if (timedOut)
                        throw new Error(_T("The service did not answer within {0} seconds", Math.round(ms / 1000), "An error message displayed when a table data request timed out, {0} is the number of seconds waited"));
                    throw e;
                }
                finally {
                    clearTimeout(timer);
                    if (busy)
                        busy();
                }
                superseded();
                if (!d || typeof d !== "object" || !Array.isArray(d.Rows) || (!t.Cols && !Array.isArray(d.Cols)))
                    throw new Error(d === undefined || d === null
                        ? _TF("The service sent no data or data that could not be read", "An error message displayed when the service reply to a table data request was empty or not valid json")
                        : _TF("The service sent data in an unexpected format", "An error message displayed when the service reply to a table data request was json but not table data"));
                //  Never more rows than the page: an api that ignores MaxRowCount (ex: answers all 10 000 rows) gets its page cut out (from
                //  the asked row when the reply is the whole list), with one console warning, so the page never draws thousands of rows
                const max = Math.max(1, body.MaxRowCount || 25);
                if (d.Rows.length > max) {
                    const whole = body.Row > 0 && (d.RowCount || 0) <= d.Rows.length;
                    if (!t.CapWarned) {
                        /** @type {boolean} The too many rows warning was written (once per table). */
                        t.CapWarned = true;
                        console.warn("Sw.Table " + t.Url + ": the api answered " + d.Rows.length + " rows for a page of " + max + "; only " + max +
                            " are shown (the api should honor TableDataRequest.Row / MaxRowCount; for rows that are all in the page use the option Rows)");
                    }
                    d.Rows = whole ? d.Rows.slice(body.Row, body.Row + max) : d.Rows.slice(0, max);
                }
                return d;
            };
            let data = await get(user);
            //  (TableData.DefaultOrder, the server's order for a request without one, is not copied into the request: the original table
            //  sent no order until the viewer chose one)
            if (t.First) {
                if (data.Rows.length <= 0 && r.Row > 0 && !plan) {
                    //  The stored page is beyond the end: go to the first page
                    r.Row = 0;
                    t.Save();
                    data = await get(true);
                }
                /** @type {string} The title the server sent ("" when none; the Name then comes from the url). */
                t.ServerTitle = data.Title || "";
                if (t.NameFromTitle && data.Title)
                    t.Name = data.Title;
                t.Grid.setAttribute("aria-label", t.Name);
                //  (the header title: the server title unless the menu / link name decided it, or the menu lookup still runs)
                if (t.PHTitle && !t.HeadNamed && !(t.MenuNameP && t.PHTitle.classList.contains("sw-table-ph-pending") && !t.MenuDone))
                    t.SetHeaderName(t.Name, false);
                data.Title = t.Name;
                //  The card template: chosen once, like the columns (the option first, then the server's). false = none, also not the server's
                await t.SetupCard(t.O.Card === false ? null : (t.O.Card || data.Card));
                superseded();
            }
            const first = t.First;
            t.First = false;
            const cc = data.Cc;
            //  (infinite scrolling: the rows of the answer join the loaded rows; nothing to show when a refresh found no change)
            const shown = plan ? t.ScrollMerge(data, plan) : data;
            try {
                if (shown)
                    t.Render(shown, user || first || !!auto);
                if (plan) {
                    if (t.ScrollTop) {
                        t.ScrollTop = false;
                        t.Scroll.scrollTop = 0;
                    }
                    t.UpdateScrollEnd();
                    t.ScrollEnsure();
                }
            }
            catch (e) {
                console.error("Failed to render table: " + (e && e.stack ? e.stack : e));
            }
            t.Cc = cc;
            t.ClearError();
            /** @type {Date} The time of the last successful update (ShowOffline: "Not updated since"). */
            t.LastOk = new Date();
            t.FirstLoaded(data);
            //  table.html: when the page has the keyboard focus (ex: opened with Enter from the app menu) start in the grid
            const ae = document.activeElement;
            if (first && t.O.Page && document.hasFocus() && (!ae || ae === document.body || ae === document.documentElement) && !Sw.Platform.IsTouch && (t.PHTitle || firstStop(t.E)))
                t.FocusGrid();
            if (t.State.AutoRowCount && r.Row === 0)
                requestAnimationFrame(() => t.Fit());
            else
                t.HostFit();
            //  (local rows change only through SetRows / the user: no polling)
            if (t.Local)
                return -1;
            let rate = data.RefreshRate;
            if (!(rate > 0))
                rate = 5000;
            return Math.max(100, rate);
        }

        /**
         * Show a failed request: without data on screen the error state (text + "Try again") replaces the table, with (stale) data a
         * single error toast is shown (repeated failures merge into it). Both are removed by the next successful request (ClearError).
         * A refused request (403, Sw.Access.IsDenied) shows the shared "not allowed" state instead (ShowDenied).
         * @param {*} e The error.
         */
        ShowError(e) {
            const t = this;
            if (Sw.Access && Sw.Access.IsDenied(e)) {
                t.ShowDenied(e);
                return;
            }
            //  The same reason repeated by server retries ("(No connection ...) (No connection ...) ...") is shown once
            const msg = ((e && e.message) ? e.message : String(e)).replace(/(\([^()]*(?:\([^()]*\)[^()]*)*\))(?:\s*\1)+/g, "$1");
            const text = _TF("Could not load the data", "An error message displayed when there was an error to fetch table data from the server, followed by the reason") + ": " + msg;
            //  An unknown table url (404) is permanent: the shared "not found" state (no "Try again", no tool bars)
            if (!t.Data && ((e && e.Status === 404) || /^\s*404\b/.test(msg))) {
                t.ShowNotFound(e);
                return;
            }
            if (!t.Data) {
                //  Name the page after the table (not the generic "Table") while it has no data: the header title (the menu name),
                //  else the name from the url
                if (t.O.Page)
                    t.HeaderReady().then(n => {
                        n = n || Sw.Page.NameFromUrl(t.Url);
                        Sw.Page.SetName(n);
                        t.SetHeaderName(n, false);
                    });
                //  The footer follows the state: not "Loading..." under an error
                t.RangeE.textContent = _TF("Not loaded", "The range text of a data table when its data could not be loaded (the error is shown above it)");
                console.warn(text);
                //  A plain sentence (ErrorSummary), the technical text (with the table api, as requested: relative to the page, shown below
                //  the site root, so a wrong url is obvious) behind a "Technical details" disclosure with a Copy button
                const abs = Sw.Url.Absolute(t.Url) || "";
                const detail = text + " (" + (abs.startsWith(Sw.Url.Root) ? abs.substring(Sw.Url.Root.length) : t.Url) + ")";
                t.ErrorText.textContent = e && e.SwCardError ? msg : ErrorSummary(msg);
                t.ErrorDetailText.textContent = detail;
                t.Error.hidden = false;
                t.Empty.hidden = true;
                t.E.classList.add("sw-table-failed");
                //  No data: nothing to refresh, download, search or choose columns of ("Try again" retries)
                t.SetToolsDisabled(true);
                return;
            }
            //  Offline: the shell line says it once, the rows stay (ShowOffline)
            if (t.ShowOffline(e))
                return;
            //  A failed page change keeps the old rows and range: tell screen readers too (once per failure streak, not on every refresh)
            if (!t.ErrorToast && Sw.Page.Announce)
                Sw.Page.Announce(text);
            t.ErrorToast = Sw.Fail(text);
        }

        /**
         * The user may not see this table (403): the shared "not allowed" state (Sw.Page.ShowNotAllowed: who is signed in, the role
         * needed, a link Home, no "Try again") replaces the grid, the tool bars (Refresh, Download, search) are hidden, the refresh
         * stops, and "access.denied" is emitted (guarded actions of the page, ex: an upload button, get disabled).
         * @param {*} e The refused request.
         */
        ShowDenied(e) {
            const t = this;
            if (t.IsDenied)
                return;
            /** @type {boolean} The server refused the data (403): no more requests. */
            t.IsDenied = true;
            clearTimeout(t.Timer);
            if (t.ErrorToast) {
                t.ErrorToast.Close();
                t.ErrorToast = null;
            }
            t.Error.hidden = true;
            t.Empty.hidden = true;
            t.DeniedHost.hidden = false;
            t.E.classList.add("sw-table-failed", "sw-table-denied-on");
            t.SetToolsDisabled(true);
            console.warn("Table " + t.Url + ": not allowed (" + ((e && e.message) || e) + ")");
            //  The table page: the page wording, named after the table ("You don't have access to Services", title "Services")
            Sw.Page.ShowNotAllowed(t.O.Page
                ? { Url: t.Url, Error: e, Element: t.DeniedHost, Scope: "page", Name: t.FallbackName() }
                : { Url: t.Url, Error: e, Element: t.DeniedHost });
        }

        /**
         * The table api does not exist (404): the shared "not found" state (Sw.Page.ShowNotFound: names the url, on the table page
         * also the q rule and a link to the list of all data tables; no "Try again") replaces the grid, the tool bars are hidden and
         * the refresh stops.
         * @param {*} e The failed request.
         */
        ShowNotFound(e) {
            const t = this;
            if (t.IsDenied)
                return;
            /** @type {boolean} No more requests (also set for a missing table: a 404 is not transient). */
            t.IsDenied = true;
            clearTimeout(t.Timer);
            if (t.ErrorToast) {
                t.ErrorToast.Close();
                t.ErrorToast = null;
            }
            t.Error.hidden = true;
            t.Empty.hidden = true;
            t.DeniedHost.hidden = false;
            t.E.classList.add("sw-table-failed", "sw-table-denied-on");
            t.SetToolsDisabled(true);
            console.warn("Table " + t.Url + ": not found (" + ((e && e.message) || e) + ")");
            const page = !!t.O.Page;
            Sw.Page.ShowNotFound({
                Url: t.Url, Element: t.DeniedHost, Scope: page ? "page" : "section", Name: t.FallbackName(),
                Hint: page ? _TF("The q parameter is the table API url relative to the site root (example: Api/debug/ApiTable) or to this page (example: ../Api/debug/ApiTable).", "Hint on the table page when the table in the q url parameter does not exist on the server") : "",
                Links: page ? [{
                    Text: _TF("Show all data tables", "Link on the table page when the table does not exist: opens the list of all data tables of the server"),
                    Url: Sw.Url.Root + "explore/table.html?q=" + encodeURIComponent("../Api/debug/ApiTableTable"),
                    Icon: "table",
                }] : [],
            });
        }

        /** Remove the error state and the error toast of a previous failed request. */
        ClearError() {
            const t = this;
            //  A table that was not allowed / not found and answers again (the retries go on, like the original table)
            if (t.IsDenied) {
                t.IsDenied = false;
                t.DeniedHost.hidden = true;
                t.DeniedHost.replaceChildren();
                t.E.classList.remove("sw-table-failed", "sw-table-denied-on");
                t.SetToolsDisabled(false);
            }
            if (t.ErrorToast) {
                t.ErrorToast.Close();
                t.ErrorToast = null;
            }
            if (!t.Error.hidden) {
                t.Error.hidden = true;
                t.E.classList.remove("sw-table-failed");
                t.SetToolsDisabled(false);
            }
            t.StaleE.hidden = true;
            if (t.WaitOnline) {
                t.WaitOnline = false;
                t.UpdateNote(!!t.NoteRun);
            }
        }

        /**
         * The connection is lost (UX guide FBK-9: Sw.Net.Online is false, or the request failed with a network error): the shell shows
         * the one calm "Offline, retrying…" line, so the table shows no error toast; with data on screen the rows stay and the footer
         * says since when they are not updated ("Not updated since 12:40"). The live refresh does not pause: it keeps retrying every
         * RetryDelay (3 s, as the original table: about 14 requests in a 40 s outage), and "net.online" fetches once at once.
         * @param {*} [e] The failed request (none: the refresh was skipped because the connection is known to be lost).
         * @returns {boolean} True if handled as offline (the caller shows nothing else).
         */
        ShowOffline(e) {
            const t = this;
            const net = Sw.Net || {};
            const offline = net.Online === false || (e !== undefined && typeof net.IsNetworkError === "function" && net.IsNetworkError(e) && net.Online !== true);
            if (!offline || !t.Data)
                return false;
            /** @type {boolean} The live refresh waits for the connection (ShowOffline; "net.online" fetches once). */
            if (!t.WaitOnline) {
                t.WaitOnline = true;
                t.UpdateNote(!!t.NoteRun);
            }
            if (t.StaleE.hidden) {
                const at = t.LastOk ? Sw.Format.Time ? Sw.Format.Time(t.LastOk) : t.LastOk.toLocaleTimeString() : "";
                const long = at
                    ? _T("Not updated since {0}", at, "Chip below a data table while the connection to the server is lost (the last rows stay), {0} is the time of the last update, ex: 12:40")
                    : _TF("Not updated (offline)", "Chip below a data table while the connection to the server is lost (the last rows stay)");
                //  (narrow footers, phones: the short form, so the chip is never cut to "⚠ N"; table.css shows one of the two)
                const short = at
                    ? _T("Offline {0}", at, "Short chip below a data table on phones while the connection to the server is lost, {0} is the time of the last update, ex: 12:40")
                    : _TF("Offline", "Short chip below a data table on phones while the connection to the server is lost");
                t.StaleE.children[1].textContent = long;
                t.StaleE.children[2].textContent = short;
                t.StaleE.title = _TF("The connection to the service is lost: the rows shown are the last ones received. They update by themselves when the connection is back.", "Tool tip of the 'Not updated since' chip below a data table while offline");
                t.StaleE.hidden = false;
            }
            return true;
        }

        /**
         * Disable (error, not allowed and not found states: there is no data) or enable the tools that work on the data: Refresh,
         * Download, the column chooser and the search field.
         * @param {boolean} off True to disable.
         */
        SetToolsDisabled(off) {
            const t = this;
            //  (the reason is the tool tip and is shown on an attempt, UX guide INP-12)
            const why = off ? _TF("Not available: the table has no data right now (see the message in the table)", "Tool tip (reason) of a disabled tool of a data table (refresh, download, columns, search) while the table shows an error or a not allowed state") : undefined;
            [t.RefreshB, t.ExportB, t.ColsB].forEach(b => b && b.SetDisabled(off, why));
            if (t.Search)
                t.Search.SetDisabled(off, why);
        }

        /**
         * Retry after an error (the "Try again" button).
         * @returns {Promise<void>} Resolves when done.
         */
        Retry() {
            const t = this;
            t.UserChange = true;
            if (!t.Data)
                t.RangeE.textContent = _TF("Loading...", "The range text of a data table while its first data loads");
            return t.UpdateNow();
        }

        /**
         * Call the first load / first data callbacks once.
         * @param {object} [data] The first data.
         */
        FirstLoaded(data) {
            const o = this.O;
            //  The skeleton rows (table.css .sw-table-loading) are only shown until the first answer (data or an error state)
            this.E.classList.remove("sw-table-loading");
            if (o.OnFirstLoad) {
                const f = o.OnFirstLoad;
                o.OnFirstLoad = null;
                f();
            }
            if (data && o.OnFirstData) {
                const f = o.OnFirstData;
                o.OnFirstData = null;
                f(data, this);
            }
        }

        /**
         * Keep an embedded table inside a host that has a fixed height (FrontendDev S5b N1, option HostFit, default on; not for page and
         * fill tables, not for the legacy Table.addTable, which overflows as the original did): when the frame ends below the host's
         * inner bottom (the host did not grow with the table, so its height is set: ex: `height: 300px`), the table becomes a fill table
         * of that host for good (`sw-table-fill sw-table-hostfit`, table.css: the frame takes the host's height, the rows scroll inside
         * it) and Fit sizes the page from the free height of its rows area, like a page table, instead of the window height below the
         * table (which gave a page that painted over the next card, and another size to each of two identical hosts). One way only, so
         * it can't flip.
         * `HostFit: true` (set by the page, not the default) says that the host has a fixed height: the table is kept in it from the first
         * fit, also while its rows don't fill it (else a short table far down a long page measured the window height below its document
         * position, nothing, and paged at 5 rows).
         * @returns {boolean} The table is kept in its host.
         */
        HostFit() {
            const t = this;
            if (t.HostFixed)
                return true;
            const h = t.O.Element;
            if (t.Dead || t.O.Page || t.Fill || t.O.HostFit === false || !h || h === document.body || h === document.documentElement || !h.isConnected)
                return false;
            const hr = h.getBoundingClientRect();
            if (!(hr.height > 0) || !t.E.getClientRects().length)
                return false;
            if (t.O.HostFit !== true) {
                const cs = getComputedStyle(h);
                const inner = hr.bottom - (parseFloat(cs.borderBottomWidth) || 0) - (parseFloat(cs.paddingBottom) || 0);
                if (t.E.getBoundingClientRect().bottom <= inner + 1)
                    return false;
            }
            /** @type {boolean} The table is kept inside its fixed height host for good (HostFit). */
            t.HostFixed = true;
            t.E.classList.add("sw-table-fill", "sw-table-hostfit");
            t.FitKey = "";
            return true;
        }

        /**
         * Automatic page size, as the original (onResizeFn): the page size plus the whole rows that fit in the space left under the shown
         * rows, a multiple of 5, 5..100, only on the first page; a page with fewer rows than its size grows step by step (25, 40, 55 ...
         * 100), a full page that doesn't fit shrinks; not in the compact card layout (the stored size, 25, stays, as on the original
         * phones).
         * Stable by design: the row height is measured once (the median of the shown rows) and kept until the window size,
         * the layout (grid / cards / narrow) or the columns change, so a live refresh, a page whose rows are taller or shorter, or a
         * scroll bar coming and going never changes the page size (before, the average of each new page gave another size, which
         * loaded another page, in a loop: the rows, the range and the footer jumped every frame on the log table). Page tables measure
         * the free height of their full height scrolled area (independent of the rows); embedded tables the window height below them.
         * Independent of the scroll bars (R1-Table-1): the free height is the area's height without any scroll bar, less a horizontal
         * bar only when the grid is wider than the whole area. Before, the client height was used: on classic (non overlay) scroll bars a
         * grid as wide as its area got a vertical bar from one more row, that bar made it too narrow, the horizontal bar that followed
         * took a row's height, one row less fit, both bars went, one more row fit... a request per render, ~50 per second. A page
         * size that still comes back to a size it just left (an oscillation) is pinned to the smaller one until the window, the layout
         * or the columns change. The new page is fetched by AutoFetch (not a user change, at most one per AutoMinGap).
         */
        Fit() {
            const t = this;
            const r = t.Req;
            t.HostFit();
            //  Infinite scrolling has no pages
            if (t.ScrollOn) {
                t.ScrollEnsure();
                return;
            }
            if (t.Dead || !t.State.AutoRowCount || r.Row !== 0) {
                //  (a size the user chose: the rows keep their own height; a later page keeps the first page's fill)
                if (!t.Dead && !t.State.AutoRowCount) {
                    t.E.style.removeProperty("--sw-row-fill");
                    t.E.classList.remove("sw-table-dense");
                }
                return;
            }
            //  (a card template has its own measure: the rows of the grid are empty)
            if (t.CardsOn()) {
                t.FitCards();
                return;
            }
            const rows = t.Body.rows;
            if (!rows.length)
                return;
            //  Never from a table that is not rendered (N46: a table in a hidden tab, display: none, measured 0 px rows in a 0 px area,
            //  the minimum height gave another size on every answer, a request loop behind the visible tab): the size waits until the
            //  table is shown again (the resize observer of the table calls Fit then, see layout in Init)
            if (!t.E.getClientRects().length || !t.Scroll.offsetHeight || !rows[0].offsetHeight) {
                /** @type {boolean} Fit was skipped because the table was not rendered (it runs when the table is shown). */
                t.FitHidden = true;
                return;
            }
            t.FitHidden = false;
            const E = t.E.classList;
            //  (the cards of a compact screen scroll in the page: the stored size stays, as the original kept 25 on a phone, where its footer
            //  followed the rows)
            if (E.contains("sw-table-cards") || E.contains("sw-table-compact"))
                return;
            const key = [window.innerWidth, window.innerHeight, E.contains("sw-table-cards"), E.contains("sw-table-narrow"), t.ColSig, (t.State.Filters || []).length, t.State.Expanded].join("|");
            if (key !== t.FitKey) {
                /** @type {Array<{N: number, At: number}>} The page sizes Fit chose for the current FitKey (the oscillation check). */
                t.FitHist = [];
                /** @type {number} The page size pinned after an oscillation for the current FitKey (0 = none). */
                t.FitPin = 0;
                t.FitCap = 0;
            }
            if (key !== t.FitKey || !(t.FitRowH > 0)) {
                //  (measured without the fill height of FillRows: the rows' own height, so filling never changes the page size)
                t.E.style.removeProperty("--sw-row-fill");
                t.E.classList.remove("sw-table-dense");
                //  The median row height: a few tall rows (a wrapped long value) do not shrink the page to a few rows (they scroll inside)
                const median = () => {
                    const hs = Array.from(rows, row => row.offsetHeight).sort((a, b) => a - b);
                    return Math.max(8, hs[Math.floor(hs.length / 2)]);
                };
                let rowH = median();
                //  The compact row height of a page table (S7a-A2-1): the rows' content in the dense style (sw-table-dense, the height of
                //  the text line), the height the page size is computed with (FillRows gives the rows the free height up to rowH), as the
                //  original's 26-28 px rows: about 30 rows at 1080 px, a result of up to ~30 rows on one page (before: the 40 px rows
                //  paged the Performance table at 15). Never below 60 % of rowH (ex: the skeleton rows, which have no text yet).
                let denseH = rowH;
                if (t.O.Page && !E.contains("sw-table-cards")) {
                    t.E.classList.add("sw-table-dense");
                    t.E.style.setProperty("--sw-row-fill", "1px");
                    denseH = Math.min(rowH, Math.max(Math.round(rowH * 0.6), median()));
                    t.E.style.removeProperty("--sw-row-fill");
                    t.E.classList.remove("sw-table-dense");
                }
                /** @type {number} The compact (dense) row height the automatic page size of a page table is computed with (Fit). */
                t.FitDenseH = denseH;
                //  Cards may be laid out side by side (wide compact screens): the height per card line
                if (E.contains("sw-table-cards") && rows[0].offsetWidth > 0)
                    rowH = Math.max(8, Math.ceil(rowH / Math.max(1, Math.floor((t.Body.clientWidth + 8) / (rows[0].offsetWidth + 8)))));
                /** @type {string} What the measured row height (FitRowH) is valid for: window size, layout and columns (Fit). */
                t.FitKey = key;
                /** @type {number} The measured (median) row height of the automatic page size (Fit). */
                t.FitRowH = rowH;
                /** @type {number} The header height (titles, filter rows, card tool bar) measured with FitRowH (Fit). */
                t.FitHead = t.Head.offsetHeight + t.FilterBody.offsetHeight + (t.CardBar.parentElement === t.Scroll ? t.CardBar.offsetHeight : 0);
            }
            const rowH = t.FitRowH;
            //  (the page size of a page table counts the compact rows, S7a-A2-1; the other tables their own row height)
            const sizeH = t.FitDenseH > 0 ? Math.min(rowH, t.FitDenseH) : rowH;
            const head = t.FitHead;
            let avail;
            if (t.O.Page || t.HostFixed) {
                //  (the scrolled area takes the free height of the frame, table.css .sw-table-area). Never its client height: that
                //  depends on the scroll bars, which depend on the rows (see above). The height without any bar, less the horizontal
                //  bar only when the grid is wider than the whole area (then it is there whatever the rows are; a grid as wide as the
                //  area gets no vertical bar from rows that fit, so no horizontal one either)
                const s = t.Scroll;
                const cs = getComputedStyle(s);
                const bt = parseFloat(cs.borderTopWidth) || 0, bb = parseFloat(cs.borderBottomWidth) || 0;
                const bl = parseFloat(cs.borderLeftWidth) || 0, br = parseFloat(cs.borderRightWidth) || 0;
                //  (a table kept in its host, HostFit: the rows area takes the free height of the host, the scrolled area its rows)
                const full = (t.O.Page ? s.offsetHeight : t.Area.clientHeight) - bt - bb;
                const aw = s.getBoundingClientRect().width - bl - br;
                const gw = t.Grid.getBoundingClientRect().width;
                //  (the columns without the filler, which gives its width to a vertical bar)
                const fill = t.E.classList.contains("sw-table-filled") ? parseFloat(t.E.style.getPropertyValue("--sw-table-fill")) || 0 : 0;
                const sb = t.BarSize();
                //  A grid wider than the whole area scrolls sideways whatever the rows are; a grid whose columns leave no room for a
                //  vertical bar would scroll sideways as soon as one row more than fits comes (classic bars only): the rows then fit
                //  above a horizontal bar, so no vertical bar comes and the grid never scrolls sideways for nothing
                const reserve = sb > 0 && (gw > aw + 1 || gw - fill > aw - sb + 1) ? sb : 0;
                avail = full - reserve - head - 1;
            }
            else {
                const top = t.Scroll.getBoundingClientRect().top + window.scrollY;
                avail = window.innerHeight - t.Foot.offsetHeight - top - head - 16 - 8;
            }
            if (avail < 128)
                avail = 128;
            //  As the original (onResizeFn): the page size plus the whole rows that fit in the space left under the shown rows, so a page
            //  with fewer rows than its size grows (25, 40, 55 ... 100) and a full page that doesn't fit shrinks
            //  (the shown rows counted with the measured row height, so a full page gives the rows that fit: stable, no loop; a page
            //  table counts its compact row height, as the original's 26-28 px rows, and FillRows shares the frame between the rows)
            const usedH = Math.min(rows.length, r.MaxRowCount) * sizeH;
            let n = t.PageRows(avail - usedH + r.MaxRowCount * sizeH, sizeH);
            //  Every row came back (the server sent fewer than asked), one decision, never steps:
            //  - they fit at the original table's row height (OrigRowH): 100, as the original, which grew there and showed every row on
            //    one page (the taller rows of the redesign paged them: 8206 ServicesTable, 23 rows, 25 -> 20 -> 15 and two pages; a page
            //    table shows them in compact rows that share the frame, FillRows, S7a-A2-1);
            //  - else the rows that fit, at once (a live table whose rows grew past that while at 100 stepped down one size per answer
            //    with the rule above: 100, 90, 80 ... 45, six requests in a second on CacheEntries)
            const d0 = t.Data;
            const all = d0 && Array.isArray(d0.Rows) && d0.Rows.length < r.MaxRowCount;
            if (t.O.Page && !t.HostFixed) {
                //  A page table asks the page sizes of the original (S7a-A2-4, "requests as before"): its onResizeFn, newCount =
                //  MaxRowCount + floor((windowHeight - tableHeight) / rowHeight), clamped to 5..100 and rounded down to a multiple of 5,
                //  in row units: MaxRowCount - shownRows + OrigPageRows (the rows of the original's page in this window). Every row came
                //  back (the total is known): the end of the original's sequence at once (it steps 25, 30, 35 ... 100, or down 100, 90 ...
                //  to its stable size; one request instead of several, approved R2-16). The rows are shown in the frame as FillRows fits
                //  them (compact rows, S7a-A2-1). Before, the frame of the redesign (about 2 rows less than the original's page) decided:
                //  28 rows went to 100 here where the original grows at 28 only in a page as high as its own.
                const cap = OrigPageRows();
                const step = (max, shown) => Math.max(5, Math.min(100, Math.floor((max - shown + cap) / 5) * 5));
                if (all) {
                    const total = d0.Rows.length;
                    let max = r.MaxRowCount;
                    for (let i = 0; i < 25; ++i) {
                        const next = step(max, Math.min(total, max));
                        if (next === max)
                            break;
                        max = next;
                    }
                    n = max;
                }
                else
                    n = step(r.MaxRowCount, Math.min(rows.length, r.MaxRowCount));
            }
            else if (all)
                n = d0.Rows.length * sizeH <= avail ? 100 : t.PageRows(avail, sizeH);
            //  Auto never scrolls: the rows of the page fit the free height (a page table at its compact height, FillRows). A short
            //  result that fits keeps its size (every row on one page), else at most the whole rows that fit, a multiple of 5 (before,
            //  a page table asked the original's page size, and the rows scrolled when they didn't fit at the compact height)
            if (!(all && d0.Rows.length * sizeH <= avail && n >= d0.Rows.length))
                n = Math.min(n, t.PageRows(avail, sizeH));
            //  ... also when the rows grew after the row height was measured (a live refresh with longer, wrapped values; the measured
            //  height is kept for the window size and the columns): when the rows area really overflows, the page size is the rows that
            //  fit at their real heights (a multiple of 5, fewer only when not even 5 fit), kept as a cap until the window, the layout,
            //  the columns or the rows the user asked for (search, filter, sort) change (FitCap). Paging then never needs scrolling: the
            //  pointer stays on "Next page" while reading every row
            //  (not while the rows of a page table are at a height that doesn't fit them, their own or the fill of the previous page,
            //  and fit at the compact height: FillRows gives them their height below, so their overflow is not growth. It capped the
            //  first answer, 25 rows at 40 px, to 15 and paged the Performance table at 15 again (AS7-1), and 30 new rows at the fill
            //  of 25 back to 25)
            const sa = t.Scroll;
            const shownN = Math.min(rows.length, r.MaxRowCount);
            const curH = E.contains("sw-table-dense") ? parseFloat(t.E.style.getPropertyValue("--sw-row-fill")) || rowH : rowH;
            const unfilled = t.O.Page && sizeH < rowH && shownN * curH > avail && shownN * sizeH <= avail;
            //  (a page table caps only rows that really grew: one taller than the measured row height, a wrapped value. Compact rows a
            //  pixel taller than the fill (a flag image: 27 px rows filled to 26) overflow the frame by a few px and scroll inside it, as
            //  S7a-A2-4 accepted for the original's page sizes; the cap paged the country table at 25 where the original shows 30, S8)
            const grew = !t.O.Page || Array.from(rows).some(x => x.offsetHeight > rowH + 1);
            if ((t.O.Page || t.HostFixed) && !unfilled && grew && sa.scrollHeight > sa.clientHeight + 1) {
                const bottom = sa.getBoundingClientRect().top + sa.clientTop + sa.clientHeight + 0.5;
                let fit = 0;
                for (const row of rows) {
                    if (row.getBoundingClientRect().bottom > bottom)
                        break;
                    ++fit;
                }
                if (fit < Math.min(rows.length, n)) {
                    /** @type {number} The page size cap of rows that grew after they were measured (0 = none; Fit). */
                    t.FitCap = fit >= 5 ? Math.floor(fit / 5) * 5 : Math.max(1, fit);
                }
            }
            if (t.FitCap > 0)
                n = Math.min(n, t.FitCap);
            //  Hysteresis: back to a size this key had a moment ago (another feedback between the rows and the layout) = an
            //  oscillation: the smaller size is kept for this key (no row under the footer), with a developer warning
            const now = Date.now();
            t.FitHist = (t.FitHist || []).filter(h => now - h.At < 10000);
            if (t.FitPin)
                n = t.FitPin;
            else if (n !== r.MaxRowCount && t.FitHist.length >= 2 && t.FitHist.some(h => h.N === n)) {
                t.FitPin = Math.min(n, r.MaxRowCount);
                Sw.DevWarn("Sw.Table " + t.Url + ": the automatic page size oscillates (" + t.FitHist.map(h => h.N).concat(n).join(", ") +
                    " rows): kept at " + t.FitPin + " rows until the window, the layout or the columns change", "Sw.Table.FitPin." + t.Url);
                n = t.FitPin;
            }
            if (n !== r.MaxRowCount) {
                t.FitHist.push({ N: n, At: now });
                r.MaxRowCount = n;
                //  Fewer rows fit than shown (the first page size was a guess from the skeleton): the extra rows go at once, so the range,
                //  the "Auto (n)" of the size select and the rows agree and no row sits under the footer while the new page loads (a slow
                //  answer showed "1-21, more follow" next to "Auto (19)" with the 21st row cut); a total the server counted stays, a
                //  lower bound stays one ("more follow")
                const d = t.Data;
                if (d && d.Rows && d.Rows.length > n && !t.Local) {
                    const shown = d.Rows.length;
                    const more = d.RowCount > r.Row + shown && d.RowCount - (r.Row + shown) <= Math.max(1, r.LookAheadCount || 0) && !d.Exact;
                    t.Render(Object.assign({}, d, { Rows: d.Rows.slice(0, n), RowCount: more ? r.Row + n + 1 : d.RowCount }), false);
                }
                //  ("Auto (n)" says the new size)
                t.UpdateSizeSel();
                //  (not a user change: never supersedes a running request, at most one per AutoMinGap; row results and kept rows stay)
                t.AutoFetch();
                //  (the rows of the new page get their compact height now, so the frame doesn't scroll while it loads; the fill of a full
                //  page is set when it is shown: Fit runs again after the render)
                t.FillRows(avail, rowH, sizeH, all ? Math.min(d0.Rows.length, n) : n, false);
                return;
            }
            t.FillRows(avail, rowH, sizeH, Math.min(rows.length, n), rows.length >= n && n < 100);
        }

        /**
         * Fill the rows area of a full automatic page (D22, S5-A-7): the page size follows the original rule (a multiple of 5 of the rows
         * that fit), which leaves up to 4 rows of empty space inside the frame with the taller rows of the redesign; the rows of a page
         * table share that space (`--sw-row-fill`, the cell height), so the grid ends at the pager, as the original's rows ended near its
         * footer. Never changes the page size or the requests: Fit measures the rows without it. Only page tables in the grid layout,
         * only a full page (as many rows as the page size) with no taller (wrapped) row, at most twice the row height; otherwise the
         * rows keep their own height.
         * Compact rows (S7a-A2-1): when the rows of the page (a full page, or every row of a short result) don't fit at their own height,
         * they share the frame down to the compact height (`sw-table-dense`: less cell padding), as the original showed about 30 rows of
         * 26-28 px at 1080 px (the Performance table: every row on one page, where the 40 px rows paged it at 15).
         * @param {number} avail The height for the rows (px), as Fit measured it.
         * @param {number} rowH The rows' own height (px, FitRowH).
         * @param {number} denseH The compact row height (px, FitDenseH; rowH = no compact rows).
         * @param {number} m The rows the page shows (the page size, or the rows of a result shorter than the page).
         * @param {boolean} full A full page (as many rows as the page size, below 100) that may also be stretched.
         */
        FillRows(avail, rowH, denseH, m, full) {
            const t = this;
            const st = t.E.style;
            const cur = parseFloat(st.getPropertyValue("--sw-row-fill")) || 0;
            const rows = t.Body.rows;
            const E = t.E.classList;
            let fill = 0;
            if (t.O.Page && !E.contains("sw-table-cards") && !E.contains("sw-table-compact") && m > 0) {
                const f = Math.floor(avail / m);
                if (f < rowH && denseH < rowH)
                    fill = Math.max(denseH, f);
                else if (full && f > rowH + 1 && f <= rowH * 2 && Array.from(rows).every(row => row.offsetHeight <= Math.max(rowH, cur) + 1))
                    fill = f;
            }
            E.toggle("sw-table-dense", fill > 0 && fill < rowH);
            if (!fill) {
                if (cur)
                    st.removeProperty("--sw-row-fill");
                return;
            }
            if (cur !== fill)
                st.setProperty("--sw-row-fill", fill + "px");
        }

        // -------------------------------------------------------------- rendering

        /**
         * A modifier click (Shift / Ctrl / Alt / Meta held; UX guide INP-2) in the table never runs the click's action (every click
         * handler of the table and of Sw.Cells checks Sw.BadClick / Sw.IsPureEvent): it moves the keyboard focus to the clicked control
         * (a row action, a header sort icon's column header, a tool bar or footer button), else to the grid cell it is in (the cell
         * becomes the current cell), and shows the ring. The table has no row selection, so Ctrl / Shift never select rows. Left alone:
         * links (`<a href>` keeps the browser's new tab / window gestures), fields (the caret) and the text selection (Shift+click
         * extends it; the browser does that on the mouse down). Capture phase on the table root, before the row handlers.
         * @param {MouseEvent} ev The click.
         */
        ModClick(ev) {
            const t = this;
            if (Sw.IsPureEvent(ev) || ev.button !== 0 || !ev.target || !ev.target.closest)
                return;
            const tgt = ev.target;
            if (tgt.closest("a[href], input, select, textarea, [contenteditable]:not([contenteditable=false]), label"))
                return;
            let f = tgt.closest("button, [role=button], summary, td, th, [tabindex]");
            if (!f || !t.E.contains(f))
                return;
            const cell = f.closest("td, th");
            if (cell && cell.closest("table") === t.Grid && (cell.parentElement.parentElement === t.Body || cell.parentElement === t.HeadRow)) {
                //  (the header cell is the keyboard target of its sort icon, BuildHead; the corner is a button of its own)
                if (f.classList.contains("sw-table-sort") || f === cell)
                    f = cell.classList.contains("sw-table-corner") ? cell.querySelector("button") || cell : cell;
                if (!cell.classList.contains("sw-table-corner"))
                    t.SetActive(cell);
                t.UserFocus = true;
            }
            else if (f.matches("td, th"))
                return;
            showFocus(f);
        }

        /**
         * The live refresh keeps the order of the shown rows (UX guide FBK-7: rows never move under the pointer or the focus): while a
         * mouse or pen pointer is over the rows, a finger touches them, the user put the keyboard focus in the grid (a click or a key
         * in it; not the initial focus of table.html), a row action waits for its confirmation, or a menu / dialog is open.
         * @returns {boolean}
         */
        HoldOrder() {
            const t = this;
            if (t.PtrIn || t.TouchIn || t.FocusWatch)
                return true;
            //  (a row action of this table runs: its row and busy button keep their place until the result is shown)
            const pa = t.PendingAct;
            if (pa && pa.Get && !pa.Ended && Date.now() - pa.At < 5 * 60000)
                return true;
            if (t.UserFocus && t.Grid.contains(document.activeElement))
                return true;
            const sel = ".sw-dialog-layer, [role=menu], [role=alertdialog], [role=dialog][aria-modal=true]";
            if (document.querySelector(sel))
                return true;
            try {
                return window.top !== window && !!window.top.document.querySelector(sel);
            }
            catch {
                return false;
            }
        }

        /**
         * The rows of a live refresh in the order of the shown rows (HoldOrder): every shown item keeps its place with its new values;
         * an item that left the page keeps its place with its last values; items new to the page wait. The rows whose place differs
         * in the new order are marked (`data-sw-move` "up" / "down" / "out": an arrow at the row start, table.css) and the footer says
         * "Order on hold". Returns the rows unchanged when the order is the same or the items can't be told apart (no unique item text
         * or row values).
         * @param {Array<object>} rows The new rows (server order).
         * @param {function(object, number): *} get Value of a column index.
         * @returns {Array<object>} The rows to show.
         */
        HoldRows(rows, get) {
            const t = this;
            const shown = Array.from(t.Body.rows);
            t.Moves = null;
            if (!shown.length || !rows.length || shown.some(tr => !tr.SwRow))
                return rows;
            /**
             * The identity of a row: its item text (key columns) when unique on both lists, else all its values.
             * @param {function(object): string} f Identity of a row.
             * @returns {?{Shown: string[], Next: string[]}} The identities, null if they are not unique.
             */
            const ids = f => {
                const a = shown.map(tr => f(tr.SwRow));
                const b = rows.map(f);
                return a.every(x => x) && b.every(x => x) && new Set(a).size === a.length && new Set(b).size === b.length ? { Shown: a, Next: b } : null;
            };
            const id = ids(row => t.ItemText(row, get)) || ids(row => JSON.stringify(row));
            if (!id || (id.Shown.length === id.Next.length && id.Shown.every((x, i) => x === id.Next[i])))
                return rows;
            const at = new Map(id.Next.map((x, i) => [x, i]));
            const dirs = [];
            let moved = 0;
            const out = id.Shown.map((x, i) => {
                const j = at.get(x);
                if (j === undefined) {
                    dirs.push("out");
                    ++moved;
                    return shown[i].SwRow;
                }
                at.delete(x);
                const d = j < i ? "up" : j > i ? "down" : "";
                if (d)
                    ++moved;
                dirs.push(d);
                return rows[j];
            });
            if (!moved && !at.size)
                return rows;
            /** @type {?{Dirs: string[], Waiting: number}} The held order of the last live refresh (HoldRows; null: the server order is shown). */
            t.Moves = { Dirs: dirs, Waiting: at.size };
            return out;
        }

        /** Show the server's order of a held live refresh (HoldRows) once nothing holds it any more (HoldOrder). */
        ReleaseHold() {
            const t = this;
            if ((!t.Moves && !t.CardHeld) || t.Dead || !t.Data || t.HoldOrder())
                return;
            t.RenderRows(t.Data, false);
        }

        /**
         * The row numbers for screen readers (UX guide LST-5): `aria-rowindex` on the header row (1), the shown filter rows and the data
         * rows (their place in the whole result: page 3 of 25 rows starts at 52 + the filter rows), and `aria-rowcount` on the grid: all
         * rows when the server counted them, -1 (unknown) while more rows follow (RowCount is then a minimum, never a total).
         */
        UpdateRowIndex() {
            const t = this;
            const d = t.Data;
            let i = 1;
            t.HeadRow.setAttribute("aria-rowindex", "1");
            for (const r of t.FilterBody.rows) {
                if (r.hidden)
                    r.removeAttribute("aria-rowindex");
                else
                    r.setAttribute("aria-rowindex", String(++i));
            }
            const first = i + t.Req.Row + 1;
            Array.from(t.Body.rows).forEach((tr, k) => {
                const v = String(first + k);
                if (tr.getAttribute("aria-rowindex") !== v)
                    tr.setAttribute("aria-rowindex", v);
            });
            if (!d)
                return;
            const n = t.Body.rows.length;
            const more = (d.RowCount || 0) > t.Req.Row + n && (d.RowCount || 0) - (t.Req.Row + n) <= Math.max(1, t.Req.LookAheadCount || 0) && !d.Exact;
            const total = Math.max(d.RowCount || 0, t.Req.Row + n);
            t.Grid.setAttribute("aria-rowcount", more ? "-1" : String(i + total));
        }

        /**
         * Show the held order marks of the rows (HoldRows) and the footer legend "Order on hold".
         */
        UpdateMoves() {
            const t = this;
            const m = t.Moves;
            const rows = t.Body.rows;
            //  The arrow goes into the first shown cell of the row (the row number column is often hidden, ApplyHidden)
            let first = 0;
            while (t.HiddenIdx && t.HiddenIdx.has(first) && first < t.Vis.length)
                ++first;
            const upT = _TF("Moves up when the pointer or the focus leaves the rows", "Accessible description of a data table row that moves up in the new order of a live refresh (the order is on hold while the pointer or the keyboard focus is in the rows)");
            const downT = _TF("Moves down when the pointer or the focus leaves the rows", "Accessible description of a data table row that moves down in the new order of a live refresh (the order is on hold while the pointer or the keyboard focus is in the rows)");
            const outT = _TF("Leaves this page when the pointer or the focus leaves the rows", "Accessible description of a data table row that is no longer on this page in the new order of a live refresh (the order is on hold while the pointer or the keyboard focus is in the rows)");
            for (let i = 0; i < rows.length; ++i) {
                const tr = rows[i];
                const d = m ? m.Dirs[i] || "" : "";
                if ((tr.dataset.swMove || "") !== d) {
                    if (d)
                        tr.dataset.swMove = d;
                    else
                        delete tr.dataset.swMove;
                }
                //  (cards: the card title)
                const want = !d ? null : t.E.classList.contains("sw-table-cards") ? tr.querySelector(":scope > td.sw-table-cardtitle") || tr.cells[first] : tr.cells[first];
                for (const td of tr.querySelectorAll(":scope > td.sw-table-movecell"))
                    if (td !== want) {
                        td.classList.remove("sw-table-movecell");
                        td.removeAttribute("aria-description");
                    }
                if (want) {
                    want.classList.add("sw-table-movecell");
                    want.setAttribute("aria-description", d === "up" ? upT : d === "down" ? downT : outT);
                }
            }
            t.HoldE.hidden = !m;
            if (m) {
                const tip = m.Waiting > 0
                    ? _T("The rows keep their place while the pointer or the keyboard focus is in them. The arrows mark rows that move; {0} new rows are shown when the pointer or the focus leaves the rows (or with Refresh).", m.Waiting, "Tool tip of the 'Order on hold' legend below a live data table, {0} is the number of rows that are new on this page and wait")
                    : _TF("The rows keep their place while the pointer or the keyboard focus is in them. The arrows mark rows that move when the pointer or the focus leaves the rows (or with Refresh).", "Tool tip of the 'Order on hold' legend below a live data table");
                if (t.HoldE.title !== tip)
                    t.HoldE.title = tip;
            }
        }

        /**
         * Render a response.
         * @param {object} data TableData.
         * @param {boolean} user Caused by the user (no flashing).
         */
        Render(data, user) {
            const t = this;
            if (data.Cols) {
                ApplyKnownTitles(data.Cols);
                const sig = JSON.stringify(data.Cols);
                if (sig !== t.ColSig) {
                    t.ColSig = sig;
                    t.Cols = data.Cols;
                    t.BuildHead();
                    t.BuildFilters();
                    t.Body.replaceChildren();
                }
            }
            if (!t.Cols)
                return;
            t.Data = data;
            t.RenderRows(data, user);
            t.UpdateFoot();
            t.UpdateEdges();
            //  (the result lines of failed row actions follow their rows: rows above may have changed height)
            t.PlaceWhys();
            t.SaveActs();
        }

        /** Rebuild the header row for the columns. */
        BuildHead() {
            const t = this;
            //  Every column of the data is in the grid, also the ones the server hides by default (C# TableDataHide, Props.Hide: hidden
            //  by ApplyHidden until the viewer shows them with the column chooser, as the attribute promises). Server hidden columns
            //  before the first shown one follow it (the first grid column names the row and is pinned while scrolling sideways,
            //  UpdatePins), and row action columns stay last (the actions at the row end)
            const all = t.Cols.map((c, i) => ({ Col: c, Index: i }));
            const isAct = v => /^Actions(;|$)/.test(String(v.Col.Format || ""));
            const first = all.findIndex(v => !srvHidden(v.Col) && !isAct(v));
            const lead = first > 0 ? all.slice(0, first).filter(v => !isAct(v)) : [];
            const rest = all.filter(v => lead.indexOf(v) < 0);
            if (first > 0)
                rest.splice(rest.indexOf(all[first]) + 1, 0, ...lead);
            t.Vis = rest.filter(v => !isAct(v)).concat(rest.filter(isAct));
            //  (the filters of a stored state are kept per grid column: each goes to the place of its column again, by name, so a state
            //  saved while the server hidden columns were left out of the grid keeps its filters on the right columns; BuildFilters
            //  fills the gaps)
            (t.State.Filters || []).forEach((set, si) => {
                if (set.some(f => f && f.ColName))
                    t.State.Filters[si] = t.Vis.map(v => set.find(f => f && f.ColName === v.Col.Name));
            });
            /** @type {object} The filter mode button (header corner). */
            //  (hidden -> filter row -> all filter options, like the original corner icon; remembered per table and user, SetExpanded)
            t.FilterB = iconButton("IconExpand", "", () => t.SetExpanded((t.State.Expanded + 1) % 3), "sw-table-filterbtn");
            const corner = el("th.sw-table-corner", { attrs: { scope: "col" } }, t.FilterB.E);
            const ths = t.Vis.map((v, vi) => {
                const c = v.Col;
                const sortB = el("button.sw-table-sort", { type: "button", tabIndex: -1 }, Sw.IconClass("IconAscending"));
                //  Keep the focus where it is (the header cell is the keyboard target)
                sortB.addEventListener("mousedown", ev => ev.preventDefault());
                sortB.addEventListener("click", ev => {
                    ev.stopPropagation();
                    //  (a modifier click: ModClick focused the header cell, the keyboard target; Sw.BadClick would focus this icon)
                    if (!Sw.IsPureEvent(ev)) {
                        ev.preventDefault();
                        return;
                    }
                    if (Sw.BadClick(ev))
                        return;
                    t.SortClick(vi, ev.shiftKey);
                });
                //  A cryptic one letter title ("C", "M") shows its explanation (shortTitleHint, as the card label), in a narrow wrapping header.
                //  A NoTitle column (an icon only column, C# TableDataNoTitleAttribute) shows no title text: the title stays for screen
                //  readers, the tool tip, the column menu and the phone card label; sorting and filtering work as usual.
                const noTitle = (c.Props & Props.NoTitle) !== 0;
                const hint = shortTitleHint(c);
                const th = el("th", {
                    tabIndex: -1,
                    class: noTitle ? "sw-table-notitle sw-table-iconcol" : hint ? "sw-table-hinted" : (c.Props & Props.WordWrap) ? "sw-table-wrap" : null,
                    attrs: { scope: "col" },
                }, el("div.sw-table-th", null, sortB, el("span.sw-table-title", { text: hint || c.Title || c.Name }), el("span.sw-table-prio")));
                th.SwCol = vi;
                th.SwSort = sortB;
                th.addEventListener("click", ev => {
                    if (Sw.BadClick(ev))
                        return;
                    t.ShowMenu(th, t.ColumnMenu(vi));
                });
                return th;
            });
            t.HeadRow.replaceChildren(corner, ...ths);
            t.UpdateHead();
        }

        /** Update the sort state of the header cells. */
        UpdateHead() {
            const t = this;
            const r = t.Req;
            const cells = t.HeadRow.cells;
            t.Vis.forEach((v, vi) => {
                const th = cells[vi + 1];
                const c = v.Col;
                const title = shortTitleHint(c) || c.Title || c.Name;
                const can = (c.Props & Props.CanSort) !== 0;
                const f = Sort.Find(r, c.Name);
                const desc = f.Index >= 0 ? f.Desc : sortDescFirst(c);
                th.classList.toggle("sw-sort-none", !can);
                th.classList.toggle("sw-sort-can", can && f.Index !== 0);
                th.classList.toggle("sw-sort-current", can && f.Index === 0);
                th.classList.toggle("sw-sort-on", f.Index >= 0);
                th.setAttribute("aria-sort", f.Index === 0 ? (f.Desc ? "descending" : "ascending") : "none");
                th.querySelector(".sw-table-prio").textContent = f.Index > 0 ? String(f.Index + 1) : "";
                const sb = th.SwSort;
                sb.hidden = !can;
                Sw.SetIconClass(sb.firstChild, desc ? "IconDescending" : "IconAscending");
                let tip = "";
                if (can) {
                    if (f.Index >= 0)
                        tip = f.Desc
                            ? _T("Sorted by {0} in descending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                            : _T("Sorted by {0} in ascending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title");
                    const nextDesc = f.Index === 0 ? !f.Desc : desc;
                    const click = nextDesc
                        ? _T("Sort by {0} in descending order.", title, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in descending order.{0} is replaced with the column title")
                        : _T("Sort by {0} in ascending order.", title, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in ascending order.{0} is replaced with the column title");
                    tip = Sw.Format.JoinNonEmpty("\n\n", tip, click);
                    sb.title = tip;
                    sb.setAttribute("aria-label", Sw.ActionName(click));
                }
                //  The tool tip starts with the full column title (a long title may be cut in a narrow column)
                let hint = c.Desc || shortTitleHint(c);
                //  Time columns say which clock they show (core/README "Times"): the cells are in the viewer's time ("Times in your time
                //  zone (WEST, UTC+1)", Sw.Format.ZoneHint), or UTC after "Show UTC times"; a server description that says "(UTC)" (the
                //  value as sent) is not repeated over local times
                const zone = timeZoneHint(c, t.State.TimeMode);
                if (zone) {
                    if (hint && zone.Local)
                        hint = hint.replace(/\s*\((?:in )?UTC\)/gi, "").replace(/\s+in UTC\b/gi, "");
                    hint = Sw.Format.JoinNonEmpty("\n", hint, zone.Text);
                }
                th.title = Sw.Format.JoinNonEmpty("\n\n", title, hint && hint !== title ? hint : null, tip, _TF("Show options", "A tool tip description on a button that if clicked will show some options"));
            });
            t.UpdateCardBar();
        }

        /**
         * Sort click on a column (the sort icon, Enter on the header).
         * A plain click works like the original table: the primary column flips its direction, another column becomes the primary key
         * and the other keys are kept (at most 3; Sort.Toggle). Shift + click sorts by this column only (Sort.Only).
         * @param {number} vi Visible column index.
         * @param {boolean} [only] Shift held: sort by this column only.
         */
        SortClick(vi, only) {
            const c = this.Vis[vi].Col;
            if ((c.Props & Props.CanSort) === 0)
                return;
            if (only)
                Sort.Only(this.Req, c.Name, sortDescFirst(c));
            else
                Sort.Toggle(this.Req, c.Name, sortDescFirst(c));
            this.UpdateHead();
            this.Changed();
        }

        /**
         * Render the data rows (rows and cells are reused; cells are only rendered when value or format changed).
         * @param {object} data TableData.
         * @param {boolean} user Caused by the user (no flashing).
         */
        RenderRows(data, user) {
            const t = this;
            if (t.CardsOn()) {
                t.RenderCards(data, user);
                return;
            }
            const cols = t.Cols;
            let rows = data.Rows || [];
            const generic = rows.length <= 0 || Array.isArray(rows[0].Values);
            /** Value of a column. */
            const get = generic ? (row, i) => row.Values[i] : (row, i) => row[cols[i].Name];
            //  A row a final action was done on (ActionDone: an ended session, a killed process) stays in its place, marked done, through the
            //  live refreshes after it, also when the server no longer lists it (the row does not vanish under the pointer, and the rows
            //  below do not jump); a manual refresh or another sort, search, filter or page shows the server's list as it is
            /** @type {number} The number of kept rows (final actions, rows gone under a confirmation) shown although the server no
             *  longer lists them (UpdateFoot counts them). */
            t.KeptShown = 0;
            /** @type {Array<string>} The states of the shown kept rows (UpdateFoot: "+ 1 ended row, 1 no longer listed"). */
            t.KeptStates = [];
            //  (the items the server lists now)
            const listed = new Set(rows.map(x => t.ItemText(x, get)));
            //  (a change the table made itself, `QuietChange`: the automatic page size, the connection coming back, the reload after a
            //  row action; not the user's refresh: row results and kept rows stay, also across the rows <-> cards switch)
            const reset = user && !t.QuietChange;
            /** @type {boolean} The next user-flagged render is the table's own change, not the user's refresh (RenderRows). */
            t.QuietChange = false;
            if (reset) {
                t.KeptRows = null;
                //  A refresh by the user shows the rows as they are now: a failure note is for the moment of the action, and a row the
                //  server still lists after a final action ("Kill: done", its actions hidden) gets its actions back (the server did not
                //  remove it; Ops R3-12); a done note of an action that is not final stays (the server may list the row until it expires)
                if (t.Acted)
                    for (const [k, a] of t.Acted)
                        if (a.Failed || ((a.Final || a.Gone) && listed.has(k)))
                            t.Acted.delete(k);
            }
            //  A row whose action confirmation is open (KeepFocus) and that a live refresh no longer lists (someone else ended / withdrew
            //  it meanwhile): it stays in its place, marked "No longer listed", so the dialog does not float over another row and the
            //  result can say what really happened (RowGone, ActionDone: "already gone", not an error). Every such row is kept, also
            //  while other rows of final actions are kept (t.KeptRows holds them all, by item)
            const fw = t.FocusWatch;
            //  (a kept row of an earlier action on the same item does not hide it: Ops R5-5, the second dialog on a row that had a done
            //  action before never said "no longer listed")
            const fwk = fw && fw.Item ? t.KeptOf(fw.Item) : null;
            if (!user && fw && fw.Item && fw.Row && !fw.Gone && !(fwk && fwk.Gone) && !listed.has(fw.Item))
                t.RowGone(fw);
            const kept = !reset && t.KeptRows
                ? Array.from(t.KeptRows.values()).filter(k => k.Cols === t.ColSig && !listed.has(k.Item)).sort((a, b) => a.At - b.At)
                : [];
            if (kept.length) {
                rows = rows.slice();
                for (const k of kept)
                    rows.splice(Math.min(k.At, rows.length), 0, k.Row);
                t.KeptShown = kept.length;
                t.KeptStates = kept.map(k => k.State);
            }
            //  A live refresh keeps the shown order while the pointer or the focus is in the rows (UX guide FBK-7; HoldRows marks the rows
            //  that move; ReleaseHold shows the new order when it leaves); a user change shows the server's order at once
            if (!user && t.HoldOrder())
                rows = t.HoldRows(rows, get);
            else
                t.Moves = null;
            const timeMode = t.State.TimeMode;
            const flashCls = user ? null : "sw-flash";
            const onRefresh = res => {
                //  A destructive row action that succeeded (End session, Kill, Delete ...) updates its row in place (ActionDone /
                //  MarkActed: "done", no repeat action) and the rows are not reloaded at once, so the row keeps its place (the next
                //  live or manual refresh shows the server's new list); any other action reloads the rows at once (a new state).
                //  `res`: the outcome Sw.Cells passes (`{ Ok, Value, Error }`), when it does
                if (!t.ActionDone(res)) {
                    //  (the reload after an action is not the user's refresh: the other rows keep their results)
                    t.QuietChange = true;
                    t.Changed(true);
                }
            };
            const body = t.Body;
            let num = t.Req.Row + 1;
            const hadFocus = t.Grid.contains(document.activeElement);
            let flashed = false;
            //  (rows appended by infinite scrolling: the rows before them did not change)
            const from = kept.length ? 0 : Math.min(t.AppendFrom, body.rows.length);
            rows.forEach((row, ri) => {
                if (ri < from) {
                    ++num;
                    return;
                }
                let tr = body.rows[ri];
                if (!tr) {
                    tr = body.insertRow();
                    const rn = tr.insertCell();
                    rn.className = "sw-table-rn";
                    rn.tabIndex = -1;
                }
                //  The item of the row (key columns), named by confirmations of destructive cell actions (Sw.Cells.ItemName)
                const item = t.ItemText(row, get);
                //  Changed values are tinted only when the row shows the same item as before: on a live table sorted by a
                //  changing value (ex: memory) the rows move, and tinting every value of a row that now shows another item flooded the
                //  table on each refresh. Without key columns: the same row number, as before.
                const flash = tr.SwNum === num && (!item || tr.SwItem === item) ? flashCls : null;
                tr.SwNum = num;
                tr.SwItem = item;
                tr.SwRow = row;
                const rn = tr.cells[0];
                if (!rn.SwNum) {
                    //  The row number and (card layout only) the "Show all n fields" button of a long card (CompactCard)
                    rn.SwNum = el("span.sw-table-rnum");
                    rn.replaceChildren(rn.SwNum);
                }
                if (rn.SwNum.textContent !== String(num))
                    rn.SwNum.textContent = num;
                if (item)
                    tr.dataset.swItem = item;
                else
                    delete tr.dataset.swItem;
                t.Vis.forEach((v, vi) => {
                    const i = v.Index;
                    const c = v.Col;
                    let td = tr.cells[vi + 1];
                    if (!td) {
                        td = tr.insertCell();
                        td.tabIndex = -1;
                        //  The label of the value in the card layout (table.css); a cryptic one letter title is explained
                        td.dataset.label = shortTitleHint(c) || c.Title || c.Name;
                        if (c.Props & Props.WordWrap)
                            td.classList.add("sw-table-wrap");
                    }
                    const value = get(row, i);
                    let fmt = c.Format;
                    if (isDateType(c.Type) && !fmt)
                        fmt = timeMode;
                    fmt = cellFormat(fmt);
                    const key = value + "|" + fmt;
                    if (td.SwKey === key && td.SwCols === t.ColSig)
                        return;
                    td.SwKey = key;
                    td.SwCols = t.ColSig;
                    const next = i + 1 < cols.length ? get(row, i + 1) : undefined;
                    //  As the original: an enum number is shown as the number (a known enum type, EnumNames, has its name in the tool tip)
                    //  and an empty auth column stays empty
                    Sw.Cells.Update(td, c.Type, value, flash, fmt, next, onRefresh);
                    const en = fmt ? null : enumText(c.Type, value);
                    if (en !== null && en !== String(value))
                        td.title = Sw.Format.JoinNonEmpty("\n\n", (c.Title || c.Name) + ": " + en, td.title);
                    t.CellHealth(td, c, value);
                    t.QuietCell(td);
                    if (flash)
                        flashed = true;
                    //  Empty values are left out of the cards (table.css)
                    td.classList.toggle("sw-table-blank", isBlankCell(td));
                    //  Long values take the full card width in the column grid of low screens (table.css)
                    td.classList.toggle("sw-table-long", td.textContent.length > 40);
                    //  A multi line text (a call stack, an exception) shows only the start of its first line in the grid (a click shows it
                    //  all): a narrower column (table.css), so it does not push the other columns off screen
                    td.classList.toggle("sw-table-multiline", typeof value === "string" && value.length > 40 && value.indexOf("\n") >= 0);
                    //  Long names wrap at word / camelCase / punctuation boundaries (cards)
                    softBreaks(td);
                    //  An icon only link (ex: the "📊" chart links of Process information) is named by the cell tool tip, and says "Show"
                    //  next to the icon in the cards (table.css sw-table-iconlink; touch screens have no tool tips)
                    const a = td.firstElementChild;
                    const iconLink = !!a && a.tagName === "A" && td.childElementCount === 1 && a.textContent.trim().length <= 2;
                    td.classList.toggle("sw-table-iconlink", iconLink);
                    //  An icon only column (C# TableDataNoTitleAttribute): compact and centred (table.css sw-table-iconcol), named
                    //  by the tool tip; a chart pictograph ("📊", a multicolour emoji) is drawn as the line chart icon in the text colour
                    const iconCol = (c.Props & Props.NoTitle) !== 0;
                    td.classList.toggle("sw-table-iconcol", iconCol);
                    if (iconLink && iconCol && /^[\u{1F4C8}\u{1F4C9}\u{1F4CA}]️?$/u.test(a.textContent.trim()))
                        a.replaceChildren(lineIcon("chart", "sw-table-cicon"));
                    if (iconLink) {
                        const name = td.title || a.title || td.dataset.label || "";
                        if (name)
                            a.setAttribute("aria-label", name);
                        if (!td.title && !a.title && td.dataset.label)
                            a.title = td.dataset.label;
                        a.dataset.swShow = _TF("Show", "Text next to an icon only link (ex: a chart icon) in the card view of a data table on phones");
                    }
                    //  A new value starts clamped again (cards, UpdateClamps)
                    td.classList.remove("sw-table-unclamp");
                    if (!td.hasAttribute("tabindex"))
                        td.tabIndex = td === t.ActiveEl ? 0 : -1;
                    td.querySelectorAll("a,button,[tabindex]").forEach(x => x.tabIndex = -1);
                });
                while (tr.cells.length > t.Vis.length + 1)
                    tr.deleteCell(-1);
                t.RowHealth(tr, get, row);
                t.MarkActed(tr);
                t.MarkCurrent(tr, row, get);
                t.CompactCard(tr);
                ++num;
            });
            while (body.rows.length > rows.length)
                body.deleteRow(-1);
            t.Empty.hidden = rows.length > 0;
            t.UpdateRowIndex();
            t.UpdateHeadAlign();
            //  The hidden columns first (the row number column is hidden by default): the pinned column offsets are measured on the
            //  columns that are really shown (measured with the row numbers still shown, the pinned columns were placed their
            //  width too far right and covered the next column until the next refresh)
            t.ApplyHidden(t.AutoHidden || []);
            t.UpdateRnw();
            t.UpdateMoves();
            t.RestoreActive(hadFocus);
            //  (the focused element went away before this redraw, ex: the button of a row that changed: its place gets the focus)
            if (!hadFocus)
                t.RestoreFocusPlace();
            t.UpdateClamps();
            t.UpdateFit();
            if (flashed)
                t.ShowChangedLegend();
        }

        /**
         * The page header title once it is decided: the menu item name (option MenuName, a promise; Sw.MenuTitle inside
         * the shell, known at once) or the link name, else null (the table name decides: the server title or the url).
         * @returns {Promise<?string>} The decided title, null when none.
         */
        HeaderReady() {
            const t = this;
            return (t.MenuNameP || Promise.resolve(null)).then(() => t.HeadNamed && t.PHTitle ? t.PHTitle.textContent : null);
        }

        /**
         * The name of a table whose data never came (not allowed, not found): the decided header title, else the name from the url
         * ("../Api/debug/ServicesTable" -> "Services", never the api method name); also shown as the header title.
         * @returns {string} The name.
         */
        FallbackName() {
            const t = this;
            const n = (t.HeadNamed && t.PHTitle && t.PHTitle.textContent) || Sw.Page.NameFromUrl(t.Url);
            t.SetHeaderName(n, false);
            return n;
        }

        /**
         * Set the page header title and show it (the title stays hidden, its line reserved, while the menu name is looked up, so it
         * never shows the api name first and then changes).
         * @param {string} n The title.
         * @param {boolean} [fixed] The final title (the menu or link name): later names (the server title) do not replace it.
         */
        SetHeaderName(n, fixed) {
            const t = this;
            const h = t.PHTitle;
            if (!h || !n || (t.HeadNamed && !fixed))
                return;
            if (fixed)
                t.HeadNamed = true;
            if (h.textContent !== n)
                h.textContent = n;
            h.classList.remove("sw-table-ph-pending");
        }

        /**
         * The name of the table as the user sees it, for export file names and export jobs (one name source, UX guide TXT-26: the file
         * says what the page says): the page header title once it is decided (the menu item name in the app shell, Sw.MenuTitle; the
         * link or menu name on its own), else the menu name of the table page in the shell, else the table name (option Name, the
         * server title, the url). Characters a file name can't have are replaced by spaces.
         * @returns {string} The name ("Recent log entries", never empty).
         */
        FileName() {
            const t = this;
            const h = t.PHTitle;
            let n = h && !h.classList.contains("sw-table-ph-pending") ? h.textContent.trim() : "";
            if (!n && t.O && t.O.Page && typeof Sw.MenuTitle === "function")
                n = String(Sw.MenuTitle() || "").trim();
            if (!n)
                n = String(t.Name || "").trim();
            n = n.replace(/[\\/:*?"<>|\u0000-\u001f]+/g, " ").replace(/\s+/g, " ").trim();
            return n || _TF("Table", "File name of a data table export when the table has no name (followed by the date and time)");
        }

        /**
         * Header alignment follows the values (table.css): a column whose values are right aligned (numbers, sizes,
         * durations, dates) gets `th.sw-table-thr` (the title ends where the values end, the sort icon before it), a centred one
         * (images, colours) `th.sw-table-thc`. Decided once per column set from the first non blank value of the first rows (the
         * computed alignment of the cell, so the cell renderers and app styles decide); columns with only blank values keep left.
         */
        UpdateHeadAlign() {
            const t = this;
            const rows = t.Body.rows;
            //  (not in the cards: they align every value left)
            if (!rows.length || t.HeadAlignSig === t.ColSig || t.E.classList.contains("sw-table-cards"))
                return;
            const ths = t.HeadRow.cells;
            let open = false;
            t.Vis.forEach((v, vi) => {
                const th = ths[vi + 1];
                if (!th || th.SwAlignSig === t.ColSig)
                    return;
                let a = "";
                for (let r = 0; r < rows.length && r < 5 && !a; ++r) {
                    const td = rows[r].cells[vi + 1];
                    if (td && !td.classList.contains("sw-table-blank") && (td.textContent.trim() !== "" || td.querySelector("img, svg, .sw-icon, button")))
                        a = getComputedStyle(td).textAlign;
                }
                if (!a) {
                    open = true;
                    return;
                }
                th.classList.toggle("sw-table-thr", a === "right" || a === "end");
                th.classList.toggle("sw-table-thc", a === "center");
                th.SwAlignSig = t.ColSig;
            });
            /** @type {string} The column set the header alignment was decided for (UpdateHeadAlign; open while a column had no value). */
            t.HeadAlignSig = open ? "" : t.ColSig;
        }

        /**
         * Calm grid details of a rendered cell (table.css): the web search glyph of a link to another site goes after the
         * text (as the external glyph; the grid shows it only on hover / focus, so the text of every link starts at the cell edge), and
         * destructive row actions (Sw.Cells.IsDestructiveAction, ex: Kill) get `sw-cell-action-danger` (the danger colour on hover).
         * @param {HTMLTableCellElement} td The cell.
         */
        QuietCell(td) {
            //  (as the original: a plain click on a value copies it, Sw.Clipboard.CopyOnClick of Sw.Cells is kept; the values are shown
            //  as the server sends them, a dot filler ". . . . ." too)
            //  One text node values: a shortened value (sw-capped, the CSS adds one "…") loses its own end dots, so a first line that
            //  ends a sentence shows "redacted…", never "redacted.…" / "....". The tool tip keeps the full value.
            const tn = td.childNodes.length === 1 && td.firstChild.nodeType === 3 ? td.firstChild : null;
            if (tn) {
                let s = tn.nodeValue;
                if (td.classList.contains("sw-capped"))
                    s = s.replace(/\s*(?:\.{1,3}|…)$/, "");
                if (s !== tn.nodeValue)
                    tn.nodeValue = s;
            }
            //  The web search glyph goes after the text in the grid, so the text starts at the column's left edge like its header and the
            //  other columns (Designer S3-W2: it sat before the text, 15 px in, also when it was invisible at rest); Sw.Cells puts it
            //  first, directly or inside the no-wrap span with the first word (span.sw-link-keep). table.css gives it no width at rest.
            for (const i of td.querySelectorAll("a.sw-link-ext .sw-link-search-icon")) {
                const a = i.closest("a.sw-link-ext");
                if (a && a.lastChild !== i)
                    a.append(i);
            }
            const C = Sw.Cells;
            if (C.IsDestructiveAction)
                for (const b of td.querySelectorAll("button.sw-cell-action"))
                    b.classList.toggle("sw-cell-action-danger", C.IsDestructiveAction({ Text: b.getAttribute("aria-label") || b.textContent, Title: b.title }));
            //  Row action icons in the one outline style of the grid (Designer S3-W1b: a filled skull among line glyphs): a server icon
            //  file with a line twin (Sw.Icons.LineOf, ex: skull.svg -> kill, registered below) is drawn as that line glyph
            const lineOf = Sw.Icons && Sw.Icons.LineOf;
            if (lineOf)
                for (const m of td.querySelectorAll("button.sw-cell-action > .sw-icon-mask")) {
                    const n = m.SwIconClass ? lineOf(m.SwIconClass) : null;
                    if (n) {
                        const extra = Array.from(m.classList).filter(c => c !== "sw-icon" && c !== "sw-icon-mask" && !/^Icon[A-Z0-9]/.test(c)).join(" ");
                        m.replaceWith(Sw.Icon(n, extra || undefined));
                    }
                }
            //  The row action buttons in one box (display: contents in the grid, hidden in the narrow row menu column, a flex row in the
            //  cards; Sw.Cells replaces the children when the actions change, then they are wrapped again)
            if (td.classList.contains(C.Classes.Actions) && td.querySelector(":scope > button.sw-cell-action")) {
                //  ... and a row menu button (shown instead of the buttons when they have no free room at the row end, table.css
                //  sw-table-actmenu): it lists the row's actions (CardActions)
                const mb = el("button.sw-table-rowmenu", {
                    type: "button", tabIndex: -1,
                    attrs: { "aria-haspopup": "menu", "aria-label": _TF("Row actions", "Accessible name of the row actions menu of a card of a data table") },
                    title: _TF("Row actions", "Accessible name of the row actions menu of a card of a data table"),
                    onclick: ev => {
                        ev.stopPropagation();
                        this.CardActions(td.parentElement, mb);
                    },
                }, lineIcon("kebab"));
                td.replaceChildren(el("span.sw-table-actbox", null, ...Array.from(td.childNodes)), mb);
            }
            //  File type images (ex: MIME mapping, "../icons/ext/php.svg"): the built-in multicolour file type icons, as on the explore
            //  pages, at the icon size of the grid; the extension is the accessible name
            for (const img of td.querySelectorAll("img")) {
                const m = /\/icons\/ext\/([^/?#]+)\.svg(?:[?#]|$)/i.exec(img.getAttribute("src") || "");
                if (!m)
                    continue;
                img.classList.add("sw-table-ftype");
                img.alt = decodeURIComponent(m[1]).replace(/^\.+/, "").toUpperCase();
            }
        }

        /**
         * The column that names a row best: the card title of the card layout and the name in action messages. Option
         * `CardTitle` (a column name) decides; else the first visible text column named like an identity (Name, Title, User, User name,
         * Login, Email, Subject, Host, File name, Label ...), else the first visible text key column that is not an opaque id (token,
         * hash, key), else a message column (Text, Message, Description ...), else the first text column that is not a key or an id. -1 = none (the card
         * heading is the row number only).
         * @returns {number} The visible column index, -1 = none.
         */
        TitleCol() {
            const t = this;
            if (t.TitleSig === t.ColSig)
                return t.TitleVi;
            /** @type {string} The column signature TitleVi was chosen for. */
            t.TitleSig = t.ColSig;
            const isAct = v => /^Actions(;|$)/.test(String(v.Col.Format || ""));
            //  (never a column the server hides by default: the card title is always shown)
            const str = v => v.Col.Type === "System.String" && !isAct(v) && (v.Col.Props & Props.NoTitle) === 0 && !srvHidden(v.Col);
            let vi = t.O.CardTitle ? t.Vis.findIndex(v => v.Col.Name === t.O.CardTitle) : -1;
            if (vi < 0) {
                const re = /^(display ?name|name|full ?name|title|user ?name|user|login|account|e-?mail|subject|host ?name|host|machine ?name|computer ?name|file ?name|label|caption)$/i;
                vi = t.Vis.findIndex(v => str(v) && (re.test(v.Col.Name) || re.test(v.Col.Title || "")));
            }
            if (vi < 0) {
                const keys = t.KeyCols || [];
                vi = t.Vis.findIndex(v => str(v) && keys.indexOf(v.Index) >= 0 && !/(token|hash|key|guid|secret)$/i.test(v.Col.Name));
            }
            //  (else the message of a log like table, else the first text column that is not an opaque id: a card opens with words, not "#1")
            if (vi < 0)
                vi = t.Vis.findIndex(v => str(v) && /^(text|message|description|desc|summary|caption)$/i.test(v.Col.Name));
            if (vi < 0)
                vi = t.Vis.findIndex(v => str(v) && !/(token|hash|key|guid|secret|id)$/i.test(v.Col.Name) && (v.Col.Props & Props.AnyKey) === 0);
            /** @type {number} The visible index of the row naming column (TitleCol), -1 = none. */
            t.TitleVi = vi;
            return vi;
        }

        /**
         * The name of a row for messages: the value of the TitleCol, else the key column text (ItemText).
         * @param {?HTMLTableRowElement} tr The row.
         * @returns {string} The name, "" = none.
         */
        RowName(tr) {
            const t = this;
            if (!tr)
                return "";
            const vi = t.TitleCol();
            const td = vi >= 0 ? tr.cells[vi + 1] : null;
            const s = td ? td.textContent.trim() : "";
            const n = s || tr.SwItem || "";
            //  The viewer's own row (MarkCurrent): "debug (you)", so a confirmation says whose session it ends
            return n && tr.classList.contains("sw-table-current")
                ? _T("{0} (you)", n, "Name of a data table row that is the viewer's own in confirmations and results (ex: End session for debug (you)), {0} the row name")
                : n;
        }

        /**
         * A click on a row action (capture phase, before Sw.Cells runs it): remember the action, its row and the number
         * of error toasts, so ActionDone can confirm the result.
         * @param {MouseEvent} ev The click.
         */
        ActionClick(ev) {
            const t = this;
            const b = ev.target && ev.target.closest ? ev.target.closest("button.sw-cell-action") : null;
            //  (a modifier click runs no action: ModClick only focuses the button)
            if (!b || b.disabled || !t.Body.contains(b) || !Sw.IsPureEvent(ev))
                return;
            //  (the previous click: stop watching it unless its request runs, ActionBusy follows that one to its end)
            if (t.PendingAct && t.PendingAct.Mo && !t.PendingAct.Get)
                t.PendingAct.Mo.disconnect();
            if (t.PendingAct && t.PendingAct.OffResult && !t.PendingAct.Get)
                t.PendingAct.OffResult();
            const tr = b.closest("tr");
            const p = {
                B: b, Tr: tr, Item: tr && tr.SwItem ? tr.SwItem : "", Name: t.RowName(tr), Text: b.getAttribute("aria-label") || b.textContent.trim(),
                //  (the done text of the action, Sw.Cells data-sw-done from the server: Admin N8 / G15)
                Done: b.getAttribute("data-sw-done") || "",
                Get: false, Errs: toastErrors(), At: Date.now(), Mo: null, Danger: false,
                //  (the keyboard place belongs to this action: the focus was on its button or in the grid when it was clicked; after
                //  every outcome it comes back to the row, KeepFocusPlace)
                HadFocus: document.activeElement === b || t.Grid.contains(document.activeElement),
            };
            //  (a new try on a row whose last action failed: its failure note goes)
            if (p.Item && t.Acted && t.Acted.has(p.Item) && t.Acted.get(p.Item).Failed) {
                t.Acted.delete(p.Item);
                t.MarkActed(tr);
            }
            //  A final action ends or removes the row's item (End session, Kill, Delete, Withdraw ...): done once. The server declares it
            //  (actionIsFinal, G47); the words of the text and the tool tip never decide it (Admin N53)
            p.Danger = actionIsFinal(b);
            //  The row's display name for Sw.Cells (its activity list item and messages: "Kill: devenv", not the key columns' labels)
            if (p.Name)
                b.dataset.swRowName = p.Name;
            //  The viewer's own row (MarkCurrent): Sw.Cells' confirmation can say that the action applies to the viewer (ex: signs you out)
            if (tr && tr.classList.contains("sw-table-current"))
                b.dataset.swRowCurrent = "true";
            else
                delete b.dataset.swRowCurrent;
            //  (Sw.Cells marks a request as busy while it runs, class sw-busy / aria-busy; a link that opens a page has none)
            if (window.MutationObserver) {
                p.Mo = new MutationObserver(() => t.ActionBusy(p));
                p.Mo.observe(b, { attributes: true, attributeFilter: ["class", "aria-busy"] });
            }
            //  The outcome is heard on the button itself too: a live refresh that moves the rows while the confirmation is open renders
            //  the row's place with another item and replaces this button, so its event no longer bubbles into the table (the
            //  failure was lost: no note, no announcement); ActionResult takes each event once
            const onResult = ev => t.ActionResult(ev);
            b.addEventListener("sw-cell-result", onResult);
            p.OffResult = () => b.removeEventListener("sw-cell-result", onResult);
            /** @type {?object} The row action that was clicked last (ActionClick), until its request completes (ActionDone). */
            t.PendingAct = p;
            t.KeepFocus(b, tr);
            /** (the confirmation watch of this action, KeepFocus: RowGone marks it, ActionResult / ActionDone mark it as ran) */
            p.Watch = t.FocusWatch && t.FocusWatch.B === b ? t.FocusWatch : null;
        }

        /**
         * A live refresh no longer lists the row of an action whose confirmation is open (someone else ended / withdrew it, or it
         * expired): the row stays in its place (KeepRow, muted, "No longer listed"), so the dialog stays next to the row it is about and
         * the rows below do not move; the clicked button gets `data-sw-row-gone` and a `sw-row-gone` event (detail `{ Item, Name }`; a
         * confirmation may listen and say so). When the action is confirmed anyway, its result says "already gone" (ActionDone) instead
         * of an error; when it is cancelled, the next refresh shows the list as it is (KeepFocus).
         * @param {object} w The confirmation watch (KeepFocus).
         */
        RowGone(w) {
            const t = this;
            w.Gone = true;
            const p = t.PendingAct && t.PendingAct.B === w.B ? t.PendingAct : null;
            if (p)
                p.Gone = true;
            t.KeepRow({ Item: w.Item, Row: w.Row, At: Math.max(0, w.At), Cols: t.ColSig, State: goneState(), Gone: true });
            if (!t.Acted)
                t.Acted = new Map();
            //  (final: the row's actions are not offered again, its item may have ended; the open confirmation says why, below)
            t.Acted.set(w.Item, { Text: p ? p.Text : "", At: new Date(), Final: true, Failed: null, Done: null, Gone: true });
            const name = p ? p.Name : "";
            //  (the cause as the table knows it: the server stopped listing the row; never "someone else" for sure)
            const reason = name
                ? _T("{0} is no longer listed: it may have ended, or someone else changed or removed it. Nothing was sent.", name, "Text of a row action confirmation after the server stopped listing its row, {0} names the row (ex: OpsVictim)")
                : _TF("This row is no longer listed: it may have ended, or someone else changed or removed it. Nothing was sent.", "Text of a row action confirmation after the server stopped listing its row");
            try {
                w.B.dataset.swRowGone = "true";
                w.B.dispatchEvent(new CustomEvent("sw-row-gone", { bubbles: true, detail: { Item: w.Item, Name: name, Reason: reason } }));
            }
            catch {
            }
        }

        /**
         * Keep a row in its place although the server may no longer list it (a final action's row, a row gone under its confirmation):
         * shown by the live refreshes until the user's next refresh (RenderRows), counted in the range text (UpdateFoot). Several rows
         * can be kept at once (by item), so a second row that goes away while another is kept is marked too.
         * @param {{Item: string, Row: object, At: number, Cols: string, State: string, Gone: ?boolean}} k The row: its item (ItemText),
         *   its data row, its place (row index), the column signature, its state word ("ended", "no longer listed") and Gone (RowGone).
         */
        KeepRow(k) {
            const t = this;
            if (!k || !k.Item || !k.Row)
                return;
            /** @type {?Map<string, {Item: string, Row: object, At: number, Cols: string, State: string, Gone: ?boolean}>} The kept rows by
             *  item (KeepRow, RenderRows). */
            t.KeptRows = t.KeptRows || new Map();
            t.KeptRows.set(k.Item, k);
        }

        /**
         * The kept row of an item (KeepRow), if any.
         * @param {string} item The item (ItemText).
         * @returns {?{Item: string, Row: object, At: number, Cols: string, State: string, Gone: ?boolean}}
         */
        KeptOf(item) {
            return item && this.KeptRows ? this.KeptRows.get(item) || null : null;
        }

        /**
         * Stop keeping the row of an item (KeepRow).
         * @param {string} item The item (ItemText).
         */
        DropKept(item) {
            if (this.KeptRows)
                this.KeptRows.delete(item);
        }

        /**
         * The row that shows an item now (rows are reused by position: after a live refresh with another order the row element of a
         * click may show another item).
         * @param {string} item The item (ItemText); "" = none.
         * @returns {?HTMLTableRowElement} The row, or null.
         */
        ItemRow(item) {
            return item ? Array.prototype.find.call(this.Body.rows, x => x.SwItem === item) || null : null;
        }

        /**
         * Focus a row after its action ended without its button (hidden by a final action, the row was rebuilt): the cell with its
         * note (the actions cell, where the result is read), else its first data cell; never another row (UX guide FOC-1, Ops R4-7).
         * @param {HTMLTableRowElement} tr The row.
         * @param {number} [ci] The cell index of the action (when the row has no actions cell).
         * @param {?HTMLElement} [from] The element the action was run from when its button is only in a menu (RowActionItems `SwActFrom`).
         */
        FocusActedRow(tr, ci, from) {
            const t = this;
            const r = Array.prototype.indexOf.call(t.Body.rows, tr) + 1;
            if (r <= 0)
                return;
            //  (an action run from the row's menu: the menu's button in the row, ex: the card's kebab, is its place)
            if (from && from.isConnected && tr.contains(from) && from.getClientRects().length) {
                const cell = from.closest("td");
                if (cell)
                    t.SetActive(cell);
                from.focus({ preventScroll: true });
                if (document.activeElement === from)
                    return;
            }
            const acts = Array.from(tr.cells).filter(c => c.classList.contains(Sw.Cells.Classes.Actions) && c.getClientRects().length);
            const host = acts.length ? acts[acts.length - 1] : null;
            const c = host ? host.cellIndex : Math.min(Math.max(1, ci || 1), tr.cells.length - 1);
            t.FocusCell(r, c);
        }

        /**
         * Keep the keyboard place through a row action's confirmation (or menu): while it is open the row keeps its action buttons shown
         * (sw-table-actpending, so the dialog can give the focus back to the button), and when it closes with the focus lost (the
         * body: the button was hidden or the row is gone) the focus returns to the row's action, else to the actions cell of the row that
         * now has its place (the next row), else to the grid.
         * @param {HTMLElement} b The action button.
         * @param {?HTMLTableRowElement} tr Its row.
         */
        KeepFocus(b, tr) {
            const t = this;
            if (!tr || !t.Body.contains(tr))
                return;
            const ri = Array.prototype.indexOf.call(t.Body.rows, tr) + 1;
            const td = b.closest("td");
            const ci = td ? td.cellIndex : tr.cells.length - 1;
            //  (Item / Row / At: a live refresh that no longer lists the row keeps it in place while its confirmation is open, RenderRows)
            const w = { Tr: tr, B: b, Item: tr.SwItem || "", Row: tr.SwRow || null, At: ri - 1 };
            /** @type {?object} The row action whose dialog is followed (KeepFocus). */
            t.FocusWatch = w;
            tr.classList.add("sw-table-actpending");
            /**
             * An overlay (dialog, menu) is open in this page or in the shell around it.
             * @returns {boolean}
             */
            const open = openOverlay;
            const lost = focusLost;
            const start = Date.now();
            let seen = false;
            const tick = () => {
                if (t.Dead || t.FocusWatch !== w)
                    return;
                const o = open();
                seen = seen || o;
                //  (no dialog after a short while: the action ran without one)
                if (o || (!seen && Date.now() - start < 600)) {
                    setTimeout(tick, 120);
                    return;
                }
                t.FocusWatch = null;
                tr.classList.remove("sw-table-actpending");
                //  (a row kept because it was gone under its confirmation, RowGone: when the confirmation was cancelled, no request ran,
                //  the next refresh shows the list as it is)
                if (w.Gone)
                    setTimeout(() => {
                        const p = t.PendingAct;
                        if (t.Dead || w.Ran || (p && p.B === b && (p.Get || p.Outcome)))
                            return;
                        const k = t.KeptOf(w.Item);
                        if (k && k.Gone) {
                            t.DropKept(w.Item);
                            if (t.Acted)
                                t.Acted.delete(w.Item);
                            t.Schedule(0);
                        }
                    }, 600);
                //  (the dialog gave the focus back into the rows: the user works there, the order stays held, FBK-7)
                if (t.Grid.contains(document.activeElement))
                    t.UserFocus = true;
                //  (an action run from the row's menu: its button has no box, so the closing dialog could not give the focus back to it
                //  and put it elsewhere, ex: the search field; the place is the row, checked right when the dialog closed)
                const fromMenu = !!(b.SwActFrom && b.SwActFrom.isConnected) && !t.Grid.contains(document.activeElement);
                if (!seen || !(lost() || fromMenu) || !document.hasFocus())
                    return;
                //  The row of the action NOW, by its item: rows are reused by position, so after a live refresh with another order the
                //  clicked row element (and its button) may show another item; the focus goes to this action's row, never to another
                //  row's result (Ops R4-7: a failed Kill put the focus on the other row's "Kill: done")
                const cur = w.Item ? t.ItemRow(w.Item) : (tr.isConnected && tr.parentElement === t.Body ? tr : null);
                if (cur) {
                    //  (the row the focus is put in for this action: ActionDone / KeepFocusPlace follow its item from there)
                    w.FocusTr = cur;
                    if (cur.contains(b) && !cur.classList.contains("sw-table-actfinal") && b.getClientRects().length) {
                        const cell = b.closest("td") || cur.cells[ci];
                        t.SetActive(cell);
                        b.focus({ preventScroll: true });
                        //  (a busy button is disabled while its request runs: its cell takes the focus, never the page body; ActionDone
                        //  gives it back to the button when it can)
                        if (document.activeElement !== b && cell)
                            cell.focus({ preventScroll: true });
                        return;
                    }
                    //  (its button is hidden, ex: a final action done before: the row's note / first cell)
                    t.FocusActedRow(cur, ci, b.SwActFrom || null);
                    return;
                }
                //  (the row is gone: the row that now has its place, else the grid)
                const rows = t.Body.rows.length;
                if (!rows) {
                    t.FocusGrid(true);
                    return;
                }
                const r = Math.min(ri, rows);
                t.FocusCell(r, Math.min(ci, t.Body.rows[r - 1].cells.length - 1));
            };
            setTimeout(tick, 60);
        }

        /**
         * The outcome of a row action, from Sw.Cells (the bubbling `sw-cell-result` event on the clicked button, dispatched before any
         * toast or activity result; detail `{ Ok, Value, Error, Text, Name, InPlace }`, see explore/README "Row actions"). The table is
         * the place of the result while the row is on the screen and the tab is visible: it sets `detail.InPlace = true`, so Sw.Cells
         * shows no toast and ends its activity job quietly (the result in exactly one place, UX guide FBK-2 / FBK-5). Off screen or in a
         * hidden tab it leaves the result to Sw.Cells (toast / activity list).
         * @param {CustomEvent} ev The event.
         */
        ActionResult(ev) {
            const t = this;
            const d = ev.detail;
            const b = ev.target && ev.target.closest ? ev.target.closest("button.sw-cell-action") : null;
            const p = t.PendingAct;
            //  (heard on the button and again in the table body: once)
            if (!d || typeof d !== "object" || !b || !p || p.B !== b || ev.SwTableSeen)
                return;
            ev.SwTableSeen = true;
            p.Outcome = d;
            if (p.Watch)
                p.Watch.Ran = true;
            const tr = t.ActRow(p);
            const inView = !!(tr && tr.isConnected && tr.getClientRects().length && document.visibilityState !== "hidden" && rowOnScreen(tr));
            if (inView) {
                d.InPlace = true;
                p.InPlace = true;
            }
        }

        /**
         * A row action request completed (the Sw.Cells refresh callback). The outcome is the server's (Sw.Cells' `sw-cell-result`, or the
         * refresh callback's argument): a thrown request and a `false` answer are failures, never "done" (UX guide FBK-5, TXT-30). Older
         * Sw.Cells without an outcome: success = no new error toast. Success marks the row (MarkActed: "✓ Kill: done", so a row the
         * server still lists does not look untouched); a failure shown in place marks it "✗ Kill: not done" with the reason. The result
         * is announced, and is a toast only when the row is not on the screen (and Sw.Cells did not show one). A final action
         * (declared by the server, actionIsFinal: End session, Kill, Delete ...) that succeeded is final for its row: updated in place (muted, the actions are not
         * offered again) and true is returned, so the caller does not reload the rows at once (the row keeps its place until the next
         * refresh); a quiet re-read right after it updates the counts (range text, `sw-table-count`, tab badges that follow it).
         * A final action that failed with a `false`, 404, 409 or 410 answer (maybeGone), or whose row the server stopped listing while its
         * confirmation was open (RowGone), is checked against the list: when the row is gone, its goal is met ("Withdraw invitation:
         * already gone, the server no longer lists it", polite, not an error); else the failure stands. The keyboard focus comes back to
         * the row after every outcome (KeepFocusPlace: the done note's cell when the buttons go away; never the page body).
         * @param {object} [res] The outcome passed to the refresh callback (`{ Ok, Value, Error, ... }`), if any.
         * @returns {boolean} True if the row was updated in place (do not reload now).
         */
        ActionDone(res) {
            const t = this;
            const p = t.PendingAct;
            if (!p)
                return false;
            t.PendingAct = null;
            if (p.Watch)
                p.Watch.Ran = true;
            //  (the outcome was dispatched before this callback)
            if (p.OffResult)
                setTimeout(p.OffResult, 0);
            //  (a request that ran: ActionBusy sees its end right after this call and finishes its activity job and note)
            if (p.Mo && !p.Get)
                p.Mo.disconnect();
            const out = res && typeof res === "object" && typeof res.Ok === "boolean" ? res : (p.Outcome && typeof p.Outcome.Ok === "boolean" ? p.Outcome : null);
            t.ActRow(p);
            //  (the place is this action's when the focus was on its button / in the grid at the click and is now in the row or lost: the
            //  confirmation closed, the busy button was disabled, the row was rebuilt)
            const hadFocus = !!(p.Tr && p.Tr.contains(document.activeElement))
                || !!(p.Watch && p.Watch.FocusTr && p.Watch.FocusTr.isConnected && p.Watch.FocusTr.contains(document.activeElement))
                || (!!p.HadFocus && focusLost())
                || !!(t.LastFocus && !t.LastFocus.Left && p.Tr && t.LastFocus.Tr === p.Tr && focusLost());
            if (!out && (!p.Get || Date.now() - p.At > 5 * 60000)) {
                t.KeepFocusPlace(p, hadFocus);
                return false;
            }
            const ok = out ? out.Ok : toastErrors() <= p.Errs;
            const inView = !!(p.Tr && p.Tr.isConnected && p.Tr.getClientRects().length);
            if (!ok) {
                //  The server says the goal is met already or the item is gone (its outcome kind, never a status guess): the row is
                //  final like after a done final action (its actions are not offered again, it stays in place until the user's refresh,
                //  counted in the range text), with the neutral "Kill: not needed. <reason>" (Ops R5-5, R5-1, Admin N35)
                const kind = outcomeKind(out);
                if (out && p.Item && endsItem(kind)) {
                    t.ActionEnded(p, out, kind, hadFocus);
                    return !!p.InPlace;
                }
                //  A failure: in the row when Sw.Cells left it to the table (else its error toast says it), announced assertively
                if (out && p.InPlace && p.Item) {
                    //  (a final action whose row may be gone already: the list decides, GoneCheck; a refusal for the user's role or
                    //  the input is never "gone")
                    if (p.Danger && (p.Gone || (kind !== "Denied" && kind !== "Invalid" && maybeGone(out)))) {
                        t.GoneCheck(p, out, hadFocus);
                        return true;
                    }
                    t.ActionFailed(p, out, hadFocus);
                    //  (nothing changed on the server: no reload now, which would also take the note away; the next refresh shows the row)
                    return true;
                }
                t.KeepFocusPlace(p, hadFocus);
                return false;
            }
            p.Ok = true;
            const msg = t.ActionMessage(p, out);
            if (out) {
                //  The result once: in place (Sw.Cells showed nothing), else Sw.Cells / the activity list showed it
                if (p.InPlace && Sw.Page.Announce)
                    Sw.Page.Announce(msg);
            }
            else {
                //  (older Sw.Cells: a toast only through the activity list when the action ran long enough to be a job there, or when the
                //  row is not on the screen)
                const job = p.BusyAt && Date.now() - p.BusyAt >= 2000 && Sw.Activity && Sw.Activity.Supported;
                if (!job) {
                    if (inView && Sw.Page.Announce)
                        Sw.Page.Announce(msg);
                    else if (Sw.Toast)
                        Sw.Toast(msg, { Kind: "ok" });
                }
            }
            t.SetActed(p, { Done: out && typeof out.Done === "string" && out.Done ? out.Done : p.Done || null });
            const final = !!(p.Danger && p.Item && p.Tr && p.Tr.isConnected && p.Tr.SwItem === p.Item);
            if (final && p.Tr.SwRow) {
                //  (the row kept in place after a final action, RenderRows; other kept rows stay kept)
                t.KeepRow({ Item: p.Item, Row: p.Tr.SwRow, At: Array.prototype.indexOf.call(t.Body.rows, p.Tr), Cols: t.ColSig, State: finalState(p.Text) });
                //  The counts follow at once (a quiet live re-read: the row stays in place, the range says "1 row (withdrawn, shown until
                //  you refresh)", sw-table-count tells tab badges), not only with the next refresh (tables without one stayed stale)
                t.Schedule(300);
            }
            t.KeepFocusPlace(p, hadFocus);
            return final;
        }

        /**
         * A final row action failed in a way that may mean its row is gone already (ActionDone: maybeGone, or RowGone saw it go under the
         * confirmation): re-read the list quietly with the row kept in place (KeepRow) and decide. Gone: the goal of the action is met,
         * the row is marked "already gone" and that is announced politely (no error, UX guide FBK-5 / TXT-30: the real outcome); the counts
         * follow (the re-read). Still listed: the failure stands ("✗ … not done", assertive), as before.
         * @param {object} p The action (ActionClick).
         * @param {object} out The outcome ({ Ok: false, ... }).
         * @param {boolean} hadFocus The focus belongs to the row (KeepFocusPlace).
         */
        async GoneCheck(p, out, hadFocus) {
            const t = this;
            const tr = p.Tr;
            if (!p.Gone && tr && tr.SwRow && tr.SwItem === p.Item)
                t.KeepRow({ Item: p.Item, Row: tr.SwRow, At: Array.prototype.indexOf.call(t.Body.rows, tr), Cols: t.ColSig, State: goneState(), Gone: true });
            try {
                await t.UpdateNow();
            }
            catch {
            }
            if (t.Dead)
                return;
            const d = t.Data;
            const gen = d && d.Rows && (d.Rows.length <= 0 || Array.isArray(d.Rows[0].Values));
            const get = gen ? (row, i) => row.Values[i] : (row, i) => row[t.Cols[i].Name];
            const listed = !!(d && d.Rows && d.Rows.some(x => t.ItemText(x, get) === p.Item));
            const k = t.KeptOf(p.Item);
            if (listed || !k) {
                t.DropKept(p.Item);
                t.ActionFailed(p, out, hadFocus);
                return;
            }
            //  Gone: the goal is met (someone else did it, or it expired); the row stays in place, muted, until the next refresh
            k.State = finalState(p.Text);
            const msg = p.Name
                ? _T("{0} ({1}): already gone, the service no longer lists it", p.Text, p.Name, "Message after a row action of a data table whose row was gone already (someone else did it, or it expired; not an error), {0} is the action (ex: Withdraw invitation), {1} names the row")
                : _T("{0}: already gone, the service no longer lists it", p.Text, "Message after a row action of a data table whose row was gone already (someone else did it, or it expired; not an error), {0} is the action (ex: Withdraw invitation)");
            const tip = _TF("Already gone: the service no longer lists it", "Note on a data table row after a row action found the row gone already (someone else did it, or it expired)");
            p.Danger = true;
            t.ActRow(p);
            t.SetActed(p, { Done: tip, Neutral: true });
            t.UpdateFoot();
            if (Sw.Page.Announce)
                Sw.Page.Announce(msg);
            t.KeepFocusPlace(p, hadFocus);
        }

        /**
         * A row action the server answered with an outcome that ends the row's item (outcomeKind "AlreadyDone": the goal was met
         * already, ex: "Process #4711 has already ended."; "Gone": the item is no longer there): not an error and not "not done". The
         * row shows the one verdict "Kill: not needed. <reason>" (or "Kill: no longer there. <reason>") as its result line in the quiet
         * colour; a final action's row (Kill, End session, Withdraw ...) is final like after a done final action: its actions are not
         * offered again (sw-table-actfinal), it stays in its place until the user's refresh (KeepRow) and the range text counts it ("1
         * ended row, shown until you refresh"). The same sentence is announced politely (when the row is on the screen). The user's
         * refresh shows the list as it is (a row the server still lists gets its actions back).
         * @param {object} p The action (ActionClick).
         * @param {object} out The outcome ({ Ok: false, Outcome, Reason, ... }).
         * @param {string} kind "AlreadyDone" or "Gone".
         * @param {boolean} hadFocus The focus belongs to the row (KeepFocusPlace).
         */
        ActionEnded(p, out, kind, hadFocus) {
            const t = this;
            const why = t.FailText(out);
            const note = t.FailNote(p, out);
            const tr = t.ActRow(p);
            const final = !!p.Danger;
            if (final && tr && tr.isConnected && tr.SwItem === p.Item && tr.SwRow)
                t.KeepRow({ Item: p.Item, Row: tr.SwRow, At: Array.prototype.indexOf.call(t.Body.rows, tr), Cols: t.ColSig, State: kind === "Gone" ? goneState() : finalState(p.Text) });
            t.SetActed(p, { Failed: why, Note: note, Neutral: true, Final: final, Kind: kind });
            if (p.InPlace && Sw.Page.Announce)
                Sw.Page.Announce(t.ActionMessage(p, out));
            //  (the counts follow at once: a quiet re-read, the row stays in place)
            if (final)
                t.Schedule(300);
            t.KeepFocusPlace(p, hadFocus);
        }

        /**
         * Remember a row action's result for its row (by ItemText, it survives the live refreshes) and mark the row (MarkActed).
         * @param {object} p The action (ActionClick).
         * @param {{Failed: ?string, Done: ?string, Note: ?string, Neutral: ?boolean, Final: ?boolean, Kind: ?string}} r The failure
         *   reason (a failed action) with its visible note ("Kill: not done. Process #65596 is protected.") and Neutral (not an error:
         *   the server says the goal is met already, or the row is gone), or the server's done text (optional); Final (default: a
         *   final action that did not fail) hides the row's actions; Kind = the server's outcome kind (outcomeKind).
         */
        SetActed(p, r) {
            const t = this;
            if (p.Item) {
                if (!t.Acted)
                    /** @type {Map<string, {Text: string, At: Date, Final: boolean, Failed: ?string, Done: ?string, Note: ?string, Neutral: boolean, Kind: ?string}>}
                     *  The rows a row action ran on (by ItemText) and its result, MarkActed. */
                    t.Acted = new Map();
                t.Acted.set(p.Item, {
                    Text: p.Text, At: new Date(), Final: typeof r.Final === "boolean" ? r.Final : (!r.Failed && !!p.Danger), Failed: r.Failed || null, Done: r.Done || null,
                    Note: r.Note || null, Neutral: !!r.Neutral, Kind: r.Kind || null,
                });
            }
            //  (the row that shows the item now: a re-read may have moved it)
            t.ActRow(p);
            if (p.Tr)
                t.MarkActed(p.Tr);
            t.SaveActs();
        }

        /**
         * The session storage key of this table's row action results (SaveActs / LoadActs).
         * @returns {string} The key.
         */
        ActsKey() {
            return "SysWeaver.TableActs." + location.pathname + "|" + (this.Url || this.O.Name || "");
        }

        /**
         * Keep the row action results (t.Acted: "Kill: done", "Kill: not done. <reason>", "No longer listed") in the tab's session
         * storage, so they survive the page being built again (the shell loads the page again when the window turns into the phone
         * layout; the rows <-> cards switch never loses a result). Only the result texts and the row keys (the masked item text), no
         * row data; at most 50, for 10 minutes.
         */
        SaveActs() {
            const t = this;
            try {
                const list = t.Acted ? Array.from(t.Acted.entries()).slice(-50).map(([k, a]) => [k, Object.assign({}, a, { At: a.At ? a.At.getTime() : Date.now() })]) : [];
                //  (written only when the results changed)
                const sig = JSON.stringify(list);
                if (sig === t.ActsSig)
                    return;
                /** @type {string} The results last kept (SaveActs). */
                t.ActsSig = sig;
                const key = t.ActsKey();
                if (!list.length)
                    Sw.Storage.Session.Remove(key);
                else
                    Sw.Storage.Session.SetJson(key, { Saved: Date.now(), List: list });
            }
            catch {
            }
        }

        /** Restore the row action results kept by SaveActs (younger than 10 minutes). */
        LoadActs() {
            const t = this;
            try {
                const s = Sw.Storage && Sw.Storage.Session ? Sw.Storage.Session.GetJson(t.ActsKey(), null) : null;
                if (!s || !Array.isArray(s.List) || !(Date.now() - (s.Saved || 0) < 10 * 60000))
                    return;
                t.Acted = new Map(s.List.filter(x => Array.isArray(x) && typeof x[0] === "string" && x[1] && typeof x[1].Text === "string")
                    .map(([k, a]) => [k, Object.assign({}, a, { At: new Date(a.At || Date.now()) })]));
            }
            catch {
            }
        }

        /**
         * A row action failed and its row is still listed: the result in the row, visible with its reason (MarkActed: the note at the row
         * end and the reason line in the row, wrapping, on cards too; never only a tool tip, UX guide FBK-1 / FBK-5, Ops R4-5, Admin N26),
         * announced (assertive; polite when the server says the goal is met already: outcomeKind), and the focus back to the row.
         * @param {object} p The action (ActionClick).
         * @param {object} out The outcome ({ Ok: false, ... } from Sw.Cells' `sw-cell-result`).
         * @param {boolean} hadFocus The focus belongs to the row (KeepFocusPlace).
         */
        ActionFailed(p, out, hadFocus) {
            const t = this;
            const neutral = endsItem(outcomeKind(out));
            t.SetActed(p, { Failed: t.FailText(out), Note: t.FailNote(p, out), Neutral: neutral });
            if (Sw.Page.Announce)
                Sw.Page.Announce(t.ActionMessage(p, out), neutral ? undefined : { Assertive: true });
            t.KeepFocusPlace(p, hadFocus);
        }

        /**
         * The visible result line of a failed row action in its row: Sw.Cells' `Note` when it gives one, else "Kill: not done. <reason>"
         * (the server's outcome kind, outcomeKind: "AlreadyDone" → "Kill: not needed. Process #65596 has already ended.", "Gone" →
         * "Withdraw invitation: no longer there. <reason>"; never "not done" for those, and never "not needed" for a refusal).
         * @param {object} p The action (ActionClick).
         * @param {object} out The outcome.
         * @returns {string} The text.
         */
        FailNote(p, out) {
            const why = this.FailText(out);
            const kind = outcomeKind(out);
            if (kind === "AlreadyDone")
                return _T("{0}: not needed. {1}", p.Text, why, "Result line in a data table row after a row action whose goal the server says was met already (not an error), {0} is the action (ex: Kill), {1} the server's reason (a sentence, ex: The process has already ended.)");
            if (kind === "Gone")
                return _T("{0}: no longer there. {1}", p.Text, why, "Result line in a data table row after a row action whose item the server says is gone (not an error), {0} is the action (ex: Withdraw invitation), {1} the server's reason (a sentence)");
            if (out && typeof out.Note === "string" && out.Note.trim())
                return out.Note.trim();
            return _T("{0}: not done. {1}", p.Text, why, "Result line in a data table row after a row action failed, {0} is the action (ex: Kill), {1} the reason (a sentence)");
        }

        /**
         * The row of a row action now: the row that shows its item (rows are reused by position, so after a refresh with another
         * order the row element the click came from may show another item), else the clicked row. Updates p.Tr.
         * @param {object} p The action (ActionClick).
         * @returns {?HTMLTableRowElement} The row.
         */
        ActRow(p) {
            const t = this;
            if (p.Item && !(p.Tr && p.Tr.isConnected && p.Tr.SwItem === p.Item)) {
                const r = Array.prototype.find.call(t.Body.rows, x => x.SwItem === p.Item);
                if (r)
                    p.Tr = r;
            }
            return p.Tr;
        }

        /**
         * The reason of a failed row action for its note and announcement: the server's message, else "the server answered no"
         * (a `false` answer), else "the server could not be reached".
         * @param {object} out The outcome ({ Ok: false, Value, Error }).
         * @returns {string} The reason.
         */
        FailText(out) {
            //  (Sw.Cells' Reason: the server's reason as a sentence; else the error text; always a sentence: capital, end mark)
            if (out && typeof out.Reason === "string" && out.Reason.trim())
                return sentence(out.Reason);
            const e = out ? out.Error : null;
            const m = e ? (typeof e === "string" ? e : (e.message || String(e))) : "";
            if (m)
                return sentence(m);
            if (out && out.Value === false)
                return _TF("The service did not do it (it answered no).", "Reason of a failed row action of a data table when the service answered false (ex: the item was already gone)");
            return _TF("The service could not be reached.", "Reason of a failed row action of a data table when the request failed without a message");
        }

        /**
         * Remember the last focused element of the grid (focusin), so the keyboard place can be restored when it goes away
         * (KeepFocusPlace, RenderRows).
         * @param {EventTarget} e The focused element.
         */
        NoteFocus(e) {
            const t = this;
            if (!e || !e.closest)
                return;
            const td = e.closest("td,th");
            const tr = td ? td.parentElement : null;
            /** @type {?{El: Element, Tr: ?HTMLTableRowElement, R: number, C: number}} The last focused place in the grid (NoteFocus). */
            t.LastFocus = { El: e, Tr: tr && tr.parentElement === t.Body ? tr : null, R: tr && tr.parentElement === t.Body ? tr.sectionRowIndex + 1 : 0, C: td ? td.cellIndex : 1 };
        }

        /**
         * Keep the keyboard place after a row action (UX guide FOC-1): when the focus was in the row and is lost (the action buttons of a
         * final action are hidden, the row was rebuilt), it moves to the row's actions cell (the done note is read there), else to the
         * cell that now has the row's place; never to the page body. Checked after the row is marked and again after the next frame.
         * @param {object} p The action (ActionClick).
         * @param {boolean} hadFocus The focus was in the row when the action ended.
         */
        KeepFocusPlace(p, hadFocus) {
            const t = this;
            if (!hadFocus)
                return;
            //  (the row element the focus is in now, when it is this action's row: rows are reused by position, so a re-read that moves
            //  the rows can leave the focus on the same element while it shows another item, with another row's result; Ops R4-7)
            const row0 = () => {
                const ae = document.activeElement;
                const x = ae && ae.closest ? ae.closest("tr") : null;
                return x && x.parentElement === t.Body ? x : null;
            };
            const tr0 = row0();
            const mine = !!(tr0 && p.Item && (tr0.SwItem === p.Item || (p.Watch && p.Watch.FocusTr === tr0)));
            const fix = () => {
                const swapped = mine && row0() === tr0 && tr0.SwItem !== p.Item;
                if (t.Dead || !(focusLost() || swapped) || !document.hasFocus() || openOverlay())
                    return;
                //  (the focus comes back into the rows: the user works there, the order stays held, FBK-7)
                t.UserFocus = true;
                const r = t.ActRow(p);
                const tr = r && r.isConnected && r.parentElement === t.Body && (!p.Item || r.SwItem === p.Item) ? r : null;
                if (tr) {
                    const host = Array.from(tr.cells).filter(c => c.classList.contains(Sw.Cells.Classes.Actions)).pop() || tr.cells[Math.min(tr.cells.length - 1, 1)];
                    if (p.B && p.B.isConnected && tr.contains(p.B) && p.B.getClientRects().length && !tr.classList.contains("sw-table-actfinal") && !p.B.disabled) {
                        t.SetActive(p.B.closest("td") || host);
                        p.B.focus({ preventScroll: true });
                        //  (a button that can't take the focus now: the cell takes it, never the page body)
                        if (document.activeElement === p.B)
                            return;
                    }
                    if (host) {
                        t.SetActive(host);
                        host.focus({ preventScroll: true });
                        return;
                    }
                }
                t.RestoreFocusPlace();
            };
            fix();
            requestAnimationFrame(fix);
            setTimeout(fix, 200);
            //  (a confirmation that is still leaving, or a busy button enabled again a little later)
            setTimeout(fix, 600);
        }

        /**
         * The focused element of the grid went away (its row was rebuilt or removed, the button was hidden) and the focus fell to the
         * page body: focus the cell at the same place (row and column, clamped), else the grid.
         */
        RestoreFocusPlace() {
            const t = this;
            const lf = t.LastFocus;
            if (!lf || lf.Left || t.Dead || !focusLost() || !document.hasFocus() || openOverlay())
                return;
            if (lf.El && lf.El.isConnected && lf.El.getClientRects().length)
                return;
            const rows = t.Body.rows.length;
            if (lf.R > 0 && rows) {
                const r = Math.min(lf.R, rows);
                t.FocusCell(r, Math.min(lf.C, t.Body.rows[r - 1].cells.length - 1));
            }
            else
                t.FocusGrid(true);
        }

        /**
         * The result text of a row action ("End session: done (ops)", "Kill (devenv): not done, <reason>", the server's done text of the outcome,
         * else the done text of the action, its button data-sw-done).
         * @param {object} p The action (ActionClick).
         * @param {?object} [out] The outcome (ActionDone), if known.
         * @returns {string} The text.
         */
        ActionMessage(p, out) {
            if (out && !out.Ok) {
                //  (named like the confirmation: Sw.Cells' Work "Kill OpsVictim (65596)", else "Kill (OpsVictim)"; one sentence per part)
                const name = typeof out.Work === "string" && out.Work.trim()
                    ? out.Work.trim()
                    : p.Name ? _T("{0} ({1})", p.Text, p.Name, "Name of a row action and its row in messages of a data table, {0} is the action (ex: Kill), {1} names the row (ex: devenv)") : p.Text;
                //  (one verdict: the same words as the row's result line, FailNote)
                const kind = outcomeKind(out);
                return kind === "AlreadyDone"
                    ? _T("{0}: not needed. {1}", name, this.FailText(out), "Message after a row action of a data table whose goal was met already (not an error), {0} is the action and its row (ex: Kill devenv (4711)), {1} the server's reason (a sentence, ex: The process has already ended.)")
                    : kind === "Gone"
                        ? _T("{0}: no longer there. {1}", name, this.FailText(out), "Message after a row action of a data table whose item the server says is gone (not an error), {0} is the action and its row (ex: Withdraw invitation (ann@example.com)), {1} the server's reason (a sentence)")
                        : _T("{0}: not done. {1}", name, this.FailText(out), "Message after a row action of a data table failed, {0} is the action and its row (ex: Kill devenv (4711)), {1} the reason (a sentence)");
            }
            if (out && typeof out.Done === "string" && out.Done)
                return out.Done;
            //  (the action's own done text from the server's column, TableDataActionsFullAttribute: Admin N8 / G15)
            if (p.Done)
                return p.Done;
            //  (named like the confirmation and the failure: "Kill OpsVictim (65596): done")
            if (out && typeof out.Work === "string" && out.Work.trim())
                return _T("{0}: done", out.Work.trim(), "Message after a row action of a data table succeeded, {0} is the action and its row (ex: Kill devenv (4711))");
            return p.Name
                ? _T("{0}: done ({1})", p.Text, p.Name, "Message after a row action of a data table succeeded, {0} is the action (ex: End session), {1} names the row (ex: the user name)")
                : _T("{0}: done", p.Text, "Message after a row action of a data table succeeded, {0} is the action (ex: End session)");
        }

        /**
         * A row action's busy state changed (its request starts or ends; Sw.Cells sets `aria-busy` / `sw-busy` on the button and shows
         * the busy button, Sw.Work, and after 2 s an activity list job). While it runs longer than 2 s (UX guide FBK-4) the table adds a
         * note in the row's actions cell that says what it waits for and the time so far ("Kill: waiting for the server, 4 s"; not a
         * live region), so the row itself shows that the action still runs; at the end the note goes (ActionEnd) and the done note or
         * the error takes its place.
         * @param {object} p The action (ActionClick).
         */
        ActionBusy(p) {
            const t = this;
            const b = p.B;
            const busy = b.classList.contains("sw-busy") || b.getAttribute("aria-busy") === "true";
            if (busy && !p.Get) {
                p.Get = true;
                p.BusyAt = Date.now();
                p.Timer = setInterval(() => t.ActionTick(p), 250);
                return;
            }
            if (busy || !p.Get || p.Ended)
                return;
            p.Ended = true;
            if (p.Mo)
                p.Mo.disconnect();
            clearInterval(p.Timer);
            //  (after Sw.Cells' refresh callback, ActionDone, and its failure toast)
            setTimeout(() => t.ActionEnd(p), 120);
        }

        /**
         * One second of a running row action (ActionBusy): after 2 s the waiting note.
         * @param {object} p The action.
         */
        ActionTick(p) {
            const t = this;
            if (p.Ended || t.Dead)
                return;
            //  One elapsed time for the note and the button's tool tip (Sw.Work: the same start, the same 250 ms tick and the same
            //  text "3 s" / "1 min 5 s"), so the two never disagree
            const ms = Date.now() - p.BusyAt;
            if (ms < 2000)
                return;
            const W = Sw.Work;
            const s = Math.max(0, Math.round(ms / 1000));
            const text = W && typeof W.Elapsed === "function" ? W.Elapsed(ms) : _T("{0} s", s, "Note next to a busy row action of a data table (ex: Kill) that still runs after 2 seconds: the seconds so far. {0} is the whole number of seconds, ex: 3");
            const tr = t.ActRow(p);
            if (tr && tr.isConnected) {
                //  (short, so the actions column does not grow over the data: the time; the tool tip says what it waits for; the
                //  button next to it shows the spinner)
                if (!p.Note)
                    p.Note = el("span.sw-table-done.sw-table-pending", null, el("span"));
                if (p.Note.lastChild.textContent !== text) {
                    p.Note.lastChild.textContent = text;
                    p.Note.title = _T("{0}: working ({1})", p.Text, text, "Tool tip of a busy button after 2 s. {0} is the button's name, {1} the elapsed time, ex: 'Kill: working (3 s)'");
                }
                const cells = Array.from(tr.cells).filter(c => c.classList.contains(Sw.Cells.Classes.Actions));
                const host = cells.length ? cells[cells.length - 1] : null;
                if (host && p.Note.parentElement !== host)
                    host.append(p.Note);
            }
        }

        /**
         * A row action's request ended (ActionBusy): remove the waiting note.
         * @param {object} p The action.
         */
        ActionEnd(p) {
            if (p.Note)
                p.Note.remove();
        }

        /**
         * Mark a row a row action ran on: a "✓ End session: done" note at the row end (in the actions cell, always shown) and in the
         * card heading, with a tool tip that says when and that the server may still list the row; after a failure shown in place
         * "⚠ Kill: not done" with the reason in the tool tip (the actions stay, so it can be tried again).
         * @param {HTMLTableRowElement} tr The row.
         */
        MarkActed(tr) {
            const t = this;
            const a = t.Acted && tr.SwItem ? t.Acted.get(tr.SwItem) : null;
            const failed = !!a && !!a.Failed;
            //  (muted values: a done action, and a row whose item ended / is gone, outcomeKind; a refused action keeps its values)
            tr.classList.toggle("sw-table-acted", !!a && (!failed || !!a.Final));
            tr.classList.toggle("sw-table-actfailed", failed);
            //  (a destructive action is final: its buttons are hidden, table.css; RowActionItems leaves them out)
            tr.classList.toggle("sw-table-actfinal", !!a && !!a.Final);
            const rn = tr.cells[0];
            let chip = tr.SwDone;
            if (!a) {
                if (chip)
                    chip.remove();
                if (rn && rn.dataset.swDone) {
                    delete rn.dataset.swDone;
                    delete rn.dataset.swDoneKind;
                }
                if (tr.SwWhy)
                    t.MarkWhy(tr, "");
                return;
            }
            //  A failure shown in place (ActionDone with Sw.Cells' outcome): "✗ Kill: not done" and the result line with the reason in
            //  the row (MarkWhy); the actions stay (try again); else the server's done text, else "✓ Kill: done"
            //  (a row the server stopped listing while its action's confirmation was open, RowGone: "No longer listed")
            const neutral = failed && !!a.Neutral;
            //  (a failure: the whole sentence with the reason, "Kill: not done. Process #65596 has already ended." (Sw.Cells' Note when it
            //  gives one); shown as the row's result line, MarkWhy, and read in the actions cell)
            const full = failed ? (a.Note || _T("{0}: not done. {1}", a.Text, a.Failed, "Result line in a data table row after a row action failed, {0} is the action (ex: Kill), {1} the reason (a sentence)")) : "";
            const text = failed
                ? full
                : a.Gone
                    ? _TF("No longer listed", "Note on a data table row that the server stopped listing while the confirmation of a row action on it was open (someone else changed or removed it)")
                    : (a.Done || _T("{0}: done", a.Text, "Message after a row action of a data table succeeded, {0} is the action (ex: End session)"));
            //  (one verdict: a neutral result's tool tip is its sentence and the time, never "did not work")
            const tip = neutral
                ? _T("{0} ({1})", full, a.At.toLocaleTimeString(), "Tool tip of the result note on a data table row after a row action whose goal was met already or whose item is gone, {0} is the result sentence (ex: Kill: not needed. The process has already ended.), {1} the time")
                : failed
                    ? _T("{0} did not work at {1}: {2}", a.Text, a.At.toLocaleTimeString(), a.Failed, "Tool tip of the failure note on a data table row after a row action failed, {0} is the action (ex: Kill), {1} the time, {2} the reason")
                    : a.Gone
                        ? _T("The service stopped listing this row at {0}: it may have ended, or someone else changed or removed it.", a.At.toLocaleTimeString(), "Tool tip of the note 'No longer listed' on a data table row, {0} the time")
                        //  (only a final action's row may still be listed until it goes, ex: an ended session; a row whose action changed
                        //  its state, ex: Disable, stays with its new actions: Admin N47)
                        : a.Final
                            ? _T("You used {0} on this row at {1}. The service may still list the row until it is removed or expires.", a.Text, a.At.toLocaleTimeString(),
                                "Tool tip of the done note on a data table row after a row action (ex: End session), {0} is the action, {1} the time")
                            : _T("You used {0} on this row at {1}.", a.Text, a.At.toLocaleTimeString(),
                                "Tool tip of the done note on a data table row after a row action that changed the row (ex: Disable a user account), {0} is the action, {1} the time");
            //  (a row that is no longer listed, or whose action found it gone already: information, not a success; ✓ only for "done")
            const info = !failed && (!!a.Gone || !!a.Neutral);
            //  The server's done text (a sentence, ex: "ann@example.com is disabled and can't sign in until the account is enabled again.")
            //  is the row's result line in the grid (MarkWhy "ok": wrapping, visible text; Admin N47: as the end note it was cut to "…" in the
            //  actions cell); the end note keeps it for screen readers. Cards show it in the card heading.
            const long = !failed && !info && !!a.Done && !t.E.classList.contains("sw-table-cards");
            const kind = neutral ? "n" : failed ? "x" : info ? "i" : "ok";
            if (chip && chip.SwKind !== kind) {
                chip.remove();
                chip = tr.SwDone = null;
            }
            if (!chip) {
                /** @type {HTMLElement} The done (or failure) note of the row (MarkActed). */
                chip = tr.SwDone = el(neutral ? "span.sw-table-done.sw-table-failnote.sw-table-neutral" : failed ? "span.sw-table-done.sw-table-failnote" : info ? "span.sw-table-done.sw-table-neutral" : "span.sw-table-done", null,
                    neutral || info ? Sw.Icon("info", "sw-table-licon") : failed ? Sw.Icon("warning", "sw-table-licon") : lineIcon("check"), el("span"));
                chip.SwFailed = failed;
                chip.SwKind = kind;
            }
            //  A failure (or a neutral "not needed") shows its reason as text in the row (the result line, MarkWhy), not only in a tool tip;
            //  so does the server's done text (long, above)
            t.MarkWhy(tr, long ? text : full, long ? "ok" : neutral);
            chip.classList.toggle("sw-table-donelong", long);
            if (chip.lastChild.textContent !== text)
                chip.lastChild.textContent = text;
            chip.title = tip;
            const cells = Array.from(tr.cells).filter(c => c.classList.contains(Sw.Cells.Classes.Actions));
            const host = cells.length ? cells[cells.length - 1] : null;
            if (host && chip.parentElement !== host)
                host.append(chip);
            //  (cards: the ✓ note in the card heading; a failure keeps its note in the actions, next to the buttons to try again)
            if (rn && failed && rn.dataset.swDone) {
                delete rn.dataset.swDone;
                delete rn.dataset.swDoneKind;
            }
            else if (rn && !failed) {
                if (rn.dataset.swDone !== text)
                    rn.dataset.swDone = text;
                //  (the card heading's mark follows the kind: ✓ done, ⓘ no longer listed / already gone)
                if (info)
                    rn.dataset.swDoneKind = "info";
                else
                    delete rn.dataset.swDoneKind;
            }
        }

        /**
         * The result line of a failed row action in its row (UX guide FBK-1 "rejected and why", FBK-5; Ops R4-5, Admin N26): the whole
         * sentence ("⚠ Kill: not done. Process #65596 has already ended.") as visible, wrapping text, never only a tool tip. Grid: a band
         * in the room the row gets below its values (the row grows by the band's height, `--sw-table-whyh`), drawn by a layer of the
         * scrolled area at the visible left edge (PlaceWhys: it stays in view when the grid scrolls sideways, and no cell clips it),
         * left of the row actions, so it covers no value and no button; screen readers read the sentence in the row's actions cell (the
         * row's note, visually hidden in the grid). Cards: a line under the card title. Neutral ("not needed": the server says the goal
         * was met already): the same line in the quiet colour. "ok": the server's done text of a successful action (MarkActed, Admin
         * N47), the same line in the success colour with the check mark. "" removes it.
         * @param {HTMLTableRowElement} tr The row.
         * @param {string} text The sentence, "" = none.
         * @param {boolean|string} [neutral] Not an error (true), or "ok" (a success).
         */
        MarkWhy(tr, text, neutral) {
            const t = this;
            let w = tr.SwWhy;
            if (!text) {
                if (w)
                    w.remove();
                tr.SwWhy = null;
                tr.classList.remove("sw-table-haswhy");
                tr.style.removeProperty("--sw-table-whyh");
                if (t.Whys && t.Whys.delete(tr))
                    t.PlaceWhys();
                return;
            }
            const kind = neutral === "ok" ? "ok" : neutral ? "n" : "x";
            if (w && w.SwKind !== kind) {
                w.remove();
                w = null;
            }
            if (!w) {
                /** @type {?HTMLElement} The result line of a row action (MarkWhy): a failure, a neutral result or the server's done text. */
                w = tr.SwWhy = kind === "ok"
                    ? el("div.sw-table-why.sw-table-ok", null, lineIcon("check"), el("span"))
                    : el(neutral ? "div.sw-table-why.sw-table-neutral" : "div.sw-table-why", null, Sw.Icon(neutral ? "info" : "warning", "sw-table-licon"), el("span"));
                w.SwKind = kind;
            }
            if (w.lastChild.textContent !== text)
                w.lastChild.textContent = text;
            tr.classList.add("sw-table-haswhy");
            if (t.E.classList.contains("sw-table-cards")) {
                //  (cards: in the card, under its title, read with it)
                const acts = Array.from(tr.cells).filter(c => c.classList.contains(Sw.Cells.Classes.Actions));
                const host = tr.querySelector(":scope > td.sw-table-cardtitle:not(.sw-table-blank)") || acts.pop() || tr.cells[0];
                w.removeAttribute("aria-hidden");
                w.style.width = "";
                w.style.transform = "";
                w.SwW = 0;
                if (host && w.parentElement !== host)
                    host.append(w);
                tr.style.removeProperty("--sw-table-whyh");
                return;
            }
            if (!t.WhyLayer) {
                /** @type {?HTMLElement} The layer of the result lines of the grid (MarkWhy, PlaceWhys), in the scrolled area. */
                t.WhyLayer = el("div.sw-table-whylayer");
                const place = () => t.PlaceWhys();
                t.Scroll.addEventListener("scroll", place, { passive: true });
                window.addEventListener("resize", place);
                t.Offs.push(() => window.removeEventListener("resize", place));
            }
            if (t.WhyLayer.parentElement !== t.Scroll)
                t.Scroll.append(t.WhyLayer);
            //  (the sentence is read in the row: its note; the band is for the eyes)
            w.setAttribute("aria-hidden", "true");
            if (w.parentElement !== t.WhyLayer)
                t.WhyLayer.append(w);
            /** @type {?Set<HTMLTableRowElement>} The grid rows with a result line (MarkWhy, PlaceWhys). */
            t.Whys = t.Whys || new Set();
            t.Whys.add(tr);
            t.PlaceWhys();
        }

        /**
         * Mark every row with a row action result again (MarkActed: notes, result lines), ex: after the rows <-> cards switch, so a
         * result never goes missing when the layout changes without a re-render.
         */
        RemarkActed() {
            const t = this;
            if (!t.Body)
                return;
            for (const tr of Array.from(t.Body.rows))
                if (tr.SwWhy || (t.Acted && tr.SwItem && t.Acted.has(tr.SwItem)))
                    t.MarkActed(tr);
            t.PlaceWhys();
        }

        /**
         * Place the result lines of the grid (MarkWhy) under their rows: the width of the visible area left of the row actions (they are
         * centred in the row and must not cover the line), the room of the row (`--sw-table-whyh`: the row grows by the line's height),
         * then the place at the bottom of the row, at the visible left edge. Lines of rows that went away are removed. Called when a line
         * changes, after each render, on scroll and resize.
         */
        PlaceWhys() {
            const t = this;
            if (!t.Whys || !t.WhyLayer)
                return;
            const s = t.Scroll;
            const cards = t.E.classList.contains("sw-table-cards");
            const sr = s.getBoundingClientRect();
            const x0 = sr.left + s.clientLeft;
            const y0 = sr.top + s.clientTop;
            //  1. the width and the room of each row
            for (const tr of Array.from(t.Whys)) {
                const w = tr.SwWhy;
                if (cards || !w || !tr.isConnected || tr.parentElement !== t.Body || w.parentElement !== t.WhyLayer) {
                    t.Whys.delete(tr);
                    continue;
                }
                const last = tr.cells[tr.cells.length - 1];
                let vw = s.clientWidth - 16;
                if (last && last.classList.contains(Sw.Cells.Classes.Actions) && last.getClientRects().length)
                    vw = Math.min(vw, last.getBoundingClientRect().left - x0 - 16);
                vw = Math.round(Math.max(160, vw));
                if (w.SwW !== vw) {
                    w.SwW = vw;
                    w.style.width = vw + "px";
                }
                const h = Math.ceil(w.getBoundingClientRect().height);
                const want = (h > 0 ? h : 18) + "px";
                if (tr.style.getPropertyValue("--sw-table-whyh") !== want)
                    tr.style.setProperty("--sw-table-whyh", want);
            }
            //  (a line whose row went away: the layer keeps only the lines of shown rows)
            const live = new Set(Array.from(t.Whys, tr => tr.SwWhy));
            for (const w of Array.from(t.WhyLayer.children))
                if (!live.has(w))
                    w.remove();
            //  2. the places, after the rows took their room
            for (const tr of t.Whys) {
                const w = tr.SwWhy;
                const r = tr.getBoundingClientRect();
                w.style.visibility = r.height > 0 ? "" : "hidden";
                const x = Math.round(s.scrollLeft + 8);
                const y = Math.round(r.bottom - y0 + s.scrollTop - w.offsetHeight - 5);
                const tf = "translate(" + x + "px, " + y + "px)";
                if (w.style.transform !== tf)
                    w.style.transform = tf;
            }
        }

        /**
         * The "You" marker (Ops F12): a row the server marks as the viewer's own (a bool column `IsCurrent`, ex: your session in Active
         * sessions; feature-detected, the column may be hidden) gets `tr.sw-table-current`, a "You" badge before its name (the
         * TitleCol cell, also the card heading; css generated content, so the cell text and the row name stay the server's) and the
         * row name for confirmations and results says "(you)" (RowName). Tables without the column are unchanged.
         * @param {HTMLTableRowElement} tr The row.
         * @param {object} row The row data.
         * @param {function(object, number):*} get The value of a column.
         */
        MarkCurrent(tr, row, get) {
            const t = this;
            if (t.CurSig !== t.ColSig) {
                /** @type {string} The column set CurIdx was found for (MarkCurrent). */
                t.CurSig = t.ColSig;
                /** @type {number} The index of the "IsCurrent" column (the viewer's own row), -1 = none (MarkCurrent). */
                t.CurIdx = (t.Cols || []).findIndex(c => /^is_?current$/i.test(c.Name || "") && (!c.Type || c.Type === "System.Boolean"));
            }
            const v = t.CurIdx >= 0 ? get(row, t.CurIdx) : false;
            const cur = v === true || v === 1 || v === "true" || v === "True";
            tr.classList.toggle("sw-table-current", cur);
            const vi = t.TitleCol();
            for (let i = 1; i < tr.cells.length; ++i) {
                const td = tr.cells[i];
                const want = cur && (vi >= 0 ? i === vi + 1 : i === 1);
                if (want) {
                    const you = _TF("You", "Badge before the name of a data table row that is the viewer's own (ex: your session in Active sessions)");
                    if (td.dataset.swYou !== you)
                        td.dataset.swYou = you;
                    td.title = td.title && td.title.indexOf(t.YouTip()) >= 0 ? td.title : (td.title ? td.title + "\n" : "") + t.YouTip();
                }
                else if (td.dataset.swYou) {
                    delete td.dataset.swYou;
                    if (td.title)
                        td.title = td.title.replace("\n" + t.YouTip(), "").replace(t.YouTip(), "");
                }
            }
        }

        /**
         * The tool tip of the "You" badge (MarkCurrent).
         * @returns {string} The text.
         */
        YouTip() {
            return _TF("This is you: your own session (or account). An action on it applies to you.", "Tool tip of the You badge on a data table row that is the viewer's own (ex: ending this session signs you out)");
        }

        /**
         * The row action buttons of a row as menu items (the same buttons, so their confirmations run): the row actions menu (cards, the
         * row menu button of the grid) and the cell menu (right click, Space / Shift+F10 / Enter on a cell). None after a final
         * (destructive) action on the row (MarkActed). A button that has no box of its own (only in the menu) gets `SwActFrom` = the
         * menu's button or cell: the place of the action for its focus and busy state.
         * @param {HTMLTableRowElement} tr The row.
         * @param {?HTMLElement} [from] The element the menu was opened from (the card's kebab, the row menu button, the cell).
         * @returns {Array<object>} The menu items.
         */
        RowActionItems(tr, from) {
            if (!tr || tr.classList.contains("sw-table-actfinal"))
                return [];
            return Array.from(tr.querySelectorAll("td button.sw-cell-action"))
                .filter(b => !b.hidden && b.style.display !== "none")
                .map(b => ({
                    Text: b.getAttribute("aria-label") || b.textContent.trim(), Title: b.title || null, Disabled: b.disabled,
                    Danger: b.classList.contains("sw-cell-action-danger"),
                    OnClick: () => {
                        //  (a button the row shows only in its menu (cards, the narrow row end): the place of the action is the menu's
                        //  button / cell, `SwActFrom`: the focus comes back there, Sw.Cells shows the busy state there; FOC-1, FBK-2)
                        b.SwActFrom = !b.getClientRects().length && from && from.isConnected ? from : null;
                        b.click();
                    },
                }));
        }

        /**
         * The row actions menu of a card: the kebab in the card heading lists the row's actions (the same buttons, with
         * their confirmations), so the actions are found on touch screens without opening the card.
         * @param {HTMLTableRowElement} tr The row.
         * @param {HTMLElement} at The anchor.
         */
        CardActions(tr, at) {
            const t = this;
            const acts = t.RowActionItems(tr, at);
            const more = [];
            if (!acts.length && tr.classList.contains("sw-table-actfinal") && tr.SwDone)
                more.push({ Text: tr.SwDone.textContent, Title: tr.SwDone.title, Disabled: true });
            if (tr.classList.contains("sw-table-collapsible") && t.E.classList.contains("sw-table-cards"))
                more.push({
                    Text: tr.classList.contains("sw-table-open")
                        ? _TF("Show fewer fields", "Text of the button at the bottom of an opened card of a data table on phones that hides the fields after the first few again")
                        : _TF("Show all fields", "Menu item of the row actions menu of a card of a data table on phones that shows all fields of the row"),
                    OnClick: () => tr.cells[0].SwMore && tr.cells[0].SwMore.click(),
                });
            //  (safe actions first, then the fields, destructive ones last: the focused first item is never Kill)
            const items = safeFirst(acts, more);
            const name = t.RowName(tr);
            Sw.Menu.Show(at, items, {
                Align: "end",
                Label: name ? _T("Actions for {0}", name, "Accessible name of the row actions menu of a card of a data table, {0} names the row") : _TF("Row actions", "Accessible name of the row actions menu of a card of a data table"),
            });
        }

        /**
         * The first time a live refresh tints changed values (`sw-flash`), show the footer legend "Changed" (a swatch in the tint
         * color; the tool tip explains the rule and the refresh interval), so the light tint does not read as a selection or a warning.
         */
        ShowChangedLegend() {
            const t = this;
            const e = t.ChangedE;
            const rate = (t.Data && t.Data.RefreshRate > 0) ? t.Data.RefreshRate : 5000;
            const secs = Math.max(1, Math.round(rate / 1000));
            const title = _T("Tinted values changed at the last automatic refresh (every {0} s); the tint fades after a second", secs, "Tool tip of the legend below a data table that explains the light tint on cells, {0} is the refresh interval in seconds");
            if (e.title !== title)
                e.title = title;
            e.hidden = false;
        }

        /**
         * Cards: long values (`td.sw-table-long`) are clamped to 3 lines by table.css; mark the ones that are really cut
         * (`sw-table-clamped`: an "… more" hint at the end of the last line from `data-more`, `aria-expanded="false"`), a tap on them
         * shows the whole value (`sw-table-unclamp`, until the
         * value changes; ToggleClamp). Keyboard focus also shows the whole value. Measured once per frame; nothing in the grid.
         */
        UpdateClamps() {
            const t = this;
            if (t.ClampFrame)
                return;
            /** @type {number} The pending animation frame of UpdateClamps. */
            t.ClampFrame = requestAnimationFrame(() => {
                t.ClampFrame = 0;
                const cards = t.E.classList.contains("sw-table-cards");
                const moreText = _TF("more", "Hint at the end of a long value in a card of a data table on phones that is cut after 3 lines (after an ellipsis); a tap shows the whole value");
                t.Body.querySelectorAll("td.sw-table-long, td.sw-table-clamped").forEach(td => {
                    let cut;
                    if (!cards || !td.classList.contains("sw-table-long"))
                        cut = false;
                    else if (td.classList.contains("sw-table-unclamp"))
                        cut = true;
                    else
                        cut = td.scrollHeight > td.clientHeight + 2;
                    td.classList.toggle("sw-table-clamped", cut);
                    if (!cut)
                        td.classList.remove("sw-table-unclamp");
                    if (cut) {
                        td.setAttribute("aria-expanded", td.classList.contains("sw-table-unclamp") ? "true" : "false");
                        //  The "... more" hint at the end of the cut value (table.css)
                        td.dataset.more = moreText;
                    }
                    else
                        td.removeAttribute("aria-expanded");
                });
            });
        }

        /**
         * A tap / click on a clamped card value (UpdateClamps) shows the whole value (and does nothing else, ex: no copy of a text
         * value); once shown, taps work as usual (copy, links) and the value stays open until it changes (next page, refresh with a new
         * value). Taps on links and buttons inside the value keep their own action. Runs in the capture phase of the table body.
         * @param {MouseEvent} ev The click event (on the table body).
         */
        ToggleClamp(ev) {
            const td = ev.target.closest ? ev.target.closest("td.sw-table-clamped:not(.sw-table-unclamp)") : null;
            if (!td || ev.target.closest("a,button,input,select,textarea") || Sw.BadClick(ev))
                return;
            td.classList.add("sw-table-unclamp");
            td.setAttribute("aria-expanded", "true");
        }

        /**
         * The item a row is about, for confirmations of destructive cell actions ("Name: msedge, Id: 40544"): the key columns (text
         * columns first, then by primary key position; at most 3 with a value), else the first visible text column with a value.
         * @param {object} row The row data.
         * @param {function(object, number): *} get Value of a column index.
         * @returns {string} The text ("" if nothing names the row).
         */
        ItemText(row, get) {
            const t = this;
            if (t.KeySig !== t.ColSig) {
                t.KeySig = t.ColSig;
                const keys = [];
                t.Cols.forEach((c, i) => {
                    if (c.Props & Props.AnyKey)
                        keys.push({ I: i, Text: c.Type === "System.String" ? 0 : 1, Pos: ((c.Props >> 8) & 7) || 8 });
                });
                keys.sort((a, b) => (a.Text - b.Text) || (a.Pos - b.Pos) || (a.I - b.I));
                if (!keys.length) {
                    const v = t.Vis.find(x => x.Col.Type === "System.String");
                    if (v)
                        keys.push({ I: v.Index });
                }
                t.KeyCols = keys.map(k => k.I);
            }
            const parts = [];
            for (const i of t.KeyCols) {
                const v = get(row, i);
                if (v === null || v === undefined || v === "")
                    continue;
                const c = t.Cols[i];
                parts.push((c.Title || c.Name) + ": " + String(v).substring(0, 80));
                if (parts.length >= 3)
                    break;
            }
            return parts.join(", ");
        }

        /**
         * Compact cards (card layout, wide tables): a card shows its first CardFields values, the sort columns always among them (empty ones are left out anyway; row action
         * buttons, ex: Kill, only in the opened card) and a "Show all n fields (and actions)" button at the bottom (in the row number
         * cell, table.css) shows the rest;
         * the open cards are remembered by row number (OpenRows). Rows with at most CardFields + 1 values show everything. The classes
         * are set in every layout (the grid ignores them).
         * @param {HTMLTableRowElement} tr The row.
         */
        CompactCard(tr) {
            const t = this;
            const cells = tr.cells;
            //  (the columns the server hides by default, C# TableDataHide, are only in the opened card, unless the viewer showed them)
            const show = t.ColPrefs().Show;
            const srv = ci => {
                const v = t.Vis[ci - 1];
                return !!v && srvHidden(v.Col) && !show.includes(v.Col.Name);
            };
            let n = 0;
            let more = false;
            for (let ci = 1; ci < cells.length; ++ci) {
                const td = cells[ci];
                if (td.classList.contains("sw-table-blank") || td.classList.contains(Sw.Cells.Classes.Actions))
                    continue;
                if (srv(ci))
                    more = true;
                else
                    ++n;
            }
            const collapsible = n > CardFields + 1 || more;
            const open = collapsible && t.OpenRows.has(tr.SwNum);
            //  The sort columns are always on the closed card (a list sorted by memory shows the memory), in the place of later fields
            const sorted = new Set();
            (t.Req.Order || []).forEach(o => {
                const name = String(o).replace(/^-/, "");
                const vi = t.Vis.findIndex(v => v.Col.Name === name);
                if (vi >= 0 && cells[vi + 1] && !cells[vi + 1].classList.contains("sw-table-blank"))
                    sorted.add(vi + 1);
            });
            //  The card title (TitleCol: the column that names the row, ex: the user of a session): the first line of the
            //  card, larger, without its label, always on the closed card
            const titleCi = t.TitleCol() + 1;
            //  (a titled card needs no "#n" heading band: the row number shows only on cards without a title, table.css sw-table-titled)
            t.E.classList.toggle("sw-table-titled", titleCi > 0);
            const room = Math.max(1, CardFields - sorted.size - (titleCi > 0 && !sorted.has(titleCi) ? 1 : 0));
            let k = 0;
            let acts = false;
            for (let ci = 1; ci < cells.length; ++ci) {
                const td = cells[ci];
                let extra = false;
                td.classList.toggle("sw-table-cardtitle", ci === titleCi);
                if (collapsible && !td.classList.contains("sw-table-blank")) {
                    //  Row actions (ex: Kill) are only in the opened card, never in the short summary of a scrolling list (a mis-tap there
                    //  started a machine wide action for the wrong row); a Toggle switch is a value and stays where it is. The card
                    //  heading has a row actions menu instead (CardActions)
                    if (td.classList.contains(Sw.Cells.Classes.Actions))
                        extra = acts = td.classList.contains("sw-fmt-Actions") || td.querySelector(".sw-cell-action") !== null;
                    else if (srv(ci))
                        extra = true;
                    else if (ci !== titleCi && !sorted.has(ci))
                        extra = ++k > room;
                }
                td.classList.toggle("sw-table-extra", extra);
            }
            tr.classList.toggle("sw-table-collapsible", collapsible);
            tr.classList.toggle("sw-table-open", open);
            const rn = cells[0];
            //  The row actions menu (a kebab at the right of the card heading; cards only, table.css), when the row has an action
            const hasAct = tr.querySelector("td button.sw-cell-action:not(:disabled)") !== null;
            let ab = rn.SwAct;
            if (hasAct && !ab) {
                /** @type {HTMLButtonElement} The row actions menu button of the card heading (CompactCard, CardActions). */
                ab = rn.SwAct = el("button.sw-table-cardact", { type: "button", tabIndex: -1, attrs: { "aria-haspopup": "menu" } }, lineIcon("kebab"));
                ab.addEventListener("click", ev => {
                    ev.stopPropagation();
                    if (Sw.BadClick(ev))
                        return;
                    t.CardActions(tr, ab);
                });
                rn.append(ab);
            }
            if (ab) {
                ab.hidden = !hasAct;
                const name = t.RowName(tr);
                const label = name
                    ? _T("Actions for {0}", name, "Accessible name of the row actions menu of a card of a data table, {0} names the row")
                    : _TF("Row actions", "Accessible name of the row actions menu of a card of a data table");
                if (ab.getAttribute("aria-label") !== label) {
                    ab.setAttribute("aria-label", label);
                    ab.title = label;
                }
            }
            tr.classList.toggle("sw-table-hasact", hasAct);
            if (hasAct)
                acts = false;
            let b = rn.SwMore;
            if (!collapsible) {
                if (b)
                    b.hidden = true;
                return;
            }
            if (!b) {
                b = rn.SwMore = el("button.sw-table-morebtn", { type: "button", tabIndex: -1 });
                b.addEventListener("click", ev => {
                    ev.stopPropagation();
                    if (Sw.BadClick(ev))
                        return;
                    const num = tr.SwNum;
                    if (t.OpenRows.has(num))
                        t.OpenRows.delete(num);
                    else
                        t.OpenRows.add(num);
                    t.CompactCard(tr);
                    if (!t.OpenRows.has(num))
                        tr.scrollIntoView({ block: "nearest" });
                });
                rn.append(b);
            }
            b.hidden = false;
            b.textContent = open
                ? _TF("Show fewer fields", "Text of the button at the bottom of an opened card of a data table on phones that hides the fields after the first few again")
                : acts
                    ? _T("Show all {0} fields and actions", n, "Text of the button at the bottom of a card of a data table on phones that shows all fields and the action buttons (ex: Kill) of the row (only the first few fields are shown), {0} is the number of fields")
                    : _T("Show all {0} fields", n, "Text of the button at the bottom of a card of a data table on phones that shows all fields of the row (only the first few are shown), {0} is the number of fields");
            b.setAttribute("aria-expanded", open ? "true" : "false");
        }

        /** Measure the row number column width (--sw-table-rnw, the left of the sticky first column in the narrow grid; not in cards). */
        UpdateRnw() {
            const t = this;
            if (t.E.classList.contains("sw-table-cards"))
                return;
            const row = t.Body.rows[0];
            //  (a hidden row number column, ApplyHidden, takes no width)
            const rnw = row && !t.HiddenIdx.has(0) ? row.cells[0].offsetWidth : 0;
            const v = rnw + "px";
            if (t.E.style.getPropertyValue("--sw-table-rnw") !== v)
                t.E.style.setProperty("--sw-table-rnw", v);
            t.UpdatePins(row, rnw);
        }

        /**
         * The pinned identifying columns of the wide grid (not narrow, not cards; table.css sw-table-pin / sw-table-pin2): the first data
         * column, and the second one too when it is the first visible text key column (the name; ex: Process information "Id", "Name"),
         * stay at the left while scrolling sideways, so the row's actions (often the last column) are never shown without the row's name.
         * Nothing is pinned when the pinned columns would take more than a third of the visible width. A last column with the Actions
         * format (row actions, ex: Kill) is pinned at the right edge (sw-table-actpin), so the actions never need sideways scrolling.
         * @param {HTMLTableRowElement} row The first data row (measured).
         * @param {number} rnw The row number column width.
         */
        UpdatePins(row, rnw) {
            const t = this;
            let pin = 0;
            if (row && t.Vis && t.Vis.length > 3 && row.cells.length > 2) {
                pin = 1;
                const keys = t.KeyCols || [];
                const vi = t.Vis.findIndex(v => v.Col.Type === "System.String" && keys.indexOf(v.Index) >= 0);
                if (vi === 1)
                    pin = 2;
                const w1 = row.cells[1].offsetWidth;
                const w = w1 + (pin > 1 ? row.cells[2].offsetWidth : 0);
                if (w > t.Scroll.clientWidth / 3)
                    pin = w1 > t.Scroll.clientWidth / 3 ? 0 : 1;
                t.E.style.setProperty("--sw-table-p2", (rnw + w1) + "px");
            }
            t.E.classList.toggle("sw-table-pin", pin > 0);
            t.E.classList.toggle("sw-table-pin2", pin > 1);
            //  The row actions (ex: Kill) at the right edge: the last column has the Actions format (table.css sw-table-actpin), unless
            //  it would take more than a third of the visible width
            const last = t.Vis && t.Vis.length > 1 ? t.Vis[t.Vis.length - 1].Col : null;
            const isAct = !!last && /^Actions(;|$)/.test(String(last.Format || "")) && !!row;
            //  The actions are a real column with the buttons on every row, quiet at rest (UX guide LST-2: always visible; the earlier
            //  hover-only gutter of 0 px hid them, CMP-B1 / B3). On mouse screens without room for three or more buttons per row
            //  (SpreadWidths, ActMode "menu") the column shows a narrow row menu button instead (sw-table-actmenu); one or two
            //  actions always stay buttons, so a single Kill is one click, not a one item menu
            const E = t.E.classList;
            const mouse = isAct && !E.contains("sw-table-narrow") && !!window.matchMedia && window.matchMedia("(hover: hover)").matches;
            E.toggle("sw-table-actmenu", mouse && t.ActMode === "menu" && t.ActMost() > 2);
            const actW = isAct ? row.cells[row.cells.length - 1].offsetWidth : 0;
            /** @type {number} The width of the pinned row actions column (0 = not pinned; UpdateEdges keeps the right edge cue left of it). */
            t.ActPinW = actW > 0 && actW <= t.Scroll.clientWidth / 3 ? actW : 0;
            E.toggle("sw-table-actpin", t.ActPinW > 0);
        }

        /** Update the footer (paging buttons, range text). */
        UpdateFoot() {
            const t = this;
            const r = t.Req;
            const d = t.Data;
            const n = d.Rows.length;
            t.UpdateProblems();
            //  (disabled pager buttons say why, UX guide INP-12: the reason is the tool tip, reachable by keyboard and touch)
            const firstWhy = _TF("This is the first page", "Reason (tool tip) of the disabled First page / Previous page buttons of a data table");
            const lastWhy = _TF("This is the last page: no more rows follow", "Reason (tool tip) of the disabled Next page button of a data table");
            t.HomeB.SetDisabled(r.Row <= 0, firstWhy);
            t.PrevB.SetDisabled(r.Row <= 0, firstWhy);
            t.NextB.SetDisabled(!(d.RowCount > r.Row + n), lastWhy);
            const fmt = v => Sw.Format.Number(v, 0, " ");
            //  RowCount means "at least this many rows": the server counts at most LookAheadCount (1) rows after the page, so huge
            //  tables are never counted. More rows than shown: the total is unknown, at least RowCount ("1-25 of 26+", "more than 25");
            //  otherwise the page is the end and RowCount the exact total ("1-15 of 15").
            //  only a RowCount of exactly the look ahead after the page is a lower bound; a larger one is a total the server
            //  counted anyway (it holds the whole list), shown as is ("1-100 of 183"). The lower bound says "more follow" instead of the
            //  confusing "of 101+" (one more than shown); the tool tip explains why the total is not counted (never counted ahead).
            const more = d.RowCount > r.Row + n && d.RowCount - (r.Row + n) <= Math.max(1, r.LookAheadCount || 0) && !d.Exact;
            const total = fmt(d.RowCount || 0);
            //  (set only when it changes: the range is a polite live region, a live refresh with the same count says nothing)
            let range;
            if (n <= 0)
                range = _T("0 of {0}", total, "The range text of a data table that shows no rows, {0} is the total number of rows");
            else if (more)
                range = _T("{0}-{1}, more follow", fmt(r.Row + 1), fmt(r.Row + n), "The range of shown rows in a data table when more rows follow on the next pages and the total number of rows is not counted, {0} is the first row number, {1} the last row number");
            else
                range = _T("{0}-{1} of {2}", fmt(r.Row + 1), fmt(r.Row + n), total, "The range of shown rows in a data table, {0} is the first row number, {1} the last row number and {2} the total number of rows");
            //  The row of a final action (Kill, End session) that the server no longer lists stays in place until the next manual
            //  refresh (RenderRows): the range says so, never "0 of 0" next to a visible row (UX guide LST-5)
            //  (several kept rows: each counted with its state, ex: "1-8 of 8 + 2 rows (1 ended, 1 no longer listed)")
            const ks = t.KeptShown > 0 ? (t.KeptStates && t.KeptStates.length ? t.KeptStates : [finalState("")]) : [];
            const groups = new Map();
            ks.forEach(s => groups.set(s, (groups.get(s) || 0) + 1));
            if (ks.length === 1)
                range = n <= 0
                    ? _T("1 row ({0}, shown until you refresh)", ks[0], "The range text of a data table that shows only the row the user just ended while the server lists no rows, {0} is its state (ex: ended, withdrawn, deleted)")
                    : _T("{0} + 1 {1} row", range, ks[0], "The range text of a data table plus the row the user just ended that is shown until the next refresh, {0} is the normal range text (ex: 1-20 of 20), {1} its state (ex: ended, withdrawn, deleted)");
            else if (ks.length > 1 && groups.size === 1)
                range = n <= 0
                    ? _T("{0} rows ({1}, shown until you refresh)", ks.length, ks[0], "The range text of a data table that shows only the rows the user just ended while the server lists no rows, {0} is their number, {1} their state (ex: ended, withdrawn, deleted)")
                    : _T("{0} + {1} {2} rows", range, ks.length, ks[0], "The range text of a data table plus the rows the user just ended that are shown until the next refresh, {0} is the normal range text (ex: 1-20 of 20), {1} their number, {2} their state (ex: ended, withdrawn, deleted)");
            else if (ks.length > 1) {
                const parts = Array.from(groups, ([s, c]) => _T("{0} {1}", c, s, "A number of rows of one state in the range text of a data table, {0} the number, {1} the state (ex: 2 ended, 1 no longer listed)")).join(", ");
                range = n <= 0
                    ? _T("{0} rows ({1}; shown until you refresh)", ks.length, parts, "The range text of a data table that shows only rows the user just ended or that are no longer listed while the server lists no rows, {0} is their number, {1} the numbers per state (ex: 1 ended, 1 no longer listed)")
                    : _T("{0} + {1} rows ({2})", range, ks.length, parts, "The range text of a data table plus rows the user just ended or that are no longer listed, shown until the next refresh, {0} is the normal range text (ex: 1-20 of 20), {1} their number, {2} the numbers per state (ex: 1 ended, 1 no longer listed)");
            }
            if (t.RangeE.textContent !== range)
                t.RangeE.textContent = range;
            //  The counts for the page (tab badges, headings): the bubbling `sw-table-count` event on the table root and the option
            //  OnCount, each time they change (a load, a refresh, the re-read after a final row action), so a badge never goes stale
            //  (detail: Count = the server's row count (a lower bound when More), Shown, First, More, Kept = the row of a final action
            //  shown until the next refresh, not in Count)
            const cnt = { Count: d.RowCount || 0, Shown: n, First: r.Row + 1, More: more, Kept: !!t.KeptShown, KeptCount: t.KeptShown || 0 };
            const csig = cnt.Count + "|" + n + "|" + cnt.First + "|" + more + "|" + cnt.KeptCount;
            if (t.CountSig !== csig) {
                /** @type {string} The counts last told (sw-table-count). */
                t.CountSig = csig;
                t.E.dispatchEvent(new CustomEvent("sw-table-count", { bubbles: true, detail: Object.assign({ Table: t }, cnt) }));
                if (typeof t.O.OnCount === "function")
                    try {
                        t.O.OnCount(cnt, t);
                    }
                    catch (e) {
                        console.error(e);
                    }
            }
            //  "Page 3" between the page buttons (the exact total is only known on the last page, so no "of n" that would appear and
            //  widen the slot there); the tool tip has the range. Local rows (and replies with an exact total) know the number of
            //  pages from the start: "Page 2 of 5" (its width does not change while paging)
            const pageNo = Math.floor(r.Row / Math.max(1, r.MaxRowCount)) + 1;
            const pages = (t.Local || d.Exact) && d.RowCount > 0 ? Math.ceil(d.RowCount / Math.max(1, r.MaxRowCount)) : 0;
            const pageText = n <= 0 ? "" : pages > 1
                ? _T("Page {0} of {1}", fmt(pageNo), fmt(pages), "Text between the page buttons below a data table when the number of pages is known, {0} is the number of the shown page, {1} the number of pages")
                : _T("Page {0}", fmt(pageNo), "Text between the page buttons below a data table, {0} is the number of the shown page");
            if (t.PageE.textContent !== pageText)
                t.PageE.textContent = pageText;
            //  The range and page slots never get narrower (their measured width only grows): paging from "16-30 of 31+"
            //  back to "1-15 of 31+", "Page 10" back to "Page 9" or a refresh never moves anything beside them
            /** @type {function(HTMLElement, string): void} Grow the min-width of a slot to its content (scrollWidth: border box). */
            const grow = (e, key) => {
                const w = Math.ceil(e.scrollWidth);
                if (w > (t[key] || 0)) {
                    t[key] = w;
                    e.style.minWidth = w + "px";
                }
            };
            grow(t.RangeE, "RangeW");
            grow(t.PageE, "PageW");
            t.RangeE.title = more
                ? _T("More than {0} rows. The total is not counted (fast on very large tables): go to the next page to see more.", fmt(r.Row + n), "Tool tip of the range text of a data table when there are more rows than shown and the total number of rows is not counted, {0} is the number of the last shown row")
                : "";
            //  (a disabled Next keeps its reason as the tool tip)
            if (d.RowCount > r.Row + n)
                t.NextB.SetTitle(more
                    ? _TF("More rows", "Tool tip of the next page button of a data table when the total number of rows is not known (more rows follow)")
                    : _TF("Go to the next page", "A tool tip description on a button that if clicked will navigate to the next page"));
        }

        // -------------------------------------------------------------- filters

        /** Rebuild all filter rows from the state (3 rows per set: operations, values, flags). */
        BuildFilters() {
            const t = this;
            const s = t.State;
            const rows = [];
            /** @type {Array<{Ops: HTMLSelectElement[], Values: HTMLInputElement[], Inverts: HTMLButtonElement[], Cases: HTMLButtonElement[]}>} Controls per set. */
            t.Sets = [];
            s.Filters.forEach((set, si) => {
                //  Bind the filters to the visible columns
                t.Vis.forEach((v, vi) => {
                    let f = set[vi];
                    if (!f)
                        f = set[vi] = new Filter(v.Col.Name);
                    f.ColName = v.Col.Name;
                    f.ColumnIndex = v.Index;
                });
                set.length = t.Vis.length;
                const ctl = { Ops: [], Values: [], Inverts: [], Cases: [] };
                t.Sets.push(ctl);
                const rOps = el("tr.sw-table-frow.sw-table-fops");
                const rVal = el("tr.sw-table-frow.sw-table-fvals");
                const rFlags = el("tr.sw-table-frow.sw-table-fflags");
                rOps.append(el("td.sw-table-fact", null, si === 0
                    ? iconButton("IconReset", _TF("Reset all filter values and parameters to their default (no filtering).", "A tool tip description on a button that if clicked will reset all parameters to default"), () => t.ResetFilters()).E
                    : iconButton("IconClose", _TF("Remove this filter set.", "A tool tip description on a button that if clicked will remove a set of filters"), () => t.RemoveSet(si)).E));
                rVal.append(el("td.sw-table-fact", null, iconButton("IconClear", _TF("Clear all filter values.", "A tool tip description on a button that if clicked will remove all filter values"), () => t.ClearSet(si)).E));
                const plus = iconButton("IconPlus", _TF("Add a new set of filters.", "A tool tip description on a button that if clicked will add a new set of filter options"), () => t.AddSet(si));
                plus.SetDisabled(s.Filters.length >= 3, _TF("At most 3 filter sets: remove one to add another", "Reason (tool tip) of the disabled Add filter set button of a data table when it already has 3 sets"));
                rFlags.append(el("td.sw-table-fact", null, plus.E));
                t.Vis.forEach((v, vi) => {
                    const f = set[vi];
                    const p = v.Col.Props;
                    const any = (p & Props.AnyFilters) !== 0;
                    const label = v.Col.Title || v.Col.Name;
                    //  Operation
                    const sel = el("select.sw-table-fop", { disabled: !any, attrs: { autocomplete: "off", "aria-label": _T("Filter operation of {0}", label, "Accessible name of the select with the filter operation of a table column, {0} is the column title") } });
                    for (let oi = 0; oi < Ops.Count; ++oi) {
                        const ok = (p & Ops.Props[oi]) !== 0;
                        sel.append(el("option", { value: String(oi), text: Ops.Names[oi], disabled: !ok, title: ok ? Ops.Descriptions[oi] + "\n" + rawHint : null }));
                    }
                    if (any) {
                        if (f.Op < 0 || f.Op >= Ops.Count || sel.options[f.Op].disabled)
                            f.Op = Ops.DefaultOrders.find(x => !sel.options[x].disabled) ?? Ops.Props.findIndex(x => (p & x) !== 0);
                        sel.value = String(f.Op);
                    }
                    //  Value
                    const input = el("input.sw-table-fval", {
                        type: "text", value: f.Value, disabled: !any, placeholder: any ? Ops.Names[f.Op] : "", title: any ? rawHint : null,
                        attrs: { "aria-label": _T("Filter value of {0}", label, "Accessible name of the input with the filter value of a table column, {0} is the column title"), autocomplete: "off", spellcheck: "false", list: any ? enumList(v.Col.Type) : null },
                    });
                    sel.addEventListener("change", () => {
                        f.Op = +sel.value;
                        input.placeholder = Ops.Names[f.Op];
                        if (f.Value !== "")
                            t.Changed();
                        else
                            t.Save();
                    });
                    /** Apply the typed value (at once on every change, as the original's keyup: one request per changed value). */
                    const commit = () => {
                        if (input.value === f.Value)
                            return;
                        f.Value = input.value;
                        t.ShowFilters();
                        t.Changed();
                    };
                    input.addEventListener("input", commit);
                    input.addEventListener("change", commit);
                    input.addEventListener("keydown", ev => {
                        if (ev.key === "Enter") {
                            ev.preventDefault();
                            commit();
                        }
                        else if (ev.key === "Escape" && input.value) {
                            ev.preventDefault();
                            ev.stopPropagation();
                            input.value = "";
                            commit();
                        }
                        else if (ev.key === "ArrowDown" && !ev.altKey && t.Body.rows.length) {
                            ev.preventDefault();
                            t.FocusCell(1, vi + 1);
                        }
                    });
                    //  Flags
                    const inv = t.FlagButton(_TF("Not", "Short text of the toggle button of a data table column filter that keeps the rows that do NOT match the filter (inverts it)"), any, () => f.Invert, x => f.Invert = x, f,
                        _TF("Not is on: showing the rows that do NOT match this filter.", "Tool tip of the pressed Not toggle button of a data table column filter (the filter is inverted)") + "\n" + _TF("Show the rows that match instead.", "Second line of the tool tip of the pressed Not toggle button of a data table column filter"),
                        _TF("Not is off: showing the rows that match this filter.", "Tool tip of the Not toggle button of a data table column filter when it is not pressed") + "\n" + _TF("Show the rows that do NOT match instead.", "Second line of the tool tip of the Not toggle button of a data table column filter when it is not pressed"));
                    const cs = t.FlagButton(_TF("Match case", "Short text of the toggle button of a data table column text filter that makes it case sensitive (upper and lower case letters must match)"), (p & Props.TextFilter) !== 0, () => f.CaseSensitive, x => f.CaseSensitive = x, f,
                        _TF("Match case is on: upper and lower case letters must match (A does not find a).", "Tool tip of the pressed Match case toggle button of a data table column text filter") + "\n" + _TF("Ignore upper and lower case.", "Second line of the tool tip of the pressed Match case toggle button of a data table column text filter"),
                        _TF("Match case is off: upper and lower case letters are ignored (A finds a).", "Tool tip of the Match case toggle button of a data table column text filter when it is not pressed") + "\n" + _TF("Make upper and lower case letters matter.", "Second line of the tool tip of the Match case toggle button of a data table column text filter when it is not pressed"));
                    ctl.Ops.push(sel);
                    ctl.Values.push(input);
                    ctl.Inverts.push(inv);
                    ctl.Cases.push(cs);
                    //  data-label: the column title of the control in the card layout (table.css)
                    const lab = { "data-label": label };
                    rOps.append(el("td", { attrs: lab }, sel));
                    rVal.append(el("td", { attrs: lab }, input));
                    rFlags.append(el("td", { attrs: lab }, inv, cs));
                });
                rows.push(rOps, rVal, rFlags);
            });
            t.FilterBody.replaceChildren(...rows);
            t.ShowFilters();
        }

        /**
         * A filter flag toggle button ("Not": invert, "Match case": case sensitive; older versions showed the cryptic "!" and "A ≠ a").
         * @param {string} text The text.
         * @param {boolean} enabled The column allows the flag.
         * @param {function():boolean} get Read the flag.
         * @param {function(boolean):void} set Write the flag.
         * @param {Filter} f The filter.
         * @param {string} onTip Tooltip when checked.
         * @param {string} offTip Tooltip when unchecked.
         * @returns {HTMLButtonElement} The button (member SwUpdate refreshes it from the filter).
         */
        FlagButton(text, enabled, get, set, f, onTip, offTip) {
            const b = el("button.sw-table-flag", { type: "button", text, disabled: !enabled });
            b.SwUpdate = () => {
                const on = get();
                b.setAttribute("aria-pressed", on ? "true" : "false");
                b.title = on ? onTip : offTip;
            };
            b.SwUpdate();
            b.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                set(!get());
                b.SwUpdate();
                if (f.Value !== "")
                    this.Changed();
                else
                    this.Save();
            });
            return b;
        }

        /** Show / hide the filter rows and update the filter mode button (State.Expanded). */
        ShowFilters() {
            const t = this;
            const ex = t.State.Expanded;
            Array.from(t.FilterBody.rows).forEach((r, i) => r.hidden = ex === 0 || (ex === 1 && i !== 1));
            t.UpdateRowIndex();
            t.UpdateFilterTabs();
            if (!t.FilterB)
                return;
            const tips = [
                _TF("Show simple filter.", "A tool tip description of a button that when clicked will show a simpler filtering options"),
                _TF("Show advanced filter.", "A tool tip description of a button that when clicked will show more advanced filtering options"),
                _TF("Hide filter.", "A tool tip description of a button that when clicked will hide any filtering options"),
            ];
            t.FilterB.SetIcon(["IconExpand", "IconMore", "IconCollapse"][ex]);
            t.FilterB.SetTitle(tips[ex]);
            t.FilterB.E.setAttribute("aria-expanded", ex ? "true" : "false");
            const active = t.State.Filters.some(set => set.some(f => f.Value !== ""));
            t.FilterB.E.classList.toggle("sw-table-filtered", active);
            t.UpdateCardBar();
        }

        /** Reset button: default operations, no flags, no values, only one set, no search. */
        ResetFilters() {
            const t = this;
            const s = t.State;
            let changed = !!t.Req.SearchText || s.Filters.slice(1).some(set => set.some(f => f.Value !== ""));
            s.Filters = [s.Filters[0]];
            s.FilterRows = 1;
            s.Filters[0].forEach(f => {
                changed = changed || f.Value !== "" || f.Invert || f.CaseSensitive || f.Op !== Ops.Contains;
                f.Value = "";
                f.Invert = false;
                f.CaseSensitive = false;
                f.Op = -1;
            });
            t.Req.SearchText = "";
            t.Search.Set("", true);
            t.BuildFilters();
            if (changed)
                t.Changed();
            else
                t.Save();
        }

        /**
         * Clear the values of a set.
         * @param {number} si Set index.
         */
        ClearSet(si) {
            const set = this.State.Filters[si];
            const changed = set.some(f => f.Value !== "");
            set.forEach(f => f.Value = "");
            this.Sets[si].Values.forEach(i => i.value = "");
            this.ShowFilters();
            if (changed)
                this.Changed();
        }

        /**
         * Remove a set.
         * @param {number} si Set index (> 0).
         */
        RemoveSet(si) {
            const s = this.State;
            const changed = s.Filters[si].some(f => f.Value !== "");
            s.Filters.splice(si, 1);
            s.FilterRows = s.Filters.length;
            this.BuildFilters();
            if (changed)
                this.Changed();
            else
                this.Save();
            this.Fit();
        }

        /**
         * Add a set after a set (max 3) and focus its first value input.
         * @param {number} si Set index.
         */
        AddSet(si) {
            const s = this.State;
            if (s.Filters.length >= 3)
                return;
            s.Filters.splice(si + 1, 0, []);
            s.FilterRows = s.Filters.length;
            s.Expanded = 2;
            this.BuildFilters();
            this.Save();
            this.Fit();
            const i = this.Sets[si + 1].Values.find(x => !x.disabled);
            if (i)
                i.focus();
        }

        /**
         * Filter a column by a value (cell menu): first set, the operation if allowed (else the column default), not inverted.
         * @param {number} vi Visible column index.
         * @param {*} value The raw value.
         * @param {number} op The operation.
         */
        FilterBy(vi, value, op) {
            const t = this;
            const f = t.State.Filters[0][vi];
            const p = t.Vis[vi].Col.Props;
            if ((p & Ops.Props[op]) !== 0)
                f.Op = op;
            f.Value = value === null || value === undefined ? "" : String(value);
            f.Invert = false;
            if (t.State.Expanded === 0)
                t.State.Expanded = 1;
            t.BuildFilters();
            t.Changed();
        }

        /**
         * Focus the filter value input of a column (shows the simple filter if hidden).
         * @param {number} vi Visible column index.
         */
        FocusFilter(vi) {
            const t = this;
            //  Card layout: the filter rows are hidden, the filter dialog has the same fields
            if (t.E.classList.contains("sw-table-cards") || t.CardsOn()) {
                t.FilterSheet();
                const focus = () => {
                    const inputs = document.querySelectorAll(".sw-table-fdialog .sw-table-frow2");
                    const lab = (t.Vis[vi] && (t.Vis[vi].Col.Title || t.Vis[vi].Col.Name)) || "";
                    const row = Array.from(inputs).find(x => x.firstChild.textContent === lab);
                    const i = row && row.querySelector("input");
                    if (i)
                        i.focus();
                };
                requestAnimationFrame(() => requestAnimationFrame(focus));
                return;
            }
            if (t.State.Expanded === 0) {
                t.State.Expanded = 1;
                t.ShowFilters();
                t.Save();
            }
            const i = t.Sets && t.Sets[0].Values[vi];
            if (i && !i.disabled) {
                i.focus();
                i.select();
            }
        }

        /**
         * The enabled filter controls (inputs, selects, buttons) of a filter row, in order.
         * @param {HTMLTableRowElement} row The row.
         * @returns {HTMLElement[]} The controls.
         */
        FilterControls(row) {
            return Array.from(row.querySelectorAll("input,select,button")).filter(x => !x.disabled);
        }

        /**
         * Tab stops of the filter rows. The simple filter (one row of value inputs) is a single tab stop, so the grid is three
         * Tab presses away (filter mode button, filter row, grid): the last focused filter control (else the first enabled value
         * input) has tabindex 0, the others -1; arrows move inside the row (OnFilterKey). The advanced filter keeps every
         * control in the tab order.
         */
        UpdateFilterTabs() {
            const t = this;
            const all = Array.from(t.FilterBody.querySelectorAll("input,select,button"));
            if (t.State.Expanded !== 1) {
                all.forEach(x => x.removeAttribute("tabindex"));
                return;
            }
            const row = Array.from(t.FilterBody.rows).find(r => !r.hidden);
            const ctl = row ? t.FilterControls(row) : [];
            let stop = t.FilterStop;
            if (!stop || ctl.indexOf(stop) < 0)
                stop = ctl.find(x => x.tagName === "INPUT") || ctl[0] || null;
            t.FilterStop = stop;
            all.forEach(x => x.tabIndex = x === stop ? 0 : -1);
        }

        /**
         * A filter control got the focus: it becomes the tab stop of the simple filter row.
         * @param {EventTarget} target The focused element.
         */
        SetFilterStop(target) {
            const t = this;
            if (t.State.Expanded !== 1 || !target || !target.matches || !target.matches("input,select,button"))
                return;
            if (t.FilterStop && t.FilterStop !== target)
                t.FilterStop.tabIndex = -1;
            t.FilterStop = target;
            target.tabIndex = 0;
        }

        /**
         * Keys in the simple filter row: Left / Right move to the previous / next control (in a value input only when the caret is
         * at its start / end), Home / End (on buttons) to the first / last control, Up to the column header. Down (to the grid),
         * Enter and Escape are handled by the value inputs; an Escape in an empty value input goes back to the grid.
         * @param {KeyboardEvent} ev The event.
         */
        OnFilterKey(ev) {
            const t = this;
            const tgt = ev.target;
            if (ev.defaultPrevented || ev.altKey || ev.ctrlKey || ev.metaKey || ev.shiftKey || !tgt.matches("input,select,button"))
                return;
            const isInput = tgt.tagName === "INPUT";
            if (ev.key === "Escape" && isInput && !tgt.value) {
                ev.preventDefault();
                t.FocusGrid();
                return;
            }
            if (t.State.Expanded !== 1)
                return;
            const row = tgt.closest("tr");
            const ctl = t.FilterControls(row);
            const i = ctl.indexOf(tgt);
            if (i < 0)
                return;
            const atStart = !isInput || (tgt.selectionStart === 0 && tgt.selectionEnd === 0);
            const atEnd = !isInput || (tgt.selectionStart === tgt.value.length && tgt.selectionEnd === tgt.value.length);
            let next = null;
            switch (ev.key) {
                case "ArrowLeft":
                    if (atStart && i > 0)
                        next = ctl[i - 1];
                    break;
                case "ArrowRight":
                    if (atEnd && i + 1 < ctl.length)
                        next = ctl[i + 1];
                    break;
                case "Home":
                    if (!isInput)
                        next = ctl[0];
                    break;
                case "End":
                    if (!isInput)
                        next = ctl[ctl.length - 1];
                    break;
                case "ArrowUp":
                    if (tgt.tagName !== "SELECT") {
                        ev.preventDefault();
                        t.FocusCell(0, Math.max(1, tgt.closest("td").cellIndex));
                    }
                    return;
                default:
                    return;
            }
            if (!next)
                return;
            ev.preventDefault();
            next.focus();
            if (next.tagName === "INPUT") {
                const p = ev.key === "ArrowLeft" ? next.value.length : 0;
                next.setSelectionRange(p, p);
            }
        }

        /**
         * Focus the grid: the current cell (the roving tab stop), else the first data cell or the first header cell.
         * @param {boolean} [noScroll] Don't scroll the cell into view.
         */
        FocusGrid(noScroll) {
            const t = this;
            if (t.CardsOn()) {
                t.CardList.focus({ preventScroll: true });
                return;
            }
            const a = t.Active;
            const r = t.CellAt(a.R, a.C) ? a.R : (t.Body.rows.length ? 1 : 0);
            const c = t.CellAt(a.R, a.C) ? a.C : 1;
            if (!noScroll) {
                t.FocusCell(r, c);
                return;
            }
            const cell = t.CellAt(r, r === 0 ? Math.max(1, c) : c);
            if (cell) {
                t.SetActive(cell);
                cell.focus({ preventScroll: true });
            }
        }

        /**
         * Page level keyboard shortcuts (table.html, Options.Page) that work anywhere on the page while the focus is not in a
         * text input or a menu / dialog: "/" the search, "g" the grid, "f" the filter of the current column, "?" the keyboard
         * help. An Escape in the empty search goes back to the grid. The grid keys (OnKey) are unchanged.
         * Several page tables on one page (tabs, master / detail) share the keys (pageKeys: one registration per page, no duplicate
         * warning): a key goes to the table that is shown and was used last (PageKeyTable), never to a table in a hidden tab.
         */
        BindPageKeys() {
            const t = this;
            pageKeys(t);
            t.Offs.push(() => pageKeys(t, true));
            //  (the table the user works in gets the page keys while it is shown)
            const mark = () => PageKeyTables.Last = t;
            t.E.addEventListener("focusin", mark);
            t.E.addEventListener("pointerdown", mark, true);
            t.Search.Input.addEventListener("keydown", ev => {
                if (ev.key === "Escape" && !ev.defaultPrevented && !t.Search.Input.value) {
                    ev.preventDefault();
                    t.FocusGrid();
                }
            });
            //  The page (frame) gets the keyboard focus without a focused element (ex: the app shell's "Skip to content" or a
            //  keyboard navigation into the frame): start in the grid
            //  (not after a click / tap, and without scrolling)
            let pointerAt = 0;
            const onPointer = () => pointerAt = Date.now();
            const onFocus = () => setTimeout(() => {
                const ae = document.activeElement;
                if (t.Data && Date.now() - pointerAt > 500 && (!ae || ae === document.body || ae === document.documentElement) && !Sw.Platform.IsTouch && PageKeyTable() === t && (t.PHTitle || firstStop(t.E)))
                    t.FocusGrid(true);
            }, 0);
            window.addEventListener("pointerdown", onPointer, true);
            window.addEventListener("focus", onFocus);
            t.Offs.push(() => {
                window.removeEventListener("pointerdown", onPointer, true);
                window.removeEventListener("focus", onFocus);
            });
        }

        /**
         * Show the keyboard help (the page shortcuts and the grid keys).
         */
        ShowKeys() {
            const row = (keys, text) => el("tr", null, el("td", null, el("kbd", { text: keys })), el("td", { text }));
            const list = el("table.sw-table-keys", null,
                ...Sw.Keys.List().map(k => row(k.Combo, k.Title)),
                row(_TF("Arrows, Home, End", "Keys of a data table, shown in the keyboard help"), _TF("Move between the cells", "Description of the arrow keys in the keyboard help of a data table")),
                row(_TF("Page Up, Page Down", "Keys of a data table, shown in the keyboard help"), this.ScrollOn ? _TF("One screen up / down", "Description of the page keys in the keyboard help of a data table with infinite scrolling") : _TF("Previous / next page", "Description of the page keys in the keyboard help of a data table")),
                row(_TF("Enter", "Key name shown in the keyboard help"), _TF("Sort by the column (header), open a link or the cell menu", "Description of the Enter key in the keyboard help of a data table")),
                row(_TF("Space, Shift+F10", "Keys of a data table, shown in the keyboard help"), _TF("Open the menu of the cell or column", "Description of the menu keys in the keyboard help of a data table")),
                row(_TF("Alt+Down", "Keys of a data table, shown in the keyboard help"), _TF("Open the column menu (header)", "Description of Alt+Down in the keyboard help of a data table")),
                row(_TF("Ctrl+C", "Keys of a data table, shown in the keyboard help"), _TF("Copy the cell", "Description of Ctrl+C in the keyboard help of a data table")),
                row(_TF("Escape", "Key name shown in the keyboard help"), _TF("Back to the cells (from an empty filter or search)", "Description of Escape in the keyboard help of a data table")));
            Sw.Dialog.Show({ Title: _TF("Keyboard shortcuts", "Title of the dialog that lists the keyboard shortcuts"), Content: list });
        }

        // -------------------------------------------------------------- menus

        /**
         * Show a menu at an element.
         * @param {HTMLElement} at The anchor.
         * @param {Array|Promise<Array>} items The items.
         * @returns {Promise<void>}
         */
        async ShowMenu(at, items) {
            const list = await items;
            if (list && list.length)
                Sw.Menu.Show(at, list, { Label: _TF("Column options", "Accessible name of the menu with the options of a data table column") });
        }

        /**
         * The column menu: sort, filter, copy, chart, local / utc times.
         * @param {number} vi Visible column index.
         * @returns {Promise<Array>} Menu items.
         */
        async ColumnMenu(vi) {
            const t = this;
            const v = t.Vis[vi];
            if (!v)
                return [];
            const c = v.Col;
            const r = t.Req;
            const title = c.Title || c.Name;
            const items = [];
            /** Apply a sort change. */
            const sorted = () => {
                t.UpdateHead();
                t.Changed();
            };
            if (c.Props & Props.CanSort) {
                const f = Sort.Find(r, c.Name);
                const asc = f.Index === 0 && !f.Desc, desc = f.Index === 0 && f.Desc;
                items.push({
                    Text: _TF("Sort ascending", "The text of a menu item on a table column header that if clicked will sort the table in ascending order based of that column"),
                    Icon: "IconAscending", Checked: asc,
                    Title: asc
                        ? _T("Sorted by {0} in ascending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title")
                        : _T("Sort by {0} in ascending order.", title, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in ascending order.{0} is replaced with the column title"),
                    OnClick: () => {
                        if (asc)
                            Sort.Remove(r, c.Name);
                        else
                            Sort.Set(r, c.Name, false);
                        sorted();
                    },
                }, {
                    Text: _TF("Sort descending", "The text of a menu item on a table column header that if clicked will sort the table in descending order based of that column"),
                    Icon: "IconDescending", Checked: desc,
                    Title: desc
                        ? _T("Sorted by {0} in descending order.", title, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                        : _T("Sort by {0} in descending order.", title, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in descending order.{0} is replaced with the column title"),
                    OnClick: () => {
                        if (desc)
                            Sort.Remove(r, c.Name);
                        else
                            Sort.Set(r, c.Name, true);
                        sorted();
                    },
                }, {
                    Text: _TF("Clear sort", "The text of a menu item that if clicked will remove any sorting of a table"),
                    Icon: "IconClearSort",
                    Title: _TF("Remove any sorting", "A tool tip description on a menu item that if clicked will remove any sorting of a table"),
                    Disabled: !Sort.Has(r),
                    OnClick: () => {
                        r.Order = null;
                        sorted();
                    },
                });
            }
            if (c.Props & Props.AnyFilters)
                items.push({
                    Text: _TF("Filter", "The text of a menu item on a table column header that moves the keyboard focus to the filter value of the column"),
                    Icon: "filter", Hint: "F",
                    Title: _T("Type a filter value for {0}", title, "Tool tip of a menu item on a table column header that moves the keyboard focus to the filter value of the column, {0} is the column title"),
                    OnClick: () => t.FocusFilter(vi),
                });
            if (items.length)
                items.push("-");
            /** A copy item. */
            const copy = (text, value, tip) => ({ Text: text, Icon: "IconCopy", Title: tip, OnClick: () => Sw.Clipboard.Copy(value) });
            items.push(copy(_TF("Copy title", "The text of a menu item on a table column header that if clicked will copy the column title to the clipboard"), title,
                _T('Copy the column title "{0}" to the clipboard', title, "The tool tip description of a menu item on a table column header that if clicked will copy the column title to the clipboard.{0} is replaced with the text that will be copied")));
            if (c.Name !== c.Title)
                items.push(copy(_TF("Copy name", "The text of a menu item on a table column header that if clicked will copy the column name (member name) to the clipboard"), c.Name,
                    _T('Copy the column name "{0}" to the clipboard', c.Name, "The tool tip description of a menu item on a table column header that if clicked will copy the column name (member name) to the clipboard.{0} is replaced with the text that will be copied")));
            if (c.Desc)
                items.push(copy(_TF("Copy description", "The text of a menu item on a table column header that if clicked will copy the column description to the clipboard"), c.Desc,
                    _T('Copy the column description "{0}" to the clipboard', c.Desc, "The tool tip description of a menu item on a table column header that if clicked will copy the column description to the clipboard.{0} is replaced with the text that will be copied")));
            items.push(copy(_TF("Copy column type", "The text of a menu item on a table column header that if clicked will copy the C# type name of the data in the column to the clipboard"), c.Type,
                _T('Copy the column type name "{0}" to the clipboard', c.Type, "The tool tip description of a menu item on a table column header that if clicked will copy the C# type name of the data in the column to the clipboard.{0} is replaced with the text that will be copied")));
            if ((c.Props & Props.CanChart) && await CanShowChart())
                items.push({
                    Text: _TF("Show as chart", "The text of a menu item on a table column header that if clicked will show a chart with the values of the column on the value axis"),
                    Icon: "IconChart",
                    Title: _TF("Show this columns as a chart", "The tool tip description of a menu item on a table column header that if clicked will show a chart with the values of the column on the value axis"),
                    OnClick: () => t.ShowChart(c),
                });
            if (c.Type === "System.DateTime" && !c.Format) {
                const utc = t.State.TimeMode === "utc";
                items.push({
                    Text: utc
                        ? _TF("Show local times", "The text of a menu item on a table column header that if clicked will show time stamps as local time instead of UTC")
                        : _TF("Show UTC times", "The text of a menu item on a table column header that if clicked will show time stamps as UTC instead of local time"),
                    Icon: "IconClock",
                    Title: utc
                        ? _TF("Show local time stamps", "The tool tip description of a menu item on a table column header if clicked will show time stamps as local time instead of UTC")
                        : _TF("Show UTC time stamps", "The tool tip description of a menu item on a table column header if clicked will show time stamps as UTC instead of local time"),
                    OnClick: () => {
                        t.State.TimeMode = utc ? null : "utc";
                        t.Changed(true);
                    },
                });
            }
            return items;
        }

        /**
         * Navigate to the chart of a column (primary keys as keys, the column as value, the current request with up to 1000 rows).
         * @param {object} c The column.
         */
        ShowChart(c) {
            const t = this;
            const keys = [];
            t.Cols.forEach(x => {
                const pk = x.Props & Props.IsPrimaryKey7;
                if (pk > 0)
                    keys[(pk >> 8) - 1] = x.Name;
            });
            const p = Object.assign({}, t.Req);
            p.MaxRowCount = 1000;
            delete p.Cc;
            delete p.LookAheadCount;
            delete p.Row;
            let api = t.Url;
            while (api.startsWith("../"))
                api = api.substring(3);
            const q = { TableApi: api, Keys: keys, Values: [c.Name], Options: p, Title: (c.Title || c.Name) + " on " + t.Name };
            //  q is encoded (filter texts may contain & or #; the chart page reads the same value); no other chart options, like the
            //  original table (the chart's own defaults)
            Sw.Page.Navigate(Sw.Url.Root + "chart/chart.html?q=" + encodeURIComponent("../Api/TableChart?" + JSON.stringify(q)), "_self");
        }

        /**
         * Items of the header context menu (long press / right click / keyboard on a header cell).
         * @param {EventTarget} target The event target.
         * @returns {Promise<Array>|null} The items.
         */
        HeadMenu(target) {
            const th = target && target.closest ? target.closest("th") : null;
            return th && th.SwCol !== undefined ? this.ColumnMenu(th.SwCol) : null;
        }

        /**
         * Items of the cell context menu: copy value / row, filter by the value, then the column menu.
         * @param {EventTarget} target The event target.
         * @returns {Promise<Array>|null} The items.
         */
        async CellMenu(target) {
            const t = this;
            const td = target && target.closest ? target.closest("td") : null;
            if (!td || !t.Body.contains(td))
                return null;
            const tr = td.parentElement;
            const vi = td.cellIndex - 1;
            const row = tr.SwRow;
            const items = [];
            const generic = row && Array.isArray(row.Values);
            const rowObj = {};
            if (row)
                t.Cols.forEach((c, i) => rowObj[c.Name] = generic ? row.Values[i] : row[c.Name]);
            const copyRow = {
                Text: _TF("Copy row as JSON", "The text of a menu item on a table cell that copies all values of the row as JSON to the clipboard"), Icon: "IconCopy",
                OnClick: () => Sw.Clipboard.Copy(JSON.stringify(rowObj, null, 4)),
            };
            if (vi < 0)
                return row ? safeFirst(t.RowActionItems(tr, td), [copyRow]) : null;
            const v = t.Vis[vi];
            const raw = rowObj[v.Col.Name];
            const text = cellCopyText(td, raw);
            //  (a row actions cell has no value to copy: its actions and "Copy row")
            if (!td.classList.contains(Sw.Cells.Classes.Actions))
                items.push({
                    Text: _TF("Copy value", "The text of a menu item on a table cell that copies the cell value to the clipboard"), Icon: "IconCopy", Hint: "Ctrl+C",
                    OnClick: () => Sw.Clipboard.Copy(text),
                });
            if (raw !== null && raw !== undefined && typeof raw !== "object" && String(raw) !== text && !td.classList.contains(Sw.Cells.Classes.Actions))
                items.push({
                    Text: _TF("Copy raw value", "The text of a menu item on a table cell that copies the unformatted cell value to the clipboard"), Icon: "IconCopy",
                    OnClick: () => Sw.Clipboard.Copy(raw),
                });
            //  A link cell: "Copy link" (its address; also Alt+click on the link, AltClickLinks), one item per link of the cell (named
            //  when there are several, the clicked one first)
            if (Sw.Clipboard.LinkItem && !td.classList.contains(Sw.Cells.Classes.Actions)) {
                const links = Array.from(td.querySelectorAll("a[href]")).filter(a => Sw.Clipboard.LinkUrl(a));
                const at = target.closest ? target.closest("a[href]") : null;
                if (at && links.indexOf(at) > 0)
                    links.unshift(links.splice(links.indexOf(at), 1)[0]);
                const seen = new Set();
                for (const a of links.slice(0, 4)) {
                    const url = Sw.Clipboard.LinkUrl(a);
                    if (seen.has(url))
                        continue;
                    seen.add(url);
                    const name = links.length > 1 ? (a.getAttribute("aria-label") || a.textContent || "").trim().substring(0, 40) : "";
                    const it = Sw.Clipboard.LinkItem(a, name);
                    if (it)
                        items.push(it);
                }
            }
            if (row)
                items.push(copyRow);
            const p = v.Col.Props;
            if ((p & Props.AnyFilters) && raw !== undefined && typeof raw !== "object") {
                const sv = String(raw ?? "");
                const short = sv.length > 32 ? sv.substring(0, 29) + "..." : sv;
                const fi = [];
                if (p & Props.Filter) {
                    fi.push({ Text: _T('Only "{0}"', short, "Menu item on a table cell that filters the table to rows with this value, {0} is the value"), Icon: "filter", OnClick: () => t.FilterBy(vi, sv, Ops.Equals) });
                    fi.push({ Text: _T('Everything but "{0}"', short, "Menu item on a table cell that filters the table to rows without this value, {0} is the value"), Icon: "filter", OnClick: () => t.FilterBy(vi, sv, Ops.NotEqual) });
                }
                else if (p & Props.TextFilter)
                    fi.push({ Text: _T('Containing "{0}"', short, "Menu item on a table cell that filters the table to rows that contain this text, {0} is the text"), Icon: "filter", OnClick: () => t.FilterBy(vi, sv, Ops.Contains) });
                else if (p & Props.OrderFilter) {
                    fi.push({ Text: _T('At least "{0}"', short, "Menu item on a table cell that filters the table to rows with a value greater or equal to this value, {0} is the value"), Icon: "filter", OnClick: () => t.FilterBy(vi, sv, Ops.GreaterEqual) });
                    fi.push({ Text: _T('At most "{0}"', short, "Menu item on a table cell that filters the table to rows with a value less or equal to this value, {0} is the value"), Icon: "filter", OnClick: () => t.FilterBy(vi, sv, Ops.LessEqual) });
                }
                if (fi.length)
                    items.push("-", ...fi);
            }
            const cm = await t.ColumnMenu(vi);
            if (cm.length)
                items.push("-", { Text: v.Col.Title || v.Col.Name, Icon: "table", Items: cm });
            //  The row's actions (ex: End session, Kill): reachable from any cell by the keyboard (Space, Shift+F10) and by a right click, also
            //  where the row end shows only a menu button; safe ones first, destructive ones last (safeFirst: the focused first item is never Kill)
            return safeFirst(t.RowActionItems(tr, td), items);
        }

        // -------------------------------------------------------------- keyboard grid

        /**
         * A grid cell by position.
         * @param {number} r Row (0 = header, 1.. = data rows).
         * @param {number} c Cell index (clamped).
         * @returns {HTMLElement|null} The cell.
         */
        CellAt(r, c) {
            const row = r === 0 ? this.HeadRow : this.Body.rows[r - 1];
            if (!row || !row.cells.length)
                return null;
            return row.cells[Math.max(0, Math.min(row.cells.length - 1, c))];
        }

        /**
         * Make a cell the roving tab stop (the header corner is a normal button, never the tab stop).
         * @param {HTMLElement} cell The cell.
         */
        SetActive(cell) {
            const t = this;
            if (cell.classList.contains("sw-table-corner"))
                return;
            if (t.ActiveEl && t.ActiveEl !== cell)
                t.ActiveEl.tabIndex = -1;
            t.ActiveEl = cell;
            cell.tabIndex = 0;
            const tr = cell.parentElement;
            //  The row actions of the active row are in the tab order (Tab from a cell goes to its row's Kill / End session, or to the
            //  row menu button; then out of the grid); other rows' actions are reached with the arrows
            const acts = r => r && r.parentElement === t.Body ? Array.from(r.querySelectorAll("td." + Sw.Cells.Classes.Actions + " :is(a,button)")) : [];
            if (t.TabRow && t.TabRow !== tr)
                acts(t.TabRow).forEach(x => x.tabIndex = -1);
            acts(tr).forEach(x => x.tabIndex = 0);
            /** @type {?HTMLTableRowElement} The row whose actions are in the tab order (SetActive). */
            t.TabRow = tr;
            t.Active = { R: tr === t.HeadRow ? 0 : tr.sectionRowIndex + 1, C: cell.cellIndex };
        }

        /**
         * Focus a cell (clamped) and scroll it into view.
         * @param {number} r Row.
         * @param {number} c Cell index.
         * @param {number} [dir] The move direction (-1: left, a hidden cell is passed to the left; else to the right).
         */
        FocusCell(r, c, dir) {
            const t = this;
            r = Math.max(0, Math.min(t.Body.rows.length, r));
            if (r === 0 && c < 1)
                c = 1;
            let cell = t.CellAt(r, c);
            if (!cell)
                return;
            //  Cards leave out empty values and the rest of a closed long card (a hidden cell can't take the focus): use the next shown
            //  cell of the row, else the previous one
            /**
             * True if a cell is not shown in the card layout.
             * @param {HTMLElement} x The cell.
             * @returns {boolean}
             */
            const cards = t.E.classList.contains("sw-table-cards");
            //  The grid skips hidden columns (ApplyHidden) the same way, in the direction of the move first (dir < 0: left)
            const hidden = cards
                ? x => x.classList.contains("sw-table-blank") || (x.classList.contains("sw-table-extra") && !x.parentElement.classList.contains("sw-table-open"))
                : x => t.HiddenIdx.has(x.cellIndex);
            if (hidden(cell)) {
                const row = cell.parentElement;
                const step = dir < 0 ? -1 : 1;
                let i = cell.cellIndex;
                while (i > 0 && i < row.cells.length && hidden(row.cells[i]))
                    i += step;
                if (i <= 0 || i >= row.cells.length) {
                    i = cell.cellIndex;
                    while (i > 0 && i < row.cells.length && hidden(row.cells[i]))
                        i -= step;
                }
                if (i > 0 && i < row.cells.length)
                    cell = row.cells[i];
            }
            t.SetActive(cell);
            cell.focus({ preventScroll: true });
            t.Reveal(cell);
        }

        /**
         * Scroll a cell into view, not hidden below the sticky header row or the sticky left column(s).
         * @param {HTMLElement} cell The cell.
         */
        Reveal(cell) {
            const t = this;
            const sc = t.Scroll;
            const ci = cell.cellIndex;
            const row = cell.parentElement;
            //  (the pinned row actions at the right edge are always in view: only the rows scroll)
            const keepX = t.E.classList.contains("sw-table-actpin") && ci === row.cells.length - 1 ? sc.scrollLeft : -1;
            cell.scrollIntoView({ block: "nearest", inline: "nearest" });
            if (keepX >= 0 && sc.scrollLeft !== keepX)
                sc.scrollLeft = keepX;
            let left = 0;
            if (ci > 0 && row.cells[0])
                left += row.cells[0].offsetWidth;
            const E = t.E.classList;
            const grid = !E.contains("sw-table-cards");
            //  Sticky columns: the first data column in the narrow grid, the pinned columns in the wide grid (UpdatePins)
            if (ci > 1 && grid && (E.contains("sw-table-narrow") || E.contains("sw-table-pin")) && row.cells[1])
                left += row.cells[1].offsetWidth;
            if (ci > 2 && grid && !E.contains("sw-table-narrow") && E.contains("sw-table-pin2") && row.cells[2])
                left += row.cells[2].offsetWidth;
            const cr = cell.getBoundingClientRect();
            const sr = sc.getBoundingClientRect();
            if (ci <= 1)
                sc.scrollLeft = 0;
            else if (cr.left < sr.left + left)
                sc.scrollLeft -= sr.left + left - cr.left;
            if (row !== t.HeadRow && !t.E.classList.contains("sw-table-cards")) {
                const top = t.Head.offsetHeight;
                if (cr.top < sr.top + top && sc.scrollHeight > sc.clientHeight)
                    sc.scrollTop -= sr.top + top - cr.top;
            }
        }

        /**
         * After a redraw: keep the roving tab stop on an existing cell (and the focus in the grid if it was there).
         * @param {boolean} hadFocus The grid had the focus before the redraw.
         */
        RestoreActive(hadFocus) {
            const t = this;
            const a = t.Active;
            const cell = t.CellAt(a.R, a.R === 0 ? Math.max(1, a.C) : a.C) || t.CellAt(1, 1) || t.CellAt(0, 1);
            if (!cell)
                return;
            t.SetActive(cell);
            if (hadFocus && !t.Grid.contains(document.activeElement))
                cell.focus({ preventScroll: true });
        }

        /**
         * Grid keyboard handling.
         * @param {KeyboardEvent} ev The event.
         */
        OnKey(ev) {
            const t = this;
            const tgt = ev.target;
            if (ev.defaultPrevented || t.FilterBody.contains(tgt))
                return;
            const cell = tgt.closest("td,th");
            if (!cell || cell.classList.contains("sw-table-corner"))
                return;
            //  Inside a cell's own controls (links, buttons): left / right move between them, Escape goes back to the cell
            if (tgt !== cell) {
                if (tgt.classList.contains("sw-table-sort"))
                    return;
                const ctl = Array.from(cell.querySelectorAll("a,button")).filter(x => x.getClientRects().length > 0);
                const i = ctl.indexOf(tgt);
                if (ev.key === "Escape") {
                    ev.preventDefault();
                    cell.focus();
                }
                else if ((ev.key === "ArrowRight" || ev.key === "ArrowLeft") && i >= 0 && ctl.length > 1) {
                    ev.preventDefault();
                    ctl[(i + (ev.key === "ArrowRight" ? 1 : ctl.length - 1)) % ctl.length].focus();
                }
                return;
            }
            const a = t.Active;
            const head = cell.parentElement === t.HeadRow;
            const ctrl = ev.ctrlKey || ev.metaKey;
            const rows = t.Body.rows.length;
            const last = cell.parentElement.cells.length - 1;
            switch (ev.key) {
                case "ArrowRight": t.FocusCell(a.R, Math.min(last, a.C + 1)); break;
                case "ArrowLeft": t.FocusCell(a.R, Math.max(head ? 1 : 0, a.C - 1), -1); break;
                case "ArrowDown":
                    if (head && ev.altKey) {
                        t.ShowMenu(cell, t.ColumnMenu(cell.SwCol));
                        break;
                    }
                    t.FocusCell(Math.min(rows, a.R + 1), a.C);
                    break;
                case "ArrowUp": t.FocusCell(Math.max(0, a.R - 1), a.C); break;
                case "Home": t.FocusCell(ctrl ? (rows ? 1 : 0) : a.R, head ? 1 : 0); break;
                case "End": t.FocusCell(ctrl ? rows : a.R, last); break;
                case "PageDown":
                    if (t.ScrollOn)
                        t.FocusCell(Math.min(rows, a.R + (t.ScrollMetrics() || { Screen: 10 }).Screen), a.C);
                    else if (t.NextB.E.disabled)
                        t.FocusCell(rows, a.C);
                    else
                        t.Page(1);
                    break;
                case "PageUp":
                    if (t.ScrollOn)
                        t.FocusCell(Math.max(rows ? 1 : 0, a.R - (t.ScrollMetrics() || { Screen: 10 }).Screen), a.C);
                    else if (t.Req.Row <= 0)
                        t.FocusCell(rows ? 1 : 0, a.C);
                    else
                        t.Page(-1);
                    break;
                case "Enter":
                case " ":
                    if (head) {
                        const c = t.Vis[cell.SwCol].Col;
                        if (ev.key === "Enter" && (c.Props & Props.CanSort))
                            t.SortClick(cell.SwCol, ev.shiftKey);
                        else
                            t.ShowMenu(cell, t.ColumnMenu(cell.SwCol));
                    }
                    else {
                        //  (only the shown controls: the row actions cell shows the buttons or the row menu button, table.css)
                        const ctl = Array.from(cell.querySelectorAll("a,button:not(:disabled)")).filter(x => x.getClientRects().length > 0);
                        if (ctl.length === 1 && ev.key === "Enter")
                            ctl[0].click();
                        else if (ctl.length)
                            ctl[0].focus();
                        else
                            t.ShowMenu(cell, t.CellMenu(cell));
                    }
                    break;
                case "/":
                    t.Search.Focus(true);
                    break;
                case "f":
                case "F":
                    if (ctrl || ev.altKey || a.C < 1)
                        return;
                    t.FocusFilter(a.C - 1);
                    break;
                case "c":
                case "C":
                    //  (a selected text copies as the browser does; the row number and the row actions cell have no value)
                    if (!ctrl || ev.shiftKey || ev.altKey || head || Sw.HasSelectionIn(cell) || cell.classList.contains("sw-table-rn") || cell.classList.contains(Sw.Cells.Classes.Actions))
                        return;
                    {
                        const row = cell.parentElement.SwRow;
                        const v = t.Vis[cell.cellIndex - 1];
                        const raw = row && v ? (Array.isArray(row.Values) ? row.Values[v.Index] : row[v.Col.Name]) : undefined;
                        Sw.Clipboard.Copy(cellCopyText(cell, raw));
                    }
                    break;
                default:
                    return;
            }
            ev.preventDefault();
        }

        // -------------------------------------------------------------- export

        /**
         * Show the export menu ("Current view", "Filtered", "All", each listing the exporters).
         * @param {HTMLElement} [at] The anchor (default: the Download button; the tool bar overflow menu on phones).
         * @returns {Promise<void>}
         */
        async ExportMenu(at) {
            const t = this;
            if (t.ExportB.E.disabled)
                return;
            //  Local rows (option Rows): the rows of the current search, filters and sort as a CSV file, made in the browser
            if (t.Local) {
                await t.FlushPending();
                const name = t.FileName() + " " + FileStamp() + ".csv";
                Sw.Net.DownloadText(name, t.Local.Csv(t.Req));
                //  (a download: the result is not visible in place, so a toast says it, UX guide FBK-5)
                if (Sw.Toast)
                    Sw.Toast(KeepStamp(_T("{0} is ready", name, "Toast after a data table export (download) finished, {0} is the file name")), { Kind: "ok" });
                return;
            }
            let list;
            try {
                //  A search / filter typed just before (applied by leaving the field when Download is clicked) first reloads the rows,
                //  so the counts in the menu and the exported rows are the ones on the screen
                await t.FlushPending();
                list = await GetExporters();
            }
            catch (e) {
                Sw.Fail(e && e.message ? e.message : String(e));
                return;
            }
            const limit = _TF("Limits on number of rows may still apply!", "A warning in tool tips of table export menu items that the server may limit the number of exported rows");
            /** Sub menu of one export mode (each format with a one line description, ExporterDesc). */
            const sub = (text, title, desc, mode) => ({
                Text: text, Title: title, Desc: desc,
                Items: [{ Title: _TF("File format", "Heading of the list of file formats in the export menu of a data table") }].concat(
                    list.map(x => ({ Text: ExporterName(x), Title: x.Title, Desc: ExporterDesc(x), Icon: x.IconClass, OnClick: () => { t.Export(mode, x, desc); } }))),
            });
            const d = t.Data;
            const shown = d && d.Rows ? d.Rows.length : 0;
            //  (infinite scrolling: "Current view" is the rows in view, partly or fully, not all loaded rows)
            const inView = t.ScrollOn ? t.VisibleRows().length : shown;
            //  The number of matching rows, when the server counted them all (the range text "1-40 of 250"; not "540+")
            const total = d && !(d.RowCount - (t.Req.Row + shown) <= Math.max(1, t.Req.LookAheadCount || 0) && d.RowCount > t.Req.Row + shown) ? (d.RowCount || 0) : -1;
            //  Exports of this table that still run: first, each with its Cancel (the same as in the activity list)
            //  (after Cancel, until the server confirmed: "Stopping: …", not offered again)
            const running = (t.Exports || []).map(r => r.Stopping
                ? {
                    Text: _T("Stopping: {0}", r.Title, "Menu item of the export menu of a data table after Cancel, until the server confirmed that the export stopped, {0} is the export (ex: Exporting Log 2026-10-20 15-04 UTC+2.xlsx)"),
                    Desc: r.Waiting(),
                    Icon: "close",
                    Disabled: true,
                }
                : {
                    Text: _T("Cancel: {0}", r.Title, "Menu item of the export menu of a data table while an export runs: stops it, {0} is the export (ex: Exporting Log 2026-10-20 15-04 UTC+2.xlsx)"),
                    Desc: r.Waiting(),
                    Icon: "close",
                    OnClick: () => r.Cancel(),
                });
            if (running.length)
                running.push("-");
            Sw.Menu.Show(at || t.ExportB.E, running.concat([
                { Title: _TF("Download the data as a file", "The heading of the export menu of a data table (the items below it choose which rows to download)") },
                sub(_TF("Current view", "The text of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including just the visible page"),
                    _TF("Export what you currently see", "The tool tip description of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including just the visible page"),
                    _T("The {0} rows you see on this page", inView, "One line description of the export menu item that downloads only the rows of the current page of a data table, {0} is the number of rows"), 0),
                sub(_TF("Filtered", "The text of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including all pages"),
                    _TF("Export all data with the current filters applied.", "The tool tip description of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including all pages") + "\n" + limit,
                    total >= 0
                        ? _T("The {0} rows that match your filters and search, all pages", Sw.Format.Number(total, 0, " "), "One line description of the export menu item that downloads all rows of a data table that pass the filters, {0} is the number of these rows")
                        : _TF("All rows that match your filters and search, all pages", "One line description of the export menu item that downloads all rows of a data table that pass the filters"), 1),
                sub(_TF("All", "The text of a menu item that can contains child items with export options for exporting the data table ignoring any used filters including all pages"),
                    _TF("Export all data.", "The tool tip description of a menu item that can contains child items with export options for exporting the data table ignoring any used filters including all pages") + "\n" + limit,
                    _TF("Every row of the table, filters are ignored", "One line description of the export menu item that downloads all rows of a data table ignoring the filters"), 2),
            ]), { Side: "above", Align: "end", Label: _TF("Export", "Accessible name of the export menu of a data table") });
        }

        /**
         * Export the table as background work (UX guide FBK-4 / FBK-5 / POP-6): the Download button is busy at once (a spinner in its
         * place, `aria-busy`, the tool tip names the export) and the export is one job of the activity list (`Sw.Activity`, in the app
         * shell): "Exporting Log 2026-10-20 15-04 UTC+2.xlsx" with Cancel and "Try again" after a failure; the result is the toast
         * "Log ….xlsx is ready" with the file saved, kept in the list ("Save again"). Without an activity list (a page without the shell)
         * a working dialog shows it, with Cancel and what it waits for. **Cancel follows the shared cancellation model:** it asks the
         * server (`Api/jobs/CancelJob`) and says "Stopping: waiting for the server to confirm" until the server answers; only then the
         * export is "cancelled", or, when the server had finished, "Too late to cancel: … was ready" (the file is offered, not saved by
         * itself); a Cancel the server refused says so and the export goes on. In the app shell the job is the activity list's
         * (`Sw.Activity.StartServer`), and the table's Cancel is the job's Cancel (`job.Cancel()`), never a shortcut around it. The same
         * export (table, rows, format) is never started twice: choosing it again shows the running one. The export menu lists the
         * running exports first, each with Cancel ("Stopping: …" while the server has not confirmed). Every shown file name keeps its
         * time stamp on one line (KeepStamp).
         * @param {number} mode 0 current view (ExportTableData), 1 filtered, 2 all (ExportTableApi).
         * @param {object} x The exporter {Id, Name, IconClass}.
         * @param {string} [text] The rows it exports (the one line description of the mode, ex: "The 35 rows you see on this page").
         * @returns {Promise<boolean>} True when the file (or link) was made, false when it failed or was cancelled.
         */
        async Export(mode, x, text) {
            const t = this;
            const A = Sw.Activity;
            //  The file name is the table name (Options.Filename), like the original table (the server title, or the n / Name given)
            const base = String(t.Name || "").replace(/[\\/:*?"<>|\u0000-\u001f]+/g, " ").replace(/\s+/g, " ").trim() || t.FileName();
            const shownName = base + ExporterExt(x);
            //  (shown names keep the time stamp on one line: "… 2026‑10‑20 20‑01 UTC+2.csv" never breaks inside the time; the saved
            //  file name keeps plain characters)
            const shown = KeepStamp(shownName);
            const title = _T("Exporting {0}", shown, "Name of a data table export while it runs (activity list, busy dialog), {0} is the file name, ex: Log 2026-10-20 15-04 UTC+2.xlsx");
            const readyText = name => KeepStamp(_T("{0} is ready", name, "Toast after a data table export (download) finished, {0} is the file name"));
            //  (the sentence of the activity list for a server job that finished before the Cancel could stop it: one text for the
            //  shell's server job path and the table's own path)
            const tooLateText = name => KeepStamp(_T("{0} was ready before it could stop; it was not saved.", name, "Result of a server job in the activity list when the user cancelled it but the server had already finished.{0} is replaced with the file name"));
            const key = "sw.table.export|" + (t.Url || t.Name || "") + "|" + mode + "|" + x.Id;
            t.Exports = t.Exports || [];
            //  No double start (FBK-2): the same export again shows the running one
            const same = t.Exports.find(r => r.Key === key);
            if (same) {
                same.Show();
                return false;
            }
            const ac = new AbortController();
            const started = Date.now();
            //  (the words of the one cancellation model, Sw.Cancel.Texts, where it exists: the activity list says the same)
            const stopText = Sw.Cancel && Sw.Cancel.Texts && Sw.Cancel.Texts.Stopping ? Sw.Cancel.Texts.Stopping() : _TF("Stopping: waiting for the service to confirm", "What a data table export says after Cancel until the service confirmed that it stopped (activity list, busy dialog, export menu)");
            //  (a Cancel that could not reach the server: kept and sent again when the connection is back; the words of the one model)
            const pendingText = Sw.Cancel && Sw.Cancel.Texts && Sw.Cancel.Texts.Pending ? Sw.Cancel.Texts.Pending() : _TF("Cancel not sent yet: the connection is lost. It is sent when the service can be reached.", "State of running work after the user chose Cancel while the connection to the service was lost (the cancel is sent when it is back)");
            const waitText = () => {
                if (run && run.CancelPending)
                    return pendingText;
                if (run && run.Stopping)
                    return stopText;
                if (run && run.Lost)
                    return _TF("Waiting for the connection: the export goes on when the service can be reached again", "What a data table export waits for while the connection to the service is lost (activity list, busy dialog, export menu); it continues by itself");
                const s = Math.max(0, Math.round((Date.now() - started) / 1000));
                return _T("Waiting for the service to make the file ({0}): {1} s", text || ExporterName(x), s, "What a data table export waits for (busy dialog, export menu), {0} the exported rows (ex: The 35 rows you see on this page), {1} the seconds so far");
            };
            const bg = !!(A && A.Supported && typeof A.Track === "function");
            /** @type {?object} The activity job (bg) or the working dialog closer. */
            let job = null;
            let close = null;
            const run = {
                Key: key, Title: title,
                Waiting: waitText,
                /** @type {boolean} Cancel was asked, the server did not confirm yet ("Stopping: …"). */
                Stopping: false,
                Cancel: () => cancel(),
                Show: () => {
                    if (bg && A.Open)
                        A.Open();
                    else if (Sw.Toast)
                        Sw.Toast(title + " " + waitText(), { Kind: "info" });
                },
            };
            t.Exports.push(run);
            t.UpdateExportBusy();
            //  In the app shell, a file export is a server job of the activity list (Sw.Activity.StartServer, the server's Api/jobs api):
            //  it goes on when the page is left (no leave question), its file is saved by the list and it comes back after a reload.
            //  The table follows the job (busy Download, "Cancel: …" in the export menu = the job's own Cancel, so the list and the table
            //  say the same: "stopping" until the server confirms). A server without the job api, a failed start (offline) or a link
            //  exporter: the export runs from the page below (exportJob, Retry "online").
            if (bg && typeof A.StartServer === "function" && typeof A.On === "function" && ExporterExt(x) && !t.NoServerJobs) {
                let sj = null;
                try {
                    const opt = { Filename: base };
                    //  (the job's texts after the end: the file name as the title, "… is ready" with an unbreakable stamp; an activity
                    //  list that does not know DoneTitle ignores it)
                    const jo = { DoneText: readyText(shownName), DoneTitle: shown, Kind: "export" };
                    sj = mode === 0
                        ? await A.StartServer(Sw.Net.Api("StartExportTableData"), { Data: t.ExportData(), ExportAs: x.Id, Options: opt, Key: key, Title: title }, jo)
                        : await A.StartServer(Sw.Net.Api("StartExportTableApi"), { Api: t.Url, Req: exportReq(t.Req, mode), ExportAs: x.Id, Options: opt, Key: key, Title: title }, jo);
                }
                catch (e) {
                    sj = null;
                    if (e && (e.Status === 404 || e.Status === 405))
                        /** @type {boolean} The server has no export jobs (Export: the page runs the export itself). */
                        t.NoServerJobs = true;
                }
                if (sj && typeof sj === "object" && "State" in sj) {
                    //  One cancel path: the job's Cancel (the activity list's model: "stopping" until the server confirms, then
                    //  "cancelled" or "too late"); an older list without Job.Cancel: its cancel function
                    run.Cancel = () => typeof sj.Cancel === "function" ? sj.Cancel() : (sj.CancelFn ? sj.CancelFn() : null);
                    Object.defineProperty(run, "Stopping", { get: () => !!sj.Stopping || sj.State === "stopping" });
                    run.Waiting = () => run.Stopping ? (sj.Text || stopText) : (sj.Text || waitText());
                    const ok = await new Promise(res => {
                        let off = null;
                        const check = () => {
                            //  (the Download button's tool tip follows the job: "Stopping: …")
                            t.UpdateExportBusy();
                            if (sj.Running || sj.State === "stopping")
                                return;
                            if (off)
                                off();
                            off = () => { };
                            res(sj.State === "done");
                        };
                        off = A.On(e => {
                            if (!e || !e.Job || e.Job === sj)
                                check();
                        });
                        check();
                    });
                    const i = t.Exports.indexOf(run);
                    if (i >= 0)
                        t.Exports.splice(i, 1);
                    t.UpdateExportBusy();
                    return ok;
                }
            }
            /** Save the file of the reply (again: the "Save again" of the activity list keeps the data). */
            let save = null;
            //  An export is a read (FBK-9: reads retry by themselves): a lost connection waits for the connection and sends again
            //  (Sw.Net Retry "online", at most 5 minutes); only a confirmed Cancel or leaving the page stops waiting, never a dropped
            //  connection
            const sendOpt = { Signal: ac.signal, NoNavigate: true, Retry: "online" };
            /** @type {string} How a Cancel ended ("" none, "cancelled", "done" = too late, "failed", "local" = the request was aborted). */
            let cancelled = "";
            /** @type {?Promise<boolean>} The running Cancel (one per export: a second Cancel waits for the same answer). */
            let stopP = null;
            /** The start was answered (a server job runs, or the server has no job api): a Cancel asked before waits for it. */
            let startDone = null;
            const startedP = new Promise(res => startDone = res);
            //  The export runs as a server job (exportJob): leaving the page no longer stops it, so the leave question says where the
            //  file is found (the activity list after a reload) instead of "… stops"
            const ctl = {
                Job: null, Starting: false,
                Started: () => {
                    run.OnServer = true;
                    if (job)
                        job.Leave = _T("{0} keeps running in the service: you find the file in the activity list when you come back.", title, "Text of the question 'Leave this page?' while a data table export runs as a server job (leaving does not stop it), {0} is the export, ex: Exporting Log 2026-10-20 15-04 UTC+2.csv");
                    startDone();
                },
                //  (the server has no job api: a Cancel asked while the start was on its way aborts the old one call export)
                Local: () => {
                    if (run.Stopping && !cancelled) {
                        cancelled = "local";
                        ac.abort();
                    }
                    startDone();
                },
            };
            const normal = text || ExporterName(x);
            const dialogWait = () => _T("Waiting for the service to make the file ({0})", normal, "What a data table export waits for (busy dialog), {0} the exported rows (ex: The 35 rows you see on this page)");
            const lostText = _TF("Waiting for the connection: the export goes on when the service can be reached again", "What a data table export waits for while the connection to the service is lost (activity list, busy dialog, export menu); it continues by itself");
            /** Show the phase in the job / dialog (stopping, lost connection, waiting). */
            const showPhase = () => {
                const s = run.CancelPending ? pendingText : run.Stopping ? stopText : run.Lost ? lostText : null;
                if (job && typeof job.Update === "function")
                    job.Update({ Text: s || normal });
                if (close && typeof close.SetText === "function")
                    close.SetText(s || dialogWait());
                t.UpdateExportBusy();
            };
            /**
             * Ask the server to cancel and act on its answer (exportCancel). A refused / failed CancelJob: the export goes on ("Stopping"
             * ends) and the error is thrown to the caller, who says so (never a silent "cancelled"). A Cancel that could not reach the
             * server (the connection is lost) is the user's decision all the same (Ops R4-1): it is kept ("Cancel not sent yet …"), sent
             * again when the connection is back (CancelJob is safe to repeat), and the file is never saved by itself after it.
             */
            const ask = async () => {
                let st;
                try {
                    st = await exportCancel(ctl);
                }
                catch (e) {
                    if ((Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(e)) || Sw.Net.Online === false) {
                        run.CancelPending = true;
                        showPhase();
                        if (!run.ResendOff && Sw.Events && typeof Sw.Events.On === "function")
                            run.ResendOff = Sw.Events.On("net.online", () => {
                                const off = run.ResendOff;
                                run.ResendOff = null;
                                if (typeof off === "function")
                                    off();
                                if (run.CancelPending)
                                    ask().catch(() => null); /* SwWeb:allow ux-fbk-5 (ask shows its own outcome: Stopping, cancelled, could not cancel) */
                            });
                        return;
                    }
                    run.Stopping = false;
                    stopP = null;
                    showPhase();
                    throw e;
                }
                run.CancelPending = false;
                cancelled = st === "local" ? "local" : st;
                //  Confirmed (or not a server job: the request is aborted, nothing was written): stop waiting at once. "done" (too late),
                //  "failed" and "stopping": the WaitJob loop goes on and ends with the server's real outcome
                if (st === "cancelled" || st === "local")
                    ac.abort();
            };
            /**
             * The user's Cancel (the activity list's job Cancel, the export menu, the working dialog): "Stopping" until the server answers.
             * @returns {Promise<boolean>} Resolves when the export ended: true when it was cancelled, false when it finished anyway (too
             *   late) or failed; rejects when the server refused the Cancel (the export goes on).
             */
            const cancel = () => {
                if (stopP)
                    return stopP;
                run.Stopping = true;
                showPhase();
                stopP = (async () => {
                    //  (a start still on its way: offline nothing reached the server, abort it; else wait for its answer, then ask)
                    if (ctl.Starting && !ctl.Job) {
                        if (Sw.Net.Online === false) {
                            cancelled = "local";
                            ac.abort();
                        }
                        else
                            await Promise.race([startedP, work.catch(() => null)]);
                    }
                    if (!cancelled && ctl.Job)
                        await ask();
                    else if (!cancelled) {
                        //  (no server job: the old one call export or a start that failed; the request is aborted, nothing was written)
                        cancelled = "local";
                        ac.abort();
                    }
                    //  The job / dialog end with the export's real outcome (Track: Done / Cancelled / Fail), then the caller (ex: the
                    //  activity list's Cancel) goes on
                    await work.catch(() => null);
                    return cancelled === "cancelled" || cancelled === "local";
                })();
                return stopP;
            };
            /** The Cancel of the export menu (without an activity job) and of the working dialog: a refusal is a toast. */
            const cancelShown = () => cancel().catch(e => {
                //  (the words of the activity list: "Could not cancel: <the server's reason>", Sw.Cancel.Texts; the reason, not "409 - …")
                const why = e && e.Reason ? e.Reason : (Sw.Net.ErrorText && e && typeof e === "object" ? (Sw.Net.ErrorText(e).Why || "") : "") || (e && e.message ? e.message : String(e || ""));
                const couldNot = Sw.Cancel && Sw.Cancel.Texts && Sw.Cancel.Texts.CouldNot ? Sw.Cancel.Texts.CouldNot(why) : _T("Could not cancel: {0}", why, "State of running work when Cancel failed.{0} is replaced with the reason");
                Sw.Fail(KeepStamp(_T("{0}: {1}", shownName, couldNot, "Error toast when the server did not accept the cancel of a data table export (the export goes on), {0} is the file name, {1} the sentence (ex: Could not cancel: The export can't be cancelled now.)")));
                return false;
            });
            //  One cancel path: with an activity job its Cancel (the list shows "Stopping…" / "Could not cancel: …"), else cancelShown
            run.Cancel = () => job && typeof job.Cancel === "function" ? job.Cancel() : cancelShown();
            const work = (async () => {
                const opt = { Filename: base };
                let file;
                if (mode === 0) {
                    file = await exportJob("ExportTableData", { Data: t.ExportData(), ExportAs: x.Id, Options: opt, Key: key, Title: title }, sendOpt, ctl);
                }
                else
                    file = await exportJob("ExportTableApi", { Api: t.Url, Req: exportReq(t.Req, mode), ExportAs: x.Id, Options: opt, Key: key, Title: title }, sendOpt, ctl);
                if (ac.signal.aborted)
                    throw new DOMException("Aborted", "AbortError");
                if (!file)
                    throw new Error(_TF("The service made no file: the exporter returned no data.", "An error message shown when table data exporter didn't generate any data"));
                //  (a Cancel that came too late: the server had made the file; it is offered, not saved by itself)
                //  (also a Cancel that is still waiting for the connection: the user's decision stands)
                const late = run.Stopping || run.CancelPending || cancelled === "done" || !!(ctl.Last && ctl.Last.CancelRequested);
                run.Stopping = false;
                run.CancelPending = false;
                if (file.Mime && file.Data) {
                    const name = file.Name || shownName;
                    save = () => {
                        const a = el("a", { href: "data:" + file.Mime + ";base64," + file.Data, download: name, style: { display: "none" } });
                        document.body.append(a);
                        a.click();
                        a.remove();
                    };
                    if (late) {
                        //  The words of the activity list for the same state (one sentence for the server job path and this one), the file
                        //  offered with Open / Save; the job ends here with its Open (after a Cancel the list delivers nothing by itself)
                        const r = { Text: tooLateText(name), Open: () => save(), Late: true };
                        if (job && job.Running && typeof job.Done === "function")
                            job.Done({ Text: r.Text, Open: r.Open, DoneTitle: KeepStamp(name) });
                        return r;
                    }
                    save();
                    return { Text: readyText(name), Open: () => save() };
                }
                if (file.Name) {
                    //  A link exporter: the data is kept on the server, the link is the result (copied when the browser allows it)
                    const u = Sw.Url.Root + file.Name;
                    let copied = false;
                    try {
                        if (!late) {
                            await Sw.Clipboard.Copy(u, { Toast: false });
                            copied = true;
                        }
                    }
                    catch {
                    }
                    //  (and opened in a new tab, like the original table)
                    if (!late)
                        window.open(u, "_blank");
                    return {
                        Text: late
                            ? _TF("The link to the exported data was ready before it could stop; it was not copied.", "Result of a link table export that the user cancelled after the server had made it")
                            : copied
                                ? _TF("The link to the exported data is ready (copied to the clipboard)", "Toast after a link table export finished and the link was copied")
                                : _TF("The link to the exported data is ready", "Toast after a link table export finished"),
                        Url: u,
                    };
                }
                throw new Error(_TF("The service made an empty file.", "An error message shown when the exported table data is empty"));
            })();
            if (bg) {
                job = A.Track(work, {
                    //  (DoneTitle: the job is titled by its file once it ended, as the shell's server jobs: one title rule, Sw.Activity)
                    Key: key, Title: title, DoneTitle: shown, Text: normal, EndsWithPage: true, Kind: "export",
                    Cancel: cancel,
                    Retry: () => t.Export(mode, x, text),
                    Result: r => r,
                });
                if (run.OnServer)
                    ctl.Started();
            }
            else
                //  (the dialog says what it waits for; it adds the time so far itself)
                close = Sw.Dialog.Working({
                    Title: title, Icon: x.IconClass, Promise: work.catch(() => null), Cancel: cancelShown,
                    Text: dialogWait(),
                });
            //  While the connection is lost the export waits (Retry "online"): the job / dialog says so, and what it waits for again
            //  when the connection is back (the export menu's line follows run.Waiting)
            const showWait = lost => {
                run.Lost = lost;
                showPhase();
            };
            const netOffs = Sw.Events && typeof Sw.Events.On === "function"
                ? [Sw.Events.On("net.offline", () => showWait(true)), Sw.Events.On("net.online", () => showWait(false))]
                : [];
            if (Sw.Net.Online === false)
                showWait(true);
            let ok = false;
            try {
                const r = await work;
                ok = !r.Late;
                //  The result toast (FBK-5): the shell's activity list shows it for a job; here for the dialog
                if (!bg && Sw.Toast) {
                    const act = r.Url
                        ? { Text: _TF("Open", "Button of the toast after a link table export: opens the link"), OnClick: () => Sw.Page.Navigate(r.Url, "_blank") }
                        : r.Late && r.Open ? { Text: _TF("Save", "Button of the toast after a cancelled data table export whose file was made anyway: saves the file"), OnClick: () => r.Open() } : null;
                    Sw.Toast(r.Text, act ? { Kind: r.Late ? "info" : "ok", Action: act } : { Kind: "ok" });
                }
            }
            catch (e) {
                if (e && e.name === "AbortError") {
                    if (!bg && Sw.Toast)
                        Sw.Toast(KeepStamp(_T("Export cancelled: {0}", shownName, "Toast after the user cancelled a data table export and the server confirmed it, {0} is the file name")), { Kind: "info" });
                }
                else if (!bg)
                    Sw.Fail(KeepStamp(_T("Could not export {0}: {1}", shownName, e && e.message ? e.message : String(e), "Error toast of a failed data table export, {0} is the file name, {1} the reason")));
            }
            finally {
                run.Stopping = false;
                run.CancelPending = false;
                if (typeof run.ResendOff === "function")
                    run.ResendOff();
                run.ResendOff = null;
                netOffs.forEach(f => typeof f === "function" && f());
                if (close)
                    close();
                const i = t.Exports.indexOf(run);
                if (i >= 0)
                    t.Exports.splice(i, 1);
                t.UpdateExportBusy();
            }
            return ok;
        }

        /**
         * The table data of the export "Current view": the shown page, with infinite scrolling the rows in view (partly or fully; all
         * loaded rows if none can be measured). The server reads generic rows ({Values: [...]} in column order, TableData): the rows of a
         * typed table (TypedTableData, one object per row with a member per column) are converted, else they export empty.
         * @returns {object} The table data.
         */
        ExportData() {
            const t = this;
            const d = Object.assign({}, t.Data, { Cols: t.Cols });
            let rows = d.Rows || [];
            if (t.ScrollOn) {
                const vis = t.VisibleRows();
                rows = vis.length ? vis : (t.SRows || []);
            }
            const cols = t.Cols || [];
            d.Rows = rows.map(row => Array.isArray(row && row.Values) ? row : { Values: cols.map(c => row ? row[c.Name] : null) });
            if (t.ScrollOn)
                d.RowCount = d.Rows.length;
            return d;
        }

        /**
         * The busy state of the Download button while exports run (Export): a spinner in the icon's place (the width stays), `aria-busy`,
         * and the tool tip names the running exports ("Exporting Log ….xlsx"); the button still opens the menu (with Cancel per export).
         */
        UpdateExportBusy() {
            const t = this;
            const b = t.ExportB;
            if (!b)
                return;
            const n = (t.Exports || []).length;
            const e = b.E;
            if (b.SwBaseTitle === undefined)
                /** @type {string} The normal tool tip of the Download button (UpdateExportBusy). */
                b.SwBaseTitle = e.title || "";
            const icon = e.querySelector(":scope > svg:not(.sw-spinner)");
            //  The busy state follows the exports, one source (Ops R4-3): the button's own click work (opening the export menu, Sw.Work)
            //  ends with "not busy" and took the state away while the export still ran (after a refused or slow Cancel); while an
            //  export runs the state is put back at once
            if (!b.SwBusyMo && window.MutationObserver) {
                /** @type {?MutationObserver} Keeps `aria-busy` on the Download button while exports run (UpdateExportBusy). */
                b.SwBusyMo = new MutationObserver(() => {
                    if ((t.Exports || []).length > 0 && e.getAttribute("aria-busy") !== "true")
                        e.setAttribute("aria-busy", "true");
                });
                b.SwBusyMo.observe(e, { attributes: true, attributeFilter: ["aria-busy"] });
                t.Offs.push(() => b.SwBusyMo.disconnect());
            }
            if (n > 0) {
                if (!b.SwSpin) {
                    /** @type {?Element} The spinner of the busy Download button (UpdateExportBusy). */
                    b.SwSpin = new Sw.Spinner({ Class: "sw-table-tspin" }).E;
                    if (icon)
                        icon.replaceWith(b.SwSpin);
                    else
                        e.prepend(b.SwSpin);
                    b.SwIcon = icon;
                }
                e.setAttribute("aria-busy", "true");
                e.classList.add("sw-table-exporting");
                b.SetTitle(b.SwBaseTitle + "\n\n" + t.Exports.map(r => r.Stopping ? r.Title + ": " + r.Waiting() : r.Title).join("\n"));
            }
            else if (b.SwSpin) {
                if (b.SwIcon)
                    b.SwSpin.replaceWith(b.SwIcon);
                else
                    b.SwSpin.remove();
                b.SwSpin = null;
                b.SwIcon = null;
                e.removeAttribute("aria-busy");
                e.classList.remove("sw-table-exporting");
                b.SetTitle(b.SwBaseTitle);
            }
        }

        /** Stop refreshing and remove the table. */
        Destroy() {
            this.Dead = true;
            clearTimeout(this.Timer);
            this.Offs.forEach(f => f());
            this.Offs = [];
            this.E.remove();
        }
    }

    Table.Props = Props;
    Table.Ops = Ops;
    Table.Filter = Filter;
    Table.State = State;
    Table.Sort = Sort;
    Table.DefaultRequest = DefaultRequest;
    Table.RepairState = RepairState;
    Table.CanShowChart = CanShowChart;
    Table.GetExporters = GetExporters;
    Table.ExporterDesc = ExporterDesc;
    Table.ExporterName = ExporterName;
    Table.ExporterExt = ExporterExt;
    Table.FileStamp = FileStamp;
    Table.KeepStamp = KeepStamp;
    /**
     * The table an element belongs to (its root `.sw-table`, or an element inside it), ex: from a page script or a test.
     * @param {Element} e The element.
     * @returns {?Table} The table, or null.
     */
    Table.Of = e => {
        const r = e && e.closest ? e.closest(".sw-table") : null;
        return r && r.SwTable ? r.SwTable : null;
    };
    //  (kept for callers: no client side default orders any more, the original table sent no order until the viewer chose one)
    Table.KnownOrders = [];
    Table.EnumValues = EnumValues;
    Table.EnumNames = EnumNames;
    Table.KnownTitles = KnownTitles;
    Table.Health = Health;
    Sw.Table = Table;

    // ------------------------------------------------------------------ legacy global api (old pages, other projects)

    /**
     * LEGACY: the old entry point (ServerManager keys.js / services.js). `rowsPerPage` is ignored (the page size is automatic).
     * @param {string} url Table api url.
     * @param {HTMLElement} [toElement] Container (default body).
     * @param {number} [rowsPerPage] Ignored.
     * @param {*} [tableParams] RequestParams.Param.
     * @param {function():void} [onFirstLoad] After the first response or error.
     * @param {string} [tableName] Name.
     * @param {string} [stateStr] State json.
     * @param {function(object):void} [onFirstData] With the first data.
     * @param {string} [filter] Filter json.
     * @param {*} [keyParam] When truthy, folded into the saved-state key so this value keeps its own filter / sort state (key =
     *   `HashString(url + "\0" + keyParam)`); when absent the key is `HashString(url)` and all params of the endpoint share one state.
     *   Supplied by `tableMain` from the `?uniqueParam=` url param (option KeyParam).
     * @returns {Promise<Table>} The table (has UpdateNow()).
     */
    function addTable(url, toElement, rowsPerPage, tableParams, onFirstLoad, tableName, stateStr, onFirstData, filter, keyParam) {
        //  (HostFit: false: as the original, the table is as high as its rows, also in a host with a fixed height)
        return Table.Create({ Url: url, Element: toElement, Param: tableParams, OnFirstLoad: onFirstLoad, Name: tableName, State: stateStr, OnFirstData: onFirstData, Filter: filter, KeyParam: keyParam || undefined, HostFit: false });
    }

    /** LEGACY global `Table` (the static helpers of the old class). */
    window.Table = {
        addTable,
        haveSort: Sort.Has,
        isCurrentSort: (req, col) => Sort.Find(req, col).Index === 0,
        isReverseSort: req => !!(req.Order && req.Order.length && req.Order[0].charAt(0) === "-"),
        removeSort: Sort.Remove,
        updateSort: Sort.Toggle,
        tableDefaultRequest: DefaultRequest,
        tableDefaultState: () => new State(),
        canShowChart: CanShowChart,
        getTableExportMenu: GetExporters,
    };
    /** LEGACY alias of Sw.Table.Props. */
    window.TableDataProps = Props;
    /** LEGACY alias of Sw.Table.Ops. */
    window.TableDataFilterOps = Ops;
    /** LEGACY alias of Sw.Table.Filter. */
    window.TableDataFilter = Filter;
    /** LEGACY alias of Sw.Table.State. */
    window.TableDataState = State;

    // ------------------------------------------------------------------ explore/table.html

    /**
     * Page entry of explore/table.html (`<body onload="tableMain()">`). Url parameters: `q` table api url (required; resolved with Sw.Url.SiteUrl, the same rule as api.html: `../Api/x` relative to explore/ or `Api/x` relative to the site root;
     * only urls of this site are accepted, another origin shows an error and nothing is fetched), `p` Param (the old `jp`, javascript
     * source run to compute the Param, was removed for security, no producer exists in any SysWeaver project; a link with it
     * shows a message in place of the table and fetches nothing), `scroll` (infinite scrolling instead of pages, unless `scroll=0` or `scroll=false`; option Scroll), `card` (`name@path`, the card template, before the server's TableData.Card; `card=0` or `card=false`: none; option Card; explore/README "Card templates"), `uniqueParam` (opts this table into its own saved filter / sort state: folded into the storage key, option
     * KeyParam; used alone it also supplies the request Param, so only one url param is needed; with `p` it only seeds the key and `p`
     * stays the Param, so a high-cardinality Param can share one stable state keyed by a table-type label), `n` title / name (untrusted: Sw.Page.SetLinkTitle, at most 80 characters, shown as plain text), `r` rows per page (accepted, unused: the page size is automatic), `s` state json, `f` filter json.
     * @returns {Promise<void>}
     */
    window.tableMain = async function () {
        //  Opened on its own (no shell, no host page): the standard page margin (table.css sw-table-alone)
        if (window.top === window.self)
            document.documentElement.classList.add("sw-table-alone");
        //  In a frame of a host without padding around it (the iframe has data-sw-page-padding, core/layout.js; ex: a Fenrir pop-up window):
        //  the page padding of the theme (table.css sw-table-padded)
        else {
            try {
                const fe = window.frameElement;
                if (fe && fe.hasAttribute("data-sw-page-padding"))
                    document.documentElement.classList.add("sw-table-padded");
            }
            catch {
            }
        }
        const remove = Sw.Page.Loading();
        try {
            const U = Sw.Url;
            const url = U.Get("q");
            if (!url) {
                //  Expected user error: explain in the page (no toast / console error)
                document.body.append(el("div.sw-table-error.sw-table-missing", { attrs: { role: "status" } },
                    Sw.Icon("info", "sw-table-error-icon"),
                    el("div.sw-table-error-text", { text: _TF("No table selected. Open a table from the menu, or add the table data API url as the q parameter, example: table.html?q=../Api/debug/ApiTable", "Text shown on the table page when it was opened without a table (the q url parameter is missing)") })));
                remove();
                return;
            }
            //  Security: only table apis of this site (a relative url or the same origin). A crafted link must not make the page fetch
            //  (and show as ours) data from another site; nothing is requested.
            //  One rule with api.html (Sw.Url.SiteUrl): "../Api/x" (relative to explore/) and "Api/x" (relative to the site root) are the same
            const abs = U.SafeSameOrigin(U.SiteUrl(url));
            if (!abs) {
                document.body.append(el("div.sw-table-error", { attrs: { role: "alert" } },
                    Sw.Icon("warning", "sw-table-error-icon"),
                    el("div.sw-table-error-text", { text: _TF("This table cannot be shown: the table address (the q parameter) must be on this site. Open the table from the menu instead.", "Error shown on the table page when the q url parameter points to another web site (not allowed)") })));
                remove();
                return;
            }
            //  `n` is text from the url: plain text, at most 80 characters (Sw.Page.SetLinkTitle; no caption, owner report 3 #4)
            const name = Sw.Page.SetLinkTitle(U.Get("n"));
            //  `uniqueParam` opts this table into its own saved filter / sort state (folded into the storage key). Used alone it also
            //  supplies the request Param; together with `p` it only seeds the key and `p` stays the request Param
            const uniqueParam = U.Get("uniqueParam");
            let p = U.Get("p");
            if (uniqueParam != null && p == null)
                p = uniqueParam;
            //  The empty state texts of the link ("et" title, "ed" sentence: plain text, at most 120 characters) and "header=false" (no
            //  page header: the host page shows its own, ex: an iframe of this page)
            const plain = v => v ? String(v).replace(/[\u0000-\u001f]/g, " ").trim().substring(0, 120) : "";
            const hdr = String(U.Get("header") || "").toLowerCase();
            //  "title=false": the page header without its title (the host shows the name, ex: the title bar of a pop-up window), the live line stays
            const ttl = String(U.Get("title") || "").toLowerCase();
            //  "scroll": infinite scrolling instead of pages, unless "scroll=0" or "scroll=false"
            const sc = U.Get("scroll");
            const scroll = sc !== null && sc !== undefined && !/^(?:0|false)$/i.test(sc);
            //  "card=name@path": the card template (before the server's TableData.Card); "card=0" or "card=false": no card template at all
            const cardArg = U.Get("card");
            const card = !cardArg ? null : /^(?:0|false)$/i.test(cardArg) ? false : cardArg;
            //  Security: "jp" (javascript from the url, run to compute the Param) was removed: a crafted link could run script in
            //  the signed in user's session. Nothing in any SysWeaver project produced it; links pass the Param as "p".
            //  An old jp link must not silently show other rows (the original used the computed Param, the rewrite can't): the page
            //  says so in place of the table and fetches nothing (S5-A-2)
            if (U.Get("jp")) {
                console.warn("explore/table.html: the jp url parameter is no longer supported (removed for security), pass the Param as p");
                document.body.append(el("div.sw-table-error.sw-table-jp", { attrs: { role: "alert" } },
                    Sw.Icon("warning", "sw-table-error-icon"),
                    el("div.sw-table-error-text", { text: _TF("This link uses the removed jp parameter (a script in the link that chose the rows), so the table is not shown: without it the rows could be the wrong ones. Open the table from the menu, or ask for a new link that passes the value as the p parameter.", "Error shown on the table page when its link has the old jp url parameter, which was removed for security") })));
                remove();
                return;
            }
            //  Inside the shell Sw.MenuTitle knows the menu item name without a request; on its own the page is named like the original
            //  table (the server's title, no menu request)
            const inShell = typeof Sw.MenuTitle === "function" && !!Sw.MenuTitle();
            const menuP = Promise.resolve(null);
            await Table.Create({
                //  The link form is kept when it is the same url (the saved table state is keyed by the url)
                Url: abs === U.SafeSameOrigin(url) ? url : abs, Param: p, KeyParam: uniqueParam || undefined, Name: name, State: U.Get("s"), Filter: U.Get("f"), Page: true,
                MenuName: menuP,
                Header: hdr === "false" || hdr === "0" ? false : undefined, HeaderTitle: ttl === "false" || ttl === "0" ? false : undefined, EmptyTitle: plain(U.Get("et")), EmptyText: plain(U.Get("ed")),
                Card: card, Scroll: scroll,
                OnFirstLoad: remove,
                OnFirstData: (data, t) => {
                    //  (in the shell the document title is the menu name, set by the table with its header)
                    if (!name && data.Title && !inShell)
                        document.title = data.Title;
                    //  The page header says what the menu (and the shell's top bar) says: the name of the menu item when there is one;
                    //  without a server title it also names the page on its own (else the name from the url stays)
                    if (!name && t)
                        menuP.then(n => {
                            if (!n || t.ServerTitle || window.top !== window.self)
                                return;
                            document.title = n;
                            t.Name = n;
                            t.Grid.setAttribute("aria-label", n);
                        });
                },
            });
        }
        catch (e) {
            Sw.Fail(e && e.message ? e.message : String(e), true);
            remove();
        }
    };
})();
