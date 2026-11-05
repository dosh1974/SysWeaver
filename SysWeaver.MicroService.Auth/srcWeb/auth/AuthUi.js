/**
 * @file AuthUi.js - the auth page framework and form widgets on the Sw UI library, plus the Login / Logout pages
 * (SysWeaver.MicroService.Auth, served as auth/AuthUi.js).
 *
 * Purpose: `Sw.AuthUi`, used by every page of the four projects that contribute to the auth/ folder
 * (MicroService.Auth: Login, Logout; Auth.Core: password hash generator, API keys; MicroService.PassKey: passkey pages;
 * MicroService.UserManager: account pages):
 *  - Page(icon, build, opts): the centered card (a real <form>, so Enter submits and password managers recognize it),
 *    the page icon, loading protocol (Sw.Page.Loading / Loaded exactly once, also on errors), Sw.Session.IgnoreUserChanges.
 *    Two Design v2 layouts (Design v2, ui/README.md Page anatomy / PORTING.md B10): sign in style pages (Login, Logout, SignUp, passkey sign in ...) keep the
 *    centered 28rem card with the page icon; account and tool pages (opts.Account inside the app shell, or page.SetHeader) become a
 *    form page: a left aligned page header (Sw.PageHeader: title = the menu name or the document title, one line description,
 *    actions) above a start aligned card (class auth-tool, Auth.css).
 *  - Field / PhoneField: labelled inputs (Sw.TextBox) with autocomplete, inputmode, show / hide password, copy and
 *    "generate a password" buttons, invalid state; Enter moves to the next empty field or submits.
 *  - Status (a live status line with kinds ok / error / warning / info, temporary texts, leak check), Text, Heading,
 *    Buttons / Button, Segments (accessible tab like selector), PolicyButton, ShortCode (the code step of the
 *    UserManager pages), UserHtml.
 *  - LoadPassKeys(): load the optional passkey plug-in on a page without a static tag only when the passkey service exists
 *    (Api/HaveService, memoized).
 *  - CheckLinkToken(kind, { Page }): a page opened from a link (e-mail / text message) checks its token on load without using it
 *    (Api/auth/CheckActionToken); a dead link shows LinkGone ("This link no longer works", the real cause, what to do) before
 *    the user acts. IsLinkGone(e): a 410 of a link action (the link died meanwhile).
 *  - Done(page, { Title, Text, Next }): end an account page with its result and where to go next (never a dead end).
 *  - Hide(result, ...parts): hide used parts of a page (a button row, its note) and move a focus that was in them to the result.
 *  - LoginMain / LogoutMain (globals loginMain / logoutMain for auth/Login.html and auth/Logout.html); the login page has a
 *    product header (Brand: the app banner banner.svg, its icon and name), an "already signed in" view (asked only when
 *    Sw.Auth.MaybeSignedIn: an anonymous visit sends the original requests), inline failure reasons, a password rules checklist
 *    (a friendly reminder; all unmet rules are listed next to Sign in, which stays focusable (aria-disabled) and explains
 *    itself when activated; hints are announced politely once typing pauses) and a quiet leak hint.
 *
 * Loading: classic `defer` script after ../sw/core.js, ../sw/ui.js and Auth.js. The optional passkey plug-in (Passkey.js /
 * Passkey.css) is linked statically by Login.html (and the UserManager ChoosePassword / ResetPassword), as the original pages
 * (a 404 when the service does not run); other pages load it on demand by LoadPassKeys.
 *
 * Globals: `Sw.AuthUi`, loginMain, logoutMain and the legacy names of the old AuthUi.js (AuthPage, CreateAuthWrapper,
 * AuthPageValidation, AuthUserHtml, AuthText, AuthLabel, AuthError, AuthTab, AuthInput, AuthPassword, AuthNewPassword,
 * AuthInputPhone, AuthAddIcon, AuthButtonRow, AuthButton, AuthHr, AuthBr, AuthSetText, AuthSetError, IsAutoFilled)
 * mapped onto Sw.AuthUi for code in other projects. New code uses `Sw.AuthUi`.
 *
 * Server: Api/auth/* through Sw.Auth (Auth.js); ../iso_data/country/<ISO>.svg flags.
 * Styles: Auth.css (classes auth-*; the box also keeps the old class SysWeaver-AuthBox).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Auth || !Sw.Button)
        throw new Error("auth/AuthUi.js requires sw/core.js, sw/ui.js and auth/Auth.js");
    const el = Sw.el;
    const A = Sw.Auth;

    // ------------------------------------------------------------------ page

    /**
     * Test if an icon is an image url (contains '.' or '/') rather than an icon class.
     * @param {string} icon Icon class or url.
     * @returns {boolean}
     */
    const isUrl = icon => (icon.indexOf(".") > 0) || (icon.indexOf("/") > 0);

    /**
     * An auth page: the centered card with the page icon and a form.
     * Properties: E (main element), Box (the form, class auth-box SysWeaver-AuthBox), IconE, Fields (registered fields),
     * OnSubmit (async function run when the form is submitted), CanSubmit (optional async check before OnSubmit), Running.
     */
    class AuthPage {
        /**
         * @param {string} [icon] Icon class ("IconAuthLogin") or image url.
         * @param {boolean} [iconIsFile] Force treating the icon as an image url.
         */
        constructor(icon, iconIsFile) {
            const t = this;
            t.Fields = [];
            t.OnSubmit = null;
            t.CanSubmit = null;
            t.Running = false;
            t.Box = el("form.auth-box.SysWeaver-AuthBox", { attrs: { novalidate: "", autocomplete: "on" } });
            //  Lets legacy code (AuthPageValidation) find the page of a box
            t.Box.AuthPage = t;
            t.IconWrap = el("div.auth-icon-wrap");
            t.Box.append(t.IconWrap);
            t.E = el("main.auth-page", null, t.Box);
            t.IconE = null;
            if (icon)
                t.SetIcon(icon, iconIsFile);
            t.Box.addEventListener("submit", e => {
                e.preventDefault();
                t.Submit();
            });
            t.Box.addEventListener("keydown", e => t.OnKey(e));
            document.body.append(t.E);
        }

        /**
         * Change the page icon.
         * @param {string} icon Icon class or image url.
         * @param {boolean} [isFile] Force an image url.
         */
        SetIcon(icon, isFile) {
            const t = this;
            const img = isFile || isUrl(icon);
            t.IconWrap.replaceChildren();
            t.IconE = img
                ? el("img.auth-icon.auth-image", { src: icon, alt: "", draggable: false })
                : Sw.IconClass(icon, { Class: "auth-icon" });
            t.IconWrap.append(t.IconE);
            t.IconWrap.classList.toggle("auth-has-image", !!img);
            //  Form page layout: the header names the page, so only an image (ex: a QR code) is shown, not the big page icon
            if (t.Header)
                t.IconWrap.hidden = !img;
        }

        /**
         * Turn the page into a Design v2 form page (Design v2, PORTING.md B10): main.auth-page gets sw-page-form + auth-tool, a left aligned
         * page header (Sw.PageHeader) is put above the card and the big page icon is hidden (an image icon such as a QR code stays).
         * Calling it again updates the header.
         * @param {{Title: string, Description: string, Actions: Array<*>, MenuTitle: boolean}} [o] Header options (Title default: the
         * document title; inside the app shell a page that is a main menu item shows the menu name, Sw.MenuTitle).
         * @returns {object} The header (Sw.PageHeader).
         */
        SetHeader(o) {
            const t = this;
            o = o || {};
            if (t.Header) {
                if (o.Title)
                    t.Header.SetTitle(o.Title);
                if (o.Description !== undefined)
                    t.Header.SetDescription(o.Description);
                if (o.Actions)
                    t.Header.SetActions(o.Actions);
                return t.Header;
            }
            t.E.classList.add("sw-page-form", "auth-tool");
            t.Header = Sw.PageHeader({ Title: o.Title || document.title, Description: o.Description || "", Actions: o.Actions || [], MenuTitle: o.MenuTitle });
            t.E.prepend(t.Header.E);
            t.IconWrap.hidden = !t.IconWrap.classList.contains("auth-has-image");
            return t.Header;
        }

        /** Remove everything except the icon (to show the next step). */
        Clear() {
            const b = this.Box;
            while (b.lastElementChild && b.lastElementChild !== this.IconWrap)
                b.lastElementChild.remove();
            this.Fields = [];
            this.OnSubmit = null;
            this.CanSubmit = null;
        }

        /**
         * Enter in a field: move to the next empty enabled field of the form instead of submitting.
         * @param {KeyboardEvent} e The event.
         */
        OnKey(e) {
            if (e.key !== "Enter" || !Sw.IsPureEvent(e) || e.isComposing)
                return;
            const inp = e.target;
            if (!(inp instanceof HTMLInputElement))
                return;
            const list = this.Fields.map(f => f.Input).filter(i => i.isConnected && !i.disabled && !i.readOnly && i.offsetParent !== null);
            const i = list.indexOf(inp);
            if (i < 0)
                return;
            //  EnterWalks (a page that set it, ex: Invite user; or a field that set it, ex: a phone number): as the old page
            //  (tabToNextOnEnter), Enter goes to the next field and from the last one to the main button; it never submits from a
            //  field (Enter on the button does)
            if (this.EnterWalks || this.Fields.some(f => f.Input === inp && f.EnterWalks)) {
                e.preventDefault();
                const n = list[i + 1] || this.MainButton?.E;
                n?.focus();
                return;
            }
            const next = list.slice(i + 1).find(x => !x.value);
            if (next) {
                e.preventDefault();
                next.focus();
            }
        }

        /**
         * Submit the form (runs OnSubmit through Run, once at a time). When CanSubmit is set it is awaited first and a
         * false result stops the submit (the form isn't shown busy: CanSubmit explains why, ex: focus and announce a hint).
         */
        async Submit() {
            const t = this;
            if (!t.OnSubmit || t.Running)
                return;
            if (t.CanSubmit && !(await t.CanSubmit()))
                return;
            if (t.Running)
                return;
            await t.Run(t.OnSubmit, t.MainButton);
        }

        /**
         * Run an action with the form busy: fields read only, the button working, other actions blocked.
         * @param {function():Promise<*>} fn The action.
         * @param {Sw.Button} [button] The button to show working.
         * @returns {Promise<*>} The result of fn (undefined on an exception, which is shown as a fail toast).
         */
        async Run(fn, button) {
            const t = this;
            if (t.Running)
                return undefined;
            t.SetBusy(true);
            button?.SetWorking(true);
            try {
                return await fn();
            }
            catch (e) {
                console.error(e);
                Sw.Fail(e && e.message ? e.message : String(e));
                return undefined;
            }
            finally {
                button?.SetWorking(false);
                t.SetBusy(false);
            }
        }

        /**
         * Mark the form busy (fields read only, class auth-busy).
         * @param {boolean} on Busy.
         */
        SetBusy(on) {
            const t = this;
            t.Running = !!on;
            t.Box.classList.toggle("auth-busy", t.Running);
            t.Box.setAttribute("aria-busy", t.Running ? "true" : "false");
            t.Fields.forEach(f => f.SetReadOnly(t.Running));
        }

        /**
         * Focus an element or field; while the page is loading (the loader keeps the focus) it is focused when loaded.
         * @param {HTMLElement|{Focus:function(boolean):void}} target The element or field.
         * @param {boolean} [select] Select the text (fields).
         */
        Focus(target, select) {
            if (!target)
                return;
            const f = () => target.Focus ? target.Focus(select) : target.focus();
            if (this.LoadRemover) {
                this.PendingFocus = f;
                this.PendingTarget = target;
            }
            else
                f();
        }

        /**
         * Signal that the page is ready (loader removed, PageLoaded posted, pending focus applied); only the first call counts.
         * The focus asked for while loading is the page's initial focus (UX guide FOC-2): on touch (and in the phone layout) a text field
         * is not focused, since that opens the on-screen keyboard unasked; the page heading gets the focus instead.
         */
        Loaded() {
            const r = this.LoadRemover;
            this.LoadRemover = null;
            if (r)
                r();
            const f = this.PendingFocus;
            const target = this.PendingTarget;
            this.PendingFocus = null;
            this.PendingTarget = null;
            if (!f)
                return;
            const input = target && (target.Input || target);
            const isText = input instanceof HTMLTextAreaElement || (input instanceof HTMLInputElement && !/^(button|submit|reset|checkbox|radio|range|color|file|image|hidden)$/.test(input.type));
            if (isText && Sw.Page && Sw.Page.InitialFocus) {
                Sw.Page.InitialFocus(input);
                return;
            }
            if (isText && window.matchMedia && window.matchMedia("(pointer: coarse)").matches) {
                const h = this.E.querySelector("h1") || document.querySelector("h1");
                if (h) {
                    if (!h.hasAttribute("tabindex"))
                        h.setAttribute("tabindex", "-1");
                    h.focus({ preventScroll: true });
                }
                return;
            }
            //  A focus refused while the page is still hidden (core/theme.js, first visit of a page that links app/app.css) is given
            //  again by core/theme.js when the page shows
            f();
        }

        /**
         * Show the card of a sign in style page (no page header) as an Sw.Dialog: the same dialog as the rest of the application (title
         * bar with the document title, its menu, moved and resized like any dialog, its position remembered, "Discard changes?" for
         * typed input), the form moved into its body unchanged. Close (×, Escape) leaves the page (LeavePage: back, else the start
         * page). Phones (a narrow window or a coarse pointer) and pages with a page header keep the plain card; called once (Page).
         */
        AsDialog() {
            const t = this;
            if (t.Dialog || t.Header || !Sw.Dialog || typeof Sw.Dialog.Open !== "function" || !t.Box.isConnected)
                return;
            let phone = !!(Sw.Layout && Sw.Layout.Mobile);
            try {
                phone = phone || window.matchMedia("(max-width: 640px), (pointer: coarse)").matches;
            }
            catch {
            }
            if (phone)
                return;
            t.Leaving = false;
            t.Dialog = Sw.Dialog.Open({
                Title: document.title || _TF("Account", "Title of the dialog of an account page without a document title"),
                Content: t.Box,
                Id: "auth:" + location.pathname.toLowerCase(),
                Modal: true,
                Wide: t.E.classList.contains("auth-wide") || document.body.classList.contains("auth-wide"),
                Class: "auth-dialog",
                OnClose: () => {
                    if (!t.Leaving)
                        LeavePage();
                },
            });
            t.E.classList.add("auth-in-dialog");
            document.body.classList.add("auth-dialog-page");
        }
    }

    /**
     * Build an auth page: ignores user change reloads (the page's own sign in / out must not reload it), shows the
     * loader, creates the card and awaits build(page); the loader is removed (and PageLoaded posted) exactly once,
     * also when build throws (the error is shown as a fail toast).
     * @param {string} icon Icon class or image url (falsy = none).
     * @param {function(AuthPage):Promise<void>} build Builds the content.
     * @param {object} [o] Options: { IconIsFile, KeepUserChanges (don't call IgnoreUserChanges), Class (extra class on the page),
     * Account (an account page of a signed in user: inside the app shell it is a form page with a page header, see SetHeader; stand
     * alone, ex: opened from an e-mail link, it keeps the centered card), Description (the one line description of that header),
     * Dialog (default true: a centered card page, one without a page header, is shown as an Sw.Dialog, movable and resizable like every
     * other dialog, its Close and Escape go back, see AuthPage.AsDialog; false keeps the plain card, ex: the Login and Logout pages;
     * phones always keep the full width card) }.
     * @returns {Promise<AuthPage>} The page.
     */
    async function Page(icon, build, o) {
        o = o || {};
        if (!o.KeepUserChanges)
            Sw.Session.IgnoreUserChanges();
        if (document.readyState === "loading")
            await new Promise(r => document.addEventListener("DOMContentLoaded", r, { once: true }));
        document.body.classList.add("sw-root", "auth-body");
        const remover = Sw.Page.Loading();
        const page = new AuthPage(icon, o.IconIsFile);
        page.LoadRemover = remover;
        if (o.Class)
            page.E.classList.add(o.Class);
        if (o.Account && Sw.Page.IsEmbedded && typeof Sw.PageHeader === "function")
            page.SetHeader({ Description: o.Description });
        try {
            if (build)
                await build(page);
        }
        catch (e) {
            console.error(e);
            Sw.Fail(e && e.message ? e.message : String(e));
        }
        finally {
            if (o.Dialog !== false)
                page.AsDialog();
            page.Loaded();
        }
        return page;
    }

    /**
     * Leave an auth page shown as a dialog (its Close / Escape): back to the previous page of this site (in the app shell: the shell's
     * previous page when it has one, else its first page), else the start page of the site (ex: a page opened from an e-mail link).
     */
    function LeavePage() {
        try {
            const top = window.top;
            const shell = top !== window && top.Sw && top.Sw.Shell && top.Sw.Shell.Current;
            if (shell) {
                if (typeof shell.CanGoBack === "function" && shell.CanGoBack())
                    top.history.back();
                else if (typeof shell.OpenFirst === "function")
                    shell.OpenFirst();
                return;
            }
        }
        catch {
            //  (a page of another origin above this one: leave as a page of its own)
        }
        let sameSite = false;
        try {
            sameSite = !!document.referrer && new URL(document.referrer).origin === location.origin;
        }
        catch {
        }
        if (sameSite && history.length > 1)
            history.back();
        else
            Sw.Page.Navigate(Sw.Url.Root, "_self");
    }

    // ------------------------------------------------------------------ simple elements

    /**
     * Append a block of text (click copies it).
     * @param {HTMLElement|AuthPage} target Parent (or a page: its box).
     * @param {string} text The text (or html with o.Html).
     * @param {object} [o] Options: { Html (text is html), Title, Class, NoCopy }.
     * @returns {HTMLParagraphElement} The element (class auth-text).
     */
    function Text(target, text, o) {
        o = o || {};
        const e = el("p.auth-text", { title: o.Title || null });
        if (o.Class)
            e.classList.add(...o.Class.split(" "));
        SetContent(e, text, o.Html);
        if (!o.NoCopy)
            Sw.Clipboard.CopyOnClick(e);
        box(target).append(e);
        return e;
    }

    /**
     * Append a heading (the page title / instruction).
     * @param {HTMLElement|AuthPage} target Parent.
     * @param {string} text The text (or html with isHtml).
     * @param {string} [title] Tooltip.
     * @param {boolean} [isHtml] Html content.
     * @returns {HTMLHeadingElement} The element (class auth-heading).
     */
    function Heading(target, text, title, isHtml) {
        const e = el("h1.auth-heading", { title: title || null });
        SetContent(e, text, isHtml);
        box(target).append(e);
        return e;
    }

    /**
     * Set the text (or html) of an element.
     * @param {HTMLElement} e The element.
     * @param {string} text Text.
     * @param {boolean} [isHtml] Html.
     */
    function SetContent(e, text, isHtml) {
        if (isHtml)
            e.innerHTML = text || "";
        else
            e.textContent = text || "";
    }

    /**
     * The reason of a button that is disabled while the form's request runs, or after it was done (the page moves on).
     * @returns {string} The text.
     */
    const waitReason = () => _TF("Wait until the current step is done", "Reason of a disabled button of an account or sign in page while its request runs");

    /**
     * The element to append to.
     * @param {HTMLElement|AuthPage|null} target Element or page (null: a detached widget, nothing is appended).
     * @returns {HTMLElement}
     */
    function box(target) {
        if (!target)
            return { append: () => { }, appendChild: x => x };
        return target instanceof AuthPage ? target.Box : target;
    }

    /**
     * Append a centered button row.
     * @param {HTMLElement|AuthPage} target Parent.
     * @returns {HTMLDivElement} The row (classes sw-row auth-buttons).
     */
    function Buttons(target) {
        const r = Sw.ButtonRow();
        r.classList.add("auth-buttons");
        box(target).append(r);
        return r;
    }

    /**
     * Append a button.
     * @param {HTMLElement} row The row.
     * @param {object} o Sw.Button options plus { Submit (a submit button: the page's main button, primary), Hidden }.
     * @param {AuthPage} [page] With Submit: the page whose MainButton it becomes.
     * @returns {Sw.Button} The button.
     */
    function Button(row, o, page) {
        //  (Submit / Hidden are this helper's own options: Sw.Button gets only its own, so its option check stays quiet)
        const opts = {};
        for (const k of Object.keys(o))
            if (k !== "Submit" && k !== "Hidden" && o[k] !== undefined)
                opts[k] = o[k];
        const b = new Sw.Button(Object.assign({ Kind: o.Submit ? "primary" : undefined, Type: o.Submit ? "submit" : "button" }, opts));
        if (o.Hidden)
            b.E.hidden = true;
        if (o.Submit && page)
            page.MainButton = b;
        row.append(b.E);
        return b;
    }

    /**
     * Join password rules into one compact phrase in the page language ("at least 8 characters, an uppercase letter and
     * a digit"): a capital first letter of a rule is lowered when followed by a lowercase letter or a space (not acronyms).
     * @param {string[]} list The rule texts (A.PolicyRules, translated).
     * @returns {string} The phrase.
     */
    function JoinRules(list) {
        const a = list.map(s => /^\p{Lu}(\p{Ll}|\s)/u.test(s) ? s.charAt(0).toLocaleLowerCase() + s.substring(1) : s);
        try {
            return new Intl.ListFormat(document.documentElement.lang || navigator.language || "en", { style: "long", type: "conjunction" }).format(a);
        }
        catch {
            return a.join(", ");
        }
    }

    /**
     * The hint next to a blocked Sign in: what the password still needs.
     * @param {string[]} missing The unmet rule texts (A.PolicyRules).
     * @param {string} err The first failing rule (A.CheckPolicy), used when no rule text is missing.
     * @returns {string} The hint.
     */
    function NeedsText(missing, err) {
        return _T("To sign in, your password needs: {0}", missing.length ? JoinRules(missing) : err, "Hint next to a blocked sign in button: the entered password breaks rules of the user's password policy.{0} is replaced by all unmet rules as a list (ex: at least 8 characters, an uppercase letter and a digit)");
    }

    /**
     * The items of a password rule checklist (class auth-rules): a mark (✓ met, • not yet) and the rule, unmet rules highlighted
     * once a password is typed. Only the mark and the colour change while typing (LAY-9: no item gets wider or taller).
     * @param {Array<{Text:string, Met:boolean}>} list The rules (A.PolicyRules).
     * @param {boolean} typed True when a password is entered.
     * @returns {HTMLElement[]} The li elements.
     */
    function RuleItems(list, typed) {
        return (list || []).map(r => el("li.auth-rule" + (r.Met ? ".auth-rule-met" : typed ? ".auth-rule-missing" : ""), null,
            el("span.auth-rule-mark", { text: r.Met ? "✓" : "•", attrs: { "aria-hidden": "true" } }),
            el("span", { text: r.Text }),
            el("span.sw-sr-only", {
                text: r.Met
                    ? _TF("(met)", "Screen reader text after a password rule in a checklist that the entered password meets")
                    : _TF("(not met)", "Screen reader text after a password rule in a checklist that the entered password doesn't meet"),
            })));
    }

    /**
     * Format user ids / e-mail addresses as emphasized, html escaped text.
     * @param {string|string[]} list One value or a list.
     * @returns {string} Html ("<em>a</em>, <em>b</em>").
     */
    function UserHtml(list) {
        const a = typeof list === "string" ? [list] : (list || []);
        return a.map(v => "<em>" + Sw.Format.HtmlSafe(String(v).trim()) + "</em>").join(", ");
    }

    // ------------------------------------------------------------------ status line

    /** Status kinds and their classes. */
    const Kinds = ["ok", "error", "warning", "info"];

    /** Unique id counter (aria-describedby targets). */
    let idSeq = 0;

    /**
     * Give an element an id (kept when it has one).
     * @param {HTMLElement} e The element.
     * @returns {string} The id.
     */
    function ensureId(e) {
        if (!e.id)
            e.id = "auth-id-" + (++idSeq);
        return e.id;
    }

    /**
     * Hide parts of a page that are done (a used button, its explanation) without dropping the focus: when the focus is in one of them
     * it moves to the result (Sw.Focus.Show; UX guide FOC-1 / FOC-2), never to the page body.
     * @param {HTMLElement|{E: HTMLElement}|null} result Where the focus goes (ex: the status line with the result).
     * @param {...(HTMLElement|{E: HTMLElement}|null)} parts The elements (or components) to hide; null / undefined are skipped.
     */
    function Hide(result, ...parts) {
        const a = document.activeElement;
        let had = false;
        for (const p of parts) {
            const e = p && p.E instanceof Element ? p.E : p;
            if (!(e instanceof Element))
                continue;
            had = had || (!!a && e.contains(a));
            e.hidden = true;
        }
        const r = result && result.E instanceof Element ? result.E : result;
        if (had && r instanceof Element)
            Sw.Focus.Show(r);
    }

    /**
     * Remove a copy on click handler set by Sw.Clipboard.CopyOnClick.
     * @param {HTMLElement} e The element.
     */
    function removeCopy(e) {
        if (e.SwCopy) {
            e.removeEventListener("click", e.SwCopy);
            e.SwCopy = null;
        }
        e.classList.remove("sw-copyable");
    }

    /**
     * A live status line (role status): validation errors, progress and results. Hints are plain text; a click copies
     * an error (to paste into a support request), or every text with o.Copy "always" (the legacy AuthError).
     * With o.LiveDelay the line itself is not live: a screen reader hears the text once it has been stable for that many
     * ms (and only when it changed), so typing doesn't announce a hint per key; errors are announced at once.
     * Properties: E, Live (the announcing element: E, or a hidden sibling with LiveDelay). Methods: Set(text, kind, o),
     * Clear(), Temp(text, ms, kind), Leak(password), Announce(), Kind.
     */
    class Status {
        /**
         * @param {HTMLElement|AuthPage} target Parent.
         * @param {object} [o] Options: { Title (tooltip), Class, Copy ("errors" (default) | "always" | "never"),
         * LiveDelay (ms, see the class description), Keep (default true: the line keeps the tallest height it had, LAY-9; false when
         * it sits in a reserved cell of its own) }.
         */
        constructor(target, o) {
            o = o || {};
            const t = this;
            t.Delay = o.LiveDelay || 0;
            t.CopyMode = o.Copy || "errors";
            t.E = el("div.auth-status", { attrs: t.Delay ? null : { role: "status", "aria-live": "polite" } });
            if (o.Class)
                t.E.classList.add(o.Class);
            //  LAY-9: the line keeps the tallest height it had (a shorter or cleared text while typing pulls nothing up)
            if (o.Keep !== false)
                Sw.Reserve.Line(t.E, { Lines: 0 });
            t.BaseTitle = o.Title || "";
            t.Seq = 0;
            t.Kind = null;
            t.Live = t.E;
            t.LastLive = "";
            box(target).append(t.E);
            if (t.Delay) {
                t.Live = el("div.sw-sr-only", { attrs: { role: "status", "aria-live": "polite" } });
                box(target).append(t.Live);
            }
            t.Set("");
        }

        /**
         * Show a text.
         * @param {string} text The text (empty keeps the line height).
         * @param {string} [kind] "ok" | "error" | "warning" | "info" (null = neutral).
         * @param {object} [o] Options: { Title, Html }.
         */
        Set(text, kind, o) {
            o = o || {};
            const t = this;
            ++t.Seq;
            clearTimeout(t.TempTimer);
            t.LeakPending = null;
            t.LeakShown = null;
            t.Apply(text, kind, o);
        }

        /**
         * Write the content (no sequence change).
         * @param {string} text Text.
         * @param {string} kind Kind.
         * @param {object} o Options ({Title, Html}).
         */
        Apply(text, kind, o) {
            const t = this;
            t.Kind = kind || null;
            Kinds.forEach(k => t.E.classList.toggle("auth-" + k, k === kind));
            SetContent(t.E, text, o.Html);
            if (!text)
                t.E.innerHTML = "&nbsp;";
            t.E.title = o.Title || t.BaseTitle || "";
            t.Text = text || "";
            if (t.CopyMode === "always" || (t.CopyMode === "errors" && kind === "error" && text))
                Sw.Clipboard.CopyOnClick(t.E);
            else
                removeCopy(t.E);
            t.Queue(kind === "error");
        }

        /**
         * LiveDelay: announce the current text once it has been stable for the delay (at once when now), unless it was the
         * last announced text; an empty text that stays resets that (the same hint is announced again later).
         * @param {boolean} [now] Announce at once.
         */
        Queue(now) {
            const t = this;
            if (!t.Delay)
                return;
            clearTimeout(t.LiveTimer);
            const run = () => {
                const s = (t.E.textContent || "").replace(/ /g, " ").trim();
                if (s === t.LastLive)
                    return;
                t.LastLive = s;
                t.Live.textContent = s;
            };
            if (now)
                run();
            else
                t.LiveTimer = setTimeout(run, t.Delay);
        }

        /**
         * Announce the current text again now (ex: the user tried a blocked action and should hear why).
         * With LiveDelay the hidden live element is emptied and refilled, otherwise the line is re-written.
         */
        Announce() {
            const t = this;
            clearTimeout(t.LiveTimer);
            const s = (t.E.textContent || "").replace(/ /g, " ").trim();
            if (!s)
                return;
            const target = t.Live;
            const html = t.Delay ? null : t.E.innerHTML;
            const seq = t.Seq;
            target.textContent = "";
            setTimeout(() => {
                if (t.Delay)
                    target.textContent = s;
                else if (t.Seq === seq)
                    target.innerHTML = html;
                t.LastLive = s;
            }, 60);
        }

        /** Clear the text. */
        Clear() {
            this.Set("");
        }

        /**
         * Show a text for a while, then restore the previous content (unless something else was shown meanwhile).
         * @param {string} text The text.
         * @param {number} [ms=5000] Duration.
         * @param {string} [kind="error"] Kind.
         */
        Temp(text, ms, kind) {
            const t = this;
            const prev = { Text: t.E.innerHTML, Kind: t.Kind, Title: t.E.title };
            //  A leak check still running when the temporary text replaces it is re-run (memoized) afterwards
            const leak = t.LeakPending;
            t.Set(text, kind ?? "error");
            const seq = t.Seq;
            t.TempTimer = setTimeout(() => {
                if (t.Seq !== seq)
                    return;
                if (leak)
                    t.Leak(leak);
                else
                    t.Apply(prev.Text, prev.Kind, { Html: true, Title: prev.Title });
            }, ms || 5000);
        }

        /**
         * Show the password leak check result: "Checking password ..", then "Password is safe (not leaked)!", "This password is part
         * of N data leaks" or "Password leak check failed!" (the old CheckPassword). The request (Sw.Auth.CheckLeak) starts at once
         * (no delay), and only the newest check is shown. As the old CheckPassword memo (el.LastCheck): the same password as the
         * running or shown check makes no new request; any other text in the line (Set) forgets it.
         * @param {string} password The password.
         * @param {boolean} [onlyLeaked] Quiet mode: nothing is shown while checking, for a safe password or when the check failed;
         * a leaked password is shown as a warning.
         * Nothing is shown when the deployment turned the leak check off (Sw.Auth.LeakCheckEnabled).
         */
        Leak(password, onlyLeaked) {
            const t = this;
            if (t.LeakPending === password || t.LeakShown === password)
                return;
            if (onlyLeaked || (A.LeakCheckIsOff && A.LeakCheckIsOff()))
                t.Set("");
            else
                t.Set(A.LeakText(null).Text, "info", { Title: A.LeakText(null).Title });
            t.LeakPending = password;
            const seq = t.Seq;
            /* SwWeb:allow ux-fbk-5 (CheckLeak never rejects: a failed request is its State 4, shown as "Password leak check failed!") */
            A.CheckLeak(password).then(r => {
                if (t.Seq !== seq)
                    return;
                t.LeakPending = null;
                const lt = A.LeakText(r);
                if (!onlyLeaked)
                    t.Apply(lt.Text, lt.Kind, { Title: lt.Title });
                else if (r && r.State === 2)
                    t.Apply(lt.Text, "warning", { Title: lt.Title });
                t.LeakShown = password;
            });
        }
    }

    // ------------------------------------------------------------------ fields

    /**
     * A labelled input of an auth form (Sw.TextBox) with optional show / hide, copy and generate buttons.
     * Properties: E (root), Input, Box (the Sw.TextBox), Page. Methods: Get(), Set(value, silent), Focus(select),
     * SetInvalid(bool), SetReadOnly(bool), SetDisabled(bool, reason).
     */
    class Field {
        /**
         * @param {HTMLElement|AuthPage} target Parent (a page registers the field for Enter navigation and busy state).
         * @param {object} o Options: { Label, Hint (tooltip of the input), Placeholder, Type ("text", "password", "email",
         * "tel"), AutoComplete, InputMode, Name, MaxLength, MinLength, Value, Reveal (show / hide for passwords, default on),
         * Copy (the Sw.TextBox copy button: default true for every field but passwords, false = none, or what it copies),
         * Generate (a "generate password" button: true or a policy),
         * Policy (the policy for Generate), LabelExtra (an element appended to the label row), OnInput(field), OnChange(field),
         * Draft (the Sw.TextBox option: keep the typed text as a draft, ex: { Context: "invite.email", Dirty: false }; never for
         * passwords), Dirty (the Sw.TextBox option, opt in: an auth field doesn't mark the page dirty, as the old pages never
         * asked "Leave without saving?"; true makes typing mark it) }. Escape keeps the typed text (the Sw.TextBox default, as the
         * old inputs).
         */
        constructor(target, o) {
            const t = this;
            o = o || {};
            t.O = o;
            const isPw = o.Type === "password";
            t.Box = new Sw.TextBox({
                Type: o.Type || "text",
                Placeholder: o.Placeholder,
                Label: o.Label,
                Title: o.Hint,
                AutoComplete: o.AutoComplete || "off",
                InputMode: o.InputMode,
                MaxLength: o.MaxLength || 128,
                Reveal: isPw && o.Reveal !== false,
                Value: o.Value,
                Draft: isPw ? undefined : o.Draft,
                //  Not user data by default (the Sw.TextBox default): typing a user id, code or e-mail doesn't make the page ask
                //  "Leave without saving?" (the old auth pages never asked; a page that wants it says so, ex: Invite user)
                Dirty: o.Dirty,
                //  As the old auth inputs: every text field (user id, e-mail, phone, code ...) has a copy button, never a password
                Copy: isPw ? undefined : (o.Copy === undefined ? true : o.Copy || undefined),
                OnInput: () => t.Changed(),
                OnChange: () => o.OnChange && o.OnChange(t),
            });
            t.E = t.Box.E;
            t.E.classList.add("auth-field");
            t.Input = t.Box.Input;
            const i = t.Input;
            if (o.Name)
                i.name = o.Name;
            i.required = true;
            i.setAttribute("autocapitalize", "none");
            i.setAttribute("autocorrect", "off");
            if (o.MinLength)
                i.minLength = o.MinLength;
            if (o.LabelExtra) {
                const lab = t.E.querySelector(".sw-text-label");
                const row = el("div.auth-label-row", null, lab, o.LabelExtra);
                t.E.prepend(row);
            }
            if (o.Generate) {
                t.GenBtn = new Sw.Button({
                    Icon: "IconAuthRandom", Kind: "ghost", Small: true, Class: "auth-gen",
                    Title: _TF("Generate a new random password and copy it to the clipboard", "Tool tip description of a button that when pressed will generated a random safe password in the input field and also copy it to the clipboard"),
                    OnClick: async () => {
                        const v = A.GeneratePassword(typeof o.Generate === "object" ? o.Generate : o.Policy);
                        t.Set(v);
                        t.Changed();
                        await Sw.Clipboard.Copy(v, { Generic: true });
                    },
                });
                t.Box.Field.append(t.GenBtn.E);
            }
            //  The copy button is the Sw.TextBox Copy option (always there, enabled when not empty: nothing moves, LAY-9)
            t.CopyBtn = t.Box.CopyBtn || null;
            box(target).append(t.E);
            if (target instanceof AuthPage) {
                t.Page = target;
                target.Fields.push(t);
            }
        }

        /** Called on every input: updates the buttons and calls OnInput. */
        Changed() {
            this.UpdateButtons();
            if (this.O.OnInput)
                this.O.OnInput(this);
        }

        /** Update the buttons (the copy button follows the text: Sw.TextBox). */
        UpdateButtons() {
            this.Box.UpdateClear();
        }

        /**
         * The value (not trimmed).
         * @returns {string}
         */
        Get() {
            return this.Input.value;
        }

        /**
         * Set the value.
         * @param {string} v Value.
         * @param {boolean} [silent] No callbacks.
         */
        Set(v, silent) {
            this.Box.Set(v, true);
            this.UpdateButtons();
            if (!silent && this.O.OnInput)
                this.O.OnInput(this);
        }

        /**
         * Focus the input.
         * @param {boolean} [select] Select the text.
         */
        Focus(select) {
            if (this.Page && this.Page.LoadRemover) {
                this.Page.Focus(this, select);
                return;
            }
            this.Box.Focus(select);
        }

        /**
         * Mark the input invalid (red border, aria-invalid).
         * @param {*} invalid Truthy marks it.
         */
        SetInvalid(invalid) {
            const on = !!invalid;
            this.E.classList.toggle("sw-invalid", on);
            if (on)
                this.Input.setAttribute("aria-invalid", "true");
            else
                this.Input.removeAttribute("aria-invalid");
        }

        /**
         * Read only (while a request runs).
         * @param {boolean} ro Read only.
         */
        SetReadOnly(ro) {
            //  The end of a request never unlocks a disabled field (the disabled box is read only too: autofill or dictation
            //  could change it otherwise)
            this.Input.readOnly = !!ro || !!this.Disabled;
            if (this.GenBtn && !this.Disabled)
                this.GenBtn.SetDisabled(!!ro, waitReason());
        }

        /**
         * Disable the field.
         * @param {boolean} d Disabled.
         */
        SetDisabled(d, reason) {
            this.Disabled = !!d;
            this.Box.SetDisabled(d, reason);
            if (this.GenBtn)
                this.GenBtn.SetDisabled(!!d, reason);
        }
    }

    /**
     * A phone number field: type tel, phone keyboard, only digits / '.' / ' ' / one leading '+', and a flag of the
     * country of the number (looked up with GetPhonePrefixInfo; tooltip lists the matching prefixes).
     */
    class PhoneField extends Field {
        /**
         * @param {HTMLElement|AuthPage} target Parent.
         * @param {object} o Field options (Type, InputMode and AutoComplete default to tel).
         */
        constructor(target, o) {
            const onInput = o.OnInput;
            super(target, Object.assign({ AutoComplete: "tel", InputMode: "tel" }, o, {
                Type: "tel",
                OnInput: f => f.PhoneInput(onInput),
            }));
            const t = this;
            //  Enter in a phone number never sends the form (Admin N44): the old phone input moved the focus on (to its country flag) and
            //  sent nothing, the message (SMS) went only from the button; here Enter goes on to the next field or the main button
            t.EnterWalks = true;
            t.E.classList.add("auth-phone");
            t.Flag = el("img.auth-flag", { alt: "", draggable: false });
            t.Box.Field.prepend(t.Flag);
            t.LastLookup = null;
            t.SetCountry(null, "");
        }

        /**
         * Clean the typed value, update the flag and call the user callback. As the old AuthInputPhone: only a changed number does
         * anything, the country is looked up first (GetPhonePrefixInfo) and the callback (the page's validation, GetPhoneNumberInfo)
         * runs once the lookup answered.
         * @param {function(Field):void} [onInput] The OnInput option.
         * @returns {Promise<void>}
         */
        async PhoneInput(onInput) {
            const t = this;
            const v = t.Input.value;
            const c = A.CleanPhone(v);
            if (c !== v) {
                const pos = Math.max(0, (t.Input.selectionStart ?? c.length) - (v.length - c.length));
                t.Input.value = c;
                try {
                    t.Input.setSelectionRange(pos, pos);
                }
                catch {
                }
            }
            if (!await t.Lookup(c.trim()))
                return;
            if (onInput)
                onInput(t);
        }

        /**
         * Look up the country of the number (newest lookup wins).
         * @param {string} v The number.
         * @returns {Promise<boolean>} False when the number is the one of the last lookup (nothing changed).
         */
        async Lookup(v) {
            const t = this;
            if (v === t.LastLookup)
                return false;
            t.LastLookup = v;
            const r = await A.PhoneCountry(v);
            if (t.LastLookup === v)
                t.SetCountry(r.Iso, r.Text);
            return true;
        }

        /**
         * Show a flag.
         * @param {string|null} iso ISO country code (null = unknown).
         * @param {string} text Matching prefixes (tooltip).
         */
        SetCountry(iso, text) {
            const t = this;
            t.Flag.src = iso ? Sw.Url.Root + "iso_data/country/" + iso.toUpperCase() + ".svg" : Sw.Url.Root + "iso_data/icons/unknown.svg";
            t.Flag.title = text || _TF("Enter a complete phone number including the country dialling code, prefixed by a + sign", "Tool tip description for an input field where the user should enter their phone number");
            t.Flag.classList.toggle("auth-unknown", !iso);
        }
    }

    /**
     * The password rules as a visible checklist below a new password field (UX guide FRM-4: the rules are shown before the user
     * types, not only behind the (i) button): met rules are ticked, unmet ones are highlighted once a password is typed. The same
     * look as the sign in reminder (classes auth-rules / auth-rule*). The list is the field's description (aria-describedby).
     * Properties: E (the ul). Methods: Update(password) (the trimmed password, "" for none).
     */
    class Rules {
        /**
         * @param {HTMLElement|AuthPage} target Parent (appended at the end).
         * @param {object} policy The create policy (A.PolicyRules).
         * @param {Field} [field] The password field it describes.
         */
        constructor(target, policy, field) {
            const t = this;
            t.Policy = policy;
            t.E = el("ul.auth-rules", {
                id: Sw.Uid ? Sw.Uid("auth-rules") : null,
                attrs: { "aria-label": _TF("Password rules", "Accessible name of a checklist of password rules below a password input on the sign in page") },
            });
            //  LAY-9: in a cell as high as the policy's longest list (with "At most ... characters", shown only when a password is too
            //  long), so typing never moves the fields below
            t.Slot = Sw.Reserve.Stack({ Class: "auth-rules-slot", Sizer: el("ul.auth-rules", null, RuleItems(A.PolicyRules("x".repeat(130), policy), true)) });
            t.Slot.append(t.E);
            box(target).append(t.Slot);
            if (field && field.Input && t.E.id)
                field.Input.setAttribute("aria-describedby", ((field.Input.getAttribute("aria-describedby") || "") + " " + t.E.id).trim());
            t.Update("");
        }

        /**
         * Tick the rules the password meets.
         * @param {string} p The trimmed password.
         */
        Update(p) {
            this.E.replaceChildren(...RuleItems(A.PolicyRules(p || "", this.Policy), !!p));
        }
    }

    /**
     * A small "?" button that shows a text (ex: the password policy) in a popup (works with touch and keyboard).
     * @param {string} text The text.
     * @param {string} [title] Tooltip / accessible name.
     * @returns {Sw.Button} The button (append .E).
     */
    function InfoButton(text, title) {
        const b = new Sw.Button({
            Icon: "info", Kind: "ghost", Small: true, Class: "auth-info",
            Title: title || _TF("Show the password requirements", "Tool tip of a button that shows the password policy"),
            OnClick: () => {
                Sw.Popup.Show(b.E, el("div.auth-popup-text.sw-root", { text: b.InfoText }), { Side: "below", Align: "end" });
            },
        });
        b.InfoText = text;
        //  A mouse / touch helper, not a tab stop: it sits in the label row before its password input, and the old forms went
        //  straight from the user id (or the current password) to the password with Tab. The rules it shows are also listed
        //  under the field (and describe the input for screen readers)
        b.E.tabIndex = -1;
        return b;
    }

    // ------------------------------------------------------------------ segments (tab like selector)

    /**
     * A row of mutually exclusive choices (role tablist; arrow keys, Home / End; one tab stop).
     * Methods: Add(text, icon, title) -> index, Select(index, silent), SetDisabled(bool). Properties: E, Selected.
     */
    class Segments {
        /**
         * @param {HTMLElement|AuthPage} target Parent.
         * @param {object} [o] Options: { Label (accessible name / tooltip), OnChange(index) }.
         */
        constructor(target, o) {
            const t = this;
            t.O = o || {};
            t.Items = [];
            t.Selected = -1;
            t.E = el("div.auth-segments", { title: t.O.Label || null, attrs: { role: "tablist", "aria-label": t.O.Label || null } });
            t.E.addEventListener("keydown", e => {
                if (t.E.closest(".auth-busy"))
                    return;
                const n = t.Items.length;
                let i = t.Selected;
                if (e.key === "ArrowRight" || e.key === "ArrowDown")
                    i = (i + 1) % n;
                else if (e.key === "ArrowLeft" || e.key === "ArrowUp")
                    i = (i - 1 + n) % n;
                else if (e.key === "Home")
                    i = 0;
                else if (e.key === "End")
                    i = n - 1;
                else
                    return;
                e.preventDefault();
                t.Select(i);
                t.Items[i].focus();
            });
            box(target).append(t.E);
        }

        /**
         * Add a choice.
         * @param {string} text Text.
         * @param {string} [icon] Icon class.
         * @param {string} [title] Tooltip.
         * @returns {number} The index.
         */
        Add(text, icon, title) {
            const t = this;
            const i = t.Items.length;
            const b = el("button.auth-segment", { type: "button", title: title || null, attrs: { role: "tab", "aria-selected": "false", tabindex: "-1" } },
                icon ? Sw.IconClass(icon) : null, el("span", { text }));
            b.addEventListener("click", () => {
                //  a busy form keeps its choice (the click is absorbed, the control keeps its look: UX guide FBK-2)
                if (!t.E.closest(".auth-busy"))
                    t.Select(i);
            });
            t.Items.push(b);
            t.E.append(b);
            if (t.Selected < 0)
                t.Select(0, true);
            return i;
        }

        /**
         * Select a choice.
         * @param {number} i Index.
         * @param {boolean} [silent] Don't call OnChange.
         */
        Select(i, silent) {
            const t = this;
            t.Selected = i;
            t.Items.forEach((b, j) => {
                b.setAttribute("aria-selected", j === i ? "true" : "false");
                b.tabIndex = j === i ? 0 : -1;
            });
            if (!silent && t.O.OnChange)
                t.O.OnChange(i);
        }

        /**
         * Disable all choices (while a request runs).
         * @param {boolean} d Disabled.
         */
        SetDisabled(d) {
            this.Items.forEach(b => b.disabled = !!d);
        }
    }

    // ------------------------------------------------------------------ short code step

    /**
     * Replace the page content with the short code step: a message (html), a code input (digits only, one-time-code
     * autofill, numeric keyboard) and a Continue button. The code is checked with o.Validate (default ValidateShortCode):
     * -1 disables the input ("expired or too many tries"), -2 asks to wait (too many wrong codes for the account, the code
     * may still work later), 0 asks to try again, 1 navigates to link + code in this window.
     * @param {AuthPage} page The page.
     * @param {object} o Options: { Html (message), Link (url prefix, ex "ChoosePassword.html?"), Label, Placeholder,
     * ButtonText, Icon (button icon, default the page icon), Again (optional { Text, Title, Url }: a way to ask for a new
     * code, offered when the code no longer works), Validate (optional async code => 1 | 0 | -1 | -2, ex: a code that
     * belongs to an account instead of this session) }.
     * @returns {{Field:Field, Button:Sw.Button, Status:Status}} The parts.
     */
    function ShortCode(page, o) {
        o = o || {};
        page.Clear();
        //  As the old code step (tabToNextOnEnter): Enter in the code goes to Continue, a second Enter continues (Admin N39)
        page.EnterWalks = true;
        if (o.Html)
            Text(page, o.Html, { Html: true });
        const ph = o.Placeholder || _TF("Enter the sent code", "Placeholder text for an input box where the user should enter a short code sent through mail or sms");
        let prev = null;
        const field = new Field(page, {
            Label: o.Label || _TF("Code", "Label text for an input box where the user should enter a short code sent through mail or sms"),
            Placeholder: ph,
            Hint: ph,
            AutoComplete: "one-time-code",
            InputMode: "numeric",
            Name: "code",
            MaxLength: 16,
            OnInput: f => {
                const c = A.CleanShortCode(f.Get(), true) ?? "";
                if (c !== f.Get())
                    f.Set(c, true);
                if (prev === c)
                    return;
                prev = c;
                status.Clear();
                f.SetInvalid(false);
                button.SetDisabled(A.CleanShortCode(c) === null, _TF("Enter the code you received first", "Reason of the disabled Continue button of the short code step while no complete code is entered"));
            },
        });
        field.Input.setAttribute("pattern", "[0-9]*");
        const status = new Status(page);
        let icon = o.Icon;
        if (!icon && page.IconE)
            icon = [...page.IconE.classList].find(c => /^Icon[A-Z]/.test(c) && !c.startsWith("IconColor"));
        const buttons = Buttons(page);
        /** The "ask for a new code" button (o.Again), made when the code no longer works. */
        let again = null;
        const button = Button(buttons, {
            Text: o.ButtonText || _TF("Continue", "Text of a button that when clicked will apply a short code that was sent through mail or sms"),
            Icon: icon, Submit: true, Disabled: _TF("Enter the code you received first", "Reason of the disabled Continue button of the short code step while no complete code is entered"),
        }, page);
        page.OnSubmit = async () => {
            const code = A.CleanShortCode(field.Get());
            if (!code) {
                field.Focus();
                status.Set(_TF("Invalid code syntax", "Error message displayed when an entered short code has an invalid syntax"), "error");
                return;
            }
            let valid = 0;
            try {
                valid = await (o.Validate ? o.Validate(code) : A.ValidateShortCode(code));
            }
            catch (e) {
                status.Set(e.message, "error");
                return;
            }
            if (valid === -2) {
                //  Not gone: the tries come back after a while (the code still works until it expires)
                field.SetInvalid(true);
                status.Set(_TF("Too many wrong codes were tried for this account. Wait a few minutes and try again.", "Error message displayed when too many wrong short codes were entered for the account (from any browser), the user must wait before trying again"), "error");
                setTimeout(() => field.Focus(true), 0);
                return;
            }
            if (valid < 0) {
                //  Nothing runs any more: the reason says why the code can't be used (not "wait")
                const gone = o.Again
                    ? _TF("This code no longer works. Ask for a new code.", "Reason of the disabled code field and Continue button of the short code step after the code expired or was entered wrong too many times")
                    : waitReason();
                field.SetDisabled(true, gone);
                button.SetDisabled(true, gone);
                status.Set(o.Again
                    ? _TF("This code no longer works (it expired or was entered wrong too many times). Ask for a new code.", "Error message displayed when an entered short code has expired or was entered wrong too many times, and the page offers to ask for a new code")
                    : _TF("Code expired or too many tries", "Error message displayed when an entered short code have expired or if it's being used multiple times"), "error");
                //  A way on (not a dead end): ask for a new code, once. It is the main button now (Enter goes there), and it is
                //  described by the status, so a screen reader reads why with the focused button
                if (o.Again && !again) {
                    again = Button(buttons, {
                        Text: o.Again.Text, Title: o.Again.Title, Icon: "IconRecover",
                        OnClick: () => Sw.Page.Navigate(o.Again.Url, "_self"),
                    }, page);
                    page.MainButton = again;
                    if (!status.E.id)
                        status.E.id = "auth-code-status";
                    again.E.setAttribute("aria-describedby", status.E.id);
                    setTimeout(() => again.E.focus(), 0);
                }
                return;
            }
            if (valid === 0) {
                field.SetInvalid(true);
                status.Set(o.Validate
                    ? _TF("Wrong or expired code, try again. A code works for 15 minutes.", "Error message displayed when an entered short code is wrong or expired, for a code that can be used in any browser")
                    : _TF("Invalid code, try again", "Error message displayed when an entered short code is invalid"), "error");
                setTimeout(() => field.Focus(true), 0);
                return;
            }
            button.SetDisabled(true, waitReason());
            Sw.Page.Navigate(o.Link + code, "_self");
        };
        field.Focus();
        return { Field: field, Button: button, Status: status };
    }

    // ------------------------------------------------------------------ login page

    /**
     * True if the browser autofilled an input (some browsers don't expose the value before the user interacts).
     * @param {HTMLInputElement} input The input.
     * @returns {boolean}
     */
    function IsAutoFilled(input) {
        if (!input)
            return false;
        for (const s of [":autofill", ":-webkit-autofill"]) {
            try {
                if (input.matches(s))
                    return true;
            }
            catch {
            }
        }
        return false;
    }

    /**
     * The passkey plug-in (Passkey.js) when loaded and supported, else null.
     * @returns {*} The PassKey class or null.
     */
    function passKeys() {
        const P = (typeof PassKey !== "undefined") ? Sw.PassKey : window.PassKey;
        return (P && P.IsSupported && P.IsSupported()) ? P : null;
    }

    /** @type {Promise<boolean>|null} Memoized LoadPassKeys result. */
    let passKeyLoad = null;

    /**
     * Load the optional passkey plug-in (auth/Passkey.js + Passkey.css, from SysWeaver.MicroService.PassKey) only when
     * the passkey service runs on the server (one `Api/HaveService "SysWeaver.MicroService.PassKeyService"` call), so
     * apps without it get no 404s. Nothing is requested when the browser has no WebAuthn support or the page has a static
     * `<script src="Passkey.js">` tag (loaded or not: Login, ChoosePassword and ResetPassword link it as the original pages did,
     * a 404 when the service doesn't run). Memoized per page.
     * @returns {Promise<boolean>} True when the PassKey class is available afterwards.
     */
    function LoadPassKeys() {
        if (!passKeyLoad)
            passKeyLoad = (async () => {
                if ((typeof PassKey !== "undefined") || window.PassKey)
                    return true;
                if (!window.PublicKeyCredential || document.querySelector('script[src$="Passkey.js"]'))
                    return false;
                let have = false;
                try {
                    have = await Sw.Net.Send(Sw.Url.Root + "Api/HaveService", "SysWeaver.MicroService.PassKeyService", { NoNavigate: true });
                }
                catch (e) {
                    console.warn("AuthUi: HaveService failed: " + ((e && e.message) || e));
                }
                if (!have)
                    return false;
                const base = Sw.Url.Root + "auth/";
                const [, js] = await Promise.all([
                    Sw.Net.IncludeCss(null, base + "Passkey.css", true),
                    Sw.Net.IncludeJs(null, base + "Passkey.js", true),
                ]);
                return !!(js && js.loaded);
            })();
        return passKeyLoad;
    }

    /** @type {Promise<string>|null} Memoized AppName result. */
    let appName = null;

    /**
     * The product name: "name" (the application's display name) of the templated app/manifest.json, memoized. Uses the page's own
     * memo (Sw.Page.AppName) when there is one, so the page asks for the manifest once.
     * @returns {Promise<string>} The name ("" when unknown).
     */
    function AppName() {
        if (!appName)
            appName = (async () => {
                try {
                    if (Sw.Page && typeof Sw.Page.AppName === "function")
                        return String((await Sw.Page.AppName()) || "").trim();
                    const m = await Sw.Net.Get(Sw.Url.Root + "app/manifest.json", { NoNavigate: true });
                    return String((m && (m.name || m.short_name)) || "").trim();
                }
                catch {
                    return "";
                }
            })();
        return appName;
    }

    // ------------------------------------------------------------------ link pages (a link sent in an e-mail / text message)

    /**
     * Check the link (action token) of this page WITHOUT using it (Api/auth/CheckActionToken of the user manager), so a link that no
     * longer works is said on load, with the real cause, before the user types or confirms anything (UX guide FBK-1, FBK-6, SAF-2).
     * Every page opened from a link calls it first:
     *
     *     const link = await Sw.AuthUi.CheckLinkToken("DeleteAccount", { Page: page });
     *     if (!link.Live)
     *         return;          // the page now says "This link no longer works", the cause and what to do
     *     // link.Account, link.Target: the facts for the confirmation
     *
     * The result never rejects. States: the server's "Live", "Used", "Replaced", "Revoked", "Expired", "Unknown", "WrongKind", and
     * - "None": no token in the url (request mode; Live false, nothing is shown);
     * - "Code": a short code (32 characters or less) typed by the user: the server can't look it up without using it, Live true
     *   (the action itself refuses a wrong code with the reason);
     * - "Unchecked": the check could not be made (the user manager does not run, a lost connection): Live true, the page works as
     *   before (the action refuses a dead link with the reason).
     * @param {string} kind The kind of link the page expects: "ResetPassword" (also add password links), "AddPassword",
     * "DeletePassword", "DeleteAccount", "AddEmail" (add / change / restore e-mail), "AddPhone" (add / change / restore phone),
     * "AddPassKey", "NewUser" (an invitation or a sign up), "UndoEmail" (undo an e-mail change, from the notice to the old address).
     * @param {object} [o] Options: { Token (default the url's, Sw.Auth.QueryToken), Page (an AuthPage: a dead link is shown on it with
     * LinkGone, else nothing is shown) }.
     * @returns {Promise<{State: string, Live: boolean, Checked: boolean, Kind: string, Reason: string, Account: (string|null),
     * Target: (string|null), Expires: (string|null), Token: (string|null)}>} The state (Checked: the server answered).
     */
    async function CheckLinkToken(kind, o) {
        o = o || {};
        const token = o.Token !== undefined ? o.Token : A.QueryToken();
        const res = { State: "None", Live: false, Checked: false, Kind: kind, Reason: "", Account: null, Target: null, Expires: null, Token: token || null };
        if (!token)
            return res;
        if (token.length <= 32) {
            res.State = "Code";
            res.Live = true;
            return res;
        }
        let r = null;
        try {
            r = await Sw.Net.Read(Sw.Url.Root + "Api/auth/CheckActionToken", { Token: token, Kind: kind }, { Post: true, Reload: true, NoNavigate: true });
        }
        catch {
            r = null;
        }
        if (!r || typeof r.State !== "string") {
            res.State = "Unchecked";
            res.Live = true;
            return res;
        }
        Object.assign(res, {
            State: r.State, Live: r.State === "Live", Checked: true, Kind: r.Kind || kind, Reason: r.Reason || "",
            Account: r.Account || null, Target: r.Target || null, Expires: r.Expires || null,
        });
        if (!res.Live && o.Page)
            LinkGone(o.Page, res, kind);
        return res;
    }

    /**
     * Show "This link no longer works" on a page (a Design v2 state view: icon, title, the cause, what to do, the actions; the page
     * content is replaced, no toast): the server's reason (the real cause: used, a newer link was sent, the password / e-mail / phone
     * changed, expired ...), what to do for this kind of link, a help line that names the application, and
     * - "ResetPassword": Request a new link (ForgotPassword.html) + Sign in;
     * - "NewUser": ask the inviter for a new invitation; Sign in;
     * - "AddPassKey": make a new QR code on the signed in device; Sign in;
     * - "UndoEmail": reset the password with the phone number of the account, or contact the support; Request a new link + Sign in;
     * - else (account links): sign in and start again from the page that sent it; Sign in.
     * CheckLinkToken calls it for a dead link; a page also calls it when its action is refused with a 410 (the link died meanwhile).
     * @param {AuthPage} page The page.
     * @param {object|null} state The CheckLinkToken result, or { Reason } (null: the generic cause).
     * @param {string} [kind] The kind of link the page expects (see CheckLinkToken).
     */
    function LinkGone(page, state, kind) {
        //  (State "Disabled": the link is fine, but an administrator disabled the account: a new link can't help, only the administrator)
        const disabled = !!(state && state.State === "Disabled");
        const title = disabled
            ? _TF("This account is disabled", "Heading of the page shown for a link (ex: a password reset link) of an account that an administrator disabled")
            : kind === "NewUser"
            ? _TF("This invitation link no longer works", "Heading of the page shown for an invitation link that was withdrawn, already used or expired")
            : (kind === "ResetPassword" || kind === "AddPassword")
                ? _TF("This password link no longer works", "Heading of the page shown for a password reset link (or code) that was already used or expired")
                : kind === "UndoEmail"
                    ? _TF("This undo link no longer works", "Heading of the page shown for the link that undoes an e-mail address change (sent to the old address) when it was already used or expired")
                    : _TF("This link no longer works", "Heading of the page shown for an account link (or code) that was already used, withdrawn or expired");
        //  The cause: the server's sentence ("This link no longer works: a newer link was sent, ..."); the part after the title when it
        //  repeats it (English), else the whole sentence
        let why = String((state && state.Reason) || "").trim();
        const m = /^this link no longer works\s*:\s*(.+)$/i.exec(why);
        if (m)
            why = m[1].charAt(0).toLocaleUpperCase() + m[1].substring(1);
        if (!why)
            why = _TF("It was already used, replaced by a newer link, withdrawn or it has expired.", "Cause on the page of a link that no longer works when the server could not say why");
        const replaced = state && state.State === "Replaced";
        const todo = replaced || disabled
            ? ""
            : kind === "ResetPassword"
                ? _TF("Request a new link. If an administrator asked you to choose a new password, signing in with your current password sends a new code.", "What to do, on the page of a password reset link that no longer works")
                : kind === "NewUser"
                    ? _TF("Ask the person who invited you to send a new invitation. If you signed up yourself, sign up again.", "What to do, on the page of an invitation link that no longer works")
                    : kind === "AddPassKey"
                        ? _TF("On the device where you are signed in, open Add passkey on another device and scan the new QR code.", "What to do, on the page of an add passkey link (QR code) that no longer works")
                        : kind === "UndoEmail"
                            ? _TF("Reset your password: on the forgot password page, enter the phone number of the account. No phone number on the account? Contact the support of this site.", "What to do, on the page of the link that undoes an e-mail address change when it no longer works")
                            : _TF("Sign in and start again from the page that sent the link: you get a new one.", "What to do, on the page of an account link (change e-mail, delete account ...) that no longer works");
        page.Clear();
        //  The state view has its own icon (the page icon is hidden; an image icon, ex: a QR code, is stale too)
        page.IconWrap.hidden = true;
        document.title = title;
        if (page.Header)
            page.Header.SetTitle(title);
        const actions = Sw.ButtonRow();
        const request = (kind === "ResetPassword" || kind === "UndoEmail") && !disabled;
        if (request)
            Button(actions, {
                Text: _TF("Request a new link", "Button on the page of a password reset link that no longer works: opens the forgot password page"),
                Title: _TF("Send a new link and code to choose a new password", "Tool tip of the button that opens the forgot password page"),
                Icon: "IconRecover", Submit: true,
            }, page);
        Button(actions, {
            Text: _TF("Sign in", "Button on the page of a link that no longer works: opens the sign in page"),
            Title: _TF("Go to the sign in page", "Tool tip of the sign in button on the page of a link that no longer works"),
            Icon: "IconAuthLogin", Submit: !request,
            OnClick: request ? () => Sw.Page.Navigate("Login.html", "_self") : undefined,
        }, page);
        page.OnSubmit = () => Sw.Page.Navigate(request ? "ForgotPassword.html" : "Login.html", "_self");
        const view = Sw.StateView({ Kind: "error", Icon: "warning", Title: title, Text: todo ? why + " " + todo : why, Actions: [actions] });
        view.E.classList.add("auth-link-gone");
        //  The title is the page's heading (screen reader heading navigation)
        const t = view.E.querySelector(".sw-state-title");
        if (t) {
            t.setAttribute("role", "heading");
            t.setAttribute("aria-level", "1");
        }
        page.Box.append(view.E);
        const help = Text(page, "", { Class: "auth-muted auth-link-help", NoCopy: true });
        AppName().then(n => help.textContent = n
            ? _T("If you need help, contact the administrator of {0}.", n, "Help line of the page shown for a link that no longer works.{0} is replaced with the application name")
            : _TF("If you need help, contact the administrator of this site.", "Help line of the page shown for a link that no longer works, when the application name is unknown"));
        page.Focus(page.MainButton ? page.MainButton.E : null);
    }

    /**
     * Is a failure of a link action a dead link (410: the link was used, replaced, revoked or expired since the page was opened)?
     * Then the page shows LinkGone with the server's reason instead of a failure line (the action can't work, "try again" can't help).
     * @param {*} e The error of the action.
     * @returns {boolean}
     */
    const IsLinkGone = e => !!(e && e.Status === 410);

    /**
     * End an account page with its result and where to go next (UX guide FBK-6: never a dead end; FOC-2: the focus on the result):
     * the page content is replaced by a state view (a check, the title as the page heading, one sentence of what happened) and one or
     * two buttons; the focus goes to the first button. Example: after "Delete account" → "Your account is deleted" + "Go to the start
     * page".
     * @param {AuthPage} page The page.
     * @param {object} o Options: { Title (what happened, the heading), Text (a sentence: the facts and what it means), Icon (Sw.Icon
     * name, default "check"), Next (array of { Text, Title, Icon, Url (navigates, "_self"), Top (true: the whole window, ex: the site
     * root after the session ended), OnClick }; default: "Go to the start page" = the site root) }.
     * @returns {{E: HTMLElement}} The state view.
     */
    function Done(page, o) {
        o = o || {};
        page.Clear();
        page.IconWrap.hidden = true;
        if (o.Title) {
            document.title = o.Title;
            if (page.Header)
                page.Header.SetTitle(o.Title);
        }
        const next = Array.isArray(o.Next) && o.Next.length ? o.Next : [{
            Text: _TF("Go to the start page", "Button at the end of an account page (ex: after the account was deleted) that opens the start page of the site"),
            Title: _TF("Open the start page of this site", "Tool tip of the go to the start page button at the end of an account page"),
            Icon: "IconHome", Url: Sw.Url.Root, Top: true,
        }];
        const go = n => {
            if (n.OnClick)
                return n.OnClick();
            if (!n.Url)
                return undefined;
            //  (the whole window: the shell around this page belongs to the ended session)
            if (n.Top && window.top !== window.self)
                try {
                    window.top.location.href = new URL(n.Url, location.href).href;
                    return undefined;
                }
                catch {
                    //  Cross origin parent: this window
                }
            return Sw.Page.Navigate(n.Url, "_self");
        };
        const actions = Sw.ButtonRow();
        next.forEach((n, i) => Button(actions, i === 0
            ? { Text: n.Text, Title: n.Title, Icon: n.Icon, Submit: true }
            : { Text: n.Text, Title: n.Title, Icon: n.Icon, Kind: "ghost", OnClick: () => go(n) }, page));
        page.OnSubmit = () => go(next[0]);
        const view = Sw.StateView({ Kind: "empty", Icon: o.Icon || "check", Title: o.Title || "", Text: o.Text || "", Actions: [actions] });
        view.E.classList.add("auth-done");
        const t = view.E.querySelector(".sw-state-title");
        if (t) {
            t.setAttribute("role", "heading");
            t.setAttribute("aria-level", "1");
        }
        page.Box.append(view.E);
        page.Focus(page.MainButton ? page.MainButton.E : null);
        return view;
    }

    /**
     * Show the product header (the app banner, img src ../banner.svg: the server generated 9:2 banner of this app, its icon and
     * name; OWN4-Auth-1, was the square icon.svg) at the top of a page. It lives in the page's icon row, so AuthPage.Clear() keeps
     * it. No separate product name: the banner names the app (a missing banner file removes the picture, the card still works).
     * Design v2 (ui/README.md Page anatomy): the card sits on the plain canvas, like every other page (the brand weave
     * behind it was louder than the form). An app that wants the weave adds `?weave=true` or the class sw-weave to the body.
     * @param {AuthPage} page The page (created without a page icon).
     */
    function Brand(page) {
        const w = page.IconWrap;
        w.classList.add("auth-brand");
        if (!Sw.Page.IsEmbedded && Sw.Url.Get("weave") === "true")
            document.body.classList.add("sw-weave");
        const img = el("img.auth-brand-banner", { src: Sw.Url.Root + "banner.svg", alt: "", draggable: false });
        img.addEventListener("error", () => img.remove(), { once: true });
        w.append(img);
    }

    /**
     * Entry point of auth/Login.html: the product header, then either the sign in form (LoginForm) or, when a user is
     * already signed in, a "You are already signed in" view (SignedInView) with Continue / Sign out.
     * The server is asked who is signed in (POST Api/auth/GetUser) only when this browser may have a signed in user
     * (Sw.Auth.MaybeSignedIn): an anonymous visit sends the original requests only (GetPasswordPolicy "").
     * Url params (to, parent, loc) are accepted and not read (the shell / server use them). The page title is "Sign in".
     * @returns {Promise<void>}
     */
    async function LoginMain() {
        document.title = _TF("Sign in", "Title of the sign in page (the browser tab)");
        await Page(null, async page => {
            Brand(page);
            const who = A.MaybeSignedIn() ? await A.GetUser().catch(() => null) : null;
            if (who)
                SignedInView(page, who);
            else
                await LoginForm(page);
        }, { Class: "auth-login", Dialog: false });
    }

    /**
     * The display name of a user.
     * @param {object} who The AuthInfo.
     * @returns {string} NickName, else Username ("" when none).
     */
    const userName = who => String((who && (who.NickName || who.Username)) || "").trim();

    /**
     * Replace the page content with the "already signed in" view: who is signed in, Continue (the post sign in
     * navigation) and Sign out (signs out, then the page loads again: the sign in form).
     * @param {AuthPage} page The login page.
     * @param {object} who The signed in user (AuthInfo).
     */
    function SignedInView(page, who) {
        page.Clear();
        Heading(page, _TF("You are already signed in", "Heading on the sign in page when a user is already signed in"));
        const name = userName(who);
        if (name)
            Text(page, _T("Signed in as {0}.", UserHtml(name), "Text on the sign in page telling which user is already signed in.{0} is replaced with the user name"), { Html: true, NoCopy: true });
        Text(page, _TF("Continue to use the application, or sign out to sign in as someone else.", "Text on the sign in page explaining the choices when a user is already signed in"), { Class: "auth-muted", NoCopy: true });
        const status = new Status(page);
        const row = Buttons(page);
        const cont = Button(row, {
            Text: _TF("Continue", "Text of a button on the sign in page that continues to the application when a user is already signed in"),
            Title: _TF("Continue as the signed in user", "Tool tip description of a button on the sign in page that continues to the application when a user is already signed in"),
            Icon: "IconHome", Submit: true,
        }, page);
        const out = Button(row, {
            Text: _TF("Sign out", "Text of a button on the sign in page that signs out the already signed in user"),
            Title: _TF("Sign out, then sign in as another user", "Tool tip description of a button on the sign in page that signs out the already signed in user"),
            Icon: "IconAuthLogout",
            OnClick: async () => {
                const ok = await page.Run(async () => {
                    status.Set(_TF("Signing out ..", "Text indicating that the user is being signed out"), "info");
                    if (await A.Logout(who))
                        return true;
                    status.Set(_TF("Failed to sign out!", "Error text indicating that the sign out request failed"), "error");
                    return false;
                }, out);
                //  Signed out: the whole page loads again (as every sign out: nothing of the user stays; in the app shell the shell reloads)
                if (ok)
                    window.location.reload();
            },
        });
        page.OnSubmit = async () => {
            out.SetDisabled(true, waitReason());
            cont.SetDisabled(true, waitReason());
            await A.StartPage(0);
        };
        page.Focus(cont.E);
    }

    /**
     * Replace the page content with the sign in form: user id + password (validated live against the user's policy, fetched as
     * before on every change of the user id, shown as a checklist of rules ticked as they are met; the password leak check as before:
     * "Checking password ..", then safe / leaked / failed, never blocking), Sign in (hashed protocol, Basic fallback) with the failure reason shown
     * inline for 5 s ("Wrong user ID or password", the leak check runs again meanwhile, as before; or "already signed in" when the
     * server refused because of a signed in user), passkey sign in (button or small key icon, conditional mediation autofill in the
     * user id field) only when Passkey.js is loaded (the service runs: Api/HaveService) and the browser supports passkeys,
     * then the post sign in navigation.
     * @param {AuthPage} page The login page.
     * @param {boolean} [refresh] Probe the passkey service again (after a sign out on this page).
     * @returns {Promise<void>}
     */
    async function LoginForm(page, refresh) {
        await LoadPassKeys();
        page.Clear();
        let P = passKeys();
        if (P && P.IsAvailable && !(await P.IsAvailable(refresh)))
            P = null;
        const user = new Field(page, {
            Label: _TF("User ID", "Label for an input box where a user should enter their user id (user name, email or phone number)"),
            Placeholder: _TF("Enter your user id", "Placeholder description on an input box where the user should enter their user id"),
            AutoComplete: P ? "username webauthn" : "username",
            Name: "username",
            OnInput: () => update(),
        });
        const policyBtn = InfoButton(A.PolicyText(null));
        const pwd = new Field(page, {
            Label: _TF("Password", "Label for an input box where a user should enter their password"),
            Placeholder: _TF("Enter your password", "Placeholder description on an input box where the user should eneter their password"),
            Type: "password",
            AutoComplete: "current-password",
            Name: "password",
            LabelExtra: policyBtn.E,
            OnInput: () => update(),
        });
        //  A friendly reminder of the user's password rules (ticked as they are met, helps remembering the password). Always shown
        //  (a reminder, UX guide FRM-4), also before a user id is typed (the common policy). No layout shift while typing (LAY-9): the
        //  list sits in a reserved cell as high as the longest list of the policies seen (reserveFor below)
        const rules = el("ul.auth-rules", {
            title: _TF("Your password rules, as a reminder", "Tool tip of a checklist below a password input on the sign in page, reminding the user of the rules their password follows"),
            attrs: { "aria-label": _TF("Password rules", "Accessible name of a checklist of password rules below a password input on the sign in page") },
        });
        const rulesSlot = Sw.Reserve.Stack({ Class: "auth-rules-slot" });
        rulesSlot.append(rules);
        page.Box.append(rulesSlot);
        //  The hint next to Sign in (also the reason of a blocked Sign in, Designer N4) and the quiet leaked password line share one
        //  reserved cell (one of them shows at a time); it keeps the tallest height it had (a failure message that goes away while
        //  typing pulls nothing up). Screen readers hear the hints once typing pauses (not per key), errors at once
        const msgSlot = Sw.Reserve.Line(Sw.Reserve.Stack({ Class: "auth-msg-slot" }), { Lines: 0 });
        page.Box.append(msgSlot);
        /** The policy the reserved cells are sized for: every policy seen on this page merged (the most rules, the widest numbers). */
        let reserved = null;
        /**
         * Size the two reserved cells for the policies this page can really show (Designer N4: no empty band sized for every rule of
         * any policy): the checklist and the longest hint of the policies seen so far. The first one (the common policy, asked before the
         * form shows) sizes them before the first paint; another user's policy only ever grows them, and only when it has more rules (one
         * authorizer: the same policy for every user id, nothing moves while typing, LAY-9).
         * @param {object|null} policy The policy just received (null: the default length rules).
         */
        function reserveFor(policy) {
            const l = A.Limits(policy);
            const p = policy || {};
            const m = {
                MinLength: Math.max(reserved ? reserved.MinLength : 0, l.Min), MaxLength: Math.max(reserved ? reserved.MaxLength : 0, l.Max),
                MixedCase: !!(reserved?.MixedCase || p.MixedCase), MixedNumerical: !!(reserved?.MixedNumerical || p.MixedNumerical),
                MixedSpecial: !!(reserved?.MixedSpecial || p.MixedSpecial),
            };
            if (reserved && JSON.stringify(m) === JSON.stringify(reserved))
                return;
            reserved = m;
            //  (a too long password adds "At most 64 characters": a real state of the list, so it is in the size)
            Sw.Reserve.SetSizer(rulesSlot, el("ul.auth-rules", null, RuleItems(A.PolicyRules("x".repeat(m.MaxLength + 1), m), true)));
            //  (the status sentences of the original shown while typing, S6-D4: each rule's sentence, A.CheckPolicy of a probe password)
            const len = Math.max(1, Math.min(m.MaxLength, Math.max(m.MinLength, 3)));
            const probe = s => s.repeat(Math.ceil(len / s.length)).slice(0, len);
            const ruleTexts = ["x", "x".repeat(m.MaxLength + 1), probe("1"), probe("a"), probe("aA"), probe("aA1")].map(x => A.CheckPolicy(x, m)).filter(x => x);
            Sw.Reserve.SetSizer(msgSlot, [
                ...ruleTexts,
                _TF("Enter your user id", "Information text explaining that the user should enter their user id (user name, email or phone number)"),
                NeedsText(A.PolicyRules("", m).map(r => r.Text), ""),
                _TF("Wrong user ID or password. Please try again.", "Error message displayed under the sign in form when the user id or password is wrong"),
            ], "auth-status auth-login-status auth-warning");
        }
        reserveFor(null);
        const leak = new Status(msgSlot, { Class: "auth-leak", LiveDelay: 1200, Keep: false });
        const status = new Status(msgSlot, { Class: "auth-login-status", LiveDelay: 1200, Keep: false });
        const row = Buttons(page);
        const signIn = Button(row, {
            Text: _TF("Sign in", "Text of a button that when pressed will attempt to sign in a user using their entered credentials"),
            Title: _TF("Sign in using the entered credentials", "Tool tip description on a button that when pressed will attempt to sign in a user using their entered credentials"),
            Icon: "IconAuthPassword", Submit: true,
        }, page);
        //  The inputs are described by the hint (and the password also by the rules checklist and the leak hint)
        user.Input.setAttribute("aria-describedby", ensureId(status.E));
        /** Point the password's aria-describedby at the rules and hints. */
        const describePwd = () => pwd.Input.setAttribute("aria-describedby",
            [ensureId(rules), ensureId(status.E), ensureId(leak.E)].filter(x => x).join(" "));

        /** True while Sign in is blocked (a rule isn't met or the user id is missing). */
        let blocked = true;
        /**
         * Block / allow Sign in. A blocked button stays focusable (aria-disabled, looks disabled): activating it explains
         * why instead of doing nothing (CanSubmit below).
         * @param {boolean} on Blocked.
         */
        function setBlocked(on, reason) {
            blocked = !!on;
            signIn.SetDisabled(false);
            signIn.E.classList.toggle("auth-blocked", blocked);
            if (blocked)
                signIn.E.setAttribute("aria-disabled", "true");
            else
                signIn.E.removeAttribute("aria-disabled");
            //  What a blocked Sign in says when it is used: the page's own hint line (the click handler below, CanSubmit); the ui's disabled
            //  guard only shows it for other kinds of activation (Designer D25: while typing the checklist alone shows the unmet rules,
            //  the "needs" sentence only on an attempt)
            if (blocked && reason)
                signIn.E.setAttribute("data-sw-disabled-reason", reason);
            else
                signIn.E.removeAttribute("data-sw-disabled-reason");
            if (blocked)
                signIn.E.setAttribute("aria-describedby", ensureId(status.E));
            else
                signIn.E.removeAttribute("aria-describedby");
        }
        setBlocked(true);
        //  A blocked Sign in used (click, tap, Enter / Space on it, Enter in a field = the form's click on its submit button) explains itself
        //  in the reserved hint line above the buttons, inside the card (Designer N4: the ui guard's bubble below the button crossed the
        //  card's edge and left the reserved line empty). Caught on the row, before the button's own guard: CanSubmit says what is
        //  missing, focuses the field to fix and announces it (and lets the sign in go when autofilled values turn out to be valid).
        row.addEventListener("click", ev => {
            //  (a modifier click goes on to the guard: it only focuses, INP-2)
            if (!blocked || !signIn.E.contains(ev.target) || !Sw.IsPureEvent(ev))
                return;
            ev.preventDefault();
            ev.stopPropagation();
            page.Submit();
        }, true);

        /** The failed sign in ({Key: user + password, Text}) shown until the inputs change. */
        let failed = null;
        /** @returns {string} The key of the current inputs. */
        const inputKey = () => A.Trim(user.Get()) + "\n" + pwd.Get();
        /** The inputs of a blocked Sign in (CanSubmit): only then the hint lists the unmet rules, until the inputs change; while typing
         *  the checklist alone says them (Designer D25: the unmet rules were stated twice, in the checklist and in the hint). */
        let needsKey = null;

        /**
         * The policy cache of the form (the old loginMain passed its form element to ValidatePassword as the cache): expandos
         * PasswordPolicy, PasswordPolicyUser, PasswordPolicyStr. ValidatePassword asks the server (POST GetPasswordPolicy(user id))
         * whenever the typed user id differs from the user of the cached policy: at once on every change of the user id, as before
         * (the policy depends on the authorizer of the user, and the client can't know how many authorizers the server has).
         */
        const policyCache = {};
        /**
         * Show the rules checklist (always shown, a reminder): met rules are ticked, unmet ones are highlighted once a password is
         * typed. Only the marks and colours change (LAY-9).
         * @param {Array<{Text:string, Met:boolean}>} list The rules (A.PolicyRules).
         * @param {boolean} typed True if a password is entered.
         */
        function showRules(list, typed) {
            describePwd();
            rules.replaceChildren(...RuleItems(list, typed));
        }
        showRules(A.PolicyRules("", null), false);

        /** @type {number} Sequence number of the newest validation (only the newest one updates the form, as the old setStatus). */
        let statusSeq = 0;
        /** @type {Promise<boolean>|null} The newest validation. */
        let statusPromise = null;

        /**
         * Validate the inputs (sequenced: an older call that finishes later answers with the newest one's result).
         * @returns {Promise<boolean>} True when sign in is possible.
         */
        function update() {
            const seq = ++statusSeq;
            const p = updateStatus(seq);
            statusPromise = p;
            return p;
        }

        /**
         * Validate the inputs, as the old sign in did: the user's password policy is awaited (Sw.Auth ValidatePassword with the form's
         * cache: a request when the user id changed, also for an empty user id: the common policy), then a user id and a password that
         * follows that policy are needed (a failed policy request gives no policy: only the blank and length rules). The rules are a
         * friendly checklist (ticked as they are met); while one isn't met Sign in is blocked and all unmet rules are listed next to
         * the button (never a silently disabled button: it stays focusable and explains itself, see CanSubmit). Autofilled but not yet
         * exposed values count as valid. When valid shows the last failure (inputs unchanged) or the leak check ("Checking password ..",
         * then its result; it never blocks).
         * @param {number} seq The sequence number of this call (update).
         * @returns {Promise<boolean>} True when sign in is possible.
         */
        async function updateStatus(seq) {
            if (!page.Box.contains(user.E))
                return false;
            const u = A.Trim(user.Get());
            const p = A.Trim(pwd.Get());
            const err = await A.ValidatePassword(u, p, policyCache, newPolicy => {
                policyBtn.InfoText = A.PolicyText(newPolicy);
                reserveFor(newPolicy);
            });
            if (seq !== statusSeq)
                return await statusPromise;
            if (!page.Box.contains(user.E))
                return false;
            const policy = policyCache.PasswordPolicy || null;
            if (!p && IsAutoFilled(pwd.Input) && (u || IsAutoFilled(user.Input))) {
                user.SetInvalid(false);
                pwd.SetInvalid(false);
                showRules(A.PolicyRules("", policy), false);
                status.Clear();
                leak.Clear();
                setBlocked(false);
                return true;
            }
            if (!u) {
                user.SetInvalid(user.Touched);
                pwd.SetInvalid(false);
                showRules(A.PolicyRules(p, policy), !!p);
                leak.Clear();
                //  The original's status sentence, always shown (S6-D4: the slot is never an empty hole): neutral at rest, an error
                //  once the field was used
                status.Set(_TF("Enter your user id", "Information text explaining that the user should enter their user id (user name, email or phone number)"), user.Touched ? "error" : null);
                setBlocked(true, _TF("Enter your user id", "Information text explaining that the user should enter their user id (user name, email or phone number)"));
                return false;
            }
            user.SetInvalid(false);
            const list = A.PolicyRules(p, policy);
            showRules(list, !!p);
            if (err) {
                pwd.SetInvalid(false);
                leak.Clear();
                const missing = list.filter(r => !r.Met).map(r => r.Text);
                if (!p)
                    status.Set(_TF("Enter your password", "Information text explaining that the user should enter their password"));
                else if (needsKey === inputKey())
                    status.Set(NeedsText(missing, err), "warning", { Title: err });
                else
                    //  while typing: the original's sentence of the first unmet rule (S6-D4, ex: "Password must be at least 8 characters!"),
                    //  quiet (the checklist marks the rule); a blocked Sign in lists them all (above)
                    status.Set(err);
            }
            else if (failed && failed.Key === inputKey()) {
                pwd.SetInvalid(true);
                status.Set(failed.Text, "error", { Title: failed.Title });
                //  As the old sign in after a failure: the leak check runs again at once (a new request, the line was cleared by the sign
                //  in); its result is shown when the failure text goes (5 s, the old message time) or the inputs change
                leak.Leak(p);
            }
            else {
                failed = null;
                pwd.SetInvalid(false);
                status.Clear();
                leak.Leak(p);
            }
            //  (one text at a time in the shared cell: the leak line waits, hidden by opacity, while the failure shows)
            msgSlot.classList.toggle("auth-failing", !err && !!failed && failed.Key === inputKey());
            setBlocked(!!err, err
                ? (p ? NeedsText(list.filter(r => !r.Met).map(r => r.Text), err) : _TF("Enter your password", "Information text explaining that the user should enter their password"))
                : null);
            return !err;
        }
        user.Input.addEventListener("blur", () => {
            user.Touched = true;
            update();
        });

        /** Passkey state: autofill controller, pre-fetched any-passkey challenge, the button. */
        let autofill = null;
        let anyKey = null;
        let keyButton = null;

        /**
         * Signed in: show it, lock the form and navigate.
         * @returns {Promise<void>}
         */
        async function signedIn() {
            leak.Clear();
            status.Set(_TF("User signed in.", "Text displayed when a user have successfully signed in"), "ok");
            signIn.SetDisabled(true, waitReason());
            keyButton?.SetDisabled(true, waitReason());
            autofill?.Stop();
            page.SetBusy(true);
            page.Running = true;
            await A.StartPage();
        }

        /**
         * Show why a sign in failed, inline under the form, for 5 s (the time the old sign in showed its "Failed to login." message)
         * or until the inputs change; then the password leak check result (checked again at once, as before) shows.
         * @param {string} text The reason.
         * @param {string} [title] Details (tooltip).
         * @returns {Promise<boolean>} False if the page switched to the "already signed in" view.
         */
        async function showFailure(text, title) {
            //  The server refuses a sign in while a user is signed in (here or in another window). Asked only when this browser may
            //  have a signed in user (Sw.Auth.MaybeSignedIn): a wrong password sends no extra request, as the original
            const who = A.MaybeSignedIn() ? await A.GetUser().catch(() => null) : null;
            if (who) {
                autofill?.Stop();
                SignedInView(page, who);
                return false;
            }
            const f = failed = { Key: inputKey(), Text: text, Title: title || "" };
            setTimeout(() => {
                if (failed !== f)
                    return;
                failed = null;
                update();
            }, 5000);
            await update();
            if (!failed) {
                //  Not valid any more (ex: autofill exposed other values): still show the reason once
                status.Set(text, "error", { Title: title });
            }
            return true;
        }

        //  A blocked Sign in (click, Enter) explains itself: focus the field to fix and announce the hint now
        page.CanSubmit = async () => {
            await update();
            if (!blocked)
                return true;
            //  (the blocked Sign in says what is missing, once: the hint lists the unmet rules until the inputs change)
            if (needsKey !== inputKey()) {
                needsKey = inputKey();
                await update();
            }
            (A.Trim(user.Get()) ? pwd : user).Focus();
            status.E.classList.remove("auth-attention");
            void status.E.offsetWidth;
            status.E.classList.add("auth-attention");
            status.Announce();
            //  The reason in view (S6-D3: at 400 % zoom it was below the fold while the focus went to the field; the original scrolled
            //  its message into view): after the field's own scroll, the nearest scroll that shows the whole reason
            requestAnimationFrame(() => {
                const r = status.E.getBoundingClientRect();
                if (r.height && (r.top < 0 || r.bottom > window.innerHeight))
                    status.E.scrollIntoView({ block: "nearest" });
            });
            return false;
        };
        page.OnSubmit = async () => {
            if (!A.Trim(user.Get()) || !A.Trim(pwd.Get())) {
                //  Autofilled values may be exposed only now
                await update();
                if (!A.Trim(user.Get()) || !A.Trim(pwd.Get())) {
                    (A.Trim(user.Get()) ? pwd : user).Focus();
                    return;
                }
            }
            keyButton?.SetDisabled(true, waitReason());
            leak.Clear();
            const u = user.Get();
            status.Set(_T('Signing in "{0}" ..', A.Trim(u), "Text displayed when a user is trying to sign in.{0} is replaced with the user id (user name, email or phone number)"), "info");
            let ok = false;
            let error = null;
            let mustChange = null;
            let disabled = false;
            try {
                const res = await A.Login(u, pwd.Get());
                ok = !!(res && res.Succeeded);
                if (!ok && res && res.PasswordChangeRequired)
                    mustChange = res;
                //  (the password was right, but an administrator disabled the account: said only to someone who knows the password)
                disabled = !ok && !!(res && res.AccountDisabled);
            }
            catch (e) {
                error = e;
            }
            if (ok) {
                await signedIn();
                return;
            }
            keyButton?.SetDisabled(false);
            //  the password is right but must be changed first (an administrator forced a password reset): no session, the
            //  server sent a reset link and a code (the code works in this session), continue with the code to auth/ResetPassword.html
            if (mustChange) {
                autofill?.Stop();
                leak.Clear();
                const sent = (mustChange.SentTo || []).filter(x => x);
                if (sent.length) {
                    ShortCode(page, {
                        Html: _TH("You must choose a new password before you can sign in. A code was sent to {0}.", UserHtml(sent), "Message on the sign in page when the password was right but an administrator asked the user to choose a new one.{0} is replaced with a list of email addresses and/or phone numbers"),
                        Link: "ResetPassword.html?",
                        ButtonText: _TF("Choose a new password", "Text of the button on the sign in page that continues with the sent code to choose a new password"),
                    });
                    return;
                }
                await showFailure(_TF("You must choose a new password before you can sign in, but no message could be sent to you. Ask an administrator for help.", "Error message on the sign in page when the user must choose a new password and the reset message could not be sent"));
                return;
            }
            if (disabled) {
                await showFailure(_TF("This account is disabled. Contact the administrator of this site.", "Error message on the sign in page when the user id and password are right but an administrator disabled the account"));
                return;
            }
            const shown = error
                ? await showFailure(_TF("Sign in failed, please try again.", "Error message displayed under the sign in form when the sign in request failed (not a wrong password)"), String(error.message || error))
                : await showFailure(_TF("Wrong user ID or password. Please try again.", "Error message displayed under the sign in form when the user id or password is wrong"));
            if (shown)
                setTimeout(() => pwd.Focus(true), 0);
        };

        if (P) {
            /**
             * Show a failed passkey sign in inline (nothing for a cancelled one).
             * @param {object} res The result ({Error, Message}).
             */
            const passKeyFailed = res => {
                if (res?.Error == P.Errors.Cancelled)
                    return;
                leak.Clear();
                status.Set(P.ErrorText(res), "error", { Title: res?.Message || "" });
            };
            /**
             * Sign in with a passkey (the button / icon): only passkeys of the typed user when a user id is entered.
             * @returns {Promise<void>}
             */
            const usePassKey = async () => {
                autofill?.Stop();
                signIn.SetDisabled(true, waitReason());
                leak.Clear();
                status.Set(_TF("Select your passkey ..", "Text explaining to the user that he should select their passkey to continue sign in"), "info");
                let res = null;
                try {
                    const u = A.Trim(user.Get());
                    res = await (u ? P.SignIn(u) : anyKey).Run();
                    if (res.Error == 0) {
                        await signedIn();
                        return;
                    }
                }
                catch (e) {
                    res = { Error: P.Errors.VerificationFailed, Message: String(e.message || e) };
                }
                anyKey.Fetch().catch(() => { });
                autofill?.Start();
                await update();
                passKeyFailed(res);
            };
            anyKey = P.SignIn();
            keyButton = P.HaveHint()
                ? Button(row, {
                    Text: _TF("Use passkey", "Text on a button that when pressed will attempt to sign in using a passkey"),
                    Title: _TF("Sign in using a passkey (if a user id is entered, only passkeys of that user are shown)", "Tool tip description of a button that when pressed will attempt to sign in using a passkey"),
                    Icon: "IconUsePasskey",
                    OnClick: () => page.Run(usePassKey),
                })
                : Button(row, {
                    //  a visible label (an icon only key button did not say what it does)
                    Text: _TF("Passkey", "Short text of a small button on the sign in page that signs in with a passkey (next to Sign in)"),
                    Icon: "IconPasskeyKey", Kind: "ghost", Class: "auth-passkey-icon",
                    Title: _TF("Sign in with a passkey, ex: on a phone or a security key (if a user id is entered, only passkeys of that user can be used)", "Tool tip description of a small icon button that when pressed will attempt to sign in using a passkey"),
                    OnClick: () => page.Run(usePassKey),
                });
            //  Pre-fetch the challenge so the browser prompt opens directly from the click (Safari); autofill starts only
            //  after a working challenge (one request when the service fails), a failing service hides the button
            anyKey.Fetch().then(c => {
                if (c.error == P.Errors.NotSupported) {
                    keyButton.E.hidden = true;
                    return false;
                }
                return P.IsAutofillSupported();
            }).then(ok => {
                if (!ok || !page.Box.contains(keyButton.E))
                    return;
                autofill = P.Autofill(async res => {
                    if (res.Error == 0) {
                        signedIn();
                        return;
                    }
                    passKeyFailed(res);
                });
            }).catch(() => {
                keyButton.E.hidden = true;
            });
        }
        await update();
        user.Focus();
        //  Browsers may autofill after load without an event (or without exposing the values): re-check for a while
        let last = "";
        for (const ms of [100, 250, 500, 1000, 2000, 3500, 5000]) {
            setTimeout(() => {
                const s = String(IsAutoFilled(user.Input)) + IsAutoFilled(pwd.Input) + user.Get().length + "," + pwd.Get().length;
                if (s === last || page.Running || !page.Box.contains(user.E))
                    return;
                last = s;
                update();
            }, ms);
        }
    }

    // ------------------------------------------------------------------ logout page

    /**
     * Entry point of auth/Logout.html: signs out at once ("Signing out .."), shows the result; "No user is signed in!"
     * when nobody is; a Retry button on failure. In the app shell the whole shell reloads right after the sign out (Sw.Session
     * SignedOut, and the server's user.logout push), as do the other windows; stand-alone this page stays and shows the result.
     * @returns {Promise<void>}
     */
    async function LogoutMain() {
        await Page("IconAuthLogout", async page => {
            const status = new Status(page, { Class: "auth-big" });
            const retry = Button(Buttons(page), {
                Text: _TF("Retry", "Text on a button that when pressed will retry an operation that failed"),
                Title: _TF("Retry sign out", "Tool tip description on a button that when pressed will retry to sign out the logged in user"),
                Icon: "IconRetry", Submit: true, Hidden: true,
            }, page);
            /**
             * One sign out attempt.
             * @returns {Promise<void>}
             */
            async function tryOnce() {
                status.Set(_TF("Signing out ..", "Text indicating that the user is being signed out"), "info");
                Hide(status, retry);
                try {
                    let signedIn = true;
                    let who;
                    try {
                        who = await A.GetUser();
                        signedIn = !!who;
                    }
                    catch {
                    }
                    //  (the result in one place, UX guide FBK-5: the status line next to Retry, no toast repeating it)
                    if (!signedIn)
                        status.Set(_TF("No user is signed in!", "Error text indicating that a sign out can't be performed since no user is signed in"), "warning");
                    else if (await A.Logout(who)) {
                        status.Set(_TF("Signed out successfully!", "Text explaining that the user was signed out correctly"), "ok");
                        return;
                    }
                    else
                        status.Set(_TF("Failed to sign out!", "Error text indicating that the sign out request failed"), "error");
                }
                catch (e) {
                    const m = _TF("Failed to sign out!", "Error text indicating that the sign out request failed");
                    status.Set(m + " " + (e && e.message ? e.message : String(e)), "error");
                }
                retry.E.hidden = false;
            }
            page.OnSubmit = tryOnce;
            page.Loaded();
            await tryOnce();
        }, { Dialog: false });
    }

    /**
     * @namespace Sw.AuthUi - the auth page framework and widgets (see the file header).
     */
    Sw.AuthUi = {
        AuthPage,
        Page,
        Hide,
        Text,
        Heading,
        SetContent,
        Buttons,
        Button,
        UserHtml,
        Status,
        Field,
        PhoneField,
        Rules,
        InfoButton,
        Segments,
        ShortCode,
        IsAutoFilled,
        AppName,
        Brand,
        LoginMain,
        LogoutMain,
        LoadPassKeys,
        CheckLinkToken,
        LinkGone,
        IsLinkGone,
        Done,
    };

    window.loginMain = LoginMain;
    window.logoutMain = LogoutMain;

    // ------------------------------------------------------------------ legacy globals (old AuthUi.js api, for other projects)

    /**
     * Define a legacy global unless already defined.
     * @param {string} name Name.
     * @param {*} value Value.
     */
    function legacy(name, value) {
        if (!(name in window))
            window[name] = value;
    }

    /**
     * Legacy wrapper of a Sw.Button with the old member names.
     * @param {Sw.Button} b The button.
     * @returns {Sw.Button} The same button with Element, SetEnabled, StartWorking, StopWorking.
     */
    function legacyButton(b) {
        b.Element = b.E;
        b.SetEnabled = (on, reason) => b.SetDisabled(!on, reason || _TF("Not available yet: fill in the field first", "Reason of a disabled button of a legacy auth form field (old api)"));
        b.StartWorking = () => b.SetWorking(true);
        b.StopWorking = () => b.SetWorking(false);
        return b;
    }

    /**
     * Legacy input factory: a Field returning its input with the old expandos (OnChange, Field).
     * @param {HTMLElement} target Parent.
     * @param {object} o Field options.
     * @param {function(HTMLInputElement):*} [onChange] Old change callback.
     * @param {function(HTMLElement):*} [onNext] Old Enter callback (gets the next focusable element).
     * @param {function} [cls] Field class.
     * @returns {HTMLInputElement} The input.
     */
    function legacyInput(target, o, onChange, onNext, cls) {
        const C = cls || Field;
        const f = new C(target, Object.assign(o, { OnInput: () => onChange && onChange(f.Input) }));
        const i = f.Input;
        i.Field = f;
        i.OnChange = async () => onChange && await onChange(i);
        if (onNext)
            i.addEventListener("keydown", e => {
                if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                    e.preventDefault();
                    onNext(Sw.Focus.Next(i) || i);
                }
            });
        return i;
    }

    legacy("CreateAuthWrapper", () => el("form.auth-box.auth-inline.SysWeaver-AuthBox", { attrs: { novalidate: "" }, on: { submit: e => e.preventDefault() } }));
    legacy("AuthPage", (icon, build, iconIsFile) => Page(icon, async page => {
        const b = page.Box;
        b.OnLoaded = () => page.Loaded();
        const img = { Element: page.IconE, ChangeImage: i => page.SetIcon(i) };
        if (build)
            await build(b, img);
    }, { IconIsFile: iconIsFile }));
    legacy("AuthText", (t, text, title, isHtml) => Text(t, text, { Title: title, Html: isHtml }));
    legacy("AuthLabel", (t, text, title, isHtml) => Heading(t, text, title, isHtml));
    legacy("AuthError", (t, text, title) => {
        const s = new Status(t, { Title: title, Copy: "always" });
        s.Set(text);
        return s.E;
    });
    legacy("AuthSetText", (e, text, isHtml) => {
        e.className = e.className.split(" ").filter(c => c.startsWith("auth-status") || c === "sw-copyable").join(" ");
        e.LastCheck = null;
        if (!text)
            e.innerHTML = "&nbsp;";
        else
            SetContent(e, text, isHtml);
    });
    legacy("AuthSetError", (e, on) => {
        if (e.Field)
            e.Field.SetInvalid(on);
        else
            e.classList.toggle("Error", !!on);
    });
    legacy("AuthUserHtml", UserHtml);
    legacy("AuthButtonRow", t => Buttons(t));
    legacy("AuthButton", (t, name, title, icon, onClick, disabled) => {
        const b = legacyButton(new Sw.Button({ Text: name, Title: title, Icon: icon, Disabled: !!disabled, OnClick: () => onClick(b) }));
        t.append(b.E);
        return b;
    });
    legacy("AuthHr", t => box(t).appendChild(el("hr.auth-hr")));
    legacy("AuthBr", t => box(t).appendChild(el("br")));
    legacy("AuthInput", (t, ph, title, ac, onChange, onNext, iconClass) =>
        legacyInput(t, { Placeholder: ph, Hint: title, AutoComplete: ac, Copy: iconClass !== "-" }, onChange, onNext));
    legacy("AuthPassword", (t, ph, title, ac, onChange, onNext, iconClass) =>
        legacyInput(t, { Placeholder: ph, Hint: title, AutoComplete: ac || "current-password", Type: "password", Reveal: iconClass !== "-" }, onChange, onNext));
    legacy("AuthNewPassword", (t, ph, title, ac, onChange, onNext, policy) =>
        legacyInput(t, { Placeholder: ph, Hint: title, AutoComplete: ac || "new-password", Type: "password", Generate: policy || true }, onChange, onNext));
    legacy("AuthInputPhone", (t, ph, title, ac, onChange, onNext) =>
        legacyInput(t, { Placeholder: ph, Hint: title }, onChange, onNext, PhoneField));
    legacy("AuthAddIcon", (e, iconClass, iconFn, iconEnabledFn, iconTitle) => {
        const b = legacyButton(new Sw.Button({ Icon: iconClass, Kind: "ghost", Small: true, Title: iconTitle, OnClick: () => iconFn(e) }));
        const host = e.closest && e.closest(".sw-text-field");
        (host || e.parentElement).append(b.E);
        const upd = () => b.SetDisabled(iconEnabledFn ? !iconEnabledFn(e) : false, _TF("Not available yet: fill in the field first", "Reason of a disabled button of a legacy auth form field (old api)"));
        e.addEventListener("input", upd);
        upd();
        e.Icon = e.Icon || b;
        return b;
    });
    legacy("AuthTab", (t, title, onChange) => {
        const s = new Segments(t, { Label: title, OnChange: onChange });
        const e = s.E;
        e.Add = (text, tt, isHtml, icon) => s.Add(text, icon, tt);
        e.SetIndex = (i, noFn) => s.Select(i, noFn);
        Object.defineProperty(e, "Selected", { get: () => s.Selected });
        return e;
    });
    legacy("AuthPageValidation", (boxEl, html, link, timeout, label, ph, buttonText) => {
        const page = boxEl.AuthPage;
        if (!page)
            throw new Error("AuthPageValidation: not an AuthPage box");
        return ShortCode(page, { Html: html, Link: link, Label: label, Placeholder: ph, ButtonText: buttonText });
    });
    legacy("IsAutoFilled", IsAutoFilled);
})();
