/**
 * @file The "Fake Mail" page (fakeEmail/index.html), the web inbox of FakeEmailService (menu link "Debug/FakeEmail";
 * works inside the app shell and stand alone).
 *
 * Loading: `<script defer src="email.js">` after ../sw/core.js and ../sw/ui.js; started by
 * `<body onload="fakeEmailMain()">`. Uses the Sw core (`Sw.Net` calls, `Sw.Url`, `Sw.Storage`, `Sw.Theme`,
 * `Sw.Page` loading protocol) and ui (`Sw.Dialog` composer / confirm, `Sw.Menu` theme menu, `Sw.Info` / `Sw.Fail`
 * toasts, `Sw.KeyClick`), and the SysWeaver themes (the theme button opens the shared theme list: the server Theme menu plus the app themes).
 *
 * Globals defined: `fakeEmailMain` (everything else is private).
 * Server api (POST JSON, never cached): `<api>GetEmails {Version}` (long poll: `{Version, From, Unchanged}` or
 * `{Version, From, Emails[]}`), `GetEmail {Id}`, `DeleteEmail {Id}`, `Clear`, `SendEmail`, `FakeIncoming`
 * (`{From, SenderName, To, Subject, Body, IsHtml, System, InReplyTo}` -> id). `<api>` = `?api=` url parameter
 * (normalized to end with "/"; only an url of this site, `Sw.Url.SafeSameOrigin`, else ignored) or "../Api/fakeEmail/".
 * Url hash: `#<email id>` selects that email after the first list load and follows the selection.
 * Storage (localStorage, JSON): `fakeEmail.read` (read ids), `fakeEmail.lastFrom`, `fakeEmail.lastName` (composer);
 * the old page theme `fakeEmail.theme` is migrated once to the SysWeaver theme and removed.
 * Keyboard (outside inputs, no dialog open, no modifiers): j / ArrowDown next, k / ArrowUp previous, r reply,
 * c compose, / search, Delete delete (after the danger confirmation), Escape deselect; Escape in an input blurs it; Ctrl/Cmd+Enter sends.
 * Drafts: the composer message (and the subject of a new email) are Sw.Draft texts ("fakeEmail.compose.*", "fakeEmail.reply.<id>.body").
 */
(function () {
    "use strict";

    /** The api base url (`?api=` override, always ending with "/"; only an url of this site, else the default is used). */
    const Api = (() => {
        const a = Sw.Url.Get("api");
        if (a && !Sw.Url.SafeSameOrigin(a))
            console.warn("fakeEmail: ignored the api parameter (not an url of this site)");
        return (a && Sw.Url.SafeSameOrigin(a) ? a : "../Api/fakeEmail/").replace(/\/?$/, "/");
    })();

    /** Storage key prefix. */
    const KeyPrefix = "fakeEmail.";

    /**
     * Shorthand for document.getElementById.
     * @param {string} id - Element id (index.html).
     * @returns {HTMLElement|null} The element.
     */
    const $ = id => document.getElementById(id);

    /**
     * The page state.
     * - version: last GetEmails Version; from: the system sender address; emails / byId: the summaries (server order);
     * - folder: "all" | "out" | "in" | "failed"; system: null = all, "" = no system; query: lower-cased search;
     * - selected: selected id; view: "render" | "source"; current: the full email in the reader;
     * - known: ids at the last list render (null before the first list); read: read ids;
     * - composer: the open composer dialog handle (or null); mode / format: composer state; title: original title;
     * - list: the email list (Sw.SelectList); ld: the list / reader layout (Sw.ListDetail: side by side, stacked on phones);
     * - composeForm: the composer checks (Sw.Form); drafts: the Sw.Draft handles of the open composer; sending: a send runs.
     */
    const State = {
        version: 0,
        from: "",
        emails: [],
        byId: new Map(),
        folder: "all",
        system: null,
        query: "",
        selected: null,
        view: "render",
        current: null,
        known: null,
        read: new Set(),
        composer: null,
        list: null,
        ld: null,
        composeForm: null,
        drafts: [],
        sending: false,
        mode: "in",
        format: "text",
        title: "",
    };

    /**
     * Folder definitions (title, predicate, empty text), keyed like li[data-folder] / span[data-count].
     * @returns {Object<string, {title:string, test:function(object):boolean, empty:string}>} Folders.
     */
    function folders() {
        return {
            all: { title: _T("All mail", "Name of a folder in a fake email inbox that shows all emails"), test: () => true, empty: _T("No emails yet", "Text shown in a fake email inbox when there are no emails") },
            out: { title: _T("Sent by system", "Name of a folder in a fake email inbox with the emails that the system sent"), test: e => !e.Incoming, empty: _T("The system hasn't sent any emails yet", "Text shown in a fake email inbox when the system has not sent any emails") },
            in: { title: _T("Received by system", "Name of a folder in a fake email inbox with the emails that the system received"), test: e => !!e.Incoming, empty: _T("No emails have been faked yet", "Text shown in a fake email inbox when no incoming emails were faked") },
            failed: { title: _T("Failed", "Name of a folder in a fake email inbox with the emails that failed"), test: e => !!e.Error, empty: _T("No failed emails", "Text shown in a fake email inbox when no email failed") },
        };
    }

    /** @type {Object<string, {title:string, test:function(object):boolean, empty:string}>} Folders (created on start). */
    let Folders = null;

    // ------------------------------------------------------------------ storage, api, helpers

    /**
     * Read a stored JSON value ("fakeEmail." + key).
     * @param {string} key - Key suffix.
     * @param {*} def - Default.
     * @returns {*} Value or def.
     */
    function load(key, def) {
        return Sw.Storage.Local.GetJson(KeyPrefix + key, def);
    }

    /**
     * Store a JSON value ("fakeEmail." + key).
     * @param {string} key - Key suffix.
     * @param {*} value - Value.
     */
    function save(key, value) {
        Sw.Storage.Local.SetJson(KeyPrefix + key, value);
    }

    /**
     * Call an api method (always POST, never cached).
     * @param {string} method - Method name.
     * @param {*} [data] - Request (null when missing).
     * @returns {Promise<*>} The decoded response (null for an empty body).
     * @throws {Error} Sw.Net.Error (the server's text in `Text`).
     */
    async function call(method, data) {
        const r = await Sw.Net.Send(Api + method, data ?? null, { Post: true, Reload: true });
        return r === undefined ? null : r;
    }

    /**
     * A displayable error text (the server's text/plain message when there is one).
     * @param {*} e - Error.
     * @returns {string} Text.
     */
    function errText(e) {
        if (e && e.Text)
            return e.Text;
        return (e && e.message) ? e.message : String(e);
    }

    /**
     * Create an element.
     * @param {string} tag - Tag name.
     * @param {?string} [cls] - Class name(s).
     * @param {*} [text] - Text content.
     * @returns {HTMLElement} The element.
     */
    function el(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls)
            e.className = cls;
        if (text != null)
            e.textContent = text;
        return e;
    }

    /**
     * A deterministic hue of a string (avatar colors).
     * @param {string} s - Text.
     * @returns {number} 0..359.
     */
    function hue(s) {
        let h = 0;
        for (let i = 0; i < s.length; ++i)
            h = (h * 31 + s.charCodeAt(i)) | 0;
        return Math.abs(h) % 360;
    }

    /**
     * The user side of an email (From of incoming mail, To of outgoing mail).
     * @param {object} e - Email.
     * @returns {string} Address.
     */
    function party(e) {
        return e.Incoming ? (e.From || "") : (e.To || "");
    }

    /**
     * Display name of the user side (SenderName of incoming mail, else the address).
     * @param {object} e - Email.
     * @returns {string} Name.
     */
    function displayName(e) {
        return (e.Incoming && e.SenderName) ? e.SenderName : party(e);
    }

    /**
     * A round letter avatar colored by the user address.
     * @param {object} e - Email.
     * @param {boolean} [big] - The large reader avatar.
     * @returns {HTMLElement} The avatar.
     */
    function avatar(e, big) {
        const a = el("div", "avatar" + (big ? " big" : ""), (displayName(e) || "?").trim().charAt(0) || "?");
        a.setAttribute("aria-hidden", "true");
        //  SwWeb:allow-design (the avatar colour is data: a hue derived from the address)
        a.style.background = "hsl(" + hue(party(e).toLowerCase()) + " 55% 45%)";
        return a;
    }

    /**
     * List time stamp: time for today, else the date (with the year when not this year).
     * @param {Date} d - Date.
     * @returns {string} Text.
     */
    function shortTime(d) {
        const now = new Date();
        if (d.toDateString() === now.toDateString())
            return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        return d.toLocaleDateString([], d.getFullYear() === now.getFullYear() ? { month: "short", day: "numeric" } : { year: "numeric", month: "short", day: "numeric" });
    }

    /**
     * Long time stamp.
     * @param {Date} d - Date.
     * @returns {string} Text.
     */
    function longTime(d) {
        return d.toLocaleString([], { dateStyle: "medium", timeStyle: "medium" });
    }

    /**
     * Append text with every (case insensitive) occurrence of the query wrapped in a mark.
     * @param {HTMLElement} parent - Target.
     * @param {string} text - Text.
     * @param {string} query - Lower-cased query ("" = none).
     */
    function appendHighlighted(parent, text, query) {
        text = text || "";
        if (!query) {
            parent.append(text);
            return;
        }
        const lt = text.toLowerCase();
        let p = 0;
        for (; ;) {
            const i = lt.indexOf(query, p);
            if (i < 0)
                break;
            parent.append(text.substring(p, i), el("mark", null, text.substring(i, i + query.length)));
            p = i + query.length;
        }
        parent.append(text.substring(p));
    }

    /**
     * Append plain text with http(s) urls turned into links (new tab).
     * @param {HTMLElement} parent - Target.
     * @param {string} text - Text.
     */
    function appendLinkified(parent, text) {
        const re = /\bhttps?:\/\/[^\s<>"']+[^\s<>"'.,;:!?)\]]/gi;
        let p = 0;
        for (const m of text.matchAll(re)) {
            parent.append(text.substring(p, m.index));
            const a = el("a", null, m[0]);
            a.href = m[0];
            a.target = "_blank";
            a.rel = "noopener noreferrer";
            parent.append(a);
            p = m.index + m[0].length;
        }
        parent.append(text.substring(p));
    }

    /**
     * Content security policy put first in every email document: no scripts, frames, objects, fetches or form posts,
     * only inline styles and data / http(s) images. A second guard next to the iframe sandbox.
     * Http images are allowed because this is a development inbox: mail templates link their images absolutely from the sending
     * server ("[@Root]icon-180.png"), and a local test server runs on http (ex: http://localhost:8211/), a real mail client loads them the same way.
     */
    const EmailCsp = "default-src 'none'; img-src data: http: https:; style-src 'unsafe-inline'; form-action 'none'";

    /**
     * Escape text for a double quoted html attribute or element text.
     * @param {string} s - Text.
     * @returns {string} Escaped text.
     */
    function escAttr(s) {
        return String(s).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    /**
     * Wrap an html body in a document for a sandboxed frame (links open in a new tab, light document style).
     * Starts with a content security policy that blocks scripts (see EmailCsp).
     * @param {string} body - Html.
     * @returns {string} Document text.
     */
    function frameHtml(body) {
        //  SwWeb:allow-design (the e-mail document itself: white like a mail client)
        return "<!DOCTYPE html><meta http-equiv=\"Content-Security-Policy\" content=\"" + EmailCsp + "\"><meta charset=\"utf-8\"><base target=\"_blank\"><style>body{font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;margin:16px;color:#1d2330;background:#fff;word-break:break-word}img{max-width:100%}</style>" + (body || "");
    }

    /**
     * A stand alone viewer document for a new tab: the email document (frameHtml) inside a full size iframe with the
     * same sandbox as the inline preview (no scripts, unique origin), and a script free outer page.
     * A blob url has the app's origin, so the email must never be the blob document itself.
     * @param {string} body - Html.
     * @param {string} title - Tab title (the subject).
     * @returns {string} Document text.
     */
    function viewerHtml(body, title) {
        return "<!DOCTYPE html><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; frame-src 'self'\"><meta charset=\"utf-8\"><title>" + escAttr(title || "") + "</title>"
            //  SwWeb:allow-design (the e-mail document frame)
            + "<style>html,body{margin:0;height:100%;background:#fff}iframe{position:fixed;inset:0;width:100%;height:100%;border:0}</style>"
            + "<iframe sandbox=\"allow-popups allow-popups-to-escape-sandbox\" title=\"" + escAttr(_T("Email body", "Accessible name of the frame that shows the body of an email")) + "\" srcdoc=\"" + escAttr(frameHtml(body)) + "\"></iframe>";
    }

    // ------------------------------------------------------------------ list and sidebar

    /**
     * The list filter (folder, system, search).
     * @param {object} e - Email summary.
     * @returns {boolean} True when shown.
     */
    function matches(e) {
        if (!Folders[State.folder].test(e))
            return false;
        if (State.system !== null && (e.System || "") !== State.system)
            return false;
        const q = State.query;
        return !q || [e.From, e.To, e.SenderName, e.Subject, e.Preview, e.System].some(x => x && x.toLowerCase().includes(q));
    }

    /** @returns {object[]} The emails shown in the list. */
    function visible() {
        return State.emails.filter(matches);
    }

    /**
     * Make a sidebar entry keyboard operable (Enter / Space) and mark the active one.
     * @param {HTMLElement} li - Entry.
     * @param {boolean} on - Active.
     * @param {function():void} fn - Action.
     */
    function sideEntry(li, on, fn) {
        li.classList.toggle("on", on);
        li.setAttribute("aria-current", on ? "true" : "false");
        if (!li.SwSide) {
            li.SwSide = true;
            li.setAttribute("role", "button");
            Sw.KeyClick(li);
            //  The icon rail (721..1100 px) hides the labels: the name (title) shows on long press too
            Sw.Hint && Sw.Hint.Attach(li);
            li.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                li.SwSideFn();
            });
        }
        li.SwSideFn = fn;
    }

    /**
     * Render the sidebar: folder counters (unread count, else total) and the system filter list.
     */
    function renderSide() {
        const unread = e => !State.read.has(e.Id);
        for (const [key, f] of Object.entries(Folders)) {
            const all = State.emails.filter(f.test);
            const u = all.filter(unread).length;
            const c = document.querySelector("[data-count=\"" + key + "\"]");
            c.textContent = u > 0 ? u : (all.length || "");
            c.classList.toggle("unread", u > 0);
            const tip = _T("{0} unread of {1}", u, all.length, "Tool tip of an email folder counter.{0} is replaced with the number of unread emails.{1} is replaced with the total number of emails in the folder");
            c.title = tip;
            c.parentElement.title = f.title + " - " + tip;
            //  Also the name in the icon rail, where the label is hidden
            c.parentElement.setAttribute("aria-label", c.parentElement.title);
        }
        for (const li of $("folders").children)
            sideEntry(li, li.dataset.folder === State.folder, () => {
                State.folder = li.dataset.folder;
                renderAll();
            });

        const systems = [...new Set(State.emails.map(e => e.System || ""))].sort();
        const box = $("systemsBox");
        box.hidden = systems.length < 2 && !(systems.length === 1 && systems[0]);
        const ul = $("systems");
        ul.replaceChildren();
        if (box.hidden)
            return;
        for (const s of systems) {
            const li = el("li");
            li.append(el("span", "dot"), el("span", "label", s || _T("(no system)", "Name of the filter in a fake email inbox for emails without a system")));
            sideEntry(li, s === State.system, () => {
                State.system = State.system === s ? null : s;
                renderAll();
            });
            ul.append(li);
        }
    }

    /**
     * Rebuild the email list (rows: avatar, direction, name, failed tag, time, subject, preview; classes sel, unread,
     * fresh) and its header.
     */
    function renderList() {
        const items = visible();
        const q = State.query;
        const fresh = State.known;
        //  The shared list (Sw.SelectList): keyboard, selection and the empty state; the rows keep the mail look (renderRow)
        State.list.SetItems(items.map(e => ({
            Key: e.Id,
            Class: "item" + (State.read.has(e.Id) ? "" : " unread") + (fresh && !fresh.has(e.Id) ? " fresh" : ""),
            Mail: e,
        })));
        State.list.Set(State.selected && State.byId.has(State.selected) ? State.selected : null, true);
        State.known = new Set(State.emails.map(e => e.Id));

        const f = Folders[State.folder];
        $("folderTitle").textContent = f.title + (State.system !== null ? " · " + (State.system || _T("(no system)", "Name of the filter in a fake email inbox for emails without a system")) : "");
        $("listInfo").textContent = items.length === State.emails.length
            ? (items.length === 1
                ? _TF("1 email", "Number of emails in a list with exactly one email")
                : _T("{0} emails", items.length, "Number of emails in a list (never 1).{0} is replaced with the count"))
            : _T("{0} of {1}", items.length, State.emails.length, "Number of emails shown in a filtered list.{0} is replaced with the shown count.{1} is replaced with the total count");
    }

    /**
     * The empty state of the list (Sw.SelectList Empty): what the folder / the search found nothing of.
     * @returns {{Icon: string, Title: string, Text: string}} The state.
     */
    function emptyState() {
        return {
            Icon: "inbox",
            Title: State.query
                ? _T("Nothing matches {0}", "“" + State.query + "”", "Text shown when a search in a fake email inbox found nothing.{0} is replaced with the quoted search text")
                : Folders[State.folder].empty,
            Text: _TF("Emails sent by the system show up here instantly.", "Second line of the empty email list of the fake email inbox"),
        };
    }

    /**
     * The content of one list row (Sw.SelectList Render): avatar, direction, name, failed tag, time, subject, preview.
     * @param {{Mail: object}} it The list item.
     * @returns {HTMLElement[]} The row content.
     */
    function renderRow(it) {
        const e = it.Mail;
        const q = State.query;
        const who = el("div", "who");
        who.append(el("span", "dir " + (e.Incoming ? "in" : "out"),
            e.Incoming ? _T("FROM", "Label in an email list for an email that a user sent to the system") : _T("TO", "Label in an email list for an email that the system sent to a user")));
        const name = el("span");
        appendHighlighted(name, displayName(e), q);
        who.append(name);
        if (e.Error)
            who.append(Sw.Badge(_T("Failed", "Tag of an email that could not be sent"), "error"));
        const d = new Date(e.Time);
        const time = el("time", null, shortTime(d));
        time.title = longTime(d);
        const subj = el("div", "subj");
        appendHighlighted(subj, e.Subject || _T("(no subject)", "Text shown for an email without a subject"), q);
        const prev = el("div", "prev");
        appendHighlighted(prev, e.Preview, q);
        return [avatar(e), who, time, subj, prev];
    }

    /**
     * Show the reader (on a phone it replaces the list; body.reading hides the floating Compose button) or the list.
     * @param {boolean} on Show the reader.
     * @param {boolean} [focus] Move the focus (to the reader, or back to the list).
     */
    function showReader(on, focus) {
        State.ld.ShowDetail(on, focus);
        document.body.classList.toggle("reading", on && State.ld.Narrow);
    }

    /** Render the sidebar and the list. */
    function renderAll() {
        renderSide();
        renderList();
        //  The bulk actions need emails: disabled with the reason (UX guide INP-12), never a silent no-op
        const none = State.emails.length <= 0;
        const why = _TF("There are no emails yet", "Reason shown when a bulk action of the fake email inbox (Delete all, Mark all as read) is not available");
        for (const id of ["clearAll", "clearAllTop", "markAllRead", "markAllReadTop"]) {
            const b = $(id);
            if (b && Sw.Disabled)
                Sw.Disabled.Set(b, none, why);
        }
    }

    // ------------------------------------------------------------------ reader

    /**
     * Select an email (null deselects): updates the list, the hash, marks it read and loads it into the reader
     * (stale responses are ignored).
     * @param {?string} id - Email id.
     * @param {boolean} [noScroll] - Don't scroll the row into view.
     * @returns {Promise<void>}
     */
    async function select(id, noScroll) {
        State.selected = id;
        if (String(State.list.Get() ?? "") !== String(id ?? ""))
            State.list.Set(id, true);
        if (!id) {
            State.current = null;
            $("mail").hidden = true;
            $("readerEmpty").hidden = false;
            showReader(false);
            if (location.hash)
                history.replaceState(history.state, "", location.pathname + location.search);
            return;
        }
        history.replaceState(history.state, "", "#" + encodeURIComponent(id));
        const row = State.list.Row(id);
        if (!noScroll && row)
            row.scrollIntoView({ block: "nearest" });
        if (!State.read.has(id)) {
            State.read.add(id);
            saveRead();
            renderSide();
            if (row)
                row.classList.remove("unread");
        }
        try {
            const e = await call("GetEmail", { Id: id });
            if (State.selected === id && e)
                showMail(e);
        }
        catch (ex) {
            if (State.selected === id)
                Sw.Fail(errText(ex), 7000);
        }
    }

    /**
     * A reader tag.
     * @param {string} text - Text.
     * @param {string} [cls] - Extra class.
     * @returns {HTMLElement} The tag.
     */
    function tag(text, cls) {
        return el("span", "tag" + (cls ? " " + cls : ""), text);
    }

    /**
     * Show a full email in the reader.
     * @param {object} e - Email (summary fields plus Body, IsHtml, InReplyTo).
     */
    function showMail(e) {
        State.current = e;
        $("readerEmpty").hidden = true;
        $("mail").hidden = false;
        $("mailSubject").textContent = e.Subject || _T("(no subject)", "Text shown for an email without a subject");
        $("mailAvatar").replaceWith(Object.assign(avatar(e, true), { id: "mailAvatar" }));
        $("mailFromName").textContent = e.Incoming ? (e.SenderName || e.From) : _T("System", "Name shown as the sender of emails sent by the system");
        $("mailFrom").textContent = "<" + (e.From || "") + ">";
        $("mailTo").textContent = e.To || "";
        const d = new Date(e.Time);
        $("mailTime").textContent = longTime(d);
        $("mailTime").dateTime = d.toISOString();

        const tags = $("mailTags");
        tags.replaceChildren(
            tag(e.Incoming ? Folders.in.title : Folders.out.title, e.Incoming ? "in" : "out"),
            tag(e.IsHtml ? _T("HTML", "Tag of an email with a html body") : _T("Plain text", "Tag of an email with a plain text body")));
        if (e.System)
            tags.append(tag(_T("System: {0}", e.System, "Tag of an email that belongs to a system.{0} is replaced with the name of the system")));
        if (e.InReplyTo) {
            const replyTag = _T("Reply to {0}", e.InReplyTo, "Tag of an email that is a reply.{0} is replaced with the id of the original email");
            if (State.byId.has(e.InReplyTo)) {
                const b = el("button", "tag link", replyTag);
                b.type = "button";
                b.title = _T("Show the original email", "Tool tip of a tag that selects the email that an email replies to");
                b.addEventListener("click", () => select(e.InReplyTo));
                tags.append(b);
            }
            else
                tags.append(tag(replyTag));
        }
        tags.append(tag(e.Id));
        //  A reply typed earlier and not sent yet (SAF-11): "Reply draft" (Reply opens it again)
        if (Sw.Draft && Sw.Draft.List)
            Sw.Draft.List("fakeEmail.reply." + e.Id + ".").then(found => {
                if (found.length && State.current === e && tags.isConnected && !tags.querySelector(".tag.draft")) {
                    const t = tag(_T("Reply draft", "Tag of an email that has a reply that was typed but not sent yet (Reply opens it again)"), "draft");
                    t.title = _T("You started a reply to this email: Reply opens it again", "Tool tip of the Reply draft tag of an email");
                    tags.append(t);
                }
            }).catch(() => { });

        $("mailError").hidden = !e.Error;
        $("mailError").textContent = e.Error || "";
        $("viewMode").hidden = !e.IsHtml;
        renderBody();
    }

    /**
     * Render the body of the current email: html in a sandboxed frame (no scripts) or as source, plain text with links.
     */
    function renderBody() {
        const e = State.current;
        const host = $("mailBody");
        host.replaceChildren();
        if (!e)
            return;
        const body = e.Body || "";
        for (const b of $("viewMode").children) {
            const on = b.dataset.view === State.view;
            b.classList.toggle("on", on);
            b.setAttribute("aria-pressed", on ? "true" : "false");
        }
        if (e.IsHtml && State.view === "render") {
            const f = el("iframe");
            f.setAttribute("sandbox", "allow-popups allow-popups-to-escape-sandbox");
            f.title = _T("Email body", "Accessible name of the frame that shows the body of an email");
            f.srcdoc = frameHtml(body);
            host.append(f);
            return;
        }
        const pre = el("pre", e.IsHtml ? null : "text");
        if (e.IsHtml)
            pre.textContent = body;
        else
            appendLinkified(pre, body);
        host.append(pre);
    }

    /**
     * Open the current body in a new tab (a blob url, revoked after 60 s, opened with noopener).
     * Html is shown through viewerHtml (sandboxed frame, no scripts), as the blob itself has the app's origin.
     */
    function openInTab() {
        const e = State.current;
        if (!e)
            return;
        const blob = e.IsHtml
            ? new Blob([viewerHtml(e.Body, e.Subject)], { type: "text/html" })
            : new Blob([e.Body || ""], { type: "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        window.open(url, "_blank", "noopener");
        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }

    /**
     * Move the focus after an email (or all of them) went away (UX guide FOC-1: never to the page body): the list, whose active
     * row is the selected neighbour, else (nothing left to show) the Compose button.
     */
    function focusAfterDelete() {
        const compose = $("compose");
        if (visible().length <= 0 && compose && compose.offsetParent !== null)
            compose.focus();
        else if (State.list && State.list.Focus)
            State.list.Focus();
    }

    /** True while a delete runs (the Delete button and the Del key ask only once). */
    let deleting = false;

    /**
     * Delete the selected email after the shared danger confirmation (UX guide SAF-1: the delete is final, the server keeps no
     * copy, so it asks instead of offering Undo): question title naming the email, From / To / time, "This cannot be undone.",
     * Cancel focused. Then the neighbour is selected and the focus goes to the list (FOC-1).
     * @returns {Promise<void>}
     */
    async function deleteSelected() {
        const id = State.selected;
        if (!id || deleting)
            return;
        const sum = State.byId.get(id) || State.current || {};
        const subject = sum.Subject || _T("(no subject)", "Text shown for an email without a subject");
        const verb = _TF("Delete", "Button of the confirmation that deletes one fake email");
        const facts = [
            { Label: _TF("From", "Label of the sender in the confirmation that deletes one fake email"), Value: sum.Incoming ? (sum.SenderName ? sum.SenderName + " <" + (sum.From || "") + ">" : (sum.From || "")) : _T("System", "Name shown as the sender of emails sent by the system") },
            { Label: _TF("To", "Label of the recipient in the confirmation that deletes one fake email"), Value: sum.To || "" },
        ];
        if (sum.Time)
            facts.push({ Label: _TF("Time", "Label of the time in the confirmation that deletes one fake email"), Value: longTime(new Date(sum.Time)) });
        const target = "“" + subject + "”";
        const done = _T("Deleted the email {0}.", target, "Announcement after a fake email was deleted.{0} is replaced with the quoted subject");
        /** The delete itself (after the confirmation): the neighbour is selected, the phone layout shows the list again. */
        const work = async () => {
            const items = visible();
            const i = items.findIndex(e => e.Id === id);
            const next = items[i + 1] || items[i - 1];
            await call("DeleteEmail", { Id: id });
            //  Its reply draft can never be sent any more (SAF-11): it goes with the email
            if (Sw.Draft && Sw.Draft.RemoveFor)
                Sw.Draft.RemoveFor("fakeEmail.reply." + id + ".");
            State.read.delete(id);
            State.emails = State.emails.filter(e => e.Id !== id);
            State.byId.delete(id);
            saveRead();
            renderAll();
            select(next ? next.Id : null);
            if (State.ld.Narrow && State.ld.Showing === "detail")
                showReader(false);
        };
        deleting = true;
        try {
            //  The framework's one safe path for destructive actions (confirmation, busy Delete button, one result)
            if (Sw.Danger && Sw.Danger.Run) {
                if (await Sw.Danger.Run({ Action: verb, Target: target, Facts: facts, Run: work, Button: $("delete"), Done: done }))
                    focusAfterDelete();
                return;
            }
            const ok = (Sw.Cells && Sw.Cells.ConfirmDanger)
                ? await Sw.Cells.ConfirmDanger({ Action: verb, Target: target, Facts: facts, OkText: verb, OkIcon: "trash" })
                : await Sw.Dialog.Confirm(null, subject, verb, { Danger: true, Target: target });
            if (!ok)
                return;
            await work();
            focusAfterDelete();
            Sw.Page.Announce(done);
        }
        catch (ex) {
            Sw.Fail(errText(ex), 7000);
        }
        finally {
            deleting = false;
        }
    }

    /**
     * Select the visible email delta steps from the selected one (the first when nothing is selected).
     * @param {number} delta - +1 next, -1 previous.
     */
    function move(delta) {
        const items = visible();
        if (items.length <= 0)
            return;
        let i = items.findIndex(e => e.Id === State.selected);
        i = i < 0 ? 0 : Math.max(0, Math.min(items.length - 1, i + delta));
        select(items[i].Id);
    }

    /** Persist the read ids (pruned to existing emails). */
    function saveRead() {
        if (State.byId.size > 0)
            for (const id of State.read)
                if (!State.byId.has(id))
                    State.read.delete(id);
        save("read", [...State.read]);
    }

    // ------------------------------------------------------------------ live updates

    /**
     * Show the connection state: the dot (#status, its accessible name), the tool tip of #statusBox (also shown on long press)
     * and the short label #statusText (shown on touch devices, email.css).
     * @param {boolean} on - Connected.
     * @param {string} text - Tool tip / accessible text.
     */
    function setStatus(on, text) {
        const s = $("status");
        s.className = "status " + (on ? "on" : "off");
        s.setAttribute("aria-label", text);
        $("statusBox").title = text;
        $("statusText").textContent = on
            ? _TF("Live", "Short label next to the connection indicator of a fake email inbox when it is connected (touch devices), keep it short")
            : _TF("Offline", "Short label next to the connection indicator of a fake email inbox when it is disconnected (touch devices), keep it short");
    }

    /**
     * Replace the email list with a GetEmails result and render; the first time also applies the "everything existing
     * counts as read" rule and the hash selection; later new emails flash the title and show a toast.
     * @param {object[]} emails - Summaries.
     */
    function applyList(emails) {
        const first = State.known === null;
        const newOnes = first ? [] : emails.filter(e => !State.byId.has(e.Id));
        State.emails = emails;
        State.byId = new Map(emails.map(e => [e.Id, e]));
        if (first) {
            if (load("read", null) === null)
                emails.forEach(e => State.read.add(e.Id));
            saveRead();
        }
        renderAll();
        if (State.selected && !State.byId.has(State.selected))
            select(null);
        if (first) {
            const id = decodeURIComponent(location.hash.substring(1));
            if (id && State.byId.has(id)) {
                select(id);
                showReader(true);
            }
        }
        const n = newOnes.length;
        if (n <= 0)
            return;
        document.title = "(" + n + ") " + State.title;
        setTimeout(() => document.title = State.title, 4000);
        if (n === 1) {
            const e = newOnes[0];
            const subject = e.Subject || _T("(no subject)", "Text shown for an email without a subject");
            Sw.Info(e.Incoming
                ? _T("Received: {0}", subject, "Notification when the system received a new email.{0} is replaced with the subject")
                : _T("Sent: {0}", subject, "Notification when the system sent a new email.{0} is replaced with the subject"), 3000);
        }
        else
            Sw.Info(_T("{0} new emails", n, "Notification when several new emails arrived.{0} is replaced with the count"), 3000);
    }

    /**
     * Replace the page with the shared "not allowed" state (Sw.Access.NotAllowed: who is signed in and the role that is needed).
     * @param {*} ex - The refused request (Sw.Net.Error, Denied).
     */
    function showDenied(ex) {
        const box = Sw.Access.NotAllowed({ Error: ex, Name: _TF("the fake email inbox", "Name of the fake email inbox in the message shown when the signed in user is not allowed to see it (You don't have access to ...)") });
        box.classList.add("denied-page");
        document.body.replaceChildren(box);
    }

    /**
     * The endless long poll of GetEmails (exponential back off 1 s .. 15 s on errors; a refusal (403) ends it, see showDenied).
     * @param {function():void} onFirst - Called once after the first answer or error (ends the loading phase).
     * @returns {Promise<void>} Resolves only when the user is not allowed to see the inbox.
     */
    async function pollLoop(onFirst) {
        let delay = 1000;
        for (; ;) {
            try {
                const r = await call("GetEmails", { Version: State.version });
                if (!r)
                    throw new Error(_T("No response", "Error text when the server did not answer"));
                setStatus(true, _T("Connected, updates are live", "Tool tip of the connection indicator of a fake email inbox when it is connected"));
                delay = 1000;
                State.version = r.Version;
                if (r.From !== State.from) {
                    State.from = r.From || "";
                    $("fromAddress").textContent = State.from;
                }
                if (!r.Unchanged)
                    applyList(r.Emails || []);
                onFirst();
            }
            catch (ex) {
                //  The inbox requires a role (FakeEmailParams.Auth, default Ops / Debug / Dev): a refusal is final, show the shared
                //  "not allowed" state instead of retrying (a user that is not signed in is sent to the sign in page by Sw.Net)
                if (Sw.Access && Sw.Access.IsDenied(ex)) {
                    showDenied(ex);
                    onFirst();
                    return;
                }
                setStatus(false, _T("Disconnected: {0}", errText(ex), "Tool tip of the connection indicator of a fake email inbox when it is disconnected.{0} is replaced with the error"));
                onFirst();
                await Sw.Util.Delay(delay);
                delay = Math.min(delay * 2, 15000);
            }
        }
    }

    // ------------------------------------------------------------------ composer

    /**
     * Toggle the pressed segment of a segmented control.
     * @param {HTMLElement} seg - The .seg element.
     * @param {string} attr - Data attribute name.
     * @param {string} value - Active value.
     */
    function setSeg(seg, attr, value) {
        for (const b of seg.children) {
            const on = b.dataset[attr] === value;
            b.classList.toggle("on", on);
            b.setAttribute("aria-pressed", on ? "true" : "false");
        }
    }

    /**
     * Set the composer mode: "in" (a user sends to the system, FakeIncoming) or "out" (the system sends, SendEmail).
     * @param {string} mode - Mode.
     */
    function setComposeMode(mode) {
        State.mode = mode;
        $("composeForm").classList.toggle("out", mode === "out");
        setSeg($("composeMode"), "mode", mode);
        $("cTo").placeholder = mode === "in" ? (State.from || _T("system address", "Placeholder of the recipient field when faking an email to the system")) : "user@example.com";
        $("composeHint").textContent = mode === "in"
            ? _T("Fakes an email that a user sends to the system: the system receives it as incoming mail", "Hint in the fake email composer when faking an email from a user")
            : _T("Sends an email using this service, as if the system sent it", "Hint in the fake email composer when sending an email as the system");
    }

    /**
     * Set the composer format ("text" or "html": monospace body and a preview button).
     * @param {string} format - Format.
     */
    function setComposeFormat(format) {
        State.format = format;
        $("composeForm").classList.toggle("html", format === "html");
        setSeg($("composeFormat"), "format", format);
        $("cPreviewBtn").hidden = format !== "html";
        if (format !== "html")
            showPreview(false);
    }

    /**
     * Switch the composer body between the text area and the html preview.
     * @param {boolean} on - Show the preview.
     */
    function showPreview(on) {
        const p = $("cPreview");
        p.hidden = !on;
        $("cBody").hidden = on;
        $("cPreviewBtn").textContent = on ? _T("Edit", "Button in the fake email composer that returns from the preview to editing") : _T("Preview", "Button in the fake email composer that shows a preview of the html body");
        if (on)
            p.srcdoc = frameHtml($("cBody").value);
    }

    /**
     * Open the composer dialog (the form of index.html moves into a Sw.Dialog and back when it closes).
     * @param {{mode:string, format:string, from:string, name:string, to:string, subject:string, system:string,
     *   body:string, replyTo:string, focus:string}} [opts] - Initial values; focus = element id.
     */
    function openComposer(opts) {
        if (State.composer)
            return;
        opts = opts || {};
        const form = $("composeForm");
        State.composeForm.Clear();
        //  A failure line of an earlier send does not belong to this email (Sw.Form Clear() keeps its status line)
        const oldStatus = form.querySelector(".sw-form-status");
        if (oldStatus)
            oldStatus.hidden = true;
        setComposeMode(opts.mode || "in");
        setComposeFormat(opts.format || "text");
        showPreview(false);
        $("cFrom").value = opts.from ?? load("lastFrom", "user@example.com");
        $("cName").value = opts.name ?? load("lastName", "");
        $("cTo").value = opts.to ?? "";
        $("cSubject").value = opts.subject ?? "";
        $("cSystem").value = opts.system ?? "";
        $("cBody").value = opts.body ?? "";
        $("cReplyTo").value = opts.replyTo ?? "";
        const focus = opts.focus ? $(opts.focus) : ((State.mode === "in" && !$("cFrom").value) ? $("cFrom") : $("cSubject"));
        const dlg = State.composer = Sw.Dialog.Open({
            Title: _T("New email", "Title of the dialog that composes a fake email"),
            Content: form,
            Wide: true,
            Modal: true,
            Class: "composer-dialog",
            Focus: focus,
            OnClose: v => {
                //  Sent: the drafts are already cleared. Closed after "Discard changes?" (a dirty dialog only closes that way): the
                //  drafts go too. A clean close keeps them (ex: a restored draft that was not changed).
                const discarded = v !== "sent" && dlg.IsDirty && dlg.IsDirty();
                for (const d of State.drafts) {
                    if (discarded)
                        d.Clear();
                    d.Dispose();
                }
                State.drafts = [];
                State.composer = null;
                $("composerHost").append(form);
            },
        });
        attachDrafts(dlg, opts.draft || "fakeEmail.compose", opts.body || "");
    }

    /**
     * Keep the Subject and the Message of the composer as drafts (UX guide SAF-11: they survive a reload or a closed tab and come back
     * with "Draft restored"). A reply keeps its own draft (context per original email) and only its message: the quoted original is
     * replaced by the draft when there is one.
     * @param {object} dlg - The composer dialog handle.
     * @param {string} context - The draft context ("fakeEmail.compose" or "fakeEmail.reply.<id>").
     * @param {string} prefilled - The prefilled message (the quote of a reply), "" for a new email.
     * @returns {Promise<void>}
     */
    async function attachDrafts(dlg, context, prefilled) {
        if (!Sw.Draft)
            return;
        const body = $("cBody");
        const subject = $("cSubject");
        //  The Subject of a new email is kept with the low level api (one "Draft restored" line, under the message, says it for both)
        if (!prefilled && !subject.value) {
            const sctx = context + ".subject";
            const stored = await Sw.Draft.Get(sctx).catch(() => null);
            if (State.composer !== dlg)
                return;
            if (stored && !subject.value)
                subject.value = stored.Text;
            let timer = 0;
            const keep = () => {
                clearTimeout(timer);
                timer = setTimeout(() => Sw.Draft.Set(sctx, subject.value), 500);
            };
            subject.addEventListener("input", keep);
            State.drafts.push({
                Clear: () => {
                    clearTimeout(timer);
                    return Sw.Draft.Remove(sctx);
                },
                Dispose: () => subject.removeEventListener("input", keep),
            });
        }
        //  The dialog asks "Discard changes?" itself, so the draft doesn't mark the page dirty (Dirty: false)
        if (prefilled) {
            const stored = await Sw.Draft.Get(context + ".body").catch(() => null);
            if (State.composer !== dlg)
                return;
            if (stored && body.value === prefilled)
                body.value = "";
        }
        const d = Sw.Draft.Attach(body, { Context: context + ".body", Dirty: false });
        if (d)
            State.drafts.push(d);
    }

    /** Close the composer dialog as its Cancel button: a dirty composer asks "Discard changes?", a busy one (sending) stays open. */
    function closeComposer() {
        if (State.composer)
            State.composer.Attempt("x");
    }

    /**
     * The quoted original of a reply (blockquote for html, "> " lines for text).
     * @param {object} e - Email.
     * @returns {string} Quote.
     */
    function quote(e) {
        const when = longTime(new Date(e.Time));
        const who = e.Incoming ? (e.SenderName || e.From) : e.From;
        const intro = _T("On {0}, {1} wrote:", when, who, "First line of a quoted email in a reply.{0} is replaced with the time.{1} is replaced with the sender");
        if (e.IsHtml)
            return "<p></p>\n<blockquote style=\"margin:0 0 0 .8ex;border-left:2px solid #ccc;padding-left:1ex\">\n<p>" + Sw.Format.HtmlSafe(intro) + "</p>\n" + (e.Body || "") + "\n</blockquote>";
        return "\n\n" + intro + "\n" + (e.Body || "").split("\n").map(l => "> " + l).join("\n");
    }

    /**
     * Reply to the current email: incoming mail is answered by the system (out), system mail by the user (in, with
     * InReplyTo); the caret starts above the quote.
     */
    function reply() {
        const e = State.current;
        if (!e)
            return;
        const subject = /^re:/i.test(e.Subject || "") ? e.Subject : "Re: " + (e.Subject || "");
        const format = e.IsHtml ? "html" : "text";
        if (e.Incoming)
            openComposer({ mode: "out", to: e.From, subject: subject, format: format, body: quote(e), focus: "cBody", draft: "fakeEmail.reply." + e.Id });
        else
            openComposer({ mode: "in", from: e.To, to: e.From, subject: subject, format: format, body: quote(e), replyTo: e.Id, focus: "cBody", draft: "fakeEmail.reply." + e.Id });
        const b = $("cBody");
        b.setSelectionRange(0, 0);
        b.scrollTop = 0;
    }

    /**
     * Send the composed email (Send, Enter in a field, Ctrl+Enter): the composer form checks the fields and runs sendWork; a failure
     * is shown once, in the form's status line above the Send row (Sw.Form, UX guide FRM-3 / FBK-5: no toast besides it).
     * @returns {Promise<boolean>} True when it was sent.
     */
    async function sendComposed() {
        if (!State.composer || State.sending)
            return false;
        return State.composeForm.Submit();
    }

    /**
     * The send itself (Sw.Form OnSubmit of the composer): FakeIncoming (FakeEmailRequest, answers the id of the new email: the id
     * counts as read; no id = not sent) or SendEmail (the IEmailSender api: OutgoingEmailMessage `{Email, Subject, Body, IsHtml}`,
     * answers nothing; a failure is an error reply, the new email arrives with the next poll). Throws with the reason when the server
     * did not take the email, so the form says it once and keeps everything typed.
     * @returns {Promise<void>}
     */
    async function sendWork() {
        const dlg = State.composer;
        if (!dlg || State.sending)
            return;
        const isIn = State.mode === "in";
        const to = $("cTo").value.trim() || null;
        const req = isIn ? {
            From: $("cFrom").value.trim(),
            SenderName: $("cName").value.trim(),
            To: to,
            Subject: $("cSubject").value,
            Body: $("cBody").value,
            IsHtml: State.format === "html",
            System: $("cSystem").value.trim() || null,
            InReplyTo: $("cReplyTo").value || null,
        } : {
            Email: to,
            Subject: $("cSubject").value,
            Body: $("cBody").value,
            IsHtml: State.format === "html",
        };
        //  Sending: the dialog is busy (one send; a close attempt gives the "still working" cue) and leaving the page asks first
        //  (UX guide SAF-10: work that stops with the page)
        State.sending = true;
        const p = call(isIn ? "FakeIncoming" : "SendEmail", req);
        dlg.SetBusy(p, {
            Text: _TF("Sending the email…", "Announced while the fake email composer sends the email"),
            CloseText: _TF("Close (the email may still be sent)", "Button of the fake email composer that appears when sending takes long: closes the dialog, the request goes on"),
        });
        Sw.Page.SetDirty("fakeEmail.send", _TF("The email may not be sent.", "Text of the question 'Leave this page?' while a fake email is being sent"), { Work: true });
        try {
            const id = await p;
            //  FakeIncoming answers the id of the new email: no id = not sent (the form shows this once, the values stay); SendEmail
            //  answers nothing (sent unless it failed with an error reply)
            if (isIn && !id)
                throw new Error(_TF("The email was not sent: the service did not accept it. Try again, or look in the log.", "Status line in the fake email composer when sending failed without a reason from the service"));
            if (isIn) {
                save("lastFrom", req.From);
                save("lastName", req.SenderName);
            }
            for (const d of State.drafts)
                await d.Clear();
            if (State.composer === dlg)
                dlg.Close("sent");
            if (id) {
                State.read.add(id);
                saveRead();
            }
        }
        finally {
            State.sending = false;
            Sw.Page.ClearDirty("fakeEmail.send");
        }
    }

    // ------------------------------------------------------------------ theme

    /**
     * Migrate the old page theme ("fakeEmail.theme", light / dark) once to the SysWeaver theme (only when no
     * SysWeaver theme is stored), then remove it.
     */
    function migrateTheme() {
        const old = load("theme", null);
        if (!old)
            return;
        Sw.Storage.Local.Remove(KeyPrefix + "theme");
        if (!Sw.Storage.Local.Get(Sw.Theme.StorageKey, null) && (old === "light" || old === "dark"))
            setTheme(old);
    }

    /** @type {Promise<Array<{Name: string, Text: string, Title: ?string, Icon: ?string}>|null>|null} The shared theme list (themeList, once). */
    let themesP = null;

    /**
     * The shared theme list, the same as the Theme section of the app shell's settings menu: the server's "Theme" menu
     * (Api/application/GetMenu "Theme": Automatic, the built in themes, in the server's order and names) plus the themes the app
     * registered with a title (Sw.Theme.Custom of the top window, ex: app/app.js). Name "" = Automatic. Null when the server has no
     * theme menu (the built in list is used then).
     * @returns {Promise<Array<{Name: string, Text: string, Title: ?string, Icon: ?string}>|null>}
     */
    function themeList() {
        if (!themesP)
            themesP = (async () => {
                const list = [];
                try {
                    const m = await Sw.Net.Send(Sw.Url.Root + "Api/application/GetMenu", "Theme", { NoNavigate: true });
                    const root = ((m && m.Items) || []).find(x => x && ("" + (x.Id || "")).toLowerCase() === "theme");
                    for (const c of (root && root.Children) || []) {
                        const d = typeof c.Data === "string" ? c.Data : "";
                        const s = /setTheme\(\s*['"]([^'"]+)['"]\s*\)/.exec(d);
                        const name = s ? s[1] : (/resetTheme\(\s*\)/.test(d) ? "" : null);
                        if (name !== null)
                            list.push({ Name: name, Text: c.Name || name, Title: c.Title || null, Icon: c.IconClass || null });
                    }
                }
                catch {
                    //  No theme menu (ex: not signed in): the built in list
                }
                if (!list.length)
                    return null;
                let custom = [];
                try {
                    const T = (window.top !== window && window.top.Sw && window.top.Sw.Theme && window.top.Sw.Theme.Custom) ? window.top.Sw.Theme : Sw.Theme;
                    custom = T.Custom ? T.Custom() : [];
                }
                catch {
                    custom = Sw.Theme.Custom ? Sw.Theme.Custom() : [];
                }
                const known = new Set(list.map(x => x.Name.toLowerCase()));
                for (const d of custom)
                    if (d && /^[\w.-]+$/.test(d.Name) && !known.has(d.Name.toLowerCase()))
                        list.push({ Name: d.Name, Text: d.Title, Title: d.Description || null, Icon: d.Icon || null });
                return list;
            })();
        return themesP;
    }

    /**
     * Show the theme menu: the shared theme list (themeList: the same themes as the app shell's settings menu, app themes included),
     * else "Automatic" and the five SysWeaver themes.
     * @param {HTMLElement} anchor - The theme button.
     * @returns {Promise<void>}
     */
    async function themeMenu(anchor) {
        const stored = Sw.Storage.Local.Get(Sw.Theme.StorageKey, null);
        const item = (name, text) => ({ Text: text, Radio: true, Checked: stored === name, OnClick: () => setTheme(name) });
        const shared = await themeList();
        const items = shared
            ? shared.map(x => x.Name
                ? { Text: x.Text, Title: x.Title, Radio: true, Checked: stored === x.Name, OnClick: () => setTheme(x.Name) }
                : { Text: x.Text, Title: x.Title, Radio: true, Checked: !stored, OnClick: () => resetTheme() })
            : [
                { Text: _T("Automatic", "Theme menu item that follows the operating system light / dark setting"), Radio: true, Checked: !stored, OnClick: () => resetTheme() },
                "-",
                item("light", _T("Light", "Name of the light theme")),
                item("dark", _T("Dark", "Name of the dark theme")),
                item("hacker", _T("Hacker", "Name of the green on black theme")),
                item("application", _T("Application", "Name of the theme that uses the application colors")),
                item("pro", _T("Pro", "Name of the professional theme")),
            ];
        Sw.Menu.Show(anchor, items, { Align: "end", Label: _T("Theme", "Accessible name of the theme menu") });
    }

    // ------------------------------------------------------------------ bulk actions

    /** Mark all emails as read (the sidebar footer link and the "More" menu). */
    function markAllRead() {
        State.emails.forEach(e => State.read.add(e.Id));
        saveRead();
        renderAll();
    }

    /**
     * Delete all emails (the sidebar footer link and the "More" menu) after the shared danger confirmation
     * (Sw.Cells.ConfirmDanger, as Kill and the log Delete: warning icon, question title, count line, "This cannot be undone.",
     * Cancel focused).
     */
    async function deleteAll() {
        const n = State.emails.length;
        if (n <= 0)
            return;
        const title = _TF("Delete all emails?", "Title (question) of the dialog that asks before deleting all fake emails");
        const text = n === 1
            ? _TF("1 email will be deleted.", "Text of the dialog that asks before deleting all fake emails when there is exactly one")
            : _T("{0} emails will be deleted.", n, "Text of the dialog that asks before deleting all fake emails (never 1).{0} is replaced with the number of emails");
        const okText = _T("Delete all", "Button that deletes all fake emails");
        //  all items of a kind: type to confirm (UX guide SAF-4)
        const word = _TF("delete all", "The text the user types to confirm deleting all fake emails (short, lower case)");
        const ok = (Sw.Cells && Sw.Cells.ConfirmDanger)
            ? await Sw.Cells.ConfirmDanger({ Title: title, Text: text, OkText: okText, OkIcon: "trash", Confirm: word })
            : await Sw.Dialog.Confirm(title, text, okText, { Danger: true });
        if (!ok)
            return;
        try {
            await call("Clear");
            //  Every reply draft goes with its email (SAF-11)
            if (Sw.Draft && Sw.Draft.RemoveFor)
                Sw.Draft.RemoveFor("fakeEmail.reply.");
            select(null);
            State.emails = [];
            State.byId.clear();
            renderAll();
            focusAfterDelete();
            Sw.Page.Announce(n === 1
                ? _TF("Deleted 1 email.", "Announcement after all fake emails were deleted, when there was exactly one")
                : _T("Deleted {0} emails.", n, "Announcement after all fake emails were deleted (never 1).{0} is replaced with the number of emails"));
        }
        catch (ex) {
            Sw.Fail(errText(ex), 7000);
        }
    }

    /**
     * Show the "More" menu (#more, shown where the sidebar footer is hidden: <= 1100px): Mark all as read, Delete all.
     * @param {HTMLElement} anchor - The More button.
     */
    function moreMenu(anchor) {
        const none = State.emails.length <= 0;
        Sw.Menu.Show(anchor, [
            { Text: _T("Mark all as read", "Menu item of the fake email inbox that marks all emails as read"), Icon: "check", Disabled: none, OnClick: markAllRead },
            "-",
            { Text: _T("Delete all", "Button that deletes all fake emails"), Icon: "trash", Danger: true, Disabled: none, OnClick: () => deleteAll() },
        ], { Align: "end", Label: _T("More", "Accessible name of the More menu of the fake email inbox (bulk actions)") });
    }

    // ------------------------------------------------------------------ start

    /** True if a dialog or popup is open (shortcuts are off then). @returns {boolean} Open. */
    function overlayOpen() {
        return !!State.composer || !!document.querySelector(".sw-dialog, .sw-popup");
    }

    /**
     * The global keyboard shortcuts (see the file header).
     * @param {KeyboardEvent} ev - Event.
     */
    function onKey(ev) {
        if (ev.defaultPrevented || overlayOpen() || ev.ctrlKey || ev.metaKey || ev.altKey)
            return;
        const t = ev.target;
        if (t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement) {
            if (ev.key === "Escape")
                t.blur();
            return;
        }
        switch (ev.key) {
            case "j":
            case "ArrowDown":
                move(1);
                break;
            case "k":
            case "ArrowUp":
                move(-1);
                break;
            case "r":
                reply();
                break;
            case "c":
                openComposer();
                break;
            case "/":
                $("search").focus();
                break;
            case "Delete":
                deleteSelected();
                break;
            case "Escape":
                //  a phone showing the reader goes back to the list (the selection stays); else Escape deselects
                if (State.ld.Narrow && State.ld.Showing === "detail")
                    showReader(false, true);
                else
                    select(null);
                break;
            default:
                return;
        }
        ev.preventDefault();
    }

    /** Wire all controls of index.html. */
    function wire() {
        $("search").addEventListener("input", Sw.Util.Debounce(() => {
            State.query = $("search").value.trim().toLowerCase();
            renderList();
        }, 120));
        for (const b of $("viewMode").children)
            b.addEventListener("click", () => {
                State.view = b.dataset.view;
                renderBody();
            });
        $("compose").addEventListener("click", () => openComposer());
        $("reply").addEventListener("click", reply);
        $("delete").addEventListener("click", deleteSelected);
        $("openTab").addEventListener("click", openInTab);
        $("back").addEventListener("click", () => showReader(false, true));
        $("theme").addEventListener("click", ev => themeMenu(ev.currentTarget));
        $("markAllRead").addEventListener("click", markAllRead);
        $("clearAll").addEventListener("click", () => deleteAll());
        $("markAllReadTop").addEventListener("click", markAllRead);
        $("clearAllTop").addEventListener("click", () => deleteAll());
        $("more").addEventListener("click", ev => moreMenu(ev.currentTarget));
        for (const b of $("composeMode").children)
            b.addEventListener("click", () => setComposeMode(b.dataset.mode));
        for (const b of $("composeFormat").children)
            b.addEventListener("click", () => setComposeFormat(b.dataset.format));
        $("cPreviewBtn").addEventListener("click", () => showPreview($("cPreview").hidden));
        $("composeCancel").addEventListener("click", closeComposer);
        const form = $("composeForm");
        //  The composer checks (shared Sw.Form: the message under the field, the focus on the first bad one, Send only when valid):
        //  a user's From address when faking an incoming email, the recipient when the system sends; addresses must look like one
        const mailRx = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
        const mailOk = v => !v || mailRx.test(v.trim()) ? null : _TF("Enter an e-mail address like name@example.com.", "Message under an e-mail field of the fake email composer that is not an address");
        State.composeForm = Sw.Form({
            Fields: [
                { Name: "From", Control: $("cFrom"), When: () => State.mode === "in", Required: _TF("Enter the address of the user that sends the email.", "Message under the empty From field of the fake email composer"), Validate: mailOk },
                { Name: "To", Control: $("cTo"), Required: false, When: () => State.mode === "out" || !!$("cTo").value.trim(), Validate: v => (State.mode === "out" && !v.trim()) ? _TF("Enter the address that the system sends to.", "Message under the empty To field of the fake email composer when the system sends") : mailOk(v) },
            ],
            //  Send: one send per click (busy), the failure in the form's status line above the Send row (.sw-form-foot)
            Submit: $("composeSend"),
            OnSubmit: () => sendWork(),
        });
        form.addEventListener("submit", ev => {
            ev.preventDefault();
            sendComposed();
        });
        form.addEventListener("keydown", ev => {
            if (ev.key === "Enter" && (ev.ctrlKey || ev.metaKey)) {
                ev.preventDefault();
                sendComposed();
            }
        });
        document.addEventListener("keydown", onKey);
        //  The keyboard short cut hints (.kbd-help) are shown once the keyboard is used (body.kbd-user, email.css)
        document.addEventListener("keydown", () => document.body.classList.add("kbd-user"), { once: true, capture: true });
    }

    /**
     * Show the "Back" link (#backToApp) when the page is not inside the app shell (the server menu item is a link that
     * replaces the whole window): it goes back in the history when the previous page is this site (the shell, at the
     * page it showed), else it opens the site root (the shell).
     */
    function initAppBack() {
        const a = $("backToApp");
        if (!a || Sw.Page.IsEmbedded)
            return;
        const text = _TF("Back to the application", "Tool tip of a link on the fake email inbox that returns to the application");
        a.title = text;
        a.setAttribute("aria-label", text);
        a.hidden = false;
        a.addEventListener("click", ev => {
            if (Sw.BadClick(ev))
                return;
            let sameSite = false;
            try {
                sameSite = !!document.referrer && new URL(document.referrer).origin === window.location.origin;
            }
            catch {
            }
            if (sameSite && history.length > 1) {
                ev.preventDefault();
                history.back();
            }
        });
    }

    /**
     * Inside the app shell (a frame whose top window runs the shell, also through the app's Home wrapper page) the shell
     * already shows the application header and theme control: body.in-shell (email.css) hides the page's own brand
     * (logo, "Fake Mail") and theme button, leaving a slim tool bar (sender address, search, connection, More).
     * Opened stand alone the page keeps its own header and theme button.
     * @returns {boolean} True when inside the shell.
     */
    function initInShell() {
        let inShell = false;
        try {
            inShell = Sw.Page.IsEmbedded && !!(window.top.Sw && window.top.Sw.Shell);
        }
        catch {
            //  Cross origin top window: not our shell
        }
        document.body.classList.toggle("in-shell", inShell);
        return inShell;
    }

    /**
     * Name the icon buttons and links: a translated tool tip (with the key short cut, if any), accessible name and
     * aria-keyshortcuts; the name also shows on long press (touch / pen) and keyboard focus (Sw.Hint). Each gets a short
     * translated text label (span.tlabel, created when missing) that email.css shows next to / below the icon on touch
     * devices, where there are no hover tool tips. The connection indicator (#statusBox) gets the long press hint too.
     */
    function nameButtons() {
        /**
         * @param {string} id - Element id.
         * @param {string} name - Full name (tool tip, accessible name; null keeps the current one).
         * @param {string} short - Short visible label.
         * @param {string} [key] - Key short cut (shown in the tool tip, aria-keyshortcuts).
         */
        const name = (id, name, short, key) => {
            const b = $(id);
            if (!b)
                return;
            if (name) {
                b.title = key ? name + "  ( " + key + " )" : name;
                b.setAttribute("aria-label", name);
            }
            if (key)
                b.setAttribute("aria-keyshortcuts", key === "Del" ? "Delete" : key);
            let l = b.querySelector("span");
            if (!l) {
                l = document.createElement("span");
                l.className = "tlabel";
                l.setAttribute("aria-hidden", "true");
                b.append(l);
            }
            l.textContent = short;
            b.classList.add("labelled");
            Sw.Hint && Sw.Hint.Attach(b);
        };
        name("more", _TF("More actions (mark all as read, delete all)", "Tool tip of the More button of the fake email inbox (narrow screens)"),
            _TF("More", "Short label of the More button of the fake email inbox (touch devices), keep it short"));
        name("theme", _TF("Choose the theme", "Tool tip of the theme button of the fake email inbox"),
            _TF("Theme", "Short label of the theme button of the fake email inbox (touch devices), keep it short"));
        name("back", _TF("Back to the email list", "Tool tip of the back button in the email reader of the fake email inbox (phones)"),
            _TF("Back", "Short label of the back button in the email reader of the fake email inbox (touch devices), keep it short"));
        name("openTab", _TF("Open the body in a new tab", "Tool tip of a button in the fake email reader that opens the email body in a new browser tab"),
            _TF("Open", "Short label of the button that opens the email body in a new tab (touch devices), keep it short"));
        name("delete", _TF("Delete the email", "Tool tip of the delete button in the fake email reader"),
            _TF("Delete", "Short label of the delete button in the fake email reader (touch devices), keep it short"), "Del");
        name("compose", _TF("Fake an email from a user to the system", "Tool tip of the compose button of the fake email inbox"),
            _TF("Compose", "Text of the compose button of the fake email inbox, keep it short"), "c");
        name("reply", _TF("Fake a reply from the user", "Tool tip of the reply button in the fake email reader"),
            _TF("Reply", "Text of the reply button in the fake email reader, keep it short"), "r");
        if (!$("backToApp").hidden)
            name("backToApp", null, _TF("Back", "Short label of the link that returns from the fake email inbox to the application, keep it short"));
        Sw.Hint && Sw.Hint.Attach($("statusBox"));
    }

    /**
     * The list and the reader from the shared parts (ui/README.md "List / detail"): the email list is a Sw.SelectList (arrows, Home / End,
     * type ahead, Enter, the active row in aria-activedescendant, the empty state) with the mail row look (renderRow); the list pane and
     * the reader are a Sw.ListDetail (side by side; below 640 px of its width the reader replaces the list, the reader's own Back button
     * and Escape return). A click opens the reader on a phone; the arrows only move the selection (the reader follows on wide screens).
     */
    function initListReader() {
        State.list = Sw.SelectList({
            Label: _TF("Emails", "Accessible name of the email list of the fake email inbox"),
            Class: "list",
            Render: renderRow,
            Empty: emptyState,
            OnSelect: (key, items, how) => {
                if (how === "api")
                    return;
                select(key);
                if (key && how === "click")
                    showReader(true);
            },
            OnOpen: it => {
                if (!it)
                    return;
                if (State.selected !== it.Key)
                    select(it.Key);
                showReader(true, true);
            },
        });
        $("listHost").replaceWith(State.list.E);
        State.list.E.id = "listHost";
        const pane = document.querySelector(".list-pane");
        const layout = document.querySelector(".layout");
        State.ld = Sw.ListDetail({
            List: pane, Detail: $("reader"), Back: false, Breakpoint: 640, Class: "mail-ld",
            Label: _TF("Emails and the selected email", "Accessible name of the list and reader area of the fake email inbox"),
            OnBack: () => document.body.classList.remove("reading"),
            OnLayout: narrow => document.body.classList.toggle("reading", narrow && State.ld && State.ld.Showing === "detail"),
        });
        layout.append(State.ld.E);
        //  No email selected: the shared state view (the key hints stay below it)
        $("readerEmpty").prepend(Sw.StateView({ Icon: "mail", Title: _TF("Select an email to read it", "Text of the empty reader of the fake email inbox") }).E);
    }

    /**
     * Page entry point (body onload): translates the static folder labels, restores the read ids, shows the back link
     * (stand alone), wires the controls and starts the long poll; the loading phase ends after the first answer.
     */
    function main() {
        const done = Sw.Page.Loading();
        State.title = document.title;
        Folders = folders();
        for (const li of $("folders").children)
            li.querySelector(".label").textContent = Folders[li.dataset.folder].title;
        migrateTheme();
        State.read = new Set(load("read", []));
        initAppBack();
        //  The bulk actions as page header actions (wide screens; the More menu on narrow ones)
        $("markAllReadTop").textContent = _T("Mark all as read", "Menu item of the fake email inbox that marks all emails as read");
        $("clearAllTop").textContent = _T("Delete all", "Button that deletes all fake emails");
        //  Inside the app shell the page title is the menu item name (Design v2: h1 = breadcrumb = menu, "Fake email inbox")
        const menuTitle = initInShell() && typeof Sw.MenuTitle === "function" ? Sw.MenuTitle() : null;
        if (menuTitle) {
            document.querySelector(".brand h1").textContent = menuTitle;
            document.title = State.title = menuTitle;
        }
        initListReader();
        nameButtons();
        //  Touch devices have no "/" key hint to show
        if (window.matchMedia && window.matchMedia("(hover: none)").matches)
            $("search").placeholder = _TF("Search emails", "Place holder of the search box of the fake email inbox");
        wire();
        let first = true;
        pollLoop(() => {
            if (!first)
                return;
            first = false;
            done();
        });
    }

    window.fakeEmailMain = main;
})();
