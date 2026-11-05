/**
 * @file mapDemo.js - page logic of MapDemo.html (developer page of the Debug/Media menu): a gallery of MapView.html
 * embeds (map types, pin styles, pin sizes, zoom, DPI, links, languages, misc).
 *
 * Url parameters (case sensitive): `key` (Google Maps key; default from `Api/GetGoogleMapsKey`, else none = OpenStreetMap),
 * `center` (default London).
 * Each card shows a MapView iframe (not focusable, the map itself is not interactive unless the demo says so), a button
 * that opens the map url in the same frame and a button that copies the url.
 *
 * Requires sw/core.js and sw/ui.js. Globals defined: none (`Sw.Media.MapDemo`).
 */
(function () {
    "use strict";

    const Sw = window.Sw;

    /**
     * Read the Google Maps key (Dev role api); empty when there is none.
     * @returns {Promise<string>} The key.
     */
    async function GetKey() {
        const k = new URL(window.location.href).searchParams.get("key");
        if (k)
            return k.trim();
        try {
            const r = await Sw.Net.Get("Api/GetGoogleMapsKey", { NoNavigate: true });
            return typeof r === "string" ? r.trim() : "";
        }
        catch {
            return "";
        }
    }
    /**
     * Live map limit: every map embed is a WebGL context and the browser drops the oldest ones above about 16 (the original
     * demo loaded all its maps at once, so some went blank). Maps load when they come near the view, all of them at once (as
     * the original did for the maps on screen); at most MaxLive are loaded. A map that left the view stays loaded until its
     * slot is needed (scrolling back and forth does not abort and reload its tiles). A map in view above the limit shows a
     * "Show this map" button instead (it unloads the map loaded first).
     */
    class LiveMaps {
        /** @type {number} Most maps loaded at once. */
        static MaxLive = 14;

        constructor() {
            /** @type {HTMLIFrameElement[]} Loaded maps, oldest first. */
            this.Live = [];
            /** @type {Set<HTMLIFrameElement>} Maps near the view. */
            this.Near = new Set();
            /** @type {IntersectionObserver} Watches the map boxes (box.SwFrame = its frame). */
            this.Observer = new IntersectionObserver(entries => {
                for (const e of entries) {
                    const f = e.target.SwFrame;
                    if (e.isIntersecting) {
                        this.Near.add(f);
                        this.Load(f, false);
                    }
                    else
                        this.Near.delete(f);
                }
            }, { rootMargin: "100px" });
        }

        /**
         * Watch a map frame (members: SwUrl = map url, SwBox = the box around the frame and SwPaused = the paused placeholder).
         * @param {HTMLIFrameElement} f The frame.
         */
        Add(f) {
            f.SwBox.SwFrame = f;
            this.Observer.observe(f.SwBox);
        }

        /**
         * Load a map at once unless the limit is reached (a loaded map that is not near the view is unloaded first).
         * @param {HTMLIFrameElement} f The frame.
         * @param {boolean} force Unload the oldest loaded map when every loaded map is near the view (the "Show this map" button).
         */
        Load(f, force) {
            if (f.SwLiveNow)
                return;
            if (this.Live.length >= LiveMaps.MaxLive) {
                const victim = this.Live.find(x => !this.Near.has(x)) || (force ? this.Live[0] : null);
                if (!victim) {
                    f.hidden = true;
                    f.SwPaused.hidden = false;
                    return;
                }
                this.Unload(victim);
            }
            f.SwPaused.hidden = true;
            f.hidden = false;
            f.SwLiveNow = true;
            this.Live.push(f);
            f.src = f.SwUrl;
        }

        /**
         * Unload a map (about:blank frees its WebGL context); a map still in view shows its paused placeholder.
         * @param {HTMLIFrameElement} f The frame.
         */
        Unload(f) {
            if (!f.SwLiveNow)
                return;
            const i = this.Live.indexOf(f);
            if (i >= 0)
                this.Live.splice(i, 1);
            f.SwLiveNow = false;
            f.src = "about:blank";
            if (this.Near.has(f)) {
                f.hidden = true;
                f.SwPaused.hidden = false;
            }
        }
    }

    /**
     * Page main.
     * @returns {Promise<void>}
     */
    async function Main() {
        const page = Sw.el("div.md-list");
        //  Design v2 page: a left aligned page header above the sections
        document.body.appendChild(Sw.el("main.sw-page-dash", null, Sw.PageHeader({
            Title: _TF("Map Demo", "Title of the map demo page (examples of the map view; the original page title)"),
            Description: _TF("Examples of map types, pins, zoom levels and links. Open a map to see it full size, or copy its url.", "One line description below the title of the map demo page"),
        }).E, page));
        const key = await GetKey();
        const maps = new LiveMaps();
        if (!key)
            page.appendChild(Sw.el("p.md-note", { text: _TF("There is no Google Maps key, so the Google map types are shown with OpenStreetMap.", "Note at the top of the map demo when no Google Maps api key is configured") }));
        const center = new URL(window.location.href).searchParams.get("center") ?? "51.507318549291746, -0.1276379124532523";
        let grid = null;

        /** @param {string} title Section title. */
        const Section = title => {
            page.appendChild(Sw.el("h2.md-section", { text: title }));
            grid = Sw.el("div.md-grid");
            page.appendChild(grid);
        };

        /**
         * Add a map card.
         * @param {string} title Card title.
         * @param {string} [type] maptype.
         * @param {string} [pin] pin.
         * @param {string} [link] link.
         * @param {string} [extra] Extra url parameters ("&a=b").
         */
        const Add = (title, type, pin, link, extra) => {
            let url = "MapView.html?key=" + encodeURIComponent(key) + "&center=" + encodeURIComponent(center);
            if (type)
                url += "&maptype=" + type;
            if (pin)
                url += "&pin=" + encodeURIComponent(pin);
            if (link)
                url += "&link=" + encodeURIComponent(link);
            if (extra)
                url += extra;
            //  The frame is loaded / unloaded by LiveMaps (no src until it comes near the view)
            const f = Sw.el("iframe.md-map", { attrs: { tabindex: "-1", title: title } });
            f.SwUrl = url;
            const show = new Sw.Button({
                Icon: "play", Text: _TF("Show this map", "Button on a paused map of the map demo (too many maps on the screen at once)"),
                Title: _TF("Too many maps are shown at once, so this one is paused. Select it to show it (another map is paused instead).", "Tool tip of the show button on a paused map of the map demo"),
                //  The accessible names say which card (the same buttons repeat on every card)
                Label: _T("Show the {0} map", title, "Accessible name of the show button on a paused map of the map demo, {0} is the card title"),
                OnClick: () => maps.Load(f, true),
            });
            f.SwPaused = Sw.el("div.md-paused", { hidden: true }, show.E);
            f.SwBox = Sw.el("div.md-mapbox", null, f, f.SwPaused);
            const open = new Sw.Button({
                Icon: "IconFullScreenEnter", Kind: "ghost", Small: true,
                Title: _TF("Show full screen", "Tool tip text on an icon that when clicked will show the content in full screen"),
                Label: _T("Show the {0} map in full screen", title, "Accessible name of the full screen button on a map demo card, {0} is the card title"),
                OnClick: () => Sw.Page.Navigate(url, "_self"),
            });
            const copy = new Sw.Button({
                Icon: "copy", Kind: "ghost", Small: true,
                Title: _TF("Copy the url of this map", "Tool tip of a button that copies the url of a map view"),
                Label: _T("Copy the url of the {0} map", title, "Accessible name of the copy button on a map demo card, {0} is the card title"),
                OnClick: () => Sw.Clipboard.Copy(url),
            });
            const card = Sw.el("section.md-card", null,
                Sw.el("div.md-title", null, Sw.el("span.md-name", { text: title }), copy.E, open.E),
                f.SwBox);
            //  The card copies the map url like the original card (ValueFormat.copyOnClick, keyboard reachable), with the shared
            //  copy model: Ctrl+C on the focused card, its menu on a right click / long press
            Sw.Clipboard.CopyOnClick(card, url, { Keyboard: true, AddTitle: true });
            grid.appendChild(card);
            maps.Add(f);
        };

        Section(_TF("Map types", "Section title in the map demo"));
        Add(_TF("Satellite", "Map demo card title"), "satellite");
        Add(_TF("Roadmap", "Map demo card title"), "roadmap");
        Add(_TF("Dark (roadmap)", "Map demo card title"), "dark");
        Add(_TF("Open street map", "Map demo card title"), "open");
        Add(_TF("Open street map (dark)", "Map demo card title"), "opendark");

        Section(_TF("Pin styles", "Section title in the map demo"));
        Add(_TF("Red flag", "Map demo card title"), "dark", "red-flag");
        Add(_TF("Green flag", "Map demo card title"), "dark", "green-flag");
        Add(_TF("Blue flag", "Map demo card title"), "dark", "blue-flag");
        Add(_TF("Red marker", "Map demo card title"), "dark", "red-marker");
        Add(_TF("Green marker", "Map demo card title"), "dark", "green-marker");
        Add(_TF("Blue marker", "Map demo card title"), "dark", "blue-marker");
        Add(_TF("Red needle", "Map demo card title"), "dark", "red-needle");
        Add(_TF("Green needle", "Map demo card title"), "dark", "green-needle");
        Add(_TF("Blue needle", "Map demo card title"), "dark", "blue-needle");
        Add(_TF("Star marker", "Map demo card title"), "dark", "star");
        Add(_TF("Green pennant", "Map demo card title"), "dark", "green-pennant");
        Add(_TF("Blue medieval", "Map demo card title"), "dark", "blue-medieval");
        Add(_TF("Game flag", "Map demo card title"), "dark", "game-flag");
        //  A pin picture from an image url: a picture of this site (the app icon; works offline, and inside the app shell a
        //  picture of another site is not loaded, see mapView.js)
        Add(_TF("Custom", "Map demo card title (a pin from an image url; the original card name)"), "dark", new URL("../icon.svg", window.location.href).href, null, "&pinX=50%25&pinY=100%25");

        Section(_TF("Pin sizes", "Section title in the map demo"));
        for (const s of [16, 24, 32, 64, 96, 128])
            Add(_T("{0} pixels", s, "Map demo card title, {0} is a size in pixels"), null, "true", null, "&pinHeight=" + s);

        Section(_TF("Zoom", "Section title in the map demo"));
        Add("5", null, null, null, "&zoom=5");
        Add("10", null, null, null, "&zoom=10");
        Add("15", null, null, null, "&zoom=15");
        Add(_T("{0} satellite", 17, "Map demo card title, {0} is a zoom level"), "satellite", null, null, "&zoom=17");
        Add(_T("{0} satellite", 19, "Map demo card title, {0} is a zoom level"), "satellite", null, null, "&zoom=19");
        Add(_T("{0} satellite", 21, "Map demo card title, {0} is a zoom level"), "satellite", null, null, "&zoom=21");

        Section(_TF("DPI scale (tile edges may be visible)", "Section title in the map demo"));
        for (const d of [50, 100, 150, 200, 300, 400])
            Add(d + "%", "satellite", null, null, "&dpi=" + d);

        Section(_TF("Links", "Section title in the map demo"));
        Add(_TF("View", "Map demo card title"), "satellite", "true", "view");
        Add(_TF("Direction", "Map demo card title"), "satellite", "true", "dir");
        Add(_TF("Custom", "Map demo card title (a link to a web page; the original card name)"), "satellite", "true", "https://www.cnn.com/");

        Section(_TF("Languages", "Section title in the map demo"));
        Add(_TF("English", "Map demo card title"), null, null, null, "&lang=en&zoom=13");
        Add(_TF("Spanish", "Map demo card title"), null, null, null, "&lang=es&zoom=13");
        Add(_TF("Russian", "Map demo card title"), null, null, null, "&lang=ru&zoom=13");
        Add(_TF("Arabic", "Map demo card title"), null, null, null, "&lang=ar&zoom=13");
        Add(_TF("Japanese", "Map demo card title"), null, null, null, "&lang=ja&zoom=13");
        Add(_TF("Korean", "Map demo card title"), null, null, null, "&lang=ko&zoom=13");

        Section(_TF("Misc", "Section title in the map demo"));
        Add(_TF("Interactive", "Map demo card title"), "satellite", null, null, "&interactive=true");
        Add(_TF("Custom title", "Map demo card title"), "dark", null, null, "&title=" + encodeURIComponent("Hello world!"));
    }

    Sw.Media = Sw.Media || {};
    Sw.Media.MapDemo = { Main };
    Sw.Page.Main(Main, { Loading: true });
})();
