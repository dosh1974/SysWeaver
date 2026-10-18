/**
 * @file The complete behavior of the standalone "Fake Mail" page (fakeEmail/index.html), the web inbox of
 * FakeEmailService (menu link "Debug/FakeEmail").
 * Loading: a static `<script src="email.js" defer>` in index.html; the last line calls init() immediately (relies on
 * `defer`, so the static DOM of index.html exists). It does NOT use common.js, the app shell, theme.css or
 * translations (_T): it has its own fetch wrapper (call), toasts, theme handling, <dialog> composer and English text.
 * Globals (strict-mode classic script): Api, $, State, Folders, Today, Composer and all functions below
 * (load, save, call, el, toast, errText, hue, party, displayName, avatar, shortTime, longTime, appendHighlighted,
 * appendLinkified, frameHtml, matches, visible, renderSide, renderList, renderAll, select, showMail, renderBody,
 * openInTab, deleteSelected, removeLocal, move, saveRead, setStatus, pollLoop, applyList, setComposeMode,
 * setComposeFormat, showPreview, openComposer, quote, reply, sendComposed, applyTheme, initTheme, init).
 * Dependencies: only the FakeEmailService web API (JSON POST to Api + method: GetEmails (long poll), GetEmail,
 * DeleteEmail, Clear, SendEmail, FakeIncoming) and the element ids of index.html.
 * Conventions: element ids are looked up with $(id); the list is fully rebuilt on every change; live updates come
 * only from long-polling GetEmails; per-browser state lives in localStorage under the "fakeEmail." prefix
 * (read, theme, lastFrom, lastName); the URL hash holds the selected email id; `?api=` overrides the API base.
 * Server DTO field names relied on: Id, Time, From, To, SenderName, Subject, Preview, Incoming, Error, System,
 * Body, IsHtml, InReplyTo (FakeEmailSummary / full email), Version, From, Unchanged, Emails (GetEmails response).
 */
"use strict";

//  The api base url, can be overridden using ?api=<url>
/**
 * API base URL: the `?api=` query parameter or "../Api/fakeEmail/", normalized to always end with "/".
 * @type {string}
 */
const Api = (new URLSearchParams(location.search).get("api") || "../Api/fakeEmail/").replace(/\/?$/, "/");

/**
 * Shorthand for document.getElementById.
 * @param {string} id - Element id (from index.html).
 * @returns {HTMLElement|null} The element, or null.
 */
const $ = id => document.getElementById(id);

/**
 * The single mutable page state.
 * - version {number}: last GetEmails Version (sent back to long-poll for changes).
 * - from {string}: the system's sender address (shown in #fromAddress).
 * - emails {Array<object>}: current list of FakeEmailSummary objects (server order).
 * - byId {Map<string,object>}: id -> summary.
 * - folder {string}: current folder key of Folders ("all" | "out" | "in" | "failed").
 * - system {string|null}: system filter; null = all systems, "" = "(no system)".
 * - query {string}: lower-cased, trimmed search text.
 * - selected {string|null}: selected email id.
 * - view {string}: HTML body view mode, "render" or "source".
 * - current {object|null}: the loaded full email shown in the reader.
 * - known {Set<string>|null}: ids seen at the last list render (marks new rows "fresh"); null before the first list.
 * - read {Set<string>}: ids of read emails (persisted as fakeEmail.read).
 * @type {object}
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
};

/**
 * Folder definitions keyed by folder key (matches li[data-folder] and span[data-count] in index.html).
 * Each entry: title (list header text), test(e) predicate on a summary (all: true; out: !Incoming; in: Incoming;
 * failed: !!Error), empty (empty-list message).
 * @type {Object<string, {title:string, test:function(object):boolean, empty:string}>}
 */
const Folders = {
    all: { title: "All mail", test: () => true, empty: "No emails yet" },
    out: { title: "Sent by system", test: e => !e.Incoming, empty: "The system hasn't sent any emails yet" },
    in: { title: "Received by system", test: e => e.Incoming, empty: "No emails have been faked yet" },
    failed: { title: "Failed", test: e => !!e.Error, empty: "No failed emails" },
};


//  Storage (best effort)

/**
 * Reads a JSON value from localStorage key "fakeEmail." + key.
 * @param {string} key - Key suffix (read, theme, lastFrom, lastName).
 * @param {*} def - Value returned when the key is missing or on any error (storage blocked, bad JSON).
 * @returns {*} The parsed value or def.
 */
function load(key, def) {
    try {
        const v = localStorage.getItem("fakeEmail." + key);
        return v == null ? def : JSON.parse(v);
    } catch {
        return def;
    }
}

/**
 * Writes JSON.stringify(value) to localStorage key "fakeEmail." + key; errors are silently ignored.
 * @param {string} key - Key suffix.
 * @param {*} value - Value to store.
 * @returns {void}
 */
function save(key, value) {
    try {
        localStorage.setItem("fakeEmail." + key, JSON.stringify(value));
    } catch {
    }
}


//  Api

/**
 * Calls a FakeEmailService API method: fetch(Api + method) as POST with a JSON body (data ?? null),
 * Content-Type application/json and cache "no-store". (Own wrapper, not common.js sendRequest.)
 * @param {string} method - API method name (GetEmails, GetEmail, DeleteEmail, Clear, SendEmail, FakeIncoming).
 * @param {*} [data] - Request object; null/undefined is sent as JSON null.
 * @returns {Promise<*>} Parsed JSON response, or null for an empty body.
 * @throws {Error} On a non-OK status, with the response text (or "status statusText") as message.
 */
async function call(method, data) {
    const res = await fetch(Api + method, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data ?? null),
        cache: "no-store",
    });
    const text = await res.text();
    if (!res.ok)
        throw new Error(text || (res.status + " " + res.statusText));
    return text.length > 0 ? JSON.parse(text) : null;
}


//  Helpers

/**
 * Creates an element.
 * @param {string} tag - Tag name.
 * @param {string} [cls] - className to set (skipped when falsy).
 * @param {*} [text] - textContent to set (skipped when null/undefined).
 * @returns {HTMLElement} The new (detached) element.
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
 * Shows a transient message: appends a div.toast (plus "err" for errors) to #toasts, removed after 3 s (7 s for errors).
 * @param {string} text - Message text.
 * @param {boolean} [isError] - True for an error toast.
 * @returns {void}
 */
function toast(text, isError) {
    const t = el("div", "toast" + (isError ? " err" : ""), text);
    $("toasts").append(t);
    setTimeout(() => t.remove(), isError ? 7000 : 3000);
}

/**
 * Gets a displayable error text.
 * @param {*} e - Error or any thrown value.
 * @returns {string} e.message if present, else String(e).
 */
function errText(e) {
    return (e && e.message) ? e.message : String(e);
}

/**
 * Deterministic string hash (h = h * 31 + charCode, 32-bit) mapped to a hue; used for avatar colors.
 * @param {string} s - Input string.
 * @returns {number} Hue in degrees, 0..359.
 */
function hue(s) {
    let h = 0;
    for (let i = 0; i < s.length; ++i)
        h = (h * 31 + s.charCodeAt(i)) | 0;
    return Math.abs(h) % 360;
}

//  The "other party" of an email (the user)
/**
 * The user side of an email: From for incoming mail, To for outgoing mail.
 * @param {object} e - Email summary or full email.
 * @returns {string} The address, or "".
 */
function party(e) {
    return e.Incoming ? (e.From || "") : (e.To || "");
}

/**
 * Display name of the other party: SenderName for incoming mail that has one, else party(e).
 * @param {object} e - Email summary or full email.
 * @returns {string} The name or address.
 */
function displayName(e) {
    if (e.Incoming && e.SenderName)
        return e.SenderName;
    return party(e);
}

/**
 * Creates a round letter avatar: div.avatar (+ "big") holding the first character of displayName(e) (or "?"),
 * with an inline background hsl(hue(lower-cased party) 55% 50%).
 * @param {object} e - Email summary or full email.
 * @param {boolean} [big] - True for the large reader avatar.
 * @returns {HTMLElement} The avatar element.
 */
function avatar(e, big) {
    const p = party(e);
    const a = el("div", "avatar" + (big ? " big" : ""), (displayName(e) || "?").trim().charAt(0) || "?");
    a.style.background = "hsl(" + hue(p.toLowerCase()) + " 55% 50%)";
    return a;
}

/**
 * Today's date string, used by shortTime to detect "today".
 * @returns {string} new Date().toDateString().
 */
const Today = () => new Date().toDateString();

/**
 * List timestamp (locale formatted): HH:MM for today, "Mon D" within the current year, else "Mon D, YYYY".
 * @param {Date} d - Date.
 * @returns {string} Formatted text.
 */
function shortTime(d) {
    if (d.toDateString() === Today())
        return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    const sameYear = d.getFullYear() === new Date().getFullYear();
    return d.toLocaleDateString([], sameYear ? { month: "short", day: "numeric" } : { year: "numeric", month: "short", day: "numeric" });
}

/**
 * Long timestamp: toLocaleString with dateStyle and timeStyle "medium".
 * @param {Date} d - Date.
 * @returns {string} Formatted text.
 */
function longTime(d) {
    return d.toLocaleString([], { dateStyle: "medium", timeStyle: "medium" });
}

/**
 * Appends text to parent, wrapping every case-insensitive occurrence of query in a <mark> (search hit highlight).
 * @param {HTMLElement} parent - Target element.
 * @param {string} text - Text (null/undefined treated as "").
 * @param {string} query - Lower-case search text; falsy appends the text unchanged.
 * @returns {void}
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
        parent.append(text.substring(p, i), el("mark", null, text.substr(i, query.length)));
        p = i + query.length;
    }
    parent.append(text.substring(p));
}

//  Append plain text with any urls turned into links
/**
 * Appends plain text, turning http(s):// URLs (trailing punctuation excluded) into
 * <a target="_blank" rel="noopener noreferrer"> links.
 * @param {HTMLElement} parent - Target element.
 * @param {string} text - Plain text.
 * @returns {void}
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

//  Html for an iframe: links opens in a new tab, no scripts (the iframe is sandboxed)
/**
 * Wraps an HTML body into a document for an iframe srcdoc or Blob: <base target="_blank">, UTF-8 meta and a fixed
 * light inline style (system-ui font, 16px margin, images max 100% wide). Used by renderBody, showPreview, openInTab.
 * @param {string} body - HTML body (null/undefined treated as "").
 * @returns {string} The HTML document text.
 */
function frameHtml(body) {
    return "<!DOCTYPE html><base target=\"_blank\"><meta charset=\"utf-8\"><style>body{font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;margin:16px;color:#1d2330;word-break:break-word}img{max-width:100%}</style>" + (body || "");
}


//  List

/**
 * List filter: the current folder's test, the system filter (State.system, "" for no system) and the search query
 * (substring of From, To, SenderName, Subject, Preview or System, lower-cased).
 * @param {object} e - Email summary.
 * @returns {boolean} True when the email is shown.
 */
function matches(e) {
    if (!Folders[State.folder].test(e))
        return false;
    if (State.system !== null && (e.System || "") !== State.system)
        return false;
    const q = State.query;
    if (!q)
        return true;
    return [e.From, e.To, e.SenderName, e.Subject, e.Preview, e.System].some(x => x && x.toLowerCase().includes(q));
}

/**
 * The emails currently shown in the list.
 * @returns {Array<object>} State.emails filtered by matches.
 */
function visible() {
    return State.emails.filter(matches);
}

/**
 * Updates the sidebar: each folder counter [data-count=key] shows the unread count when > 0 (class "unread"),
 * otherwise the total (or empty), with a "u unread of n" tooltip; toggles "on" on the #folders item of State.folder.
 * Rebuilds #systems from the distinct System values (sorted) and hides #systemsBox unless there are at least two
 * systems or a single non-empty one.
 * @returns {void}
 */
function renderSide() {
    /**
     * Inner predicate: email is not in State.read.
     * @param {object} e - Email summary.
     * @returns {boolean} True when unread.
     */
    const unread = e => !State.read.has(e.Id);
    for (const [key, f] of Object.entries(Folders)) {
        const all = State.emails.filter(f.test);
        const u = all.filter(unread).length;
        const c = document.querySelector("[data-count=\"" + key + "\"]");
        c.textContent = u > 0 ? u : (all.length || "");
        c.classList.toggle("unread", u > 0);
        c.title = u + " unread of " + all.length;
    }
    for (const li of $("folders").children)
        li.classList.toggle("on", li.dataset.folder === State.folder);

    const systems = [...new Set(State.emails.map(e => e.System || ""))].sort();
    const box = $("systemsBox");
    box.hidden = systems.length < 2 && !(systems.length === 1 && systems[0]);
    const ul = $("systems");
    ul.replaceChildren();
    if (!box.hidden) {
        /**
         * Inner helper: appends an li (class "on" when it is the active filter) with a span.dot and the label to
         * #systems. Clicking it toggles State.system between value and null and calls renderAll().
         * @param {string} value - System value ("" = no system).
         * @param {string} label - Display text.
         * @returns {void}
         */
        const add = (value, label) => {
            const li = el("li", value === State.system ? "on" : "");
            li.append(el("span", "dot"), label);
            li.onclick = () => {
                State.system = State.system === value ? null : value;
                renderAll();
            };
            ul.append(li);
        };
        for (const s of systems)
            add(s, s || "(no system)");
    }
}

/**
 * Rebuilds #list from visible(). Each email becomes li.item[data-id] with classes sel (selected), unread (not in
 * State.read) and fresh (not in State.known), containing an avatar, a .who line (span.dir.in/out "FROM"/"TO",
 * highlighted name, optional span.tag.failed), a <time> (shortTime, longTime tooltip), highlighted .subj and .prev.
 * Clicking a row calls select(id). Afterwards resets State.known to all current ids and updates #folderTitle
 * (folder title + system filter), #listInfo ("n emails" or "n of m"), #empty visibility and #emptyText.
 * @returns {void}
 */
function renderList() {
    const list = $("list");
    const items = visible();
    const q = State.query;
    const fresh = State.known;
    list.replaceChildren();
    for (const e of items) {
        const li = el("li", "item");
        li.dataset.id = e.Id;
        if (e.Id === State.selected)
            li.classList.add("sel");
        if (!State.read.has(e.Id))
            li.classList.add("unread");
        if (fresh && !fresh.has(e.Id))
            li.classList.add("fresh");

        const who = el("div", "who");
        who.append(el("span", "dir " + (e.Incoming ? "in" : "out"), e.Incoming ? "FROM" : "TO"));
        const name = el("span");
        appendHighlighted(name, displayName(e), q);
        who.append(name);
        if (e.Error)
            who.append(el("span", "tag failed", "Failed"));
        const time = el("time", null, shortTime(new Date(e.Time)));
        time.title = longTime(new Date(e.Time));
        const subj = el("div", "subj");
        appendHighlighted(subj, e.Subject || "(no subject)", q);
        const prev = el("div", "prev");
        appendHighlighted(prev, e.Preview, q);
        li.append(avatar(e), who, time, subj, prev);
        li.onclick = () => select(e.Id);
        list.append(li);
    }
    State.known = new Set(State.emails.map(e => e.Id));

    const f = Folders[State.folder];
    $("folderTitle").textContent = f.title + (State.system !== null ? " · " + (State.system || "(no system)") : "");
    $("listInfo").textContent = items.length === State.emails.length ? items.length + " emails" : items.length + " of " + State.emails.length;
    $("empty").hidden = items.length > 0;
    $("emptyText").textContent = State.query ? "Nothing matches “" + State.query + "”" : f.empty;
}

/**
 * Re-renders sidebar and list (renderSide then renderList).
 * @returns {void}
 */
function renderAll() {
    renderSide();
    renderList();
}


//  Reader

/**
 * Selects an email (or clears the selection) and loads it into the reader.
 * Toggles "sel" on the list rows. A falsy id clears State.current, hides #mail, shows #readerEmpty, removes
 * body.reading and strips the URL hash. Otherwise sets the URL hash to the encoded id (history.replaceState), adds
 * body.reading (mobile layout), scrolls the selected row into view unless noScroll, marks an unread email read
 * (saveRead, renderSide, removes "unread" from the row), then fetches GetEmail {Id} and calls showMail if the
 * selection has not changed meanwhile; errors become an error toast (also only if still selected).
 * @param {string|null} id - Email id, or null/"" to deselect.
 * @param {boolean} [noScroll] - True to skip scrolling the row into view.
 * @returns {Promise<void>} Resolves after the email was loaded (or failed).
 */
async function select(id, noScroll) {
    State.selected = id;
    for (const li of $("list").children)
        li.classList.toggle("sel", li.dataset.id === id);
    if (!id) {
        State.current = null;
        $("mail").hidden = true;
        $("readerEmpty").hidden = false;
        document.body.classList.remove("reading");
        if (location.hash)
            history.replaceState(null, "", location.pathname + location.search);
        return;
    }
    history.replaceState(null, "", "#" + encodeURIComponent(id));
    document.body.classList.add("reading");
    if (!noScroll)
        $("list").querySelector(".sel")?.scrollIntoView({ block: "nearest" });
    if (!State.read.has(id)) {
        State.read.add(id);
        saveRead();
        renderSide();
        $("list").querySelector(".sel")?.classList.remove("unread");
    }
    try {
        const e = await call("GetEmail", { Id: id });
        if (State.selected === id)
            showMail(e);
    } catch (ex) {
        if (State.selected === id)
            toast(errText(ex), true);
    }
}

/**
 * Fills the reader with a full email: sets State.current, #mailSubject, replaces #mailAvatar with a big avatar,
 * #mailFromName (sender name/address for incoming, "System" for outgoing), #mailFrom ("<address>"), #mailTo,
 * #mailTime (text + dateTime). Builds #mailTags: direction (tag in/out), HTML/Plain text, "System: x",
 * "Reply to <id>" (clickable -> select(InReplyTo) when that email is in State.byId) and the id.
 * Shows #mailError with e.Error, shows #viewMode only for HTML mail, then calls renderBody().
 * @param {object} e - Full email from GetEmail (summary fields plus Body, IsHtml, InReplyTo).
 * @returns {void}
 */
function showMail(e) {
    State.current = e;
    $("readerEmpty").hidden = true;
    $("mail").hidden = false;
    $("mailSubject").textContent = e.Subject || "(no subject)";
    $("mailAvatar").replaceWith(Object.assign(avatar(e, true), { id: "mailAvatar" }));
    $("mailFromName").textContent = e.Incoming ? (e.SenderName || e.From) : "System";
    $("mailFrom").textContent = "<" + (e.From || "") + ">";
    $("mailTo").textContent = e.To || "";
    const d = new Date(e.Time);
    $("mailTime").textContent = longTime(d);
    $("mailTime").dateTime = d.toISOString();

    const tags = $("mailTags");
    tags.replaceChildren(
        el("span", "tag " + (e.Incoming ? "in" : "out"), e.Incoming ? "Received by system" : "Sent by system"),
        el("span", "tag", e.IsHtml ? "HTML" : "Plain text"));
    if (e.System)
        tags.append(el("span", "tag", "System: " + e.System));
    if (e.InReplyTo) {
        const t = el("span", "tag", "Reply to " + e.InReplyTo);
        if (State.byId.has(e.InReplyTo)) {
            t.style.cursor = "pointer";
            t.title = "Show the original email";
            t.onclick = () => select(e.InReplyTo);
        }
        tags.append(t);
    }
    tags.append(el("span", "tag", e.Id));

    $("mailError").hidden = !e.Error;
    $("mailError").textContent = e.Error || "";
    $("viewMode").hidden = !e.IsHtml;
    renderBody();
}

/**
 * Renders State.current.Body into #mailBody (cleared first; nothing when no current email) and toggles "on" on the
 * #viewMode buttons matching State.view. HTML in "render" view -> <iframe sandbox="allow-popups
 * allow-popups-to-escape-sandbox"> (no scripts) with srcdoc = frameHtml(body). HTML in "source" view -> <pre> with
 * the raw text. Plain text -> pre.text with linkified URLs.
 * @returns {void}
 */
function renderBody() {
    const e = State.current;
    const host = $("mailBody");
    host.replaceChildren();
    if (!e)
        return;
    const body = e.Body || "";
    for (const b of $("viewMode").children)
        b.classList.toggle("on", b.dataset.view === State.view);
    if (e.IsHtml && State.view === "render") {
        const f = el("iframe");
        f.setAttribute("sandbox", "allow-popups allow-popups-to-escape-sandbox");
        f.title = "Email body";
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
 * Opens the current email body in a new tab from a Blob URL (text/html via frameHtml, or text/plain;charset=utf-8);
 * the URL is revoked after 60 s. Does nothing without a current email.
 * @returns {void}
 */
function openInTab() {
    const e = State.current;
    if (!e)
        return;
    const blob = e.IsHtml
        ? new Blob([frameHtml(e.Body)], { type: "text/html" })
        : new Blob([e.Body || ""], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    window.open(url, "_blank", "noopener");
    setTimeout(() => URL.revokeObjectURL(url), 60000);
}

/**
 * Deletes the selected email: DeleteEmail {Id}, then removes it from State.read (saveRead) and the local list
 * (removeLocal), and selects the next visible email, else the previous one, else none. Errors become an error toast.
 * @returns {Promise<void>}
 */
async function deleteSelected() {
    const id = State.selected;
    if (!id)
        return;
    const items = visible();
    const i = items.findIndex(e => e.Id === id);
    const next = items[i + 1] || items[i - 1];
    try {
        await call("DeleteEmail", { Id: id });
        State.read.delete(id);
        saveRead();
        removeLocal(id);
        select(next ? next.Id : null);
    } catch (ex) {
        toast(errText(ex), true);
    }
}

/**
 * Removes an email from State.emails and State.byId and re-renders (renderAll). No server call.
 * @param {string} id - Email id.
 * @returns {void}
 */
function removeLocal(id) {
    State.emails = State.emails.filter(e => e.Id !== id);
    State.byId.delete(id);
    renderAll();
}

/**
 * Keyboard navigation: selects the visible email delta steps from the selected one, clamped to the list;
 * with no (visible) selection it selects the first. Does nothing for an empty list.
 * @param {number} delta - Step (+1 next, -1 previous).
 * @returns {void}
 */
function move(delta) {
    const items = visible();
    if (items.length <= 0)
        return;
    let i = items.findIndex(e => e.Id === State.selected);
    i = i < 0 ? 0 : Math.max(0, Math.min(items.length - 1, i + delta));
    select(items[i].Id);
}

/**
 * Persists the read set: prunes State.read to ids still in State.byId (only when byId is non-empty), then saves it
 * as localStorage "fakeEmail.read" (array).
 * @returns {void}
 */
function saveRead() {
    //  Only keep ids that still exists
    if (State.byId.size > 0)
        for (const id of State.read)
            if (!State.byId.has(id))
                State.read.delete(id);
    save("read", [...State.read]);
}


//  Live updates (long polling)

/**
 * Sets the connection indicator #status to class "status on" or "status off" with text as tooltip.
 * @param {boolean} on - Connected.
 * @param {string} text - Tooltip text.
 * @returns {void}
 */
function setStatus(on, text) {
    const s = $("status");
    s.className = "status " + (on ? "on" : "off");
    s.title = text;
}

/**
 * Endless long-poll loop: GetEmails {Version: State.version}. The server holds the request until the version changes
 * or its timeout passes and returns {Version, From, Unchanged} or {Version, From, Emails[]}. On success: status
 * connected, delay reset to 1 s, State.version updated, State.from / #fromAddress updated when From changed, and
 * applyList(Emails) unless Unchanged. On error: status disconnected, then waits with exponential back-off
 * (1 s doubling, max 15 s).
 * @returns {Promise<void>} Never resolves.
 */
async function pollLoop() {
    let delay = 1000;
    for (; ;) {
        try {
            const r = await call("GetEmails", { Version: State.version });
            setStatus(true, "Connected, updates are live");
            delay = 1000;
            State.version = r.Version;
            if (r.From !== State.from) {
                State.from = r.From || "";
                $("fromAddress").textContent = State.from;
            }
            if (r.Unchanged)
                continue;
            applyList(r.Emails || []);
        } catch (ex) {
            setStatus(false, "Disconnected: " + errText(ex));
            await new Promise(res => setTimeout(res, delay));
            delay = Math.min(delay * 2, 15000);
        }
    }
}

/**
 * Replaces the whole email list (State.emails, State.byId) with a GetEmails result and re-renders.
 * First call (State.known === null): if no "fakeEmail.read" was ever stored, all existing emails count as read;
 * saves the read set; after rendering selects the email named in the URL hash, if it exists.
 * Later calls: detects new ids and, if any, sets document.title to "(n) Fake Mail" for 4 s and shows a toast
 * ("Received: subject" / "Sent: subject" for one, "n new emails" for more).
 * Clears the selection when the selected email no longer exists.
 * @param {Array<object>} emails - FakeEmailSummary list.
 * @returns {void}
 */
function applyList(emails) {
    const first = State.known === null;
    const newOnes = first ? [] : emails.filter(e => !State.byId.has(e.Id));
    State.emails = emails;
    State.byId = new Map(emails.map(e => [e.Id, e]));
    if (first) {
        //  Everything that existed before this page was ever opened counts as read
        const stored = load("read", null);
        if (stored === null)
            emails.forEach(e => State.read.add(e.Id));
        saveRead();
    }
    renderAll();
    if (State.selected && !State.byId.has(State.selected))
        select(null);
    if (first) {
        const id = decodeURIComponent(location.hash.substring(1));
        if (id && State.byId.has(id))
            select(id);
    }
    if (newOnes.length > 0) {
        const n = newOnes.length;
        document.title = "(" + n + ") Fake Mail";
        setTimeout(() => document.title = "Fake Mail", 4000);
        if (n === 1)
            toast((newOnes[0].Incoming ? "Received: " : "Sent: ") + (newOnes[0].Subject || "(no subject)"));
        else
            toast(n + " new emails");
    }
}


//  Composer

/**
 * Composer dialog state: mode "in" (fake an email from a user to the system, FakeIncoming) or "out" (the system
 * sends an email, SendEmail); format "text" or "html".
 * @type {{mode:string, format:string}}
 */
const Composer = {
    mode: "in",
    format: "text",
};

/**
 * Sets Composer.mode: toggles class "out" on #composer (CSS hides the .in-only fields), marks the matching
 * #composeMode button "on", sets the #cTo placeholder (system address for "in", user@example.com for "out") and
 * the #composeHint text.
 * @param {string} mode - "in" or "out".
 * @returns {void}
 */
function setComposeMode(mode) {
    Composer.mode = mode;
    const d = $("composer");
    d.classList.toggle("out", mode === "out");
    for (const b of $("composeMode").children)
        b.classList.toggle("on", b.dataset.mode === mode);
    $("cTo").placeholder = mode === "in" ? (State.from || "system address") : "user@example.com";
    $("composeHint").textContent = mode === "in"
        ? "Fakes an email from a user, it's returned by GetIncoming"
        : "Sends an email using this service, as if the system sent it";
}

/**
 * Sets Composer.format: toggles class "html" on #composer (monospace textarea), marks the matching #composeFormat
 * button "on", shows #cPreviewBtn only for HTML and closes the preview when leaving HTML.
 * @param {string} format - "text" or "html".
 * @returns {void}
 */
function setComposeFormat(format) {
    Composer.format = format;
    $("composer").classList.toggle("html", format === "html");
    for (const b of $("composeFormat").children)
        b.classList.toggle("on", b.dataset.format === format);
    $("cPreviewBtn").hidden = format !== "html";
    if (format !== "html")
        showPreview(false);
}

/**
 * Switches the composer body between the #cBody textarea and the sandboxed #cPreview iframe
 * (srcdoc = frameHtml(#cBody value) when turned on), and sets #cPreviewBtn text to "Edit" / "Preview".
 * @param {boolean} on - True to show the preview.
 * @returns {void}
 */
function showPreview(on) {
    const p = $("cPreview");
    p.hidden = !on;
    $("cBody").hidden = on;
    $("cPreviewBtn").textContent = on ? "Edit" : "Preview";
    if (on)
        p.srcdoc = frameHtml($("cBody").value);
}

/**
 * Resets and opens the composer dialog (#composer.showModal()). Mode defaults to "in", format to "text"; the preview
 * is closed. From / Name default to the stored fakeEmail.lastFrom ("user@example.com") / fakeEmail.lastName ("").
 * Focuses opts.focus, else #cFrom (in mode with empty From), else #cSubject.
 * @param {{mode?:string, format?:string, from?:string, name?:string, to?:string, subject?:string, system?:string,
 *   body?:string, replyTo?:string, focus?:string}} [opts] - Initial values; focus is an element id.
 * @returns {void}
 */
function openComposer(opts) {
    opts = opts || {};
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
    $("composer").showModal();
    (opts.focus ? $(opts.focus) : (Composer.mode === "in" && !$("cFrom").value ? $("cFrom") : $("cSubject"))).focus();
}

/**
 * Builds a reply quote "On <longTime>, <who> wrote:" plus the original body: a styled <blockquote> for HTML mail,
 * "> "-prefixed lines (after two blank lines) for plain text. who = SenderName || From for incoming, else From.
 * @param {object} e - Full email.
 * @returns {string} Quote text / HTML.
 */
function quote(e) {
    const when = longTime(new Date(e.Time));
    const who = e.Incoming ? (e.SenderName || e.From) : e.From;
    if (e.IsHtml)
        return "<p></p>\n<blockquote style=\"margin:0 0 0 .8ex;border-left:2px solid #ccc;padding-left:1ex\">\n<p>On " + when + ", " + who + " wrote:</p>\n" + (e.Body || "") + "\n</blockquote>";
    return "\n\nOn " + when + ", " + who + " wrote:\n" + (e.Body || "").split("\n").map(l => "> " + l).join("\n");
}

/**
 * Opens the composer as a reply to State.current (no-op without one), with "Re: " prefixed unless present, the same
 * format and a quote(e) body, focus in the body with the caret at the start.
 * Incoming email -> "out" mode (system replies) to e.From. Outgoing email -> "in" mode (user replies) from e.To to
 * e.From with replyTo = e.Id.
 * @returns {void}
 */
function reply() {
    const e = State.current;
    if (!e)
        return;
    const subject = /^re:/i.test(e.Subject || "") ? e.Subject : "Re: " + (e.Subject || "");
    if (e.Incoming) {
        //  Reply as the system to the user
        openComposer({ mode: "out", to: e.From, subject: subject, format: e.IsHtml ? "html" : "text", body: quote(e), focus: "cBody" });
    } else {
        //  Reply as the user to the system
        openComposer({ mode: "in", from: e.To, to: e.From, subject: subject, format: e.IsHtml ? "html" : "text", body: quote(e), replyTo: e.Id, focus: "cBody" });
    }
    const b = $("cBody");
    b.setSelectionRange(0, 0);
    b.scrollTop = 0;
}

/**
 * Sends the composer content as FakeEmailRequest {From, SenderName, To, Subject, Body, IsHtml, System, InReplyTo}
 * (From, SenderName, System, InReplyTo only in "in" mode, else null) to FakeIncoming ("in") or SendEmail ("out"),
 * with #composeSend disabled during the call. On success: saves lastFrom/lastName ("in" mode), closes the dialog
 * and marks the returned id read. Errors become an error toast. The list itself updates via pollLoop.
 * @returns {Promise<void>}
 */
async function sendComposed() {
    const isIn = Composer.mode === "in";
    const req = {
        From: isIn ? $("cFrom").value.trim() : null,
        SenderName: isIn ? $("cName").value.trim() : null,
        To: $("cTo").value.trim() || null,
        Subject: $("cSubject").value,
        Body: $("cBody").value,
        IsHtml: Composer.format === "html",
        System: isIn ? ($("cSystem").value.trim() || null) : null,
        InReplyTo: isIn ? ($("cReplyTo").value || null) : null,
    };
    const btn = $("composeSend");
    btn.disabled = true;
    try {
        const id = await call(isIn ? "FakeIncoming" : "SendEmail", req);
        if (isIn) {
            save("lastFrom", req.From);
            save("lastName", req.SenderName);
        }
        $("composer").close();
        //  We sent it, so it's read
        if (id) {
            State.read.add(id);
            saveRead();
        }
    } catch (ex) {
        toast(errText(ex), true);
    } finally {
        btn.disabled = false;
    }
}


//  Theme

/**
 * Applies a theme by setting <html data-theme> (selects the :root[data-theme="dark"] palette in email.css).
 * @param {string} t - "light" or "dark".
 * @returns {void}
 */
function applyTheme(t) {
    document.documentElement.dataset.theme = t;
}

/**
 * Applies the stored fakeEmail.theme, else the OS prefers-color-scheme, and follows OS changes while nothing is
 * stored. Wires the #theme button to toggle light/dark and store the choice.
 * @returns {void}
 */
function initTheme() {
    const mq = matchMedia("(prefers-color-scheme: dark)");
    const stored = load("theme", null);
    applyTheme(stored || (mq.matches ? "dark" : "light"));
    mq.addEventListener("change", ev => {
        if (!load("theme", null))
            applyTheme(ev.matches ? "dark" : "light");
    });
    $("theme").onclick = () => {
        const t = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
        save("theme", t);
        applyTheme(t);
    };
}


//  Init

/**
 * Page initialization (called at the end of this script): initTheme, restores State.read from fakeEmail.read,
 * wires folder items (set State.folder), the 120 ms debounced #search input (State.query, renderList), the #viewMode
 * buttons (State.view, renderBody), #compose, #reply, #delete, #openTab, #back, #markAllRead (all read), #clearAll
 * (confirm, then Clear and deselect), composer mode/format buttons, #cPreviewBtn, close/cancel, form submit and
 * Ctrl/Cmd+Enter (sendComposed). Global shortcuts (only when the composer is closed, no modifier, focus not in an
 * input/textarea - where Escape blurs): j/ArrowDown, k/ArrowUp (move), r (reply), c (compose), / (focus search),
 * Delete (deleteSelected), Escape (deselect). Finally starts pollLoop().
 * @returns {void}
 */
function init() {
    initTheme();
    State.read = new Set(load("read", []));

    for (const li of $("folders").children)
        li.onclick = () => {
            State.folder = li.dataset.folder;
            renderAll();
        };

    let searchTimer = 0;
    $("search").oninput = ev => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => {
            State.query = ev.target.value.trim().toLowerCase();
            renderList();
        }, 120);
    };

    for (const b of $("viewMode").children)
        b.onclick = () => {
            State.view = b.dataset.view;
            renderBody();
        };

    $("compose").onclick = () => openComposer();
    $("reply").onclick = reply;
    $("delete").onclick = deleteSelected;
    $("openTab").onclick = openInTab;
    $("back").onclick = () => select(null);

    $("markAllRead").onclick = () => {
        State.emails.forEach(e => State.read.add(e.Id));
        saveRead();
        renderAll();
    };
    $("clearAll").onclick = async () => {
        if (State.emails.length <= 0 || !confirm("Delete all " + State.emails.length + " emails?"))
            return;
        try {
            await call("Clear");
            select(null);
        } catch (ex) {
            toast(errText(ex), true);
        }
    };

    for (const b of $("composeMode").children)
        b.onclick = () => setComposeMode(b.dataset.mode);
    for (const b of $("composeFormat").children)
        b.onclick = () => setComposeFormat(b.dataset.format);
    $("cPreviewBtn").onclick = () => showPreview($("cPreview").hidden);
    $("composeClose").onclick = $("composeCancel").onclick = () => $("composer").close();
    $("composeForm").onsubmit = ev => {
        ev.preventDefault();
        sendComposed();
    };
    $("composer").addEventListener("keydown", ev => {
        if (ev.key === "Enter" && (ev.ctrlKey || ev.metaKey)) {
            ev.preventDefault();
            sendComposed();
        }
    });

    document.addEventListener("keydown", ev => {
        if ($("composer").open || ev.ctrlKey || ev.metaKey || ev.altKey)
            return;
        const t = ev.target;
        const typing = t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement;
        if (typing) {
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
                select(null);
                break;
            default:
                return;
        }
        ev.preventDefault();
    });

    pollLoop();
}

init();
