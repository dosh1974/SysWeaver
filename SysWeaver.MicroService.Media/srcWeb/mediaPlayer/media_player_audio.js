
/**
 * @file media_player_audio.js - HTML5 audio player (a near line-for-line copy of media_player_video.js without visuals).
 *
 * Loading: included by MediaPlayer.Init (when window.MediaLoadAll is set) or lazily by MediaPlayer.Create
 * (MediaTypes.Audio); MediaPlayerTools.CanUnMute also requires it (uses MediaPlayerAudio.LoadAudio).
 * Globals defined: MediaPlayerParamsAudio, MediaPlayerAudio, MediaAudioAnalyser (the spectrum / waveform of the sound) and
 * MediaPlayerAudioTexture (the sound as a texture of MediaPlayerEffect, ShaderToy's audio input).
 * Dependencies: MediaPlayerTools (media_player_tools.js: InitBase, OnCacheComplete), and MediaPlayerTools.waitEvent2
 * and MediaPlayerTools.delay (no SysWeaver core needed).
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
 * -> OnDispose (InitBase default, calls Stop). Shared state and default methods come from MediaPlayerTools.InitBase.
 * Instance fields: Params (MediaPlayerParamsAudio), Url/Source (the url), AtStart (positioned at the window start),
 * AtEnd (paused at the window end), LoopWatch (native-loop wrap detector, see Cache).
 */
class MediaPlayerAudio {
    /**
     * Creates the player and its muted <audio> element (not loaded until Cache).
     * Sets HaveVisual=false, HaveAudio=true, calls MediaPlayerTools.InitBase, sets element.loop = Looping,
     * CanPlay=true, Url = Source = url, AtStart = true.
     * @param {string} url - URL of the audio file.
     * @param {MediaPlayerParamsAudio|object} [params] - Partial params, merged over the defaults.
     */
    constructor(url, params) {
        const t = this;
        const cp = new MediaPlayerParamsAudio();
        if (params)
            Object.assign(cp, params);
        params = cp;
        t.Params = params;
        const e = document.createElement("audio");
        e.muted = true;
        t.HaveVisual = false;
        t.HaveAudio = true;
        MediaPlayerTools.InitBase(t, e, params);
        e.loop = this.Looping;
        t.CanPlay = true;
        t.Url = url;
        t.Source = url;
        t.AtStart = true;
    }

    /**
     * Loads the audio and installs the play-window logic. Must be awaited before using the player.
     * Sets Width=Height=0 and a 0-size element, reads Duration, seeks to StartAt (when > 0), computes the
     * effective endTime (EndAt, or the duration when EndAt <= StartAt; EndAt > duration counts as 0) and the
     * loop-watch midpoint (endTime + StartAt) / 2, then installs:
     * - ontimeupdate: updates Duration; before endTime it detects a native `loop` wrap back below the midpoint
     *   (when LoopWatch is armed) and seeks back to StartAt + OnLoop, otherwise arms LoopWatch once past the
     *   midpoint (only when StartAt > 0, looping and playing); at/after endTime it seeks to StartAt when looping,
     *   else pauses at endTime, sets AtEnd and calls OnEnded.
     * - onended: natural end while playing -> pause, AtEnd = true, OnEnded.
     * No ApplyClip (audio has no visual).
     * @param {boolean} [keepHidden] - When true the element keeps the MediaHidden class.
     * @returns {Promise<boolean>} True when loaded, false when loading failed.
     */
    async Cache(keepHidden) {

        const t = this;
        const e = t.Element;
        if (!await MediaPlayerAudio.LoadAudio(e, t.Url))
            return false;
        t.Width = 0;
        t.Height = 0;
        e.style.width = "0";
        e.style.height = "0";
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
            t.Duration = e.duration;
            const ct = e.currentTime;
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
                    t.LoopWatch = false;
                    await t.OnEnded();
                }
            }
        };
        e.onended = async ev => {
            if (!t.Paused) {
                t.Paused = true;
                t.AtEnd = true;
                e.pause();
                t.LoopWatch = false;
                await t.OnEnded();
            }
        };
        return MediaPlayerTools.OnCacheComplete(t, keepHidden);
    }

    /**
     * Loads a media URL into an <audio> (or <video>) element: appends a <source src=c> child, waits for
     * "loadeddata" or "error" (MediaPlayerTools.waitEvent2), then polls every 100 ms until readyState >= 3 (HAVE_FUTURE_DATA).
     * Also used by MediaPlayerTools.CanUnMute.
     * @param {HTMLMediaElement} e - The media element to load into.
     * @param {string} c - The source URL.
     * @returns {Promise<boolean>} False when the element fired "error", else true.
     */
    static async LoadAudio(e, c) {
        const res = await MediaPlayerTools.waitEvent2(e, "loadeddata", "error", () => {
            const source = document.createElement('source');
            source.src = c;
            //source.type = "audio/webm";
            //  A failed <source> fires "error" on the source only (it does not bubble): without this a broken url waited forever
            source.addEventListener("error", () => e.dispatchEvent(new Event("error")), { once: true });
            e.appendChild(source);
        });
        if (res.type == "error")
            return false;
        while (e.readyState < 3) {
            //  (the caller gave up waiting, ex: the sound probe of MediaPlayerTools.CanUnMute)
            if (e.SwCancelLoad)
                return false;
            await MediaPlayerTools.delay(100);
        }
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
        this.ApplyVolume();
    }

    /**
     * Unmutes the player (ApplyVolume). No-op when not muted. May be blocked by the browser autoplay policy (see
     * MediaPlayerTools.CanUnMute); a waiting analyser tries to start (a click unmutes).
     * @returns {Promise<void>}
     */
    async UnMute() {
        if (!this.Muted)
            return;
        this.Muted = false;
        this.ApplyVolume();
        this.Analyser?.TryStart();
    }

    /**
     * Sets the player volume. Clamps to 0..1, stores it in Volume (no-op when unchanged) and applies it (ApplyVolume).
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
        this.ApplyVolume();
    }

    /**
     * Applies Muted and Volume * Params.Volume * 0.01 to the sound. Without a running analyser on the element (muted, element.volume);
     * with one, on the analyser's gain: the element plays at full volume and unmuted, so the analyser gets the sound also while muted.
     * @returns {void}
     */
    ApplyVolume() {
        const t = this;
        const e = t.Element;
        const v = t.Volume * t.Params.Volume * 0.01;
        const a = t.Analyser;
        if (a && a.Live) {
            e.volume = 1;
            e.muted = false;
            a.SetGain(t.Muted ? 0 : v);
            return;
        }
        e.volume = v;
        e.muted = t.Muted;
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
        t.Analyser?.TryStart();
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
        const n = document.createElement("audio");
        n.muted = e.muted;
        n.volume = e.volume;
        if (!await MediaPlayerAudio.LoadAudio(n, this.Url)) {
            t.Paused = true;
            const res = await MediaPlayerTools.waitEvent2(e, "loadeddata", "error", () => e.load());
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
        t.Element = n;
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
     * The playback speed (1 = normal).
     * @type {number}
     */
    Rate = 1;

    /**
     * The fastest speed SetRate takes (the browsers play media up to 16 times faster).
     * @type {number}
     */
    MaxRate = 16;

    /**
     * Set the playback speed (element.playbackRate; the pitch of the sound is kept).
     * @param {number} rate The speed, 1 = normal (clamped to 0.0625..MaxRate).
     * @returns {Promise<void>}
     */
    async SetRate(rate) {
        const r = Math.max(0.0625, Math.min(this.MaxRate, rate || 1));
        this.Rate = r;
        this.Element.playbackRate = r;
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
        if (t.Live || t.Disposed)
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
    m.MediaPlayerAudioTexture = MediaPlayerAudioTexture;
})();
