/**
 * @file media.js - media viewer / preview / editor page logic (plain global functions, no module).
 *
 * Loaded with a classic `<script defer>` by:
 * - `MediaView.html` (boot `mediaViewMain()` from `<body onload>`): player + stats + controls, remote-controllable.
 * - `MediaPreview.html` (boot `mediaPreviewMain()`): bare full-window player, also used for screenshots.
 * - `mediaEditMain()` is the boot function of an embeddable "assignment media editor" for an external host page;
 *   nothing in this repository calls it.
 *
 * Globals defined: functions `mediaCreatePlayer`, `mediaPlayWithPopup`, `mediaEditProps`, `mediaEditMain`,
 * `mediaGetTypeFromUrl`, `mediaViewMain`, `mediaPreviewMain` and class `SsHack`. Implicit globals:
 * `props` (assigned without declaration in `mediaEditMain`, i.e. `window.props`) and `objState` (assigned by the
 * save callback created in `mediaEditMain`; NOT the local `objState` of `mediaEditProps`).
 *
 * Dependencies:
 * - mediaPlayer/media_player.js (loaded below via includeJs): `MediaPlayer`, `MediaTypes`, and through it
 *   `MediaPlayerTools` (media_player_tools.js) and the per-type player classes (lazily included).
 * - common.js: `includeJs`, `Fail`, `Info`, `Button`, `ButtonStyle`, `badClick`, `keyboardClick`, `ValueFormat`,
 *   `isInDocument`, `getUrlParams`, `sendRequest`, `waitEvent`, `Open`, `EditOrOpenUrl`, `InterOp`, `FullScreen`,
 *   `PageLoaded`, `getFileExtension`, `isImageExt`/`isVideoExt`/`isAudioExt`, `YaMD5`.
 * - Edit service scripts (only for the editor): `Edit`, `EditOptions`, `UndoStack`; and `QuizzRights`.
 * - Styles: media.css (custom elements Media-Preview, Media-AspectContainer, Media-Overlay, Media-Stats, ...).
 *
 * Conventions:
 * - User preferences are stored in localStorage keys `QuizzWeaver.MediaLoop` ("true"/"false"),
 *   `QuizzWeaver.MediaVolume` (float string, default 0.25) and `QuizzWeaver.MediaMute` ("true"/"false").
 * - Editor <-> MediaView.html remote control uses `InterOp.Post(<messagePrefix><Name>, data)` messages with names
 *   GetProps, SetProps, Reload, SetVolume, UpdateSize, Crop, Compile.
 */

/**
 * Top-level call executed at parse time: loads the media player framework relative to this script's URL.
 * The load is asynchronous; the boot functions expect `MediaPlayer`/`MediaTypes` to be ready when `onload` fires.
 */
includeJs(document.currentScript.src, "../mediaPlayer/media_player.js");

/**
 * Creates a media player with its preview box and (optionally) a stats block and a controls block.
 *
 * DOM: creates `<Media-Preview>` (the player's TopElement) holding `<Media-AspectContainer>` (the clip box) and a
 * temporary `<Media-Cache>` (removed once laid out). With `before` the preview gets class `Replacing` (built
 * off-screen) and is inserted before `before`; otherwise it is appended to `target`. The player element is put in
 * the clip, followed by a `<Media-Overlay>` (crop visualization). The clip gets class `MediaPreviewFit` or
 * `MediaPreviewFill`; the preview gets `NoVisual` for media without a visual. Unless `noStats`, `<Media-Stats>`
 * is appended to `target`; unless `noControls`, `<Media-Controls>` is appended to `target`.
 *
 * Layout: the inner `UpdateSize` runs now, from a ResizeObserver on `<Media-Preview>` and from `player.OnClipChanged`.
 *
 * Members added to the player: `SetButtonState` (refresh the control buttons), `UpdateSize` (re-layout),
 * `TopElement` (the `<Media-Preview>`), and with controls `MessageElement` (the `<Media-Controls>`, which listens
 * for the `mediaEnded` event).
 *
 * Errors: when no player can be created, calls `Fail(...)`, removes the preview and returns undefined; when
 * `player.Cache()` fails, calls `Fail(...)` and returns undefined (the preview stays in the DOM).
 *
 * @param {HTMLElement} target - Container that receives the preview, stats and controls.
 * @param {number} type - Media type (one of `MediaTypes`).
 * @param {string} data - Media URL (or other type specific data, e.g. YouTube id).
 * @param {?object} props - Media params (mirrors the C# Media* params types); when null, `player.Params` is used.
 * @param {boolean} [noControls] - True to skip the `<Media-Controls>` block.
 * @param {?HTMLElement} [before] - Existing element (old TopElement) to insert the new preview before (player rebuild).
 * @param {boolean} [noStats] - True to skip the `<Media-Stats>` block.
 * @param {boolean} [fit=true] - True: scale the media to fit inside the box (MediaPreviewFit) and position the
 *   stats/controls right under it; false: scale to fill (MediaPreviewFill, overflow clipped).
 * @param {number} [aspectX=16] - Box aspect X; `<= 0` (either aspect) means automatic aspect (from the clipped media
 *   size, or from the preview box when `AdaptiveSize` is set).
 * @param {number} [aspectY=9] - Box aspect Y.
 * @param {number} [alignX=0.5] - Horizontal alignment (0..1) of the clip inside the preview box.
 * @param {number} [alignY=0] - Vertical alignment (0..1) of the clip inside the preview box.
 * @returns {Promise<(Array|undefined)>} `[player, mediaControls, mediaStats]` (controls/stats are null when not
 *   created), or undefined on failure.
 */
async function mediaCreatePlayer(target, type, data, props, noControls, before, noStats, fit, aspectX, aspectY, alignX, alignY) {
    const p = document.createElement("Media-Preview");
    if (before) {
        p.classList.add("Replacing");
        target.insertBefore(p, before);
    }
    else
        target.appendChild(p);

    const clip = document.createElement("Media-AspectContainer");
    p.appendChild(clip);
    const cache = document.createElement("Media-Cache");
    p.appendChild(cache);

    
    const player = await MediaPlayer.Create(type, data, props);
    if (!player) {
        Fail("No supported player found for type " + type, 100000);
        p.remove();
        return;
    }
    if (!props)
        props = player.Params;
    if (!player.HaveVisual)
        p.classList.add("NoVisual");


    clip.appendChild(player.Element);
    if (!(await player.Cache())) {
        cache.remove();
        Fail("Failed to cache the media", 100000);
        return;
    }

    const mediaOverlay = document.createElement("Media-Overlay");
    clip.appendChild(mediaOverlay);

    if (typeof fit === "undefined")
        fit = true;
    if (typeof aspectX === "undefined")
        aspectX = 16;
    if (typeof aspectY === "undefined")
        aspectY = 9;
    if (typeof alignX === "undefined")
        alignX = 0.5;
    if (typeof alignY === "undefined")
        alignY = 0;
    const autoAspect = (aspectX <= 0) || (aspectY <= 0);


    clip.classList.add(fit ? "MediaPreviewFit" : "MediaPreviewFill");

    let mediaControls = null;
    let mediaStats = null;


    /**
     * Lays out the clip box and the player element inside `<Media-Preview>`.
     * - When called with an event and the player element is no longer in the document, removes itself as a window
     *   `resize` listener and returns (legacy: the resize listener registration is commented out).
     * - Re-reads `props = player.Params`; with automatic aspect recomputes it (preview box size with AdaptiveSize,
     *   otherwise the clipped media size).
     * - A zero/negative clipped size collapses the clip to 0x0.
     * - AdaptiveSize: the player element gets the box size in px, no transform, clip inline styles reset.
     * - Otherwise: the player element gets `props.Width`/`props.Height` px plus a `scale(..%) translate(..px, ..px)`
     *   transform (fit = min scale, fill = max scale, honoring ClipX/ClipY/ClipWidth/ClipHeight when `player.UseClip`),
     *   and the clip is sized to the aspect and positioned with alignX/alignY.
     *   When `player.UseClip` is false, the overlay gets class `Show` and semi-transparent red borders
     *   (rgba(255,0,0,0.25)) whose widths equal the cropped margins; otherwise `Show` is removed.
     * - With `fit`, sets `style.top` of `<Media-Stats>` (or, without stats, `<Media-Controls>`) to just below the media
     *   (+2% of the window height).
     * - Copies the player transform and client size to `<Media-Overlay>`.
     * @param {Event} [ev] - Optional resize event.
     * @returns {void}
     */
    function UpdateSize(ev) {
        if (ev) {
            if (!isInDocument(player.Element)) {
                window.removeEventListener("resize", UpdateSize);
                return;
            }
        }
        props = player.Params;
        if (autoAspect) {
            if (props.AdaptiveSize) {
                const pr = p.getBoundingClientRect();
                aspectX = pr.width;
                aspectY = pr.height;
            } else {
                const useClip = player.UseClip;
                const ow = player.Width;
                const oh = player.Height;
                const crw = (useClip ? player.ClipWidth : ow) ?? ow;
                const crh = (useClip ? player.ClipHeight : oh) ?? oh;
                aspectX = crw;
                aspectY = crh;
            }
        }

        const useClip = player.UseClip;
        const ow = player.Width;
        const oh = player.Height;
        const crw = (useClip ? player.ClipWidth : ow) ?? ow;
        const crh = (useClip ? player.ClipHeight : oh) ?? oh;
        if ((crw <= 0) || (crh <= 0)) {
            clip.style.width = "0";
            clip.style.height = "0";
            return;
        }
        const crx = (useClip ? player.ClipX : 0) ?? 0;
        const cry = (useClip ? player.ClipY : 0) ?? 0;

        const rect = p.getBoundingClientRect();
        let cw = rect.width;
        let ch = cw * aspectY / aspectX;
        if (ch > rect.height) {
            ch = rect.height;
            cw = ch * aspectX / aspectY;
        }

        const e = player.Element;
        const playerE = e.style;
        const overlayS = mediaOverlay.style;
        if (props.AdaptiveSize) {
            playerE.transform = null;
            playerE.width = Math.round(rect.width) + "px";
            playerE.height = Math.round(rect.height) + "px";


            clip.style.left = null;
            clip.style.top = null;
            clip.style.width = null;
            clip.style.height = null;

            if (fit) {
                const b = window.innerHeight;
                const controlBottom = rect.bottom + (b * 0.02);
                if (mediaStats)
                    mediaStats.style.top = (controlBottom | 0) + "px";
                else
                    if (mediaControls)
                        mediaControls.style.top = (controlBottom | 0) + "px";
            }

        } else {
            playerE.width = props.Width + "px";
            playerE.height = props.Height + "px";

            const ox = (rect.width - cw) * alignX;
            const oy = (rect.height - ch) * alignY;
            clip.style.left = ox + "px";
            clip.style.top = oy + "px";
            clip.style.width = cw + "px";
            clip.style.height = ch + "px";
            const scaleX = cw / crw;
            const scaleY = ch / crh;
            const scale = fit ? (scaleX < scaleY ? scaleX : scaleY) : (scaleX > scaleY ? scaleX : scaleY);

            const dx = (cw - (scale * (crw + crx * 2))) * 0.5;
            const dy = (ch - (scale * (crh + cry * 2))) * 0.5;
            const tr = "scale(" + (scale * 100) + "%) translate(" + (dx / scale) + "px, " + (dy / scale) + "px)";
            playerE.transform = tr;
            if (fit) {
                const b = window.innerHeight;
                const controlBottom = oy + ch + rect.y + (b * 0.02);
                if (mediaStats)
                    mediaStats.style.top = (controlBottom | 0) + "px";
                else
                    if (mediaControls)
                        mediaControls.style.top = (controlBottom | 0) + "px";
            }
            if (player.UseClip) {
                mediaOverlay.classList.remove("Show");
            } else {
                mediaOverlay.classList.add("Show");
                const col = "px solid rgba(255, 0, 0, 0.25)";
                const cl = player.ClipX;
                const ct = player.ClipY;
                const cr = ow - cl - player.ClipWidth;
                const cb = oh - ct - player.ClipHeight;
                const ms = mediaOverlay.style;
                if (cl > 0)
                    ms.borderLeft = cl + col;
                else
                    ms.borderLeft = null;
                if (ct > 0)
                    ms.borderTop = ct + col;
                else
                    ms.borderTop = null;

                if (cr > 0)
                    ms.borderRight = cr + col;
                else
                    ms.borderRight = null;
                if (cb > 0)
                    ms.borderBottom = cb + col;
                else
                    ms.borderBottom = null;
                playerE.clipPath = null;
            }
        }
        overlayS.transform = playerE.transform;
        overlayS.width = e.clientWidth + "px";
        overlayS.height = e.clientHeight + "px";
    }
    UpdateSize();
    /**
     * Player callback invoked when the clip (crop) changes; re-runs `UpdateSize()`.
     * @returns {void}
     */
    player.OnClipChanged = () => {

        UpdateSize();
    }

    new ResizeObserver(() => UpdateSize()).observe(p);
//    window.addEventListener("resize", UpdateSize);
    cache.remove();

    /**
     * Refreshes the enabled state of the control buttons from the player state. A no-op until the controls are
     * created (only when `player.CanPlay`); then it enables Mute/Un mute, Pause/Play, Loop/Once and the four volume
     * buttons so that only the actions that change the state are enabled. Exposed as `player.SetButtonState` and
     * returned by every button callback so common.js `Button` calls it after the click.
     * @returns {void}
     */
    let setButtonState = () => { };

    if (!noStats) {

        const p = document.createElement("Media-Stats");
        mediaStats = p;
        target.appendChild(p);

        /**
         * Appends one stat chip `<Media-Stat>` (`<Media-StatName>` "name:" + `<Media-StatValue>`) to `<Media-Stats>`.
         * The tooltip is `title` plus "Click to copy value to the clipboard."; clicking (or keyboard activation via
         * `keyboardClick`) copies `name: text (value)` with `ValueFormat.copyToClipboard`.
         * @param {string} name - Display name of the stat.
         * @param {string} [title] - Tooltip description.
         * @param {string} [className] - Optional category class: "Adjust" (trim), "Clip" (crop) or "Actual" (render size).
         * @returns {function(string=, *=):void} The `setText(newText, newValue)` setter of this stat.
         */
        function createStats(name, title, className) {
            const statE = document.createElement("Media-Stat");
            if (title)
                statE.title = title + "\n\nClick to copy value to the clipboard.";
            else
                statE.title = "Click to copy value to the clipboard.";
            if (className)
                statE.classList.add(className);
            p.appendChild(statE);
            const nameE = document.createElement("Media-StatName");
            nameE.innerText = name + ":";
            statE.appendChild(nameE);

            const val = document.createElement("Media-StatValue");
            val.innerText = "-";
            val.Value = 0;
            statE.appendChild(val);
            let currentText = "-";
            const statStyle = statE.classList;
            /**
             * Updates the stat. A falsy `newText` hides the chip (class `Hide`); otherwise shows it and, when the
             * text changed, sets the displayed text and stores `newValue` (or the text) in `val.Value` (used by copy).
             * @param {string} [newText] - Text to display; omit to hide the stat.
             * @param {*} [newValue] - Raw value copied together with the text.
             * @returns {void}
             */
            function setText(newText, newValue) {
                if (!newText) {
                    statStyle.add("Hide");
                    return;
                }
                statStyle.remove("Hide");
                if (newText === currentText)
                    return;
                currentText = newText;
                val.innerText = newText;
                val.Value = typeof newValue === "undefined" ? newText : newValue;
            }
            statE.onclick = ev => {
                if (badClick(ev))
                    return;
                ValueFormat.copyToClipboard(name + ": " + val.innerText + " (" + val.Value + ")");
            };
            keyboardClick(statE);
            return setText;
        }

        let width, height, gpuTime, compileTime, sVolume;
        const time = createStats("Time", "The current position in seconds");
        const duration = createStats("Duration", "The duration of the media in seconds, less than zero means unknown or infinity");
        if ((typeof player.AvgDrawTimeMs !== "undefined") && (typeof player.Query !== "undefined"))
            gpuTime = createStats("GPU time", "The GPU time spent rendering the effect in ms");
        if (typeof player.CompilationTime !== "undefined")
            compileTime = createStats("Build", "The duration of the compilation step of this effect effect in ms");
        if (player.HaveAudio)
            sVolume = createStats("Volume", "The current audio volume of the media as a percentage");
        if (player.HaveVisual) {
            width = createStats("Width", "The width in pixels of the media");
            height = createStats("Height", "The height in pixels of the media");
        }
        let adjustTime, adjustDuration;
        if (typeof props.StartAt !== "undefined") {
            adjustTime = createStats("Trimmed time", "The current position in the trimmed period in seconds", "Adjust");
            adjustDuration = createStats("Trimmed duration", "The duration of the trimmed period in seconds", "Adjust");
        }
        let clipWidth, clipHeight, clipX, clipY;
        if (player.HaveVisual && (typeof props.Crop !== "undefined")) {
            clipWidth = createStats("Cropped width", "The cropped width in pixels", "Clip");
            clipHeight = createStats("Cropped height", "The cropped height in pixels", "Clip");
            clipX = createStats("Cropped left", "The number of cropped pixels on the left", "Clip");
            clipY = createStats("Cropped top", "The number of cropped pixels on the top", "Clip");
        }
        let renderWidth, renderHeight;
        if (player.HaveVisual && (typeof props.AdaptiveSize !== "undefined")) {
            renderWidth = createStats("Render width", "The actual rendered width in pixels", "Actual");
            renderHeight = createStats("Render height", "The actual rendered height in pixels", "Actual");
        }


        /**
         * Animation-frame loop that refreshes all stat chips from the player: size, position, duration ("?" when
         * negative), true volume, GPU time (AvgDrawTimeMs, or TotalTime / MeasureCount when <= 0), build time
         * (-3 = "error", other negatives = "compiling"), trimmed time/duration (hidden unless StartAt > 0 or
         * EndAt > StartAt), crop values (hidden when nothing is cropped) and render size (only with AdaptiveSize).
         * Each read is wrapped in try/catch. Reschedules itself with requestAnimationFrame and stops once
         * `<Media-Stats>` has left the document. The document check is skipped on the first direct call (no atime)
         * and runs on every requestAnimationFrame call.
         * @param {number} [atime] - requestAnimationFrame timestamp (unused otherwise).
         * @returns {void}
         */
        function updateStats(atime) {

            if (typeof atime !== "undefined")
                if (!isInDocument(p))
                    return;
            const par = player.Params;
            if (width) {
                try {
                    const sv = player.Width;
                    width(sv + " px", sv);
                }
                catch
                {
                }
                try {
                    const sv = player.Height;
                    height(sv + " px", sv);
                }
                catch
                {
                }
            }
            try {
                const sv = player.GetPos();
                time(sv.toFixed(2) + " s",  sv);
            }
            catch
            {
            }
            try {
                const sv = player.Duration;
                duration(sv < 0 ? "?" : (sv.toFixed(2) + " s"), sv);
            }
            catch
            {
            }
            if (sVolume) {
                try {
                    const sv = player.GetTrueVolume();
                    sVolume((sv * 100).toFixed(1) + " %", sv);
                }
                catch
                {
                }
            }
            if (gpuTime) {
                try {
                    let sv = player.AvgDrawTimeMs;
                    if (sv <= 0)
                        sv = player.TotalTime / Math.max(1, player.MeasureCount);
                    gpuTime(sv.toFixed(3) + " ms", sv);
                }
                catch
                {
                }
            }
            if (compileTime) {
                try {
                    const sv = player.CompilationTime;
                    if (sv < 0) {
                        switch (sv) {
                            case -3:
                                compileTime("error");
                                break;
                            default:
                                compileTime("compiling");
                                break;
                        }
                    } else {
                        compileTime(sv.toFixed(1) + " ms", sv);
                    }
                }
                catch
                {
                }
            }

            
            if (adjustTime) {
                try {
                    const b = par.StartAt;
                    if (b > 0) {
                        const sv = player.GetPos() - b;
                        adjustTime(sv.toFixed(2) + " s", sv);
                    } else {
                        adjustTime();
                    }
                }
                catch
                {
                }
            }
            if (adjustDuration) {
                try {
                    const b = par.StartAt;
                    const e = par.EndAt;
                    if ((b > 0) || (e > b)) {
                        let ee = e > b ? e : player.Duration;
                        ee -= b;
                        adjustDuration(ee.toFixed(2) + " s", ee);
                    } else {
                        adjustDuration();
                    }
                }
                catch
                {
                }
            }
            if (clipWidth) {
                try {
                    const cw = player.ClipWidth;
                    const ch = player.ClipHeight;
                    const cx = player.ClipX;
                    const cy = player.ClipY;
                    if ((cw !== player.Width) || (ch !== player.Height) || (cx !== 0) || (cy !== 0)) {
                        clipWidth(cw + " px", cw);
                        clipHeight(ch + " px", ch);
                        clipX(cx + " px", cx);
                        clipY(cy + " px", cy);
                    } else {
                        clipWidth();
                        clipHeight();
                        clipX();
                        clipY();
                    }
                }
                catch
                {
                }
            }
            if (renderWidth) {
                try {
                    if (props.AdaptiveSize) {
                        const res = player.Element;
                        const rsw = res.width;
                        const rsh = res.height;
                        renderWidth(rsw + "px", rsw);
                        renderHeight(rsh + "px", rsh);
                    } else {
                        renderWidth();
                        renderHeight();
                    }
                }
                catch
                {

                }
            }
            requestAnimationFrame(updateStats);
        }
        updateStats();
    }
    if (!noControls) {

        const p = document.createElement("Media-Controls");
        mediaControls = p;
        target.appendChild(p);


        
        UpdateSize();


        const bs = new ButtonStyle();
        bs.ButtonElement = "Media-Button";


        const canPlay = player.CanPlay;
        if (canPlay) {

            const buttonMute = new Button(bs, "Mute", "Click to mute media", "MediaIconMute", !player.Muted, async button => {
                localStorage.setItem("QuizzWeaver.MediaMute", "true");
                await player.Mute();
                return setButtonState;
            });

            const buttonUnMute = new Button(bs, "Un mute", "Click to un-mute media", "MediaIconUnMute", player.Muted, async button => {
                localStorage.setItem("QuizzWeaver.MediaMute", "false");
                await player.UnMute();
                return setButtonState;
            });

            const buttonPause = new Button(bs, "Pause", "Click to pause media", "MediaIconPause", !player.Paused, async button => {
                await player.Pause();
                return setButtonState;
            });

            const buttonPlay = new Button(bs, "Play", "Click to play media", "MediaIconPlay", player.Paused, async button => {
                await player.Play();
                return setButtonState;
            });

            const buttonStop = new Button(bs, "Stop", "Click to stop playing media", "MediaIconStop", true, async button => {
                await player.Stop();
                return setButtonState;
            });


            const buttonLoop = new Button(bs, "Loop", "Click to loop media", "MediaIconLoop", !player.Looping, async button => {
                localStorage.setItem("QuizzWeaver.MediaLoop", "true");
                await player.Loop();
                return setButtonState;
            });

            const buttonOnce = new Button(bs, "Once", "Click to play media once then stop", "MediaIconOnce", player.Looping, async button => {
                localStorage.setItem("QuizzWeaver.MediaLoop", "false");
                await player.Once();
                return setButtonState;
            });

            const buttonVol100 = new Button(bs, "VOL: 100%", "Click to set the volume to 100%", "MediaIconVolLoud", player.Volume != 1, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "1");
                await player.SetVolume(1);
                return setButtonState;
            });

            const buttonVol75 = new Button(bs, "VOL: 75%", "Click to set the volume to 75%", "MediaIconVolLoud", player.Volume != 0.75, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.75");
                await player.SetVolume(0.75);
                return setButtonState;
            });

            const buttonVol50 = new Button(bs, "VOL: 50%", "Click to set the volume to 50%", "MediaIconVolLow", player.Volume != 0.5, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.5");
                await player.SetVolume(0.5);
                return setButtonState;
            });

            const buttonVol25 = new Button(bs, "VOL: 25%", "Click to set the volume to 25%", "MediaIconVolLow", player.Volume != 0.25, async button => {
                localStorage.setItem("QuizzWeaver.MediaVolume", "0.25");
                await player.SetVolume(0.25);
                return setButtonState;
            });

            setButtonState = () => {
                buttonMute.SetEnabled(!player.Muted);
                buttonUnMute.SetEnabled(player.Muted);
                buttonPause.SetEnabled(!player.Paused);
                buttonPlay.SetEnabled(player.Paused);
                //buttonStop.SetEnabled(!player.Paused);

                buttonLoop.SetEnabled(!player.Looping);
                buttonOnce.SetEnabled(player.Looping);


                buttonVol100.SetEnabled(player.Volume != 1);
                buttonVol75.SetEnabled(player.Volume != 0.75);
                buttonVol50.SetEnabled(player.Volume != 0.5);
                buttonVol25.SetEnabled(player.Volume != 0.25);
            };

            const haveAudio = player.HaveAudio;
            p.appendChild(buttonPause.Element);
            p.appendChild(buttonPlay.Element);
            p.appendChild(buttonStop.Element);

            p.appendChild(buttonLoop.Element);
            p.appendChild(buttonOnce.Element);

            if (haveAudio) {
                p.appendChild(buttonMute.Element);
                p.appendChild(buttonUnMute.Element);
                p.appendChild(buttonVol100.Element);
                p.appendChild(buttonVol75.Element);
                p.appendChild(buttonVol50.Element);
                p.appendChild(buttonVol25.Element);
            }
        }
        const buttonShow = new Button(bs, "Source", "Click to open the source in a new tab", "MediaIconExpand", true, async button => {
            EditOrOpenUrl(player.Source);
        });
        p.appendChild(buttonShow.Element);

        player.MessageElement = p;
        p.addEventListener("mediaEnded", ev => setButtonState());
    }
    player.SetButtonState = setButtonState;
    player.UpdateSize = UpdateSize;
    player.TopElement = p;

    return [player, mediaControls, mediaStats];
}
/**
 * Applies the stored user preferences and starts playback, asking for a click when audio can't be un-muted.
 * - Loop: `localStorage["QuizzWeaver.MediaLoop"] === "true"` -> `Loop()`, else `Once()`.
 * - Volume: `QuizzWeaver.MediaVolume` (default "0.25") -> `SetVolume` (not awaited).
 * - No audio: plays (if `CanPlay`) and returns.
 * - `MediaPlayerTools.CanUnMute()`: un-mutes unless `QuizzWeaver.MediaMute` is "true", then plays.
 * - Un-mute blocked but the user prefers mute: plays muted.
 * - Otherwise appends a full-screen `<Media-NoAudio>` ("Must click once to get audio!") to the body; its click
 *   un-mutes, plays, refreshes the buttons and removes the overlay (playback does not start before that click).
 * Calls `player.SetButtonState()` after starting playback.
 * @param {object} player - Player returned by `mediaCreatePlayer` (needs `SetButtonState`).
 * @returns {Promise<void>}
 */
async function mediaPlayWithPopup(player) {
    if (localStorage.getItem("QuizzWeaver.MediaLoop") !== "true")
        await player.Once();
    else
        await player.Loop();
    player.SetVolume(parseFloat(localStorage.getItem("QuizzWeaver.MediaVolume") ?? "0.25"));
    if (!player.HaveAudio) {
        if (player.CanPlay)
            await player.Play();
        player.SetButtonState();
        return;
    }
    if (await MediaPlayerTools.CanUnMute()) {
        if (localStorage.getItem("QuizzWeaver.MediaMute") !== "true")
            await player.UnMute();
        await player.Play();
        player.SetButtonState();
        return;
    }
    if (localStorage.getItem("QuizzWeaver.MediaMute") === "true") {
        await player.Play();
        player.SetButtonState();
        return;
    }
    const e = document.createElement("Media-NoAudio");
    e.innerText = "Must click once to get audio!";
    e.title = "Click anywhere!";
    e.onclick = async ev => {
        if (badClick(ev))
            return;
        await player.UnMute();
        await player.Play();
        player.SetButtonState();
        e.remove();
    }
    document.body.appendChild(e);
}
/**
 * Appends a property editor for the media params below the player.
 *
 * Always appends `<Media-Edit>` to `target`; the editor itself is only created when `props` has more than one key.
 * Then: forces `props.Crop.VisualizeMargins = false`; fetches the type info with
 * `sendRequest('../edit/GetTypeInfo', props["$type"], false)`; for effect players (`player.ScriptTypeInfo`) registers
 * the script type with `Edit.AddType`, copies `player.Params.EffectProps` into `props.EffectProps` and adds
 * `player.ScriptMember` (the shader's `//var:` constants) to the members; creates an `Edit` (ReadOnly = !canEdit,
 * no title, Dev only if canEdit, with an UndoStack) bound to `props`.
 *
 * When editable, adds a `<SysWeaver-CenterBlock>` with "Save changes" (IconSave, only if `saveFn`) and
 * "Apply changes" (IconApply) buttons, and listens for the Edit `EditChange` event:
 * - Live (no reload): Volume (`ResetVolume`, posts SetVolume), Speed (posts SetProps), AdaptiveSize (`UpdateSize`,
 *   posts UpdateSize), Crop (`UseClip`/`ApplyClip`/`UpdateSize`, posts Crop), EffectProps (`LazyCompile`, mirrors
 *   `props.FxProps = JSON.stringify(EffectProps)`, posts Compile; compile errors are logged).
 * - Any other member enables "Apply changes" while `GetApplyState()` differs from the applied state.
 * - "Save changes" is enabled while `JSON.stringify(props)` differs from the initial object state.
 * Messages are posted as `InterOp.Post(messagePrefix + <Name>, ...)` only when `messagePrefix` is given;
 * Save and Apply also post `<prefix>Reload` with `props`.
 *
 * @param {HTMLElement} target - Container (same one used for the player).
 * @param {Array} playerRes - `[player, mediaControls, mediaStats]` as returned by `mediaCreatePlayer`.
 * @param {number} type - Media type, reused to rebuild the player.
 * @param {string} data - Media data/URL, reused to rebuild the player.
 * @param {?object} props - The media params object to edit (modified in place).
 * @param {boolean} canEdit - False makes the editor read-only and adds no buttons.
 * @param {function(function():Promise<void>):Promise<boolean>} [saveFn] - Async save callback; receives the inner
 *   `reload` function and returns truthy on success.
 * @param {string} [messagePrefix] - Prefix for InterOp messages to a remote `MediaView.html` (its `ctrl` param).
 * @returns {Promise<void>}
 */
async function mediaEditProps(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix) {
    let player = playerRes[0];
    const editE = document.createElement("Media-Edit");
    target.appendChild(editE);
    let mediaStats = playerRes[2];
    let mediaControls = playerRes[1];
/*    if (mediaStats)
        editE.appendChild(mediaStats);
    if (mediaControls)
        editE.appendChild(mediaControls);
*/

    if (props) {
        if (Object.keys(props).length > 1) {
            if (props.Crop)
                props.Crop.VisualizeMargins = false;

            /**
             * Returns `JSON.stringify(props)` with `Volume` temporarily removed (volume changes never need Apply).
             * Compared with the last applied state to decide whether "Apply changes" is enabled.
             * Side effect: `Volume` is re-added, so it moves to the end of the key order.
             * @returns {string} The serialized apply-relevant state.
             */
            function GetApplyState() {

                const oldVol = props.Volume;
                delete props.Volume;
                const ret = JSON.stringify(props);
                if (typeof oldVol !== "undefined")
                    props.Volume = oldVol;
                return ret;
            }


//            editE.appendChild(document.createElement("br"));
//            editE.appendChild(document.createElement("br"));
            const typeInfo = await sendRequest('../edit/GetTypeInfo', props["$type"], false);
            if (player.ScriptTypeInfo) {
                Edit.AddType(player.ScriptTypeInfo);
                props.EffectProps = player.Params.EffectProps;
                typeInfo.Members.push(player.ScriptMember);
            }

            //const canEdit = (info.Edit & QuizzRights.UpdatePrivateData) !== 0;
            const options = new EditOptions();
            options.ReadOnly = !canEdit;
            options.Title = false;
            options.Dev = canEdit & options.Dev;
            options.UndoStack = new UndoStack();

            const input = new Edit(typeInfo, null, options);
            await input.SetObject(props);
            editE.appendChild(input.Element);
            if (canEdit) {
                const commands = document.createElement("SysWeaver-CenterBlock");
                input.Element.appendChild(commands);


                let applyButton = null;

                /**
                 * Rebuilds the player with the current `props` (used by Apply and passed to `saveFn`).
                 * Disables Apply; calls `mediaCreatePlayer(target, type, data, props, !mediaControls, oldTopElement,
                 * !mediaStats)` (default fit/aspect); swaps the new controls/stats elements in place of the old ones;
                 * restores OnEnded, volume, un-mute (only if it was un-muted), Once (only if it was not looping),
                 * Play (if it was playing) and position; removes the old `<Media-Preview>`, removes `Replacing` from the
                 * new one, awaits `oldP.OnDispose()`, refreshes the buttons and resets the applied state.
                 * @returns {Promise<void>}
                 */
                async function reload() {

                    applyButton.Disable();
                    const oldP = player;
                    const pVolume = player.Volume;
                    const pMuted = player.Muted;
                    const pPaused = player.Paused;
                    const pLooping = player.Looping;
                    const pPos = player.GetPos();
                    const orgE = player.OnEnded;
                    const prevE = player.TopElement;
                    const pd = await mediaCreatePlayer(target, type, data, props, !mediaControls, prevE, !mediaStats);
                    player = pd[0];
                    if (pd[1])
                        mediaControls.parentElement.replaceChild(pd[1], mediaControls);
                    if (pd[2])
                        mediaStats.parentElement.replaceChild(pd[2], mediaStats);
                    mediaControls = pd[1];
                    mediaStats = pd[2];
                    player.OnEnded = orgE;

                    await player.SetVolume(pVolume);
                    if (!pMuted)
                        await player.UnMute();
                    if (!pLooping)
                        await player.Once();
                    if (!pPaused)
                        await player.Play();
                    if (pPos > 0)
                        await player.Seek(pPos);

                    oldP.TopElement.remove();

                    player.TopElement.classList.remove("Replacing");
                    await oldP.OnDispose();


                    player.SetButtonState();
                    applyState = GetApplyState();
                };



                let objState = JSON.stringify(input.GetObject());
                let saveButton = null;
                if (saveFn) {
                    saveButton = new Button(null, "Save changes", "Click to save changes", "IconSave", false, async () => {
                        saveButton.Disable();
                        saveButton.StartWorking("IconWorking");
                        try {
                            if (await saveFn(reload)) {
                                if (messagePrefix)
                                    InterOp.Post(messagePrefix + "Reload", props);
                                saveButton.StopWorking();
                                Info("Saved updates!");
                                return;
                            }
                            Fail("Failed to update!");
                        }
                        catch (e) {
                            Fail("Failed to update!\n" + e);
                        }
                        saveButton.StopWorking();
                        saveButton.Enable();
                    });
                    commands.appendChild(saveButton.Element);
                }

                let applyState = GetApplyState();
                applyButton = new Button(null, "Apply changes", "Apply the changes that can't be applied on the fly.\nWithout saving.", "IconApply", false, async () => {
                    applyButton.StartWorking("IconWorking");
                    await reload();
                    if (messagePrefix)
                        InterOp.Post(messagePrefix + "Reload", props);
                    applyButton.StopWorking();
                });
                commands.appendChild(applyButton.Element);



                let clip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                input.Element.addEventListener("EditChange", async ev => {
                    let needApply = true;
                    const mn = ev.detail.Member.Name;
                    if (mn === "Volume") {
                        player.Params.Volume = props.Volume;
                        await player.ResetVolume();
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "SetVolume", { Volume: props.Volume });
                        needApply = false;
                    }
                    if (mn === "Speed") {
                        player.Params.Speed = props.Speed;
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "SetProps", { Speed: props.Speed });
                        needApply = false;
                    }
                    if (mn === "AdaptiveSize") {
                        player.UpdateSize();
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "UpdateSize", { AdaptiveSize: props.AdaptiveSize });
                        needApply = false;
                    }
                    if (mn === "Crop") {
                        const newClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                        if (newClip !== clip) {
                            clip = newClip;
                            player.UseClip = clip;
                            player.ApplyClip(props);
                            player.UpdateSize();
                        } else {
                            player.ApplyClip(props);
                        }
                        if (messagePrefix)
                            InterOp.Post(messagePrefix + "Crop", { Crop: props.Crop });
                        needApply = false;
                    }
                    if (mn === "EffectProps"){
                        try {
                            player.Params.EffectProps = props.EffectProps;
                            player.LazyCompile();
                            needApply = false;
                            props.FxProps = JSON.stringify(props.EffectProps);
                            if (messagePrefix)
                                InterOp.Post(messagePrefix + "Compile", { EffectProps: props.EffectProps });
                        }
                        catch (e)
                        {
                            console.warn("Failed to compile, error: \n" + e);
                        }
                    }
                    if (saveButton)
                        saveButton.SetEnabled(JSON.stringify(props) !== objState);
                    const newApplyState = GetApplyState();
                    if (needApply)
                        applyButton.SetEnabled(newApplyState !== applyState);
                    else
                        applyState = newApplyState;
                });


            }

        }
    }

}
/**
 * Boot function of the embeddable "assignment media editor" (used by an external host page, not by this folder).
 *
 * Appends `<SysWeaver-Page class="Media">` (fixed background) and `<Media-Full>` (editor container) to the body.
 * URL params: `get` (API that returns the media info), `aid` (JSON assignment id: a number or an object; required),
 * `update` (save API name; when missing it is derived from `get`: "MediaGetAssignedInfo" -> "MediaSet...Data",
 * otherwise "Get" -> "Update").
 * Loads `{Data, Type, Link, Edit}` with `sendRequest(getApi, aid)`, assigns the implicit global `props = info.Data`,
 * creates the player (default fit/aspect), wraps `player.OnLoop`/`player.OnEnded` with console logging, and adds a
 * `<SysWeaver-CenterBlock>` with a Collapse/Expand button (inner `toggleCollape`) and a "New window" button that
 * collapses the player and opens `MediaView.html?type=&link=&props=&ctrl=<messagePrefix>` in a new tab.
 * `messagePrefix = YaMD5.hashAsciiStr(getApi + "_" + aid + "_" + type + "_" + data)`.
 * Calls `mediaEditProps` with `canEdit = (info.Edit & QuizzRights.UpdatePrivateData) !== 0` and a save callback that
 * posts `{AssignmentId: aid}` (number aid) or the aid object, plus `Data = props`, to `'../quizz/' + updateApi`;
 * on success it sets the implicit global `objState` and reloads the player.
 * Plays with `mediaPlayWithPopup`, answers `<prefix>GetProps` InterOp messages with `<prefix>SetProps` (a copy of
 * props) and posts an initial `<prefix>Reload`. On error clears the container and calls `Fail`; otherwise `PageLoaded()`.
 * @returns {Promise<void>}
 */
async function mediaEditMain() {
    let target = document.body;

    const pageBack = document.createElement("SysWeaver-Page");
    pageBack.classList.add("Media");
    target.appendChild(pageBack);
    const page = document.createElement("Media-Full");
    target.appendChild(page);
    target = page;
    try {
        const ps = getUrlParams();
        const getApi = ps.get('get');
        const aidT = ps.get('aid');
        if (!aidT) {
            Fail("No assignment id parameter 'aid' supplied!", 400);
            return;
        }
        const aid = JSON.parse(aidT);
        let updateApi = ps.get('update');
        if (!updateApi) {
            updateApi = getApi.replace("MediaGetAssignedInfo", "MediaSet");
            if (updateApi != getApi) {
                updateApi += "Data";
            } else {
                updateApi = updateApi.replace("Get", "Update");
            }
        }


        const info = await sendRequest(getApi, aid);
        props = info.Data;
        const type = info.Type;
        const data = info.Link;



        const pd = await mediaCreatePlayer(target, type, data, props);
        let player = pd[0];
        if (!props)
            props = JSON.parse(JSON.stringify(player.Params));
        /**
         * Player callback on loop: logs "Loop" to the console.
         * @returns {void}
         */
        player.OnLoop = () => console.log("Loop");
        const orgE = player.OnEnded;
        /**
         * Player callback on end: logs "End" and then calls the original OnEnded handler (if any).
         * @returns {*} The result of the original handler.
         */
        player.OnEnded = () => {
            console.log("End");
            if (orgE)
                return orgE();
        };
        //  Collapse expand
        const exp = document.createElement("SysWeaver-CenterBlock");
        target.appendChild(exp);
        let collapseButton;
        const pcl = page.classList;
        let didPlay;
        /**
         * Toggles class `Hide` on `<Media-Full>` (hides player, stats and controls via media.css).
         * Hiding remembers whether the player was playing and pauses it; showing resumes playback if it was playing.
         * Updates the collapse button text, title and icon (IconMediaCollapse / IconMediaExpand).
         * @returns {void}
         */
        function toggleCollape() {
            if (pcl.contains("Hide")) {
                pcl.remove("Hide");
                collapseButton.ChangeText("Collapse");
                collapseButton.ChangeTitle("Click to hide the media player");
                collapseButton.ChangeImage("IconMediaCollapse");
                if (didPlay)
                    player.Play();
            } else {
                didPlay = !player.Paused;
                player.Pause();
                pcl.add("Hide");
                collapseButton.ChangeText("Expand");
                collapseButton.ChangeTitle("Click to show the media player");
                collapseButton.ChangeImage("IconMediaExpand");
            }
        };
        collapseButton = new Button(null, "Collapse", "Click to hide the media player", "IconMediaCollapse", true, toggleCollape);
        exp.appendChild(collapseButton.Element);

        const messagePrefix = YaMD5.hashAsciiStr("" + getApi + "_" + aid + "_" + type + "_" + data);
        const newWindowButton = new Button(null, "New window", "Click to open the media player in a new window", "IconMediaNewWindow", true, () => {
            if (!pcl.contains("Hide"))
                toggleCollape();
            Open("MediaView.html?type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + JSON.stringify(props) + "&ctrl=" + messagePrefix, "_blank");

        });
        exp.appendChild(newWindowButton.Element);

        //  Props
        const canEdit = (info.Edit & QuizzRights.UpdatePrivateData) !== 0;
        await mediaEditProps(target, pd, type, data, props, canEdit, async reload =>
        {
            const r = {};
            if (typeof (aid) === "number")
                r.AssignmentId = aid;
            else
                Object.assign(r, aid);
            r.Data = props;
            if (await sendRequest('../quizz/' + updateApi, r, false)) {
                objState = JSON.stringify(props);
                await reload();
                return true;
            }
            return false;
        }, messagePrefix);
    //  Play
        await mediaPlayWithPopup(player);

        if (messagePrefix) {
            InterOp.AddListener(ev => {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const name = type.substring(messagePrefix.length);
                if (name === "GetProps") {
                    const dm = {};
                    Object.assign(dm, props);
                    InterOp.Post(messagePrefix + "SetProps", dm);
                }
            });
            {
                const dm = {};
                Object.assign(dm, props);
                InterOp.Post(messagePrefix + "Reload", dm);
            }   

        }

    }
    catch (e) {
        target.innerText = "";
        Fail("Generic failure.\n" + e);
        return;
    }
    PageLoaded();
}

/**
 * Debug stand-in for the native `ScreenShotHost` host object (CefSharp / WebView2) used by `mediaPreviewMain`.
 * Instantiated there when the URL has the `hack` parameter and no real host exists; every method only logs its call.
 */
class SsHack {
    /**
     * Logs "isSupported()".
     * @returns {void}
     */
    isSupported() {
        console.log("isSupported()");
    }

    /**
     * Logs the requested window size (a real host resizes the window).
     * @param {number} width - Requested width in px.
     * @param {number} height - Requested height in px.
     * @returns {void}
     */
    setSize(width, height) {
        console.log("setSize(" + width + ", " + height + ")");
    }

    /**
     * Logs the final report (a real host takes the screenshot / stores the measurements).
     * @param {number} duration - Media duration in seconds.
     * @param {number} fps - Measured frames per second (0 if unknown).
     * @param {?string} error - Error message, or null on success.
     * @param {number} loadDuration - Compilation/load time in ms (player.CompilationTime).
     * @returns {void}
     */
    doIt(duration, fps, error, loadDuration) {
        console.log("doIt(" + duration + ", " + fps + ", " + error + ", " + loadDuration + ")");

    }
}

/**
 * Guesses the media type from the file extension of a URL (common.js isImageExt/isVideoExt/isAudioExt).
 * @param {string} url - Media URL.
 * @returns {number} `MediaTypes.Image`, `MediaTypes.Video`, `MediaTypes.Audio`, `MediaTypes.Effect` (".glsl"), or -1.
 */
function mediaGetTypeFromUrl(url) {
    const ext = getFileExtension(url).toLowerCase();
    if (isImageExt(ext))
        return MediaTypes.Image;
    if (isVideoExt(ext))
        return MediaTypes.Video;
    if (isAudioExt(ext))
        return MediaTypes.Audio;
    if (ext === "glsl")
        return MediaTypes.Effect;
    return -1;
}



/**
 * Boot function of `MediaView.html` (called from `<body onload>`).
 * URL params: `link`/`d` (required), `type`/`t` (else guessed with `mediaGetTypeFromUrl`), `props` (JSON),
 * `no_stats`, `no_controls`, `ctrl` (InterOp message prefix of a controlling editor, see mediaEditMain).
 * With `ctrl`, first asks the opener for the current props: posts `<ctrl>GetProps` and waits up to 100 ms for the
 * `MediaGotProps` event raised by the inner `getPropsFn` on `<ctrl>SetProps`.
 * Creates `<Media-View>` (3-row grid) and a player with fit = false, automatic aspect (0:0) and centered alignment;
 * clicking the media (inner `showFull`) navigates to `MediaPreview.html?full&back&pos=...` in the same tab.
 * Wraps OnLoop/OnEnded with console logging. With `ctrl`, listens for `<ctrl>*` InterOp messages, copies message
 * fields that already exist in `props` into it, and handles Reload (inner `reload`), SetVolume (`ResetVolume`),
 * UpdateSize, Crop (UseClip + ApplyClip + UpdateSize) and Compile (`LazyCompile`).
 * Plays with `mediaPlayWithPopup`, then `PageLoaded()`; on error clears the body and calls `Fail`.
 * @returns {Promise<void>}
 */
async function mediaViewMain() {
    const target = document.body;
    try {
        const ps = getUrlParams();
        const stats = !ps.has('no_stats');
        const controls = !ps.has('no_controls');

        const data = ps.get('link') ?? ps.get('d');
        if (!data) {
            Fail("No link parameter 'link' supplied!");
            return;
        }
        //  Use the file name as title (like edit/text.js)
        {
            const path = data.split('?')[0];
            const fname = path.substring(path.lastIndexOf('/') + 1);
            if (fname)
                document.title = fname;
        }

        let type = ps.get('type') ?? ps.get('t');
        if (!type) {
            type = mediaGetTypeFromUrl(data);
            if (type < 0) {
                Fail("No type parameter 'type' supplied!");
                return;
            }
        } else {
            type = parseInt(type);
        }


        let props = ps.get('props');
        if (props) {
            props = JSON.parse(props);
        } else {
            props = null;
        }

        const messagePrefix = ps.get('ctrl');

        if (messagePrefix) {
            let newProps = null;
            /**
             * Start-up InterOp listener: on `<prefix>SetProps` copies the message (minus Type, From, MagicXyz) into
             * `newProps` and dispatches a `MediaGotProps` event on the body (ends the wait).
             * @param {MessageEvent} ev - The InterOp message event.
             * @returns {void}
             */
            function getPropsFn(ev) {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const name = type.substring(messagePrefix.length);
                if (name !== "SetProps")
                    return;
                newProps = {};
                Object.assign(newProps, m);
                delete newProps.Type;
                delete newProps.From;
                delete newProps.MagicXyz;
                target.dispatchEvent(new Event("MediaGotProps"));
            }
            InterOp.AddListener(getPropsFn);
            await waitEvent(target, "MediaGotProps", () => InterOp.Post(messagePrefix + "GetProps"), 100);
            InterOp.RemoveListener(getPropsFn);
            if (newProps)
                props = newProps;
        }

        const mview = document.createElement("Media-View");
        target.appendChild(mview);
/*
        target.onclick = ev => {
            const tw = target.clientWidth;
            const th = target.clientHeight;

            let size = Math.min(tw, th) * 0.1;
            if (size < 16)
                size = 16;
            if (size > 96)
                size = 96;
            if (ev.clientY >= size)
                return;
            if (ev.clientX < (tw - size))
                return;
            Open("MediaPreview.html?type=" + type + "&link=" + data + "&props=" + JSON.stringify(player.Params), "_self");
        };
*/
        const pd = await mediaCreatePlayer(mview, type, data, props, !controls, null, !stats, false, 0, 0, 0.5, 0.5);
        let player = pd[0];

        /**
         * Opens `MediaPreview.html?full&back&pos=<current pos>&type=&link=&props=<player.Params JSON>` in the same tab
         * (full-screen preview; a click there goes back). Set as click handler of the player's parent (the clip box).
         * @returns {void}
         */
        const showFull = () => Open("MediaPreview.html?full&back&pos=" + player.GetPos() + "&type=" + type + "&link=" + encodeURIComponent(data) + "&props=" + JSON.stringify(player.Params), "_self");

        player.Element.parentElement.onclick = showFull;

        let mediaControls = null;
        if (pd[1])
            mediaControls = pd[1];
        let mediaStats = null;
        if (pd[2])
            mediaStats = pd[2];
        props = player.Params;

        /**
         * Player callback on loop: logs "Loop" to the console.
         * @returns {void}
         */
        player.OnLoop = () => console.log("Loop");
        const orgE = player.OnEnded;
        /**
         * Player callback on end: logs "End" and then calls the original OnEnded handler (if any).
         * @returns {*} The result of the original handler.
         */
        player.OnEnded = () => {
            console.log("End");
            if (orgE)
                return orgE();
        };
        /**
         * Rebuilds the player with the current `props` (triggered by a `<prefix>Reload` message); same procedure as
         * the `reload` of mediaEditProps (keeps volume, un-mute, once, play state and position), re-attaches
         * `showFull` and sets `props` to the new `player.Params`.
         * @returns {Promise<void>}
         */
        async function reload() {

            const oldP = player;
            const pVolume = player.Volume;
            const pMuted = player.Muted;
            const pPaused = player.Paused;
            const pLooping = player.Looping;
            const pPos = player.GetPos();
            const orgE = player.OnEnded;
            const prevE = player.TopElement;
            const pd = await mediaCreatePlayer(mview, type, data, props, !controls, prevE, !stats, false, 0, 0, 0.5, 0.5);
            player = pd[0];
            player.Element.parentElement.onclick = showFull;
            if (pd[1])
                mediaControls.parentElement.replaceChild(pd[1], mediaControls);
            if (pd[2])
                mediaStats.parentElement.replaceChild(pd[2], mediaStats);
            mediaControls = pd[1];
            mediaStats = pd[2];
            player.OnEnded = orgE;
            props = player.Params;

            await player.SetVolume(pVolume);
            if (!pMuted)
                await player.UnMute();
            if (!pLooping)
                await player.Once();
            if (!pPaused)
                await player.Play();
            if (pPos > 0)
                await player.Seek(pPos);

            oldP.TopElement.remove();

            player.TopElement.classList.remove("Replacing");
            await oldP.OnDispose();


            player.SetButtonState();
        };



        if (messagePrefix) {
            InterOp.AddListener(ev => {
                const m = InterOp.GetMessage(ev);
                const type = m.Type;
                if (!type)
                    return;
                if (!type.startsWith(messagePrefix))
                    return;
                const clip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                Object.getOwnPropertyNames(m).forEach(pn => {
                    if (typeof props[pn] === "undefined")
                        return;
                    props[pn] = m[pn];
                });
                const name = type.substring(messagePrefix.length);
                if (name === "Reload")
                    reload();
                if (name === "SetVolume")
                    player.ResetVolume();
                if (name === "UpdateSize")
                    player.UpdateSize();

                if (name === "Crop") {
                    player.UseClip = props.Crop ? (!props.Crop.VisualizeMargins) : true;
                    player.ApplyClip(props);
                    player.UpdateSize();
                }
                if (name === "Compile")
                    player.LazyCompile();
            });
        }
        await mediaPlayWithPopup(player);


    }
    catch (e) {
        target.innerText = "";
        Fail("Generic failure.\n" + e);
        return;
    }
    PageLoaded();
}

/**
 * Boot function of `MediaPreview.html`: a bare player that fills the window, also used to take screenshots.
 * Adds class `MediaPreview` to the body and binds CefSharp objects when CefSharp is present.
 * Screenshot host: `window.ScreenShotHost` ?? WebView2 host object (inner `GetHo`) ?? `new SsHack()` when `hack` is
 * set. With a host: awaits `isSupported()`, stores it as `window.ScreenShotHost`, adds class `NoFade` and uses fill mode.
 * URL params: `link`/`d` (required), `type`/`t` (else guessed), `pos` (seek position, s), `props` (JSON), `fill`,
 * `full` (enter FullScreen; re-entered on the first key/click), `back` (a click calls `history.go(-1)`, suppressed
 * while full screen or audio still has to be enabled by the first interaction).
 * Playback: creates the player with `MediaPlayer.Create`, appends its element to the body, `Cache(true)`, sizes it
 * with `MediaPlayerTools.UpdateSize` under a ResizeObserver (inner `updateSize`), seeks, un-mutes when allowed
 * (else on the first interaction), waits 50 ms, resizes the host window (`shost.setSize`) to the clipped size (or to
 * the aspect ratio with AdaptiveSize) and waits for `resize`, then `Play()`, `Show()` and
 * `MediaPlayerTools.SetupMediaMessages(player)` (window message commands like MediaPlay/MediaPause/MediaSeek).
 * Screenshot report: after playback starts, calls `ResetCounters()` (if available) and measures for 600 ms,
 * pauses, seeks back to `pos` and calls `shost.doIt(duration, fps, null, compilationTime)` with
 * fps = 1000 * MeasureCount / TotalTime. On a playback error `doIt(0, 0, message, 0)` is called and `Fail` shown;
 * the final report block is then skipped, so `doIt` is called exactly once.
 * On an outer error the body text is replaced with "Generic failure."; otherwise `PageLoaded()` is called.
 * @returns {Promise<void>}
 */
async function mediaPreviewMain() {
    const target = document.body;
    try {
        const ps = getUrlParams();
        target.classList.add("MediaPreview");
        if (window["CefSharp"])
            await CefSharp.BindObjectAsync();
        /**
         * Returns the WebView2 host object `chrome.webview.hostObjects.ScreenShotHost`, or null when absent
         * (also when accessing it throws).
         * @returns {?object} The screenshot host proxy or null.
         */
        function GetHo() {
            try {
                return chrome?.webview?.hostObjects?.ScreenShotHost ?? null;
            }
            catch
            {
                return null;
            }
        }

        const shost = window["ScreenShotHost"] ?? GetHo() ?? (ps.has("hack") ? new SsHack() : null);
        if (shost) {
            await shost.isSupported();
            window["ScreenShotHost"] = shost;
            target.classList.add("NoFade");
        }
        const fill = ps.has('fill') || shost;
        const full = ps.has('full');
        const back = ps.has('back');

        const data = ps.get('link') ?? ps.get('d');
        if (!data) {
            Fail("No link parameter 'link' supplied!");
            return;
        }

        let type = ps.get('type') ?? ps.get('t');
        if (!type) {
            type = mediaGetTypeFromUrl(data);
            if (type < 0) {
                Fail("No type parameter 'type' supplied!");
                return;
            }
        } else {
            type = parseInt(type);
        }

        let pos = ps.get("pos")
        pos = pos ? parseFloat(pos) : 0;
        let props = ps.get('props');
        if (props) {
            props = JSON.parse(props);
        } else {
            props = null;
        }
        let enabledFull = true;
        if (full) {
            await FullScreen.Enter();
            /**
             * One-shot keyup/click handler (full screen mode): removes itself, re-enters full screen (entering may
             * require a user gesture) and re-enables `back` navigation after 100 ms.
             * @returns {Promise<void>}
             */
            const onInteraction = async () => {
                target.removeEventListener("keyup", onInteraction);
                target.removeEventListener("click", onInteraction);
                await FullScreen.Enter();
                setTimeout(() => enabledFull = true, 100);
            }
            enabledFull = FullScreen.IsFull();
            target.addEventListener("keyup", onInteraction);
            target.addEventListener("click", onInteraction);
        }
        let enabledAudio = true;
        if (back) {
            target.addEventListener("click", () => {
                if (!enabledAudio)
                    return;
                if (!enabledFull)
                    return;
                history.go(-1);
            });
        }

        const player = await MediaPlayer.Create(type, data, props);
        const e = player.Element;
        e.draggable = false;
        const es = e.style;
        target.appendChild(e);
        let failed = false;
        try {
            await player.Cache(true);
            const pw = player.Width;
            const ph = player.Height;
            const adapt = !!player.Params.AdaptiveSize;
            /**
             * Sizes the player to the body with `MediaPlayerTools.UpdateSize(target, player, fill)`.
             * @returns {Array<number>} The clipped media size `[w, h]` (used to resize the screenshot window).
             */
            function updateSize() {
                return MediaPlayerTools.UpdateSize(target, player, fill);
            }
            new ResizeObserver(updateSize).observe(target);
            if (pos > 0)
                await player.Seek(pos);
            if (player.HaveAudio) {
                if (await MediaPlayerTools.CanUnMute()) {
                    await player.UnMute();
                } else {
                    enabledAudio = false;
                    /**
                     * One-shot keyup/click handler (un-mute blocked by the browser): removes itself, un-mutes the
                     * player and re-enables `back` navigation after 100 ms.
                     * @returns {void}
                     */
                    const onInteraction = () => {
                        target.removeEventListener("keyup", onInteraction);
                        target.removeEventListener("click", onInteraction);
                        player.UnMute();
                        setTimeout(() => enabledAudio = true, 100);
                    }
                    target.addEventListener("keyup", onInteraction);
                    target.addEventListener("click", onInteraction);
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
                            await waitEvent(window, "resize", async () => {
                                await shost.setSize(aw, window.innerHeight);
                            }, 1000);
                            await MediaPlayerTools.delay(200);
                        }
                    }
                } else {
                    if ((window.innerWidth !== clippedSize[0]) || (window.innerHeight !== clippedSize[1])) {
                        await waitEvent(window, "resize", async () => {
                            await shost.setSize(clippedSize[0], clippedSize[1]);
                        }, 1000);
                        await MediaPlayerTools.delay(200);
                    }
                }
            }
            await player.Play();
            await player.Show();
            MediaPlayerTools.SetupMediaMessages(player);
        }
        catch (e) {
            failed = true;
            if (shost)
                await shost.doIt(0, 0, "" + e, 0);
            Fail(e);
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
            let fps = tot > 0 ? (1000.0 * count / tot) : 0;
            await shost.doIt(player.Duration ?? 0, fps, null, player.CompilationTime ?? 0);
        }

    }
    catch (e) {
        target.innerText = "Generic failure.\n" + e;
        return;
    }
    

    PageLoaded();
}

