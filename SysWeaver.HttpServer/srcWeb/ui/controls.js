/**
 * @file ui/controls.js - SysWeaver UI library (Sw), part 2 of 7: input controls.
 *
 * Button, Group (a card with a header row), Segmented (segmented control), Toggle (switch), Slider, NumberBox, TextBox, Select (drop down, searchable for long lists),
 * ColorPicker, Badge, Chip, ProgressBar, Spinner, ButtonRow and CopyButton.
 * Needs ui/base.js. Select and ColorPicker open popups from ui/overlay.js (looked up when opened, so the load order of the two files
 * doesn't matter). Documentation: ui/README.md.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const { el, clamp, snap, Icon } = Sw;
    const formatNumber = Sw.FormatNumber;

    /**
     * One name per concept (ui/README "Naming rules"): the old names stay as aliases. The tone of a red / green state is "danger" /
     * "ok" everywhere ("error" and "success" are read as the same).
     * @param {string} kind A kind.
     * @param {boolean} [errorName] Return "error" for the red tone (badges, toasts: their css says sw-error).
     * @returns {string} The kind.
     */
    function canonKind(kind, errorName) {
        if (kind === "error" || kind === "danger")
            return errorName ? "error" : "danger";
        if (kind === "success")
            return "ok";
        return kind;
    }

    /**
     * The items of a list option ([{Value, Text}]): Items or its alias Options, each item's Text or its alias Label (Select: the other
     * way round), plain values become {Value, Text}.
     * @param {Array<*>} list The items.
     * @param {string} textKey The text key the control reads ("Text" or "Label").
     * @returns {Array<object>} The items.
     */
    function canonItems(list, textKey) {
        const other = textKey === "Text" ? "Label" : "Text";
        return (list || []).map(x => {
            if (typeof x !== "object" || x === null)
                return { Value: x, [textKey]: String(x) };
            if (x[textKey] === undefined && x[other] !== undefined)
                return Object.assign({}, x, { [textKey]: x[other] });
            return x;
        });
    }

    // ------------------------------------------------------------------ the options of each control (Sw.DevOptions, DevMode only)

    /** The type name of a text option (a constant: the schema below is no list of labels). */
    const Str = "string";
    /** A Label and an AriaLabel together: AriaLabel wins and the visible label is not shown. */
    const LabelCollide = { Type: Str, Collides: ["AriaLabel", "AriaLabel only names the control: the visible Label is not shown. Pass one of them (Label = visible + name)."] };
    /** Options every value control reads (dirty tracking, validation). */
    const ValueOpts = { Dirty: "boolean|string", Required: "boolean|string", Validate: "function", Messages: "array", OnChange: "function", Disabled: "boolean|string", Title: Str };
    /**
     * @param {Array<object>} items The items / options. @param {*} v The value.
     * @returns {string} A message when the value is not one of them.
     */
    const notAmong = (items, v) => v !== undefined && v !== null && v !== "" && Array.isArray(items) && items.length && !canonItems(items, "Text").some(x => x.Value === v)
        ? "Value " + JSON.stringify(v).slice(0, 40) + " is not one of the items (compared with ===: a number 1 is not the text \"1\"); nothing is shown as selected." : "";
    const Schema = {
        Button: {
            Text: Str, Icon: Str, Title: Str, Label: Str, AriaLabel: Str, Small: "boolean", Block: "boolean",
            Kind: { Type: Str, Check: v => /^(primary|accent|ghost|danger|danger-solid|error|secondary|link|success|ok|warning)$/.test(v) ? "" : '"' + v + '" is no kind (primary, accent, ghost, danger, danger-solid; default secondary).' },
            Pressable: "boolean", Pressed: "boolean", AutoToggle: "boolean", Repeat: "boolean", Disabled: "boolean|string", Class: Str,
            Type: Str, IconRight: "boolean", Menu: "array|function", MenuOptions: "object", Copy: "any", Done: "boolean|string",
            Shortcut: Str, OnClick: "function", Saves: "boolean", Leave: "boolean", Working: "boolean", Submit: "boolean", Hidden: "boolean",
            $Check: o => [
                o.Pressed !== undefined && !o.Pressable ? 'Pressed without Pressable: the button is no toggle, Pressed is ignored.' : "",
                o.Repeat && o.Menu ? "Repeat and Menu collide: a held press repeats, the menu never opens." : "",
            ],
        },
        Segmented: Object.assign({ Items: "array", Options: "array", Value: "any", Label: Str, AriaLabel: Str, Class: Str, Small: "boolean" }, ValueOpts, {
            $Check: o => [o.Items && o.Options ? "Items and Options are the same option (Options is the alias): pass one." : "", notAmong(o.Items || o.Options, o.Value)],
        }),
        Toggle: Object.assign({ Label: Str, Value: "boolean" }, ValueOpts),
        Slider: {
            Min: "number", Max: "number", Step: "number", Value: "number", Default: "number", Log: "boolean", Vertical: "boolean", Fader: "boolean",
            Accent2: "boolean", Center: "number", Title: Str, Label: Str, AriaLabel: Str, Disabled: "boolean|string",
            OnInput: "function", OnChange: "function", OnStart: "function", OnEnd: "function", Dirty: "boolean|string",
            $Check: o => [
                o.Log && !((o.Min ?? 0) > 0) ? "Log needs Min > 0: the slider is linear." : "",
                (o.Max ?? 1) <= (o.Min ?? 0) ? "Max must be above Min (Min " + (o.Min ?? 0) + ", Max " + (o.Max ?? 1) + "; the default range is 0..1)." : "",
                typeof o.Value === "number" && (o.Value < (o.Min ?? 0) || o.Value > (o.Max ?? 1)) ? "Value " + o.Value + " is outside Min..Max (" + (o.Min ?? 0) + ".." + (o.Max ?? 1) + "; the default range is 0..1): it is clamped." : "",
            ],
        },
        NumberBox: Object.assign({
            Min: "number", Max: "number", Step: "number", Value: "number", Width: Str, Label: LabelCollide, AriaLabel: Str, Hint: Str,
            Format: "function", Class: Str, EscapeReverts: "boolean",
        }, ValueOpts, {
            $Check: o => [typeof o.Value === "number" && ((o.Min !== undefined && o.Value < o.Min) || (o.Max !== undefined && o.Value > o.Max)) ? "Value " + o.Value + " is outside Min..Max: it is clamped." : ""],
        }),
        TextBox: Object.assign({
            Value: "string|number", Placeholder: Str, Label: LabelCollide, AriaLabel: Str, Hint: Str,
            Type: { Type: Str, Check: v => /^(text|password|email|search|url|tel|number)$/.test(v) ? "" : '"' + v + '" is no type (text, password, email, search, url, tel).' },
            Multiline: "boolean", Rows: "number", Clear: "boolean", Reveal: "boolean", Icon: Str, MaxLength: "number", AutoComplete: Str,
            InputMode: Str, SpellCheck: "boolean", Width: Str, ReadOnly: "boolean", Mono: "boolean", OnInput: "function", OnEnter: "function",
            Draft: "string|object|boolean", Saves: "boolean", Leave: "boolean", Copy: "boolean|string", EscapeReverts: "boolean",
        }, ValueOpts, {
            $Check: o => [
                o.Reveal && o.Type !== "password" ? 'Reveal works only with Type: "password".' : "",
                o.Copy && o.Type === "password" ? "Copy on a password box: a password is never offered for copying (UX guide SEC-1); the copy button is not shown." : "",
                o.Multiline && o.OnEnter ? "OnEnter never runs in a Multiline box (Enter adds a line): use a button." : "",
                o.Clear && o.ReadOnly ? "Clear on a ReadOnly box: the clear button is never shown." : "",
            ],
        }),
        Select: Object.assign({
            Options: "array", Items: "array", Value: "any", Placeholder: Str, Small: "boolean", Width: Str, Search: "boolean",
            SearchFrom: "number", Label: LabelCollide, AriaLabel: Str, Hint: Str,
        }, ValueOpts, {
            $Check: o => [o.Items && o.Options ? "Items and Options are the same option (two names for the list): pass one." : "",
                o.Placeholder ? "" : notAmong(o.Options || o.Items, o.Value).replace("nothing is shown as selected", "the raw value is shown")],
        }),
        ColorPicker: { Value: "array|string", Hex: "boolean", Title: Str, Disabled: "boolean|string", OnInput: "function", OnChange: "function", Dirty: "boolean|string" },
        Chip: { Text: Str, Icon: Str, Title: Str, Pressed: "boolean", Pressable: "boolean", Removable: "boolean", Disabled: "boolean|string", OnChange: "function", OnRemove: "function", OnClick: "function" },
        Field: { Label: Str, Hint: Str, Control: "element|component", Width: Str, Class: Str, Required: "boolean|string" },
        ProgressBar: {
            Value: "number",
            Text: Str, Title: Str, Indeterminate: "boolean", Small: "boolean", Label: Str, AriaLabel: Str,
        },
        Group: { Title: Str, Sub: Str, Actions: "array", Tools: "array", Class: Str, Flush: "boolean", Content: "array" },
        Spinner: { Size: "number|string", Title: Str, Class: Str },
        CopyButton: {
            Value: "any", Get: "function", What: Str, Title: Str, Label: Str, Generic: "boolean", Secret: "boolean", Small: "boolean", Kind: Str,
            Text: Str, Class: Str,
        },
    };
    //  The one registry of option schemas (Sw.DevOptions.Register through Sw.Work.RegisterOptions): every control is known to it
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function")
        for (const k of Object.keys(Schema))
            Sw.Work.RegisterOptions("Sw." + k, Schema[k]);
    if (Sw.DevOptions && typeof Sw.DevOptions.NoOptions === "function")
        Sw.DevOptions.NoOptions("Sw.ListPicker", "opts is the list of items; po goes to Sw.Popup.Show (checked there)");

    // ------------------------------------------------------------------ unsaved edits of the controls (UX guide SAF-10)

    /**
     * Value controls (TextBox, NumberBox, Select, Toggle, Segmented) with Dirty: true (or the leave text; opt in, P3-A-4: the original
     * pages never asked "Leave site?") mark the page dirty by themselves (Sw.Page.SetDirty), so leaving with unsaved edits asks
     * "Leave without saving?" without page code:
     *  - dirty = the user changed the value away from its base (the Value option, or the last Set() from code: loading data is not an
     *    edit; changing it back is clean again);
     *  - grouped per container (the closest card, group, form or section): one sentence "Your changes to <card title> are lost.";
     *  - clean again when it is saved: an async action of a primary (or Saves: true) Sw.Button in the same container (or anywhere when the
     *    button is in no container) ends without an error, OnChange returned a promise / started a request that succeeded (an auto
     *    saving control), MarkClean() on the control, or the control is removed;
     *  - not tracked: Dirty: false; a control of a Sw.Form (the form tracks it) or inside a dialog (the dialog asks, POP-6); search boxes
     *    (Type or Icon "search"); read only boxes; a TextBox with a Draft (the draft keeps the text); a committed value whose OnChange
     *    returns nothing (it applies the value at once: a filter, a view option, a setting); controls in toolbars, page headers, card tools and tables.
     * Every user change also dispatches a bubbling "sw-change" event on the control's E: detail { Control, Value, Changed (differs from
     * the base) } (dialogs use it to compare with the opening values).
     */
    const dirtyGroups = new Map();

    // ------------------------------------------------------------------ who changed it: the user or code

    /**
     * The user's own changes (UX guide FRM-5, SAF-10; FD R4 F2: a value the work wrote into a field is not an edit): a trusted input /
     * change event of a field, and every user change of a Sw control (a click, a key, a pick; userEdit). A value set by code (Set(),
     * .value, a dispatched event) is never in it. Sw.Edits.UserSince(root, since) / UserAt(root) read it.
     * @type {Array<{E: Element, At: number}>}
     */
    const userEdits = [];

    /** Sw.Edits (filled at the end of the file): the times of an auto save that does not answer. */
    const Edits = { SlowMs: 10000, GiveUpMs: 30000 };

    /**
     * Note a change by the user.
     * @param {?Element} e The field / control element.
     */
    function noteUser(e) {
        if (!e || e.nodeType !== 1)
            return;
        userEdits.push({ E: e, At: performance.now() });
        if (userEdits.length > 300)
            userEdits.splice(0, userEdits.length - 300);
    }
    ["input", "change"].forEach(n => document.addEventListener(n, ev => {
        if (ev.isTrusted)
            noteUser(ev.target);
    }, true));

    /**
     * The time (performance.now()) of the user's last change inside an element.
     * @param {?Element} [root] The element (a dialog body, a card, a field; default: anywhere).
     * @returns {number} The time, 0 when the user changed nothing there.
     */
    function userAt(root) {
        root = root && root.E instanceof Element ? root.E : root;
        for (let i = userEdits.length - 1; i >= 0; --i) {
            const x = userEdits[i];
            if (!root || root === x.E || (root.contains && root.contains(x.E)))
                return x.At;
        }
        return 0;
    }
    /** @type {number} The prune timer (removed controls drop their dirty state). */
    let dirtyTimer = 0;
    /** Containers that hold no document data (their controls filter, navigate or apply at once). */
    const NoDirtySel = "[aria-modal='true'], .sw-dialog, [data-sw-nodirty], [role=toolbar], [role=search], [role=menu], .sw-page-head, .sw-page-header, .sw-card-tools, .sw-group-tools, .sw-table, .sw-pager, header, nav, .sw-shell";
    /** Containers that group the dirty state of their controls. */
    const GroupSel = "[data-sw-dirty-group], form, .sw-card, .sw-group, section, .sw-steps";

    /**
     * Compare two values (primitives, arrays, plain objects).
     * @param {*} a A value. @param {*} b A value.
     * @returns {boolean} True when equal.
     */
    function sameValue(a, b) {
        if (a === b)
            return true;
        if ((a === null || a === undefined || a === "") && (b === null || b === undefined || b === ""))
            return true;
        try {
            return JSON.stringify(a) === JSON.stringify(b);
        }
        catch {
            return false;
        }
    }

    /**
     * Test if a control's page dirty state is not tracked.
     * @param {object} t The control.
     * @returns {boolean} True to skip.
     */
    function dirtySkip(t) {
        const o = t.O || {};
        //  (a field drafted by hand, Sw.Draft.Attach(box.Input): the draft keeps the text, it is not "lost")
        const hand = t.Input && t.Input.SwDraft;
        //  Opt in (Dirty: true or the leave text): the original pages never asked "Leave site?" for a typed field (P3-A-4)
        if (!(o.Dirty === true || typeof o.Dirty === "string"))
            return true;
        if (t.SwForm || (t.DraftH && t.DraftH.Active !== false) || (hand && hand.Active !== false) || o.ReadOnly || o.Type === "search" || o.Type === "password" || o.Icon === "search" || !t.E || !t.E.isConnected)
            return true;
        return !!t.E.closest(NoDirtySel);
    }

    /**
     * The group of a control (its container) with the sentence of the leave question.
     * @param {object} t The control.
     * @returns {{Key: string, Text: (string|boolean), C: Set<object>, Root: Element}} The group.
     */
    function dirtyGroup(t) {
        const root = t.E.parentElement ? t.E.parentElement.closest(GroupSel) : null;
        const host = root || document.body;
        if (!host.SwDirtyKey)
            host.SwDirtyKey = "sw-edit:" + (root ? uid("g") : "page");
        let g = dirtyGroups.get(host.SwDirtyKey);
        if (!g) {
            g = { Key: host.SwDirtyKey, C: new Set(), Root: host };
            dirtyGroups.set(g.Key, g);
        }
        const te = root && root.querySelector(".sw-card-title, .sw-group-title, .sw-steps-title, h1, h2, h3, legend");
        const name = (root && (root.getAttribute("data-sw-dirty-group") || root.getAttribute("aria-label"))) || (te && te.textContent.trim()) || "";
        g.Text = name
            ? _T("Your changes to {0} are lost.", name, "Text of the question 'Leave without saving?'. {0} names what was edited, ex: a card title 'Mail settings'")
            : _TF("Your changes on this page are lost.", "Text of the question 'Leave without saving?' when the edited part has no name");
        return g;
    }

    /** Update the page dirty mark of every group (removed controls are dropped). */
    function dirtySync() {
        for (const g of dirtyGroups.values()) {
            for (const c of Array.from(g.C))
                if (!c.E || !c.E.isConnected)
                    g.C.delete(c);
            if (Sw.Page && Sw.Page.SetDirty)
                Sw.Page.SetDirty(g.Key, g.C.size && !g.Suspended ? g.Text : null);
            if (!g.C.size)
                dirtyGroups.delete(g.Key);
        }
        if (dirtyGroups.size && !dirtyTimer)
            dirtyTimer = setInterval(() => {
                dirtySync();
                if (!dirtyGroups.size) {
                    clearInterval(dirtyTimer);
                    dirtyTimer = 0;
                }
            }, 2000);
    }

    /**
     * Set the dirty state of a control.
     * @param {object} t The control.
     * @param {boolean} on Dirty.
     */
    function setCtlDirty(t, on) {
        t.IsDirtyEdit = !!on;
        if (on) {
            const g = dirtyGroup(t);
            g.C.add(t);
            t.DirtyGroup = g;
            t.DirtyAt = Date.now();
            watchPageDirty();
            //  The page marks the same edit itself (its own SetDirty right with the user's edit): the leave question would say it twice
            const pk = watchPageDirty.Last;
            if (pk && Math.abs(Date.now() - pk.At) < 1500)
                Sw.DevWarn('The page marks itself dirty (SetDirty("' + pk.Key + '")) for an edit that "' + (ctlName(t) || "a control") + '" tracks already (it opted in with Dirty and marks the page itself): the leave question says it twice. Remove your SetDirty, or the Dirty option of the control (UX guide SAF-10).', "dirty-twice:" + pk.Key);
        }
        else if (t.DirtyGroup)
            t.DirtyGroup.C.delete(t);
        dirtySync();
    }

    /**
     * Follow the page's own Sw.Page.SetDirty calls (not the framework's keys), so a second mark of the same edit gives a developer
     * warning (setCtlDirty). Installed once, only in Sw.DevMode().
     */
    function watchPageDirty() {
        if (watchPageDirty.Done || !Sw.Page || typeof Sw.Page.SetDirty !== "function" || !(typeof Sw.DevMode === "function" && Sw.DevMode()))
            return;
        watchPageDirty.Done = true;
        const f = Sw.Page.SetDirty;
        Sw.Page.SetDirty = function (key, text, opts) {
            const k = String(key || "page");
            if (text !== null && text !== false && text !== "" && !(opts && (opts.Work || opts.Draft)) && !/^(sw-|draft:)/.test(k)) {
                watchPageDirty.Last = { Key: k, At: Date.now() };
                //  (a Sw.Form that tracks itself, page.js)
                if (Sw.Form && Sw.Form.DirtyAt && Date.now() - Sw.Form.DirtyAt < 1500)
                    Sw.DevWarn('The page marks itself dirty (SetDirty("' + k + '")) for an edit its Sw.Form tracks already (it opted in with Dirty and marks the page itself): the leave question says it twice. Remove your SetDirty, or the Dirty option of the form (UX guide SAF-10).', "dirty-twice-form:" + k);
                //  (the other order: the control was marked first)
                for (const g of dirtyGroups.values())
                    for (const c of g.C)
                        if (c.DirtyAt && Date.now() - c.DirtyAt < 1500)
                            Sw.DevWarn('The page marks itself dirty (SetDirty("' + k + '")) for an edit that "' + (ctlName(c) || "a control") + '" tracks already (it opted in with Dirty and marks the page itself): the leave question says it twice. Remove your SetDirty, or the Dirty option of the control (UX guide SAF-10).', "dirty-twice:" + k);
            }
            return f.apply(this, arguments);
        };
    }

    /**
     * A control's value was set from code (loading data): the new base, not an edit.
     * @param {object} t The control.
     * @param {*} v The value.
     */
    function setBase(t, v) {
        t.Base = v;
        t.UserEdited = false;
        if (t.IsDirtyEdit)
            setCtlDirty(t, false);
    }

    /**
     * A value set by code on the user's behalf is the user's edit (SAF-10: a "Same as billing" button copied the address
     * with Set(), the page stayed clean and leaving lost it): Set() inside the synchronous part of work the user started (a Sw.Button
     * OnClick, Sw.OnClick, Enter, a pick: Sw.Work.InUser) counts like typing, so the page is dirty until it is saved. Not in a dialog,
     * a toolbar or a table (a dialog compares its own values: a value its work writes is no newer edit). The second argument of Set
     * decides explicitly: { User: true } (always the user's), { User: false } (loading data inside a click: the new base).
     * @param {object} t The control.
     * @param {*} v The value set.
     * @param {*} opt Set's second argument (true = silent, or { Silent, User }).
     * @returns {boolean} True when it is handled as the user's edit (the caller does not make it the base).
     */
    function codeSet(t, v, opt) {
        const given = opt && typeof opt === "object" && typeof opt.User === "boolean" ? opt.User : null;
        const user = given !== null ? given : !!(Sw.Work && typeof Sw.Work.InUser === "function" && Sw.Work.InUser());
        if (!user || !(t.E instanceof Element) || !t.E.isConnected || (given === null && t.E.closest(NoDirtySel)) || sameValue(v, t.Base))
            return false;
        queueMicrotask(() => {
            if (!t.Destroyed && t.E)
                userEdit(t, t.Get(), null);
        });
        return true;
    }

    /**
     * Set's second argument: true / false (silent: OnChange is not called) or { Silent, User }.
     * @param {*} opt The argument.
     * @returns {boolean} Silent.
     */
    function silentOf(opt) {
        return opt && typeof opt === "object" ? !!opt.Silent : !!opt;
    }

    /**
     * A user changed a control's value: dispatch sw-change and update the page dirty state.
     * @param {object} t The control.
     * @param {*} v The new value.
     * @param {object} [sc] The Sw.Work scope of its OnChange (a promise / request = it saves itself), or "applied" (the value applies
     * at once: a Select / Toggle / Segmented with OnChange).
     */
    function userEdit(t, v, sc, quiet) {
        const changed = !sameValue(v, t.Base);
        //  (not user data: a type-to-confirm word, a filter; the dialog's dirty check reads Changed: UX guide POP-6)
        const noData = !!(t.O && t.O.Dirty === false) || !!(t.E && t.E.closest && t.E.closest("[data-sw-nodirty]")) || !!(t.Input && t.Input.hasAttribute && t.Input.hasAttribute("data-sw-nodirty"));
        //  The user's change (User: true): only these count as edits / "newer edits"; code writes (Set, .value) never come here
        t.UserEdited = true;
        if (!quiet)
            noteUser(t.E);
        if (t.E && !quiet)
            t.E.dispatchEvent(new CustomEvent("sw-change", { bubbles: true, detail: { Control: t, Value: v, Changed: changed && !noData, NoData: noData, User: true } }));
        //  A change made while this control's save runs waits for it (callChange): unsaved until its turn
        if (sc && typeof sc === "object" && sc.Queued) {
            if (!dirtySkip(t))
                setCtlDirty(t, changed);
            return;
        }
        const auto = sc && sc !== "applied" && typeof sc === "object";
        const p = auto && !sc.Threw ? Sw.Work.Promise(sc) : null;
        //  An auto save (OnChange returned a promise / started a request, or threw): the one result path (Sw.Work.Outcome), also for
        //  controls the page dirty state skips (a toolbar, a table, a dialog): a failure reverts a picked value and says why (FBK-5).
        //  One save at a time per control, the latest value wins (F4: four fast clicks ran four saves that ended out of order, the
        //  switch showed off, the server had on): changes made while it runs are queued (callChange) and only the newest is sent next
        if (auto && (p || sc.Threw)) {
            const base = t.Base;
            const watch = Sw.Work.Watch();
            //  A save that never answers (FD R4 P7: the control stayed busy for ever, newer values never sent): "still saving" after
            //  SlowMs, then after GiveUpMs a failure like any other (the saved value back, the reason said, the next change goes); a
            //  late answer after that is taken ("saved after all")
            let ended = false, slow = 0, give = 0;
            if (p) {
                t.SaveRun = { V: v };
                if (t.E)
                    t.E.setAttribute("aria-busy", "true");
                slow = setTimeout(() => {
                    if (ended || t.Destroyed)
                        return;
                    ctlStatus(t, ctlName(t)
                        ? _T("{0}: still saving…", ctlName(t), "Line under a setting that saves itself (a switch, a choice) whose save has not answered for 10 seconds. {0} is its label, ex: 'Weekly digest'")
                        : _TF("Still saving…", "Line under a setting that saves itself whose save has not answered for 10 seconds"));
                    Sw.DevWarn('The OnChange save of "' + (ctlName(t) || "a control") + '" has not settled after ' + Math.round(Edits.SlowMs / 1000) + " s. Give its request a time limit (Sw.Net options) or resolve / reject it; after " + Math.round(Edits.GiveUpMs / 1000) + " s the control treats it as failed (UX guide FBK-4).", "change-slow:" + ctlName(t));
                }, Edits.SlowMs);
                give = setTimeout(() => {
                    if (ended || t.Destroyed)
                        return;
                    const x = new Error(_TF("There was no answer from the service.", "Reason after a setting that saves itself got no answer from the service for a long time"));
                    x.SwTimeout = true;
                    end({ Failed: true, Error: x });
                }, Edits.GiveUpMs);
            }
            const late = res => {
                //  (the answer after the control gave up: a success means the server has the value after all)
                const out = Sw.Work.Outcome(sc, res);
                if (!out.Ok || t.Destroyed || t.SaveRun || !sameValue(t.Get(), base))
                    return;
                t.Set(v, true);
                if (typeof t.SetError === "function")
                    t.SetError(null);
                const name = ctlName(t);
                if (Sw.Page && Sw.Page.Announce)
                    Sw.Page.Announce(name ? _T("{0}: saved after all.", name, "Announced when a setting whose save had given up (no answer) was saved by the server later. {0} is its label") : _TF("Saved after all.", "Announced when a setting whose save had given up was saved by the server later"));
            };
            const end = res => {
                if (ended) {
                    late(res);
                    return;
                }
                ended = true;
                clearTimeout(slow);
                clearTimeout(give);
                ctlStatus(t, null);
                const out = Sw.Work.Outcome(sc, res);
                const said = watch.Stop();
                const next = t.SaveNext;
                t.SaveNext = null;
                t.SaveRun = null;
                if (next) {
                    //  The server has v now (or still the old base when it failed); the newest change goes next, unless it is the
                    //  server's value already (then nothing is unsaved and an older failure is moot)
                    if (out.Ok)
                        setBase(t, v);
                    const nv = next.Args[0];
                    if (!t.Destroyed && !sameValue(nv, t.Base)) {
                        const sc2 = callChange(t, next.Fn, next.Args);
                        userEdit(t, nv, savesItself(sc2) ? sc2 : "applied", true);
                        if (!t.SaveRun && t.E)
                            t.E.removeAttribute("aria-busy");
                        return;
                    }
                    if (t.E)
                        t.E.removeAttribute("aria-busy");
                    if (t.IsDirtyEdit && sameValue(t.Get(), t.Base))
                        setCtlDirty(t, false);
                    return;
                }
                if (t.E)
                    t.E.removeAttribute("aria-busy");
                if (out.Ok) {
                    if (sameValue(t.Get(), v))
                        setBase(t, v);
                    return;
                }
                autoSaveFailed(t, v, base, out, said);
            };
            if (sc.Threw)
                end({ Failed: true, Error: sc.Error });
            else
                Promise.resolve(p).then(value => end({ Value: value }), x => end({ Failed: true, Error: x }));
            if (!dirtySkip(t) && p)
                setCtlDirty(t, changed);
            return;
        }
        //  Tracked only when the control opted in (Dirty: true or the leave sentence; P3-A-4, the original never asked): an opted-in
        //  control is tracked whenever the user changes it (the automatic save-path detection went with the opt in; S5-A-4)
        if (dirtySkip(t)) {
            if (t.IsDirtyEdit)
                setCtlDirty(t, false);
            return;
        }
        if (sc === "applied") {
            setBase(t, v);
            return;
        }
        setCtlDirty(t, changed);
    }

    /**
     * An auto save failed (the OnChange threw, rejected, a request it started failed, or it resolved false): a picked value goes back
     * to the saved one (a typed text stays, it is the user's work), and the reason is shown once (FBK-5): at the control when it is on
     * the screen (its message line, announced), else as a toast; nothing more when the handler showed it itself. An abort says nothing.
     * @param {object} t The control.
     * @param {*} v The value that was not saved.
     * @param {*} base The saved value before the change.
     * @param {object} out The outcome (Sw.Work.Outcome).
     * @param {boolean} said The handler showed a message itself.
     */
    function autoSaveFailed(t, v, base, out, said) {
        const typed = t instanceof TextBox;
        const back = !typed && sameValue(t.Get(), v) && !out.Aborted;
        if (back) {
            t.InUser = true;
            try {
                t.Set(base, true);
            }
            finally {
                t.InUser = false;
            }
            if (t.IsDirtyEdit)
                setCtlDirty(t, false);
            if (t.E)
                t.E.dispatchEvent(new CustomEvent("sw-change", { bubbles: true, detail: { Control: t, Value: t.Get(), Changed: false, Reverted: true, User: false } }));
        }
        if (out.Aborted)
            return;
        if (out.NotDone && !said)
            Sw.DevWarn('The OnChange of "' + ctlName(t) + '" resolved false and showed nothing: say why (a toast, or throw an Error with the reason; UX guide FBK-5). The user sees "not saved".', "change-notdone:" + ctlName(t));
        if (said)
            return;
        const name = ctlName(t);
        let why = "";
        if (out.Error) {
            if (Sw.Net && Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(out.Error))
                why = _TF("The connection to the service was lost.", "Reason after a setting could not be saved because the service could not be reached");
            else {
                //  (the error object: its status and the server's reason, never the generic sentence of its status alone)
                const m = out.Error && out.Error.message ? out.Error.message : String(out.Error);
                why = Sw.Net && Sw.Net.ErrorText ? (Sw.Net.ErrorText(typeof out.Error === "object" ? out.Error : m).Text || m) : m;
            }
            console.warn(out.Error);
        }
        const msg = (name
            ? _T("{0}: not saved.", name, "Message after a setting that saves itself (a switch, a choice) could not be saved. {0} is its label, ex: 'Weekly digest'")
            : _TF("Not saved.", "Message after a setting that saves itself could not be saved"))
            + (why ? " " + why + (/[.!?:]$/.test(why) ? "" : ".") : "")
            + (back ? " " + _TF("The previous value is back.", "Added to 'not saved' after a switch or choice went back to its saved value") : "");
        //  In place when it is on the screen (its message line), else a toast: one place (FBK-2)
        const r = t.E && t.E.isConnected ? t.E.getBoundingClientRect() : null;
        const onScreen = !!(r && r.width && r.bottom > 0 && r.top < (window.innerHeight || 0) && document.visibilityState !== "hidden");
        if (onScreen && typeof t.SetError === "function") {
            //  Said once (F17b): the message line is a live region, made assertive for this message; only a control without one
            //  is announced through the page
            const live = t.ErrorE && t.ErrorE.hasAttribute && t.ErrorE.hasAttribute("aria-live") && !t.ErrorE.hidden && t.ErrorE.isConnected ? t.ErrorE : null;
            if (live)
                live.setAttribute("aria-live", "assertive");
            t.SetError(msg);
            t.AutoSaveError = true;
            if (!live && Sw.Page && Sw.Page.Announce)
                Sw.Page.Announce(msg, { Assertive: true });
        }
        else if (Sw.Fail)
            Sw.Fail(msg);
    }

    /**
     * The name of a control for messages (its label, else its accessible name, else its title).
     * @param {object} t The control.
     * @returns {string} The name.
     */
    function ctlName(t) {
        const o = t.O || {};
        const f = focusOf(t);
        return String(o.Label || o.AriaLabel || (f && f.getAttribute && f.getAttribute("aria-label")) || o.Title || o.Placeholder || "").trim();
    }

    /**
     * Set a value as the user's edit (arrow keys of a number box): Set (with OnChange), then the dirty state.
     * @param {object} t The control.
     * @param {*} v The value.
     */
    function userSet(t, v) {
        t.InUser = true;
        t.ChangeScope = null;
        try {
            t.Set(v);
        }
        finally {
            t.InUser = false;
        }
        userEdit(t, t.Get(), changeResult(t, t.O.OnChange));
    }

    /**
     * What a user change's OnChange did (for userEdit): its scope when it saves (a promise / request) or threw, "applied" when it
     * returned nothing, null without OnChange.
     * @param {object} t The control (ChangeScope: the scope of the OnChange its Set ran for the user).
     * @param {?function} fn The OnChange option.
     * @returns {object|string|null} The scope, "applied" or null.
     */
    function changeResult(t, fn) {
        const sc = t.ChangeScope;
        t.ChangeScope = null;
        if (!fn)
            return null;
        if (savesItself(sc))
            return sc;
        return "applied";
    }

    /**
     * True when an OnChange scope saves the value itself (returned a promise, started a request, threw) or waits for the running save.
     * @param {?object} sc The scope (callChange).
     * @returns {boolean} True for an auto save.
     */
    function savesItself(sc) {
        return !!(sc && typeof sc === "object" && (sc.Queued || sc.Threw || Sw.Work.Promise(sc)));
    }

    /**
     * Call a control's OnChange in a click scope (Sw.Work: a returned promise or a started request means the control saves itself).
     * @param {object} t The control.
     * @param {function} fn The handler.
     * @param {Array<*>} args The arguments.
     * @returns {object} The scope.
     */
    function callChange(t, fn, args) {
        //  (a throw is a failed save: userEdit reverts and says why, the one result path)
        if (t.AutoSaveError) {
            t.AutoSaveError = false;
            if (typeof t.SetError === "function")
                t.SetError(null);
            if (t.ErrorE && t.ErrorE.getAttribute && t.ErrorE.getAttribute("aria-live") === "assertive")
                t.ErrorE.setAttribute("aria-live", "polite");
        }
        //  This control's save still runs: the change waits, and only the newest waiting change is sent when it ends (F4: one save
        //  at a time, the latest value wins, so the shown value is the saved one)
        if (t.SaveRun) {
            t.SaveNext = { Fn: fn, Args: args };
            return { Queued: true, Result: undefined, Threw: false, Error: null, Requests: [], Controller: null, Writes: 0 };
        }
        return Sw.Work.Scope(() => fn(...args));
    }

    /**
     * A save ended: the dirty controls of the button's container (every group when the button is in none) are clean (their values are
     * the new base).
     * @param {Element} e The button.
     */
    function savedBy(e) {
        const root = e.closest(GroupSel);
        for (const g of Array.from(dirtyGroups.values()))
            if (!root || g.Root === document.body || g.Root.contains(e) || root.contains(g.Root))
                for (const c of Array.from(g.C))
                    setBase(c, c.Get());
    }

    /**
     * A save starts (a primary / Saves button's action): the dirty groups it saves stop marking the page while it runs, so a save that
     * navigates when done (location.href, Sw.Page.Navigate) is not asked about; a failed save marks them again.
     * @param {Element} e The button.
     * @returns {function(boolean):void} End(ok): ok = saved (clean), else dirty again.
     */
    function savingBy(e) {
        const root = e.closest(GroupSel);
        const list = Array.from(dirtyGroups.values()).filter(g => !root || g.Root === document.body || g.Root.contains(e) || root.contains(g.Root));
        list.forEach(g => g.Suspended = true);
        dirtySync();
        return ok => {
            list.forEach(g => g.Suspended = false);
            if (ok)
                savedBy(e);
            else
                dirtySync();
        };
    }

    /**
     * The dirty groups a button / field saves: the groups of its container (every group when it is in none).
     * @param {Element} e The button or field.
     * @returns {Array<object>} The groups.
     */
    function groupsOf(e) {
        const root = e.closest(GroupSel);
        return Array.from(dirtyGroups.values()).filter(g => !root || g.Root === document.body || g.Root.contains(e) || root.contains(g.Root));
    }

    /**
     * Start watching a work that may be a save without saying so (a button that is not primary, Enter in a box): the edited values of
     * its groups and a recorder of the requests it sends (Sw.Work.Record). Null when nothing around it is edited.
     * @param {Element} e The button or field.
     * @returns {?{List: Array<{C: object, V: *}>, Rec: object}} The watch.
     */
    function sentValues(e) {
        const list = [];
        const own = new Set(groupsOf(e));
        for (const g of own)
            for (const c of g.C)
                if (c.IsDirtyEdit)
                    list.push({ C: c, V: c.Get() });
        //  The edits elsewhere on the page count too when the request carried them clearly (dirty belongs to the page, A3-10: a button
        //  in another card that sends them saved them): only texts of 3+ characters and objects (a short value or a number could be
        //  anything in a payload)
        for (const g of dirtyGroups.values())
            if (!own.has(g))
                for (const c of g.C) {
                    const v = c.IsDirtyEdit ? c.Get() : undefined;
                    if ((typeof v === "string" && v.trim().length >= 3) || (v && typeof v === "object"))
                        list.push({ C: c, V: v, Far: true });
                }
        //  (or a Sw.Form with edits: a button outside it may be its save, ex: Save in the page header)
        const forms = !!(Sw.Edits && Sw.Edits.Forms && Array.from(Sw.Edits.Forms).some(f => f.IsDirty && f.IsDirty()));
        if ((!list.length && !forms && !drafted.size) || !Sw.Work.Record)
            return null;
        return { List: list, Rec: Sw.Work.Record() };
    }

    /**
     * Test if a sent request carries a value (the payload as JSON / text, or the url).
     * @param {string} sent The payload text.
     * @param {*} v The value.
     * @returns {boolean} True if found.
     */
    function carries(sent, v) {
        if (v === null || v === undefined || v === "")
            return /""|null|=(&|$)/.test(sent);
        if (typeof v === "string")
            return sent.indexOf(JSON.stringify(v).slice(1, -1)) >= 0 || sent.indexOf(v) >= 0 || sent.indexOf(encodeURIComponent(v)) >= 0;
        if (typeof v === "number" || typeof v === "boolean")
            return new RegExp("[:\\[,=\\s]" + String(v).replace(/[.+-]/g, "\\$&") + "(?![\\w.])").test(sent);
        try {
            return sent.indexOf(JSON.stringify(v)) >= 0;
        }
        catch {
            return false;
        }
    }

    /**
     * The work of a button that is not a declared save ended well: every edited control whose value it sent (a write whose payload
     * carries the value) is saved, so it is clean again (SAF-10: a save by any path, A2-3). A write that carried none of them keeps
     * them dirty and gives a developer warning ("mark the button Saves: true").
     * @param {Element} e The button or field.
     * @param {?{List: Array<{C: object, V: *}>, Rec: object}} w The watch (sentValues).
     * @param {string} name The button's name (for the warning).
     */
    function savedIfSent(e, w, name) {
        if (!w)
            return;
        const all = w.Rec.Stop();
        detachedSave(all, w, name, true);
        const writes = all.filter(x => !x.Detached);
        //  (Sw.Form fields around it: a write that carried all of the form's edits saved the form, ex: a Save in the page header)
        if (writes.length && Sw.Edits && Sw.Edits.Forms)
            for (const fm of Array.from(Sw.Edits.Forms)) {
                if (typeof fm.Gone === "function" && fm.Gone())
                    Sw.Edits.Forms.delete(fm);
                else if (fm.IsDirty && fm.IsDirty() && typeof fm.SentBy === "function")
                    fm.SentBy(writesText(writes));
            }
        //  A drafted text box whose text this successful write carried was sent: its draft goes now, after the success (FD R4 F6; a
        //  draft is never cleared before the save is known to work)
        if (writes.length) {
            const sentText = writesText(writes);
            for (const d of Array.from(drafted))
                if (d.DraftH && d.E.isConnected && d.Get().trim().length >= 3 && carries(sentText, d.Get()))
                    d.ClearDraft();
        }
        if (!writes.length || !w.List.length)
            return;
        const text = writesText(writes);
        let kept = 0;
        for (const it of w.List) {
            if (!it.C.IsDirtyEdit || !sameValue(it.C.Get(), it.V))
                continue;
            if (carries(text, it.V))
                setBase(it.C, it.V);
            else if (!it.Far)
                ++kept;
        }
        if (kept)
            Sw.DevWarn('"' + name + '" sent data and succeeded, but ' + kept + " edited control(s) around it are still dirty (their values were not in the request). If it saves them, mark it Saves: true (or call MarkClean()); if it doesn't, Saves: false (UX guide SAF-10).", "saves-unsure:" + name);
    }

    /**
     * The url and payload of recorded writes as one text (to look for the edited values in it).
     * @param {Array<{Url: string, Body: *}>} writes The writes.
     * @returns {string} The text.
     */
    function writesText(writes) {
        return writes.map(x => {
            let b = "";
            try {
                b = typeof x.Body === "string" ? x.Body : (x.Body instanceof URLSearchParams ? x.Body.toString() : JSON.stringify(x.Body ?? null));
            }
            catch {
                b = String(x.Body);
            }
            return x.Url + "\n" + b;
        }).join("\n");
    }

    /**
     * A Detached write (not the click's work) that carried the edited values around the button is the button's own save marked
     * Detached by mistake (FD N11: no busy state, the page stayed dirty after the save): a developer warning.
     * @param {Array<object>} all The recorded writes.
     * @param {?{List: Array<{C: object, V: *}>}} w The watch.
     * @param {string} name The button's name.
     * @param {boolean} [returned] The work returned it (the button was busy with it: FD R4 F13, the text says what really happens).
     */
    function detachedSave(all, w, name, returned) {
        const det = all.filter(x => x.Detached);
        if (!det.length || !w || !w.List.length)
            return;
        const text = writesText(det);
        if (w.List.some(it => it.V !== "" && it.V !== null && it.V !== undefined && typeof it.V !== "boolean" && carries(text, it.V)))
            Sw.DevWarn(returned
                ? '"' + name + '" sent the edited values in a request marked Detached / Background (Sw.Net.Live): returned from OnClick it keeps the button busy, but Detached takes it out of the click\'s work: a dialog\'s Cancel / Stop waiting and the leave question don\'t see it. It is the button\'s own save: use Sw.Net.Send without Detached (UX guide FBK-2, POP-6).'
                : '"' + name + '" sent the edited values in a request marked Detached / Background and did not return it: that is the button\'s own save, so it is not busy, a failure is not shown and the edits stay "unsaved". Remove Detached and return the request (OnClick: () => Sw.Net.Send(...)) (UX guide FBK-2, SAF-10).', "detached-save:" + name);
    }

    /** The common methods of the value controls for the dirty state. */
    const DirtyMethods = {
        /** @returns {boolean} True when the user changed the value since it was set (or saved). */
        IsDirty() {
            //  (only the user's changes: a value written by code, ex: the server's number put in by a save, is no edit; FD R4 F2)
            return !!this.UserEdited && !sameValue(this.Get(), this.Base);
        },
        /** The current value is saved: clean again (the new base). */
        MarkClean() {
            setBase(this, this.Get());
        },
    };

    // ------------------------------------------------------------------ button

    /**
     * A button.
     * Options: Text, Icon (an Sw.Icons name, a SysWeaver icon class like "IconReload", or an icon url), Title (tooltip),
     * Label (the accessible name, say which item it acts on when there are several alike, ex: "Copy the url of Fire"),
     * Kind ("primary": the one main action | "accent" | "ghost": toolbars | "danger": outline | "danger-solid"; default: secondary), Small, Block (full width), Pressable (toggle button, aria-pressed), Pressed,
     * AutoToggle (default true: a Pressable button flips on click; false = call SetPressed yourself), Repeat (repeat OnClick while held;
     * a click without a press, from assistive technology, acts once), Disabled (true, or the reason: "Select a row first"), Class,
     * Type (the html button type, default "button"), IconRight (the icon after the text), Menu (items or fn(button, event) -> items: a
     * menu button, see Sw.MenuButton; MenuOptions for Sw.Menu.Show), Copy (a copy button: Sw.Clipboard.Copy in OnClick confirms on it;
     * automatic with a copy icon), Done (false: no result announcement; a text: announce it when done), Shortcut (ex: "Mod+S": added
     * to the tool tip as "(Ctrl+S)" and set as aria-keyshortcuts; register the key itself with Sw.Keys.Add),
     * OnClick(button, event, { Signal }): if it returns a promise (or starts Sw.Net requests / fetch without returning them: a developer
     * warning says to return it) the button is busy until it settles; Signal is an AbortSignal fired by Abort() (a dialog Cancel) and when
     * the page is left, and requests started in OnClick without a signal use it. A busy state of more than 2 s shows the elapsed time in
     * the tool tip and makes leaving the page ask "<name> may not finish." (Leave: false opts out). While a dialog opened by the click
     * waits for the user the button is not busy (no spinner). Saves (default: true for Kind "primary"): when the action ends without an
     * error the edited controls of its card / form are clean again (SAF-10). The busy state: (repeated clicks are absorbed, aria-busy, a
     * spinner after 250 ms in place of the icon or over the label (the width stays) announced "Working…", then "<name>: done" unless the
     * action showed a toast; a rejection is shown as a fail toast; a refused request, 403, names the role needed and disables the button).
     * Disabled is aria-disabled (UX guide INP-12): focusable, muted, a click / Enter shows and announces the reason (Sw.Disabled);
     * button.E.disabled reads / sets that state. A click with a modifier key only moves the focus (INP-2, Sw.ModifierClick).
     * Accessible name: Label; else the visible text (the title stays the tool tip / description); else (icon only, or a text without
     * letters / digits like "+") the title as an action (Sw.ActionName: "Click to reload the file" -> "Reload the file"); else the icon name.
     * Methods: SetContent(text, icon), SetText, SetIcon, SetTitle, SetLabel, SetPressed, SetDisabled(bool, reason), SetWorking, SetKind,
     * Click(), OpenMenu(), Name(), Abort(), CanAbort().
     */
    class Button {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.OGiven = o;
            //  aliases: AriaLabel = Label (the accessible name), Kind "error" = "danger"
            if ((o.AriaLabel && o.Label === undefined) || o.Kind === "error")
                o = Object.assign({}, o, { Label: o.Label ?? o.AriaLabel, Kind: canonKind(o.Kind) });
            //  Submit: true = the form's send button (type submit, primary unless a Kind is given): the option people guess first
            if (o.Submit && (!o.Type || o.Kind === undefined))
                o = Object.assign({}, o, { Type: o.Type || "submit", Kind: o.Kind === undefined ? "primary" : o.Kind });
            t.O = o;
            t.Busy = false;
            t.Working = false;
            t.Kind = o.Kind || null;
            const cls = ["sw-button"];
            if (o.Kind)
                cls.push("sw-" + o.Kind);
            if (o.Small)
                cls.push("sw-small");
            if (o.Block)
                cls.push("sw-block");
            if (o.Class)
                cls.push(o.Class);
            t.E = el("button", { class: cls.join(" "), type: o.Type || "button" });
            t.E.SwButton = t;
            if (o.Hidden)
                t.E.hidden = true;
            //  A declared save anywhere on the page (the page header's Actions, a toolbar): the edits it saves are tracked (A3-10)
            if (o.Saves === true)
                t.E.setAttribute("data-sw-saves", "");
            t.Disabled = false;
            //  button.E.disabled keeps working: it reads / sets the aria-disabled state (UX guide INP-12)
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.E, d => t.SetDisabled(d));
            t.Text = o.Text || "";
            t.IconName = o.Icon || null;
            t.SetContent(o.Text, o.Icon);
            t.SetTitle(o.Title);
            if (o.Pressable)
                t.SetPressed(!!o.Pressed);
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            if (o.Working)
                t.SetWorking(true);
            if (o.Menu) {
                //  A menu button (INP-3): click, pure right click, long press, Menu key, Shift+F10; aria-haspopup / aria-expanded
                t.MenuB = Sw.MenuButton(t.E, ev => typeof o.Menu === "function" ? o.Menu(t, ev) : o.Menu, { NoClick: true, Menu: o.MenuOptions });
            }
            if (o.Repeat) {
                //  Press and hold repeats (acts on press, INP-7 exception); a plain click (assistive technology, voice control: no
                //  pointer down before it) acts once (INP-1); Enter / Space act once per key press (auto repeat repeats)
                let timer = 0;
                const stop = () => {
                    clearTimeout(timer);
                    timer = 0;
                };
                const act = e => {
                    if (t.Disabled || t.Busy)
                        return false;
                    o.OnClick && o.OnClick(t, e);
                    return true;
                };
                const fire = (e, first) => {
                    if (!act(e))
                        return;
                    timer = setTimeout(() => fire(e, false), first ? 400 : 70);
                };
                t.E.addEventListener("pointerdown", e => {
                    if (e.button !== 0 || !Sw.IsPureEvent(e) || t.Disabled)
                        return;
                    try {
                        t.E.setPointerCapture(e.pointerId);
                    }
                    catch {
                    }
                    fire(e, true);
                });
                t.E.addEventListener("pointerup", stop);
                t.E.addEventListener("pointercancel", stop);
                t.E.addEventListener("lostpointercapture", stop);
                t.E.addEventListener("click", e => {
                    if (Sw.ModifierClick(e))
                        return;
                    //  A pointer click (detail > 0) acted on its press already; a click without a press (detail 0) acts once
                    if (e.detail === 0)
                        act(e);
                });
                t.E.addEventListener("keydown", e => {
                    if ((e.key === "Enter" || e.key === " ") && Sw.IsPureEvent(e)) {
                        e.preventDefault();
                        if (t.Disabled)
                            Sw.Disabled.Reject(t.E, e);
                        else
                            act(e);
                    }
                });
            }
            else {
                t.E.addEventListener("click", e => {
                    //  A modifier click only moves the focus (INP-2)
                    if (Sw.ModifierClick(e))
                        return;
                    t.Click(e);
                });
                //  Icon only buttons: the title is shown on long press (touch) and keyboard focus too (a menu button's long press opens
                //  its menu instead, INP-5)
                if (o.Title && o.Icon && !o.Text && Sw.Hint && !o.Menu)
                    Sw.Hint.Attach(t.E);
            }
            ctlInit(t, "Sw.Button", Schema.Button);
        }

        /**
         * Open the menu of a menu button (the Menu option).
         * @param {Event} [ev] The triggering event.
         */
        OpenMenu(ev) {
            if (this.MenuB)
                this.MenuB.Open(ev);
        }

        /**
         * Run the click action (as if clicked): flips a Pressable button and calls OnClick, tracking a returned promise.
         * @param {Event} [e] The triggering event.
         * @returns {Promise<void>} Settles when the action is done.
         */
        async Click(e) {
            const t = this;
            const o = t.O;
            //  Busy: a repeated activation is absorbed (FBK-1 / FBK-2), its busy state is the cue
            if (t.Busy)
                return;
            if (t.Disabled) {
                Sw.Disabled.Reject(t.E, e);
                return;
            }
            if (o.Pressable && o.AutoToggle !== false)
                t.SetPressed(!t.Pressed);
            if (!o.OnClick) {
                if (t.MenuB)
                    t.OpenMenu(e);
                return;
            }
            //  Sw.Clipboard.Copy called from OnClick confirms on this button (copy buttons, INP-31)
            const prev = Button.Current;
            Button.Current = t;
            const modals = Sw.Work.OpenModals();
            //  A save (primary, or Saves: true): the edited controls around it stop marking the page while it runs (a save that navigates
            //  when done is not asked about), clean when it succeeds, dirty again when it fails (SAF-10)
            const endSave = o.Saves === true || (o.Saves !== false && t.Kind === "primary") ? savingBy(t.E) : null;
            //  A click scope (Sw.Work): requests started by OnClick make the button busy even when it forgot to return the promise
            //  (FBK-2), and get the AbortSignal passed as the third argument (Abort(), leaving the page)
            let sc;
            //  (also for a declared save: a Sw.Form whose values it sent is saved too, and a Detached save gives a warning)
            const before = o.Saves !== false ? sentValues(t.E) : null;
            try {
                sc = Sw.Work.Scope(signal => o.OnClick(t, e, { Signal: signal }));
            }
            finally {
                Button.Current = prev;
            }
            if (sc.Threw) {
                if (endSave)
                    endSave(false);
                if (before)
                    before.Rec.Stop();
                Sw.Work.Report(Sw.Work.Outcome(sc, { Failed: true, Error: sc.Error }), { Name: t.Name(), Button: t });
                return;
            }
            const r = Sw.Work.Promise(sc);
            if (r && sc.Implicit)
                Sw.DevWarn('Sw.Button "' + t.Name() + '": OnClick started a request but returned no promise. Return it (OnClick: () => Sw.Net.Send(...)) so a failure is shown; the button is kept busy until the request ends (UX guide FBK-2).', "btn-noreturn:" + t.Name());
            if (!r) {
                if (endSave)
                    endSave(false);
                if (before)
                    detachedSave(before.Rec.Stop(), before, t.Name());
                return;
            }
            t.Busy = true;
            t.Controller = sc.Controller;
            const onHide = () => t.Abort();
            window.addEventListener("pagehide", onHide);
            const toasts = Sw.Work.Watch();
            //  The focus stays on the page through what the action rebuilds or hides (FOC-1)
            const keep = Sw.Focus.Keep(t.E.parentElement || t.E);
            //  The busy state of Sw.Work: aria-busy, the spinner after 250 ms announced "Working…" (FBK-3), the elapsed time in the tool
            //  tip after 2 s and the leave question (FBK-4, SAF-10); not while a dialog opened by the click waits for the user
            const res = await Sw.Work.Busy(t.E, r, {
                Name: t.Name(),
                Done: sc.Done,
                Leave: o.Leave,
                Modals: modals,
                SetSpin: on => {
                    if (on || !o.Working)
                        t.SetWorking(on);
                },
                SetTitle: tt => {
                    if (tt)
                        t.E.title = tt;
                    else
                        t.SetTitle(t.Title);
                },
            });
            window.removeEventListener("pagehide", onHide);
            t.Busy = false;
            t.Controller = null;
            t.E.removeAttribute("aria-busy");
            //  The one result path (Sw.Work.Outcome): a throw, a rejection, a rejected request it started, a false result (ex:
            //  Sw.Form.Submit with an invalid field: it said why) or an aborted signal (Abort, a dialog's Cancel, the page was left)
            //  is never "done"
            const out = Sw.Work.Outcome(sc, res);
            //  A save ended: the edited controls around it are clean (SAF-10; primary buttons, Saves: true, or any button whose work
            //  sent the edited values; Saves: false opts out)
            if (endSave)
                endSave(out.Ok);
            if (out.Ok)
                savedIfSent(t.E, before, t.Name());
            else if (before)
                before.Rec.Stop();
            t.LastOutcome = out;
            //  The result once (FBK-2, FBK-5): nothing more when the action showed it (a toast, its status line, a field message), else
            //  the failure toast / "not done" / "stopped", else "<name>: done"; a toast may be added a moment after the action (the
            //  first toast of a page), so the button waits a little before it speaks
            setTimeout(() => {
                Sw.Work.Report(out, { Name: t.Name(), Said: toasts.Stop(), Spun: res.Spun, Done: o.Done, Button: t });
                keep.Restore();
                keep.Stop();
            }, 150);
            return out;
        }

        /**
         * Abort the running action: its AbortSignal (the third OnClick argument, { Signal }) fires, and every request it started without
         * a signal of its own is aborted (Sw.Work). A dialog's Cancel, or leaving the page, call it. No-op when not busy.
         * @returns {boolean} True when an action was running.
         */
        Abort() {
            const c = this.Controller;
            if (!c)
                return false;
            try {
                c.abort();
            }
            catch {
                //  aborted already
            }
            return true;
        }

        /**
         * True while the action runs and its OnClick got an AbortSignal it can be stopped with (Abort). A dialog shows its Cancel for it.
         * @returns {boolean} True if abortable.
         */
        CanAbort() {
            return !!this.Controller;
        }

        /**
         * The accessible name of the button (aria-label, else the text, else the title).
         * @returns {string} The name.
         */
        Name() {
            return (this.E.getAttribute("aria-label") || this.Text || this.Title || "").trim();
        }

        /**
         * Replace the content (the icon and the text). Children a page added with the class `sw-button-extra` (ex: a short word below
         * the icon, a badge) are kept, after the new content.
         * @param {string} [text] The text.
         * @param {string} [icon] The icon (see the Icon option).
         */
        SetContent(text, icon) {
            const t = this;
            const e = t.E;
            t.Text = text || "";
            t.IconName = icon || null;
            const extras = Array.from(e.children).filter(x => x.classList.contains("sw-button-extra"));
            e.replaceChildren();
            e.classList.toggle("sw-icon-only", !!icon && !text && !extras.length);
            //  Working: the spinner takes the icon's place, or sits in front of the label of a text only button, inside its padding (the
            //  one busy look, Sw.Work.SpinBeside: the label stays readable, the width stays; FBK-2 / FBK-3)
            const spin = t.Working ? new Spinner({ Class: "sw-button-spin" }).E : null;
            const ic = icon ? (spin || Icon(icon)) : null;
            const sp = text ? el("span.sw-button-text", { text }) : null;
            if (t.O.IconRight)
                e.append(...[sp, ic].filter(x => x));
            else
                e.append(...[ic, sp].filter(x => x));
            e.append(...extras);
            Sw.Work.SpinBeside(e, spin && !icon ? spin : null);
            t.UpdateLabel();
        }

        /** Update the accessible name (see the class description: Label, else the visible text, else the title as an action, else the icon name). */
        UpdateLabel() {
            const t = this;
            const label = t.O.Label;
            const named = t.Text && /[\p{L}\p{N}]/u.test(t.Text);
            const l = label || (named ? "" : (Sw.ActionName(t.Title) || t.Text || t.IconName || ""));
            if (l)
                t.E.setAttribute("aria-label", l);
            else
                t.E.removeAttribute("aria-label");
        }

        /**
         * Change the accessible name (the Label option; empty = derived from the text / title again).
         * @param {string} label The name.
         */
        SetLabel(label) {
            this.O.Label = label || "";
            this.UpdateLabel();
        }

        /**
         * Change the text (keeps the icon).
         * @param {string} text The text.
         */
        SetText(text) {
            this.SetContent(text, this.IconName);
        }

        /**
         * Change the icon (keeps the text).
         * @param {string} icon The icon.
         */
        SetIcon(icon) {
            this.SetContent(this.Text, icon);
        }

        /**
         * Change the tooltip (and accessible name).
         * @param {string} title The title.
         */
        SetTitle(title) {
            const e = this.E;
            //  The Shortcut option is named in the tool tip (INP-23, TIP-1) and given as aria-keyshortcuts (TIP-2)
            const sc = this.O.Shortcut;
            if (sc) {
                const shown = Sw.Keys && Sw.Keys.Format ? Sw.Keys.Format(sc) : sc;
                if (title && title.indexOf(shown) < 0)
                    title = title + " (" + shown + ")";
                e.setAttribute("aria-keyshortcuts", sc.replace(/\bMod\b/g, Sw.Keys && Sw.Keys.IsMac ? "Meta" : "Control").replace(/\bCtrl\b/g, "Control"));
            }
            this.Title = title || "";
            //  Disabled with a reason: the reason stays the tool tip, the title comes back when enabled
            if (e.SwDisabledReason)
                e.SwTitleBefore = title || null;
            else if (title)
                e.title = title;
            else
                e.removeAttribute("title");
            this.UpdateLabel();
        }

        /**
         * Set the pressed state (toggle buttons).
         * @param {boolean} p Pressed.
         */
        SetPressed(p) {
            this.Pressed = !!p;
            this.E.setAttribute("aria-pressed", this.Pressed ? "true" : "false");
        }

        /**
         * Enable or disable the button. Disabled is aria-disabled (UX guide INP-12): the button stays focusable, looks muted, ignores
         * activation and shows / announces the reason when used (Sw.Disabled).
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why, and what enables it ("Select a row first"): the tool tip while disabled (default: the title).
         */
        SetDisabled(d, reason) {
            this.Disabled = !!d;
            Sw.Disabled.Set(this.E, d, reason === undefined ? undefined : { Reason: reason });
        }

        /**
         * Show or hide the working state (a spinner, aria-busy; the old StartWorking / StopWorking). The width stays: the spinner
         * replaces the icon, or overlays the label of a text only button (FBK-3).
         * @param {boolean} w Working.
         */
        SetWorking(w) {
            const t = this;
            w = !!w;
            if (t.Working === w)
                return;
            const e = t.E;
            if (w) {
                t.WidthBefore = e.style.minWidth;
                const width = e.getBoundingClientRect().width;
                if (width)
                    e.style.minWidth = width + "px";
            }
            else {
                e.style.minWidth = t.WidthBefore || "";
                t.WidthBefore = null;
            }
            t.Working = w;
            e.classList.toggle("sw-working", w);
            if (w)
                e.setAttribute("aria-busy", "true");
            else if (!t.Busy)
                e.removeAttribute("aria-busy");
            t.SetContent(t.Text, t.IconName);
        }

        /**
         * Change the kind (style).
         * @param {string|null} kind "primary" | "accent" | "ghost" | "danger" | "danger-solid" or null.
         */
        SetKind(kind) {
            const t = this;
            if (t.Kind)
                t.E.classList.remove("sw-" + t.Kind);
            t.Kind = kind || null;
            if (kind)
                t.E.classList.add("sw-" + kind);
        }
    }

    /**
     * Log an error and show it as a fail toast (when overlays are loaded). A refused request (Sw.Access.IsDenied, 403) is no
     * failure: a toast names the role needed and the button is disabled with that reason (Sw.Access.ActionDenied).
     * @param {*} x The error.
     * @param {Button} [b] The button whose action failed.
     */
    function reportError(x, b) {
        //  (the one failure display of Sw.Work: a refused request names the role, a lost connection is calm, never "Failed to fetch")
        Sw.Work.Error(x, { Name: b && b.Name ? b.Name() : "", Button: b });
    }

    /** @type {Button|null} The button whose OnClick runs now (synchronously; Sw.Clipboard.Copy confirms on it). */
    Button.Current = null;

    /**
     * Announce a text to screen readers (Sw.Page.Announce when the core is loaded).
     * @param {string} text The text.
     */
    function announce(text) {
        if (text && Sw.Page && Sw.Page.Announce)
            Sw.Page.Announce(text);
    }

    /**
     * Create a row for buttons (wrapping, centered, the old Button.CreateRow / SysWeaver-CenterBlock).
     * @param {...(Node|{E: Node})} items Elements or components to add.
     * @returns {HTMLDivElement} The row (class sw-row).
     */
    function ButtonRow(...items) {
        return el("div.sw-row", null, ...items.map(x => x && x.E ? x.E : x));
    }

    // ------------------------------------------------------------------ group (card with a header row)

    /**
     * A group of controls that belong together, drawn as a card (Design v2: a flat surface with a hairline, a 48 px header row with the
     * title, an optional sub text and actions on the right, then the body; no fieldset look).
     * Named Group (the Ew Frame) since Sw.Frame is the core's frame messaging namespace.
     * Options: Title, Sub (a secondary line under the title), Actions (buttons / elements / components for the right of the header),
     * Class, Flush (no body padding: tables, lists), Content (elements appended to the body).
     * Properties: E (the card), Head (the header row, hidden without title and actions), Title (the title element), Tools (the actions
     * area), Body (append content here). Methods: SetTitle(text), SetSub(text).
     */
    class Group {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            o = o || {};
            Sw.Work.CheckOptions("Sw.Group", o, Schema.Group);
            const comp = x => x && x.E ? x.E : x;
            this.Title = el("div.sw-group-title");
            this.Sub = el("div.sw-group-sub");
            //  option Actions (alias Tools); the area is Tools (alias Actions, the name of the option and of PageHeader)
            this.Tools = el("div.sw-group-tools", null, ...(o.Actions || o.Tools || []).map(comp));
            this.Head = el("div.sw-group-head", null, el("div.sw-group-titles", null, this.Title, this.Sub), this.Tools);
            this.Body = el("div.sw-group-body", null, ...(o.Content || []).map(comp));
            this.E = el("section.sw-group", { class: o.Class || null }, this.Head, this.Body);
            if (o.Flush)
                this.E.classList.add("sw-flush");
            this.SetSub(o.Sub);
            this.SetTitle(o.Title);
        }

        /**
         * Change the title.
         * @param {string} text The title (empty hides it; the header row hides when it has no title and no actions).
         */
        SetTitle(text) {
            this.Title.textContent = text || "";
            this.Title.hidden = !text;
            this.Head.hidden = !text && !this.Tools.firstChild;
            if (text)
                this.E.setAttribute("aria-label", text);
            else
                this.E.removeAttribute("aria-label");
        }

        /** @type {HTMLElement} The actions area of the header (alias of Tools). */
        get Actions() {
            return this.Tools;
        }

        /**
         * Change the sub text (a secondary line under the title).
         * @param {string} text The text (empty hides it).
         */
        SetSub(text) {
            this.Sub.textContent = text || "";
        }
    }

    // ------------------------------------------------------------------ segmented control

    /**
     * A segmented control: a few mutually exclusive options shown side by side (ranges "3 min / 1 h / 24 h", views, densities).
     * Options: Items ([{Value, Text, Icon, Title, Disabled}]), Value, Label (accessible name of the group), Class, Small,
     *          OnChange(value, item), Required (with Value null: Validate() shows "Choose a value" until one is picked), Validate(value).
     * A radio group (role radiogroup / radio, aria-checked); keyboard: Left / Right / Up / Down / Home / End move and select (roving focus).
     * Methods: Get(), Set(value, silent), SetDisabled(bool), and the common methods (Focus, SetLabel, SetError, Validate, Destroy).
     */
    class Segmented {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            //  Items (alias Options), item Text (alias Label); AriaLabel (alias Label) names the group
            t.Items = canonItems(o.Items || o.Options, "Text");
            t.E = el("div.sw-seg", { class: o.Class || null, attrs: { role: "radiogroup", "aria-label": o.AriaLabel || o.Label || null, "aria-required": o.Required ? "true" : null } });
            t.Buttons = t.Items.map(it => {
                const b = el("button.sw-seg-item", {
                    type: "button", title: it.Title || null,
                    attrs: { role: "radio", "aria-checked": "false", "aria-label": !it.Text ? (it.Title || String(it.Value)) : null },
                }, it.Icon ? Icon(it.Icon) : null, it.Text ? el("span", { text: it.Text }) : null);
                //  A disabled segment is aria-disabled (INP-12: focusable, its title says why); a modifier click only focuses (INP-2)
                if (it.Disabled)
                    Sw.Disabled.Set(b, true, typeof it.Disabled === "string" ? it.Disabled : undefined);
                b.addEventListener("click", ev => {
                    if (Sw.ModifierClick(ev))
                        return;
                    t.Pick(it.Value);
                });
                t.E.append(b);
                return b;
            });
            t.E.addEventListener("keydown", e => {
                const keys = { ArrowLeft: -1, ArrowUp: -1, ArrowRight: 1, ArrowDown: 1 };
                const live = t.Buttons.filter(b => !Sw.Disabled.Is(b));
                if (!live.length) {
                    //  The whole control is disabled: the arrows say why
                    const item = e.target.closest && e.target.closest(".sw-seg-item");
                    if (item && (e.key in keys || e.key === "Home" || e.key === "End")) {
                        e.preventDefault();
                        Sw.Disabled.Reject(item, e);
                    }
                    return;
                }
                let i = live.indexOf(document.activeElement);
                if (e.key in keys)
                    i = (i + keys[e.key] + live.length) % live.length;
                else if (e.key === "Home")
                    i = 0;
                else if (e.key === "End")
                    i = live.length - 1;
                else
                    return;
                e.preventDefault();
                live[i].focus();
                live[i].click();
            });
            t.Value = undefined;
            t.Set(o.Value !== undefined ? o.Value : (t.Items[0] ? t.Items[0].Value : null), true);
            //  Disabled: true | "reason" (INP-12), the same option as every Sw control
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            ctlInit(t, "Sw.Segmented", Schema.Segmented, true);
        }

        /**
         * Get the selected value.
         * @returns {*} The value.
         */
        Get() {
            return this.Value;
        }

        /**
         * Select a value.
         * @param {*} v The value.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Set(v, silent) {
            const t = this;
            if (!t.InUser && !codeSet(t, v, silent))
                setBase(t, v);
            silent = silentOf(silent);
            const changed = v !== t.Value;
            t.Value = v;
            let any = false;
            t.Items.forEach((it, i) => {
                const on = it.Value === v;
                any = any || on;
                t.Buttons[i].setAttribute("aria-checked", on ? "true" : "false");
                t.Buttons[i].tabIndex = on ? 0 : -1;
            });
            if (!any && t.Buttons[0])
                t.Buttons[0].tabIndex = 0;
            if (t.Error)
                t.Validate();
            if (changed && !silent && t.O.OnChange)
                t.O.OnChange(v, t.Items.find(it => it.Value === v));
        }

        /**
         * Enable or disable every segment (aria-disabled: they stay focusable and say why, INP-12; an item's own Disabled stays).
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why (the tool tip while disabled).
         */
        SetDisabled(d, reason) {
            this.Disabled = !!d;
            //  The group says it too (a screen reader reads "radio group, dimmed"); the segments keep the reason
            if (this.Disabled)
                this.E.setAttribute("aria-disabled", "true");
            else
                this.E.removeAttribute("aria-disabled");
            this.E.classList.toggle("sw-disabled", this.Disabled);
            this.Buttons.forEach((b, i) => {
                const it = this.Items[i];
                const own = typeof it.Disabled === "string" ? it.Disabled : undefined;
                Sw.Disabled.Set(b, !!d || !!it.Disabled, d ? reason : own);
            });
        }
    }

    // ------------------------------------------------------------------ toggle (switch)

    /**
     * A switch.
     * Options: Label, Value, Title, Disabled, OnChange(value), Required (true, or the message: the switch must be on, ex: "accept the
     * terms"; Validate() shows "Turn this on to continue" under it; the label gets a marker, the switch aria-required), Validate(value)
     * -> error text or null.
     * Methods: Get(), Set(value, silent), SetDisabled(bool), SetLabel(text), and the common methods (Focus, SetError, Validate, Destroy).
     */
    class Toggle {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.LabelE = el("span.sw-toggle-label");
            t.E = el("div.sw-toggle", { tabIndex: 0, title: o.Title || null, attrs: { role: "switch" } }, el("span.sw-toggle-track"), t.LabelE);
            t.SetLabel(o.Label);
            t.Set(!!o.Value, true);
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            if (o.Required)
                t.E.setAttribute("aria-required", "true");
            const flip = e => {
                if (t.Disabled) {
                    Sw.Disabled.Reject(t.E, e);
                    return;
                }
                t.Pick(!t.Value);
                //  (Pick calls OnChange)
            };
            t.E.addEventListener("click", e => {
                //  A modifier click only focuses (INP-2)
                if (Sw.ModifierClick(e))
                    return;
                flip(e);
            });
            t.E.addEventListener("keydown", e => {
                if ((e.key === " " || e.key === "Enter") && Sw.IsPureEvent(e)) {
                    e.preventDefault();
                    flip(e);
                }
            });
            ctlInit(t, "Sw.Toggle", Schema.Toggle, true);
        }

        /**
         * Change the label.
         * @param {string} text The label (empty hides it).
         */
        SetLabel(text) {
            this.LabelE.textContent = text || "";
            if (text && this.O.Required)
                this.LabelE.append(requiredMark(this.O));
            this.LabelE.hidden = !text;
            const l = text || this.O.Title;
            if (l)
                this.E.setAttribute("aria-label", l);
        }

        /**
         * The message of a Required switch that is off.
         * @returns {string} The message.
         */
        EmptyMessage() {
            return _TF("Turn this on to continue", "Message under a required switch (ex: accept the terms) that is still off");
        }

        /**
         * Get the value.
         * @returns {boolean} The state.
         */
        Get() {
            return this.Value;
        }

        /**
         * Set the value.
         * @param {boolean} v The state.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Set(v, silent) {
            if (!this.InUser && !codeSet(this, !!v, silent))
                setBase(this, !!v);
            silent = silentOf(silent);
            this.Value = !!v;
            this.E.setAttribute("aria-checked", this.Value ? "true" : "false");
            //  A shown message is checked again, also after a value set from code (FRM-3)
            if (this.Error)
                this.Validate();
            if (!silent && this.O.OnChange)
                this.O.OnChange(this.Value);
        }

        /**
         * Enable or disable (aria-disabled: the switch stays focusable, its tool tip says why, a click or Space shows the reason; INP-12).
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why (the tool tip while disabled).
         */
        SetDisabled(d, reason) {
            this.Disabled = !!d;
            Sw.Disabled.Set(this.E, this.Disabled, reason);
        }
    }

    // ------------------------------------------------------------------ slider

    /**
     * A slider (horizontal or vertical), touch friendly:
     *  - drag the thumb (relative, no jump), tap / drag the track (jumps to the position),
     *  - double tap / double click resets to Default (if given; two taps close together, a quick second drag stays a drag),
     *  - keyboard: arrows (Shift = 10 steps), Home / End, PageUp / PageDown.
     * Options: Min, Max, Step, Value, Default, Log (logarithmic, Min must be > 0), Vertical, Fader (fader cap style),
     *          Center (value that gets a mark, e.g. 1 for speeds), Accent2 (use the second accent color), Title / Label (accessible name),
     *          Disabled, OnInput(value) (while dragging), OnChange(value) (released / committed), OnStart(), OnEnd().
     * Methods: Get(), Set(value, silent, force) (ignored while the user drags unless force), SetRange(min, max, step), SetDisabled(bool).
     * Property Dragging is true while the user drags.
     */
    class Slider {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.Min = o.Min ?? 0;
            t.Max = o.Max ?? 1;
            t.Step = o.Step ?? 0;
            t.Log = !!o.Log && t.Min > 0;
            t.Vertical = !!o.Vertical;
            const cls = ["sw-slider"];
            if (t.Vertical)
                cls.push("sw-vertical");
            if (o.Fader)
                cls.push("sw-fader");
            if (o.Accent2)
                cls.push("sw-accent2");
            t.Fill = el("div.sw-slider-fill");
            t.Thumb = el("div.sw-slider-thumb");
            t.Track = el("div.sw-slider-track", null, t.Fill);
            if (o.Center !== undefined) {
                t.Mark = el("div.sw-slider-mark");
                t.Track.append(t.Mark);
            }
            t.Track.append(t.Thumb);
            t.E = el("div", {
                class: cls.join(" "), tabIndex: 0, title: o.Title || null,
                attrs: { role: "slider", "aria-orientation": t.Vertical ? "vertical" : "horizontal", "aria-label": o.AriaLabel || o.Label || o.Title || null },
            }, t.Track);
            t.Value = t.Min;
            t.Dragging = false;
            t.UpdateRangeAria();
            t.Set(o.Value ?? t.Min, true);
            t.Bind();
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            if (t.Mark)
                requestAnimationFrame(() => t.PlaceMark());
            ctlInit(t, "Sw.Slider", Schema.Slider, true);
        }

        /** Update aria-valuemin / aria-valuemax. */
        UpdateRangeAria() {
            this.E.setAttribute("aria-valuemin", String(this.Min));
            this.E.setAttribute("aria-valuemax", String(this.Max));
        }

        /**
         * Value to position.
         * @param {number} v The value.
         * @returns {number} 0..1.
         */
        ToPos(v) {
            const t = this;
            if (t.Max === t.Min)
                return 0;
            if (t.Log)
                return clamp(Math.log(v / t.Min) / Math.log(t.Max / t.Min), 0, 1);
            return clamp((v - t.Min) / (t.Max - t.Min), 0, 1);
        }

        /**
         * Position to value (snapped to the step; log sliders snap to 2 significant digits and to the center value).
         * @param {number} p 0..1.
         * @returns {number} The value.
         */
        FromPos(p) {
            const t = this;
            p = clamp(p, 0, 1);
            let v = t.Log ? t.Min * Math.pow(t.Max / t.Min, p) : t.Min + (t.Max - t.Min) * p;
            if (t.Log) {
                const c = t.O.Center;
                if (c !== undefined && Math.abs(Math.log(v / c)) < 0.025)
                    v = c;
                else {
                    const m = Math.pow(10, Math.floor(Math.log10(v)) - 1);
                    v = Math.round(v / m) * m;
                }
                v = parseFloat(v.toPrecision(6));
            }
            else
                v = snap(v, t.Step, t.Min);
            return clamp(v, t.Min, t.Max);
        }

        /** Position the center mark. */
        PlaceMark() {
            const t = this;
            const p = t.ToPos(t.O.Center);
            if (t.Vertical)
                t.Mark.style.bottom = (p * 100) + "%";
            else
                t.Mark.style.left = (p * 100) + "%";
        }

        /** Render the value (thumb, fill, aria). */
        Render() {
            const t = this;
            const p = t.ToPos(t.Value);
            const pct = (p * 100).toFixed(3) + "%";
            if (t.Vertical) {
                t.Thumb.style.bottom = pct;
                t.Fill.style.height = pct;
            }
            else {
                t.Thumb.style.left = pct;
                t.Fill.style.width = pct;
            }
            t.E.setAttribute("aria-valuenow", String(t.Value));
        }

        /**
         * Get the value.
         * @returns {number} The value.
         */
        Get() {
            return this.Value;
        }

        /**
         * Set the value (ignored while the user drags unless force).
         * @param {number} v The value.
         * @param {boolean} [silent] Don't call OnInput / OnChange.
         * @param {boolean} [force] Set it even while dragging.
         */
        Set(v, silent, force) {
            const t = this;
            if (t.Dragging && !force)
                return;
            v = typeof v === "number" && isFinite(v) ? clamp(v, t.Min, t.Max) : t.Min;
            t.Value = v;
            t.Render();
            if (!silent) {
                t.O.OnInput && t.O.OnInput(v);
                t.O.OnChange && t.O.OnChange(v);
            }
        }

        /**
         * Change the range (the value is clamped, no callbacks).
         * @param {number} min Minimum.
         * @param {number} max Maximum.
         * @param {number} [step] Step.
         */
        SetRange(min, max, step) {
            const t = this;
            t.Min = min;
            t.Max = max;
            if (step !== undefined)
                t.Step = step;
            t.UpdateRangeAria();
            t.Set(t.Value, true, true);
            if (t.Mark)
                t.PlaceMark();
        }

        /**
         * Enable or disable.
         * @param {boolean} d Disabled.
         */
        SetDisabled(d, reason) {
            //  aria-disabled (INP-12): stays focusable, a click or an arrow key shows the reason
            this.Disabled = !!d;
            Sw.Disabled.Set(this.E, this.Disabled, reason);
        }

        /** Set up the pointer and keyboard handlers. */
        Bind() {
            const t = this;
            const e = t.E;
            let grab = null;
            //  Double tap: two taps (no drag, short) close to each other, a quick second drag is a drag (not a reset)
            let lastTap = null;
            const posFromEvent = ev => {
                const r = t.Track.getBoundingClientRect();
                return t.Vertical ? 1 - (ev.clientY - r.top) / r.height : (ev.clientX - r.left) / r.width;
            };
            const emit = v => {
                noteUser(e);
                if (v === t.Value)
                    return;
                t.Value = v;
                t.Render();
                t.O.OnInput && t.O.OnInput(v);
            };
            e.addEventListener("pointerdown", ev => {
                if (ev.button !== 0 || grab)
                    return;
                //  Disabled: the click guard shows the reason; a modifier press only focuses (INP-2, the click follows)
                if (t.Disabled || !Sw.IsPureEvent(ev))
                    return;
                ev.preventDefault();
                e.focus({ preventScroll: true });
                const now = performance.now();
                if (lastTap && now - lastTap.At < 320 && Math.hypot(ev.clientX - lastTap.X, ev.clientY - lastTap.Y) < 24 && t.O.Default !== undefined) {
                    lastTap = null;
                    t.Set(t.O.Default);
                    return;
                }
                lastTap = null;
                try {
                    e.setPointerCapture(ev.pointerId);
                }
                catch {
                }
                const onThumb = t.Thumb.contains(ev.target);
                const p = posFromEvent(ev);
                const tp = t.ToPos(t.Value);
                //  On (or near) the thumb: relative drag, else jump
                const r = t.Track.getBoundingClientRect();
                const len = t.Vertical ? r.height : r.width;
                const near = Math.abs(p - tp) * len < 18;
                grab = { Id: ev.pointerId, Offset: (onThumb || near) ? tp - p : 0, Start: t.Value, X: ev.clientX, Y: ev.clientY, At: now, Moved: false };
                t.Dragging = true;
                e.classList.add("sw-dragging");
                t.O.OnStart && t.O.OnStart();
                emit(t.FromPos(p + grab.Offset));
            });
            e.addEventListener("pointermove", ev => {
                if (!grab || ev.pointerId !== grab.Id)
                    return;
                if (Math.hypot(ev.clientX - grab.X, ev.clientY - grab.Y) > 6)
                    grab.Moved = true;
                emit(t.FromPos(posFromEvent(ev) + grab.Offset));
            });
            const end = ev => {
                if (!grab || ev.pointerId !== grab.Id)
                    return;
                const changed = grab.Start !== t.Value;
                //  A tap (can be the first of a double tap)
                lastTap = !grab.Moved && performance.now() - grab.At < 300 ? { At: performance.now(), X: grab.X, Y: grab.Y } : null;
                grab = null;
                t.Dragging = false;
                e.classList.remove("sw-dragging");
                t.O.OnEnd && t.O.OnEnd();
                if (changed)
                    t.O.OnChange && t.O.OnChange(t.Value);
            };
            e.addEventListener("pointerup", end);
            e.addEventListener("pointercancel", end);
            e.addEventListener("keydown", ev => {
                //  Disabled (read only editors): the keys don't change the value, a value key shows the reason
                if (t.Disabled || e.classList.contains("sw-disabled")) {
                    if (/^(Arrow(Left|Right|Up|Down)|Page(Up|Down)|Home|End)$/.test(ev.key)) {
                        ev.preventDefault();
                        Sw.Disabled.Reject(e, ev);
                    }
                    return;
                }
                const big = ev.shiftKey ? 10 : 1;
                const unit = t.Log ? 0.01 : (t.Step > 0 && t.Max > t.Min ? t.Step / (t.Max - t.Min) : 0.01);
                let p = t.ToPos(t.Value);
                switch (ev.key) {
                    case "ArrowRight": case "ArrowUp": p += unit * big; break;
                    case "ArrowLeft": case "ArrowDown": p -= unit * big; break;
                    case "PageUp": p += 0.1; break;
                    case "PageDown": p -= 0.1; break;
                    case "Home": p = 0; break;
                    case "End": p = 1; break;
                    default: return;
                }
                ev.preventDefault();
                let v = t.FromPos(p);
                //  Make sure a key press always moves (snapping could round back)
                if (v === t.Value && t.Step > 0 && !t.Log)
                    v = clamp(t.Value + Math.sign(p - t.ToPos(t.Value)) * t.Step * big, t.Min, t.Max);
                noteUser(e);
                t.Set(v);
            });
            e.addEventListener("dblclick", ev => ev.preventDefault());
        }
    }

    // ------------------------------------------------------------------ field (label + control)

    /**
     * The one form field layout (ui/README "Form fields"): a visible label above a control, an optional hint under it.
     * TextBox, NumberBox and Select render their Label option with it, so every labelled control looks the same; Sw.Field wraps any other
     * control (a Slider, a Segmented, a custom element). The label names the control: label[for] for labelable elements (input, button,
     * select, textarea), aria-labelledby (and a click on the label focuses it) for the others.
     * @param {HTMLElement} control The control element (the focusable part).
     * @param {{Label: string, Hint: (string|undefined), Width: (string|undefined), Class: (string|undefined)}} o Options.
     * @param {HTMLElement} [holder] The element put in the field (default the control; a component's E that contains the control).
     * @returns {{E: HTMLElement, LabelE: HTMLElement, HintE: (HTMLElement|null)}} The field (div.sw-field) and its parts.
     */
    function fieldOf(control, o, holder) {
        holder = holder || control;
        const lab = el("label.sw-field-label", { text: o.Label || "" }, requiredMark(o));
        if (/^(INPUT|BUTTON|SELECT|TEXTAREA|METER|OUTPUT|PROGRESS)$/.test(control.tagName)) {
            if (!control.id)
                control.id = uid("sw-field");
            lab.htmlFor = control.id;
            //  (a later id change by page code keeps the label: FD R4 P1)
            keepLabelFor(control, lab);
        }
        else {
            lab.id = uid("sw-field-l");
            control.setAttribute("aria-labelledby", lab.id);
            lab.addEventListener("click", () => control.focus && control.focus());
        }
        control.removeAttribute("aria-label");
        let hint = null;
        if (o.Hint) {
            hint = el("div.sw-field-hint", { id: uid("sw-field-h"), text: o.Hint });
            control.setAttribute("aria-describedby", ((control.getAttribute("aria-describedby") || "") + " " + hint.id).trim());
        }
        const e = el("div.sw-field", { class: o.Class || null }, lab, holder, hint);
        if (o.Width) {
            e.style.width = o.Width;
            holder.style.width = "100%";
        }
        return { E: e, LabelE: lab, HintE: hint };
    }

    /**
     * A labelled form row around any control: Sw.Field({ Label, Hint, Control, Width, Class, Required (the label marker; the control validates itself) }).
     * Control is an element or a component ({E}; its Input / Button is the labelled part when it has one). Properties: E (div.sw-field),
     * Control, LabelE, HintE. Methods: SetLabel(text), SetHint(text).
     */
    class Field {
        /**
         * @param {object} o Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.Control = o.Control;
            const comp = o.Control && !(o.Control instanceof Node) ? o.Control : null;
            const c = (comp ? (comp.Input || comp.Button || comp.E) : o.Control) || el("span");
            //  A component's E may wrap the labelled part (ex: a TextBox): the whole E goes in the field
            const f = fieldOf(c, o, comp && comp.E && comp.E.contains(c) ? comp.E : c);
            //  Required: the marker after the label and aria-required on the control (FRM-2)
            if (o.Required && c instanceof Element)
                c.setAttribute("aria-required", "true");
            t.E = f.E;
            t.LabelE = f.LabelE;
            t.HintE = f.HintE;
            t.O = Object.assign({}, o);
            ctlInit(t, "Sw.Field", Schema.Field);
        }

        /**
         * Change the label text.
         * @param {string} text The label.
         */
        SetLabel(text) {
            setLabelText(this.LabelE, text);
        }

        /**
         * Change the hint text (shown under the control).
         * @param {string} text The hint ("" hides it).
         */
        SetHint(text) {
            const t = this;
            if (!t.HintE) {
                t.HintE = el("div.sw-field-hint", { id: uid("sw-field-h") });
                t.E.append(t.HintE);
            }
            //  LAY-9: a hint that was shown keeps its line (a hint that changes or goes while typing moves nothing)
            if (text)
                Sw.Reserve.Line(t.HintE);
            t.HintE.textContent = text || "";
            t.HintE.hidden = !text;
        }
    }

    /**
     * The required marker after a field label (option Required): a red asterisk, hidden from screen readers (the control has
     * aria-required, which they announce).
     * @param {{Required: (boolean|string|undefined)}} o The control options.
     * @returns {HTMLElement|null} The marker or null.
     */
    function requiredMark(o) {
        return o && o.Required ? el("span.sw-field-req", { text: "*", title: _TF("Required", "Tool tip of the asterisk after the label of a form field that must be filled in"), attrs: { "aria-hidden": "true" } }) : null;
    }

    /**
     * The message of an empty required field: the Required option when it is a text, else "Enter a value".
     * @param {{Required: (boolean|string|undefined)}} o The control options.
     * @returns {string} The message.
     */
    function requiredText(o) {
        return typeof o.Required === "string" && o.Required ? o.Required : _TF("Enter a value", "Message under an empty form field that must be filled in");
    }

    /**
     * Add or remove an id in an element's aria-describedby (other ids, ex: the hint, are kept).
     * @param {Element} target The described element.
     * @param {string} id The id.
     * @param {boolean} on Add (true) or remove.
     */
    function describedBy(target, id, on) {
        const ids = (target.getAttribute("aria-describedby") || "").split(/\s+/).filter(x => x && x !== id);
        if (on)
            ids.push(id);
        if (ids.length)
            target.setAttribute("aria-describedby", ids.join(" "));
        else
            target.removeAttribute("aria-describedby");
    }

    /**
     * Show (or clear) a control's validation message, the one way for every control: the text in its message element (ErrorE,
     * created after the control or inside its field on first use), aria-invalid on the control, the message linked by
     * aria-describedby (a screen reader reads the reason with "invalid"), class sw-invalid on its field.
     * @param {object} t The component (E, ErrorE, Error).
     * @param {Element} target The element that has the focus / the accessible name (the input, button, switch or radio group).
     * @param {string|null} text The message, null clears it.
     */
    function showError(t, target, text) {
        t.Error = text || null;
        //  LAY-9: the cell of a reserved message line (its height before this message: a longer one is a developer warning)
        const cell = t.ErrorE && t.ErrorE.parentElement && t.ErrorE.parentElement.classList.contains("sw-reserve-stack") ? t.ErrorE.parentElement : t.ErrorE;
        const h0 = text && cell && cell.isConnected && t.ErrorE.classList.contains("sw-reserve") ? cell.getBoundingClientRect().height : 0;
        if (!t.ErrorE && text) {
            t.ErrorE = el("div.sw-text-error", { id: uid("sw-err"), attrs: { "aria-live": "polite" } });
            const host = t.E.classList.contains("sw-field") || t.E.classList.contains("sw-text") ? t.E : t.E.closest(".sw-field");
            if (host)
                host.append(t.ErrorE);
            else
                t.E.after(t.ErrorE);
        }
        if (t.ErrorE) {
            //  LAY-9: a message line that was shown keeps its space (a cleared or a shorter message never pulls up what follows)
            if (text)
                Sw.Reserve.Line(t.ErrorE);
            t.ErrorE.textContent = text || "";
            t.ErrorE.hidden = !text;
            describedBy(target, t.ErrorE.id, !!text);
            if (h0 > 0) {
                const h1 = cell.getBoundingClientRect().height;
                if (h1 > h0 + 1)
                    Sw.DevWarn("\"" + (ctlName(t) || "a control") + "\": the message \"" + String(text).slice(0, 80) + "\" needs " + Math.round(h1 - h0) + " px more than the space its field reserved, so what follows moved (UX guide LAY-9). Pass the field's longest messages as the Messages option, so their space is there before the first key.", "msg-grew:" + (ctlName(t) || "") + ":" + String(text).slice(0, 40));
            }
        }
        const field = t.E.classList.contains("sw-field") ? t.E : t.E.closest(".sw-field");
        if (field)
            field.classList.toggle("sw-invalid", !!text);
        if (text)
            target.setAttribute("aria-invalid", "true");
        else
            target.removeAttribute("aria-invalid");
    }

    /**
     * LAY-9: the message line of a control that validates (Required, Validate) is there before the first message: it keeps its box
     * when hidden and the tallest height it had (Sw.Reserve.Line), and it sits in a cell as high as its longest known message (the
     * Required text and the texts of the Messages option, as invisible copies), so a message that comes, changes or goes while the user
     * types moves nothing.
     * @param {object} t The control (its ErrorE).
     * @param {object} o The options (Required, Messages).
     * @param {string} required The message of an empty value.
     * @returns {HTMLElement} What goes under the control: the cell, or the line itself without known texts.
     */
    function reserveError(t, o, required) {
        Sw.Reserve.Line(t.ErrorE);
        const texts = [];
        if (o.Required && required)
            texts.push(required);
        if (Array.isArray(o.Messages))
            texts.push(...o.Messages.filter(m => typeof m === "string" && m));
        if (!texts.length)
            return t.ErrorE;
        const slot = Sw.Reserve.Stack({ Class: "sw-text-error-slot", Sizer: texts, SizerClass: "sw-text-error" });
        slot.append(t.ErrorE);
        return slot;
    }

    /**
     * Set a label element's text and keep its required marker.
     * @param {Element} lab The label.
     * @param {string} text The text.
     */
    function setLabelText(lab, text) {
        const mark = lab.querySelector(".sw-field-req");
        lab.textContent = text || "";
        if (mark)
            lab.append(mark);
    }

    // ------------------------------------------------------------------ number box

    /**
     * A number input (text box, commits on Enter / blur, arrow keys step; Escape keeps the typed text as the original inputs did,
     * EscapeReverts: true reverts it).
     * Options: Min, Max, Step, Value, Width (css), Label (the visible label above the box, the same as TextBox / Select; a
     * div.sw-field then is E), AriaLabel (only the accessible name, no visible label; use it inside a row that is labelled already),
     * Hint, Title (tool tip), Disabled, Format(value) -> string, OnChange(value), Required (true, or the message: the box may be empty
     * (Value null) and an empty box shows "Enter a value" under it; the label gets a marker), Validate(value) -> error text or null
     * (checked on change and by Validate(); value is null when empty). Value null starts empty (Get() returns null until a number is entered).
     * Properties: E (the input, or the field with a Label / Required / Validate), Input (the input).
     * Methods: Get() (the number, also while it is being typed; null when empty), Set(value, silent), SetError(text), Validate()
     * (check now and show the message; returns the message or null), SetDisabled(bool).
     */
    class NumberBox {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.Input = el("input.sw-input.sw-number", {
                type: "text", title: o.Title || null,
                attrs: { inputmode: "decimal", spellcheck: "false", autocomplete: "off", "aria-label": o.AriaLabel || o.Label || o.Title || null },
            });
            t.E = t.Input;
            if (o.Width)
                t.Input.style.width = o.Width;
            t.Disabled = false;
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.Input, d => t.SetDisabled(d));
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            /** @type {boolean} The box may be empty (Value null): Value null or Required. */
            t.Nullable = o.Value === null || !!o.Required;
            t.Value = o.Value === undefined && o.Required ? null : (o.Value ?? (t.Nullable ? null : 0));
            t.Base = t.Value;
            t.Render();
            const commit = () => {
                const s = t.Input.value.trim();
                if (!s && t.Nullable) {
                    if (t.Value !== null)
                        t.Set(null);
                    t.Validate();
                    return;
                }
                const v = t.Parse(s);
                if (v === null) {
                    t.Render();
                    return;
                }
                if (v !== t.Value)
                    t.Set(v);
                else
                    t.Render();
                t.Validate();
            };
            /** A commit by the user (blur, Enter): the value is an edit, not a new base. */
            const userCommit = () => {
                t.InUser = true;
                t.ChangeScope = null;
                try {
                    commit();
                }
                finally {
                    t.InUser = false;
                }
                userEdit(t, t.Value, changeResult(t, o.OnChange));
            };
            t.Input.addEventListener("change", userCommit);
            //  Typing is an edit (sw-change, the page dirty state; SAF-10)
            t.Input.addEventListener("input", () => userEdit(t, t.Get(), null));
            t.Input.addEventListener("focus", () => t.Input.select());
            t.Input.addEventListener("keydown", e => {
                if (e.key === "Enter") {
                    userCommit();
                    t.Input.blur();
                }
                else if (e.key === "Escape" && o.EscapeReverts) {
                    const dirty = t.Input.value !== t.Text();
                    t.Render();
                    if (dirty) {
                        e.stopPropagation();
                        //  The revert is an edit like typing (a dialog and the page compare the value again)
                        t.Input.dispatchEvent(new Event("input", { bubbles: true }));
                    }
                    t.Input.blur();
                }
                else if (e.key === "ArrowUp" || e.key === "ArrowDown") {
                    e.preventDefault();
                    if (t.Disabled) {
                        Sw.Disabled.Reject(t.Input, e);
                        return;
                    }
                    const s = (o.Step || 1) * (e.shiftKey ? 10 : 1);
                    userSet(t, (t.Value ?? 0) + (e.key === "ArrowUp" ? s : -s));
                }
            });
            //  Label is the visible label (the same field as TextBox / Select); AriaLabel only names it
            if (o.Label && !o.AriaLabel) {
                const f = fieldOf(t.Input, o);
                t.E = f.E;
                t.LabelE = f.LabelE;
                t.E.classList.add("sw-field-number");
            }
            if (o.Required)
                t.Input.setAttribute("aria-required", "true");
            //  The validation message under the box (the same as TextBox); a box without a label gets a field around it for it
            if (o.Required || o.Validate) {
                if (t.E === t.Input)
                    t.E = el("div.sw-field.sw-field-number", null, t.Input);
                t.ErrorE = el("div.sw-text-error", { id: uid("sw-num-err"), attrs: { "aria-live": "polite" } });
                t.ErrorE.hidden = true;
                //  its line is there before the first message (LAY-9: typing never moves the fields below)
                t.E.append(reserveError(t, o, requiredText(o)));
                t.Input.addEventListener("input", () => {
                    if (t.Error)
                        t.SetError(null);
                });
            }
            t.Steps = true;
            ctlInit(t, "Sw.NumberBox", Schema.NumberBox, true);
        }

        /**
         * Parse a typed number ("1 234,5" too), clamped to Min / Max.
         * @param {string} s The text.
         * @returns {number|null} The number, null when the text is not a number.
         */
        Parse(s) {
            const v = parseFloat(String(s).replace(",", ".").replace(/\s/g, ""));
            return isFinite(v) ? clamp(v, this.O.Min ?? -Infinity, this.O.Max ?? Infinity) : null;
        }

        /**
         * Show (or clear) a validation message under the box (needs Required or Validate; else only aria-invalid is set).
         * @param {string|null} text The message, null clears it.
         */
        SetError(text) {
            showError(this, this.Input, text);
        }

        /**
         * Check the box now (Required, Validate) and show the message (ex: on a form's Save / Next). The number being typed counts.
         * @returns {string|null} The message, null when valid (Sw.Form calls it).
         */
        Validate() {
            const t = this;
            const v = t.Get();
            let err = null;
            if (t.O.Required && v === null)
                err = requiredText(t.O);
            else if (t.O.Validate)
                err = t.O.Validate(v) || null;
            t.SetError(err);
            return err;
        }

        /**
         * The display text of the current value.
         * @returns {string} Text.
         */
        Text() {
            const o = this.O;
            if (this.Value === null || this.Value === undefined)
                return "";
            if (o.Format)
                return o.Format(this.Value);
            //  without a Step the value is shown as it is (1, 0.25; up to 6 decimals), not as "1.000"
            if (o.Step === undefined || o.Step === null)
                return typeof this.Value === "number" && isFinite(this.Value) ? String(Number(this.Value.toFixed(6))) : "";
            return formatNumber(this.Value, o.Step);
        }

        /** Render the value. */
        Render() {
            this.Input.value = this.Text();
            if (this.Value === null || this.Value === undefined)
                this.Input.removeAttribute("aria-valuenow");
            else
                this.Input.setAttribute("aria-valuenow", String(this.Value));
        }

        /**
         * Get the value.
         * @returns {number} The value.
         */
        Get() {
            const t = this;
            //  typed but not yet committed (no blur / Enter yet, ex: a form read on a button click): the typed number
            if (document.activeElement === t.Input && t.Input.value !== t.Text()) {
                const s = t.Input.value.trim();
                if (!s)
                    return t.Nullable ? null : t.Value;
                const v = t.Parse(s);
                return v === null ? t.Value : v;
            }
            return t.Value;
        }

        /**
         * Set the value (clamped to Min / Max).
         * @param {number} v The value.
         * @param {boolean} [silent] Don't call OnChange (and don't overwrite what the user is typing).
         */
        Set(v, silent) {
            const t = this;
            const o = t.O;
            v = v === null && t.Nullable ? null : clamp(v, o.Min ?? -Infinity, o.Max ?? Infinity);
            if (!t.InUser && !codeSet(t, v, silent))
                setBase(t, v);
            silent = silentOf(silent);
            t.Value = v;
            if (document.activeElement !== t.Input || !silent)
                t.Render();
            if (t.Error)
                t.Validate();
            //  The user's change runs OnChange in a scope (an auto save: its failure goes back and says why, userEdit)
            if (!silent && o.OnChange) {
                if (t.InUser)
                    t.ChangeScope = callChange(t, o.OnChange, [v]);
                else
                    o.OnChange(v);
            }
        }

        /**
         * Enable or disable.
         * @param {boolean} d Disabled.
         */
        SetDisabled(d, reason) {
            disableInput(this, d, reason);
        }
    }

    /**
     * Disable a text / number box the reachable way (UX guide INP-12): aria-disabled and read only (the box keeps the focus and its
     * value can be selected and copied; typing, paste and drop show the reason), muted. Not the disabled attribute.
     * @param {{Input: HTMLInputElement, O: object, Disabled: boolean}} t The control.
     * @param {boolean} d Disabled.
     * @param {string} [reason] Why (the tool tip while disabled).
     */
    function disableInput(t, d, reason) {
        const i = t.Input;
        t.Disabled = !!d;
        i.readOnly = t.Disabled || !!t.O.ReadOnly;
        Sw.Disabled.Set(i, t.Disabled, reason);
        if (i.SwDisabledInput)
            return;
        i.SwDisabledInput = true;
        /** An attempt to change the value of a disabled box: the reason. */
        const reject = ev => {
            if (!t.Disabled)
                return;
            ev.preventDefault();
            Sw.Disabled.Reject(i, ev);
        };
        i.addEventListener("keydown", ev => {
            if (t.Disabled && !ev.ctrlKey && !ev.metaKey && !ev.altKey && (ev.key.length === 1 || ev.key === "Backspace" || ev.key === "Delete"))
                reject(ev);
        });
        i.addEventListener("paste", reject);
        i.addEventListener("drop", reject);
    }

    // ------------------------------------------------------------------ text box

    /** Counter for unique element ids. */
    let idCounter = 0;

    /**
     * Create a unique element id.
     * @param {string} prefix Prefix.
     * @returns {string} The id.
     */
    function uid(prefix) {
        return prefix + (++idCounter) + "-" + Math.random().toString(36).slice(2, 6);
    }

    /**
     * A text input with an optional label, leading icon, clear button, password reveal and validation message.
     * Options: Value, Placeholder, Label (shown above, the same field label as NumberBox / Select), AriaLabel (only the accessible name), Hint (a line under the box), Title, Type ("text" | "password" | "email" | "search" | "url" | "tel"),
     *          Multiline (a textarea, Rows), Clear (a clear button), Reveal (password: a show / hide button), Icon (leading icon),
     *          Copy (true, or what it copies, ex: "user ID": a small copy button in the box (Sw.CopyButton), always there so nothing
     *          moves (LAY-9): disabled with a reason while the box is empty; not a tab stop (Ctrl+C copies in the box); never on a password),
     *          MaxLength, AutoComplete, InputMode, SpellCheck, Width (css), Disabled, ReadOnly, Mono (monospace font),
     *          Required (true, or the message: an empty box shows "Enter a value" under it on change / Validate(); the label gets a
     *          marker, the input aria-required), Validate(value) -> error text or null (checked on change), OnInput(value) (while typing),
     *          OnChange(value) (blur / Enter), OnEnter(value) (Enter without modifiers in a single line box).
     * Escape keeps the typed text, as the original inputs did; EscapeReverts: true makes Escape revert uncommitted typing (and only then
     * stop the event, so an Escape in an unchanged box can still close a dialog).
     * Properties: E (the root), Input (the input / textarea). Methods: Get(), Set(value, silent), Focus(select), SetError(text), Validate() (check Required / Validate now and show the
     * message; returns the message or null; Commit() returns false when invalid), SetDisabled(bool).
     */
    class TextBox {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            const id = uid("sw-text");
            const tag = o.Multiline ? "textarea" : "input";
            t.Input = el(tag + ".sw-input.sw-text-input", {
                id,
                placeholder: o.Placeholder || "",
                title: o.Title || null,
                readOnly: !!o.ReadOnly,
                attrs: {
                    type: o.Multiline ? null : (o.Type || "text"),
                    rows: o.Multiline ? (o.Rows || 4) : null,
                    maxlength: o.MaxLength || null,
                    autocomplete: o.AutoComplete || (o.Type === "password" ? "current-password" : "off"),
                    inputmode: o.InputMode || null,
                    spellcheck: o.SpellCheck ? "true" : "false",
                    "aria-label": o.AriaLabel || (!o.Label ? (o.Title || o.Placeholder || null) : null),
                },
            });
            if (o.Mono)
                t.Input.classList.add("sw-mono");
            t.Disabled = false;
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.Input, d => t.SetDisabled(d));
            if (o.Disabled)
                disableInput(t, true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            t.Field = el("div.sw-text-field", null, o.Icon ? Icon(o.Icon, "sw-text-icon") : null, t.Input);
            //  Copy: the first button (the right most, it never moves when the clear button comes and goes)
            if (o.Copy && o.Type !== "password") {
                t.CopyBtn = CopyButton({
                    Get: () => t.Input.value,
                    What: typeof o.Copy === "string" ? o.Copy : undefined,
                    Title: typeof o.Copy === "string" ? undefined : _TF("Copy the text", "Tool tip of a small copy button inside an input box: copies the text of the box"),
                    Class: "sw-text-copy",
                });
                //  A mouse / touch helper: the keyboard copies in the box itself (no extra tab stop between the fields of a form)
                t.CopyBtn.E.tabIndex = -1;
                t.Field.append(t.CopyBtn.E);
            }
            if (o.Clear) {
                t.ClearBtn = new Button({
                    Icon: "clearInput", Kind: "ghost", Small: true, Class: "sw-text-clear",
                    Title: _TF("Clear", "Tool tip of a button that clears the text of an input field"),
                    OnClick: () => {
                        /* SwWeb:allow ux-fbk-2 (an auto save the clear starts is the text box's own work: busy, failure and revert are shown at the box) */
                        userSet(t, "");
                        t.Input.focus();
                        if (o.OnInput)
                            o.OnInput("");
                    },
                });
                t.Field.append(t.ClearBtn.E);
            }
            if (o.Reveal && o.Type === "password") {
                const rb = new Button({
                    Icon: "eye", Kind: "ghost", Small: true, Class: "sw-text-reveal", Pressable: true,
                    Title: _TF("Show or hide the password", "Tool tip of a button that toggles the visibility of a password field"),
                    OnClick: b => {
                        t.Input.type = b.Pressed ? "text" : "password";
                        b.SetIcon(b.Pressed ? "eyeOff" : "eye");
                    },
                });
                t.Field.append(rb.E);
            }
            t.ErrorE = el("div.sw-text-error", { id: id + "-err", attrs: { "aria-live": "polite" } });
            t.ErrorE.hidden = true;
            //  a box that validates (Required, Validate) has its message line before the first message (LAY-9)
            const errorPart = o.Required || o.Validate ? reserveError(t, o, requiredText(o)) : t.ErrorE;
            t.LabelE = o.Label && !o.AriaLabel ? el("label.sw-text-label.sw-field-label", { htmlFor: id, text: o.Label }, requiredMark(o)) : null;
            t.E = el("div.sw-text", null, t.LabelE,
                t.Field, o.Hint ? el("div.sw-field-hint", { id: id + "-hint", text: o.Hint }) : null, errorPart);
            if (o.Hint)
                t.Input.setAttribute("aria-describedby", id + "-hint");
            if (o.Required)
                t.Input.setAttribute("aria-required", "true");
            if (o.Width)
                t.E.style.width = o.Width;
            t.Committed = o.Value ?? "";
            t.Base = t.Committed;
            t.Input.value = t.Committed;
            t.UpdateClear();
            //  Not user data (Dirty: false: a type-to-confirm word, a search): said on the root too, so every dirty check sees it (POP-6)
            if (o.Dirty === false)
                t.E.setAttribute("data-sw-nodirty", "");
            t.Input.addEventListener("input", e => {
                t.UpdateClear();
                if (t.Error)
                    t.SetError(null);
                o.OnInput && o.OnInput(t.Input.value);
                //  A value put in by code (a draft restored by Sw.Draft.Attach, a script: an input event that is not the user's and not
                //  this box's own Escape revert) is the new base, not an edit (no false "lost": A2-8, F9)
                if (!e.isTrusted && !t.OwnInput) {
                    setBase(t, t.Input.value);
                    return;
                }
                //  Typing is an edit: sw-change and the page dirty state (SAF-10; typing back to the base is clean)
                userEdit(t, t.Input.value, null);
            });
            //  Draft: "context" (or { Context, Dirty }): the text is kept as a draft while typing and restored on the next load (SAF-11);
            //  the notice goes on its own line under the field and its hint (the hint belongs to the field)
            if (o.Draft && Sw.Draft && Sw.Draft.Attach) {
                const dopt = typeof o.Draft === "object" ? o.Draft : { Context: String(o.Draft) };
                if (/\b(key|token|secret|password|passphrase|pin|otp)\b/i.test(String(o.Label || o.AriaLabel || o.Placeholder || "")) || o.Type === "password")
                    Sw.DevWarn('Sw.TextBox "' + (o.Label || o.AriaLabel || "") + '" with Draft: the field looks like a secret (key / token / password); a secret is never kept in local storage (UX guide SEC-1). Remove Draft.', "tb-draft-secret:" + (o.Label || ""));
                t.DraftH = Sw.Draft.Attach(t.Input, Object.assign({}, dopt, { Notice: dopt.Notice || t.E.querySelector(":scope > .sw-field-hint") || t.Field }));
                if (t.DraftH)
                    watchDraft(t);
            }
            t.Input.addEventListener("change", () => t.Commit());
            t.Input.addEventListener("keydown", e => {
                if (e.key === "Enter" && !o.Multiline && Sw.IsPureEvent(e)) {
                    t.Commit();
                    if (o.OnEnter) {
                        e.preventDefault();
                        t.Enter();
                    }
                }
                else if (e.key === "Escape" && o.EscapeReverts && t.Input.value !== t.Committed) {
                    e.stopPropagation();
                    e.preventDefault();
                    t.Input.value = t.Committed;
                    //  The revert is an edit like typing: an input event (OnInput, drafts, a dialog's and the page's dirty state see it)
                    t.OwnInput = true;
                    try {
                        t.Input.dispatchEvent(new Event("input", { bubbles: true }));
                    }
                    finally {
                        t.OwnInput = false;
                    }
                }
            });
            if (t.LabelE)
                keepLabelFor(t.Input, t.LabelE);
            ctlInit(t, "Sw.TextBox", Schema.TextBox, true);
        }

        /**
         * Run OnEnter the way Enter does: in a click scope, so the one result path applies (a failure or a false result is shown once,
         * FBK-5) and a successful Enter that sent the edited values cleans them (a save by Enter, SAF-10). Busy while it runs (repeats
         * absorbed).
         * @returns {Promise<void>} Settles when it ended.
         */
        async Enter() {
            const t = this;
            const o = t.O;
            if (!o.OnEnter || t.EnterBusy)
                return;
            const watchSave = o.Saves !== false ? sentValues(t.E) : null;
            const sc = Sw.Work.Scope(signal => o.OnEnter(t.Input.value, { Signal: signal }));
            const p = sc.Threw ? null : Sw.Work.Promise(sc);
            if (!p) {
                if (watchSave)
                    watchSave.Rec.Stop();
                if (sc.Threw)
                    Sw.Work.Report(Sw.Work.Outcome(sc, { Failed: true, Error: sc.Error }), { Name: ctlName(t) });
                return;
            }
            t.EnterBusy = true;
            const said = Sw.Work.Watch();
            const res = await Sw.Work.Busy(t.Input, p, { Name: ctlName(t), SetSpin: () => { }, SetTitle: () => { }, Leave: o.Leave });
            t.EnterBusy = false;
            const out = Sw.Work.Outcome(sc, res);
            if (out.Ok)
                savedIfSent(t.E, watchSave, ctlName(t) + " (Enter)");
            else if (watchSave)
                watchSave.Rec.Stop();
            Sw.Work.Report(out, { Name: ctlName(t), Said: said.Stop(), Spun: false, Done: false });
        }

        /** Show the clear button only when there is text; the copy button stays (LAY-9) and is enabled only when there is text. */
        UpdateClear() {
            if (this.ClearBtn)
                this.ClearBtn.E.hidden = !this.Input.value || this.Disabled || this.Input.readOnly;
            if (this.CopyBtn) {
                const empty = !this.Input.value;
                if (empty !== !!this.CopyEmpty) {
                    this.CopyEmpty = empty;
                    this.CopyBtn.SetDisabled(empty, _TF("There is nothing to copy yet", "Reason of the disabled copy button of an empty input box"));
                }
            }
        }

        /**
         * Commit the typed value (validate, call OnChange if it changed).
         * @returns {boolean} False if the validation failed.
         */
        Commit() {
            const t = this;
            const v = t.Input.value;
            if (t.O.Required && !v.trim()) {
                t.SetError(requiredText(t.O));
                return false;
            }
            if (t.O.Validate) {
                const err = t.O.Validate(v);
                t.SetError(err || null);
                if (err)
                    return false;
            }
            //  Valid: a shown message goes (also one set by a form, FRM-3)
            if (t.Error)
                t.SetError(null);
            if (v === t.Committed)
                return true;
            t.Committed = v;
            if (t.O.OnChange) {
                //  An OnChange that returns a promise or starts a request saves the text: clean when it succeeds
                const sc = callChange(t, t.O.OnChange, [v]);
                userEdit(t, v, savesItself(sc) ? sc : "applied");
            }
            return true;
        }

        /** Stop keeping a draft (the Draft option) and remove its stored text: call it after the text was sent or saved (Sw.Form does). */
        ClearDraft() {
            if (this.DraftH)
                this.DraftH.Clear();
        }

        /**
         * Get the current text.
         * @returns {string} The text.
         */
        Get() {
            return this.Input.value;
        }

        /**
         * Check the box now (Required, Validate) and show the message under it (ex: on a form's Save / Next; Sw.Form calls it).
         * @returns {string|null} The message, null when valid.
         */
        Validate() {
            return this.Commit() ? null : this.Error;
        }

        /**
         * Set the text.
         * @param {string} v The text.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Set(v, silent) {
            const t = this;
            v = v === null || v === undefined ? "" : String(v);
            t.Input.value = v;
            t.Committed = v;
            if (!t.InUser && !codeSet(t, v, silent))
                setBase(t, v);
            silent = silentOf(silent);
            t.UpdateClear();
            if (t.Error)
                t.Validate();
            if (!silent && t.O.OnChange)
                t.O.OnChange(v);
        }

        /**
         * Focus the input.
         * @param {boolean} [select] Also select the text.
         */
        Focus(select) {
            this.Input.focus();
            if (select)
                this.Input.select();
        }

        /**
         * Show (or clear) a validation message.
         * @param {string|null} text The message, null clears it.
         */
        SetError(text) {
            const t = this;
            showError(t, t.Input, text);
            t.E.classList.toggle("sw-invalid", !!text);
        }

        /**
         * Enable or disable.
         * @param {boolean} d Disabled.
         */
        SetDisabled(d, reason) {
            disableInput(this, d, reason);
            this.UpdateClear();
        }
    }

    /** @type {Set<TextBox>} Text boxes with a draft, disposed when they leave the page (a closed dialog: no leak, no stale dirty mark). */
    const drafted = new Set();
    /** @type {number} The timer that checks them. */
    let draftTimer = 0;

    /**
     * Dispose the draft of a TextBox once it was on the page and is removed (the stored draft stays; Discard / save clear it).
     * @param {TextBox} t The text box.
     */
    function watchDraft(t) {
        drafted.add(t);
        //  A locked field (disabled / read only, ex: a sent invitation) offers no Restore (Admin N12: Restore rewrote a sent, locked
        //  address): the draft handle is locked (Sw.Draft Lock) or its notices hidden while the field is locked
        const sync = () => lockDraft(t, !!(t.Input.readOnly || t.Input.disabled || t.Input.getAttribute("aria-disabled") === "true"));
        sync();
        if (typeof MutationObserver === "function" && !t.LockMo) {
            t.LockMo = new MutationObserver(sync);
            t.LockMo.observe(t.Input, { attributes: true, attributeFilter: ["readonly", "disabled", "aria-disabled"] });
        }
        if (draftTimer)
            return;
        draftTimer = setInterval(() => {
            for (const x of Array.from(drafted)) {
                if (x.E.isConnected) {
                    x.WasOnPage = true;
                    continue;
                }
                if (!x.WasOnPage && !x.Destroyed)
                    continue;
                drafted.delete(x);
                if (x.DraftH)
                    x.DraftH.Dispose();
                x.DraftH = null;
            }
            if (!drafted.size) {
                clearInterval(draftTimer);
                draftTimer = 0;
            }
        }, 1000);
    }

    /**
     * Lock or unlock the draft of a text box: a locked field (disabled, read only) can't take a restored text, so Restore / Discard are
     * not offered (Sw.Draft handle Lock(bool) when the core has it, else its notices are hidden while locked).
     * @param {TextBox} t The text box.
     * @param {boolean} locked Locked.
     */
    function lockDraft(t, locked) {
        if (!!t.DraftLocked === locked)
            return;
        t.DraftLocked = locked;
        const hs = [t.DraftH, t.Input && t.Input.SwDraft].filter((h, i, a) => h && a.indexOf(h) === i);
        let api = false;
        for (const h of hs)
            if (typeof h.Lock === "function") {
                api = true;
                try {
                    h.Lock(locked);
                }
                catch {
                    //  (the handle was disposed)
                }
            }
        if (api)
            return;
        const host = t.Field || t.E;
        const box = host && host.parentElement ? host.parentElement : host;
        if (!box)
            return;
        const apply = () => {
            for (const n of box.querySelectorAll(".sw-draft-notice")) {
                if (!host.contains(n) && n.previousElementSibling !== host && !(t.E && t.E.contains(n)))
                    continue;
                if (t.DraftLocked && !n.hidden) {
                    n.hidden = true;
                    n.SwLockHidden = true;
                }
                else if (!t.DraftLocked && n.SwLockHidden) {
                    n.hidden = false;
                    n.SwLockHidden = false;
                }
            }
        };
        apply();
        //  (a notice shown later, ex: after the user is known, is hidden too while the field is locked)
        if (locked && typeof MutationObserver === "function") {
            if (!t.LockNoticeMo)
                t.LockNoticeMo = new MutationObserver(apply);
            t.LockNoticeMo.observe(box, { childList: true, subtree: true });
        }
        else if (t.LockNoticeMo)
            t.LockNoticeMo.disconnect();
    }

    /** Destroy a TextBox: its draft handle is disposed too (the common Destroy for the rest). */
    TextBox.prototype.Destroy = function () {
        if (this.DraftH) {
            this.DraftH.Dispose();
            this.DraftH = null;
        }
        drafted.delete(this);
        Common.Destroy.call(this);
    };

    // ------------------------------------------------------------------ select

    /**
     * A drop down: a button that opens a touch friendly list. Lists longer than SearchFrom (or with Search: true) open a popup with a filter box.
     * Options: Options: [{ Value, Label, Icon, Hint, Title (option tooltip), Disabled }], Value, Placeholder (shown when no option matches the value),
     *          Small, Title (tooltip / aria label), Width (css), Search (true / false, default: automatic), SearchFrom (default 12),
     *          Label (the visible label above it, the same field as TextBox / NumberBox; E is then a div.sw-field and the
     *          accessible name reads "label, value"), AriaLabel (only the accessible name), Hint, Disabled, OnChange(value),
     *          Required (true, or the message: with no value Validate() shows "Choose a value" under it; the label gets a marker).
     * Keyboard: Enter / Space / ArrowDown open it; in the list arrows, Home / End, PageUp / PageDown move, Enter selects, Escape closes, typing filters.
     * Properties: E (the button, or the field with a Label), Button (the button).
     * Methods: Get(), Set(value, silent), SetOptions(options, value) (no OnChange), SetDisabled(bool), Open(), SetError(text),
     * Validate() (Required: check now and show the message; returns the message or null).
     */
    class Select {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            //  Options (alias Items), option Label (alias Text)
            //  (the check sees the options as given: Items is a documented name, no warning for it)
            t.OGiven = o;
            o = Object.assign({}, o, { Options: canonItems(o.Options || o.Items, "Label") });
            t.O = o;
            t.Value = o.Value;
            t.Base = o.Value;
            t.Label = el("span.sw-select-label");
            t.IconE = el("span.sw-select-icon");
            t.Button = el("button.sw-button.sw-select", {
                type: "button", title: o.Title || null,
                attrs: { "aria-haspopup": "listbox", "aria-expanded": "false", "aria-label": o.AriaLabel || o.Title || null },
            }, t.IconE, t.Label, Icon("down", "sw-select-arrow"));
            t.E = t.Button;
            if (o.Small)
                t.Button.classList.add("sw-small");
            if (o.Width)
                t.Button.style.width = o.Width;
            t.Disabled = false;
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.Button, d => t.SetDisabled(d));
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            t.Render();
            t.Button.addEventListener("click", e => {
                //  A modifier click only focuses (INP-2)
                if (Sw.ModifierClick(e))
                    return;
                t.Open();
            });
            t.Button.addEventListener("keydown", e => {
                if ((e.key === "ArrowDown" || e.key === "ArrowUp") && !e.ctrlKey && !e.metaKey) {
                    e.preventDefault();
                    if (t.Disabled)
                        Sw.Disabled.Reject(t.Button, e);
                    else
                        t.Open();
                }
            });
            //  Label is the visible label; the name is "label, value" (aria-labelledby: the label, then the shown value)
            if (o.Label && !o.AriaLabel) {
                const f = fieldOf(t.Button, o);
                f.LabelE.id = uid("sw-field-l");
                t.Label.id = uid("sw-select-v");
                t.Button.setAttribute("aria-labelledby", f.LabelE.id + " " + t.Label.id);
                t.E = f.E;
                t.LabelE = f.LabelE;
                t.E.classList.add("sw-field-select");
            }
            if (o.Required) {
                t.Button.setAttribute("aria-required", "true");
                if (t.E === t.Button)
                    t.E = el("div.sw-field.sw-field-select", null, t.Button);
                t.ErrorE = el("div.sw-text-error", { id: uid("sw-sel-err"), attrs: { "aria-live": "polite" } });
                t.ErrorE.hidden = true;
                //  its line is there before the first message (LAY-9)
                t.E.append(reserveError(t, o, typeof o.Required === "string" ? o.Required : _TF("Choose a value", "Message under an empty drop down that must be filled in")));
            }
            ctlInit(t, "Sw.Select", Schema.Select, true);
        }

        /**
         * Show (or clear) a validation message under the select (with Required).
         * @param {string|null} text The message, null clears it.
         */
        SetError(text) {
            showError(this, this.Button, text);
        }

        /**
         * Check now (Required) and show the message (Sw.Form calls it).
         * @returns {string|null} The message, null when valid.
         */
        Validate() {
            const t = this;
            const v = t.Value;
            const empty = v === undefined || v === null || v === "";
            t.SetError(t.O.Required && empty
                ? (typeof t.O.Required === "string" ? t.O.Required : _TF("Choose a value", "Message under an empty drop down that must be filled in"))
                : null);
            return t.Error;
        }

        /** Render the selected option. */
        Render() {
            const t = this;
            const opt = (t.O.Options || []).find(x => x.Value === t.Value);
            t.Label.textContent = opt ? opt.Label : (t.Value === undefined || t.Value === null || t.Value === "" ? (t.O.Placeholder || "") : String(t.Value));
            t.Label.classList.toggle("sw-placeholder", !opt && !!t.O.Placeholder);
            t.IconE.replaceChildren();
            if (opt && opt.Icon)
                t.IconE.append(Icon(opt.Icon));
            t.IconE.hidden = !(opt && opt.Icon);
            //  The popup kind: a short list opens a menu, a long one a list box with a filter (INP-3)
            t.Button.setAttribute("aria-haspopup", t.IsSearchable() ? "listbox" : "menu");
        }

        /**
         * Test if the list is searchable.
         * @returns {boolean} True if a filter box is shown.
         */
        IsSearchable() {
            const s = this.O.Search;
            if (s === true || s === false)
                return s;
            return (this.O.Options || []).length >= (this.O.SearchFrom ?? 12);
        }

        /** Open the list. */
        Open() {
            const t = this;
            if (t.Popup)
                return;
            if (t.Disabled) {
                Sw.Disabled.Reject(t.Button);
                return;
            }
            if (Sw.Disabled.Is(t.Button))
                return;
            const opts = t.O.Options || [];
            const done = () => {
                t.Popup = null;
                t.Button.setAttribute("aria-expanded", "false");
            };
            t.Button.setAttribute("aria-expanded", "true");
            if (!t.IsSearchable()) {
                t.Popup = Sw.Menu.Show(t.Button, opts.map(opt => ({
                    Text: opt.Label,
                    Icon: opt.Icon,
                    Hint: opt.Hint,
                    Title: opt.Title,
                    Disabled: opt.Disabled,
                    Checked: opt.Value === t.Value,
                    OnClick: () => t.Pick(opt.Value),
                })), { OnClose: done, MinWidth: t.Button.offsetWidth, Focus: opts.findIndex(x => x.Value === t.Value) });
                return;
            }
            t.Popup = ListPicker(t.Button, opts, t.Value, v => t.Pick(v), { OnClose: done });
        }

        /**
         * Get the value.
         * @returns {*} The selected value.
         */
        Get() {
            return this.Value;
        }

        /**
         * Replace the options (no OnChange).
         * @param {Array<{Value: *, Label: string}>} options The options.
         * @param {*} [value] Optional new value.
         */
        SetOptions(options, value) {
            this.O.Options = canonItems(options, "Label");
            if (value !== undefined)
                this.Value = value;
            this.Render();
        }

        /**
         * Set the value.
         * @param {*} v The value.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Set(v, silent) {
            if (!this.InUser && !codeSet(this, v, silent))
                setBase(this, v);
            silent = silentOf(silent);
            this.Value = v;
            this.Render();
            if (this.Error)
                this.SetError(null);
            if (!silent && this.O.OnChange)
                this.O.OnChange(v);
        }

        /**
         * Enable or disable.
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why (the tool tip while disabled).
         */
        SetDisabled(d, reason) {
            //  aria-disabled (INP-12): stays focusable, a click or an arrow key shows the reason
            this.Disabled = !!d;
            Sw.Disabled.Set(this.Button, this.Disabled, reason);
        }
    }

    /**
     * Show a searchable list popup (used by Select): a filter box and a list (role listbox) of options.
     * @param {HTMLElement} anchor The element to show it at.
     * @param {Array<{Value: *, Label: string, Icon: string, Hint: string, Disabled: boolean}>} opts The options.
     * @param {*} value The current value (marked and initially active).
     * @param {function(*):void} onPick Called with the picked value.
     * @param {object} [po] Options passed to Sw.Popup.Show (OnClose ...).
     * @returns {{Close: function():void}} The popup.
     */
    function ListPicker(anchor, opts, value, onPick, po) {
        const listId = uid("sw-list");
        const input = el("input.sw-input.sw-list-filter", {
            type: "search", placeholder: _TF("Type to filter", "Placeholder of a search box that filters a list of options"),
            attrs: { "aria-controls": listId, "aria-autocomplete": "list", role: "combobox", "aria-expanded": "true", autocomplete: "off", spellcheck: "false" },
        });
        const list = el("div.sw-list", { id: listId, attrs: { role: "listbox" } });
        const empty = el("div.sw-list-empty", { text: _TF("Nothing found", "Shown when filtering a list gave no results") });
        let shown = [];
        let active = -1;
        let popup = null;
        /** Mark the active option (keyboard selection) and scroll it into view. */
        const setActive = i => {
            if (!shown.length) {
                active = -1;
                input.removeAttribute("aria-activedescendant");
                return;
            }
            active = clamp(i, 0, shown.length - 1);
            shown.forEach((x, k) => x.E.classList.toggle("sw-active", k === active));
            const a = shown[active].E;
            input.setAttribute("aria-activedescendant", a.id);
            //  Scroll only the list (scrollIntoView would also scroll the clipped popup box)
            if (a.offsetTop < list.scrollTop)
                list.scrollTop = a.offsetTop;
            else if (a.offsetTop + a.offsetHeight > list.scrollTop + list.clientHeight)
                list.scrollTop = a.offsetTop + a.offsetHeight - list.clientHeight;
        };
        /** Pick an option: close the popup and report the value. */
        const pick = opt => {
            if (opt.Disabled)
                return;
            popup.Close();
            onPick(opt.Value);
        };
        /** Render the options matching the filter text. */
        const render = () => {
            const f = input.value.trim().toLowerCase();
            list.replaceChildren();
            shown = [];
            for (const opt of opts) {
                const label = String(opt.Label ?? opt.Value);
                if (f && label.toLowerCase().indexOf(f) < 0 && String(opt.Hint || "").toLowerCase().indexOf(f) < 0)
                    continue;
                const row = el("div.sw-list-option", {
                    id: uid("sw-opt"), title: opt.Title || null,
                    attrs: { role: "option", "aria-selected": opt.Value === value ? "true" : "false", "aria-disabled": opt.Disabled ? "true" : null },
                },
                    opt.Value === value ? Icon("check", "sw-list-check") : el("span.sw-list-check"),
                    opt.Icon ? Icon(opt.Icon) : null,
                    el("span.sw-list-text", { text: label }),
                    opt.Hint ? el("span.sw-list-hint", { text: opt.Hint }) : null);
                row.addEventListener("click", ev => {
                    //  A modifier click marks the option (the current item, INP-2) without picking it; a disabled one says why
                    if (!Sw.IsPureEvent(ev)) {
                        setActive(shown.findIndex(x => x.E === row));
                        return;
                    }
                    if (opt.Disabled) {
                        Sw.Disabled.Reject(row, ev);
                        return;
                    }
                    pick(opt);
                });
                shown.push({ E: row, Opt: opt });
                list.append(row);
            }
            if (!shown.length)
                list.append(empty);
            const sel = shown.findIndex(x => x.Opt.Value === value);
            setActive(f ? 0 : Math.max(0, sel));
        };
        input.addEventListener("input", render);
        input.addEventListener("keydown", e => {
            const page = Math.max(1, Math.floor(list.clientHeight / 40));
            switch (e.key) {
                case "ArrowDown": setActive(active + 1); break;
                case "ArrowUp": setActive(active - 1); break;
                case "PageDown": setActive(active + page); break;
                case "PageUp": setActive(active - page); break;
                case "Home": if (e.ctrlKey) setActive(0); else return; break;
                case "End": if (e.ctrlKey) setActive(shown.length - 1); else return; break;
                case "Enter":
                    if (active >= 0 && shown[active])
                        pick(shown[active].Opt);
                    break;
                default: return;
            }
            e.preventDefault();
        });
        const box = el("div.sw-listpicker", null, input, list);
        popup = Sw.Popup.Show(anchor, box, Object.assign({ MinWidth: anchor.offsetWidth, Class: "sw-listpicker-popup" }, po || {}));
        render();
        popup.Place();
        input.focus({ preventScroll: true });
        return popup;
    }

    // ------------------------------------------------------------------ color picker

    const { HsvToRgb: hsvToRgb, RgbToHsv: rgbToHsv, ToHex: toHex, FromHex: fromHex } = Sw.Color;

    /**
     * A color swatch that opens a picker popup (saturation / value square, hue bar, hex field, intensity for HDR colors).
     * Values are arrays of 0..1 floats: [r, g, b] or [r, g, b, a] (alpha is kept as is); with Hex: true values are "#rrggbb" strings.
     * Components above 1 (HDR) are shown as color × intensity.
     * Keyboard: the square and the bar take the focus, arrows move (Shift = bigger steps).
     * Options: Value, Hex, Title, Disabled, OnInput(value) (while dragging), OnChange(value) (when the popup closes with a new value).
     * Methods: Get(), Set(value, silent), Open(), SetDisabled(bool).
     */
    class ColorPicker {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.Swatch = el("span.sw-swatch-color");
            t.E = el("button.sw-button.sw-swatch", { type: "button", title: o.Title || null, attrs: { "aria-label": o.Title || _TF("Color", "Accessible name of a color picker button") } }, t.Swatch);
            t.Disabled = false;
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.E, d => t.SetDisabled(d));
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            t.Set(o.Value || (o.Hex ? "#ffffff" : [1, 1, 1]), true);
            t.E.addEventListener("click", e => {
                //  A modifier click only focuses (INP-2); a disabled picker is stopped by its guard (INP-12)
                if (Sw.ModifierClick(e))
                    return;
                t.Open();
            });
            ctlInit(t, "Sw.ColorPicker", Schema.ColorPicker, true);
        }

        /**
         * Convert an internal value to the external format.
         * @param {number[]} v [r, g, b(, a)].
         * @returns {number[]|string} The value (a copy, or a hex string).
         */
        Out(v) {
            return this.O.Hex ? toHex(v) : v.slice();
        }

        /**
         * Get the value.
         * @returns {number[]|string} The color.
         */
        Get() {
            return this.Out(this.Value);
        }

        /**
         * Set the value.
         * @param {number[]|string} v The color ([r, g, b(, a)] or a hex string).
         * @param {boolean} [silent] Don't call OnInput / OnChange.
         */
        Set(v, silent) {
            const t = this;
            if (typeof v === "string")
                v = fromHex(v) || [1, 1, 1];
            t.Value = Array.isArray(v) ? v.slice() : [1, 1, 1];
            const m = Math.max(1, t.Value[0], t.Value[1], t.Value[2]);
            const hex = toHex(t.Value.slice(0, 3).map(x => x / m));
            t.Swatch.style.background = hex;
            t.E.setAttribute("data-color", hex);
            if (!silent) {
                t.O.OnInput && t.O.OnInput(t.Get());
                t.O.OnChange && t.O.OnChange(t.Get());
            }
        }

        /**
         * Enable or disable.
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why (the tool tip while disabled).
         */
        SetDisabled(d, reason) {
            //  aria-disabled (INP-12): stays focusable, a click shows the reason
            this.Disabled = !!d;
            Sw.Disabled.Set(this.E, this.Disabled, reason);
        }

        /** Open the picker popup. */
        Open() {
            const t = this;
            const v0 = t.Value.slice();
            const intensity0 = Math.max(1, v0[0], v0[1], v0[2]);
            let [h, s, v] = rgbToHsv(v0[0] / intensity0, v0[1] / intensity0, v0[2] / intensity0);
            let intensity = intensity0;
            const sv = el("div.sw-cp-sv", { tabIndex: 0, attrs: { role: "slider", "aria-label": _TF("Saturation and brightness", "Accessible name of the color area of a color picker") } });
            const svDot = el("div.sw-cp-dot");
            const hue = el("div.sw-cp-hue", { tabIndex: 0, attrs: { role: "slider", "aria-label": _TF("Hue", "Accessible name of the hue bar of a color picker"), "aria-valuemin": "0", "aria-valuemax": "360" } });
            const hueBar = el("div.sw-cp-bar");
            sv.append(svDot);
            hue.append(hueBar);
            const hex = el("input.sw-input.sw-mono", { type: "text", attrs: { spellcheck: "false", maxlength: "7", "aria-label": _TF("Hex color", "Accessible name of the hex text field of a color picker") } });
            const preview = el("span.sw-swatch-color.sw-cp-preview");
            const hdr = intensity0 > 1;
            const intensitySlider = hdr ? new Slider({
                Min: 1, Max: Math.max(4, Math.ceil(intensity0 * 2)), Step: 0.01, Value: intensity0,
                Label: _TF("Intensity", "Label of the intensity slider of a color picker (colors brighter than white)"),
                OnInput: x => {
                    intensity = x;
                    update(true);
                },
            }) : null;
            const body = el("div.sw-cp", null, sv, hue, el("div.sw-cp-row", null, preview, hex),
                intensitySlider ? el("div.sw-cp-intensity", null, el("div.sw-muted", { text: _TF("Intensity", "Label of the intensity slider of a color picker (colors brighter than white)") }), intensitySlider.E) : null);
            const value = () => {
                const rgb = hsvToRgb(h, s, v).map(x => x * intensity);
                return v0.length > 3 ? rgb.concat(v0.slice(3)) : rgb;
            };
            const update = (emit, fromHexInput) => {
                const rgb = hsvToRgb(h, s, v);
                sv.style.backgroundColor = toHex(hsvToRgb(h, 1, 1));
                svDot.style.left = (s * 100) + "%";
                svDot.style.top = ((1 - v) * 100) + "%";
                hueBar.style.top = (h * 100) + "%";
                preview.style.background = toHex(rgb);
                sv.setAttribute("aria-valuetext", Math.round(s * 100) + "%, " + Math.round(v * 100) + "%");
                hue.setAttribute("aria-valuenow", String(Math.round(h * 360)));
                if (!fromHexInput)
                    hex.value = toHex(rgb);
                if (emit) {
                    noteUser(t.E);
                    const val = value();
                    t.Set(val, true);
                    t.O.OnInput && t.O.OnInput(t.Get());
                }
            };
            const drag = (area, fn) => {
                Sw.Drag(area, {
                    Threshold: 0,
                    OnStart: e => {
                        area.focus({ preventScroll: true });
                        fn(e, area.getBoundingClientRect());
                    },
                    OnMove: e => fn(e, area.getBoundingClientRect()),
                });
            };
            drag(sv, (e, r) => {
                s = clamp((e.clientX - r.left) / r.width, 0, 1);
                v = clamp(1 - (e.clientY - r.top) / r.height, 0, 1);
                update(true);
            });
            drag(hue, (e, r) => {
                h = clamp((e.clientY - r.top) / r.height, 0, 0.9999);
                update(true);
            });
            sv.addEventListener("keydown", e => {
                const d = e.shiftKey ? 0.1 : 0.02;
                switch (e.key) {
                    case "ArrowLeft": s = clamp(s - d, 0, 1); break;
                    case "ArrowRight": s = clamp(s + d, 0, 1); break;
                    case "ArrowUp": v = clamp(v + d, 0, 1); break;
                    case "ArrowDown": v = clamp(v - d, 0, 1); break;
                    default: return;
                }
                e.preventDefault();
                update(true);
            });
            hue.addEventListener("keydown", e => {
                const d = e.shiftKey ? 0.05 : 0.01;
                switch (e.key) {
                    case "ArrowUp": case "ArrowLeft": h = clamp(h - d, 0, 0.9999); break;
                    case "ArrowDown": case "ArrowRight": h = clamp(h + d, 0, 0.9999); break;
                    default: return;
                }
                e.preventDefault();
                update(true);
            });
            hex.addEventListener("input", () => {
                const rgb = fromHex(hex.value);
                if (!rgb)
                    return;
                [h, s, v] = rgbToHsv(rgb[0], rgb[1], rgb[2]);
                update(true, true);
            });
            hex.addEventListener("keydown", e => {
                if (e.key === "Enter")
                    popup.Close();
            });
            update(false);
            const popup = Sw.Popup.Show(t.E, body, {
                Class: "sw-cp-popup",
                OnClose: () => {
                    const val = t.Value;
                    if (val.some((x, i) => x !== v0[i]))
                        t.O.OnChange && t.O.OnChange(t.Get());
                },
            });
            sv.focus({ preventScroll: true });
        }
    }

    // ------------------------------------------------------------------ badge, chip

    /**
     * Create a badge (a small rounded label).
     * @param {string} text The text.
     * @param {string} [kind] "ok" | "error" | "warning" | "accent" | "accent2" (default: neutral).
     * @param {string} [title] Optional tooltip.
     * @returns {HTMLSpanElement} The badge (class sw-badge).
     */
    function Badge(text, kind, title) {
        //  also Sw.Badge({ Text, Kind, Title }) (the options form of every other component); Kind "danger" = "error"
        if (text && typeof text === "object" && !(text instanceof Node)) {
            kind = text.Kind;
            title = text.Title;
            text = text.Text;
        }
        kind = canonKind(kind, true);
        return el("span.sw-badge", { class: kind ? "sw-" + kind : null, text: text, title: title || null });
    }

    /**
     * A chip: a small rounded toggle (filters, tags), optionally removable.
     * Options: Text, Icon, Title, Pressed, Pressable (default true), Removable (an x button), Disabled (true or the reason), OnChange(pressed),
     * OnRemove(chip), OnClick(chip, event).
     * Methods: Get() / Set(pressed, silent), Remove(), SetDisabled(bool, reason).
     */
    class Chip {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.Pressable = o.Pressable !== false;
            t.Btn = el("button.sw-chip", { type: "button", title: o.Title || null }, o.Icon ? Icon(o.Icon) : null, el("span", { text: o.Text || "" }));
            t.E = o.Removable ? el("span.sw-chip-wrap", null, t.Btn) : t.Btn;
            if (t.Pressable)
                t.Set(!!o.Pressed, true);
            t.Disabled = false;
            /* SwWeb:allow ux-inp-12 (the legacy E.disabled = true setter carries no reason; the developer warning names the control) */
            Sw.Disabled.Shadow(t.Btn, d => t.SetDisabled(d));
            t.Btn.addEventListener("click", e => {
                //  A modifier click only focuses (INP-2); a disabled chip is stopped by its guard (INP-12)
                if (Sw.ModifierClick(e))
                    return;
                noteUser(t.E);
                if (t.Pressable)
                    t.Set(!t.Pressed);
                o.OnClick && o.OnClick(t, e);
            });
            if (o.Removable) {
                const x = el("button.sw-chip-remove", {
                    type: "button", title: _TF("Remove", "Tool tip of a button that removes a tag / chip"),
                    attrs: { "aria-label": o.Text ? _T("Remove {0}", o.Text, "Accessible name of the button that removes a tag / chip. {0} is the text of the chip, ex: 'Errors only'") : _TF("Remove", "Tool tip of a button that removes a tag / chip") },
                }, Icon("close"));
                t.RemoveB = x;
                x.addEventListener("click", e => {
                    if (Sw.ModifierClick(e))
                        return;
                    t.Remove();
                });
                t.E.append(x);
                t.Btn.addEventListener("keydown", e => {
                    if ((e.key === "Delete" || e.key === "Backspace") && Sw.IsPureEvent(e)) {
                        e.preventDefault();
                        if (t.Disabled)
                            Sw.Disabled.Reject(t.Btn, e);
                        else
                            t.Remove();
                    }
                });
            }
            if (o.Disabled)
                t.SetDisabled(true, typeof o.Disabled === "string" ? o.Disabled : undefined);
            ctlInit(t, "Sw.Chip", Schema.Chip, true);
        }

        /**
         * Enable or disable the chip (and its remove button): aria-disabled, focusable, says why when used (INP-12).
         * @param {boolean} d Disabled.
         * @param {string} [reason] Why (the tool tip while disabled).
         */
        SetDisabled(d, reason) {
            this.Disabled = !!d;
            Sw.Disabled.Set(this.Btn, this.Disabled, reason);
            if (this.RemoveB)
                Sw.Disabled.Set(this.RemoveB, this.Disabled, reason);
        }

        /**
         * Get the pressed state.
         * @returns {boolean} Pressed.
         */
        Get() {
            return !!this.Pressed;
        }

        /**
         * Set the pressed state.
         * @param {boolean} p Pressed.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Set(p, silent) {
            this.Pressed = !!p;
            this.Btn.setAttribute("aria-pressed", this.Pressed ? "true" : "false");
            if (!silent && this.O.OnChange)
                this.O.OnChange(this.Pressed);
        }

        /** Remove the chip (calls OnRemove). */
        Remove() {
            this.E.remove();
            this.O.OnRemove && this.O.OnRemove(this);
        }
    }

    // ------------------------------------------------------------------ progress bar, spinner

    /**
     * Format a text template with the arguments {0}, {1}, ... (uses the core's string formatter when present).
     * @param {string} fmt Template.
     * @param {...*} args Arguments.
     * @returns {string} The text.
     */
    function fmtArgs(fmt, ...args) {
        const F = Sw.Format;
        if (F && F.StringFormat)
            return F.StringFormat(fmt, ...args);
        return String(fmt).replace(/{(\d+)}/g, (m, n) => args[n] === undefined ? m : String(args[n]));
    }

    /**
     * Format a number with optional decimals (uses the core's formatter when present).
     * @param {number} v Value.
     * @param {number} decimals Decimals (negative = only when needed).
     * @returns {string} The text.
     */
    function fmtNum(v, decimals) {
        const F = Sw.Format;
        if (F && F.Number)
            return F.Number(v, decimals);
        if (decimals < 0)
            return Number.isInteger(v) ? String(v) : v.toFixed(-decimals);
        return v.toFixed(decimals);
    }

    /**
     * A progress bar (the old ProgressBar) with a centered text that stays readable on both the filled and the empty part.
     * Options: Value (percent 0..100), Text, Title, Indeterminate, Small, Label (accessible name).
     * Methods: SetValue(current, max, text, title) (templates: {0} percentage, {1} current, {2} max; default "current / max"),
     * SetPercentage(value, text, title) (templates: {0} percentage), SetIndeterminate(bool).
     */
    class ProgressBar {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.Fill = el("div.sw-progress-fill");
            t.TextA = el("div.sw-progress-text");
            t.TextB = el("div.sw-progress-text.sw-on");
            t.E = el("div.sw-progress", { attrs: { role: "progressbar", "aria-valuemin": "0", "aria-valuemax": "100", "aria-label": o.AriaLabel || o.Label || null } }, t.Fill, t.TextA, t.TextB);
            if (o.Small)
                t.E.classList.add("sw-small");
            /** Compatibility aliases of the old element names. */
            t.Element = t.E;
            t.Prog = t.Fill;
            if (o.Indeterminate)
                t.SetIndeterminate(true);
            else
                t.SetPercentage(o.Value ?? 0, o.Text, o.Title);
            t.O = o;
            ctlInit(t, "Sw.ProgressBar", Schema.ProgressBar);
        }

        /**
         * Set the progress as current / max.
         * @param {number} current Current value.
         * @param {number} max Maximum value (0 or less shows 0%).
         * @param {string} [text] Text template ({0} percentage, {1} current, {2} max), default "current / max".
         * @param {string} [title] Tooltip template (same arguments), default "current / max - percentage".
         */
        SetValue(current, max, text, title) {
            const val = max <= 0 ? 0 : (current * 100 / max);
            const pc = fmtNum(current, -2);
            const pm = fmtNum(max, -2);
            const pval = pc + " / " + pm;
            this.FromValue = true;
            try {
                this.SetPercentage(val, text ? fmtArgs(text, "{0}", pc, pm) : pval, title ? fmtArgs(title, "{0}", pc, pm) : pval + " - {0}");
            }
            finally {
                this.FromValue = false;
            }
        }

        /**
         * Set the progress in percent (clamped to 0..100).
         * @param {number} value Percentage.
         * @param {string} [text] Text template, {0} = the percentage ("12.34%"), default the percentage.
         * @param {string} [title] Tooltip template, {0} = the percentage, default the percentage.
         */
        SetPercentage(value, text, title) {
            const t = this;
            t.SetIndeterminate(false);
            //  (percent, not a fraction: 0.4 shows "0.40%"; a value over 100 is clamped: say it to the developer)
            if (!t.FromValue && typeof value === "number" && ((value > 0 && value < 1 && !Number.isInteger(value)) || value > 100 || value < 0))
                Sw.DevWarn("Sw.ProgressBar.SetPercentage(" + value + "): the value is in percent (0..100)" + (value > 0 && value < 1 ? "; " + value + " looks like a fraction (0..1): multiply by 100, or use SetValue(current, max)." : ": it is clamped."), "progress-range");
            value = clamp(+value || 0, 0, 100);
            const pf = fmtNum(value, 2) + "%";
            const tx = text ? fmtArgs(text, pf) : pf;
            t.E.title = title ? fmtArgs(title, pf) : pf;
            t.E.style.setProperty("--sw-p", value + "%");
            t.TextA.textContent = tx;
            t.TextB.textContent = tx;
            t.E.setAttribute("aria-valuenow", String(Math.round(value)));
            t.E.setAttribute("aria-valuetext", tx);
        }

        /**
         * Show an indeterminate (unknown progress) animation.
         * @param {boolean} on True for indeterminate.
         */
        SetIndeterminate(on) {
            this.E.classList.toggle("sw-indeterminate", !!on);
            if (on) {
                this.E.removeAttribute("aria-valuenow");
                this.TextA.textContent = "";
                this.TextB.textContent = "";
            }
        }
    }

    /**
     * A spinner (busy indicator).
     * Options: Size (css length or px number, default 1.35em), Title (accessible name, makes it role status), Class.
     */
    class Spinner {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            o = o || {};
            Sw.Work.CheckOptions("Sw.Spinner", o, Schema.Spinner);
            const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
            svg.setAttribute("viewBox", "0 0 24 24");
            svg.setAttribute("class", "sw-spinner" + (o.Class ? " " + o.Class : ""));
            svg.innerHTML = '<circle class="sw-spinner-track" cx="12" cy="12" r="9"/><path class="sw-spinner-arc" d="M12 3a9 9 0 0 1 9 9"/>';
            if (o.Size) {
                const s = typeof o.Size === "number" ? o.Size + "px" : o.Size;
                svg.style.width = s;
                svg.style.height = s;
            }
            if (o.Title) {
                svg.setAttribute("role", "status");
                svg.setAttribute("aria-label", o.Title);
            }
            else
                svg.setAttribute("aria-hidden", "true");
            this.E = svg;
        }
    }

    // ------------------------------------------------------------------ copy button (UX guide INP-31)

    /**
     * A copy button for a value users paste elsewhere (an id, a key, a url, error details): a ghost icon button that copies exactly the
     * value and confirms in place (the icon becomes a check mark for 1.5 s, the tool tip says "Copied", the copy is announced), no toast.
     * On touch it is a 44 px target (the touch density). Options: Value (the text) or Get() (returns the text, or a promise of it, at click
     * time), What (what it copies, ex: "API key": the tool tip "Copy the API key", the message "API key copied"), Title (tool tip; default
     * from What, else 'Copy "value"'), Label (accessible name; give the item when there are several alike: "Copy the API key of
     * MailService"), Generic (never show the value: secrets), Small (default true), Kind (default "ghost"), Text (a visible label).
     * @param {object} o Options.
     * @returns {Button} The button (a Sw.Button: E, SetDisabled ...).
     */
    function CopyButton(o) {
        o = o || {};
        Sw.Work.CheckOptions("Sw.CopyButton", o, Schema.CopyButton);
        if (!o.Get && (o.Value === undefined || o.Value === null))
            Sw.DevWarn("Sw.CopyButton without Value or Get: nothing would be copied (pass Value, or Get() returning the text).", "copy-novalue");
        const val = () => o.Get ? o.Get() : o.Value;
        //  A secret (Secret: true, or a value that looks like one: an api key, a token) is never quoted in the tool tip or the message (SEC-1)
        const secret = !!o.Secret || (o.Value !== undefined && o.Value !== null && Sw.Clipboard.LooksSecret(String(o.Value)));
        const shown = o.Generic || secret || o.Value === undefined || o.Value === null ? null : String(o.Value);
        const title = o.Title || (o.What
            ? _T("Copy the {0}", o.What, "Tool tip of a copy button. {0} names what it copies, ex: 'API key', 'url'; written in lower case as part of the sentence")
            : shown !== null
                ? _T('Copy "{0}" to the clipboard', shown.length > 120 ? shown.substring(0, 117) + "..." : shown, "Tool tip of a copy button. {0} is the value it copies")
                : _TF("Copy to the clipboard", "Tool tip of a copy button whose value is not shown (ex: a secret)"));
        return new Button({
            Icon: "copy", Kind: o.Kind || "ghost", Small: o.Small !== false, Text: o.Text, Title: title, Label: o.Label, Class: o.Class, Copy: true,
            //  The copy confirms itself ("API key copied", Sw.Clipboard.Confirm): no second "<name>: done"
            Done: false, Leave: false,
            OnClick: b => {
                const v = val();
                const mo = { Button: b, What: o.What, Generic: o.Generic, Secret: secret || o.Secret };
                const copy = x => {
                    if (x === undefined || x === null || x === "") {
                        //  Nothing to copy: never overwrite the clipboard with "" and say "Copied"
                        Sw.DevWarn("Sw.CopyButton: the value to copy is empty.", "copy-empty");
                        if (Sw.Fail)
                            Sw.Fail(_TF("There is nothing to copy.", "Error when a copy button has no value to copy"));
                        return false;
                    }
                    return Sw.Clipboard.Copy(x, mo);
                };
                if (v && typeof v.then === "function")
                    return v.then(copy);
                copy(v);
            },
        });
    }

    // ------------------------------------------------------------------ common methods

    /** The elements that can take the focus inside a component. */
    const FocusSel = "input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex='-1'])";

    /**
     * The element of a component that takes the focus (and carries aria-invalid): the input, the button, the checked radio of a
     * Segmented, else E itself or the first focusable element in it.
     * @param {object} t The component.
     * @returns {Element|null}
     */
    function focusOf(t) {
        if (t.Input)
            return t.Input;
        if (t.Button instanceof Element)
            return t.Button;
        if (t.Buttons)
            return t.Buttons.find(b => b.tabIndex === 0 && !b.disabled) || t.Buttons.find(b => !b.disabled) || null;
        if (t.Control && !(t.Control instanceof Element) && t.Control !== t)
            return focusOf(t.Control);
        const e = t.Control instanceof Element ? t.Control : t.E;
        if (!(e instanceof Element))
            return null;
        return e.matches(FocusSel) ? e : e.querySelector(FocusSel);
    }

    // ------------------------------------------------------------------ locked controls (busy work: UX guide POP-6, FRM-5, FRM-8)

    /** Keys that change the value of a control that is not a text field (Tab, Escape and the modifiers pass). */
    const LockKeys = /^( |Spacebar|Enter|ArrowUp|ArrowDown|ArrowLeft|ArrowRight|Home|End|PageUp|PageDown|Delete|Backspace)$/;

    /**
     * @param {*} x An element.
     * @returns {boolean} True for a text field (typing, caret and selection).
     */
    function isTextField(x) {
        return !!x && (x.tagName === "TEXTAREA" || (x.tagName === "INPUT" && !/^(checkbox|radio|range|button|submit|reset|image|file|color|hidden)$/.test(x.type || "")));
    }

    /**
     * A change of a locked control was refused: the event is stopped and a bubbling "sw-locked" event says so (detail { Control, How });
     * a dialog shows its line for it (it calls preventDefault()), else the page announces why (once a second).
     * @param {object} t The control.
     * @param {string} how "key" | "wheel" | "pointer" | "input".
     * @param {Event} ev The event.
     */
    function lockRefuse(t, how, ev) {
        if (ev.cancelable)
            ev.preventDefault();
        ev.stopImmediatePropagation();
        const now = Date.now();
        if (now - (t.LockCue || 0) < 1000)
            return;
        t.LockCue = now;
        const ce = new CustomEvent("sw-locked", { bubbles: true, cancelable: true, detail: { Control: t, How: how } });
        if (!t.E.dispatchEvent(ce))
            return;
        const name = ctlName(t);
        //  Honest words: "while the work runs" only when work runs for it now (its lock's work, or work of its form / card)
        const running = !!t.LockWork || !!(Sw.Work && typeof Sw.Work.Running === "function" && Sw.Work.Running(t.E));
        if (Sw.Page && Sw.Page.Announce)
            Sw.Page.Announce(running
                ? (name
                    ? _T("{0} can't be changed while the work runs. You can change it again when it is done.", name, "Announced when the user tries to change a control that is locked while work (ex: a save) runs. {0} is its label, ex: 'Port'")
                    : _TF("This can't be changed while the work runs. You can change it again when it is done.", "Announced when the user tries to change a locked control while work runs"))
                : (name
                    ? _T("{0} can't be changed now.", name, "Announced when the user tries to change a control that the page has locked (no work runs). {0} is its label, ex: 'Port'")
                    : _TF("This can't be changed now.", "Announced when the user tries to change a control that the page has locked (no work runs)")));
    }

    /**
     * Tie a lock to work, so it always ends (`Locked(true); await save; Locked(false)` left the field locked for ever when
     * the save failed, and its message said work was running): the lock ends when the work settles, whatever the outcome. A bare
     * Locked(true) inside a click's work (or while work of its form / card runs) is tied to that work too: when the work FAILED the
     * control is unlocked again (with a DevWarn: use Sw.Lock or Locked(true, work)); after a success it stays locked as asked.
     * @param {object} t The control.
     * @param {?Promise} work The work (null: a bare lock; the click's / card's work is looked up).
     * @param {boolean} bare A bare Locked(true) (no work given).
     */
    function lockUntil(t, work, bare) {
        let p = work;
        if (!p && Sw.Work) {
            try {
                p = (typeof Sw.Work.InUser === "function" && Sw.Work.InUser() && typeof Sw.Work.Current === "function" ? Sw.Work.Current() : null)
                    || (typeof Sw.Work.Running === "function" ? Sw.Work.Running(t.E) : null);
            }
            catch {
                p = null;
            }
        }
        if (!p || typeof p.then !== "function")
            return;
        const id = (t.LockId = (t.LockId || 0) + 1);
        t.LockWork = p;
        const end = ok => {
            if (t.LockId !== id)
                return;
            t.LockWork = null;
            if (!bare) {
                setLocked(t, false);
                return;
            }
            //  (a bare lock: a failed / stopped work never leaves it locked; the dialog that locked it unlocks itself first)
            if (ok === false)
                setTimeout(() => {
                    if (t.LockId !== id || !t.IsLocked || t.Destroyed)
                        return;
                    setLocked(t, false);
                    Sw.DevWarn('"' + (ctlName(t) || "A control") + '" was locked with Locked(true) by work that failed: it is unlocked again (a lock must never outlive failed work). Use Sw.Lock(control, work) or Locked(true, work): they always unlock (UX guide FRM-8, INP-12).', "lock-failed:" + ctlName(t));
                }, 400);
        };
        Promise.resolve(p).then(v => end(v !== false), () => end(false));
    }

    /**
     * Install the lock guard of a control once (capture listeners on its root, so they run before its own handlers): while locked no
     * key, press, wheel, paste or drop changes its value; the focus, the caret, selection and copy stay (Tab, Escape pass).
     * @param {object} t The control.
     */
    function lockGuard(t) {
        if (t.LockGuard || !(t.E instanceof Element))
            return;
        t.LockGuard = true;
        const root = t.E;
        root.addEventListener("keydown", ev => {
            if (!t.IsLocked || ev.ctrlKey || ev.metaKey || ev.altKey || typeof ev.key !== "string")
                return;
            const x = ev.target;
            if (isTextField(x)) {
                //  (a number box steps with the arrows; typing is stopped by read only, keys that edit too for safety)
                if (((ev.key === "ArrowUp" || ev.key === "ArrowDown" || ev.key === "PageUp" || ev.key === "PageDown") && t.Steps) || ((ev.key.length === 1 || ev.key === "Backspace" || ev.key === "Delete") && !x.readOnly))
                    lockRefuse(t, "key", ev);
                return;
            }
            if (x && x.closest && x.closest(".sw-text-reveal"))
                return;
            if (LockKeys.test(ev.key) || ev.key.length === 1)
                lockRefuse(t, "key", ev);
        }, true);
        ["beforeinput", "paste", "drop", "cut"].forEach(n => root.addEventListener(n, ev => {
            if (t.IsLocked && (isTextField(ev.target) || n === "drop"))
                lockRefuse(t, "input", ev);
        }, true));
        ["pointerdown", "mousedown", "click", "dblclick"].forEach(n => root.addEventListener(n, ev => {
            if (!t.IsLocked)
                return;
            const x = ev.target;
            //  (a press in a text field keeps the caret / selection; the password reveal and a label only move the focus / view)
            if (isTextField(x) || (x && x.closest && x.closest(".sw-text-reveal, label, .sw-field-hint, .sw-text-error")))
                return;
            lockRefuse(t, "pointer", ev);
        }, true));
        //  (no Sw control changes its value by the wheel; a control that does sets t.WheelSteps)
        root.addEventListener("wheel", ev => {
            if (t.IsLocked && t.WheelSteps)
                lockRefuse(t, "wheel", ev);
        }, { capture: true, passive: false });
    }

    /**
     * Lock or unlock a control (the Locked method of every Sw control).
     * @param {object} t The control.
     * @param {boolean} on Locked.
     */
    function setLocked(t, on) {
        on = !!on;
        if (!!t.IsLocked === on)
            return;
        t.IsLocked = on;
        lockGuard(t);
        const root = t.E;
        if (root instanceof Element) {
            root.toggleAttribute("data-sw-locked", on);
            root.classList.toggle("sw-locked", on);
        }
        //  text fields: read only (FRM-8 look, typing / paste / drop / IME are refused by the browser); others: aria-readonly
        const i = t.Input;
        if (i && isTextField(i)) {
            if (on && !i.readOnly) {
                i.readOnly = true;
                i.SwLockedRO = true;
            }
            else if (!on && i.SwLockedRO) {
                i.SwLockedRO = false;
                i.readOnly = !!(t.Disabled || (t.O && t.O.ReadOnly));
            }
        }
        const f = i || focusOf(t);
        const ariaTarget = t.Buttons ? t.E : f;
        if (ariaTarget instanceof Element && !isTextField(ariaTarget) && /^(switch|slider|radiogroup|checkbox|spinbutton|combobox|listbox)$/.test(ariaTarget.getAttribute("role") || "")) {
            if (on)
                ariaTarget.setAttribute("aria-readonly", "true");
            else
                ariaTarget.removeAttribute("aria-readonly");
        }
        if (typeof t.UpdateClear === "function")
            t.UpdateClear();
        //  an open list / picker of the control closes (it would change the value)
        if (on && t.Popup && typeof t.Popup.Close === "function")
            try {
                t.Popup.Close();
            }
            catch {
                //  (closed already)
            }
    }

    /**
     * A neutral status line under a control ("Weekly digest: still saving…"), polite, not an error (no aria-invalid); null removes it.
     * @param {object} t The control.
     * @param {?string} text The text.
     */
    function ctlStatus(t, text) {
        if (!text) {
            if (t.StatusE)
                t.StatusE.hidden = true;
            return;
        }
        if (!t.StatusE) {
            t.StatusE = el("div.sw-ctl-status", { attrs: { "aria-live": "polite" } });
            const host = t.E.classList.contains("sw-field") || t.E.classList.contains("sw-text") ? t.E : t.E.closest(".sw-field");
            if (host)
                host.append(t.StatusE);
            else
                t.E.after(t.StatusE);
        }
        t.StatusE.textContent = text;
        t.StatusE.hidden = false;
    }

    /**
     * Keep a label's for= on its input when page code changes the input's id (FD R4 P1: `tb.Input.id = "pw"` cut the label off: no
     * accessible name, and Sw.Draft no longer saw the "Password" label).
     * @param {Element} input The input.
     * @param {HTMLLabelElement} lab The label.
     */
    function keepLabelFor(input, lab) {
        if (!input || !lab || typeof MutationObserver !== "function" || input.SwLabelMo)
            return;
        input.SwLabelMo = new MutationObserver(() => {
            if (!input.id)
                input.id = uid("sw-field");
            if (lab.htmlFor !== input.id)
                lab.htmlFor = input.id;
        });
        input.SwLabelMo.observe(input, { attributes: true, attributeFilter: ["id"] });
    }

    /**
     * The end of every control constructor: the developer check of its options (Sw.DevOptions: unknown / wrong type / colliding), and
     * for value controls E.SwControl (a dialog finds the controls in its body: their value, Locked).
     * @param {object} t The control.
     * @param {string} where The API name ("Sw.TextBox").
     * @param {object} schema The options schema.
     * @param {boolean} [valued] A value control (sets E.SwControl).
     */
    function ctlInit(t, where, schema, valued) {
        if (valued && t.E instanceof Element && !t.E.SwControl)
            t.E.SwControl = t;
        //  (the options as the page gave them: aliases are normalized after the check)
        const given = t.OGiven || t.O;
        t.OGiven = null;
        if (Sw.Work && typeof Sw.Work.CheckOptions === "function" && given)
            Sw.Work.CheckOptions(where, given, schema);
    }

    /**
     * The common methods of every component (ui/README.md "Components: common methods"), added where a class has no own version, so
     * code can treat the controls alike: Focus(select), SetLabel(text), SetError(text), Validate(), Destroy().
     */
    const Common = {
        /**
         * Lock or unlock the control while work runs (a dialog's save: UX guide POP-6, FRM-5): no change of the value by keyboard,
         * pointer, wheel, paste or drop (a number box's arrows too); the focus, caret, selection and copy stay; a refused change
         * fires "sw-locked" (a dialog shows its line; else it is announced). Values set by code still apply. Locked() reads the state.
         * Locked(true, work) (a promise or a function that returns one) locks until the work settles, whatever its outcome: the scoped
         * form, like Sw.Lock(control, work). A bare Locked(true) inside a click's work is undone when that work fails.
         * @param {boolean} [on] Locked.
         * @param {Promise|function} [work] Locked until this work settles (then unlocked on any outcome).
         * @returns {boolean} The state.
         */
        Locked(on, work) {
            const t = this;
            if (on === undefined)
                return t instanceof Field && t.Control && typeof t.Control.Locked === "function" ? !!t.Control.Locked() : !!t.IsLocked;
            if (typeof work === "function") {
                try {
                    work = work();
                }
                catch (x) {
                    work = Promise.reject(x);
                }
            }
            if (work && typeof work.then === "function" && typeof work.catch === "function")
                work.catch(() => { });
            //  (a field locks the control it holds)
            if (t instanceof Field && t.Control && typeof t.Control.Locked === "function") {
                t.Control.Locked(on, on ? work : undefined);
                t.IsLocked = !!on;
                return t.IsLocked;
            }
            t.LockId = (t.LockId || 0) + 1;
            t.LockWork = null;
            setLocked(t, on);
            if (on)
                lockUntil(t, work && typeof work.then === "function" ? work : null, !(work && typeof work.then === "function"));
            return !!t.IsLocked;
        },
        /**
         * A component is not text (FD R4 F16: `div.append(card)` showed "[object Object]"): an empty text and a developer warning
         * that says to append its element.
         * @returns {string} "".
         */
        toString() {
            const n = this && this.constructor && this.constructor.name ? this.constructor.name : "component";
            Sw.DevWarn("A Sw." + n + " was turned into text (native append / innerHTML / a template string): it shows nothing. Append its element (x.E), or pass the component to Sw.el / a Sw container (they take components).", "comp-text:" + n);
            return "";
        },
        /**
         * Move the keyboard focus to the control.
         * @param {boolean} [select] Also select the text (text inputs).
         */
        Focus(select) {
            const f = focusOf(this);
            if (!f)
                return;
            f.focus();
            if (select && typeof f.select === "function")
                f.select();
        },
        /**
         * Change the label: the visible label when the control has one (the required marker stays), else the accessible name.
         * @param {string} text The label.
         */
        SetLabel(text) {
            const t = this;
            if (t.LabelE) {
                setLabelText(t.LabelE, text);
                return;
            }
            //  a radio group / progress bar is named on its root, an input / button on itself
            const target = t.Buttons || !focusOf(t) ? t.E : focusOf(t);
            if (!(target instanceof Element))
                return;
            if (text)
                target.setAttribute("aria-label", text);
            else
                target.removeAttribute("aria-label");
        },
        /**
         * Show (or clear) a validation message under the control, linked by aria-describedby, with aria-invalid.
         * @param {string|null} text The message, null clears it.
         */
        SetError(text) {
            const t = this;
            const target = t.Buttons ? t.E : (focusOf(t) || t.E);
            if (target instanceof Element)
                showError(t, target, text);
        },
        /**
         * Check now (Required: an empty value, a switch that is off; the Validate(value) option) and show the message.
         * @returns {string|null} The message, null when valid (Sw.Form calls it).
         */
        Validate() {
            const t = this;
            const o = t.O || {};
            const v = typeof t.Get === "function" ? t.Get() : undefined;
            const empty = v === undefined || v === null || v === "" || v === false || (Array.isArray(v) && !v.length);
            let err = null;
            if (o.Required && empty)
                err = typeof o.Required === "string" ? o.Required : (t.EmptyMessage ? t.EmptyMessage() : _TF("Choose a value", "Message under an empty drop down that must be filled in"));
            else if (typeof o.Validate === "function")
                err = o.Validate(v) || null;
            t.SetError(err);
            return err;
        },
        /**
         * Remove the control from the page: closes its popup, removes its element and its message; the object is not used again.
         */
        Destroy() {
            const t = this;
            if (t.Destroyed)
                return;
            t.Destroyed = true;
            try {
                if (t.Popup && typeof t.Popup.Close === "function")
                    t.Popup.Close();
            }
            catch {
                //  the popup was closed already
            }
            if (t.ErrorE)
                (t.ErrorE.parentElement && t.ErrorE.parentElement.classList.contains("sw-text-error-slot") ? t.ErrorE.parentElement : t.ErrorE).remove();
            if (t.E instanceof Element)
                t.E.remove();
        },
    };

    /** The components that get the common methods: controls all of them (a value control also SetError / Validate), displays fewer. */
    const CommonOf = [
        [[Button, Segmented, Toggle, Slider, NumberBox, Field, TextBox, Select, ColorPicker, Chip], ["Focus", "SetLabel", "SetError", "Validate", "Destroy", "Locked", "toString"]],
        [[ProgressBar], ["SetLabel", "Destroy", "toString"]],
        [[Group, Spinner], ["Destroy", "toString"]],
    ];
    /**
     * Select a value as the user did (a click, a key, a pick from the list): Set without OnChange, then OnChange in a click scope, then
     * the sw-change event and the page dirty state (an OnChange that returns nothing applies the value at once: not dirty; one that
     * returns a promise or starts a request saves it: dirty until that succeeds).
     * @param {*} v The value.
     */
    DirtyMethods.Pick = function (v) {
        const t = this;
        if (sameValue(v, t.Get()))
            return;
        //  (a pick from a list that sits outside the control, ex: a Select's menu, while it is locked: refused like a key)
        if (t.IsLocked) {
            lockRefuse(t, "pointer", new Event("sw-pick", { cancelable: true }));
            return;
        }
        t.InUser = true;
        try {
            t.Set(v, true);
        }
        finally {
            t.InUser = false;
        }
        const args = t.ChangeArgs ? t.ChangeArgs(v) : [t.Get()];
        const sc = t.O.OnChange ? callChange(t, t.O.OnChange, args) : null;
        userEdit(t, t.Get(), sc ? (savesItself(sc) ? sc : "applied") : null);
    };
    Segmented.prototype.ChangeArgs = function (v) {
        return [v, this.Items.find(it => it.Value === v)];
    };
    for (const cls of [Segmented, Toggle, Select, TextBox, NumberBox])
        for (const name of Object.keys(DirtyMethods))
            if (typeof cls.prototype[name] !== "function")
                Object.defineProperty(cls.prototype, name, { value: DirtyMethods[name], writable: true, configurable: true });
    for (const [classes, names] of CommonOf)
        for (const cls of classes) {
            const valued = typeof cls.prototype.Get === "function";
            for (const name of names) {
                if (Object.prototype.hasOwnProperty.call(cls.prototype, name))
                    continue;
                if (!valued && (name === "SetError" || name === "Validate"))
                    continue;
                Object.defineProperty(cls.prototype, name, { value: Common[name], writable: true, configurable: true });
            }
        }

    /**
     * Lock controls while work runs, the scoped way (FRM-8, INP-12): Sw.Lock(targets, work) locks every target (a control, an
     * element with E.SwControl, a container: every Sw control inside it, or an array of these) that is not locked already, runs / waits
     * for the work (a function, called at once, or a promise) and unlocks the same controls when it settles, whatever the outcome; a
     * failure still rejects for the caller. The right use is the only use: no try / finally to forget.
     * Example: OnClick: () => Sw.Lock(port, Sw.Net.Send(api + "TestPort", { Port: port.Get() }))
     * @param {object|Element|Array<object|Element>} targets The controls.
     * @param {function|Promise} work The work.
     * @returns {Promise<*>} The work's result.
     */
    function Lock(targets, work) {
        const list = [];
        const add = x => {
            if (!x)
                return;
            if (Array.isArray(x)) {
                x.forEach(add);
                return;
            }
            if (typeof x.Locked === "function") {
                list.push(x);
                return;
            }
            const e = x.E instanceof Element ? x.E : x;
            if (!(e instanceof Element))
                return;
            if (e.SwControl && typeof e.SwControl.Locked === "function")
                list.push(e.SwControl);
            else
                e.querySelectorAll("*").forEach(c => {
                    if (c.SwControl && typeof c.SwControl.Locked === "function" && !list.includes(c.SwControl))
                        list.push(c.SwControl);
                });
        };
        add(targets);
        let p;
        try {
            p = Promise.resolve(typeof work === "function" ? work() : work);
        }
        catch (x) {
            p = Promise.reject(x);
        }
        if (!list.length)
            Sw.DevWarn("Sw.Lock found no Sw control to lock (pass a control, its element or a container of controls).", "lock-none");
        p.catch(() => { });
        for (const c of list)
            if (!c.Locked())
                c.Locked(true, p);
        return p;
    }

    Object.assign(Sw, {
        Lock,
        Button,
        ButtonRow,
        Group,
        Segmented,
        Toggle,
        Slider,
        NumberBox,
        Field,
        TextBox,
        Select,
        ListPicker,
        ColorPicker,
        Badge,
        Chip,
        ProgressBar,
        Spinner,
        CopyButton,
        Uid: uid,
        /**
         * The page dirty state of the value controls (ui/README "Unsaved edits of the controls"): Saved(el) = a save by el (a button,
         * a form's submit) succeeded: the edited controls of its card / form are clean; Saving(el) -> End(ok) = a save by el runs (its
         * groups don't mark the page meanwhile); IsDirty() = any control edit is unsaved. Forms: the live Sw.Form objects (page.js adds
         * them): a button anywhere whose work sent all of a form's edited values saved it (form.SentBy(text)). Who changed it:
         * UserSince(root, since) = the user changed a field / control inside root after since (a performance.now() time; trusted input
         * / change events and Sw control user changes; code writes never count), UserAt(root) = the time of the user's last change
         * there (0 = never). SlowMs / GiveUpMs: an auto save that has not answered says "still saving…" / is taken as failed.
         */
        Edits: Object.assign(Edits, {
            Saved: savedBy,
            Saving: savingBy,
            IsDirty: () => Array.from(dirtyGroups.values()).some(g => g.C.size > 0),
            Forms: new Set(),
            Carries: carries,
            UserAt: userAt,
            UserSince: (root, since) => userAt(root) > (+since || 0),
            NoteUser: noteUser,
        }),
    });
    //  (the scoped lock is work: Sw.Work.Lock is the same function)
    if (Sw.Work && !Sw.Work.Lock)
        Sw.Work.Lock = Lock;
})();
