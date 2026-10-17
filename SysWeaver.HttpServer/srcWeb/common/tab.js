/**
 * @file Small tab bar control (class Tab).
 * Loaded as a classic script with a static `<script defer src="../common/tab.js">` tag, together with
 * `<link rel="stylesheet" href="../common/tab.css">`, by `explore/api.html` (SysWeaver.HttpServer.ExploreModule,
 * where `explore/api.js` creates `new Tab("Api.Args")` and `new Tab("Api.Results")`) and `edit/type.html`
 * (SysWeaver.MicroService.Edit).
 * Globals defined: the class `Tab` (a global lexical binding, not a window property). No code runs at load time.
 * Dependencies: `badClick(ev)` from common/common.js (used by the tab click handler, so common.js must be loaded).
 * Conventions: selection is expressed purely through the CSS class `tabactive`, which is toggled on the tab header
 * (`<SysWeaver-Tab>`) and on its associated content element(s); tab.css hides `SysWeaver-TabContent` unless it has
 * `tabactive`. With a save key the selected index is persisted in `localStorage["SysWeaver.Tabs." + saveKey]`.
 */

/**
 * A tab bar backed by a `<SysWeaver-TabBar>` element.
 * Usage: `const t = new Tab("Key")`, append `t.Element` to the page yourself, then call `AddTab(...)` once per tab.
 * The first added tab (or the tab whose index equals a restored saved index) is selected automatically.
 * Each tab header element carries expando properties: `Element` (content element), `OnActivate`, `OnDeactivate`,
 * `Title` and `TitleSelected`. There is no dispose; tabs can be removed with `RemoveTab`.
 */
class Tab
{

    /**
     * Creates the (unattached) `<SysWeaver-TabBar>` element and optionally restores the saved selection.
     * Instance fields:
     * - `Element` {HTMLElement} the `<SysWeaver-TabBar>`; its children are the tab headers.
     * - `Selected` {number} selected tab index or -1; after a restore it may hold an index that does not exist yet
     *   (it is applied when that tab is added).
     * - `SaveKey` {string|null} full localStorage key `"SysWeaver.Tabs." + saveKey`, or null.
     * The restore reads `localStorage[SaveKey]` via `parseInt(... ?? "-1") | 0`, so an unparsable value (NaN) becomes 0.
     * Storage exceptions are swallowed.
     * @param {string} [saveKey=null] - Optional key suffix used to persist the selected index; falsy disables persistence.
     */
    constructor(saveKey) {
        saveKey = saveKey ?? null;
        const e = document.createElement("SysWeaver-TabBar");
        this.Selected = -1;
        this.Element = e;
        if (saveKey) {
            saveKey = "SysWeaver.Tabs." + saveKey;
            try {
                const t = parseInt(localStorage.getItem(saveKey) ?? "-1");
                if (typeof t === "number")
                    this.Selected = t | 0;
            }
            catch
            {
            }
        }
        this.SaveKey = saveKey;
    }


    /**
     * Selects the tab at `index`, moving the `tabactive` class from the previous tab to the new one.
     * - `index` undefined or negative: if the current selection is valid, nothing happens; otherwise tab 0 is selected
     *   without saving (or `Selected` becomes -1 when there are no tabs).
     * - Returns early when `index` already equals `Selected`.
     * - Previous tab (if valid): restores its `title` to `Title`, removes `tabactive` from `OnActivate` when it is an
     *   element, calls `OnDeactivate(tabEl, oldIndex)` when it is a function, removes `tabactive` from its `Element`
     *   and from the header itself.
     * - New tab: sets `title` to `TitleSelected`, adds `tabactive` to the header; if `OnActivate` is an element it gets
     *   `tabactive`, otherwise it is called as `OnActivate(tabEl, index)`; its `Element` gets `tabactive`.
     * - Explicit selections are saved to `localStorage[SaveKey]` (errors swallowed). Finally sets `Selected = index`.
     * The index is not range-checked: an index beyond the last tab throws (children[index] is undefined).
     * @param {number} [index] - Zero-based tab index to select.
     * @returns {void}
     */
    Select(index) {
        let save = true;
        const p = this.Element;
        const tabCount = p.childElementCount;
        let curSel = this.Selected;
        if ((typeof index === "undefined") || (index < 0)) {
            if ((curSel >= 0) && (curSel < tabCount))
                return;
            index = 0;
            if (tabCount <= 0) {
                this.Selected = -1;
                return;
            }
            save = false;
        }
        if (index == curSel)
            return;
        const tabActive = "tabactive";
        if ((curSel >= 0) && (curSel < tabCount)){
            const ce = p.children[curSel];
            ce.title = ce.Title;
            let fn = ce.OnActivate;
            if (fn) {
                if (typeof fn !== "function")
                    fn.classList.remove(tabActive);
            }
            fn = ce.OnDeactivate;
            if (fn) {
                if (typeof fn === "function")
                    fn(ce, curSel);
            }
            fn = ce.Element;
            if (fn)
                fn.classList.remove(tabActive);
            ce.classList.remove(tabActive);
        }
        const e = p.children[index];
        e.title = e.TitleSelected;
        e.classList.add(tabActive);
        let fn = e.OnActivate;
        if (fn) {
            if (typeof fn !== "function")
                fn.classList.add(tabActive);
            else
                fn(e, index);
        }
        fn = e.Element;
        if (fn)
            fn.classList.add(tabActive);
        if (save) {
            const saveKey = this.SaveKey;
            if (saveKey) {
                try {
                    localStorage.setItem(saveKey, "" + index);
                }
                catch
                {
                }
            }
        }
        this.Selected = index;
    }

    // Add a new tab, onActive takes one argument containing the element to add data to
    /**
     * Appends a new `<SysWeaver-Tab>` header with text `name` to the tab bar.
     * The header's click handler ignores clicks rejected by `badClick(ev)` (common.js), computes the tab's index from
     * its preceding siblings and calls `Select(index)` (which saves the selection).
     * After appending: if nothing is selected (`Selected < 0`) `Select(0)` is called (which also saves); otherwise, if the
     * new tab's index equals a restored `Selected` value, that tab is activated (`Selected` reset to -1, then `Select`).
     * @param {string} name - Header text (set via innerText).
     * @param {(function(HTMLElement, number):void|HTMLElement)} [onActivate] - Function called as `(tabEl, index)` when the
     *   tab is selected, or an element that receives the `tabactive` class while the tab is selected.
     * @param {string} [title=""] - Tooltip shown while the tab is not selected (stored as expando `Title`).
     * @param {function(HTMLElement, number):void} [onDeactivate] - Optional function called as `(tabEl, index)` when the
     *   tab loses the selection.
     * @param {HTMLElement} [element] - Optional content element (usually `SysWeaver-TabContent`) that receives
     *   `tabactive` while the tab is selected (stored as expando `Element`).
     * @param {string} [titleSelected=""] - Tooltip shown while the tab is selected (stored as expando `TitleSelected`).
     * @returns {HTMLElement} The created `<SysWeaver-Tab>` element.
     */
    AddTab(name, onActivate, title, onDeactivate, element, titleSelected) {
        const t = this;
        const p = t.Element;
        const e = document.createElement("SysWeaver-Tab");
        e.Element = element;
        e.onclick = ev => {
            if (badClick(ev))
                return;
            let index;
            let q = e;
            for (index = -1; q; ++index)
                q = q.previousElementSibling;
            t.Select(index);
        };
        e.OnActivate = onActivate;
        e.OnDeactivate = onDeactivate;
        e.innerText = name;
        if (!title)
            title = "";
        if (!titleSelected)
            titleSelected = "";
        e.Title = title;
        e.TitleSelected = titleSelected;
        e.title = title;
        const tabIndex = p.childElementCount;
        p.appendChild(e);
        if (t.Selected < 0)
            t.Select(0);
        else {
            if (tabIndex === t.Selected)
            {
                t.Selected = -1;
                t.Select(tabIndex);
            }
        }
        return e;
    }

    /**
     * Removes the tab header at `index` (no-op if there is none).
     * If it was the selected tab, `Selected` is set to -1 and a neighbour (`index-1`, or `index+1` for index 0, capped at
     * the last index) is selected via `Select` (which saves it); because `Selected` was reset first, the removed tab's
     * deactivation (class removal / `OnDeactivate`) is not performed, so its content element keeps `tabactive`.
     * If the remaining selection is above `index` it is decremented and, with a `SaveKey`, stored in localStorage
     * (errors swallowed). The content element (`Element` expando) is not removed. No callers were found.
     * @param {number} index - Zero-based index of the tab to remove.
     * @returns {void}
     */
    RemoveTab(index) {
        const p = this.Element;
        const e = p.children[index];
        if (!e)
            return;
        if (this.Selected == index) {
            let newSel = index > 0 ? (index - 1) : (index + 1);
            const selMax = p.childElementCount - 1;
            if (newSel > selMax)
                newSel = selMax;
            this.Selected = -1;
            if ((newSel >= 0) && (newSel != index))
                this.Select(newSel);
        }
        const sel = this.Selected;
        if (sel > index) {
            this.Selected = sel - 1;
            const saveKey = this.SaveKey;
            if (saveKey) {
                try {
                    localStorage.setItem(saveKey, "" + (sel - 1));
                }
                catch
                {
                }
            }
        }
        e.remove();
    }
}

