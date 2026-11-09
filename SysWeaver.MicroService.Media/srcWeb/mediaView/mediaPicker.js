/**
 * @file mediaView/mediaPicker.js - Sw.MediaPicker: a dialog to choose one media (a file of a media library, a new upload or a link),
 * with a live player (MediaView.html: the user can play, pause, seek and listen) at the media's own aspect ratio. The app supplies the
 * sources (its own apis), the upload and the link parsing, so any app with media (QuizzWeaver questions and themes, a news editor ...) gets
 * the same picker.
 *
 * Sw.MediaPicker.Pick(o) -> Promise<item | null>, options:
 * - Title: the dialog title (default "Choose media").
 * - Sources: [{ Id, Text, Query: async ({ Text, Row, Count, Kind, Sort }) => ({ Items: item[], More: bool }), Kinds, Sorts }]: browsable
 *   libraries (ex: "My media", "Stock"); Text is the search text, Row the first row, Count the page size. Optional Kinds: [{ Value, Text }]
 *   (a "what" filter shown as a segmented row, the first one is the default, ex: All / Pictures / Videos) and Sorts: [{ Value, Text }]
 *   (a sort select, the first one is the default, ex: Newest / Most used): the chosen values are passed to Query as Kind and Sort and
 *   remembered per source (localStorage "SysWeaver.MediaPicker.View.<Id>").
 * - Upload: { Accept (input accept), Upload: async (files: File[]) => item | null } (optional): an "Upload" tab (pick or drop files); the
 *   app uploads and returns the new item.
 * - Link: { Parse: text => item | null, Placeholder } (optional): a "Link" tab (paste an address, a YouTube link ...).
 * - Start: the Id of the first tab ("upload" / "link" / a source Id), else the last used (localStorage "SysWeaver.MediaPicker.Tab").
 * - Files: files to upload at once (ex: files dropped on the host's media field).
 * - Count: page size (default 48). More files load when the list is scrolled to its end (and with "Show more").
 * An item: { Name, Thumb (image url), Type (MediaTypes: Image 0, Video 1, Audio 2, YouTube 3, Effect 4), Url (for the player, relative to
 * the page), Title (tool tip), Info (one line under the player), Badge (optional short text on the tile, ex: "Video 0:32"), Width, Height
 * (optional: the player keeps this aspect ratio, else 16:9; audio gets a short player), Value (anything the app needs back) }. The promise
 * resolves with the chosen item (Value included) or null when cancelled. "Open in a new tab" shows the selected media in MediaView.html.
 */
(function () {
    "use strict";

    const scriptUrl = document.currentScript ? document.currentScript.src : document.baseURI;
    const StoreKey = "SysWeaver.MediaPicker.Tab";
    const ViewKey = "SysWeaver.MediaPicker.View.";

    function el(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls)
            e.className = cls;
        if (text)
            e.textContent = text;
        return e;
    }

    /**
     * The url of the media viewer (MediaView.html of this folder) for an item (the item url made absolute).
     * @param {object} item The item.
     * @param {boolean} small True for the player inside the picker (no statistics).
     * @returns {string} The url.
     */
    function viewUrl(item, small) {
        const u = new URL("MediaView.html", scriptUrl);
        u.searchParams.set("type", String(item.Type));
        u.searchParams.set("link", item.Type === 3 ? item.Url : new URL(item.Url, document.baseURI).href);
        u.searchParams.set("edit", "false");
        if (small)
            u.searchParams.set("no_stats", "");
        return u.href;
    }

    /** The aspect ratio (width / height) of the player of an item. */
    function aspectOf(item) {
        if (item.Type === 2)
            return 3;
        const w = +item.Width, h = +item.Height;
        if (w > 0 && h > 0)
            return Math.min(4, Math.max(0.4, w / h));
        return 16 / 9;
    }

    async function Pick(o) {
        o = o || {};
        await Sw.Net.IncludeCss(scriptUrl, "mediaPicker.css");
        const count = o.Count || 48;
        const sources = o.Sources || [];
        const tabs = sources.map(s => ({ Value: s.Id, Text: s.Text }));
        if (o.Upload)
            tabs.push({ Value: "upload", Text: _T("Upload", "Tab of the media picker that uploads new files") });
        if (o.Link)
            tabs.push({ Value: "link", Text: _T("Link", "Tab of the media picker that takes a media address") });
        let tab = o.Files && o.Upload ? "upload" : (o.Start || Sw.Storage.Local.Get(StoreKey));
        if (!tabs.some(t => t.Value === tab))
            tab = tabs.length ? tabs[0].Value : null;

        let selected = null;
        let dlg = null;
        let useButton = null;

        const body = el("div", "sw-mpick");
        const tabBar = el("div", "sw-mpick-tabs");
        const left = el("div", "sw-mpick-left");
        const right = el("div", "sw-mpick-right");
        const stage = el("div", "sw-mpick-stage");
        const frame = el("iframe", "sw-mpick-frame");
        frame.title = _T("Player of the selected media", "Accessible name of the player in the media picker");
        frame.allow = "autoplay; fullscreen";
        const empty = el("div", "sw-mpick-empty", _T("Select a media to see it here.", "Text of the empty preview of the media picker"));
        const name = el("div", "sw-mpick-name");
        const info = el("div", "sw-mpick-info");
        const openTab = new Sw.Button({
            Text: _T("Open in a new tab", "Button of the media picker that shows the selected media in a tab of its own"),
            Icon: "external", Kind: "ghost", Small: true,
            Title: _T("Show the media full size in a new tab", "Tool tip of the media picker button that opens the media in a new tab"),
            OnClick: () => {
                if (selected)
                    window.open(viewUrl(selected, false), "_blank", "noopener");
            },
        });
        openTab.E.hidden = true;
        stage.append(empty, frame);
        right.append(stage, name, info, openTab.E);
        frame.hidden = true;
        body.append(tabBar, el("div", "sw-mpick-main"));
        body.lastChild.append(left, right);

        //  The player keeps the aspect ratio of the media: as wide as the side allows, not higher than the room left
        const narrow = window.matchMedia("(max-width: 40rem)");
        function fit() {
            const ar = selected ? aspectOf(selected) : 16 / 9;
            const w = right.clientWidth;
            if (w <= 0)
                return;
            const other = name.offsetHeight + info.offsetHeight + openTab.E.offsetHeight + 40;
            const maxH = Math.max(140, narrow.matches ? window.innerHeight * 0.35 : (right.clientHeight - other));
            const fw = Math.max(80, Math.min(w, maxH * ar));
            stage.style.width = Math.floor(fw) + "px";
            stage.style.height = Math.floor(fw / ar) + "px";
        }
        const ro = typeof ResizeObserver === "function" ? new ResizeObserver(() => fit()) : null;
        if (ro)
            ro.observe(right);

        function select(item, tile) {
            selected = item;
            for (const t of left.querySelectorAll(".sw-mpick-tile[aria-pressed=true]"))
                t.setAttribute("aria-pressed", "false");
            if (tile)
                tile.setAttribute("aria-pressed", "true");
            empty.hidden = !!item;
            frame.hidden = !item;
            openTab.E.hidden = !item;
            name.textContent = item ? item.Name : "";
            info.textContent = item ? (item.Info || "") : "";
            if (item)
                frame.src = viewUrl(item, true);
            else
                frame.removeAttribute("src");
            fit();
            if (useButton)
                useButton.SetDisabled(!item, _T("Select a media first", "Reason why the Use button of the media picker is disabled"));
        }

        function tileOf(item) {
            const t = el("button", "sw-mpick-tile");
            t.type = "button";
            t.setAttribute("aria-pressed", "false");
            t.title = item.Title || item.Name;
            const pic = el("span", "sw-mpick-pic");
            const img = el("img");
            img.alt = "";
            img.loading = "lazy";
            img.draggable = false;
            img.src = item.Thumb;
            img.addEventListener("error", () => t.classList.add("sw-mpick-nothumb"), { once: true });
            pic.append(img);
            if (item.Badge)
                pic.append(el("span", "sw-mpick-badge", item.Badge));
            t.append(pic, el("span", "sw-mpick-tname", item.Name));
            t.addEventListener("click", () => select(item, t));
            t.addEventListener("dblclick", () => {
                select(item, t);
                if (useButton)
                    useButton.Click();
            });
            t.addEventListener("keydown", ev => {
                if (ev.key === "Enter" && selected === item && useButton) {
                    ev.preventDefault();
                    useButton.Click();
                }
            });
            return t;
        }

        /** A browsable source: search box, sort, kind filter, tiles, more (also when scrolled to the end). */
        function showSource(src) {
            left.replaceChildren();
            const kinds = src.Kinds && src.Kinds.length ? src.Kinds : null;
            const sorts = src.Sorts && src.Sorts.length ? src.Sorts : null;
            const saved = Sw.Storage.Local.GetJson(ViewKey + src.Id, null) || {};
            let kind = kinds ? (kinds.some(k => k.Value === saved.Kind) ? saved.Kind : kinds[0].Value) : undefined;
            let sort = sorts ? (sorts.some(k => k.Value === saved.Sort) ? saved.Sort : sorts[0].Value) : undefined;
            const remember = () => Sw.Storage.Local.SetJson(ViewKey + src.Id, { Kind: kind, Sort: sort });
            let text = "";
            let row = 0;
            let seq = 0;
            let more = false;
            let busy = false;
            const grid = el("div", "sw-mpick-grid");
            const moreBtn = new Sw.Button({ Text: _T("Show more", "Button of the media picker that loads more files"), Kind: "ghost", OnClick: () => load(false) });
            moreBtn.E.hidden = true;
            const state = el("div", "sw-mpick-state");
            const search = new Sw.TextBox({
                Type: "search",
                Icon: "IconSearch",
                Clear: true,
                AriaLabel: _T("Search", "Accessible name of the search box of the media picker"),
                Placeholder: _T("Search by name or description", "Placeholder of the search box of the media picker"),
                OnInput: v => {
                    text = v || "";
                    clearTimeout(search.Timer);
                    search.Timer = setTimeout(() => load(true), 300);
                },
            });
            const bar = el("div", "sw-mpick-bar");
            bar.append(search.E);
            if (sorts)
                bar.append(new Sw.Select({
                    Items: sorts, Value: sort, Small: true,
                    AriaLabel: _T("Sort by", "Accessible name of the sort select of the media picker"),
                    OnChange: v => { sort = v; remember(); load(true); },
                }).E);
            const head = el("div", "sw-mpick-head");
            head.append(bar);
            left.append(head);
            if (kinds)
                head.append(new Sw.Segmented({
                    Items: kinds, Value: kind, Small: true, Class: "sw-mpick-kinds",
                    Label: _T("Kind of media", "Accessible name of the kind filter of the media picker"),
                    OnChange: v => { kind = v; remember(); load(true); },
                }).E);
            left.append(grid, state, moreBtn.E);
            async function load(reset) {
                if (!reset && (busy || !more))
                    return;
                const my = ++seq;
                busy = true;
                if (reset) {
                    row = 0;
                    grid.replaceChildren();
                    select(null);
                    left.scrollTop = 0;
                }
                state.textContent = _T("Loading…", "Shown while the media picker loads files");
                try {
                    const r = await src.Query({ Text: text, Row: row, Count: count, Kind: kind, Sort: sort });
                    if (my !== seq)
                        return;
                    const items = (r && r.Items) || [];
                    for (const it of items)
                        grid.appendChild(tileOf(it));
                    row += items.length;
                    more = !!(r && r.More);
                    moreBtn.E.hidden = !more;
                    state.textContent = grid.childElementCount ? "" : (text ? _T("Nothing matches the search.", "Media picker: the search found nothing") : _T("There are no files here yet.", "Media picker: the library is empty"));
                }
                catch (e) {
                    if (my === seq)
                        state.textContent = _T("The files could not be loaded: {0}", e && e.message ? e.message : e, "Media picker: loading the files failed, {0} is the reason");
                }
                finally {
                    if (my === seq)
                        busy = false;
                }
            }
            //  Load the next page when the end of the list comes into view
            if (typeof IntersectionObserver === "function")
                new IntersectionObserver(en => {
                    if (en.some(x => x.isIntersecting))
                        load(false);
                }, { root: left, rootMargin: "400px" }).observe(moreBtn.E);
            load(true);
        }

        function showUpload(files) {
            left.replaceChildren();
            const drop = el("div", "sw-mpick-drop");
            drop.tabIndex = 0;
            drop.setAttribute("role", "button");
            drop.append(el("strong", null, _T("Drop files here", "Media picker upload area, first line")), el("span", null, _T("or click to choose a file on this device", "Media picker upload area, second line")));
            const state = el("div", "sw-mpick-state");
            left.append(drop, state);
            async function up(list) {
                if (!list || !list.length)
                    return;
                state.textContent = _T("Uploading…", "Shown while the media picker uploads a file");
                try {
                    const item = await o.Upload.Upload(Array.from(list));
                    state.textContent = item ? _T("Uploaded: {0}", item.Name, "Media picker: a file was uploaded, {0} is its name") : "";
                    if (item)
                        select(item, null);
                }
                catch (e) {
                    state.textContent = "";
                    Sw.Fail(e);
                }
            }
            drop.addEventListener("click", async () => {
                await Sw.Net.IncludeJs(scriptUrl, "../common/fileUploader.js");
                up(await Sw.Upload.Pick({ Accept: o.Upload.Accept }));
            });
            drop.addEventListener("keydown", ev => {
                if (ev.key === "Enter" || ev.key === " ") {
                    ev.preventDefault();
                    drop.click();
                }
            });
            drop.addEventListener("dragover", ev => { ev.preventDefault(); drop.classList.add("sw-mpick-over"); });
            drop.addEventListener("dragleave", () => drop.classList.remove("sw-mpick-over"));
            drop.addEventListener("drop", ev => {
                ev.preventDefault();
                drop.classList.remove("sw-mpick-over");
                up(ev.dataTransfer.files);
            });
            if (files)
                up(files);
        }

        function showLink() {
            left.replaceChildren();
            const state = el("div", "sw-mpick-state");
            const box = new Sw.TextBox({
                Type: "url",
                Clear: true,
                Label: _T("Media address", "Label of the address box of the media picker"),
                Placeholder: o.Link.Placeholder || _T("Paste a link to an image, a video, a sound or a YouTube clip", "Placeholder of the address box of the media picker"),
                OnInput: v => {
                    const item = v ? o.Link.Parse(v.trim()) : null;
                    state.textContent = v && !item ? _T("This is not a media address that can be used.", "Media picker: the pasted address is no usable media") : "";
                    select(item, null);
                },
            });
            left.append(box.E, state);
            setTimeout(() => box.Focus && box.Focus(), 50);
        }

        function show(id) {
            tab = id;
            Sw.Storage.Local.Set(StoreKey, id);
            select(null);
            if (id === "upload")
                showUpload(null);
            else if (id === "link")
                showLink();
            else
                showSource(sources.find(s => s.Id === id));
        }

        if (tabs.length > 1)
            tabBar.appendChild(new Sw.Segmented({ Items: tabs, Value: tab, Label: _T("Where the media comes from", "Accessible name of the tabs of the media picker"), OnChange: v => show(v) }).E);

        useButton = new Sw.Button({
            Text: _T("Use", "Button of the media picker that uses the selected media"),
            Kind: "primary",
            Disabled: _T("Select a media first", "Reason why the Use button of the media picker is disabled"),
            OnClick: () => dlg.Close(selected),
        });
        dlg = Sw.Dialog.Open({
            Title: o.Title || _T("Choose media", "Default title of the media picker"),
            Content: body,
            Id: "sw-mpick",
            Wide: true,
            Full: true,
            Buttons: [{ Text: _T("Cancel", "Button that closes the media picker without a choice"), Value: null }],
        });
        dlg.Box.querySelector(".sw-dialog-foot").append(useButton.E);
        if (o.Files && o.Upload)
            showUpload(o.Files);
        else if (tab)
            show(tab);
        const r = await dlg.Result;
        if (ro)
            ro.disconnect();
        frame.removeAttribute("src");
        return r && typeof r === "object" ? r : null;
    }

    Sw.MediaPicker = { Pick };
})();
