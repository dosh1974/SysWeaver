


class WebMenu {

    RootUri = "";
    Items = [];
    Name = "";

    static From(obj, name) {
        const m = Object.assign(new WebMenu(), obj);
        const c = m.Items;
        if (!c)
            return m;
        const cl = c.length;
        if (cl <= 0)
            return m;
        for (let i = 0; i < cl; ++i)
            c[i] = WebMenuItem.From(c[i]);
        if (name)
            m.Name = name;
        return m;
    }

}


class WebMenuItem {
    // Id of the item
    Id = "";
    // Name of the item
    Name = ""
    // The type of the item
    Type = "";
    // Title (tool tip)
    Title = "";
    // Class name for an icon
    IconClass = "";
    // Data (type dependent, typically an url).
    Data = "";
    // Optional child items
    Children = null;
    // 1 = Disabled, 2 = Checked
    Flags = 0;

    static From(obj) {
        const m = Object.assign(new WebMenuItem(), obj);
        const c = m.Children;
        if (!c)
            return m;
        const cl = c.length;
        if (cl <= 0)
            return m;
        for (let i = 0; i < cl; ++i)
            c[i] = WebMenuItem.From(c[i]);  
        return m;
    }

}


class MainMenuStyle {

    IconWidth = 32;
    IconHeight = 32;

    ExpandIconWidth = 20;
    ExpandIconHeight = 20;
    ExpandTab = 16;

    IconColorClass = "IconColorThemeBackground";
    IconSelColorClass = "IconColorThemeAcc1";
    IconExpander = "IconCollapse,IconExpand";
    MenuStyle = "DefaultMenu";
    Content = null;
    SelectFn = null;
    ExternalContentFn = null;
    HideFn = null;


}

class MainMenu {



    static SetIcon(icon, iconClasses) {
        if (!iconClasses)
            return;
        const x = iconClasses.split(',');
        icon.ChangeImage(x[0].trim());
    }

    static SetExpIcon(icon, isExpanded, iconClasses) {
        if (!iconClasses)
            return;
        const x = iconClasses.split(',');
        if (x.length < 2) {
            icon.ChangeImage(x[0].trim());
            return;
        }
        icon.ChangeImage(x[isExpanded ? 0 : 1].trim());
        icon.SetTitle(isExpanded ?
            _TF("Click to collapse", "The tool tip description of a button on a menu row that when pressed will hide the sub menu (child) items")
            :
            _TF("Click to expand", "The tool tip description of a button on a menu row that when pressed will show the sub menu (child) items")
        );
    }


    constructor(menu, style, toElement) {
        style = style ?? new MainMenuStyle();
        this.Data = menu;
        this.Style = style;
        const c = menu.Items;
        if (!c)
            return;
        const cl = c.length;
        if (cl <= 0)
            return;

        let name = menu.Name;
        if ((!name) || (name.length <= 0))
            name = "NoName";
        this.ExpKey = "SysWeaver.MenuCollapsed." + name + ".";

        const e = document.createElement("table");
        e.setAttribute("cellspacing", "0");
        e.setAttribute("cellpadding", "0");
        for (let i = 0; i < cl; ++i)
            this.Add(e, c[i], 0, style);
        this.Element = e;
        if (toElement) {
            toElement.classList.add(style.MenuStyle);
            toElement.appendChild(e);
        }
    }

    

    LoaderRemoved(style) {
        const content = style.Content;
        if (!content)
            return;
        const prevIFrame = content.lastElementChild;
        if (prevIFrame && (prevIFrame.tagName === "IFRAME")) {
            if (prevIFrame.DoRemove) {
                prevIFrame.DoRemove();
                //console.log("Loader done!");
            }
        }
    }


    static GetEmbeddedUrl(content) {
        if (!content)
            return null;
        const le = content.lastElementChild;
        if (!le)
            return null;
        if (le.tagName !== "IFRAME")
            return null;
        try {
            const cv = le.contentWindow;
            return cv.location.href;
        }
        catch
        {
        }
        return le.src;
    }

    EmbeddContent(url, style, noNewState)
    {
        const content = style.Content;
        if (!content)
            return;
        url = GetAbsolutePath(url);
        if (!noNewState)
            ClearOtherHistoryState(url);

        const fn = style.ExternalContentFn;
        if (fn)
            fn();

        const prevIFrame = content.firstElementChild;
        if (prevIFrame && (prevIFrame.tagName === "IFRAME")){

            prevIFrame.classList.add("Hidden");
            setTimeout(() => prevIFrame.remove(), 25);
        } else {
            content.innerText = "";
        }
        const e = document.createElement("iframe");
        e.classList.add("Hidden");
        e.setAttribute("frameborder", "0");
        e.setAttribute("allowtransparency", "true");
        e.DoRemove = () => {
            const cwin = e.contentWindow;
            let newUrl = null;
            let title;
            if (cwin) {
                const loc = cwin.location;
                newUrl = loc.href;
                console.log("Iframe loaded: " + newUrl);

                try {
                    const bod = cwin.document.getElementsByTagName('body')[0];
                    bod.classList.add("Embedded");
                }
                catch
                {
                }


                const hrow = this.RowMap.get(newUrl);
                title = loc.pathname;
                if (hrow) {
                    title = hrow["MenuItem"].Name;
                    this.Select(hrow);
                }
                else {
                    try {
                        title = cwin.document.title;
                    }
                    catch
                    {
                    }
                    this.Select(null);
                }
            }
            if (e.RemoveTimer)
                clearTimeout(e.RemoveTimer);
            e.RemoveTimer = null;
            e.DoRemove = null;
            e.classList.remove("Hidden");
            if (fn && newUrl)
                fn(newUrl, title);
        };
        e.onload = async ev => {
            e.RemoveTimer = setTimeout(() => {
                if (e.DoRemove)
                    e.DoRemove();
                //console.log("Timeout done!");
            }, 1000);
        };
        if (!noNewState)
            if (window.SysWeaverDoneFirst)
                this.SetUrl(url);
        window.SysWeaverDoneFirst = true;
        e.src = appendTheme(url);
        content.appendChild(e);
    }

    Data = null;
    Style = null;
    Selected = null;
    SelTitle = null;
    RowMap = new Map();

    Select(row)
    {
        const s = this.Selected;
        const style = this.Style;
        if (s) {
            s.classList.remove("MenuSel");
            s.title = this.SelTitle;
            const i = s["MenuIcon"];
            if (i) {
                i.SetEnabled(true);
                i.ChangeColor(style.IconColorClass);
            }

        }
        this.Selected = row;
        let data = null;
        if (row) {
            row.classList.add("MenuSel");
            this.SelTitle = row.title;
            row.title = "";
            const i = row["MenuIcon"];
            if (i) {
                i.SetEnabled(false);
                i.ChangeColor(style.IconSelColorClass);
            }
            data = row["MenuItem"];
        }
        
        const f = style.SelectFn;
        if (f)
            f(data);
    }

    SetUrl(key) {
        const n = new URL(window.location);
        n.hash = Base64EncodeString(key);
//        window.location.replace(n);
        window.history.pushState(key, "", n);
    }

    ReplaceUrl(key) {
        const n = new URL(window.location);
        n.hash = Base64EncodeString(key);
        //        window.location.replace(n);
        window.history.replaceState(key, "", n);
    }

    Add(toElement, data, depth, style)
    {
        let expId = data.Id;
        if ((!expId) || (expId.length <= 0))
            expId = data.Name;
        const expKey = this.ExpKey + expId;

        const rowData = data;
        const hrow = toElement.insertRow();
        let title = data.Title;
        hrow.classList.add("MenuData");
        hrow["MenuItem"] = rowData;
        hrow.title = title;

        const spacing = hrow.insertCell();
        const spaceWidth = (depth * style.ExpandTab + 6) + "px";
        spacing.style.width = spaceWidth;
        spacing.style.minWidth = spaceWidth;

        const exp = hrow.insertCell();
        const name = hrow.insertCell();
        name.innerText = data.Name;
        const icon = hrow.insertCell();
        const flags = data.Flags ?? 0;

        const c = data.Children;
        const cl = c ? c.length : 0;
        const canExpand = cl > 0;

        if (canExpand) {
            hrow.classList.add("SubMenu");
        }

        const disabled = (flags & 1) !== 0;
        const checked = (flags & 2) !== 0;

        let onclick = null;
        let url = null;
        const typeData = data.Data;
        if (typeof typeData === "function") {
            onclick = async ev => {
                if (badClick(ev))
                    return;
                try {
                    if (await typeData(data, style, !!ev)) {
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    }
                }
                catch (ex) {
                    console.warn("Menu: Can't run function, error: " + ex);
                }
            }

        } else {
            switch (data.Type) {
                case 1:
                    url = "explore/table.html?q=../" + typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    };
                    break;
                case 2:
                    url = typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    }
                    break;
                case 3:
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                        window.location.href = typeData;
                    }
                    break;
                case 4:
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                        window.open(typeData);
                    }
                    break;
                case 5:
                    onclick = async ev => {
                        if (badClick(ev))
                            return;
                        try {
                            const fn = window[typeData];
                            if (typeof fn === "function") {
                                if (await fn(data, style, !!ev)) {
                                    const fn = style.HideFn;
                                    if (fn)
                                        fn();
                                }
                            } else {
                                if (eval(typeData)) {
                                    const fn = style.HideFn;
                                    if (fn)
                                        fn();
                                }
                            }
                        }
                        catch (ex) {
                            console.warn("Menu: Can't evalute js '" + typeData + "', error: " + ex);
                        }
                    }
                    break;
                case 6:
                    url = "chart/chart.html?q=../" + typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    };
                    break;
            }
        }
        if (url) {
            if (url.indexOf("://") < 0) {
                const uri = window.location;
                let p = uri.pathname;
                if (!p.endsWith("/"))
                    p = p.substring(0, p.lastIndexOf('/') + 1);
                url = uri.origin + p + url;
            }
            this.RowMap.set(url, hrow);
        }

        const irow = canExpand ? toElement.insertRow() : null;

        const expIconClass = style.IconExpander;
        //  Force icon for expandable
        let iconClass = data.IconClass;
        const useExpIcon = (!iconClass) && (!onclick) && (canExpand);
        if (useExpIcon)
            iconClass = expIconClass;

        const expandFn = canExpand ? function (ev)
        {
            if (badClick(ev))
                return;
            if (ev)
                ev.stopPropagation();
            irow.classList.toggle("MenuHide");
            const isExpanded = !irow.classList.contains("MenuHide");
            if (isExpanded) {
                hrow.classList.remove("SubMenuHide");
                localStorage.removeItem(expKey);
            } else {
                hrow.classList.add("SubMenuHide");
                localStorage.setItem(expKey, "1");
            }
            const eic = icon["MenuIcon"];
            if (eic)
                MainMenu.SetExpIcon(eic, isExpanded, iconClass);
            const eic2 = exp["MenuIcon"];
            if (eic2)
                MainMenu.SetExpIcon(eic2, isExpanded, expIconClass);
        } : null;

        if (!onclick)
            onclick = expandFn;
   
        if (disabled) {
            hrow.classList.add("MenuDisabled");
            onclick = null;
        }
        if (checked)
            hrow.classList.add("MenuChecked");

        data.OnClick = onclick;
        hrow.onclick = onclick;
        if (onclick)
            hrow.classList.add("MenuClick");

        let iconH = null;
        const iconWidth = "calc(var(--ThemeIconSize)*" + style.IconWidth + "px)";
        if (iconClass) {
            iconH = new ColorIcon(null, style.IconColorClass, style.IconWidth, style.IconHeight, title, onclick);
            MainMenu.SetIcon(iconH, iconClass)
            icon.appendChild(iconH.Element);
            icon.style.width = iconWidth;
            icon.style.minWidth = iconWidth;
            icon["MenuIcon"] = iconH;
            hrow["MenuIcon"] = iconH;
        } 

        exp.style.width = (style.IconWidth / 2) + "px";

        if (!canExpand)
            return;


        const isExpanded = localStorage.getItem(expKey) !== "1";
        if (!isExpanded) {
            hrow.classList.add("SubMenuHide");
            irow.classList.add("MenuHide");
        }
        MainMenu.SetExpIcon(iconH, isExpanded, iconClass)
        const iconE = new ColorIcon("", style.IconColorClass, style.ExpandIconWidth, style.ExpandIconHeight, null, expandFn);
        MainMenu.SetExpIcon(iconE, isExpanded, expIconClass);
        const expIconWidth = style.ExpandIconWidth + "px";
        exp.style.width = expIconWidth;
        exp.style.minWidth = expIconWidth;
        exp["MenuIcon"] = iconE;
        exp.appendChild(iconE.Element);
//        irow.insertCell();
        const items = irow.insertCell();
        items.setAttribute("colspan", "4");
        items.classList.add("MenuNested");

        const nt = document.createElement("table");
        nt.setAttribute("cellspacing", "0");
        nt.setAttribute("cellpadding", "0");
        items.appendChild(nt);
        const nextDepth = depth + 1;
        for (let i = 0; i < cl; ++i)
            this.Add(nt, c[i], nextDepth, style);
    }


}


function CreateTabBlock() {
    const e = document.createElement("SysWeaver-TabBlock");
    e.tabIndex = "0";
    return e;
}

// Prevents tabbing outside of this element (useful for pop-ups), should call UnblockTab when element is deleted
function BlockTab(element) {
    let tabBlockers = window.TabBlockers;
    if (typeof tabBlockers === "undefined") {
        tabBlockers = [];
        window.TabBlockers = tabBlockers;
        console.log("Creating tab block hook");
        let prevFocus = null;
        const tabBlockFn = ev => {

            const l = tabBlockers.length;
            if (l <= 0)
                return;
            const block = tabBlockers[l - 1][0];
            if (!IsAttached(block)) {
                console.warn("Element " + block + " is blocking tabs, but isn't attached, unblocking tabs!");
                UnblockTab(block);
                return;
            }
            let e = ev.target;
            if (e === block) {
                console.log("Preventing shift-tab");
                if (prevFocus)
                    prevFocus.focus();
                return;
            }
            if (e.tagName === "SYSWEAVER-TABBLOCK") {
                console.log("Preventing tab (block)");
                if (prevFocus)
                    prevFocus.focus();
                return;
            }
            while (e) {
                if (e === block) {
                    prevFocus = ev.target;
                    return;
                }
                e = e.parentElement;
            }
            console.log("Preventing tab");
            if (prevFocus)
                prevFocus.focus();
            else
                block.focus();
        }
        document.body.addEventListener("focus", tabBlockFn, true);
        window.TabBlockFn = tabBlockFn;
    }
    console.log("Adding tab block");
    tabBlockers.push([element, document.activeElement]);
}

function UnblockTab(element) {
    console.log("Removing tab block");
    const tabBlockers = window.TabBlockers;
    if (typeof tabBlockers === "undefined")
        return;
    const l = tabBlockers.length;
    let i = l;
    while (i > 0) {
        --i;
        const tb = tabBlockers[i];
        if (tb[0] === element) {
            tabBlockers.splice(i, 1);
            if (l === 1) {
                console.log("Deleting tab block hook");
                document.body.removeEventListener("focus", window.TabBlockFn, true);
                delete window.TabBlockFn;
                delete window.TabBlockers;
            }
            if ((i + 1) === l) {
                const cFocus = tb[1];
                if (cFocus) {
                    if (IsAttached(cFocus)) {
                        console.log("Restoring foucus");
                        cFocus.focus();
                    }
                }
            }
            return;
        }
    }
}

/**
 * Creates a "blocker" element that captures all clicks, dims the background etc.
 * Returns [blocker element, async close function]
 * @param {boolean} dontAllowClose If true, the blocker can't be removed be pressing 'Esc'.
 * @param {function():Promise} onClose Optional async function that get's called when closed.
 * @returns {array} [0] = The HTMLElement of the blocker, [1] = an async function that when called removes the blocker.
 */
function CreatePopUpBlocker(dontAllowClose, onClose) {
    const block = document.createElement("SysWeaver-Blocker");

    BlockTab(block);


    async function Close() {
        if (onClose)
            await onClose();
        UnblockTab(block);
        block.remove();
        const endFn = Close.EndFn;
        if (endFn)
            endFn();
    }

    block.tabIndex = "0";

    let gotClick = false;
    block.onmousedown = ev => {
        gotClick = false;
        if (badClick(ev, true))
            return;
        gotClick = ev.target === block;
    };

    block.onmouseup = ev => {
        if (!gotClick)
            return;
        gotClick = false;
        if (ev.target !== block)
            return;
        if (badClick(ev))
            return;
        if (!dontAllowClose)
            Close();
    };


    block.onkeydown = ev => {
        ev.stopPropagation();
        if (isPureClick(ev)) {
            if (ev.key === "Escape") {
                if (!dontAllowClose) {
                    ev.preventDefault();
                    Close();
                }
            }
        }
    };

    return [block, Close];
}


// Creates a "blocker" element that captures all clicks, dims the background etc.
// Position of the popup element is by default at the normal flow position.
// The position can be adjusted, like: dx = widthScale * element.width + offsetX, dy = heightScale * element.height + offsetY.
// Returns [popup element (build on this), async close function, blocker element]
function CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose) {
    if (typeof widthScale !== "number")
        widthScale = 0;
    if (typeof heightScale !== "number")
        heightScale = 0;
    if (typeof offsetX !== "number")
        offsetX = 0;
    if (typeof offsetY !== "number")
        offsetY = 0;
        
    const popupMenuElement = document.createElement("SysWeaver-PopupMenu");


    function setPos() {

        const rect = element.getBoundingClientRect();
        const dx = rect.width * widthScale + offsetX + rect.x;
        const dy = rect.height * heightScale + offsetY + rect.y;
        popupMenuElement.style.left = dx + "px";
        popupMenuElement.style.top = dy + "px";
    }

    setPos();
    //new ResizeObserver(setPos).observe(document.body);


    /*
    const s = window.getComputedStyle(element);
    const disp = s.getPropertyValue("position");
    if (disp == "absolute") {
        const dx = parseInt(s.getPropertyValue("width").split('p')[0]) * -0.25;
        const dy = parseInt(s.getPropertyValue("height").split('p')[0]) + 8;
        popupMenuElement.style.left = dx + "px";
        popupMenuElement.style.top = dy + "px";
    }
    */
    const bec = CreatePopUpBlocker(dontAllowClose, onClose);
    const block = bec[0];
    const close = bec[1];
    block.appendChild(popupMenuElement);
    //popupMenuElement.appendChild(block);
    return [popupMenuElement, close, block];
}

function PopUpMenu(element, build, rightAlign, bottomAlign) {

/*
    const scaleX = rightAlign ? -0.25 : -0.75;
    const scaleY = bottomAlign ? 0 : 1;
    const offsetY = bottomAlign ? -8 : 8;

    const bec = CreatePopUpAtElement(element, scaleX, scaleY, 0, offsetY);
    const popupMenuElement = bec[0];
    const close = bec[1];
    const block = bec[2];

    const popupMenuBackElement = document.createElement("SysWeaver-PopupMenuBack");
    popupMenuElement.appendChild(popupMenuBackElement);
    if (rightAlign)
        popupMenuBackElement.classList.add("Right");
*/

    PopUpElementMenu(element, (popupMenuBackElement, close) =>
    {


        popupMenuBackElement.classList.add("DefaultMenu");

        const menuStyle = new MainMenuStyle();
        const appContent = document.getElementsByTagName("app-content")[0];
        const appHeaderMiddle = document.getElementsByTagName("app-ContentTitle")[0];
        menuStyle.HideFn = close;
        menuStyle.Content = appContent;
        menuStyle.ExternalContentFn = (url, title) => {
            if (!title) {
                appHeaderMiddle.classList.add("Hidden");
            } else {
                appHeaderMiddle.classList.remove("Hidden");
                appHeaderMiddle.innerText = title;
                document.title = window.SysWeaverAppTitle + " - " + title;
            }
            contentUrl = url;
        };
        const menu = build(close, popupMenuBackElement);
        const popupMenu = new MainMenu(menu, menuStyle, popupMenuBackElement);
    }, false, false, null, rightAlign, bottomAlign);

}


// build is async and takes 2 arguments, first is the target element (add html there), second is a function that when called closes the pop up.
// The position can be adjusted, like: dx = widthScale * element.width + offsetX, dy = heightScale * element.height + offsetY.
/**
 * Create a visual pop-up that blocks input behind, positioned and tracked using some element as anchor.
 * The position can be adjusted, like: dx = widthScale * element.width + offsetX, dy = heightScale * element.height + offsetY.
 * @param {HTMLElement} element The element used to position the pop-up.
 * @param {function(HTMLElement, function():Promise<void>):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution.
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {number} widthScale Part of the horizontal offset of the anchor point, as in a fraction of the element width (offsetX += widthScale * element.width).
 * @param {number} heightScale Part of the vertical offset of the anchor point, as in a fraction of the element height (offsetY += heightScale * element.height).
 * @param {number} offsetX The horizontal offset of the anchor point in pixels.
 * @param {number} offsetY The vertical offset of the anchor point in pixels.
 */
async function PopUpElement(element, build, blockUntilClosed, dontAllowClose, onClose, widthScale, heightScale, offsetX, offsetY) {

    const bec = CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose);
    const page = bec[0];
    const close = bec[1];
    const block = bec[2];
    page.Close = close;
    new ResizeObserver(function () {
        if (page.scrollHeight > page.clientHeight)
            block.classList.add("Scroll");
        else
            block.classList.remove("Scroll");
    }).observe(page)

    document.body.appendChild(block);
    block.focus();

    await build(page, close);

    page.appendChild(CreateTabBlock());
    if (blockUntilClosed) {
        await waitFor(async endUsing => {
            close.EndFn = endUsing;
        });
    }
}



/**
 * Create a visual pop-up that blocks input behind, positioned and tracked using some element as anchor.
 * Useful for context menues etc.
 * @param {HTMLElement} element The element used to position the pop-up.
 * @param {function(HTMLElement, function():Promise<void>):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution.
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {boolean} rightAlign If true the pop-up is right aligned (else it left aligned).
 * @param {boolean} bottomAlign If true the pop-up is bottom aligned (else it top aligned).
 */
async function PopUpElementMenu(element, build, blockUntilClosed, dontAllowClose, onClose, rightAlign, bottomAlign) {

    const scaleX = rightAlign ? -0.25 : -0.75;
    const scaleY = bottomAlign ? 0 : 1;
    const offsetY = bottomAlign ? -8 : 8;
    await PopUpElement(element, async (toE, close) => {
        const popupMenuBackElement = document.createElement("SysWeaver-PopupMenuBack");
        toE.appendChild(popupMenuBackElement);
        if (rightAlign)
            popupMenuBackElement.classList.add("Right");
        popupMenuBackElement.Close = close;
        await build(popupMenuBackElement, close);
    }, blockUntilClosed, dontAllowClose, onClose, scaleX, scaleY, 0, offsetY);
}




/**
 * Create a visual pop-up that blocks input behind.
 * @param {function(HTMLElement, function():Promise<void>, HTMLElement):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution. Third argument is the container for the close button, other button may be added here (can be null).
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {boolean} haveCloseButton If true, a close button will be visible (unless dontAllowClose is true).
 * @returns {function():Promise<void>} The async function to use for closing the pop-up (only usefull when blockUntilClosed is false).
 */
async function PopUp(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton) {

    if (typeof haveCloseButton === "undefined")
        haveCloseButton = !dontAllowClose;

    const bec = CreatePopUpBlocker(dontAllowClose, onClose);
    const block = bec[0];
    const Close = bec[1];


    const page = document.createElement("SysWeaver-PopUpBackground");
    page.onclick = ev => ev.stopPropagation();
    page.Close = Close;
    page.Block = block;
    page.MakeFullWidth = () => block.classList.add("FullWidth");
    page.MakeMaxHeight = () => block.classList.add("MaxHeight");
    block.appendChild(page);
    /*
    if (haveCloseButton) {
        const blockClose = document.createElement("SysWeaver-BlockerClose");
        blockClose.onclick = ev => ev.stopPropagation();
        block.appendChild(blockClose);
        const cb = document.createElement("SysWeaver-BlockerCloseButton");
        blockClose.appendChild(cb);
        const closeButton = new ColorIcon("IconClosePopUp", "IconColorThemeAcc1", 32, 32, "Click to close pop-up", Close);
        cb.appendChild(closeButton.Element);
    }*/

    new ResizeObserver(function () {
        if (page.scrollHeight > page.clientHeight)
            block.classList.add("Scroll");
        else
            block.classList.remove("Scroll");
    }).observe(page)

    document.body.appendChild(block);
    const cb = haveCloseButton ? document.createElement("SysWeaver-BlockerCloseButton") : null;
    await build(page, Close, cb);

    page.appendChild(CreateTabBlock());

    if (haveCloseButton) {
        page.appendChild(cb);
        const closeButton = new ColorIcon("IconClosePopUp", "IconColorThemeAcc1", 32, 32,
            _TF("Click to close pop-up", "The tool tip description on a button that when pressed will close a pop-up dialog"),
            Close);
        cb.appendChild(closeButton.Element);
    }



    if (blockUntilClosed) {
        await waitFor(async endUsing => {
            Close.EndFn = endUsing;
        });
    } else {
        return Close;
    }
}


async function PopUpWorking(title, text, iconClass, iconWidth, iconHeight) {
    return await PopUp(el => {
        if (title) {
            const t = document.createElement("SysWeaver-PopUpWorkingTitle");
            t.innerText = title;
            el.appendChild(t);
        }
        {
            const i = document.createElement("SysWeaver-PopUpWorkingIcon");
            el.appendChild(i);
            const ci = new ColorIcon("IconWorking", "IconColorThemeMain", 256, 256);
            i.appendChild(ci.Element);
        }
        if (text) {
            const t = document.createElement("SysWeaver-PopUpWorkingText");
            t.innerText = text;
            el.appendChild(t);
        }
        if (iconClass) {
            const i = document.createElement("SysWeaver-PopUpWorkingIcon");
            el.appendChild(i);
            if (!iconWidth)
                iconWidth = 64;
            if (!iconHeight)
                iconHeight = 64;
            const ci = new ColorIcon(iconClass, "IconColorThemeMain", iconWidth, iconHeight);
            i.appendChild(ci.Element);
        }
    }, false, true);
}


/**
 * Pop up a selection of custom items that are searchable
 * @param {string} header Header text
 * @param {string} title Header title (tool tip)
 * @param {function(string, function):HTMLElement[]} getItems Get a list of items as HTML elements given the search text, the function closes the dialog (use when select)
 * @param {string} startText Optional initial search text
 * @param {boolean} getAll If true getItems will be called even if the search text is empty.
 * @param {boolean} blockUntilClosed Block the thread until the pop-up is closed.
 * @param {boolean} dontAllowClose If true, the pop-up can't be closed and closeFn must be called to close it.
 * @param {function} onClose Optional callback that is called when the pop-up is closed.
 */
async function PopUpSelection(text, title, getItems, startText, getAll, blockUntilClosed, dontAllowClose, onClose) {

    await PopUp(async (page, close) => {


        const textE = document.createElement("SysWeaver-PopUpText");
        page.appendChild(textE);
        textE.innerText = text;
        textE.title = title;


        const inputRow = document.createElement("SysWeaver-PopUpFilter");
        page.appendChild(inputRow);

        const inputE = document.createElement("input");
        inputRow.appendChild(inputE);
        inputE.tabIndex = "0";
        inputE.placeholder = title;
        if (startText)
            inputE.value = startText;

        const items = document.createElement("SysWeaver-SelectList");
        page.appendChild(items);

        let prevSearch = null;
        async function update() {
            const search = inputE.value;
            if (!getAll) {
                if (!search)
                    return;
                if (search.length <= 0)
                    return;
            }
            if (search == prevSearch)
                return;
            prevSearch = search;
            const r = await getItems(search, close);
            if (inputE.value != search)
                return;
            const rl = r.length;
            items.innerText = "";
            for (let i = 0; i < rl; ++i)
                items.appendChild(r[i]);
        }

        inputE.focus();
        await update();

        const filterChangeFn = async () => 
        {
            clearButton.SetEnabled(!!inputE.value);
            await update();
        };

        inputE.oninput = filterChangeFn;

        inputE.onchange = filterChangeFn;

        inputE.onkeydown = async ev => {
            if (ev.key !== "Enter")
                return;
            if (!isPureClick(ev))
                return;
            ev.stopPropagation();
            const fe = items.firstElementChild;
            if (fe) {
                fe.click();
            }
        }

        const clearButton = new ColorIcon("IconClearInput", "IconColorThemeAcc1", 35, 35,
            _TF("Click to clear filter", "The tool tip description on a button that when pressed will clear the search filter"),
            async ev => {
                inputE.value = "";
                await filterChangeFn();
            });
        clearButton.SetEnabled(!!inputE.value);
        inputRow.appendChild(clearButton.Element);


    }, blockUntilClosed, dontAllowClose, onClose);
}

async function Confirm(title, text, okButtonText, cancelButtonText, okButtonImageClass, cancelButtonImageClass, okButtonTitle, cancelButtonTitle, okStyle, cancelStyle)
{
    let ok = false;
    await PopUp(async (to, close) => {
        if (title) {
            const te = document.createElement("SysWeaver-ConfTitle");
            te.innerText = title;
            te.title = title + "\n\n" + _TF("Click to copy text to clipboard", "A tool tip description on a confirmation dialog title that when clicked will copy the title text to the clipboard");
            ValueFormat.copyOnClick(te, title);
            to.appendChild(te);
        }
        if (text) {
            const te = document.createElement("SysWeaver-ConfText");
            te.innerText = text;
            te.title = text + "\n\n" + _TF("Click to copy text to clipboard", "A tool tip description on a confirmation dialog text area that when clicked will copy the text to the clipboard");
            ValueFormat.copyOnClick(te, text);
            to.appendChild(te);
        }
        const b = document.createElement("SysWeaver-ConfButtons");
        to.appendChild(b);
        if (!okButtonText)
            okButtonText = _TF("Ok", "The text of a button on a confirmation dialog that when pressed will confirm that the user wants to perform the action");
        const okButton = new Button(okStyle ?? null, okButtonText, okButtonTitle ?? "", okButtonImageClass ?? "IconOk", true, () => {
            ok = true;
            close();
        });
        b.appendChild(okButton.Element);
        if (!cancelButtonText)
            cancelButtonText = _TF("Cancel", "The text of a button on a confirmation dialog that when pressed will close the dialog without performing the action");
        const cancelButton = new Button(cancelStyle ?? null, cancelButtonText, cancelButtonTitle ?? "", cancelButtonImageClass ?? "IconCancel", true, close);
        b.appendChild(cancelButton.Element);
    }, true);
    return ok;
}

class SearchablePoupSelection {
    Key;
    CreateElementFn;
    FindFn;
    MaxRecent;
    GetRecentIdFn;
    constructor(id, getRecentIdFn, createElementFn, findFn, maxRecent) {
        if (!maxRecent)
            maxRecent = 10;
        this.Key = "SysWeaver.PopUpRecent." + id;
        this.CreateElementFn = createElementFn;
        this.FindFn = findFn;
        this.MaxRecent = maxRecent;
        this.GetRecentIdFn = getRecentIdFn;
    }


    async Show(titleText, onSelect, maxView, titleToolTip, filterPlaceholder, blockUntilClose, dontAllowClose, onClose) {
        if (!maxView)
            maxView = 20;
        let state = {
            Filter: "",
            Recent: [],
        };
        let prevState = localStorage.getItem(this.Key);
        if (prevState) {

            try {
                prevState = JSON.parse(prevState);
                Object.assign(state, prevState);
                if (typeof state.Filter !== "string")
                    state.Filter = "";
                const r = state.Recent;
                const mx = this.MaxRecent;
                if (r.length > mx)
                    r.splice(mx, r.length - mx);
            }
            catch (e) {
                console.warn("Failed to parse previous state, error: " + e.message);
                state = {
                    Filter: "",
                    Recent: [],
                };
            }
        }
        const recentMap = new Map();
        const recent = state.Recent;
        const rl = recent.length;
        for (let i = 0; i < rl; ++i)
            recentMap.set(recent[i], 1 + i);

        const createElementFn = this.CreateElementFn;


        await PopUp(async (e, closeFn) => {

            const th = this;
            const titleEl = document.createElement("SysWeaver-PopUpText");
            titleEl.innerText = titleText;
            titleEl.title = titleToolTip ?? titleText;
            const inputRow = document.createElement("SysWeaver-PopUpFilter");
            const inputEl = document.createElement("input");
            inputRow.appendChild(inputEl);
            inputEl.type = "text";
            inputEl.placeholder = filterPlaceholder ?? _TF("Enter some text to filter", "The placeholder text of an input field that is used for filtering some items");
            inputEl.value = state.Filter;

            const dataEl = document.createElement("SysWeaver-PopSelData");
            const getRecentFn = th.GetRecentIdFn;
            const findFn = th.FindFn;
            const onSelectFn = async val => {
                if (onSelect) {
                    try {
                        await onSelect(val);
                    }
                    catch
                    {
                    }
                }
                try {
                    const recentId = getRecentFn(val);
                    const ri = recent.indexOf(recentId);
                    if (ri >= 0)
                        recent.splice(ri, 1);
                    recent.splice(0, 0, recentId);
                    const mx = th.MaxRecent;
                    if (recent.length > mx)
                        recent.splice(mx, recent.length - mx);
                    localStorage.setItem(th.Key, JSON.stringify(state));
                }
                catch
                {
                }
                closeFn();
            };
            let offset = 0;
            let lastSearch = null;
            let lastOffset = -1;
            let topSearch = null;
            const updateList = async () => {

                const filter = inputEl.value;
                if ((lastSearch === filter) && (lastOffset === offset))
                    return;
                if ((lastOffset >= 0) && (offset <= 0)) {
                    state.Filter = filter;
                    try {
                        localStorage.setItem(th.Key, JSON.stringify(state));
                    }
                    catch
                    {
                    }
                }
                lastSearch = filter;
                lastOffset = offset;

                const found = await findFn(filter, recentMap, maxView + 1, offset);
                if (offset <= 0)
                    topSearch = null;
                if (found && (found.length > 0)) {
                    if (offset <= 0)
                        dataEl.innerText = "";
                    else
                        dataEl.lastElementChild.remove();
                    let l = found.length;
                    const gotMore = l > maxView;
                    if (gotMore)
                        l = maxView;
                    for (let i = 0; i < l; ++i) {
                        const data = found[i];
                        if (!topSearch)
                            topSearch = data;
                        const ie = createElementFn(data, onSelectFn, recentMap.has(getRecentFn(data)));
                        dataEl.appendChild(ie);
                    }

                    const end = document.createElement("SysWeaver-PopSelEnd");
                    dataEl.appendChild(end);
                    if (gotMore) {
                        const moreIcon = new ColorIcon("IconMoreData", "IconColorThemeMain", 64, 64,
                            _TF("Click to get more results", "The tool tip description on a button that when clicked will view more results"),
                            async ev => {
                                moreIcon.StartWorking();
                                offset += maxView;
                                await updateList();
                            });
                        end.appendChild(moreIcon.Element);
                    } else {
                        end.innerText = _TF("No more data!", "A text that is displayed at the end of a list of items when no more items is available");
                    }
                } else {
                    if (offset <= 0) {
                        dataEl.innerText = "";
                        const end = document.createElement("SysWeaver-PopSelEnd");
                        end.innerText = _TF("Nothing found!", "A text that is displayed when the result of filtering some data yielded no results");
                        dataEl.appendChild(end);
                    } else {
                        dataEl.lastElementChild.innerText = _TF("No more data!", "A text that is displayed at the end of a list of items when no more items is available");
                    }
                }
            };

            await updateList();

            const filterChangedFn = async () => {
                clearButton.SetEnabled(!!inputEl.value);
                offset = 0;
                await updateList();
            };
            inputEl.onkeydown = async ev => {
                if (ev.key !== "Enter")
                    return;
                if (!isPureClick(ev))
                    return;
                ev.preventDefault();
                ev.stopPropagation();
                if (!topSearch)
                    return;
                await onSelectFn(topSearch);
            };

            const clearButton = new ColorIcon("IconClearInput", "IconColorThemeAcc1", 35, 35,
                _TF("Click to clear filter", "The tool tip description on a button that when pressed will clear the search filter"),
                async ev => {
                    inputEl.value = "";
                    await filterChangedFn();
                });
            clearButton.SetEnabled(!!inputEl.value);
            inputRow.appendChild(clearButton.Element);

            inputEl.onchange = filterChangedFn;
            inputEl.oninput = filterChangedFn;

            e.appendChild(titleEl);
            e.appendChild(inputRow);
            e.appendChild(dataEl);
            inputEl.focus();
        }, blockUntilClose, dontAllowClose, onClose);

    }


}


class AppParams {

    PageElement = null;
    MainMenu = true;
    MainMenus = null;
    Content = true;
    Navigation = true;
    SettingsButton = true;
    SettingMenus = null;
    SettingsFs = true;
    SettingsNewTab = true;
    OnSettingsMenuOpenFn = null; // (webMenu, closeFn)
    OnMainMenuDefFn = null;  // async (menuDef, mainMenuStyle, hideFn)
}


class App {

    Page;
    AppHeader;
    AppMenu;
    Content;
    AppMenuButton;
    AppBackButton;
    Title;
    InitPage;

    static async Create(params) {
        const app = new App();
        app.InitPage = await app.Init(params);
        return app;
    }

    async Init(params) {
        if (!params)
            params = new AppParams();
        const docTitle = document.title;
        window.SysWeaverAppTitle = docTitle;
        const t = this;
        window.SysWeaverApp = t;
        const page = params.PageElement || document.body;
        t.Page = page;
        const collapsedStyle = "Collapsed";
        //  Header
        const appHeader = document.createElement("app-header");
        t.AppHeader = appHeader;
        page.appendChild(appHeader);
        const appHeaderMiddle = document.createElement("app-ContentTitle")
        t.Title = appHeaderMiddle;
        appHeader.appendChild(appHeaderMiddle);
        if (params.SettingsButton) {
            const appSettingsButton = document.createElement("app-SettingsButton")
            appHeader.appendChild(appSettingsButton);

            const settingMenus = params.SettingMenus ?? "User,Theme";
            const themeItems = params.SettingMenus === "" ? [] : WebMenu.From(await sendRequest("Api/application/GetMenu", settingMenus)).Items;
            const til = themeItems.length;

            const expandEmbeddedIcon = new ColorIcon("IconSettings", "IconColorThemeBackground", 48, 48,
                _TF("Click to show settings", "The tool tip description on a button that when clicked will show some settings"),
                ev => {
                PopUpMenu(appSettingsButton, close => {

                    const menu = new WebMenu();
                    menu.Name = "AppSettings";
                    if (params.SettingsFs) {
                        if (FullScreen.IsFull()) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Exit full screen", "The text of a menu option that when clicked will exit from full screen mode"),
                                Flags: 0,
                                IconClass: "IconFullScreenExit",
                                Title: _TF("Exit full screen mode", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                Data: async () => {

                                    await FullScreen.Exit();
                                    close();
                                },
                            }));
                        } else {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Enter full screen", "The text of a menu option that when clicked will enter full screen mode"),
                                Flags: 0,
                                IconClass: "IconFullScreenEnter",
                                Title: _TF("Enter full screen mode", "The tool tip description of a menu option that when clicked will enter full screen mode"),
                                Data: async () => {

                                    await FullScreen.Enter();
                                    close();
                                },
                            }));
                        }
                    }

                    for (let i = 0; i < til; ++i)
                        menu.Items.push(themeItems[i]);

                    if (params.SettingsNewTab && params.Content) {
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Open in new tab", "The text of a menu option that when clicked will open the current content in a new browser tab"),
                            Flags: 0,
                            IconClass: "IconNewTab",
                            Title: _TF("Open the current content in a new tab", "The tool tip description of a menu option that when clicked will open the current content in a new browser tab"),
                            Data: async () => {
                                const contentUrl = MainMenu.GetEmbeddedUrl(t.Content);
                                if (contentUrl)
                                    Open(contentUrl);
                                close();
                            },
                        }));
                    }
                    if (params.OnSettingsMenuOpenFn)
                        params.OnSettingsMenuOpenFn(menu, close);
                    return menu;
                }, true);

            }, null, null, true);

            appSettingsButton.appendChild(expandEmbeddedIcon.Element);


        }


        if (params.Navigation) {
            const appBackButton = document.createElement("app-BackButton")
            t.AppBackButton = appBackButton;
            appHeader.appendChild(appBackButton);

            const backIcon = new ColorIcon("IconNavBack", "IconColorThemeBackground", 48, 48,
                _TF("Click to navigate back", "The tool tip description on a button that when clicked will navigate to the previous page in the history"),
                ev => {
                    history.back();
                });
            backIcon.Element.classList.add("IconButton");
            appBackButton.appendChild(backIcon.Element);

            if (!params.MainMenu)
                appBackButton.style.left = "8px";

        }


        //  Content
        if (params.Content) {

            const appContent = document.createElement("app-content");
            this.Content = appContent;
            page.appendChild(appContent);
        }


        //  Main menu
        if (params.MainMenu) {
            const appMenu = document.createElement("app-menu");
            t.AppMenu = appMenu;
            page.appendChild(appMenu);
            appMenu.classList.add("Collapsed");
            const appMenuButton = document.createElement("app-MenuButton")
            t.AppMenuButton = appMenuButton;
            appHeader.appendChild(appMenuButton);

            const appMenuCapture = document.createElement("app-capture");

            let mouseOverFn = null;
            const tempDisableAutoShow = function () {
                appMenu.removeEventListener("mouseover", mouseOverFn);
                setTimeout(() => {
                    appMenu.addEventListener("mouseover", mouseOverFn);
                }, 250);
            };

            const menuHide = function () {
                if (appMenu.classList.contains(collapsedStyle))
                    return;
                console.log("hide");
                appMenuCapture.remove();
                document.body.removeEventListener("mouseover", menuCapture);
                appMenu.classList.add(collapsedStyle);
                tempDisableAutoShow();
            }

            const menuCapture = function (ev) {
                let target = ev.target;
                while (target != document.body) {
                    if (target == appMenu)
                        return;
                    if (target == appHeader)
                        return;
                    target = target.parentElement;
                }
                menuHide();
            }

            const menuShow = function () {
                if (!appMenu.classList.contains(collapsedStyle))
                    return;
                console.log("Menu show");
                document.body.appendChild(appMenuCapture);
                document.body.addEventListener("mouseover", menuCapture, true);
                appMenu.classList.remove(collapsedStyle);
            }


            const menuToggle = function () {
                if (appMenu.classList.contains(collapsedStyle))
                    menuShow();
                else
                    menuHide();
            }

            const noOpenWidth = 4;

            mouseOverFn = ev => {
                if (!appMenu.classList.contains(collapsedStyle))
                    return;

                //const maxX = ev.target.getBoundingClientRect().width - noOpenWidth;
                if (ev.clientX < noOpenWidth)
                    menuShow();
            };

            const menuIcon = new ColorIcon("IconMenu", "IconColorThemeBackground", 48, 48,
                _TF("Click to show the main menu", "The tool tip description of a button that when clicked will show the main application menu"),
                menuToggle
                );

            appMenu.addEventListener("mousemove", mouseOverFn);
            menuIcon.Element.classList.add("IconButton");
            appMenuButton.appendChild(menuIcon.Element);

            const menuDef = params.MainMenus === "" ? [] : WebMenu.From(await sendRequest("Api/application/GetMenu", params.MainMenus ?? "Default"));
            const menuStyle = new MainMenuStyle();
            menuStyle.Content = t.Content;
            menuStyle.HideFn = menuHide;
            menuStyle.SelectFn = data => {

            };
            menuStyle.ExternalContentFn = (url, title) => {
                if (!title) {
                    appHeaderMiddle.classList.add("Hidden");
                } else {
                    appHeaderMiddle.classList.remove("Hidden");
                    appHeaderMiddle.innerText = title;
                    document.title = docTitle + " - " + title;
                }
            };

            if (params.OnMainMenuDefFn)
                await params.OnMainMenuDefFn(menuDef, menuStyle, menuHide);
            menuDef.Name = "Main";
            const mainMenu = new MainMenu(menuDef, menuStyle, appMenu);







            window.addEventListener("popstate", ev => {
                const s = ev.state;
                if (s) {
                    if (typeof s === "string") {
                        console.log("Navigated to '" + s + '"');
                        mainMenu.EmbeddContent(s, menuStyle, true);
                    }
                }
                else {
                    console.log("Navigated to front page");
                    window.SysWeaverDoneFirst = false;
                    menuDef.Items[0].OnClick();
                }
            });
            window.addEventListener("message", ev => {
                const d = ev.data;
                if (d.Type === "IframeNavigating") {
                    mainMenu.EmbeddContent(d.Url, menuStyle);
                }
                if (d.Type === "LoaderRemoved") {
                    mainMenu.LoaderRemoved(menuStyle);
                }
            });

            const clh = window.location.hash;
            if (clh) {
                const hash = clh.substring(1);
                if (hash.length > 0) {
                    const key = Base64DecodeString(hash);
                    return () => mainMenu.EmbeddContent(key, menuStyle);
                } else {
                    if (menuDef.Items.length > 0) {
                        const c = menuDef.Items[0].OnClick;
                        return c;
                    }
                }
            } else {
                if (menuDef.Items.length > 0) {
                    const c = menuDef.Items[0].OnClick;
                    return c;
                }
            }

        }
        return () => { };
    }



}



async function appMain(settingMenus) {

    const page = document.body;
    const removeLoader = AddLoading(page);
    const app = await App.Create();
    const f = window["AppMain"];
    if (typeof f === "function")
        await f();
    removeLoader();
    await delay(1);
    app.InitPage();


}