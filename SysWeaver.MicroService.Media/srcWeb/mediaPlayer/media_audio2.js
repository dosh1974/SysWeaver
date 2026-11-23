/**
 * @file media_audio2.js - The audio textures of live inputs and the rich "audio" texture (the media kind audio, was audio2; legacyaudio is
 * ShaderToy's 512 x 2 texture), shared by SysWeaver's media player (MediaPlayerAudioTexture), Effect Weaver's editor (its media.js) and the
 * web pages Effect Weaver exports (embedded as it is).
 *
 * Loading: a classic (non-module) script, no dependencies; included with media_player_audio.js by MediaPlayer, by the editor's pages.
 * Globals defined: MediaLiveAudio (a microphone / line-in: ShaderToy's 512 x 2 audio texture and the samples of the audio texture),
 * MediaAudio2Engine (the 512 x 64 audio texture of a decoded sound or a live input).
 */

/**
 * A live audio input (getUserMedia: a microphone, a line-in) analysed by Web Audio analysers (not played: it would feed back).
 * ShaderToy's texture (FillShaderToy: fft 2048, smoothing 0.5, -100 .. -30 dB, the last 512 samples) and the latest samples of left and right
 * for MediaAudio2Engine (Pull: 8192 samples). A mono input: left = right.
 * A context the browser keeps suspended (no user gesture yet) is resumed by the next click / key.
 */
class MediaLiveAudio {
    /**
     * Open an input.
     * @param {string} [device] - A device id ("default" / empty: the system's default input).
     * @returns {Promise<MediaLiveAudio>} The input (rejects when the browser can't / may not use it: the error's name says why).
     */
    static async Open(device) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
            throw new Error("audio inputs can't be used here (the page needs https)");
        const id = device && device !== "default" ? device : null;
        //  The raw signal: no echo cancelling / noise suppression / gain control (music, a line-in)
        const stream = await navigator.mediaDevices.getUserMedia({ audio: { deviceId: id ? { exact: id } : undefined, echoCancellation: false, noiseSuppression: false, autoGainControl: false } });
        return new this(stream);
    }

    /** @param {MediaStream} stream - The input's stream. */
    constructor(stream) {
        const t = this;
        t.Live = true;
        t.Stream = stream;
        const AC = window.AudioContext || window.webkitAudioContext;
        t.Ctx = new AC();
        t.Rate = t.Ctx.sampleRate;
        t.Duration = Infinity;
        t.Label = (stream.getAudioTracks()[0] || {}).label || "";
        const track = stream.getAudioTracks()[0];
        t.Stereo = !!(track && track.getSettings && track.getSettings().channelCount > 1);
        t.Src = t.Ctx.createMediaStreamSource(stream);
        //  The analysers are pulled through a silent gain (an analyser must be on a running graph)
        t.Mute = t.Ctx.createGain();
        t.Mute.gain.value = 0;
        t.Mute.connect(t.Ctx.destination);
        t.An = t.Analysers(0.5, 2048);
        t.Freq = new Uint8Array(1024);
        t.Wave = new Float32Array(2048);
        t.Resume = () => t.Ctx && t.Ctx.state === "suspended" && t.Ctx.resume().catch(() => { });
        for (const ev of ["pointerdown", "keydown"])
            window.addEventListener(ev, t.Resume, { capture: true, passive: true });
        t.Resume();
    }

    /**
     * Analysers of mono, left, right (-100 .. -30 dB) with a smoothing.
     * @param {number} smoothing @param {number} size The fft size. @returns {AnalyserNode[]}
     */
    Analysers(smoothing, size) {
        const t = this;
        const mk = () => {
            const a = t.Ctx.createAnalyser();
            a.fftSize = size;
            a.smoothingTimeConstant = smoothing;
            a.minDecibels = -100;
            a.maxDecibels = -30;
            a.connect(t.Mute);
            return a;
        };
        const an = [mk(), mk(), mk()];
        t.Src.connect(an[0]);
        const split = t.Ctx.createChannelSplitter(2);
        t.Src.connect(split);
        split.connect(an[1], 0);
        split.connect(an[2], t.Stereo ? 1 : 0);
        return an;
    }

    /** The input runs whatever the effect time is. @param {number} time @returns {number} The time as it is. */
    Wrap(time) {
        return time;
    }

    /**
     * ShaderToy's audio texture now: out = 512 x 2 RGB values (row 0 the spectrum 0..1, row 1 the waveform, 0.5 = silence), times scale.
     * @param {Float32Array|Uint8Array} out - 512 * 2 * 3 values.
     * @param {number} [scale] - 1 (floats, default) or 255 (bytes).
     */
    FillShaderToy(out, scale) {
        const t = this, w = 512, sc = scale || 1;
        for (let c = 0; c < 3; ++c) {
            const a = t.An[c];
            a.getByteFrequencyData(t.Freq);
            for (let i = 0; i < w; ++i)
                out[i * 3 + c] = t.Freq[i] / 255 * sc;
            a.getFloatTimeDomainData(t.Wave);
            for (let i = 0; i < w; ++i) {
                const v = 0.5 + 0.5 * t.Wave[2048 - w + i];
                out[(w + i) * 3 + c] = (v > 0 ? (v < 1 ? v : 1) : 0) * sc;
            }
        }
    }

    /**
     * The latest 8192 samples of left and right now (MediaAudio2Engine). Its own analysers, made on first use.
     * @returns {Float32Array[]} [left, right] (reused arrays).
     */
    Pull() {
        const t = this;
        if (!t.An2) {
            t.An2 = t.Analysers(0, 8192);
            t.Time2 = [new Float32Array(8192), new Float32Array(8192)];
        }
        t.An2[1].getFloatTimeDomainData(t.Time2[0]);
        t.An2[2].getFloatTimeDomainData(t.Time2[1]);
        return t.Time2;
    }

    /** Stop the input (the browser's "recording" indicator goes away). */
    Stop() {
        const t = this;
        for (const tr of t.Stream.getTracks())
            tr.stop();
        for (const ev of ["pointerdown", "keydown"])
            window.removeEventListener(ev, t.Resume, { capture: true });
        if (t.Ctx)
            t.Ctx.close().catch(() => { });
        t.Ctx = null;
    }
}

/** A cache of the values of steps, the oldest added go first (Effect Weaver's AudioTrack2.Cache). @private */
class MediaAudio2Cache {
    /** @param {number} max The most values kept. */
    constructor(max) {
        this.Max = max;
        this.Map = new Map();
    }

    /** @param {number} k @returns {*} The value, undefined if none. */
    Get(k) {
        return this.Map.get(k);
    }

    /** @param {number} k @returns {boolean} */
    Has(k) {
        return this.Map.has(k);
    }

    /** Add a value (an existing key keeps its value). @param {number} k @param {*} v */
    Add(k, v) {
        const m = this.Map;
        if (m.has(k))
            return;
        m.set(k, v);
        while (m.size > this.Max)
            m.delete(m.keys().next().value);
    }
}

/**
 * The "audio" texture (media kind audio, was audio2): 512 x 64 RGB floats at a fixed 60 analysis steps per second of sound time (the same at
 * any frame rate, when seeking and in the video export: Effect Weaver's server, AudioTrack2.cs, is a line by line copy: keep both the same).
 * r = the mix (mono), g = left, b = right; rows marked "mix" hold the mix's value in all three. Every value is a function of the samples up to
 * its step through fixed windows / chains (the time domain filters go on from the step before when playing on: the same within rounding).
 * Rows (u slots: quarters q1 .. q4, eighths e1 .. e8, sixteenths s1 .. s16; the snippet EwAudio2 has the constants and functions):
 *    0 ..  9  spectrum, log frequency 20 Hz .. 20 kHz: 512 .. 1 bands, the band's power per 1/3 octave (8192 FFT below 300 Hz, crossfaded into a
 *             2048 one by 600 Hz), +1.5 dB / octave around 1 kHz, (dB + 74) / 58
 *   10        bass energy (30 .. 250 Hz) | BPM / 255 (mix)
 *   11        beat phase | beat pulse (mix)
 *   12        loudness (K-weighted, 400 ms) | peak | brightness | onset (mix)
 *   13        kick | snare | hi-hat pulse | mid energy
 *   14        kick | snare | hi-hat energy | tempo confidence (mix)
 *   15        (mix, eighths) short-term loudness | stereo width | sub energy | intensity | kick presence | build-up | drop | loudness range
 *   16 .. 25  waveform, the last 512 samples: 512 .. 1 segments (the sample with the largest amplitude, sign kept), 0.5 = silence
 *   26        history of the last 512 steps, newest at u = 1: r low, g mid, b high band envelopes
 *   27        (mix) relative (auto gain) bass | mid | high | loudness
 *   28        (mix) bar phase | downbeat pulse | beat in the bar / 4 | kick pulse on the beat grid
 *   29        (mix, sixteenths) chroma C .. B | tonalness | chroma hue | hue strength
 *   30        (mix, eighths) since the last kick | its strength | snare | strength | hi-hat | strength | bar confidence | -
 *   31        (mix) travel of bass | mid | high | loudness (seconds)
 *   32 .. 63  rows 0 .. 31 smoothed (attack 0.5, release 0.1 per step); phases, positions, hue, since, travel and history copied
 * Source: a decoded sound ({ Rate, Ch: [mono, left, right] Float32Arrays }) or a live input (MediaLiveAudio: Live, Rate, Pull(): the latest
 * samples are placed before the end of each new step, in a ring of ~6 s).
 */
class MediaAudio2Engine {
    static W = 512;
    static H = 64;
    static Steps = 60;
    static NL = 8192;
    static NH = 2048;
    static FLo = 20;
    static FHi = 20000;
    static DbFloor = 74;
    static DbRange = 58;
    static Tilt = 1.5;
    static LowF0 = 300;
    static LowF1 = 600;
    static PulseDecay = 0.85;
    static Attack = 0.5;
    static Release = 0.1;
    static Hist = 360;
    static TempoEvery = 15;
    static MaxLag = 240;
    static Chain = 8;
    static Pll = 150;
    static SmWarm = 48;
    static HitWarm = 4096;
    static Long = 960;
    static PMin = 18;
    static PMax = 60;
    static PStep = 0.25;
    static PllGain = 0.06;
    static TrigRise = [3, 2, 1.5];
    static TrigGate = [4, 4, 4];
    static Refractory = [6, 6, 3];
    static TrigScale = 6;
    static PresLo = -10;
    static PresHi = 0;
    static BarBeats = 32;
    static TravelK = 120;
    static HistoryLen = 512;
    static RelWin = 480;
    static IntensityWarm = 600;
    static RateWin = 120;
    static EnvFloor = 60;
    static EnvRange = 50;
    static HitKeep = 6144;
    /** The steps of a block of the per step sums (Cum) */
    static CumBlock = 1024;
    /** The steps of sound before a step that its values read (the longest history: the bar chain); the video export sends them */
    static Lead = 2 * 960 + (32 + 2) * 60 + 160;
    /** The steps of history before a step that the values other than the bar read (the intensity: its warm up, the loudness range) */
    static Near = 600 + 960 + 180 + 48 + 64;
    /** Frequency ranges (Hz) of the energies: bass, mid, kick, snare, hi-hat, sub, high */
    static EnergyDefs = [[30, 250], [250, 2000], [45, 110], [1500, 6000], [8000, 16000], [25, 55], [4000, 16000]];
    /** The slots that aren't smoothed (phases, positions, the tempo, hue, since, travel, history): [row, u0, u1] */
    static Unsmoothed = [[10, 0.5, 1], [11, 0, 0.5], [26, 0, 1], [28, 0, 0.25], [28, 0.5, 0.75], [29, 13 / 16, 14 / 16], [30, 0, 0.125], [30, 0.25, 0.375], [30, 0.5, 0.625], [31, 0, 1]];

    /** src: a decoded sound ({ Rate, Ch }) or a live input (MediaLiveAudio) */
    constructor(src) {
        const t = this, E = MediaAudio2Engine, W = E.W;
        t.Src = src;
        t.Rate = src.Rate;
        t.Live = !!src.Live;
        if (t.Live) {
            t.RingSize = 1 << 18;
            t.Ring = [new Float32Array(t.RingSize), new Float32Array(t.RingSize), new Float32Array(t.RingSize)];
            t.RingEnd = -Infinity;
        }
        else
            t.Ch = src.Ch;
        t.Hi = Math.min(E.FHi, t.Rate * 0.5 * 0.999);
        t.WOct = Math.log2(t.Hi / E.FLo) / W;
        t.LowW = new Float64Array(W);
        t.TiltLin = new Float64Array(W);
        t.K0L = new Float64Array(W);
        t.K1L = new Float64Array(W);
        t.K0H = new Float64Array(W);
        t.K1H = new Float64Array(W);
        for (let b = 0; b < W; ++b) {
            const f0 = E.FLo * Math.pow(t.Hi / E.FLo, b / W), f1 = E.FLo * Math.pow(t.Hi / E.FLo, (b + 1) / W), fc = Math.sqrt(f0 * f1);
            t.K0L[b] = f0 * E.NL / t.Rate;
            t.K1L[b] = f1 * E.NL / t.Rate;
            t.K0H[b] = f0 * E.NH / t.Rate;
            t.K1H[b] = f1 * E.NH / t.Rate;
            t.LowW[b] = E.Clamp01(Math.log2(E.LowF1 / fc));
            //  Per 1/3 octave (the band is WOct wide), the tilt; x 2: a full scale sine (power 0.5) is 0 dB
            t.TiltLin[b] = 2 * (1 / 3) / t.WOct * Math.pow(10, E.Tilt * Math.log2(fc / 1000) / 10);
        }
        t.Range = E.EnergyDefs.map(([f0, f1]) => {
            const b0 = t.BandOf(f0);
            return [b0, Math.max(b0 + 1, t.BandOf(f1))];
        });
        //  (the bins of the bands, BandPowerT)
        t.TabL = E.BandTable(t.K0L, t.K1L, E.NL / 2);
        t.TabH = E.BandTable(t.K0H, t.K1H, E.NH / 2);
        t.FL = MediaAudio2Engine.Fft(E.NL);
        t.FH = MediaAudio2Engine.Fft(E.NH);
        t.WinL = E.Blackman(E.NL);
        t.WinH = E.Blackman(E.NH);
        const R = t.Rate;
        //  0, 1 kick 45 .. 110 Hz; 2 .. 5 snare crack 2 .. 5 kHz (4th order); 6, 7 snare body 150 .. 300 Hz; 8, 9 hi-hat > 8 kHz (4th order);
        //  10, 11 K-weighting
        t.Filters = [E.HighPass(R, 45), E.LowPass(R, 110),
            E.HighPass(R, 2000), E.HighPass(R, 2000), E.LowPass(R, 5000), E.LowPass(R, 5000),
            E.HighPass(R, 150), E.LowPass(R, 300),
            E.HighPass(R, 8000), E.HighPass(R, 8000),
            E.KShelf(R), E.KHighPass(R)];
        //  (the coefficients in one array, 5 per filter: Hits)
        t.Cf = new Float64Array(t.Filters.length * 5);
        t.Filters.forEach((q, j) => t.Cf.set(q, j * 5));
        t.EnvA10 = 1 - Math.exp(-1 / (0.01 * R));
        t.EnvA20 = 1 - Math.exp(-1 / (0.02 * R));
        t.Raw = new Float32Array(W * 32 * 3);
        t.Sm = new Float32Array(W * 32 * 3);
        t.SmStep = null;
        //  (the full analysis of the last steps: the texture, the hue; the history reads the light Mix)
        t.StepCache = new MediaAudio2Cache(128);
        t.MixCache = new MediaAudio2Cache(1024);
        //  (scratch: the power spectra of a step)
        t.PL = [new Float64Array(E.NL / 2), new Float64Array(E.NL / 2), new Float64Array(E.NL / 2)];
        t.PH = [new Float64Array(E.NH / 2), new Float64Array(E.NH / 2), new Float64Array(E.NH / 2)];
        t.PMid = new Float64Array(E.NH / 2);
        t.PSide = new Float64Array(E.NH / 2);
        t.MixQ = new Float64Array(W);
        t.HitCache = [new MediaAudio2Cache(E.HitKeep), new MediaAudio2Cache(512), new MediaAudio2Cache(512)];
        t.Runs = [null, null, null];
        t.Blocks = new MediaAudio2Cache(E.Chain + 32);
        t.TempoCache = new MediaAudio2Cache(64);
        t.CombCache = new MediaAudio2Cache(E.Pll + 64);
        t.OnsetCache = new MediaAudio2Cache(900);
        t.PresCache = new MediaAudio2Cache(2048);
        t.StCache = new MediaAudio2Cache(2048);
        t.IntensityCache = new MediaAudio2Cache(E.IntensityWarm + 64);
        t.RelCache = new MediaAudio2Cache(E.TravelK + 64);
        t.RateCache = new MediaAudio2Cache(2048);
        t.MomCache = new MediaAudio2Cache(2048);
        t.CumCache = new MediaAudio2Cache(16);
        t.BarMemo = null;
        //  (the live input's last step pulled)
        t.Pulled = -Infinity;
    }

    static Clamp01(v) {
        return v > 0 ? (v < 1 ? v : 1) : 0;
    }

    /** A level in dB → 0 .. 1 */
    static MapDb(db) {
        const E = MediaAudio2Engine;
        return E.Clamp01((db + E.DbFloor) / E.DbRange);
    }

    static Db(p) {
        return p > 1e-20 ? 10 * Math.log10(p) : -200;
    }

    static MapLufs(lufs) {
        return MediaAudio2Engine.Clamp01((lufs + 45) / 40);
    }

    static Blackman(n) {
        const w = new Float64Array(n);
        let sum = 0, sum2 = 0;
        for (let i = 0; i < n; ++i) {
            w[i] = 0.42 - 0.5 * Math.cos(2 * Math.PI * i / n) + 0.08 * Math.cos(4 * Math.PI * i / n);
            sum += w[i];
            sum2 += w[i] * w[i];
        }
        w.Sum = sum;
        w.Sum2 = sum2;
        return w;
    }

    /** A complex radix 2 FFT of n points: { N, Re, Im, Run() } (in place) */
    static Fft(n) {
        const f = { N: n, Re: new Float64Array(n), Im: new Float64Array(n), Cos: new Float64Array(n / 2), Sin: new Float64Array(n / 2), Rev: new Int32Array(n) };
        const bits = Math.round(Math.log2(n));
        for (let i = 0; i < n; ++i) {
            let x = i, y = 0;
            for (let b = 0; b < bits; ++b) {
                y = (y << 1) | (x & 1);
                x >>= 1;
            }
            f.Rev[i] = y;
        }
        for (let i = 0; i < n / 2; ++i) {
            f.Cos[i] = Math.cos(-2 * Math.PI * i / n);
            f.Sin[i] = Math.sin(-2 * Math.PI * i / n);
        }
        f.Run = () => {
            const N = f.N, Re = f.Re, Im = f.Im, Rev = f.Rev, Cos = f.Cos, Sin = f.Sin;
            for (let i = 0; i < N; ++i) {
                const j = Rev[i];
                if (j > i) {
                    let v = Re[i];
                    Re[i] = Re[j];
                    Re[j] = v;
                    v = Im[i];
                    Im[i] = Im[j];
                    Im[j] = v;
                }
            }
            for (let size = 2; size <= N; size <<= 1) {
                const half = size >> 1, step = N / size;
                for (let i = 0; i < N; i += size) {
                    for (let j = 0, k = 0; j < half; ++j, k += step) {
                        const a = i + j, b = a + half;
                        const tr = Re[b] * Cos[k] - Im[b] * Sin[k];
                        const ti = Re[b] * Sin[k] + Im[b] * Cos[k];
                        Re[b] = Re[a] - tr;
                        Im[b] = Im[a] - ti;
                        Re[a] += tr;
                        Im[a] += ti;
                    }
                }
            }
        };
        return f;
    }

    /** A biquad [b0, b1, b2, a1, a2] (normalized) */
    static Make(b0, b1, b2, a0, a1, a2) {
        return [b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0];
    }

    static LowPass(rate, f, q) {
        q = q || 0.7071067811865476;
        const w = 2 * Math.PI * f / rate, cs = Math.cos(w), al = Math.sin(w) / (2 * q);
        return MediaAudio2Engine.Make((1 - cs) / 2, 1 - cs, (1 - cs) / 2, 1 + al, -2 * cs, 1 - al);
    }

    static HighPass(rate, f, q) {
        q = q || 0.7071067811865476;
        const w = 2 * Math.PI * f / rate, cs = Math.cos(w), al = Math.sin(w) / (2 * q);
        return MediaAudio2Engine.Make((1 + cs) / 2, -(1 + cs), (1 + cs) / 2, 1 + al, -2 * cs, 1 - al);
    }

    /** The K-weighting high shelf of BS.1770 at any rate (the closed form) */
    static KShelf(rate) {
        const k = Math.tan(Math.PI * 1681.974450955533 / rate), vh = Math.pow(10, 3.999843853973347 / 20), vb = Math.pow(vh, 0.4996667741545416), q = 0.7071752369554196;
        return MediaAudio2Engine.Make(vh + vb * k / q + k * k, 2 * (k * k - vh), vh - vb * k / q + k * k, 1 + k / q + k * k, 2 * (k * k - 1), 1 - k / q + k * k);
    }

    static KHighPass(rate) {
        return MediaAudio2Engine.HighPass(rate, 38.13547087602444, 0.5003270373238773);
    }

    /** The log band of a frequency (clamped) */
    BandOf(f) {
        const E = MediaAudio2Engine;
        return Math.max(0, Math.min(E.W, Math.floor(E.W * Math.log(f / E.FLo) / Math.log(this.Hi / E.FLo) + 0.5)));
    }

    /** The last sample (exclusive) of step n */
    EndOf(n) {
        return Math.floor(n * this.Rate / MediaAudio2Engine.Steps + 0.5);
    }

    /** A sample of channel c (0 mix, 1 left, 2 right) at a position (0 outside the sound / the live ring) */
    Sample(c, k) {
        if (this.Live) {
            if (k >= this.RingEnd || k < this.RingEnd - this.RingSize || k < 0)
                return 0;
            //  (positions pass 2^31 in a long live session: no bit operations)
            return this.Ring[c][k % this.RingSize];
        }
        const x = this.Ch[c];
        return k >= 0 && k < x.length ? x[k] : 0;
    }

    /** count samples of channel c from position s0 (0 outside the sound / the live ring, as Sample): a view of the decoded samples when
     * they are all inside, else a copy */
    Span(c, s0, count) {
        const t = this;
        if (!t.Live) {
            const x = t.Ch[c];
            if (s0 >= 0 && s0 + count <= x.length)
                return x.subarray(s0, s0 + count);
        }
        const out = new Float32Array(Math.max(0, count));
        for (let i = 0; i < count; ++i)
            out[i] = t.Sample(c, s0 + i);
        return out;
    }

    /** A decoded sound's step n when it reads only the silence before the start (its windows end at or before sample 0): the analysis shared
     * by all of them ({ Mix, Step }, filled by the first one computed), else null. A live input: null */
    Silent(n) {
        const t = this;
        if (t.Live || t.EndOf(n) > 0)
            return null;
        return t.SilentData || (t.SilentData = { Mix: null, Step: null });
    }

    /** A live input: its latest samples placed before the end of step n (once per new step) */
    Pull(n) {
        const t = this;
        if (!t.Live || n <= t.Pulled)
            return;
        t.Pulled = n;
        const [l, r] = t.Src.Pull(), len = l.length, end = t.EndOf(n), m = t.RingSize;
        for (let i = 0; i < len; ++i) {
            const k = (end - len + i) % m;
            t.Ring[1][k] = l[i];
            t.Ring[2][k] = r[i];
            t.Ring[0][k] = (l[i] + r[i]) * 0.5;
        }
        t.RingEnd = Math.max(t.RingEnd, end);
    }

    // ------------------------------------------------------------------ spectrum

    /** The power per bin (Parseval scaled) of the mix, left and right for the n samples ending before end (DC removed), one complex FFT of
     * left + i right; also the mid / side powers (for the width) */
    Psd(f, win, end, psd, mid, side) {
        const n = f.N, start = end - n, Re = f.Re, Im = f.Im;
        const L = this.Span(1, start, n), R = this.Span(2, start, n);
        let ml = 0, mr = 0;
        for (let i = 0; i < n; ++i) {
            ml += L[i] * win[i];
            mr += R[i] * win[i];
        }
        ml /= win.Sum;
        mr /= win.Sum;
        for (let i = 0; i < n; ++i) {
            Re[i] = (L[i] - ml) * win[i];
            Im[i] = (R[i] - mr) * win[i];
        }
        f.Run();
        const scale = 2 / (n * win.Sum2), p0 = psd[0], p1 = psd[1], p2 = psd[2];
        for (let k = 1; k < n / 2; ++k) {
            //  Two real signals in one complex FFT: L = (Z[k] + conj Z[n-k]) / 2, R = (Z[k] - conj Z[n-k]) / 2i
            const ar = Re[k], ai = Im[k], br = Re[n - k], bi = -Im[n - k];
            const lr = (ar + br) * 0.5, li = (ai + bi) * 0.5;
            const rr = (ai - bi) * 0.5, ri = -(ar - br) * 0.5;
            p1[k] = (lr * lr + li * li) * scale;
            p2[k] = (rr * rr + ri * ri) * scale;
            const mr2 = (lr + rr) * 0.5, mi2 = (li + ri) * 0.5;
            p0[k] = (mr2 * mr2 + mi2 * mi2) * scale;
            if (mid) {
                const sr2 = (lr - rr) * 0.5, si2 = (li - ri) * 0.5;
                mid[k] = p0[k];
                side[k] = (sr2 * sr2 + si2 * si2) * scale;
            }
        }
        p0[0] = p1[0] = p2[0] = 0;
    }

    /** The bins and weights of the bands [K0[b], K1[b]) of a spectrum of nb bins, as BandPower reads them (in its order: the same sums):
     * { Off (the first entry of band b, W + 1), Bin, Wt, NI (a band narrower than a bin: its bin, else -1), NF, NW } */
    static BandTable(K0, K1, nb) {
        const W = K0.length, off = new Int32Array(W + 1), bins = [], wts = [], ni = new Int32Array(W), nf = new Float64Array(W), nw = new Float64Array(W);
        for (let b = 0; b < W; ++b) {
            off[b] = bins.length;
            const k0 = K0[b], k1 = K1[b];
            ni[b] = -1;
            if (k1 - k0 < 1) {
                const c = Math.min(nb - 1.001, (k0 + k1) * 0.5), i = Math.floor(c);
                ni[b] = i;
                nf[b] = c - i;
                nw[b] = k1 - k0;
                continue;
            }
            const e = Math.min(nb - 1, Math.ceil(k1 + 0.5));
            for (let k = Math.max(0, Math.floor(k0 - 0.5)); k <= e; ++k) {
                const o = Math.min(k + 0.5, k1) - Math.max(k - 0.5, k0);
                if (o > 0) {
                    bins.push(k);
                    wts.push(o);
                }
            }
        }
        off[W] = bins.length;
        return { Off: off, Bin: Int32Array.from(bins), Wt: Float64Array.from(wts), NI: ni, NF: nf, NW: nw };
    }

    /** The power of band b of a spectrum p by its table (BandTable): BandPower */
    static BandPowerT(p, tb, b) {
        const i = tb.NI[b];
        if (i >= 0) {
            const fr = tb.NF[b];
            return (p[i] * (1 - fr) + p[i + 1] * fr) * tb.NW[b];
        }
        const bin = tb.Bin, wt = tb.Wt, e = tb.Off[b + 1];
        let s = 0;
        for (let j = tb.Off[b]; j < e; ++j)
            s += p[bin[j]] * wt[j];
        return s;
    }

    /** The power of a band [k0, k1) (bin coordinates): the bins' sum, fractional at the edges; narrower than a bin: interpolated x width */
    static BandPower(p, k0, k1) {
        const nb = p.length;
        if (k1 - k0 < 1) {
            const c = Math.min(nb - 1.001, (k0 + k1) * 0.5), i = Math.floor(c), fr = c - i;
            return (p[i] * (1 - fr) + p[i + 1] * fr) * (k1 - k0);
        }
        let s = 0;
        const e = Math.min(nb - 1, Math.ceil(k1 + 0.5));
        for (let k = Math.max(0, Math.floor(k0 - 0.5)); k <= e; ++k) {
            const o = Math.min(k + 0.5, k1) - Math.max(k - 0.5, k0);
            if (o > 0)
                s += p[k] * o;
        }
        return s;
    }

    /** The mean of Q over bands [b0, b1) in dB */
    static EnergyDb(q, b0, b1) {
        let s = 0;
        for (let b = b0; b < b1; ++b)
            s += q[b];
        return MediaAudio2Engine.Db(s / (b1 - b0));
    }

    /** The light analysis of the mix at step n (cached), from the 2048 FFT alone (cheap: what the history reads, the onsets and the relative
     * values): { G (64 groups of 8 bands, the tilted power sums: the spectral flux), En (the energies, dB) }. ph: the power spectra of the
     * step when known (Step), else computed */
    Mix(n, ph) {
        const t = this, E = MediaAudio2Engine, W = E.W;
        let m = t.MixCache.Get(n);
        if (m)
            return m;
        //  (a step before the sound's start: silence, the same values at every such step, computed once: Silent)
        const silent = t.Silent(n);
        if (silent && silent.Mix)
            return silent.Mix;
        if (!ph) {
            ph = t.PH;
            t.Psd(t.FH, t.WinH, t.EndOf(n), ph, null, null);
        }
        const q = t.MixQ, p = ph[0];
        for (let b = 0; b < W; ++b)
            q[b] = E.BandPowerT(p, t.TabH, b) * t.TiltLin[b];
        const g = new Float64Array(64);
        for (let i = 0; i < 64; ++i) {
            let s = 0;
            for (let j = 0; j < 8; ++j)
                s += q[i * 8 + j];
            g[i] = s;
        }
        m = { G: g, En: t.Range.map(([b0, b1]) => E.EnergyDb(q, b0, b1)) };
        if (silent)
            silent.Mix = m;
        else
            t.MixCache.Add(n, m);
        return m;
    }

    /** The spectral analysis of step n (cached): { Q[c] (tilted band powers), En[c] (energies, dB), Wave[c], Peak[c], Chroma, Tonal, Width } */
    Step(n) {
        const t = this, E = MediaAudio2Engine, W = E.W, NL = E.NL, NH = E.NH;
        let s = t.StepCache.Get(n);
        if (s)
            return s;
        const silent = t.Silent(n);
        if (silent && silent.Step)
            return silent.Step;
        s = { Q: [], En: [], Wave: [], Peak: [], Chroma: new Float64Array(12), Tonal: 0, Width: 0 };
        const end = t.EndOf(n);
        const pl = t.PL, ph = t.PH, mid = t.PMid, side = t.PSide;
        t.Psd(t.FL, t.WinL, end, pl, null, null);
        t.Psd(t.FH, t.WinH, end, ph, mid, side);
        //  (the light analysis of the history, from the same 2048 FFT)
        t.Mix(n, ph);
        for (let c = 0; c < 3; ++c) {
            const q = new Float64Array(W);
            for (let b = 0; b < W; ++b) {
                const lw = t.LowW[b];
                const p = (lw > 0 ? lw * E.BandPowerT(pl[c], t.TabL, b) : 0) + (lw < 1 ? (1 - lw) * E.BandPowerT(ph[c], t.TabH, b) : 0);
                q[b] = p * t.TiltLin[b];
            }
            s.Q[c] = q;
            s.En[c] = t.Range.map(([b0, b1]) => E.EnergyDb(q, b0, b1));
            const wave = new Float32Array(W);
            let peak = 0;
            const xs = t.Span(c, end - NH, NH);
            for (let i = 0; i < NH; ++i) {
                const v = xs[i];
                peak = Math.max(peak, Math.abs(v));
                if (i >= NH - W)
                    wave[i - (NH - W)] = v;
            }
            s.Wave[c] = wave;
            s.Peak[c] = Math.min(1, peak);
        }
        //  Tonalness: the spectral flatness of 220 Hz .. 4 kHz in dB, -3 .. -30 dB → 0 .. 1. Chroma: the spectral peaks (6 dB over the bins 3
        //  away) of 220 Hz .. 4 kHz, weighted log(1 + 1000 amplitude), by pitch class (C = 0), max = 1, times the tonalness
        const p0 = pl[0], k0 = Math.ceil(220 * NL / t.Rate), k1 = Math.min(NL / 2 - 4, Math.floor(4000 * NL / t.Rate));
        let lsum = 0, sum = 0;
        for (let k = k0; k <= k1; ++k) {
            lsum += Math.log(p0[k] + 1e-20);
            sum += p0[k];
        }
        const cnt = k1 - k0 + 1;
        const flat = sum > 1e-20 ? Math.exp(lsum / cnt) / (sum / cnt) : 1;
        s.Tonal = E.Clamp01((-10 * Math.log10(Math.max(1e-12, flat)) - 3) / 27);
        for (let k = k0; k <= k1; ++k) {
            const p = p0[k];
            if (!(p > p0[k - 1] && p >= p0[k + 1] && p >= 4 * Math.max(p0[k - 3], p0[k + 3]) && p > 1e-12))
                continue;
            //  The peak between the bins (parabola on the dB values)
            const a = E.Db(p0[k - 1]), b = E.Db(p), c = E.Db(p0[k + 1]), den = a - 2 * b + c;
            const off = den < 0 ? Math.max(-0.5, Math.min(0.5, 0.5 * (a - c) / den)) : 0;
            const f = (k + off) * t.Rate / NL;
            const pc = ((Math.floor(12 * Math.log2(f / 440) + 0.5) % 12) + 21) % 12;
            s.Chroma[pc] += Math.log(1 + 1000 * Math.sqrt(p));
        }
        let cmax = 0;
        for (let i = 0; i < 12; ++i)
            cmax = Math.max(cmax, s.Chroma[i]);
        for (let i = 0; i < 12; ++i)
            s.Chroma[i] = cmax > 1e-12 ? s.Chroma[i] / cmax * s.Tonal : 0;
        //  Stereo width above 200 Hz: side / (mid + side) power
        let sm = 0, ss = 0;
        for (let k = Math.ceil(200 * NH / t.Rate); k < NH / 2; ++k) {
            sm += mid[k];
            ss += side[k];
        }
        s.Width = sm + ss > 1e-12 ? ss / (sm + ss) : 0;
        if (silent)
            silent.Step = s;
        else
            t.StepCache.Add(n, s);
        return s;
    }

    // ------------------------------------------------------------------ hits / loudness (time domain filters)

    /**
     * The time domain analysis of step n of channel c: { Kick, Crack, Body, Hat (envelopes in dB, the step's max), Kms (K-weighted mean square),
     * Raw, Trig }. The steps n0 .. n run through the filters in one go (from HitWarm samples before, or on from the end of the step before
     * when it was the last one run), the ones not cached yet are stored.
     */
    Hits(n, c, n0) {
        const t = this, E = MediaAudio2Engine, cache = t.HitCache[c];
        let h = cache.Get(n);
        if (h)
            return h;
        if (n0 === undefined || n0 < 0 || n0 > n)
            n0 = n;
        //  A step before the sound's start: silence (the filters of zeros stay at zero: the values the run would give)
        if (t.Silent(n)) {
            h = { Kick: -200, Crack: -200, Body: -200, Hat: -200, Kms: 0, KmsR: 0, Raw: [-1, -1, -1], Trig: [-1, -1, -1] };
            cache.Add(n, h);
            return h;
        }
        if (!t.Live && n0 < 1)
            n0 = 1;
        while (n0 < n && cache.Has(n0))
            ++n0;
        const run = t.Runs[c], nf = t.Filters.length;
        let fs, env, s0;
        if (run && run.Step === n0 - 1) {
            fs = run.Fs;
            env = run.Env;
            s0 = t.EndOf(n0 - 1);
        }
        else {
            fs = new Float64Array((nf + 2) * 4);
            env = new Float64Array(4);
            s0 = t.EndOf(n0 - 1) - E.HitWarm;
            //  (the zeros before the sound leave the states at zero)
            if (!t.Live && s0 < 0)
                s0 = 0;
        }
        const s1 = t.EndOf(n), len = s1 - s0;
        //  The samples (the mix: also left and right for its K-weighting), the coefficients (5 per filter), the states (4 per filter, the
        //  right channel's K-weighting at 12, 13): the filters inlined (a closure per biquad was 10x slower)
        const xs = t.Span(c, s0, len), ls = c === 0 ? t.Span(1, s0, len) : null, rs = c === 0 ? t.Span(2, s0, len) : null, cf = t.Cf;
        let step = n0, stepStart = t.EndOf(n0 - 1), stepEnd = t.EndOf(n0);
        let m0 = 0, m1 = 0, m2 = 0, m3 = 0, kms = 0, kmsR = 0;
        let e0 = env[0], e1 = env[1], e2 = env[2], e3 = env[3];
        const a10 = t.EnvA10, a20 = t.EnvA20, mix = c === 0;
        for (let j = 0; j < len; ++j) {
            const i = s0 + j, x = xs[j], xw = mix ? ls[j] : x;
            const k0 = cf[0] * x + cf[1] * fs[0] + cf[2] * fs[1] - cf[3] * fs[2] - cf[4] * fs[3];
            fs[1] = fs[0]; fs[0] = x; fs[3] = fs[2]; fs[2] = k0;
            const yk = cf[5] * k0 + cf[6] * fs[4] + cf[7] * fs[5] - cf[8] * fs[6] - cf[9] * fs[7];
            fs[5] = fs[4]; fs[4] = k0; fs[7] = fs[6]; fs[6] = yk;
            const c0 = cf[10] * x + cf[11] * fs[8] + cf[12] * fs[9] - cf[13] * fs[10] - cf[14] * fs[11];
            fs[9] = fs[8]; fs[8] = x; fs[11] = fs[10]; fs[10] = c0;
            const c1 = cf[15] * c0 + cf[16] * fs[12] + cf[17] * fs[13] - cf[18] * fs[14] - cf[19] * fs[15];
            fs[13] = fs[12]; fs[12] = c0; fs[15] = fs[14]; fs[14] = c1;
            const c2 = cf[20] * c1 + cf[21] * fs[16] + cf[22] * fs[17] - cf[23] * fs[18] - cf[24] * fs[19];
            fs[17] = fs[16]; fs[16] = c1; fs[19] = fs[18]; fs[18] = c2;
            const yc = cf[25] * c2 + cf[26] * fs[20] + cf[27] * fs[21] - cf[28] * fs[22] - cf[29] * fs[23];
            fs[21] = fs[20]; fs[20] = c2; fs[23] = fs[22]; fs[22] = yc;
            const b0 = cf[30] * x + cf[31] * fs[24] + cf[32] * fs[25] - cf[33] * fs[26] - cf[34] * fs[27];
            fs[25] = fs[24]; fs[24] = x; fs[27] = fs[26]; fs[26] = b0;
            const yb = cf[35] * b0 + cf[36] * fs[28] + cf[37] * fs[29] - cf[38] * fs[30] - cf[39] * fs[31];
            fs[29] = fs[28]; fs[28] = b0; fs[31] = fs[30]; fs[30] = yb;
            const h0 = cf[40] * x + cf[41] * fs[32] + cf[42] * fs[33] - cf[43] * fs[34] - cf[44] * fs[35];
            fs[33] = fs[32]; fs[32] = x; fs[35] = fs[34]; fs[34] = h0;
            const yh = cf[45] * h0 + cf[46] * fs[36] + cf[47] * fs[37] - cf[48] * fs[38] - cf[49] * fs[39];
            fs[37] = fs[36]; fs[36] = h0; fs[39] = fs[38]; fs[38] = yh;
            const w0 = cf[50] * xw + cf[51] * fs[40] + cf[52] * fs[41] - cf[53] * fs[42] - cf[54] * fs[43];
            fs[41] = fs[40]; fs[40] = xw; fs[43] = fs[42]; fs[42] = w0;
            const yw = cf[55] * w0 + cf[56] * fs[44] + cf[57] * fs[45] - cf[58] * fs[46] - cf[59] * fs[47];
            fs[45] = fs[44]; fs[44] = w0; fs[47] = fs[46]; fs[46] = yw;
            let ywr = 0;
            if (mix) {
                const xr = rs[j];
                const r0 = cf[50] * xr + cf[51] * fs[48] + cf[52] * fs[49] - cf[53] * fs[50] - cf[54] * fs[51];
                fs[49] = fs[48]; fs[48] = xr; fs[51] = fs[50]; fs[50] = r0;
                const ywr0 = cf[55] * r0 + cf[56] * fs[52] + cf[57] * fs[53] - cf[58] * fs[54] - cf[59] * fs[55];
                fs[53] = fs[52]; fs[52] = r0; fs[55] = fs[54]; fs[54] = ywr0;

                ywr = ywr0;
            }
            e0 += a20 * (yk * yk - e0);
            e1 += a10 * (yc * yc - e1);
            e2 += a10 * (yb * yb - e2);
            e3 += a10 * (yh * yh - e3);
            if (i < stepStart)
                continue;
            if (e0 > m0)
                m0 = e0;
            if (e1 > m1)
                m1 = e1;
            if (e2 > m2)
                m2 = e2;
            if (e3 > m3)
                m3 = e3;
            kms += yw * yw;
            kmsR += ywr * ywr;
            if (i === stepEnd - 1) {
                if (!cache.Has(step))
                    cache.Add(step, { Kick: E.Db(2 * m0), Crack: E.Db(2 * m1), Body: E.Db(2 * m2), Hat: E.Db(2 * m3), Kms: kms / Math.max(1, stepEnd - stepStart), KmsR: kmsR / Math.max(1, stepEnd - stepStart), Raw: [-1, -1, -1], Trig: [-1, -1, -1] });
                ++step;
                stepStart = stepEnd;
                stepEnd = t.EndOf(step);
                m0 = m1 = m2 = m3 = kms = kmsR = 0;
            }
        }
        env[0] = e0;
        env[1] = e1;
        env[2] = e2;
        env[3] = e3;
        t.Runs[c] = { Step: n, Fs: fs, Env: env };
        return cache.Get(n) || { Kick: -200, Crack: -200, Body: -200, Hat: -200, Kms: 0, Raw: [0, 0, 0], Trig: [0, 0, 0] };
    }

    static HitValue(h, k) {
        return k === 0 ? h.Kick : k === 1 ? h.Crack : h.Hat;
    }

    /** A hit candidate of kind k (0 kick, 1 snare, 2 hi-hat) at step n of channel c: the envelope's rise over the steps 2 and 3 before (a snare
     * also needs its body to rise 0.5 dB), above -60 dB and TrigGate dB over the 20th percentile of the last 30 steps; its strength */
    RawTrigger(n, c, k) {
        const t = this, E = MediaAudio2Engine, h = t.Hits(n, c, n - 40);
        if (h.Raw[k] >= 0)
            return h.Raw[k];
        let v = 0;
        const e = E.HitValue(h, k), h2 = t.Hits(n - 2, c), h3 = t.Hits(n - 3, c);
        const r = e - Math.max(E.HitValue(h2, k), E.HitValue(h3, k));
        if (e >= -60 && r > E.TrigRise[k] && (k !== 1 || h.Body - Math.max(h2.Body, h3.Body) > 0.5)) {
            const hist = new Float64Array(30);
            for (let i = 0; i < 30; ++i)
                hist[i] = E.HitValue(t.Hits(n - 1 - i, c), k);
            hist.sort();
            if (e >= hist[6] + E.TrigGate[k])
                v = E.Clamp01((r - E.TrigRise[k]) / E.TrigScale);
        }
        h.Raw[k] = v;
        return v;
    }

    /** A hit of kind k at step n: the candidate, unless there was a candidate within the refractory steps before it (an attack is a
     * candidate on 2 steps: the first fires) */
    Trigger(n, c, k) {
        const t = this, E = MediaAudio2Engine, h = t.Hits(n, c, n - 48);
        if (h.Trig[k] >= 0)
            return h.Trig[k];
        let v = t.RawTrigger(n, c, k);
        if (v > 0)
            for (let i = 1; i <= E.Refractory[k]; ++i)
                if (t.RawTrigger(n - i, c, k) > 0) {
                    v = 0;
                    break;
                }
        h.Trig[k] = v;
        return v;
    }

    /** A pulse: the hits of the last 24 steps, decaying (x 0.85 per step) */
    Pulse(n, c, k) {
        let p = 0;
        for (let i = n - 24; i <= n; ++i)
            p = Math.max(this.Trigger(i, c, k), p * MediaAudio2Engine.PulseDecay);
        return p;
    }

    /** [steps since the last hit of kind k (at most max), its strength] */
    Since(n, k, max) {
        for (let i = 0; i < max; ++i) {
            const v = this.Trigger(n - i, 0, k);
            if (v > 0)
                return [i, v];
        }
        return [max, 0];
    }

    /** The K-weighted loudness (LUFS) over the steps n - count + 1 .. n: channel 1 / 2 alone, the mix (0) = left + right power */
    Loudness(n, c, count) {
        const t = this;
        let s = 0;
        if (c === 0)
            s = t.CumSum(0, n - count + 1, n);
        else {
            t.Hits(n, c, n - count);
            for (let i = 0; i < count; ++i)
                s += t.Hits(n - i, c).Kms;
        }
        return -0.691 + MediaAudio2Engine.Db(s / count);
    }

    /** The per step sums of the mix over its block of CumBlock steps up to step n (blocks aligned to multiples of CumBlock: the same values
     * at any seek), cached per block: [0] the K-weighted power (left + right mean squares), [1] the kick envelope (dB, at least -90) */
    Cum(kind, n) {
        const t = this, B = MediaAudio2Engine.CumBlock, b = Math.floor(n / B), base = b * B, i = n - base;
        let blk = t.CumCache.Get(b);
        if (!blk) {
            blk = { A: [new Float64Array(B), new Float64Array(B)], Len: 0 };
            t.CumCache.Add(b, blk);
        }
        if (i >= blk.Len) {
            t.Hits(n, 0, base + blk.Len);
            const a0 = blk.A[0], a1 = blk.A[1];
            let s0 = blk.Len > 0 ? a0[blk.Len - 1] : 0, s1 = blk.Len > 0 ? a1[blk.Len - 1] : 0;
            for (let k = blk.Len; k <= i; ++k) {
                const h = t.Hits(base + k, 0);
                s0 += h.Kms + h.KmsR;
                s1 += Math.max(-90, h.Kick);
                a0[k] = s0;
                a1[k] = s1;
            }
            blk.Len = i + 1;
        }
        return blk.A[kind][i];
    }

    /** The sum of a per step value of the mix (Cum) over the steps a .. b (at most CumBlock steps) */
    CumSum(kind, a, b) {
        const t = this, B = MediaAudio2Engine.CumBlock, ba = Math.floor(a / B);
        const before = a - ba * B > 0 ? t.Cum(kind, a - 1) : 0;
        if (Math.floor(b / B) === ba)
            return t.Cum(kind, b) - before;
        return t.Cum(kind, ba * B + B - 1) - before + t.Cum(kind, b);
    }

    /** The short-term (3 s) loudness of the mix at step n */
    ShortTerm(n) {
        let v = this.StCache.Get(n);
        if (v === undefined) {
            v = this.Loudness(n, 0, 180);
            this.StCache.Add(n, v);
        }
        return v;
    }

    /** The short-term loudness at step n within its range of the last 16 s (every 15 steps, at least 6 dB): 0 .. 1 */
    LoudnessRange(n) {
        const st = this.ShortTerm(n);
        let lmin = st, lmax = st;
        for (let i = 15; i <= MediaAudio2Engine.Long; i += 15) {
            const l = this.ShortTerm(n - i);
            lmin = Math.min(lmin, l);
            lmax = Math.max(lmax, l);
        }
        return MediaAudio2Engine.Clamp01((st - lmin) / Math.max(6, lmax - lmin));
    }

    // ------------------------------------------------------------------ onsets, tempo, beat

    /** The onset strength of the mix at step n (dB): kick rises x 4, snare x 1.5, hi-hat x 0.25, plus the spectral flux (64 log bands) */
    Onset(n) {
        const t = this, E = MediaAudio2Engine;
        let v = t.OnsetCache.Get(n);
        if (v !== undefined)
            return v;
        const a = t.Hits(n, 0, n - E.Hist - 8), b = t.Hits(n - 1, 0);
        const o = 4 * Math.max(0, a.Kick - b.Kick) + 1.5 * Math.max(0, a.Crack - b.Crack) + 0.25 * Math.max(0, a.Hat - b.Hat);
        const ga = t.Mix(n).G, gb = t.Mix(n - 1).G;
        let fl = 0;
        for (let i = 0; i < 64; ++i)
            fl += Math.max(0, Math.max(-60, E.Db(ga[i] / 8)) - Math.max(-60, E.Db(gb[i] / 8)));
        v = o + fl / 64;
        t.OnsetCache.Add(n, v);
        return v;
    }

    static Interp(a, x) {
        if (x <= 0)
            return a[0];
        const i = Math.floor(x);
        if (i >= a.length - 1)
            return a[a.length - 1];
        const f = x - i;
        return a[i] * (1 - f) + a[i + 1] * f;
    }

    /** The tempo analysis of the block ending at step m: the normalized autocorrelation of the detrended onsets of the last Hist steps, the
     * score of every period PMin .. PMax (the autocorrelation at 1 .. 4 x the period, a prior around 122 BPM) */
    Block(m) {
        const t = this, E = MediaAudio2Engine, H = E.Hist;
        let bl = t.Blocks.Get(m);
        if (bl)
            return bl;
        const o = new Float64Array(H);
        for (let i = 0; i < H; ++i)
            o[i] = t.Onset(m - H + 1 + i);
        const d = new Float64Array(H);
        for (let i = 0; i < H; ++i) {
            let s = 0, c = 0;
            for (let j = Math.max(0, i - 6); j <= Math.min(H - 1, i + 6); ++j, ++c)
                s += o[j];
            d[i] = Math.max(0, o[i] - s / c);
        }
        let mean = 0;
        for (let i = 0; i < H; ++i)
            mean += d[i];
        mean /= H;
        for (let i = 0; i < H; ++i)
            d[i] -= mean;
        const ac = new Float64Array(E.MaxLag + 2);
        for (let lag = 0; lag <= E.MaxLag + 1; ++lag) {
            let s = 0;
            for (let i = lag; i < H; ++i)
                s += d[i] * d[i - lag];
            ac[lag] = s / (H - lag);
        }
        const scores = new Float64Array(Math.round((E.PMax - E.PMin) / E.PStep) + 1);
        let best = 0;
        for (let j = 0; j < scores.length; ++j) {
            const p = E.PMin + j * E.PStep;
            let s = 0, cnt = 0;
            for (let k = 1; k <= 4 && k * p <= E.MaxLag; ++k, ++cnt)
                s += E.Interp(ac, k * p);
            const bpm = 60 * E.Steps / p, oct = Math.log2(bpm / 122) / 0.7;
            scores[j] = s / cnt * Math.exp(-0.5 * oct * oct);
            if (scores[j] > scores[best])
                best = j;
        }
        bl = { Scores: scores, Ac: ac, Ac0: ac[0], Best: best };
        t.Blocks.Add(m, bl);
        return bl;
    }

    static Related(a, b) {
        const r = a / b;
        return Math.abs(r - 2) < 0.15 || Math.abs(r - 0.5) < 0.04 || Math.abs(r - 1.5) < 0.1 || Math.abs(r - 2 / 3) < 0.05 || Math.abs(r - 3) < 0.2 || Math.abs(r - 1 / 3) < 0.03;
    }

    /** The tempo at step n: [bpm, period (steps), confidence]. The period of the block follows the blocks before it (a fixed chain of Chain
     * blocks): it changes when another period scores 15 % higher (40 % for a related one: 2:1, 3:2, 3:1); refined between the periods */
    Tempo(n) {
        const t = this, E = MediaAudio2Engine, TE = E.TempoEvery;
        const m = n - (((n % TE) + TE) % TE);
        let r = t.TempoCache.Get(m);
        if (r)
            return r;
        let idx = t.Block(m - E.Chain * TE).Best;
        for (let i = E.Chain - 1; i >= 0; --i) {
            const bl = t.Block(m - i * TE), period = E.PMin + idx * E.PStep;
            const lo = Math.max(0, Math.floor((period * 0.97 - E.PMin) / E.PStep)), hi = Math.min(bl.Scores.length - 1, Math.ceil((period * 1.03 - E.PMin) / E.PStep));
            let loc = lo;
            for (let j = lo; j <= hi; ++j)
                if (bl.Scores[j] > bl.Scores[loc])
                    loc = j;
            const sl = bl.Scores[loc], sb = bl.Scores[bl.Best];
            //  (a half / double / 3:2 tempo isn't taken while the kick is away: rolls and fills of a build-up double it)
            const related = E.Related(E.PMin + bl.Best * E.PStep, E.PMin + loc * E.PStep);
            const th = related ? (t.Presence(m - i * TE) < 0.3 ? Number.MAX_VALUE : 1.4) : 1.15;
            idx = sl <= 0 || sb > th * sl ? bl.Best : loc;
        }
        const last = t.Block(m);
        let conf = 0, bpm = 0, per = 30;
        if (last.Ac0 > 1e-9) {
            const sc = last.Scores;
            let off = 0;
            if (idx > 0 && idx < sc.length - 1) {
                const a = sc[idx - 1], b = sc[idx], c = sc[idx + 1], den = a - 2 * b + c;
                off = den < 0 ? Math.max(-0.5, Math.min(0.5, 0.5 * (a - c) / den)) : 0;
            }
            per = E.PMin + (idx + off) * E.PStep;
            conf = E.Clamp01(E.Interp(last.Ac, per) / last.Ac0);
            bpm = 60 * E.Steps / per;
        }
        r = [bpm, per, conf];
        t.TempoCache.Add(m, r);
        return r;
    }

    /** The raw beat phase at step n: the offset whose comb of the onsets of the last Hist steps (recent beats weigh more) is the strongest
     * (parabola between the offsets), / the period */
    Comb(n) {
        const t = this, E = MediaAudio2Engine;
        let r = t.CombCache.Get(n);
        if (r !== undefined)
            return r;
        const T = t.Tempo(n)[1], H = E.Hist;
        const o = new Float64Array(H);
        for (let i = 0; i < H; ++i)
            o[i] = t.Onset(n - i);
        const beats = Math.max(1, Math.floor((H - 2) / T) - 1), cnt = Math.ceil(T / E.PStep);
        const sc = new Float64Array(cnt);
        let best = 0;
        for (let j = 0; j < cnt; ++j) {
            const f = j * E.PStep;
            let s = 0;
            for (let b = 0; b < beats; ++b)
                s += Math.exp(-b * T / 120) * E.Interp(o, f + b * T);
            sc[j] = s;
            if (s > sc[best])
                best = j;
        }
        const y0 = sc[(best - 1 + cnt) % cnt], y1 = sc[best], y2 = sc[(best + 1) % cnt], den = y0 - 2 * y1 + y2;
        const off = den < 0 ? Math.max(-0.5, Math.min(0.5, 0.5 * (y0 - y2) / den)) : 0;
        r = (best + off) * E.PStep / T;
        r -= Math.floor(r);
        t.CombCache.Add(n, r);
        return r;
    }

    /** The beat phase at step n (0 on a beat .. 1): a phase locked loop over the last Pll steps, advancing by 1 / period per step and pulled
     * towards the comb by PllGain of the error */
    Phase(n) {
        const t = this, E = MediaAudio2Engine;
        let ph = t.Comb(n - E.Pll);
        for (let i = n - E.Pll + 1; i <= n; ++i) {
            ph += 1 / t.Tempo(i)[1];
            ph -= Math.floor(ph);
            let e = t.Comb(i) - ph;
            e -= Math.floor(e + 0.5);
            ph += E.PllGain * e;
            ph -= Math.floor(ph);
        }
        return ph;
    }

    /** The bar at step n: [beat index in the bar 0 .. 3, confidence 0 .. 1] (a function of the current beat's step, the anchor and the period:
     * computed once per beat) */
    Bar(n, phase, T) {
        const t = this, L = MediaAudio2Engine.Long;
        const last = Math.floor(n - phase * T + 0.5), anchor = Math.floor((n - L) / L) * L;
        const bm = t.BarMemo;
        if (bm && bm[0] === last && bm[1] === anchor && bm[2] === T)
            return [bm[3], bm[4]];
        const r = t.BarOf(n, last, anchor, T);
        t.BarMemo = [last, anchor, T, r[0], r[1]];
        return r;
    }

    /**
     * The bar: a beat's score as each beat of its bar comes from the BarBeats completed beats before it: the kick, a crash (hi-hat band hit)
     * and a loudness rise on the downbeat, the snare on beats 2 and 4. A chain from the first beat after an anchor step (a multiple of Long,
     * Long .. 2 Long steps back): the bar goes on (+1 per beat) unless another position scores 30 % higher at 2 beats in a row.
     */
    BarOf(n, last, anchor, T) {
        const t = this, E = MediaAudio2Engine, BB = E.BarBeats;
        //  The beats back from the current one: a period back from the one after, moved to the strongest hit within 20 % of a period (so the
        //  walk follows the beats through tempo changes); the chain starts at the first beat after the anchor
        const maxBeats = Math.ceil((last - anchor) / T) + BB + 2;
        const pos = new Float64Array(maxBeats + 1);
        pos[0] = last;
        let chain = 0;
        for (let k = 1; k <= maxBeats; ++k) {
            let x = pos[k - 1] - T;
            const i0 = Math.floor(x - 0.2 * T + 0.5), i1 = Math.min(n - 3, Math.floor(x + 0.2 * T + 0.5));
            let bestV = 0;
            for (let i = i0; i <= i1; ++i) {
                const v = t.Trigger(i, 0, 0) + t.Trigger(i, 0, 1) + 0.5 * t.Trigger(i, 0, 2);
                if (v > bestV) {
                    bestV = v;
                    x = i;
                }
            }
            pos[k] = x;
            if (x >= anchor)
                chain = k;
        }
        chain = Math.max(1, chain);
        const beats = Math.min(maxBeats, chain + BB);
        const kick = new Float64Array(beats + 1), snare = new Float64Array(beats + 1), hat = new Float64Array(beats + 1), lift = new Float64Array(beats + 1);
        for (let k = 1; k <= beats; ++k) {
            const at = Math.floor(pos[k] + 0.5);
            for (let i = at - 2; i <= Math.min(n, at + 2); ++i) {
                kick[k] = Math.max(kick[k], t.Trigger(i, 0, 0));
                snare[k] = Math.max(snare[k], t.Trigger(i, 0, 1));
                hat[k] = Math.max(hat[k], t.Trigger(i, 0, 2));
            }
            //  The loudness after the beat against before it (dB, up only, 6 dB = 1)
            if (at + 8 <= n)
                lift[k] = E.Clamp01((t.Loudness(at + 8, 0, 8) - t.Loudness(at - 2, 0, 8)) / 6);
        }
        const scores = b => {
            const sc = [0, 0, 0, 0];
            for (let k = b + 1; k <= Math.min(beats, b + BB); ++k) {
                const w = Math.exp(-(k - b) / 32);
                for (let ib = 0; ib < 4; ++ib) {
                    const rel = (((ib - (k - b)) % 4) + 4) % 4;
                    sc[ib] += w * (rel === 0 ? kick[k] - snare[k] + 0.5 * hat[k] + lift[k] : rel === 2 ? -0.5 * snare[k] : snare[k]);
                }
            }
            return sc;
        };
        const bestOf = sc => {
            let best = 0;
            for (let j = 1; j < 4; ++j)
                if (sc[j] > sc[best])
                    best = j;
            return best;
        };
        let idx = bestOf(scores(chain)), pending = -1, cur = scores(chain);
        for (let b = chain - 1; b >= 0; --b) {
            cur = scores(b);
            const pred = (idx + 1) % 4, best = bestOf(cur);
            if (best !== pred && cur[best] > cur[pred] + 0.3 * Math.abs(cur[best]) + 0.05) {
                //  (a new position must win at 2 beats in a row)
                if (pending === best) {
                    idx = best;
                    pending = -1;
                    continue;
                }
                pending = (best + 1) % 4;
            }
            else
                pending = -1;
            idx = pred;
        }
        let tot = 0, second = -Number.MAX_VALUE;
        for (let j = 0; j < 4; ++j) {
            tot += Math.abs(cur[j]);
            if (j !== idx)
                second = Math.max(second, cur[j]);
        }
        return [idx, tot > 1e-9 ? E.Clamp01((cur[idx] - second) / tot * 2) : 0];
    }

    // ------------------------------------------------------------------ sections

    /** The kick presence at step n: the kick band's envelope over the last 1 s against the short-term loudness, PresLo .. PresHi dB → 0 .. 1 */
    Presence(n) {
        const t = this, E = MediaAudio2Engine;
        let p = t.PresCache.Get(n);
        if (p !== undefined)
            return p;
        const s = t.CumSum(1, n - E.Steps + 1, n);
        const st = t.ShortTerm(n);
        p = st < -60 ? 0 : E.Clamp01((s / E.Steps - st - E.PresLo) / (E.PresHi - E.PresLo));
        t.PresCache.Add(n, p);
        return p;
    }

    /** A drop at step i: a kick hit (0.3 or more) after a breakdown (the presence below 0.3, not silent, for 6 of the 8 s before the last 0.5 s) and louder
     * than every kick of it */
    DropAt(i) {
        const t = this;
        if (t.Trigger(i, 0, 0) < 0.3 || t.Presence(i - 1) >= 0.5)
            return false;
        let low = 0, kmax = -200;
        for (let j = i - 510; j < i - 30; j += 4) {
            if (t.Presence(j) < 0.3 && t.ShortTerm(j) > -35)
                ++low;
            kmax = Math.max(kmax, t.Hits(j, 0).Kick);
        }
        return low >= 90 && t.Hits(i, 0).Kick > kmax;
    }

    /** The drop at step n: 1 on a drop, falling linearly to 0 over a bar (4 beats) */
    Drop(n, T) {
        const bar = Math.ceil(4 * T);
        for (let i = 0; i < bar; ++i)
            if (this.DropAt(n - i))
                return 1 - i / bar;
        return 0;
    }

    /** Hits per step (kick, snare / 2, hi-hat / 4) over the RateWin steps up to step n (playing on: from the step before) */
    HitRate(n) {
        const t = this, W = MediaAudio2Engine.RateWin;
        let v = t.RateCache.Get(n);
        if (v !== undefined)
            return v;
        const hit = i => t.Trigger(i, 0, 0) + 0.5 * t.Trigger(i, 0, 1) + 0.25 * t.Trigger(i, 0, 2);
        const p = t.RateCache.Get(n - 1);
        if (p !== undefined)
            v = p + (hit(n) - hit(n - W)) / W;
        else {
            let d = 0;
            for (let i = 0; i < W; ++i)
                d += hit(n - i);
            v = d / W;
        }
        t.RateCache.Add(n, v);
        return v;
    }

    /** The momentary (400 ms) loudness of the mix at step n */
    Momentary(n) {
        let v = this.MomCache.Get(n);
        if (v === undefined) {
            v = this.Loudness(n, 0, 24);
            this.MomCache.Add(n, v);
        }
        return v;
    }

    /** The mean of an envelope (kind k) over the 60 steps up to step n (dB) */
    EnvMean(n, k) {
        let m = 0;
        for (let i = 0; i < 60; ++i)
            m += Math.max(-90, MediaAudio2Engine.HitValue(this.Hits(n - i, 0), k));
        return m / 60;
    }

    /** The build-up at step n: over 8 s the snare / hi-hat hit rate and the hi-hat band rising, while the kick is away (or its hits get
     * denser: a kick roll), 0 for 4 bars after a drop */
    BuildUp(n, pres, T) {
        const t = this, bar = Math.ceil(4 * T);
        for (let i = 0; i < 4 * bar; ++i)
            if (t.DropAt(n - i))
                return 0;
        const rate = (t.HitRate(n) - t.HitRate(n - 360)) * 20;
        const hat = (t.EnvMean(n, 2) - t.EnvMean(n - 420, 2)) / 12;
        let kickNow = 0, kickThen = 0;
        for (let i = 0; i < 120; ++i) {
            kickNow += t.Trigger(n - i, 0, 0);
            kickThen += t.Trigger(n - 360 - i, 0, 0);
        }
        return MediaAudio2Engine.Clamp01(rate + hat) * (kickNow > kickThen + 0.5 ? Math.max(1 - pres, 0.3) : 1 - pres);
    }

    /** The intensity at step n before smoothing: the loudness range position, the kick presence, the hit rate */
    IntensityRaw(n) {
        const t = this;
        let v = t.IntensityCache.Get(n);
        if (v === undefined) {
            v = 0.5 * t.LoudnessRange(n) + 0.3 * t.Presence(n) + 0.2 * MediaAudio2Engine.Clamp01(t.HitRate(n) * 20);
            t.IntensityCache.Add(n, v);
        }
        return v;
    }

    /** The intensity: IntensityRaw followed with an attack of 0.5 s and a release of 3 s, over the last IntensityWarm steps */
    Intensity(n) {
        const E = MediaAudio2Engine, w = E.IntensityWarm;
        const a = 1 - Math.exp(-1 / (0.5 * E.Steps)), r = 1 - Math.exp(-1 / (3 * E.Steps));
        let v = this.IntensityRaw(n - w);
        for (let i = n - w + 1; i <= n; ++i) {
            const x = this.IntensityRaw(i);
            v += (x > v ? a : r) * (x - v);
        }
        return v;
    }

    /** The relative (auto gain) bass, mid, high, loudness at step n: the value between the 10th and 95th percentile of the last 8 s (every 4
     * steps, a span of at least 12 dB, 6 dB for the loudness) */
    Relative(n) {
        const t = this, E = MediaAudio2Engine;
        let r = t.RelCache.Get(n);
        if (r)
            return r;
        r = [0, 0, 0, 0];
        const cnt = E.RelWin / 4, h = new Float64Array(cnt);
        for (let k = 0; k < 4; ++k) {
            const val = i => k < 3 ? t.Mix(i).En[k === 0 ? 0 : k === 1 ? 1 : 6] : t.Momentary(i);
            const cur = val(n);
            for (let i = 0; i < cnt; ++i)
                h[i] = val(n - i * 4);
            h.sort();
            const p10 = h[Math.floor(cnt / 10)], p95 = h[Math.floor(cnt * 95 / 100)];
            r[k] = cur < -70 ? 0 : E.Clamp01((cur - p10) / Math.max(k < 3 ? 12 : 6, p95 - p10));
        }
        t.RelCache.Add(n, r);
        return r;
    }

    /** The travel of the relative value k at step n (seconds): Σ e (K - age) / K / Steps over the last K steps. It changes by (e - the mean
     * of the last K) / Steps per step: time + travel runs faster when the value is above its recent mean, never backwards */
    Travel(n, k) {
        const K = MediaAudio2Engine.TravelK;
        let s = 0;
        for (let age = 0; age < K; ++age)
            s += this.Relative(n - age)[k] * (K - age) / K;
        return s / MediaAudio2Engine.Steps;
    }

    /** The chroma hue of the last 0.5 s: [the circular mean of the pitch classes (0 .. 1, C = 0), its strength (0 .. 1)] */
    Hue(n) {
        let x = 0, y = 0, tot = 0;
        for (let i = 0; i < 30; ++i) {
            const ch = this.Step(n - i).Chroma;
            for (let p = 0; p < 12; ++p) {
                x += ch[p] * Math.cos(2 * Math.PI * p / 12);
                y += ch[p] * Math.sin(2 * Math.PI * p / 12);
                tot += ch[p];
            }
        }
        if (tot < 1e-9)
            return [0, 0];
        const hue = Math.atan2(y, x) / (2 * Math.PI);
        return [hue - Math.floor(hue), MediaAudio2Engine.Clamp01(Math.sqrt(x * x + y * y) / tot)];
    }

    // ------------------------------------------------------------------ the texture

    /** The raw rows 0 .. 31 of step n into out (512 x 32 x 3) */
    RawRows(n, out) {
        const t = this, E = MediaAudio2Engine, W = E.W;
        out.fill(0);
        const s = t.Step(n);
        const put = (row, u0, u1, c, v) => {
            const o = row * W;
            for (let x = Math.round(u0 * W); x < Math.round(u1 * W); ++x)
                out[(o + x) * 3 + c] = v;
        };
        const mix = (row, u0, u1, v) => {
            for (let c = 0; c < 3; ++c)
                put(row, u0, u1, c, v);
        };
        const tempo = t.Tempo(n), T = tempo[1], phase = t.Phase(n);
        for (let c = 0; c < 3; ++c) {
            let q = s.Q[c];
            for (let l = 0; l < 10; ++l) {
                const cnt = W >> l, span = 1 << l, o = l * W;
                if (l > 0) {
                    const qq = new Float64Array(cnt);
                    for (let i = 0; i < cnt; ++i)
                        qq[i] = (q[2 * i] + q[2 * i + 1]) * 0.5;
                    q = qq;
                }
                for (let i = 0; i < cnt; ++i) {
                    const v = E.MapDb(E.Db(q[i]));
                    for (let x = 0; x < span; ++x)
                        out[(o + i * span + x) * 3 + c] = v;
                }
            }
            const w = s.Wave[c];
            for (let l = 0; l < 10; ++l) {
                const span = 1 << l, o = (16 + l) * W;
                for (let i = 0; i < W; i += span) {
                    let m = 0;
                    for (let x = 0; x < span; ++x)
                        if (Math.abs(w[i + x]) > Math.abs(m))
                            m = w[i + x];
                    const vv = E.Clamp01(0.5 + 0.5 * m);
                    for (let x = 0; x < span; ++x)
                        out[(o + i + x) * 3 + c] = vv;
                }
            }
            const en = s.En[c];
            put(10, 0, 0.5, c, E.MapDb(en[0]));
            put(12, 0, 0.25, c, E.MapLufs(c === 0 ? t.Momentary(n) : t.Loudness(n, c, 24)));
            put(12, 0.25, 0.5, c, s.Peak[c]);
            //  Brightness: the centroid of the band powers on the log axis
            let sp = 0, sw = 0;
            const qc = s.Q[c];
            for (let b = 0; b < W; ++b) {
                sp += qc[b] * b;
                sw += qc[b];
            }
            put(12, 0.5, 0.75, c, sw > 0 ? sp / sw / (W - 1) : 0);
            put(13, 0, 0.25, c, t.Pulse(n, c, 0));
            put(13, 0.25, 0.5, c, t.Pulse(n, c, 1));
            put(13, 0.5, 0.75, c, t.Pulse(n, c, 2));
            put(13, 0.75, 1, c, E.MapDb(en[1]));
            put(14, 0, 0.25, c, E.MapDb(en[2]));
            put(14, 0.25, 0.5, c, E.MapDb(en[3]));
            put(14, 0.5, 0.75, c, E.MapDb(en[4]));
        }
        //  The mix
        mix(10, 0.5, 1, Math.min(1, tempo[0] / 255));
        mix(11, 0, 0.5, phase);
        mix(11, 0.5, 1, Math.pow(E.PulseDecay, phase * T));
        mix(12, 0.75, 1, E.Clamp01(t.Onset(n) / 25));
        mix(14, 0.75, 1, tempo[2]);
        //  Row 15: short-term loudness, width, sub, intensity, kick presence, build-up, drop, loudness range position
        const pres = t.Presence(n), drop = t.Drop(n, T);
        mix(15, 0, 0.125, E.MapLufs(t.ShortTerm(n)));
        mix(15, 0.125, 0.25, s.Width);
        mix(15, 0.25, 0.375, E.MapDb(s.En[0][5]));
        mix(15, 0.375, 0.5, t.Intensity(n));
        mix(15, 0.5, 0.625, pres);
        mix(15, 0.625, 0.75, t.BuildUp(n, pres, T));
        mix(15, 0.75, 0.875, drop);
        mix(15, 0.875, 1, t.LoudnessRange(n));
        //  Row 26: the history of the band envelopes (r low, g mid, b high), newest at u = 1
        for (let x = 0; x < E.HistoryLen; ++x) {
            const h = t.Hits(n - (E.HistoryLen - 1 - x), 0), o = (26 * W + x) * 3;
            out[o] = E.Clamp01((h.Kick + E.EnvFloor) / E.EnvRange);
            out[o + 1] = E.Clamp01((h.Crack + E.EnvFloor) / E.EnvRange);
            out[o + 2] = E.Clamp01((h.Hat + E.EnvFloor) / E.EnvRange);
        }
        //  Row 27: relative bass, mid, high, loudness
        const rel = t.Relative(n);
        for (let k = 0; k < 4; ++k)
            mix(27, k / 4, (k + 1) / 4, rel[k]);
        //  Row 28: bar phase, downbeat pulse, beat in the bar, kick on the grid
        const [idx, barConf] = tempo[0] > 0 ? t.Bar(n, phase, T) : [0, 0];
        mix(28, 0, 0.25, (idx + phase) / 4);
        mix(28, 0.25, 0.5, Math.pow(E.PulseDecay, (idx + phase) * T));
        mix(28, 0.5, 0.75, idx / 4);
        mix(28, 0.75, 1, t.Pulse(n, 0, 0) * (phase < 0.15 || phase > 0.85 ? 1 : 0));
        //  Row 29: chroma, tonalness, hue, hue strength
        for (let i = 0; i < 12; ++i)
            mix(29, i / 16, (i + 1) / 16, s.Chroma[i]);
        mix(29, 12 / 16, 13 / 16, s.Tonal);
        const [hue, hs] = t.Hue(n);
        mix(29, 13 / 16, 14 / 16, hue);
        mix(29, 14 / 16, 15 / 16, hs);
        //  Row 30: since the last kick / snare / hi-hat (seconds / 2, at most 2 s) and its strength, the bar confidence
        for (let k = 0; k < 3; ++k) {
            const [since, str] = t.Since(n, k, 2 * E.Steps);
            mix(30, k / 4, k / 4 + 0.125, since / (2 * E.Steps));
            mix(30, k / 4 + 0.125, (k + 1) / 4, str);
        }
        mix(30, 0.75, 0.875, barConf);
        //  Row 31: travel of bass, mid, high, loudness
        for (let k = 0; k < 4; ++k)
            mix(31, k / 4, (k + 1) / 4, t.Travel(n, k));
    }

    /** One step of the smoothed rows: attack / release */
    SmoothStep() {
        const E = MediaAudio2Engine, raw = this.Raw, sm = this.Sm, half = E.W * 32 * 3;
        for (let i = 0; i < half; ++i) {
            const d = raw[i] - sm[i];
            sm[i] += (d > 0 ? E.Attack : E.Release) * d;
        }
    }

    /** The texture at step n into out (512 x 64 x 3): the raw rows, the smoothed rows (incremental while playing on, else from SmWarm steps
     * back), the slots that aren't smoothed copied */
    Fill(out, n) {
        const t = this, E = MediaAudio2Engine, W = E.W, half = W * 32 * 3;
        t.Pull(n);
        //  The filters run over the history in one go (a cold start: seeking), then one step at a time: the near history (tempo, intensity,
        //  sections), then back to where the bar chain reads at this tempo (its anchor and BarBeats + 2 beats, at most Lead; what is read
        //  further back is filtered when it is read)
        const run0 = t.Runs[0];
        if (run0 && run0.Step === n - 1)
            t.Hits(n, 0, n);
        else if (!t.HitCache[0].Has(n)) {
            t.Hits(n, 0, n - E.Near);
            const T = t.Tempo(n)[1], anchor = Math.floor((n - E.SmWarm - E.Long) / E.Long) * E.Long;
            const far = Math.max(n - E.Lead, anchor - Math.ceil((E.BarBeats + 2) * T * 1.2) - 48);
            if (far < n - E.Near) {
                //  (playing on continues the near run)
                const run = t.Runs[0];
                t.Hits(n - E.Near - 1, 0, far);
                t.Runs[0] = run;
            }
        }
        //  (on from the step before when it is at most SmWarm steps back, as the warm start would compute them: at a low frame rate a frame
        //  advances several steps, the warm start would cost SmWarm steps every frame)
        if (t.SmStep !== null && n > t.SmStep && n - t.SmStep <= E.SmWarm) {
            for (let k = t.SmStep + 1; k <= n; ++k) {
                t.RawRows(k, t.Raw);
                t.SmoothStep();
            }
        }
        else if (t.SmStep !== n) {
            t.Sm.fill(0);
            for (let k = n - E.SmWarm; k <= n; ++k) {
                t.RawRows(k, t.Raw);
                t.SmoothStep();
            }
        }
        //  (the same step: Raw / Sm hold it already)
        t.SmStep = n;
        out.set(t.Raw, 0);
        out.set(t.Sm, half);
        for (const [row, u0, u1] of E.Unsmoothed)
            for (let x = Math.round(u0 * W); x < Math.round(u1 * W); ++x)
                for (let c = 0; c < 3; ++c)
                    out[((row + 32) * W + x) * 3 + c] = t.Raw[(row * W + x) * 3 + c];
    }
}


/** Publish the classes of this file under the namespace Sw.Media (the global class names stay the public API). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    const m = Sw.Media = Sw.Media || {};
    m.MediaLiveAudio = MediaLiveAudio;
    m.MediaAudio2Engine = MediaAudio2Engine;
})();
