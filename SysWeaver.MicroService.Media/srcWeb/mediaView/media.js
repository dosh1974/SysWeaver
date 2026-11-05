/**
 * @file media.js - media viewer / preview / editor page logic.
 *
 * Pages:
 * - `MediaView.html` (new format page, boots `Sw.MediaView.ViewMain` through `Sw.Page.Main`): a media player with auto hiding
 *   controls on top of the media (CreateChrome: time line, transport, volume, stats panel, full screen), remote controllable by a
 *   linked editor (`ctrl` url parameter).
 * - `MediaPreview.html` (boots `Sw.MediaView.PreviewMain`): bare full-window player, also the screenshot page of the
 *   headless thumbnail services (`ScreenShotHost` host object contract: isSupported / setSize / doIt).
 * - `MediaEdit.html` (boots `Sw.MediaView.EditMain`): the media editor: player + the media settings in the object editor
 *   of the Edit service (loaded on demand from ../edit/Edit.js, types from its TypeService). Link mode (`link`, `type`,
 *   `props`, like MediaView.html; "Copy settings") or host data mode (`get` / `id` / `update` apis of the host app, with
 *   "Save changes"); see EditMain. Host pages that load media.js may call `Sw.MediaView.EditMain` themselves.
 *
 * Requires the SysWeaver core and ui (`sw/core.js` + `sw/ui.js`, or the legacy `common/common.js` which contains both).
 * Loads `../mediaPlayer/media_player.js` (relative to this script) at parse time, and `../edit/Edit.js` when an editor is shown.
 *
 * Globals defined: the new namespace `Sw.MediaView` and, as legacy aliases for host pages, the functions
 * `mediaCreatePlayer`, `mediaPlayWithPopup`, `mediaEditProps`, `mediaEditMain`, `mediaGetTypeFromUrl`, `mediaViewMain`,
 * `mediaPreviewMain` and the class `SsHack`.
 *
 * Conventions:
 * - User preferences are stored in localStorage keys `SysWeaver.Media.Loop` ("true"/"false"),
 *   `SysWeaver.Media.Volume` (float string, default 0.25) and `SysWeaver.Media.Mute` ("true"/"false"); a value saved
 *   under the old names (`QuizzWeaver.MediaLoop` / `MediaVolume` / `MediaMute`) is still read until a new one is saved.
 * - Editor <-> MediaView.html remote control uses InterOp messages `<prefix><Name>` with names
 *   GetProps, SetProps, Reload, SetVolume, UpdateSize, Crop, Compile.
 * - Styles: media.css (custom elements Media-Preview, Media-AspectContainer, Media-Overlay, Media-Stats, ...).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const scriptUrl = document.currentScript ? document.currentScript.src : document.baseURI;

    /** Loads the media player library (MediaPlayer, MediaTypes, MediaPlayerTools ...) relative to this script. */
    const playerReady = Sw.Net.IncludeJs(scriptUrl, "../mediaPlayer/media_player.js").then(() => MediaPlayer.Ready);

    // ------------------------------------------------------------------ preferences

    /**
     * The viewer playback preferences (localStorage; a value saved under the old QuizzWeaver.Media* key is read until a new one is saved).
     */
    const Prefs = {
        /** @type {string} Storage key: "true" = muted. */
        MuteKey: "SysWeaver.Media.Mute",
        /** @type {string} Storage key: "true" = loop, else play once. */
        LoopKey: "SysWeaver.Media.Loop",
        /** @type {string} Storage key: volume 0..1 as text. */
        VolumeKey: "SysWeaver.Media.Volume",
        /**
         * Read a preference, falling back to the value saved under the old name ("SysWeaver.Media.X" -> "QuizzWeaver.MediaX").
         * @param {string} key The storage key.
         * @returns {?string} The stored text or null.
         */
        Read: key => Sw.Storage.Get(key) ?? Sw.Storage.Get(key.replace("SysWeaver.Media.", "QuizzWeaver.Media")),
        /** @returns {boolean} True if the user chose mute. */
        Muted: () => Prefs.Read(Prefs.MuteKey) === "true",
        /** @returns {boolean} True if the user chose loop. */
        Loop: () => Prefs.Read(Prefs.LoopKey) === "true",
        /** @returns {number} The chosen volume (default 0.25). */
        Volume: () => {
            const v = parseFloat(Prefs.Read(Prefs.VolumeKey) ?? "0.25");
            return isNaN(v) ? 0.25 : v;
        },
        /** @param {boolean} v Mute. */
        SetMuted: v => Sw.Storage.Set(Prefs.MuteKey, v ? "true" : "false"),
        /** @param {boolean} v Loop. */
        SetLoop: v => Sw.Storage.Set(Prefs.LoopKey, v ? "true" : "false"),
        /** @param {number} v Volume. */
        SetVolume: v => Sw.Storage.Set(Prefs.VolumeKey, "" + v),
    };

    // ------------------------------------------------------------------ helpers

    /**
     * Guesses the media type from the file extension of a URL.
     * @param {string} url - Media URL.
     * @returns {number} `MediaTypes.Image`, `MediaTypes.Video`, `MediaTypes.Audio`, `MediaTypes.Effect` (".glsl"), or -1.
     */
    function GetTypeFromUrl(url) {
        if (ParseYouTube(url))
            return MediaTypes.YouTube;
        const F = Sw.Format;
        //  The extension of the path ("a/b.png?v=2"), else of the whole url like the original page (an api with the file name in
        //  the query: "Api/Read?x.png", quotes dropped)
        let ext = (F.FileExtension(url.split("?")[0].split("#")[0]) || "").toLowerCase();
        if (!(F.IsImageExt(ext) || F.IsVideoExt(ext) || F.IsAudioExt(ext) || ext === "glsl"))
            ext = (F.FileExtension(url.replace(/["']/g, "")) || "").toLowerCase();
        if (F.IsImageExt(ext))
            return MediaTypes.Image;
        if (F.IsVideoExt(ext))
            return MediaTypes.Video;
        if (F.IsAudioExt(ext))
            return MediaTypes.Audio;
        if (ext === "glsl")
            return MediaTypes.Effect;
        return -1;
    }

    /**
     * A time of a YouTube link ("t" / "start"): "90", "90s", "1m30s", "1h2m3s".
     * @param {?string} s The text.
     * @returns {number} Seconds (0 when missing or not valid).
     */
    function ParseYouTubeTime(s) {
        if (!s)
            return 0;
        s = String(s).trim().toLowerCase();
        if (/^\d+(\.\d+)?$/.test(s))
            return parseFloat(s);
        const m = s.match(/^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$/);
        return m ? (parseInt(m[1] || "0") * 3600) + (parseInt(m[2] || "0") * 60) + parseInt(m[3] || "0") : 0;
    }

    /**
     * The video code of a YouTube link: youtube.com/watch?v=, youtu.be/, youtube.com/embed/ | shorts/ | live/ | v/, m. / music. /
     * youtube-nocookie.com, with an optional start time ("t" or "start").
     * @param {string} url The link.
     * @returns {?{Id: string, Start: number}} The code and the start (seconds), null when it is not a YouTube video link.
     */
    function ParseYouTube(url) {
        if (typeof url !== "string" || !/^\s*(https?:)?\/\/|^\s*(www\.|m\.)?(youtube\.com|youtu\.be)\//i.test(url))
            return null;
        let u;
        try {
            u = new URL(/^\s*(https?:)?\/\//i.test(url) ? url.trim() : "https://" + url.trim(), window.location.href);
        }
        catch {
            return null;
        }
        const h = u.hostname.toLowerCase().replace(/^(www\.|m\.|music\.)/, "");
        let id = null;
        if (h === "youtu.be")
            id = u.pathname.split("/")[1];
        else if (h === "youtube.com" || h === "youtube-nocookie.com") {
            if (u.pathname === "/watch")
                id = u.searchParams.get("v");
            else {
                const m = u.pathname.match(/^\/(embed|shorts|live|v|e)\/([^/?#]+)/);
                if (m)
                    id = m[2];
            }
        }
        if (!id || !/^[A-Za-z0-9_-]{6,20}$/.test(id))
            return null;
        return { Id: id, Start: ParseYouTubeTime(u.searchParams.get("t") || u.searchParams.get("start")) };
    }

    /**
     * Read the common viewer / preview url parameters (keys are case insensitive).
     * `link` | `d` (required), `type` | `t` (else guessed from the extension, a YouTube link is a YouTube media), `props` (JSON).
     * A YouTube link (also with type 3) becomes its video code; its start time ("t=") is returned as Start.
     * @returns {{Data: string, Type: number, Props: object|null, Start: number}} The values (Start: seconds to start at, 0 = the start).
     * @throws {Error} A translated message when a parameter is missing or invalid.
     */
    function ReadMediaParams() {
        const U = Sw.Url;
        let data = U.Get("link") ?? U.Get("d");
        if (!data)
            throw new Error(_TF("No link parameter 'link' supplied!", "Error shown when the media viewer page is opened without the link url parameter"));
        let type = U.Get("type") ?? U.Get("t");
        if (!type) {
            type = GetTypeFromUrl(data);
            if (type < 0)
                throw new Error(_TF("No type parameter 'type' supplied!", "Error shown when the media viewer page can't find out the media type and no type url parameter was given"));
        } else
            type = parseInt(type);
        let start = 0;
        if (type === MediaTypes.YouTube) {
            const yt = ParseYouTube(data);
            if (yt) {
                data = yt.Id;
                start = yt.Start;
            }
        }
        const p = U.Get("props");
        //  A text media is the text itself: limited inside the app shell (TextFromUrl)
        const props = p ? JSON.parse(p) : null;
        //  A collage (CollageSeparator) holds several short texts: a larger limit
        return { Data: type === MediaTypes.Text ? TextFromUrl(data, props && props.CollageSeparator ? 200 : MaxAppText) : data, Type: type, Props: props, Start: start };
    }

    /**
     * Read a dropped or pasted text (or a typed one, "Show url") as a media link.
     * @param {string} text The text: a url (a YouTube link, a file of a known type), else nothing.
     * @returns {?{Link: string, Type: number}} The link and its type, null when it is not a media link.
     */
    function ParseMediaLink(text) {
        const s = String(text || "").trim().split(/\r?\n/).find(l => l && !l.startsWith("#"));
        if (!s || s.length > 4096)
            return null;
        if (ParseYouTube(s))
            return { Link: s, Type: MediaTypes.YouTube };
        if (!/^(https?:\/\/|\/|\.\.?\/)/i.test(s) && !/^[\w.-]+\/[^\s]*$/.test(s))
            return null;
        const type = GetTypeFromUrl(s);
        return type < 0 ? null : { Link: s, Type: type };
    }

    /**
     * Security check of the `link` parameter (it comes from the url, so a crafted same-origin deep link could show any
     * media under the application's header). For the url types (image, video, audio, effect):
     *  - a http(s) url of this site (Sw.Url.SafeSameOrigin) is always shown;
     *  - any other scheme ("javascript:", "data:", "blob:", ...) is refused;
     *  - a url of another site is refused inside the app shell (a same-origin top window with SysWeaverApp), where it would
     *    look like content of this site; it is shown in a window of its own (a new tab, the editor's "New window", the
     *    screenshot host) or when framed by another host.
     * YouTube (a video code) and text (the text itself) are not urls and are not checked.
     * The same rule applies to the url-like members of the `props` parameter: `Effect` (the effect of an image / text) and
     * `FontUrl` (the font of a text); for them "Open in a new tab" opens this page in a new tab.
     * @param {{Data: string, Type: number, Props: (object|null)}} mp The media parameters (ReadMediaParams).
     * @returns {{Text: string, Url: (string|null)}|null} null when allowed, else the reason and (for another site) the
     * absolute url that may be opened in a new tab.
     */
    function MediaLinkProblem(mp) {
        const t = mp.Type;
        if (t !== MediaTypes.YouTube && t !== MediaTypes.Text) {
            const k = UrlKind(mp.Data);
            if (k === "Scheme")
                return {
                    Text: _TF("This media cannot be shown: its address (the link parameter) must be a web address.", "Error on the media viewer page when the link url parameter is not a http(s) address (not allowed)"),
                    Url: null,
                };
            if (k) {
                const u = new URL(mp.Data, window.location.href);
                return {
                    Text: _T("This media is on another site ({0}), so it is not shown inside the application. Open it in a new tab to see it.", u.host,
                        "Message on the media viewer page inside the application when the media link points to another web site. {0} is the name of that site (host name)"),
                    Url: u.href,
                };
            }
        }
        const props = mp.Props;
        if (!props || typeof props !== "object")
            return null;
        for (const name of ["Effect", "FontUrl"]) {
            const v = props[name];
            if (v === null || typeof v === "undefined" || v === "")
                continue;
            const k = typeof v === "string" ? UrlKind(v) : "Scheme";
            if (k === "Scheme")
                return {
                    Text: _TF("This media cannot be shown: a file address in its settings (the props parameter) is not a web address.", "Error on the media viewer page when the effect or font address in the props url parameter is not a http(s) address (not allowed)"),
                    Url: null,
                };
            if (k)
                return {
                    Text: _T("This media uses a file from another site ({0}), so it is not shown inside the application. Open it in a new tab to see it.", new URL(v, window.location.href).host,
                        "Message on the media viewer page inside the application when the effect or font of the media is on another web site. {0} is the name of that site (host name)"),
                    Url: window.location.href,
                };
        }
        return null;
    }

    /**
     * True when this page is shown inside the app shell (a same-origin top window with SysWeaverApp), at any depth.
     * @returns {boolean}
     */
    function InApp() {
        try {
            return window.top !== window && !!window.top.SysWeaverApp;
        }
        catch {
            return false;
        }
    }

    /**
     * Classify a media url from the page url (the rule of MediaLinkProblem).
     * @param {string} url The url.
     * @returns {string|null} null when allowed, "Scheme" when it is not a http(s) url, "Site" for another site inside the
     * app shell.
     */
    function UrlKind(url) {
        const U = Sw.Url;
        if (U.SafeSameOrigin(url))
            return null;
        if (!U.IsHttp(url))
            return "Scheme";
        return InApp() ? "Site" : null;
    }

    /**
     * Maximum length of a text from the url (the `link` of a text media) rendered inside the app shell.
     * @type {number}
     */
    const MaxAppText = 80;

    /**
     * Milliseconds the viewer waits for a media to load (CreatePlayer quiet mode) before it shows its error state.
     * @type {number}
     */
    const LoadTimeoutMs = 30000;

    /**
     * The reason text of a load error: a specific text for a web font that failed (MediaPlayerTools.LoadFont, the error
     * has FontLoad; the browser only says "A network error occurred."), else the error message.
     * @param {*} e The error.
     * @returns {string} The reason (text only).
     */
    function FailReason(e) {
        if (e && e.FontLoad) {
            console.warn("Media: " + e.message);
            return _TF("The font could not be loaded.", "Reason on the media viewer error state when the web font of a text media (FontUrl) could not be loaded");
        }
        return e && e.message ? e.message : ("" + e);
    }

    /**
     * Show the viewer's error state: a short reason and a "Try again" button (reloads the page); the reason is also
     * announced to screen readers. Replaces the content of target.
     * @param {HTMLElement} target The element to show it in.
     * @param {string} reason The reason (text only).
     */
    function ShowLoadError(target, reason) {
        console.warn("Media: failed to show the media: " + reason);
        target.textContent = "";
        target.classList.remove("MediaWithCaption");
        const text = _TF("This media can't be shown.", "Title of the media viewer error state when the media failed to load or play");
        target.append(Sw.el("div.MediaRefused.MediaFailed", { attrs: { role: "alert" } },
            Sw.el("p.MediaRefusedText", null, Sw.el("strong", { text: text })),
            Sw.el("p.MediaRefusedText", { text: reason }),
            new Sw.Button({
                Text: _TF("Try again", "Button text on the media viewer error state, loads the media again"),
                Icon: "IconReload",
                OnClick: () => Sw.Page.Reload(),
            }).E));
        Sw.Page.Announce(text + " " + reason);
    }

    /**
     * Show a small persistent notice (`p.MediaFontNotice`, also announced to screen readers) when the web font of a text media
     * could not be loaded: the players then render with a default font (MediaPlayerTools.LoadFontOrFallback dispatches
     * "MediaFontFallback" on the window). Call before the player is created; at most one notice per page.
     * @param {HTMLElement} target The element to add the notice to.
     * @param {function(Error):void} [onFail] - Also called with the font error (PreviewMain: a real screenshot host still
     * reports the failure).
     */
    function FontNotice(target, onFail) {
        let shown = false;
        window.addEventListener("MediaFontFallback", ev => {
            if (onFail)
                onFail(ev.detail);
            if (shown)
                return;
            shown = true;
            const text = _TF("The font could not be loaded, a default font is used.", "Small notice on a media page when the web font of a text (FontUrl) could not be loaded and the text is shown with a default font");
            target.appendChild(Sw.el("p.MediaFontNotice", { text: text }));
            Sw.Page.Announce(text);
        });
    }

    /**
     * Limit a text from the url inside the app shell (MaxAppText characters, an ellipsis marks the cut), so a crafted
     * link can't paint a long message under the application header. Unchanged outside the app shell.
     * @param {string} text The text.
     * @param {number} max The maximum length.
     * @returns {string} The text to render.
     */
    function TextFromUrl(text, max) {
        const s = String(text ?? "");
        return s.length <= max || !InApp() ? s : s.substring(0, max - 1) + "…";
    }

    /**
     * True when this page is the content frame of the app shell (its parent is the same-origin top window with
     * SysWeaverApp).
     * @returns {boolean}
     */
    function IsShellContent() {
        try {
            return window.parent !== window && window.parent === window.top && !!window.top.SysWeaverApp;
        }
        catch {
            return false;
        }
    }

    /**
     * Add a caption "Text from the link" (text only) for a text media from the url, so the text can't pass as a message
     * of the application: when this page is the content frame of the app shell, or always with force (MediaPreview with
     * the `caption` url parameter: an effect page in the content frame renders through it). Place it with
     * PlaceTextCaption once the text is shown.
     * @param {HTMLElement} target The element to add the caption to (class MediaWithCaption is added).
     * @param {boolean} [force] - Add it even when this page is not the shell's content frame.
     * @returns {HTMLElement|null} The caption, null when none was added.
     */
    function AddTextCaption(target, force) {
        if (!force && !IsShellContent())
            return null;
        target.classList.add("MediaWithCaption");
        const c = Sw.el("p.MediaTextCaption", { text: _TF("Text from the link", "Caption on the media viewer inside the application when it shows a text media: the text comes from the page address (a link), not from the application") });
        target.appendChild(c);
        return c;
    }

    /**
     * Put a "Text from the link" caption directly above the rendered text (the visible box of the media element, after
     * its transform), centered on it and kept inside the window; below the text when there is no room above, and over
     * its top edge when there is neither. Hidden (no class Placed) while the media has no size.
     * @param {HTMLElement} caption The caption (AddTextCaption).
     * @param {HTMLElement} media The element that shows the text (the player element).
     */
    function PlaceTextCaption(caption, media) {
        const r = media.getBoundingClientRect();
        if (!(r.width > 0 && r.height > 0)) {
            caption.classList.remove("Placed");
            return;
        }
        const de = document.documentElement;
        const vw = de.clientWidth;
        const vh = de.clientHeight;
        const cw = caption.offsetWidth;
        const ch = caption.offsetHeight;
        const gap = Math.max(4, Math.round(ch * 0.25));
        const top = Math.max(r.top, 0);
        const bottom = Math.min(r.bottom, vh);
        let y = top - gap - ch;
        if (y < 4)
            y = bottom + gap;
        if (y + ch > vh - 4)
            y = Math.max(4, top + gap);
        const x = Math.min(Math.max(r.left + (r.width - cw) / 2, 8), Math.max(8, vw - cw - 8));
        caption.style.left = Math.round(x) + "px";
        caption.style.top = Math.round(y) + "px";
        caption.classList.add("Placed");
    }

    /**
     * Keep a caption placed at its text (PlaceTextCaption) on window resize / scroll and when the observed boxes change
     * size (the player layout runs first, so the placing waits one frame).
     * @param {HTMLElement} caption The caption.
     * @param {function():(HTMLElement|null)} media Returns the current media element (a rebuild replaces it).
     * @param {HTMLElement[]} observe Elements whose size changes move the text.
     */
    function TrackTextCaption(caption, media, observe) {
        let pending = 0;
        const place = () => {
            if (pending)
                return;
            pending = requestAnimationFrame(() => {
                pending = 0;
                const m = media();
                if (m)
                    PlaceTextCaption(caption, m);
            });
        };
        const ro = new ResizeObserver(place);
        for (const e of observe)
            if (e)
                ro.observe(e);
        window.addEventListener("resize", place);
        window.addEventListener("scroll", place, true);
        //  Late layout (fonts, the first frame of an effect)
        place();
        setTimeout(place, 250);
        setTimeout(place, 1000);
    }

    /**
     * Show a refused media link (MediaLinkProblem) as an in-page state: the reason and, for another site, a button that
     * opens the media in a new tab. The reason is also announced to screen readers.
     * @param {HTMLElement} target The element to show it in.
     * @param {{Text: string, Url: (string|null)}} problem The problem.
     */
    function ShowLinkProblem(target, problem) {
        console.warn("Media: refused the link parameter");
        const box = Sw.el("div.MediaRefused", { attrs: { role: "status" } },
            Sw.el("p.MediaRefusedText", { text: problem.Text }));
        if (problem.Url) {
            box.append(new Sw.Button({
                Text: _TF("Open in a new tab", "Button text on the media viewer page, opens media from another site in a new browser tab"),
                Title: problem.Url,
                Icon: "IconMediaNewWindow",
                OnClick: () => Sw.Page.Navigate(problem.Url, "_blank"),
            }).E);
        }
        target.append(box);
        Sw.Page.Announce(problem.Text);
    }

    /**
     * Rebuild a player in place with new props, keeping volume, mute, loop, play state and position (the new player is
     * built off-screen, class `Replacing`, then the old one is removed and disposed).
     * @param {object} player The current player (from CreatePlayer).
     * @param {function(HTMLElement):Promise<Array>} create Creates the new player before the given element; returns the
     *   CreatePlayer result.
     * @returns {Promise<object>} The new player.
     */
    async function RebuildPlayer(player, create) {
        const old = player;
        const state = {
            Volume: old.Volume, Muted: old.Muted, Paused: old.Paused, Looping: old.Looping, Pos: old.GetPos(), OnEnded: old.OnEnded,
        };
        const pd = await create(old.TopElement);
        const np = pd[0];
        if (old.Controls && np.Controls)
            old.Controls.replaceWith(np.Controls);
        if (old.Stats && np.Stats)
            old.Stats.replaceWith(np.Stats);
        np.OnEnded = state.OnEnded;
        await np.SetVolume(state.Volume);
        if (!state.Muted)
            await np.UnMute();
        if (!state.Looping)
            await np.Once();
        if (!state.Paused)
            await np.Play();
        if (state.Pos > 0)
            await np.Seek(state.Pos);
        old.TopElement.remove();
        np.TopElement.classList.remove("Replacing");
        await old.OnDispose();
        np.SetButtonState();
        return np;
    }

    // ------------------------------------------------------------------ stats

    /**
     * Create the `<Media-Stats>` block of a player: live stat chips (updated every frame while attached).
     * A chip copies "name: text (value)" on click / Enter / Space; its description shows as tooltip, on long press and focus.
     * @param {object} player The player.
     * @param {object} props The media params.
     * @returns {HTMLElement} The `<Media-Stats>` element.
     */
    function CreateStats(player, props) {
        const root = document.createElement("Media-Stats");
        const copyHint = _TF("Select to copy the value to the clipboard.", "Tool tip suffix of a media statistics value");

        /**
         * Add a stat chip.
         * @param {string} name Display name.
         * @param {string} title Description.
         * @param {string} [cls] Category class: "Adjust" (trim), "Clip" (crop) or "Actual" (render size).
         * @param {function():(Array|null)} get Returns [text, value] or null to hide the chip.
         * @returns {function():void} Updates the chip.
         */
        function add(name, title, cls, get) {
            const statE = document.createElement("Media-Stat");
            statE.title = title + "\n\n" + copyHint;
            statE.tabIndex = 0;
            statE.setAttribute("role", "button");
            if (cls)
                statE.classList.add(cls);
            const nameE = document.createElement("Media-StatName");
            nameE.textContent = name + ":";
            const val = document.createElement("Media-StatValue");
            val.textContent = "-";
            statE.append(nameE, val);
            root.appendChild(statE);
            let text = "-";
            let value = "-";
            const copy = ev => {
                if (Sw.BadClick(ev))
                    return;
                Sw.Clipboard.Copy(name + ": " + text + " (" + value + ")");
            };
            statE.addEventListener("click", copy);
            Sw.KeyClick(statE);
            if (Sw.Hint)
                Sw.Hint.Attach(statE, statE.title);
            return () => {
                let r = null;
                try {
                    r = get();
                }
                catch {
                    return;
                }
                if (!r) {
                    statE.classList.add("Hide");
                    return;
                }
                statE.classList.remove("Hide");
                if (r[0] === text)
                    return;
                text = r[0];
                value = r.length > 1 ? r[1] : r[0];
                val.textContent = text;
            };
        }

        /** @param {number} v Seconds. @returns {Array} [text, value]. */
        const secs = v => [_T("{0} s", Sw.Format.Number(v, 2), "A media statistics value in seconds (s is the unit), {0} is the number"), v];
        /** @param {number} v Pixels. @returns {Array} [text, value]. */
        const px = v => [v + " px", v];
        const ups = [];
        const visual = player.HaveVisual;
        //  Every player shows its play position and duration, also a still image (its slideshow timeline), as before the rewrite
        {
            ups.push(add(_TF("Time", "Name of a media statistics value: the current play position"), _TF("The current position in seconds", "Description of a media statistics value"), null,
                () => secs(player.GetPos())));
            ups.push(add(_TF("Duration", "Name of a media statistics value: the length of the media"), _TF("The duration of the media in seconds, less than zero means unknown or infinity", "Description of a media statistics value"), null,
                () => {
                    const v = player.Duration;
                    return v < 0 ? ["?", v] : secs(v);
                }));
        }
        if ((typeof player.AvgDrawTimeMs !== "undefined") && (typeof player.Query !== "undefined"))
            ups.push(add(_TF("GPU time", "Name of a media statistics value: time spent on the graphics card"), _TF("The GPU time spent rendering the effect in ms", "Description of a media statistics value"), null,
                () => {
                    let v = player.AvgDrawTimeMs;
                    if (v <= 0)
                        v = player.TotalTime / Math.max(1, player.MeasureCount);
                    return [_T("{0} ms", Sw.Format.Number(v, 3), "A media statistics value in milliseconds (ms is the unit), {0} is the number"), v];
                }));
        if (typeof player.CompilationTime !== "undefined")
            ups.push(add(_TF("Build", "Name of a media statistics value: the shader compilation time"), _TF("The duration of the compilation step of this effect in ms", "Description of a media statistics value"), null,
                () => {
                    const v = player.CompilationTime;
                    if (v === -3)
                        return [_TF("error", "Media statistics value: the effect failed to compile")];
                    if (v < 0)
                        return [_TF("compiling", "Media statistics value: the effect is being compiled")];
                    return [_T("{0} ms", Sw.Format.Number(v, 1), "A media statistics value in milliseconds (ms is the unit), {0} is the number"), v];
                }));
        if (player.HaveAudio)
            ups.push(add(_TF("Volume", "Name of a media statistics value: the audio volume"), _TF("The current audio volume of the media as a percentage", "Description of a media statistics value"), null,
                () => {
                    const v = player.GetTrueVolume();
                    return [_T("{0} %", Sw.Format.Number(v * 100, 1), "A media statistics value as a percentage, {0} is the number"), v];
                }));
        if (visual) {
            ups.push(add(_TF("Width", "Name of a media statistics value: the media width"), _TF("The width in pixels of the media", "Description of a media statistics value"), null, () => px(player.Width)));
            ups.push(add(_TF("Height", "Name of a media statistics value: the media height"), _TF("The height in pixels of the media", "Description of a media statistics value"), null, () => px(player.Height)));
        }
        if (typeof props.StartAt !== "undefined") {
            ups.push(add(_TF("Trimmed time", "Name of a media statistics value: the position inside the trimmed part"), _TF("The current position in the trimmed period in seconds", "Description of a media statistics value"), "Adjust",
                () => {
                    const b = player.Params.StartAt;
                    return b > 0 ? secs(player.GetPos() - b) : null;
                }));
            ups.push(add(_TF("Trimmed duration", "Name of a media statistics value: the length of the trimmed part"), _TF("The duration of the trimmed period in seconds", "Description of a media statistics value"), "Adjust",
                () => {
                    const par = player.Params;
                    const b = par.StartAt;
                    const e = par.EndAt;
                    if (!((b > 0) || (e > b)))
                        return null;
                    return secs((e > b ? e : player.Duration) - b);
                }));
        }
        if (visual && (typeof props.Crop !== "undefined")) {
            /** @returns {boolean} True if something is cropped. */
            const cropped = () => (player.ClipWidth !== player.Width) || (player.ClipHeight !== player.Height) || (player.ClipX !== 0) || (player.ClipY !== 0);
            ups.push(add(_TF("Cropped width", "Name of a media statistics value"), _TF("The cropped width in pixels", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipWidth) : null));
            ups.push(add(_TF("Cropped height", "Name of a media statistics value"), _TF("The cropped height in pixels", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipHeight) : null));
            ups.push(add(_TF("Cropped left", "Name of a media statistics value"), _TF("The number of cropped pixels on the left", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipX) : null));
            ups.push(add(_TF("Cropped top", "Name of a media statistics value"), _TF("The number of cropped pixels on the top", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipY) : null));
        }
        if (visual && (typeof props.AdaptiveSize !== "undefined")) {
            ups.push(add(_TF("Render width", "Name of a media statistics value: the canvas pixel width"), _TF("The actual rendered width in pixels", "Description of a media statistics value"), "Actual",
                () => player.Params.AdaptiveSize ? px(player.Element.width) : null));
            ups.push(add(_TF("Render height", "Name of a media statistics value: the canvas pixel height"), _TF("The actual rendered height in pixels", "Description of a media statistics value"), "Actual",
                () => player.Params.AdaptiveSize ? px(player.Element.height) : null));
        }

        /**
         * Frame loop: refresh every chip; stops once the block left the document (after the first frame).
         * @param {number} [time] rAF time stamp (undefined on the first, direct call).
         */
        function update(time) {
            if ((typeof time !== "undefined") && !root.isConnected)
                return;
            for (const u of ups)
                u();
            requestAnimationFrame(update);
        }
        update();
        return root;
    }

    // ------------------------------------------------------------------ controls

    /**
     * Create the `<Media-Controls>` block of a player (transport, loop, audio and source buttons) and the player's
     * `SetButtonState` (buttons whose action would not change the state are disabled). Mute / loop / volume choices are
     * stored in the preferences.
     * @param {object} player The player.
     * @returns {HTMLElement} The `<Media-Controls>` element.
     */
    function CreateControls(player) {
        const root = document.createElement("Media-Controls");
        const buttons = [];

        /**
         * Add a button.
         * @param {string} text Text.
         * @param {string} title Tooltip.
         * @param {string} icon Icon class (media.css / ui icons).
         * @param {function():boolean} enabled Enabled state (null = always).
         * @param {function():Promise<*>} action Click action.
         */
        function add(text, title, icon, enabled, action) {
            const b = new Sw.Button({
                Text: text, Title: title, Icon: icon, Class: "media-button", Small: true,
                OnClick: async () => {
                    await action();
                    player.SetButtonState();
                },
            });
            buttons.push([b, enabled]);
            root.appendChild(b.E);
        }

        //  Every player that can play gets the transport buttons, also a still image (its slideshow timeline), as before the rewrite
        if (player.CanPlay) {
            add(_TF("Pause", "Media player button text"), _TF("Pause media", "Media player button tool tip"), "IconMediaPreviewPause", () => !player.Paused, () => player.Pause());
            add(_TF("Play", "Media player button text"), _TF("Play media", "Media player button tool tip"), "IconMediaPreviewPlay", () => player.Paused, () => player.Play());
            add(_TF("Stop", "Media player button text"), _TF("Stop playing media", "Media player button tool tip"), "IconMediaPreviewStop", null, () => player.Stop());
            add(_TF("Loop", "Media player button text"), _TF("Loop media", "Media player button tool tip"), "IconMediaPreviewLoop", () => !player.Looping, () => {
                Prefs.SetLoop(true);
                return player.Loop();
            });
            add(_TF("Once", "Media player button text"), _TF("Play media once then stop", "Media player button tool tip"), "IconMediaPreviewOnce", () => player.Looping, () => {
                Prefs.SetLoop(false);
                return player.Once();
            });
            if (player.HaveAudio) {
                add(_TF("Mute", "Media player button text"), _TF("Mute media", "Media player button tool tip"), "IconMediaPreviewMute", () => !player.Muted, () => {
                    Prefs.SetMuted(true);
                    return player.Mute();
                });
                add(_TF("Un mute", "Media player button text"), _TF("Un-mute media", "Media player button tool tip"), "IconMediaPreviewUnMute", () => player.Muted, () => {
                    Prefs.SetMuted(false);
                    return player.UnMute();
                });
                for (const v of [1, 0.75, 0.5, 0.25]) {
                    const pct = Math.round(v * 100);
                    add(_T("VOL: {0}%", pct, "Media player button text, sets the volume. {0} is the volume in percent"),
                        _T("Set the volume to {0}%", pct, "Media player button tool tip. {0} is the volume in percent"),
                        v >= 0.75 ? "IconMediaPreviewVolLoud" : "IconMediaPreviewVolLow", () => player.Volume != v, () => {
                            Prefs.SetVolume(v);
                            return player.SetVolume(v);
                        });
                }
            }
        }
        //  Opens the original media file: in its registered editor when there is one, else the file itself in a new tab
        add(_TF("Open file", "Media player button text, opens the original media file (in a new tab, or in its editor when there is one)"),
            _TF("Open the original file in a new tab (or in its editor, when there is one)", "Media player button tool tip of the button that opens the original media file"),
            "external", null, () => Sw.Page.EditOrOpen(player.Source));

        player.SetButtonState = () => {
            for (const [b, enabled] of buttons)
                if (enabled)
                    b.SetDisabled(!enabled(), _TF("The player is already in this state", "Why a media player button (ex: Play, Mute, a volume) is disabled: the player already plays, is muted, has that volume and so on"));
        };
        player.MessageElement = root;
        root.addEventListener("mediaEnded", () => player.SetButtonState());
        return root;
    }

    // ------------------------------------------------------------------ player with chrome

    /**
     * Creates a media player with its preview box and (optionally) a stats block and a controls block.
     *
     * DOM: `<Media-Preview>` (the player's TopElement) holding `<Media-AspectContainer>` (the clip box, class
     * `MediaPreviewFit` or `MediaPreviewFill`) with the player element and a `<Media-Overlay>` (crop visualization).
     * With `before` the preview gets class `Replacing` (built off-screen) and is inserted before `before`.
     * Unless `noStats` / `noControls`, `<Media-Stats>` / `<Media-Controls>` are appended to `target`.
     * The layout runs now, from a ResizeObserver on the preview and on clip changes.
     *
     * Members added to the player: `SetButtonState`, `UpdateSize`, `TopElement`, `Stats`, `Controls` and with controls
     * `MessageElement` (the `<Media-Controls>`, which listens for `mediaEnded`).
     *
     * @param {HTMLElement} target - Container that receives the preview, stats and controls.
     * @param {number} type - Media type (one of `MediaTypes`).
     * @param {string} data - Media URL (or other type specific data, e.g. YouTube id).
     * @param {?object} props - Media params; when null, `player.Params` is used.
     * @param {boolean} [noControls] - True to skip the controls.
     * @param {?HTMLElement} [before] - Existing element to insert the new preview before (player rebuild).
     * @param {boolean} [noStats] - True to skip the stats.
     * @param {boolean} [fit=true] - True: fit inside the box and place the stats/controls right under the media; false: fill.
     * @param {number} [aspectX=16] - Box aspect X; `<= 0` (either aspect) means automatic.
     * @param {number} [aspectY=9] - Box aspect Y.
     * @param {number} [alignX=0.5] - Horizontal alignment (0..1) of the clip inside the preview box.
     * @param {number} [alignY=0] - Vertical alignment (0..1) of the clip inside the preview box.
     * @param {boolean} [quiet] - True: a failure throws an Error with a short translated reason (the caller shows it, ex:
     * the viewer's error state) instead of showing a toast and returning undefined; loading is limited to LoadTimeoutMs.
     * @returns {Promise<(Array|undefined)>} `[player, mediaControls, mediaStats]` (null when not created), or undefined on failure.
     */
    async function CreatePlayer(target, type, data, props, noControls, before, noStats, fit, aspectX, aspectY, alignX, alignY, quiet) {
        await playerReady;
        fit = fit ?? true;
        aspectX = aspectX ?? 16;
        aspectY = aspectY ?? 9;
        alignX = alignX ?? 0.5;
        alignY = alignY ?? 0;
        const autoAspect = (aspectX <= 0) || (aspectY <= 0);

        const p = document.createElement("Media-Preview");
        if (before) {
            p.classList.add("Replacing");
            target.insertBefore(p, before);
        }
        else
            target.appendChild(p);
        const clip = document.createElement("Media-AspectContainer");
        p.appendChild(clip);

        const player = await MediaPlayer.Create(type, data, props);
        if (!player) {
            const text = _T("No supported player found for type {0}", type, "Error when no media player exists for a media type. {0} is the type number");
            p.remove();
            if (quiet)
                throw new Error(text);
            Sw.Fail(text, 100000);
            return;
        }
        if (!props)
            props = player.Params;
        if (!player.HaveVisual)
            p.classList.add("NoVisual");
        clip.appendChild(player.Element);
        let cached = false;
        if (quiet) {
            let timer = 0;
            const timeout = new Promise(r => timer = setTimeout(() => r("timeout"), LoadTimeoutMs));
            try {
                cached = await Promise.race([player.Cache(), timeout]);
            }
            finally {
                clearTimeout(timer);
            }
            if (cached === "timeout") {
                p.remove();
                throw new Error(_TF("The media took too long to load.", "Reason on the media viewer error state when the media did not load within some seconds"));
            }
        }
        else
            cached = await player.Cache();
        if (!cached) {
            const text = _TF("Failed to cache the media", "Error when a media file could not be loaded");
            if (quiet) {
                p.remove();
                throw new Error(_TF("The media could not be loaded: the file is missing or this browser can't show it.", "Reason on the media viewer error state when the media file could not be loaded"));
            }
            Sw.Fail(text, 100000);
            return;
        }
        const overlay = document.createElement("Media-Overlay");
        clip.appendChild(overlay);
        clip.classList.add(fit ? "MediaPreviewFit" : "MediaPreviewFill");

        //  The stats follow the player's params (as the original, whose layout step read props = player.Params first): an image or
        //  text with an effect gets the render size of its effect too
        const stats = noStats ? null : CreateStats(player, player.Params || props);
        player.SetButtonState = () => { };
        const controls = noControls ? null : CreateControls(player);
        if (stats)
            target.appendChild(stats);
        if (controls)
            target.appendChild(controls);

        /**
         * Place the stats (or the controls) just under the visible media (fit mode).
         * @param {number} bottom The media bottom in viewport px.
         */
        function placeBelow(bottom) {
            if (!fit)
                return;
            const e = stats ?? controls;
            if (e)
                e.style.top = ((bottom + window.innerHeight * 0.02) | 0) + "px";
        }

        /**
         * Lay out the clip box and the player element inside `<Media-Preview>` (see the file README).
         * AdaptiveSize: the player gets the box size; otherwise the media size scaled (fit = min, fill = max scale) around
         * the clipped area, and the clip box is sized to the aspect and aligned. Without UseClip the overlay shows the cropped
         * margins as semi-transparent red borders.
         */
        function UpdateSize() {
            const par = player.Params;
            const useClip = player.UseClip;
            const ow = player.Width;
            const oh = player.Height;
            const crw = (useClip ? player.ClipWidth : ow) ?? ow;
            const crh = (useClip ? player.ClipHeight : oh) ?? oh;
            const rect = p.getBoundingClientRect();
            if (autoAspect) {
                if (par.AdaptiveSize) {
                    aspectX = rect.width;
                    aspectY = rect.height;
                } else {
                    aspectX = crw;
                    aspectY = crh;
                }
            }
            if ((crw <= 0) || (crh <= 0)) {
                clip.style.width = "0";
                clip.style.height = "0";
                return;
            }
            const crx = (useClip ? player.ClipX : 0) ?? 0;
            const cry = (useClip ? player.ClipY : 0) ?? 0;
            let cw = rect.width;
            let ch = cw * aspectY / aspectX;
            if (ch > rect.height) {
                ch = rect.height;
                cw = ch * aspectX / aspectY;
            }
            const e = player.Element;
            const es = e.style;
            const cs = clip.style;
            if (par.AdaptiveSize) {
                es.transform = null;
                es.width = Math.round(rect.width) + "px";
                es.height = Math.round(rect.height) + "px";
                cs.left = cs.top = cs.width = cs.height = null;
                placeBelow(rect.bottom);
            } else {
                es.width = par.Width + "px";
                es.height = par.Height + "px";
                const ox = (rect.width - cw) * alignX;
                const oy = (rect.height - ch) * alignY;
                cs.left = ox + "px";
                cs.top = oy + "px";
                cs.width = cw + "px";
                cs.height = ch + "px";
                const scaleX = cw / crw;
                const scaleY = ch / crh;
                const scale = fit ? Math.min(scaleX, scaleY) : Math.max(scaleX, scaleY);
                const dx = (cw - (scale * (crw + crx * 2))) * 0.5;
                const dy = (ch - (scale * (crh + cry * 2))) * 0.5;
                es.transform = "scale(" + (scale * 100) + "%) translate(" + (dx / scale) + "px, " + (dy / scale) + "px)";
                placeBelow(oy + ch + rect.y);
                const os = overlay.style;
                if (useClip)
                    overlay.classList.remove("Show");
                else {
                    overlay.classList.add("Show");
                    const col = "px solid rgba(255, 0, 0, 0.25)";
                    const cl = player.ClipX;
                    const ct = player.ClipY;
                    const cr = ow - cl - player.ClipWidth;
                    const cb = oh - ct - player.ClipHeight;
                    os.borderLeft = cl > 0 ? cl + col : null;
                    os.borderTop = ct > 0 ? ct + col : null;
                    os.borderRight = cr > 0 ? cr + col : null;
                    os.borderBottom = cb > 0 ? cb + col : null;
                    es.clipPath = null;
                }
            }
            const os = overlay.style;
            os.transform = es.transform;
            os.width = e.clientWidth + "px";
            os.height = e.clientHeight + "px";
        }
        UpdateSize();
        player.OnClipChanged = () => UpdateSize();
        new ResizeObserver(() => UpdateSize()).observe(p);

        player.UpdateSize = UpdateSize;
        player.TopElement = p;
        player.Stats = stats;
        player.Controls = controls;
        return [player, controls, stats];
    }

    /**
     * Applies the stored user preferences and starts playback, asking for a click when audio can't be un-muted:
     * loop / once, volume, then un-mute (unless the user chose mute) and play. When the browser blocks audio and the user
     * did not choose mute, a full page `<Media-NoAudio>` button ("Tap or click once to get audio!") un-mutes and plays on
     * click / Enter / Space (playback does not start before that).
     * @param {object} player - Player returned by CreatePlayer (needs `SetButtonState`).
     * @returns {Promise<void>}
     */
    async function PlayWithPopup(player) {
        if (Prefs.Loop())
            await player.Loop();
        else
            await player.Once();
        player.SetVolume(Prefs.Volume());
        const muted = Prefs.Muted();
        if (!player.HaveAudio) {
            if (player.CanPlay)
                await player.Play();
            player.SetButtonState();
            return;
        }
        if (await MediaPlayerTools.CanUnMute()) {
            if (!muted)
                await player.UnMute();
            await player.Play();
            player.SetButtonState();
            return;
        }
        if (muted) {
            await player.Play();
            player.SetButtonState();
            return;
        }
        const e = document.createElement("Media-NoAudio");
        e.textContent = _TF("Tap or click once to get audio!", "Shown over a media player when the browser requires a click before playing sound");
        e.title = _TF("Tap or click anywhere", "Tool tip of the click to get audio overlay");
        e.tabIndex = 0;
        e.setAttribute("role", "button");
        e.addEventListener("click", async ev => {
            if (Sw.BadClick(ev))
                return;
            //  The button goes away: keep the focus on the media (the full screen action) instead of the page body (FOC-1)
            const hadFocus = e.contains(document.activeElement);
            e.remove();
            if (hadFocus) {
                const box = player.Element && player.Element.parentElement;
                //  The viewer's player (CreateChrome) is the focusable view around the media
                const view = player.TopElement && player.TopElement.closest("Media-View.MediaChrome");
                const to = view ? view
                    : (box && box.isConnected && box.getAttribute("tabindex") === "0") ? box
                        : (player.Controls && player.Controls.querySelector("button:not(:disabled)"));
                if (to)
                    Sw.Focus.Show(to);
            }
            await player.UnMute();
            await player.Play();
            player.SetButtonState();
        });
        Sw.KeyClick(e);
        document.body.appendChild(e);
        e.focus();
    }

    // ------------------------------------------------------------------ editor (MediaEdit.html and host pages)

    /** @type {Promise<boolean>|null} Memoized LoadEditor result. */
    let editorLoad = null;

    /**
     * Load the object editor of the Edit service (`../edit/Edit.js`, relative to this script) unless the page already has it,
     * and wait until its files are ready. Memoized per page.
     * @returns {Promise<boolean>} True when `Sw.Edit` is usable, false when the Edit service files are missing.
     */
    function LoadEditor() {
        if (!editorLoad)
            editorLoad = (async () => {
                try {
                    if (!Sw.Edit)
                        await Sw.Net.IncludeJs(scriptUrl, "../edit/Edit.js");
                    if (!Sw.Edit)
                        return false;
                    await (Sw.Edit.Ready || Sw.Edit.Init());
                    return true;
                }
                catch (e) {
                    console.warn("Media editor: the object editor (edit/Edit.js) could not be loaded: " + (e && e.message ? e.message : e));
                    return false;
                }
            })();
        return editorLoad;
    }

    /**
     * The .NET type name of the media params of a media type (SysWeaver.MicroService.Media, described by the TypeService of
     * the Edit service). Used when the props carry no `$type`.
     * @param {number} type A MediaTypes value.
     * @returns {string|null} The type name, or null when the type has no server side params type (text).
     */
    function ParamTypeName(type) {
        const n = {
            [MediaTypes.Image]: "MediaImage",
            [MediaTypes.Video]: "MediaVideo",
            [MediaTypes.Audio]: "MediaAudio",
            [MediaTypes.YouTube]: "MediaYouTube",
            [MediaTypes.Effect]: "MediaEffect",
        }[type];
        return n ? "SysWeaver.MicroService.Media." + n : null;
    }

    /**
     * Appends a property editor for the media params below the player (the object editor of the Edit service, loaded on
     * demand from ../edit/Edit.js; the type description comes from its TypeService `edit/GetTypeInfo` by `props.$type`).
     * Live edits (no rebuild): Volume, Speed, AdaptiveSize, Crop, EffectProps (shader `//var:` constants); other edits
     * enable "Apply changes" (rebuild) while the apply state differs; "Save changes" (only with saveFn) is enabled while the
     * props differ from the initial state. With messagePrefix every change is posted to a linked MediaView.html.
     * Without the Edit service (or a type description) a short note replaces the editor; the player keeps working.
     * @param {HTMLElement} target - Container (same one used for the player).
     * @param {Array} playerRes - `[player, mediaControls, mediaStats]` as returned by CreatePlayer.
     * @param {number} type - Media type, reused to rebuild the player.
     * @param {string} data - Media data/URL, reused to rebuild the player.
     * @param {?object} props - The media params object to edit (modified in place).
     * @param {boolean} canEdit - False makes the editor read-only and adds no buttons.
     * @param {function(function():Promise<void>):Promise<boolean>} [saveFn] - Async save callback; receives the
     *   `reload` function and returns truthy on success.
     * @param {string} [messagePrefix] - Prefix for InterOp messages to a remote `MediaView.html` (its `ctrl` param).
     * @param {Array<HTMLElement>} [extraButtons] - Buttons added to the editor's button row (after Save / Apply).
     * @returns {Promise<boolean>} True when an editor was added.
     */
    async function EditProps(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix, extraButtons) {
        let player = playerRes[0];
        const editE = document.createElement("Media-Edit");
        target.appendChild(editE);
        /** @param {string} text The note shown instead of the editor. */
        const note = text => editE.appendChild(Sw.el("p.media-edit-note", { text: text }));
        if (!props || Object.keys(props).length <= 1) {
            note(_TF("This media has no settings to edit.", "Shown in the media editor when the media type has no editable settings"));
            return false;
        }
        if (props.Crop)
            props.Crop.VisualizeMargins = false;
        /** @param {string} name Message name. @param {object} [d] Data. */
        const post = (name, d) => {
            if (messagePrefix)
                Sw.InterOp.Post(messagePrefix + name, d);
        };

        /**
         * The props as JSON without Volume (volume changes never need Apply). Side effect: Volume moves to the end of the key order.
         * @returns {string} The apply relevant state.
         */
        function GetApplyState() {
            const oldVol = props.Volume;
            delete props.Volume;
            const ret = JSON.stringify(props);
            if (typeof oldVol !== "undefined")
                props.Volume = oldVol;
            return ret;
        }

        const E = (await LoadEditor()) ? Sw.Edit : null;
        const typeName = props["$type"] || ParamTypeName(type);
        const typeInfo = E && typeName ? await E.GetType(typeName) : null;
        if (!typeInfo) {
            note(E
                ? _TF("The settings of this media can't be edited: the service does not describe its type.", "Shown in the media editor when the service has no type information for the media settings")
                : _TF("The settings editor is not available: the edit service is missing.", "Shown in the media editor when the object editor files of the edit service are missing"));
            return false;
        }
        if (player.ScriptTypeInfo) {
            E.AddType(player.ScriptTypeInfo);
            props.EffectProps = player.Params.EffectProps;
            typeInfo.Members.push(player.ScriptMember);
        }
        const options = new E.Options();
        options.ReadOnly = !canEdit;
        options.Title = false;
        options.Dev = canEdit && options.Dev;
        options.UndoStack = new E.UndoStack();
        const input = new E(typeInfo, null, options);
        await input.SetObject(props);
        editE.appendChild(input.Element);
        if (!canEdit)
            return true;

        let applyState = GetApplyState();
        let objState = JSON.stringify(input.GetObject());
        let applyButton = null;
        let saveButton = null;

        /**
         * Rebuild the player with the current props (Apply, and passed to saveFn).
         * @returns {Promise<void>}
         */
        async function reload() {
            applyButton.SetDisabled(true, _TF("No changes to apply: change a setting that can't be applied on the fly first", "Why the apply button of the media editor is disabled"));
            player = await RebuildPlayer(player, before => CreatePlayer(target, type, data, props, !player.Controls, before, !player.Stats));
            applyState = GetApplyState();
        }

        const row = Sw.ButtonRow();
        input.Element.appendChild(row);
        if (saveFn) {
            saveButton = new Sw.Button({
                Text: _TF("Save changes", "Button text in the media editor"),
                Icon: "IconSave", Kind: "primary", Disabled: _TF("No changes to save yet", "Why the save button of the media editor is disabled"),
                OnClick: async () => {
                    saveButton.SetDisabled(true, _TF("Wait until the changes are saved", "Why the save button of the media editor is disabled while saving"));
                    try {
                        if (await saveFn(reload)) {
                            objState = JSON.stringify(props);
                            post("Reload", props);
                            Sw.Info(_TF("Saved updates!", "Notification after the media settings were saved"));
                            return;
                        }
                        Sw.Fail(_TF("Failed to update!", "Error when the media settings could not be saved"));
                    }
                    catch (e) {
                        Sw.Fail(_TF("Failed to update!", "Error when the media settings could not be saved") + "\n" + e);
                    }
                    saveButton.SetDisabled(false);
                },
            });
            row.appendChild(saveButton.E);
        }
        applyButton = new Sw.Button({
            Text: _TF("Apply changes", "Button text in the media editor"),
            Title: _TF("Apply the changes that can't be applied on the fly, without saving.", "Button tool tip in the media editor"),
            Icon: "IconApply", Disabled: _TF("No changes to apply: change a setting that can't be applied on the fly first", "Why the apply button of the media editor is disabled"),
            OnClick: async () => {
                await reload();
                post("Reload", props);
            },
        });
        row.appendChild(applyButton.E);
        (extraButtons || []).forEach(b => row.appendChild(b));

        let clip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
        input.Element.addEventListener("EditChange", async ev => {
            let needApply = true;
            const mn = ev.detail.Member.Name;
            if (mn === "Volume") {
                player.Params.Volume = props.Volume;
                await player.ResetVolume();
                post("SetVolume", { Volume: props.Volume });
                needApply = false;
            }
            if (mn === "Speed") {
                player.Params.Speed = props.Speed;
                post("SetProps", { Speed: props.Speed });
                needApply = false;
            }
            if (mn === "AdaptiveSize") {
                player.UpdateSize();
                post("UpdateSize", { AdaptiveSize: props.AdaptiveSize });
                needApply = false;
            }
            if (mn === "Crop") {
                const newClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                if (newClip !== clip) {
                    clip = newClip;
                    player.UseClip = clip;
                    player.ApplyClip(props);
                    player.UpdateSize();
                } else
                    player.ApplyClip(props);
                post("Crop", { Crop: props.Crop });
                needApply = false;
            }
            if (mn === "EffectProps") {
                try {
                    player.Params.EffectProps = props.EffectProps;
                    player.LazyCompile();
                    needApply = false;
                    props.FxProps = JSON.stringify(props.EffectProps);
                    post("Compile", { EffectProps: props.EffectProps });
                }
                catch (e) {
                    console.warn("Failed to compile, error: \n" + e);
                }
            }
            if (saveButton)
                saveButton.SetDisabled(JSON.stringify(props) === objState, _TF("No changes to save yet", "Why the save button of the media editor is disabled"));
            const newApplyState = GetApplyState();
            if (needApply)
                applyButton.SetDisabled(newApplyState === applyState, _TF("No changes to apply: change a setting that can't be applied on the fly first", "Why the apply button of the media editor is disabled"));
            else
                applyState = newApplyState;
        });
        return true;
    }

    /**
     * Compute the InterOp message prefix that links an editor with its detached viewer.
     * @param {string} text The identifying text.
     * @returns {Promise<string>} A url safe prefix.
     */
    async function MessagePrefix(text) {
        return "MediaEdit" + (await Sw.Util.HashString(text)).replace(/[^A-Za-z0-9]/g, "").substring(0, 24);
    }

    /**
     * Read the data source of the editor from the url (host data mode, see EditMain).
     * @returns {{Get: string, Update: string|null, Id: *, Legacy: boolean, EditMask: number}|null} Null without `get`.
     * @throws {Error} A translated message when a parameter is missing or not on this site.
     */
    function ReadEditSource() {
        const U = Sw.Url;
        const getApi = U.Get("get");
        if (!getApi)
            return null;
        const legacy = !U.Has("id") && U.Has("aid");
        const idT = legacy ? U.Get("aid") : U.Get("id");
        if (!idT)
            throw new Error(_TF("No id parameter 'id' supplied!", "Error when the media editor is opened with a get url parameter but without the id url parameter"));
        let id;
        try {
            id = JSON.parse(idT);
        }
        catch {
            id = idT;
        }
        //  The update api: the url parameter, else the get api with its first "Get" (in the last path part) replaced by "Update"
        let updateApi = U.Get("update");
        if (!updateApi) {
            const derived = getApi.replace(/Get(?=[^/]*$)/, "Update");
            updateApi = derived !== getApi ? derived : null;
        }
        //  Security: the get and update apis must be on this site (a crafted link must not load or send the media data elsewhere)
        if (!U.SafeSameOrigin(getApi) || (updateApi && !U.SafeSameOrigin(updateApi)))
            throw new Error(_TF("The media editor cannot be opened: the api addresses (the get and update parameters) must be on this site.", "Error when the media editor is opened with an api url parameter that points to another web site (not allowed)"));
        const mask = parseInt(U.Get("editmask") ?? "", 10);
        return { Get: getApi, Update: updateApi, Id: id, Legacy: legacy, EditMask: isNaN(mask) ? 0 : mask };
    }

    /**
     * Boot function of the media editor (`MediaEdit.html`, also callable by a host page that loads media.js).
     *
     * Two modes, chosen by the url:
     * - **Link mode** (no `get`): the same parameters as MediaView.html: `link`/`d` (required), `type`/`t` (else guessed),
     *   `props` (JSON). The settings start from the server default of the media params type (`edit/GetDefaultInstance`)
     *   with `props` on top. Nothing is saved: "Copy settings" puts the settings JSON on the clipboard.
     * - **Host data mode** (`get`): `get` = an api url (relative to the page, on this site) answering `{Data, Type, Link, Edit}`
     *   for the request `id` (JSON, or a plain text; legacy name `aid`). `Edit` grants editing: any truthy value, or with
     *   `editmask=<bits>` a number sharing a bit with it. `update` = the save api (relative to the page, on this site;
     *   default: `get` with "Get" replaced by "Update" in its last path part), posted `{...id, Data}` for an object id,
     *   else `{Id: id, Data}` (legacy `aid`: `{AssignmentId: aid, Data}`); a truthy answer is success.
     *
     * Both modes show the player, Collapse / Expand, "New window" (a linked `MediaView.html?type&link&props&ctrl=<prefix>`
     * that follows every change) and the property editor (EditProps); the editor answers `<prefix>GetProps` with
     * `<prefix>SetProps`.
     * @returns {Promise<void>}
     */
    async function EditMain() {
        const page = document.createElement("Media-Full");
        document.body.appendChild(page);
        const target = page;
        const remove = Sw.Page.Loading();
        try {
            await playerReady;
            const src = ReadEditSource();
            let props, type, data, canEdit;
            if (src) {
                const info = await Sw.Net.Send(src.Get, src.Id);
                if (!info)
                    throw new Error(_TF("The media was not found.", "Error when the media editor's get api returned nothing"));
                props = info.Data;
                type = info.Type;
                data = info.Link;
                const e = info.Edit;
                canEdit = src.EditMask ? ((typeof e === "number" ? e : 0) & src.EditMask) !== 0 : !!e;
            }
            else {
                const mp = ReadMediaParams();
                const problem = MediaLinkProblem(mp);
                if (problem) {
                    ShowLinkProblem(target, problem);
                    return;
                }
                type = mp.Type;
                data = mp.Data;
                const tn = (mp.Props && mp.Props["$type"]) || ParamTypeName(type);
                let def = null;
                if (tn && await LoadEditor())
                    try {
                        def = await Sw.Edit.TypeInfo.GetInstance(tn);
                    }
                    catch (e) {
                        console.warn("Media editor: no default settings for " + tn + ": " + (e && e.message ? e.message : e));
                    }
                props = mp.Props || def ? Object.assign(def || {}, mp.Props || {}) : null;
                canEdit = true;
            }
            const pd = await CreatePlayer(target, type, data, props, false, null, false, true, 16, 9, 0.5, 0, true);
            const player = pd[0];
            if (!props)
                props = JSON.parse(JSON.stringify(player.Params));
            if (!props["$type"] && ParamTypeName(type))
                props["$type"] = ParamTypeName(type);

            //  Collapse / expand, new window
            const row = Sw.ButtonRow();
            target.appendChild(row);
            const pcl = page.classList;
            let didPlay = false;
            const collapseButton = new Sw.Button({
                Text: _TF("Collapse", "Button text, hides the media player"), Title: _TF("Hide the media player", "Button tool tip"),
                Icon: "IconMediaCollapse", Pressable: true,
                OnClick: () => toggleCollapse(),
            });
            /** Hide (pause) or show (resume) the player, stats and controls. */
            function toggleCollapse() {
                if (pcl.contains("Hide")) {
                    pcl.remove("Hide");
                    collapseButton.SetContent(_TF("Collapse", "Button text, hides the media player"), "IconMediaCollapse");
                    collapseButton.SetTitle(_TF("Hide the media player", "Button tool tip"));
                    if (didPlay)
                        player.Play();
                } else {
                    didPlay = !player.Paused;
                    player.Pause();
                    pcl.add("Hide");
                    collapseButton.SetContent(_TF("Expand", "Button text, shows the media player"), "IconMediaExpand");
                    collapseButton.SetTitle(_TF("Show the media player", "Button tool tip"));
                }
                collapseButton.SetPressed(pcl.contains("Hide"));
            }
            row.appendChild(collapseButton.E);
            const messagePrefix = await MessagePrefix((src ? src.Get + "_" + JSON.stringify(src.Id) : location.href) + "_" + type + "_" + data);
            row.appendChild(new Sw.Button({
                Text: _TF("New window", "Button text, opens the media player in a new window"),
                Title: _TF("Open the media player in a new window", "Button tool tip"),
                Icon: "IconMediaNewWindow",
                OnClick: () => {
                    if (!pcl.contains("Hide"))
                        toggleCollapse();
                    Sw.Page.Navigate(new URL("MediaView.html", scriptUrl).href + "?type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + encodeURIComponent(JSON.stringify(props)) + "&ctrl=" + encodeURIComponent(messagePrefix), "_blank");
                },
            }).E);

            //  Link mode: nothing is saved, the settings can be copied
            const extra = src ? null : [new Sw.Button({
                Text: _TF("Copy settings", "Button text in the media editor, copies the media settings as JSON"),
                Title: _TF("Copy the media settings (JSON) to the clipboard", "Button tool tip in the media editor"),
                Icon: "copy",
                OnClick: () => Sw.Clipboard.Copy(JSON.stringify(props, null, 2)),
            }).E];
            const saveFn = src && src.Update ? async reload => {
                const id = src.Id;
                const r = (id !== null && typeof id === "object" && !Array.isArray(id)) ? Object.assign({}, id) : (src.Legacy ? { AssignmentId: id } : { Id: id });
                r.Data = props;
                if (await Sw.Net.Send(src.Update, r)) {
                    await reload();
                    return true;
                }
                return false;
            } : null;
            await EditProps(target, pd, type, data, props, canEdit, saveFn, messagePrefix, extra);
            await PlayWithPopup(player);
            Sw.InterOp.On("*", m => {
                if (m && m.Type === messagePrefix + "GetProps")
                    Sw.InterOp.Post(messagePrefix + "SetProps", Object.assign({}, props));
            });
            Sw.InterOp.Post(messagePrefix + "Reload", Object.assign({}, props));
        }
        catch (e) {
            target.textContent = "";
            ShowLoadError(target, FailReason(e));
        }
        finally {
            remove();
            Sw.Page.Loaded();
        }
    }

    // ------------------------------------------------------------------ MediaView.html

    /**
     * Wait (up to 100 ms) for a linked editor to answer `<prefix>GetProps` with `<prefix>SetProps`.
     * @param {string} prefix The message prefix.
     * @returns {Promise<object|null>} The props sent by the editor, or null.
     */
    function AskEditorProps(prefix) {
        return new Promise(resolve => {
            let off = null;
            const done = v => {
                if (off)
                    off();
                off = null;
                resolve(v);
            };
            off = Sw.InterOp.On("*", m => {
                if (!m || m.Type !== prefix + "SetProps")
                    return;
                const p = Object.assign({}, m);
                delete p.Type;
                delete p.From;
                delete p.MagicXyz;
                delete p.To;
                done(p);
            });
            setTimeout(() => done(null), 100);
            Sw.InterOp.Post(prefix + "GetProps");
        });
    }

    /**
     * Boot function of `MediaView.html`.
     * URL params: `link`/`d` (required), `type`/`t`, `props` (JSON), `no_stats`, `no_controls`, `ctrl` (InterOp message
     * prefix of a controlling editor). The media fills the `<Media-View>` grid (fill = false, automatic aspect, centered);
     * activating the media (click, Enter, Space) opens `MediaPreview.html?full&back&pos=...` in the same frame.
     * With `ctrl` the props are first requested from the editor and later `<ctrl>*` messages update the player:
     * Reload (rebuild), SetVolume, UpdateSize, Crop, Compile; matching message fields are copied into the props.
     * @returns {Promise<void>}
     */
    async function ViewMain() {
        const target = document.body;
        try {
            await playerReady;
            const U = Sw.Url;
            const stats = !U.Has("no_stats");
            const controls = !U.Has("no_controls");
            const mp = ReadMediaParams();
            const problem = MediaLinkProblem(mp);
            if (problem) {
                ShowLinkProblem(target, problem);
                return;
            }
            const type = mp.Type;
            const data = mp.Data;
            let props = mp.Props;
            //  Use the file name as title (like edit/text.js and upstream; the link passed MediaLinkProblem): the last path segment of
            //  the link without its query. Also for a text media, like upstream (the link is the text itself; its length limited
            //  version inside the app shell, ReadMediaParams)
            {
                const path = String(type === MediaTypes.Text ? data : (U.Get("link") ?? U.Get("d") ?? "")).split("?")[0];
                //  (a YouTube link: "watch", "embed" ... is no name: the video code)
                const fname = type === MediaTypes.YouTube ? _T("YouTube {0}", data, "Title of the media viewer showing a YouTube video, {0} is the video code") : Sw.Url.FileName(path);
                if (fname)
                    document.title = fname;
            }
            const messagePrefix = U.Get("ctrl");
            if (messagePrefix) {
                const np = await AskEditorProps(messagePrefix);
                if (np)
                    props = np;
            }
            const mview = document.createElement("Media-View");
            target.appendChild(mview);
            const caption = type === MediaTypes.Text ? AddTextCaption(target) : null;
            //  A web font that failed: a default font is used, a small notice stays on the page
            FontNotice(target);

            /**
             * Create (or rebuild before an element) the viewer player and hook the full view action.
             * @param {HTMLElement|null} before Element to insert before.
             * @returns {Promise<Array>} The CreatePlayer result.
             */
            async function create(before) {
                //  The first player fails with a reason (shown as the error state), a rebuild keeps the toast.
                //  With controls the player chrome (CreateChrome) replaces the button row and holds the stats
                const pd = await CreatePlayer(mview, type, data, props, true, before, !stats, false, 0, 0, 0.5, 0.5, !before);
                if (!pd)
                    throw new Error(_TF("Failed to cache the media", "Error when a media file could not be loaded"));
                return pd;
            }

            /**
             * Open the full page preview of the current position (a click there goes back): when the browser can't show the
             * player in full screen (ex: an iPhone).
             * @param {object} pl The player.
             */
            function showFull(pl) {
                Sw.Page.Navigate("MediaPreview.html?full&back&pos=" + pl.GetPos() + "&type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + encodeURIComponent(JSON.stringify(pl.Params)), "_self");
            }

            let player = (await create(null))[0];
            props = player.Params;
            if (controls)
                CreateChrome(mview, () => player, {
                    Stats: stats,
                    FullPage: () => showFull(player),
                    Info: () => ({
                        Type: type,
                        Link: type === MediaTypes.YouTube ? "https://www.youtube.com/watch?v=" + data
                            : type === MediaTypes.Text ? null : new URL(data, window.location.href).href,
                    }),
                    Load: m => ShowLink(m),
                });
            //  The caption sits directly above the text (a rebuild replaces the player: read it at each placing)
            if (caption)
                TrackTextCaption(caption, () => player ? player.Element : null, [mview]);
            player.OnLoop = () => console.log("Loop");
            const orgE = player.OnEnded;
            player.OnEnded = () => {
                console.log("End");
                if (orgE)
                    return orgE();
            };

            if (messagePrefix) {
                Sw.InterOp.On("*", async m => {
                    const t = m && m.Type;
                    if (!t || !t.startsWith(messagePrefix))
                        return;
                    Object.getOwnPropertyNames(m).forEach(pn => {
                        if (typeof props[pn] !== "undefined")
                            props[pn] = m[pn];
                    });
                    switch (t.substring(messagePrefix.length)) {
                        case "Reload":
                            player = await RebuildPlayer(player, create);
                            props = player.Params;
                            break;
                        case "SetVolume":
                            player.ResetVolume();
                            break;
                        case "UpdateSize":
                            player.UpdateSize();
                            break;
                        case "Crop":
                            player.UseClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                            player.ApplyClip(props);
                            player.UpdateSize();
                            break;
                        case "Compile":
                            player.LazyCompile();
                            break;
                    }
                });
            }
            //  A YouTube link with a start time ("t=90"): start there
            if (mp.Start > 0)
                await player.Seek(mp.Start);
            await PlayWithPopup(player);
        }
        catch (e) {
            //  An in-page error state with the reason and "Try again" (was a toast over an empty page)
            ShowLoadError(target, FailReason(e));
        }
    }

    /**
     * Show another media in the viewer (a dropped / pasted / typed link): this page with the new `link` (and `type`), keeping the
     * view options (`no_stats`, ...) but not `props` / `ctrl` (they belong to the old media). The page is loaded again, so the link
     * goes through the same checks (MediaLinkProblem) as a link in the address.
     * @param {{Link: string, Type: number}} m The media link (ParseMediaLink).
     */
    function ShowLink(m) {
        const u = new URL(window.location.href);
        const keep = new URLSearchParams();
        for (const [k, v] of u.searchParams)
            if (!/^(link|d|type|t|props|ctrl)$/i.test(k))
                keep.append(k, v);
        keep.set("link", m.Link);
        keep.set("type", String(m.Type));
        u.search = keep.toString();
        Sw.Page.Navigate(u.href, "_self");
    }

    // ------------------------------------------------------------------ player chrome (MediaView.html)

    /** @type {string} Storage key: "true" = the stats panel of the viewer is open. */
    const StatsKey = "SysWeaver.Media.ShowStats";

    /** @type {number} Milliseconds without pointer / key activity before the controls of a playing media hide. */
    const ChromeHideMs = 2500;

    /**
     * A play position as text: m:ss, h:mm:ss from one hour.
     * @param {number} s Seconds.
     * @returns {string} The text.
     */
    function FormatTime(s) {
        s = Math.max(0, Math.floor(s || 0));
        const h = Math.floor(s / 3600);
        const m = Math.floor(s / 60) % 60;
        const ss = String(s % 60).padStart(2, "0");
        return h > 0 ? h + ":" + String(m).padStart(2, "0") + ":" + ss : m + ":" + ss;
    }

    /**
     * The time line length of a media without a known end (an effect runs forever): it grows in steps of 5 minutes, so the
     * thumb moves visibly and a position in the past can still be picked.
     * @param {number} pos The play position in seconds.
     * @returns {number} The length in seconds.
     */
    function OpenEndedLength(pos) {
        return Math.max(300, Math.ceil((Math.max(0, pos) + 1) / 300) * 300);
    }

    /**
     * The media player controls of MediaView.html, on top of the media (like a video player): a bottom bar with the time line,
     * Play / Pause, Stop, Loop, the volume (Mute + a slider), the time, Stats, Open file and Full screen, a title bar with the
     * file name, a big Play button while paused and the stats panel (`<Media-Stats>` of the player, opened with Stats or "i",
     * remembered in `localStorage["SysWeaver.Media.ShowStats"]`).
     * The bars hide after 2.5 s without pointer or key activity while the media plays (also the mouse pointer); they stay while
     * paused, while the pointer is on them, while a control has the keyboard focus and for audio only media.
     * Mouse: a click on the media plays / pauses, a double click toggles full screen. Touch: a tap shows / hides the controls.
     * Keys (the player has the focus): Space / K play / pause, Left / Right -5 / +5 s, Home the start, Up / Down the volume,
     * M mute, F full screen, I stats.
     * A media without a known end (Duration <= 0: an effect) gets a time line that grows in steps of 5 minutes and "∞" as its length.
     * The state is read from the player every frame, so a rebuilt player (getPlayer returns the new one) is followed.
     * @param {HTMLElement} view The `<Media-View>`.
     * @param {function():?object} getPlayer Returns the current player.
     * @param {object} o Options.
     * @param {boolean} o.Stats True: a Stats button and panel (the player's `Stats` element is moved into it).
     * @param {function():void} o.FullPage Called when the browser has no full screen for the element (opens the full page preview).
     */
    function CreateChrome(view, getPlayer, o) {
        const el = Sw.el;
        const player0 = getPlayer();
        view.classList.add("MediaChrome");
        view.tabIndex = 0;
        view.setAttribute("role", "region");
        view.setAttribute("aria-label", _TF("Media player", "Accessible name of the media player area of the media viewer"));
        if (!player0.HaveVisual)
            view.classList.add("MediaAudioOnly");

        const b = (icon, title, fn, opts) => new Sw.Button(Object.assign({
            Icon: icon, Title: title, Kind: "ghost", Class: "media-chrome-button",
            OnClick: () => {
                fn();
                show();
            },
        }, opts || {}));

        /** Play or pause. */
        const togglePlay = () => {
            const p = getPlayer();
            if (!p || !p.CanPlay)
                return;
            if (p.Paused)
                p.Play();
            else
                p.Pause();
        };

        /**
         * Seek, keeping one seek in flight (a drag on the time line asks for many positions; the last one wins).
         * @param {number} v The position in seconds.
         */
        let seekBusy = false;
        let seekNext = null;
        const seek = async v => {
            const p = getPlayer();
            if (!p || !p.CanPlay)
                return;
            seekNext = Math.max(0, v);
            if (seekBusy)
                return;
            seekBusy = true;
            try {
                while (seekNext !== null) {
                    const to = seekNext;
                    seekNext = null;
                    await p.Seek(to);
                }
            }
            finally {
                seekBusy = false;
            }
        };

        /** @param {number} v The volume 0..1 (0 mutes, more un mutes). */
        const setVolume = async v => {
            const p = getPlayer();
            if (!p || !p.HaveAudio)
                return;
            v = Math.max(0, Math.min(1, v));
            if (v > 0) {
                Prefs.SetVolume(v);
                await p.SetVolume(v);
                if (p.Muted) {
                    Prefs.SetMuted(false);
                    await p.UnMute();
                }
            }
            else if (!p.Muted) {
                Prefs.SetMuted(true);
                await p.Mute();
            }
        };

        const toggleMute = async () => {
            const p = getPlayer();
            if (!p || !p.HaveAudio)
                return;
            Prefs.SetMuted(!p.Muted);
            if (p.Muted)
                await p.UnMute();
            else
                await p.Mute();
        };

        const toggleLoop = async () => {
            const p = getPlayer();
            if (!p)
                return;
            Prefs.SetLoop(!p.Looping);
            if (p.Looping)
                await p.Once();
            else
                await p.Loop();
        };

        const toggleFull = async () => {
            if (Sw.FullScreen.Is()) {
                await Sw.FullScreen.Exit();
                return;
            }
            if (!(await Sw.FullScreen.Enter(view)))
                o.FullPage();
        };

        /**
         * A part as a percentage text ("125 %").
         * @param {number} v The part (1 = 100 %).
         * @returns {string} The text.
         */
        const pct = v => _T("{0} %", Sw.Format.Number(v * 100, 0), "A percentage in the media viewer (a speed or a volume), {0} is the number");

        /** @type {number} The chosen speed (kept for a rebuilt player). */
        let rate = 1;
        /** @param {number} r The speed (1 = normal). */
        const setRate = async r => {
            const p = getPlayer();
            if (!p || typeof p.SetRate !== "function")
                return;
            rate = r;
            p.SetRateFor = r;
            await p.SetRate(r);
        };

        /** The jump feedback ("-15 s" / "+15 s" on the side that was double tapped). */
        const skipE = el("div.media-skip", { attrs: { "aria-hidden": "true" } });
        let skipTimer = 0;
        /**
         * Jump back or forward (clamped to the start and, with a known end, the end).
         * @param {number} s Seconds (negative = back).
         */
        const skip = s => {
            const p = getPlayer();
            if (!p || !p.CanPlay)
                return;
            const dur = p.Duration;
            let to = Math.max(0, (p.GetPos() || 0) + s);
            if (dur > 0 && isFinite(dur))
                to = Math.min(dur, to);
            seek(to);
            skipE.textContent = (s < 0 ? "-" : "+") + _T("{0} s", Math.abs(s), "A jump of the media viewer in seconds (s is the unit), {0} is the number, ex: 15 s");
            skipE.classList.toggle("media-skip-back", s < 0);
            skipE.classList.add("media-skip-on");
            clearTimeout(skipTimer);
            skipTimer = setTimeout(() => skipE.classList.remove("media-skip-on"), 600);
            Sw.Page.Announce(skipE.textContent);
        };

        /** Copy the address, the type, the state, the stats and the settings of the media to the clipboard (text). */
        const copyInfo = () => {
            const p = getPlayer();
            if (!p)
                return;
            const info = o.Info();
            const lines = [];
            const add = (name, value) => {
                if (value !== null && value !== undefined && value !== "")
                    lines.push(name + ": " + value);
            };
            const secs = v => Sw.Format.Number(v, 2) + " s";
            const dur = p.Duration;
            add(_TF("Media", "Name of a line of the copied media information: the name of the media"), document.title);
            add(_TF("Address", "Name of a line of the copied media information: the address (url) of the media"), info.Link);
            add(_TF("Type", "Name of a line of the copied media information: the media type (Image, Video, YouTube ...)"), MediaTypes.Names[info.Type] ?? info.Type);
            add(_TF("Page", "Name of a line of the copied media information: the address of the media viewer page"), window.location.href);
            add(_TF("State", "Name of a line of the copied media information: playing or paused"), p.CanPlay ? (p.Paused ? _TF("Paused", "Media state in the copied media information") : _TF("Playing", "Media state in the copied media information")) : null);
            add(_TF("Position", "Name of a line of the copied media information: the play position"), p.CanPlay ? secs(p.GetPos() || 0) : null);
            add(_TF("Duration", "Name of a media statistics value: the length of the media"), p.CanPlay ? (dur > 0 && isFinite(dur) ? secs(dur) : "∞") : null);
            add(_TF("Loop", "Name of a line of the copied media information: loop on or off"), p.CanPlay ? (p.Looping ? _TF("on", "A switch value in the copied media information") : _TF("off", "A switch value in the copied media information")) : null);
            add(_TF("Speed", "Name of a line of the copied media information: the play speed"), typeof p.SetRate === "function" ? pct(p.Rate || 1) : null);
            if (p.HaveAudio) {
                add(_TF("Muted", "Name of a line of the copied media information: the sound is muted"), p.Muted ? _TF("on", "A switch value in the copied media information") : _TF("off", "A switch value in the copied media information"));
                add(_TF("Volume", "Name of a media statistics value: the audio volume"), pct(p.Volume || 0));
            }
            //  The stat chips (the render size, the GPU time, the crop ... as shown)
            //  (a name already written above, ex: Duration, is not repeated)
            const have = new Set(lines.map(l => l.split(": ")[0]));
            const chips = p.Stats ? [...p.Stats.querySelectorAll("Media-Stat:not(.Hide)")] : [];
            for (const c of chips) {
                const n = c.querySelector("Media-StatName");
                const v = c.querySelector("Media-StatValue");
                const name = n ? n.textContent.replace(/:\s*$/, "") : "";
                if (n && v && !have.has(name))
                    add(name, v.textContent);
            }
            let settings = null;
            try {
                settings = JSON.stringify(p.Params, null, 2);
            }
            catch {
                settings = null;
            }
            if (settings)
                lines.push("", _TF("Settings", "Name of the last part of the copied media information: the media settings as json") + ":", settings);
            Sw.Clipboard.Copy(lines.join("\n"), { Generic: true, What: _TF("Media information", "What the media viewer copied to the clipboard (the address, state, statistics and settings of the media), used in the copied message") });
        };

        /** Ask for an address and show it (a media link: a file of a known type or a YouTube link). */
        const askLink = async () => {
            const info = o.Info();
            const v = await Sw.Dialog.Prompt(_TF("Show a media", "Title of the dialog of the media viewer that asks for a media address"), info.Link || "", "https://www.youtube.com/watch?v=...", {
                Text: _TF("The address (url) of an image, a video, an audio file, an effect (.glsl) or a YouTube video.", "Text of the dialog of the media viewer that asks for a media address"),
                Label: _TF("Address", "Accessible name of the address box of the media viewer dialog that asks for a media address"),
                OkText: _TF("Show", "Button of the media viewer dialog that shows the typed media address"),
                Validate: x => ParseMediaLink(x) ? null : _TF("Not a media address: use a YouTube link or the address of a file of a known type (an image, a video, an audio file, .glsl).", "Error of the media viewer dialog that asks for a media address"),
            });
            if (v)
                o.Load(ParseMediaLink(v));
        };

        /**
         * The items of the ⋮ menu (also the context menu of the media).
         * @returns {Array<object|string>} The menu items.
         */
        const menuItems = () => {
            const p = getPlayer();
            if (!p)
                return [];
            const items = [];
            if (p.CanPlay) {
                items.push(
                    { Text: p.Paused ? _TF("Play", "Tool tip of the play button of the media viewer") : _TF("Pause", "Tool tip of the pause button of the media viewer"), Icon: p.Paused ? "play" : "pause", Hint: _TF("Space", "The name of the space bar key (a keyboard shortcut shown in a menu)"), OnClick: togglePlay },
                    { Text: _TF("Stop: back to the start", "Tool tip of the stop button of the media viewer"), Icon: "stop", OnClick: () => p.Stop() },
                    { Text: _TF("Back 15 seconds", "Menu item of the media viewer: jump back 15 seconds (also a double tap on the left side)"), Icon: "left", Hint: "J", OnClick: () => skip(-15) },
                    { Text: _TF("Forward 15 seconds", "Menu item of the media viewer: jump forward 15 seconds (also a double tap on the right side)"), Icon: "right", Hint: "L", OnClick: () => skip(15) },
                    { Text: _TF("Loop: start again at the end", "Tool tip of the loop switch of the media viewer"), Checked: !!p.Looping, OnClick: toggleLoop });
                if (typeof p.SetRate === "function") {
                    const max = p.MaxRate || Infinity;
                    const cur = p.Rate || 1;
                    items.push({
                        Text: _TF("Speed", "Menu item of the media viewer with the play speeds (a sub menu)"), Icon: "speed", Hint: pct(cur),
                        Items: [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 10].filter(r => r <= max).map(r => ({
                            Text: r === 1 ? _TF("Normal (100 %)", "Play speed of the media viewer: the normal speed") : pct(r),
                            Radio: true, Checked: Math.abs(cur - r) < 0.001, OnClick: () => setRate(r),
                        })),
                    });
                }
            }
            if (p.HaveAudio)
                items.push({ Text: _TF("Mute", "Tool tip of the mute button of the media viewer"), Checked: !!p.Muted, Hint: "M", OnClick: toggleMute });
            items.push("-");
            if (panel)
                items.push({ Text: _TF("Show the statistics", "Menu item of the media viewer that shows / hides the statistics panel"), Checked: !panel.hidden, Hint: "I", OnClick: () => setStats(panel.hidden) });
            items.push({ Text: Sw.FullScreen.Is() ? _TF("Leave full screen", "Tool tip of the full screen button of the media viewer in full screen") : _TF("Full screen", "Tool tip of the full screen button of the media viewer"), Icon: Sw.FullScreen.Is() ? "fullscreenExit" : "fullscreen", Hint: "F", OnClick: toggleFull });
            items.push("-",
                { Text: _TF("Show url...", "Menu item of the media viewer that asks for a media address and shows it"), Icon: "link", Hint: "U", OnClick: askLink },
                { Text: _TF("Copy information to clipboard", "Menu item of the media viewer that copies the address, the state, the statistics and the settings of the media"), Icon: "copy", OnClick: copyInfo },
                { Text: _TF("Open the original file", "Menu item of the media viewer that opens the original file in a new tab (or its editor)"), Icon: "external", OnClick: () => Sw.Page.EditOrOpen(p.Source) });
            return items;
        };

        //  The panel with the stats chips of the player (moved here from below the media)
        const panel = o.Stats ? el("div.media-stats-panel", { attrs: { role: "region", "aria-label": _TF("Media statistics", "Accessible name of the statistics panel of the media viewer") } }) : null;
        if (panel && player0.Stats)
            panel.append(player0.Stats);
        const setStats = on => {
            if (!panel)
                return;
            panel.hidden = !on;
            statsB.SetPressed(on);
            Sw.Storage.Set(StatsKey, on ? "true" : "false");
        };

        const playB = b("play", _TF("Play", "Tool tip of the play button of the media viewer"), togglePlay, { Shortcut: "Space" });
        const stopB = b("stop", _TF("Stop: back to the start", "Tool tip of the stop button of the media viewer"), () => getPlayer().Stop(), { Class: "media-chrome-button media-stop" });
        const loopB = b("IconMediaPreviewLoop", _TF("Loop: start again at the end", "Tool tip of the loop switch of the media viewer"), toggleLoop, { Pressable: true, AutoToggle: false });
        const muteB = b("IconMediaPreviewVolLoud", _TF("Mute", "Tool tip of the mute button of the media viewer"), toggleMute, { Shortcut: "M" });
        const volS = new Sw.Slider({
            Min: 0, Max: 1, Step: 0.01, Value: player0.Muted ? 0 : player0.Volume,
            Title: _TF("Volume", "Accessible name of the volume slider of the media viewer"),
            OnInput: v => setVolume(v),
        });
        volS.E.classList.add("media-volume");
        const timeS = new Sw.Slider({
            Min: 0, Max: 1, Step: 0.01, Value: 0,
            Title: _TF("Play position", "Accessible name of the time line slider of the media viewer"),
            OnInput: v => seek(v), OnChange: v => seek(v),
        });
        timeS.E.classList.add("media-timeline");
        const timeE = el("span.media-time", { attrs: { "aria-hidden": "true" } });
        const statsB = panel ? b("info", _TF("Statistics: the size, the time and the render details", "Tool tip of the statistics switch of the media viewer"), () => setStats(panel.hidden), { Pressable: true, AutoToggle: false, Shortcut: "I" }) : null;
        const openB = b("external", _TF("Open the original file in a new tab (or in its editor, when there is one)", "Media player button tool tip of the button that opens the original media file"),
            () => Sw.Page.EditOrOpen(getPlayer().Source));
        const fullB = b("fullscreen", _TF("Full screen", "Tool tip of the full screen button of the media viewer"), toggleFull, { Shortcut: "F" });
        const moreB = new Sw.Button({
            Icon: "moreV", Title: _TF("More: speed, jump 15 seconds, show an address, copy the information ...", "Tool tip of the menu button of the media viewer"),
            Kind: "ghost", Class: "media-chrome-button", Menu: () => menuItems(), MenuOptions: { Side: "above", Align: "end" },
        });
        const bigPlay = new Sw.Button({
            Icon: "play", Title: _TF("Play", "Tool tip of the play button of the media viewer"), Class: "media-bigplay",
            OnClick: () => {
                togglePlay();
                show();
            },
        });
        const title = el("span.media-title", { text: document.title });
        const top = el("div.media-top.media-chrome-part", null, title);
        const bar = el("div.media-bar.media-chrome-part", null,
            timeS.E,
            el("div.media-bar-row", null,
                playB.E, stopB.E, loopB.E,
                player0.HaveAudio ? muteB.E : null, player0.HaveAudio ? volS.E : null,
                timeE,
                el("span.media-bar-gap"),
                statsB ? statsB.E : null, openB.E, fullB.E, moreB.E));
        const art = player0.HaveVisual ? null : el("div.media-audio-art", { attrs: { "aria-hidden": "true" } }, Sw.Icon("IconMediaPreviewVolLoud"));
        const chrome = el("div.media-chrome", null, art, skipE, bigPlay.E, top, panel, bar);
        view.append(chrome);
        if (!player0.CanPlay) {
            for (const c of [playB, stopB, loopB, bigPlay])
                c.E.hidden = true;
            timeS.E.hidden = true;
        }
        if (panel)
            setStats(Sw.Storage.Get(StatsKey) === "true");

        // ---- auto hide

        let hideTimer = 0;
        const parts = [top, bar, panel].filter(x => x);
        /** @returns {boolean} True when the controls must stay (pointer on them, a focused control, a drag). */
        const mustStay = () => parts.some(x => x.matches(":hover")) || timeS.Dragging || volS.Dragging || !!chrome.querySelector(":focus-visible")
            || moreB.E.getAttribute("aria-expanded") === "true";
        function hide() {
            clearTimeout(hideTimer);
            if (mustStay()) {
                hideTimer = setTimeout(hide, ChromeHideMs);
                return;
            }
            view.classList.remove("MediaChromeOn");
        }
        function show() {
            view.classList.add("MediaChromeOn");
            clearTimeout(hideTimer);
            hideTimer = setTimeout(hide, ChromeHideMs);
        }
        let lastTouch = 0;
        view.addEventListener("pointermove", ev => {
            if (ev.pointerType === "mouse")
                show();
        });
        view.addEventListener("pointerleave", ev => {
            if (ev.pointerType === "mouse" && !timeS.Dragging && !volS.Dragging) {
                clearTimeout(hideTimer);
                view.classList.remove("MediaChromeOn");
            }
        });
        view.addEventListener("pointerdown", ev => {
            if (ev.pointerType !== "mouse")
                lastTouch = Date.now();
        });
        view.addEventListener("focusin", () => show());

        //  The media itself: a click plays / pauses (a tap shows / hides the controls); a double click / tap on the left or right third
        //  jumps 15 s back / forward (more taps in a row jump again), in the middle it toggles full screen
        const onMedia = ev => !ev.target.closest(".media-chrome-part, .media-stats-panel, .media-bigplay");
        /** @returns {number} -1 = the left third, 1 = the right third, 0 = the middle. */
        const sideOf = ev => {
            const r = view.getBoundingClientRect();
            const x = (ev.clientX - r.left) / Math.max(1, r.width);
            return x < 1 / 3 ? -1 : x > 2 / 3 ? 1 : 0;
        };
        let clickTimer = 0;
        let lastClick = 0;
        let lastSide = 0;
        let skipping = 0;
        view.addEventListener("click", ev => {
            if (!onMedia(ev) || Sw.BadClick(ev))
                return;
            const now = Date.now();
            const side = sideOf(ev);
            const touch = now - lastTouch < 800;
            //  A tap / click right after a jump on the same side jumps again (YouTube like)
            if (skipping && side === lastSide && now - skipping < 700) {
                clearTimeout(clickTimer);
                skipping = now;
                lastClick = 0;
                skip(side * 15);
                return;
            }
            if (now - lastClick < 300 && side === lastSide) {
                //  A double click / tap
                clearTimeout(clickTimer);
                lastClick = 0;
                if (side !== 0) {
                    skipping = now;
                    skip(side * 15);
                }
                else
                    toggleFull();
                return;
            }
            lastClick = now;
            lastSide = side;
            skipping = 0;
            //  Wait a moment: the first click of a double click must not act
            clearTimeout(clickTimer);
            clickTimer = setTimeout(() => {
                if (touch) {
                    if (view.classList.contains("MediaChromeOn"))
                        view.classList.remove("MediaChromeOn");
                    else
                        show();
                    return;
                }
                togglePlay();
                show();
            }, 300);
        });

        //  The context menu of the media: the ⋮ menu
        view.addEventListener("contextmenu", ev => {
            if (!onMedia(ev) || !Sw.Menu)
                return;
            ev.preventDefault();
            Sw.Menu.Show({ X: ev.clientX, Y: ev.clientY }, menuItems(), { Label: _TF("Media player", "Accessible name of the media player area of the media viewer") });
        });

        // ---- an address dropped or pasted on the page: show it

        /**
         * Show a dropped / pasted text when it is a media link, else say why not.
         * @param {string} text The text.
         */
        const takeLink = text => {
            const m = ParseMediaLink(text);
            if (m)
                o.Load(m);
            else
                Sw.Toast(_TF("Not a media address: use a YouTube link or the address of a file of a known type (an image, a video, an audio file, .glsl).", "Error of the media viewer dialog that asks for a media address"), { Kind: "warning" });
        };
        const dropText = dt => dt ? (dt.getData("text/uri-list") || dt.getData("text/plain") || "") : "";
        view.addEventListener("dragover", ev => {
            const types = ev.dataTransfer ? [...ev.dataTransfer.types] : [];
            if (!types.includes("text/uri-list") && !types.includes("text/plain"))
                return;
            ev.preventDefault();
            ev.dataTransfer.dropEffect = "link";
            view.classList.add("MediaDropping");
        });
        view.addEventListener("dragleave", ev => {
            if (!view.contains(ev.relatedTarget))
                view.classList.remove("MediaDropping");
        });
        view.addEventListener("drop", ev => {
            view.classList.remove("MediaDropping");
            const text = dropText(ev.dataTransfer);
            if (!text)
                return;
            ev.preventDefault();
            takeLink(text);
        });
        document.addEventListener("paste", ev => {
            const a = document.activeElement;
            if (a && (a.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(a.tagName)))
                return;
            const text = ev.clipboardData ? ev.clipboardData.getData("text/plain") : "";
            if (!text)
                return;
            ev.preventDefault();
            takeLink(text);
        });

        // ---- keys

        view.addEventListener("keydown", ev => {
            if (ev.ctrlKey || ev.altKey || ev.metaKey)
                return;
            const t = ev.target;
            const onSlider = t.closest && t.closest(".sw-slider");
            const onButton = t.closest && t.closest("button, [role=button]");
            const p = getPlayer();
            if (!p)
                return;
            const k = ev.key.length === 1 ? ev.key.toLowerCase() : ev.key;
            let done = true;
            switch (k) {
                case " ":
                case "k":
                    if (onButton && k === " ")
                        return;
                    togglePlay();
                    break;
                case "ArrowLeft":
                case "ArrowRight":
                    if (onSlider)
                        return;
                    seek(p.GetPos() + (k === "ArrowLeft" ? -5 : 5));
                    break;
                case "ArrowUp":
                case "ArrowDown":
                    if (onSlider || !p.HaveAudio)
                        return;
                    setVolume((p.Muted ? 0 : p.Volume) + (k === "ArrowUp" ? 0.1 : -0.1));
                    break;
                case "Home":
                    if (onSlider)
                        return;
                    seek((p.Params && p.Params.StartAt) || 0);
                    break;
                case "j":
                case "l":
                    skip(k === "j" ? -15 : 15);
                    break;
                case "u":
                    askLink();
                    break;
                case "m":
                    toggleMute();
                    break;
                case "f":
                    toggleFull();
                    break;
                case "i":
                    if (panel)
                        setStats(panel.hidden);
                    break;
                default:
                    done = false;
            }
            if (done) {
                ev.preventDefault();
                show();
            }
        });

        // ---- state (every frame while attached)

        const last = {};
        /**
         * Set a value once (skips the DOM work when unchanged).
         * @param {string} k Key.
         * @param {*} v Value.
         * @param {function(*):void} fn Applies it.
         */
        const put = (k, v, fn) => {
            if (last[k] === v)
                return;
            last[k] = v;
            fn(v);
        };
        function update() {
            if (!view.isConnected)
                return;
            const p = getPlayer();
            if (p) {
                const paused = !!p.Paused;
                put("paused", paused, v => {
                    view.classList.toggle("MediaPaused", v);
                    playB.SetIcon(v ? "play" : "pause");
                    playB.SetTitle(v ? _TF("Play", "Tool tip of the play button of the media viewer") : _TF("Pause", "Tool tip of the pause button of the media viewer"));
                    if (!v)
                        show();
                });
                put("loop", !!p.Looping, v => loopB.SetPressed(v));
                //  A rebuilt player (an editor change) gets the chosen speed again
                if (rate !== 1 && typeof p.SetRate === "function" && p.SetRateFor !== rate) {
                    p.SetRateFor = rate;
                    p.SetRate(rate);
                }
                const pos = p.GetPos() || 0;
                const dur = p.Duration;
                const open = !(dur > 0) || !isFinite(dur);
                const len = open ? OpenEndedLength(pos) : dur;
                put("len", len, v => timeS.SetRange(0, v, 0.01));
                if (!timeS.Dragging)
                    timeS.Set(Math.min(len, pos), true);
                const shown = timeS.Dragging ? timeS.Get() : pos;
                put("time", FormatTime(shown) + " / " + (open ? "∞" : FormatTime(dur)), v => timeE.textContent = v);
                put("open", open, v => timeE.title = v ? _TF("The media has no end (∞): the time line grows while it plays", "Tool tip of the time of the media viewer for a media without an end, ex: an effect") : "");
                if (p.HaveAudio) {
                    const vol = p.Muted ? 0 : p.Volume;
                    put("vol", vol, v => {
                        if (!volS.Dragging)
                            volS.Set(v, true);
                        muteB.SetIcon(v <= 0 ? "IconMediaPreviewMute" : v < 0.5 ? "IconMediaPreviewVolLow" : "IconMediaPreviewVolLoud");
                        muteB.SetTitle(p.Muted ? _TF("Un mute", "Tool tip of the mute button of the media viewer while muted") : _TF("Mute", "Tool tip of the mute button of the media viewer"));
                    });
                }
                put("full", Sw.FullScreen.Is(), v => {
                    fullB.SetIcon(v ? "fullscreenExit" : "fullscreen");
                    fullB.SetTitle(v ? _TF("Leave full screen", "Tool tip of the full screen button of the media viewer in full screen") : _TF("Full screen", "Tool tip of the full screen button of the media viewer"));
                });
                put("title", document.title, v => title.textContent = v);
            }
            requestAnimationFrame(update);
        }
        update();
        show();
    }

    // ------------------------------------------------------------------ MediaPreview.html

    /**
     * Debug stand-in for the native `ScreenShotHost` host object (CefSharp / WebView2), used by PreviewMain with the
     * `hack` url parameter when no real host exists; every method only logs its call.
     */
    class SsHack {
        /** Logs "isSupported()". */
        isSupported() {
            console.log("isSupported()");
        }

        /**
         * Logs the requested window size (a real host resizes the window).
         * @param {number} width - Requested width in px.
         * @param {number} height - Requested height in px.
         */
        setSize(width, height) {
            console.log("setSize(" + width + ", " + height + ")");
        }

        /**
         * Logs the final report (a real host takes the screenshot / stores the measurements).
         * @param {number} duration - Media duration in seconds.
         * @param {number} fps - Measured frames per second (0 if unknown).
         * @param {?string} error - Error message, or null on success.
         * @param {number} loadDuration - Compilation/load time in ms.
         */
        doIt(duration, fps, error, loadDuration) {
            console.log("doIt(" + duration + ", " + fps + ", " + error + ", " + loadDuration + ")");
        }
    }

    /**
     * Find the screenshot host: `window.ScreenShotHost` (CefSharp, after BindObjectAsync), the WebView2 host object, or
     * (url parameter `hack`) an SsHack.
     * @returns {Promise<object|null>} The host or null.
     */
    async function GetScreenShotHost() {
        if (window.CefSharp)
            await CefSharp.BindObjectAsync();
        let h = window.ScreenShotHost ?? null;
        if (!h) {
            try {
                h = window.chrome?.webview?.hostObjects?.ScreenShotHost ?? null;
            }
            catch {
                h = null;
            }
        }
        if (!h && Sw.Url.Has("hack"))
            h = new SsHack();
        return h;
    }

    /**
     * Run an action and wait (max timeout ms) for the window resize it causes.
     * @param {function():*} fn The action (ex: the host's setSize, which may block until the host resized the window).
     * @param {number} timeout Max wait in ms.
     * @returns {Promise<void>}
     */
    async function WaitResize(fn, timeout) {
        await Sw.Util.WaitEvent(window, "resize", timeout, () => {
            try {
                const r = fn();
                if (r && r.catch)
                    r.catch(e => console.warn(e));
            }
            catch (e) {
                console.warn(e);
            }
        });
    }

    /**
     * Boot function of `MediaPreview.html`: a bare player that fills the window, also used to take screenshots.
     * URL params: `link`/`d`, `type`/`t`, `props`, `pos` (seek position, s), `fill`, `full` (full screen; re-entered on the
     * first key / click), `back` (a click goes back, held off until the full screen / audio gestures were consumed),
     * `hack` (logging screenshot host; it never removes the caption), `caption` (a text media gets the "Text from the link" caption and the limited
     * area, as in the shell's content frame; set by the effect pages). With a screenshot host (contract with Thumbnail.Web / MediaThumbnail): `isSupported()`
     * first, fill mode, class `NoFade` (checkerboard, no transitions), `setSize(w, h)` to the clipped media size (or the
     * aspect with AdaptiveSize), play, measure 600 ms, pause, seek back to pos and `doIt(duration, fps, null, compileTime)`;
     * on a playback error `doIt(0, 0, message, 0)` once.
     * @returns {Promise<void>}
     */
    async function PreviewMain() {
        const target = document.body;
        try {
            await playerReady;
            const U = Sw.Url;
            target.classList.add("MediaPreview");
            const shost = await GetScreenShotHost();
            if (shost) {
                await shost.isSupported();
                window.ScreenShotHost = shost;
                target.classList.add("NoFade");
            }
            let fill = U.Has("fill") || !!shost;
            const full = U.Has("full");
            const back = U.Has("back");
            //  A missing link or an unknown type: the reason alone (as the original's Fail, not "Generic failure."), shown as the
            //  error state of MediaView; a screenshot host gets the failure
            let mp;
            try {
                mp = ReadMediaParams();
            }
            catch (pe) {
                const reason = FailReason(pe);
                if (shost)
                    await shost.doIt(0, 0, reason, 0);
                ShowLoadError(target, reason);
                Sw.Page.Loaded();
                return;
            }
            const problem = MediaLinkProblem(mp);
            if (problem) {
                if (shost)
                    await shost.doIt(0, 0, problem.Text, 0);
                ShowLinkProblem(target, problem);
                Sw.Page.Loaded();
                return;
            }
            const pos = parseFloat(U.Get("pos") ?? "0") || 0;
            //  A text from the url inside the app shell: captioned and limited to an area in the middle (MediaCaptioned).
            //  Only a real screenshot host (headless capture, never inside the app) skips it: the caption depends only on the
            //  text coming from the url, never on other url parameters (the debug `hack` host or unknown ones can't drop it)
            const realHost = !!shost && !(shost instanceof SsHack);
            const caption = mp.Type === MediaTypes.Text && !realHost ? AddTextCaption(target, U.Has("caption")) : null;
            if (caption) {
                target.classList.add("MediaCaptioned");
                //  The captioned text is fitted into its limited area (no fill, also with `fill` or the debug `hack` host)
                fill = false;
                if (mp.Props && mp.Props.CollageSeparator)
                    target.classList.add("MediaCollage");
            }

            let enabledFull = true;
            let enabledAudio = true;
            /**
             * Add a one shot handler for the first key up / click on the page.
             * @param {function():*} fn The handler.
             */
            const onFirstGesture = fn => {
                const h = () => {
                    target.removeEventListener("keyup", h);
                    target.removeEventListener("click", h);
                    fn();
                };
                target.addEventListener("keyup", h);
                target.addEventListener("click", h);
            };
            if (full) {
                await Sw.FullScreen.Enter();
                enabledFull = Sw.FullScreen.IsFull();
                //  Entering full screen may need a user gesture: try again on the first one
                onFirstGesture(async () => {
                    await Sw.FullScreen.Enter();
                    setTimeout(() => enabledFull = true, 100);
                });
            }
            if (back) {
                /** Go back (click, Escape); not while the first gesture is still needed for full screen / audio. */
                const goBack = () => {
                    if (enabledAudio && enabledFull)
                        history.go(-1);
                };
                target.addEventListener("click", goBack);
                target.addEventListener("keydown", ev => {
                    if (ev.key === "Escape" || ev.key === "Backspace")
                        goBack();
                });
                target.title = _TF("Go back", "Tool tip of the full screen media preview");
            }

            //  A web font that failed: a default font is used and a small notice stays (the page never goes blank); a real
            //  screenshot host still gets the failure (as before)
            let fontError = null;
            FontNotice(target, err => fontError = fontError ?? err);
            await playerReady;
            const player = await MediaPlayer.Create(mp.Type, mp.Data, mp.Props);
            if (!player)
                throw new Error(_T("No supported player found for type {0}", mp.Type, "Error when no media player exists for a media type. {0} is the type number"));
            const e = player.Element;
            e.draggable = false;
            target.appendChild(e);
            let failed = false;
            try {
                if (!(await player.Cache(true)))
                    throw new Error(_TF("Failed to cache the media", "Error when a media file could not be loaded"));
                if (fontError && realHost)
                    throw fontError;
                const adapt = !!player.Params.AdaptiveSize;
                /** @returns {Array<number>} The clipped media size [w, h]. */
                const updateSize = () => MediaPlayerTools.UpdateSize(target, player, fill);
                new ResizeObserver(updateSize).observe(target);
                if (pos > 0)
                    await player.Seek(pos);
                if (player.HaveAudio) {
                    if (await MediaPlayerTools.CanUnMute())
                        await player.UnMute();
                    else {
                        enabledAudio = false;
                        onFirstGesture(() => {
                            player.UnMute();
                            setTimeout(() => enabledAudio = true, 100);
                        });
                    }
                }
                await MediaPlayerTools.delay(50);
                const clippedSize = updateSize();
                if (shost) {
                    if (adapt) {
                        const ar = player.AspectRatio;
                        if (ar) {
                            const aw = (window.innerHeight * ar) | 0;
                            if (window.innerWidth !== aw) {
                                await WaitResize(() => shost.setSize(aw, window.innerHeight), 1000);
                                await MediaPlayerTools.delay(200);
                            }
                        }
                    } else if ((window.innerWidth !== clippedSize[0]) || (window.innerHeight !== clippedSize[1])) {
                        await WaitResize(() => shost.setSize(clippedSize[0], clippedSize[1]), 1000);
                        await MediaPlayerTools.delay(200);
                    }
                }
                await player.Play();
                await player.Show();
                if (caption)
                    TrackTextCaption(caption, () => e, [target]);
                MediaPlayerTools.SetupMediaMessages(player);
            }
            catch (ex) {
                failed = true;
                if (shost)
                    await shost.doIt(0, 0, "" + ex, 0);
                //  A persistent error state with the reason and "Try again" (was only a toast over an empty page)
                if (realHost)
                    Sw.Fail(FailReason(ex));
                else {
                    target.classList.remove("MediaCaptioned", "MediaCollage");
                    ShowLoadError(target, FailReason(ex));
                }
            }
            if (shost && !failed) {
                await MediaPlayerTools.delay(200);
                if (typeof player.ResetCounters !== "undefined") {
                    player.ResetCounters();
                    await MediaPlayerTools.delay(600);
                }
                const tot = player.TotalTime ?? 0;
                const count = player.MeasureCount ?? 0;
                await player.Pause();
                await MediaPlayerTools.delay(200);
                await player.Seek(pos);
                await MediaPlayerTools.delay(200);
                const fps = tot > 0 ? (1000.0 * count / tot) : 0;
                await shost.doIt(player.Duration ?? 0, fps, null, player.CompilationTime ?? 0);
            }
        }
        catch (e) {
            target.textContent = _TF("Generic failure.", "Error shown when a media page failed to start") + "\n" + (e && e.message ? e.message : e);
            const h = window.ScreenShotHost;
            if (h)
                try {
                    await h.doIt(0, 0, "" + e, 0);
                }
                catch {
                }
        }
        Sw.Page.Loaded();
    }

    // ------------------------------------------------------------------ exports

    /** The new API of this file. */
    Sw.MediaView = {
        Prefs,
        GetTypeFromUrl,
        CreatePlayer,
        PlayWithPopup,
        RebuildPlayer,
        EditProps,
        EditMain,
        ViewMain,
        PreviewMain,
        SsHack,
        Ready: playerReady,
    };

    //  Legacy global names (external host pages)
    window.mediaCreatePlayer = CreatePlayer;
    window.mediaPlayWithPopup = PlayWithPopup;
    window.mediaEditProps = EditProps;
    window.mediaEditMain = EditMain;
    window.mediaGetTypeFromUrl = GetTypeFromUrl;
    window.mediaViewMain = ViewMain;
    window.mediaPreviewMain = PreviewMain;
    window.SsHack = SsHack;
})();
