

function IntToString(value) {
    var v = "" + value;
    var l = v.length - 3;
    while (l > 0) {
        v = v.substring(0, l) + " " + v.substring(l);
        l -= 3;
    }
    return v;
}

function GetDate(dd) {
    return new Date(dd);
}

function SetByteSize(el, size)
{
    if (typeof size === "string") {
        el.innerText = size;
        return true;
    }
    if (size === null) {
        el.innerText = "-";
        el.title = _TF("Size is not known, this is probably a dynamically generated resource", "A tool tip description of a text field containing the size of a web resources with an unknown size");
        return true;
    }
    el.innerText = IntToString(size);
    var t = _T("Click to copy \"{0}\" to the clipboard.", size, "A tool tip description of a text field that when clicked will copy a text to the clipboard.{0} is replaced with the text that will be copied");
    var kb = (size + 512) >> 10;
    if (kb > 0)
    {
        t += "\n\n" + IntToString(kb) + " kb";
        var mb = (size + 524288) >> 20;
        if (mb > 0)
            t += "\n" + IntToString(mb) + " Mb";
        var gb = (size + 536870912) >> 30;
        if (gb > 0)
            t += "\n" + IntToString(gb) + " Gb";
        el.title = t;
    }
    return false;
}


function SetDateString(el, dd) {
    ValueFormat.updateDateTime(el, dd);
}

function SetCacheDuration(el, dd) {

    if (dd <= 0) {
        el.classList.add("Dimmed");
        el.innerText = "-";
        el.title = _TF("Not cached", "A tool tip descriptions for a text describing that a web resource will never be cached");
        return;
    }
    if (dd > 1500000000) {
        el.innerText = "∞";
        el.title = _TF("Infinite (until the server is restarted)", "A tool tip description of a text that let the user know that a web resource will be cached until the server is restarted");
        return;
    }
    el.innerText = _T("{0} s", IntToString(dd), "A very short text describing how long a web resource will be cached in seconds.{0} will be replaced with the number of seconds");
    el.title = _T("Cached for {0} seconds", dd, "A tool tip descriptions for a text describing how long a web resource will be cached in seconds.{0} will be replaced with the number of seconds");
}


function GetSettings() {
    var s = localStorage.getItem("ExploreSettings");
    try {
        if (!s)
            s = {};
        else
            s = JSON.parse(s);
    }
    catch
    {
        s = {};
    }
    if (!s.Order)
        s.Order = [1, 2];
    if ((!s.Cols) || (s.Cols.length != 10))
        s.Cols = [0, 1, 2, 5, 3, 6, 7, 4, 8, 9];
    return s;
}

function UpdateSettings(s) {
    localStorage.setItem("ExploreSettings", JSON.stringify(s));
}


function GetFileExtension(n, t) {

    if (t)
    {
        if ((t < 2) || (t > 3))
            return "";
    }
    return n.slice((n.lastIndexOf(".") - 1 >>> 0) + 2).toLowerCase();
}

function InsertTableCell(row) {
    const c = document.createElement("th");
    row.replaceChild(c, row.insertCell());
    return c;
}

function DoSort(index, set, data) {
    let order = set.Order;
    let i = order.indexOf(index);
    if (i >= 0) 
        order.splice(i, 1);
    i = order.indexOf(-index);
    if (i >= 0)
        order.splice(i, 1);
    order.unshift(index);
    BuildTable(data, set);
    UpdateSettings(set);
}

function AddSortHeader(data, set, el, index, name, title, extra)
{
    ++index;
    let desc = false;
    let current = false;
    let prio = 0;
    const order = set.Order;
    const orderLen = order.length;
    for (let i = 0; i < orderLen; ++i) {
        let ord = order[i];
        if (ord < 0) {

            ord = -ord;
            if (ord === index) {
                prio = i + 1;
                desc = true;
                current = (i === 0);
                break;
            }
            continue;
        }
        if (ord === index) {
            prio = i + 1;
            current = (i === 0);
            break;
        }
    }
    const flip = current ^ desc;
    const cp = current || (prio <= 0);
    let tit =
        flip
            ?
            (cp
                ?
                _T("Click to sort by {0} in descending order", name, "A tool tip description to a button on a table column that when clicked will sort the table in descending based on that column.{0} is replaced by the title of the column")
                :
                _T("Click to sort by {0} in descending order as priority #1", name, "A tool tip description to a button on a table column that when clicked will sort the table in descending based on that column.{0} is replaced by the title of the column")
            )
            :
            (cp
                ?
                _T("Click to sort by {0} in ascending order", name, "A tool tip description to a button on a table column that when clicked will sort the table in ascending based on that column.{0} is replaced by the title of the column")
                :
                _T("Click to sort by {0} in ascending order as priority #1", name, "A tool tip description to a button on a table column that when clicked will sort the table in ascending based on that column.{0} is replaced by the title of the column")
            );
    if (prio > 0)
        tit += "\n" + _T("Currently at priority #{0}.", prio, "A tool tip description to a table column header, that indicates the sorting priority of that column.{0} is replaced with the priority number, 1, 2, 3 and so on");
    if (extra)
        tit = extra + "\n\n" + tit;
    var click = function (ev) {
        DoSort(flip ? -index : index, set, data);
    };

    const div = document.createElement("div");
    el.appendChild(div);

    const img = document.createElement("img");
    img.src = data.IconBase + (desc ? "m_sort_desc.svg" : "m_sort_asc.svg");
    img.title = tit;
    img.onclick = click;
    if (!current)
        img.classList.add("Dimmed");

    div.appendChild(img);

    if (!title)
        return;
    const s = document.createElement("span");
    s.innerText = title;
    s.title = tit;
    s.onclick = click;
    div.appendChild(s);
}

function BuildNavigator(data, set) {
    const nav = document.createElement("div");
    nav.classList.add("ExploreNavigator");
    nav.classList.add("ThemeBaseFg");
    const href = window.location.href;
    const li = href.indexOf('?');
    const qp = li <= 0 ? "" : href.substring(li);
    document.body.appendChild(nav);
    const parts = data.LocalUrl.split('/');
    const l = parts.length;
    const explore = data.FolderSuffix.substring(1);
    const fname = data.BaseUrl.split('/');
    for (let i = 0; i < l; ++i)
    {
        const value = i == 0 ? _TF("ROOT", "Text shown as the web root in a navigation bar") : parts[i - 1];

        let a;
//        const a = document.createElement("a");
//        a.classList.add("ThemeMainBg");
//        a.innerText = value;
        if ((i + 1) >= l) {
            const depth = l - i - 1;
            a = ValueFormat.createLink("./" + qp, value, "_blank",
                _T('Open "{0}" in a new tab', fname.slice(0, fname.length - depth - 1).join("/") + "/", "Tool tip description of a text field that when clicked will open a web resource in a new browser tab.{0} is replaced with the url to the web resource"));
        } else {
            const depth = l - i - 1;
            a = ValueFormat.createLink("../".repeat(depth) + explore + qp, value, "_self",
                _T('Explore the folder "{0}"', fname.slice(0, fname.length - depth - 1).join("/") + "/", "Tool tip description of a text field that when clicked will explore a web folder.{0} is replaced with the url to the web folder"));
        }
        a.classList.add("Path");
        nav.appendChild(a);
    }



}

function BuildTable(data, set)
{
    const table = document.createElement("table");
    table.classList.add("Explore");
  
    const baseUrl = data.BaseUrl;
    const order = set.Order;
    const orderLen = order.length;
    data.Items.sort(function (a, b) {
        for (let i = 0; i < orderLen; ++i) {
            let ord = order[i];
            const sign = ord < 0 ? -1 : 1;
            ord *= sign;
            switch (ord) {
                case 1:
                //  Type
                    {
                        const d = a.Type - b.Type;
                        if (d != 0)
                            return d * sign;
                    }
                    break;
                case 2:
                //  Name
                    {
                        const ca = a.Name.toLowerCase();
                        const cb = b.Name.toLowerCase();
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 3:
                //  Size
                    {
                        const d = a.Size - b.Size;
                        if (d != 0)
                            return d * sign;
                    }
                    break;
                case 4:
                    //  Extension
                    {
                        const ca = GetFileExtension(a.Name, a.Type);
                        const cb = GetFileExtension(b.Name, b.Type);
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 5:
                    //  Auth
                    {
                        const ca = a.Auth;
                        const cb = b.Auth;
                        if (!ca)
                            return cb ? -sign : sign;
                        if (!cb)
                            return sign;

                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 6:
                    //  Time
                    {
                        const ca = GetDate(a.LastModified);
                        const cb = GetDate(b.LastModified);
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 7:
                    //  Client Cache Duration
                    {
                        const ca = a.ClientCacheDuration;
                        const cb = b.ClientCacheDuration;
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 8:
                    //  Request cache duration
                    {
                        const ca = a.RequestCacheDuration;
                        const cb = b.RequestCacheDuration;
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 9:
                    //  Comp preference
                    {
                        const ca = a.CompPreference;
                        const cb = b.CompPreference;
                        if (!ca)
                            return cb ? -sign : sign;
                        if (!cb)
                            return sign;
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
                case 10:
                    //  PreCompressed
                    {
                        const ca = a.PreCompressed;
                        const cb = b.PreCompressed;
                        if (!ca)
                            return cb ? -sign : sign;
                        if (!cb)
                            return sign;
                        if (ca < cb)
                            return -sign;
                        if (ca > cb)
                            return sign;
                        break;
                    }
            }
        }
        return 0;
    });
    const cols = set.Cols;
    const colCount = cols.length;
    //  Headers

    const row = table.insertRow();
    row.classList.add("ThemeAccBg");
    row.classList.add("ThemeBaseFg");
    let accent = false;
    for (let colIndex = 0; colIndex < colCount; ++colIndex) {
        const col = cols[colIndex];
        switch (col) {
            case 0:
                //  Icon
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreIconH");
                    AddSortHeader(data, set, c, col,
                        _TF("type", "A table column name of a column with web resource types, used in tool tips"),
                        null,
                        _TF("The type of the web resource.", "A table column description of a column with web resource types, used in tool tips")
                    );
                    break;
                }
            case 1:
                //  Name
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreNameH");
                    AddSortHeader(data, set, c, col,
                        _TF("name", "A table column name of a column with web resource names, used in tool tips"),
                        _TF("Name", "A table column title of a column with web resource names, used as column title text"),
                        _TF("Name of the web resource.", "A table column name of a column with web resource names, used in tool tips")
                    );
                }
                break;
            case 2:
                //  Size
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreSizeH");
                    AddSortHeader(data, set, c, col,
                        _TF("size", "A table column name of a column with web resource byte sizes, used in tool tips"),
                        _TF("Size", "A table column title of a column with web resource byte sizes, used as column title text"),
                        _TF("Size of the web resource if applicable", "A table column name of a column with web resource byte sizes, used in tool tips")
                    );
                }
                break;
            case 3:
                //  Extension
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreExtH");
                    AddSortHeader(data, set, c, col,
                        _TF("web resource extension", "A table column name of a column with web resource file extensions, used in tool tips"),
                        _TF("Ext", "A table column title of a column with web resource file extensions, used as column title text"),
                        _TF("Web resource extension if applicable", "A table column name of a column with web resource file extensions, used in tool tips")
                    );
                }
                break;
            case 4:
                //  Auth
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreAuthH");
                    AddSortHeader(data, set, c, col,
                        _TF("required authentication", "A table column name of a column with required web resource authentication tokens, used in tool tips"),
                        _TF("Auth", "A table column title of a column with required web resource authentication tokens, used as column title text"),
                        _TF("Required authentication and/or any required authentication tokens.", "A table column name of a column with required web resource authentication tokens, used in tool tips")
                    );
                }
                break;
            case 5:
                //  Time
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreTimeH");
                    AddSortHeader(data, set, c, col,
                        _TF("last modified time stamp", "A table column name of a column with the last modified web resource time stamps, used in tool tips"),
                        _TF("Time", "A table column title of a column with the last modified web resource time stamps, used as column title text"),
                        _TF("Last modified time stamp.", "A table column name of a column with the last modified web resource time stamps, used in tool tips")
                    );
                }
                break;
            case 6:
                //  Client Cache Duration
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreClientCacheH");
                    AddSortHeader(data, set, c, col,
                        _TF("client cache duration", "A table column name of a column with web resource client caching, used in tool tips"),
                        _TF("Client", "A table column title of a column with web resource client caching, used as column title text"),
                        _TF("The duration is seconds that web clients are instructed to keep the data.", "A table column name of a column with web resource client caching, used in tool tips")
                    );
                }
                break;
            case 7:
                //  Request Cache Duration
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreRequestCacheH");
                    AddSortHeader(data, set, c, col,
                        _TF("server cache duration", "A table column name of a column with web resource server caching, used in tool tips"),
                        _TF("Server", "A table column title of a column with web resource server caching, used as column title text"),
                        _TF("The duration in seconds that the data is cached on the server.", "A table column name of a column with web resource server caching, used in tool tips")
                    );
                }
                break;
            case 8:
                //  CompPreference
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExploreCompPreferenceH");
                    AddSortHeader(data, set, c, col,
                        _TF("compression preference", "A table column name of a column with on-the-fly web resource compression preference, used in tool tips"),
                        _TF("Compression", "A table column title of a column with on-the-fly web resource compression preference, used as column title text"),
                        _TF("The on-the-fly compression to apply, if any.", "A table column name of a column with on-the-fly web resource compression preference, used in tool tips")
                    );
                }
                break;
            case 9:
                //  PreCompressed
                {
                    const c = InsertTableCell(row);
                    c.classList.add("ExplorePreCompressedH");
                    AddSortHeader(data, set, c, col,
                        _TF("stored compression method", "A table column name of a column with the compression method used to store web resources, used in tool tips"),
                        _TF("Stored", "A table column title of a column with the compression method used to store web resources, used as column title text"),
                        _TF("The storage compression method used (if stored compressed).", "A table column name of a column with the compression method used to store web resources, used in tool tips")
                        );
                }
                break;

            default:
                //  Unknown
                {
                    const c = InsertTableCell(row);
                }
                break;
        }
    }

    const href = window.location.href;
    const li = href.indexOf('?');
    const qp = li <= 0 ? "" : href.substring(li);
    const isTopLevel = window.location === window.parent.location;
    //  Items
    data.Items.forEach((item) => {

        const row = table.insertRow();
        let img = null;
        const fname = item.Name;
        let size = "";
        let ext = "";
        const type = item.Type;
        let newWindow = true;
        let isRes = false;
        let link = fname + qp
        const absName = baseUrl + fname;
        const localName = data.LocalUrl + fname;

        let open = "\n\n" + _T('Click to open "{0}"', absName, "A tool tip description on an item that when clicked will open a url.{0} is replaced with the url");
        switch (type) {

            case 0:
                ext = GetFileExtension(fname);
                img = data.ApiIcon;
                size = "[???]";
                isRes = true;
                newWindow = false;
                break;
            case 1:
                img = data.FolderIcon;
                if (!item.Location.startsWith("[Folder]"))
                    img = data.VirtualFolderIcon;
                open = "\n\n" + _T('Click to explore folder "{0}"', absName, "A tool tip description on an item that when clicked will explore the content of a web folder.{0} is replaced with the url of the web folder");
                link = fname + data.FolderSuffix + qp
                size = "<DIR>";
                newWindow = false;
                break;
            case 2:
                ext = GetFileExtension(fname);
                img = data.ExtIconBase + (ext === "" ? "bin" : ext) + ".svg";
                size = item.Size;
                isRes = true;
                break;
            case 3:
                ext = GetFileExtension(fname);
                img = data.ApiIcon;
                link = data.IconBase + '../explore/api.html?q=' + localName;
                open = "\n\n" + _T('Click to explore the API "{0}"', absName, "A tool tip description on an item that when clicked will explore the API of that url.{0} is replaced with the url of the API");
                size = "[API]";
                isRes = true;
                newWindow = false;
                break;
            case 4:
                ext = "";
                img = data.ApiIcon;
                size = "[UP]";
                isRes = true;
                break;
        }
        if (isTopLevel)
            newWindow = false;
        let accent = false;
        for (let colIndex = 0; colIndex < colCount; ++colIndex) {
            const col = cols[colIndex];
            switch (col) {
                case 0:
                    //  Icon
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreIcon");
                        if (img) {
                            //const a = document.createElement("a");
                            //if (newWindow)
                                //a.target = "_blank";
                            //a.href = link;
                            const a = ValueFormat.createLink(link, img, newWindow ? "_blank" : "_self", (item.Mime ?? item.Location) + open, true);
                            //const imgE = document.createElement("img");
                            //imgE.src = img;
                            //imgE.title = (item.Mime ?? item.Location) + open;
                            //a.appendChild(imgE);
                            c.appendChild(a);
                        }
                        break;
                    }
                case 1:
                    //  Name
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreName");
                        /*const a = document.createElement("a");
                        if (newWindow)
                            a.target = "_blank";
                        a.href = link;
                        a.innerText = fname;
                        a.title = item.Location + open;
                        */
                        const a = ValueFormat.createLink(link, fname, newWindow ? "_blank" : "_self", item.Location + open);
                        if (!accent)
                            a.classList.add("ThemeMain");
                        accent = !accent;
                        c.appendChild(a);
                    }
                    break;
                case 2:
                    //  Size
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreSize");
                        if (accent)
                            c.classList.add("ThemeAccFg");
                        accent = !accent;
                        if (SetByteSize(c, size))
                            c.classList.add("Dimmed");
                    }
                    break;
                case 3:
                    //  Extension
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreExt");
                        if (accent)
                            c.classList.add("ThemeAccFg");
                        accent = !accent;
                        if (isRes) {
                            c.innerText = ext;
                            c.title = item.Mime;
                        }
                    }
                    break;
                case 4:
                    //  Auth
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreAuth");
                        c.classList.add("ThemeBackgroundFg");
                        const cn = accent ? "ThemeAccBg" : "ThemeMainBg";
                        accent = !accent;
                        if (isRes) {
                            const wd = document.createElement("div");
                            c.appendChild(wd);
                            const a = item.Auth;
                            const imgE = document.createElement("img");
                            wd.appendChild(imgE);
                            if (a != null) {
                                imgE.src = data.IconBase + "g_protected.svg";
                                const ft = [];
                                if (a !== "") {
                                    const tokens = a.split(",");
                                    tokens.forEach((token) => {
                                        const tt = token.trim();
                                        if (tt.length > 0) {

                                            const s = document.createElement("span");
                                            s.classList.add(cn);
                                            s.innerText = tt;
                                            wd.appendChild(s);
                                            ft.push(tt);
                                        }
                                    });
                                }
                                c.title = ft.length > 0 ?
                                    (_TF("Client must have authorization to access this resource and have one of the following authorization tokens:", "A tool tip description for a list of authorization tokens that are required to user a web resource, a list of token will follow") + "\n" + ft.join("\n"))
                                    :
                                    _TF("Client must have authorization to access this resource.", "A tool tip description explaining that a web resource requires an authorized user to be accessed");
                            } else {
                                imgE.src = data.IconBase + "g_warning.svg";
                                imgE.classList.add("Dimmed");
                                c.title = _TF("Anyone can access this resource.", "A tool tip description explaining that a web resource can be accessed at all times");
                            }
                        }
                    }
                    break;
                case 5:
                    //  Time
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreTime");
                        if (accent)
                            c.classList.add("ThemeAccFg");
                        accent = !accent;
                        SetDateString(c, GetDate(item.LastModified));
                    }
                    break;
                case 6:
                    //  Client cache duration
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreClientCache");
                        if (accent)
                            c.classList.add("ThemeAccFg");
                        accent = !accent;
                        SetCacheDuration(c, item.ClientCacheDuration);
                    }
                    break;
                case 7:
                    //  Request cache duration
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreRequestCache");
                        if (accent)
                            c.classList.add("ThemeAccFg");
                        accent = !accent;
                        SetCacheDuration(c, item.RequestCacheDuration);
                    }
                    break;
                case 8:
                    //  CompPreference
                    {
                        const c = row.insertCell();
                        c.classList.add("ExploreCompPreference");
                        c.classList.add("ThemeBackgroundFg");
                        const cn = accent ? "ThemeAccBg" : "ThemeMainBg";
                        const val = item.CompPreference;
                        if (val) {
                            const wd = document.createElement("div");
                            c.appendChild(wd);
                            const methods = val.split(',');
                            methods.forEach((method) => {
                                const tt = method.trim();
                                if (tt.length > 0)
                                {
                                    const s = document.createElement("span");
                                    const kv = tt.split(':');
                                    s.classList.add(cn);
                                    s.innerText = kv[0];
                                    s.title = _T("{1] quality using {0}-compression.", kv[0], kv[1], "A tool tip description describing the comporession method and quality used by a web resource.{0} is the name of the compession method, ex: Brotli, Zip, Deflate.{1} is the compression quality, ex: Fast, Normal, Best");
                                    wd.appendChild(s);
                                }
                            });
                        }
                        accent = !accent;
                    }
                    break;
                case 9:
                    //  PreCompressed
                    {
                        const c = row.insertCell();
                        c.classList.add("ExplorePreCompressed");
                        const val = item.PreCompressed;
                        c.innerText = val ?? "";
                        accent = !accent;
                    }
                    break;
                default:
                    //  Unknown
                    {
                        const c = row.insertCell();
                    }
                    break;
            }
        }
    });

    const el = document.body;
    const fc = el.getElementsByTagName('table')[0];
    if (fc)
        el.replaceChild(table, fc);
    else
        el.appendChild(table);
}


function Build(data) {
    const closeLoader = AddLoading();
    const set = GetSettings();
    document.body.innerText = "";
    BuildNavigator(data, set);
    BuildTable(data, set);
    closeLoader();
}