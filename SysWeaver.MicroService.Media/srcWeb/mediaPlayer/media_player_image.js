


/**
 * @file media_player_image.js - static image player with a timer-based virtual timeline.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Image without an Effect; with an Effect, MediaPlayerEffect + MediaPlayerImageTexture are used instead).
 * Globals defined: MediaPlayerParamsImage, MediaPlayerImage.
 * Dependencies: MediaPlayerTools (media_player_tools.js: InitBase, ApplyClip default, OnCacheComplete) and the
 * common.js global waitEvent2. The timeline code is duplicated in MediaPlayerText (media_player_text.js).
 */

/**
 * Parameters of an image player; mirrors the C# type MediaImage (SysWeaver.MicroService.Media.MediaImage).
 * The field initializers are the defaults; MediaPlayerImage merges the supplied params over a new instance.
 * Also used as the params of MediaPlayerImageTexture when an Effect is set.
 */
class MediaPlayerParamsImage {
    /**
     * Optional crop (C# MediaCrop): {MarginTop, MarginRight, MarginBottom, MarginLeft} in pixels, or, when
     * MarginAsPercentage is true, {MarginTopP, MarginRightP, MarginBottomP, MarginLeftP} in percent.
     * null = no crop. See MediaPlayerTools.ComputeClip.
     * @type {object|null}
     */
    Crop = null;
    /**
     * Display duration in seconds (length of the virtual timeline); <= 0 means infinite / still image.
     * @type {number}
     */
    Duration = 10;
    /**
     * URL of a .glsl effect; when set, MediaPlayer.Create builds a MediaPlayerEffect that uses this image as
     * texture (MediaPlayerImageTexture) instead of a MediaPlayerImage. null = plain image.
     * @type {string|null}
     */
    Effect = null;
    /**
     * Parameters for the Effect (MediaPlayerParamsEffect values); becomes the effect player's Params. null = defaults.
     * @type {object|null}
     */
    EffectParams = null;
    /**
     * When an Effect is used: true makes the effect canvas take the (cropped) image size
     * (MediaPlayerImageTexture.Cache returns [w,h]); false keeps the effect's own size.
     * @type {boolean}
     */
    EffectSizeFromImage = true;
    /**
     * Whether the image has transparency (mirrors C#); not read by MediaPlayerImage itself.
     * @type {boolean}
     */
    Transparent = false;
    /**
     * When true the <img> uses object-fit: contain, AspectRatio is set and MediaPlayerTools.UpdateSize sizes the
     * element to its container (no transform). The constructor pre-sets it to true for ".svg" URLs before
     * applying the supplied params, so the class default false only applies to non-SVG images.
     * @type {boolean}
     */
    AdaptiveSize = false;
}

/**
 * Image player: shows an <img> and simulates a timeline of Params.Duration seconds with setTimeout.
 * Created by MediaPlayer.Create(MediaTypes.Image, url, params) when no Effect is given.
 * Lifecycle: constructor -> Cache -> Play/Pause/Seek/... -> OnDispose (InitBase default, calls Stop).
 * Instance fields: Params, Url/Source (the url), CurrentPosition (paused / last position in seconds),
 * StartedAt (performance.now() based start of the current run), TimeFn (active timeout id or null),
 * AspectRatio (width / height, adaptive only). Other state comes from MediaPlayerTools.InitBase.
 */
class MediaPlayerImage {




    /**
     * Creates the player and its <img> element (not loaded until Cache).
     * AdaptiveSize defaults to url.endsWith(".svg") before params are merged; when adaptive the element gets
     * object-fit: contain. Sets HaveVisual, CanPlay, calls MediaPlayerTools.InitBase, Url = Source = url,
     * CurrentPosition = 0, Duration = Params.Duration.
     * @param {string} url - URL of the image.
     * @param {MediaPlayerParamsImage|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsImage();
        const isSvg = url.endsWith(".svg");
        cp.AdaptiveSize = isSvg;
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("img");
        if (params.AdaptiveSize)
            e.style.objectFit = "contain";

        t.HaveVisual = true;
        t.CanPlay = true;
        MediaPlayerTools.InitBase(t, e, params);
        t.Url = url;
        t.Source = url;
        t.CurrentPosition = 0;
        t.Duration = params.Duration;
    }

    /**
     * Loads the image: sets src and waits for "load"/"error" (common.js waitEvent2), reads naturalWidth/Height
     * into Width/Height, sets AspectRatio when AdaptiveSize, calls ApplyClip() and MediaPlayerTools.OnCacheComplete.
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false on a load error.
     */
    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        const c = t.Url;
        const res = await waitEvent2(e, "load", "error", () => {
            e.src = c;
        });
        if (res.type == "error")
            return false;
        t.Width = e.naturalWidth;
        t.Height = e.naturalHeight;
        if (t.Params.AdaptiveSize) {
            if ((t.Width > 0) && (t.Height > 0)) {
                t.AspectRatio = t.Width / t.Height;
            }
        }
        t.ApplyClip();
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    /**
     * Starts the virtual timeline from CurrentPosition. No-op when already playing.
     * @returns {Promise<void>}
     */
    async Play() {
        const t = this;
        if (!t.Paused)
            return;
        t.Paused = false;
        /**
         * Arms (or re-arms) the end-of-duration timer. Duration <= 0: position 0 and, when not looping, pauses
         * immediately and calls OnEnded. Otherwise restarts from 0 if the remaining time is <= 0, sets StartedAt
         * and a setTimeout (TimeFn) for the remaining time; on expiry it either loops (position 0, re-arm, OnLoop)
         * or pauses with CurrentPosition = Duration and calls OnEnded.
         * @returns {Promise<void>}
         */
        const setTimer = async () => {
            const dur = t.Params.Duration;
            if (dur <= 0) {
                t.CurrentPosition = 0;
                if (!t.Looping) {
                    t.Paused = true;
                    await t.OnEnded();
                }
                return;
            }
            let timeLeft = (dur - t.CurrentPosition) * 1000;
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            if (timeLeft <= 0) {
                t.CurrentPosition = 0;
                timeLeft = dur * 1000;
            }
            t.StartedAt = performance.now() - t.CurrentPosition * 1000;
            t.TimeFn = setTimeout(async () => {
                t.TimeFn = null;
                if (t.Looping) {
                    t.CurrentPosition = 0;
                    await setTimer();
                    await t.OnLoop();
                } else {
                    t.Paused = true;
                    t.CurrentPosition = dur;
                    await t.OnEnded();
                }
            }, timeLeft);
        };
        await setTimer();
    }

    /**
     * Pauses the timeline (synchronous, unlike the InitBase default): clears the timer, stores GetPos() in
     * CurrentPosition and sets Paused. No-op when already paused.
     * @returns {void}
     */
    Pause() {
        const t = this;
        if (t.Paused)
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.CurrentPosition = t.GetPos();
        t.TimeFn = null;
        t.Paused = true;
    }

    /**
     * Stops the timeline (synchronous): clears the timer, pauses and resets CurrentPosition to 0.
     * No-op when already paused at position 0.
     * @returns {void}
     */
    Stop() {
        const t = this;
        if (t.Paused && (t.CurrentPosition <= 0))
            return;
        if (t.TimeFn)
            clearTimeout(t.TimeFn);
        t.TimeFn = null;
        t.Paused = true;
        t.CurrentPosition = 0;
    }

    /**
     * Gets the timeline position.
     * @returns {number} CurrentPosition when paused; 0 when Duration <= 0; otherwise the seconds since StartedAt,
     * capped at Duration.
     */
    GetPos() {
        const t = this;
        if (t.Paused)
            return t.CurrentPosition;
        const dur = t.Params.Duration;
        if (dur <= 0)
            return 0;
        return Math.min((performance.now() - t.StartedAt) / 1000, dur);
    }

    /**
     * Disables looping. When playing with an infinite Duration (<= 0) it pauses immediately and calls OnEnded;
     * with a finite Duration the running timer ends playback at Duration.
     * @returns {Promise<void>}
     */
    async Once() {
        const t = this;
        t.Looping = false;
        if (t.Paused)
            return;
        if (t.Params.Duration > 0)
            return;
        t.Paused = true;
        await t.OnEnded();
    }

    /**
     * Seeks the timeline. Negative times are ignored; times >= Duration are clamped to Duration. When playing,
     * the timer is stopped, CurrentPosition set and Play() called again; when paused only CurrentPosition changes.
     * @param {number} time - Position in seconds.
     * @returns {Promise<null|undefined>} null when the time was negative (ignored), else undefined.
     */
    async Seek(time) {
        if (time < 0)
            return null;
        const t = this;
        const dur = t.Params.Duration;
        if (time >= dur)
            time = dur;
        const p = t.Paused;
        if (!p) {
            if (t.TimeFn)
                clearTimeout(t.TimeFn);
            t.TimeFn = null;
            t.Paused = true;
        }
        t.CurrentPosition = time;
        if (!p)
            await t.Play();
    }


}




