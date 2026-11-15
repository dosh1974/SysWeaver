
/**
 * @file media_player_video.js - HTML5 video player with a StartAt/EndAt play window.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Video).
 * Globals defined: MediaPlayerParamsVideo, MediaPlayerVideo.
 * Dependencies: MediaPlayerElement and MediaPlayerTools (media_player_tools.js: the play window, volume and transport shared with
 * MediaPlayerAudio, InitBase, ApplyClip default, OnCacheComplete, waitEvent2, delay).
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
 * -> OnDispose (InitBase default, calls Stop). The play window, volume and transport are those of MediaPlayerElement
 * (media_player_tools.js, shared with MediaPlayerAudio); this class adds the picture: size, clip and the Stream choice.
 * Instance fields: Params (MediaPlayerParamsVideo), Url/Source (the url), AtStart (positioned at the window start),
 * AtEnd (paused at the window end), LoopWatch (native-loop wrap detector, see MediaPlayerElement.Cache).
 */
class MediaPlayerVideo extends MediaPlayerElement {
    /**
     * Creates the player and its muted <video> element (not loaded until Cache).
     * Sets HaveVisual/HaveAudio from Params.Stream and hides the element (0 size, opacity 0) for audio-only
     * (see MediaPlayerElement for the rest).
     * @param {string} url - URL of the video file.
     * @param {MediaPlayerParamsVideo|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const cp = new MediaPlayerParamsVideo();
        if (params)
            Object.assign(cp, params);
        const audioOnly = cp.Stream == 2;
        super(url, cp, "video", !audioOnly, cp.Stream != 1);
        if (audioOnly)
            this.HideElement();
    }

    /**
     * Makes the element invisible (audio-only video): 0 size and opacity 0.
     * @returns {void}
     */
    HideElement() {
        const s = this.Element.style;
        s.width = "0";
        s.height = "0";
        s.opacity = "0";
    }

    /**
     * Called by Cache when the video has loaded: reads videoWidth/Height into Width/Height (0 and a hidden element for
     * audio-only), then ApplyClip().
     * @returns {void}
     */
    Measure() {
        const t = this;
        if (t.HaveVisual) {
            t.Width = t.Element.videoWidth;
            t.Height = t.Element.videoHeight;
        } else {
            t.Width = 0;
            t.Height = 0;
            t.HideElement();
        }
        t.ApplyClip();
    }

    /**
     * Loads a media URL into a <video> element (see MediaPlayerElement.Load).
     * @param {HTMLVideoElement} e - The video element to load into.
     * @param {string} c - The source URL.
     * @returns {Promise<boolean>} False when the element fired "error", else true.
     */
    static LoadVideo(e, c) {
        return MediaPlayerElement.Load(e, c);
    }
}

/** Publish the classes of this file under the new namespace Sw.Media (the global class names stay the public legacy API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaPlayerParamsVideo = MediaPlayerParamsVideo;
    m.MediaPlayerVideo = MediaPlayerVideo;
})();
