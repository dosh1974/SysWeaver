


/**
 * @file table.js - reusable live data table component plus the page script of explore/table.html.
 *
 * Loading: classic `<script>` tag (NOT a module / not injected later) because `Table.CS` reads
 * `document.currentScript.src` when the class is defined. Loaded by explore/table.html
 * (`<body onload="tableMain()">`, after common/common.js and app/application.js) and directly by other pages
 * that call `Table.addTable` (e.g. ServerManager keys.html / services.html). Static field initializers run at
 * load time (translated `TableDataFilterOps.Names` / `Descriptions`, `Table.CS`).
 *
 * Globals defined: insertTH, TableDataProps, TableDataFilterOps, TableDataFilter, TableDataState, Table, tableMain.
 *
 * Dependencies: common/common.js (getUrlParams, getRequest, sendRequest, hashString, delay, AbortHandler,
 * includeJs, includeCss, ColorIcon, ValueFormat, badClick, isPureClick, keyboardClick, Open, GetAbsolutePath,
 * AddLoading, Fail, _T, _TF); app/application.js at runtime for the header / export menus (PopUpMenu,
 * PopUpWorking, WebMenu, WebMenuItem) - the export path includes it on demand.
 *
 * Server contracts: the table API (any `TableData X(TableDataRequest)`) is POSTed a TableDataRequest
 * (`Cc, Row, MaxRowCount, LookAheadCount, Order[], Filters[], Param, SearchText`) and returns TableData
 * (`Cols[] {Name, Title, Desc, Type, Format, Props}`, `Rows[]` (`{Values:[...]}` or typed objects), `RowCount`,
 * `Title`, `Cc`, `RefreshRate`). Also `../Api/GetTableExporters`, `../Api/ExportTableData`,
 * `../Api/ExportTableApi` (SysWeaver.MicroService.HttpServer) and `../Api/TableChartAvailable` /
 * `../chart/chart.html` (SysWeaver.MicroService.ChartJs). All relative urls assume the host page lives one
 * folder below the site root.
 *
 * Storage: `localStorage["SysWeaver.Table." + hashString(url)]` = JSON TableDataState, saved on every change.
 * Styled by table.css (which also styles ValueFormat cell output from common.js).
 */

/**
 * Appends a header cell to a table row: inserts a `td`, replaces it with `<th></th>` via `outerHTML`
 * and returns the new last cell.
 * @param {HTMLTableRowElement} trow - The row to append to.
 * @returns {HTMLTableCellElement} The new `th` element.
 */
function insertTH(trow) {
    const c = trow.insertCell();
    c.outerHTML = "<th></th>"
    return trow.cells[trow.cells.length - 1];
}

/**
 * Bit flags of a column's `Props` (TableData.Cols[i].Props), hand-maintained mirror of the C# enum
 * `TableDataColumnProps` (SysWeaver.Common/Data). Never instantiated; static fields only.
 * The primary key position 1..7 is encoded as a 3-bit number in bits 256/512/1024:
 * `(Props & TableDataProps.IsPrimaryKey7) >> 8` = position (0 = not a primary key).
 */
class TableDataProps {
    /** @type {number} Column can be sorted. */
    static CanSort = 1;
    /** @type {number} Default sort direction is descending. */
    static SortedDesc = 2;
    /** @type {number} Column is hidden (no header, filter or data cell). */
    static Hide = 4;
    /** @type {number} Equality / any-of filters (Equals, NotEqual, AnyOf, NoneOf) allowed. */
    static Filter = 8;
    /** @type {number} Text filters (Contains, StartsWith, EndsWith) and case sensitivity allowed. */
    static TextFilter = 16;
    /** @type {number} Ordering filters (less / greater / range) allowed. */
    static OrderFilter = 32;
    /** @type {number} Column can be shown as a chart value ("Show as chart" header menu item). */
    static CanChart = 64;
    /** @type {number} Column is a key. */
    static IsKey = 128;
    /** @type {number} Primary key position 1 (value 256). */
    static IsPrimaryKey1 = 256;
    /** @type {number} Primary key position 2 (value 512). */
    static IsPrimaryKey2 = 512;
    /** @type {number} Primary key position 3 (value 768). */
    static IsPrimaryKey3 = 256 | 512;
    /** @type {number} Primary key position 4 (value 1024). */
    static IsPrimaryKey4 = 1024;
    /** @type {number} Primary key position 5 (value 1280). */
    static IsPrimaryKey5 = 1024 | 256;
    /** @type {number} Primary key position 6 (value 1536). */
    static IsPrimaryKey6 = 1024 | 512;
    /** @type {number} Primary key position 7 (value 1792); also the mask of the 3 position bits. */
    static IsPrimaryKey7 = 1024 | 512 | 256;

    //  Computed
    /** @type {number} Mask: any filter kind enabled (Filter | TextFilter | OrderFilter). */
    static AnyFilters = TableDataProps.Filter | TableDataProps.TextFilter | TableDataProps.OrderFilter;
    /** @type {number} Mask: any key flag (primary key bits | IsKey). */
    static AnyKey = TableDataProps.IsPrimaryKey7 | TableDataProps.IsKey;
}


/**
 * Filter operation ids, hand-maintained mirror of the C# enum `TableDataFilterOps`, plus per-op UI metadata
 * (parallel arrays indexed by op id). Never instantiated; static fields only.
 */
class TableDataFilterOps {
    /** @type {number} 0: value equals the filter value. */
    static Equals = 0;
    /** @type {number} 1: value does not equal the filter value. */
    static NotEqual = 1;
    /** @type {number} 2: value is less than the filter value. */
    static LessThan = 2;
    /** @type {number} 3: value is greater than the filter value. */
    static GreaterThan = 3;
    /** @type {number} 4: value is less than or equal to the filter value. */
    static LessEqual = 4;
    /** @type {number} 5: value is greater than or equal to the filter value. */
    static GreaterEqual = 5;
    /** @type {number} 6: value (as text) contains the filter value. */
    static Contains = 6;
    /** @type {number} 7: value (as text) starts with the filter value. */
    static StartsWith = 7;
    /** @type {number} 8: value (as text) ends with the filter value. */
    static EndsWith = 8;
    /** @type {number} 9: value is one of the comma separated filter values. */
    static AnyOf = 9;
    /** @type {number} 10: value is none of the comma separated filter values. */
    static NoneOf = 10;
    /** @type {number} 11: min <= value < max of the comma separated filter values. */
    static InRange = 11;
    /** @type {number} 12: value < min or value > max of the comma separated filter values. */
    static OutsideRange = 12;
    /** @type {number} Number of operations (13). */
    static Count = 13;


    /**
     * Preference order used by `Table.addFilterRow` to pick a column's default op when the stored op is
     * unset (-1) or not allowed by the column props: the first allowed of Contains, Equals, GreaterThan.
     * @type {number[]}
     */
    static DefaultOrders = [
        TableDataFilterOps.Contains,
        TableDataFilterOps.Equals,
        TableDataFilterOps.GreaterThan,
    ];


    /**
     * Translated short op names (indexed by op id); used as `<option>` text and as the value input placeholder.
     * @type {string[]}
     */
    static Names = [
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
    ];

    /**
     * The TableDataProps flag a column must have to enable each op (indexed by op id): Filter for
     * Equals/NotEqual/AnyOf/NoneOf, OrderFilter for the comparisons and ranges, TextFilter for
     * Contains/StartsWith/EndsWith.
     * @type {number[]}
     */
    static Props = [
        TableDataProps.Filter,
        TableDataProps.Filter,
        TableDataProps.OrderFilter,
        TableDataProps.OrderFilter,
        TableDataProps.OrderFilter,
        TableDataProps.OrderFilter,
        TableDataProps.TextFilter,
        TableDataProps.TextFilter,
        TableDataProps.TextFilter,
        TableDataProps.Filter,
        TableDataProps.Filter,
        TableDataProps.OrderFilter,
        TableDataProps.OrderFilter,
    ];


    /**
     * Translated op descriptions (indexed by op id); used as `<option>` tooltips.
     * @type {string[]}
     */
    static Descriptions = [
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
    ];



}

/**
 * One filter for one visible column; serialized as the C# `TableDataFilter` in `TableDataRequest.Filters`
 * (only filters with a non-empty `Value` are sent). Created by `Table.addFilterRow`
 * (`new TableDataFilter(col.Name)`) or restored as plain objects from the stored TableDataState.
 */
class TableDataFilter {

    /**
     * Creates a filter for a column. Class field initializers run before the constructor body, so the
     * `ColName = ""` initializer below does not override `colName`.
     * @param {string} colName - Column name (TableData.Cols[i].Name).
     */
    constructor(colName) {
        this.ColName = colName;
    }

    /** @type {boolean} Invert the match (keep rows that do NOT match). Toggled by the "!" check. */
    Invert = false;
    /** @type {boolean} Case-sensitive text matching. Toggled by the "A ≠ a" check (TextFilter columns only). */
    CaseSensitive = false;
    /** @type {number} TableDataFilterOps id; -1 = not chosen yet (a default is picked by addFilterRow). */
    Op = -1;
    /** @type {string} Filter value text; empty filters are not sent. */
    Value = "";
    /** @type {string} Column name. */
    ColName = "";
    /** @type {number} Index into TableData.Cols (including hidden columns). */
    ColumnIndex = 0;
}


/**
 * Persisted per-table UI state (mirror of the C# `TableDataState`), stored as JSON in
 * `localStorage["SysWeaver.Table." + hashString(url)]` or passed in as `stateStr`.
 * Created by `Table.tableDefaultState()`; loaded objects are plain JSON objects repaired by `Table.addTable`.
 * Note: its `RequestParams` initializer calls `Table.tableDefaultRequest()`, which is fine because instances
 * are only created after the `Table` class has been defined.
 */
class TableDataState {

    /** @type {number} Number of filter sets (1..3); each set uses 3 filter rows in the table. */
    FilterRows = 1;
    /** @type {TableDataFilter[][]} One array per filter set, one filter per visible column. */
    Filters = [];
    /** @type {number} Filter UI mode: 0 hidden, 1 simple (first value row only), 2 advanced (all filter rows). */
    Expanded = 1;
    /** @type {object} The TableDataRequest sent to the table API (also carries Param, Filters, SearchText). */
    RequestParams = Table.tableDefaultRequest();
    /** @type {boolean} Adjust RequestParams.MaxRowCount to the available height (on the first page). */
    AutoRowCount = true;
    /** @type {string|null} Display mode of unformatted System.DateTime columns: null (local) or "utc". */
    TimeMode = null;
}



/**
 * All-static live data table component. A table is created with `Table.addTable(...)`; its per-table state
 * lives in closures inside `addTable` and in DOM expando properties on the `<table>` (`Info`, `Cols`,
 * `HomeIcon`, `PrevIcon`, `NextIcon`, `ExportIcon`, `ClearSearch`), on header cells (`Icon`, `Text`, `Value`,
 * `Expanded`), rows (`DataRow`) and table-check elements (`Update`). Never instantiated; there is no
 * teardown API (the window resize listener and the refresh timer live as long as the page).
 *
 * Table row layout: row 0 = header (cell 0 = filter mode icon), rows 1..FilterRows*3 = filter rows
 * (3 per filter set: op selects, value inputs, invert / case checks), then the data rows.
 */
class Table {



    /**
     * Applies `state.Expanded` to the filter mode icon (`cell.Icon` of header cell 0) and the filter rows
     * 1..FilterRows*3: 0 -> icon `IconExpand`, all filter rows get `HideRow`; 1 -> `IconMore`, only row 2 (the
     * value row of the first set) visible; 2 (default) -> `IconCollapse`, all filter rows visible.
     * Does nothing when the header cell's expando `Expanded` already equals the mode.
     * @param {HTMLTableElement} table - The data table.
     * @param {TableDataState} state - The table state.
     * @returns {void}
     */
    static tableUpdateFilterIcon(table, state) {
        const rows = table.rows;
        const cell = rows[0].cells[0];
        const expanded = state.Expanded;
        if (cell.Expanded == expanded)
            return;
        cell.Expanded = expanded;
        const rowEnd = state.FilterRows * 3 + 1;
        const icon = cell.Icon;
        switch (expanded) {
            case 0:
                icon.ChangeImage("IconExpand");
                icon.SetTitle(_TF("Click to show simple filter.", "A tool tip description of a button that when clicked will show a simpler filtering options"));
                for (let ri = 1; ri < rowEnd; ++ri)
                    rows[ri].classList.add("HideRow");
                break;
            case 1:
                icon.ChangeImage("IconMore");
                icon.SetTitle(_TF("Click to show advanced filter.", "A tool tip description of a button that when clicked will show more advanced filtering options"));
                for (let ri = 1; ri < rowEnd; ++ri) {
                    if (ri == 2)
                        rows[ri].classList.remove("HideRow");
                    else
                        rows[ri].classList.add("HideRow");
                }
                break;
            default:
                icon.ChangeImage("IconCollapse");
                icon.SetTitle(_TF("Click to hide filter.", "A tool tip description of a button that when clicked will hide any filtering options"));
                for (let ri = 1; ri < rowEnd; ++ri)
                    rows[ri].classList.remove("HideRow");
                break;
        }
    }

    /**
     * Tells whether any sort order is set.
     * @param {object} requestParams - The TableDataRequest (`Order` = array of column names, "-" prefix = descending).
     * @returns {boolean} True when `requestParams.Order` is a non-empty array.
     */
    static haveSort(requestParams) {
        const o = requestParams.Order;
        if (!o)
            return false;
        return o.length > 0;
    }


    /**
     * Tells whether a column is the primary (first) sort key, in either direction.
     * @param {object} requestParams - The TableDataRequest.
     * @param {string} colName - Column name.
     * @returns {boolean} True when `Order[0]` is `colName` or `"-" + colName`.
     */
    static isCurrentSort(requestParams, colName) {
        const o = requestParams.Order;
        if (!o)
            return false;
        const ol = o.length;
        if (ol < 1)
            return false;
        if (o[0] === colName)
            return true;
        return o[0] === ("-" + colName);
    }

    /**
     * Tells whether the primary sort key is descending.
     * @param {object} requestParams - The TableDataRequest.
     * @returns {boolean} True when `Order[0]` starts with "-".
     */
    static isReverseSort(requestParams) {
        const o = requestParams.Order;
        if (!o)
            return false;
        if (o.length <= 0)
            return false;
        return o[0].charAt(0) === '-';
    }

    /**
     * Removes a column (`colName` or `"-" + colName`, first match only) from the sort order; sets
     * `requestParams.Order = null` when it is or becomes empty. Does not reset `Row` (callers do).
     * @param {object} requestParams - The TableDataRequest (modified in place).
     * @param {string} colName - Column name.
     * @returns {void}
     */
    static removeSort(requestParams, colName) {
        const o = requestParams.Order;
        if (!o)
            return;
        let ol = o.length;
        if (ol < 1) {
            requestParams.Order = null;
            return;
        }
        let i = o.indexOf(colName);
        if (i >= 0) {
            o.splice(i, 1);
            if (o.length < 1)
                requestParams.Order = null;
            return;
        }
        i = o.indexOf("-" + colName);
        if (i >= 0) {
            o.splice(i, 1);
            if (o.length < 1)
                requestParams.Order = null;
        }
    }

    /**
     * Applies a sort click on a column: when `Order` is missing/empty it becomes `[col]` (descending when
     * `defaultDesc`); when the column is already first its direction is toggled (`defaultDesc` ignored);
     * otherwise the column is inserted first (descending when `defaultDesc`), other occurrences of it are
     * removed and `Order` is truncated to 3 entries. Does not reset `Row` (callers do).
     * @param {object} requestParams - The TableDataRequest (modified in place).
     * @param {string} colName - Column name.
     * @param {boolean} defaultDesc - Direction to use when the column becomes the primary sort.
     * @returns {void}
     */
    static updateSort(requestParams, colName, defaultDesc) {
        const o = requestParams.Order;
        const ncol = "-" + colName;
        const decol = defaultDesc ? ncol : colName; 
        if (!o) {
            requestParams.Order = [decol];
            return;
        }
        let ol = o.length;
        if (ol < 1) {
            requestParams.Order = [decol];
            return;
        }
        if (o[0] === colName) {
            o[0] = ncol;
            return;
        }
        if (o[0] === ncol) {
            o[0] = colName;
            return;
        }
        o.splice(0, 0, decol);
        ++ol;
        while (ol > 1) {
            --ol;
            const t = o[ol];
            if (t === colName)
                o.splice(ol, 1);
            if (t === ncol)
                o.splice(ol, 1);
        }
        ol = o.length;
        if (ol > 3)
            o.splice(3, ol - 3);
    }

    /**
     * Updates the header cells (from cell 1, one per non-hidden column) for the current sort state:
     * - sortable columns: class `CurrentSort` (primary sort) or `Sort`, the cell icon becomes
     *   `IconAscending` / `IconDescending` (class `IconButton`), tooltip "Sorted by ..." / "Click to sort ...",
     *   icon click = `onClickFn` (re-sort);
     * - non-sortable columns: class `NoSort`, icon removed and collapsed (`maxWidth = 0`);
     * - the title span (`cell.Text`) is set from `col.Title`; the cell tooltip is the description plus
     *   "Click to show options";
     * - cell `onclick` / `oncontextmenu` open the column menu (`contextMenu`).
     * Also collects the primary key column names (ordered by IsPrimaryKeyN position) used by the chart link,
     * and a `keys` list of the columns flagged `TableDataProps.IsKey` (it is not used).
     * @param {Array<{Name:string, Title:string, Desc:string, Type:string, Format:string, Props:number}>} cols - TableData.Cols (incl. hidden columns).
     * @param {HTMLTableElement} table - The data table (header must already be built by tableRedraw).
     * @param {TableDataState} state - The table state (RequestParams.Order / Row are changed by clicks).
     * @param {function():void} onChangeFn - Save + refetch callback (from addTable).
     * @param {function():void} saveFn - Save-only callback (from addTable).
     * @returns {void}
     */
    static tableUpdateHeader(cols, table, state, onChangeFn, saveFn) {
        const requestParams = state.RequestParams;
        let sourceIndex = -1;
        let cellIndex = 1;
        const cells = table.rows[0].cells;
        /** @type {string[]} Primary key column names; index = IsPrimaryKeyN position - 1 (sent as chart Keys). */
        const primaryKey = [];
        /** @type {Array<string[]>} [Name, Title] pairs of IsKey columns; unused. */
        const keys = [];
        cols.forEach(col => {
            const primIndex = col.Props & TableDataProps.IsPrimaryKey7;
            if (primIndex > 0)
                primaryKey[(primIndex >> 8) - 1] = col.Name;
            if ((col.Props & TableDataProps.IsKey) !== 0)
                keys.push([col.Name, col.Title]);
        });

        cols.forEach(col => {
            ++sourceIndex;
            const dataCol = sourceIndex;
            if ((col.Props & TableDataProps.Hide) === 0) {
                const cell = cells[cellIndex];
                const icon = cell.Icon;
                const i = icon.Element;
                const s = cell.Text;
                /**
                 * Sort icon click handler (set below in two variants, null for non-sortable columns).
                 * Ignores `badClick`; calls `Table.updateSort` (current sort: toggles direction; other column:
                 * becomes primary sort in its `SortedDesc` default direction), logs the new order, sets
                 * `Row = 0`, re-runs `tableUpdateHeader` and calls `onChangeFn()` (save + refetch).
                 * @type {function(MouseEvent):void|null}
                 */
                let onClickFn = null;
                let title = "";
                const colTitle = col.Title;
                const canSort = (col.Props & TableDataProps.CanSort) !== 0;
                if (canSort) {
                    let rev = (col.Props & TableDataProps.SortedDesc) !== 0;
                    if (Table.isCurrentSort(requestParams, col.Name)) {
                        const defRev = rev;
                        rev ^= Table.isReverseSort(requestParams);
                        cell.classList.add("CurrentSort");
                        cell.classList.remove("Sort");
                        cell.classList.remove("NoSort");


                        title = rev
                            ?
                            _T("Sorted by {0} in descending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                            :
                            _T("Sorted by {0} in ascending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title")
                            ;

                        title += "\n\n";

                        title += rev 
                            ?
                            _T("Click to sort by {0} in ascending order.", colTitle, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in ascending order.{0} is replaced with the column title")
                            :
                            _T("Click to sort by {0} in descending order.", colTitle, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in descending order.{0} is replaced with the column title")
                            ;
                        onClickFn = ev => {
                            if (badClick(ev))
                                return;
                            Table.updateSort(requestParams, col.Name, defRev);
                            console.log("New sort order: " + JSON.stringify(requestParams.Order));
                            requestParams.Row = 0;
                            Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
                            onChangeFn();
                        };
                    } else {
                        cell.classList.remove("CurrentSort");
                        cell.classList.add("Sort");
                        cell.classList.remove("NoSort");
                        title = rev
                            ?
                            _T("Sorted by {0} in descending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                            :
                            _T("Sorted by {0} in ascending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title")
                            ;

                        onClickFn = ev => {
                            if (badClick(ev))
                                return;
                            Table.updateSort(requestParams, col.Name, rev);
                            console.log("New sort order: " + JSON.stringify(requestParams.Order));
                            requestParams.Row = 0;
                            Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
                            onChangeFn();
                        };
                    }
                    icon.ChangeImage(rev ? "IconDescending" : "IconAscending");
                    i.classList.add("IconButton");
                    i.style.maxWidth = null;

                } else {
                    cell.classList.remove("CurrentSort");
                    cell.classList.remove("Sort");
                    cell.classList.add("NoSort");

                    i.style.maxWidth = "0";
                    i.classList.remove("IconButton");
                    icon.ChangeImage(null);

                }
                title = ValueFormat.joinNonEmpty("\n\n", col.Desc, title);
                ValueFormat.updateText(s, col.Title, null, null);

                cell.title = ValueFormat.joinNonEmpty("\n\n", col.Desc, _TF("Click to show options", "A tool tip description on a button that if clicked will show some options"));

                /**
                 * Opens the column options menu: awaits `Table.canShowChart()`, then `PopUpMenu` anchored at the
                 * column icon with a `WebMenu` named "TableHeader" (the popup also gets class `TableHeader`):
                 * - sortable columns: "Sort ascending" / "Sort descending" (flag checked when active; clicking an
                 *   active one removes the sort via removeSort), "Clear sort" (read-only when unsorted); each
                 *   resets Row, re-runs tableUpdateHeader, calls onChangeFn and closes the menu;
                 * - "Copy title", "Copy name" (when different from the title), "Copy description" (when any),
                 *   "Copy column type" (C# type name);
                 * - "Show as chart" (`IconChart`) when charts are available and the column has `CanChart`: navigates
                 *   (`_self`) to `../chart/chart.html?q=../Api/TableChart?{TableApi (url without leading "../"),
                 *   Keys: primaryKey, Values: [col.Name], Options: request params with MaxRowCount 1000 and without
                 *   Cc/LookAheadCount/Row, Title: "<col> on <table name>"}`;
                 * - unformatted `System.DateTime` columns: "Show local times" / "Show UTC times" (sets
                 *   `state.TimeMode` to null / "utc", saveFn, onChangeFn).
                 * Menu item flags: 2 = checked, 1 = read-only.
                 * @param {Event} ev - The click / contextmenu event (unused).
                 * @returns {Promise<void>}
                 */
                const contextMenu = async ev => {
                    const checked = 2;
                    const readonly = 1;
                    const checkedReadonly = checked | readonly;
                    const canShowChart = await Table.canShowChart();
                    PopUpMenu(icon.Element, (close, backEl) => {

                        /**
                         * Copies a text to the clipboard (with user feedback) and closes the menu.
                         * @param {string} text - Text to copy.
                         * @returns {Promise<void>}
                         */
                        async function Copy(text) {
                            await ValueFormat.copyToClipboardInfo(text);
                            close();
                        }

                        const pp = backEl.parentElement;
                        pp.classList.add("TableHeader");
                        const menu = new WebMenu();
                        menu.Name = "TableHeader";
                        // Sort
                        if (canSort) {
                            const isCurrent = Table.isCurrentSort(requestParams, col.Name);
                            const isRev = Table.isReverseSort(requestParams);
                            const isSelAsc = (isCurrent && (!isRev));


                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Sort ascending", "The text of a menu item on a table column header that if clicked will sort the table in ascending order based of that column"),
                                IconClass: "IconAscending",
                                Title: isSelAsc
                                    ?
                                    _T("Sorted by {0} in ascending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in ascending order.{0} is replaced with the column title")
                                    :
                                    _T("Click to sort by {0} in ascending order.", colTitle, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in ascending order.{0} is replaced with the column title")
                                ,
                                Flags: isSelAsc ? checked : 0,
                                Data: () => {

                                    if (isSelAsc)
                                        Table.removeSort(requestParams, col.Name);
                                    else
                                        Table.updateSort(requestParams, col.Name, false);
                                    console.log("New sort order: " + JSON.stringify(requestParams.Order));
                                    requestParams.Row = 0;
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
                                    onChangeFn();
                                    close();
                                }
                            }));

                            const isSelDesc = (isCurrent && (isRev));
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Sort descending", "The text of a menu item on a table column header that if clicked will sort the table in descending order based of that column"),
                                IconClass: "IconDescending",
                                Title: isSelDesc
                                    ?
                                    _T("Sorted by {0} in descending order.", colTitle, "A tool tip description on a table column header that explains that a table is currently sorted by that column in descending order.{0} is replaced with the column title")
                                    :
                                    _T("Click to sort by {0} in descending order.", colTitle, "A tool tip description on a table column header that explains that if clicked the table will be sorted by that column in descending order.{0} is replaced with the column title")
                                ,
                                Flags: isSelDesc ? checked : 0,
                                Data: () => {
                                    if (isSelDesc)
                                        Table.removeSort(requestParams, col.Name);
                                    else
                                        Table.updateSort(requestParams, col.Name, true);
                                    console.log("New sort order: " + JSON.stringify(requestParams.Order));
                                    requestParams.Row = 0;
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
                                    onChangeFn();
                                    close();
                                }
                            }));

                            const haveSort = Table.haveSort(requestParams);
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Clear sort", "The text of a menu item that if clicked will remove any sorting of a table"),
                                IconClass: "IconClearSort",
                                Title: _TF("Remove any sorting", "A tool tip description on a menu item that if clicked will remove any sorting of a table"),
                                Flags: haveSort ? 0 : readonly,
                                Data: () => {
                                    requestParams.Order = null;
                                    console.log("New sort order: " + JSON.stringify(requestParams.Order));
                                    requestParams.Row = 0;
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
                                    onChangeFn();
                                    close();
                                }
                            }));
                        }
                        //  Copy to clipboard
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Copy title", "The text of a menu item on a table column header that if clicked will copy the column title to the clipboard"),
                            IconClass: "IconCopy",
                            Title: _T('Click to copy the column title "{0}" to the clipboard', colTitle, "The tool tip description of a menu item on a table column header that if clicked will copy the column title to the clipboard.{0} is replaced with the text that will be copied"),
                            Data: async () => await Copy(col.Title),
                        }));
                        if (col.Name !== col.Title) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy name", "The text of a menu item on a table column header that if clicked will copy the column name (member name) to the clipboard"),
                                IconClass: "IconCopy",
                                Title: _T('Click to copy the column name "{0}" to the clipboard', col.Name, "The tool tip description of a menu item on a table column header that if clicked will copy the column name (member name) to the clipboard.{0} is replaced with the text that will be copied"),
                                Data: async () => await Copy(col.Name),
                            }));
                        }
                        if (col.Desc) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy description", "The text of a menu item on a table column header that if clicked will copy the column description to the clipboard"),
                                IconClass: "IconCopy",
                                Title: _T('Click to copy the column description "{0}" to the clipboard', col.Desc, "The tool tip description of a menu item on a table column header that if clicked will copy the column description to the clipboard.{0} is replaced with the text that will be copied"),
                                Data: async () => await Copy(col.Desc),
                            }));
                        }
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Copy column type", "The text of a menu item on a table column header that if clicked will copy the C# type name of the data in the column to the clipboard"),
                            IconClass: "IconCopy",
                            Title: _T('Click to copy the column type name "{0}" to the clipboard', col.Type, "The tool tip description of a menu item on a table column header that if clicked will copy the C# type name of the data in the column to the clipboard.{0} is replaced with the text that will be copied"),
                            Data: async () => await Copy(col.Type),
                        }));

                        if (canShowChart) {
                            if ((col.Props & TableDataProps.CanChart) !== 0) {
                                const tableInfo = table.Info;
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Show as chart", "The text of a menu item on a table column header that if clicked will show a chart with the values of the column on the value axis"),
                                    IconClass: "IconChart",
                                    Title: _TF("Click to show this columns as a chart", "The tool tip description of a menu item on a table column header that if clicked will show a chart with the values of the column on the value axis"),
                                    Data: () => {
                                        const p = Object.assign({}, requestParams);
                                        p.MaxRowCount = 1000;
                                        delete p.Cc;
                                        delete p.LookAheadCount;
                                        delete p.Row;
                                        let url = tableInfo.Url;
                                        while (url.startsWith("../"))
                                            url = url.substring(3);
                                        const q = {
                                            TableApi: url,
                                            Keys: primaryKey,
                                            Values: [col.Name],
                                            Options: p,
                                            Title: col.Title + " on " + tableInfo.Name
                                        };
                                        const cu = "../chart/chart.html?q=../Api/TableChart?" + JSON.stringify(q);
                                        Open(cu, "_self");
                                    },
                                }));
                            }
                        }
                        if ((col.Type === "System.DateTime") && (!col.Format)) {
                            if (state.TimeMode) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Show local times", "The text of a menu item on a table column header that if clicked will show time stamps as local time instead of UTC"),
                                    IconClass: "IconClock",
                                    Title: _TF("Click to show local time stamps", "The tool tip description of a menu item on a table column header if clicked will show time stamps as local time instead of UTC"),
                                    Data: () => {
                                        state.TimeMode = null;
                                        saveFn();
                                        onChangeFn();
                                        close();
                                    },
                                }));

                            } else {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Show UTC times", "The text of a menu item on a table column header that if clicked will show time stamps as UTC instead of local time"),
                                    IconClass: "IconClock",
                                    Title: _TF("Click to show UTC time stamps", "The tool tip description of a menu item on a table column header if clicked will show time stamps as UTC instead of local time"),
                                    Data: () => {
                                        state.TimeMode = "utc";
                                        saveFn();
                                        onChangeFn();
                                        close();
                                    },
                                }));
                            }
                        }
                        return menu;
                    });
                };
                // Left click and context menu on the header cell open the column menu (the context menu suppresses the browser menu).
                cell.onclick = async ev => {
                    if (badClick(ev))
                        return true;
                    await contextMenu(ev);
                };
                cell.oncontextmenu = async ev => {
                    if (badClick(ev, true))
                        return true;
                    ev.preventDefault();
                    ev.stopPropagation();
                    await contextMenu(ev);
                    return false;
                };



                i.onclick = onClickFn;
                i.title = title;

                //i.title = title;
                //i.onclick = onClickFn;
                //s.onclick = onClickFn;
                ++cellIndex;
            }
        });
    }

    /**
     * Inserts one filter set (3 `tr.FilterRow` rows) at `rowIndex` and creates / updates the
     * `state.Filters[set]` entries (one TableDataFilter per visible column; `ColName` and `ColumnIndex`
     * refreshed from `cols`). Rows get `HideRow` according to `state.Expanded`.
     * - Row 1 (ops): action cell = `IconReset` on the first set (resets ops to Contains, flags and values,
     *   deletes the extra sets, clears the search) or `IconClose` on other sets (removes the set); then one op
     *   `<select>` per column with all TableDataFilterOps (options disabled unless the column props allow them;
     *   whole select disabled when none; default picked from `DefaultOrders`). Changing the op updates the value
     *   input placeholder.
     * - Row 2 (values): action cell = `IconClear` (clears all values of the set); one text `<input>` per column
     *   (applies on keyup / change; disabled without `AnyFilters`). Visible in simple mode for the first set only.
     * - Row 3 (flags): action cell = `IconPlus` (inserts a new set after this one, max 3 sets); two
     *   `table-check` toggles per column: "!" (Invert, needs `AnyFilters`) and "A ≠ a" (CaseSensitive, needs
     *   `TextFilter`), class `Checked` / `Disabled`, each with an expando `Update` re-rendering it.
     * Changes that affect the result (a value is set) reset `Row` to 0 and call `onChangeFn` (save + refetch);
     * other changes only call `saveFn`.
     * @param {HTMLTableElement} table - The data table.
     * @param {TableDataState} state - The table state (Filters / FilterRows are modified).
     * @param {number} rowIndex - Table row index of the first row of the set (1 + setIndex * 3).
     * @param {number} columnCount - Number of visible (non-hidden) columns.
     * @param {function():void} onChangeFn - Save + refetch callback.
     * @param {function():void} saveFn - Save-only callback.
     * @param {Array<object>} cols - TableData.Cols (incl. hidden columns).
     * @returns {void}
     */
    static addFilterRow(table, state, rowIndex, columnCount, onChangeFn, saveFn, cols) {
        const iconSize = 23;
        const requestParams = state.RequestParams;
        /**
         * Returns the filter set index of a table row index (rows 1..3 -> 0, 4..6 -> 1, ...).
         * @param {number} indexOfRow - Table row index of a filter row.
         * @returns {number} Filter set index: `((indexOfRow - 1) / 3) | 0`.
         */
        const getFilterIndex = function (indexOfRow) {
            return ((indexOfRow - 1) / 3) | 0
        };
        const gfi = getFilterIndex(rowIndex);
        let filters = state.Filters[gfi];
        if (!filters) {
            filters = [];
            state.Filters[gfi] = filters;
        }

        let ci = 0;
        for (let sourceIndex = 0; sourceIndex < cols.length; ++sourceIndex) {
            const col = cols[sourceIndex];
            if ((col.Props & TableDataProps.Hide) !== 0)
                continue;
            let filter = filters[ci];
            if (!filter) {
                filter = new TableDataFilter(col.Name);
                filters[ci] = filter;
            }
            else
                filter.ColName = col.Name;
            filter.ColumnIndex = sourceIndex;
            ++ci;
        }
        /**
         * Finds the first table row of the filter set containing an element (computed at event time because
         * sets can shift when other sets are added or removed).
         * @param {HTMLElement} element - An element inside a filter row (or the row itself).
         * @returns {number} Row index `((rowIndex - 1) / 3 | 0) * 3 + 1` of the enclosing TR.
         */
        const getFirstRowIndex = function (element) {
            while (element.tagName != "TR") {
                element = element.parentElement;
            }
            let ri = element.rowIndex;
            --ri;
            ri /= 3;
            ri |= 0;
            ri *= 3;
            ++ri;
            return ri;
        };


        const rows = table.rows;
        //  Filter row 1
        {
            const trow = table.insertRow(rowIndex);
            trow.classList.add("FilterRow");
            if (state.Expanded != 2)
                trow.classList.add("HideRow");
            //  Action
            {
                const cell = trow.insertCell();

                /**
                 * Removes the filter set starting at table row `rIndex`: decrements `state.FilterRows`, splices the
                 * set out of `state.Filters` and deletes its 3 rows. Uses (and changes) the shared loop variable `ci`.
                 * @param {number} rIndex - Table row index of the first row of the set.
                 * @returns {boolean} True when the removed set had a non-empty filter value (i.e. a refetch is needed).
                 */
                const deleteFilters = function (rIndex) {

                    const filterIndex = getFilterIndex(rIndex);
                    const filters = state.Filters[filterIndex];
                    let changed = false;
                    for (ci = 0; ci < columnCount; ++ci) {
                        changed = filters[ci].Value != "";
                        if (changed)
                            break;
                    }
                    --state.FilterRows;
                    state.Filters.splice(filterIndex, 1);
                    table.deleteRow(rIndex + 2);
                    table.deleteRow(rIndex + 1);
                    table.deleteRow(rIndex);
                    return changed;
                };


                /*
                 * First set: Reset icon. Resets Invert / CaseSensitive / Op (to Contains) / Value of every filter of the
                 * set (updating the controls), deletes all other sets and clears the search text. Cleared values and
                 * flag/op changes count as a change (`changed |= fchanged`); deleted sets with values and a
                 * cleared search also count. Changed -> Row = 0 + onChangeFn, else saveFn.
                 * Other sets: Close icon removing this set (deleteFilters), refetching when it had values.
                 */
                if (rowIndex == 1) {
                    const defIndex = TableDataFilterOps.Contains;
                    const icon = new ColorIcon("IconReset", "IconColorThemeAcc1", iconSize, iconSize,
                        _TF("Click to reset all filter values and parameters to their default (no filtering).", "A tool tip description on a button that if clicked will reset all paramaters to default")
                        , ev => {
                            let changed = false;
                            let fchanged = false;
                            const rowFirst = getFirstRowIndex(trow);
                            for (ci = 0; ci < columnCount; ++ci) {
                                const filter = filters[ci];
                                if (filter.Invert) {
                                    filter.Invert = false;
                                    fchanged = true;
                                    rows[rowFirst + 2].cells[ci + 1].firstElementChild.Update();
                                }
                                if (filter.CaseSensitive) {
                                    filter.CaseSensitive = false;
                                    fchanged = true;
                                    rows[rowFirst + 2].cells[ci + 1].children[1].Update();
                                }
                                if (filter.Op != defIndex) {
                                    filter.Op = defIndex;
                                    fchanged = true;
                                    rows[rowFirst].cells[ci + 1].firstElementChild.options[defIndex].selected = "selected";
                                }
                                if (filter.Value != "") {
                                    filter.Value = "";
                                    changed = true;
                                    rows[rowFirst + 1].cells[ci + 1].firstElementChild.value = "";
                                }
                            }
                            changed |= fchanged;
                            while (state.FilterRows > 1)
                                changed |= deleteFilters(rowFirst + 3);
                            changed |= table.ClearSearch();
                            if (changed) {
                                requestParams.Row = 0;
                                onChangeFn();
                            }
                            else {
                                saveFn();
                            }
                        });
                    cell.Icon = icon;
                    cell.appendChild(icon.Element);
                } else {
                    const icon = new ColorIcon("IconClose", "IconColorThemeAcc1", iconSize, iconSize,
                        _TF("Click to remove this filter set.", "A tool tip description on a button that if clicked will remove a set of filters")
                        , ev => {
                        const rowFirst = getFirstRowIndex(trow);
                        if (deleteFilters(rowFirst)) {
                            requestParams.Row = 0;
                            onChangeFn();
                        } else {
                            saveFn();
                        }

                    });
                    cell.Icon = icon;
                    cell.appendChild(icon.Element);
                }
            }
            //  Selections
            for (ci = 0; ci < columnCount; ++ci) {
                const filter = filters[ci];
                const cellIndex = ci;
                const col = cols[filter.ColumnIndex];
                const props = col.Props;
                const cell = trow.insertCell();
                const s = document.createElement("select");
                cell.appendChild(s);
                let anyEnabled = false;
                for (let oi = 0; oi < TableDataFilterOps.Count; ++oi)
                {
                    const oName = TableDataFilterOps.Names[oi];
                    const oDesc = TableDataFilterOps.Descriptions[oi];
                    const oProps = TableDataFilterOps.Props[oi];
                    const o = document.createElement("option");
                    o.value = oi;
                    o.innerText = oName;
                    if ((props & oProps) !== 0) {
                        o.title = oDesc + "\n" + _TF("Filtering is applied on the raw value before any formatting.", "A tool tip description letting the user know that any filtering is performed on the raw values and not as displayed after formatting");
                        anyEnabled = true;
                    } else {
                        o.disabled = true;
                    }
                    s.appendChild(o);
                }
                if (anyEnabled) {
                    if ((filter.Op < 0) || (s.options[filter.Op].disabled)) {
                        const orders = TableDataFilterOps.DefaultOrders;
                        const ool = orders.length;
                        for (let i = 0; i < ool; ++i) {
                            const opi = orders[i];
                            if (!s.options[opi].disabled) {
                                filter.Op = opi;
                                s.options[opi].selected = 'selected';
                                break;
                            }
                        }
                    } else {
                        s.options[filter.Op].selected = 'selected';
                    }
                    // Op changed: store it (as the option value string), update the value input placeholder; refetch only when a value is set.
                    s.onchange = ev => {
                        const newOp = s.options[s.selectedIndex].value;
                        filter.Op = newOp;
                        const rowFirst = getFirstRowIndex(trow);
                        rows[rowFirst + 1].cells[cellIndex + 1].firstElementChild.placeholder = TableDataFilterOps.Names[newOp];
                        if (filter.Value != "") {
                            requestParams.Row = 0;
                            onChangeFn();
                        } else {
                            saveFn();
                        }
                    };
                } else {
                    s.disabled = true;
                }
            };
        }
        ++rowIndex;

        //  Filter row 2 (text)
        {
            const trow = table.insertRow(rowIndex);
            trow.classList.add("FilterRow");
            if (state.Expanded != 2) {
                const rowFirst = getFirstRowIndex(trow);
                if ((rowFirst != 1) || (state.Expanded != 1))
                    trow.classList.add("HideRow");
            }
            const cells = trow.cells;
            //  Action
            {
                const cell = trow.insertCell();
                const icon = new ColorIcon("IconClear", "IconColorThemeAcc2", iconSize, iconSize,
                    _TF("Click to clear all filter values.", "A tool tip description on a button that if clicked will remove all filter values")
                    , ev => {
                    let changed = false;
                    for (ci = 0; ci < columnCount; ++ci) {
                        const filter = filters[ci];
                        if (filter.Value == "")
                            continue;
                        changed = true;
                        filter.Value = "";
                        cells[ci + 1].firstElementChild.value = "";
                    }
                    if (changed) {
                        requestParams.Row = 0;
                        onChangeFn();
                    }
                    else {
                        saveFn();
                    }
                });
                cell.Icon = icon;
                cell.appendChild(icon.Element);
            }
            for (ci = 0; ci < columnCount; ++ci) {
                const filter = filters[ci];
                const cell = trow.insertCell();
                const col = cols[filter.ColumnIndex];
                const props = col.Props;
                const s = document.createElement("input");
                cell.appendChild(s);
                s.type = "text";
                if ((props & TableDataProps.AnyFilters) !== 0) {

                    s.value = filter.Value;
                    s.placeholder = TableDataFilterOps.Names[filter.Op];
                    s.title = _TF("Filtering is applied on the raw value before any formatting.", "A tool tip description letting the user know that any filtering is performed on the raw values and not as displayed after formatting");
                    // Value typed / changed: when different from the stored value, store it, reset Row and refetch.
                    s.onkeyup = ev => {
                        const val = s.value;
                        if (val == filter.Value)
                            return;
                        filter.Value = val;
                        requestParams.Row = 0;
                        onChangeFn();
                    };
                    s.onchange = ev => {
                        const val = s.value;
                        if (val == filter.Value)
                            return;
                        filter.Value = val;
                        requestParams.Row = 0;
                        onChangeFn();
                    };
                } else {
                    s.disabled = true;
                }
            };
        }
        ++rowIndex;

        //  Filter row 3 (flags)
        {
            const trow = table.insertRow(rowIndex);
            trow.classList.add("FilterRow");
            if (state.Expanded != 2)
                trow.classList.add("HideRow");
            {
                const cell = trow.insertCell();
                const icon = new ColorIcon("IconPlus", "IconColorThemeMain", iconSize, iconSize,
                    _TF("Click to add a new set of filters.", "A tool tip description on a button that if clicked will add a new set of filter options")
                    , ev => {
                    if (state.FilterRows >= 3)
                        return;
                    const rowFirst = getFirstRowIndex(trow);
                    const filterIndex = getFilterIndex(rowFirst);
                    state.Filters.splice(filterIndex + 1, 0, []);
                    ++state.FilterRows;
                    Table.addFilterRow(table, state, rowFirst + 3, columnCount, onChangeFn, saveFn, cols);
                    saveFn();
                });
                cell.Icon = icon;
                cell.appendChild(icon.Element);
            }
            for (ci = 0; ci < columnCount; ++ci) {
                const filter = filters[ci];
                const cell = trow.insertCell();
                const col = cols[filter.ColumnIndex];
                const props = col.Props;
                const invert = document.createElement("table-check");
                const caseSens = document.createElement("table-check");
                keyboardClick(invert);
                keyboardClick(caseSens);
                cell.appendChild(invert)
                cell.appendChild(caseSens);
                //const checkPrefix = "☑ ";
                //const unCheckPrefix = "☐ ";

                const checkPrefix = "";
                const unCheckPrefix = "";
                /**
                 * Renders the "!" (Invert) toggle from `filter.Invert`: text, tooltip and class `Checked`.
                 * Stored as the expando `invert.Update` (called by the Reset handler).
                 * @returns {void}
                 */
                const setInvert = function () {
                    const e = filter.Invert;
                    invert.innerText = (e ? checkPrefix : unCheckPrefix) + "!";
                    invert.title = e
                        ?
                        _TF("Showing data that doesn't pass filter function.\nUncheck to show data that pass the filter function.", "A tool tip description on a toggle button that is checked, indicating that data that is NOT passing the filter is kept")
                        :
                        _TF("Showing data that pass the filter function.\nCheck to show data that doesn't pass the filter function.", "A tool tip description on a toggle button that is unchecked, indicating that data that is passing the filter is kept")
                        ;
                    if (e)
                        invert.classList.add("Checked");
                    else
                        invert.classList.remove("Checked");
                };
                /**
                 * Renders the "A ≠ a" (CaseSensitive) toggle from `filter.CaseSensitive`: text, tooltip and class
                 * `Checked`. Stored as the expando `caseSens.Update` (called by the Reset handler).
                 * @returns {void}
                 */
                const setCaseSens = function () {
                    const e = filter.CaseSensitive;
                    caseSens.innerText = (e ? checkPrefix : unCheckPrefix) + "A ≠ a";
                    caseSens.title = e
                        ?
                        _TF("Case sensitive matching is enabled.\nUncheck to perform a case insensitive match instead.", "A tool tip description on a toggle button that is checked, indicating that data that case sensitive text filtering should be applied")
                        :
                        _TF("Case insensitive matching is enabled.\nCheck to perform a case sensitive math instead.", "A tool tip description on a toggle button that is unchecked, indicating that data that case insensitive text filtering should be applied")
                        ;
                    if (e)
                        caseSens.classList.add("Checked");
                    else
                        caseSens.classList.remove("Checked");
                };
                invert.Update = setInvert;
                caseSens.Update = setCaseSens;
                setInvert();
                setCaseSens();
                if ((props & TableDataProps.AnyFilters) !== 0) {
                    invert.tabIndex = "0";
                    // Toggle Invert; refetch (Row = 0) when the filter has a value, else just save. Same pattern for caseSens below.
                    invert.onclick = ev => {
                        if (badClick(ev))
                            return;
                        filter.Invert = !filter.Invert;
                        setInvert();
                        if (filter.Value != "") {
                            requestParams.Row = 0;
                            onChangeFn();
                        }
                        else {
                            saveFn();
                        }
                    };
                } else {
                    invert.classList.add("Disabled");
                }
                if ((props & TableDataProps.TextFilter) !== 0) {
                    caseSens.tabIndex = "0";
                    // Toggle CaseSensitive; refetch (Row = 0) when the filter has a value, else just save.
                    caseSens.onclick = ev => {
                        if (badClick(ev))
                            return;
                        filter.CaseSensitive = !filter.CaseSensitive;
                        setCaseSens();
                        if (filter.Value != "") {
                            requestParams.Row = 0;
                            onChangeFn();
                        }
                        else {
                            saveFn();
                        }
                    };
                } else {
                    caseSens.classList.add("Disabled");
                }
            };
        }
    }

    /**
     * Renders a TableData response into the table.
     * When `data.Cols` is present (first response or column change): stores it as `table.Cols`, clears the
     * table, builds the header row (`insertTH`; cell 0 = `IconPlus` ColorIcon cycling `state.Expanded`
     * 0 -> 1 -> 2 -> 0 with tableUpdateFilterIcon + saveFn; then per visible column a `div` with a 20px ColorIcon
     * and a `span`, stored as `cell.Icon` / `cell.Text`), adds `state.FilterRows` filter sets (addFilterRow),
     * then calls tableUpdateFilterIcon and tableUpdateHeader. Otherwise reuses `table.Cols` (the server may omit
     * Cols on later responses).
     * Data rows start at row `1 + FilterRows * 3` and are reused in place: cell 0 = 1-based row number
     * (`Row + 1 + i`, expando `trow.DataRow`), other cells are updated with
     * `ValueFormat.update(cell, type, value, flash, fmt, next, onChangeFn)` only when `value + "|" + fmt`
     * (cached in expando `cell.Value`) changed or the columns were rebuilt. Unformatted `System.DateTime`
     * columns (Format === null) use `state.TimeMode` as format. `flash` is "Flash" for changed cells of a row that
     * shows the same data row number as before, and only when `didChange` is false (periodic refresh). Surplus
     * rows are deleted.
     * @param {HTMLTableElement} table - The data table.
     * @param {object} data - The TableData response (`Cols?`, `Rows`).
     * @param {TableDataState} state - The table state.
     * @param {function():void} onChangeFn - Save + refetch callback (also passed to ValueFormat.update for editable cells).
     * @param {function():void} saveFn - Save-only callback.
     * @param {boolean} didChange - True when the request was caused by a user change (suppresses flashing).
     * @returns {void}
     */
    static tableRedraw(table, data, state, onChangeFn, saveFn, didChange) {
        const requestParams = state.RequestParams;
        let cols = data.Cols;
        const flashClass = didChange ? null : "Flash";
        const trows = table.rows;
        const newCol = !!cols;
        let trowCount = trows.length;
        let rowIndex = 0;
        if (cols) {
            table["Cols"] = cols;
            table.innerHTML = "";
            let columnCount = 0;
            //  Header
            {
                const trow = table.insertRow();
                const cells = trow.cells;
                let cellIndex = 0;
                {
                    const cell = insertTH(trow);
                    const icon = new ColorIcon("IconPlus", "IconColorThemeMain", 23, 23, null, ev => {
                        state.Expanded = (state.Expanded + 1) % 3;
                        Table.tableUpdateFilterIcon(table, state);
                        saveFn();
                    });
                    cell.Icon = icon;
                    cell.appendChild(icon.Element);
                }
                ++cellIndex;
                let sourceIndex = -1;
                cols.forEach(col => {
                    ++sourceIndex;
                    if ((col.Props & TableDataProps.Hide) === 0) {
                        ++columnCount;
                        const cell = insertTH(trow);
                        const d = document.createElement("div");
                        const s = document.createElement("span");
                        const icon = new ColorIcon("", "IconColorThemeAcc1", 20, 20);
                        cell.Icon = icon;
                        cell.Text = s;
                        d.appendChild(icon.Element);
                        d.appendChild(s);
                        cell.appendChild(d);
                        ++cellIndex;
                    }
                });
                while (cells.length > cellIndex)
                    cells[cells.length - 1].remove();
            }
            ++rowIndex;
            for (var fi = 0; fi < state.FilterRows; ++fi) {
                Table.addFilterRow(table, state, rowIndex, columnCount, onChangeFn, saveFn, cols);
                rowIndex += 3;
            }
            Table.tableUpdateFilterIcon(table, state);
            Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn);
        } else {
            cols = table["Cols"];
        }
        trowCount = trows.length;
        rowIndex = 1 + state.FilterRows * 3;
        const colCount = cols.length;
        let dataRow = requestParams.Row + 1;
        const drows = data.Rows;

        const isGeneric = drows.length <= 0 || Array.isArray(drows[0].Values);
        /**
         * Value accessor chosen once per redraw: generic rows (first row has a `Values` array, or no rows)
         * use `rowData.Values[colIndex]`, typed rows use `rowData[colName]`.
         * @type {function(object, number, string):*}
         */
        const getRowData = isGeneric
            ?
            (rowData, colIndex, colName) => rowData.Values[colIndex]
            :
            (rowData, colIndex, colName) => rowData[colName]
            ;
        const timeMode = state.TimeMode;
        drows.forEach(drow => {
            const trow = trowCount <= rowIndex ? table.insertRow() : trows[rowIndex];
            const er = trow["DataRow"];
            const flash = (er && (er === dataRow)) ? flashClass : null;
            const cells = trow.cells;
            const cellCount = cells.length;
            let cellIndex = 0;
            const cell = cellIndex < cellCount ? cells[cellIndex] : trow.insertCell();
            ++cellIndex;
            if (cell["Value"] !== dataRow) {
                cell["Value"] = dataRow;
                ValueFormat.updateText(cell, dataRow);
            }
            trow["DataRow"] = dataRow;
            for (let i = 0; i < colCount; ++i) {
                const col = cols[i];
                if ((col.Props & TableDataProps.Hide) !== 0)
                    continue;
                const value = getRowData(drow, i, col.Name);
                const type = col.Type;
                let fmt = col.Format;
                if ((type === "System.DateTime") && (fmt === null))
                    fmt = timeMode;
                const cValue = value + "|" + fmt;
                const cell = cellIndex < cellCount ? cells[cellIndex] : trow.insertCell();
                if (!newCol) {
                    if (cell["Value"] === cValue)
                    {
                        ++cellIndex;
                        continue;
                    }
                }
                const next = (i + 1) < colCount ? getRowData(drow, i + 1, cols[i + 1].Name) : undefined;
                cell["Value"] = cValue;
                ValueFormat.update(cell, type, value, flash, fmt, next, onChangeFn);
                ++cellIndex;
            }
            ++dataRow;
            ++rowIndex;
        });
        while (trowCount > rowIndex) {
            --trowCount;
            table.deleteRow(trowCount);
        }
    }

    /**
     * Creates the default TableDataRequest.
     * @returns {{Cc:number, Row:number, MaxRowCount:number, LookAheadCount:number, Order:(string[]|null)}}
     *   `{Cc: 0, Row: 0, MaxRowCount: 25, LookAheadCount: 1, Order: null}`.
     */
    static tableDefaultRequest() {
        return {
            Cc: 0,
            Row: 0,
            MaxRowCount: 25,
            LookAheadCount: 1,
            Order: null,
        };
    }

    /**
     * Creates a default table state.
     * @returns {TableDataState} `new TableDataState()`.
     */
    static tableDefaultState() {
        return new TableDataState();
    }


    /**
     * Returns the list of table exporters, cached in `Table.MenuExportItems`. On first use loads
     * `../app/application.js` and `../app/application.css` (via includeJs / includeCss, relative to `Table.CS`)
     * in parallel with `getRequest("../Api/GetTableExporters")`.
     * @returns {Promise<Array<{Id:string, Name:string, IconClass:string}>>} The exporter items.
     */
    static async getTableExportMenu() {
        let m = Table.MenuExportItems;
        if (m)
            return m;
        const current = Table.CS;
        const [js, css, exp] = await Promise.all([
            includeJs(current, "../app/application.js"),
            includeCss(current, "../app/application.css"),
            getRequest("../Api/GetTableExporters")
        ]);
        Table.MenuExportItems = exp;
        return exp;
    }

    /** @type {string} Url of this script (`document.currentScript.src` at load time); base for the dynamic includes. */
    static CS = document.currentScript.src;
    /** @type {Array<object>|null} Cached exporter list from getTableExportMenu (null until loaded). */
    static MenuExportItems = null;

    /**
     * Creates a live, auto-refreshing data table for a table API - the main reusable entry point.
     *
     * Setup:
     * - state key `"SysWeaver.Table." + await hashString(url)`; the state comes from `stateStr` (when given) or
     *   localStorage, is JSON-parsed (invalid -> default) and repaired (missing RequestParams fields, FilterRows,
     *   Filters, AutoRowCount); `filter` replaces `state.Filters` with a single filter set; saved when repaired.
     *   `RequestParams.Param = tableParams`.
     * - DOM appended to `toElement`: `table.DataTable` (expandos `Info {Name, Url}`, `Cols`, `HomeIcon`,
     *   `PrevIcon`, `NextIcon`, `ExportIcon`, `ClearSearch`) followed by a `datatable-footer` with 26px ColorIcons
     *   Home / Prev / Next / Export, a search `<input>` and a clear-search icon.
     * - registers a `window` resize listener (auto row count) and starts the update loop (`Update()`, not awaited).
     *
     * Anonymous handlers: Home (Row = 0), Prev (Row - MaxRowCount, min 0), Next (Row + MaxRowCount) - each calls
     * onChangeFn; Export opens a `PopUpMenu` (popup class `Table`) with "Current view" / "Filtered" / "All"
     * sub menus listing the exporters; the clear-search icon clears the text and refetches when a search was set.
     * @param {string} url - Table API url (relative to the host page, normally `../Api/...`); POSTed a TableDataRequest.
     * @param {HTMLElement} [toElement=document.body] - Container the table and footer are appended to.
     * @param {number|string} [rowsPerPage=25] - Defaulted but otherwise unused (page size is RequestParams.MaxRowCount).
     * @param {*} tableParams - Value sent as `RequestParams.Param`.
     * @param {function():void} [onFirstLoad] - Called once after the first response or the first error.
     * @param {string} [tableName] - Table name (export filename, chart title); default: camel-case-split last url
     *   segment, replaced by `data.Title` of the first response when not given.
     * @param {string} [stateStr] - JSON TableDataState used instead of the stored state.
     * @param {function(object):void} [onFirstData] - Called once with the first TableData received.
     * @param {string} [filter] - JSON of one TableDataFilter or an array of them; replaces the stored filters.
     * @returns {Promise<{UpdateNow: function():Promise<void>}>} Resolves after setup; `UpdateNow` forces an
     *   immediate refresh (used e.g. by ServerManager keys.js after an upload).
     */
    static async addTable(url, toElement, rowsPerPage, tableParams, onFirstLoad, tableName, stateStr, onFirstData, filter) {
        if (!toElement)
            toElement = document.body;
        if (!rowsPerPage)
            rowsPerPage = 25;
        //  Get request params, define save function
        const key = "SysWeaver.Table." + await hashString(url);
        const useTitleAsName = !tableName;

        if (useTitleAsName) {
            const tt = url.split('?')[0].split('/');
            tableName = ValueFormat.removeCamelCase(tt[tt.length - 1]);
        }

        const tableInfo =
        {
            Name: tableName,
            Url: url,
        };

        let state = stateStr ?? localStorage.getItem(key);

        /**
         * Persists `state` as JSON to `localStorage[key]` (logs "Saved state"; failures are only logged).
         * @returns {void}
         */
        const save = function () {

            try {
                localStorage.setItem(key, JSON.stringify(state));
                console.log("Saved state");
            }
            catch (ex) {
                console.log("Failed to save request parameters to: " + key + ", ex: " + ex);
            }
        };

        try {
            if (state)
                state = JSON.parse(state);
        }
        catch (e)
        {
            console.warn("Invalid state: " + state + "\n" + e);
            state = null;
        }
        {
            let fixed = false;
            if (!state) {
                state = Table.tableDefaultState();
                fixed = true;
            }
            if (filter) {
                try {
                    const filters = JSON.parse(filter);
                    if (Array.isArray(filters))
                        state.Filters = [filters];
                    else
                        state.Filters = [[filters]];
                    fixed = true;
                }
                catch (e) {
                    console.warn("Invalid filters: " + filter + "\n" + e);
                }
            }
            if (!state.RequestParams) {
                state.RequestParams = Table.tableDefaultRequest();
                fixed = true;
            } else {
                const rr = state.RequestParams;
                if (!rr.Cc)
                    rr.Cc = 0;
                if (!rr.Row)
                    rr.Row = 0;
                if (!rr.MaxRowCount)
                    rr.MaxRowCount = 25;
                if (!rr.LookAheadCount)
                    rr.LookAheadCount = 1;
                if (!rr.Order)
                    rr.Order = null;
            }
            if ((!state.FilterRows) || (state.FilterRows <= 0)) {
                state.FilterRows = 1;
                fixed = true;
            }
            if (!state.Filters) {
                state.Filters = [[]];
                state.Expanded = 1;
                fixed = true;
            }
            if (!state.Filters[0]) {
                state.Filters[0] = [];
                fixed = true;
            }
            if (typeof state.AutoRowCount !== "boolean") {
                state.AutoRowCount = true;
                fixed = true;
            }
            if (fixed)
                save();
        }
        const requestParams = state.RequestParams;
        requestParams.Param = tableParams;

        //  Define on change function, that will abort the delay and fetch new data
        /** @type {number} Change counter of the last response (TableData.Cc), sent back as RequestParams.Cc. */
        let cc = 0;
        /** @type {AbortHandler} Created but never used. */
        const aborter = new AbortHandler();
        /** @type {boolean} True while a user-caused change is pending; passed to sendRequest and suppresses cell flashing. */
        let didChange = false;
        /**
         * Change callback for anything that requires new data: saves the state, sets `didChange` and
         * calls `UpdateNow()` (cancels the refresh timer and fetches immediately). Passed to the header, filter
         * and redraw code and to `ValueFormat.update`.
         * @returns {void}
         */
        const onChangeFn = () => {
            //console.log("Request changed! Sort: " + requestParams.SortCol + (requestParams.SortReverse ? " revered" : ""));
            save();
            didChange = true;
            UpdateNow();
        }

        //  Define on resize chnage

        /**
         * Auto row count: from the average height of the data rows and the space between the table top and the
         * footer (minus 40px, at least 128px) computes how many rows fit, clamps the new `MaxRowCount` to 5..100
         * rounded down to a multiple of 5, and calls `onChangeFn()` when it differs. Does nothing without data rows.
         * Called by the window resize listener and saveFn (both only when AutoRowCount and Row == 0) and after
         * each redraw (via setTimeout).
         * @param {UIEvent} [ev] - The resize event (unused).
         * @returns {void}
         */
        const onResizeFn = ev => {
            const startRow = 1 + state.FilterRows * 3;
            const rows = table.rows;
            const rowC = rows.length;
            if (startRow < rowC) {
                let dataHeight = 0;
                for (let i = startRow; i < rowC; ++i)
                    dataHeight += rows[i].offsetHeight;
                const rowHeight = Math.ceil(dataHeight / (rowC - startRow));
                let windowHeight = footer.offsetTop - table.offsetTop - 16 - 24;
                if (windowHeight < 128)
                    windowHeight = 128;
                const tableHeight = table.offsetHeight;
                const spaceLeft = windowHeight - tableHeight;
                const countLeft = Math.floor(spaceLeft / rowHeight);
                let newCount = requestParams.MaxRowCount + countLeft;
                if (newCount < 5)
                    newCount = 5;
                if (newCount > 100)
                    newCount = 100;
                newCount = ((newCount / 5) | 0) * 5;
                if (newCount != requestParams.MaxRowCount) {
                    requestParams.MaxRowCount = newCount;
                    onChangeFn();
                }
            }
        };
        window.addEventListener('resize', ev => {
            if (state.AutoRowCount && (requestParams.Row == 0))
                onResizeFn(ev);
        });

        /**
         * Save callback for changes that do not need new data: `save()` plus `onResizeFn()` when auto row
         * count applies (AutoRowCount and first page), since filter rows may have been shown or hidden.
         * @returns {void}
         */
        const saveFn = function () {
            save();
            if (state.AutoRowCount && (requestParams.Row == 0))
                onResizeFn();
        };

        //  Create the table
        const table = document.createElement("table");
        table.Info = tableInfo;
        table.classList.add("DataTable");
        toElement.appendChild(table);
        const footer = document.createElement("datatable-footer");
        toElement.appendChild(footer);
        const navSize = 26;
        const navColor = "IconColorThemeAcc1";
        //  Home
        const homeIcon = new ColorIcon("IconHome", navColor, navSize, navSize,
            _TF("Go to the first page", "A tool tip description on a button that if clicked will navigate to the first page"),
            ev => {
            if (requestParams.Row != 0) {
                requestParams.Row = 0;
                onChangeFn();
            }
        }).SetEnabled(false);
        table["HomeIcon"] = homeIcon;
        footer.appendChild(homeIcon.Element);
        //  Prev
        const prevIcon = new ColorIcon("IconPrev", navColor, navSize, navSize,
            _TF("Go to the previous page", "A tool tip description on a button that if clicked will navigate to the previous page"),
            ev => {
            let row = requestParams.Row;
            if (row > 0) {
                row -= requestParams.MaxRowCount;
                if (row < 0)
                    row = 0;
                requestParams.Row = row;
                onChangeFn();
            }
        }).SetEnabled(false);
        table["PrevIcon"] = prevIcon;
        footer.appendChild(prevIcon.Element);
        const nextIcon = new ColorIcon("IconNext", navColor, navSize, navSize,
            _TF("Go to the next page", "A tool tip description on a button that if clicked will navigate to the next page"),
            ev => {
            let row = requestParams.Row;
            row += requestParams.MaxRowCount;
            requestParams.Row = row;
            onChangeFn();
        }).SetEnabled(false);
        table["NextIcon"] = nextIcon;
        footer.appendChild(nextIcon.Element);
        //  Export
        const exportIcon = new ColorIcon("IconExport", navColor, navSize, navSize,
            _TF("Click to show export options", "A tool tip description on a button that if clicked will show a menu with data table export options"),
            async ev => {
            const menuItems = await Table.getTableExportMenu();
            PopUpMenu(exportIcon.Element, (close, backEl) => {

                const pp = backEl.parentElement;
                pp.classList.add("Table");
                const menu = new WebMenu();
                menu.Name = "Table";
                /**
                 * Adds an export sub menu to the "Table" WebMenu with one child per exporter (from
                 * getTableExportMenu); each child's `Data` calls `exportTable(mode, item.Id, item.Name, close, title,
                 * item.IconClass)`.
                 * @param {string} name - Sub menu text ("Current view", "Filtered", "All").
                 * @param {string} title - Sub menu tooltip (also shown in the "Exporting" popup).
                 * @param {number} mode - Export mode: 0 current view, 1 filtered (all pages), 2 all data.
                 * @returns {void}
                 */
                function add(name, title, mode)
                {
                    const mm = mode;
                    const subMenu = WebMenuItem.From({
                        Name: name,
                        Title: title,
                        Children: [],
                    });
                    menu.Items.push(subMenu);
                    menuItems.forEach(menuItem => {
                        const sm = WebMenuItem.From(menuItem);
                        const id = menuItem.Id;
                        const name = menuItem.Name;
                        const desc = menuItem.Title;
                        const icon = menuItem.IconClass;
                        /**
                         * Menu item action: export with this exporter in the sub menu's mode.
                         * @returns {Promise<void>}
                         */
                        sm.Data = () => exportTable(mm, id, name, close, title, icon);
                        subMenu.Children.push(sm);
                    });
                }
                add(
                    _TF("Current view", "The text of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including just the visible page"),
                    _TF("Export what you currently see", "The tool tip description of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including just the visible page"),
                    0);
                add(
                    _TF("Filtered", "The text of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including all pages"),
                    _TF("Export all data with the current filters applied.\nLimits on number of rows may still apply!", "The tool tip description of a menu item that can contains child items with export options for exporting the data table using the applied filters and sort including all pages"),
                    1);
                add(
                    _TF("All", "The text of a menu item that can contains child items with export options for exporting the data table ignoring any used filters including all pages"),
                    _TF("Export all data.\nLimits on number of rows may still apply!", "The tool tip description of a menu item that can contains child items with export options for exporting the data table ignoring any used filters including all pages"),
                    2);
                return menu;
            }, false, true); 

        }, null, null, true);
        table["ExportIcon"] = exportIcon;
        footer.appendChild(exportIcon.Element);

        /** @type {object|null} The last TableData received (exported by mode 0). */
        let latestData = null;
        /**
         * Exports the table: closes the menu, shows `PopUpWorking("Exporting", ...)`, then
         * - mode 0: `sendRequest("../Api/ExportTableData", {Data: latestData (Cols set to table.Cols), ExportAs: id,
         *   Options: {Filename: tableName}})`;
         * - mode 1 / 2: `sendRequest("../Api/ExportTableApi", {Api: url, Req: copy of requestParams with Cc and Row 0
         *   (mode 2 also nulls Filters, SearchIndex, SearchText), ExportAs: id, Options: {Filename: tableName}})`.
         * The returned MemoryFile is downloaded through a hidden `a[download]` with a base64 `data:` url when it has
         * `Mime` and `Data`; with only `Name`, the absolute url of `../<Name>` is copied to the clipboard and opened;
         * otherwise / on error `Fail`. The working popup is closed afterwards.
         * @param {number} mode - 0 current view, 1 filtered, 2 all.
         * @param {string} id - Exporter id (ExportAs).
         * @param {string} name - Exporter name (popup text).
         * @param {function():void} closeFn - Closes the export menu.
         * @param {string} text - Sub menu tooltip (popup text).
         * @param {string} icon - Exporter icon class (popup icon).
         * @returns {Promise<void>}
         */
        async function exportTable(mode, id, name, closeFn, text, icon) {

            console.log("Exporting " + mode + " using " + id);
            closeFn();
            const close = await PopUpWorking(
                _TF("Exporting", "A message shown while exporting a data table"),
                name + ".\n\n" + text + ".", icon);
            try {
                let dataFile = null;
                switch (mode) {
                    case 0:
                        latestData.Cols = table.Cols;
                        dataFile = await sendRequest("../Api/ExportTableData", {
                            Data: latestData,
                            ExportAs: id,
                            Options: {
                                Filename: tableName,
                            },
                        });
                        break;
                    case 1:
                    case 2:
                        const pc = Object.assign({}, requestParams);
                        if (mode === 2) {
                            pc.Filters = null;
                            pc.SearchIndex = null;
                            pc.SearchText = null;
                        }
                        pc.Cc = 0;
                        pc.Row = 0;
                        dataFile = await sendRequest("../Api/ExportTableApi", {
                            Api: url,
                            Req: pc,
                            ExportAs: id,
                            Options: {
                                Filename: tableName,
                            },
                        });
                        break;
                }
                if (dataFile) {
                    if (dataFile.Mime && dataFile.Data) {
                        const del = document.createElement('a');
                        del.setAttribute("href", "data:" + dataFile.Mime + ";base64," + dataFile.Data);
                        del.setAttribute('download', dataFile.Name);
                        del.style.display = 'none';
                        document.body.appendChild(del);
                        del.click();
                        document.body.removeChild(del);
                        await delay(250);
                    } else {
                        if (dataFile.Name) {
                            const str = GetAbsolutePath("../" + dataFile.Name);
                            await ValueFormat.copyToClipboardInfo(str);
                            Open(str);
                        } else {
                            Fail(_TF("Empty data!", "An error message shown when the exported table data is empty"));
                        }
                    }

                } else {
                    Fail(_TF("No data!", "An error message shown when table data exporter didn't generate any data"));
                }
            }
            catch (e) {
                Fail(e);
            }
            close();
        }


        //  Search
        var searchEI = document.createElement("input");
        searchEI.type = "text";
        searchEI.value = state.RequestParams.SearchText ?? "";
        searchEI.placeholder = _TF("Search for..", "A placehodler text for an input field that contains textual keyword used for searching a table of data");
        footer.appendChild(searchEI);
        /**
         * Clears the search without refetching (exposed as `table.ClearSearch`, used by the filter Reset icon):
         * when the input has text, empties it, disables the clear icon and sets `RequestParams.SearchText = ""`.
         * @returns {boolean} True when there was search text to clear.
         */
        function clearSearch() {
            if (!searchEI.value)
                return false;
            searchEI.value = "";
            clearSearchButton.SetEnabled(false);
            state.RequestParams.SearchText = "";
            return true;
        }
        table.ClearSearch = clearSearch;
        const clearSearchButton = new ColorIcon("IconClear", navColor, navSize, navSize,
            _TF("Clear the search text field", "A tool tip description on a button that if clicked will clear an input field that contains a search text"),
            ev => {
                searchEI.value = "";
                clearSearchButton.SetEnabled(false);
                if (state.RequestParams.SearchText) {
                    state.RequestParams.SearchText = "";
                    state.RequestParams.Row = 0;
                    onChangeFn();
                }
            }).SetEnabled(searchEI.value);
        /**
         * Search input change: trims the text, enables the clear icon when non-empty, stores a changed text in
         * `RequestParams.SearchText` (resetting Row to 0) and always calls `onChangeFn()`.
         * @returns {void}
         */
        searchEI.onchange = () => {
            const st = searchEI.value.trim();
            clearSearchButton.SetEnabled(st);
            if (st !== state.RequestParams.SearchText) {
                state.RequestParams.SearchText = st;
                state.RequestParams.Row = 0;
            }
            onChangeFn();
        };
        footer.appendChild(searchEI);
        footer.appendChild(clearSearchButton.Element);


        /** @type {boolean} True until the first response (or failure) has been processed. */
        let first = true;
        //  Update loop

        /** @type {number|null} Pending setTimeout id of the next Update, or null. */
        let timeout = null;
        /** @type {boolean} True while Update runs; serializes updates (others poll with delay(10)). */
        let isUpdating = false;

        /**
         * Forces an immediate refresh: waits while an update is running (polling `delay(10)`), cancels the
         * pending timer and starts `Update()` (not awaited). Returned to callers of addTable.
         * @returns {Promise<void>}
         */
        async function UpdateNow() {
            while (isUpdating) {
                await delay(10);
            }
            if (timeout)
                clearTimeout(timeout);
            timeout = null;
            Update();
        }

        /**
         * One refresh cycle of the update loop:
         * 1. waits for a running update, sets `requestParams.Cc = cc` and `requestParams.Filters` (all filters
         *    with a non-empty Value from all filter sets, or null), POSTs `sendRequest(url, requestParams, didChange)`;
         * 2. null data -> `Fail("No data found" / "No update found")` and retry in 3000 ms;
         * 3. on the first data: when no rows came back and Row > 0, resets Row to 0, saves and requests again;
         *    resolves the table name (data.Title when no name was given) into tableInfo.Name and data.Title;
         * 4. stores `latestData`, enables Home / Prev (Row > 0) and Next (RowCount > Row + Rows.length), calls
         *    `Table.tableRedraw`, schedules `onResizeFn` (auto row count on the first page), clears `didChange`
         *    (redraw errors are only logged);
         * 5. stores `cc = data.Cc`, calls `onFirstLoad` / `onFirstData` once, and reschedules itself after
         *    `data.RefreshRate` ms (<= 0 -> 5000, minimum 100).
         * Exceptions -> `Fail("Failed to fetch data: ...")`, `onFirstLoad` once, retry in 3000 ms.
         * @returns {Promise<void>}
         */
        async function Update()
        {
            while (isUpdating) {
                await delay(10);
            }
            timeout = null;
            isUpdating = true;
            try {
                //  Try to fetch new data
                requestParams.Cc = cc;
                const ofilters = [];
                const sfilters = state.Filters;
                for (let fs = 0; fs < sfilters.length; ++fs) {
                    const filters = sfilters[fs];
                    for (let fi = 0; fi < filters.length; ++fi) {
                        const filter = filters[fi];
                        if (filter.Value == "")
                            continue;
                        ofilters.push(filter);
                    }
                }
                if (ofilters.length > 0) {
                    requestParams.Filters = ofilters;
                } else {
                    requestParams.Filters = null;
                }
                let data = await sendRequest(url, requestParams, didChange);
                if (!data) {
                    if (first)
                        Fail(_TF("No data found", "An error message displayed when no initial data was returned from the server"));
                    else {
                        if (latestData) {
                            Fail(_TF("No update found", "An error message displayed when no update data was returned from the server"));
                        } else {
                            Fail(_TF("No data found", "An error message displayed when no initial data was returned from the server"));
                        }
                    }
                    first = false;
                    timeout = setTimeout(Update, 3000);
                    return;
                }
                if (first) {
                    if ((data.Rows.length <= 0) && (requestParams.Row > 0)) {
                        //  If no data is returned and we're not requesting the first rows, "jump" to the first row
                        requestParams.Row = 0;
                        save();
                        data = await sendRequest(url, requestParams, didChange);
                    }
                    if (useTitleAsName) {
                        const o = data.Title;
                        if (o)
                            tableName = o;
                    }
                    tableInfo.Name = tableName;
                    data.Title = tableName;
                }
                latestData = data;
                first = false;
                //  Redraw the table
                try {
                    //  Update navigation
                    const currentRow = requestParams.Row;
                    homeIcon.SetEnabled(currentRow > 0);
                    prevIcon.SetEnabled(currentRow > 0);
                    const gotCount = requestParams.Row + data.Rows.length;
                    nextIcon.SetEnabled(data.RowCount > gotCount);
                    Table.tableRedraw(table, data, state, onChangeFn, saveFn, didChange);
                    if (state.AutoRowCount && (requestParams.Row == 0))
                        setTimeout(onResizeFn, 0);
                    didChange = false;
                }
                catch (ex2) {
                    console.log("Failed to redraw table: " + ex2);
                }
                //  Get the refresh rate (delay), santizie it.
                cc = data.Cc;
                let refreshRate = data.RefreshRate;
                if (refreshRate <= 0)
                    refreshRate = 5000;
                if (refreshRate < 100)
                    refreshRate = 100;
                if (onFirstLoad) {
                    onFirstLoad();
                    onFirstLoad = null;
                }
                if (onFirstData) {
                    onFirstData(data);
                    onFirstData = null;
                }
                //  Wait for the delay duration (or an abort happens)
                timeout = setTimeout(Update, refreshRate);

            }
            catch (ex) {
                //  If we failed to fetch the data, retry in a second
                Fail(_TF("Failed to fetch data", "An error message displayed when there was an error to fetch table data from the server") + ": " + ex);
                if (onFirstLoad) {
                    onFirstLoad();
                    onFirstLoad = null;
                }
                timeout = setTimeout(Update, 3000);
            }
            finally {
                isUpdating = false;
            }
        }
        Update();
        return { UpdateNow: UpdateNow };

    }


    /**
     * Tells whether table charts are available (SysWeaver.MicroService.ChartJs present). The result of
     * `getRequest("../Api/TableChartAvailable")` (false on error) is cached in the static `Table._CanShowChart`
     * (created on first call).
     * @returns {Promise<boolean>} True when the "Show as chart" header menu item can be offered.
     */
    static async canShowChart() {
        let c = Table._CanShowChart;
        if (typeof c === "boolean")
            return c;
        try {
            c = await getRequest("../Api/TableChartAvailable");
        }
        catch
        {
            c = false;
        }
        Table._CanShowChart = c;
        return c;
    }


}

/**
 * Entry point of explore/table.html (`<body onload="tableMain()">`): a generic viewer for any table API.
 * URL parameters: `q` table API url (required, else Fail), `n` title / table name (sets document.title),
 * `p` string Param, `jp` JavaScript source run as an async function body whose result replaces `p`,
 * `r` rows per page (passed on, unused), `s` initial state JSON, `f` filter JSON.
 * Awaits `Table.addTable(q, null, r, p, removeLoader, n, s, onFirstData, f)` where onFirstData sets
 * `document.title` to `data.Title` when no `n` was given. The loader is removed by onFirstLoad and in `finally`.
 * @returns {Promise<void>} Resolves once the table has been set up.
 */
async function tableMain() {

    const removeLoader = AddLoading();
    try {

        const ps = getUrlParams();
        const url = ps.get('q');
        if (!url) {
            Fail(_TF("No query parameter specified!", "An error message that is shown when a required parameter isn't present"), true);
            return;
        }
        const tit = ps.get('n');
        if (tit)
            document.title = tit;
        let p = ps.get('p');
        let jp = ps.get('jp');
        if (jp) {
            p = await Object.getPrototypeOf(async function () { }).constructor(jp)();
        }
        await Table.addTable(url, null, ps.get('r'), p, removeLoader, tit, ps.get('s'), data => {
            if (tit)
                return;
            const nt = data.Title;
            if (nt)
                document.title = nt;
        }, ps.get('f'));
    }
    finally {
        removeLoader();
    }
}
