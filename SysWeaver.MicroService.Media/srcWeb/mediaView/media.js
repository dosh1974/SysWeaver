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
        /** @type {string} Storage key: the stage behind a media (a Backgrounds value). */
        BackgroundKey: "SysWeaver.Media.Background",
        /** @type {string} The default stage: the page background of the theme (also behind transparent media). */
        DefaultBackground: "theme",
        /** @returns {string} The chosen stage background (default: the page background of the theme). */
        Background: () => {
            const v = Sw.Storage.Get(Prefs.BackgroundKey) ?? "";
            return Backgrounds.some(x => x.Value === v) ? v : Prefs.DefaultBackground;
        },
        /** @param {string} v The stage background. */
        SetBackground: v => Sw.Storage.Set(Prefs.BackgroundKey, v || Prefs.DefaultBackground),
        /**
         * The effect the viewer's menu chose last for a media type (EffectKind: a sound's visualization, an image's effect), used for a
         * media of that type whose settings name no effect.
         * @param {number} type The media type.
         * @returns {?string} The effect ("audioEffects/X.glsl", "" = none), null: none chosen (a sound: the default effect).
         */
        Effect: type => {
            const k = EffectKind(type);
            const v = k ? Sw.Storage.Get(k.Key) : null;
            //  (only a built in effect of the kind, or none)
            return typeof v === "string" && (v === "" || (v.startsWith(k.Folder) && /^[\w\-. ]+\.glsl$/i.test(v.substring(k.Folder.length)))) ? v : null;
        },
        /**
         * Remember the effect chosen for a media type.
         * @param {number} type The media type.
         * @param {?string} v The effect ("" = none, null = forget).
         */
        SetEffect: (type, v) => {
            const k = EffectKind(type);
            if (!k)
                return;
            if (v === null || v === undefined)
                Sw.Storage.Remove(k.Key);
            else
                Sw.Storage.Set(k.Key, v);
        },
    };

    /**
     * The params of a media with the effect the viewer chose (its effects menu, the remembered choice): an image's or a video's effect gets the
     * size of the view, not the picture's (EffectSizeFromImage = false: an effect is drawn at the player's size, like an effect of its own). The given
     * params are not changed.
     * @param {number} type The media type.
     * @param {?object} props The media params.
     * @param {string} fx The effect ("" = none).
     * @returns {object} New params.
     */
    function WithViewerEffect(type, props, fx) {
        const np = Object.assign({}, props || {}, { Effect: fx });
        if (fx && (type === MediaTypes.Image || type === MediaTypes.Video))
            np.EffectSizeFromImage = false;
        return np;
    }

    /**
     * The effects the viewer's menu picks for a media type (CreateChrome option Effects): the list of the built in effects (Api), their
     * folder (relative to this script), the storage key of the last choice (Prefs.Effect), the effect when the settings name none and no
     * choice was made (a sound: DefaultAudioEffect), and the name of the menu item.
     * @param {number} type The media type.
     * @returns {?{Api:string, Folder:string, Key:string, Default:string, Text:function():string}} The kind, null: no effect choice (a video ...).
     */
    function EffectKind(type) {
        if (type === MediaTypes.Audio)
            return {
                Api: "Api/GetAudioEffects", Folder: "audioEffects/", Key: "SysWeaver.Media.AudioEffect", Default: DefaultAudioEffect,
                Text: () => _TF("Visualization", "Menu item of the media viewer with the effects that visualize a sound (a sub menu: none and the built in audio effects)"),
            };
        if (type === MediaTypes.Image)
            return {
                Api: "Api/GetImageEffects", Folder: "imageEffects/", Key: "SysWeaver.Media.ImageEffect", Default: "",
                Text: () => _TF("Effects", "Menu item of the media viewer with the effects of an image (a sub menu: none and the built in image effects)"),
            };
        //  (a video with an effect: the effect draws its frames, like a picture: the image effects)
        if (type === MediaTypes.Video)
            return {
                Api: "Api/GetImageEffects", Folder: "imageEffects/", Key: "SysWeaver.Media.VideoEffect", Default: "",
                Text: () => _TF("Effects", "Menu item of the media viewer with the effects of a video (a sub menu: none and the built in image effects)"),
            };
        return null;
    }

    /**
     * The stages behind a media (the player chrome's background menu: what shows through the transparent parts of an effect, an image
     * or a video, and around a media that doesn't fill the view). The view gets `data-media-bg` (media.css). The default is the page
     * background of the theme (Prefs.DefaultBackground).
     * @type {Array<{Value:string, Label:function():string}>}
     */
    const Backgrounds = [
        { Value: "theme", Label: () => _TF("Page background", "Menu item of the background menu of the media player: the background color of the page theme is shown behind the media") },
        { Value: "checker", Label: () => _TF("Checkerboard", "Menu item of the background menu of the media player: a checkerboard pattern is shown behind the transparent parts of the media") },
        { Value: "white", Label: () => _TF("White", "Menu item of the background menu of the media player: this color is shown behind the media. A color name.") },
        { Value: "black", Label: () => _TF("Black", "Menu item of the background menu of the media player: this color is shown behind the media. A color name.") },
        { Value: "gray", Label: () => _TF("Gray", "Menu item of the background menu of the media player: this color is shown behind the media. A color name.") },
        { Value: "magenta", Label: () => _TF("Magenta", "Menu item of the background menu of the media player: this color is shown behind the media. A color name.") },
    ];

    // ------------------------------------------------------------------ helpers

    /**
     * The effect a sound plays with in the viewer and the editor when its settings name none (its Effect is null or missing); an
     * empty Effect ("") plays the sound without an effect, any other value is the effect to use. Relative to this script's folder.
     * Change it here to use another effect, or set it to "" for no default effect.
     * @type {string}
     */
    const DefaultAudioEffect = "audioEffects/Corridor.glsl";

    /** @type {string} The folder of this script (DefaultAudioEffect is relative to it, the pages using it can be elsewhere). */
    const ScriptBase = document.currentScript ? document.currentScript.src : document.baseURI;

    /**
     * The params a player is made with: a sound whose Effect is null or missing gets the default effect (DefaultAudioEffect), an
     * Effect of "" means none (the audio player). The given props are not changed.
     * @param {number} type The media type.
     * @param {?object} props The media params.
     * @returns {?object} The params for MediaPlayer.Create.
     */
    function PlayerParams(type, props) {
        if (type !== MediaTypes.Audio)
            return props;
        const fx = props ? props.Effect : null;
        if (fx === "")
            return Object.assign({}, props, { Effect: null });
        if ((fx !== null && fx !== undefined) || !DefaultAudioEffect)
            return props;
        return Object.assign({}, props || {}, { Effect: new URL(DefaultAudioEffect, ScriptBase).href });
    }

    /**
     * The params of the sound of a player (Volume, StartAt, EndAt): its own, or, for an effect that plays a sound (an audio with an
     * effect, an effect with a sound texture), the sound's (the effect's Params are the effect settings).
     * @param {?object} player - The player.
     * @returns {?object} The params, null without a player.
     */
    function SoundParams(player) {
        if (!player)
            return null;
        return player.Media ? player.Media.Params : player.Params;
    }

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
     *  - a http(s) url of this site (Sw.Url.SafeSameOrigin), or a blob: url created by a page of this site, is always shown;
     *  - any other scheme ("javascript:", "data:", a blob: url of another origin, ...) is refused;
     *  - a url of another site is refused inside the app shell (a same-origin top window with SysWeaverApp), where it would
     *    look like content of this site; it is shown in a window of its own (a new tab, the editor's "New window", the
     *    screenshot host) or when framed by another host.
     * YouTube (a video code) and text (the text itself) are not urls and are not checked.
     * The same rule applies to the url-like members of the `props` parameter: `Effect` (the effect of an image / text / sound),
     * `FontUrl` (the font of a text) and `Texture` (the image or sound of an effect); for them "Open in a new tab" opens this page in a new tab.
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
        for (const name of ["Effect", "FontUrl", "Texture"]) {
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
        //  A blob: url created by a page of this site (data handed over by a page of this site, ex: an api result shown by explore/api.html);
        //  only a script of the same origin can create one, so it can't point to content of another site
        try {
            const b = new URL(url, window.location.href);
            if ((b.protocol === "blob:") && (b.origin === window.location.origin))
                return null;
        }
        catch {
        }
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
     * The error of a YouTube video that did not play, from YouTube's error code (the player's ErrorCode): why, and (except for a bad
     * code) `Open` = "Watch on YouTube" for ShowLoadError. YouTube codes: 2 bad video code, 5 html5 player, 100 removed or private,
     * 101 / 150 the owner allows no playing on other sites, 153 the request had no referrer.
     * @param {number|undefined} code The error code (undefined: no onError, ex: a time out).
     * @param {string} id The video code.
     * @returns {Error} The error.
     */
    function YouTubeError(code, id) {
        let text;
        switch (code) {
            case 101:
            case 150:
                text = _TF("The owner of this YouTube video does not allow it to be played on other sites: it can only be watched on YouTube.", "Reason on the media viewer error state when a YouTube video may not be embedded (YouTube error 101 / 150)");
                break;
            case 100:
                text = _TF("This YouTube video was removed or is private.", "Reason on the media viewer error state for YouTube error 100");
                break;
            case 2:
                text = _TF("This is not a valid YouTube video code.", "Reason on the media viewer error state for YouTube error 2");
                break;
            case 5:
                text = _TF("The YouTube player can't play this video in this browser.", "Reason on the media viewer error state for YouTube error 5");
                break;
            case 153:
                text = _TF("YouTube refused to play the video: the page sent no address (referrer). The site's Referrer-Policy must allow it.", "Reason on the media viewer error state for YouTube error 153");
                break;
            default:
                text = _TF("YouTube could not play this video: it may be private, removed or not allowed on other sites.", "Reason on the media viewer error state when a YouTube video failed without a known reason");
        }
        const e = new Error(text);
        e.YouTubeCode = code;
        if (code !== 2)
            e.Open = { Url: "https://www.youtube.com/watch?v=" + encodeURIComponent(id), Text: _TF("Watch on YouTube", "Button on the media viewer error state of a YouTube video, opens it on youtube.com in a new tab") };
        return e;
    }

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
     * announced to screen readers. Replaces the content of target. An error with `Open: {Url, Text}` (ex: a YouTube video that
     * may only be watched on YouTube) adds a button that opens that address in a new tab.
     * @param {HTMLElement} target The element to show it in.
     * @param {string|Error} reason The reason (text only), or the error (FailReason gives its text).
     */
    function ShowLoadError(target, reason) {
        const open = reason && typeof reason === "object" ? reason.Open : null;
        if (typeof reason !== "string")
            reason = FailReason(reason);
        console.warn("Media: failed to show the media: " + reason);
        target.textContent = "";
        target.classList.remove("MediaWithCaption");
        const text = _TF("This media can't be shown.", "Title of the media viewer error state when the media failed to load or play");
        target.append(Sw.el("div.MediaRefused.MediaFailed", { attrs: { role: "alert" } },
            Sw.el("p.MediaRefusedText", null, Sw.el("strong", { text: text })),
            Sw.el("p.MediaRefusedText", { text: reason }),
            open ? new Sw.Button({
                Text: open.Text, Icon: "external", Kind: "primary",
                OnClick: () => window.open(open.Url, "_blank", "noopener,noreferrer"),
            }).E : null,
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
     * The MIME types of the file extensions the viewer plays (the type a server would send; the real one comes from the resource timing
     * when the browser has it, see CreateStats).
     * @type {Object<string, string>}
     */
    const MimeOfExt = {
        png: "image/png", apng: "image/apng", jpg: "image/jpeg", jpeg: "image/jpeg", jfif: "image/jpeg", gif: "image/gif", webp: "image/webp",
        avif: "image/avif", svg: "image/svg+xml", bmp: "image/bmp", ico: "image/x-icon", cur: "image/x-icon", tif: "image/tiff", tiff: "image/tiff",
        jxl: "image/jxl", heic: "image/heic", heif: "image/heif", psd: "image/vnd.adobe.photoshop",
        mp4: "video/mp4", m4v: "video/mp4", webm: "video/webm", mkv: "video/x-matroska", mov: "video/quicktime", ogv: "video/ogg",
        avi: "video/x-msvideo", wmv: "video/x-ms-wmv", "3gp": "video/3gpp", ts: "video/mp2t", ivf: "video/x-ivf",
        mp3: "audio/mpeg", wav: "audio/wav", ogg: "audio/ogg", oga: "audio/ogg", opus: "audio/opus", flac: "audio/flac", aac: "audio/aac",
        adts: "audio/aac", m4a: "audio/mp4", weba: "audio/webm", mid: "audio/midi", midi: "audio/midi", wma: "audio/x-ms-wma",
        glsl: "text/x-glsl",
    };

    /** @type {?{Renderer:string, Vendor:string, MaxTexture:number}} The GPU of WebGL (read once, see CreateStats). */
    let GpuInfo = null;

    /**
     * The GPU that renders the effects (WebGL's renderer and vendor, the largest texture). Firefox and Safari name it in RENDERER; Chrome
     * only through WEBGL_debug_renderer_info (asked only when RENDERER is the generic "WebKit WebGL": Firefox warns that it is deprecated).
     * @param {WebGLRenderingContext} gl A context.
     * @returns {{Renderer:string, Vendor:string, MaxTexture:number}} The GPU.
     */
    function GetGpuInfo(gl) {
        if (GpuInfo)
            return GpuInfo;
        let r = String(gl.getParameter(gl.RENDERER) || "");
        let v = String(gl.getParameter(gl.VENDOR) || "");
        if (/^WebKit/i.test(r)) {
            const ext = gl.getExtension("WEBGL_debug_renderer_info");
            if (ext) {
                r = String(gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) || r);
                v = String(gl.getParameter(ext.UNMASKED_VENDOR_WEBGL) || v);
            }
        }
        GpuInfo = { Renderer: r, Vendor: v, MaxTexture: gl.getParameter(gl.MAX_TEXTURE_SIZE) || 0 };
        return GpuInfo;
    }

    /**
     * Create the `<Media-Stats>` block of a player: the technical details of the media in groups (the file, the picture, the video, the
     * sound, the effect), each a `<Media-Stat>` row with a name and a value. A click (or Enter / Space) on the name copies the name, on the
     * value the value; the description shows as tooltip, on long press and focus.
     * Only what the browser already has is shown (no extra download, no decoding): the resource timing of the file (size, transfer, MIME
     * type, protocol), the element (natural size, frame counters, buffered time), the decoded sound when the player has one (an effect with
     * the audio2 texture, the offline analysis), the effect's counters. The values are refreshed 4 times a second while the block is shown.
     * @param {object} player The player.
     * @param {object} props The media params.
     * @param {number} [type] The media type (MediaTypes).
     * @param {string} [data] The url (or the text / the YouTube code).
     * @returns {HTMLElement} The `<Media-Stats>` element.
     */
    function CreateStats(player, props, type, data) {
        const root = document.createElement("Media-Stats");
        const copyName = _TF("Select to copy the name.", "Tool tip suffix of the name of a media statistics value");
        const copyValue = _TF("Select to copy the value.", "Tool tip suffix of a media statistics value");

        /**
         * A copy target: the name or the value of a row (click / Enter / Space copies its text).
         * @param {string} tag The element name.
         * @param {string} title The tool tip.
         * @returns {HTMLElement} The element.
         */
        function target(tag, title) {
            const e = document.createElement(tag);
            e.title = title;
            e.tabIndex = 0;
            e.setAttribute("role", "button");
            e.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                ev.stopPropagation();
                Sw.Clipboard.Copy(e.textContent);
            });
            Sw.KeyClick(e);
            if (Sw.Hint)
                Sw.Hint.Attach(e, title);
            return e;
        }

        /**
         * A part of the statistics panel: a section (a header that collapses it, "Image", "Effect" ...) or a group in a section (a small label).
         * A header without a shown row is hidden. The chips under the media show neither.
         * @typedef {{G:HTMLElement, Rows:HTMLElement[]}} StatPart
         */
        /** @type {?StatPart} The section the next rows belong to. */
        let sectionP = null;
        /** @type {?StatPart} The group (in a section of mixed media) the next rows belong to. */
        let groupP = null;
        /** @type {StatPart[]} The sections and groups. */
        const parts = [];
        /** @type {Set<string>} The ids of the collapsed sections (remembered: StatsFoldedKey). */
        const folded = (() => {
            try {
                const a = JSON.parse(Sw.Storage.Get(StatsFoldedKey) || "[]");
                return new Set(Array.isArray(a) ? a.filter(x => typeof x === "string") : []);
            }
            catch {
                return new Set();
            }
        })();

        /**
         * Start a section (a header that collapses / expands its rows, remembered by its id).
         * @param {string} id The id of the section (the same in every language: "Image", "Effect" ...).
         * @param {string} name The header.
         */
        function section(id, name) {
            const h = document.createElement("Media-StatSection");
            h.textContent = name;
            h.tabIndex = 0;
            h.setAttribute("role", "button");
            const p = { G: h, Rows: [], Id: id };
            parts.push(p);
            sectionP = p;
            groupP = null;
            const apply = () => {
                const on = folded.has(id);
                h.classList.toggle("Folded", on);
                h.setAttribute("aria-expanded", String(!on));
                h.title = on ? _TF("Show this part of the statistics", "Tool tip of a collapsed section header of the media statistics") : _TF("Hide this part of the statistics", "Tool tip of an expanded section header of the media statistics");
                for (const r of p.Rows)
                    r.classList.toggle("Fold", on);
            };
            p.Apply = apply;
            h.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                ev.stopPropagation();
                if (folded.has(id))
                    folded.delete(id);
                else
                    folded.add(id);
                Sw.Storage.Set(StatsFoldedKey, JSON.stringify([...folded]));
                apply();
            });
            Sw.KeyClick(h);
            root.appendChild(h);
        }

        /**
         * Start a group of rows: in a section of mixed media (an image / a sound with an effect) a label in the section, else a section of its own.
         * @param {string} id The id (the same in every language).
         * @param {string} name The name of the group.
         */
        function group(id, name) {
            if (!mixed) {
                section(id, name);
                return;
            }
            const g = document.createElement("Media-StatGroup");
            g.textContent = name;
            root.appendChild(g);
            groupP = { G: g, Rows: [] };
            parts.push(groupP);
            sectionP.Rows.push(g);
        }

        /** @type {Array<function():void>} The updates of the rows. */
        const ups = [];

        /**
         * Add a row.
         * @param {string} name Display name.
         * @param {string} title Description.
         * @param {string} [cls] Category class: "Adjust" (trim), "Clip" (crop) or "Actual" (render size).
         * @param {function():(Array|null)} get Returns [text, value] or null to hide the row.
         */
        function add(name, title, cls, get) {
            const statE = document.createElement("Media-Stat");
            if (cls)
                statE.classList.add(cls);
            const nameE = target("Media-StatName", title + "\n\n" + copyName);
            nameE.textContent = name;
            const val = target("Media-StatValue", title + "\n\n" + copyValue);
            val.textContent = "-";
            statE.append(nameE, val);
            root.appendChild(statE);
            if (sectionP)
                sectionP.Rows.push(statE);
            if (groupP)
                groupP.Rows.push(statE);
            let text = "-";
            /** @type {boolean} The row had a value once: without one now it stays, dimmed with "-" (Idle), so the rows below don't jump. */
            let had = false;
            ups.push(() => {
                let r = null;
                try {
                    r = get();
                }
                catch {
                    r = null;
                }
                if (!r || r[0] === null || r[0] === undefined || r[0] === "") {
                    //  (a value that was never there: no row; one that went away, ex: the frame time while paused: "-", dimmed)
                    if (!had) {
                        statE.classList.add("Hide");
                        return;
                    }
                    statE.classList.add("Idle");
                    if (text !== "-") {
                        text = "-";
                        val.textContent = text;
                    }
                    return;
                }
                had = true;
                statE.classList.remove("Hide", "Idle");
                const tx = String(r[0]);
                if (tx === text)
                    return;
                text = tx;
                val.textContent = tx;
            });
        }

        const n = (v, d) => Sw.Format.Number(v, d);
        /** @param {number} v Seconds. @returns {Array} [text, value]. */
        const secs = v => [_T("{0} s", n(v, 2), "A media statistics value in seconds (s is the unit), {0} is the number"), v];
        /** @param {number} v Milliseconds. @returns {Array} [text, value]. */
        const msv = (v, d) => [_T("{0} ms", n(v, d ?? 1), "A media statistics value in milliseconds (ms is the unit), {0} is the number"), v];
        /** @param {number} v Pixels. @returns {Array} [text, value]. */
        const px = v => [v + " px", v];
        /** @param {number} w Width. @param {number} h Height. @returns {?Array} "w × h" in pixels, null without a size. */
        const size = (w, h) => w > 0 && h > 0 ? [_T("{0} × {1} px", w, h, "A media statistics value: a size in pixels, {0} the width, {1} the height"), w + "x" + h] : null;
        /** @param {number} v Bytes. @returns {?Array} The size ("12.3 KB"). */
        const bytes = v => v > 0 ? [Sw.Format.Bytes(v, 1), v] : null;
        /** @param {number} v Bits per second. @returns {?Array} The rate ("1.2 Mbit/s"). */
        const bitrate = v => {
            if (!(v > 0) || !isFinite(v))
                return null;
            return v >= 1e6 ? [_T("{0} Mbit/s", n(v / 1e6, 2), "A media statistics value: a bit rate in megabits per second"), v]
                : [_T("{0} kbit/s", n(v / 1e3, 0), "A media statistics value: a bit rate in kilobits per second"), v];
        };
        /** @param {boolean} v A switch. @returns {Array} yes / no. */
        const yesNo = v => [v ? _TF("yes", "A media statistics value: a switch that is on") : _TF("no", "A media statistics value: a switch that is off"), !!v];
        /**
         * The aspect ratio of a size: reduced ("16:9") and as a number.
         * @param {number} w Width. @param {number} h Height. @returns {?Array} The ratio, null without a size.
         */
        const aspect = (w, h) => {
            if (!(w > 0 && h > 0))
                return null;
            const gcd = (a, b) => b ? gcd(b, a % b) : a;
            const g = gcd(Math.round(w), Math.round(h));
            const a = w / h;
            const rw = Math.round(w) / g, rh = Math.round(h) / g;
            return [(rw <= 64 && rh <= 64 ? rw + ":" + rh + " · " : "") + n(a, 3), a];
        };

        const visual = player.HaveVisual;
        const fx = typeof player.CompilationTime !== "undefined";
        /** @type {?object} The player of the sound (an effect that plays a sound: its sound's). */
        const soundPlayer = player.Media || (player.HaveAudio && !fx ? player : null);
        /** @type {?HTMLMediaElement} The element of a video or a sound. */
        const mediaE = player.Element instanceof HTMLMediaElement ? player.Element : (soundPlayer && soundPlayer.Element instanceof HTMLMediaElement ? soundPlayer.Element : null);
        const videoE = mediaE instanceof HTMLVideoElement ? mediaE : null;
        const imgE = player.Element instanceof HTMLImageElement ? player.Element : null;
        const isText = type === MediaTypes.Text;
        const isYouTube = type === MediaTypes.YouTube;
        /** @type {?object} The picture texture of an effect (an image with an effect, an effect with a picture), null if none. */
        const texImg = fx && player.Texture && player.Texture.Image instanceof HTMLImageElement ? player.Texture : null;
        /**
         * Mixed media: an image, a sound or a text with an effect (or an effect with a sound / a picture): its own values first in a section
         * "Image" / "Audio" / "Text", then the effect's in a section "Effect". null: one media, a section per group.
         * @type {?string}
         */
        const baseKind = !fx ? null
            : player.Media ? (player.Media.Element instanceof HTMLVideoElement ? "Video" : "Audio")
                : (type === MediaTypes.Image || texImg) ? "Image"
                    : isText ? "Text" : null;
        const mixed = !!baseKind;
        /** @param {?string} u A url. @returns {?string} It absolute, null if none / not a url. */
        const abs = u => {
            try {
                return u ? new URL(u, document.baseURI).href : null;
            }
            catch {
                return null;
            }
        };
        /** @type {?string} The file of the media (absolute url; mixed: the image / the sound, not the effect), null for a text / YouTube. */
        const fileUrl = isYouTube || (isText && !texImg) ? null
            : (baseKind === "Audio" || baseKind === "Video") ? abs(player.Media.Url)
                : baseKind === "Image" ? abs(type === MediaTypes.Image ? data : texImg.Url)
                    : abs(player.Source || player.Url || data);
        /** @type {?string} The effect file of mixed media (absolute url), null otherwise. */
        const fxUrl = mixed && typeof player.Url === "string" ? abs(player.Url) : null;

        /**
         * The resource timing of a file (its size, transfer, MIME type ...): what the browser recorded when it loaded it, no request.
         * Looked up until found (a media element loads in parts), at most 40 times. Cross origin files without Timing-Allow-Origin have
         * no sizes (0).
         * @param {?string} url The file.
         * @returns {function():?PerformanceResourceTiming} Returns the timing, null when the browser has none.
         */
        const timingOf = url => {
            let timing;
            let tries = 0;
            return () => {
                if (timing || !url || /^(blob|data):/i.test(url) || tries > 40)
                    return timing || null;
                ++tries;
                try {
                    const e = performance.getEntriesByName(url, "resource");
                    timing = e.length ? e[e.length - 1] : undefined;
                }
                catch {
                    timing = null;
                }
                return timing || null;
            };
        };
        const getTiming = timingOf(fileUrl);
        const getFxTiming = timingOf(fxUrl);
        /** @returns {number} The size of the file in bytes (as sent: compressed, else decoded), 0 if unknown. */
        const fileSize = () => {
            const t = getTiming();
            return t ? (t.encodedBodySize || t.decodedBodySize || 0) : 0;
        };
        /** @returns {string} The extension of the file (lower case), "" if none. */
        const ext = (() => {
            const m = /\.([a-z0-9]+)$/i.exec((fileUrl || "").split(/[?#]/)[0]);
            return m ? m[1].toLowerCase() : "";
        })();
        /**
         * The size of the picture of the media: an image with an effect: the image (not the effect's canvas), else the player's.
         * @returns {number[]} [width, height] (0 when unknown).
         */
        const pic = () => {
            if (baseKind === "Image") {
                const t = player.Texture;
                const im = t && t.Image;
                return [(im && im.naturalWidth) || (t && t.Width) || 0, (im && im.naturalHeight) || (t && t.Height) || 0];
            }
            if (baseKind === "Video")
                return [player.Media.Element.videoWidth || 0, player.Media.Element.videoHeight || 0];
            //  (an effect: the canvas it draws, it follows the player size, full screen ...)
            if (fx && player.Element instanceof HTMLCanvasElement)
                return [player.Element.width || 0, player.Element.height || 0];
            return [player.Width || 0, player.Height || 0];
        };

        // ---- the media (mixed media: first a section of the image / sound / text itself, then one of the effect)
        if (mixed)
            section(baseKind, baseKind === "Audio" ? _TF("Audio", "Header of the section of the media statistics with the values of the sound of a sound with an effect")
                : baseKind === "Video" ? _TF("Video", "Header of the section of the media statistics with the values of the video of a video with an effect")
                    : baseKind === "Image" ? _TF("Image", "Header of the section of the media statistics with the values of the picture of an image with an effect")
                        : _TF("Text", "Header of the section of the media statistics with the values of the text of a text with an effect"));
        group("Media", _TF("Media", "Header of a group of the media statistics: the play position, duration and volume"));
        add(_TF("Type", "Name of a media statistics value: the media type (Image, Video ...)"), _TF("The type of the media", "Description of a media statistics value"), null,
            () => typeof type === "number" ? [(MediaTypes.Names && MediaTypes.Names[type]) || type, type] : null);
        //  Every player shows its play position and duration, also a still image (its slideshow timeline), as before the rewrite
        add(_TF("Time", "Name of a media statistics value: the current play position"), _TF("The current position in seconds", "Description of a media statistics value"), null,
            () => secs(player.GetPos()));
        add(_TF("Duration", "Name of a media statistics value: the length of the media"), _TF("The duration of the media in seconds, less than zero means unknown or infinity", "Description of a media statistics value"), null,
            () => {
                const v = player.Duration;
                return v < 0 || !isFinite(v) ? ["∞", v] : secs(v);
            });
        if (player.HaveAudio)
            add(_TF("Volume", "Name of a media statistics value: the audio volume"), _TF("The current audio volume of the media as a percentage", "Description of a media statistics value"), null,
                () => {
                    const v = player.GetTrueVolume();
                    return [_T("{0} %", n(v * 100, 1), "A media statistics value as a percentage, {0} is the number"), v];
                });
        if (mediaE)
            add(_TF("Speed", "Name of a media statistics value: the play speed"), _TF("The play speed (1 = normal)", "Description of a media statistics value"), null,
                () => mediaE.playbackRate !== 1 ? [n(mediaE.playbackRate * 100, 0) + " %", mediaE.playbackRate] : null);
        //  (an effect that plays a sound: the play window is the sound's)
        const soundParams = SoundParams(player) || props || {};
        if (typeof soundParams.StartAt !== "undefined") {
            add(_TF("Trimmed time", "Name of a media statistics value: the position inside the trimmed part"), _TF("The current position in the trimmed period in seconds", "Description of a media statistics value"), "Adjust",
                () => {
                    const b = soundParams.StartAt;
                    return b > 0 ? secs(player.GetPos() - b) : null;
                });
            add(_TF("Trimmed duration", "Name of a media statistics value: the length of the trimmed part"), _TF("The duration of the trimmed period in seconds", "Description of a media statistics value"), "Adjust",
                () => {
                    const b = soundParams.StartAt;
                    const e = soundParams.EndAt;
                    if (!((b > 0) || (e > b)))
                        return null;
                    return secs((e > b ? e : player.Duration) - b);
                });
        }

        // ---- the file (what the browser recorded when it loaded it)
        if (fileUrl) {
            group("File", _TF("File", "Header of a group of the media statistics: the file (name, type, size, transfer)"));
            add(_TF("Name", "Name of a media statistics value: the file name"), _TF("The name of the file", "Description of a media statistics value"), null,
                () => {
                    const f = Sw.Url.FileName(fileUrl.split(/[?#]/)[0]);
                    return f ? [decodeURIComponent(f), f] : null;
                });
            add(_TF("MIME type", "Name of a media statistics value: the content type of the file, ex: image/png"), _TF("The content type the server sent (else the one of the file extension)", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    const ct = t && t.contentType;
                    return ct ? [ct, ct] : MimeOfExt[ext] ? [MimeOfExt[ext] + " (." + ext + ")", MimeOfExt[ext]] : ext ? ["." + ext, ext] : null;
                });
            add(_TF("Size", "Name of a media statistics value: the file size"), _TF("The size of the file as sent by the server (known when the browser may measure it)", "Description of a media statistics value"), null,
                () => bytes(fileSize()));
            add(_TF("Decoded size", "Name of a media statistics value: the file size after the transfer compression was removed"), _TF("The size of the file after the transfer compression (gzip, br) was removed", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    return t && t.decodedBodySize && t.decodedBodySize !== t.encodedBodySize ? bytes(t.decodedBodySize) : null;
                });
            add(_TF("Transferred", "Name of a media statistics value: the bytes that went over the network"), _TF("The bytes that went over the network (headers included); from the cache means nothing was downloaded", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    if (!t || !(t.encodedBodySize > 0))
                        return null;
                    return t.transferSize === 0 ? [_TF("from the cache", "A media statistics value: the file was not downloaded, the browser had it"), 0] : bytes(t.transferSize);
                });
            add(_TF("Load time", "Name of a media statistics value: the time the file took to load"), _TF("From the request to the last byte of the file in ms", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    return t && t.duration > 0 ? msv(t.duration, 0) : null;
                });
            add(_TF("Protocol", "Name of a media statistics value: the network protocol, ex: h2"), _TF("The network protocol of the transfer (h2 = HTTP/2, h3 = HTTP/3)", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    return t && t.nextHopProtocol ? [t.nextHopProtocol, t.nextHopProtocol] : null;
                });
            add(_TF("Status", "Name of a media statistics value: the HTTP status of the response"), _TF("The HTTP status code of the response", "Description of a media statistics value"), null,
                () => {
                    const t = getTiming();
                    return t && t.responseStatus ? [t.responseStatus, t.responseStatus] : null;
                });
            if (mediaE || player.Duration > 0)
                add(_TF("Average bit rate", "Name of a media statistics value: the file size divided by the duration"), _TF("The size of the file divided by its duration", "Description of a media statistics value"), null,
                    () => {
                        const d = (mediaE && mediaE.duration) || player.Duration;
                        return d > 0 && isFinite(d) ? bitrate(fileSize() * 8 / d) : null;
                    });
        }

        // ---- the picture: of the media itself (an image with an effect: the image, not the effect's canvas); how it is shown (its size on
        //  the page, the crop) goes to the effect's section of mixed media
        /** Add the rows of how the media is shown: its size on the page and scale, the device pixel ratio, the crop. */
        const addShown = () => {
            add(_TF("Shown at", "Name of a media statistics value: the size the media is shown at on the page"), _TF("The size the media is shown at on the page in CSS pixels, and its scale (100 % = one pixel of the media on one pixel of the screen)", "Description of a media statistics value"), null,
                () => {
                    const e = player.Element;
                    //  (the pixels of the media: an effect's canvas, else the picture; read every time: a resize, full screen change them)
                    const mw = e instanceof HTMLCanvasElement ? e.width : player.Width;
                    if (!e || !e.getBoundingClientRect || !(mw > 0))
                        return null;
                    const r = e.getBoundingClientRect();
                    if (!(r.width > 0))
                        return null;
                    const scale = r.width * (window.devicePixelRatio || 1) / mw;
                    return [_T("{0} × {1} px ({2} %)", Math.round(r.width), Math.round(r.height), n(scale * 100, 0), "A media statistics value: the shown size of the media in CSS pixels ({0} the width, {1} the height) and its scale in percent ({2})"), scale];
                });
            add(_TF("Device pixel ratio", "Name of a media statistics value: screen pixels per CSS pixel"), _TF("The screen pixels per CSS pixel (the zoom and the display scaling)", "Description of a media statistics value"), null,
                () => [n(window.devicePixelRatio || 1, 2), window.devicePixelRatio || 1]);
            if (props && (typeof props.Crop !== "undefined")) {
                /** @returns {boolean} True if something is cropped. */
                const cropped = () => (player.ClipWidth !== player.Width) || (player.ClipHeight !== player.Height) || (player.ClipX !== 0) || (player.ClipY !== 0);
                add(_TF("Cropped width", "Name of a media statistics value"), _TF("The cropped width in pixels", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipWidth) : null);
                add(_TF("Cropped height", "Name of a media statistics value"), _TF("The cropped height in pixels", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipHeight) : null);
                add(_TF("Cropped left", "Name of a media statistics value"), _TF("The number of cropped pixels on the left", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipX) : null);
                add(_TF("Cropped top", "Name of a media statistics value"), _TF("The number of cropped pixels on the top", "Description of a media statistics value"), "Clip", () => cropped() ? px(player.ClipY) : null);
            }
        };
        if (visual && (!mixed || baseKind === "Image" || baseKind === "Video")) {
            group("Picture", _TF("Picture", "Header of a group of the media statistics: the size of the picture"));
            add(_TF("Width", "Name of a media statistics value: the media width"), _TF("The width in pixels of the media", "Description of a media statistics value"), null, () => pic()[0] > 0 ? px(pic()[0]) : null);
            add(_TF("Height", "Name of a media statistics value: the media height"), _TF("The height in pixels of the media", "Description of a media statistics value"), null, () => pic()[1] > 0 ? px(pic()[1]) : null);
            add(_TF("Megapixels", "Name of a media statistics value: the number of pixels in millions"), _TF("The number of pixels in millions", "Description of a media statistics value"), null,
                () => {
                    const [w, h] = pic();
                    return w > 0 && h > 0 ? [n(w * h / 1e6, 2) + " MP", w * h] : null;
                });
            add(_TF("Aspect ratio", "Name of a media statistics value: width / height"), _TF("The width divided by the height", "Description of a media statistics value"), null,
                () => aspect(...pic()));
            if (!mixed)
                addShown();
            if (imgE || baseKind === "Image") {
                add(_TF("Memory", "Name of a media statistics value: the memory the decoded picture takes"), _TF("The memory the decoded picture takes (4 bytes per pixel, an estimate)", "Description of a media statistics value"), null,
                    () => {
                        const [w, h] = pic();
                        return ext === "svg" ? null : bytes(w * h * 4);
                    });
                add(_TF("Bits per pixel", "Name of a media statistics value: the file size in bits divided by the number of pixels"), _TF("The size of the file in bits divided by the number of pixels (24 = uncompressed colors)", "Description of a media statistics value"), null,
                    () => {
                        const s = fileSize();
                        const [w, h] = pic();
                        return s > 0 && w > 0 && h > 0 && ext !== "svg" ? [n(s * 8 / (w * h), 2), s * 8 / (w * h)] : null;
                    });
                /**
                 * The bytes per pixel of the uncompressed picture: 3 (rgb) for a format without an alpha channel (jpeg), else 4 (rgba; a file
                 * of such a format may still have no alpha, that isn't known without reading the file).
                 * @returns {number} 3 or 4.
                 */
                const rawBpp = () => {
                    const t = getTiming();
                    const ct = (t && t.contentType) || MimeOfExt[ext] || "";
                    return /^image\/(jpeg|pjpeg)$/i.test(ct) || /^(jpe?g|jfif|jpe)$/.test(ext) ? 3 : 4;
                };
                add(_TF("Compression", "Name of a media statistics value: how many times smaller the file is than the decoded picture"), _TF("How many times smaller the file is than the uncompressed picture (3 bytes per pixel for a format without transparency, ex: jpeg, else 4)", "Description of a media statistics value"), null,
                    () => {
                        const s = fileSize();
                        const [w, h] = pic();
                        const b = rawBpp();
                        return s > 0 && w > 0 && h > 0 && ext !== "svg" ? [n(w * h * b / s, 1) + " : 1", w * h * b / s] : null;
                    });
                add(_TF("Vector", "Name of a media statistics value: the picture is a vector graphic (svg)"), _TF("A vector graphic: it is drawn sharp at any size", "Description of a media statistics value"), null,
                    () => ext === "svg" ? yesNo(true) : null);
            }
        }

        // ---- the video (the element's counters; the frame rate from requestVideoFrameCallback while the statistics are shown)
        /** @type {{Rate:number, F:number, T:number}} The measured frame rate of the video (frames presented per second of video). */
        const vfps = { Rate: 0, F: -1, T: 0 };
        if (videoE) {
            group("Video", _TF("Video", "Header of a group of the media statistics: the video stream"));
            add(_TF("Frame rate", "Name of a media statistics value: the frames per second of the video"), _TF("The frames per second of the video, measured while it plays", "Description of a media statistics value"), null,
                () => vfps.Rate > 0 ? [_T("{0} fps", n(vfps.Rate, 2), "A media statistics value: frames per second"), vfps.Rate] : null);
            /** @returns {?VideoPlaybackQuality} The frame counters of the video. */
            const q = () => typeof videoE.getVideoPlaybackQuality === "function" ? videoE.getVideoPlaybackQuality() : null;
            add(_TF("Frames shown", "Name of a media statistics value: the number of video frames shown"), _TF("The number of video frames the browser decoded and shown", "Description of a media statistics value"), null,
                () => {
                    const v = q();
                    return v ? [n(v.totalVideoFrames, 0), v.totalVideoFrames] : null;
                });
            add(_TF("Dropped frames", "Name of a media statistics value: the video frames that were skipped"), _TF("The video frames that were skipped (the computer was too slow), with the share of all frames", "Description of a media statistics value"), null,
                () => {
                    const v = q();
                    return v && v.totalVideoFrames > 0 ? [n(v.droppedVideoFrames, 0) + " (" + n(v.droppedVideoFrames * 100 / v.totalVideoFrames, 1) + " %)", v.droppedVideoFrames] : null;
                });
            add(_TF("Corrupted frames", "Name of a media statistics value: the video frames that were damaged"), _TF("The video frames that were damaged", "Description of a media statistics value"), null,
                () => {
                    const v = q();
                    return v && v.corruptedVideoFrames > 0 ? [n(v.corruptedVideoFrames, 0), v.corruptedVideoFrames] : null;
                });
            add(_TF("Video data rate", "Name of a media statistics value: the bit rate of the decoded video data"), _TF("The video data the browser decoded per second of play time (Chrome and Edge)", "Description of a media statistics value"), null,
                () => typeof videoE.webkitVideoDecodedByteCount === "number" && videoE.currentTime > 1 ? bitrate(videoE.webkitVideoDecodedByteCount * 8 / videoE.currentTime) : null);
            add(_TF("Has sound", "Name of a media statistics value: the video has a sound track"), _TF("The video has a sound track", "Description of a media statistics value"), null,
                () => {
                    if (videoE.audioTracks)
                        return yesNo(videoE.audioTracks.length > 0);
                    if (typeof videoE.mozHasAudio === "boolean")
                        return yesNo(videoE.mozHasAudio);
                    if (typeof videoE.webkitAudioDecodedByteCount === "number" && videoE.currentTime > 1)
                        return yesNo(videoE.webkitAudioDecodedByteCount > 0);
                    return null;
                });
            //  The frame rate: requestVideoFrameCallback reports the frames presented and their media time (only while the statistics show)
            if (typeof videoE.requestVideoFrameCallback === "function") {
                const onFrame = (now, md) => {
                    if (!root.isConnected)
                        return;
                    if (vfps.F >= 0 && md.presentedFrames > vfps.F && md.mediaTime > vfps.T) {
                        const df = md.presentedFrames - vfps.F;
                        const dt = md.mediaTime - vfps.T;
                        //  (a second of video at least, seeks and pauses are skipped: frames presented in a row only)
                        if (dt >= 1 && df / dt < 1000) {
                            vfps.Rate = df / dt;
                            vfps.F = md.presentedFrames;
                            vfps.T = md.mediaTime;
                        }
                        else if (dt > 3) {
                            vfps.F = -1;
                        }
                    }
                    else {
                        vfps.F = md.presentedFrames;
                        vfps.T = md.mediaTime;
                    }
                    videoE.requestVideoFrameCallback(onFrame);
                };
                videoE.requestVideoFrameCallback(onFrame);
            }
        }

        // ---- the sound (the decoded sound when the player has one: an effect with the audio2 texture, the offline analysis)
        if (soundPlayer) {
            /** @returns {?object} The decoded sound ({Rate, Channels}), null when the player didn't decode it. */
            const decoded = () => soundPlayer.Analyser?.Samples || soundPlayer.Audio2Engine?.Src || null;
            /** @type {number} 0 = not known, 1 = the two channels differed (stereo). */
            let stereoSeen = 0;
            group("Sound", _TF("Sound", "Header of a group of the media statistics: the sound (channels, sample rate)"));
            add(_TF("Channels", "Name of a media statistics value: mono, stereo ..."), _TF("The channels of the sound (from the decoded sound; else seen in the effect's sound texture: the left and the right channel differ)", "Description of a media statistics value"), null,
                () => {
                    const d = decoded();
                    const c = d && d.Channels;
                    if (c > 0)
                        return [c === 1 ? _TF("mono", "A media statistics value: a sound with one channel") : c === 2 ? _TF("stereo", "A media statistics value: a sound with two channels") : _T("{0} channels", c, "A media statistics value: the number of channels of a sound"), c];
                    //  The effect's sound texture (r = mono, g = left, b = right): the first row (the spectrum) of the left and right differ
                    const tx = player.Texture && player.Texture.Data;
                    if (!stereoSeen && tx && tx.length >= 512 * 3)
                        for (let i = 0; i < 512; ++i)
                            if (Math.abs(tx[i * 3 + 1] - tx[i * 3 + 2]) > 1e-3) {
                                stereoSeen = 1;
                                break;
                            }
                    return stereoSeen ? [_TF("stereo", "A media statistics value: a sound with two channels"), 2] : null;
                });
            add(_TF("Sample rate", "Name of a media statistics value: the samples per second of the sound"), _TF("The samples per second of the sound file (else the one it is played at)", "Description of a media statistics value"), null,
                () => {
                    const d = decoded();
                    if (d && d.Rate)
                        return [_T("{0} Hz", n(d.Rate, 0), "A media statistics value: a frequency in hertz"), d.Rate];
                    const ctx = soundPlayer.Analyser && soundPlayer.Analyser.Ctx;
                    return ctx ? [_T("{0} Hz (played at)", n(ctx.sampleRate, 0), "A media statistics value: the sample rate of the audio output in hertz"), ctx.sampleRate] : null;
                });
            if (mediaE)
                add(_TF("Sound data rate", "Name of a media statistics value: the bit rate of the decoded sound data"), _TF("The sound data the browser decoded per second of play time (Chrome and Edge)", "Description of a media statistics value"), null,
                    () => !videoE && typeof mediaE.webkitAudioDecodedByteCount === "number" && mediaE.currentTime > 1 ? bitrate(mediaE.webkitAudioDecodedByteCount * 8 / mediaE.currentTime) : null);
        }

        // ---- loading (the element's state)
        if (mediaE) {
            group("Loading", _TF("Loading", "Header of a group of the media statistics: the loading state of a video or sound"));
            const states = [
                _TF("nothing", "A media statistics value: the ready state of a video / sound, no data yet"),
                _TF("metadata", "A media statistics value: the ready state of a video / sound, the size and duration are known"),
                _TF("current frame", "A media statistics value: the ready state of a video / sound, the current position can be shown"),
                _TF("some ahead", "A media statistics value: the ready state of a video / sound, a little ahead is loaded"),
                _TF("enough", "A media statistics value: the ready state of a video / sound, enough is loaded to play on"),
            ];
            add(_TF("Ready state", "Name of a media statistics value: how much of the media the browser has"), _TF("How much of the media the browser has at the current position", "Description of a media statistics value"), null,
                () => [states[mediaE.readyState] || mediaE.readyState, mediaE.readyState]);
            add(_TF("Buffered ahead", "Name of a media statistics value: the seconds loaded after the current position"), _TF("The seconds loaded after the current position", "Description of a media statistics value"), null,
                () => {
                    const b = mediaE.buffered;
                    const p = mediaE.currentTime;
                    for (let i = 0; i < b.length; ++i)
                        if (b.start(i) <= p + 0.01 && b.end(i) >= p)
                            return secs(b.end(i) - p);
                    return null;
                });
            add(_TF("Buffered", "Name of a media statistics value: the share of the media that is loaded"), _TF("The share of the media that is loaded", "Description of a media statistics value"), null,
                () => {
                    const b = mediaE.buffered;
                    const d = mediaE.duration;
                    if (!(d > 0) || !isFinite(d))
                        return null;
                    let s = 0;
                    for (let i = 0; i < b.length; ++i)
                        s += b.end(i) - b.start(i);
                    return [n(Math.min(100, s * 100 / d), 0) + " %", s / d];
                });
        }

        // ---- the effect (its build, the GPU, the frame rate)
        /** @type {{Rate:number, F:number, T:number}} The measured frame rate of the effect (frames drawn per second). */
        const efps = { Rate: 0, F: -1, T: 0 };
        if (fx) {
            //  Mixed media: a section of its own, with the effect file, the render values and how the effect is shown
            if (mixed)
                section("Effect", _TF("Effect", "Header of the section of the media statistics with the values of the effect of an image / a sound with an effect"));
            /** Mixed media: the effect file and how the effect is shown, after its render values. */
            const addFxExtras = () => {
                if (fxUrl) {
                    group("EffectFile", _TF("File", "Header of a group of the media statistics: the file (name, type, size, transfer)"));
                    add(_TF("Name", "Name of a media statistics value: the file name"), _TF("The name of the effect file", "Description of a media statistics value"), null,
                        () => {
                            const f = Sw.Url.FileName(fxUrl.split(/[?#]/)[0]);
                            return f ? [decodeURIComponent(f), f] : null;
                        });
                    add(_TF("Size", "Name of a media statistics value: the file size"), _TF("The size of the effect file as sent by the server (known when the browser may measure it)", "Description of a media statistics value"), null,
                        () => {
                            const t = getFxTiming();
                            return t ? bytes(t.encodedBodySize || t.decodedBodySize || 0) : null;
                        });
                    add(_TF("Transferred", "Name of a media statistics value: the bytes that went over the network"), _TF("The bytes that went over the network (headers included); from the cache means nothing was downloaded", "Description of a media statistics value"), null,
                        () => {
                            const t = getFxTiming();
                            if (!t || !(t.encodedBodySize > 0))
                                return null;
                            return t.transferSize === 0 ? [_TF("from the cache", "A media statistics value: the file was not downloaded, the browser had it"), 0] : bytes(t.transferSize);
                        });
                    add(_TF("Load time", "Name of a media statistics value: the time the file took to load"), _TF("From the request to the last byte of the file in ms", "Description of a media statistics value"), null,
                        () => {
                            const t = getFxTiming();
                            return t && t.duration > 0 ? msv(t.duration, 0) : null;
                        });
                }
                group("EffectPicture", _TF("Picture", "Header of a group of the media statistics: the size of the picture"));
                add(_TF("Size", "Name of a media statistics value: the size of the effect's picture"), _TF("The size of the effect's picture in pixels", "Description of a media statistics value"), null,
                    () => player.Element instanceof HTMLCanvasElement ? size(player.Element.width, player.Element.height) : size(player.Width, player.Height));
                add(_TF("Aspect ratio", "Name of a media statistics value: width / height"), _TF("The width divided by the height", "Description of a media statistics value"), null,
                    () => player.Element instanceof HTMLCanvasElement ? aspect(player.Element.width, player.Element.height) : aspect(player.Width, player.Height));
                addShown();
            };
            group("Effect", mixed ? _TF("Render", "Header of a group of the media statistics: how the effect is rendered (frame rate, GPU, build)") : _TF("Effect", "Header of a group of the media statistics: the effect (shader) build and render times"));
            add(_TF("Frame rate", "Name of a media statistics value: the frames per second of the effect"), _TF("The frames the effect draws per second (0 while it doesn't animate)", "Description of a media statistics value"), null,
                () => {
                    const f = player.Frames;
                    if (typeof f !== "number")
                        return null;
                    const now = performance.now();
                    if (efps.F < 0 || f < efps.F) {
                        efps.F = f;
                        efps.T = now;
                    }
                    else if (now - efps.T >= 1000) {
                        efps.Rate = (f - efps.F) * 1000 / (now - efps.T);
                        efps.F = f;
                        efps.T = now;
                    }
                    return efps.T > 0 && efps.Rate >= 0 ? [_T("{0} fps", n(efps.Rate, 1), "A media statistics value: frames per second"), efps.Rate] : null;
                });
            add(_TF("Frame time", "Name of a media statistics value: the time between two frames of the effect"), _TF("The time between two frames in ms (1000 / the frame rate)", "Description of a media statistics value"), null,
                () => efps.Rate > 0 ? msv(1000 / efps.Rate, 1) : null);
            //  (an effect with a sound / video: its own clock runs on when the media loops, the time line is the media's)
            if (player.Media)
                add(_TF("Effect time", "Name of a media statistics value: the effect's own clock (it runs on when its sound / video loops)"), _TF("The effect's own clock in seconds: the media time, plus the time of the loops before when the sound / video loops (the effect doesn't start again with it)", "Description of a media statistics value"), null,
                    () => typeof player.EffectClock === "number" ? secs(player.EffectClock) : null);
            if (typeof player.AvgDrawTimeMs !== "undefined")
                add(_TF("GPU time", "Name of a media statistics value: time spent on the graphics card"), _TF("The time the graphics card spends on a frame of the effect in ms (the shortest of the last frames; measured by the GPU when it can, else the frame time)", "Description of a media statistics value"), null,
                    () => {
                        let v = player.AvgDrawTimeMs;
                        if (!(v > 0) && player.MeasureCount > 0 && player.TotalTime < 1e10)
                            v = player.TotalTime;
                        return v > 0 ? msv(v, 3) : null;
                    });
            add(_TF("GPU timer", "Name of a media statistics value: the GPU time is measured by the GPU"), _TF("The graphics card measures the GPU time (EXT_disjoint_timer_query), else it is the frame time", "Description of a media statistics value"), null,
                () => player.GL ? yesNo(!!player.Query) : null);
            add(_TF("Build", "Name of a media statistics value: the shader compilation time"), _TF("The duration of the compilation step of this effect in ms", "Description of a media statistics value"), null,
                () => {
                    const v = player.CompilationTime;
                    if (v === -3)
                        return [_TF("error", "Media statistics value: the effect failed to compile")];
                    if (v < 0)
                        return [_TF("compiling", "Media statistics value: the effect is being compiled")];
                    return msv(v, 1);
                });
            add(_TF("Effect Weaver build", "Name of a media statistics value: the effect was built by the server (Ew snippets, a vertex stage, controls)"), _TF("The server built the effect the way Effect Weaver does (it uses Ew snippets, a vertex stage or live controls)", "Description of a media statistics value"), null,
                () => player.GL ? yesNo(!!player.Built) : null);
            add(_TF("Vertex stage", "Name of a media statistics value: the effect has its own vertex shader"), _TF("The effect has its own vertex shader (vertexMain)", "Description of a media statistics value"), null,
                () => player.Built ? yesNo(!!player.Built.Vertex) : null);
            add(_TF("GLSL", "Name of a media statistics value: the shading language version, ex: ES 3.00"), _TF("The version of the shading language the effect is compiled as", "Description of a media statistics value"), null,
                () => {
                    if (!player.GL)
                        return null;
                    const es3 = player.Built ? !!player.Built.Es3 : !!(player.ProgramData && player.ProgramData.Es3) || /^\s*#version\s+300/m.test(player.Src || "");
                    return ["ES " + (es3 ? "3.00" : "1.00"), es3 ? 300 : 100];
                });
            add(_TF("Source", "Name of a media statistics value: the size of the effect source"), _TF("The lines and characters of the effect source", "Description of a media statistics value"), null,
                () => typeof player.Src === "string" && player.Src.length ? [_T("{0} lines, {1} characters", n(player.Src.split("\n").length, 0), n(player.Src.length, 0), "A media statistics value: the size of a source text"), player.Src.length] : null);
            add(_TF("Canvas", "Name of a media statistics value: the pixel size the effect is drawn at"), _TF("The pixel size the effect is drawn at", "Description of a media statistics value"), "Actual",
                () => player.Element ? size(player.Element.width, player.Element.height) : null);
            add(_TF("Texture", "Name of a media statistics value: the size of the effect's texture (its picture or sound)"), _TF("The size of the effect's texture (its picture, text or sound as a texture)", "Description of a media statistics value"), null,
                () => {
                    const tx = player.Texture;
                    if (!tx || !(tx.Width > 0) || !(tx.Height > 0))
                        return null;
                    const kind = tx.Audio2 ? " (audio2)" : typeof tx.Quiet === "boolean" ? " (" + _TF("sound", "A media statistics value: the texture of the effect is a sound") + ")" : "";
                    return [size(tx.Width, tx.Height)[0] + kind, tx.Width + "x" + tx.Height];
                });
            add(_TF("Transparent", "Name of a media statistics value: the effect draws with transparency"), _TF("The effect draws see-through pixels", "Description of a media statistics value"), null,
                () => player.Params && player.GL ? yesNo(!!player.Params.Transparent) : null);
            add(_TF("WebGL", "Name of a media statistics value: the WebGL version"), _TF("The WebGL version the effect is drawn with", "Description of a media statistics value"), null,
                () => {
                    const gl = player.GL;
                    if (!gl)
                        return null;
                    const v2 = typeof WebGL2RenderingContext !== "undefined" && gl instanceof WebGL2RenderingContext;
                    return [v2 ? "2.0" : "1.0", v2 ? 2 : 1];
                });
            add(_TF("GPU", "Name of a media statistics value: the graphics card"), _TF("The graphics card (as the browser names it)", "Description of a media statistics value"), null,
                () => player.GL ? [GetGpuInfo(player.GL).Renderer, GetGpuInfo(player.GL).Renderer] : null);
            add(_TF("Max texture size", "Name of a media statistics value: the largest texture the graphics card takes"), _TF("The largest texture the graphics card takes", "Description of a media statistics value"), null,
                () => player.GL && GetGpuInfo(player.GL).MaxTexture > 0 ? px(GetGpuInfo(player.GL).MaxTexture) : null);
            if (mixed)
                addFxExtras();
        }
        if (visual && props && (typeof props.AdaptiveSize !== "undefined") && !fx) {
            add(_TF("Render width", "Name of a media statistics value: the canvas pixel width"), _TF("The actual rendered width in pixels", "Description of a media statistics value"), "Actual",
                () => player.Params.AdaptiveSize && player.Element ? px(player.Element.width) : null);
            add(_TF("Render height", "Name of a media statistics value: the canvas pixel height"), _TF("The actual rendered height in pixels", "Description of a media statistics value"), "Actual",
                () => player.Params.AdaptiveSize && player.Element ? px(player.Element.height) : null);
        }

        /**
         * Refresh the rows 4 times a second while the block is shown (none while it is hidden: a closed panel costs nothing); stops once
         * the block left the document (after the first refresh).
         * @param {boolean} [first] The first call.
         */
        function update(first) {
            if (!first && !root.isConnected)
                return;
            const shown = first || (typeof root.checkVisibility === "function" ? root.checkVisibility() : root.offsetParent !== null);
            if (shown) {
                for (const u of ups)
                    u();
                //  (a section or group without a shown value: no header)
                for (const p of parts)
                    p.G.classList.toggle("Hide", p.Rows.every(r => r.tagName !== "MEDIA-STAT" || r.classList.contains("Hide")));
            }
            setTimeout(update, shown ? 250 : 500);
        }
        for (const p of parts)
            if (p.Apply)
                p.Apply();
        update(true);
        //  (read the values now, also while hidden: "Copy information to clipboard" copies them)
        root.SwRefresh = () => {
            for (const u of ups)
                u();
        };
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

        const player = await MediaPlayer.Create(type, data, PlayerParams(type, props));
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
                if (type === MediaTypes.YouTube)
                    throw YouTubeError(player.ErrorCode, data);
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
        const stats = noStats ? null : CreateStats(player, player.Params || props, type, data);
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
     * @param {{Muted: boolean, Loop: boolean}} [o] Instead of the stored preferences (ex: the `muted` / `loop` url options of MediaView.html).
     * @returns {Promise<void>}
     */
    async function PlayWithPopup(player, o) {
        o = o || {};
        if (o.Loop ?? Prefs.Loop())
            await player.Loop();
        else
            await player.Once();
        player.SetVolume(Prefs.Volume());
        const muted = o.Muted ?? Prefs.Muted();
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
                SoundParams(player).Volume = props.Volume;
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
            ShowLoadError(target, e);
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
     * URL params: `link`/`d` (required), `type`/`t`, `props` (JSON), `no_stats`, `no_controls`, `muted`, `loop` (start muted / looping instead of the stored preferences), `pos` (seconds to start at), `edit=false` (no "Edit" menu
     * item / E key, which open the media in MediaEdit.html), `ctrl` (InterOp message prefix of a controlling editor). The media fills the `<Media-View>` grid (fill = false, automatic aspect, centered);
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
            //  The `name` | `n` url parameter names the media (ex: a blob: link of an api result has no name of its own, explore/api.html
            //  passes the file name the service sent): it's the title and the file name of a download (limited inside the app shell)
            const givenName = (U.Get("name") ?? U.Get("n") ?? "").trim();
            const linkName = Sw.Url.FileName(String(U.Get("link") ?? U.Get("d") ?? "").split("?")[0].split("#")[0]);
            {
                const path = String(type === MediaTypes.Text ? data : (U.Get("link") ?? U.Get("d") ?? "")).split("?")[0];
                //  (a YouTube link: "watch", "embed" ... is no name: the video code)
                const fname = givenName ? TextFromUrl(givenName, MaxAppText)
                    : type === MediaTypes.YouTube ? _T("YouTube {0}", data, "Title of the media viewer showing a YouTube video, {0} is the video code") : Sw.Url.FileName(path);
                if (fname)
                    document.title = fname;
            }
            //  A file media (an image, a video, a sound) can be downloaded: as the given name, else the file name of the link
            const canDownload = type === MediaTypes.Image || type === MediaTypes.Video || type === MediaTypes.Audio;
            const downloadName = (givenName || linkName || "media").replace(/[\\/:*?"<>|]/g, "_");
            const messagePrefix = U.Get("ctrl");
            if (messagePrefix) {
                const np = await AskEditorProps(messagePrefix);
                if (np)
                    props = np;
                //  The editor docks its player again when this window goes
                window.addEventListener("pagehide", () => Sw.InterOp.Post(messagePrefix + "Closed"));
            }
            //  The media params as given (the url, the editor); `props` below becomes the player's params (for an image or a text
            //  with an effect those are the effect's params), a rebuild uses these
            let mediaProps = props;
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
                const pd = await CreatePlayer(mview, type, data, mediaProps, true, before, !stats, false, 0, 0, 0.5, 0.5, !before);
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

            //  A sound or an image (not linked to an editor, it owns the settings): the menu picks its effect (a sound's visualization), a
            //  built in effect or none; the choice is remembered (Prefs.Effect) and used for the next sound / image whose settings name none
            const fxKind = messagePrefix ? null : EffectKind(type);
            /** @type {?string[]} The names of the built in effects of the kind (filled when the list arrives), null: no effect choice. */
            let fxNames = null;
            /** @type {boolean} The remembered effect was used (the media's settings named none). */
            let fxFromPrefs = false;
            if (fxKind) {
                fxNames = [];
                Sw.Net.Get(fxKind.Api).then(x => fxNames = Array.isArray(x) ? x.filter(n => n && n !== "None") : []).catch(() => { });
                const fx0 = mediaProps ? mediaProps.Effect : null;
                const pref = Prefs.Effect(type);
                if ((fx0 === null || fx0 === undefined) && pref !== null) {
                    mediaProps = WithViewerEffect(type, mediaProps, pref);
                    fxFromPrefs = true;
                }
            }

            let player;
            try {
                player = (await create(null))[0];
            }
            catch (e) {
                //  A remembered effect that can't be used (ex: it was removed) doesn't stop the media: without it, and it is forgotten
                if (!fxFromPrefs)
                    throw e;
                console.warn("Media: the remembered effect " + mediaProps.Effect + " can't be used: " + FailReason(e));
                Prefs.SetEffect(type, null);
                mediaProps = props;
                fxFromPrefs = false;
                player = (await create(null))[0];
            }
            props = player.Params;

            /** @returns {string} The effect the media shows ("" = none; a missing Effect is the default of the kind). */
            const curFx = () => {
                const fx = mediaProps ? mediaProps.Effect : null;
                return fx === null || fx === undefined ? (fxKind && fxKind.Default ? fxKind.Default : "") : fx;
            };
            /** @returns {boolean} True when the effect addresses a and b are the same file. */
            const sameFx = (a, b) => {
                if (!a || !b)
                    return a === b;
                try {
                    return new URL(a, ScriptBase).href === new URL(b, ScriptBase).href;
                }
                catch {
                    return a === b;
                }
            };
            let fxBusy = false;
            /**
             * Show the media with another effect (a rebuild keeps the position, the play state and the volume); the page address keeps the
             * choice, a built in effect or none is remembered for the next media of the kind.
             * @param {string} fx The effect ("" = none).
             * @param {boolean} remember Remember it (Prefs.SetEffect).
             */
            const setFx = async (fx, remember) => {
                if (fxBusy)
                    return;
                if (sameFx(curFx(), fx)) {
                    if (remember)
                        Prefs.SetEffect(type, fx);
                    return;
                }
                fxBusy = true;
                const old = mediaProps;
                try {
                    const np = WithViewerEffect(type, mediaProps, fx);
                    //  The shader values of the old effect don't belong to the new one (its other settings, ex: the speed, stay)
                    if (np.EffectParams) {
                        np.EffectParams = Object.assign({}, np.EffectParams);
                        delete np.EffectParams.FxProps;
                        delete np.EffectParams.EffectProps;
                    }
                    mediaProps = np;
                    player = await RebuildPlayer(player, create);
                    props = player.Params;
                    if (remember)
                        Prefs.SetEffect(type, fx);
                    const u = new URL(window.location.href);
                    u.searchParams.set("props", JSON.stringify(mediaProps));
                    history.replaceState(history.state, "", u.href);
                }
                catch (e) {
                    mediaProps = old;
                    Sw.Toast(_T("The effect can't be shown: {0}", FailReason(e), "Error of the media viewer when a sound or an image could not be shown with the chosen effect (a sound's visualization), {0} the reason"), { Kind: "error" });
                }
                finally {
                    fxBusy = false;
                }
            };
            /** @returns {?Array<object>} The items of the effects menu, null: none (not a sound or an image). */
            const fxItems = () => {
                if (!fxNames)
                    return null;
                const cur = curFx();
                const items = [{ Text: _TF("None", "Menu item of the effects menu of the media viewer (a sound's visualization, an image's effect): the media is shown without an effect"), Radio: true, Checked: !cur, OnClick: () => setFx("", true) }];
                let found = !cur;
                for (const n of fxNames) {
                    const v = fxKind.Folder + n + ".glsl";
                    const on = sameFx(cur, v);
                    found = found || on;
                    items.push({ Text: Sw.Format.BeautifyName(n), Radio: true, Checked: on, OnClick: () => setFx(v, true) });
                }
                //  An effect of its own (not a built in one, never remembered): listed too, so the choice can go back to it
                if (!found) {
                    const own = cur;
                    items.push("-", { Text: Sw.Format.BeautifyName(Sw.Url.FileName(own.split(/[?#]/)[0]).replace(/\.glsl$/i, "")) || own, Radio: true, Checked: true, OnClick: () => setFx(own, false) });
                }
                return items;
            };

            if (controls)
                CreateChrome(mview, () => player, {
                    Stats: stats,
                    StartHidden: true,
                    //  ?endhud (or endhud=true): show the controls when the media ends (default: nothing over the last picture)
                    EndHud: U.Has("endhud") && !/^(false|0|no)$/i.test(U.Get("endhud") ?? ""),
                    Transparent: () => !!(mediaProps && mediaProps.Transparent),
                    FullPage: () => showFull(player),
                    Info: () => ({
                        Type: type,
                        Link: type === MediaTypes.YouTube ? "https://www.youtube.com/watch?v=" + data
                            : type === MediaTypes.Text ? null : new URL(data, window.location.href).href,
                    }),
                    Load: m => ShowLink(m),
                    Effects: fxNames ? fxItems : null,
                    EffectsText: fxKind ? fxKind.Text() : null,
                    //  (the media file: with an effect the player's Source is the effect, the file is the sound / video it follows or the link)
                    Download: canDownload ? () => Sw.Net.Download((player.Media && player.Media.Url) || (typeof player.CompilationTime !== "undefined" ? data : player.Source) || data, downloadName) : null,
                    //  "Edit": this media with its settings in MediaEdit.html (edit=false hides it; not for a player linked to an editor)
                    Edit: messagePrefix || /^(false|0|no)$/i.test(U.Get("edit") ?? "") ? null : () => {
                        const q = new URLSearchParams();
                        q.set("link", U.Get("link") ?? U.Get("d"));
                        q.set("type", String(type));
                        if (mediaProps)
                            q.set("props", JSON.stringify(mediaProps));
                        Sw.Page.Navigate("MediaEdit.html?" + q.toString(), "_self");
                    },
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
                    const name = t.substring(messagePrefix.length);
                    if (name === "Load") {
                        //  Another media in the editor: this page with its link and type, still linked (the props come with GetProps)
                        const u = new URL(window.location.href);
                        u.searchParams.set("link", m.Link);
                        u.searchParams.set("type", String(m.MediaType));
                        u.searchParams.delete("props");
                        u.searchParams.delete("pos");
                        Sw.Page.Navigate(u.href, "_self");
                        return;
                    }
                    if (name === "Reload") {
                        //  The editor's complete media params (also new members, ex: a crop that was null)
                        const np = Object.assign({}, m);
                        for (const k of ["Type", "From", "To", "MagicXyz"])
                            delete np[k];
                        mediaProps = np;
                        player = await RebuildPlayer(player, create);
                        props = player.Params;
                        return;
                    }
                    Object.getOwnPropertyNames(m).forEach(pn => {
                        if (typeof props[pn] !== "undefined")
                            props[pn] = m[pn];
                    });
                    switch (name) {
                        case "SetVolume":
                            //  (an effect that plays a sound: the media volume is the sound's)
                            if (player.Media && typeof m.Volume === "number")
                                player.Media.Params.Volume = m.Volume;
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
            //  A YouTube link with a start time ("t=90"), or `pos` (seconds, ex: a window opened by the editor): start there
            const pos = parseFloat(U.Get("pos") ?? "");
            const start = pos > 0 ? pos : mp.Start;
            if (start > 0)
                await player.Seek(start);
            //  `muted` / `loop` (ex: a background video of a page): instead of the visitor's stored preferences, which they don't change
            await PlayWithPopup(player, { Muted: U.Has("muted") ? true : undefined, Loop: U.Has("loop") ? true : undefined });
        }
        catch (e) {
            //  An in-page error state with the reason and "Try again" (was a toast over an empty page)
            ShowLoadError(target, e);
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

    /** @type {string} Storage key: the statistics panel of the viewer, JSON { X, Y (0..1 of the free room, null = the default place), Collapsed }. */
    const StatsPanelKey = "SysWeaver.Media.StatsPanel";

    /** @type {string} Storage key: the collapsed sections of the statistics, JSON array of their ids ("Image", "Effect", "File" ...). */
    const StatsFoldedKey = "SysWeaver.Media.StatsFolded";

    /** @type {string} Storage key: "true" = the controls panel of the viewer is wanted (it shows for an effect with controls). */
    const ControlsKey = "SysWeaver.Media.ShowControls";

    /** @type {string} Storage key: the controls panel of the viewer, JSON { X, Y, Collapsed } (as StatsPanelKey). */
    const ControlsPanelKey = "SysWeaver.Media.ControlsPanel";

    /**
     * The live controls an effect describes (its "// iMouse.x = Name | Description | Default | Min .. Max | Unit" comments, from the
     * server build: MediaService.BuildEffect ControlDefs), the channels it doesn't describe left out.
     * @param {?object} p A player (an effect player, also one with an image / video / sound / text).
     * @returns {Array<{Index:number, Name:string, Desc:string, Default:number, Used:boolean, Map:?object}>} 0 to 4 controls.
     */
    function ControlDefsOf(p) {
        const b = p && typeof p.SetControls === "function" ? p.Built : null;
        if (!b)
            return [];
        const defs = Array.isArray(b.ControlDefs) ? b.ControlDefs
            : (b.ControlNames || []).map((n, i) => n ? { Name: n, Desc: "", Default: (b.Controls || [])[i] ?? 0.5, Used: true } : null);
        const r = [];
        for (let i = 0; i < 4; ++i) {
            const d = defs[i];
            if (d && d.Name)
                r.push({ Index: i, Name: d.Name, Desc: d.Desc || "", Default: typeof d.Default === "number" ? d.Default : 0.5, Used: d.Used !== false, Map: d.Map || null });
        }
        return r;
    }

    /**
     * The control values chosen per effect (the controls panel of the viewer), in localStorage["SysWeaver.Media.EffectControls"]:
     * JSON { "<effect address>": { V: { "<channel 0..3>": value 0..1 }, T: last change (ms) } }, the most recently changed MaxEntries
     * effects. An effect is known by its address (no fragment); one without a lasting address (a blob: / data: url, a local file) isn't
     * remembered. Writes are delayed a moment (a slider drag changes the value many times a second).
     */
    const EffectControls = {
        /** @type {string} Storage key. */
        Key: "SysWeaver.Media.EffectControls",
        /** @type {number} The most effects remembered (the oldest changes are dropped). */
        MaxEntries: 200,
        /** @type {?object} The map (read once). */
        Map: null,
        /** @type {number} The delayed write. */
        Timer: 0,

        /**
         * @param {?object} p A player of an effect.
         * @returns {?string} The address of its effect, null when it has no lasting one.
         */
        IdOf(p) {
            const u = p && typeof p.Url === "string" ? p.Url : "";
            if (!u)
                return null;
            try {
                const x = new URL(u, document.baseURI);
                if (x.protocol !== "http:" && x.protocol !== "https:")
                    return null;
                x.hash = "";
                return x.href;
            }
            catch {
                return null;
            }
        },

        /** @returns {object} The map. */
        All() {
            const t = EffectControls;
            if (!t.Map) {
                let m = null;
                try {
                    m = JSON.parse(Sw.Storage.Get(t.Key) || "null");
                }
                catch {
                    m = null;
                }
                t.Map = m && typeof m === "object" && !Array.isArray(m) ? m : {};
            }
            return t.Map;
        },

        /**
         * @param {?object} p A player of an effect.
         * @returns {?Object<string, number>} The remembered values by channel ("0".."3"), null if none.
         */
        Get(p) {
            const id = EffectControls.IdOf(p);
            const e = id ? EffectControls.All()[id] : null;
            return e && e.V && typeof e.V === "object" ? e.V : null;
        },

        /**
         * Remember the values of an effect.
         * @param {?object} p A player of an effect.
         * @param {Object<string, number>} values The values by channel.
         */
        Set(p, values) {
            const t = EffectControls;
            const id = t.IdOf(p);
            if (!id)
                return;
            const m = t.All();
            m[id] = { V: values, T: Date.now() };
            clearTimeout(t.Timer);
            t.Timer = setTimeout(() => t.Flush(), 300);
            //  (a change right before the page closes is written then)
            if (!t.OnHide) {
                t.OnHide = () => t.Timer && t.Flush();
                window.addEventListener("pagehide", t.OnHide);
            }
        },

        /** Write the map now (the oldest effects dropped past MaxEntries). */
        Flush() {
            const t = EffectControls;
            clearTimeout(t.Timer);
            t.Timer = 0;
            const m = t.All();
            const keys = Object.keys(m);
            if (keys.length > t.MaxEntries)
                for (const k of keys.sort((a, b) => (m[a].T || 0) - (m[b].T || 0)).slice(0, keys.length - t.MaxEntries))
                    delete m[k];
            Sw.Storage.Set(t.Key, JSON.stringify(m));
        },

        /** @type {?function():void} The pagehide listener (added with the first change). */
        OnHide: null,
    };

    /**
     * The Sw pop-ups (menus, dialogs, toasts, hints): in full screen they are inside the player (Sw.FullScreen.OverlayRoot), so the
     * player's own clicks and keys skip them and the controls stay while one is open.
     * @type {string}
     */
    const SwOverlays = ".sw-menu-host, .sw-popup, .sw-layer, .sw-dialog, .sw-toasts, .sw-hint";

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
     * A floating window over the media of the viewer (the statistics, the effect's controls): a title bar (drag it to move the panel, the
     * arrow keys on it too, a double click / Enter collapses it), Collapse and Close. The position and the collapsed state are remembered
     * (o.StateKey, JSON { X, Y (0..1 of the free room, null = the default place), Collapsed }); the position is kept as a share of the free
     * room, so the panel stays in the same corner when the player is resized (full screen).
     * @param {HTMLElement} view The `<Media-View>` (the panel is placed in it).
     * @param {object} o Options.
     * @param {string} o.Title The title.
     * @param {string} o.Label The accessible name of the panel.
     * @param {string} o.MoveLabel The accessible name of the title bar (the drag handle).
     * @param {string} o.CollapseTitle The tool tip of the Collapse button while expanded.
     * @param {string} o.ExpandTitle The tool tip of the Collapse button while collapsed.
     * @param {string} o.CloseTitle The tool tip of the Close button.
     * @param {function():void} o.OnClose Called by the Close button.
     * @param {string} o.StateKey The storage key of the position and the collapsed state.
     * @param {number} [o.DefaultX] The default place: 0 = left .. 1 = right (default 1).
     * @param {string} [o.Class] An extra class of the panel.
     * @param {boolean} [o.Resizable] True: the panel can be resized by its bottom right corner, the size is remembered too (W, H in px).
     * @returns {{E:HTMLElement, Body:HTMLElement, Place:function():void, Show:function(boolean):void}} The panel (hidden at first), its
     *   body (the content goes there), Place (after the content changed size) and Show.
     */
    function FloatingPanel(view, o) {
        const el = Sw.el;
        const panel = el("div.media-stats-panel" + (o.Class ? "." + o.Class : ""), { attrs: { role: "dialog", "aria-modal": "false", "aria-label": o.Label } });
        panel.hidden = true;
        const body = el("div.media-stats-body");
        /** @type {{X:?number, Y:?number, Collapsed:boolean}} The remembered state. */
        const state = (() => {
            let s = null;
            try {
                s = JSON.parse(Sw.Storage.Get(o.StateKey) || "null");
            }
            catch {
                s = null;
            }
            const f = v => typeof v === "number" && isFinite(v) ? Math.max(0, Math.min(1, v)) : null;
            const px = v => typeof v === "number" && isFinite(v) && v >= 64 ? Math.round(v) : null;
            return { X: f(s && s.X), Y: f(s && s.Y), W: px(s && s.W), H: px(s && s.H), Collapsed: !!(s && s.Collapsed) };
        })();
        //  A resizable panel (the corner grip, media.css media-stats-resizable): its remembered size (px, clamped into the player by CSS)
        if (o.Resizable) {
            panel.classList.add("media-stats-resizable");
            if (state.W)
                panel.style.width = state.W + "px";
        }
        const save = () => Sw.Storage.Set(o.StateKey, JSON.stringify(state));
        /** The margin (px) the panel keeps from the edges of the player. */
        const margin = 8;
        /** @returns {number[]} The free room [width, height] the panel can move in (0 when it is as large as the player). */
        const room = () => [
            Math.max(0, view.clientWidth - panel.offsetWidth - 2 * margin),
            Math.max(0, view.clientHeight - panel.offsetHeight - 2 * margin),
        ];
        /** Place the panel (the remembered share of the free room; by default at the top, below the title bar). */
        const place = () => {
            if (panel.hidden)
                return;
            const [w, h] = room();
            const x = state.X ?? (o.DefaultX ?? 1);
            const y = state.Y ?? (h > 0 ? Math.min(1, 44 / h) : 0);
            panel.style.left = Math.round(margin + x * w) + "px";
            panel.style.top = Math.round(margin + y * h) + "px";
        };
        /**
         * Move the panel (px from the player's top left, clamped into the player).
         * @param {number} left The left edge.
         * @param {number} top The top edge.
         */
        const move = (left, top) => {
            const [w, h] = room();
            state.X = w > 0 ? Math.max(0, Math.min(1, (left - margin) / w)) : 0;
            state.Y = h > 0 ? Math.max(0, Math.min(1, (top - margin) / h)) : 0;
            place();
        };
        const collapseB = new Sw.Button({ Icon: "up", Kind: "ghost", Small: true, Class: "media-stats-button", OnClick: () => setCollapsed(!state.Collapsed) });
        /** @param {boolean} on Collapse the panel (only its title bar shows). */
        const setCollapsed = on => {
            state.Collapsed = on;
            body.hidden = on;
            panel.classList.toggle("media-stats-collapsed", on);
            collapseB.SetIcon(on ? "down" : "up");
            collapseB.SetTitle(on ? o.ExpandTitle : o.CollapseTitle);
            collapseB.E.setAttribute("aria-expanded", String(!on));
            //  (collapsed: only the title bar, not the chosen height)
            if (o.Resizable)
                panel.style.height = !on && state.H ? state.H + "px" : "";
            save();
            place();
        };
        const handle = el("div.media-stats-title", { text: o.Title, attrs: { tabindex: "0", role: "button", "aria-label": o.MoveLabel } });
        handle.title = _TF("Drag to move the panel (or the arrow keys)", "Tool tip of the title bar of a floating panel of the media viewer (the statistics, the controls)");
        const closeB = new Sw.Button({ Icon: "close", Kind: "ghost", Small: true, Class: "media-stats-button", Title: o.CloseTitle, OnClick: () => o.OnClose() });
        const head = el("div.media-stats-head", null, handle, collapseB.E, closeB.E);
        panel.append(head, body);
        //  Dragging by the title bar (pointer capture: the move follows outside the panel); a double click collapses / expands
        let drag = null;
        head.addEventListener("pointerdown", ev => {
            if (ev.button !== 0 || ev.target.closest(".media-stats-button"))
                return;
            const r = panel.getBoundingClientRect();
            const v = view.getBoundingClientRect();
            drag = { Id: ev.pointerId, Dx: ev.clientX - r.left, Dy: ev.clientY - r.top, V: v };
            head.setPointerCapture(ev.pointerId);
            panel.classList.add("media-stats-dragging");
            ev.preventDefault();
        });
        head.addEventListener("pointermove", ev => {
            if (!drag || ev.pointerId !== drag.Id)
                return;
            move(ev.clientX - drag.V.left - drag.Dx, ev.clientY - drag.V.top - drag.Dy);
        });
        const endDrag = ev => {
            if (!drag || ev.pointerId !== drag.Id)
                return;
            drag = null;
            panel.classList.remove("media-stats-dragging");
            save();
        };
        head.addEventListener("pointerup", endDrag);
        head.addEventListener("pointercancel", endDrag);
        head.addEventListener("dblclick", ev => {
            if (!ev.target.closest(".media-stats-button"))
                setCollapsed(!state.Collapsed);
        });
        //  The arrow keys on the title bar move the panel (Shift: in large steps); the player doesn't get them (no seeking)
        handle.addEventListener("keydown", ev => {
            const d = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[ev.key];
            if (ev.key === "Enter" || ev.key === " ") {
                ev.preventDefault();
                ev.stopPropagation();
                setCollapsed(!state.Collapsed);
                return;
            }
            if (!d || ev.ctrlKey || ev.altKey || ev.metaKey)
                return;
            ev.preventDefault();
            ev.stopPropagation();
            const step = ev.shiftKey ? 50 : 10;
            move(panel.offsetLeft + d[0] * step, panel.offsetTop + d[1] * step);
            save();
        });
        //  (the player resized, full screen: the same share of the free room)
        if (typeof ResizeObserver !== "undefined")
            new ResizeObserver(() => place()).observe(view);
        //  Resized by its grip (the browser sets the inline width / height): the size is remembered, the panel keeps its place (its share
        //  of the free room is taken again from where it is)
        if (o.Resizable && typeof ResizeObserver !== "undefined") {
            let saveTimer = 0;
            new ResizeObserver(() => {
                if (panel.hidden || state.Collapsed)
                    return;
                const w = parseFloat(panel.style.width), h = parseFloat(panel.style.height);
                const nw = isFinite(w) ? Math.round(w) : state.W, nh = isFinite(h) ? Math.round(h) : state.H;
                if (nw === state.W && nh === state.H)
                    return;
                state.W = nw;
                state.H = nh;
                move(panel.offsetLeft, panel.offsetTop);
                clearTimeout(saveTimer);
                saveTimer = setTimeout(save, 250);
            }).observe(panel);
        }
        setCollapsed(state.Collapsed);
        return {
            E: panel,
            Body: body,
            Place: place,
            Show: on => {
                panel.hidden = !on;
                place();
            },
        };
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
     * M mute, F full screen, I stats, C controls.
     * The controls panel (o.Controls): the live controls an effect describes (ControlDefsOf: up to 4 named sliders, also shown in the
     * value they stand for), a floating window like the statistics; a media without controls has the button and menu item disabled.
     * A media without a known end (Duration <= 0: an effect) gets a time line that grows in steps of 5 minutes and "∞" as its length.
     * The state is read from the player every frame, so a rebuilt player (getPlayer returns the new one) is followed.
     * @param {HTMLElement} view The `<Media-View>`.
     * @param {function():?object} getPlayer Returns the current player.
     * @param {object} o Options.
     * @param {boolean} o.Stats True: a Stats button and panel (the player's `Stats` element is moved into it).
     * @param {boolean} [o.Controls] False: no controls button and panel (the host has its own, ex: an editor). Default true.
     * @param {function():void} o.FullPage Called when the browser has no full screen for the element (opens the full page preview).
     * @param {?function():void} [o.Edit] Opens the media in the editor: an "Edit" menu item and the E key (none: no edit).
     * @param {?function():?Array<object>} [o.Effects] The items of an effects sub menu (the effect of an image, the visualization of a sound:
     *   none and the built in effects), null / none: no such menu item.
     * @param {?string} [o.EffectsText] The name of that menu item (default "Effects").
     * @param {?function():void} [o.Download] Saves the media file: a "Download" menu item (none: the media is no file, ex: a YouTube video or a text).
     * @param {function():boolean} [o.Transparent] True when the media is transparent (its settings; the player's own Params.Transparent
     *   counts too, an image / text with an effect has the effect's params): class MediaTransparent (the stage is the page background anyway, see o.Background).
     * @param {function():string} [o.Title] The name in the title bar (default: the page title).
     * @param {boolean} [o.StartHidden] True: no controls and no title at the start (until a pointer move, a tap, a key or the focus;
     *   shown when the media still doesn't play after 1.5 s; never for audio only media).
     * @param {boolean} [o.EndHud] True: the controls and the big Play button show when the media reaches its end (not looped). Default
     *   false: nothing over the last picture until a pointer move, a tap, a key or the focus (never for audio only media).
     * @param {{Get:function():string, Set:function(string):void}} [o.Background] The stage behind the media (Backgrounds values: the
     *   background button and menu of a media with a picture). Default: the viewer's preference (localStorage
     *   "SysWeaver.Media.Background"), shown by the view itself (`data-media-bg`, media.css). A host that draws its own background passes
     *   Get / Set.
     * @param {boolean} [o.Links] False: no address features (no Show url, Open the original file, Copy information, no pasted or dropped
     *   links), for a player of something that isn't a media file (ex: Effect Weaver's editor view). Default true.
     * @param {function():boolean} [o.MediaClicks] False: a click / double click on the media doesn't play / pause / jump / go full screen
     *   (the host uses the pointer, ex: dragging sets a value). Default: always true.
     */
    function CreateChrome(view, getPlayer, o) {
        const el = Sw.el;
        const player0 = getPlayer();
        const links = o.Links !== false;
        //  The stage behind the media: the host's (o.Background), else the viewer's preference shown by the view (data-media-bg)
        const bg = o.Background || {
            Get: () => Prefs.Background(),
            Set: v => {
                Prefs.SetBackground(v);
                view.dataset.mediaBg = v;
            },
        };
        if (!o.Background)
            view.dataset.mediaBg = Prefs.Background();
        /** @returns {Array<object>} The items of the background menu. */
        const bgItems = () => Backgrounds.map(x => ({ Text: x.Label(), Radio: true, Checked: (bg.Get() || Prefs.DefaultBackground) === x.Value, OnClick: () => bg.Set(x.Value) }));
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
            if (p.Stats && typeof p.Stats.SwRefresh === "function")
                p.Stats.SwRefresh();
            const chips = p.Stats ? [...p.Stats.querySelectorAll("Media-Stat:not(.Hide):not(.Idle)")] : [];
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
            if (p.HaveVisual)
                items.push({ Text: _TF("Background", "Menu item of the media viewer with the stages behind the media (a sub menu: checkerboard, colors)"), Icon: "layers", Items: bgItems() });
            const fx = o.Effects ? o.Effects() : null;
            if (fx)
                items.push({ Text: o.EffectsText || _TF("Effects", "Menu item of the media viewer with the effects of an image (a sub menu: none and the built in image effects)"), Icon: "sparkle", Items: fx });
            items.push("-");
            if (panel)
                items.push({ Text: _TF("Show the statistics", "Menu item of the media viewer that shows / hides the statistics panel"), Checked: !panel.hidden, Hint: "I", OnClick: () => setStats(panel.hidden) });
            if (ctl)
                items.push({ Text: _TF("Show the controls", "Menu item of the media viewer that shows / hides the controls panel (the live controls of an effect, sliders)"), Icon: "sliders", Checked: !ctl.E.hidden, Hint: "C",
                    Disabled: !ctlDefs.length, Desc: ctlDefs.length ? undefined : _TF("The media has no controls", "Why the Show the controls menu item of the media viewer is disabled"), OnClick: () => setControls(ctl.E.hidden) });
            items.push({ Text: Sw.FullScreen.Is() ? _TF("Leave full screen", "Tool tip of the full screen button of the media viewer in full screen") : _TF("Full screen", "Tool tip of the full screen button of the media viewer"), Icon: Sw.FullScreen.Is() ? "fullscreenExit" : "fullscreen", Hint: "F", OnClick: toggleFull });
            if (links)
                items.push("-",
                    { Text: _TF("Show url...", "Menu item of the media viewer that asks for a media address and shows it"), Icon: "link", Hint: "U", OnClick: askLink },
                    { Text: _TF("Copy information to clipboard", "Menu item of the media viewer that copies the address, the state, the statistics and the settings of the media"), Icon: "copy", OnClick: copyInfo },
                    { Text: _TF("Open the original file", "Menu item of the media viewer that opens the original file in a new tab (or its editor)"), Icon: "external", OnClick: () => Sw.Page.EditOrOpen(p.Source) });
            if (o.Download)
                items.push({ Text: _TF("Download", "Menu item of the media viewer that saves the media file (an image, a video, a sound) to the computer"), Icon: "download", OnClick: o.Download });
            if (o.Edit)
                items.push({ Text: _TF("Edit", "Menu item of the media viewer that opens the media with its settings in the media editor"), Icon: "edit", Hint: "E", OnClick: o.Edit });
            return items;
        };

        //  The statistics panel (the player's stats moved here from below the media): a floating window over the media (FloatingPanel),
        //  open / collapsed / position remembered (StatsKey, StatsPanelKey)
        const stats = o.Stats ? FloatingPanel(view, {
            Title: _TF("Statistics", "Title of the statistics panel of the media viewer"),
            Label: _TF("Media statistics", "Accessible name of the statistics panel of the media viewer"),
            MoveLabel: _TF("Move the statistics panel (drag, or the arrow keys)", "Accessible name of the title bar of the statistics panel of the media viewer"),
            CollapseTitle: _TF("Collapse the statistics (only the title bar stays)", "Tool tip of the button of the statistics panel of the media viewer that hides its values"),
            ExpandTitle: _TF("Expand the statistics", "Tool tip of the button of the statistics panel of the media viewer that shows its values again"),
            CloseTitle: _TF("Close the statistics (I)", "Tool tip of the close button of the statistics panel of the media viewer"),
            OnClose: () => setStats(false),
            StateKey: StatsPanelKey,
            Resizable: true,
        }) : null;
        const panel = stats ? stats.E : null;
        if (stats && player0.Stats)
            stats.Body.append(player0.Stats);
        const setStats = on => {
            if (!stats)
                return;
            stats.Show(on);
            statsB.SetPressed(on);
            Sw.Storage.Set(StatsKey, on ? "true" : "false");
        };

        //  The controls panel: the live controls the effect describes (its named iMouse channels: sliders in [0, 1], also shown in the
        //  value they stand for), a floating window like the statistics; none: the panel, its button and menu item are disabled. Open /
        //  collapsed / position remembered (ControlsKey, ControlsPanelKey)
        const ctl = o.Controls !== false ? FloatingPanel(view, {
            Title: _TF("Controls", "Title of the controls panel of the media viewer: the live controls of an effect (sliders)"),
            Label: _TF("Effect controls", "Accessible name of the controls panel of the media viewer"),
            MoveLabel: _TF("Move the controls panel (drag, or the arrow keys)", "Accessible name of the title bar of the controls panel of the media viewer"),
            CollapseTitle: _TF("Collapse the controls (only the title bar stays)", "Tool tip of the button of the controls panel of the media viewer that hides its sliders"),
            ExpandTitle: _TF("Expand the controls", "Tool tip of the button of the controls panel of the media viewer that shows its sliders again"),
            CloseTitle: _TF("Close the controls (C)", "Tool tip of the close button of the controls panel of the media viewer"),
            OnClose: () => setControls(false),
            StateKey: ControlsPanelKey,
            DefaultX: 0,
            Class: "media-controls-panel",
            Resizable: true,
        }) : null;
        /** @type {boolean} The controls panel is wanted (shown when the effect has controls). */
        let ctlOn = Sw.Storage.Get(ControlsKey) === "true";
        /** @type {Array<object>} The controls of the current effect ({ Index (the iMouse channel), Name, Desc, Default, Used, Map }). */
        let ctlDefs = [];
        /** @type {?object} The property editor of the controls panel. */
        let ctlEditor = null;
        /** Show / hide the controls panel and its button state (shown only when wanted and the effect has controls). */
        const showControls = () => {
            if (!ctl)
                return;
            const has = ctlDefs.length > 0;
            ctl.Show(ctlOn && has);
            ctlB.SetPressed(ctlOn && has);
            ctlB.SetDisabled(!has, has ? "" : _TF("The media has no controls (an effect can describe up to 4)", "Why the controls button of the media viewer is disabled"));
        };
        const setControls = on => {
            if (!ctl || (on && !ctlDefs.length))
                return;
            ctlOn = on;
            Sw.Storage.Set(ControlsKey, on ? "true" : "false");
            showControls();
        };
        /**
         * The controls of a player (rebuilt when the player or its effect build changes, see update).
         * @param {?object} p The player.
         */
        const buildControls = p => {
            ctlDefs = ControlDefsOf(p);
            ctlEditor = null;
            ctl.Body.replaceChildren();
            if (ctlDefs.length) {
                //  The values chosen for this effect before (EffectControls) win over its defaults and settings
                const saved = EffectControls.Get(p);
                if (saved && ctlDefs.some(d => typeof saved[d.Index] === "number")) {
                    const c = p.GetControls();
                    for (const d of ctlDefs)
                        if (typeof saved[d.Index] === "number")
                            c[d.Index] = Math.max(0, Math.min(1, saved[d.Index]));
                    p.SetControls(c);
                }
                const values = typeof p.GetControls === "function" ? p.GetControls() : [];
                ctlEditor = new Sw.PropertyEditor({
                    Defs: ctlDefs.map(d => ({ Key: "c" + d.Index, Label: d.Name, Type: "float", Min: 0, Max: 1, Step: 0.001, Default: d.Default, Map: d.Map || undefined, Desc: d.Desc || undefined })),
                    Values: Object.fromEntries(ctlDefs.map(d => ["c" + d.Index, values[d.Index] ?? d.Default])),
                    ResetButtons: true,
                    OnInput: (k, v) => setControl(+k.slice(1), v),
                    OnChange: (k, v) => setControl(+k.slice(1), v),
                });
                //  (a control the effect's code doesn't read: dimmed)
                for (const d of ctlDefs)
                    if (d.Used === false)
                        ctlEditor.SetEnabled("c" + d.Index, false);
                ctl.Body.append(ctlEditor.E);
            }
            showControls();
        };
        /**
         * Set a control of the current player.
         * @param {number} i The channel (0..3).
         * @param {number} v The value in [0, 1].
         */
        const setControl = (i, v) => {
            const p = getPlayer();
            if (!p || typeof p.SetControls !== "function")
                return;
            const c = p.GetControls();
            c[i] = v;
            p.SetControls(c);
            //  Remembered for this effect (the controls it names; a reset to the default is remembered too, it wins over a setting)
            EffectControls.Set(p, Object.fromEntries(ctlDefs.map(d => [d.Index, c[d.Index]])));
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
        const ctlB = b("sliders", _TF("Controls: the live controls of the effect", "Tool tip of the controls switch of the media viewer (sliders that change an effect while it plays)"), () => setControls(ctl.E.hidden), { Pressable: true, AutoToggle: false, Shortcut: "C" });
        ctlB.E.hidden = !ctl;
        const openB = b("external", _TF("Open the original file in a new tab (or in its editor, when there is one)", "Media player button tool tip of the button that opens the original media file"),
            () => Sw.Page.EditOrOpen(getPlayer().Source));
        const fullB = b("fullscreen", _TF("Full screen", "Tool tip of the full screen button of the media viewer"), toggleFull, { Shortcut: "F" });
        //  The stage behind the media (a media with a picture: an effect, an image, a video)
        const bgB = new Sw.Button({
            Icon: "layers", Title: _TF("Background: what shows behind the media (transparent parts, around it)", "Tool tip of the background button of the media viewer"),
            Kind: "ghost", Class: "media-chrome-button", Menu: () => [{ Title: _TF("Background", "Header of the background menu of the media viewer") }, ...bgItems()], MenuOptions: { Side: "above", Align: "end" },
        });
        bgB.E.hidden = !player0.HaveVisual;
        if (!links)
            openB.E.hidden = true;
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
        const title = el("span.media-title", { text: o.Title ? o.Title() : document.title });
        const top = el("div.media-top.media-chrome-part", null, title);
        const bar = el("div.media-bar.media-chrome-part", null,
            timeS.E,
            el("div.media-bar-row", null,
                playB.E, stopB.E, loopB.E,
                muteB.E, volS.E,
                timeE,
                el("span.media-bar-gap"),
                ctlB.E, statsB ? statsB.E : null, bgB.E, openB.E, fullB.E, moreB.E));
        //  The sound controls and the speaker of a media without a picture follow the current player (update): a rebuild in the editor
        //  can add a picture (a sound with an effect) or a sound (an effect with a sound texture)
        const art = el("div.media-audio-art", { attrs: { "aria-hidden": "true" } }, Sw.Icon("IconMediaPreviewVolLoud"));
        art.hidden = !!player0.HaveVisual;
        muteB.E.hidden = volS.E.hidden = !player0.HaveAudio;
        const chrome = el("div.media-chrome", null, art, skipE, bigPlay.E, top, ctl ? ctl.E : null, panel, bar);
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
        const parts = [top, bar, panel, ctl ? ctl.E : null].filter(x => x);
        /** @returns {boolean} True when the controls must stay (pointer on them, a focused control, a drag). */
        const mustStay = () => parts.some(x => x.matches(":hover")) || timeS.Dragging || volS.Dragging || !!chrome.querySelector(":focus-visible")
            || moreB.E.getAttribute("aria-expanded") === "true" || !!view.querySelector(".sw-menu-host, .sw-popup, .sw-dialog");
        function hide() {
            clearTimeout(hideTimer);
            if (mustStay()) {
                hideTimer = setTimeout(hide, ChromeHideMs);
                return;
            }
            view.classList.remove("MediaChromeOn");
        }
        function show() {
            view.classList.remove("MediaChromeFresh");
            view.classList.add("MediaChromeOn");
            clearTimeout(hideTimer);
            hideTimer = setTimeout(hide, ChromeHideMs);
        }
        //  The start: no controls and no title over the picture (also while it loads, paused) until the first activity (a pointer move,
        //  a tap, a key, the focus); a media that still doesn't play after a moment (the browser refused to start it) shows its Play
        //  button and controls. Audio only media keep the controls (there is nothing else to see)
        if (o.StartHidden && player0.HaveVisual) {
            view.classList.add("MediaChromeFresh");
            setTimeout(() => {
                const p = getPlayer();
                if (p && p.CanPlay && p.Paused)
                    view.classList.remove("MediaChromeFresh");
            }, 1500);
        }
        let lastTouch = 0;
        //  A mouse shows hidden controls only after it moved a little (RevealPx from where it was first seen since they hid): a bumped
        //  mouse or a pointer resting on the media doesn't bring them back. While shown any move keeps them
        const RevealPx = 24;
        let anchor = null;
        //  The mouse outside the player (or the window): nothing over the picture, also while paused (MediaPointerOut, media.css); the
        //  controls show again when the mouse is back over it, on a tap or a key
        const pointerOut = () => {
            anchor = null;
            if (timeS.Dragging || volS.Dragging)
                return;
            clearTimeout(hideTimer);
            view.classList.remove("MediaChromeOn");
            view.classList.add("MediaPointerOut");
        };
        document.addEventListener("mouseout", ev => {
            if (!ev.relatedTarget)
                pointerOut();
        });
        view.addEventListener("pointermove", ev => {
            if (ev.pointerType !== "mouse")
                return;
            view.classList.remove("MediaPointerOut");
            if (view.classList.contains("MediaChromeOn")) {
                anchor = null;
                show();
                return;
            }
            if (!anchor) {
                anchor = { X: ev.clientX, Y: ev.clientY };
                return;
            }
            if (Math.hypot(ev.clientX - anchor.X, ev.clientY - anchor.Y) < RevealPx)
                return;
            anchor = null;
            show();
        });
        view.addEventListener("pointerleave", ev => {
            anchor = null;
            if (ev.pointerType === "mouse")
                pointerOut();
        });
        view.addEventListener("pointerdown", ev => {
            view.classList.remove("MediaPointerOut");
            if (ev.pointerType !== "mouse")
                lastTouch = Date.now();
        });
        view.addEventListener("keydown", () => view.classList.remove("MediaPointerOut"));
        view.addEventListener("focusin", () => show());

        //  The media itself: a click plays / pauses (a tap shows / hides the controls); a double click / tap on the left or right third
        //  jumps 15 s back / forward (more taps in a row jump again), in the middle it toggles full screen
        const onMedia = ev => !ev.target.closest(".media-chrome-part, .media-stats-panel, .media-bigplay, " + SwOverlays);
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
            if (!onMedia(ev) || Sw.BadClick(ev) || (o.MediaClicks && !o.MediaClicks()))
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

        //  The context menu of the media: the ⋮ menu (a pure right click; with Shift, Ctrl, Alt ... the browser's menu, UX guide INP-3)
        view.addEventListener("contextmenu", ev => {
            if (!onMedia(ev) || !Sw.Menu || !Sw.IsPureContextMenu(ev))
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
        if (links) {
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
        }

        // ---- keys

        view.addEventListener("keydown", ev => {
            if (ev.ctrlKey || ev.altKey || ev.metaKey)
                return;
            const t = ev.target;
            //  A menu, dialog ... inside the player (full screen): its keys are its own
            if (t.closest && t.closest(SwOverlays))
                return;
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
                    seek((SoundParams(p) || {}).StartAt || 0);
                    break;
                case "j":
                case "l":
                    skip(k === "j" ? -15 : 15);
                    break;
                case "u":
                    if (!links)
                        return;
                    askLink();
                    break;
                case "e":
                    if (!o.Edit)
                        return;
                    o.Edit();
                    break;
                case "m":
                    toggleMute();
                    break;
                case "f":
                    toggleFull();
                    break;
                case "c":
                    if (!ctl || !ctlDefs.length)
                        return;
                    setControls(ctl.E.hidden);
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
                //  A transparent media (its Transparent setting): the theme background shows through instead of the black stage
                put("transparent", !!((p.Params && p.Params.Transparent) || (o.Transparent && o.Transparent())), v => view.classList.toggle("MediaTransparent", v));
                const paused = !!p.Paused;
                put("paused", paused, v => {
                    view.classList.toggle("MediaPaused", v);
                    //  The end of a media (not looped): nothing over the picture (as at the start) until the next activity, unless EndHud
                    if (v && !o.EndHud && p.HaveVisual && !p.Looping && p.Duration > 0 && isFinite(p.Duration) && (p.AtEnd || (p.GetPos() || 0) >= p.Duration - 0.1)) {
                        clearTimeout(hideTimer);
                        view.classList.remove("MediaChromeOn");
                        view.classList.add("MediaChromeFresh");
                    }
                    playB.SetIcon(v ? "play" : "pause");
                    playB.SetTitle(v ? _TF("Play", "Tool tip of the play button of the media viewer") : _TF("Pause", "Tool tip of the pause button of the media viewer"));
                    //  Playing again: the bars hide after a moment (not at the start: they are not shown yet)
                    if (!v && !view.classList.contains("MediaChromeFresh"))
                        show();
                });
                put("loop", !!p.Looping, v => loopB.SetPressed(v));
                put("visual", !!p.HaveVisual, v => {
                    art.hidden = v;
                    bgB.E.hidden = !v;
                    view.classList.toggle("MediaAudioOnly", !v);
                });
                put("audio", !!p.HaveAudio, v => muteB.E.hidden = volS.E.hidden = !v);
                //  The controls of the effect: rebuilt for another player or another build of its effect (the editor rebuilds it)
                if (ctl) {
                    put("ctlPlayer", p, () => buildControls(p));
                    put("ctlBuilt", p.Built, () => buildControls(p));
                }
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
                put("title", o.Title ? o.Title() : document.title, v => title.textContent = v);
            }
            requestAnimationFrame(update);
        }
        update();
        if (!view.classList.contains("MediaChromeFresh"))
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
     * URL params: `link`/`d`, `type`/`t`, `props`, `pos` (seek position, s), `rpos` (the seek position as a part of the duration, 0.5 = the
     * middle; a media without a duration uses pos), `fill`, `full` (full screen; re-entered on the
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
                //  The sound of an audio effect is analysed from its samples (a headless browser runs no AudioContext; the same picture
                //  every time, at the paused position)
                window.MediaAudioOffline = true;
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
            let pos = parseFloat(U.Get("pos") ?? "0") || 0;
            //  `rpos`: the position as a part of the duration (0.5 = the middle of a sound), when the media has one (else pos)
            const rpos = parseFloat(U.Get("rpos") ?? "");
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
            const player = await MediaPlayer.Create(mp.Type, mp.Data, PlayerParams(mp.Type, mp.Props));
            if (!player)
                throw new Error(_T("No supported player found for type {0}", mp.Type, "Error when no media player exists for a media type. {0} is the type number"));
            const e = player.Element;
            e.draggable = false;
            target.appendChild(e);
            let failed = false;
            try {
                if (!(await player.Cache(true)))
                    throw new Error(_TF("Failed to cache the media", "Error when a media file could not be loaded"));
                //  A screenshot host gets the effect itself, not the still shown while it compiles
                if (shost && player.WhenReady)
                    await player.WhenReady();
                if (fontError && realHost)
                    throw fontError;
                if (isFinite(rpos) && rpos >= 0 && player.Duration > 0 && isFinite(player.Duration))
                    pos = player.Duration * Math.min(1, rpos);
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
                    ShowLoadError(target, ex);
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
        //  Shared with the media editor (mediaEdit.js, MediaEdit.html)
        LoadEditor,
        ReadMediaParams,
        ReadEditSource,
        ParseYouTube,
        ParseMediaLink,
        MediaLinkProblem,
        ShowLinkProblem,
        ShowLoadError,
        FailReason,
        CreateChrome,
        MessagePrefix,
        ParamTypeName,
        FormatTime,
        InApp,
        DefaultAudioEffect,
        PlayerParams,
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
