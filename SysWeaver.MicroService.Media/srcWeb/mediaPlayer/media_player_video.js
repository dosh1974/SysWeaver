
/**
 * @file media_player_video.js - HTML5 video player with a StartAt/EndAt play window.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Video).
 * Globals defined: MediaPlayerParamsVideo, MediaPlayerVideo.
 * Dependencies: MediaPlayerTools (media_player_tools.js: InitBase, ApplyClip default, OnCacheComplete), and the
 * common.js globals waitEvent2 and delay. media_player_audio.js is a near line-for-line copy of this file.
 */

/**
 * Parameters of a video player; mirrors the C# type MediaVideo (SysWeaver.MicroService.Media.MediaVideo).
 * The field initializers are the defaults; MediaPlayerVideo merges the supplied params over a new instance.
 * A Crop object (see MediaPlayerTools.ComputeClip) may also be supplied and is used by ApplyClip.
 */
class MediaPlayerParamsVideo {

    /**
     * Start of the play window in seconds (playback, Stop and loops return here).
     * @type {number}
     */
    StartAt = 0;
    /**
     * End of the play window in seconds; 0, a value <= StartAt or a value > the media duration means "to the end".
     * @type {number}
     */
    EndAt = 0;
    /**
     * Volume in percent (0..100), multiplied with the player volume (0..1): element.volume = Volume(player) * Params.Volume * 0.01.
     * @type {number}
     */
    Volume = 100;
    /**
     * Which streams to use: 0 = video + audio, 1 = video only (HaveAudio = false, volume never applied),
     * 2 = audio only (HaveVisual = false, element sized 0x0 with opacity 0).
     * @type {number}
     */
    Stream = 0;
    /**
     * Whether the video has transparency (mirrors C#); not read by MediaPlayerVideo itself.
     * @type {boolean}
     */
    Transparent = false;
}


/**
 * Video player using an initially muted <video> element with a StartAt/EndAt play window.
 * Created by MediaPlayer.Create(MediaTypes.Video, url, params). Lifecycle: constructor -> Cache -> Play/Pause/...
 * -> OnDispose (InitBase default, calls Stop). Shared state and default methods come from MediaPlayerTools.InitBase.
 * Instance fields: Params (MediaPlayerParamsVideo), Url/Source (the url), AtStart (positioned at the window start),
 * AtEnd (paused at the window end), LoopWatch (native-loop wrap detector, see Cache).
 */
class MediaPlayerVideo {
    /**
     * Creates the player and its muted <video> element (not loaded until Cache).
     * Sets HaveVisual/HaveAudio from Params.Stream, calls MediaPlayerTools.InitBase, sets element.loop = Looping,
     * hides the element (0 size, opacity 0) for audio-only, sets CanPlay=true, Url = Source = url, AtStart = true.
     * @param {string} url - URL of the video file.
     * @param {MediaPlayerParamsVideo|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsVideo();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("video");
        e.muted = true;
        const audioOnly = params.Stream == 2;
        t.HaveVisual = !audioOnly;
        t.HaveAudio = params.Stream != 1;
        MediaPlayerTools.InitBase(t, e, params);
        e.loop = t.Looping;
        if (audioOnly) {
            e.style.width = "0";
            e.style.height = "0";
            e.style.opacity = "0";
        }
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.AtStart = true;
    }

    /**
     * Loads the video and installs the play-window logic. Must be awaited before using the player.
     * Reads videoWidth/Height into Width/Height (0 and a hidden element for audio-only), calls ApplyClip(),
     * reads Duration, seeks to StartAt (when > 0), computes the effective endTime (EndAt, or the duration when
     * EndAt <= StartAt; EndAt > duration counts as 0) and the loop-watch midpoint (endTime + StartAt) / 2, then installs:
     * - ontimeupdate: updates Duration; before endTime it detects a native `loop` wrap back below the midpoint
     *   (when LoopWatch is armed) and seeks back to StartAt + OnLoop, otherwise arms LoopWatch once past the
     *   midpoint (only when StartAt > 0, looping and playing); at/after endTime it seeks to StartAt when looping,
     *   else pauses at endTime, sets AtEnd and calls OnEnded.
     * - onended: natural end while playing -> pause, AtEnd = true, OnEnded.
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false when loading failed.
     */
    async Cache(keepHidden) {
        const t = this;
        const e = t.Element;
        if (!await MediaPlayerVideo.LoadVideo(e, t.Url))
            return false;
        if (t.HaveVisual) {
            t.Width = e.videoWidth;
            t.Height = e.videoHeight;
        } else {
            t.Width = 0;
            t.Height = 0;
            e.style.width = "0";
            e.style.height = "0";
            e.style.opacity = "0";
        }
        t.ApplyClip();
        const duration = e.duration;
        t.Duration = duration;
        const p = t.Params;
        const startAt = p.StartAt;
        if (startAt > 0) {
            e.currentTime = startAt;
            t.LoopWatch = false;
        }
        let endAt = p.EndAt;
        if (endAt > duration)
            endAt = 0;
        const endTime = endAt > startAt ? endAt : duration;
        const loopWatch = (endTime + startAt) * 0.5;
        e.ontimeupdate = async () => {
            const ct = e.currentTime;
            t.Duration = e.duration;
            if (ct < endTime) {
                if (t.LoopWatch) {
                    if (ct < loopWatch) {
                        e.currentTime = startAt;
                        t.LoopWatch = false;
                        if (t.OnLoop)
                            await t.OnLoop();
                    }
                } else {
                    t.LoopWatch = (startAt > 0) && t.Looping && (!t.Paused) && (ct > loopWatch);
                }
                return;
            }
            if (t.Looping) {
                e.currentTime = startAt;
                t.LoopWatch = false;
            } else {
                if (!t.Paused) {
                    t.Paused = true;
                    t.AtEnd = true;
                    e.pause();
                    e.currentTime = endTime;
                    await t.OnEnded();
                }
            }
        };
        e.onended = async ev => {
            if (!t.Paused) {
                t.Paused = true;
                e.pause();
                t.AtEnd = true;
                await t.OnEnded();
            }
        };
        return MediaPlayerTools.OnCacheComplete(this, keepHidden);
    }


    //static VideoCache = new Map();

    /**
     * Loads a media URL into a <video> element: appends a <source src=c> child, waits for "loadeddata" or "error"
     * (common.js waitEvent2), then polls every 100 ms until readyState >= 3 (HAVE_FUTURE_DATA).
     * The commented-out block at the start is an inactive blob-cache implementation (VideoCache).
     * @param {HTMLVideoElement} e - The video element to load into.
     * @param {string} c - The source URL.
     * @returns {Promise<boolean>} False when the element fired "error", else true.
     */
    static async LoadVideo(e, c) {
        /*
        const cache = MediaPlayerVideo.VideoCache;
        let blobUrl = cache.get(c);
        if (!blobUrl) {
            if (blobUrl !== "failed") {

                const r = new Request(c, {
                    method: "GET",
                    mode: "cors",
                    cache: "default",
                });
                const res = await fetch(r);
                if (res.status != 200) {
                    setTimeout(() => LoadVideos(e, c), 10000);
                    return;
                }
                const blob = await res.blob();
                if (blob.type !== "") {
                    const newC = URL.createObjectURL(blob);
                    cache.set(c, newC);
                    c = newC;
                } else {
                    log.warn("Can't use video blob cache for \"" + c + "\"");
                    cache.set(c, "failed");
                }
            }
        }
        */

        const res = await waitEvent2(e, "loadeddata", "error", () => {
            const source = document.createElement('source');
            source.src = c;
            //source.type = "video/webm";
            e.appendChild(source);
        });
        if (res.type == "error") {
            return false;
        }
        while (e.readyState < 3)
            await delay(100);
        return true;
    }

    /**
     * Mutes the player (sets Muted and element.muted). No-op when already muted.
     * @returns {Promise<void>}
     */
    async Mute() {
        if (this.Muted)
            return;
        this.Muted = true;
        this.Element.muted = true;
    }

    /**
     * Unmutes the player: clears Muted and, when the video has audio (Stream != 1), applies
     * element.volume = Volume * Params.Volume * 0.01 and clears element.muted. No-op when not muted.
     * @returns {Promise<void>}
     */
    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        if (!this.HaveAudio)
            return;
        this.Element.volume = this.Volume * this.Params.Volume * 0.01;
        this.Element.muted = false;
    }

    /**
     * Sets the player volume. Clamps to 0..1, stores it in Volume (no-op when unchanged) and, when the video has
     * audio and is not muted, applies vol * Params.Volume * 0.01 to the element.
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
        this.Element.volume = vol * this.Params.Volume * 0.01;
    }


    /**
     * Starts playback. No-op when already playing. When AtEnd, seeks to StartAt first. Sets element.loop = Looping
     * and awaits element.play(); on success Paused=false, AtStart=false and Duration is refreshed. A rejected
     * play() (e.g. autoplay policy) is swallowed and the player stays paused.
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        const e = t.Element;
        const p = t.Params;
        const startAt = p.StartAt;
        if (t.AtEnd) {
            t.AtEnd = false;
            e.currentTime = startAt;
            t.LoopWatch = false;
        }
        try {
            e.loop = t.Looping;
            await e.play();
            t.Paused = false;
            t.AtStart = false;
            t.Duration = e.duration;
        }
        catch
        {
        }
    }


    /**
     * Stops playback: pauses, seeks to StartAt, clears LoopWatch and sets Paused = AtStart = true.
     * No-op when already paused at the start. The commented-out block is an older "replace the element" variant.
     * @returns {Promise<true|undefined>} True when stopped, undefined when it was already stopped.
     */
    async Stop() {
        const t = this;
        const e = t.Element;
        if (t.Paused) {
            if (t.AtStart)
                return;
        }
/*
        const n = document.createElement("video");
        n.muted = e.muted;
        n.volume = e.volume;
        if (!await MediaPlayerVideo.LoadVideo(n, t.Url)) {
            t.Paused = true;
            const res = await waitEvent2(e, "loadeddata", "error", () => e.load());
            if (res.type == "error")
                return false;
            t.Paused = true;
            t.AtStart = true;
            return;
        }
        const startAt = t.Params.StartAt;
        if (startAt > 0)
        {
            n.currentTime = startAt;
            t.LoopWatch = false;
        }
        n.className = e.className;
        n.setAttribute("style", e.getAttribute("style"));
        e.replaceWith(n);
        this.Element = n;
*/
        e.pause();
        e.currentTime = t.Params.StartAt;
        t.LoopWatch = false;
        t.Paused = true;
        t.AtStart = true;
        return true;
    }

    /**
     * Pauses playback (keeps the position). No-op when already paused.
     * @returns {Promise<void>}
     */
    async Pause() {
        if (this.Paused)
            return;
        this.Paused = true;
        this.Element.pause();
    }

    /**
     * Seeks to a time: clamps it to [StartAt, endTime] (endTime = EndAt when > StartAt, else the duration),
     * updates AtEnd/AtStart, sets element.currentTime and clears LoopWatch. Does not change the play state.
     * @param {number} time - Position in seconds (absolute media time, not relative to StartAt).
     * @returns {Promise<void>}
     */
    async Seek(time) {
        const t = this;
        const p = t.Params;
        const e = t.Element;
        const startAt = this.Params.StartAt;
        const endAt = p.EndAt;
        const endTime = endAt > startAt ? endAt : e.duration;
        if (time < startAt)
            time = startAt;
        if (time > endTime)
            time = endTime;
        t.AtEnd = time >= endTime;
       t.AtStart = time <= startAt;
        e.currentTime = time;
        t.LoopWatch = false;
    }

    /**
     * Gets the current playback position.
     * @returns {number} element.currentTime in seconds (absolute media time).
     */
    GetPos() {
        return this.Element.currentTime;
    }

    /**
     * Enables looping (Looping = element.loop = true). No-op when already looping.
     * @returns {Promise<void>}
     */
    async Loop() {
        const t = this;
        if (t.Looping)
            return;
        const e = t.Element;
        t.Looping = true;
        e.loop = true;
    }

    /**
     * Disables looping (Looping = element.loop = false), so playback ends at the window end. No-op when not looping.
     * @returns {Promise<void>}
     */
    async Once() {
        const t = this;
        if (!t.Looping)
            return;
        t.Looping = false;
        t.Element.loop = false;
    }

}
