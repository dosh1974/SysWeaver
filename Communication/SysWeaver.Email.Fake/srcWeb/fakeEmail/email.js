"use strict";

//  The api base url, can be overridden using ?api=<url>
const Api = (new URLSearchParams(location.search).get("api") || "../Api/fakeEmail/").replace(/\/?$/, "/");

const $ = id => document.getElementById(id);

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

const Folders = {
    all: { title: "All mail", test: () => true, empty: "No emails yet" },
    out: { title: "Sent by system", test: e => !e.Incoming, empty: "The system hasn't sent any emails yet" },
    in: { title: "Received by system", test: e => e.Incoming, empty: "No emails have been faked yet" },
    failed: { title: "Failed", test: e => !!e.Error, empty: "No failed emails" },
};


//  Storage (best effort)

function load(key, def) {
    try {
        const v = localStorage.getItem("fakeEmail." + key);
        return v == null ? def : JSON.parse(v);
    } catch {
        return def;
    }
}

function save(key, value) {
    try {
        localStorage.setItem("fakeEmail." + key, JSON.stringify(value));
    } catch {
    }
}


//  Api

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

function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls)
        e.className = cls;
    if (text != null)
        e.textContent = text;
    return e;
}

function toast(text, isError) {
    const t = el("div", "toast" + (isError ? " err" : ""), text);
    $("toasts").append(t);
    setTimeout(() => t.remove(), isError ? 7000 : 3000);
}

function errText(e) {
    return (e && e.message) ? e.message : String(e);
}

function hue(s) {
    let h = 0;
    for (let i = 0; i < s.length; ++i)
        h = (h * 31 + s.charCodeAt(i)) | 0;
    return Math.abs(h) % 360;
}

//  The "other party" of an email (the user)
function party(e) {
    return e.Incoming ? (e.From || "") : (e.To || "");
}

function displayName(e) {
    if (e.Incoming && e.SenderName)
        return e.SenderName;
    return party(e);
}

function avatar(e, big) {
    const p = party(e);
    const a = el("div", "avatar" + (big ? " big" : ""), (displayName(e) || "?").trim().charAt(0) || "?");
    a.style.background = "hsl(" + hue(p.toLowerCase()) + " 55% 50%)";
    return a;
}

const Today = () => new Date().toDateString();

function shortTime(d) {
    if (d.toDateString() === Today())
        return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    const sameYear = d.getFullYear() === new Date().getFullYear();
    return d.toLocaleDateString([], sameYear ? { month: "short", day: "numeric" } : { year: "numeric", month: "short", day: "numeric" });
}

function longTime(d) {
    return d.toLocaleString([], { dateStyle: "medium", timeStyle: "medium" });
}

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
function frameHtml(body) {
    return "<!DOCTYPE html><base target=\"_blank\"><meta charset=\"utf-8\"><style>body{font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;margin:16px;color:#1d2330;word-break:break-word}img{max-width:100%}</style>" + (body || "");
}


//  List

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

function visible() {
    return State.emails.filter(matches);
}

function renderSide() {
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

function renderAll() {
    renderSide();
    renderList();
}


//  Reader

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

function removeLocal(id) {
    State.emails = State.emails.filter(e => e.Id !== id);
    State.byId.delete(id);
    renderAll();
}

function move(delta) {
    const items = visible();
    if (items.length <= 0)
        return;
    let i = items.findIndex(e => e.Id === State.selected);
    i = i < 0 ? 0 : Math.max(0, Math.min(items.length - 1, i + delta));
    select(items[i].Id);
}

function saveRead() {
    //  Only keep ids that still exists
    if (State.byId.size > 0)
        for (const id of State.read)
            if (!State.byId.has(id))
                State.read.delete(id);
    save("read", [...State.read]);
}


//  Live updates (long polling)

function setStatus(on, text) {
    const s = $("status");
    s.className = "status " + (on ? "on" : "off");
    s.title = text;
}

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

const Composer = {
    mode: "in",
    format: "text",
};

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

function setComposeFormat(format) {
    Composer.format = format;
    $("composer").classList.toggle("html", format === "html");
    for (const b of $("composeFormat").children)
        b.classList.toggle("on", b.dataset.format === format);
    $("cPreviewBtn").hidden = format !== "html";
    if (format !== "html")
        showPreview(false);
}

function showPreview(on) {
    const p = $("cPreview");
    p.hidden = !on;
    $("cBody").hidden = on;
    $("cPreviewBtn").textContent = on ? "Edit" : "Preview";
    if (on)
        p.srcdoc = frameHtml($("cBody").value);
}

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

function quote(e) {
    const when = longTime(new Date(e.Time));
    const who = e.Incoming ? (e.SenderName || e.From) : e.From;
    if (e.IsHtml)
        return "<p></p>\n<blockquote style=\"margin:0 0 0 .8ex;border-left:2px solid #ccc;padding-left:1ex\">\n<p>On " + when + ", " + who + " wrote:</p>\n" + (e.Body || "") + "\n</blockquote>";
    return "\n\nOn " + when + ", " + who + " wrote:\n" + (e.Body || "").split("\n").map(l => "> " + l).join("\n");
}

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

function applyTheme(t) {
    document.documentElement.dataset.theme = t;
}

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
