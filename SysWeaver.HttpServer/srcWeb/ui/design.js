/**
 * @file ui/design.js - Design v2 prototype behaviour (ui/design.html only; not used by product pages).
 * SwWeb:no-tr-file (a developer prototype with static English demo texts, never shown to users).
 * Static demo data, no server calls. Shows: sidebar (pin/rail, collapse all, expand all, filter), drawer on narrow screens, theme and
 * density switching, a data table with a footer that never moves (fixed page size, fixed slot widths), empty / loading / error states,
 * a property editor (collection add / drag re-order, null value, read-only group, JSON tab that converts both ways), charts with a
 * crosshair tooltip, a confirm dialog, a row menu and toasts.
 */
(function () {
    "use strict";

    const $ = (s, r) => (r || document).querySelector(s);
    const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
    const ico = (id, cls) => `<svg class="dv-ico${cls ? " " + cls : ""}"><use href="#i-${id}"/></svg>`;
    const params = new URLSearchParams(location.search);
    const root = document.documentElement;
    const app = $("#app");

    // ---------------------------------------------------------------- theme / density

    function setTheme(t) {
        if (t === "light")
            root.removeAttribute("data-theme");
        else
            root.setAttribute("data-theme", t);
        $("#themeSel").value = t;
        $("#schemeBtn use").setAttribute("href", t === "light" ? "#i-moon" : "#i-sun");
        drawCharts();
    }

    function setDensity(d) {
        if (d)
            root.setAttribute("data-density", d);
        else
            root.removeAttribute("data-density");
        $("#densSel").value = d || "";
        drawCharts();
    }

    $("#themeSel").addEventListener("change", e => setTheme(e.target.value));
    $("#densSel").addEventListener("change", e => setDensity(e.target.value));
    $("#schemeBtn").addEventListener("click", () => setTheme(root.getAttribute("data-theme") ? "light" : "dark"));
    $("#densBtn").addEventListener("click", ev => {
        Menu(ev.currentTarget, [
            { Text: "Compact", Icon: "rows", Do: () => setDensity("compact") },
            { Text: "Comfortable", Icon: "rows", Do: () => setDensity("comfortable") },
            { Text: "Touch", Icon: "rows", Do: () => setDensity("touch") },
        ]);
    });

    // ---------------------------------------------------------------- navigation

    const NAV = [
        { Items: [["Home", "home", "home"], ["Server", "server", "server"], ["Managed services", "grid"], ["Key files", "key"], ["Process information", "cpu", "table"], ["Drive information", "drive"], ["Chunk storage", "layers"], ["Files", "file"]] },
        { Title: "Monitoring", Items: [["Recent log entries", "list"], ["Log file", "file"], ["Problems", "alert", null, "3"], ["Performance", "gauge"], ["Method calls chart", "chart"], ["Statistics", "chart"], ["Active sessions", "users"], ["Scheduled tasks", "clock"]] },
        { Title: "Operations", Items: [["Cache entries", "memory"], ["Managed folders", "folder"], ["Shared folders", "folder"]] },
        { Title: "Administration", Items: [["Users", "users"], ["API keys", "key"], ["Generate password hash", "lock"]] },
        { Title: "Development", Collapsed: true, Items: [["Explore", "search"], ["Api's", "braces", "editor"], ["Icons", "grid"], ["Data tables", "db"]] },
        { Title: "Data", Collapsed: true, Items: [["Countries", "globe"], ["Currencies", "db"], ["Languages", "globe"], ["Mime types", "file"]] },
    ];
    const CRUMBS = { home: ["Home"], server: ["Server"], table: ["Process information"], editor: ["Development", "Api's", "SetLanguages"] };

    function buildNav() {
        const nav = $("#nav");
        nav.innerHTML = NAV.map((sec, si) => {
            const items = sec.Items.map(([name, icon, view, count]) =>
                `<li><a class="dv-navitem" href="#${view || ""}" data-view="${view || ""}" title="${name}">${ico(icon)}<span>${name}</span>${count ? `<span class="dv-navcount">${count}</span>` : ""}</a></li>`).join("");
            if (!sec.Title)
                return `<ul class="dv-navlist">${items}</ul>`;
            return `<div class="dv-navsec${sec.Collapsed ? " is-collapsed" : ""}" data-sec="${si}"><button class="dv-navsec-head" aria-expanded="${!sec.Collapsed}">${sec.Title}${ico("chev-d")}</button><ul class="dv-navlist">${items}</ul></div>`;
        }).join("");
        $$(".dv-navsec-head", nav).forEach(b => b.addEventListener("click", () => {
            const s = b.parentElement;
            s.classList.toggle("is-collapsed");
            b.setAttribute("aria-expanded", !s.classList.contains("is-collapsed"));
        }));
        $$(".dv-navitem", nav).forEach(a => a.addEventListener("click", ev => {
            ev.preventDefault();
            const v = a.dataset.view;
            if (!v) {
                Toast(`"${a.textContent}" is not part of the prototype.`, "info");
                return;
            }
            show(v);
            app.classList.remove("is-drawer");
        }));
    }

    function setAllSections(collapsed) {
        $$(".dv-navsec").forEach(s => {
            s.classList.toggle("is-collapsed", collapsed);
            $(".dv-navsec-head", s).setAttribute("aria-expanded", !collapsed);
        });
    }

    $('[data-act="collapse"]').addEventListener("click", () => setAllSections(true));
    $('[data-act="expand"]').addEventListener("click", () => setAllSections(false));
    $('[data-act="pin"]').addEventListener("click", ev => {
        const rail = app.classList.toggle("is-rail");
        ev.currentTarget.setAttribute("aria-pressed", !rail);
        ev.currentTarget.title = rail ? "Expand the menu" : "Keep the menu open (pin)";
        setTimeout(drawCharts, 260);
    });

    $("#navFilter").addEventListener("input", e => {
        const q = e.target.value.trim().toLowerCase();
        $$(".dv-navitem").forEach(a => a.parentElement.hidden = !!q && !a.textContent.toLowerCase().includes(q));
        $$(".dv-navsec").forEach(s => {
            const any = $$("li", s).some(li => !li.hidden);
            s.hidden = !!q && !any;
            if (q)
                s.classList.remove("is-collapsed");
        });
    });

    document.addEventListener("keydown", e => {
        if (e.altKey && (e.key === "m" || e.key === "M")) {
            e.preventDefault();
            if (app.classList.contains("is-rail"))
                $('[data-act="pin"]').click();
            $("#navFilter").focus();
        }
        if (e.key === "Escape") {
            closeMenu();
            app.classList.remove("is-drawer");
        }
    });

    $("#menuBtn").addEventListener("click", () => {
        const open = app.classList.toggle("is-drawer");
        $("#menuBtn").setAttribute("aria-expanded", open);
    });
    $("#backdrop").addEventListener("click", () => app.classList.remove("is-drawer"));

    function show(view) {
        $$(".dv-view").forEach(v => v.classList.toggle("is-active", v.dataset.view === view));
        $$(".dv-navitem").forEach(a => {
            if (a.dataset.view === view)
                a.setAttribute("aria-current", "page");
            else
                a.removeAttribute("aria-current");
        });
        const c = CRUMBS[view] || [view];
        $("#crumbs").innerHTML = c.map((t, i) => i < c.length - 1 ? `<span>${t}</span>${ico("chev-r")}` : `<b>${t}</b>`).join("");
        document.title = c[c.length - 1] + " - SysWeaver Design v2";
        if (history.replaceState) {
            const p = new URLSearchParams(location.search);
            p.set("view", view);
            history.replaceState(null, "", "?" + p.toString());
        }
        $("#main").scrollTop = 0;
        requestAnimationFrame(drawCharts);
    }

    // ---------------------------------------------------------------- home tiles

    $("#tiles").innerHTML = [
        ["server", "Server", "Live resource use and charts", "server"],
        ["grid", "Managed services", "Start, stop and update services"],
        ["cpu", "Process information", "Running processes", "table"],
        ["drive", "Drive information", "Space and health of the drives"],
        ["key", "Key files", "Keys in the key file folder"],
        ["braces", "Api's", "Call any API of this server", "editor"],
    ].map(([i, t, d, v]) => `<a class="dv-tile" href="#" data-go="${v || ""}"><span class="dv-tile-ico">${ico(i)}</span><div><b>${t}</b><span>${d}</span></div></a>`).join("");
    $$("[data-go]").forEach(a => a.addEventListener("click", ev => {
        ev.preventDefault();
        if (a.dataset.go)
            show(a.dataset.go);
        else
            Toast("Not part of the prototype.", "info");
    }));

    // ---------------------------------------------------------------- charts

    function series(n, base, amp, seed) {
        let s = seed, v = base;
        const out = [];
        for (let i = 0; i < n; ++i) {
            s = (s * 9301 + 49297) % 233280;
            const r = s / 233280;
            v = Math.max(0.5, base + (v - base) * 0.6 + (r - 0.5) * amp + (r > 0.97 ? amp * 1.5 : 0));
            out.push(v);
        }
        return out;
    }

    const DATA = { cpu: series(90, 7, 8, 11), mem: series(90, 44, 1.2, 5).map((v, i) => v - 3 + i * 0.04) };

    function drawCharts() {
        $$(".dv-chart").forEach(el => {
            if (!el.offsetParent)
                return;
            const data = DATA[el.dataset.chart], max = +el.dataset.max, unit = el.dataset.unit;
            const w = el.clientWidth - 32, h = el.querySelector("svg") ? el.querySelector("svg").clientHeight : 208;
            const L = 36, B = 22, T = 8, R = 4, iw = w - L - R, ih = h - B - T;
            const x = i => L + i * iw / (data.length - 1), y = v => T + ih - v / max * ih;
            let g = "";
            for (let k = 0; k <= 3; ++k) {
                const v = max * k / 3, yy = Math.round(y(v)) + 0.5;
                g += `<line class="grid" x1="${L}" x2="${w - R}" y1="${yy}" y2="${yy}"/><text class="axis" x="${L - 8}" y="${yy + 4}" text-anchor="end">${v.toFixed(0)}${unit}</text>`;
            }
            const now = new Date(2026, 9, 19, 20, 10, 53);
            for (let k = 0; k <= 3; ++k) {
                const i = Math.round((data.length - 1) * k / 3);
                const t = new Date(now - (data.length - 1 - i) * 2000);
                g += `<text class="axis" x="${x(i)}" y="${h - 4}" text-anchor="${k === 0 ? "start" : k === 3 ? "end" : "middle"}">${t.toTimeString().slice(0, 5)}</text>`;
            }
            const pts = data.map((v, i) => `${x(i).toFixed(1)},${y(Math.min(v, max)).toFixed(1)}`);
            const area = `M${L},${y(0)} L${pts.join(" L")} L${x(data.length - 1)},${y(0)} Z`;
            el.innerHTML = `<svg viewBox="0 0 ${w} ${h}" role="img" aria-label="${el.dataset.chart === "cpu" ? "CPU" : "Memory"} use, last 3 minutes"><g>${g}</g><path class="area" d="${area}"/><polyline class="line" points="${pts.join(" ")}"/><line class="xhair" y1="${T}" y2="${T + ih}" x1="0" x2="0" visibility="hidden"/><circle class="pt" r="4" cx="0" cy="0" visibility="hidden"/></svg><div class="dv-tip" hidden></div>`;
            const svg = el.querySelector("svg"), tip = el.querySelector(".dv-tip"), xh = svg.querySelector(".xhair"), pt = svg.querySelector(".pt");
            svg.addEventListener("pointermove", ev => {
                const r = svg.getBoundingClientRect();
                const px = (ev.clientX - r.left) * w / r.width;
                const i = Math.max(0, Math.min(data.length - 1, Math.round((px - L) / iw * (data.length - 1))));
                xh.setAttribute("visibility", "visible");
                pt.setAttribute("visibility", "visible");
                xh.setAttribute("x1", x(i));
                xh.setAttribute("x2", x(i));
                pt.setAttribute("cx", x(i));
                pt.setAttribute("cy", y(Math.min(data[i], max)));
                const t = new Date(now - (data.length - 1 - i) * 2000);
                tip.hidden = false;
                tip.innerHTML = `${t.toTimeString().slice(0, 8)} &nbsp;<b>${data[i].toFixed(1)} ${unit}</b>`;
                tip.style.left = (16 + x(i) * r.width / w) + "px";
                tip.style.top = (8 + y(Math.min(data[i], max)) * r.height / h) + "px";
            });
            svg.addEventListener("pointerleave", () => {
                tip.hidden = true;
                xh.setAttribute("visibility", "hidden");
                pt.setAttribute("visibility", "hidden");
            });
        });
    }

    window.addEventListener("resize", () => requestAnimationFrame(drawCharts));

    // ---------------------------------------------------------------- table

    const NAMES = ["devenv", "claude", "DevHub", "explorer", "SwWebTestHost", "SwServerManager", "msedge", "UserManagerTest", "ExchangeRateService", "FakeEmailService", "ReverseProxyServer", "svchost", "dotnet", "Code", "OneDrive", "MsMpEng", "dwm", "sqlservr", "mysqld", "pwsh", "conhost", "RuntimeBroker", "SearchHost", "TranslationService"];
    const PRIO = ["Normal", "BelowNormal", "AboveNormal", "Idle", "High"];
    const ROWS = [];
    {
        let s = 7;
        const rnd = () => (s = (s * 16807) % 2147483647) / 2147483647;
        for (let i = 0; i < 671; ++i) {
            const mem = Math.round(Math.pow(rnd(), 3) * 4200 * 10) / 10 + 1.2;
            ROWS.push({
                Name: NAMES[Math.floor(rnd() * NAMES.length)],
                Pid: 1000 + Math.floor(rnd() * 70000),
                Start: new Date(2026, 9, 17 + Math.floor(rnd() * 3), Math.floor(rnd() * 24), Math.floor(rnd() * 60), Math.floor(rnd() * 60)),
                Cpu: Math.round(Math.pow(rnd(), 4) * 2500) / 100,
                Mem: mem,
                Peak: Math.round(mem * (1 + rnd() * 0.6) * 10) / 10,
                Handles: Math.floor(rnd() * 6000) + 40,
                Threads: Math.floor(rnd() * 120) + 1,
                Prio: PRIO[Math.floor(Math.pow(rnd(), 2) * PRIO.length)],
            });
        }
    }
    const fmtN = (v, d) => v.toLocaleString("en-US", { minimumFractionDigits: d || 0, maximumFractionDigits: d || 0 }).replace(/,/g, " ");
    const fmtMB = v => v >= 1024 ? fmtN(v / 1024, 2) + " GB" : fmtN(v, 1) + " MB";
    const fmtT = d => d.toISOString().slice(0, 16).replace("T", " ");
    const COLS = [
        { Key: "Name", Title: "Name", W: "12rem", Sticky: true },
        { Key: "Pid", Title: "PID", W: "6rem", Num: true, F: v => fmtN(v) },
        { Key: "Start", Title: "Started (UTC)", W: "9.5rem", F: fmtT },
        { Key: "Cpu", Title: "CPU", W: "8rem", Num: true, Html: v => `<span class="dv-inbar"><span>${v.toFixed(2)} %</span><i style="--v:${Math.min(100, v * 4)}%"></i></span>` },
        { Key: "Mem", Title: "Memory", W: "7rem", Num: true, F: fmtMB },
        { Key: "Peak", Title: "Peak memory", W: "7.5rem", Num: true, F: fmtMB },
        { Key: "Handles", Title: "Handles", W: "6rem", Num: true, F: v => fmtN(v) },
        { Key: "Threads", Title: "Threads", W: "5.5rem", Num: true, F: v => fmtN(v) },
        { Key: "Prio", Title: "Priority class", W: "9rem" },
    ];
    const T = { Page: 0, Size: 50, Sort: "Mem", Desc: true, Q: "", Filter: false, State: params.get("state") || "data" };

    function rows() {
        let r = ROWS;
        if (T.Q)
            r = r.filter(x => x.Name.toLowerCase().includes(T.Q));
        if (T.Filter)
            r = r.filter(x => x.Mem > 100);
        const k = T.Sort, d = T.Desc ? -1 : 1;
        return r.slice().sort((a, b) => (a[k] > b[k] ? 1 : a[k] < b[k] ? -1 : 0) * d);
    }

    function renderHead() {
        $("#tcols").innerHTML = `<col style="width:2.5rem">` + COLS.map(c => `<col style="width:${c.W}">`).join("") + `<col style="width:5.5rem">`;
        $("#thead").innerHTML = `<th class="is-check"><input type="checkbox" class="dv-cb" aria-label="Select all rows on this page"></th>` +
            COLS.map(c => `<th class="${c.Num ? "is-num" : ""}${c.Sticky ? " is-sticky-l" : ""}"${T.Sort === c.Key ? ` aria-sort="${T.Desc ? "descending" : "ascending"}"` : ""}><button class="dv-th" data-sort="${c.Key}">${c.Num ? ico("sort") + c.Title : c.Title + ico("sort")}</button></th>`).join("") +
            `<th class="is-sticky-r"><span class="dv-sr">Actions</span></th>`;
        $$("[aria-sort] .dv-ico").forEach(i => { if (!T.Desc) i.style.transform = "rotate(180deg)"; });
        $$(".dv-th").forEach(b => b.addEventListener("click", () => {
            const k = b.dataset.sort;
            if (T.Sort === k)
                T.Desc = !T.Desc;
            else {
                T.Sort = k;
                T.Desc = false;
            }
            T.Page = 0;
            renderHead();
            renderBody();
        }));
    }

    function renderBody() {
        const all = rows(), n = all.length, pages = Math.max(1, Math.ceil(n / T.Size));
        T.Page = Math.min(T.Page, pages - 1);
        const from = T.Page * T.Size, page = all.slice(from, from + T.Size);
        const st = $("#tstate");
        st.hidden = true;
        let html = "", cards = "";
        if (T.State === "loading") {
            for (let i = 0; i < 12; ++i)
                html += `<tr><td class="is-check"></td>${COLS.map(c => `<td${c.Sticky ? ' class="is-sticky-l"' : ""}><span class="dv-skel" style="width:${c.Num ? 50 : 70 + (i * 13 % 25)}%;${c.Num ? "margin-left:auto" : ""}"></span></td>`).join("")}<td class="is-sticky-r"></td></tr>`;
        }
        else if (T.State === "data" && n) {
            html = page.map((r, i) => `<tr aria-rowindex="${from + i + 2}">` +
                `<td class="is-check"><input type="checkbox" class="dv-cb" aria-label="Select ${r.Name} (${r.Pid})"></td>` +
                COLS.map(c => `<td class="${c.Num ? "is-num" : ""}${c.Sticky ? " is-sticky-l" : ""}${c.Key === "Cpu" && r.Cpu === 0 ? " is-muted" : ""}" title="${c.Html ? "" : (c.F ? c.F(r[c.Key]) : r[c.Key])}">${c.Html ? c.Html(r[c.Key]) : c.F ? c.F(r[c.Key]) : r[c.Key]}</td>`).join("") +
                `<td class="is-sticky-r"><span class="dv-rowacts"><button class="dv-btn is-ghost is-sm is-icon" title="Chart" aria-label="Chart of ${r.Name}">${ico("chart")}</button><button class="dv-btn is-ghost is-sm is-icon" data-rowmenu="${from + i}" aria-label="Actions for ${r.Name}">${ico("more")}</button></span></td></tr>`).join("");
            cards = page.map((r, i) => `<li class="dv-rcard"><div class="dv-rcard-head"><b>${r.Name}</b><span class="dv-badge">${fmtN(r.Pid)}</span><button class="dv-btn is-ghost is-icon" data-rowmenu="${from + i}" aria-label="Actions for ${r.Name}">${ico("more")}</button></div><dl><div><dt>CPU</dt><dd>${r.Cpu.toFixed(2)} %</dd></div><div><dt>Memory</dt><dd>${fmtMB(r.Mem)}</dd></div><div><dt>Started (UTC)</dt><dd>${fmtT(r.Start)}</dd></div><div><dt>Priority</dt><dd>${r.Prio}</dd></div></dl></li>`).join("");
        }
        else if (T.State === "error") {
            st.hidden = false;
            st.innerHTML = `<div class="dv-state-ico is-danger">${ico("alert")}</div><h3>The processes could not be loaded</h3><p>The server did not answer within 30 seconds (ServerManager/ProcessInfoTable). The table keeps its last data when this happens during a refresh.</p><div style="display:flex;gap:8px"><button class="dv-btn is-primary">${ico("refresh")}Try again</button><button class="dv-btn">Details</button></div>`;
        }
        else {
            st.hidden = false;
            const filtered = T.Q || T.Filter;
            st.innerHTML = `<div class="dv-state-ico">${ico(filtered ? "search" : "cpu")}</div><h3>${filtered ? "No processes match" : "No processes"}</h3><p>${filtered ? "Nothing matches the search and the filters. Change or clear them." : "The server reports no running processes."}</p>${filtered ? '<button class="dv-btn" id="clearAll">Clear search and filters</button>' : ""}`;
            const ca = $("#clearAll");
            if (ca)
                ca.addEventListener("click", () => {
                    T.Q = "";
                    $("#tSearch").value = "";
                    setFilter(false);
                });
        }
        $("#tbody").innerHTML = html;
        $("#tcards").innerHTML = cards;
        const shown = T.State === "data" ? n : 0;
        $("#trange").innerHTML = T.State === "loading" ? "Loading&hellip;" : shown ? `<b>${fmtN(from + 1)}&ndash;${fmtN(Math.min(n, from + T.Size))}</b> of ${fmtN(n)}` : "No rows";
        $("#pageNo").textContent = shown ? `Page ${T.Page + 1} of ${pages}` : "Page 1 of 1";
        $("#pFirst").disabled = $("#pPrev").disabled = !shown || T.Page === 0;
        $("#pNext").disabled = $("#pLast").disabled = !shown || T.Page >= pages - 1;
        $$("[data-rowmenu]").forEach(b => b.addEventListener("click", ev => {
            const r = all[+b.dataset.rowmenu];
            Menu(ev.currentTarget, [
                { Text: "Show chart", Icon: "chart" },
                { Text: "Copy the row", Icon: "copy", Kbd: "Ctrl+C", Do: () => Toast("Row copied to the clipboard.", "ok") },
                { Sep: true },
                { Text: "Kill process…", Icon: "kill", Danger: true, Do: () => Confirm(`Kill ${r.Name} (${fmtN(r.Pid)})?`, "The process is stopped at once. Unsaved work in it is lost.", "Kill process", () => Toast(`${r.Name} was stopped.`, "ok")) },
            ]);
        }));
    }

    function go(p) {
        T.Page = p;
        renderBody();
        $("#tscroll").scrollTop = 0;
    }

    $("#pFirst").addEventListener("click", () => go(0));
    $("#pPrev").addEventListener("click", () => go(T.Page - 1));
    $("#pNext").addEventListener("click", () => go(T.Page + 1));
    $("#pLast").addEventListener("click", () => go(1e9));
    $("#pageSize").addEventListener("change", e => {
        T.Size = +e.target.value;
        go(0);
    });
    $("#tSearch").addEventListener("input", e => {
        T.Q = e.target.value.trim().toLowerCase();
        go(0);
    });

    function setFilter(on) {
        T.Filter = on;
        $("#chips").hidden = !on;
        $("#filterCount").hidden = !on;
        $("#filterBtn").setAttribute("aria-pressed", on);
        go(0);
    }

    $("#filterBtn").addEventListener("click", () => setFilter(!T.Filter));
    $("#chips .dv-chip button").addEventListener("click", () => setFilter(false));
    $("#chips .dv-linkbtn").addEventListener("click", () => setFilter(false));
    $("#refreshBtn").addEventListener("click", () => {
        const b = $("#refreshBtn .dv-ico");
        b.animate([{ transform: "rotate(0)" }, { transform: "rotate(360deg)" }], { duration: 500 });
        Toast("Refreshed: 671 processes.", "ok");
    });
    $$("#stateSeg button").forEach(b => b.addEventListener("click", () => setState(b.dataset.state)));

    function setState(s) {
        T.State = s;
        $$("#stateSeg button").forEach(x => x.setAttribute("aria-pressed", x.dataset.state === s));
        renderBody();
    }

    // ---------------------------------------------------------------- editor

    const LANGS = [
        { Iso: "aa", Name: "Afar", LocalName: "Afar", Rtl: false },
        { Iso: "ar", Name: "Arabic", LocalName: "العربية", Rtl: true },
        { Iso: "sv", Name: "Swedish", LocalName: "Svenska", Rtl: false },
    ];
    let openItem = 0, dragFrom = -1;

    function renderColl() {
        $("#collCount").textContent = LANGS.length;
        $("#collItems").innerHTML = LANGS.map((l, i) => `<div class="dv-citem${openItem === i ? " is-open" : ""}" data-i="${i}">` +
            `<button class="dv-grip" draggable="true" aria-label="Move ${l.Name} (drag, or Alt+Up / Alt+Down)" title="Drag to move">${ico("grip")}</button>` +
            `<button class="dv-btn is-ghost is-sm is-icon" data-toggle="${i}" aria-expanded="${openItem === i}" aria-label="Edit ${l.Name}">${ico(openItem === i ? "chev-d" : "chev-r")}</button>` +
            `<span class="dv-idx">[${i}]</span><span class="dv-summary">${l.Name} <span>&middot; ${l.Iso} &middot; ${l.LocalName}</span></span>` +
            `<button class="dv-btn is-ghost is-sm is-icon" data-itemmenu="${i}" aria-label="Actions for ${l.Name}">${ico("more")}</button></div>` +
            (openItem === i ? `<div class="dv-cbody"><div class="dv-pgrow"><label class="dv-pglabel" for="c-iso">Iso</label><div class="dv-pgval"><input class="dv-input" id="c-iso" value="${l.Iso}" style="max-width:8rem"></div></div><div class="dv-pgrow"><label class="dv-pglabel" for="c-name">Name</label><div class="dv-pgval"><input class="dv-input" id="c-name" value="${l.Name}"></div></div><div class="dv-pgrow"><label class="dv-pglabel" for="c-local">Local name</label><div class="dv-pgval"><input class="dv-input" id="c-local" value="${l.LocalName}"></div></div><div class="dv-pgrow"><span class="dv-pglabel" id="c-rtl">Right to left</span><div class="dv-pgval"><button class="dv-switch" role="switch" aria-checked="${l.Rtl}" aria-labelledby="c-rtl"></button></div></div></div>` : "")).join("");
        $$("[data-toggle]").forEach(b => b.addEventListener("click", () => {
            openItem = openItem === +b.dataset.toggle ? -1 : +b.dataset.toggle;
            renderColl();
        }));
        $$("[data-itemmenu]").forEach(b => b.addEventListener("click", ev => {
            const i = +b.dataset.itemmenu;
            Menu(ev.currentTarget, [
                { Text: "Move up", Icon: "chev-l", Kbd: "Alt+Up", Do: () => move(i, i - 1) },
                { Text: "Move down", Icon: "chev-r", Kbd: "Alt+Down", Do: () => move(i, i + 1) },
                { Text: "Duplicate", Icon: "copy", Do: () => { LANGS.splice(i + 1, 0, Object.assign({}, LANGS[i])); renderColl(); } },
                { Text: "Copy", Icon: "copy", Kbd: "Ctrl+C" },
                { Sep: true },
                { Text: "Remove", Icon: "trash", Danger: true, Do: () => { LANGS.splice(i, 1); openItem = -1; renderColl(); Toast("Item removed.", "ok", "Undo"); } },
            ]);
        }));
        $$(".dv-grip").forEach(g => {
            const item = g.parentElement, i = +item.dataset.i;
            g.addEventListener("dragstart", ev => {
                dragFrom = i;
                ev.dataTransfer.effectAllowed = "move";
                ev.dataTransfer.setData("text/plain", JSON.stringify(LANGS[i]));
                item.classList.add("is-drag");
            });
            g.addEventListener("dragend", () => item.classList.remove("is-drag"));
            g.addEventListener("keydown", ev => {
                if (ev.altKey && (ev.key === "ArrowUp" || ev.key === "ArrowDown")) {
                    ev.preventDefault();
                    move(i, i + (ev.key === "ArrowUp" ? -1 : 1), true);
                }
            });
        });
        $$(".dv-citem").forEach(item => {
            item.addEventListener("dragover", ev => {
                ev.preventDefault();
                item.classList.add("is-drop");
            });
            item.addEventListener("dragleave", () => item.classList.remove("is-drop"));
            item.addEventListener("drop", ev => {
                ev.preventDefault();
                move(dragFrom, +item.dataset.i);
            });
        });
        $$(".dv-switch").forEach(s => s.onclick = () => s.setAttribute("aria-checked", s.getAttribute("aria-checked") !== "true"));
    }

    function move(from, to, focus) {
        if (from < 0 || to < 0 || to >= LANGS.length || from === to)
            return renderColl();
        const [x] = LANGS.splice(from, 1);
        LANGS.splice(to, 0, x);
        openItem = openItem === from ? to : openItem;
        renderColl();
        if (focus)
            $(`.dv-citem[data-i="${to}"] .dv-grip`).focus();
    }

    function addItem() {
        LANGS.push({ Iso: "", Name: "", LocalName: "", Rtl: false });
        openItem = LANGS.length - 1;
        renderColl();
        $("#c-iso").focus();
    }

    $("#collAdd").addEventListener("click", addItem);
    $("#collAdd2").addEventListener("click", addItem);

    function inputObject() {
        return {
            Name: $("#f-name").value, RootUrl: $("#f-root").value || null, MaxItems: +$("#f-max").value, Enabled: true,
            Fallback: $("#f-mode").value.replace(/ /g, ""), Highlight: $("#f-col").value, Description: null, Languages: LANGS,
        };
    }

    $$("[data-itab]").forEach(t => t.addEventListener("click", () => {
        const json = t.dataset.itab === "json";
        if (json)
            $("#ijsonText").value = JSON.stringify(inputObject(), null, 2);
        else {
            try {
                const o = JSON.parse($("#ijsonText").value || "{}");
                if (Array.isArray(o.Languages)) {
                    LANGS.length = 0;
                    LANGS.push(...o.Languages);
                }
                if (typeof o.Name === "string")
                    $("#f-name").value = o.Name;
                renderColl();
            }
            catch (e) {
                Toast("The JSON is not valid: " + e.message, "danger");
                return;
            }
        }
        $$("[data-itab]").forEach(x => x.setAttribute("aria-selected", x === t));
        $("#iform").hidden = json;
        $("#ijson").hidden = !json;
    }));
    $("#ijsonText").addEventListener("input", e => {
        let ok = true, msg = "Valid JSON. Switching back to Input applies it.";
        try {
            JSON.parse(e.target.value);
        }
        catch (x) {
            ok = false;
            msg = x.message;
        }
        $("#ijsonMsg").innerHTML = `<svg class="dv-ico" style="width:14px;height:14px;color:var(--sw-${ok ? "ok" : "danger"})"><use href="#i-${ok ? "check" : "alert"}"/></svg>${msg}`;
    });

    const RESULT = { RootUrl: "http://localhost:8208/", Items: null, Count: 3, Languages: ["aa", "ar", "sv"] };
    let resultMode = params.get("result") || "null";

    function renderResult() {
        $$("[data-result]").forEach(b => b.setAttribute("aria-pressed", b.dataset.result === resultMode));
        const r = resultMode === "null" ? null : RESULT;
        $("#rjsonText").value = JSON.stringify(r, null, 2);
        $("#rresult").innerHTML = r === null
            ? `<div class="dv-empty"><div class="dv-state-ico">${ico("info")}</div><h3><span class="dv-null" style="font-size:var(--sw-text-md)">null</span></h3><p>The call succeeded and returned no value.</p></div>`
            : `<div class="dv-pg"><div class="dv-pgrow"><span class="dv-pglabel">Root url</span><div class="dv-pgval"><span class="dv-ro"><a href="#">${r.RootUrl}</a></span></div></div><div class="dv-pgrow"><span class="dv-pglabel">Items<small>MenuItem[]</small></span><div class="dv-pgval"><span class="dv-null">null</span></div></div><div class="dv-pgrow"><span class="dv-pglabel">Count</span><div class="dv-pgval"><span class="dv-ro">3</span></div></div><div class="dv-pgrow"><span class="dv-pglabel">Languages<small>String[] &middot; 3 items</small></span><div class="dv-pgval"><span class="dv-ro">aa, ar, sv</span></div></div></div>`;
    }

    $$("[data-result]").forEach(b => b.addEventListener("click", () => {
        resultMode = b.dataset.result;
        renderResult();
    }));
    $$("[data-rtab]").forEach(t => t.addEventListener("click", () => {
        $$("[data-rtab]").forEach(x => x.setAttribute("aria-selected", x === t));
        $("#rresult").hidden = t.dataset.rtab !== "result";
        $("#rjson").hidden = t.dataset.rtab === "result";
    }));
    $("#invokeBtn").addEventListener("click", () => Toast("Invoked SetLanguages: 200 OK in 14 ms.", "ok"));

    // ---------------------------------------------------------------- overlays

    let menuEl = null;

    function closeMenu() {
        if (menuEl) {
            menuEl.remove();
            menuEl = null;
        }
    }

    function Menu(anchor, items) {
        closeMenu();
        const m = document.createElement("div");
        m.className = "dv-menu";
        m.setAttribute("role", "menu");
        m.innerHTML = items.map((it, i) => it.Sep ? "<hr>" : `<button role="menuitem" data-i="${i}" class="${it.Danger ? "is-danger" : ""}">${ico(it.Icon || "check")}${it.Text}${it.Kbd ? `<kbd>${it.Kbd}</kbd>` : ""}</button>`).join("");
        document.body.append(m);
        const r = anchor.getBoundingClientRect(), mr = m.getBoundingClientRect();
        m.style.left = Math.max(8, Math.min(innerWidth - mr.width - 8, r.right - mr.width)) + "px";
        m.style.top = (r.bottom + mr.height + 8 > innerHeight ? r.top - mr.height - 4 : r.bottom + 4) + "px";
        $$("button", m).forEach(b => b.addEventListener("click", () => {
            const it = items[+b.dataset.i];
            closeMenu();
            if (it.Do)
                it.Do();
        }));
        menuEl = m;
        m.querySelector("button").focus();
        setTimeout(() => document.addEventListener("pointerdown", function h(ev) {
            if (!m.contains(ev.target)) {
                closeMenu();
                document.removeEventListener("pointerdown", h);
            }
        }), 0);
    }

    function Confirm(title, text, ok, onOk) {
        const s = document.createElement("div");
        s.className = "dv-scrim";
        s.innerHTML = `<div class="dv-dialog" role="alertdialog" aria-modal="true" aria-labelledby="dlgT"><header><h2 id="dlgT">${title}</h2><button class="dv-btn is-ghost is-icon" aria-label="Close">${ico("x")}</button></header><div class="dv-dbody">${text}</div><footer><button class="dv-btn" data-c>Cancel</button><button class="dv-btn is-danger is-solid" data-ok>${ok}</button></footer></div>`;
        document.body.append(s);
        const close = () => s.remove();
        s.addEventListener("click", ev => { if (ev.target === s) close(); });
        $("header .dv-btn", s).addEventListener("click", close);
        $("[data-c]", s).addEventListener("click", close);
        $("[data-ok]", s).addEventListener("click", () => { close(); onOk(); });
        s.addEventListener("keydown", ev => { if (ev.key === "Escape") close(); });
        $("[data-c]", s).focus();
    }

    $$('[data-dialog="reboot"]').forEach(b => b.addEventListener("click", () => Confirm("Reboot HOME-SERVER?", "All 5 managed services and every user session stop. The server is back in about 2 minutes.", "Reboot", () => Toast("Reboot requested.", "ok"))));

    function Toast(text, kind, action) {
        const t = document.createElement("div");
        t.className = "dv-toast";
        t.setAttribute("role", kind === "danger" ? "alert" : "status");
        t.innerHTML = `${ico(kind === "ok" ? "check" : kind === "danger" ? "alert" : "info", kind === "ok" ? "is-ok" : "")}<span>${text}</span>${action ? `<button class="dv-btn is-ghost is-sm">${action}</button>` : ""}<button class="dv-btn is-ghost is-sm is-icon" aria-label="Close">${ico("x")}</button>`;
        $("#toasts").append(t);
        while ($("#toasts").children.length > 3)
            $("#toasts").firstChild.remove();
        $$("button", t).forEach(b => b.addEventListener("click", () => t.remove()));
        setTimeout(() => t.remove(), kind === "danger" ? 10000 : 4000);
    }

    // ---------------------------------------------------------------- start

    buildNav();
    renderHead();
    renderBody();
    renderColl();
    renderResult();
    setTheme(params.get("theme") || "light");
    setDensity(params.get("density") || "");
    if (params.get("rail") === "1")
        $('[data-act="pin"]').click();
    show(params.get("view") || "home");
    if (params.get("drawer") === "1")
        app.classList.add("is-drawer");
    if (params.get("filter") === "1")
        setFilter(true);
    setState(T.State);
})();
