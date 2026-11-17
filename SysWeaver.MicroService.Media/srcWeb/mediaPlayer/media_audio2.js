/**
 * @file media_audio2.js - The audio textures of live inputs and the rich "audio2" texture, shared by SysWeaver's media player
 * (MediaPlayerAudioTexture), Effect Weaver's editor (its media.js) and the web pages Effect Weaver exports (embedded as it is).
 *
 * Loading: a classic (non-module) script, no dependencies; included with media_player_audio.js by MediaPlayer, by the editor's pages.
 * Globals defined: MediaLiveAudio (a microphone / line-in: ShaderToy's 512 x 2 audio texture and the raw analysis of audio2),
 * MediaAudio2Engine (the 512 x 64 audio2 texture of a decoded sound or a live input).
 */

/**
 * A live audio input (getUserMedia: a microphone, a line-in) analysed by Web Audio analysers (not played: it would feed back).
 * ShaderToy's texture (FillShaderToy: fft 2048, smoothing 0.5, -100 .. -30 dB, the last 512 samples) and the raw analysis of the audio2
 * texture (Analyze2: no smoothing, MediaAudio2Engine smooths). r / g / b = mono / left / right (a mono input: left = right).
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
        t.An = t.Analysers(0.5);
        t.Freq = new Uint8Array(1024);
        t.Wave = new Float32Array(2048);
        t.Resume = () => t.Ctx && t.Ctx.state === "suspended" && t.Ctx.resume().catch(() => { });
        for (const ev of ["pointerdown", "keydown"])
            window.addEventListener(ev, t.Resume, { capture: true, passive: true });
        t.Resume();
    }

    /** Analysers of mono, left, right (fft 2048, -100 .. -30 dB) with a smoothing. @param {number} smoothing @returns {AnalyserNode[]} */
    Analysers(smoothing) {
        const t = this;
        const mk = () => {
            const a = t.Ctx.createAnalyser();
            a.fftSize = 2048;
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
     * The raw analysis of now for MediaAudio2Engine: per channel the power spectrum in dB (1024 bins, no smoothing) and the last 2048
     * samples. Its own analysers, made on first use.
     * @returns {{Db: Float32Array[], Time: Float32Array[]}} The data (reused arrays).
     */
    Analyze2() {
        const t = this;
        if (!t.An2) {
            t.An2 = t.Analysers(0);
            t.Db2 = [0, 1, 2].map(() => new Float32Array(1024));
            t.Time2 = [0, 1, 2].map(() => new Float32Array(2048));
        }
        for (let c = 0; c < 3; ++c) {
            t.An2[c].getFloatFrequencyData(t.Db2[c]);
            t.An2[c].getFloatTimeDomainData(t.Time2[c]);
        }
        return { Db: t.Db2, Time: t.Time2 };
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

/**
 * The "audio2" texture (media kind audio2): 512 x 64 RGB floats (r = mono, g = left, b = right), computed at a fixed 60 analysis steps per
 * second of sound time (the same result at any frame rate, when seeking and in the video export: Effect Weaver's server, AudioTrack2.cs, mirrors this exactly).
 * Rows (v = (row + 0.5) / 64, the snippet EwAudio2 has the constants):
 *    0 ..  9  spectrum, log frequency 20 Hz .. 20 kHz: 512, 256, ... 1 bands (a band of row r spans 2^r pixels), level -100 .. -30 dB → 0 .. 1
 *   10        bass energy (u < 0.5) | tempo BPM / 255 (u >= 0.5)
 *   11        beat phase 0..1 | beat pulse (1 on a beat, decays)
 *   12        level (RMS, -60 .. 0 dB) | peak | brightness (spectral centroid, 0..1 of the log axis) | onset (spectral flux)
 *   13        kick pulse | snare pulse | hi-hat pulse | mid energy (quarters)
 *   14        kick energy | snare energy | hi-hat energy | tempo confidence
 *   16 .. 25  waveform, the last 512 samples: 512, 256, ... 1 segments, each the sample with the largest amplitude (with its sign), 0.5 = silence
 *   26        hi-hat energy (the whole row)
 *   15, 27 .. 31 reserved (0)
 *   32 .. 63  rows 0 .. 31 smoothed: s += 0.2 * (x - s) per analysis step (MediaAudio2Engine.Smooth)
 * The tempo / beat values are of the mono mix (the same in r, g, b).
 * Source: a decoded sound ({ Rate, Ch: [mono, left, right] Float32Arrays }: deterministic) or a live input (MediaLiveAudio: Live, Rate,
 * Analyze2(); one step per 1/60 s of the clock).
 */
class MediaAudio2Engine {
    static W = 512;
    static H = 64;
    static Steps = 60;
    static N = 2048;
    static Bins = 1024;
    static FLo = 20;
    static FHi = 20000;
    static Smooth = 0.2;
    /** Steps the smoothing / pulses are computed back from after a seek (0.8^24 = 0.5 %) */
    static Warm = 24;
    /** The onset history of the tempo (6 s) and how often the tempo is estimated (4 / s) */
    static Hist = 360;
    static TempoEvery = 15;
    /** The longest lag of the tempo's autocorrelation: 4 x the slowest period (60 BPM = 60 steps) */
    static MaxLag = 240;
    static PulseDecay = 0.85;
    /** Frequency ranges (Hz) of the energies / pulses */
    static Bands = { Bass: [20, 150], Mid: [250, 2000], Kick: [40, 120], Snare: [1000, 5000], HiHat: [6000, 16000] };

    /** src: a decoded sound ({ Rate, Ch }) or a live input (MediaLiveAudio) */
    constructor(src) {
        const t = this, E = MediaAudio2Engine;
        t.Src = src;
        t.Rate = src.Rate;
        t.Live = !!src.Live;
        //  The log bands in bins (bin k = k * rate / N Hz): [K0, K1) of each band
        const hi = Math.min(E.FHi, t.Rate * 0.5 * 0.999);
        t.Hi = hi;
        t.K0 = new Float64Array(E.W);
        t.K1 = new Float64Array(E.W);
        for (let b = 0; b < E.W; ++b) {
            t.K0[b] = E.FLo * Math.pow(hi / E.FLo, b / E.W) * E.N / t.Rate;
            t.K1[b] = E.FLo * Math.pow(hi / E.FLo, (b + 1) / E.W) * E.N / t.Rate;
        }
        t.Range = {};
        for (const [k, [f0, f1]] of Object.entries(E.Bands))
            t.Range[k] = [t.BandOf(f0), Math.max(t.BandOf(f0) + 1, t.BandOf(f1))];
        t.Steps = new Map();
        t.Tempos = new Map();
        t.Raw = new Float32Array(E.W * 32 * 3);
        t.Sm = new Float32Array(E.W * 32 * 3);
        t.SmStep = null;
        t.Mag = new Float32Array(E.Bins);
        //  The FFT of a decoded sound (Blackman window, radix 2; like the browser's AnalyserNode and the editor's EwAudio)
        const N = E.N;
        t.Win = new Float32Array(N);
        t.Rev = new Uint16Array(N);
        t.Cos = new Float64Array(N / 2);
        t.Sin = new Float64Array(N / 2);
        t.Re = new Float64Array(N);
        t.Im = new Float64Array(N);
        for (let i = 0; i < N; ++i) {
            t.Win[i] = 0.42 - 0.5 * Math.cos(2 * Math.PI * i / N) + 0.08 * Math.cos(4 * Math.PI * i / N);
            let x = i, y = 0;
            for (let b = 0; b < 11; ++b) {
                y = (y << 1) | (x & 1);
                x >>= 1;
            }
            t.Rev[i] = y;
        }
        for (let i = 0; i < N / 2; ++i) {
            t.Cos[i] = Math.cos(-2 * Math.PI * i / N);
            t.Sin[i] = Math.sin(-2 * Math.PI * i / N);
        }
    }

    /** The log band of a frequency (clamped) */
    BandOf(f) {
        const E = MediaAudio2Engine;
        return Math.max(0, Math.min(E.W, Math.round(E.W * Math.log(f / E.FLo) / Math.log(this.Hi / E.FLo))));
    }

    /** The magnitudes (linear, / N) of the first out.length bins of the samples x for the N samples ending before sample end (0 outside) */
    Spectrum(x, end, out) {
        const t = this, n = MediaAudio2Engine.N, re = t.Re, im = t.Im, w = t.Win, rev = t.Rev;
        const start = end - n;
        for (let i = 0; i < n; ++i) {
            const k = start + i;
            re[rev[i]] = k >= 0 && k < x.length ? x[k] * w[i] : 0;
            im[rev[i]] = 0;
        }
        for (let size = 2; size <= n; size <<= 1) {
            const half = size >> 1, step = n / size;
            for (let i = 0; i < n; i += size) {
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
        }
        for (let i = 0; i < out.length; ++i)
            out[i] = Math.sqrt(re[i] * re[i] + im[i] * im[i]) / n;
    }

    /** dB → 0..1 like ShaderToy (power: 10 log10) */
    static Map(p) {
        if (!(p > 0))
            return 0;
        const v = (10 * Math.log10(p) + 100) / 70;
        return v > 0 ? (v < 1 ? v : 1) : 0;
    }

    /** The power of the log bands from the power spectrum (narrow bands: interpolated at the center, wide: the bins' average) */
    Bands(pw, out) {
        const t = this, E = MediaAudio2Engine, nb = E.Bins;
        for (let b = 0; b < E.W; ++b) {
            const k0 = t.K0[b], k1 = t.K1[b];
            if (k1 - k0 < 1) {
                const c = Math.min(nb - 1.001, (k0 + k1) * 0.5), i = Math.floor(c), f = c - i;
                out[b] = pw[i] * (1 - f) + pw[i + 1] * f;
            }
            else {
                let s = 0, w = 0;
                for (let k = Math.max(0, Math.floor(k0 - 0.5)); k <= Math.min(nb - 1, Math.ceil(k1 + 0.5)); ++k) {
                    const o = Math.min(k + 0.5, k1) - Math.max(k - 0.5, k0);
                    if (o > 0) {
                        s += pw[k] * o;
                        w += o;
                    }
                }
                out[b] = w > 0 ? s / w : 0;
            }
        }
    }

    /** The analysis of step n (cached): per channel the band powers P, mapped values V, the 512 wave samples, rms, peak; energies */
    Step(n, monoOnly) {
        const t = this, E = MediaAudio2Engine;
        let s = t.Steps.get(n);
        if (s && (s.Full || monoOnly))
            return s;
        const chans = monoOnly ? 1 : 3;
        s = s || { P: [], V: [], Wave: [], Rms: [], Peak: [], En: [] };
        const d = t.Src.Live ? t.Src.Analyze2() : null;
        for (let c = s.P.length; c < chans; ++c) {
            const pw = new Float64Array(E.Bins);
            const wave = new Float32Array(E.W);
            let rms = 0, peak = 0;
            if (d) {
                for (let k = 0; k < E.Bins; ++k)
                    pw[k] = Math.pow(10, d.Db[c][k] / 10);
                const x = d.Time[c];
                for (let i = 0; i < E.N; ++i) {
                    rms += x[i] * x[i];
                    peak = Math.max(peak, Math.abs(x[i]));
                }
                wave.set(x.subarray(E.N - E.W));
            }
            else {
                const src = t.Src, end = Math.round(n / E.Steps * t.Rate), x = src.Ch[c];
                t.Spectrum(x, end, t.Mag);
                for (let k = 0; k < E.Bins; ++k)
                    pw[k] = t.Mag[k] * t.Mag[k];
                for (let i = 0; i < E.N; ++i) {
                    const j = end - E.N + i;
                    const v = j >= 0 && j < x.length ? x[j] : 0;
                    rms += v * v;
                    peak = Math.max(peak, Math.abs(v));
                    if (i >= E.N - E.W)
                        wave[i - (E.N - E.W)] = v;
                }
            }
            const P = new Float64Array(E.W);
            t.Bands(pw, P);
            const V = new Float32Array(E.W);
            for (let b = 0; b < E.W; ++b)
                V[b] = MediaAudio2Engine.Map(P[b]);
            const en = {};
            for (const [k, [b0, b1]] of Object.entries(t.Range)) {
                let sum = 0;
                for (let b = b0; b < b1; ++b)
                    sum += P[b];
                en[k] = MediaAudio2Engine.Map(sum / (b1 - b0));
            }
            s.P[c] = P;
            s.V[c] = V;
            s.Wave[c] = wave;
            s.Rms[c] = Math.sqrt(rms / E.N);
            s.Peak[c] = Math.min(1, peak);
            s.En[c] = en;
        }
        s.Full = chans === 3;
        t.Steps.set(n, s);
        //  (the onset history of the tempo and the warm up stay cached; older steps go)
        if (t.Steps.size > E.Hist + 2 * E.Warm + 16)
            t.Steps.delete(t.Steps.keys().next().value);
        return s;
    }

    /** The spectral flux of channel c at step n: the mean rise of the mapped bands since the step before */
    Onset(n, c) {
        const E = MediaAudio2Engine;
        const a = this.Step(n, c === 0).V[c], b = this.Step(n - 1, c === 0).V[c];
        let s = 0;
        for (let i = 0; i < E.W; ++i)
            s += Math.max(0, a[i] - b[i]);
        return s / E.W;
    }

    /** The rise of an energy of channel c at step n */
    Rise(n, c, k) {
        return Math.max(0, this.Step(n).En[c][k] - this.Step(n - 1).En[c][k]);
    }

    /** A pulse of channel c (an energy's rises, decaying), from Warm steps back */
    Pulse(n, c, k) {
        const E = MediaAudio2Engine;
        let p = 0;
        for (let i = n - E.Warm; i <= n; ++i)
            p = Math.max(Math.min(1, this.Rise(i, c, k) * 6), p * E.PulseDecay);
        return p;
    }

    /** The tempo at step n (estimated every TempoEvery steps from the mono onsets of the last Hist steps): { Bpm, Period (steps), Conf } */
    Tempo(n) {
        const t = this, E = MediaAudio2Engine;
        const m = n - (((n % E.TempoEvery) + E.TempoEvery) % E.TempoEvery);
        let r = t.Tempos.get(m);
        if (r)
            return r;
        const h = E.Hist, o = new Float64Array(h);
        let mean = 0;
        for (let i = 0; i < h; ++i)
            mean += o[i] = t.Onset(m - h + 1 + i, 0);
        mean /= h;
        for (let i = 0; i < h; ++i)
            o[i] -= mean;
        let e0 = 0;
        for (let i = 0; i < h; ++i)
            e0 += o[i] * o[i];
        //  Autocorrelation of the onsets up to 4 x the slowest period (lag 240 steps)
        const ac = new Float64Array(E.MaxLag + 2);
        for (let L = 1; L <= E.MaxLag + 1; ++L) {
            let s = 0;
            for (let i = L; i < h; ++i)
                s += o[i] * o[i - L];
            ac[L] = s;
        }
        //  The period 60 .. 200 BPM (lags 60 .. 18 steps): the autocorrelation at 1, 2, 3, 4 x the lag (a beat repeats at its multiples, a half /
        //  double / 3:2 tempo doesn't at all of them), weighted towards 120 BPM (one octave wide)
        let best = -1, bestW = 0;
        for (let L = 18; L <= 60; ++L) {
            let s = 0, cnt = 0;
            for (let k = 1; k <= 4 && k * L <= E.MaxLag; ++k, ++cnt)
                s += ac[k * L];
            const bpm = 60 * E.Steps / L, oct = Math.log2(bpm / 120);
            const w = s / cnt * Math.exp(-0.5 * oct * oct);
            if (best < 0 || w > bestW) {
                best = L;
                bestW = w;
            }
        }
        let period = 30, bpm = 0, conf = 0;
        if (e0 > 1e-12 && ac[best] > 0) {
            //  The peak between the lags (parabola)
            const a = ac[best - 1], b = ac[best], c = ac[best + 1], den = a - 2 * b + c;
            const off = den < 0 ? Math.max(-0.5, Math.min(0.5, 0.5 * (a - c) / den)) : 0;
            period = best + off;
            bpm = 60 * E.Steps / period;
            conf = Math.max(0, Math.min(1, b / e0));
        }
        r = { Bpm: bpm, Period: period, Conf: conf };
        t.Tempos.set(m, r);
        if (t.Tempos.size > 64)
            t.Tempos.delete(t.Tempos.keys().next().value);
        return r;
    }

    /** The beat at step n: { Phase (0 on a beat .. 1), Since (steps since the beat) } from the onsets combed with the tempo's period */
    Beat(n, tempo) {
        const t = this, E = MediaAudio2Engine, T = tempo.Period;
        const beats = Math.floor(E.Hist / T);
        let best = 0, bestS = -1;
        for (let f = 0; f < Math.ceil(T); ++f) {
            let s = 0;
            for (let j = 0; j < beats; ++j)
                s += t.Onset(n - f - Math.round(j * T), 0);
            if (s > bestS) {
                bestS = s;
                best = f;
            }
        }
        return { Phase: Math.min(1, best / T), Since: best };
    }

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
        const tempo = t.Tempo(n);
        const beat = t.Beat(n, tempo);
        for (let c = 0; c < 3; ++c) {
            //  Spectrum: level 0 the bands, level l the power average of 2^l bands
            let P = s.P[c];
            for (let l = 0; l < 10; ++l) {
                const cnt = W >> l, span = 1 << l, o = l * W;
                if (l > 0) {
                    const q = new Float64Array(cnt);
                    for (let i = 0; i < cnt; ++i)
                        q[i] = (P[2 * i] + P[2 * i + 1]) * 0.5;
                    P = q;
                }
                for (let i = 0; i < cnt; ++i) {
                    const v = MediaAudio2Engine.Map(P[i]);
                    for (let x = 0; x < span; ++x)
                        out[(o + i * span + x) * 3 + c] = v;
                }
            }
            //  Waveform: level l = the sample with the largest amplitude of each 2^l samples (its sign kept)
            const w = s.Wave[c];
            for (let l = 0; l < 10; ++l) {
                const span = 1 << l, o = (16 + l) * W;
                for (let i = 0; i < W; i += span) {
                    let m = 0;
                    for (let x = 0; x < span; ++x)
                        if (Math.abs(w[i + x]) > Math.abs(m))
                            m = w[i + x];
                    const v = 0.5 + 0.5 * m;
                    const vv = v > 0 ? (v < 1 ? v : 1) : 0;
                    for (let x = 0; x < span; ++x)
                        out[(o + i + x) * 3 + c] = vv;
                }
            }
            const en = s.En[c];
            put(10, 0, 0.5, c, en.Bass);
            put(10, 0.5, 1, c, Math.min(1, tempo.Bpm / 255));
            put(11, 0, 0.5, c, beat.Phase);
            put(11, 0.5, 1, c, Math.pow(E.PulseDecay, beat.Since));
            const lv = s.Rms[c] > 0 ? (20 * Math.log10(s.Rms[c]) + 60) / 60 : 0;
            put(12, 0, 0.25, c, Math.max(0, Math.min(1, lv)));
            put(12, 0.25, 0.5, c, s.Peak[c]);
            let sp = 0, sw = 0;
            for (let b = 0; b < W; ++b) {
                sp += s.P[c][b] * b;
                sw += s.P[c][b];
            }
            put(12, 0.5, 0.75, c, sw > 0 ? sp / sw / (W - 1) : 0);
            put(12, 0.75, 1, c, Math.min(1, t.Onset(n, c) * 10));
            put(13, 0, 0.25, c, t.Pulse(n, c, "Kick"));
            put(13, 0.25, 0.5, c, t.Pulse(n, c, "Snare"));
            put(13, 0.5, 0.75, c, t.Pulse(n, c, "HiHat"));
            put(13, 0.75, 1, c, en.Mid);
            put(14, 0, 0.25, c, en.Kick);
            put(14, 0.25, 0.5, c, en.Snare);
            put(14, 0.5, 0.75, c, en.HiHat);
            put(14, 0.75, 1, c, tempo.Conf);
            put(26, 0, 1, c, en.HiHat);
        }
    }

    /** The texture at step n into out (512 x 64 x 3): the raw rows, the smoothed rows (incremental while playing on, else from Warm steps back) */
    Fill(out, n) {
        const t = this, E = MediaAudio2Engine, half = E.W * 32 * 3;
        if (t.SmStep !== null && n > t.SmStep && n - t.SmStep <= 4) {
            for (let k = t.SmStep + 1; k <= n; ++k) {
                t.RawRows(k, t.Raw);
                for (let i = 0; i < half; ++i)
                    t.Sm[i] += E.Smooth * (t.Raw[i] - t.Sm[i]);
            }
        }
        else if (t.SmStep !== n) {
            t.Sm.fill(0);
            for (let k = n - E.Warm; k <= n; ++k) {
                t.RawRows(k, t.Raw);
                for (let i = 0; i < half; ++i)
                    t.Sm[i] += E.Smooth * (t.Raw[i] - t.Sm[i]);
            }
        }
        //  (the same step: Raw / Sm hold it already)
        t.SmStep = n;
        out.set(t.Raw, 0);
        out.set(t.Sm, half);
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
