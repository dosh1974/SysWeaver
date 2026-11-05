/**
 * @file LEGACY tab bar control (class `Tab`), kept for old code: `explore/api.js` (`new Tab("Api.Args")`,
 * `new Tab("Api.Results")`) and `edit/type.html`. New code uses `Sw.Tabs` (ui library).
 * Loaded as a classic script with `<script defer src="../common/tab.js">` together with
 * `<link rel="stylesheet" href="../common/tab.css">`. No code runs at load time.
 *
 * Globals defined: the class `Tab` (a global lexical binding). Standalone: no dependency on common.js or Sw (modifier
 * clicks are ignored like the old `badClick` guard did).
 * Conventions: selection is expressed through the css class `tabactive` on the tab header (`<SysWeaver-Tab>`) and on
 * its content element(s); tab.css hides `SysWeaver-TabContent` unless it has `tabactive`. With a save key the
 * selected index is persisted in `localStorage["SysWeaver.Tabs." + saveKey]`.
 * Accessibility: the bar is a `tablist`, headers are `tab`s (`aria-selected`) with a roving tab stop; Left / Right,
 * Home / End move and select, Enter / Space select. Content elements get `role="tabpanel"`.
 */

/**
 * A tab bar backed by a `<SysWeaver-TabBar>` element.
 * Usage: `const t = new Tab("Key")`, append `t.Element` to the page, then call `AddTab(...)` once per tab. The first
 * added tab (or the tab whose index equals a restored saved index) is selected automatically.
 * Header expandos: `Element` (content element), `OnActivate`, `OnDeactivate`, `Title`, `TitleSelected`.
 */
class Tab {

    /**
     * Create the (unattached) `<SysWeaver-TabBar>` and restore the saved selection.
     * @param {string} [saveKey] - Optional key suffix to persist the selected index; falsy disables persistence.
     */
    constructor(saveKey) {
        const e = document.createElement("SysWeaver-TabBar");
        e.setAttribute("role", "tablist");
        e.addEventListener("keydown", ev => this.#onKey(ev));
        /** @type {HTMLElement} The bar; its children are the tab headers. */
        this.Element = e;
        /** @type {number} Selected tab index or -1 (after a restore it may be an index that is added later). */
        this.Selected = -1;
        /** @type {?string} The full localStorage key ("SysWeaver.Tabs." + saveKey) or null. */
        this.SaveKey = saveKey ? ("SysWeaver.Tabs." + saveKey) : null;
        if (this.SaveKey) {
            try {
                const t = parseInt(localStorage.getItem(this.SaveKey) ?? "-1", 10);
                this.Selected = Number.isNaN(t) ? 0 : t;
            }
            catch {
            }
        }
    }

    /**
     * Store the selected index (errors ignored).
     * @param {number} index - Index.
     */
    #save(index) {
        if (!this.SaveKey)
            return;
        try {
            localStorage.setItem(this.SaveKey, "" + index);
        }
        catch {
        }
    }

    /**
     * Index of a header element.
     * @param {HTMLElement} e - Header.
     * @returns {number} Index.
     */
    #indexOf(e) {
        return Array.prototype.indexOf.call(this.Element.children, e);
    }

    /**
     * Keyboard handling of the bar: Left / Right / Home / End move and select (focus follows), Enter / Space select.
     * @param {KeyboardEvent} ev - Event.
     */
    #onKey(ev) {
        if (ev.ctrlKey || ev.altKey || ev.metaKey)
            return;
        const p = this.Element;
        const count = p.childElementCount;
        if (count <= 0)
            return;
        const cur = this.#indexOf(document.activeElement);
        let next = -1;
        switch (ev.key) {
            case "ArrowLeft":
                next = cur <= 0 ? count - 1 : cur - 1;
                break;
            case "ArrowRight":
                next = (cur < 0 || cur >= count - 1) ? 0 : cur + 1;
                break;
            case "Home":
                next = 0;
                break;
            case "End":
                next = count - 1;
                break;
            case "Enter":
            case " ":
                next = cur;
                break;
            default:
                return;
        }
        if (next < 0)
            return;
        ev.preventDefault();
        this.Select(next);
        p.children[next].focus();
    }

    /**
     * Select the tab at index: moves `tabactive` from the previous tab (and its content / OnActivate element) to the
     * new one, calls OnDeactivate(tabEl, oldIndex) / OnActivate(tabEl, index) functions and swaps the tool tips.
     * - index undefined or negative: keeps a valid selection, otherwise selects tab 0 without saving.
     * - Explicit selections are saved. Out of range indices are ignored.
     * @param {number} [index] - Zero based index.
     */
    Select(index) {
        let save = true;
        const p = this.Element;
        const tabCount = p.childElementCount;
        const curSel = this.Selected;
        if ((typeof index === "undefined") || (index < 0)) {
            if ((curSel >= 0) && (curSel < tabCount))
                return;
            if (tabCount <= 0) {
                this.Selected = -1;
                return;
            }
            index = 0;
            save = false;
        }
        if ((index === curSel) || (index >= tabCount))
            return;
        if ((curSel >= 0) && (curSel < tabCount))
            Tab.#setActive(p.children[curSel], curSel, false);
        Tab.#setActive(p.children[index], index, true);
        if (save)
            this.#save(index);
        this.Selected = index;
    }

    /**
     * Activate / deactivate one tab header and its associated elements.
     * @param {HTMLElement} e - Header.
     * @param {number} index - Its index.
     * @param {boolean} on - Activate.
     */
    static #setActive(e, index, on) {
        const cls = "tabactive";
        e.title = (on ? e.TitleSelected : e.Title) || "";
        e.classList.toggle(cls, on);
        e.setAttribute("aria-selected", on ? "true" : "false");
        e.tabIndex = on ? 0 : -1;
        const act = e.OnActivate;
        if (act) {
            if (typeof act === "function") {
                if (on)
                    act(e, index);
            }
            else
                act.classList.toggle(cls, on);
        }
        if (!on && (typeof e.OnDeactivate === "function"))
            e.OnDeactivate(e, index);
        if (e.Element)
            e.Element.classList.toggle(cls, on);
    }

    /**
     * Append a `<SysWeaver-Tab>` header.
     * @param {string} name - Header text.
     * @param {(function(HTMLElement, number):void|HTMLElement)} [onActivate] - Called as (tabEl, index) when selected,
     * or an element that gets `tabactive` while the tab is selected.
     * @param {string} [title=""] - Tool tip while not selected.
     * @param {function(HTMLElement, number):void} [onDeactivate] - Called as (tabEl, index) when deselected.
     * @param {HTMLElement} [element] - Content element that gets `tabactive` while selected.
     * @param {string} [titleSelected=""] - Tool tip while selected.
     * @returns {HTMLElement} The header element.
     */
    AddTab(name, onActivate, title, onDeactivate, element, titleSelected) {
        const p = this.Element;
        const e = document.createElement("SysWeaver-Tab");
        e.setAttribute("role", "tab");
        e.setAttribute("aria-selected", "false");
        e.tabIndex = -1;
        e.Element = element;
        e.OnActivate = onActivate;
        e.OnDeactivate = onDeactivate;
        e.Title = title || "";
        e.TitleSelected = titleSelected || "";
        e.title = e.Title;
        e.textContent = name;
        for (const panel of [element, (onActivate && typeof onActivate !== "function") ? onActivate : null])
            if (panel && !panel.hasAttribute("role"))
                panel.setAttribute("role", "tabpanel");
        e.addEventListener("click", ev => {
            ev.preventDefault();
            ev.stopPropagation();
            if (ev.shiftKey || ev.altKey || ev.ctrlKey || ev.metaKey)
                return;
            this.Select(this.#indexOf(e));
        });
        const tabIndex = p.childElementCount;
        p.appendChild(e);
        if (this.Selected < 0)
            this.Select(0);
        else if (tabIndex === this.Selected) {
            this.Selected = -1;
            this.Select(tabIndex);
        }
        if (p.querySelector("[tabindex='0']") === null)
            e.tabIndex = 0;
        return e;
    }

    /**
     * Remove the tab header at index (its content element is not removed). A removed selected tab is deactivated and
     * a neighbour (index - 1, or the next one) is selected.
     * @param {number} index - Zero based index.
     */
    RemoveTab(index) {
        const p = this.Element;
        const e = p.children[index];
        if (!e)
            return;
        const wasSelected = this.Selected === index;
        if (wasSelected)
            Tab.#setActive(e, index, false);
        e.remove();
        if (wasSelected) {
            this.Selected = -1;
            const count = p.childElementCount;
            if (count > 0)
                this.Select(Math.min(index > 0 ? index - 1 : 0, count - 1));
            else
                this.#save(-1);
        }
        else if (this.Selected > index) {
            this.Selected = this.Selected - 1;
            this.#save(this.Selected);
        }
    }
}
