


function insertTH(trow) {
    const c = trow.insertCell();
    c.outerHTML = "<th></th>"
    return trow.cells[trow.cells.length - 1];
}

class TableDataProps {
    static CanSort = 1;
    static SortedDesc = 2;
    static Hide = 4;
    static Filter = 8;
    static TextFilter = 16;
    static OrderFilter = 32;
    static CanChart = 64;
    static IsKey = 128;
    static IsPrimaryKey1 = 256;
    static IsPrimaryKey2 = 512;
    static IsPrimaryKey3 = 256 | 512;
    static IsPrimaryKey4 = 1024;
    static IsPrimaryKey5 = 1024 | 256;
    static IsPrimaryKey6 = 1024 | 512;
    static IsPrimaryKey7 = 1024 | 512 | 256;

    //  Computed
    static AnyFilters = TableDataProps.Filter | TableDataProps.TextFilter | TableDataProps.OrderFilter;
    static AnyKey = TableDataProps.IsPrimaryKey7 | TableDataProps.IsKey;
}


class TableDataFilterOps {
    static Equals = 0;
    static NotEqual = 1;
    static LessThan = 2;
    static GreaterThan = 3;
    static LessEqual = 4;
    static GreaterEqual = 5;
    static Contains = 6;
    static StartsWith = 7;
    static EndsWith = 8;
    static AnyOf = 9;
    static NoneOf = 10;
    static InRange = 11;
    static OutsideRange = 12;
    static Count = 13;


    static DefaultOrders = [
        TableDataFilterOps.Contains,
        TableDataFilterOps.Equals,
        TableDataFilterOps.GreaterThan,
    ];


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

class TableDataFilter {

    constructor(colName) {
        this.ColName = colName;
    }

    Invert = false;
    CaseSensitive = false;
    Op = -1;
    Value = "";
    ColName = "";
    ColumnIndex = 0;
}


class TableDataState {

    FilterRows = 1;
    Filters = [];
    Expanded = 1;
    RequestParams = Table.tableDefaultRequest();
    AutoRowCount = true;
}



class Table {



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

    static haveSort(requestParams) {
        const o = requestParams.Order;
        if (!o)
            return false;
        return o.length > 0;
    }


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

    static isReverseSort(requestParams) {
        const o = requestParams.Order;
        if (!o)
            return false;
        if (o.length <= 0)
            return false;
        return o[0].charAt(0) === '-';
    }

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

    static tableUpdateHeader(cols, table, state, onChangeFn) {
        const requestParams = state.RequestParams;
        let sourceIndex = -1;
        let cellIndex = 1;
        const cells = table.rows[0].cells;
        const primaryKey = [];
        const keys = [];
        cols.forEach(col => {
            const primIndex = col.Props & TableDataProps.IsPrimaryKey7;
            if (primIndex > 0)
                primaryKey[(primIndex >> 8) - 1] = col.Name;
            if ((col.Props & TableDataProps.key) !== 0)
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
                            Table.tableUpdateHeader(cols, table, state, onChangeFn);
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
                            Table.tableUpdateHeader(cols, table, state, onChangeFn);
                            onChangeFn();
                        };
                    }
                    icon.ChangeImage(rev ? "IconDescending" : "IconAscending");
                    i.classList.add("IconButton");
                } else {
                    cell.classList.remove("CurrentSort");
                    cell.classList.remove("Sort");
                    cell.classList.add("NoSort");
                    i.classList.remove("IconButton");
                    icon.ChangeImage(0);
                }
                title = ValueFormat.joinNonEmpty("\n\n", col.Desc, title);
                ValueFormat.updateText(s, col.Title, null, null);

                cell.title = ValueFormat.joinNonEmpty("\n\n", col.Desc, _TF("Click to show options", "A tool tip description on a button that if clicked will show some options"));


                const contextMenu = async ev => {
                    if (badClick(ev))
                        return true;
                    const checked = 2;
                    const readonly = 1;
                    const checkedReadonly = checked | readonly;
                    const canShowChart = await Table.canShowChart();
                    PopUpMenu(icon.Element, (close, backEl) => {

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
                                Name: _TF("Sort asceneding", "The text of a menu item on a table column header that if clicked will sort the table in asceneding order based of that column"),
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
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn);
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
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn);
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
                                    Table.tableUpdateHeader(cols, table, state, onChangeFn);
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



                        return menu;
                    });
                    return false;
                };

                cell.onclick = contextMenu;
                cell.oncontextmenu = contextMenu;



                i.onclick = onClickFn;
                i.title = title;

                //i.title = title;
                //i.onclick = onClickFn;
                //s.onclick = onClickFn;
                ++cellIndex;
            }
        });
    }

    static addFilterRow(table, state, rowIndex, columnCount, onChangeFn, saveFn, cols) {
        const iconSize = 23;
        const requestParams = state.RequestParams;
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


                if (rowIndex == 1) {
                    const defIndex = TableDataFilterOps.Contains;
                    const icon = new ColorIcon("IconReset", "IconColorThemeAcc1", iconSize, iconSize,
                        _TF("Click to reset all filter values and parameters to their default (no filtering).", "A tool tip description on a button that if clicked will reset all paramaters to default")
                        , ev => {
                        if (!isPureClick(ev))
                            return;
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
                        changed &= fchanged;
                        while (state.FilterRows > 1)
                            changed |= deleteFilters(rowFirst + 3);
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
                        if (!isPureClick(ev))
                            return;
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
                    if (!isPureClick(ev))
                        return;
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
                    if (!isPureClick(ev))
                        return;
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
                const setInvert = function () {
                    const e = filter.Invert;
                    invert.innerText = e ? "☑ !" : "☐ !";
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
                const setCaseSens = function () {
                    const e = filter.CaseSensitive;
                    caseSens.innerText = e ? "☑ A ≠ a" : "☐ A ≠ a";
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
                    invert.onclick = ev => {
                        if (!isPureClick(ev))
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
                    caseSens.onclick = ev => {
                        if (!isPureClick(ev))
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
                        if (!isPureClick(ev))
                            return;
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
                        const icon = new ColorIcon("", "IconColorThemeBackground", 16, 16);
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
            Table.tableUpdateHeader(cols, table, state, onChangeFn);
        } else {
            cols = table["Cols"];
        }
        trowCount = trows.length;
        rowIndex = 1 + state.FilterRows * 3;
        const colCount = cols.length;
        let dataRow = requestParams.Row + 1;
        const drows = data.Rows;
        drows.forEach(drow => {
            const trow = trowCount <= rowIndex ? table.insertRow() : trows[rowIndex];
            const er = trow["DataRow"];
            const flash = (er && (er === dataRow)) ? flashClass : null;
            const cells = trow.cells;
            const cellCount = cells.length;
            let cellIndex = 0;
            const cell = cellIndex < cellCount ? cells[cellIndex] : trow.insertCell();
            ++cellIndex;
            if (cell["Value"] != dataRow) {
                cell["Value"] = dataRow;
                if (ValueFormat.updateText(cell, dataRow))
                    cell.classList.add("Right");
            }
            const dvalues = drow.Values;
            trow["DataRow"] = dataRow;
            for (let i = 0; i < colCount; ++i) {
                const col = cols[i];
                if ((col.Props & TableDataProps.Hide) !== 0)
                    continue;
                const value = dvalues[i];
                const cell = cellIndex < cellCount ? cells[cellIndex] : trow.insertCell();
                if (!newCol) {
                    if (cell["Value"] == value)
                    {
                        ++cellIndex;
                        continue;
                    }
                }
                const next = (i + 1) < colCount ? dvalues[i + 1] : undefined;
                cell["Value"] = value;
                ValueFormat.update(cell, col.Type, value, flash, col.Format, next, onChangeFn);
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

    static tableDefaultRequest() {
        return {
            Cc: 0,
            Row: 0,
            MaxRowCount: 25,
            LookAheadCount: 1,
            Order: null,
        };
    }

    static tableDefaultState() {
        return new TableDataState();
    }


    static async getTableExportMenu() {
        let m = Table.MenuExportItems;
        if (m)
            return m;
        const current = Table.CS;
        await Promise.all([
            await includeJs(current, "../app/application.js"),
            await includeCss(current, "../app/application.css")
        ]);
        m = await getRequest("../Api/GetTableExporters");
        Table.MenuExportItems = m;
        return m;
    }

    static CS = document.currentScript.src;
    static MenuExportItems = null;

    static async addTable(url, toElement, rowsPerPage, tableParams, onFirstLoad, tableName, stateStr, onFirstData, filter) {
        if (!toElement)
            toElement = document.body;
        if (!rowsPerPage)
            rowsPerPage = 25;
        //  Get request params, define save function
        const key = "SysWeaver.Table." + await hashString(url);
        const useTitleAsName = !tableName;

        if (useTitleAsName) {
            const tt = url.split('/');
            tableName = tt[tt.length - 1];
        }

        const tableInfo =
        {
            Name: tableName,
            Url: url,
        };

        let state = stateStr ?? localStorage.getItem(key);

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
        let cc = 0;
        const aborter = new AbortHandler();
        let didChange = false;
        const onChangeFn = () => {
            //console.log("Request changed! Sort: " + requestParams.SortCol + (requestParams.SortReverse ? " revered" : ""));
            aborter.raise();
            save();
            didChange = true;
        }

        //  Define on resize chnage

        const onResizeFn = ev => {
            const startRow = 1 + state.FilterRows * 3;
            const rows = table.rows;
            const rowC = rows.length;
            if (startRow < rowC) {
                let dataHeight = 0;
                for (let i = startRow; i < rowC; ++i)
                    dataHeight += rows[i].offsetHeight;
                const rowHeight = Math.ceil(dataHeight / (rowC - startRow));
                let windowHeight = footer.offsetTop - table.offsetTop - 16;
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
        const navColor = "IconColorThemeBackground";
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

        let latestData = null;
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


        let first = true;
        //  Update loop
        for (; ;) {
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
                    await delayWithAbort(3000, aborter);
                    continue;
                }
                if (first) {
                    if ((data.Rows.length <= 0) && (requestParams.Row > 0)) {
                        //  If no data is returned and we're not requesting the first rows, "jump" to the first row
                        requestParams.Row = 0;
                        save();
                        data = await sendRequest(url, requestParams, didChange);
                    }
                    if (useTitleAsName) {
                        if (data.Title) {
                            tableName = data.Title;
                            tableInfo.Name = tableName;
                        }
                    }
                }
                latestData = data;
                first = false;
                //  Redraw the table
                try {
                    //  Update navigation
                    const currentRow = requestParams.Row;
                    homeIcon.SetEnabled(currentRow > 0);
                    prevIcon.SetEnabled(currentRow > 0);
                    nextIcon.SetEnabled(data.RowCount > requestParams.MaxRowCount);
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
                await delayWithAbort(refreshRate, aborter);
            }
            catch (ex) {
                //  If we failed to fetch the data, retry in a second
                Fail(_TF("Failed to fetch data", "An error message displayed when there was an error to fetch table data from the server") + ": " + ex);
                if (onFirstLoad) {
                    onFirstLoad();
                    onFirstLoad = null;
                }
                await delayWithAbort(3000, aborter);
            }
        }
    }


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
        let p = ps.get('p');
        let jp = ps.get('jp');
        if (jp) {
            p = await Object.getPrototypeOf(async function () { }).constructor(jp)();
        }
        await Table.addTable(url, null, ps.get('r'), p, removeLoader, tit, ps.get('s'), data =>
        {
            const nt = data.Title;
            if (tit)
                document.title = tit;
            else
                if (nt)
                    document.title = nt;
        }, ps.get('f'));
    }
    finally {
        removeLoader();
    }
}
