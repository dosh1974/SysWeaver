
/**
 * @file media_player_audio.js - HTML5 audio player (the media element player of media_player_tools.js without visuals, plus the
 * analyser).
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Audio).
 * Globals defined: MediaPlayerParamsAudio, MediaPlayerAudio, MediaAudioAnalyser (the spectrum / waveform of the sound), MediaAudioSamples (its offline analysis) and
 * MediaPlayerAudioTexture (the sound as a texture of MediaPlayerEffect, ShaderToy's audio input).
 * Dependencies: MediaPlayerElement and MediaPlayerTools (media_player_tools.js: the play window, volume and transport shared with
 * MediaPlayerVideo, InitBase, OnCacheComplete, waitEvent2, delay; no SysWeaver core needed).
 */

/**
 * Parameters of an audio player; mirrors the C# type MediaAudio (SysWeaver.MicroService.Media.MediaAudio).
 * The field initializers are the defaults; MediaPlayerAudio merges the supplied params over a new instance.
 */
class MediaPlayerParamsAudio {

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
}

/**
 * Audio player using a hidden, zero-sized, initially muted <audio> element with a StartAt/EndAt play window.
 * Created by MediaPlayer.Create(MediaTypes.Audio, url, params). Lifecycle: constructor -> Cache -> Play/Pause/...
 * -> OnDispose (InitBase default, calls Stop). The play window and transport are those of MediaPlayerElement
 * (media_player_tools.js, shared with MediaPlayerVideo); this class adds the analyser (spectrum / waveform) and routes the
 * volume through it when one runs. No ApplyClip (audio has no visual).
 * Instance fields: Params (MediaPlayerParamsAudio), Url/Source (the url), AtStart (positioned at the window start),
 * AtEnd (paused at the window end), LoopWatch (native-loop wrap detector, see MediaPlayerElement.Cache), Analyser.
 */
class MediaPlayerAudio extends MediaPlayerElement {
    /**
     * Creates the player and its muted <audio> element (not loaded until Cache): HaveVisual = false, HaveAudio = true
     * (see MediaPlayerElement for the rest).
     * @param {string} url - URL of the audio file.
     * @param {MediaPlayerParamsAudio|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const cp = new MediaPlayerParamsAudio();
        if (params)
            Object.assign(cp, params);
        super(url, cp, "audio", false, true);
    }

    /**
     * Loads a media URL into an <audio> (or <video>) element (see MediaPlayerElement.Load).
     * @param {HTMLMediaElement} e - The media element to load into.
     * @param {string} c - The source URL.
     * @returns {Promise<boolean>} False when the element fired "error", else true.
     */
    static LoadAudio(e, c) {
        return MediaPlayerElement.Load(e, c);
    }

    /**
     * Unmutes the player (ApplyVolume). No-op when not muted. May be blocked by the browser autoplay policy (see
     * MediaPlayerTools.CanUnMute); a waiting analyser tries to start (a click unmutes).
     * @returns {Promise<void>}
     */
    async UnMute() {
        if (!this.Muted)
            return;
        await super.UnMute();
        this.Analyser?.TryStart();
    }

    /**
     * Applies Muted and Volume * Params.Volume * 0.01 to the sound. Without a running analyser on the element (muted, element.volume);
     * with one, on the analyser's gain: the element plays at full volume and unmuted, so the analyser gets the sound also while muted.
     * @returns {void}
     */
    ApplyVolume() {
        const t = this;
        const a = t.Analyser;
        if (!a || !a.Live) {
            super.ApplyVolume();
            return;
        }
        t.Element.volume = 1;
        t.Element.muted = false;
        a.SetGain(t.Muted ? 0 : t.Volume * t.Params.Volume * 0.01);
    }

    /** @type {MediaAudioAnalyser|null} The analyser of the sound (GetAnalyser), null when none is used. */
    Analyser = null;

    /**
     * The analyser of the sound (created on the first call; an element has one at most, see MediaAudioAnalyser).
     * @returns {MediaAudioAnalyser} The analyser.
     */
    GetAnalyser() {
        return this.Analyser ??= new MediaAudioAnalyser(this);
    }

    /**
     * Called by Play before the element plays: a waiting analyser tries to start.
     * @returns {void}
     */
    BeforePlay() {
        this.Analyser?.TryStart();
    }
}

/**
 * The spectrum and the waveform of the sound of an audio player (Web Audio AnalyserNodes on its element), like ShaderToy's audio input:
 * an fft of 2048 samples, the first Width (512) frequency bins (0..~11 kHz at 44.1 kHz) and the first Width samples, as 0..1 values
 * (spectrum: minDecibels..maxDecibels, waveform: 0.5 = silence), for the mono mix, the left and the right channel (a mono sound: left = right).
 *
 * The browsers only start an AudioContext after a user gesture (or when sound may play by itself, MediaPlayerTools.CanUnMute), so the
 * graph is made when it can run: on the first click / key, on "AudioEnabled", and on Play / UnMute of the player. Until then the
 * element plays as before (muted or not) and the data is silence. Once running the element plays unmuted at full volume into the graph,
 * the player's mute and volume go to a gain node (MediaPlayerAudio.ApplyVolume), so the analysis goes on while muted.
 * An element can only feed one graph (createMediaElementSource): the player keeps one analyser (MediaPlayerAudio.GetAnalyser).
 */
class MediaAudioAnalyser {

    /** @type {number} The number of values per row (frequency bins / samples), ShaderToy's audio texture width. */
    static Width = 512;

    /**
     * Offline analysis (also `window.MediaAudioOffline = true` before the player is made, ex: the screenshot page): the sound is decoded
     * (no user gesture needed) and the data is computed from its samples at the play position (MediaAudioSamples), so it is the same
     * on every run, also paused or seeked, and without any sound output. Default false: the live AnalyserNodes.
     * @type {boolean}
     */
    static Offline = false;

    /** @returns {boolean} The analysis is offline (Offline or window.MediaAudioOffline). */
    static get IsOffline() {
        return MediaAudioAnalyser.Offline || !!window.MediaAudioOffline;
    }

    /**
     * @param {MediaPlayerAudio} player - The player of the sound.
     */
    constructor(player) {
        const t = this;
        t.Player = player;
        //  (set at once: a graph that connects right away applies the volume through it)
        player.Analyser = t;
        const n = MediaAudioAnalyser.Width;
        t.Freq = new Float32Array(n);
        t.Wave = new Float32Array(n);
        if (MediaAudioAnalyser.IsOffline) {
            //  The samples of the sound (Ready: decoded, or failed: silence), no live graph
            t.Ready = MediaAudioSamples.Load(player.Url).then(s => t.Samples = s, e => {
                console.warn("Media: no offline audio analysis, " + (e && e.message ? e.message : e));
                return null;
            });
            return;
        }
        /** Gesture / "AudioEnabled" handler. @returns {void} */
        t.OnGesture = () => t.TryStart();
        for (const ev of ["pointerdown", "keydown"])
            window.addEventListener(ev, t.OnGesture, { capture: true, passive: true });
        document.body?.addEventListener("AudioEnabled", t.OnGesture);
        t.TryStart();
    }

    /** @type {AudioContext|null} The context, null until it may start. */
    Ctx = null;
    /** @type {boolean} The graph is connected and running (the data is live). */
    Live = false;
    /** @type {AnalyserNode[]} The analysers: mono, left, right (when Live). */
    Nodes = [];
    /** @type {GainNode|null} The output volume (when Live). */
    Gain = null;

    /** @returns {number} The sample rate of the sound (44100 until the context runs). */
    get SampleRate() {
        return this.Ctx?.sampleRate ?? 44100;
    }

    /**
     * Creates / resumes the context when the page may play sound, then connects the graph (Connect). No-op when live or disposed.
     * @returns {void}
     */
    TryStart() {
        const t = this;
        //  (offline analysis: no live graph, the samples are used)
        if (t.Live || t.Disposed || t.Ready)
            return;
        let ctx = t.Ctx;
        if (!ctx) {
            //  (a context made before any gesture is only "suspended", with a console warning)
            const ua = navigator.userActivation;
            if (ua && (!ua.hasBeenActive) && (!MediaPlayerTools.Result))
                return;
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC)
                return;
            try {
                ctx = t.Ctx = new AC();
            }
            catch (e) {
                console.warn("Media: no audio analysis, " + e.message);
                return;
            }
            ctx.onstatechange = () => t.Connect();
        }
        if (ctx.state === "suspended")
            ctx.resume().catch(() => { });
        t.Connect();
    }

    /**
     * Connects element -> gain -> output and element -> analysers (mono; left and right through a stereo up-mix and a splitter) once the
     * context runs, then lets the player apply its volume to the gain.
     * @returns {void}
     */
    Connect() {
        const t = this;
        const ctx = t.Ctx;
        if (t.Live || t.Disposed || (!ctx) || (ctx.state !== "running"))
            return;
        try {
            const src = ctx.createMediaElementSource(t.Player.Element);
            const gain = ctx.createGain();
            src.connect(gain);
            gain.connect(ctx.destination);
            /** @returns {AnalyserNode} A ShaderToy like analyser. */
            const mk = () => {
                const a = ctx.createAnalyser();
                a.fftSize = 2048;
                a.smoothingTimeConstant = 0.5;
                return a;
            };
            const mono = mk();
            src.connect(mono);
            const stereo = ctx.createGain();
            stereo.channelCount = 2;
            stereo.channelCountMode = "explicit";
            stereo.channelInterpretation = "speakers";
            src.connect(stereo);
            const split = ctx.createChannelSplitter(2);
            stereo.connect(split);
            const left = mk();
            const right = mk();
            split.connect(left, 0);
            split.connect(right, 1);
            t.Nodes = [mono, left, right];
            t.Gain = gain;
        }
        catch (e) {
            console.warn("Media: no audio analysis, " + e.message);
            t.Disposed = true;
            return;
        }
        t.Live = true;
        t.RemoveListeners();
        t.Player.ApplyVolume();
    }

    /**
     * Sets the output volume (when live).
     * @param {number} v - Volume 0..1.
     * @returns {void}
     */
    SetGain(v) {
        const g = this.Gain;
        if (g)
            g.gain.value = v;
    }

    /**
     * Reads the current spectrum and waveform into an interleaved RGB array of 2 rows (row 0 = spectrum, row 1 = waveform; r = mono,
     * g = left, b = right), times scale (1 for floats, 255 for bytes).
     * @param {Float32Array|Uint8ClampedArray} out - Width * 2 * 3 values.
     * @param {number} scale - Multiplier of the 0..1 values.
     * @returns {void}
     */
    Read(out, scale) {
        const t = this;
        const n = MediaAudioAnalyser.Width;
        const nodes = t.Nodes;
        t.Quiet = true;
        //  Offline: from the samples at the play position
        if (t.Samples) {
            t.Silent = null;
            t.Quiet = t.Samples.Fill(out, t.Player.GetPos() || 0, scale);
            return;
        }
        if (!t.Live) {
            if (t.Silent === out)
                return;
            t.Silent = out;
            for (let i = 0; i < n * 3; ++i) {
                out[i] = 0;
                out[n * 3 + i] = 0.5 * scale;
            }
            return;
        }
        t.Silent = null;
        const f = t.Freq;
        const w = t.Wave;
        for (let c = 0; c < 3; ++c) {
            const a = nodes[c];
            a.getFloatFrequencyData(f);
            a.getFloatTimeDomainData(w);
            const min = a.minDecibels;
            const k = scale / (a.maxDecibels - min);
            const hs = scale * 0.5;
            let loud = 0;
            for (let i = 0; i < n; ++i) {
                const v = (f[i] - min) * k;
                out[i * 3 + c] = v > 0 ? (v < scale ? v : scale) : 0;
                const s = (w[i] + 1) * hs;
                out[(n + i) * 3 + c] = s > 0 ? (s < scale ? s : scale) : 0;
                loud = Math.max(loud, v, Math.abs(w[i]) * scale);
            }
            //  (below a 1000th of the range: silence)
            if (loud > scale * 0.001)
                t.Quiet = false;
        }
    }

    /** @type {boolean} The last Read was silence (the whole spectrum at 0, the waveform at 0.5): what the sound left has faded out. */
    Quiet = true;

    /**
     * Removes the gesture listeners.
     * @returns {void}
     */
    RemoveListeners() {
        const t = this;
        for (const ev of ["pointerdown", "keydown"])
            window.removeEventListener(ev, t.OnGesture, { capture: true });
        document.body?.removeEventListener("AudioEnabled", t.OnGesture);
    }

    /**
     * Stops the analysis: removes the listeners and closes the context (the element plays no more sound after that; the player is
     * disposed with it).
     * @returns {void}
     */
    Dispose() {
        const t = this;
        t.Disposed = true;
        t.Live = false;
        t.RemoveListeners();
        const ctx = t.Ctx;
        t.Ctx = null;
        t.Nodes = [];
        t.Gain = null;
        if (ctx)
            ctx.close().catch(() => { });
    }
}

/**
 * A decoded sound and ShaderToy's audio data at any position (the offline analysis of MediaAudioAnalyser): computed from the samples like
 * the browser's AnalyserNode (fft of 2048 samples, Blackman window, -100 .. -30 dB, smoothing 0.5 over the frames at 60 / s), the same as
 * Effect Weaver's editor (EwAudio), so a screenshot shows the sound at that time without a running AudioContext (a headless browser).
 */
class MediaAudioSamples {
    static #N = 2048;
    static #Smooth = [0.5, 0.25, 0.125, 0.125];

    /**
     * Decode a sound (44.1 kHz, an offline context: no user gesture needed).
     * @param {string} url The url of the sound.
     * @returns {Promise<MediaAudioSamples>} The samples.
     */
    static async Load(url) {
        const res = await fetch(url, { credentials: "same-origin" });
        if (!res.ok)
            throw new Error("HTTP " + res.status + " " + url);
        const buf = await new OfflineAudioContext(2, 1, 44100).decodeAudioData(await res.arrayBuffer());
        return new MediaAudioSamples(buf);
    }

    /** @param {AudioBuffer} buffer The decoded sound. */
    constructor(buffer) {
        const t = this;
        const n = MediaAudioSamples.#N;
        t.Rate = buffer.sampleRate;
        t.Duration = buffer.duration;
        const l = buffer.getChannelData(0);
        const r = buffer.numberOfChannels > 1 ? buffer.getChannelData(1) : l;
        const m = new Float32Array(l.length);
        for (let i = 0; i < m.length; ++i)
            m[i] = (l[i] + r[i]) * 0.5;
        t.Ch = [m, l, r];
        t.Win = new Float32Array(n);
        t.Rev = new Uint16Array(n);
        t.Cos = new Float64Array(n / 2);
        t.Sin = new Float64Array(n / 2);
        t.Re = new Float64Array(n);
        t.Im = new Float64Array(n);
        for (let i = 0; i < n; ++i) {
            t.Win[i] = 0.42 - 0.5 * Math.cos(2 * Math.PI * i / n) + 0.08 * Math.cos(4 * Math.PI * i / n);
            let x = i, y = 0;
            for (let b = 0; b < 11; ++b) {
                y = (y << 1) | (x & 1);
                x >>= 1;
            }
            t.Rev[i] = y;
        }
        for (let i = 0; i < n / 2; ++i) {
            t.Cos[i] = Math.cos(-2 * Math.PI * i / n);
            t.Sin[i] = Math.sin(-2 * Math.PI * i / n);
        }
        t.Cache = new Map();
    }

    /** The magnitudes (/ N) of the first Width bins of channel c, for the N samples before sample end. */
    #Spectrum(c, end, out) {
        const t = this;
        const n = MediaAudioSamples.#N, re = t.Re, im = t.Im, x = t.Ch[c], w = t.Win, rev = t.Rev;
        const start = end - n;
        for (let i = 0; i < n; ++i) {
            const k = start + i;
            re[rev[i]] = k >= 0 && k < x.length ? x[k] * w[i] : 0;
            im[rev[i]] = 0;
        }
        for (let size = 2; size <= n; size <<= 1) {
            const half = size >> 1, step = n / size;
            for (let i = 0; i < n; i += size)
                for (let j = 0, k = 0; j < half; ++j, k += step) {
                    const a = i + j, b = a + half;
                    const tr = re[b] * t.Cos[k] - im[b] * t.Sin[k];
                    const ti = re[b] * t.Sin[k] + im[b] * t.Cos[k];
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
        }
        for (let i = 0; i < out.length; ++i)
            out[i] = Math.sqrt(re[i] * re[i] + im[i] * im[i]) / n;
    }

    /** The spectra of frame q (60 / s), cached. */
    #Frame(q) {
        const t = this;
        let f = t.Cache.get(q);
        if (f)
            return f;
        const w = MediaAudioAnalyser.Width;
        f = new Float32Array(3 * w);
        for (let c = 0; c < 3; ++c)
            t.#Spectrum(c, Math.round(q / 60 * t.Rate), f.subarray(c * w, c * w + w));
        t.Cache.set(q, f);
        if (t.Cache.size > 24)
            t.Cache.delete(t.Cache.keys().next().value);
        return f;
    }

    /**
     * The audio data at a position: Width x 2 interleaved RGB (row 0 the spectrum, row 1 the waveform; r = mono, g = left, b = right), times scale.
     * @param {Float32Array|Uint8ClampedArray} out The data.
     * @param {number} time The position in the sound (seconds).
     * @param {number} scale 1 for floats, 255 for bytes.
     * @returns {boolean} True when it is silence.
     */
    Fill(out, time, scale) {
        const t = this;
        const w = MediaAudioAnalyser.Width;
        const s = Math.max(0, Math.min(t.Duration, time));
        const q = Math.floor(s * 60);
        const K = MediaAudioSamples.#Smooth;
        const frames = K.map((_, j) => t.#Frame(q - j));
        const end = Math.floor(s * t.Rate);
        let quiet = true;
        for (let c = 0; c < 3; ++c) {
            for (let i = 0; i < w; ++i) {
                let m = 0;
                for (let j = 0; j < K.length; ++j)
                    m += K[j] * frames[j][c * w + i];
                const v = ((m > 0 ? 20 * Math.log10(m) : -1000) + 100) / 70;
                const x = v > 0 ? (v < 1 ? v : 1) : 0;
                if (x > 0.001)
                    quiet = false;
                out[i * 3 + c] = x * scale;
            }
            const ch = t.Ch[c];
            for (let i = 0; i < w; ++i) {
                const j = end - w + i;
                const v = ((j >= 0 && j < ch.length ? ch[j] : 0) + 1) * 0.5;
                out[(w + i) * 3 + c] = (v > 0 ? (v < 1 ? v : 1) : 0) * scale;
            }
        }
        return quiet;
    }
}

/**
 * Texture provider of the sound of an audio player for MediaPlayerEffect (duck typed like MediaPlayerImageTexture: Cache(haveLod),
 * Apply(uv, size), ApplyClip(p), Dispose(), Width/Height, plus Update() before every frame and SampleRate).
 * ShaderToy's audio texture: Width (512) x 2 texels, row 0 (v = 0.25) the spectrum, row 1 (v = 0.75) the waveform; r = mono, g = left,
 * b = right. WebGL 2: 16 bit floats (RGB16F), WebGL 1: 8 bits (RGB).
 * One per WebGL context (made again after a lost context); the sound and its analyser belong to the player and outlive it.
 */
class MediaPlayerAudioTexture {

    /**
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - Effect context.
     * @param {MediaPlayerAudio} media - The player of the sound.
     */
    constructor(gl, media) {
        const t = this;
        t.GL = gl;
        t.Media = media;
        t.Texture = gl.createTexture();
        t.Width = MediaAudioAnalyser.Width;
        t.Height = 2;
        t.Gl2 = (typeof WebGL2RenderingContext !== "undefined") && (gl instanceof WebGL2RenderingContext);
        const size = t.Width * t.Height * 3;
        t.Data = t.Gl2 ? new Float32Array(size) : new Uint8ClampedArray(size);
        t.Analyser = media.GetAnalyser();
    }

    /** @returns {number} The sample rate of the sound (iSampleRate). */
    get SampleRate() {
        return this.Analyser.SampleRate;
    }

    /** @returns {boolean} The last upload was silence (see MediaAudioAnalyser.Quiet; MediaPlayerEffect.RenderTail). */
    get Quiet() {
        return this.Analyser.Quiet;
    }

    /**
     * Creates the texture (linear, clamped, no mipmaps) with the current data.
     * @returns {Promise<null>} null (the effect keeps its own size).
     */
    async Cache() {
        const t = this;
        const gl = t.GL;
        //  Offline analysis: the sound is decoded first (the first frames already show it)
        if (t.Analyser.Ready)
            await t.Analyser.Ready;
        gl.bindTexture(gl.TEXTURE_2D, t.Texture);
        t.Upload(true);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        return null;
    }

    /**
     * Reads the analyser and uploads the data into the bound texture.
     * @param {boolean} [create] - Allocate the texture (texImage2D), else replace its data.
     * @returns {void}
     */
    Upload(create) {
        const t = this;
        const gl = t.GL;
        const gl2 = t.Gl2;
        t.Analyser.Read(t.Data, gl2 ? 1 : 255);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
        const type = gl2 ? gl.FLOAT : gl.UNSIGNED_BYTE;
        if (create)
            gl.texImage2D(gl.TEXTURE_2D, 0, gl2 ? gl.RGB16F : gl.RGB, t.Width, t.Height, 0, gl.RGB, type, t.Data);
        else
            gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, t.Width, t.Height, gl.RGB, type, t.Data);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 4);
    }

    /**
     * Uploads the current sound data (called by MediaPlayerEffect.render before every frame, texture unit 0 active).
     * @returns {void}
     */
    Update() {
        const t = this;
        t.GL.bindTexture(t.GL.TEXTURE_2D, t.Texture);
        t.Upload(false);
    }

    /**
     * Binds the texture; texUv = (1, 1, 0, 0) (no flip), texSize = 512 x 2.
     * @param {WebGLUniformLocation|null} uvScaleAndOffset - texUv uniform.
     * @param {WebGLUniformLocation|null} texSize - texSize uniform.
     * @returns {void}
     */
    Apply(uvScaleAndOffset, texSize) {
        const t = this;
        const gl = t.GL;
        gl.bindTexture(gl.TEXTURE_2D, t.Texture);
        if (uvScaleAndOffset)
            gl.uniform4f(uvScaleAndOffset, 1.0, 1.0, 0.0, 0.0);
        if (texSize)
            gl.uniform2f(texSize, t.Width, t.Height);
    }

    /** No crop for sound. @returns {void} */
    ApplyClip() {
    }

    /**
     * Deletes the GL texture (the sound plays on).
     * @returns {void}
     */
    Dispose() {
        this.GL.deleteTexture(this.Texture);
    }
}

/** Publish the classes of this file under the new namespace Sw.Media (the global class names stay the public legacy API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaPlayerParamsAudio = MediaPlayerParamsAudio;
    m.MediaPlayerAudio = MediaPlayerAudio;
    m.MediaAudioAnalyser = MediaAudioAnalyser;
    m.MediaAudioSamples = MediaAudioSamples;
    m.MediaPlayerAudioTexture = MediaPlayerAudioTexture;
})();
