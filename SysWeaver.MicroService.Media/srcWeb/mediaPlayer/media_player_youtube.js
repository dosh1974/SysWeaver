
/**
 * @file media_player_youtube.js - YouTube player driving a youtube-nocookie.com embed iframe through the raw
 * YouTube IFrame postMessage protocol (no YouTube JS API library is loaded).
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.YouTube; data is the YouTube video id).
 * Globals defined: MediaPlayerParamsYouTube, MediaPlayerYoutube.
 * Dependencies: MediaPlayerTools (media_player_tools.js: InitBase, MustBeAttached, OnCacheComplete, ApplyClip
 * default) and the common.js globals createIFrame, waitEvent2 and delay.
 * Protocol: outgoing JSON strings {"event":"listening",...} and {"event":"command","func":...,"args":...} are
 * posted to the iframe's contentWindow (target "*"); incoming window "message" events with a matching "id" are
 * re-dispatched on the iframe as DOM CustomEvents "YT" + event (YTonReady, YTinfoDelivery, YTonError, ...).
 * The synthetic Event "YTInitCompleted" signals that the initial cue (paused at StartAt) is done.
 */

/**
 * Parameters of a YouTube player; mirrors the C# type MediaYouTube (SysWeaver.MicroService.Media.MediaYouTube).
 * The field initializers are the defaults; MediaPlayerYoutube merges the supplied params over a new instance
 * (Object.assign, so a supplied Crop replaces the default Crop object entirely).
 */
class MediaPlayerParamsYouTube {

    /**
     * Pixel width of the iframe (native player width); falls back to 1280 when falsy. Ignored (0) for audio-only.
     * @type {number}
     */
    Width = 1920;
    /**
     * Pixel height of the iframe; falls back to Width * 9 / 16 when falsy. Ignored (0) for audio-only.
     * The square 1920x1920 default is cropped to 16:9 by the default Crop.
     * @type {number}
     */
    Height = 1920;
    /**
     * Start of the play window in seconds (the initial cue, Stop and loops return here).
     * @type {number}
     */
    StartAt = 0;
    /**
     * End of the play window in seconds; 0, a value <= StartAt or a value > the clip duration means the clip end.
     * @type {number}
     */
    EndAt = 0;
    /**
     * Volume in percent (0..100); YouTube's setVolume receives Volume(player, 0..1) * Params.Volume.
     * @type {number}
     */
    Volume = 100;
    /**
     * Which streams to use: 0 = video + audio, 1 = video only (HaveAudio = false), 2 = audio only
     * (HaveVisual = false, 0x0 iframe with opacity 0).
     * @type {number}
     */
    Stream = 0;
    /**
     * Crop (C# MediaCrop, see MediaPlayerTools.ComputeClip). Default: 21.875 % top and bottom, in percent mode,
     * which crops the square iframe to 16:9 (1 - 2 * 0.21875 = 0.5625 = 9/16).
     * @type {{MarginAsPercentage:boolean, MarginTopP:number, MarginRightP:number, MarginBottomP:number, MarginLeftP:number}}
     */
    Crop = {
        "MarginAsPercentage": true,
        "MarginTopP": 21.875,
        "MarginRightP": 0,
        "MarginBottomP": 21.875,
        "MarginLeftP": 0,
    }
}

/**
 * YouTube player. Created by MediaPlayer.Create(MediaTypes.YouTube, code, params).
 * Lifecycle: constructor (iframe created, nothing loaded) -> Cache (loads the embed, connects, cues StartAt
 * paused) -> Play/Pause/Seek/... -> OnDispose (wrapped in Cache to remove the window message listener, then the
 * InitBase default which calls Stop). Loop/Once are the InitBase defaults (they only set Looping, which the
 * message handler reads).
 * Instance fields: Params (MediaPlayerParamsYouTube), Code (video id), InstanceId (postMessage "id"),
 * Source (watch URL), CurrentTime (last time reported by infoDelivery), PauseGreaterThan (pending "pause once
 * past this time" seek target, -1 = none), PlayerVolume (last volume sent), AtStart/AtEnd (window flags),
 * done (set false, otherwise unused), WaitCommand (set in Cache). Other state comes from MediaPlayerTools.InitBase.
 */
class MediaPlayerYoutube {
    /**
     * Creates the player and its iframe (common.js createIFrame) with scrolling="no",
     * sandbox="allow-same-origin allow-scripts allow-presentation", allow="autoplay" and a fixed pixel size
     * (Width x Height, or 0x0 + opacity 0 for audio-only). Sets HaveVisual/HaveAudio from Stream, calls
     * MediaPlayerTools.InitBase, assigns a new InstanceId from GlobalInstanceId, Source, CurrentTime = StartAt,
     * PauseGreaterThan = -1, Duration = 0 and calls ApplyClip().
     * @param {string} code - The YouTube video id.
     * @param {MediaPlayerParamsYouTube|object} [params] - Partial params, merged over the defaults.
     */
    constructor(code, params) {
        const t = this;
        const cp = new MediaPlayerParamsYouTube();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        //  No load fade (fadeIn = 0): visibility is controlled by the "MediaHidden" class (Show / Hide) and, for audio
        //  only, an inline opacity of 0; the generic load fade would override both while loading and then clear them.
        const e = createIFrame(0);
        const audioOnly = params.Stream == 2;
        t.HaveVisual = !audioOnly;
        t.HaveAudio = params.Stream != 1;
        MediaPlayerTools.InitBase(t, e, params);
        t.Code = code;
        e.setAttribute("scrolling", "no");
        e.sandbox = "allow-same-origin allow-scripts allow-presentation";
        e.allow = "autoplay";
        const w = audioOnly ? 0 : (params.Width ? params.Width : 1280);
        const h = audioOnly ? 0 : (params.Height ? params.Height : (w * 9 / 16));
        e.style.width = w + "px";
        e.style.height = h + "px";
        if (audioOnly)
            e.style.opacity = "0";
        t.Width = w;
        t.Height = h;
        t.CanPlay = true;
        t.done = false;
        let instanceId = MediaPlayerYoutube.GlobalInstanceId;
        ++instanceId;
        MediaPlayerYoutube.GlobalInstanceId = instanceId;
        t.InstanceId = instanceId;
        t.Source = "https://www.youtube.com/watch?v=" + code;
        t.CurrentTime = params.StartAt;
        t.PauseGreaterThan = -1;
        t.Duration = 0;
        t.ApplyClip();
    }

    /**
     * Counter of created instances; each instance takes the incremented value as its InstanceId, used as the
     * "id" of the postMessage protocol so several players can share the window message stream.
     * @type {number}
     */
    static GlobalInstanceId = 0;


    /**
     * Loads the embed and establishes the postMessage connection. Must be awaited before using the player.
     * Temporarily attaches the iframe off-screen (MediaPlayerTools.MustBeAttached, restored in finally), defines
     * t.WaitCommand, registers messageHandler on window, wraps OnDispose to remove it, then calls connect() up to
     * 6 times (retry delays 50 ms doubling, max 5 s). After connecting it mutes, seeks to just before StartAt with
     * PauseGreaterThan = StartAt and waits for "YTInitCompleted" (the handler pauses on the exact frame) or
     * "YTonError" (returns false), waits 50 ms and completes with MediaPlayerTools.OnCacheComplete.
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when connected and cued, false on failure.
     */
    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        const restore = MediaPlayerTools.MustBeAttached(e);
        try {
            const code = t.Code;
            const p = t.Params;
            let haveConnection = false;
            const instanceId = t.InstanceId;
            const url = "https://www.youtube-nocookie.com/embed/" + code + "?&autoplay=0&mute=1&rel=0&start=0&autohide=1&showinfo=0&controls=0&showsearch=0&fs=0&enablejsapi=1&iv_load_policy=3&cc_load_policy=3&disablekb=1&playsinline=1&loop=1&cc_lang_pref=en&playlist=" + code;
            let isLooping = false;
            let gotError = false;
            let shouldPauseOnce = t.Paused;
            let startAt = p.StartAt;
            if (!startAt)
                startAt = 0;

            /**
             * Sends a command (SendCommand) and waits up to 500 ms for the next "YTinfoDelivery" event.
             * @param {string} cmd - YouTube player function name (e.g. "playVideo", "pauseVideo", "seekTo").
             * @param {string} [args] - JSON text of the arguments array (e.g. "[12.5,true]").
             * @returns {Promise<boolean>} True when an infoDelivery arrived; false on YTonError or timeout (warns).
             */
            t.WaitCommand = async (cmd, args) => {
                const ev = await waitEvent2(e, "YTinfoDelivery", "YTonError", () => t.SendCommand(cmd, args), 500);
                const res = ev && (ev.type === "YTinfoDelivery");
                if (!res)
                    console.warn("YT command \"" + cmd + "\", with args: \"" + args + "\" failed!");
                return res;
            }
            let sendPauseEvent = false;

            /**
             * window "message" listener. Parses ev.data as JSON and ignores non-JSON data and other instance ids.
             * Marks the connection as established, flags onError. For "infoDelivery" with a currentTime it:
             * grows Duration, updates CurrentTime, computes the window end (EndAt, or when looping without EndAt the
             * midpoint between StartAt and the duration, or duration - 0.5 when not looping without EndAt);
             * executes a pending PauseGreaterThan once passed (pause, seek back to it, unmute if not muted, fire
             * "YTInitCompleted" once during Cache); while waiting for a native loop wrap (isLooping) seeks to StartAt
             * + OnLoop once the time is below the midpoint; at the window end either pauses at the end (AtEnd,
             * OnEnded) when not looping, or loops (seekTo StartAt + OnLoop when EndAt is set, else waits for the
             * native wrap). Always re-dispatches the message as CustomEvent("YT" + event, {detail: data}) on the iframe.
             * @param {MessageEvent} ev - The window message event.
             * @returns {Promise<void>}
             */
            async function messageHandler(ev) {
                let d;
                try {
                    d = JSON.parse(ev.data);
                    if (d.id != instanceId)
                        return;
                }
                catch (e) {
                    return;
                }
                try {
                    if (!haveConnection) {
                        haveConnection = true;
                        console.log("Connected with iframe " + instanceId);
                    }
                    if (d.event === "onError") {
                        gotError = true;
                    }
                    if (d.event === "infoDelivery") {

                        const now = d.info.currentTime;
                        if (typeof now === "undefined")
                            return;
                        const newD = d.info.progressState.duration;
                        if (newD > t.Duration)
                            t.Duration = newD;

                        const duration = t.Duration;
                        t.CurrentTime = now;
                        const shouldLoop = t.Looping;
                        let endAt = p.EndAt;
                        if (endAt > duration)
                            endAt = 0;
                        let endTime = endAt > startAt ? endAt : duration;
                        const loopWatch = (endTime + startAt) * 0.5;
                        if (shouldLoop)
                            endTime = endAt > startAt ? endAt : loopWatch;
                        else
                            endTime -= (endAt > startAt ? 0 : 0.5);

                        const pgt = t.PauseGreaterThan;
                        if ((pgt >= 0) && (now > pgt)) {
                            t.PauseGreaterThan = -1;
                            await t.WaitCommand("pauseVideo");
                            await t.WaitCommand("seekTo", "[" + pgt + ",true]");
                            if (!t.Muted)
                                t.SendCommand("unMute");
                            t.CurrentTime = pgt;
                            if (sendPauseEvent)
                                e.dispatchEvent(new Event("YTInitCompleted"));
                            sendPauseEvent = false;
                        }
                        if (isLooping) {
                            if (now < loopWatch) {
                                if (startAt > 0)
                                    if (now <= 0)
                                        return;
                                t.SendCommand("seekTo", "[" + startAt + ",true]");
                                isLooping = false;
                                if (t.OnLoop)
                                    await t.OnLoop();
                            }
                            return;
                        }
                        if (now < endTime)
                            return;
                        if (!shouldLoop) {
                            if (!t.Paused) {
                                t.SendCommand("pauseVideo");
                                t.SendCommand("seekTo", "[" + endTime + ",true]");
                                t.CurrentTime = endTime;
                                t.Paused = true;
                                t.AtStart = false;
                                t.AtEnd = true;
                                await t.OnEnded();
                            }
                            return;
                        }
                        if (endAt > startAt) {
                            t.SendCommand("seekTo", "[" + startAt + ",true]");
                            if (t.OnLoop)
                                await t.OnLoop();
                        } else {
                            isLooping = true;
                        }

                    }

                }
                catch (e) {
                }
                finally {
                    e.dispatchEvent(new CustomEvent("YT" + d.event, {
                        detail: d,
                    }));
                    //console.log("YT" + d.event + ": " + ev.data);
                }
            }

            /**
             * One connection attempt: mutes, resets the connection/loop/error flags, sets the iframe src to the embed
             * URL (autoplay=0, mute=1, controls=0, enablejsapi=1, loop=1, playlist=<code>, playsinline=1, ...) and
             * waits for "load", then keeps posting the "listening" handshake (conFn) until "YTonReady" or "YTonError".
             * @returns {Promise<boolean>} True when ready without errors, false otherwise.
             */
            async function connect() {
                t.SendCommand("mute");
                haveConnection = false;
                isLooping = false;
                gotError = false;
                const res = await waitEvent2(e, "load", "error", () => e.src = url);
                if (res.type === "error")
                    return false;
                /**
                 * Posts {"event":"listening","id":<InstanceId>,"channel":"widget"} to the iframe and re-schedules
                 * itself every 100 ms until the first message for this instance has arrived (haveConnection).
                 * @returns {void}
                 */
                const conFn = () => {
                    if (haveConnection)
                        return;
                    e.contentWindow.postMessage('{"event":"listening","id":' + instanceId + ',"channel":"widget"}', '*');
                    setTimeout(conFn, 100);
                };
                const res2 = await waitEvent2(e, "YTonReady", "YTonError", conFn);
                if (res2.type === "YTonError")
                    return false;
                if (gotError)
                    return false;
                return true;
            }

            window.addEventListener("message", messageHandler);
            const onD = this.OnDispose;
            /**
             * Wrapped dispose: removes messageHandler from window, then calls the previous OnDispose (the InitBase
             * default, which awaits Stop; its promise is not awaited here).
             * @returns {void}
             */
            this.OnDispose = () => {
                window.removeEventListener("message", messageHandler);
                onD();
            };
            let timeOut = 50;
            for (let retry = 0; ; ++retry) {
                if (await connect()) {
                    t.SendCommand("mute");
                    const res2 = await waitEvent2(e, "YTInitCompleted", "YTonError", () => {
                        let time = startAt - 0.001;
                        if (time < 0)
                            time = 0;
                        t.PauseGreaterThan = startAt;
                        sendPauseEvent = true;
                        t.SendCommand("seekTo", "[" + time + ",true]");
                    });
                    if (res2.type === "YTonError") {
                        console.error("Youtube video playback failed for " + code);
                        return false;
                    }
                    await delay(50);
                    break;
                }
                if (retry >= 5) {
                    console.error("Youtube video playback failed for " + code);
                    return false;
                }
                console.warn("Youtube video playback failed for " + code + ", retrying in " + timeOut + " ms");
                await delay(timeOut);
                timeOut *= 2;
                if (timeOut > 5000)
                    timeOut = 5000;
            }
            console.log("Cached youtube video " + code);
            return MediaPlayerTools.OnCacheComplete(t, keepHidden);
        }
        finally {
            restore();
        }
    }

    //https://memo.ag2works.tokyo/post-1283/
    /**
     * Posts a player command to the iframe: '{"event":"command", "func":command[, "args":args]}' (target "*").
     * Does nothing when the iframe has no contentWindow (not attached).
     * @param {string} command - YouTube player function name (e.g. "mute", "setVolume", "seekTo").
     * @param {string} [args] - JSON text of the arguments array, inserted verbatim (e.g. "[50]").
     * @returns {void}
     */
    SendCommand(command, args) {

        const cmd = typeof args === "undefined" ? '{"event":"command", "func":"' + command + '"}' : '{"event":"command", "func":"' + command + '", "args":' + args + '}';
        const ce = this.Element.contentWindow;
        if (!ce)
            return;
        //console.log("YT sending command:\n" + cmd);
        ce.postMessage(cmd, '*');
    }


    /**
     * Mutes the player (sets Muted, sends "mute"). No-op when already muted.
     * @returns {Promise<void>}
     */
    async Mute() {
        if (this.Muted)
            return;
        this.Muted = true;
        this.SendCommand("mute");
    }

    /**
     * Unmutes the player: clears Muted and, when it has audio and Volume * Params.Volume > 0, sends
     * "setVolume [Volume * Params.Volume]" and "unMute". No-op when not muted.
     * @returns {Promise<void>}
     */
    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        if (!this.HaveAudio)
            return;
        const vol = this.Volume * this.Params.Volume;
        if (vol > 0) {
            this.SendCommand("setVolume", "[" + vol + "]");
            this.SendCommand("unMute");
        }
    }

    /**
     * Sets the player volume. Clamps to 0..1 and stores it in Volume (no-op when unchanged). When it has audio and
     * is not muted, sends "mute" (for 0) or "setVolume [vol * Params.Volume]" + "unMute", de-duplicated through
     * PlayerVolume.
     * @param {number} vol - Volume 0..1.
     * @returns {Promise<void>}
     */
    async SetVolume(vol) {
        if (vol < 0)
            vol = 0;
        if (vol > 1)
            vol = 1;
        if (vol == this.Volume)
            return;
        this.Volume = vol;
        if (!this.HaveAudio)
            return;
        if (this.Muted)
            return;
        vol *= this.Params.Volume;
        if (this.PlayerVolume !== vol) {
            this.PlayerVolume = vol;
            if (vol <= 0)
                this.SendCommand("mute");
            else {
                this.SendCommand("setVolume", "[" + vol + "]");
                this.SendCommand("unMute");
            }
        }
    }


    /**
     * Starts playback. No-op when already playing. When AtEnd, first seeks to StartAt; then WaitCommand("playVideo").
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        if (t.AtEnd) {
            t.AtEnd = false;
            await t.WaitCommand("seekTo", "[" + t.Params.StartAt + ",true]");
        }
        await t.WaitCommand("playVideo");
    }

    /**
     * Stops playback and shows the StartAt frame. No-op when paused at or before StartAt. Sets Paused,
     * CurrentTime = StartAt and PauseGreaterThan = StartAt, seeks to just before it, then (muting if needed) plays
     * so that the message handler pauses on the exact frame once the time passes StartAt.
     * @returns {Promise<void>}
     */
    async Stop() {
        const t = this;
        const startTime = t.Params.StartAt;
        if ((t.Paused) && (t.CurrentTime <= startTime))
            return;
        t.Paused = true;
        t.CurrentTime = startTime;
        let time = startTime;
        t.PauseGreaterThan = time;
        time -= 0.001;
        if (time < 0)
            time = 0;
        await t.WaitCommand("seekTo", "[" + time + ",true]");
        if (t.Paused) {
            if (!t.Muted)
                t.SendCommand("mute");
            await t.WaitCommand("playVideo");
        }
    }

    /**
     * Pauses playback (WaitCommand("pauseVideo")). No-op when already paused.
     * @returns {Promise<void>}
     */
    async Pause() {
        const t = this;
        if (t.Paused)
            return;
        t.Paused = true;
        await t.WaitCommand("pauseVideo");
    }

    /**
     * Seeks to a time. Ignores negative or unchanged times. While paused it uses the PauseGreaterThan trick
     * (seek just before the time and play; the handler pauses on the frame); while playing a plain seekTo.
     * The time is not clamped to the StartAt/EndAt window.
     * @param {number} time - Position in seconds (absolute clip time).
     * @returns {Promise<void>}
     */
    async Seek(time) {
        if (time < 0)
            return;
        const t = this;
        if (time == t.CurrentTime)
            return;
        t.CurrentTime = time;
        if (t.Paused) {
            t.PauseGreaterThan = time;
            time -= 0.001;
            if (time < 0)
                time = 0;
            await t.WaitCommand("seekTo", "[" + time + ",true]");
            await t.WaitCommand("playVideo");
        } else {
            await t.WaitCommand("seekTo", "[" + time + ",true]");
        }
    }

    /**
     * Gets the playback position.
     * @returns {number} CurrentTime in seconds, as last reported by an infoDelivery message (or set by Stop/Seek).
     */
    GetPos() {
        return this.CurrentTime;
    }

}

