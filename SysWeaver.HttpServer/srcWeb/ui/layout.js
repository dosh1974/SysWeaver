/**
 * @file ui/layout.js - SysWeaver UI library (Sw), part 4 of 7: layout components.
 *
 * Tabs (tab strip + bodies), Split (two resizable panes), Pager (a strip of items moved by pages / swipes), PageHeader (title,
 * description, status, actions; Design v2 page anatomy), EmptyState (empty / error state of a region) and
 * Columns (responsive columns that wrap to fewer columns on narrow screens).
 * Needs ui/base.js and ui/controls.js. Documentation: ui/README.md.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const { el, clamp, Icon } = Sw;

    /** A text option (a type name, not a label). */
    const Str = "string";

    /**
     * The option schemas of the layout parts (DevMode checks through the one registry, Sw.Work.RegisterOptions: every public api
     * that takes options checks them, a typo is never silently ignored).
     */
    const LayoutSchema = {
        Tabs: {
            Class: Str, Closable: "boolean", OnSelect: "function", OnClose: "function", OnAdd: "function", AddTitle: Str, Small: "boolean",
            Label: Str, Fill: "boolean",
        },
        "Tabs tab": { Id: "any", Title: Str, Icon: "any", Tip: Str, Content: "any", Closable: "boolean", Data: "any", Select: "boolean" },
        Split: { A: "any", B: "any", Vertical: "boolean", Size: "number", Min: "number", Key: Str, Label: Str, OnResize: "function" },
        Pager: { OnChange: "function", Swipe: "boolean" },
        Columns: { MinWidth: "number", Gap: "string|number", Max: "number", Class: Str, Content: "any" },
        PageHeader: {
            Title: Str, Description: Str, Status: "any", Actions: "any", Compact: "boolean", Level: "number", Class: Str,
            MenuTitle: "boolean|string", Overflow: "boolean",
        },
        EmptyState: { Icon: "any", Title: Str, Text: Str, Actions: "any", Error: "boolean", Class: Str },
    };
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function")
        for (const k of Object.keys(LayoutSchema))
            Sw.Work.RegisterOptions("Sw." + k, LayoutSchema[k]);

    /**
     * Check the options of a layout part (DevMode; nothing in production).
     * @param {string} name The part ("Tabs"). @param {object} o The options.
     */
    function checkOpts(name, o) {
        if (o && typeof o === "object" && Sw.Work && typeof Sw.Work.CheckOptions === "function")
            Sw.Work.CheckOptions("Sw." + name, o, LayoutSchema[name]);
    }

    // ------------------------------------------------------------------ tabs

    /**
     * Tabs: a strip of tab headers and a body showing the content of the selected tab (the others are hidden, not removed, so they keep their state).
     * Options: Class, Closable (default for the tabs), OnSelect(tab, previous), OnClose(tab) (return false / a promise of false to keep it),
     *          OnAdd() (shows a "+" button), AddTitle, Small (compact headers), Label (aria label of the tab list), Fill (tab contents fill the body:
     *          position absolute, default true; false = the body grows with the content).
     * Tab options (Add): { Id, Title, Icon, Tip (tooltip), Content (element), Closable, Data (anything), Select (false: don't select it even if it's the first) }.
     * Methods: Add(tab) (returns the tab: { Id, Head, Content, Data, SetTitle(text), SetTip(text), SetDirty(bool), SetBadge(text, kind) }),
     *          Select(idOrTab), Close(idOrTab) (asks OnClose), Remove(idOrTab) (no questions), Get(id), All() (in order), Next(dir), Active,
     *          Tools (append buttons right of the headers).
     * Keyboard (tab headers, roving focus): Left / Right / Home / End move and select, Delete closes a closable tab. Middle click closes a tab.
     * Ctrl+Tab / Ctrl+Shift+Tab are left to the page (call Next(dir)).
     */
    class Tabs {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            checkOpts("Tabs", o);
            const t = this;
            o = o || {};
            t.O = o;
            t.Items = [];
            t.Active = null;
            t.Strip = el("div.sw-tabs-strip", { attrs: { role: "tablist", "aria-label": o.Label || null } });
            t.Tools = el("div.sw-tabs-tools");
            if (o.OnAdd)
                t.Tools.append(new Sw.Button({ Icon: "plus", Title: o.AddTitle || _TF("Add", "Tool tip of the button that adds a new tab"), Kind: "ghost", Small: true, OnClick: () => o.OnAdd() }).E);
            t.Head = el("div.sw-tabs-head", null, t.Strip, t.Tools);
            t.Body = el("div.sw-tabs-body");
            t.E = el("div.sw-tabs", { class: (o.Class || "") + (o.Small ? " sw-tabs-small" : "") + (o.Fill === false ? " sw-tabs-flow" : "") }, t.Head, t.Body);
            //  Vertical wheel scrolls the strip sideways
            t.Strip.addEventListener("wheel", e => {
                if (Math.abs(e.deltaY) > Math.abs(e.deltaX) && t.Strip.scrollWidth > t.Strip.clientWidth) {
                    t.Strip.scrollLeft += e.deltaY;
                    e.preventDefault();
                }
            }, { passive: false });
            t.Strip.addEventListener("keydown", e => {
                const i = t.Items.findIndex(x => x.Head === document.activeElement);
                if (i < 0)
                    return;
                let n = -1;
                switch (e.key) {
                    case "ArrowRight": n = (i + 1) % t.Items.length; break;
                    case "ArrowLeft": n = (i - 1 + t.Items.length) % t.Items.length; break;
                    case "Home": n = 0; break;
                    case "End": n = t.Items.length - 1; break;
                    case "Delete":
                        if (t.Items[i].Closable) {
                            e.preventDefault();
                            t.Close(t.Items[i]);
                        }
                        return;
                    default: return;
                }
                e.preventDefault();
                const tab = t.Items[n];
                t.Select(tab);
                tab.Head.focus();
            });
        }

        /**
         * Find a tab.
         * @param {string|object} x An id or a tab.
         * @returns {object|null} The tab.
         */
        Find(x) {
            return typeof x === "object" && x ? x : this.Items.find(i => i.Id === x) || null;
        }

        /**
         * Get a tab by id.
         * @param {string} id The id.
         * @returns {object|null} The tab.
         */
        Get(id) {
            return this.Find(id);
        }

        /**
         * All tabs in order.
         * @returns {object[]} The tabs.
         */
        All() {
            return this.Items.slice();
        }

        /**
         * Add a tab.
         * @param {object} opts Tab options (see the class description).
         * @returns {object} The tab.
         */
        Add(opts) {
            const t = this;
            checkOpts("Tabs tab", opts);
            const id = opts.Id ?? Sw.Uid("tab");
            const tab = { Id: id, Data: opts.Data, Content: opts.Content || el("div") };
            const closable = !!(opts.Closable ?? t.O.Closable);
            tab.Closable = closable;
            const headId = Sw.Uid("sw-tab");
            const panelId = Sw.Uid("sw-panel");
            const label = el("span.sw-tab-label");
            const dirty = el("span.sw-tab-dirty", { text: "●", title: _TF("Modified", "Tool tip of the dot on a tab with unsaved changes") });
            const badge = el("span.sw-tab-badge");
            badge.hidden = true;
            dirty.hidden = true;
            tab.Head = el("div.sw-tab", { id: headId, tabIndex: -1, attrs: { role: "tab", "aria-selected": "false", "aria-controls": panelId } },
                opts.Icon ? Icon(opts.Icon) : null, label, badge, dirty);
            if (closable) {
                const x = el("button.sw-tab-close", { type: "button", tabIndex: -1, title: _TF("Close", "Tool tip of the button that closes a tab"), attrs: { "aria-label": _TF("Close", "Tool tip of the button that closes a tab") } }, Icon("close"));
                x.addEventListener("click", e => {
                    e.stopPropagation();
                    t.Close(tab);
                });
                tab.Head.append(x);
            }
            tab.SetTitle = text => {
                tab.Title = text;
                label.textContent = text;
            };
            tab.SetTip = text => tab.Head.title = text || "";
            tab.SetDirty = d => {
                tab.Dirty = !!d;
                dirty.hidden = !d;
                tab.Head.classList.toggle("sw-tab-modified", !!d);
            };
            tab.SetBadge = (text, kind) => {
                badge.hidden = !text;
                badge.textContent = text || "";
                badge.className = "sw-tab-badge" + (kind ? " sw-" + kind : "");
            };
            tab.SetTitle(opts.Title || "");
            tab.SetTip(opts.Tip);
            tab.Head.addEventListener("pointerdown", e => {
                if (e.button === 1)
                    e.preventDefault();
            });
            tab.Head.addEventListener("click", () => t.Select(tab));
            tab.Head.addEventListener("auxclick", e => {
                if (e.button === 1 && closable)
                    t.Close(tab);
            });
            tab.Head.addEventListener("keydown", e => {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    t.Select(tab);
                }
            });
            tab.Content.classList.add("sw-tab-content");
            tab.Content.id = tab.Content.id || panelId;
            tab.Head.setAttribute("aria-controls", tab.Content.id);
            tab.Content.setAttribute("role", "tabpanel");
            tab.Content.setAttribute("aria-labelledby", headId);
            tab.Content.hidden = true;
            t.Items.push(tab);
            t.Strip.append(tab.Head);
            t.Body.append(tab.Content);
            if (!t.Active && opts.Select !== false)
                t.Select(tab);
            else if (!t.Active && t.Items.length === 1)
                tab.Head.tabIndex = 0;
            return tab;
        }

        /**
         * Select a tab.
         * @param {string|object} x An id or a tab.
         * @returns {object|null} The tab.
         */
        Select(x) {
            const t = this;
            const tab = t.Find(x);
            if (!tab || tab === t.Active)
                return tab;
            const prev = t.Active;
            if (prev) {
                prev.Head.classList.remove("sw-active");
                prev.Head.setAttribute("aria-selected", "false");
                prev.Head.tabIndex = -1;
                prev.Content.hidden = true;
            }
            t.Items.forEach(i => i.Head.tabIndex = -1);
            t.Active = tab;
            tab.Head.classList.add("sw-active");
            tab.Head.setAttribute("aria-selected", "true");
            tab.Head.tabIndex = 0;
            tab.Content.hidden = false;
            tab.Head.scrollIntoView({ block: "nearest", inline: "nearest" });
            t.O.OnSelect && t.O.OnSelect(tab, prev);
            return tab;
        }

        /**
         * Select the next (dir 1) / previous (dir -1) tab.
         * @param {number} dir Direction.
         */
        Next(dir) {
            const t = this;
            if (t.Items.length < 2)
                return;
            const i = t.Items.indexOf(t.Active);
            t.Select(t.Items[(i + (dir < 0 ? t.Items.length - 1 : 1)) % t.Items.length]);
        }

        /**
         * Close a tab (asks OnClose).
         * @param {string|object} x An id or a tab.
         * @returns {Promise<boolean>} True if closed.
         */
        async Close(x) {
            const t = this;
            const tab = t.Find(x);
            if (!tab)
                return false;
            if (t.O.OnClose && (await t.O.OnClose(tab)) === false)
                return false;
            const hadFocus = tab.Head.contains(document.activeElement);
            t.Remove(tab);
            if (hadFocus && t.Active)
                t.Active.Head.focus();
            return true;
        }

        /**
         * Remove a tab (no questions).
         * @param {string|object} x An id or a tab.
         */
        Remove(x) {
            const t = this;
            const tab = t.Find(x);
            if (!tab)
                return;
            const i = t.Items.indexOf(tab);
            if (i < 0)
                return;
            t.Items.splice(i, 1);
            tab.Head.remove();
            tab.Content.remove();
            if (t.Active === tab) {
                t.Active = null;
                const next = t.Items[Math.min(i, t.Items.length - 1)];
                if (next)
                    t.Select(next);
                else
                    t.O.OnSelect && t.O.OnSelect(null, tab);
            }
        }
    }

    // ------------------------------------------------------------------ split

    /**
     * Two panes with a draggable bar between them.
     * Options: A, B (elements), Vertical (true: A above B, else A left of B), Size (fraction of A, default 0.5), Min (px, default 80),
     *          Key (local storage key to remember the size), Label (aria label of the bar), OnResize(fraction).
     * Methods: SetSize(fraction), Collapse(null | "A" | "B") (hide a pane, the other one takes all the space).
     * Double click (or double tap) on the bar resets the size. Keyboard on the bar: arrows move it (Shift = bigger steps), Home / End = minimum / maximum,
     * Enter resets. The panes are flex columns, give their child flex: 1.
     */
    class Split {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            checkOpts("Split", o);
            const t = this;
            o = o || {};
            t.O = o;
            t.Default = o.Size ?? 0.5;
            t.Size = t.Default;
            if (o.Key) {
                try {
                    const v = parseFloat(localStorage.getItem(o.Key));
                    if (v > 0 && v < 1)
                        t.Size = v;
                }
                catch {
                }
            }
            t.A = el("div.sw-split-pane.sw-split-a", null, o.A || null);
            t.B = el("div.sw-split-pane.sw-split-b", null, o.B || null);
            t.Bar = el("div.sw-split-bar", {
                tabIndex: 0,
                attrs: { role: "separator", "aria-orientation": o.Vertical ? "horizontal" : "vertical", "aria-valuemin": "0", "aria-valuemax": "100", "aria-label": o.Label || _TF("Resize", "Accessible name of the bar between two resizable panes") },
            });
            t.E = el("div.sw-split" + (o.Vertical ? ".sw-split-v" : ".sw-split-h"), null, t.A, t.Bar, t.B);
            t.Collapsed = null;
            t.Apply();
            let drag = null;
            let lastTap = 0;
            Sw.Drag(t.Bar, {
                Threshold: 0,
                OnStart: e => {
                    e.preventDefault();
                    drag = { R: t.E.getBoundingClientRect() };
                    t.E.classList.add("sw-split-dragging");
                    const now = performance.now();
                    if (now - lastTap < 320) {
                        t.SetSize(t.Default);
                        t.Store();
                        drag = null;
                        return false;
                    }
                    lastTap = now;
                },
                OnMove: e => {
                    if (!drag)
                        return;
                    const r = drag.R;
                    const total = o.Vertical ? r.height : r.width;
                    const pos = o.Vertical ? e.clientY - r.top : e.clientX - r.left;
                    const min = Math.min(o.Min ?? 80, total / 2);
                    t.SetSize(clamp(pos, min, total - min) / total);
                },
                OnEnd: () => {
                    drag = null;
                    t.E.classList.remove("sw-split-dragging");
                    t.Store();
                },
                OnTap: () => t.E.classList.remove("sw-split-dragging"),
            });
            t.Bar.addEventListener("keydown", e => {
                const step = e.shiftKey ? 0.1 : 0.02;
                const dec = o.Vertical ? "ArrowUp" : "ArrowLeft";
                const inc = o.Vertical ? "ArrowDown" : "ArrowRight";
                switch (e.key) {
                    case dec: t.SetSize(t.Size - step); break;
                    case inc: t.SetSize(t.Size + step); break;
                    case "Home": t.SetSize(0.05); break;
                    case "End": t.SetSize(0.95); break;
                    case "Enter": t.SetSize(t.Default); break;
                    default: return;
                }
                e.preventDefault();
                t.Store();
            });
        }

        /** Remember the size (if Key is set). */
        Store() {
            if (!this.O.Key)
                return;
            try {
                localStorage.setItem(this.O.Key, String(this.Size));
            }
            catch {
            }
        }

        /** Apply the size / collapsed state to the panes. */
        Apply() {
            const t = this;
            const c = t.Collapsed;
            t.A.hidden = c === "A";
            t.B.hidden = c === "B";
            t.Bar.hidden = !!c;
            t.A.style.flex = c ? "1 1 0" : (t.Size * 100) + " 1 0";
            t.B.style.flex = c ? "1 1 0" : ((1 - t.Size) * 100) + " 1 0";
            t.Bar.setAttribute("aria-valuenow", String(Math.round(t.Size * 100)));
        }

        /**
         * Set the size of pane A.
         * @param {number} f Fraction 0..1 (clamped to 0.02..0.98).
         */
        SetSize(f) {
            this.Size = clamp(f, 0.02, 0.98);
            this.Apply();
            this.O.OnResize && this.O.OnResize(this.Size);
        }

        /**
         * Collapse (hide) a pane.
         * @param {null|"A"|"B"} pane The pane to hide, null shows both.
         */
        Collapse(pane) {
            this.Collapsed = pane === "A" || pane === "B" ? pane : null;
            this.Apply();
            this.O.OnResize && this.O.OnResize(this.Size);
        }
    }

    // ------------------------------------------------------------------ pager

    /**
     * A horizontal strip of items that is scrolled by pages (no scroll bar): Next() / Prev() move one page (smoothly),
     * swiping (touch / pen drag) moves too, and the keyboard focus (Tab into a hidden item) reveals the item.
     * Items are the children of .Content and should have the same width.
     * The offset is always a whole number of items (the stride = item width + gap), so several pagers with the same item size
     * stay aligned (the last page may end with empty space).
     * Options: OnChange(pager) (page / page count changed, update your buttons with CanPrev() / CanNext()), Swipe (default true).
     * Methods: Next(), Prev(), CanPrev(), CanNext(), Reveal(item), Go(offset, animate), CancelSwipe().
     */
    class Pager {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            checkOpts("Pager", o);
            const t = this;
            o = o || {};
            t.O = o;
            t.Content = el("div.sw-pager-content");
            t.E = el("div.sw-pager", null, t.Content);
            t.Offset = 0;
            t.Last = "";
            const ro = new ResizeObserver(() => t.Clamp());
            ro.observe(t.E);
            ro.observe(t.Content);
            if (o.Swipe !== false)
                t.BindSwipe();
            //  Keyboard focus on a hidden item: page to it
            t.Content.addEventListener("focusin", e => {
                const item = Array.from(t.Content.children).find(c => c.contains(e.target));
                if (item) {
                    t.E.scrollLeft = 0;
                    t.Reveal(item);
                }
            });
        }

        /**
         * The visible width.
         * @returns {number} px.
         */
        Viewport() {
            return this.E.clientWidth;
        }

        /**
         * Item width + gap (the width of the first item), 0 if empty.
         * @returns {number} px.
         */
        Stride() {
            const first = this.Content.firstElementChild;
            if (!first)
                return 0;
            const gap = parseFloat(getComputedStyle(this.Content).columnGap) || 0;
            return first.offsetWidth + gap;
        }

        /**
         * The largest offset: whole items, the last item fully visible.
         * @returns {number} px.
         */
        MaxOffset() {
            const t = this;
            const over = t.Content.scrollWidth - t.Viewport();
            if (over <= 0)
                return 0;
            const s = t.Stride();
            return s > 0 ? Math.ceil(over / s - 0.01) * s : over;
        }

        /**
         * Page step: the items that fit in the viewport (at least one).
         * @returns {number} px.
         */
        PageStep() {
            const t = this;
            const s = t.Stride();
            if (s <= 0)
                return t.Viewport();
            const pad = parseFloat(getComputedStyle(t.Content).paddingLeft) || 0;
            return Math.max(1, Math.floor((t.Viewport() - pad) / s + 0.01)) * s;
        }

        /**
         * Move to an offset (snapped to whole items).
         * @param {number} offset px.
         * @param {boolean} [animate] False: jump without the transition.
         */
        Go(offset, animate) {
            const t = this;
            const s = t.Stride();
            if (s > 0)
                offset = Math.round(offset / s) * s;
            t.Offset = clamp(offset, 0, t.MaxOffset());
            if (animate === false)
                t.Content.style.transition = "none";
            t.Content.style.transform = "translateX(" + (-t.Offset) + "px)";
            if (animate === false) {
                void t.Content.offsetWidth;
                t.Content.style.transition = "";
            }
            t.Notify();
        }

        /** Move one page forward. */
        Next() {
            this.Go(this.Offset + this.PageStep());
        }

        /** Move one page back. */
        Prev() {
            this.Go(this.Offset - this.PageStep());
        }

        /**
         * Test if there is a previous page.
         * @returns {boolean} True if possible.
         */
        CanPrev() {
            return this.Offset > 1;
        }

        /**
         * Test if there is a next page.
         * @returns {boolean} True if possible.
         */
        CanNext() {
            return this.Offset < this.MaxOffset() - 1;
        }

        /**
         * Make sure an item is visible (moves by whole pages).
         * @param {HTMLElement} item The item.
         * @param {boolean} [animate] False: jump.
         */
        Reveal(item, animate) {
            const t = this;
            if (!item)
                return;
            const l = item.offsetLeft, r = l + item.offsetWidth;
            const vw = t.Viewport();
            if (l >= t.Offset && r <= t.Offset + vw)
                return;
            const step = t.PageStep();
            let off = t.Offset;
            while (r > off + vw && off < t.MaxOffset())
                off += step;
            while (l < off && off > 0)
                off -= step;
            t.Go(off, animate);
        }

        /** Keep the offset valid (resized, items added / removed). */
        Clamp() {
            const t = this;
            const s = t.Stride();
            const max = t.MaxOffset();
            if (t.Offset > max || (s > 0 && Math.abs(t.Offset / s - Math.round(t.Offset / s)) > 0.01))
                t.Go(Math.min(t.Offset, max), false);
            else
                t.Notify();
        }

        /** Call OnChange when CanPrev / CanNext changed. */
        Notify() {
            const t = this;
            const s = t.CanPrev() + "," + t.CanNext();
            if (s !== t.Last) {
                t.Last = s;
                t.O.OnChange && t.O.OnChange(t);
            }
        }

        /** Stop a touch swipe that hasn't started yet (another gesture, e.g. dragging an item, takes over). */
        CancelSwipe() {
            this.CancelSwipeFn && this.CancelSwipeFn();
        }

        /** Set up touch / pen swiping. */
        BindSwipe() {
            const t = this;
            let drag = null;
            t.CancelSwipeFn = () => {
                if (drag && !drag.Active)
                    drag = null;
            };
            t.E.addEventListener("pointerdown", e => {
                if (e.pointerType === "mouse" || drag)
                    return;
                drag = { Id: e.pointerId, X: e.clientX, Y: e.clientY, Off: t.Offset, Active: false };
            });
            t.E.addEventListener("pointermove", e => {
                if (!drag || e.pointerId !== drag.Id)
                    return;
                const dx = e.clientX - drag.X;
                if (!drag.Active) {
                    if (Math.abs(dx) < 14 || Math.abs(dx) < Math.abs(e.clientY - drag.Y))
                        return;
                    drag.Active = true;
                    try {
                        t.E.setPointerCapture(e.pointerId);
                    }
                    catch {
                    }
                    t.E.classList.add("sw-dragging");
                }
                const max = t.MaxOffset();
                let off = drag.Off - dx;
                //  Rubber band at the ends
                if (off < 0)
                    off *= 0.3;
                else if (off > max)
                    off = max + (off - max) * 0.3;
                t.Content.style.transform = "translateX(" + (-off) + "px)";
            });
            const end = e => {
                if (!drag || e.pointerId !== drag.Id)
                    return;
                const d = drag;
                drag = null;
                if (!d.Active)
                    return;
                t.E.classList.remove("sw-dragging");
                //  The click after a swipe is not a tap
                const block = ev => {
                    ev.stopPropagation();
                    ev.preventDefault();
                };
                t.E.addEventListener("click", block, { capture: true, once: true });
                setTimeout(() => t.E.removeEventListener("click", block, { capture: true }), 50);
                const dx = e.clientX - d.X;
                const step = t.PageStep();
                if (Math.abs(dx) > 40)
                    t.Go(d.Off + (dx < 0 ? step : -step));
                else
                    t.Go(d.Off);
            };
            t.E.addEventListener("pointerup", end);
            t.E.addEventListener("pointercancel", end);
        }
    }

    // ------------------------------------------------------------------ responsive columns

    /**
     * Responsive columns: as many columns of at least MinWidth as fit (css grid auto-fit), one column on narrow screens.
     * Options: MinWidth (px, default 320), Gap (css length, default var(--sw-gap) * 2), Max (maximum number of columns), Class, Content (elements / components).
     * Methods: Add(...items) (elements or components with .E).
     */
    class Columns {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            o = o || {};
            checkOpts("Columns", o);
            this.E = el("div.sw-columns", { class: o.Class || null });
            const s = this.E.style;
            s.setProperty("--sw-col-min", (o.MinWidth ?? 320) + "px");
            if (o.Gap)
                s.setProperty("--sw-col-gap", o.Gap);
            if (o.Max)
                s.setProperty("--sw-col-max", String(o.Max));
            if (o.Content)
                this.Add(...o.Content);
        }

        /**
         * Add items.
         * @param {...(Node|{E: Node})} items Elements or components.
         * @returns {Columns} This.
         */
        Add(...items) {
            for (const x of items)
                if (x)
                    this.E.append(x.E ? x.E : x);
            return this;
        }
    }

    // ------------------------------------------------------------------ page header

    /**
     * Turn a list of elements / components / texts into nodes (components contribute their E).
     * @param {Array<*>|*} items Items (null / false are skipped).
     * @returns {Node[]} The nodes.
     */
    function nodes(items) {
        return (Array.isArray(items) ? items : [items]).filter(x => x !== null && x !== undefined && x !== false)
            .map(x => x.E ? x.E : (x instanceof Node ? x : document.createTextNode(String(x))));
    }

    /**
     * Make a status badge from a status option.
     * @param {string|{Text:string, Kind:string, Dot:boolean, Title:string}|Node|{E:Node}} s The status.
     * @returns {Node|null} The badge.
     */
    function statusBadge(s) {
        if (!s)
            return null;
        if (s instanceof Node)
            return s;
        if (s.E)
            return s.E;
        const o = typeof s === "string" ? { Text: s } : s;
        const b = Sw.Badge ? Sw.Badge(o.Text, o.Kind, o.Title) : el("span.sw-badge", { text: o.Text });
        if (o.Dot)
            b.classList.add("sw-dot");
        return b;
    }

    /**
     * Page header (Design v2 page anatomy): the page title (h1, the heading font), a one line description, an optional status badge
     * and the page actions on the right (secondary buttons, at most one primary, destructive ones as danger outline, an overflow menu).
     * Put it first in the page container (sw-page-dash / -form / -read; on fill pages use Compact: smaller, hidden on phones where the
     * shell's top bar shows the title).
     * Options: Title, Description, Status (a text, {Text, Kind ("ok" | "error" | "warning" | "accent" | "info"), Dot, Title}, or an element),
     *          Actions (buttons / elements / components), Compact, Level (heading level, default 1), Class,
     *          MenuTitle (default true: inside the app shell a level 1 header of a page that is a main menu item shows the item's name,
     *          Sw.MenuTitle (ui/page.js), and the document title follows, so the h1, the breadcrumb and the menu say the same; false
     *          keeps Title, ex: a page that shows one record),
     *          Overflow (default true: actions that do not fit collapse into a "More actions" kebab menu, see below).
     * Overflow (every app): the actions stay on one line. A wide header keeps them beside the title (the text keeps at least
     * 20rem); a narrow one (phones) puts them on their own line under the text. When they do not fit, the last actions move into the
     * "More actions" menu (a kebab button at the end), in their order, with their text, icon, tool tip, disabled and danger look; the
     * menu item clicks the hidden button, so confirmations and busy states are the same. The primary action and controls that are not
     * buttons (a select, a segmented control) never move. An action with the class `sw-ph-in-menu` (`new Sw.Button({ Class:
     * "sw-ph-in-menu" })`) is always in the menu (a destructive action such as Delete, kept away from the daily ones). The header fits
     * again on every size change and when actions are added, removed, shown, hidden, enabled or renamed (also when a page changes
     * ActionsE directly). Compact headers of fill pages are hidden on phones, but a header with actions keeps its action row there.
     * Properties: E (the header), TitleE, DescE, StatusE (the status slot), ActionsE, MoreB (the kebab button), FromMenu (the title is the
     * menu item name).
     * Methods: SetTitle(text), SetDescription(text), SetStatus(status), SetActions(items), AddAction(item), FitActions() (fit now).
     */
    class PageHeader {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            checkOpts("PageHeader", o);
            const t = this;
            o = o || {};
            const level = Math.max(1, Math.min(6, o.Level || 1));
            const menu = level === 1 && o.MenuTitle !== false && typeof Sw.MenuTitle === "function" ? Sw.MenuTitle() : null;
            t.FromMenu = !!menu;
            //  A level 1 header that does not show the menu item's name (MenuTitle: false, a record page,
            //  or no menu item) makes the document title follow its Title / SetTitle, so the shell's breadcrumb says what the h1 says
            t.DocTitle = level === 1 && !menu;
            if (menu) {
                o = Object.assign({}, o, { Title: menu });
                if (document.title !== menu)
                    document.title = menu;
            }
            t.TitleE = el("h" + level + ".sw-ph-title");
            t.StatusE = el("span.sw-ph-status");
            t.DescE = el("p.sw-ph-desc");
            t.ActionsE = el("div.sw-ph-actions");
            t.E = el("header.sw-ph", { class: o.Class || null },
                el("div.sw-ph-text", null, el("div.sw-ph-titlerow", null, t.TitleE, t.StatusE), t.DescE), t.ActionsE);
            if (o.Compact)
                t.E.classList.add("sw-ph-compact");
            /** @type {WeakMap<Node, object>} The component of an action element (its text and icon for the overflow menu). */
            t.Comps = new WeakMap();
            /** @type {?object} The "More actions" kebab button at the end of the actions (only shown when actions overflow). */
            t.MoreB = null;
            if (o.Overflow !== false && Sw.Button) {
                const more = _TF("More actions", "Tool tip and accessible name of the kebab button of a page header that lists the page actions that do not fit");
                //  A menu button (UX guide INP-3): click, right click, long press, Menu key, Shift+F10; aria-haspopup + aria-expanded
                t.MoreB = new Sw.Button({ Icon: "kebab", Kind: "ghost", Title: more, Label: more, Class: "sw-ph-more", Menu: () => t.MoreItems(), MenuOptions: { Align: "end", Label: more } });
                t.MoreB.E.hidden = true;
                t.ActionsE.classList.add("sw-ph-fit");
            }
            t.SetTitle(o.Title);
            t.SetDescription(o.Description);
            t.SetStatus(o.Status);
            t.SetActions(o.Actions);
            if (t.MoreB) {
                let raf = 0;
                const later = () => {
                    if (!raf)
                        raf = requestAnimationFrame(() => {
                            raf = 0;
                            t.FitActions();
                        });
                };
                if (window.ResizeObserver)
                    new ResizeObserver(later).observe(t.E);
                if (window.MutationObserver)
                    new MutationObserver(recs => {
                        if (recs.some(r => r.target !== t.MoreB.E && r.target !== t.ActionsE || r.type === "childList"))
                            later();
                    }).observe(t.ActionsE, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ["hidden", "disabled"] });
                if (document.fonts && document.fonts.ready)
                    document.fonts.ready.then(later);
            }
        }

        /**
         * The action elements (without the kebab button).
         * @returns {HTMLElement[]} The elements.
         */
        ActionItems() {
            const m = this.MoreB ? this.MoreB.E : null;
            return Array.from(this.ActionsE.children).filter(c => c !== m);
        }

        /**
         * Fit the actions on one line (see the class description): show all, then move the last movable ones into the kebab menu
         * until the rest fits. Called on size and action changes; call it after changing an action's size in another way.
         */
        FitActions() {
            const t = this;
            if (!t.MoreB)
                return;
            const a = t.ActionsE;
            const more = t.MoreB.E;
            if (a.lastElementChild !== more)
                a.append(more);
            const items = t.ActionItems();
            items.forEach(c => c.classList.remove("sw-ph-overflowed"));
            more.hidden = true;
            //  (the actions the page shows: not hidden by the page, displayed)
            a.classList.remove("sw-ph-actions-none");
            const shown = items.filter(c => !c.hidden && c.getClientRects().length > 0);
            a.classList.toggle("sw-ph-actions-none", shown.length === 0);
            const W = t.E.clientWidth;
            //  Actions with the class sw-ph-in-menu always live in the menu (a destructive action, ex: Delete, that
            //  should not sit beside the daily ones; the table row kebab pattern)
            const always = shown.filter(c => c.classList.contains("sw-ph-in-menu"));
            always.forEach(c => c.classList.add("sw-ph-overflowed"));
            if (always.length)
                more.hidden = false;
            const fit = shown.filter(c => !c.classList.contains("sw-ph-in-menu"));
            if (!fit.length || !(W > 0))
                return;
            const cs = getComputedStyle(a);
            const gap = parseFloat(cs.columnGap) || 8;
            const hgap = parseFloat(getComputedStyle(t.E).columnGap) || 24;
            const ws = fit.map(c => c.getBoundingClientRect().width);
            const total = ws.reduce((s, w) => s + w, 0) + gap * (fit.length - 1) + (always.length ? gap + more.getBoundingClientRect().width : 0);
            //  Beside the text (it keeps at least 20rem) on wide headers, else a line of its own
            const textMin = 20 * (parseFloat(getComputedStyle(document.documentElement).fontSize) || 16);
            const besideRoom = W - textMin - hgap;
            if (total <= besideRoom || total <= W && W < 760)
                return;
            const room = W >= 760 ? Math.max(besideRoom, 0) : W;
            more.hidden = false;
            const moreW = more.getBoundingClientRect().width;
            /** Can the element move into the menu: a button / link / label, not the primary action. */
            const movable = c => /^(BUTTON|A|LABEL)$/.test(c.tagName) && !c.classList.contains("sw-primary");
            let used = always.length ? total : total + gap + moreW;
            for (let i = fit.length - 1; i >= 0 && used > room; --i) {
                if (!movable(fit[i]))
                    continue;
                fit[i].classList.add("sw-ph-overflowed");
                used -= ws[i] + gap;
            }
            if (!shown.some(c => c.classList.contains("sw-ph-overflowed")))
                more.hidden = true;
        }

        /** Open the "More actions" menu with the actions that do not fit (FitActions). */
        ShowMore() {
            const t = this;
            if (t.MoreB && t.MoreB.OpenMenu)
                t.MoreB.OpenMenu();
        }

        /**
         * The items of the "More actions" menu: the actions that do not fit (FitActions), destructive ones last.
         * @returns {Array} Sw.Menu items.
         */
        MoreItems() {
            const t = this;
            const items = t.ActionItems().filter(c => c.classList.contains("sw-ph-overflowed") && !c.hidden).map(c => {
                const comp = t.Comps.get(c);
                const text = (comp && comp.Text) || c.textContent.trim() || c.getAttribute("aria-label") || c.title || "";
                const pressed = c.getAttribute("aria-pressed");
                return {
                    Text: text,
                    Icon: (comp && comp.IconName) || undefined,
                    Title: c.title && c.title !== text ? c.title : undefined,
                    Disabled: !!c.disabled || c.getAttribute("aria-disabled") === "true",
                    Danger: c.classList.contains("sw-danger") || c.classList.contains("sw-danger-solid"),
                    Checked: pressed === null ? undefined : pressed === "true",
                    OnClick: () => c.click(),
                };
            });
            //  (destructive actions last, after a separator: Design v2, PORTING.md B12)
            const di = items.findIndex(x => x.Danger);
            if (di > 0 && items.slice(di).every(x => x.Danger))
                items.splice(di, 0, "-");
            return items;
        }

        /**
         * Change the title.
         * @param {string} text The title.
         */
        SetTitle(text) {
            this.TitleE.textContent = text || "";
            if (this.DocTitle && text && document.title !== String(text))
                document.title = String(text);
        }

        /**
         * Change the description (empty hides it).
         * @param {string} text The description.
         */
        SetDescription(text) {
            this.DescE.textContent = text || "";
        }

        /**
         * Change the status badge (null removes it).
         * @param {string|object|Node|null} s The status (see the class description).
         */
        SetStatus(s) {
            const b = statusBadge(s);
            this.StatusE.replaceChildren(...(b ? [b] : []));
            this.StatusE.hidden = !b;
        }

        /**
         * Replace the actions.
         * @param {Array<*>} items Buttons / elements / components.
         */
        SetActions(items) {
            this.Remember(items);
            //  (the focused action keeps its place when the actions are replaced: FOC-1)
            const keep = Sw.Focus && Sw.Focus.Keep ? Sw.Focus.Keep(this.ActionsE) : null;
            this.ActionsE.replaceChildren(...(items ? nodes(items) : []), ...(this.MoreB ? [this.MoreB.E] : []));
            if (keep) {
                keep.Restore();
                keep.Stop();
            }
        }

        /**
         * Append an action.
         * @param {*} item A button / element / component.
         */
        AddAction(item) {
            this.Remember(item);
            const n = nodes(item);
            if (this.MoreB)
                this.MoreB.E.before(...n);
            else
                this.ActionsE.append(...n);
        }

        /**
         * Remember the components of action elements (their text and icon name for the overflow menu).
         * @param {*} items Components / elements, or an array of them.
         */
        Remember(items) {
            (Array.isArray(items) ? items : [items]).forEach(x => {
                if (x && x.E instanceof Node)
                    this.Comps.set(x.E, x);
            });
        }
    }

    // ------------------------------------------------------------------ empty state

    /**
     * Empty / error state for a region (a table without rows, a null result, a failed load): an icon in a sunken circle, a title, one
     * sentence, actions ("Clear filters", "Try again").
     * Options: Icon (an Sw.Icons name, default "info"; "warning" for errors), Title, Text, Actions (buttons / elements), Error (danger look,
     * role alert), Class.
     * Properties: E. Methods: Set({Icon, Title, Text, Actions, Error}).
     */
    class EmptyState {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            checkOpts("EmptyState", o);
            this.E = el("div.sw-empty", { class: (o && o.Class) || null });
            this.Set(o || {});
        }

        /**
         * Change the content.
         * @param {object} o Options (see the class description).
         */
        Set(o) {
            const e = this.E;
            e.classList.toggle("sw-error", !!o.Error);
            e.setAttribute("role", o.Error ? "alert" : "status");
            //  (a focused action, ex: Try again, keeps the focus on its successor: FOC-1)
            const keep = Sw.Focus && Sw.Focus.Keep ? Sw.Focus.Keep(e) : null;
            e.replaceChildren(
                el("div.sw-empty-icon", null, Icon(o.Icon || (o.Error ? "warning" : "info"))),
                el("h3.sw-empty-title", { text: o.Title || "" }),
                el("p.sw-empty-text", { text: o.Text || "" }),
                el("div.sw-empty-actions", null, ...(o.Actions ? nodes(o.Actions) : [])));
            if (keep) {
                keep.Restore();
                keep.Stop();
            }
        }
    }

    //  A component is not text (FD R4 F16: native append of a component showed "[object Object]"): "" and a developer warning
    for (const cls of [Tabs, Split, Pager, Columns, PageHeader, EmptyState])
        if (!Object.prototype.hasOwnProperty.call(cls.prototype, "toString"))
            Object.defineProperty(cls.prototype, "toString", {
                value() {
                    Sw.DevWarn("A Sw." + cls.name + " was turned into text (native append / innerHTML / a template string): it shows nothing. Append its element (x.E), or pass the component to Sw.el / a Sw container (they take components).", "comp-text:" + cls.name);
                    return "";
                },
                configurable: true, writable: true,
            });

    Object.assign(Sw, {
        Tabs,
        Split,
        Pager,
        Columns,
        //  Callable with or without new (Sw.PageHeader({...}) and new Sw.PageHeader({...}) both return the component)
        PageHeader: function PageHeaderFactory(o) {
            return new PageHeader(o);
        },
        EmptyState: function EmptyStateFactory(o) {
            return new EmptyState(o);
        },
    });
})();
