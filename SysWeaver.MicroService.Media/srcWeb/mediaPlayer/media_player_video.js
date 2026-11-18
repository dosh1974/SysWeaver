
/**
 * @file media_player_video.js - HTML5 video player with a StartAt/EndAt play window.
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Video).
 * Globals defined: MediaPlayerParamsVideo, MediaPlayerVideo, MediaPlayerVideoTexture (a video as the texture of an effect).
 * Dependencies: MediaPlayerElement, MediaPlayerTexture and MediaPlayerTools (media_player_tools.js: the play window, volume and transport
 * shared with MediaPlayerAudio, InitBase, ApplyClip default, OnCacheComplete, waitEvent2, delay; the texture base).
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
    /**
     * An effect that draws the video (url of a .glsl, the video is its texture: MediaPlayerVideoTexture), null / "" = none.
     * @type {?string}
     */
    Effect = null;
    /**
     * The params of the effect (MediaPlayerParamsEffect), null = its defaults.
     * @type {?object}
     */
    EffectParams = null;
    /**
     * With an effect: true = the effect takes the size of the video, false = its own size (the size of the view).
     * @type {boolean}
     */
    EffectSizeFromImage = true;
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

/**
 * A video as the texture of an effect (a video with an effect: MediaPlayer.Create, MediaPlayerEffect.FollowMedia): the frame the video shows
 * now, uploaded before every frame the effect draws while the video plays, and after a seek / the first frame while it is paused. The video
 * plays from its own element (not shown: the effect draws it), on the effect's time line (the effect follows the video, like a sound).
 * Like a picture (MediaPlayerTexture): flipped (uv (0, 0) = bottom left), the crop of the video's params, mipmaps when the shader reads
 * levels (made again for every frame) or EffectParams.MipMap; EffectSizeFromImage: the effect takes the size of the video.
 * Texture provider interface of MediaPlayerEffect: Cache(haveLod), Update() (before every frame), Apply(uv, size), ApplyClip(p), Dispose().
 */
class MediaPlayerVideoTexture extends MediaPlayerTexture {
    /**
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - Effect context.
     * @param {MediaPlayerParamsVideo|object} params - The video params (Crop, EffectSizeFromImage, EffectParams ...).
     * @param {MediaPlayerVideo} media - The player of the video (loaded by the effect before its texture: MediaPlayerEffect.Cache).
     */
    constructor(gl, params, media) {
        super(gl, Object.assign(new MediaPlayerParamsVideo(), params || {}), media.Url);
        const t = this;
        t.Media = media;
        /** @type {boolean} A new frame must be uploaded (a seek, the first frame), also while paused. */
        t.Dirty = true;
        /** @type {number} The video time of the uploaded frame (-1 = none). */
        t.LastTime = -1;
        t.HaveLod = false;
        t.Created = false;
    }

    /**
     * Uploads the first frame (the video is loaded: the effect loads its media first).
     * @param {boolean} haveLod - The effect reads texture levels (mipmaps for every frame).
     * @returns {Promise<Array<number>|null|false>} [w,h] of the video when Params.EffectSizeFromImage, null otherwise, false without a picture.
     */
    async Cache(haveLod) {
        const t = this;
        t.HaveLod = !!haveLod;
        const e = t.Media.Element;
        if (!(e.videoWidth > 0))
            return false;
        t.UploadFrame();
        return t.Params.EffectSizeFromImage ? [e.videoWidth, e.videoHeight] : null;
    }

    /**
     * Uploads the frame the video shows now into the (bound) texture (the first upload sets the wrapping, filtering and size).
     * @returns {boolean} True when a frame was uploaded (false: the video has no frame yet).
     */
    UploadFrame() {
        const t = this;
        const gl = t.GL;
        const e = t.Media.Element;
        //  (HAVE_CURRENT_DATA: a frame to show)
        if (!gl || e.readyState < 2 || !(e.videoWidth > 0))
            return false;
        t.Width = e.videoWidth;
        t.Height = e.videoHeight;
        gl.bindTexture(gl.TEXTURE_2D, t.Texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, e);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        const ep = t.Params.EffectParams;
        //  (WebGL 1 has mipmaps for power-of-two sizes only)
        const pow2 = v => v && !(v & (v - 1));
        const mips = (t.HaveLod || (ep && ep.MipMap)) && ((gl instanceof WebGL2RenderingContext) || (pow2(t.Width) && pow2(t.Height)));
        if (mips)
            gl.generateMipmap(gl.TEXTURE_2D);
        if (!t.Created) {
            t.Created = true;
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, mips ? gl.LINEAR_MIPMAP_LINEAR : gl.LINEAR);
        }
        t.LastTime = e.currentTime;
        t.Dirty = false;
        return true;
    }

    /**
     * Before every frame of the effect (texture unit 0 active): a new frame while the video plays, or when its time changed (a seek) or a
     * frame is due (Dirty); a paused video at the same time isn't uploaded again.
     * @returns {void}
     */
    Update() {
        const t = this;
        const e = t.Media.Element;
        if (e.paused && !t.Dirty && e.currentTime === t.LastTime)
            return;
        t.UploadFrame();
    }
}

/** Publish the classes of this file under the new namespace Sw.Media (the global class names stay the public legacy API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaPlayerParamsVideo = MediaPlayerParamsVideo;
    m.MediaPlayerVideo = MediaPlayerVideo;
    m.MediaPlayerVideoTexture = MediaPlayerVideoTexture;
})();
